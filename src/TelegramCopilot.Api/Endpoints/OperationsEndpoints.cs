using Microsoft.AspNetCore.Http.HttpResults;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Services;

namespace TelegramCopilot.Api.Endpoints;

public static class OperationsEndpoints
{
    public static void MapOperations(this WebApplication app)
    {
        app.MapGet("/health/live", (CancellationToken ct) => TypedResults.Ok(new { status = "Alive" }))
            .WithName("Liveness").WithSummary("Process liveness; does not require Telegram credentials");
        app.MapGet("/health/ready", async Task<Results<Ok<HealthResponse>, ProblemHttpResult>> (IOperationsService service, CancellationToken ct) =>
        {
            var health = await service.HealthAsync(ct);
            return health.Status == "Healthy" ? TypedResults.Ok(health)
                : TypedResults.Problem(statusCode: 503, title: "Monitoring service is not ready");
        }).WithName("Readiness").WithSummary("Requires native TDLib and a recent durable monitoring heartbeat");
        app.MapGet("/api/health", async (IOperationsService service, CancellationToken ct) => TypedResults.Ok(await service.HealthAsync(ct)))
            .WithName("Health").WithSummary("Native TDLib availability and durable worker heartbeat");
        app.MapGet("/api/accounts/{id:guid}/status", async (Guid id, IOperationsService service, CancellationToken ct) =>
            TypedResults.Ok(await service.AccountAsync(id, ct)))
            .WithName("AccountStatus").WithSummary("Inspect Telegram login challenge and connection state");
        app.MapPost("/api/accounts/{id:guid}/login", async (Guid id, LoginRequest request, IOperationsService service, CancellationToken ct) =>
            TypedResults.Ok(await service.LoginAsync(id, request, ct)))
            .WithName("SubmitLogin").WithSummary("Submit Telegram code, 2FA password, email or email code; secrets are not stored");
        app.MapGet("/api/events", async (DateTimeOffset from, DateTimeOffset to, IOperationsService service, CancellationToken ct,
            Guid? targetId = null, string? type = null, int page = 1, int pageSize = 100) =>
            TypedResults.Ok(await service.EventsAsync(targetId, type, new(from, to, page, pageSize), ct)))
            .WithName("Events").WithSummary("Query target transitions, account status, errors and worker lifecycle events");
        app.MapGet("/api/deliveries", async (IOperationsService service, CancellationToken ct, string? channel = null,
            Guid? resourceId = null, int page = 1, int pageSize = 100) =>
            TypedResults.Ok(await service.DeliveriesAsync(channel, resourceId, page, pageSize, ct)))
            .WithName("Deliveries").WithSummary("Inspect persisted Telegram and Bark delivery outcomes");
    }
}
