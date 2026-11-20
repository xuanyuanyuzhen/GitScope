using System.Globalization;
using System.Text;

namespace GitScope.Core;

public sealed class HistoryService(GitProcessRunner runner) : IHistoryService
{
    private readonly RepositoryService _repositories = new(runner);
    private readonly GitObjectReader _objects = new(runner);
    private static readonly HashSet<string> DependencyNames = new(StringComparer.Ordinal)
    {
        "package.json", "package-lock.json", "npm-shrinkwrap.json", "pnpm-lock.yaml", "yarn.lock", "pnpm-workspace.yaml"
    };
    private const long MaxTextPreviewBytes = 2 * 1024 * 1024;

    public async Task<HistorySnapshot> QueryAsync(RepositoryInfo repository, string tipOid, HistoryFilter filter,
        string requestId, CancellationToken cancellationToken = default)
    {
        var dates = DateSemantics.Parse(filter);
        if (!Enum.IsDefined(filter.Mode)) throw new ScopeException(ErrorCode.InvalidConfiguration, "历史范围设置无效。");
        RepositoryService.ValidateOid(tipOid, repository.ObjectFormat);
        await _repositories.EnsureSupportedAsync(repository, cancellationToken);
        var args = new List<string> { "log", "--no-color", "--no-decorate", "--no-show-signature", "--no-notes", "--encoding=UTF-8",
            "--format=%H%x00%P%x00%an%x00%at%x00%ct%x00%s%x00" };
        if (filter.Mode == HistoryMode.FirstParent) args.Add("--first-parent");
        args.Add(tipOid); args.Add("--");
        // Deliberately no --since, --until, --skip or --max-count: arbitrary commit clocks may go backwards.
        var result = await runner.RunAsync(repository.RootPath, args, cancellationToken);
        if (result.ExitCode != 0) throw new ScopeException(ErrorCode.MissingObject, "无法读取完整本地提交历史，所选提交或其祖先对象可能缺失。");
        var fields = result.Text.Split('\0');
        var commits = new List<CommitSummary>();
        for (var i = 0; i + 5 < fields.Length; i += 6)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var oid = fields[i].TrimStart('\r', '\n');
            RepositoryService.ValidateOid(oid, repository.ObjectFormat);
            if (!long.TryParse(fields[i + 3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var authorTime) ||
                !long.TryParse(fields[i + 4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var committerTime))
                throw new ScopeException(ErrorCode.GitCommandFailed, "Git 返回了无法解析的提交时间。");
            if (dates.StartInclusive is { } start && committerTime < start || dates.EndExclusive is { } end && committerTime >= end) continue;
            var parents = fields[i + 1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var parent in parents) RepositoryService.ValidateOid(parent, repository.ObjectFormat);
            commits.Add(new CommitSummary(oid, parents, fields[i + 5], fields[i + 2], authorTime, committerTime));
        }
        if ((fields.Length - 1) % 6 != 0 || fields[^1].Trim('\r', '\n').Length > 0)
            throw new ScopeException(ErrorCode.GitCommandFailed, "Git 提交列表被截断或格式不完整。");
        var ordered = commits.OrderByDescending(x => x.CommitterTime).ThenBy(x => x.Oid, StringComparer.Ordinal).ToArray();
        string[] warnings = repository.IsShallow ? [ordered.Length == 0 ? "浅历史：本机已知历史无匹配，不代表远端没有提交。" : "浅历史：仅包含本机已有提交，未自动加深历史。"] : [];
        return new HistorySnapshot(requestId, tipOid, ordered, repository.IsShallow ? HistoryCoverage.Shallow : HistoryCoverage.LocalComplete, warnings);
    }

    public async Task<CommitPreview> PreviewAsync(RepositoryInfo repository, string commitOid, CancellationToken cancellationToken = default)
    {
        await _repositories.EnsureSupportedAsync(repository, cancellationToken);
        RepositoryService.ValidateOid(commitOid, repository.ObjectFormat);
        var parent = await FirstParentAsync(repository, commitOid, cancellationToken);
        var after = (await _objects.ReadTreeAsync(repository, commitOid, cancellationToken)).ToDictionary(x => x.Path, StringComparer.Ordinal);
        var before = parent is null ? new Dictionary<string, GitTreeEntry>(StringComparer.Ordinal) :
            (await _objects.ReadTreeAsync(repository, parent, cancellationToken)).ToDictionary(x => x.Path, StringComparer.Ordinal);
        var diffArgs = DiffArguments(commitOid, parent, false);
        var stats = await runner.RunAsync(repository.RootPath, diffArgs, cancellationToken);
        if (stats.ExitCode != 0) throw new ScopeException(ErrorCode.MissingObject, "无法读取比较基线；浅历史边界或缺失的父提交无法提供完整预览。");
        var binary = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in stats.Text.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var one = record.IndexOf('\t'); var two = one < 0 ? -1 : record.IndexOf('\t', one + 1);
            if (one < 0 || two < 0) throw new ScopeException(ErrorCode.GitCommandFailed, "Git 文件预览返回了不完整记录。");
            if (record[..one] == "-" || record[(one + 1)..two] == "-") binary.Add(record[(two + 1)..]);
        }
        var files = new List<ChangedFile>();
        foreach (var path in before.Keys.Union(after.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            before.TryGetValue(path, out var old); after.TryGetValue(path, out var current);
            if (old?.ObjectId == current?.ObjectId && old?.Mode == current?.Mode) continue;
            files.Add(new ChangedFile(path, old is null ? "A" : current is null ? "D" : "M", binary.Contains(path), old?.Size, current?.Size));
        }
        return new CommitPreview(commitOid, parent is null ? "根提交 · 相对空树" : $"相对第一父提交 {parent}", files);
    }

    public async Task<string> DiffAsync(RepositoryInfo repository, string commitOid, string path, CancellationToken cancellationToken = default)
    {
        await _repositories.EnsureSupportedAsync(repository, cancellationToken);
        RepositoryService.ValidateOid(commitOid, repository.ObjectFormat);
        if (string.IsNullOrEmpty(path) || path.Contains('\0')) throw new ScopeException(ErrorCode.GitCommandFailed, "请选择有效的变更文件。");
        var parent = await FirstParentAsync(repository, commitOid, cancellationToken);
        var after = (await _objects.ReadTreeAsync(repository, commitOid, cancellationToken)).FirstOrDefault(x => x.Path == path);
        var before = parent is null ? null : (await _objects.ReadTreeAsync(repository, parent, cancellationToken)).FirstOrDefault(x => x.Path == path);
        if (before is null && after is null) throw new ScopeException(ErrorCode.GitCommandFailed, "所选文件不属于当前提交或其第一父基线。");
        if (before is not null && before.Type != "blob" || after is not null && after.Type != "blob")
            return "此项为子模块或特殊 Git 条目，暂不提供文本差异。";
        if ((before?.Size ?? 0) > MaxTextPreviewBytes || (after?.Size ?? 0) > MaxTextPreviewBytes)
            return $"文件较大，暂不加载文本差异（单侧上限 2 MiB）。旧 {before?.Size ?? 0:N0} 字节 → 新 {after?.Size ?? 0:N0} 字节。";
        foreach (var item in new[] { before, after }.Where(x => x is not null).DistinctBy(x => x!.ObjectId))
        {
            var bytes = await _objects.ReadBlobAsync(repository, item!.ObjectId, cancellationToken);
            if (bytes.AsSpan().Contains((byte)0)) return $"二进制文件：旧 {before?.Size ?? 0:N0} 字节 → 新 {after?.Size ?? 0:N0} 字节。";
            try { _ = new UTF8Encoding(false, true).GetCharCount(bytes); }
            catch (DecoderFallbackException) { return "该文件不是有效 UTF-8 文本，暂不作为文本差异显示。"; }
        }
        var args = DiffArguments(commitOid, parent, true);
        args.Add(":(literal)" + path);
        var result = await runner.RunAsync(repository.RootPath, args, cancellationToken);
        if (result.ExitCode != 0) throw new ScopeException(ErrorCode.GitCommandFailed, "无法读取此文件的文本差异。");
        return result.Text.Length == 0 ? "此文件相对比较基线没有内容差异。" : result.Text;
    }

    public async Task<DependencyNotice> DependencyNoticeAsync(RepositoryInfo repository, string? previousOid, string commitOid, CancellationToken cancellationToken = default)
    {
        await _repositories.EnsureSupportedAsync(repository, cancellationToken);
        RepositoryService.ValidateOid(commitOid, repository.ObjectFormat);
        if (string.IsNullOrWhiteSpace(previousOid))
            return new DependencyNotice(DependencyState.FirstExport, [], "首次导出，没有上次版本可比较；请自行检查并同步依赖。");
        try
        {
            RepositoryService.ValidateOid(previousOid, repository.ObjectFormat);
            var before = Dependencies(await _objects.ReadTreeAsync(repository, previousOid, cancellationToken));
            var after = Dependencies(await _objects.ReadTreeAsync(repository, commitOid, cancellationToken));
            var changed = before.Keys.Union(after.Keys, StringComparer.Ordinal)
                .Where(path => before.GetValueOrDefault(path) != after.GetValueOrDefault(path)).Order(StringComparer.Ordinal).ToArray();
            return changed.Length == 0
                ? new DependencyNotice(DependencyState.Unchanged, [], "声明与锁文件未变化；仍请自行确认本地已安装依赖是否兼容。")
                : new DependencyNotice(DependencyState.Changed, changed, "声明或锁文件有变化（含新增/删除），请自行同步依赖。保留的依赖不会自动安装或更新。");
        }
        catch (ScopeException e) when (e.Code is ErrorCode.MissingObject or ErrorCode.MissingRevision or ErrorCode.GitCommandFailed)
        {
            return new DependencyNotice(DependencyState.Unknown, [], "无法完整读取上次或本次版本，依赖变化未知；请自行检查并同步依赖。");
        }
    }

    private static Dictionary<string, string> Dependencies(IReadOnlyList<GitTreeEntry> entries) => entries
        .Where(x => DependencyNames.Contains(x.Path[(x.Path.LastIndexOf('/') + 1)..]))
        .ToDictionary(x => x.Path, x => x.ObjectId + ":" + x.Mode, StringComparer.Ordinal);

    private async Task<string?> FirstParentAsync(RepositoryInfo repository, string oid, CancellationToken token)
    {
        var commit = await runner.RunAsync(repository.RootPath, ["cat-file", "commit", oid], token);
        if (commit.ExitCode != 0) throw new ScopeException(ErrorCode.MissingObject, "提交对象缺失或不是提交，无法预览。");
        foreach (var line in commit.Text.Split('\n'))
        {
            if (line.Length == 0) break;
            if (!line.StartsWith("parent ", StringComparison.Ordinal)) continue;
            var parent = line[7..]; RepositoryService.ValidateOid(parent, repository.ObjectFormat); return parent;
        }
        return null;
    }

    private static List<string> DiffArguments(string commitOid, string? parent, bool patch)
    {
        var args = new List<string> { "diff-tree", "--no-commit-id", "-r", "--no-renames", "--no-ext-diff", "--no-textconv", "--no-color" };
        if (patch) args.Add("--patch"); else { args.Add("--numstat"); args.Add("-z"); }
        if (parent is null) args.Add("--root"); else args.Add(parent);
        args.Add(commitOid); args.Add("--"); return args;
    }
}
