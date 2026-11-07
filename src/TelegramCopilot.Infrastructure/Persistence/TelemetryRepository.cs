using Microsoft.Data.Sqlite;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Infrastructure.Persistence;

public sealed partial class TelemetryRepository(Database db) : ITelemetryRepository
{
    private readonly SemaphoreSlim _gate = new(1);
    public async Task RecordAsync(Observation o, string name, Guid? accountId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var c = await db.Open(ct);
            using var transaction = c.BeginTransaction();
            var previous = await OpenInterval(c, o.TargetId, ct);
            if (previous is not null && (o.ObservedAt < previous.ObservedAt || o.ObservedAt < previous.Start)) return;
            if (previous?.State == o.State && (o.State != PresenceState.Unknown || previous.Reason == o.Reason)) return;
            var boundary = o.ObservedAt;
            var transitionKnown = previous is not null && previous.State != PresenceState.Unknown && o.State != PresenceState.Unknown;
            if (previous is not null)
            {
                await Database.Exec(c, "UPDATE intervals SET end=$end,end_known=$known WHERE id=$id", ct,
                    ("$end", Database.Ms(boundary)), ("$known", transitionKnown), ("$id", previous.Id));
            }
            await Database.Exec(c, """
                INSERT INTO intervals(target_id,state,start,reason,observed,telegram_at,start_known)
                VALUES($target,$state,$start,$reason,$observed,$telegram,$known)
                """, ct, ("$target", o.TargetId.ToString()), ("$state", o.State.ToString()), ("$start", Database.Ms(boundary)),
                ("$reason", o.Reason), ("$observed", Database.Ms(o.ObservedAt)),
                ("$telegram", o.TelegramAt is { } t ? Database.Ms(t) : null), ("$known", transitionKnown));
            var type = o.State switch { PresenceState.Online => EventType.Online, PresenceState.Offline => EventType.Offline, _ => EventType.Unknown };
            var duration = previous is { State: PresenceState.Online, StartKnown: true } && transitionKnown
                ? (boundary - previous.Start).TotalSeconds : (double?)null;
            await InsertEvent(c, new(0, o.TargetId, accountId, name, type, o.ObservedAt, duration, o.Reason), ct);
            if (previous?.State == PresenceState.Unknown && o.State != PresenceState.Unknown)
                await InsertEvent(c, new(0, o.TargetId, accountId, name, EventType.MonitorRestored, o.ObservedAt, null, "Observation resumed"), ct);
            transaction.Commit();
        }
        finally { _gate.Release(); }
    }
    public async Task<IReadOnlyList<PresenceInterval>> IntervalsAsync(Guid targetId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        var heartbeat = await HeartbeatAsync(ct);
        using var cmd = Database.Command(c, "SELECT * FROM intervals WHERE target_id=$target AND start<$to AND (end IS NULL OR end>$from) ORDER BY start,id",
            ("$target", targetId.ToString()), ("$from", Database.Ms(from)), ("$to", Database.Ms(to)));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<PresenceInterval>();
        while (await r.ReadAsync(ct))
        {
            var row = ReadInterval(r);
            if (row.End is null && row.State != PresenceState.Unknown)
                row = row with { End = heartbeat is { } h && h < to ? h : to, EndKnown = false };
            rows.Add(row);
        }
        return rows;
    }
    public async Task<DateTimeOffset?> HeartbeatAsync(CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        using var cmd = Database.Command(c, "SELECT heartbeat FROM worker WHERE id=1");
        return await cmd.ExecuteScalarAsync(ct) is long n ? Database.At(n) : null;
    }
    public async Task CheckpointAsync(DateTimeOffset at, CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        await Database.Exec(c, "INSERT INTO worker VALUES(1,$at) ON CONFLICT(id) DO UPDATE SET heartbeat=$at", ct, ("$at", Database.Ms(at)));
    }
    public async Task RecoverAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        var checkpoint = await HeartbeatAsync(ct) ?? now;
        using var transaction = c.BeginTransaction();
        await Database.Exec(c, "UPDATE intervals SET end=MAX(start,$at),end_known=0 WHERE end IS NULL", ct, ("$at", Database.Ms(checkpoint)));
        transaction.Commit();
    }
    private static async Task<PresenceInterval?> OpenInterval(SqliteConnection c, Guid targetId, CancellationToken ct)
    {
        using var cmd = Database.Command(c, "SELECT * FROM intervals WHERE target_id=$target AND end IS NULL", ("$target", targetId.ToString()));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadInterval(r) : null;
    }
    private static PresenceInterval ReadInterval(SqliteDataReader r) => new(r.GetInt64(0), Guid.Parse(r.GetString(1)),
        Enum.Parse<PresenceState>(r.GetString(2)), Database.At(r.GetInt64(3)), r.IsDBNull(4) ? null : Database.At(r.GetInt64(4)),
        r.GetString(5), Database.At(r.GetInt64(6)), r.IsDBNull(7) ? null : Database.At(r.GetInt64(7)), r.GetBoolean(8), r.GetBoolean(9));
}
