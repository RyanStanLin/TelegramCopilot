using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using TelegramCopilot.Application.Ports;

namespace TelegramCopilot.Api.Realtime;

public sealed class LiveConnection(LiveSnapshots snapshots, IRealtimeChanges changes,
    Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json)
{
    private sealed record Command(string Type, string Path);
    public async Task RunAsync(WebSocket socket, CancellationToken stopping)
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        using var subscription = changes.Subscribe();
        var paths = new ConcurrentDictionary<string, byte>();
        var closeStatus = WebSocketCloseStatus.NormalClosure;
        var receive = Receive();
        var send = Send();
        await Task.WhenAny(receive, send);
        await cancel.CancelAsync();
        try { await Task.WhenAll(receive, send); }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        catch (WebSocketException) { }
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            using var close = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { await socket.CloseAsync(closeStatus, "Closed", close.Token); }
            catch (Exception error) when (error is WebSocketException or OperationCanceledException) { socket.Abort(); }
        }

        async Task Receive()
        {
            var buffer = new byte[4096];
            while (!cancel.IsCancellationRequested)
            {
                var length = 0;
                ValueWebSocketReceiveResult frame;
                do
                {
                    frame = await socket.ReceiveAsync(buffer.AsMemory(length), cancel.Token);
                    if (frame.MessageType == WebSocketMessageType.Close) return;
                    if (frame.MessageType != WebSocketMessageType.Text)
                    { await Reject(WebSocketCloseStatus.InvalidMessageType); return; }
                    length += frame.Count;
                    if (length == buffer.Length && !frame.EndOfMessage)
                    { await Reject(WebSocketCloseStatus.MessageTooBig); return; }
                } while (!frame.EndOfMessage);
                Command? command;
                try { command = JsonSerializer.Deserialize<Command>(buffer.AsSpan(0, length), json.Value.SerializerOptions); }
                catch (JsonException) { await Reject(WebSocketCloseStatus.InvalidPayloadData); return; }
                if (command is null || string.IsNullOrEmpty(command.Path) || command.Path.Length > 512
                    || command.Type is not ("subscribe" or "unsubscribe" or "refresh"))
                { await Reject(WebSocketCloseStatus.PolicyViolation); return; }
                if (command.Type == "unsubscribe") paths.TryRemove(command.Path, out _);
                else
                {
                    if (paths.Count >= 32 && !paths.ContainsKey(command.Path))
                    { await Reject(WebSocketCloseStatus.PolicyViolation); return; }
                    paths[command.Path] = 0;
                    subscription.Refresh();
                }
            }
        }
        async Task Send()
        {
            await foreach (var _ in subscription.ReadAsync(cancel.Token))
                foreach (var path in paths.Keys)
                {
                    object message;
                    try { message = new { path, data = await snapshots.ReadAsync(path, cancel.Token) }; }
                    catch (KeyNotFoundException) { message = new { path, error = "NotFound" }; }
                    catch (ArgumentException) { message = new { path, error = "InvalidSubscription" }; }
                    catch (Exception) when (!cancel.IsCancellationRequested) { message = new { path, error = "Unavailable" }; }
                    var payload = JsonSerializer.SerializeToUtf8Bytes(message, json.Value.SerializerOptions);
                    await socket.SendAsync(payload, WebSocketMessageType.Text, true, cancel.Token);
                }
        }
        Task Reject(WebSocketCloseStatus status)
        {
            closeStatus = status;
            return Task.CompletedTask;
        }
    }
}
