using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Infrastructure.Telegram;
using TelegramCopilot.Infrastructure.Workers;

namespace TelegramCopilot.Tests;

internal sealed class FakeTdTransport : ITdTransport
{
    public event Action<int, JsonObject>? Update;
    public bool Available => true;
    public string? Error => null;
    public ConcurrentQueue<int> Clients { get; } = new();
    public ConcurrentQueue<(int Client, JsonObject Body)> Posts { get; } = new();
    public ConcurrentQueue<(int Client, JsonObject Body)> Requests { get; } = new();
    public Func<int, JsonObject, JsonObject> Handle { get; set; } = (_, _) => new() { ["@type"] = "ok" };
    private int _next;
    public int Create() { var id = Interlocked.Increment(ref _next); Clients.Enqueue(id); return id; }
    public void Post(int clientId, JsonObject request)
    {
        Posts.Enqueue((clientId, request.DeepClone().AsObject()));
        if (request["@type"]?.ToString() == "close") Auth(clientId, "authorizationStateClosed");
    }
    public Task<JsonObject> Request(int clientId, JsonObject request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Requests.Enqueue((clientId, request.DeepClone().AsObject()));
        return Task.FromResult(JsonNode.Parse(Handle(clientId, request).ToJsonString())!.AsObject());
    }
    public void Emit(int client, JsonObject update) => Update?.Invoke(client, JsonNode.Parse(update.ToJsonString())!.AsObject());
    public void Auth(int client, string state) => Emit(client,
        new() { ["@type"] = "updateAuthorizationState", ["authorization_state"] = new JsonObject { ["@type"] = state } });
    public void Ready(int client)
    {
        Auth(client, "authorizationStateReady");
        Emit(client, new() { ["@type"] = "updateConnectionState", ["state"] = new JsonObject { ["@type"] = "connectionStateReady" } });
    }
}

internal sealed class TelegramTestHost : IAsyncDisposable
{
    public TestStore Store { get; } = new();
    public FakeTdTransport Transport { get; } = new();
    public ObservationStream Observations { get; } = new();
    public TelegramGateway Gateway { get; }
    public ConfigurationResponse Account { get; private set; } = null!;
    public Resource Target { get; private set; } = null!;
    public int Client => Transport.Clients.First();
    public TelegramTestHost()
    {
        Gateway = new(Transport, Store.Database, Store.Clock, Observations);
        Transport.Handle = DefaultResponse;
    }
    public async Task Initialize(bool ready = true)
    {
        await Store.Initialize();
        Account = await Store.Account();
        var target = await Store.Target(Account.Id);
        Target = (await Store.Configs.FindAsync(target.Id, default))!;
        await Gateway.ReconcileAccountsAsync(await Store.Configs.ListAsync(ResourceKind.Accounts, default), default);
        if (ready) Transport.Ready(Client);
    }
    public JsonObject DefaultResponse(int client, JsonObject request) => request["@type"]!.ToString() switch
    {
        "searchPublicChat" => new() { ["@type"] = "chat", ["id"] = 314,
            ["type"] = new JsonObject { ["@type"] = "chatTypePrivate", ["user_id"] = request["username"]?.ToString() == "bob" ? 43 : 42 } },
        "searchUserByPhoneNumber" => new() { ["@type"] = "user", ["id"] = 42 },
        "getUser" => new() { ["@type"] = "user", ["id"] = request["user_id"]!.DeepClone(),
            ["status"] = new JsonObject { ["@type"] = "userStatusOnline", ["expires"] = Store.Clock.Now.AddMinutes(5).ToUnixTimeSeconds() } },
        "createPrivateChat" => new() { ["@type"] = "chat", ["id"] = 314 },
        _ => new() { ["@type"] = "ok" }
    };
    public Resource TargetWith(string identifier) => Target with
        { Settings = JsonSerializer.SerializeToElement(new TargetSettings(Account.Id, identifier), JsonDefaults.Options) };
    public async ValueTask DisposeAsync()
    {
        await Gateway.DisposeAsync();
        await Store.DisposeAsync();
    }
}
