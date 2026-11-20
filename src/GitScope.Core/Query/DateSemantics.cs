using System.Globalization;

namespace GitScope.Core;

/// <summary>Natural-day boundaries are calendar operations in an explicitly stored IANA time zone.</summary>
public static class DateSemantics
{
    public static DateWindow Parse(HistoryFilter filter)
    {
        var zone = GetTimeZone(filter.TimeZoneId);
        var start = ParseDate(filter.StartDate);
        var end = ParseDate(filter.EndDate);
        if (start.HasValue && end.HasValue && start > end)
            throw new ScopeException(ErrorCode.InvalidDateRange, "开始日期不能晚于结束日期。");
        DateTime? next = null;
        if (end.HasValue)
        {
            try { next = end.Value.AddDays(1); }
            catch (ArgumentOutOfRangeException e) { throw new ScopeException(ErrorCode.InvalidDateRange, "结束日期无法换算为次日边界。", e); }
        }
        return new DateWindow(start.HasValue ? Boundary(start.Value, zone) : null,
            next.HasValue ? Boundary(next.Value, zone) : null, filter.TimeZoneId);
    }

    public static TimeZoneInfo GetTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId != timeZoneId.Trim())
            throw new ScopeException(ErrorCode.InvalidTimeZone, "请选择有效的 IANA 时区，例如 Asia/Shanghai。");
        try
        {
            // Windows names are mapped once at first use, never silently substituted during restore.
            if (!TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out _))
                throw new ScopeException(ErrorCode.InvalidTimeZone, $"不支持的 IANA 时区：{timeZoneId}。");
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ScopeException(ErrorCode.InvalidTimeZone, $"无法读取时区：{timeZoneId}。请修正后重试。", e);
        }
    }

    public static string GetSystemTimeZoneId()
    {
        var local = TimeZoneInfo.Local;
        if (local.HasIanaId) return local.Id;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(local.Id, out var iana) && iana is not null) return iana;
        throw new ScopeException(ErrorCode.InvalidTimeZone, "本机时区无法映射为 IANA 标识，请手动选择时区。");
    }

    private static DateTime? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var result))
            throw new ScopeException(ErrorCode.InvalidDateRange, "日期请填写 yyyy-MM-dd，或留空。");
        return DateTime.SpecifyKind(result.Date, DateTimeKind.Unspecified);
    }

    private static long Boundary(DateTime local, TimeZoneInfo zone)
    {
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
            throw new ScopeException(ErrorCode.InvalidDateRange,
                $"{local:yyyy-MM-dd} 的零点在 {zone.Id} 中不存在或有两个含义，请调整日期。");
        try { return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone)).ToUnixTimeSeconds(); }
        catch (ArgumentException e) { throw new ScopeException(ErrorCode.InvalidDateRange, "日期边界无法唯一转换为 UTC。", e); }
    }
}
