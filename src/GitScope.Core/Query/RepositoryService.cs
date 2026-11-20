using System.Security.Cryptography;
using System.Text;

namespace GitScope.Core;

public sealed class RepositoryService(GitProcessRunner runner) : IRepositoryService
{
    public async Task<RepositoryInfo> InspectAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            throw new ScopeException(ErrorCode.InvalidRepository, "仓库目录不存在，请重新选择。");
        string supplied;
        try { supplied = Path.GetFullPath(path); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        { throw new ScopeException(ErrorCode.InvalidRepository, "仓库路径无效。", e); }
        var rootResult = await runner.RunAsync(supplied, ["rev-parse", "--path-format=absolute", "--show-toplevel"], cancellationToken);
        if (rootResult.ExitCode != 0)
            throw new ScopeException(ErrorCode.InvalidRepository, "所选目录不是可读取的 Git 工作区（裸仓库不能作为代码工作区）。");
        var root = Normalize(rootResult.Text.TrimEnd('\r', '\n'));
        var git = Normalize((await runner.RequireAsync(root, ["rev-parse", "--absolute-git-dir"], cancellationToken)).Text.TrimEnd('\r', '\n'));
        var common = Normalize((await runner.RequireAsync(root, ["rev-parse", "--path-format=absolute", "--git-common-dir"], cancellationToken)).Text.TrimEnd('\r', '\n'));
        if (!string.Equals(git, common, StringComparison.OrdinalIgnoreCase))
        {
            var backlink = Path.Combine(git, "gitdir");
            if (!File.Exists(backlink)) throw new ScopeException(ErrorCode.InvalidRepository, "关联工作区缺少 Git 反向登记。");
            var recorded = Normalize(Path.GetFullPath(File.ReadAllText(backlink).TrimEnd('\r', '\n'), git));
            if (!string.Equals(recorded, Normalize(Path.Combine(root, ".git")), StringComparison.OrdinalIgnoreCase))
                throw new ScopeException(ErrorCode.InvalidRepository, "关联工作区的 Git 登记与所选目录不一致，请先修复工作区登记。");
        }
        await RejectPartialAsync(root, common, cancellationToken);
        var shallow = (await runner.RequireAsync(root, ["rev-parse", "--is-shallow-repository"], cancellationToken)).Text.Trim() == "true";
        var objectFormat = (await runner.RequireAsync(root, ["rev-parse", "--show-object-format"], cancellationToken)).Text.Trim();
        if (objectFormat is not ("sha1" or "sha256"))
            throw new ScopeException(ErrorCode.UnsupportedRepository, $"暂不支持对象格式 {objectFormat}。");
        var protectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root, git, common };
        var worktrees = await runner.RequireAsync(root, ["worktree", "list", "--porcelain", "-z"], cancellationToken);
        foreach (var item in worktrees.Text.Split('\0'))
            if (item.StartsWith("worktree ", StringComparison.Ordinal)) protectedPaths.Add(Normalize(item[9..]));
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToUpperInvariant() + "\0" + common.ToUpperInvariant()))).ToLowerInvariant();
        return new RepositoryInfo(id, root, git, common, shallow, objectFormat, protectedPaths.ToArray());
    }

    public async Task<IReadOnlyList<RevisionInfo>> GetReferencesAsync(RepositoryInfo repository, CancellationToken cancellationToken = default)
    {
        await EnsureSupportedAsync(repository, cancellationToken);
        var result = await runner.RequireAsync(repository.RootPath,
            ["for-each-ref", "--format=%(refname)%00%(objectname)%00%(objecttype)%00", "refs/heads", "refs/remotes", "refs/tags"], cancellationToken);
        var fields = result.Text.Split('\0');
        var refs = new List<RevisionInfo>();
        for (var i = 0; i + 2 < fields.Length; i += 3)
        {
            var name = fields[i].TrimStart('\r', '\n');
            if (name.Length == 0) continue;
            var oid = fields[i + 1];
            if (fields[i + 2] != "commit")
            {
                var resolved = await runner.RunAsync(repository.RootPath, ["rev-parse", "--verify", "--end-of-options", name + "^{commit}"], cancellationToken);
                if (resolved.ExitCode != 0) continue; // A tag to a blob/tree is not a selectable revision.
                oid = resolved.Text.Trim();
            }
            ValidateOid(oid, repository.ObjectFormat);
            refs.Add(new RevisionInfo(name, oid, name.StartsWith("refs/heads/", StringComparison.Ordinal) ? "local" : name.StartsWith("refs/remotes/", StringComparison.Ordinal) ? "remote" : "tag"));
        }
        var head = await runner.RunAsync(repository.RootPath, ["rev-parse", "--verify", "--end-of-options", "HEAD^{commit}"], cancellationToken);
        if (head.ExitCode == 0)
        {
            var oid = head.Text.Trim(); ValidateOid(oid, repository.ObjectFormat);
            refs.Insert(0, new RevisionInfo("HEAD", oid, "head"));
        }
        return refs;
    }

    public async Task<string> ResolveCommitAsync(RepositoryInfo repository, string revision, CancellationToken cancellationToken = default)
    {
        await EnsureSupportedAsync(repository, cancellationToken);
        if (string.IsNullOrWhiteSpace(revision) || revision.Contains('\0') ||
            !(revision == "HEAD" || revision.StartsWith("refs/", StringComparison.Ordinal) || IsOid(revision, repository.ObjectFormat)))
            throw new ScopeException(ErrorCode.MissingRevision, "请选择完整引用名或完整提交 ID。");
        var result = await runner.RunAsync(repository.RootPath, ["rev-parse", "--verify", "--end-of-options", revision + "^{commit}"], cancellationToken);
        if (result.ExitCode != 0) throw new ScopeException(ErrorCode.MissingRevision, "所选引用不存在或仓库尚无提交。");
        var oid = result.Text.Trim(); ValidateOid(oid, repository.ObjectFormat); return oid;
    }

    public Task EnsureSupportedAsync(RepositoryInfo repository, CancellationToken cancellationToken = default) =>
        RejectPartialAsync(repository.RootPath, repository.CommonDirectory, cancellationToken);

    private async Task RejectPartialAsync(string root, string common, CancellationToken token)
    {
        var config = await runner.RunAsync(root, ["config", "--null", "--get-regexp", "^(extensions\\.partialclone|remote\\..*\\.(promisor|partialclonefilter))$"], token);
        if (config.ExitCode is not (0 or 1))
            throw new ScopeException(ErrorCode.InvalidRepository, "无法读取 Git 配置，已停止对象读取。");
        foreach (var entry in config.Text.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = entry.IndexOf('\n');
            var key = separator < 0 ? entry : entry[..separator];
            var value = separator < 0 ? "" : entry[(separator + 1)..].Trim();
            bool disabled = value.Equals("false", StringComparison.OrdinalIgnoreCase) || value.Equals("no", StringComparison.OrdinalIgnoreCase) || value.Equals("off", StringComparison.OrdinalIgnoreCase) || value == "0";
            if (!key.EndsWith(".promisor", StringComparison.OrdinalIgnoreCase) || !disabled)
                throw Partial();
        }
        var checkedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>(); pending.Push(Path.Combine(common, "objects"));
        while (pending.TryPop(out var objectDirectory))
        {
            token.ThrowIfCancellationRequested();
            objectDirectory = Normalize(objectDirectory);
            if (!checkedDirectories.Add(objectDirectory)) continue;
            var pack = Path.Combine(objectDirectory, "pack");
            if (Directory.Exists(pack) && Directory.EnumerateFiles(pack, "*.promisor", SearchOption.TopDirectoryOnly).Any()) throw Partial();
            var alternates = Path.Combine(objectDirectory, "info", "alternates");
            if (File.Exists(alternates))
                foreach (var line in File.ReadLines(alternates).Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    // Quoted alternate paths require Git's C-style decoding. Refuse instead of under-checking them.
                    if (line.StartsWith('"')) throw new ScopeException(ErrorCode.UnsupportedRepository, "暂不支持带转义引号的 Git 对象替代目录。");
                    pending.Push(Path.GetFullPath(line, objectDirectory));
                }
        }
    }

    private static ScopeException Partial() => new(ErrorCode.UnsupportedRepository,
        "检测到 partial/promisor 仓库，首版暂不读取其对象，以避免隐式联网。请使用本地对象完整的仓库。");
    internal static bool IsOid(string oid, string format) => oid.Length == (format == "sha256" ? 64 : 40) && oid.All(char.IsAsciiHexDigit);
    internal static void ValidateOid(string oid, string format)
    {
        if (!IsOid(oid, format)) throw new ScopeException(ErrorCode.MissingRevision, "需要与仓库对象格式匹配的完整提交 ID。");
    }
    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
