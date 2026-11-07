using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Infrastructure.Telegram;

public sealed partial class TelegramGateway
{
    private readonly ConcurrentDictionary<Guid, (Guid AccountId, string Identifier, TelegramProfile Profile)> _profiles = new();
    private readonly ConcurrentDictionary<Guid, (string Key, TelegramAvatar Avatar)> _avatars = new();
    private readonly SemaphoreSlim _avatarLock = new(1);
    internal static TelegramProfile ReadProfile(JsonObject user)
    {
        var usernames = user["usernames"];
        var username = usernames?["active_usernames"]?.AsArray().FirstOrDefault()?.GetValue<string>()
            ?? usernames?["editable_username"]?.GetValue<string>();
        return new(user["id"]!.GetValue<long>(), user["first_name"]?.GetValue<string>() ?? "",
            user["last_name"]?.GetValue<string>() ?? "", string.IsNullOrWhiteSpace(username) ? null : username,
            user["profile_photo"]?["small"]?["id"]?.GetValue<int>(), user["profile_photo"]?["id"]?.ToString());
    }
    public async Task<TelegramAvatar?> AvatarAsync(Resource target, CancellationToken ct)
    {
        var settings = target.Read<TargetSettings>();
        if (!_profiles.TryGetValue(target.Id, out var cached) || cached.AccountId != settings.AccountId
            || cached.Identifier != settings.Identifier || cached.Profile.PhotoFileId is not > 0) return null;
        var key = $"{cached.AccountId}:{cached.Identifier}:{cached.Profile.PhotoId}:{cached.Profile.PhotoFileId}";
        if (_avatars.TryGetValue(target.Id, out var existing) && existing.Key == key) return existing.Avatar;
        var session = Required(settings.AccountId);
        if (session.Auth != "authorizationStateReady") return null;
        await _avatarLock.WaitAsync(ct);
        try
        {
            if (_avatars.TryGetValue(target.Id, out existing) && existing.Key == key) return existing.Avatar;
            var file = await _transport.Request(session.ClientId, new()
            {
                ["@type"] = "downloadFile", ["file_id"] = cached.Profile.PhotoFileId.Value,
                ["priority"] = 1, ["offset"] = 0, ["limit"] = 0, ["synchronous"] = true
            }, ct);
            var path = file["local"]?["path"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(path) || file["local"]?["is_downloading_completed"]?.GetValue<bool>() != true) return null;
            var root = Path.Combine(_db.DirectoryPath, "sessions", settings.AccountId.ToString()) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(path).StartsWith(root, StringComparison.Ordinal) || !File.Exists(path)
                || new FileInfo(path).Length > 1_048_576) return null;
            var bytes = await File.ReadAllBytesAsync(path, ct);
            var contentType = bytes.Length > 2 && bytes[0] == 0xff && bytes[1] == 0xd8 ? "image/jpeg"
                : bytes.Length > 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png" : null;
            if (contentType is null) return null;
            var avatar = new TelegramAvatar(bytes, contentType);
            if (_avatars.Count >= 16) _avatars.TryRemove(_avatars.Keys.First(), out _);
            _avatars[target.Id] = (key, avatar);
            return avatar;
        }
        finally { _avatarLock.Release(); }
    }
}
