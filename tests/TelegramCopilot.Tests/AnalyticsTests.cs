using TelegramCopilot.Core.Presence;
using TelegramCopilot.Application.Contracts;

namespace TelegramCopilot.Tests;

public sealed class AnalyticsTests
{
    [Fact]
    public async Task DisabledSystemSettings_UseDefaultsUntilReenabled()
    {
        await using var s = new TestStore(); await s.Initialize(); var account = await s.Account(); var target = await s.Target(account.Id);
        var settings = await s.Configuration.SaveAsync(TelegramCopilot.Core.Configuration.ResourceKind.Settings, null,
            TestStore.Request("System", new TelegramCopilot.Core.Configuration.SystemSettings("UTC")) with { Enabled = false }, default);
        var query = new TimelineQuery(s.Clock.Now.AddHours(-1), s.Clock.Now);
        Assert.Equal("Asia/Taipei", (await s.Analytics.StatisticsAsync(target.Id, query, "day", null, default)).TimeZone);
        await s.Configuration.SaveAsync(TelegramCopilot.Core.Configuration.ResourceKind.Settings, settings.Id,
            new() { Name = settings.Name, Settings = settings.Settings, Revision = settings.Revision, Enabled = true }, default);
        Assert.Equal("UTC", (await s.Analytics.StatisticsAsync(target.Id, query, "day", null, default)).TimeZone);
    }

    [Fact]
    public async Task Statistics_CountDurationAverageAndUnknownCoverage_AreIndependent()
    {
        await using var s = new TestStore(); await s.Initialize(); var a = await s.Account(); var target = await s.Target(a.Id);
        var t = s.Clock.Now;
        await s.Telemetry.RecordAsync(new(target.Id, PresenceState.Offline, t, "Push"), "Alice", a.Id, default);
        await s.Telemetry.RecordAsync(new(target.Id, PresenceState.Online, t.AddSeconds(10), "Push"), "Alice", a.Id, default);
        await s.Telemetry.RecordAsync(new(target.Id, PresenceState.Offline, t.AddSeconds(40), "Push"), "Alice", a.Id, default);
        await s.Telemetry.RecordAsync(new(target.Id, PresenceState.Unknown, t.AddSeconds(50), "Outage"), "Alice", a.Id, default);
        s.Clock.Now = t.AddSeconds(60);
        var result = await s.Analytics.StatisticsAsync(target.Id, new(t, s.Clock.Now), "day", "UTC", default);
        Assert.Equal(1, result.Totals.Count); Assert.Equal(30, result.Totals.OnlineSeconds);
        Assert.Equal(20, result.Totals.OfflineSeconds); Assert.Equal(10, result.Totals.UnknownSeconds);
        Assert.Equal(30, result.Totals.AverageSessionSeconds); Assert.Equal(50d / 60d, result.Totals.CoverageRatio, 8);
        Assert.Single(result.Buckets.Items);
        var clipped = await s.Analytics.StatisticsAsync(target.Id, new(t.AddSeconds(20), t.AddSeconds(30)), "day", "UTC", default);
        Assert.Equal(10, clipped.Totals.OnlineSeconds); Assert.Equal(0, clipped.Totals.Count);
        Assert.Null(clipped.Totals.AverageSessionSeconds);
    }
    [Fact]
    public async Task CalendarDay_UsesRequestedTimezoneAndPaginates()
    {
        await using var s = new TestStore(); await s.Initialize(); var a = await s.Account(); var target = await s.Target(a.Id);
        s.Clock.Now = s.Clock.Now.AddDays(3);
        var result = await s.Analytics.StatisticsAsync(target.Id, new(s.Clock.Now.AddDays(-3), s.Clock.Now, 2, 1), "day", "Asia/Taipei", default);
        Assert.Equal(4, result.Buckets.Total);
        Assert.Equal(1, result.Buckets.PageSize);
        var row = Assert.Single(result.Buckets.Items);
        Assert.Equal(0, TimeZoneInfo.ConvertTimeBySystemTimeZoneId(row.From, "Asia/Taipei").Hour);
        Assert.Equal(86400, row.UnknownSeconds);
    }
    [Fact]
    public async Task InvalidQueryRangeAndPaging_AreRejected()
    {
        await using var s = new TestStore(); await s.Initialize(); var a = await s.Account(); var target = await s.Target(a.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Analytics.TimelineAsync(target.Id, new(s.Clock.Now, s.Clock.Now), default));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Analytics.TimelineAsync(target.Id, new(s.Clock.Now.AddDays(-1), s.Clock.Now, PageSize: 501), default));
    }
}
