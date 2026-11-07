using System.Text.Json.Nodes;

namespace TelegramCopilot.Infrastructure.Telegram;

public interface ITdTransport
{
    event Action<int, JsonObject>? Update;
    bool Available { get; }
    string? Error { get; }
    int Create();
    void Post(int clientId, JsonObject request);
    Task<JsonObject> Request(int clientId, JsonObject request, CancellationToken ct);
}
