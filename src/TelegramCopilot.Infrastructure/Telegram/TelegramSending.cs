using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using TelegramCopilot.Application.Contracts;

namespace TelegramCopilot.Infrastructure.Telegram;

public sealed partial class TelegramGateway
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _sends = new();
    private readonly ConcurrentDictionary<string, JsonObject> _sendResults = new();
    private async Task<long> ResolveUser(Session s, string identifier, CancellationToken ct)
    {
        if (identifier.StartsWith('+'))
        {
            var user = await _transport.Request(s.ClientId, new() { ["@type"] = "searchUserByPhoneNumber",
                ["phone_number"] = identifier, ["only_local"] = false }, ct);
            return user["id"]!.GetValue<long>();
        }
        var chat = await _transport.Request(s.ClientId, new() { ["@type"] = "searchPublicChat", ["username"] = identifier.TrimStart('@') }, ct);
        if (chat["type"]?["@type"]?.GetValue<string>() != "chatTypePrivate")
            throw new ArgumentException("The identifier must resolve to a Telegram user or bot, not a channel/group.");
        return chat["type"]!["user_id"]!.GetValue<long>();
    }
    public async Task<SendReceipt> SendAsync(Guid accountId, string recipient, string message, CancellationToken ct)
    {
        var s = Required(accountId);
        if (s.Auth != "authorizationStateReady") throw new InvalidOperationException("Telegram account is not authorized.");
        var userId = await ResolveUser(s, recipient, ct);
        var chat = await _transport.Request(s.ClientId, new() { ["@type"] = "createPrivateChat", ["user_id"] = userId, ["force"] = false }, ct);
        var result = await _transport.Request(s.ClientId, new()
        {
            ["@type"] = "sendMessage", ["chat_id"] = chat["id"]!.DeepClone(),
            ["input_message_content"] = new JsonObject { ["@type"] = "inputMessageText",
                ["text"] = new JsonObject { ["@type"] = "formattedText", ["text"] = message } }
        }, ct);
        if (result["sending_state"] is null) return new(result["id"]!.ToString(), "Sent");
        var key = $"{s.ClientId}:{result["chat_id"]}:{result["id"]}";
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _sends[key] = completion;
        try
        {
            if (_sendResults.TryRemove(key, out var early))
            {
                if (early["@type"]!.GetValue<string>() == "updateMessageSendFailed") throw new TelegramException(early["error"]!["code"]!.GetValue<int>());
                return new(early["message"]!["id"]!.ToString(), "Sent");
            }
            return new(await completion.Task.WaitAsync(TimeSpan.FromSeconds(60), ct), "Sent");
        }
        finally { _sends.TryRemove(key, out _); }
    }
}
