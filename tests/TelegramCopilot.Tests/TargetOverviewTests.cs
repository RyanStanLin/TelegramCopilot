using TelegramCopilot.Application.Services;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;
using TelegramCopilot.Infrastructure.Persistence;

namespace TelegramCopilot.Tests;

public sealed class TargetOverviewTests
{
    [Fact]
    public async Task TargetOverview_UsesLocalDayAndMondayBoundariesWithConfirmedSessionCounts()
    {
        await using var h = new TelegramTestHost(); await h.Initialize(); var s = h.Store; var id = h.Target.Id;
        await s.Configuration.SaveAsync(ResourceKind.Settings, null, TestStore.Request("System", new SystemSettings("Asia/Taipei")), default);
        var monday = DateTimeOffset.Parse("2026-11-01T16:00:00Z");
        var today = monday.AddDays(5); s.Clock.Now = today.AddHours(2);
        await s.Telemetry.RecordAsync(new(id, PresenceState.Offline, monday.AddSeconds(-1), "Push"), "Alice", h.Account.Id, default);
        await s.Telemetry.RecordAsync(new(id, PresenceState.Online, monday.AddHours(1), "Push"), "Alice", h.Account.Id, default);
        await s.Telemetry.RecordAsync(new(id, PresenceState.Offline, monday.AddHours(2), "Push"), "Alice", h.Account.Id, default);
        await s.Telemetry.RecordAsync(new(id, PresenceState.Online, today.AddMinutes(-30), "Push"), "Alice", h.Account.Id, default);
        await s.Telemetry.RecordAsync(new(id, PresenceState.Offline, today.AddMinutes(30), "Push"), "Alice", h.Account.Id, default);
        await s.Telemetry.RecordAsync(new(id, PresenceState.Online, today.AddHours(1), "Push"), "Alice", h.Account.Id, default);
        await s.Telemetry.RecordAsync(new(id, PresenceState.Unknown, today.AddMinutes(90), "ConnectionUnavailable"), "Alice", h.Account.Id, default);
        await s.Telemetry.CheckpointAsync(s.Clock.Now, default);
        var monitoring = new MonitoringRepository(s.Database);
        await monitoring.StartPollAsync(h.Target, s.Clock.Now.AddSeconds(-3), s.Clock.Now, default);
        await monitoring.CompletePollAsync(h.Target, new(id, PresenceState.Offline, s.Clock.Now.AddSeconds(-2), "Cache"), null, default);
        await monitoring.StartPollAsync(h.Target, s.Clock.Now.AddSeconds(-1), s.Clock.Now, default);
        await monitoring.CompletePollAsync(h.Target, new(id, PresenceState.Unknown, s.Clock.Now, "ConnectionUnavailable"), null, default);
        var service = new MonitoringService(s.Configs, monitoring,
            new OperationsService(s.Configs, s.Telemetry, s.Deliveries, h.Gateway, s.Clock), h.Gateway, s.Analytics, s.Clock);
        var result = await service.TargetAsync(id, default); var activity = result.Activity!;
        Assert.Equal("Asia/Taipei", activity.TimeZone); Assert.Equal(today, activity.Today.From); Assert.Equal(monday, activity.Week.From);
        Assert.Equal(3600, activity.Today.OnlineSeconds); Assert.Equal(9000, activity.Week.OnlineSeconds);
        Assert.Equal(1, activity.Today.Count); Assert.Equal(3, activity.Week.Count);
        Assert.Equal(1800, activity.Today.UnknownSeconds); Assert.Equal(0.75, activity.Today.CoverageRatio);
        Assert.Equal(2, result.PollCount); Assert.Equal(1, result.SuccessfulPollCount); Assert.Equal(1, result.FailedPollCount);
        s.Clock.Now = monday.AddDays(7);
        var midnight = (await service.TargetAsync(id, default)).Activity!;
        Assert.Equal(0, midnight.Today.OnlineSeconds); Assert.Equal(0, midnight.Week.Count);
        Assert.Equal(s.Clock.Now, midnight.Week.From); Assert.Equal(midnight.Today, midnight.Week);
    }
}
