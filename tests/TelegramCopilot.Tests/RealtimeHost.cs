using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TelegramCopilot.Api.Endpoints;
using TelegramCopilot.Api.Realtime;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Application.Services;
using TelegramCopilot.Infrastructure.Persistence;

namespace TelegramCopilot.Tests;

internal sealed class RealtimeHost : IAsyncDisposable
{
    public TelegramTestHost Telegram { get; } = new();
    public MonitoringRepository Monitoring { get; private set; } = null!;
    public WebApplication App { get; private set; } = null!;
    public Uri Address { get; private set; } = null!;
    public async Task Initialize()
    {
        await Telegram.Initialize();
        var store = Telegram.Store;
        Monitoring = new(store.Database);
        var operations = new OperationsService(store.Configs, store.Telemetry, store.Deliveries, Telegram.Gateway, store.Clock);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IRealtimeChanges>(store.Database.Changes);
        builder.Services.AddSingleton<IOperationsService>(operations);
        builder.Services.AddSingleton<ITelemetryService>(store.Analytics);
        builder.Services.AddSingleton<IMonitoringService>(new MonitoringService(store.Configs, Monitoring, operations,
            Telegram.Gateway, store.Analytics, store.Clock));
        builder.Services.AddSingleton<LiveSnapshots>(); builder.Services.AddSingleton<LiveConnection>();
        App = builder.Build(); App.UseWebSockets(); App.MapRealtime();
        await App.StartAsync();
        Address = new(App.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
    }
    public async Task<ClientWebSocket> Connect(string? expires = null)
    {
        var socket = new ClientWebSocket();
        if (expires is not null) socket.Options.SetRequestHeader("X-Copilot-Session-Expires", expires);
        await socket.ConnectAsync(new UriBuilder(Address) { Scheme = "ws", Path = "/api/live" }.Uri, default);
        return socket;
    }
    public static Task Send(ClientWebSocket socket, string path, string type = "subscribe") =>
        socket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type, path })), WebSocketMessageType.Text, true, default);
    public static async Task<JsonElement> Read(ClientWebSocket socket, Func<JsonElement, bool> match)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[16384];
        while (true)
        {
            var count = 0;
            WebSocketReceiveResult frame;
            do
            {
                frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, count, buffer.Length - count), timeout.Token);
                Assert.Equal(WebSocketMessageType.Text, frame.MessageType);
                count += frame.Count;
            } while (!frame.EndOfMessage);
            using var document = JsonDocument.Parse(buffer.AsMemory(0, count));
            if (match(document.RootElement)) return document.RootElement.Clone();
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (App is not null) { await App.StopAsync(); await App.DisposeAsync(); }
        await Telegram.DisposeAsync();
    }
}
