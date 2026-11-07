using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;

namespace TelegramCopilot.Application.Services;

public interface IMonitoringService
{
    Task<MonitoringOverviewResponse> OverviewAsync(CancellationToken ct);
    Task<TargetMonitoringResponse> TargetAsync(Guid id, CancellationToken ct);
    Task<TelegramAvatar?> AvatarAsync(Guid id, CancellationToken ct);
}
public sealed class MonitoringService(IConfigurationRepository configs, IMonitoringRepository monitoring,
    IOperationsService operations, ITelegramGateway telegram, TimeProvider clock) : IMonitoringService
{
    public async Task<MonitoringOverviewResponse> OverviewAsync(CancellationToken ct)
    {
        var totals = await monitoring.TotalsAsync(ct);
        return new(clock.GetUtcNow(), await operations.HealthAsync(ct), totals.Count,
            (await configs.ListAsync(ResourceKind.Targets, ct)).Count(x => x.Enabled), totals.LastPoll);
    }
    public async Task<TargetMonitoringResponse> TargetAsync(Guid id, CancellationToken ct) =>
        await monitoring.GetAsync(await Required(id, ct), clock.GetUtcNow(), ct);
    public async Task<TelegramAvatar?> AvatarAsync(Guid id, CancellationToken ct) =>
        await telegram.AvatarAsync(await Required(id, ct), ct);
    private async Task<Resource> Required(Guid id, CancellationToken ct) =>
        await configs.FindAsync(id, ct) is { Kind: ResourceKind.Targets } target ? target : throw new KeyNotFoundException("Target not found.");
}
