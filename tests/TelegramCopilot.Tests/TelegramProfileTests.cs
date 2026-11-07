using System.Text.Json.Nodes;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Tests;

public sealed class TelegramProfileTests
{
    [Fact]
    public async Task ObservingUser_CapturesNicknameAvatarAndTelegramOfflineTimestamp()
    {
        await using var h = new TelegramTestHost(); await h.Initialize();
        var seen = h.Store.Clock.Now.AddHours(-2);
        h.Transport.Handle = (_, request) => request["@type"]!.ToString() == "getUser" ? User(seen) : h.DefaultResponse(0, request);
        var result = await h.Gateway.ObserveAsync(h.Target, default);
        Assert.Equal(PresenceState.Offline, result.State); Assert.Equal(seen, result.TelegramAt);
        Assert.Equal("userStatusOffline", result.TelegramStatus); Assert.Equal(new(42, "Alice", "Lee", "alice", 7, "888"), result.Profile);
    }

    [Theory]
    [InlineData("userStatusRecently")]
    [InlineData("userStatusLastWeek")]
    [InlineData("userStatusLastMonth")]
    [InlineData("userStatusEmpty")]
    public async Task CoarsePrivacyStatus_NeverInventsAnExactLastSeen(string state)
    {
        await using var h = new TelegramTestHost(); await h.Initialize();
        h.Transport.Handle = (_, request) => request["@type"]!.ToString() == "getUser"
            ? new() { ["id"] = 42, ["status"] = new JsonObject { ["@type"] = state } } : h.DefaultResponse(0, request);
        var result = await h.Gateway.ObserveAsync(h.Target, default);
        Assert.Equal(PresenceState.Unknown, result.State); Assert.Equal(state, result.TelegramStatus); Assert.Null(result.TelegramAt);
    }

    [Fact]
    public async Task Avatar_IsDownloadedOnceAndRejectsPathsOutsideTheConfiguredAccount()
    {
        await using var h = new TelegramTestHost(); await h.Initialize();
        var directory = Path.Combine(h.Store.Database.DirectoryPath, "sessions", h.Account.Id.ToString(), "files");
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, "avatar.jpg"); byte[] bytes = [255, 216, 255, 224, 0, 0];
        await File.WriteAllBytesAsync(path, bytes);
        var outside = false;
        h.Transport.Handle = (_, request) => request["@type"]!.ToString() switch
        {
            "getUser" => User(h.Store.Clock.Now.AddHours(-1)),
            "downloadFile" => new() { ["local"] = new JsonObject { ["path"] = outside ? Path.Combine(h.Store.Database.DirectoryPath, "unrelated.jpg") : path, ["is_downloading_completed"] = true } },
            _ => h.DefaultResponse(0, request)
        };
        await h.Gateway.ObserveAsync(h.Target, default);
        var first = await h.Gateway.AvatarAsync(h.Target, default);
        Assert.NotNull(first); Assert.Equal("image/jpeg", first.ContentType); Assert.Equal(bytes, first.Content);
        Assert.Same(first, await h.Gateway.AvatarAsync(h.Target, default));
        Assert.Single(h.Transport.Requests, x => x.Body["@type"]!.ToString() == "downloadFile");
        outside = true;
        h.Transport.Emit(h.Client, new() { ["@type"] = "updateUser", ["user"] = User(h.Store.Clock.Now) });
        var different = h.TargetWith("@bob"); await h.Gateway.ObserveAsync(different, default); await h.Gateway.ObserveAsync(different, default);
        Assert.Null(await h.Gateway.AvatarAsync(different, default));
    }
    private static JsonObject User(DateTimeOffset seen) => new()
    {
        ["id"] = 42, ["first_name"] = "Alice", ["last_name"] = "Lee",
        ["usernames"] = new JsonObject { ["active_usernames"] = new JsonArray("alice") },
        ["profile_photo"] = new JsonObject { ["id"] = 888, ["small"] = new JsonObject { ["id"] = 7 } },
        ["status"] = new JsonObject { ["@type"] = "userStatusOffline", ["was_online"] = seen.ToUnixTimeSeconds() }
    };
}
