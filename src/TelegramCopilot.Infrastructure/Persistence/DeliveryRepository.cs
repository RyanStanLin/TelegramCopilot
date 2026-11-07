using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Application.Services;

namespace TelegramCopilot.Infrastructure.Persistence;

public sealed class DeliveryRepository(Database db) : IDeliveryRepository
{
    public async Task<DeliveryResponse?> FindAsync(string id, CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        using var command = Database.Command(c, "SELECT * FROM deliveries WHERE id=$id", ("$id", id));
        await using var r = await command.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new(r.GetString(0), r.GetString(1), Guid.Parse(r.GetString(2)), Database.At(r.GetInt64(3)),
            r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), Database.At(r.GetInt64(7))) : null;
    }
    public async Task<bool> ClaimAsync(DeliveryResponse d, CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        return await Database.Exec(c, """
            INSERT OR IGNORE INTO deliveries(id,channel,resource_id,due_at,status,updated_at)
            VALUES($id,$channel,$resource,$due,'Sending',$at)
            """, ct, ("$id", d.Id), ("$channel", d.Channel), ("$resource", d.ResourceId.ToString()),
            ("$due", Database.Ms(d.DueAt)), ("$at", Database.Ms(d.UpdatedAt))) == 1;
    }
    public async Task CompleteAsync(string id, string status, string? externalId, string? error, DateTimeOffset at, CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        await Database.Exec(c, "UPDATE deliveries SET status=$status,external_id=$external,error=$error,updated_at=$at WHERE id=$id", ct,
            ("$id", id), ("$status", status), ("$external", externalId), ("$error", error), ("$at", Database.Ms(at)));
    }
    public async Task RecoverAsync(DateTimeOffset at, CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        await Database.Exec(c, "UPDATE deliveries SET status='Uncertain',error='Worker interrupted during send',updated_at=$at WHERE status='Sending'", ct,
            ("$at", Database.Ms(at)));
    }
    public async Task<PageResponse<DeliveryResponse>> ListAsync(string? channel, Guid? resourceId, int page, int pageSize, CancellationToken ct)
    {
        QueryPolicy.Page(page, pageSize);
        await using var c = await db.Open(ct);
        const string where = " WHERE ($channel IS NULL OR channel=$channel) AND ($resource IS NULL OR resource_id=$resource) ";
        var parameters = new (string, object?)[] { ("$channel", channel), ("$resource", resourceId?.ToString()) };
        using var count = Database.Command(c, "SELECT COUNT(*) FROM deliveries" + where, parameters);
        var total = (long)(await count.ExecuteScalarAsync(ct))!;
        using var cmd = Database.Command(c, "SELECT * FROM deliveries" + where + "ORDER BY updated_at DESC,id LIMIT $limit OFFSET $offset",
            [..parameters, ("$limit", pageSize), ("$offset", (page - 1) * pageSize)]);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<DeliveryResponse>();
        while (await r.ReadAsync(ct)) rows.Add(new(r.GetString(0), r.GetString(1), Guid.Parse(r.GetString(2)), Database.At(r.GetInt64(3)),
            r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), Database.At(r.GetInt64(7))));
        return new(rows, total, page, pageSize);
    }
}
