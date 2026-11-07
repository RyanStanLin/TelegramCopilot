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
            var decision = new PresenceMonitor(o.TargetId, previous).Observe(o);
            if (decision.Effect == ObservationEffect.Ignore) return;
            if (decision.Effect == ObservationEffect.Confirm)
            {
                await Database.Exec(c, "UPDATE intervals SET observed=$at WHERE id=$id", ct,
                    ("$at", Database.Ms(o.ObservedAt)), ("$id", previous!.Id));
                transaction.Commit();
                return;
            }
            var boundary = o.ObservedAt;
            var transitionKnown = decision.BoundaryKnown;
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
            await InsertEvent(c, new(0, o.TargetId, accountId, name, decision.Event, o.ObservedAt, decision.OnlineSeconds, o.Reason), ct);
            if (decision.Restored)
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
        var open = new List<(string Target, long End)>();
        using (var command = Database.Command(c, "SELECT target_id,MAX(start,$at) FROM intervals WHERE end IS NULL", ("$at", Database.Ms(checkpoint))))
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) open.Add((reader.GetString(0), reader.GetInt64(1)));
        }
        await Database.Exec(c, "UPDATE intervals SET end=MAX(start,$at),end_known=0 WHERE end IS NULL", ct, ("$at", Database.Ms(checkpoint)));
        foreach (var item in open)
            await Database.Exec(c, """
                INSERT INTO intervals(target_id,state,start,reason,observed,start_known)
                VALUES($target,'Unknown',$start,'WorkerUnavailable',$now,0)
                """, ct, ("$target", item.Target), ("$start", item.End), ("$now", Database.Ms(now)));
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
