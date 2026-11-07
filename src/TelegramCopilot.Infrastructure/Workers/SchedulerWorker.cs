using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;
using TelegramCopilot.Core.Scheduling;

namespace TelegramCopilot.Infrastructure.Workers;

public sealed class SchedulerWorker(IConfigurationRepository configs, IDeliveryRepository deliveries,
    ITelegramGateway telegram, ITelemetryRepository telemetry, TimeProvider clock, ILogger<SchedulerWorker> logger) : BackgroundService
{
    private readonly Dictionary<Guid, (long Revision, DateTimeOffset Due)> _next = new();
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var started = clock.GetUtcNow();
        var sending = new List<Task>();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                sending.RemoveAll(x => x.IsCompletedSuccessfully);
                var now = clock.GetUtcNow();
                var settings = (await configs.ListAsync(ResourceKind.Settings, ct)).FirstOrDefault(x => x.Enabled)?.Read<SystemSettings>() ?? new();
                foreach (var schedule in (await configs.ListAsync(ResourceKind.Schedules, ct)).Where(x => x.Enabled))
                {
                    var s = schedule.Read<ScheduleSettings>();
                    if (!_next.TryGetValue(schedule.Id, out var next) || next.Revision != schedule.Revision)
                    {
                        var earliest = now.AddSeconds(-settings.SchedulerGraceSeconds);
                        if (earliest < started) earliest = started;
                        if (earliest < schedule.UpdatedAt) earliest = schedule.UpdatedAt;
                        next = (schedule.Revision, s.NextAfter(earliest.AddMilliseconds(-1)));
                    }
                    if (next.Due <= now)
                    {
                        var late = (now - next.Due).TotalSeconds > settings.SchedulerGraceSeconds;
                        sending.Add(Dispatch(schedule, s, next.Due, late, ct));
                        next = (schedule.Revision, s.NextAfter(now));
                    }
                    _next[schedule.Id] = next;
                }
                await Task.Delay(TimeSpan.FromSeconds(1), clock, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { await Task.WhenAll(sending); }
    }
    private async Task Dispatch(Resource resource, ScheduleSettings settings, DateTimeOffset due, bool late, CancellationToken ct)
    {
        var id = $"schedule:{resource.Id}:{due.ToUnixTimeSeconds()}";
        try
        {
            if (!await deliveries.ClaimAsync(new(id, "Telegram", resource.Id, due, "Sending", null, null, clock.GetUtcNow()), ct)) return;
            if (late)
            {
                await deliveries.CompleteAsync(id, "Skipped", null, "Outside scheduler grace period", clock.GetUtcNow(), ct);
                return;
            }
            var receipt = await telegram.SendAsync(settings.AccountId, settings.Recipient, settings.Message, ct);
            await deliveries.CompleteAsync(id, receipt.Status, receipt.MessageId, null, clock.GetUtcNow(), ct);
            await telemetry.EventAsync(new(0, null, settings.AccountId, resource.Name, EventType.MessageSent,
                clock.GetUtcNow(), null, id), ct);
        }
        catch (Exception ex)
        {
            var uncertain = ex is TimeoutException or OperationCanceledException;
            await deliveries.CompleteAsync(id, uncertain ? "Uncertain" : "Failed", null, ex.GetType().Name, clock.GetUtcNow(), CancellationToken.None);
            await telemetry.EventAsync(new(0, null, settings.AccountId, resource.Name, EventType.MessageFailed, clock.GetUtcNow(), null,
                $"{id}: {ex.GetType().Name}"), CancellationToken.None);
            logger.LogWarning("Scheduled delivery {Id} failed: {Type}", id, ex.GetType().Name);
        }
    }
}
