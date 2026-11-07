using Microsoft.Data.Sqlite;

namespace TelegramCopilot.Infrastructure.Persistence;

public sealed partial class TelemetryRepository
{
    public async Task RecoverAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        var checkpoint = Database.Ms(await HeartbeatAsync(ct) ?? now);
        using var transaction = c.BeginTransaction();
        var open = new List<(string Target, long End, long Unavailable)>();
        using (var command = Database.Command(c, "SELECT target_id,start,CASE WHEN state='Online' THEN expires_at ELSE NULL END FROM intervals WHERE end IS NULL"))
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var start = reader.GetInt64(1);
                var unavailable = Math.Max(start, checkpoint);
                var end = reader.IsDBNull(2) ? unavailable : Math.Max(start, Math.Min(unavailable, reader.GetInt64(2)));
                open.Add((reader.GetString(0), end, unavailable));
            }
        }
        foreach (var item in open)
        {
            await Database.Exec(c, "UPDATE intervals SET end=$end,end_known=0 WHERE target_id=$target AND end IS NULL", ct,
                ("$end", item.End), ("$target", item.Target));
            if (item.End < item.Unavailable)
                await InsertUnknown(c, item.Target, item.End, item.Unavailable, Database.Ms(now), "ExpiredOnlineStatus", ct);
            await InsertUnknown(c, item.Target, item.Unavailable, null, Database.Ms(now), "WorkerUnavailable", ct);
        }
        transaction.Commit();
    }
    private static Task<int> InsertUnknown(SqliteConnection connection, string target, long start, long? end,
        long observed, string reason, CancellationToken ct) => Database.Exec(connection, """
            INSERT INTO intervals(target_id,state,start,end,reason,observed,start_known)
            VALUES($target,'Unknown',$start,$end,$reason,$observed,0)
            """, ct, ("$target", target), ("$start", start), ("$end", end), ("$reason", reason), ("$observed", observed));
}
