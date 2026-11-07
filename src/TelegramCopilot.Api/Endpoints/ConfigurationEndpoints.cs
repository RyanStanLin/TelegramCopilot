using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Services;
using TelegramCopilot.Core.Configuration;

namespace TelegramCopilot.Api.Endpoints;

public static class ConfigurationEndpoints
{
    public static void MapConfigurations(this WebApplication app)
    {
        foreach (var kind in Enum.GetValues<ResourceKind>())
        {
            var group = app.MapGroup("/api/" + kind.ToString().ToLowerInvariant()).WithTags(kind.ToString());
            group.MapGet("/", async (IConfigurationService service, CancellationToken ct, string? search = null, int page = 1, int pageSize = 100) =>
                TypedResults.Ok(await service.ListAsync(kind, search, page, pageSize, ct)))
                .WithName($"List{kind}").WithSummary($"List {kind} with search and bounded pagination");
            group.MapGet("/{id:guid}", async (Guid id, IConfigurationService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetAsync(kind, id, ct)))
                .WithName($"Get{kind}").WithSummary($"Read one {kind} resource with secret redaction");
            group.MapPost("/", async (SaveConfigurationRequest request, IConfigurationService service, CancellationToken ct) =>
            {
                var value = await service.SaveAsync(kind, null, request, ct);
                return TypedResults.Created($"/api/{kind.ToString().ToLowerInvariant()}/{value.Id}", value);
            }).WithName($"Create{kind}").WithSummary($"Create a validated {kind} resource");
            group.MapPut("/{id:guid}", async (Guid id, SaveConfigurationRequest request, IConfigurationService service, CancellationToken ct) =>
                TypedResults.Ok(await service.SaveAsync(kind, id, request, ct)))
                .WithName($"Update{kind}").WithSummary($"Replace {kind} settings with revision conflict checks");
            group.MapDelete("/{id:guid}", async (Guid id, long revision, IConfigurationService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(kind, id, revision, ct);
                return TypedResults.NoContent();
            }).WithName($"Delete{kind}").WithSummary($"Delete {kind} configuration; retain telemetry history");
        }
    }
}
