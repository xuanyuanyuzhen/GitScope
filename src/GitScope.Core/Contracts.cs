namespace GitScope.Core;

public enum ErrorCode
{
    GitUnavailable, InvalidRepository, UnsupportedRepository, MissingRevision,
    InvalidDateRange, InvalidTimeZone, GitCommandFailed, UnsafeTarget, TargetBusy,
    PreservationConflict, TargetChanged, UnsupportedEntry, MissingObject,
    InsufficientSpace, ExportValidationFailed, ReplaceFailed, RecoveryRequired,
    InvalidConfiguration
}
public sealed class ScopeException(ErrorCode code, string message, Exception? inner = null) : Exception(message, inner)
{
    public ErrorCode Code { get; } = code;
}
public enum ThemeMode { System, Light, Dark }
public enum HistoryMode { All, FirstParent }
public enum HistoryCoverage { LocalComplete, Shallow }
public enum OperationOutcome { Success, Failed, Cancelled, RecoveryRequired }
public enum DependencyState { FirstExport, Changed, Unchanged, Unknown }

public sealed record RepositoryInfo(string Id, string RootPath, string GitDirectory,
    string CommonDirectory, bool IsShallow, string ObjectFormat, IReadOnlyList<string> ProtectedPaths);
public sealed record RevisionInfo(string Name, string Oid, string Kind);
public sealed record CommitSummary(string Oid, IReadOnlyList<string> Parents, string Subject,
    string AuthorName, long AuthorTime, long CommitterTime);
public sealed record HistoryFilter(string? StartDate, string? EndDate, string TimeZoneId, HistoryMode Mode = HistoryMode.All);
public sealed record DateWindow(long? StartInclusive, long? EndExclusive, string TimeZoneId);
public sealed record HistorySnapshot(string RequestId, string TipOid, IReadOnlyList<CommitSummary> Items,
    HistoryCoverage Coverage, IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<CommitSummary> Page(int pageIndex, int pageSize = 50) =>
        Items.Skip(Math.Max(0, pageIndex) * Math.Clamp(pageSize, 1, 500)).Take(Math.Clamp(pageSize, 1, 500)).ToArray();
}
public sealed record ChangedFile(string Path, string Status, bool IsBinary, long? OldSize, long? NewSize);
public sealed record CommitPreview(string CommitOid, string Comparison, IReadOnlyList<ChangedFile> Files);
public sealed record DependencyNotice(DependencyState State, IReadOnlyList<string> ChangedPaths, string Message);
public sealed record GitTreeEntry(string Path, string Mode, string Type, string ObjectId, long Size);

public sealed record PreservationSettings(int SchemaVersion, int SettingsVersion, IReadOnlyList<string> Paths)
{
    public static PreservationSettings Empty => new(1, 0, []);
}
public sealed record PreservationRoot(string RelativePath, bool Exists, string? Identity, string? Fingerprint);
public sealed record ManagedTarget(int SchemaVersion, string TargetId, string RepositoryId, string Path,
    string Identity, string? LastCommitOid, string? LastExportId, IReadOnlyList<string> LastTrackedPaths,
    PreservationSettings Preservation);
public sealed record ExportPlan(int SchemaVersion, string PlanId, string RequestId, RepositoryInfo Repository,
    ManagedTarget Target, string CommitOid, string TreeOid, IReadOnlyList<GitTreeEntry> Entries,
    PreservationSettings Preservation, IReadOnlyList<PreservationRoot> Roots, IReadOnlyList<string> Warnings);
public sealed record OperationProgress(string RequestId, string Stage, string Message, int? Completed = null, int? Total = null);
public sealed record ExportResult(OperationOutcome Outcome, string TargetId, string? CommitOid, string Message,
    bool CleanupPending = false, ErrorCode? Error = null);
public sealed record RecoveryItem(string TargetId, string TargetPath, string TransactionId, string Stage, bool CleanupOnly, string Message);

public interface IRepositoryService
{
    Task<RepositoryInfo> InspectAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RevisionInfo>> GetReferencesAsync(RepositoryInfo repository, CancellationToken cancellationToken = default);
    Task<string> ResolveCommitAsync(RepositoryInfo repository, string revision, CancellationToken cancellationToken = default);
}
public interface IHistoryService
{
    Task<HistorySnapshot> QueryAsync(RepositoryInfo repository, string tipOid, HistoryFilter filter, string requestId, CancellationToken cancellationToken = default);
    Task<CommitPreview> PreviewAsync(RepositoryInfo repository, string commitOid, CancellationToken cancellationToken = default);
    Task<string> DiffAsync(RepositoryInfo repository, string commitOid, string path, CancellationToken cancellationToken = default);
    Task<DependencyNotice> DependencyNoticeAsync(RepositoryInfo repository, string? previousOid, string commitOid, CancellationToken cancellationToken = default);
}
public interface ITargetService
{
    Task<IReadOnlyList<ManagedTarget>> ListAsync(CancellationToken cancellationToken = default);
    Task<ManagedTarget> RegisterAsync(RepositoryInfo repository, string path, CancellationToken cancellationToken = default);
    Task<ExportPlan> PlanAsync(RepositoryInfo repository, string commitOid, string targetId, IReadOnlyList<string> keepPaths, string requestId, CancellationToken cancellationToken = default);
    Task<ExportResult> ExecuteAsync(ExportPlan plan, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecoveryItem>> PendingAsync(CancellationToken cancellationToken = default);
    Task<ExportResult> RecoverAsync(string targetId, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
}

public sealed record AppSettings(int SchemaVersion = 1, ThemeMode Theme = ThemeMode.System,
    string RepositoryPath = "", string Reference = "", string StartDate = "", string EndDate = "",
    string TimeZoneId = "Asia/Shanghai", string TargetId = "", string TargetPath = "", string[]? KeepPaths = null,
    HistoryMode HistoryMode = HistoryMode.All);
