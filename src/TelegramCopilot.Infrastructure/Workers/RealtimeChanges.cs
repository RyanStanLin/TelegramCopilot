using System.Collections.Concurrent;
using System.Threading.Channels;
using TelegramCopilot.Application.Ports;

namespace TelegramCopilot.Infrastructure.Workers;

public sealed class RealtimeChanges : IRealtimeChanges
{
    private readonly ConcurrentDictionary<Guid, Subscription> _readers = new();
    public IRealtimeSubscription Subscribe()
    {
        var id = Guid.NewGuid();
        var reader = new Subscription(() => _readers.TryRemove(id, out _));
        _readers[id] = reader;
        return reader;
    }
    public void Publish()
    {
        foreach (var reader in _readers.Values) reader.Refresh();
    }
    private sealed class Subscription(Action remove) : IRealtimeSubscription
    {
        private readonly Channel<bool> _pending = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, AllowSynchronousContinuations = false });
        public void Refresh() => _pending.Writer.TryWrite(true);
        public IAsyncEnumerable<bool> ReadAsync(CancellationToken ct) => _pending.Reader.ReadAllAsync(ct);
        public void Dispose()
        {
            remove();
            _pending.Writer.TryComplete();
        }
    }
}
