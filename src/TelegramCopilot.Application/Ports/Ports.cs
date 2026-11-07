using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Application.Ports;

public interface IConfigurationRepository
{
    Task<IReadOnlyList<Resource>> ListAsync(ResourceKind? kind, CancellationToken ct);
    Task<Resource?> FindAsync(Guid id, CancellationToken ct, bool includeDeleted = false);
    Task SaveAsync(Resource value, long? previousRevision, CancellationToken ct);
    Task DeleteAsync(Guid id, long revision, CancellationToken ct);
}
public interface ITelemetryRepository
{
    Task RecordAsync(Observation observation, string name, Guid? accountId, CancellationToken ct);
    Task<IReadOnlyList<PresenceInterval>> IntervalsAsync(Guid targetId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<long> EventAsync(MonitorEvent value, CancellationToken ct);
    Task<PageResponse<EventResponse>> EventsAsync(Guid? targetId, string? type, DateTimeOffset from,
        DateTimeOffset to, int page, int pageSize, CancellationToken ct);
    Task<DateTimeOffset?> HeartbeatAsync(CancellationToken ct);
    Task CheckpointAsync(DateTimeOffset at, CancellationToken ct);
    Task RecoverAsync(DateTimeOffset now, CancellationToken ct);
    Task<MonitorEvent?> NextNotificationEventAsync(CancellationToken ct);
    Task CompleteNotificationEventAsync(long id, CancellationToken ct);
}
public interface IDeliveryRepository
{
    Task<bool> ClaimAsync(DeliveryResponse delivery, CancellationToken ct);
    Task CompleteAsync(string id, string status, string? externalId, string? error, DateTimeOffset at, CancellationToken ct);
    Task<PageResponse<DeliveryResponse>> ListAsync(string? channel, Guid? resourceId, int page, int pageSize, CancellationToken ct);
    Task RecoverAsync(DateTimeOffset at, CancellationToken ct);
}
public interface ITelegramGateway
{
    bool NativeAvailable { get; }
    string? NativeError { get; }
    Task ReconcileAccountsAsync(IReadOnlyList<Resource> accounts, CancellationToken ct);
    Task<AccountStatusResponse> StatusAsync(Guid accountId, CancellationToken ct);
    Task<AccountStatusResponse> LoginAsync(Guid accountId, LoginRequest request, CancellationToken ct);
    Task<Observation> ObserveAsync(Resource target, CancellationToken ct);
    Task<SendReceipt> SendAsync(Guid accountId, string recipient, string message, CancellationToken ct);
    IAsyncEnumerable<Observation> UpdatesAsync(CancellationToken ct);
}
public interface IBarkGateway
{
    Task SendAsync(DestinationSettings destination, RuleSettings rule, MonitorEvent value, CancellationToken ct);
}
