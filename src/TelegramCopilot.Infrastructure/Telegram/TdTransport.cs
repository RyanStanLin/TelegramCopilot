using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TelegramCopilot.Infrastructure.Telegram;

public sealed class TelegramException(int code) : Exception($"Telegram request failed (code {code}). Check account status or retry after any flood-wait.")
{
    public int Code { get; } = code;
}
public sealed class TdTransport(ILogger<TdTransport> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonObject>> _pending = new();
    private readonly ConcurrentDictionary<int, byte> _clients = new();
    public event Action<int, JsonObject>? Update;
    private volatile bool _available;
    private volatile string? _error;
    public bool Available => _available;
    public string? Error => _error;
    public int Create()
    {
        if (!Available) throw new InvalidOperationException("Native TDLib is unavailable. Install libtdjson or use the Docker image.");
        var id = TdNative.Create();
        _clients[id] = 0;
        Post(id, new() { ["@type"] = "getAuthorizationState" });
        return id;
    }
    public void Post(int clientId, JsonObject request) => TdNative.Send(clientId, request.ToJsonString());
    public async Task<JsonObject> Request(int clientId, JsonObject request, CancellationToken ct)
    {
        var key = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        request["@extra"] = key;
        _pending[key] = completion;
        try
        {
            Post(clientId, request);
            var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            if (result["@type"]?.GetValue<string>() == "error") throw new TelegramException(result["code"]!.GetValue<int>());
            return result;
        }
        finally { _pending.TryRemove(key, out _); }
    }
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Factory.StartNew(() =>
    {
        try
        {
            TdNative.Execute("{\"@type\":\"setLogVerbosityLevel\",\"new_verbosity_level\":0}");
            _available = true;
            DateTimeOffset? shutdownAt = null;
            while (true)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    if (shutdownAt is null)
                    {
                        shutdownAt = DateTimeOffset.UtcNow.AddSeconds(10);
                        foreach (var id in _clients.Keys) Post(id, new() { ["@type"] = "close" });
                    }
                    if (_clients.IsEmpty || DateTimeOffset.UtcNow >= shutdownAt) break;
                }
                var pointer = TdNative.Receive(0.2);
                if (pointer == 0) continue;
                var text = Marshal.PtrToStringUTF8(pointer)!;
                var data = JsonNode.Parse(text)!.AsObject();
                var clientId = data["@client_id"]?.GetValue<int>() ?? 0;
                if (data["authorization_state"]?["@type"]?.GetValue<string>() == "authorizationStateClosed") _clients.TryRemove(clientId, out _);
                var key = data["@extra"]?.GetValue<string>();
                if (key is not null && _pending.TryGetValue(key, out var pending)) pending.TrySetResult(data);
                try { Update?.Invoke(clientId, data); }
                catch (Exception ex) { logger.LogError("TDLib update handler failed: {Type}", ex.GetType().Name); }
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            _error = "TDLib native library unavailable or incompatible";
            logger.LogWarning("{Error}", Error);
        }
        finally
        {
            _available = false;
            foreach (var p in _pending.Values) p.TrySetCanceled();
        }
    }, stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
}
