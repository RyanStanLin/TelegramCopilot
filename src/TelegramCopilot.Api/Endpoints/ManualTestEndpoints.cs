using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Services;

namespace TelegramCopilot.Api.Endpoints;

public static class ManualTestEndpoints
{
    public static void MapManualTests(this WebApplication app)
    {
        app.MapPost("/api/rules/validate", async (SaveConfigurationRequest request, IRuleValidationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ValidateAsync(request, ct)))
            .WithName("ValidateRule").WithSummary("Validate an unsaved rule draft and preview a simulated notification without sending");
        app.MapPost("/api/schedules/{id:guid}/test", async (Guid id, ManualTestRequest request, IManualTestService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ScheduleAsync(id, request, ct)))
            .WithName("TestSchedule").WithSummary("Send the saved scheduled message immediately, with deduplication and delivery audit; recurring due time is unchanged");
        app.MapPost("/api/rules/{id:guid}/test", async (Guid id, ManualTestRequest request, IManualTestService service, CancellationToken ct) =>
            TypedResults.Ok(await service.RuleAsync(id, request, ct)))
            .WithName("TestRule").WithSummary("Validate and send a simulated event through this saved rule only, with per-device audit and deduplication");
    }
}
