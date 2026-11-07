using System.Text.Json.Nodes;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Infrastructure.Telegram;

public sealed partial class TelegramGateway
{
    private void OnUpdate(int clientId, JsonObject data)
    {
        var entry = _sessions.FirstOrDefault(x => x.Value.ClientId == clientId);
        var s = entry.Value;
        if (s is null) return;
        var type = data["@type"]?.GetValue<string>();
        if (type == "updateAuthorizationState" || type?.StartsWith("authorizationState", StringComparison.Ordinal) == true)
        {
            var state = type == "updateAuthorizationState" ? data["authorization_state"]!.AsObject() : data;
            var auth = state["@type"]!.GetValue<string>();
            s.Login = auth switch
            {
                "authorizationStateWaitPhoneNumber" => new("phone", s.Config.Read<AccountSettings>().Phone),
                "authorizationStateWaitCode" => new("code", state["code_info"]?["phone_number"]?.GetValue<string>(),
                    state["code_info"]?["type"]?["@type"]?.GetValue<string>(),
                    state["code_info"]?["type"]?["length"]?.GetValue<int>()),
                "authorizationStateWaitPassword" => new("password", PasswordHint: state["password_hint"]?.GetValue<string>()),
                "authorizationStateWaitEmailAddress" => new("email"),
                "authorizationStateWaitEmailCode" => new("emailCode", CodeLength: state["code_info"]?["length"]?.GetValue<int>(),
                    EmailAddressPattern: state["code_info"]?["email_address_pattern"]?.GetValue<string>()),
                _ => null
            };
            if (auth == s.Auth) return;
            s.Error = null;
            s.Auth = auth;
            if (auth == "authorizationStateWaitTdlibParameters")
            {
                var account = s.Config.Read<AccountSettings>();
                var identity = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(account.Phone)))[..16];
                var directory = Path.Combine(_db.DirectoryPath, "sessions", entry.Key.ToString(), identity);
                Directory.CreateDirectory(directory);
                _transport.Post(clientId, new()
                {
                    ["@type"] = "setTdlibParameters", ["database_directory"] = directory,
                    ["files_directory"] = Path.Combine(directory, "files"), ["api_id"] = account.ApiId,
                    ["api_hash"] = account.ApiHash, ["system_language_code"] = "en",
                    ["device_model"] = "TelegramCopilot", ["application_version"] = "1.0",
                    ["use_chat_info_database"] = true, ["use_message_database"] = false,
                    ["use_secret_chats"] = false
                });
            }
            if (auth == "authorizationStateWaitPhoneNumber")
                _transport.Post(clientId, new() { ["@type"] = "setAuthenticationPhoneNumber", ["phone_number"] = s.Config.Read<AccountSettings>().Phone });
            if (auth == "authorizationStateReady") s.Error = null;
            if (auth == "authorizationStateReady")
                _transport.Post(clientId, new() { ["@type"] = "setOption", ["name"] = "online",
                    ["value"] = new JsonObject { ["@type"] = "optionValueBoolean", ["value"] = true } });
        }
        if (type == "updateConnectionState")
        {
            s.Connection = data["state"]!["@type"]!.GetValue<string>();
            if (s.Connection != "connectionStateReady")
                foreach (var (id, watch) in _targets.Where(x => x.Value.AccountId == entry.Key))
                    _observations.Publish(new(id, PresenceState.Unknown, _clock.GetUtcNow(), "ConnectionUnavailable",
                        SourceAccountId: entry.Key, SourceIdentifier: watch.Identifier));
        }
        if (type == "error")
        {
            var code = data["code"]!.GetValue<int>();
            s.Error = TelegramException.SafeError(code, data["message"]?.ToString());
            if (code == 429)
            {
                var seconds = TelegramException.RetryAfter(code, data["message"]?.ToString()) ?? 60;
                s.NotBefore = _clock.GetUtcNow().AddSeconds(seconds);
            }
        }
        if (type is "updateUserStatus" or "updateUser")
        {
            var userId = type == "updateUserStatus" ? data["user_id"]!.GetValue<long>() : data["user"]!["id"]!.GetValue<long>();
            var status = (type == "updateUserStatus" ? data["status"] : data["user"]!["status"])!.AsObject();
            var profile = type == "updateUser" ? ReadProfile(data["user"]!.AsObject()) : null;
            foreach (var (id, watch) in _targets.Where(x => x.Value.AccountId == entry.Key && x.Value.UserId == userId))
            {
                if (profile is not null) _profiles[id] = (entry.Key, watch.Identifier, profile);
                _observations.Publish(ToObservation(id, status, _clock.GetUtcNow(), "TelegramUpdate")
                    with { SourceAccountId = entry.Key, SourceIdentifier = watch.Identifier, Profile = profile });
            }
        }
        if (type is "updateMessageSendSucceeded" or "updateMessageSendFailed")
        {
            var message = data["message"]!.AsObject();
            var key = $"{clientId}:{message["chat_id"]}:{data["old_message_id"]}";
            if (_sends.TryRemove(key, out var completion))
            {
                if (type == "updateMessageSendSucceeded") completion.TrySetResult(message["id"]!.ToString());
                else completion.TrySetException(new TelegramException(data["error"]!["code"]!.GetValue<int>()));
            }
            else _sendResults[key] = data.DeepClone().AsObject();
        }
    }
    internal static Observation ToObservation(Guid targetId, JsonObject status, DateTimeOffset at, string reason)
    {
        var type = status["@type"]!.GetValue<string>();
        if (type == "userStatusOnline")
        {
            var expires = DateTimeOffset.FromUnixTimeSeconds(status["expires"]!.GetValue<long>());
            return expires > at ? new(targetId, PresenceState.Online, at, reason, null, expires, TelegramStatus: type)
                : new(targetId, PresenceState.Unknown, at, "ExpiredOnlineStatus", TelegramStatus: type);
        }
        if (type == "userStatusOffline")
            return new(targetId, PresenceState.Offline, at, reason,
                status["was_online"]!.GetValue<long>() is > 0 and var seen ? DateTimeOffset.FromUnixTimeSeconds(seen) : null,
                TelegramStatus: type);
        return new(targetId, PresenceState.Unknown, at, type, TelegramStatus: type);
    }
}
