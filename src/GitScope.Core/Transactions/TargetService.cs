using System.Text.Json;

namespace GitScope.Core;

public sealed class TargetService : ITargetService
{
    private readonly TransactionStore _store;
    private readonly GitProcessRunner _runner;
    private readonly GitObjectReader _objects;
    private readonly Action<string>? _fault;
    public TargetService(string stateDirectory, GitProcessRunner runner, Action<string>? faultInjector = null)
    { _store = new(stateDirectory); _runner = runner; _objects = new(runner); _fault = faultInjector; }

    public Task<IReadOnlyList<ManagedTarget>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<ManagedTarget>>(() => { cancellationToken.ThrowIfCancellationRequested(); return _store.Targets(); }, cancellationToken);

    public async Task<ManagedTarget> RegisterAsync(RepositoryInfo repository, string path, CancellationToken cancellationToken = default)
    {
        var repo = await RefreshRepository(repository, cancellationToken);
        return await Task.Run(() =>
        {
            var full = FileSafety.Canonical(path);
            CheckTargetPath(repo, full);
            using var gate = _store.Lock(full);
            var id = FileSafety.Key(full);
            if (File.Exists(_store.TargetFile(id)))
            {
                var existing = _store.Target(id);
                if (existing.RepositoryId != repo.Id) throw new ScopeException(ErrorCode.UnsafeTarget, "此输出目录已绑定另一个仓库，请选择独立目录。");
                _store.Claim(existing.Path, existing.Identity);
                RequireNoRecovery(id); RequireIdentity(existing); return existing;
            }
            cancellationToken.ThrowIfCancellationRequested();
            var parent = Path.GetDirectoryName(full)!;
            if (!Directory.Exists(parent)) throw new ScopeException(ErrorCode.UnsafeTarget, "请先选择一个已存在的父目录；GitScope 仅创建最后一级专用输出目录。");
            using var held = FileSafety.HoldParents(full);
            if (FileSafety.Exists(full) && (!Directory.Exists(full) || Directory.EnumerateFileSystemEntries(full).Any()))
                throw new ScopeException(ErrorCode.UnsafeTarget, "首次绑定只接受空目录或新目录，不会接管已有文件。");
            Directory.CreateDirectory(full);
            var target = new ManagedTarget(1, id, repo.Id, full, FileSafety.Identity(full), null, null, [], PreservationSettings.Empty);
            _store.Claim(full, target.Identity);
            _store.SaveTarget(target);
            return target;
        }, cancellationToken);
    }
    public async Task<ExportPlan> PlanAsync(RepositoryInfo repository, string commitOid, string targetId,
        IReadOnlyList<string> keepPaths, string requestId, CancellationToken cancellationToken = default)
    {
        var repo = await RefreshRepository(repository, cancellationToken);
        var resolved = await new RepositoryService(_runner).ResolveCommitAsync(repo, commitOid, cancellationToken);
        // The caller may supply a ref, but the plan itself always contains an immutable full OID.
        var tree = await _objects.ResolveTreeAsync(repo, resolved, cancellationToken);
        var entries = await _objects.ReadTreeAsync(repo, resolved, cancellationToken);
        SnapshotExporter.ValidateEntries(entries);
        return await Task.Run(() =>
        {
            var target = _store.Target(targetId);
            using var gate = _store.Lock(target.Path);
            target = _store.Target(targetId);
            CheckTargetPath(repo, target.Path);
            if (target.RepositoryId != repo.Id) throw new ScopeException(ErrorCode.UnsafeTarget, "输出目录不属于当前仓库。");
            RequireNoRecovery(targetId); RequireIdentity(target);
            var normalized = NormalizeKeeps(keepPaths);
            foreach (var path in normalized)
                if (target.LastTrackedPaths.Concat(entries.Select(e => e.Path)).Any(p => FileSafety.RelativeRelated(path, p)))
                    throw new ScopeException(ErrorCode.PreservationConflict, $"保留项与旧版或新版受跟踪文件冲突，请取消保留：{path}");
            var settings = target.Preservation;
            if (!settings.Paths.SequenceEqual(normalized, StringComparer.Ordinal))
                settings = new(1, checked(settings.SettingsVersion + 1), normalized);
            var roots = new List<PreservationRoot>(); var warnings = new List<string>();
            foreach (var relative in normalized)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = FileSafety.Child(target.Path, relative);
                FileSafety.NoReparseAncestors(path);
                bool exists = FileSafety.Exists(path);
                roots.Add(new(relative, exists, exists ? FileSafety.Identity(path) : null,
                    exists ? FileSafety.Fingerprint(path, cancellationToken) : null));
                if (!exists) warnings.Add($"未找到 {relative}，本次跳过，不创建内容。");
            }
            warnings.Add("更新会移除所有未选择保留的旧内容。请先停止输出目录中的开发服务器、编辑器写入和包管理器。");
            target = target with { Preservation = settings };
            _store.SaveTarget(target);
            var plan = new ExportPlan(1, Guid.NewGuid().ToString("N"), requestId, repo, target, resolved, tree,
                entries, settings, roots, warnings);
            _store.Write(_store.PlanFile(plan.PlanId), plan);
            return plan;
        }, cancellationToken);
    }

    public Task<ExportResult> ExecuteAsync(ExportPlan plan, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.Run(() => ExecuteCoreAsync(plan, progress, cancellationToken));

    private async Task<ExportResult> ExecuteCoreAsync(ExportPlan plan, IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        TransactionJournal? journal = null;
        IDisposable? gate = null, heldParents = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var savedPlan = _store.Read<ExportPlan>(_store.PlanFile(plan.PlanId));
            if (JsonSerializer.Serialize(plan, TransactionStore.Json) != JsonSerializer.Serialize(savedPlan, TransactionStore.Json))
                throw new ScopeException(ErrorCode.TargetChanged, "导出计划已改变，请重新预检。");
            gate = _store.Lock(plan.Target.Path);
            if (File.Exists(_store.JournalFile(plan.PlanId)))
                throw new ScopeException(ErrorCode.TargetChanged, "此计划已经执行，请重新预检生成新计划。");
            var target = _store.Target(plan.Target.TargetId);
            _store.Claim(target.Path, target.Identity);
            RequireNoRecovery(target.TargetId);
            if (target != plan.Target && JsonSerializer.Serialize(target, TransactionStore.Json) != JsonSerializer.Serialize(plan.Target, TransactionStore.Json))
                throw new ScopeException(ErrorCode.TargetChanged, "目标版本或保留选择已更新，请重新预检。");
            var repo = await RefreshRepository(plan.Repository, cancellationToken);
            CheckTargetPath(repo, target.Path); RequireIdentity(target);
            heldParents = FileSafety.HoldParents(target.Path);
            CheckRoots(plan, target.Path, cancellationToken);
            SnapshotExporter.ValidateEntries(plan.Entries);
            if (await _objects.ResolveTreeAsync(repo, plan.CommitOid, cancellationToken) != plan.TreeOid)
                throw new ScopeException(ErrorCode.TargetChanged, "Git 树不匹配冻结计划。");
            var parent = Path.GetDirectoryName(target.Path)!;
            long requiredBytes = plan.Entries.Sum(entry => entry.Size);
            if (new DriveInfo(Path.GetPathRoot(parent)!).AvailableFreeSpace < requiredBytes + 1024 * 1024)
                throw new ScopeException(ErrorCode.InsufficientSpace, "同卷暂存空间不足，现有输出未改变。");
            var oldFingerprint = await Task.Run(() => FileSafety.Fingerprint(target.Path, cancellationToken), cancellationToken);
            journal = new()
            {
                TransactionId = plan.PlanId, Plan = plan,
                StagePath = Path.Combine(parent, ".gitscope-" + plan.PlanId + ".stage"),
                BackupPath = Path.Combine(parent, ".gitscope-" + plan.PlanId + ".backup"),
                ParentIdentity = FileSafety.Identity(parent), OldFingerprint = oldFingerprint
            };
            if (FileSafety.Exists(journal.StagePath) || FileSafety.Exists(journal.BackupPath))
                throw new ScopeException(ErrorCode.TargetChanged, "事务目录已被占用，请保留现场并重新预检。");
            Save(journal, "STAGE_CREATE_INTENT");
            Hit("before-stage-create");
            Directory.CreateDirectory(journal.StagePath);
            Hit("after-stage-directory-action");
            journal.StageIdentity = FileSafety.Identity(journal.StagePath);
            Save(journal, "STAGING");
            Hit("after-stage-create");
            progress?.Report(new(plan.RequestId, "STAGING", "准备完整代码；现有输出尚未改变。"));
            await new SnapshotExporter(_objects).WriteAsync(plan, journal.StagePath, progress, cancellationToken);
            Hit("before-stage-verify");
            await Task.Run(() => SnapshotExporter.Verify(plan, journal.StagePath, false, cancellationToken), cancellationToken);
            Hit("after-stage-verify");
            RequireIdentity(target); CheckRoots(plan, target.Path, cancellationToken);
            if (FileSafety.Fingerprint(target.Path, cancellationToken) != journal.OldFingerprint)
                throw new ScopeException(ErrorCode.TargetChanged, "准备代码期间输出目录有外部修改，本次未替换，请停止写入后重试。");
            Save(journal, "VERIFIED");
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new(plan.RequestId, "OLD_MOVED", "正在替换目录；取消请求将在安全收尾后处理。"));
            Save(journal, "OLD_MOVE_INTENT");
            ValidateParent(journal); Hit("before-old-move");
            FileSafety.Move(target.Path, journal.BackupPath, target.Identity);
            Hit("after-old-move"); Save(journal, "OLD_MOVED");
            for (int i = 0; i < plan.Roots.Count; i++)
            {
                var r = plan.Roots[i]; if (!r.Exists) continue;
                Save(journal, "PRESERVE_INTENT:" + i);
                var from = FileSafety.Child(journal.BackupPath, r.RelativePath);
                var to = FileSafety.Child(journal.StagePath, r.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                if (FileSafety.Fingerprint(from) != r.Fingerprint) throw new ScopeException(ErrorCode.TargetChanged, "保留内容在迁移前发生变化。");
                Hit("before-preserve:" + i); ValidateParent(journal);
                FileSafety.Move(from, to, r.Identity!);
                Hit("after-preserve:" + i);
                journal.MovedRoots.Add(r.RelativePath); Save(journal, "PRESERVED:" + i);
            }
            progress?.Report(new(plan.RequestId, "PRESERVING", "核对新代码与保留内容。"));
            SnapshotExporter.Verify(plan, journal.StagePath, true);
            Save(journal, "PROMOTE_INTENT"); Hit("before-promote"); ValidateParent(journal);
            FileSafety.Move(journal.StagePath, target.Path, journal.StageIdentity!);
            Hit("after-promote"); Save(journal, "NEW_PROMOTED");
            SnapshotExporter.Verify(plan, target.Path, true);
            Hit("after-final-verify");
            // The durable journal is the authority. Any failure after it commits must never roll back.
            journal.Committed = true;
            Save(journal, "COMMITTED");
            Hit("before-target-registration"); _store.SaveTarget(CommittedTarget(journal)); Hit("after-target-registration");
            progress?.Report(new(plan.RequestId, "COMMITTED", "代码已更新，正在清理本次旧备份。"));
            return FinishCommitted(journal);
        }
        catch (Exception exception)
        {
            bool cancelled = exception is OperationCanceledException;
            var code = exception is ScopeException known ? known.Code : exception is IOException io && (io.HResult & 0xffff) is 39 or 112
                ? ErrorCode.InsufficientSpace : ErrorCode.ReplaceFailed;
            if (journal is null) return new(cancelled ? OperationOutcome.Cancelled : OperationOutcome.Failed,
                plan.Target.TargetId, plan.Target.LastCommitOid, cancelled ? "已取消，现有输出未改变。" : exception.Message, false, cancelled ? null : code);
            try
            {
                if (File.Exists(_store.JournalFile(journal.TransactionId))) journal = _store.ReadJournal(journal.TransactionId);
                else
                {
                    // Failed first journal write: no filesystem action is permitted before it.
                    RequireIdentity(plan.Target);
                    return new(cancelled ? OperationOutcome.Cancelled : OperationOutcome.Failed, plan.Target.TargetId,
                        plan.Target.LastCommitOid, "未能持久化事务，现有输出未改变。", false, code);
                }
                if (journal.Committed)
                {
                    journal.Message = "新版本已提交，登记或清理待恢复：" + exception.Message;
                    try { Save(journal, "COMMITTED_CLEANUP_PENDING"); } catch { /* Existing COMMITTED record remains authoritative. */ }
                    return new(OperationOutcome.Success, plan.Target.TargetId, plan.CommitOid, journal.Message, true);
                }
                Rollback(journal, progress);
                return new(cancelled ? OperationOutcome.Cancelled : OperationOutcome.Failed, plan.Target.TargetId,
                    plan.Target.LastCommitOid, cancelled ? "已取消并恢复原目录。" : "操作失败，原目录已完整恢复：" + exception.Message, false, cancelled ? null : code);
            }
            catch (Exception recovery)
            {
                journal.Message = "保留全部现场，需恢复：" + recovery.Message;
                try { Save(journal, "RECOVERY_REQUIRED"); } catch { }
                return new(OperationOutcome.RecoveryRequired, plan.Target.TargetId, null, journal.Message, false, ErrorCode.RecoveryRequired);
            }
        }
        finally { heldParents?.Dispose(); gate?.Dispose(); }
    }

    public Task<IReadOnlyList<RecoveryItem>> PendingAsync(CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<RecoveryItem>>(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _store.Journals().Where(j => !j.IsTerminal).OrderBy(j => j.StartedUtc)
            .Select(j => new RecoveryItem(j.Plan.Target.TargetId, j.Plan.Target.Path, j.TransactionId, j.State, j.Committed,
                j.Committed ? "新版本已提交；重试登记或仅清理本次旧备份。" : "上次操作未完成；需恢复原目录后才能再次导出。")).ToArray();
    }, cancellationToken);

    public Task<ExportResult> RecoverAsync(string targetId, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var target = _store.Target(targetId);
        using var gate = _store.Lock(target.Path);
        using var held = FileSafety.HoldParents(target.Path);
        var journals = _store.Journals().Where(j => j.Plan.Target.TargetId == targetId && !j.IsTerminal).OrderBy(j => j.StartedUtc).ToArray();
        if (journals.Length == 0) return new ExportResult(OperationOutcome.Success, targetId, target.LastCommitOid, "没有待恢复事务。");
        bool cleanupPending = false; string? commit = target.LastCommitOid;
        foreach (var j in journals)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ValidateParent(j);
                if (j.Committed)
                {
                    var result = FinishCommitted(j);
                    cleanupPending |= result.CleanupPending; commit = result.CommitOid;
                }
                else { Rollback(j, progress); commit = j.Plan.Target.LastCommitOid; }
            }
            catch (Exception e)
            {
                j.Message = "未能安全恢复，保留全部现场：" + e.Message;
                try { Save(j, j.Committed ? "COMMITTED_CLEANUP_PENDING" : "RECOVERY_REQUIRED"); } catch { }
                return new ExportResult(j.Committed ? OperationOutcome.Success : OperationOutcome.RecoveryRequired,
                    targetId, j.Committed ? j.Plan.CommitOid : null, j.Message, j.Committed, ErrorCode.RecoveryRequired);
            }
        }
        return new ExportResult(OperationOutcome.Success, targetId, commit,
            cleanupPending ? "新版本可用，仍有待清理备份。" : "恢复完成，输出目录可继续使用。", cleanupPending);
    }, cancellationToken);

    private ExportResult FinishCommitted(TransactionJournal j)
    {
        try
        {
            ValidateParent(j);
            var current = _store.Target(j.Plan.Target.TargetId);
            if (FileSafety.IdentityOrNull(j.Plan.Target.Path) == j.StageIdentity)
            {
                // Do not validate byte equality here: users can legitimately edit a committed output.
                if (current.LastExportId != j.TransactionId) _store.SaveTarget(CommittedTarget(j));
            }
            else if (current.LastExportId == j.TransactionId || current.LastExportId == j.Plan.Target.LastExportId)
                throw new ScopeException(ErrorCode.TargetChanged, "已提交输出的身份异常，保留备份，需核对目录。");
            else RequireIdentity(current); // A later export succeeded; only clean this earlier transaction's backup.
            Hit("before-backup-cleanup");
            FileSafety.DeleteOwnedTree(j.BackupPath, j.Plan.Target.Identity, Path.GetDirectoryName(j.Plan.Target.Path)!);
            Hit("after-backup-cleanup"); Save(j, "CLEANED");
            return new(OperationOutcome.Success, j.Plan.Target.TargetId, current.LastExportId == j.TransactionId || current.LastExportId == j.Plan.Target.LastExportId
                ? j.Plan.CommitOid : current.LastCommitOid, "代码更新成功。保留的依赖是否匹配，需要自行检查。", false);
        }
        catch (Exception e)
        {
            j.Message = "新版本已提交，清理或登记待重试：" + e.Message;
            try { Save(j, "COMMITTED_CLEANUP_PENDING"); } catch { }
            return new(OperationOutcome.Success, j.Plan.Target.TargetId, j.Plan.CommitOid, j.Message, true);
        }
    }
    private static ManagedTarget CommittedTarget(TransactionJournal j) => j.Plan.Target with
    { Identity = j.StageIdentity!, LastCommitOid = j.Plan.CommitOid, LastExportId = j.TransactionId,
        LastTrackedPaths = j.Plan.Entries.Select(e => e.Path).ToArray(), Preservation = j.Plan.Preservation };

    private void Rollback(TransactionJournal j, IProgress<OperationProgress>? progress)
    {
        if (j.Committed) throw new ScopeException(ErrorCode.RecoveryRequired, "已提交事务不能回滚。");
        ValidateParent(j);
        var target = j.Plan.Target;
        progress?.Report(new(j.Plan.RequestId, "ROLLING_BACK", "正在恢复迁移前的目录和本地修改。"));
        Save(j, "ROLLBACK_INTENT");
        if (FileSafety.IdentityOrNull(target.Path) == target.Identity)
        {
            if (FileSafety.Exists(j.BackupPath)) throw new ScopeException(ErrorCode.RecoveryRequired, "原目录与备份同时存在，拒绝猜测。");
            if (FileSafety.Fingerprint(target.Path) != j.OldFingerprint)
                throw new ScopeException(ErrorCode.TargetChanged, "原目录内容有外部变化，保留现场待核对。");
        }
        else
        {
            if (FileSafety.IdentityOrNull(j.BackupPath) != target.Identity)
                throw new ScopeException(ErrorCode.RecoveryRequired, "无法确认原目录备份的身份。");
            if (FileSafety.Exists(target.Path))
            {
                if (j.StageIdentity is null || FileSafety.Identity(target.Path) != j.StageIdentity || FileSafety.Exists(j.StagePath))
                    throw new ScopeException(ErrorCode.RecoveryRequired, "输出位置存在未知内容，未覆盖或删除。");
                Save(j, "ISOLATE_NEW_INTENT"); Hit("before-isolate-new");
                FileSafety.Move(target.Path, j.StagePath, j.StageIdentity);
                Hit("after-isolate-new"); Save(j, "NEW_ISOLATED");
            }
            if (j.StageIdentity is null || FileSafety.IdentityOrNull(j.StagePath) != j.StageIdentity)
                throw new ScopeException(ErrorCode.RecoveryRequired, "无法确认新目录位置，保留备份等待恢复。");
            for (int i = j.Plan.Roots.Count - 1; i >= 0; i--)
            {
                var r = j.Plan.Roots[i]; if (!r.Exists) continue;
                var oldPath = FileSafety.Child(j.BackupPath, r.RelativePath);
                var newPath = FileSafety.Child(j.StagePath, r.RelativePath);
                var oldId = FileSafety.IdentityOrNull(oldPath); var newId = FileSafety.IdentityOrNull(newPath);
                if (oldId == r.Identity && newId is null) continue;
                if (oldId is not null || newId != r.Identity)
                    throw new ScopeException(ErrorCode.RecoveryRequired, $"保留项位置不明确，未删除任何副本：{r.RelativePath}");
                if (FileSafety.Fingerprint(newPath) != r.Fingerprint)
                    throw new ScopeException(ErrorCode.TargetChanged, $"保留项发生外部修改，保留现场：{r.RelativePath}");
                // Its parent belonged to the old tree and remains after moving only the root.
                if (!Directory.Exists(Path.GetDirectoryName(oldPath))) throw new ScopeException(ErrorCode.RecoveryRequired, "保留项原父目录丢失。");
                Save(j, "RETURN_INTENT:" + i); Hit("before-return:" + i);
                FileSafety.Move(newPath, oldPath, r.Identity!);
                Hit("after-return:" + i); Save(j, "RETURNED:" + i);
            }
            if (FileSafety.Fingerprint(j.BackupPath) != j.OldFingerprint)
                throw new ScopeException(ErrorCode.TargetChanged, "回滚后的旧目录内容不匹配迁移前快照，保留现场。");
            Save(j, "RESTORE_OLD_INTENT"); Hit("before-restore-old");
            FileSafety.Move(j.BackupPath, target.Path, target.Identity);
            Hit("after-restore-old"); Save(j, "OLD_RESTORED");
        }
        if (FileSafety.Exists(j.StagePath))
        {
            if (j.StageIdentity is null) throw new ScopeException(ErrorCode.RecoveryRequired, "暂存创建后尚未记录身份；原目录安全，暂存需人工核对。");
            foreach (var r in j.Plan.Roots.Where(r => r.Exists))
                if (FileSafety.Exists(FileSafety.Child(j.StagePath, r.RelativePath)))
                    throw new ScopeException(ErrorCode.RecoveryRequired, "暂存中仍有保留路径，禁止清理。");
            Hit("before-stage-cleanup");
            FileSafety.DeleteOwnedTree(j.StagePath, j.StageIdentity, Path.GetDirectoryName(target.Path)!);
            Hit("after-stage-cleanup");
        }
        _store.SaveTarget(target);
        Save(j, "ROLLED_BACK");
    }
    private void Save(TransactionJournal journal, string state)
    {
        Hit("before-journal:" + state);
        journal.State = state;
        _store.Write(_store.JournalFile(journal.TransactionId), journal);
        Hit("after-journal:" + state);
    }
    private void Hit(string point) => _fault?.Invoke(point);
    private static void ValidateParent(TransactionJournal j)
    {
        TransactionStore.ValidateJournal(j);
        var parent = Path.GetDirectoryName(j.Plan.Target.Path)!;
        FileSafety.NoReparseAncestors(parent);
        foreach (var path in new[] { j.Plan.Target.Path, j.StagePath, j.BackupPath }) FileSafety.NoReparseAncestors(path);
        if (FileSafety.Identity(parent) != j.ParentIdentity)
            throw new ScopeException(ErrorCode.TargetChanged, "事务父目录身份改变，保留全部现场。");
    }
    private void RequireNoRecovery(string targetId)
    {
        if (_store.Journals().Any(j => j.Plan.Target.TargetId == targetId && !j.IsTerminal && !j.Committed))
            throw new ScopeException(ErrorCode.RecoveryRequired, "此目录有未完成事务，请先从恢复入口恢复原目录。");
    }
    private static void RequireIdentity(ManagedTarget t)
    {
        FileSafety.NoReparseAncestors(t.Path);
        if (!Directory.Exists(t.Path) || FileSafety.Identity(t.Path) != t.Identity)
            throw new ScopeException(ErrorCode.TargetChanged, "输出目录已丢失或被替换，不能凭同名路径认领。");
    }
    private static void CheckRoots(ExportPlan plan, string root, CancellationToken token)
    {
        foreach (var r in plan.Roots)
        {
            var path = FileSafety.Child(root, r.RelativePath);
            FileSafety.NoReparseAncestors(path);
            if (FileSafety.Exists(path) != r.Exists || r.Exists && (FileSafety.Identity(path) != r.Identity || FileSafety.Fingerprint(path, token) != r.Fingerprint))
                throw new ScopeException(ErrorCode.TargetChanged, $"保留项自预检后发生变化，请重新预检：{r.RelativePath}");
        }
    }
    private void CheckTargetPath(RepositoryInfo repo, string path)
    {
        FileSafety.NoReparseAncestors(path);
        var driveRoot = Path.GetPathRoot(path)!;
        if (Path.TrimEndingDirectorySeparator(path).Equals(Path.TrimEndingDirectorySeparator(driveRoot), StringComparison.OrdinalIgnoreCase))
            throw new ScopeException(ErrorCode.UnsafeTarget, "不能把磁盘根目录作为输出目录。");
        FileSafety.Relative(path[driveRoot.Length..]);
        var drive = new DriveInfo(driveRoot);
        if (drive.DriveType != DriveType.Fixed || !drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
            throw new ScopeException(ErrorCode.UnsafeTarget, "首版目录事务仅支持本机固定 NTFS 磁盘。");
        var protectedPaths = repo.ProtectedPaths.Concat(new[] { repo.RootPath, repo.GitDirectory, repo.CommonDirectory, _store.Root, TransactionStore.CoordinationRoot, AppContext.BaseDirectory });
        foreach (var p in protectedPaths)
        {
            FileSafety.NoReparseAncestors(p);
            if (FileSafety.Related(path, FileSafety.Canonical(p)))
                throw new ScopeException(ErrorCode.UnsafeTarget, "输出目录不能与源仓库、关联 worktree、Git 元数据、程序或配置目录相互包含。");
        }
    }
    private async Task<RepositoryInfo> RefreshRepository(RepositoryInfo repo, CancellationToken token)
    {
        var current = await new RepositoryService(_runner).InspectAsync(repo.RootPath, token);
        if (current.Id != repo.Id) throw new ScopeException(ErrorCode.InvalidRepository, "仓库身份发生变化，请重新选择仓库。");
        return current;
    }
    private static string[] NormalizeKeeps(IReadOnlyList<string> values)
    {
        var roots = new List<string>();
        foreach (var path in values.Select(p => FileSafety.Relative(p, ErrorCode.PreservationConflict))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p.Length).ThenBy(p => p, StringComparer.Ordinal))
            if (!roots.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase) || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase))) roots.Add(path);
        return roots.ToArray();
    }
}
