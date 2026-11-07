using System.Text;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Services;

namespace TelegramCopilot.Api.Endpoints;

public static class TelemetryEndpoints
{
    public static void MapTelemetry(this WebApplication app)
    {
        var group = app.MapGroup("/api/targets/{id:guid}").WithTags("Presence");
        group.MapGet("/timeline", async (Guid id, DateTimeOffset from, DateTimeOffset to, ITelemetryService service,
            CancellationToken ct, int page = 1, int pageSize = 100, string? state = null) =>
            TypedResults.Ok(await service.TimelineAsync(id, new(from, to, page, pageSize, state), ct)))
            .WithName("GetTimeline").WithSummary("Get clipped presence intervals, including unknown downtime and gaps");
        group.MapGet("/statistics", async (Guid id, DateTimeOffset from, DateTimeOffset to, ITelemetryService service,
            CancellationToken ct, string period = "day", string timeZone = "UTC", int page = 1, int pageSize = 100) =>
            TypedResults.Ok(await service.StatisticsAsync(id, new(from, to, page, pageSize), period, timeZone, ct)))
            .WithName("GetStatistics").WithSummary("Aggregate presence by calendar day, ISO week, month or year in a timezone");
        group.MapGet("/export.csv", async (Guid id, DateTimeOffset from, DateTimeOffset to, ITelemetryService service,
            CancellationToken ct, string? state = null) =>
            TypedResults.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(await service.CsvAsync(id, new(from, to, State: state), ct))).ToArray(),
                "text/csv; charset=utf-8", $"presence-{id}-{from:yyyyMMdd}.csv"))
            .WithName("ExportPresenceCsv").WithSummary("Export all matching intervals as spreadsheet-safe UTF-8 CSV");
    }
}
