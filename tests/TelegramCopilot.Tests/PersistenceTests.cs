using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task DuplicateUpdates_DoNotCreateSessionsAndOfflineCapturesDuration()
    {
        await using var s = new TestStore(); await s.Initialize();
        var target = Guid.NewGuid(); var t = s.Clock.Now;
        await s.Telemetry.RecordAsync(new(target, PresenceState.Offline, t, "Push"), "Alice", null, default);
        await s.Telemetry.RecordAsync(new(target, PresenceState.Online, t.AddSeconds(10), "Push"), "Alice", null, default);
        await s.Telemetry.RecordAsync(new(target, PresenceState.Online, t.AddSeconds(20), "Push"), "Alice", null, default);
        await s.Telemetry.RecordAsync(new(target, PresenceState.Offline, t.AddSeconds(40), "Push"), "Alice", null, default);
        var rows = await s.Telemetry.IntervalsAsync(target, t, t.AddSeconds(50), default);
        Assert.Equal(3, rows.Count);
        var online = Assert.Single(rows, x => x.State == PresenceState.Online);
        Assert.Equal(30, online.Seconds(t.AddSeconds(50)));
        var events = await s.Telemetry.EventsAsync(target, "Offline", t, t.AddSeconds(50), 1, 100, default);
        Assert.Equal(30, events.Items.Single(x => x.OnlineSeconds is not null).OnlineSeconds);
    }
    [Fact]
    public async Task CrashRecovery_CapsKnownCoverageAtDurableCheckpoint()
    {
        await using var s = new TestStore(); await s.Initialize(); var id = Guid.NewGuid(); var t = s.Clock.Now;
        await s.Telemetry.RecordAsync(new(id, PresenceState.Online, t, "Push"), "Alice", null, default);
        await s.Telemetry.CheckpointAsync(t.AddSeconds(10), default);
        var before = await s.Telemetry.IntervalsAsync(id, t, t.AddSeconds(60), default);
        Assert.Equal(t.AddSeconds(10), Assert.Single(before).End);
        await s.Telemetry.RecoverAsync(t.AddSeconds(60), default);
        var result = PresenceTimeline.Fill(await s.Telemetry.IntervalsAsync(id, t, t.AddSeconds(60), default), id, t, t.AddSeconds(60));
        Assert.Equal(new[] { PresenceState.Online, PresenceState.Unknown }, result.Select(x => x.State));
        Assert.Equal(50, result[1].Seconds(t.AddSeconds(60)));
        Assert.False(result[0].EndKnown);
    }
    [Fact]
    public async Task InterruptedOnline_DoesNotInventCompletedSessionDuration()
    {
        await using var s = new TestStore(); await s.Initialize(); var id = Guid.NewGuid(); var t = s.Clock.Now;
        await s.Telemetry.RecordAsync(new(id, PresenceState.Online, t, "Push"), "Alice", null, default);
        await s.Telemetry.RecordAsync(new(id, PresenceState.Unknown, t.AddSeconds(10), "Disconnected"), "Alice", null, default);
        var events = await s.Telemetry.EventsAsync(id, null, t, t.AddSeconds(20), 1, 100, default);
        Assert.All(events.Items, x => Assert.Null(x.OnlineSeconds));
    }
    [Fact]
    public async Task DeliveryClaim_IsDurableAndInterruptedSendIsUncertain()
    {
        await using var s = new TestStore(); await s.Initialize();
        var d = new DeliveryResponse("occurrence", "Telegram", Guid.NewGuid(), s.Clock.Now, "Sending", null, null, s.Clock.Now);
        Assert.True(await s.Deliveries.ClaimAsync(d, default));
        Assert.False(await s.Deliveries.ClaimAsync(d, default));
        await s.Deliveries.RecoverAsync(s.Clock.Now.AddSeconds(10), default);
        var page = await s.Deliveries.ListAsync("Telegram", d.ResourceId, 1, 10, default);
        Assert.Equal("Uncertain", Assert.Single(page.Items).Status);
        Assert.False(await s.Deliveries.ClaimAsync(d, default));
    }
}
