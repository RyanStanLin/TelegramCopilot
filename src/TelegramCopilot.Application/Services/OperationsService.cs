using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;

namespace TelegramCopilot.Application.Services;

public interface IOperationsService
{
    Task<AccountStatusResponse> AccountAsync(Guid id, CancellationToken ct);
    Task<AccountStatusResponse> LoginAsync(Guid id, LoginRequest request, CancellationToken ct);
    Task<HealthResponse> HealthAsync(CancellationToken ct);
    Task<PageResponse<EventResponse>> EventsAsync(Guid? targetId, string? type, TimelineQuery query, CancellationToken ct);
    Task<PageResponse<DeliveryResponse>> DeliveriesAsync(string? channel, Guid? id, int page, int pageSize, CancellationToken ct);
}
public sealed class OperationsService(IConfigurationRepository configs, ITelemetryRepository telemetry,
    IDeliveryRepository deliveries, ITelegramGateway telegram, TimeProvider clock) : IOperationsService
{
    public async Task<AccountStatusResponse> AccountAsync(Guid id, CancellationToken ct)
    {
        await RequireAccount(id, ct);
        return await telegram.StatusAsync(id, ct);
    }
    public async Task<AccountStatusResponse> LoginAsync(Guid id, LoginRequest request, CancellationToken ct)
    {
        await RequireAccount(id, ct);
        await telegram.ReconcileAccountsAsync(await configs.ListAsync(ResourceKind.Accounts, ct), ct);
        return await telegram.LoginAsync(id, request, ct);
    }
    public async Task<HealthResponse> HealthAsync(CancellationToken ct)
    {
        var heartbeat = await telemetry.HeartbeatAsync(ct);
        var settings = (await configs.ListAsync(ResourceKind.Settings, ct)).FirstOrDefault(x => x.Enabled)?.Read<SystemSettings>() ?? new();
        var healthy = telegram.NativeAvailable && heartbeat is { } h && (clock.GetUtcNow() - h).TotalSeconds <= settings.StaleAfterSeconds;
        return new(healthy ? "Healthy" : "Degraded", clock.GetUtcNow(), heartbeat, telegram.NativeAvailable, telegram.NativeError);
    }
    public Task<PageResponse<EventResponse>> EventsAsync(Guid? targetId, string? type, TimelineQuery query, CancellationToken ct) =>
        telemetry.EventsAsync(targetId, type, query.From, query.To, query.Page, query.PageSize, ct);
    public Task<PageResponse<DeliveryResponse>> DeliveriesAsync(string? channel, Guid? id, int page, int pageSize, CancellationToken ct) =>
        deliveries.ListAsync(channel, id, page, pageSize, ct);
    private async Task RequireAccount(Guid id, CancellationToken ct)
    {
        if ((await configs.FindAsync(id, ct))?.Kind != ResourceKind.Accounts) throw new KeyNotFoundException("Account not found.");
    }
}
