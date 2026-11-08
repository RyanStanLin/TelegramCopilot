using Microsoft.Extensions.Configuration;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;
using TelegramCopilot.Infrastructure.Notifications;
using TelegramCopilot.Infrastructure.Persistence;

namespace TelegramCopilot.Tests;

public sealed class NotificationAvatarTests
{
    [Fact]
    public async Task AvatarResolver_UsesEachEventsTargetAndRejectsMissingOrReconfiguredProfiles()
    {
        await using var s = new TestStore(); await s.Initialize(); var account = await s.Account();
        var alice = await s.Target(account.Id);
        var bob = await s.Configuration.SaveAsync(ResourceKind.Targets, null, TestStore.Request("Bob", new TargetSettings(account.Id, "@bob")), default);
        var repository = new MonitoringRepository(s.Database);
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PublicApiOrigin"] = "https://api.example.com" }).Build();
        var resolver = new NotificationAvatar(settings, s.Configs, repository, s.Clock);
        foreach (var configured in new[] { alice, bob })
        {
            var target = (await s.Configs.FindAsync(configured.Id, default))!;
            await repository.ObserveAsync(target, new(target.Id, PresenceState.Online, s.Clock.Now, "Push",
                Profile: new(42, target.Name, "", null, 7, "avatar-" + target.Name)), default);
            var value = new MonitorEvent(1, target.Id, account.Id, target.Name, EventType.Online, s.Clock.Now, null, "Push");
            var uri = new Uri((await resolver.ForEventAsync(value, default))!);
            Assert.Equal($"/api/targets/{target.Id}/avatar", uri.AbsolutePath);
            Assert.Equal("?v=avatar-" + target.Name, Uri.UnescapeDataString(uri.Query));
        }
        var original = new MonitorEvent(1, alice.Id, account.Id, "Alice", EventType.Offline, s.Clock.Now, 65, "Push");
        s.Clock.Now = s.Clock.Now.AddSeconds(1);
        await s.Configuration.SaveAsync(ResourceKind.Targets, alice.Id, TestStore.Request("Changed", new TargetSettings(account.Id, "@changed"), 1), default);
        Assert.Null(await resolver.ForEventAsync(original, default));
        Assert.Null(await resolver.ForEventAsync(original with { At = s.Clock.Now }, default));
        Assert.Null(await resolver.ForEventAsync(original with { TargetId = Guid.NewGuid() }, default));
        Assert.Null(await resolver.ForEventAsync(original with { TargetId = null }, default));
    }

    [Fact]
    public void EnglishTemplates_RenderReadableDurationAndUnknownWithoutChineseUnits()
    {
        var value = new MonitorEvent(1, null, null, "Larry", EventType.Offline, DateTimeOffset.UnixEpoch, 9121, "Push");
        Assert.Equal("Larry was online for 2h 32m 1s", TemplateRenderer.Render("{NAME} was online for {TIME_OF_ONLINE}", value, "en"));
        Assert.Equal("unknown", TemplateRenderer.Render("{TIME_OF_ONLINE}", value with { OnlineSeconds = null }, "en"));
        Assert.Equal("未知", TemplateRenderer.Render("{TIME_OF_ONLINE}", value with { OnlineSeconds = null }, "zh-CN"));
    }
}
