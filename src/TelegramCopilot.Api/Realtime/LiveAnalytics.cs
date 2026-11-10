using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Services;

namespace TelegramCopilot.Api.Realtime;

internal static class LiveAnalytics
{
    public static async Task<object> ReadAsync(ITelemetryService telemetry, Guid id, string kind,
        string query, CancellationToken ct)
    {
        var fields = QueryHelpers.ParseQuery(query);
        string[] allowed = kind == "timeline"
            ? ["from", "to", "page", "pageSize"]
            : ["from", "to", "page", "pageSize", "period", "timeZone"];
        if (fields.Any(x => !allowed.Contains(x.Key) || x.Value.Count != 1)
            || !DateTimeOffset.TryParse(fields["from"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
            || !DateTimeOffset.TryParse(fields["to"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var to)
            || from >= to || to - from > TimeSpan.FromDays(31))
            throw new ArgumentException("Invalid analytics subscription.");
        var page = Number("page", 1); var size = Number("pageSize", 500);
        QueryPolicy.Page(page, size);
        if (page > 32) throw new ArgumentException("Analytics page limit exceeded.");
        var range = new TimelineQuery(from, to, page, size);
        if (kind == "timeline") return await telemetry.TimelineAsync(id, range, ct);
        var period = fields["period"].ToString();
        var zone = fields["timeZone"].ToString();
        if (period != "day" || !TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _))
            throw new ArgumentException("Invalid daily statistics subscription.");
        return await telemetry.StatisticsAsync(id, range, period, zone, ct);

        int Number(string key, int fallback) => !fields.ContainsKey(key) ? fallback
            : int.TryParse(fields[key].ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? value : throw new ArgumentException("Invalid analytics pagination.");
    }
}
