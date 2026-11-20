using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace GitScope.Core;

public sealed class GitObjectReader(GitProcessRunner runner)
{
    public async Task<string> ResolveTreeAsync(RepositoryInfo repository, string oid, CancellationToken cancellationToken = default)
    {
        ValidateOid(oid);
        var result = await runner.RequireAsync(repository.RootPath, ["rev-parse", "--verify", "--end-of-options", oid + "^{tree}"], cancellationToken).ConfigureAwait(false);
        var tree = result.Text.Trim(); ValidateOid(tree); return tree;
    }

    public async Task<IReadOnlyList<GitTreeEntry>> ReadTreeAsync(RepositoryInfo repository, string oid, CancellationToken cancellationToken = default)
    {
        ValidateOid(oid);
        var result = await runner.RequireAsync(repository.RootPath, ["ls-tree", "-r", "-l", "-z", "--full-tree", oid], cancellationToken).ConfigureAwait(false);
        var entries = new List<GitTreeEntry>();
        // Strict UTF-8 avoids silently mapping two different Git byte names to one Windows path.
        string text;
        try { text = new UTF8Encoding(false, true).GetString(result.Output); }
        catch (DecoderFallbackException e) { throw new ScopeException(ErrorCode.UnsupportedEntry, "Git 树含无法映射到 Windows 的非 UTF-8 文件名。", e); }
        foreach (var record in text.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = record.IndexOf('\t');
            if (tab <= 0) throw new ScopeException(ErrorCode.GitCommandFailed, "Git 树记录格式无效。");
            var header = record[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (header.Length != 4) throw new ScopeException(ErrorCode.GitCommandFailed, "Git 树记录缺少字段。");
            ValidateOid(header[2]);
            var size = header[3] == "-" ? -1 : long.Parse(header[3], CultureInfo.InvariantCulture);
            entries.Add(new(record[(tab + 1)..], header[0], header[1], header[2], size));
        }
        return entries;
    }

    public GitBlobBatch OpenBatch(RepositoryInfo repository) => new(runner, repository.RootPath);

    public async Task<byte[]> ReadBlobAsync(RepositoryInfo repository, string oid, CancellationToken cancellationToken = default)
    {
        await using var batch = OpenBatch(repository);
        using var memory = new MemoryStream();
        await batch.CopyBlobAsync(oid, memory, cancellationToken).ConfigureAwait(false);
        return memory.ToArray();
    }

    public static void ValidateOid(string value)
    {
        if ((value.Length != 40 && value.Length != 64) || value.Any(c => !Uri.IsHexDigit(c)))
            throw new ScopeException(ErrorCode.MissingRevision, "必须使用完整的 SHA-1 或 SHA-256 对象 ID。");
    }
}

public sealed class GitBlobBatch : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Task<string> _stderr;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;
    internal GitBlobBatch(GitProcessRunner runner, string repositoryPath)
    {
        _process = runner.Start(repositoryPath, ["cat-file", "--batch"], true);
        _stderr = _process.StandardError.ReadToEndAsync(CancellationToken.None);
    }

    public async Task<long> CopyBlobAsync(string oid, Stream destination, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        GitObjectReader.ValidateOid(oid);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        using var registration = cancellationToken.Register(() => GitProcessRunner.Kill(_process));
        try
        {
            await _process.StandardInput.WriteAsync((oid + "\n").AsMemory(), cancellationToken).ConfigureAwait(false);
            await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            var header = await ReadHeaderAsync(cancellationToken).ConfigureAwait(false);
            var fields = header.Split(' ');
            if (fields.Length == 2 && fields[1] == "missing")
                throw new ScopeException(ErrorCode.MissingObject, $"本地缺少 Git 对象 {oid}；未尝试联网。");
            if (fields.Length != 3 || fields[0] != oid || fields[1] != "blob" || !long.TryParse(fields[2], out var length) || length < 0)
                throw new ScopeException(ErrorCode.MissingObject, "Git blob 响应无效或对象类型不受支持。");
            var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
            try
            {
                long remaining = length;
                while (remaining > 0)
                {
                    int read = await _process.StandardOutput.BaseStream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(remaining, buffer.Length)), cancellationToken).ConfigureAwait(false);
                    if (read == 0) throw new ScopeException(ErrorCode.MissingObject, "Git blob 数据被截断。");
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    remaining -= read;
                }
                var terminator = buffer.AsMemory(0, 1);
                if (await _process.StandardOutput.BaseStream.ReadAsync(terminator, cancellationToken).ConfigureAwait(false) != 1 || buffer[0] != '\n')
                    throw new ScopeException(ErrorCode.MissingObject, "Git blob 缺少结束分隔符。");
                return length;
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }
        catch
        {
            GitProcessRunner.Kill(_process);
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        finally { _gate.Release(); }
    }

    private async Task<string> ReadHeaderAsync(CancellationToken token)
    {
        var bytes = new byte[512]; var one = new byte[1]; int position = 0;
        while (position < bytes.Length)
        {
            int read = await _process.StandardOutput.BaseStream.ReadAsync(one, token).ConfigureAwait(false);
            if (read == 0) throw new ScopeException(ErrorCode.MissingObject, "Git 对象读取意外结束。");
            if (one[0] == '\n') return Encoding.ASCII.GetString(bytes, 0, position);
            bytes[position++] = one[0];
        }
        throw new ScopeException(ErrorCode.MissingObject, "Git 对象响应头超长。");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _process.StandardInput.Close();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { await _process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { GitProcessRunner.Kill(_process); await _process.WaitForExitAsync().ConfigureAwait(false); }
            await _stderr.ConfigureAwait(false);
        }
        finally { _process.Dispose(); _gate.Dispose(); }
    }
}
