namespace SaveState.Core.Backup;

/// <summary>Human-friendly sizes and times, matching the website's wording.</summary>
public static class Sizes
{
    /// <summary>43_991_040 → "42 MB". Binary units with the familiar labels, like Windows Explorer.</summary>
    public static string Format(long bytes)
    {
        if (bytes < 0) bytes = 0;
        if (bytes < 1024) return $"{bytes} B";
        string[] units = ["KB", "MB", "GB", "TB"];
        double value = bytes / 1024d;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        var text = value < 10 ? value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) : Math.Round(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"{text} {units[unit]}";
    }

    /// <summary>"in 23 days", "in 5 hours", "in under an hour", or "expired".</summary>
    public static string TimeLeft(DateTimeOffset expiresAt, DateTimeOffset? now = null)
    {
        var left = expiresAt - (now ?? DateTimeOffset.UtcNow);
        if (left <= TimeSpan.Zero) return "expired";
        if (left.TotalHours < 1) return "in under an hour";
        if (left.TotalHours < 24)
        {
            var h = (int)left.TotalHours;
            return $"in {h} {(h == 1 ? "hour" : "hours")}";
        }
        var d = (int)left.TotalDays;
        return $"in {d} {(d == 1 ? "day" : "days")}";
    }
}
