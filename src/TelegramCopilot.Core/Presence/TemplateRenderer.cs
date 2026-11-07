using System.Globalization;

namespace TelegramCopilot.Core.Presence;

public static class TemplateRenderer
{
    public static string Render(string template, MonitorEvent value)
    {
        var seconds = value.OnlineSeconds;
        var duration = seconds is { } s ? TimeSpan.FromSeconds(s).ToString("c", CultureInfo.InvariantCulture) : "unknown";
        var replacements = new Dictionary<string, string>
        {
            ["NAME"] = value.Name, ["EVENT"] = value.Type.ToString(), ["TIME_OF_ONLINE"] = duration,
            ["ONLINE_SECONDS"] = seconds?.ToString("0.###", CultureInfo.InvariantCulture) ?? "unknown",
            ["AT"] = value.At.ToString("O"), ["DETAIL"] = value.Detail,
            ["TARGET_ID"] = value.TargetId?.ToString() ?? "", ["ACCOUNT_ID"] = value.AccountId?.ToString() ?? ""
        };
        return System.Text.RegularExpressions.Regex.Replace(template, @"\{([A-Z_]+)\}",
            match => replacements.TryGetValue(match.Groups[1].Value, out var text) ? text : match.Value);
    }
}
