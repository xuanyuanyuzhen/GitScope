using System.Text.Json;

namespace GitScope.Core;

internal sealed class TransactionStore
{
    public string Root { get; }
    public static string CoordinationRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitScope", "target-coordination");
    internal static JsonSerializerOptions Json { get; } = new() { WriteIndented = true };
    public TransactionStore(string directory)
    {
        Root = FileSafety.Canonical(directory);
        FileSafety.NoReparseAncestors(Root);
        Directory.CreateDirectory(Root);
        foreach (var name in new[] { "targets", "plans", "transactions", "locks" })
        {
            var path = Path.Combine(Root, name); FileSafety.NoReparseAncestors(path); Directory.CreateDirectory(path);
        }
    }
    public string TargetFile(string id) => Path.Combine(Root, "targets", SafeId(id) + ".json");
    public string PlanFile(string id) => Path.Combine(Root, "plans", SafeId(id) + ".json");
    public string JournalFile(string id) => Path.Combine(Root, "transactions", SafeId(id) + ".json");
    private static string SafeId(string id)
    {
        if (id.Length is < 16 or > 64 || id.Any(c => !char.IsAsciiHexDigit(c)))
            throw new ScopeException(ErrorCode.InvalidConfiguration, "状态记录标识无效，拒绝写入。");
        return id;
    }
    public IDisposable Lock(string path)
    {
        FileSafety.NoReparseAncestors(CoordinationRoot);
        Directory.CreateDirectory(CoordinationRoot);
        var file = Path.Combine(CoordinationRoot, FileSafety.Key(path) + ".lock");
        FileSafety.NoReparseAncestors(file);
        try { return new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException e) { throw new ScopeException(ErrorCode.TargetBusy, "另一个进程正在操作此输出目录，请稍后重试。", e); }
    }
    public void Claim(string targetPath, string identity)
    {
        var file = Path.Combine(CoordinationRoot, FileSafety.Key(targetPath) + ".owner.json");
        if (File.Exists(file))
        {
            var owner = Read<TargetOwnership>(file);
            if (!owner.StateRoot.Equals(Root, StringComparison.OrdinalIgnoreCase) || owner.TargetPath != targetPath)
                throw new ScopeException(ErrorCode.UnsafeTarget, "此输出目录已由另一配置目录登记，不能重复接管。");
        }
        else Write(file, new TargetOwnership(1, Root, targetPath, identity));
    }
    private sealed record TargetOwnership(int SchemaVersion, string StateRoot, string TargetPath, string Identity);
    public T Read<T>(string file)
    {
        FileSafety.NoReparseAncestors(file);
        try
        {
            var text = File.ReadAllText(file);
            using var parsed = JsonDocument.Parse(text);
            if (!parsed.RootElement.TryGetProperty("SchemaVersion", out var schema) || !schema.TryGetInt32(out var n) || n != 1)
                throw new ScopeException(ErrorCode.InvalidConfiguration, $"不支持的状态版本，请保留此文件并使用兼容版本恢复：{file}");
            return JsonSerializer.Deserialize<T>(text, Json) ?? throw new JsonException("Empty state");
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        { throw new ScopeException(ErrorCode.InvalidConfiguration, $"状态文件损坏，已保留原文件：{file}", e); }
    }
    public void Write<T>(string file, T data)
    {
        FileSafety.NoReparseAncestors(file);
        var temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        // A temp file is never a substitute for a valid canonical state file at recovery time.
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, data, Json);
                stream.Flush(true);
            }
            File.Move(temp, file, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public IReadOnlyList<ManagedTarget> Targets() => Directory.EnumerateFiles(Path.Combine(Root, "targets"), "*.json")
        .Select(p => Read<ManagedTarget>(p)).Select(ValidateTarget).ToArray();
    public ManagedTarget Target(string id) => ValidateTarget(Read<ManagedTarget>(TargetFile(id)));
    public void SaveTarget(ManagedTarget target) => Write(TargetFile(target.TargetId), ValidateTarget(target));
    public IReadOnlyList<TransactionJournal> Journals() => Directory.EnumerateFiles(Path.Combine(Root, "transactions"), "*.json")
        .Select(p => Read<TransactionJournal>(p)).Select(ValidateJournal).ToArray();
    public TransactionJournal ReadJournal(string id) => ValidateJournal(Read<TransactionJournal>(JournalFile(id)));
    private static ManagedTarget ValidateTarget(ManagedTarget t)
    {
        if (t.SchemaVersion != 1 || t.Preservation is null || t.Preservation.SchemaVersion != 1 || t.LastTrackedPaths is null
            || string.IsNullOrWhiteSpace(t.Path) || t.TargetId != FileSafety.Key(t.Path) || string.IsNullOrWhiteSpace(t.Identity)
            || t.RepositoryId is null)
            throw new ScopeException(ErrorCode.InvalidConfiguration, "输出目录登记损坏，拒绝猜测归属。");
        return t;
    }
    public static TransactionJournal ValidateJournal(TransactionJournal j)
    {
        if (j.Plan is null || j.Plan.Target is null || j.Plan.SchemaVersion != 1 || j.Plan.Roots is null || j.Plan.Entries is null
            || j.TransactionId != j.Plan.PlanId || j.TransactionId.Length != 32 || j.TransactionId.Any(c => !char.IsAsciiHexDigit(c)))
            throw new ScopeException(ErrorCode.InvalidConfiguration, "事务记录无效，保留现场等待人工核对。");
        ValidateTarget(j.Plan.Target);
        var parent = Path.GetDirectoryName(j.Plan.Target.Path)!;
        if (j.StagePath != Path.Combine(parent, ".gitscope-" + j.TransactionId + ".stage")
            || j.BackupPath != Path.Combine(parent, ".gitscope-" + j.TransactionId + ".backup")
            || j.ParentIdentity.Length == 0 || j.OldFingerprint.Length == 0)
            throw new ScopeException(ErrorCode.InvalidConfiguration, "事务路径或目录身份无效，拒绝清理。");
        return j;
    }
}

internal sealed record TransactionJournal
{
    public int SchemaVersion { get; init; } = 1;
    public string TransactionId { get; init; } = "";
    public required ExportPlan Plan { get; init; }
    public string StagePath { get; init; } = "";
    public string BackupPath { get; init; } = "";
    public string ParentIdentity { get; init; } = "";
    public string OldFingerprint { get; init; } = "";
    public string? StageIdentity { get; set; }
    public string State { get; set; } = "VALIDATING";
    public bool Committed { get; set; }
    public string Message { get; set; } = "";
    public string? OldOriginalState { get; set; }
    public List<string> CreatedOldParents { get; set; } = [];
    public List<string> MovedRoots { get; set; } = [];
    public DateTimeOffset StartedUtc { get; init; } = DateTimeOffset.UtcNow;
    public bool IsTerminal => State is "CLEANED" or "ROLLED_BACK";
}
