using TelegramCopilot.Api.Realtime;

namespace TelegramCopilot.Api.Endpoints;

public static class RealtimeEndpoints
{
    public static void MapRealtime(this WebApplication app)
    {
        app.Map("/api/live", async (HttpContext context, LiveConnection connection, IHostApplicationLifetime lifetime) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            { context.Response.StatusCode = StatusCodes.Status426UpgradeRequired; return; }
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, lifetime.ApplicationStopping);
            if (long.TryParse(context.Request.Headers["X-Copilot-Session-Expires"], out var expires))
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (expires <= now) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
                cancel.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(expires - now, TimeSpan.FromHours(8).TotalMilliseconds)));
            }
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await connection.RunAsync(socket, cancel.Token);
        }).WithName("RealtimeSnapshots").WithSummary("Subscribe to readonly monitoring and account snapshots over WebSocket")
            .ExcludeFromDescription();
    }
}
