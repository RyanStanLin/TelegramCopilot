using System.Threading.Channels;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Infrastructure.Workers;

public sealed class ObservationStream : IObservationStream
{
    private readonly Channel<Observation> _channel = Channel.CreateUnbounded<Observation>(new() { SingleReader = true });
    public void Publish(Observation observation) => _channel.Writer.TryWrite(observation);
    public IAsyncEnumerable<Observation> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
