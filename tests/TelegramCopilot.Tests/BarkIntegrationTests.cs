using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;
using TelegramCopilot.Infrastructure.Notifications;

namespace TelegramCopilot.Tests;

public sealed class BarkIntegrationTests
{
    [Fact]
    public async Task BarkerSharper_TransmitsTemplatesParametersAndEachDeviceKey()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var received = new List<JsonObject>();
            var server = Task.Run(async () =>
            {
                for (var i = 0; i < 2; i++)
                {
                    using var client = await listener.AcceptTcpClientAsync(timeout.Token);
                    await using var stream = client.GetStream();
                    var header = new List<byte>();
                    var one = new byte[1];
                    while (true)
                    {
                        await stream.ReadExactlyAsync(one, timeout.Token); header.Add(one[0]);
                        if (header.Count >= 4 && Encoding.ASCII.GetString(header.TakeLast(4).ToArray()) == "\r\n\r\n") break;
                    }
                    var headers = Encoding.ASCII.GetString(header.ToArray());
                    var lengthLine = headers.Split("\r\n").Single(x => x.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                    var payload = new byte[int.Parse(lengthLine.Split(':')[1].Trim())];
                    await stream.ReadExactlyAsync(payload, timeout.Token);
                    received.Add(JsonNode.Parse(payload)!.AsObject());
                    const string response = "{\"code\":200,\"message\":\"success\",\"timestamp\":1}";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {response.Length}\r\nConnection: close\r\n\r\n{response}"), timeout.Token);
                }
            }, timeout.Token);
            var destination = new DestinationSettings($"http://127.0.0.1:{port}", ["first-test-key", "second-test-key"]);
            var parameters = JsonSerializer.SerializeToElement(new { level = "timeSensitive", sound = "bell", volume = 7, device_key = "must-not-override", url = "https://example.com" });
            var rule = new RuleSettings(Guid.NewGuid(), EventType.Offline, null, "{NAME}", "Online {TIME_OF_ONLINE}", "{NAME} {EVENT}", "presence-{NAME}", parameters);
            var value = new MonitorEvent(1, null, null, "Alice", EventType.Offline, DateTimeOffset.UnixEpoch, 65, "Push");
            await new BarkGateway().SendAsync(destination, rule, value, timeout.Token);
            await server;
            Assert.Equal(new[] { "first-test-key", "second-test-key" }, received.Select(x => x["device_key"]!.ToString()));
            Assert.All(received, payload =>
            {
                Assert.Equal("Alice", payload["title"]!.ToString());
                Assert.Equal("Online 00:01:05", payload["body"]!.ToString());
                Assert.Equal("Alice Offline", payload["subtitle"]!.ToString());
                Assert.Equal("presence-Alice", payload["group"]!.ToString());
                Assert.Equal("timeSensitive", payload["level"]!.ToString());
                Assert.Equal(7, payload["volume"]!.GetValue<int>());
                Assert.Equal("https://example.com", payload["url"]!.ToString());
            });
        }
        finally { listener.Stop(); }
    }
}
