using System.Globalization;
using System.Text;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Application.Services;

public interface ITelemetryService
{
    Task<PageResponse<IntervalResponse>> TimelineAsync(Guid targetId, TimelineQuery query, CancellationToken ct);
    Task<string> CsvAsync(Guid targetId, TimelineQuery query, CancellationToken ct);
    Task<StatisticsResponse> StatisticsAsync(Guid targetId, TimelineQuery query, string period, string? timeZone, CancellationToken ct);
}
public sealed class TelemetryService(IConfigurationRepository configs, ITelemetryRepository telemetry, TimeProvider clock) : ITelemetryService
{
    public async Task<PageResponse<IntervalResponse>> TimelineAsync(Guid targetId, TimelineQuery query, CancellationToken ct)
    {
        var all = await Read(targetId, query, ct);
        var rows = new List<IntervalResponse>();
        DateTimeOffset? previousEnd = null;
        foreach (var i in all)
        {
            var gap = i.State == PresenceState.Online && i.StartKnown && previousEnd is { } end ? (i.Start - end).TotalSeconds : (double?)null;
            if (i.State == PresenceState.Online && i.EndKnown) previousEnd = i.End;
            if (i.State == PresenceState.Unknown) previousEnd = null;
            rows.Add(new(i.Id, i.TargetId, i.State.ToString(), i.Start, i.End!.Value, i.Seconds(query.To), i.Reason,
                i.ObservedAt, i.TelegramAt, i.StartKnown, i.EndKnown, gap));
        }
        if (query.State is { } state)
        {
            if (!Enum.TryParse<PresenceState>(state, true, out var filter) || !Enum.IsDefined(filter))
                throw new ArgumentException("state must be Online, Offline or Unknown.");
            rows = rows.Where(x => x.State == filter.ToString()).ToList();
        }
        return new(rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray(), rows.Count, query.Page, query.PageSize);
    }
    public async Task<string> CsvAsync(Guid targetId, TimelineQuery query, CancellationToken ct)
    {
        var text = new StringBuilder("id,target_id,state,start_utc,end_utc,seconds,reason,observed_at,telegram_at,start_known,end_known,gap_seconds\r\n");
        var page = 1;
        while (true)
        {
            var result = await TimelineAsync(targetId, query with { Page = page, PageSize = 500 }, ct);
            foreach (var r in result.Items)
                text.AppendLine(string.Join(',', new object?[] { r.Id, r.TargetId, r.State, r.Start.ToString("O"), r.End.ToString("O"),
                    r.Seconds, r.Reason, r.ObservedAt.ToString("O"), r.TelegramAt?.ToString("O"), r.StartKnown,
                    r.EndKnown, r.GapSincePreviousOnlineSeconds }.Select(CsvCell)));
            if (page * 500 >= result.Total) return text.ToString();
            if (text.Length > 100_000_000) throw new ArgumentException("CSV is too large. Export a smaller time range.");
            page++;
        }
    }
    public async Task<StatisticsResponse> StatisticsAsync(Guid targetId, TimelineQuery query, string period, string? timeZone, CancellationToken ct)
    {
        var all = await Read(targetId, query, ct);
        if (period is not ("day" or "week" or "month" or "year")) throw new ArgumentException("period must be day, week, month or year.");
        timeZone ??= (await configs.ListAsync(ResourceKind.Settings, ct)).FirstOrDefault()?.Read<SystemSettings>().TimeZone ?? "Asia/Taipei";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var date = TimeZoneInfo.ConvertTime(query.From, zone).Date;
        date = period switch
        {
            "week" => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
            "month" => new(date.Year, date.Month, 1),
            "year" => new(date.Year, 1, 1),
            _ => date
        };
        var buckets = new List<StatisticsBucket>();
        var effectiveTo = query.To < clock.GetUtcNow() ? query.To : clock.GetUtcNow();
        while (Boundary(date, zone) < effectiveTo)
        {
            var next = period switch { "week" => date.AddDays(7), "month" => date.AddMonths(1), "year" => date.AddYears(1), _ => date.AddDays(1) };
            var from = Boundary(date, zone); var to = Boundary(next, zone);
            buckets.Add(Summarize(all, from < query.From ? query.From : from, to > effectiveTo ? effectiveTo : to));
            date = next;
        }
        return new(targetId, period, timeZone, Summarize(all, query.From, effectiveTo),
            new(buckets.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray(), buckets.Count, query.Page, query.PageSize));
    }
    private async Task<IReadOnlyList<PresenceInterval>> Read(Guid targetId, TimelineQuery query, CancellationToken ct)
    {
        QueryPolicy.Range(query.From, query.To); QueryPolicy.Page(query.Page, query.PageSize);
        var resource = await configs.FindAsync(targetId, ct, includeDeleted: true);
        if (resource is null || resource.Kind != ResourceKind.Targets) throw new KeyNotFoundException("Target not found.");
        var to = query.To < clock.GetUtcNow() ? query.To : clock.GetUtcNow();
        if (query.From >= to) throw new ArgumentException("The query must begin in the past.");
        var intervals = await telemetry.IntervalsAsync(targetId, query.From, to, ct);
        if (resource.DeletedAt is { } deletedAt)
            intervals = intervals.Where(x => x.Start < deletedAt).Select(x => x.End is null || x.End > deletedAt
                ? x with { End = deletedAt, EndKnown = false } : x).ToArray();
        return PresenceTimeline.Fill(intervals, targetId, query.From, to);
    }
    private static StatisticsBucket Summarize(IReadOnlyList<PresenceInterval> rows, DateTimeOffset from, DateTimeOffset to)
    {
        var duration = new double[3]; var count = 0; var completed = new List<double>();
        foreach (var r in rows)
        {
            var start = r.Start < from ? from : r.Start;
            var end = r.End!.Value > to ? to : r.End.Value;
            if (end <= start) continue;
            duration[(int)r.State] += (end - start).TotalSeconds;
            if (r.State != PresenceState.Online) continue;
            if (r.StartKnown && r.Start >= from && r.Start < to) count++;
            if (r.StartKnown && r.EndKnown && r.Start >= from && r.End <= to) completed.Add(r.Seconds(to));
        }
        var total = (to - from).TotalSeconds;
        return new(from, to, count, duration[0], duration[1], duration[2], completed.Count > 0 ? completed.Average() : null,
            total > 0 ? (duration[0] + duration[1]) / total : 0);
    }
    private static DateTimeOffset Boundary(DateTime local, TimeZoneInfo zone)
    {
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
    public static string CsvCell(object? value)
    {
        var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        if (s.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@' or '\t' or '\r') s = "'" + s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
