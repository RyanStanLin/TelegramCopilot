using Microsoft.Data.Sqlite;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Services;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Infrastructure.Persistence;

public sealed partial class TelemetryRepository
{
    public async Task<long> EventAsync(MonitorEvent value, CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        return await InsertEvent(c, value, ct);
    }
    private static async Task<long> InsertEvent(SqliteConnection c, MonitorEvent value, CancellationToken ct)
    {
        using var cmd = Database.Command(c, """
            INSERT INTO events(target_id,account_id,name,type,at,online_seconds,detail,confirmed_offline)
            VALUES($target,$account,$name,$type,$at,$seconds,$detail,$confirmed); SELECT last_insert_rowid();
            """, ("$target", value.TargetId?.ToString()), ("$account", value.AccountId?.ToString()),
            ("$name", value.Name), ("$type", value.Type.ToString()), ("$at", Database.Ms(value.At)),
            ("$seconds", value.OnlineSeconds), ("$detail", value.Detail), ("$confirmed", value.ConfirmedOnlineToOffline));
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }
    public async Task<PageResponse<EventResponse>> EventsAsync(Guid? targetId, string? type, DateTimeOffset from,
        DateTimeOffset to, int page, int pageSize, CancellationToken ct)
    {
        QueryPolicy.Page(page, pageSize); QueryPolicy.Range(from, to);
        if (type is not null && (!Enum.TryParse<EventType>(type, true, out var e) || !Enum.IsDefined(e))) throw new ArgumentException("Unknown event type.");
        await using var c = await db.Open(ct);
        const string where = " WHERE at>=$from AND at<$to AND ($target IS NULL OR target_id=$target) AND ($type IS NULL OR type=$type) ";
        var parameters = new (string, object?)[] { ("$from", Database.Ms(from)), ("$to", Database.Ms(to)),
            ("$target", targetId?.ToString()), ("$type", type is null ? null : Enum.Parse<EventType>(type, true).ToString()) };
        using var count = Database.Command(c, "SELECT COUNT(*) FROM events" + where, parameters);
        var total = (long)(await count.ExecuteScalarAsync(ct))!;
        using var cmd = Database.Command(c, "SELECT * FROM events" + where + "ORDER BY at DESC,id DESC LIMIT $limit OFFSET $offset",
            [..parameters, ("$limit", pageSize), ("$offset", (page - 1) * pageSize)]);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var values = new List<EventResponse>();
        while (await r.ReadAsync(ct))
        {
            var v = ReadEvent(r);
            values.Add(new(v.Id, v.TargetId, v.AccountId, v.Name, v.Type.ToString(), v.At, v.OnlineSeconds, v.Detail));
        }
        return new(values, total, page, pageSize);
    }
    public async Task<MonitorEvent?> NextNotificationEventAsync(CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        using var cmd = Database.Command(c, "SELECT * FROM events WHERE notified=0 ORDER BY id LIMIT 1");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadEvent(r) : null;
    }
    public async Task CompleteNotificationEventAsync(long id, CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        await Database.Exec(c, "UPDATE events SET notified=1 WHERE id=$id", ct, ("$id", id));
    }
    private static MonitorEvent ReadEvent(SqliteDataReader r) => new(r.GetInt64(0), r.IsDBNull(1) ? null : Guid.Parse(r.GetString(1)),
        r.IsDBNull(2) ? null : Guid.Parse(r.GetString(2)), r.GetString(3), Enum.Parse<EventType>(r.GetString(4)),
        Database.At(r.GetInt64(5)), r.IsDBNull(6) ? null : r.GetDouble(6), r.GetString(7), r.GetBoolean(9));
}
