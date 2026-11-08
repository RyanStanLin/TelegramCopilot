using Microsoft.Extensions.Logging.Abstractions;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;
using TelegramCopilot.Infrastructure.Persistence;
using TelegramCopilot.Infrastructure.Workers;

namespace TelegramCopilot.Tests;

public sealed class OfflineNotificationTests
{
    [Fact]
    public async Task OfflineNotifications_RequireUninterruptedOnlineToOfflineAcrossRestarts()
    {
        await using var s = new TestStore(); await s.Initialize(); var account = await s.Account(); var target = await s.Target(account.Id);
        var destination = await s.Configuration.SaveAsync(ResourceKind.Destinations, null,
            TestStore.Request("Bark", new DestinationSettings("https://api.day.app", ["test-device"])), default);
        await s.Configuration.SaveAsync(ResourceKind.Rules, null,
            TestStore.Request("Offline", new RuleSettings(destination.Id, EventType.Offline)), default);
        var at = s.Clock.Now;
        await Observe(PresenceState.Offline, 0);
        await Observe(PresenceState.Online, 1);
        await Observe(PresenceState.Offline, 2);
        await Observe(PresenceState.Offline, 3);
        await Observe(PresenceState.Online, 4);
        await Observe(PresenceState.Unknown, 5);
        await Observe(PresenceState.Offline, 6);
        await s.Telemetry.RecordAsync(new(target.Id, PresenceState.Online, at.AddSeconds(7), "Push", ExpiresAt: at.AddSeconds(8)), "Alice", account.Id, default);
        await Observe(PresenceState.Offline, 9);
        await Observe(PresenceState.Online, 10);
        await s.Telemetry.CheckpointAsync(at.AddSeconds(11), default);
        await s.Telemetry.RecoverAsync(at.AddSeconds(12), default);
        await Observe(PresenceState.Offline, 13);
        await s.Telemetry.EventAsync(new(0, target.Id, account.Id, "Legacy", EventType.Offline, at.AddSeconds(20), 65, "Push"), default);
        await s.Initialize();
        var repository = new TelemetryRepository(s.Database); var bark = new CaptureBark();
        using var worker = new NotificationWorker(s.Configs, repository, s.Deliveries, bark, s.Clock, NullLogger<NotificationWorker>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await worker.StartAsync(timeout.Token);
        try { while (await repository.NextNotificationEventAsync(default) is not null) await Task.Delay(10, timeout.Token); }
        finally { await worker.StopAsync(CancellationToken.None); }
        var sent = Assert.Single(bark.Events);
        Assert.Equal(at.AddSeconds(2), sent.At); Assert.True(sent.ConfirmedOnlineToOffline); Assert.Equal(1, sent.OnlineSeconds);
        Assert.Equal(6, (await repository.EventsAsync(target.Id, "Offline", at, at.AddMinutes(1), 1, 100, default)).Total);
        Assert.Single((await s.Deliveries.ListAsync("Bark", null, 1, 100, default)).Items);

        Task Observe(PresenceState state, int seconds) => s.Telemetry.RecordAsync(new(target.Id, state, at.AddSeconds(seconds), "Push"), "Alice", account.Id, default);
    }

    [Fact]
    public async Task FirstOnlineFollowedByOffline_IsEligibleWithoutInventingSessionDuration()
    {
        await using var s = new TestStore(); await s.Initialize(); var id = Guid.NewGuid(); var at = s.Clock.Now;
        await s.Telemetry.RecordAsync(new(id, PresenceState.Online, at, "CachedReconciliation"), "Alice", null, default);
        await s.Telemetry.RecordAsync(new(id, PresenceState.Offline, at.AddSeconds(1), "StatusUpdate"), "Alice", null, default);
        var first = (await s.Telemetry.NextNotificationEventAsync(default))!;
        await s.Telemetry.CompleteNotificationEventAsync(first.Id, default);
        var offline = (await s.Telemetry.NextNotificationEventAsync(default))!;
        Assert.Equal(EventType.Offline, offline.Type); Assert.True(offline.ConfirmedOnlineToOffline); Assert.Null(offline.OnlineSeconds);
    }

    [Fact]
    public async Task LegacyPendingOfflineEvents_ArePreservedButNeverAssumedConfirmed()
    {
        await using var s = new TestStore();
        await using (var c = await s.Database.Open(default))
        {
            using var command = c.CreateCommand();
            command.CommandText = """
                CREATE TABLE events(id INTEGER PRIMARY KEY AUTOINCREMENT,target_id TEXT,account_id TEXT,name TEXT NOT NULL,
                    type TEXT NOT NULL,at INTEGER NOT NULL,online_seconds REAL,detail TEXT NOT NULL,notified INTEGER NOT NULL DEFAULT 0);
                INSERT INTO events(name,type,at,online_seconds,detail) VALUES('Legacy','Offline',0,65,'Push');
                """;
            await command.ExecuteNonQueryAsync();
        }
        await s.Initialize(); await s.Initialize();
        var value = (await s.Telemetry.NextNotificationEventAsync(default))!;
        Assert.Equal("Legacy", value.Name); Assert.False(value.ConfirmedOnlineToOffline); Assert.Equal(65, value.OnlineSeconds);
    }

    private sealed class CaptureBark : IBarkGateway
    {
        public System.Collections.Concurrent.ConcurrentQueue<MonitorEvent> Events { get; } = new();
        public Task SendAsync(DestinationSettings destination, RuleSettings rule, MonitorEvent value, CancellationToken ct)
        { Events.Enqueue(value); return Task.CompletedTask; }
    }
}
