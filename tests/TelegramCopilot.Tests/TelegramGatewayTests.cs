using System.Text.Json.Nodes;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Tests;

public sealed class TelegramGatewayTests
{
    [Theory]
    [InlineData("code", "authorizationStateWaitCode", "checkAuthenticationCode", "code")]
    [InlineData("password", "authorizationStateWaitPassword", "checkAuthenticationPassword", "password")]
    [InlineData("email", "authorizationStateWaitEmailAddress", "setAuthenticationEmailAddress", "email_address")]
    [InlineData("emailCode", "authorizationStateWaitEmailCode", "checkAuthenticationEmailCode", "code")]
    public async Task LoginChallenges_AreSubmittedToTheSelectedAccount(string type, string state, string method, string field)
    {
        await using var h = new TelegramTestHost(); await h.Initialize(ready: false);
        var second = await h.Store.Configuration.SaveAsync(ResourceKind.Accounts, null,
            TestStore.Request("Second", new AccountSettings(12345, "0123456789abcdef0123456789abcdef", "+886912345679")), default);
        await h.Gateway.ReconcileAccountsAsync(await h.Store.Configs.ListAsync(ResourceKind.Accounts, default), default);
        var client = h.Transport.Clients.Last();
        h.Transport.Auth(h.Client, "authorizationStateWaitCode");
        h.Transport.Auth(client, state);
        h.Transport.Handle = (id, _) => { h.Transport.Ready(id); return new() { ["@type"] = "ok" }; };
        var status = await h.Gateway.LoginAsync(second.Id, new LoginRequest(type, "fixture-value"), default);
        Assert.Equal(second.Id, status.AccountId);
        Assert.Equal("authorizationStateReady", status.AuthorizationState);
        Assert.Equal("authorizationStateWaitCode", (await h.Gateway.StatusAsync(h.Account.Id, default)).AuthorizationState);
        var submitted = Assert.Single(h.Transport.Requests);
        Assert.Equal(client, submitted.Client);
        Assert.Equal(method, submitted.Body["@type"]!.ToString());
        if (type == "emailCode")
        {
            Assert.Equal("emailAddressAuthenticationCode", submitted.Body[field]!["@type"]!.ToString());
            Assert.Equal("fixture-value", submitted.Body[field]!["code"]!.ToString());
        }
        else Assert.Equal("fixture-value", submitted.Body[field]!.ToString());
    }

    [Fact]
    public async Task AccountLifecycle_IsolatesSessionDirectoriesAndClosesDisabledAccount()
    {
        await using var h = new TelegramTestHost(); await h.Initialize(ready: false);
        var second = await h.Store.Configuration.SaveAsync(ResourceKind.Accounts, null,
            TestStore.Request("Second", new AccountSettings(12345, "0123456789abcdef0123456789abcdef", "+886912345679")), default);
        await h.Gateway.ReconcileAccountsAsync(await h.Store.Configs.ListAsync(ResourceKind.Accounts, default), default);
        var secondClient = h.Transport.Clients.Last();
        foreach (var client in h.Transport.Clients)
        {
            h.Transport.Auth(client, "authorizationStateWaitTdlibParameters");
            h.Transport.Auth(client, "authorizationStateWaitPhoneNumber");
        }
        var parameters = h.Transport.Posts.Where(value => value.Body["@type"]!.ToString() == "setTdlibParameters").ToArray();
        Assert.Equal(2, parameters.Length);
        var directories = parameters.Select(value => value.Body["database_directory"]!.ToString()).ToArray();
        Assert.Equal(2, directories.Distinct().Count());
        Assert.All(directories, directory => Assert.True(Directory.Exists(directory)));
        Assert.Contains(directories, directory => directory.Contains(h.Account.Id.ToString()));
        Assert.Contains(directories, directory => directory.Contains(second.Id.ToString()));
        Assert.Equal(new[] { "+886912345678", "+886912345679" }, h.Transport.Posts
            .Where(value => value.Body["@type"]!.ToString() == "setAuthenticationPhoneNumber")
            .Select(value => value.Body["phone_number"]!.ToString()).Order().ToArray());
        await h.Store.Configuration.SaveAsync(ResourceKind.Accounts, h.Account.Id,
            new() { Name = h.Account.Name, Enabled = false, Settings = h.Account.Settings, Revision = 1 }, default);
        await h.Gateway.ReconcileAccountsAsync(await h.Store.Configs.ListAsync(ResourceKind.Accounts, default), default);
        Assert.Equal("Inactive", (await h.Gateway.StatusAsync(h.Account.Id, default)).AuthorizationState);
        Assert.Equal("authorizationStateWaitPhoneNumber", (await h.Gateway.StatusAsync(second.Id, default)).AuthorizationState);
        Assert.Contains(h.Transport.Posts, value => value.Client == h.Client && value.Body["@type"]!.ToString() == "close");
        Assert.DoesNotContain(h.Transport.Posts, value => value.Client == secondClient && value.Body["@type"]!.ToString() == "close");
    }

    [Fact]
    public async Task PresenceUpdates_RetainIdentityAndIgnoreThePreviousResolvedUser()
    {
        await using var h = new TelegramTestHost(); await h.Initialize();
        var observed = await h.Gateway.ObserveAsync(h.Target, default);
        Assert.Equal(PresenceState.Online, observed.State);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var reader = h.Observations.ReadAllAsync(timeout.Token).GetAsyncEnumerator();
        h.Transport.Emit(h.Client, UserStatus(42, "userStatusRecently"));
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(PresenceState.Unknown, reader.Current.State);
        Assert.Equal("userStatusRecently", reader.Current.Reason);
        Assert.Equal(h.Account.Id, reader.Current.SourceAccountId);
        Assert.Equal("@alice", reader.Current.SourceIdentifier);
        var changed = await h.Gateway.ObserveAsync(h.TargetWith("@bob"), default);
        Assert.Equal("TargetReconfigured", changed.Reason);
        h.Transport.Emit(h.Client, UserStatus(42, "userStatusEmpty"));
        h.Transport.Emit(h.Client, UserStatus(43, "userStatusLastWeek"));
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal("userStatusLastWeek", reader.Current.Reason);
        Assert.Equal("@bob", reader.Current.SourceIdentifier);
        Assert.Equal(h.Target.Id, reader.Current.TargetId);
    }

    [Fact]
    public async Task PhoneResolution_UsesReadOnlyLookupAndDoesNotImportContacts()
    {
        await using var h = new TelegramTestHost(); await h.Initialize();
        var result = await h.Gateway.ObserveAsync(h.TargetWith("+886912345680"), default);
        Assert.Equal(PresenceState.Online, result.State);
        var lookup = Assert.Single(h.Transport.Requests, value => value.Body["@type"]!.ToString() == "searchUserByPhoneNumber");
        Assert.Equal("+886912345680", lookup.Body["phone_number"]!.ToString());
        Assert.False(lookup.Body["only_local"]!.GetValue<bool>());
        Assert.DoesNotContain(h.Transport.Requests, value => value.Body["@type"]!.ToString() == "importContacts");
    }

    [Fact]
    public async Task FloodWait_ProducesUnknownUntilTheServerSpecifiedDelayElapses()
    {
        await using var h = new TelegramTestHost(); await h.Initialize();
        h.Transport.Emit(h.Client, new() { ["@type"] = "error", ["code"] = 429, ["message"] = "retry after 4" });
        var blocked = await h.Gateway.ObserveAsync(h.Target, default);
        Assert.Equal(PresenceState.Unknown, blocked.State);
        Assert.Equal("RateLimited", blocked.Reason);
        Assert.Empty(h.Transport.Requests);
        h.Store.Clock.Now = h.Store.Clock.Now.AddSeconds(5);
        Assert.Equal(PresenceState.Online, (await h.Gateway.ObserveAsync(h.Target, default)).State);
    }

    private static JsonObject UserStatus(long id, string status) => new()
        { ["@type"] = "updateUserStatus", ["user_id"] = id, ["status"] = new JsonObject { ["@type"] = status } };
}
