using System.Text.Json;
using GitScope.Core;

namespace GitScope.App;

public sealed class AppSettingsStore(string directory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public string FilePath => Path.Combine(DirectoryPath, "settings.json");
    private bool _blocked;

    public AppSettings Read()
    {
        if (!File.Exists(FilePath))
        {
            string zone;
            try { zone = DateSemantics.GetSystemTimeZoneId(); }
            catch (ScopeException) { zone = ""; }
            return new AppSettings(TimeZoneId: zone);
        }
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (!json.RootElement.TryGetProperty("SchemaVersion", out var version) || version.GetInt32() != 1)
                throw new JsonException("不支持此设置版本。");
            var saved = json.RootElement.Deserialize<AppSettings>(JsonOptions) ?? throw new JsonException("设置为空。");
            if (!Enum.IsDefined(saved.Theme) || !Enum.IsDefined(saved.HistoryMode) || saved.RepositoryPath is null ||
                saved.Reference is null || saved.StartDate is null || saved.EndDate is null || saved.TimeZoneId is null ||
                saved.TargetId is null || saved.TargetPath is null || saved.KeepPaths?.Any(p => p is null) == true)
                throw new JsonException("设置字段无效。");
            return saved;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            _blocked = true;
            throw new ScopeException(ErrorCode.InvalidConfiguration, "设置无法读取，已保留原文件且暂停保存。请检查 " + FilePath, e);
        }
    }

    public void Save(AppSettings settings)
    {
        if (_blocked) throw new ScopeException(ErrorCode.InvalidConfiguration, "原设置文件尚未修复，本次选择仅在当前窗口生效。");
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, JsonOptions);
                stream.Flush(true);
            }
            File.Move(temporary, FilePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
