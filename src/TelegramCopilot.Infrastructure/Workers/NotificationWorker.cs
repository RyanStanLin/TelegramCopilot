using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Infrastructure.Workers;

public sealed class NotificationWorker(IConfigurationRepository configs, ITelemetryRepository telemetry,
    IDeliveryRepository deliveries, IBarkGateway bark, TimeProvider clock, ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var value = await telemetry.NextNotificationEventAsync(ct);
                if (value is null) { await Task.Delay(500, ct); continue; }
                var all = await configs.ListAsync(null, ct);
                foreach (var resource in all.Where(x => x.Kind == ResourceKind.Rules && x.Enabled && x.CreatedAt <= value.At))
                {
                    var rule = resource.Read<RuleSettings>();
                    if (rule.Event != value.Type || rule.TargetId is { } id && id != value.TargetId) continue;
                    var destination = all.FirstOrDefault(x => x.Id == rule.DestinationId && x.Kind == ResourceKind.Destinations && x.Enabled);
                    if (destination is null) continue;
                    await Dispatch(resource.Id, destination.Read<DestinationSettings>(), rule, value, ct);
                }
                await telemetry.CompleteNotificationEventAsync(value.Id, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError("Notification worker failed: {Type}", ex.GetType().Name);
                await Task.Delay(1000, ct);
            }
        }
    }
    private async Task Dispatch(Guid ruleId, DestinationSettings destination, RuleSettings rule, MonitorEvent value, CancellationToken ct)
    {
        var id = $"bark:{value.Id}:{ruleId}";
        if (!await deliveries.ClaimAsync(new(id, "Bark", ruleId, value.At, "Sending", null, null, clock.GetUtcNow()), ct)) return;
        try
        {
            await bark.SendAsync(destination, rule, value, ct);
            await deliveries.CompleteAsync(id, "Sent", null, null, clock.GetUtcNow(), ct);
        }
        catch (Exception ex)
        {
            await deliveries.CompleteAsync(id, ex is TimeoutException or OperationCanceledException ? "Uncertain" : "Failed",
                null, ex.GetType().Name, clock.GetUtcNow(), CancellationToken.None);
        }
    }
}
