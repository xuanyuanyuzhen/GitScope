using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GitScope.Core;

internal sealed partial class SnapshotExporter(GitObjectReader objects)
{
    public static void ValidateEntries(IReadOnlyList<GitTreeEntry> entries)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directorySpellings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            if (e.Type != "blob" || e.Mode is not ("100644" or "100755"))
                throw new ScopeException(ErrorCode.UnsupportedEntry, $"此版本含尚不支持的链接或子模块：{e.Path}");
            var normalized = FileSafety.Relative(e.Path, ErrorCode.UnsupportedEntry);
            if (normalized != e.Path || e.Size < 0 || !paths.Add(e.Path))
                throw new ScopeException(ErrorCode.UnsupportedEntry, $"版本包含重复、大小写冲突或不可映射的路径：{e.Path}");
            var segments = e.Path.Split('/');
            for (var i = 1; i < segments.Length; i++)
            {
                var directory = string.Join('/', segments.Take(i));
                if (directorySpellings.TryGetValue(directory, out var spelling) && spelling != directory)
                    throw new ScopeException(ErrorCode.UnsupportedEntry, $"版本目录的大小写发生冲突：{directory}");
                directorySpellings[directory] = directory;
            }
        }
        if (paths.Any(directorySpellings.ContainsKey))
            throw new ScopeException(ErrorCode.UnsupportedEntry, "版本中同一路径同时用作文件和目录。");
    }
    public async Task WriteAsync(ExportPlan plan, string stage, IProgress<OperationProgress>? progress, CancellationToken token)
    {
        ValidateEntries(plan.Entries);
        await using var batch = objects.OpenBatch(plan.Repository);
        int n = 0;
        foreach (var e in plan.Entries)
        {
            token.ThrowIfCancellationRequested();
            var path = FileSafety.Child(stage, e.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            FileSafety.NoReparseAncestors(path);
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var count = await batch.CopyBlobAsync(e.ObjectId, file, token);
                if (count != e.Size) throw new ScopeException(ErrorCode.ExportValidationFailed, $"对象长度不匹配：{e.Path}");
                await file.FlushAsync(token); file.Flush(true);
            }
            if (e.Size <= 8192 && IsLfsPointer(path))
                throw new ScopeException(ErrorCode.UnsupportedEntry, $"版本包含 Git LFS 指针，尚未支持完整 LFS 导出：{e.Path}");
            progress?.Report(new(plan.RequestId, "STAGING", $"正在准备 {e.Path}", ++n, plan.Entries.Count));
        }
    }
    private static bool IsLfsPointer(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal);
        return text.StartsWith("version https://git-lfs.github.com/spec/v1\n", StringComparison.Ordinal)
            && LfsOid().IsMatch(text) && LfsSize().IsMatch(text);
    }
    [GeneratedRegex(@"(?m)^oid sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant)] private static partial Regex LfsOid();
    [GeneratedRegex(@"(?m)^size [0-9]+$", RegexOptions.CultureInvariant)] private static partial Regex LfsSize();
    public static void Verify(ExportPlan plan, string root, bool includePreserved, CancellationToken token = default)
    {
        ValidateEntries(plan.Entries);
        var tracked = plan.Entries.ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase);
        var allowedRoots = includePreserved ? plan.Roots.Where(r => r.Exists).ToArray() : [];
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(string directory, string prefix)
        {
            using var stable = FileSafety.Open(directory, true);
            FileSafety.NoReparseAncestors(directory);
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                token.ThrowIfCancellationRequested();
                var relative = prefix + Path.GetFileName(path);
                if (allowedRoots.Any(r => relative.Equals(r.RelativePath, StringComparison.OrdinalIgnoreCase))) continue;
                var attrs = File.GetAttributes(path);
                if (attrs.HasFlag(FileAttributes.ReparsePoint)) throw new ScopeException(ErrorCode.ExportValidationFailed, $"新代码出现未声明链接：{relative}");
                if (attrs.HasFlag(FileAttributes.Directory))
                {
                    if (!tracked.Keys.Any(k => k.StartsWith(relative + "/", StringComparison.OrdinalIgnoreCase))
                        && !allowedRoots.Any(r => r.RelativePath.StartsWith(relative + "/", StringComparison.OrdinalIgnoreCase)))
                        throw new ScopeException(ErrorCode.ExportValidationFailed, $"新目录出现多余内容：{relative}");
                    Walk(path, relative + "/");
                }
                else
                {
                    if (!tracked.TryGetValue(relative, out var entry)) throw new ScopeException(ErrorCode.ExportValidationFailed, $"新目录出现多余文件：{relative}");
                    files.Add(relative);
                    VerifyBlob(path, entry, plan.Repository.ObjectFormat, token);
                }
            }
        }
        Walk(root, "");
        if (!files.SetEquals(tracked.Keys)) throw new ScopeException(ErrorCode.ExportValidationFailed, "新目录的文件集合不匹配选定 Git 树。");
        foreach (var r in allowedRoots)
        {
            var path = FileSafety.Child(root, r.RelativePath);
            if (FileSafety.IdentityOrNull(path) != r.Identity || FileSafety.Fingerprint(path, token) != r.Fingerprint)
                throw new ScopeException(ErrorCode.TargetChanged, $"保留项在移动期间发生变化：{r.RelativePath}");
        }
    }
    private static void VerifyBlob(string path, GitTreeEntry entry, string format, CancellationToken token)
    {
        using var stable = FileSafety.Open(path, true);
        FileSafety.NoReparseAncestors(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length != entry.Size) throw new ScopeException(ErrorCode.ExportValidationFailed, $"文件长度校验失败：{entry.Path}");
        var algorithm = format switch { "sha1" => HashAlgorithmName.SHA1, "sha256" => HashAlgorithmName.SHA256,
            _ => throw new ScopeException(ErrorCode.UnsupportedRepository, "尚未支持此仓库对象格式。") };
        using var hash = IncrementalHash.CreateHash(algorithm);
        hash.AppendData(Encoding.ASCII.GetBytes("blob " + entry.Size.ToString(CultureInfo.InvariantCulture) + "\0"));
        var buffer = new byte[65536]; int count;
        while ((count = stream.Read(buffer)) > 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, count); }
        if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(entry.ObjectId, StringComparison.OrdinalIgnoreCase))
            throw new ScopeException(ErrorCode.ExportValidationFailed, $"Git 对象摘要校验失败：{entry.Path}");
    }
}
