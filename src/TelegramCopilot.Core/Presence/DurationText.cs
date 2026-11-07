using System.Globalization;

namespace TelegramCopilot.Core.Presence;

public static class DurationText
{
    public static string Format(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) return "unknown";
        var remaining = Math.Round(seconds, 3);
        if (remaining < 60) return remaining.ToString("0.###", CultureInfo.InvariantCulture) + "s";
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
