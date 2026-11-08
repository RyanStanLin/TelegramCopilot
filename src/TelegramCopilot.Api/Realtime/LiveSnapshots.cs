using TelegramCopilot.Application.Services;

namespace TelegramCopilot.Api.Realtime;

public sealed class LiveSnapshots(IMonitoringService monitoring, IOperationsService operations)
{
    public async Task<object> ReadAsync(string path, CancellationToken ct)
    {
        if (path == "/monitoring") return await monitoring.OverviewAsync(ct);
        var parts = path.Split('/', StringSplitOptions.None);
        if (parts.Length == 4 && parts[0] == "" && Guid.TryParse(parts[2], out var id))
        {
            if (parts[1] == "targets" && parts[3] == "monitoring") return await monitoring.TargetAsync(id, ct);
            if (parts[1] == "accounts" && parts[3] == "status") return await operations.AccountAsync(id, ct);
        }
        throw new ArgumentException("Invalid subscription.");
    }
}
