using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Infrastructure.Workers;

public sealed class MonitorWorker(IConfigurationRepository configs, ITelemetryRepository telemetry,
    ITelegramGateway telegram, IObservationStream observations, TimeProvider clock, ILogger<MonitorWorker> logger) : BackgroundService
{
    private readonly Dictionary<Guid, DateTimeOffset> _next = new();
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await telemetry.EventAsync(new(0, null, null, "TelegramCopilot", EventType.WorkerStarted, clock.GetUtcNow(), null, "Worker started"), ct);
        try { await Task.WhenAll(Poll(ct), Receive(ct), Heartbeat(ct)); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            foreach (var target in await configs.ListAsync(ResourceKind.Targets, shutdown.Token))
                await telemetry.RecordAsync(new(target.Id, PresenceState.Unknown, clock.GetUtcNow(), "WorkerStopped"), target.Name,
                    target.Read<TargetSettings>().AccountId, shutdown.Token);
            await telemetry.EventAsync(new(0, null, null, "TelegramCopilot", EventType.WorkerStopped, clock.GetUtcNow(), null, "Worker stopped"), shutdown.Token);
        }
    }
    private async Task Heartbeat(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var settings = (await configs.ListAsync(ResourceKind.Settings, ct)).FirstOrDefault()?.Read<SystemSettings>() ?? new();
            await telemetry.CheckpointAsync(clock.GetUtcNow(), ct);
            await Task.Delay(TimeSpan.FromSeconds(settings.HeartbeatSeconds), clock, ct);
        }
    }
    private async Task Poll(CancellationToken ct)
    {
        var states = new Dictionary<Guid, string>();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var accounts = await configs.ListAsync(ResourceKind.Accounts, ct);
                await telegram.ReconcileAccountsAsync(accounts, ct);
                foreach (var account in accounts)
                {
                    var state = await telegram.StatusAsync(account.Id, ct);
                    var signature = $"{state.AuthorizationState}:{state.ConnectionState}";
                    if (states.TryGetValue(account.Id, out var previous) && previous == signature) continue;
                    states[account.Id] = signature;
                    var ready = state.AuthorizationState == "authorizationStateReady" && state.ConnectionState == "connectionStateReady";
                    await telemetry.EventAsync(new(0, null, account.Id, account.Name, ready ? EventType.AccountReady : EventType.AccountUnavailable,
                        clock.GetUtcNow(), null, signature), ct);
                }
                var targets = await configs.ListAsync(ResourceKind.Targets, ct);
                await Parallel.ForEachAsync(targets, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (target, token) =>
                {
                    var settings = target.Read<TargetSettings>();
                    var now = clock.GetUtcNow();
                    lock (_next)
                    {
                        if (target.Enabled && _next.TryGetValue(target.Id, out var next) && next > now && target.UpdatedAt < next.AddSeconds(-settings.RefreshSeconds)) return;
                        _next[target.Id] = now.AddSeconds(settings.RefreshSeconds);
                    }
                    try
                    {
                        var result = target.Enabled ? await telegram.ObserveAsync(target, token)
                            : new Observation(target.Id, PresenceState.Unknown, now, "MonitoringDisabled");
                        observations.Publish(result with { SourceAccountId = settings.AccountId, SourceIdentifier = settings.Identifier });
                        if (result.Reason == "TargetReconfigured") lock (_next) _next[target.Id] = now.AddSeconds(1);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        observations.Publish(new(target.Id, PresenceState.Unknown, clock.GetUtcNow(), "ObservationFailed",
                            SourceAccountId: settings.AccountId, SourceIdentifier: settings.Identifier));
                        await telemetry.EventAsync(new(0, target.Id, settings.AccountId, target.Name, EventType.Error, clock.GetUtcNow(), null,
                            ex is Telegram.TelegramException te ? $"Telegram error {te.Code}" : ex.GetType().Name), token);
                    }
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError("Monitor cycle failed: {Error}", ex.GetType().Name);
                await telemetry.EventAsync(new(0, null, null, "TelegramCopilot", EventType.Error, clock.GetUtcNow(), null, ex.GetType().Name), ct);
            }
            await Task.Delay(TimeSpan.FromSeconds(1), clock, ct);
        }
    }
    private async Task Receive(CancellationToken ct)
    {
        await foreach (var observation in observations.ReadAllAsync(ct))
        {
            var target = await configs.FindAsync(observation.TargetId, ct);
            if (target is null || observation.ObservedAt < target.UpdatedAt
                || !target.Enabled && observation.Reason != "MonitoringDisabled") continue;
            var settings = target.Read<TargetSettings>();
            if (observation.SourceAccountId is { } account && account != settings.AccountId) continue;
            if (observation.SourceIdentifier is { } identifier && identifier != settings.Identifier) continue;
            await telemetry.RecordAsync(observation, target.Name, settings.AccountId, ct);
        }
    }
}
