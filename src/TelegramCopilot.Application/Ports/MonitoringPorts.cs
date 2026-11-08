using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Application.Ports;

public interface IMonitoringRepository
{
    Task StartPollAsync(Resource target, DateTimeOffset at, DateTimeOffset next, CancellationToken ct);
    Task CompletePollAsync(Resource target, Observation result, string? error, CancellationToken ct);
    Task ObserveAsync(Resource target, Observation observation, CancellationToken ct);
    Task<TargetMonitoringResponse> GetAsync(Resource target, DateTimeOffset now, CancellationToken ct);
    Task<(long Count, DateTimeOffset? LastPoll, long Successful, long Failed)> TotalsAsync(CancellationToken ct);
    Task RecoverAsync(DateTimeOffset at, CancellationToken ct);
}
