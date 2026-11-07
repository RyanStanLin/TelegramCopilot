using System.Text.Json.Nodes;
using TelegramCopilot.Core.Presence;
using TelegramCopilot.Infrastructure.Telegram;

namespace TelegramCopilot.Tests;

public sealed class TelegramStatusTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-07T00:00:00Z");
    [Theory]
    [InlineData("userStatusEmpty")]
    [InlineData("userStatusRecently")]
    [InlineData("userStatusLastWeek")]
    [InlineData("userStatusLastMonth")]
    public void PrivateOrCoarseStatus_IsUnknownRatherThanOffline(string type)
    {
        var observation = TelegramGateway.ToObservation(Guid.Empty, new() { ["@type"] = type }, Now, "Push");
        Assert.Equal(PresenceState.Unknown, observation.State);
        Assert.Equal(type, observation.Reason);
        Assert.Null(observation.TelegramAt);
    }
    [Fact]
    public void ExpiredCachedOnlineStatus_DoesNotExtendOnlineTime()
    {
        var observation = TelegramGateway.ToObservation(Guid.Empty, new()
            { ["@type"] = "userStatusOnline", ["expires"] = Now.AddSeconds(-1).ToUnixTimeSeconds() }, Now, "Cache");
        Assert.Equal(PresenceState.Unknown, observation.State);
        Assert.Equal("ExpiredOnlineStatus", observation.Reason);
    }
    [Fact]
    public void OfflineServerTimestamp_RemainsSeparateFromObservedTime()
    {
        var server = Now.AddSeconds(-15);
        var observation = TelegramGateway.ToObservation(Guid.Empty, new()
            { ["@type"] = "userStatusOffline", ["was_online"] = server.ToUnixTimeSeconds() }, Now, "Push");
        Assert.Equal(PresenceState.Offline, observation.State);
        Assert.Equal(Now, observation.ObservedAt);
        Assert.Equal(server, observation.TelegramAt);
    }
}
