using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;

namespace TelegramCopilot.Infrastructure.Persistence;

public sealed class ConfigurationRepository(Database db, IDataProtectionProvider protection) : IConfigurationRepository
{
    private readonly IDataProtector _protector = protection.CreateProtector("TelegramCopilot.Configuration.v1");
    public async Task<IReadOnlyList<Resource>> ListAsync(ResourceKind? kind, CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        using var cmd = Database.Command(c, "SELECT body FROM resources WHERE deleted=0 AND ($kind IS NULL OR kind=$kind)", ("$kind", kind?.ToString()));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<Resource>();
        while (await r.ReadAsync(ct)) rows.Add(Read(r.GetString(0)));
        return rows;
    }
    public async Task<Resource?> FindAsync(Guid id, CancellationToken ct, bool includeDeleted = false)
    {
        await using var c = await db.Open(ct);
        using var cmd = Database.Command(c, "SELECT body FROM resources WHERE id=$id AND ($include=1 OR deleted=0)", ("$id", id.ToString()), ("$include", includeDeleted));
        return await cmd.ExecuteScalarAsync(ct) is string s ? Read(s) : null;
    }
    public async Task SaveAsync(Resource value, long? previousRevision, CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        var body = _protector.Protect(JsonSerializer.Serialize(value, JsonDefaults.Options));
        var count = previousRevision is null
            ? await Database.Exec(c, "INSERT INTO resources(id,kind,body,revision) VALUES($id,$kind,$body,$revision)", ct,
                ("$id", value.Id.ToString()), ("$kind", value.Kind.ToString()), ("$body", body), ("$revision", value.Revision))
            : await Database.Exec(c, "UPDATE resources SET body=$body,revision=$revision WHERE id=$id AND revision=$old AND deleted=0", ct,
                ("$id", value.Id.ToString()), ("$body", body), ("$revision", value.Revision), ("$old", previousRevision));
        if (count == 0) throw new InvalidOperationException("Revision conflict. Reload before saving.");
    }
    public async Task DeleteAsync(Guid id, long revision, DateTimeOffset at, CancellationToken ct)
    {
        await using var c = await db.Open(ct);
        var resource = await FindAsync(id, ct) ?? throw new KeyNotFoundException("Configuration resource not found.");
        var tombstone = _protector.Protect(JsonSerializer.Serialize(resource with { Enabled = false, DeletedAt = at, UpdatedAt = at }, JsonDefaults.Options));
        if (await Database.Exec(c, "UPDATE resources SET deleted=1,body=$body WHERE id=$id AND revision=$revision AND deleted=0", ct,
            ("$id", id.ToString()), ("$revision", revision), ("$body", tombstone)) == 0) throw new InvalidOperationException("Revision conflict. Reload before deleting.");
    }
    private Resource Read(string s) => JsonSerializer.Deserialize<Resource>(_protector.Unprotect(s), JsonDefaults.Options)!;
}
