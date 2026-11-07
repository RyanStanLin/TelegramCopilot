using TelegramCopilot.Core.Configuration;

namespace TelegramCopilot.Core.Presence;

public enum ObservationEffect { Ignore, Confirm, Transition }
public sealed record PresenceDecision(ObservationEffect Effect, bool BoundaryKnown = false,
    EventType Event = EventType.Unknown, double? OnlineSeconds = null, bool Restored = false);

public sealed class PresenceMonitor(Guid targetId, PresenceInterval? current)
{
    public PresenceDecision Observe(Observation observation)
    {
        if (observation.TargetId != targetId || current is not null && current.TargetId != targetId)
            throw new ArgumentException("Observation belongs to a different monitor.");
        if (current is not null && (observation.ObservedAt < current.ObservedAt || observation.ObservedAt < current.Start))
            return new(ObservationEffect.Ignore);
        if (current?.State == observation.State && (observation.State != PresenceState.Unknown || current.Reason == observation.Reason))
            return new(ObservationEffect.Confirm);
        var known = current is not null && current.State != PresenceState.Unknown && observation.State != PresenceState.Unknown;
        var duration = current is { State: PresenceState.Online, StartKnown: true } && known
            ? (observation.ObservedAt - current.Start).TotalSeconds : (double?)null;
        var type = observation.State switch
        {
            PresenceState.Online => EventType.Online,
            PresenceState.Offline => EventType.Offline,
            _ => EventType.Unknown
        };
        return new(ObservationEffect.Transition, known, type, duration,
            current?.State == PresenceState.Unknown && observation.State != PresenceState.Unknown);
    }
}
