using System.Text.Json;
using TelegramCopilot.Core.Configuration;

namespace TelegramCopilot.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public async Task CreateUpdate_RedactsSecretsAndPreservesMaskedValues()
    {
        await using var s = new TestStore(); await s.Initialize();
        var account = await s.Account();
        Assert.Equal("***", account.Settings.GetProperty("apiHash").GetString());
        var update = TestStore.Request("Renamed", account.Settings, account.Revision);
        var saved = await s.Configuration.SaveAsync(ResourceKind.Accounts, account.Id, update, default);
        Assert.Equal(2, saved.Revision); Assert.Equal("Renamed", saved.Name);
        Assert.Equal("0123456789abcdef0123456789abcdef", (await s.Configs.FindAsync(account.Id, default))!.Read<AccountSettings>().ApiHash);
        var databaseText = await File.ReadAllBytesAsync(Path.Combine(s.Database.DirectoryPath, "telegramcopilot.db"));
        Assert.DoesNotContain("0123456789abcdef0123456789abcdef", System.Text.Encoding.UTF8.GetString(databaseText));
    }
    [Fact]
    public async Task ReferencedAccount_CannotBeDeleted()
    {
        await using var s = new TestStore(); await s.Initialize();
        var account = await s.Account(); await s.Target(account.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Configuration.DeleteAsync(ResourceKind.Accounts, account.Id, account.Revision, default));
        Assert.NotNull(await s.Configs.FindAsync(account.Id, default));
    }
    [Fact]
    public async Task MissingAccountReference_IsRejected()
    {
        await using var s = new TestStore(); await s.Initialize();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => s.Target(Guid.NewGuid()));
        Assert.Empty(await s.Configs.ListAsync(ResourceKind.Targets, default));
    }
    [Fact]
    public async Task StaleRevision_DoesNotOverwriteOrDelete()
    {
        await using var s = new TestStore(); await s.Initialize(); var a = await s.Account();
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Configuration.SaveAsync(ResourceKind.Accounts, a.Id,
            TestStore.Request("Stale", a.Settings, 0), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Configuration.DeleteAsync(ResourceKind.Accounts, a.Id, 0, default));
        Assert.Equal("Account", (await s.Configs.FindAsync(a.Id, default))!.Name);
    }
    [Fact]
    public async Task DeletedTarget_CanStillBeQueriedForHistory()
    {
        await using var s = new TestStore(); await s.Initialize(); var a = await s.Account(); var target = await s.Target(a.Id);
        await s.Configuration.DeleteAsync(ResourceKind.Targets, target.Id, target.Revision, default);
        s.Clock.Now = s.Clock.Now.AddHours(1);
        var page = await s.Analytics.TimelineAsync(target.Id, new(s.Clock.Now.AddHours(-1), s.Clock.Now), default);
        Assert.Equal("Unknown", Assert.Single(page.Items).State);
        Assert.Null(await s.Configs.FindAsync(target.Id, default));
    }
    [Fact]
    public async Task InvalidSettingsAndMaskedCreate_AreRejected()
    {
        await using var s = new TestStore(); await s.Initialize();
        await Assert.ThrowsAsync<ArgumentException>(() => s.Configuration.SaveAsync(ResourceKind.Accounts, null,
            TestStore.Request("Bad", new AccountSettings(1, "***", "+886912345678")), default));
        await Assert.ThrowsAsync<JsonException>(() => s.Configuration.SaveAsync(ResourceKind.Accounts, null,
            TestStore.Request("Bad", new { apiId = 1, apiHash = "bad", phone = "+886912345678", unknown = 2 }), default));
    }
    [Fact]
    public async Task DeletedOnlineTarget_DoesNotExtendKnownPresenceAfterDeletion()
    {
        await using var s = new TestStore(); await s.Initialize(); var a = await s.Account(); var target = await s.Target(a.Id);
        var start = s.Clock.Now;
        await s.Telemetry.RecordAsync(new(target.Id, Core.Presence.PresenceState.Online, start, "Push"), target.Name, a.Id, default);
        s.Clock.Now = start.AddSeconds(10);
        await s.Configuration.DeleteAsync(ResourceKind.Targets, target.Id, target.Revision, default);
        s.Clock.Now = start.AddSeconds(60);
        await s.Telemetry.CheckpointAsync(s.Clock.Now, default);
        var page = await s.Analytics.TimelineAsync(target.Id, new(start, s.Clock.Now), default);
        Assert.Equal(new[] { "Online", "Unknown" }, page.Items.Select(x => x.State));
        Assert.Equal(10, page.Items[0].Seconds);
        Assert.Equal(50, page.Items[1].Seconds);
        Assert.False(page.Items[0].EndKnown);
    }
}
