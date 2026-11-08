using System.Globalization;

namespace TelegramCopilot.Core.Presence;

public static class DurationText
{
    public static string Format(double seconds, string? language = null)
    {
        if (!double.IsFinite(seconds) || seconds < 0) return "unknown";
        var remaining = Math.Round(seconds, 3);
        if (remaining < 60) return remaining.ToString("0.###", CultureInfo.InvariantCulture) + "s";
        if (language == "en")
        {
            var parts = new List<string>();
            foreach (var (unit, suffix) in new[] { (86400, "d"), (3600, "h"), (60, "m") })
            {
                var count = Math.Floor(remaining / unit);
                if (count > 0) parts.Add(count.ToString(CultureInfo.InvariantCulture) + suffix);
                remaining -= count * unit;
            }
            if (remaining > 0) parts.Add(remaining.ToString("0.###", CultureInfo.InvariantCulture) + "s");
            return string.Join(' ', parts);
        }
        var result = new System.Text.StringBuilder();
        foreach (var (unit, suffix) in new[] { (86400, "天"), (3600, "小时"), (60, seconds < 3600 ? "分钟" : "分") })
        {
            var value = Math.Floor(remaining / unit);
            if (value > 0) result.Append(value.ToString(CultureInfo.InvariantCulture)).Append(suffix);
            remaining -= value * unit;
        }
        if (remaining > 0) result.Append(remaining.ToString("0.###", CultureInfo.InvariantCulture)).Append("秒");
        return result.ToString();
    }
}
