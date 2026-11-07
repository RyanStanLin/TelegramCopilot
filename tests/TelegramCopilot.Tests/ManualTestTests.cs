using System.Text.Json;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Application.Services;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;
using TelegramCopilot.Core.Scheduling;

namespace TelegramCopilot.Tests;

public sealed class ManualTestTests
{
    [Fact]
    public async Task ManualSchedule_SendsSavedMessageOnceAndPreservesItsRecurringConfiguration()
    {
        await using var h = new TelegramTestHost(); await h.Initialize(); var bark = new TestBark();
        var schedule = await h.Store.Configuration.SaveAsync(ResourceKind.Schedules, null,
            TestStore.Request("Test schedule", new ScheduleSettings(h.Account.Id, "@alice", "Saved message", Recurrence.Daily, "UTC", "07:00:00")), default);
        h.Transport.Handle = (_, request) => request["@type"]!.ToString() == "sendMessage"
            ? new() { ["id"] = 991, ["chat_id"] = 314 } : h.DefaultResponse(0, request);
        var service = Service(h, bark); var key = new ManualTestRequest(Guid.NewGuid());
        var first = await service.ScheduleAsync(schedule.Id, key, default);
        var second = await service.ScheduleAsync(schedule.Id, key, default);
        var sent = Assert.Single(h.Transport.Requests, x => x.Body["@type"]!.ToString() == "sendMessage");
        Assert.Equal("Saved message", sent.Body["input_message_content"]!["text"]!["text"]!.ToString());
        var delivery = Assert.Single(first.Deliveries); Assert.Equal("Sent", delivery.Status); Assert.Equal("991", delivery.ExternalId);
        Assert.StartsWith("test:telegram:", delivery.Id); Assert.Equal(first.Deliveries, second.Deliveries);
        var unchanged = await h.Store.Configuration.GetAsync(ResourceKind.Schedules, schedule.Id, default);
        Assert.Equal(schedule.Revision, unchanged.Revision); Assert.Equal(schedule.Settings.GetRawText(), unchanged.Settings.GetRawText());
        Assert.Empty(bark.Keys);
    }

    [Fact]
    public async Task RuleValidation_ProducesPreviewWithoutSendingAndFindsInvalidFields()
    {
        await using var h = new TelegramTestHost(); await h.Initialize(); var bark = new TestBark(); var destination = await Destination(h);
        var rule = new RuleSettings(destination.Id, EventType.Offline, h.Target.Id, "{NAME}", "Online {TIME_OF_ONLINE}");
        var validation = new RuleValidationService(h.Store.Configs, h.Store.Clock);
        var good = await validation.ValidateAsync(TestStore.Request("Offline", rule), default);
        Assert.True(good.Valid); Assert.Equal("Alice", good.Preview!.Title); Assert.Equal("Online 2小时32分1秒", good.Preview.Body);
        Assert.Equal(2, good.Preview.DeviceCount); Assert.DoesNotContain("first-device", JsonSerializer.Serialize(good));
        var bad = await validation.ValidateAsync(TestStore.Request("Invalid", rule with { Body = "{TYPO}",
            Parameters = JsonSerializer.SerializeToElement(new { badge = "invalid", device_key = "override" }) }), default);
        Assert.False(bad.Valid); Assert.Null(bad.Preview);
        Assert.Contains(bad.Errors, x => x.Code == "UnknownTemplateVariable");
        Assert.Contains(bad.Errors, x => x.Code == "NonNegativeIntegerRequired");
        Assert.Contains(bad.Errors, x => x.Code == "ReservedBarkParameter");
        Assert.Empty(bark.Keys); Assert.Empty((await h.Store.Deliveries.ListAsync(null, null, 1, 100, default)).Items);
        var missing = await validation.ValidateAsync(TestStore.Request("Missing", rule with { DestinationId = Guid.NewGuid(), TargetId = Guid.NewGuid() }), default);
        Assert.Contains(missing.Errors, x => x.Code == "MissingDestination"); Assert.Contains(missing.Errors, x => x.Code == "MissingTarget");
    }

    [Fact]
    public async Task ManualRule_RecordsEachDevicesOutcomeAndDoesNotReplayOnDuplicateRequest()
    {
        await using var h = new TelegramTestHost(); await h.Initialize(); var bark = new TestBark { FailSecond = true }; var destination = await Destination(h);
        var rule = await h.Store.Configuration.SaveAsync(ResourceKind.Rules, null,
            TestStore.Request("Offline", new RuleSettings(destination.Id, EventType.Offline, h.Target.Id)), default);
        var key = new ManualTestRequest(Guid.NewGuid()); var service = Service(h, bark);
        var first = await service.RuleAsync(rule.Id, key, default); await service.RuleAsync(rule.Id, key, default);
        Assert.Equal(new[] { "first-device", "second-device" }, bark.Keys);
        Assert.Single(first.Deliveries, x => x.Status == "Sent"); Assert.Single(first.Deliveries, x => x.Status == "Failed");
        Assert.All(first.Deliveries, x => Assert.StartsWith("test:bark:", x.Id));
        Assert.Equal(2, (await h.Store.Deliveries.ListAsync("Bark", rule.Id, 1, 100, default)).Total);
        Assert.Empty((await h.Store.Telemetry.EventsAsync(null, null, h.Store.Clock.Now.AddDays(-1), h.Store.Clock.Now.AddDays(1), 1, 100, default)).Items);
    }

    [Fact]
    public async Task InvalidRuleAndEmptyRequestId_AreRejectedBeforeClaimingOrSending()
    {
        await using var h = new TelegramTestHost(); await h.Initialize(); var bark = new TestBark(); var destination = await Destination(h);
        var rule = await h.Store.Configuration.SaveAsync(ResourceKind.Rules, null,
            TestStore.Request("Invalid template", new RuleSettings(destination.Id, EventType.Online, null, "{TYPO}")), default);
        var service = Service(h, bark);
        await Assert.ThrowsAsync<ArgumentException>(() => service.RuleAsync(rule.Id, new(Guid.NewGuid()), default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RuleAsync(rule.Id, new(Guid.Empty), default));
        Assert.Empty(bark.Keys); Assert.Empty((await h.Store.Deliveries.ListAsync(null, null, 1, 100, default)).Items);
    }
    private static Task<ConfigurationResponse> Destination(TelegramTestHost h) => h.Store.Configuration.SaveAsync(ResourceKind.Destinations, null,
        TestStore.Request("Bark", new DestinationSettings("http://127.0.0.1:1", ["first-device", "second-device"])), default);
    private static ManualTestService Service(TelegramTestHost h, TestBark bark) => new(h.Store.Configs, h.Store.Deliveries,
        h.Gateway, bark, new RuleValidationService(h.Store.Configs, h.Store.Clock), h.Store.Clock);
    private sealed class TestBark : IBarkGateway
    {
        public List<string> Keys { get; } = [];
        public bool FailSecond { get; init; }
        public Task SendAsync(DestinationSettings destination, RuleSettings rule, MonitorEvent value, CancellationToken ct)
        {
            var key = Assert.Single(destination.DeviceKeys); Keys.Add(key);
            if (FailSecond && key == "second-device") throw new HttpRequestException("Fixture failure");
            return Task.CompletedTask;
        }
    }
}
