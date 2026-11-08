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
    IOperationsService operations, ITelegramGateway telegram, ITelemetryService telemetry, TimeProvider clock) : IMonitoringService
{
    public async Task<MonitoringOverviewResponse> OverviewAsync(CancellationToken ct)
    {
        var totals = await monitoring.TotalsAsync(ct);
        return new(clock.GetUtcNow(), await operations.HealthAsync(ct), totals.Count,
            (await configs.ListAsync(ResourceKind.Targets, ct)).Count(x => x.Enabled), totals.LastPoll, totals.Successful, totals.Failed);
    }
    public async Task<TargetMonitoringResponse> TargetAsync(Guid id, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var snapshot = await monitoring.GetAsync(await Required(id, ct), now, ct);
        var zoneName = (await configs.ListAsync(ResourceKind.Settings, ct)).FirstOrDefault(x => x.Enabled)?.Read<SystemSettings>().TimeZone ?? "Asia/Taipei";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneName);
        var date = TimeZoneInfo.ConvertTime(now, zone).Date;
        var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        return snapshot with { Activity = new(zoneName, await Totals(date), await Totals(monday)) };

        async Task<StatisticsBucket> Totals(DateTime start)
        {
            while (zone.IsInvalidTime(start)) start = start.AddMinutes(1);
            var offset = zone.IsAmbiguousTime(start) ? zone.GetAmbiguousTimeOffsets(start).Max() : zone.GetUtcOffset(start);
            var from = new DateTimeOffset(start, offset).ToUniversalTime();
            return from >= now ? new(now, now, 0, 0, 0, 0, null, 0)
                : (await telemetry.StatisticsAsync(id, new(from, now), "week", zoneName, ct)).Totals;
        }
    }
    public async Task<TelegramAvatar?> AvatarAsync(Guid id, CancellationToken ct) =>
        await telegram.AvatarAsync(await Required(id, ct), ct);
    private async Task<Resource> Required(Guid id, CancellationToken ct) =>
        await configs.FindAsync(id, ct) is { Kind: ResourceKind.Targets } target ? target : throw new KeyNotFoundException("Target not found.");
}
