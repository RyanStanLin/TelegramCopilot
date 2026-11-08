namespace TelegramCopilot.Application.Ports;

public interface IRealtimeChanges
{
    IRealtimeSubscription Subscribe();
    void Publish();
}

public interface IRealtimeSubscription : IDisposable
{
    void Refresh();
    IAsyncEnumerable<bool> ReadAsync(CancellationToken ct);
}
