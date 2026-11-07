using System.Text.Json;
using System.Text.Json.Nodes;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;

namespace TelegramCopilot.Application.Services;

public interface IConfigurationService
{
    Task<PageResponse<ConfigurationResponse>> ListAsync(ResourceKind kind, string? search, int page, int pageSize, CancellationToken ct);
    Task<ConfigurationResponse> GetAsync(ResourceKind kind, Guid id, CancellationToken ct);
    Task<ConfigurationResponse> SaveAsync(ResourceKind kind, Guid? id, SaveConfigurationRequest request, CancellationToken ct);
    Task DeleteAsync(ResourceKind kind, Guid id, long revision, CancellationToken ct);
}
public sealed class ConfigurationService(IConfigurationRepository repository, TimeProvider clock) : IConfigurationService
{
    private readonly SemaphoreSlim _gate = new(1);
    public async Task<PageResponse<ConfigurationResponse>> ListAsync(ResourceKind kind, string? search, int page, int pageSize, CancellationToken ct)
    {
        QueryPolicy.Page(page, pageSize);
        var values = (await repository.ListAsync(kind, ct)).Where(x => search is null
            || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArray();
        return new(values.Skip((page - 1) * pageSize).Take(pageSize).Select(Response).ToArray(), values.Length, page, pageSize);
    }
    public async Task<ConfigurationResponse> GetAsync(ResourceKind kind, Guid id, CancellationToken ct) => Response(await Required(kind, id, ct));
    public async Task<ConfigurationResponse> SaveAsync(ResourceKind kind, Guid? id, SaveConfigurationRequest request, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var old = id is { } key ? await Required(kind, key, ct) : null;
            if (old is not null && request.Revision != old.Revision) throw new InvalidOperationException("Revision conflict. Reload before saving.");
            if (kind == ResourceKind.Settings && old is null && (await repository.ListAsync(kind, ct)).Count > 0)
                throw new InvalidOperationException("Only one system settings resource is allowed.");
            var now = clock.GetUtcNow();
            var settings = Secrets(request.Settings, kind, old?.Settings, false);
            var value = new Resource(old?.Id ?? Guid.NewGuid(), kind, request.Name.Trim(), request.Enabled,
                settings, (old?.Revision ?? 0) + 1, old?.CreatedAt ?? now, now);
            ConfigurationPolicy.Validate(value);
            foreach (var (refKind, refId) in ConfigurationPolicy.References(value)) await Required(refKind, refId, ct);
            await repository.SaveAsync(value, old?.Revision, ct);
            return Response(value);
        }
        finally { _gate.Release(); }
    }
    public async Task DeleteAsync(ResourceKind kind, Guid id, long revision, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await Required(kind, id, ct);
            if ((await repository.ListAsync(null, ct)).Any(r => ConfigurationPolicy.References(r).Any(x => x.Id == id)))
                throw new InvalidOperationException("Resource is still referenced. Remove dependent configuration first.");
            await repository.DeleteAsync(id, revision, ct);
        }
        finally { _gate.Release(); }
    }
    private async Task<Resource> Required(ResourceKind kind, Guid id, CancellationToken ct)
    {
        var r = await repository.FindAsync(id, ct);
        return r is not null && r.Kind == kind ? r : throw new KeyNotFoundException("Configuration resource not found.");
    }
    private static ConfigurationResponse Response(Resource r) => new(r.Id, r.Kind.ToString(), r.Name, r.Enabled,
        Secrets(r.Settings, r.Kind, null, true), r.Revision, r.CreatedAt, r.UpdatedAt);
    private static JsonElement Secrets(JsonElement source, ResourceKind kind, JsonElement? previous, bool redact)
    {
        var node = JsonNode.Parse(source.GetRawText()) as JsonObject ?? throw new ArgumentException("Settings must be an object.");
        var old = previous is { } p ? JsonNode.Parse(p.GetRawText()) : null;
        var paths = kind switch
        {
            ResourceKind.Accounts => new[] { "apiHash" },
            ResourceKind.Destinations => ["deviceKeys", "encryption.key", "encryption.iv"],
            _ => []
        };
        foreach (var path in paths)
        {
            var keys = path.Split('.');
            JsonNode? parent = node; JsonNode? oldParent = old;
            foreach (var key in keys[..^1]) { parent = parent?[key]; oldParent = oldParent?[key]; }
            var leaf = keys[^1];
            if (parent?[leaf] is not { } field) continue;
            var masked = leaf == "deviceKeys" ? JsonNode.Parse("[\"***\"]") : JsonValue.Create("***");
            if (redact) parent[leaf] = masked;
            else if (JsonNode.DeepEquals(field, masked))
                parent[leaf] = oldParent?[leaf]?.DeepClone() ?? throw new ArgumentException("Supply secret values when creating a resource.");
        }
        return JsonSerializer.SerializeToElement(node, JsonDefaults.Options);
    }
}
