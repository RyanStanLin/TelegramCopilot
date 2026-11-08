using Microsoft.AspNetCore.Http.HttpResults;
using TelegramCopilot.Application.Services;

namespace TelegramCopilot.Api.Endpoints;

public static class MonitoringEndpoints
{
    public static void MapMonitoring(this WebApplication app)
    {
        app.MapGet("/api/monitoring", async (IMonitoringService service, CancellationToken ct) =>
            TypedResults.Ok(await service.OverviewAsync(ct)))
            .WithName("MonitoringOverview").WithSummary("Read durable background poll counters and worker liveness without triggering a poll");
        app.MapGet("/api/targets/{id:guid}/monitoring", async (Guid id, IMonitoringService service, CancellationToken ct) =>
            TypedResults.Ok(await service.TargetAsync(id, ct)))
            .WithName("TargetMonitoring").WithSummary("Read poll history, Telegram profile, native last-seen and calendar day/week activity");
        app.MapGet("/api/targets/{id:guid}/avatar", async Task<Results<FileContentHttpResult, NotFound>>
            (Guid id, IMonitoringService service, CancellationToken ct) =>
                await service.AvatarAsync(id, ct) is { } avatar ? TypedResults.File(avatar.Content, avatar.ContentType) : TypedResults.NotFound())
            .WithName("TargetAvatar").WithSummary("Read the target's current Telegram avatar through its configured account");
    }
}
