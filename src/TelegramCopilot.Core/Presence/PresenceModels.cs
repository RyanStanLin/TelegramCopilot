namespace TelegramCopilot.Core.Presence;

public enum PresenceState { Online, Offline, Unknown }
public sealed record Observation(Guid TargetId, PresenceState State, DateTimeOffset ObservedAt,
    string Reason, DateTimeOffset? TelegramAt = null, DateTimeOffset? ExpiresAt = null,
    Guid? SourceAccountId = null, string? SourceIdentifier = null);
public sealed record PresenceInterval(long Id, Guid TargetId, PresenceState State, DateTimeOffset Start,
    DateTimeOffset? End, string Reason, DateTimeOffset ObservedAt, DateTimeOffset? TelegramAt,
    bool StartKnown, bool EndKnown, DateTimeOffset? ExpiresAt = null)
{
    public double Seconds(DateTimeOffset until) => Math.Max(0, ((End ?? until) - Start).TotalSeconds);
}
public sealed record MonitorEvent(long Id, Guid? TargetId, Guid? AccountId, string Name,
    Configuration.EventType Type, DateTimeOffset At, double? OnlineSeconds, string Detail);

public static class PresenceTimeline
{
    public static IReadOnlyList<PresenceInterval> Fill(IEnumerable<PresenceInterval> source,
        Guid targetId, DateTimeOffset from, DateTimeOffset to)
    {
        var result = new List<PresenceInterval>();
        var cursor = from;
        foreach (var interval in source.OrderBy(x => x.Start).ThenBy(x => x.Id))
        {
            var start = interval.Start < from ? from : interval.Start;
            if (start < cursor) start = cursor;
            var end = interval.End is { } e && e < to ? e : to;
            if (end <= start) continue;
            if (start > cursor) result.Add(Gap(cursor, start));
            result.Add(interval with { Start = start, End = end,
                StartKnown = interval.StartKnown && start == interval.Start,
                EndKnown = interval.EndKnown && end == interval.End });
            cursor = end;
        }
        if (cursor < to) result.Add(Gap(cursor, to));
        return result;
        PresenceInterval Gap(DateTimeOffset s, DateTimeOffset e) =>
            new(0, targetId, PresenceState.Unknown, s, e, "NoObservation", s, null, false, false);
    }
}
