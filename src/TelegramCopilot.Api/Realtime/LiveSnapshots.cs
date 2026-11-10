using TelegramCopilot.Application.Services;

namespace TelegramCopilot.Api.Realtime;

public sealed class LiveSnapshots(IMonitoringService monitoring, IOperationsService operations,
    ITelemetryService telemetry)
{
    public async Task<object> ReadAsync(string path, CancellationToken ct)
    {
        if (path == "/monitoring") return await monitoring.OverviewAsync(ct);
        var route = path.Split('?', 2);
        var parts = route[0].Split('/', StringSplitOptions.None);
        if (parts.Length == 4 && parts[0] == "" && Guid.TryParse(parts[2], out var id))
        {
            if (route.Length == 1 && parts[1] == "targets" && parts[3] == "monitoring") return await monitoring.TargetAsync(id, ct);
            if (route.Length == 1 && parts[1] == "accounts" && parts[3] == "status") return await operations.AccountAsync(id, ct);
            if (route.Length == 2 && parts[1] == "targets" && parts[3] is "timeline" or "statistics")
                return await LiveAnalytics.ReadAsync(telemetry, id, parts[3], route[1], ct);
        }
        throw new ArgumentException("Invalid subscription.");
    }
}
