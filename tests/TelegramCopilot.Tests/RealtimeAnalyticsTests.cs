using System.Net.WebSockets;
using System.Text.Json;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Tests;

public sealed class RealtimeAnalyticsTests
{
    [Fact]
    public async Task WebSocket_AnalyticsPushCommittedIntervalsAndDailyTotals()
    {
        await using var h = new RealtimeHost(); await h.Initialize();
        var store = h.Telegram.Store; var from = store.Clock.Now;
        store.Clock.Now = from.AddHours(2);
        using var socket = await h.Connect();
        var query = $"from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(from.AddHours(1).ToString("O"))}";
        var timeline = $"/targets/{h.Telegram.Target.Id}/timeline?{query}&page=1&pageSize=500";
        var statistics = $"/targets/{h.Telegram.Target.Id}/statistics?{query}&period=day&timeZone=Asia%2FTaipei&pageSize=7";
        Assert.True(statistics.Length > 128);
        await RealtimeHost.Send(socket, timeline);
        var empty = await RealtimeHost.Read(socket, v => v.GetProperty("path").GetString() == timeline);
        Assert.Equal("Unknown", empty.GetProperty("data").GetProperty("items")[0].GetProperty("state").GetString());
        await RealtimeHost.Send(socket, statistics);
        var initial = await RealtimeHost.Read(socket, v => v.GetProperty("path").GetString() == statistics);
        Assert.Equal(3600, initial.GetProperty("data").GetProperty("totals").GetProperty("unknownSeconds").GetDouble());
        foreach (var (state, seconds) in new[] { (PresenceState.Offline, 0), (PresenceState.Online, 10), (PresenceState.Offline, 30) })
            await store.Telemetry.RecordAsync(new(h.Telegram.Target.Id, state, from.AddSeconds(seconds), "TelegramUpdate"),
                "Alice", h.Telegram.Account.Id, default);
        await store.Telemetry.CheckpointAsync(store.Clock.Now, default);
        JsonElement? pushed = null; JsonElement? aggregate = null;
        await RealtimeHost.Read(socket, v =>
        {
            if (v.GetProperty("path").GetString() == timeline && v.GetProperty("data").GetProperty("total").GetInt32() == 3)
                pushed = v.GetProperty("data").Clone();
            if (v.GetProperty("path").GetString() == statistics
                && v.GetProperty("data").GetProperty("totals").GetProperty("onlineSeconds").GetDouble() == 20
                && v.GetProperty("data").GetProperty("totals").GetProperty("offlineSeconds").GetDouble() == 3580)
                aggregate = v.GetProperty("data").GetProperty("totals").Clone();
            return pushed is not null && aggregate is not null;
        });
        var rows = pushed!.Value.GetProperty("items");
        Assert.Equal("Online", rows[1].GetProperty("state").GetString());
        Assert.Equal(20, rows[1].GetProperty("seconds").GetDouble());
        Assert.True(rows[1].GetProperty("startKnown").GetBoolean());
        var totals = aggregate!.Value;
        Assert.Equal(1, totals.GetProperty("count").GetInt32());
        Assert.Equal(3580, totals.GetProperty("offlineSeconds").GetDouble());
        Assert.Equal(1, totals.GetProperty("coverageRatio").GetDouble());
        Assert.Equal(20, totals.GetProperty("averageSessionSeconds").GetDouble());
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", default);
    }

    [Theory]
    [InlineData("timeline", "page=0")]
    [InlineData("timeline", "page=33")]
    [InlineData("timeline", "page=1.5")]
    [InlineData("timeline", "pageSize=501")]
    [InlineData("timeline", "pageSize=0")]
    [InlineData("timeline", "state=Online")]
    [InlineData("timeline", "from=bad")]
    [InlineData("timeline", "unknown=value")]
    [InlineData("statistics", "period=week&timeZone=UTC")]
    [InlineData("statistics", "period=day&timeZone=Invalid%2FZone")]
    [InlineData("statistics", "period=day&timeZone=UTC&timeZone=Asia%2FTaipei")]
    public async Task WebSocket_AnalyticsRejectInvalidQueryWithoutMutations(string kind, string extra)
    {
        await using var h = new RealtimeHost(); await h.Initialize();
        using var socket = await h.Connect();
        var path = $"/targets/{h.Telegram.Target.Id}/{kind}?from=2026-11-06T00:00:00Z&to=2026-11-07T00:00:00Z&{extra}";
        await RealtimeHost.Send(socket, path);
        var error = await RealtimeHost.Read(socket, v => v.TryGetProperty("error", out _));
        Assert.Equal("InvalidSubscription", error.GetProperty("error").GetString());
        Assert.Empty(h.Telegram.Transport.Requests);
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", default);
    }

    [Theory]
    [InlineData("2026-11-07T00:00:00Z", "2026-11-06T00:00:00Z")]
    [InlineData("2026-11-06T00:00:00Z", "2026-11-06T00:00:00Z")]
    [InlineData("2026-10-01T00:00:00Z", "2026-11-02T00:00:00Z")]
    public async Task WebSocket_AnalyticsRejectUnboundedOrReversedRanges(string from, string to)
    {
        await using var h = new RealtimeHost(); await h.Initialize();
        using var socket = await h.Connect();
        await RealtimeHost.Send(socket, $"/targets/{h.Telegram.Target.Id}/timeline?from={from}&to={to}");
        var error = await RealtimeHost.Read(socket, v => v.TryGetProperty("error", out _));
        Assert.Equal("InvalidSubscription", error.GetProperty("error").GetString());
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", default);
    }
}
