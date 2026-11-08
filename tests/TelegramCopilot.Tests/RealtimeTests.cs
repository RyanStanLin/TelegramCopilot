using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TelegramCopilot.Core.Presence;
using TelegramCopilot.Infrastructure.Workers;

namespace TelegramCopilot.Tests;

public sealed class RealtimeTests
{
    [Fact]
    public async Task WebSocket_PushesCommittedCountersToEveryClientAndSurvivesPeerDisconnect()
    {
        await using var h = new RealtimeHost(); await h.Initialize();
        using var first = await h.Connect(); using var second = await h.Connect();
        var target = h.Telegram.Target; var path = $"/targets/{target.Id}/monitoring";
        foreach (var socket in new[] { first, second })
        {
            await RealtimeHost.Send(socket, path);
            var initial = await RealtimeHost.Read(socket, v => v.GetProperty("path").GetString() == path);
            Assert.Equal(0, initial.GetProperty("data").GetProperty("pollCount").GetInt64());
        }
        await RealtimeHost.Send(first, "/monitoring");
        var at = h.Telegram.Store.Clock.Now;
        await h.Monitoring.StartPollAsync(target, at, at.AddSeconds(5), default);
        await h.Monitoring.CompletePollAsync(target, new(target.Id, PresenceState.Offline, at, "CachedReconciliation"), null, default);
        bool Completed(JsonElement v) => v.GetProperty("path").GetString() == path
            && v.GetProperty("data").GetProperty("successfulPollCount").GetInt64() == 1;
        foreach (var socket in new[] { first, second })
        {
            var frame = await RealtimeHost.Read(socket, Completed);
            Assert.Equal(1, frame.GetProperty("data").GetProperty("pollCount").GetInt64());
            Assert.Equal(0, frame.GetProperty("data").GetProperty("failedPollCount").GetInt64());
        }
        var overview = await RealtimeHost.Read(first, v => v.GetProperty("path").GetString() == "/monitoring"
            && v.GetProperty("data").GetProperty("successfulPollCount").GetInt64() == 1);
        Assert.Equal(1, overview.GetProperty("data").GetProperty("pollCount").GetInt64());
        await second.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", default);
        await h.Monitoring.StartPollAsync(target, at.AddSeconds(5), at.AddSeconds(10), default);
        var next = await RealtimeHost.Read(first, v => v.GetProperty("path").GetString() == path
            && v.GetProperty("data").GetProperty("pollCount").GetInt64() == 2);
        Assert.True(next.GetProperty("data").GetProperty("isPolling").GetBoolean());
        await first.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", default);
    }

    [Fact]
    public async Task WebSocket_PushesNativeLoginChangesAndRejectsMutationSubscriptions()
    {
        await using var h = new RealtimeHost(); await h.Initialize();
        using var socket = await h.Connect();
        var path = $"/accounts/{h.Telegram.Account.Id}/status";
        await RealtimeHost.Send(socket, path);
        await RealtimeHost.Read(socket, v => v.GetProperty("data").GetProperty("authorizationState").GetString() == "authorizationStateReady");
        h.Telegram.Transport.Emit(h.Telegram.Client, new() { ["@type"] = "updateAuthorizationState",
            ["authorization_state"] = new JsonObject { ["@type"] = "authorizationStateWaitPassword", ["password_hint"] = "QA hint" } });
        var changed = await RealtimeHost.Read(socket, v => v.GetProperty("data").GetProperty("authorizationState").GetString() == "authorizationStateWaitPassword");
        Assert.Equal("QA hint", changed.GetProperty("data").GetProperty("login").GetProperty("passwordHint").GetString());
        await RealtimeHost.Send(socket, $"/accounts/{h.Telegram.Account.Id}/login");
        var error = await RealtimeHost.Read(socket, v => v.TryGetProperty("error", out _));
        Assert.Equal("InvalidSubscription", error.GetProperty("error").GetString());
        Assert.Empty(h.Telegram.Transport.Requests);
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", default);
    }

    [Theory]
    [InlineData("malformed", WebSocketCloseStatus.InvalidPayloadData)]
    [InlineData("oversized", WebSocketCloseStatus.MessageTooBig)]
    [InlineData("binary", WebSocketCloseStatus.InvalidMessageType)]
    public async Task WebSocket_RejectsInvalidFramesWithProtocolClose(string kind, WebSocketCloseStatus expected)
    {
        await using var h = new RealtimeHost(); await h.Initialize();
        using var socket = await h.Connect();
        var payload = Encoding.UTF8.GetBytes(kind == "oversized" ? new string('x', 4097) : "{");
        await socket.SendAsync(payload, kind == "binary" ? WebSocketMessageType.Binary : WebSocketMessageType.Text, true, default);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var close = await socket.ReceiveAsync(new ArraySegment<byte>(new byte[256]), timeout.Token);
        Assert.Equal(WebSocketMessageType.Close, close.MessageType); Assert.Equal(expected, close.CloseStatus);
        await socket.CloseOutputAsync(expected, "Acknowledged", timeout.Token);
    }

    [Fact]
    public async Task WebSocket_RequiresUpgradeAndHonorsExpiredConsoleSession()
    {
        await using var h = new RealtimeHost(); await h.Initialize();
        using var client = new HttpClient { BaseAddress = h.Address };
        Assert.Equal(HttpStatusCode.UpgradeRequired, (await client.GetAsync("/api/live")).StatusCode);
        var error = await Assert.ThrowsAsync<WebSocketException>(() => h.Connect("0"));
        Assert.Contains("401", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangeFeed_BroadcastsAndCoalescesWithoutCompetingReaders()
    {
        var changes = new RealtimeChanges();
        using var first = changes.Subscribe(); using var second = changes.Subscribe();
        for (var n = 0; n < 1000; n++) changes.Publish();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var a = first.ReadAsync(timeout.Token).GetAsyncEnumerator();
        await using var b = second.ReadAsync(timeout.Token).GetAsyncEnumerator();
        Assert.True(await a.MoveNextAsync()); Assert.True(await b.MoveNextAsync());
        Assert.True(a.Current); Assert.True(b.Current);
        first.Dispose(); Assert.False(await a.MoveNextAsync());
        changes.Publish(); Assert.True(await b.MoveNextAsync());
        second.Dispose(); Assert.False(await b.MoveNextAsync());
    }
}
