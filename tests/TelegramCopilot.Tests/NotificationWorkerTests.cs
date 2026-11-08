using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;
using TelegramCopilot.Infrastructure.Workers;

namespace TelegramCopilot.Tests;

public sealed class NotificationWorkerTests
{
    [Fact]
    public async Task EventRules_FilterTargetAndTypeAndPersistEachDeviceOutcome()
    {
        await using var s = new TestStore(); await s.Initialize(); var account = await s.Account(); var target = await s.Target(account.Id);
        var destination = await s.Configuration.SaveAsync(ResourceKind.Destinations, null,
            TestStore.Request("Bark", new DestinationSettings("https://api.day.app", ["first-device", "second-device"])), default);
        var rule = await s.Configuration.SaveAsync(ResourceKind.Rules, null,
            TestStore.Request("Offline rule", new RuleSettings(destination.Id, EventType.Offline, target.Id, "{NAME}", "{TIME_OF_ONLINE}")), default);
        await s.Telemetry.EventAsync(new(0, target.Id, account.Id, "Alice", EventType.Online, s.Clock.Now, null, "Push"), default);
        await s.Telemetry.EventAsync(new(0, Guid.NewGuid(), account.Id, "Other", EventType.Offline, s.Clock.Now, 5, "Push", true), default);
        await s.Telemetry.EventAsync(new(0, target.Id, account.Id, "Alice", EventType.Offline, s.Clock.Now, 65, "Push", true), default);
        var bark = new DeviceGateway();
        using var worker = new NotificationWorker(s.Configs, s.Telemetry, s.Deliveries, bark, s.Clock, NullLogger<NotificationWorker>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await worker.StartAsync(timeout.Token);
        try
        {
            while (await s.Telemetry.NextNotificationEventAsync(default) is not null) await Task.Delay(10, timeout.Token);
        }
        finally { await worker.StopAsync(CancellationToken.None); }
        Assert.Equal(new[] { "first-device", "second-device" }, bark.Devices.ToArray());
        Assert.All(bark.Events, value =>
        {
            Assert.Equal(target.Id, value.TargetId);
            Assert.Equal(EventType.Offline, value.Type);
            Assert.Equal(65, value.OnlineSeconds);
        });
        var deliveries = (await s.Deliveries.ListAsync("Bark", rule.Id, 1, 100, default)).Items;
        Assert.Equal(2, deliveries.Count);
        Assert.Equal(2, deliveries.Select(value => value.Id).Distinct().Count());
        Assert.Single(deliveries, value => value.Status == "Sent");
        Assert.Equal(nameof(InvalidOperationException), Assert.Single(deliveries, value => value.Status == "Failed").Error);
    }

    private sealed class DeviceGateway : IBarkGateway
    {
        public ConcurrentQueue<string> Devices { get; } = new();
        public ConcurrentQueue<MonitorEvent> Events { get; } = new();
        public Task SendAsync(DestinationSettings destination, RuleSettings rule, MonitorEvent value, CancellationToken ct)
        {
            var key = Assert.Single(destination.DeviceKeys);
            Devices.Enqueue(key); Events.Enqueue(value);
            if (key == "second-device") throw new InvalidOperationException("Fixture failure");
            return Task.CompletedTask;
        }
    }
}
