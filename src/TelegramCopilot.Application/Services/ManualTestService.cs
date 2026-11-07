using System.Security.Cryptography;
using System.Text;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Scheduling;

namespace TelegramCopilot.Application.Services;

public interface IManualTestService
{
    Task<ManualTestResponse> ScheduleAsync(Guid id, ManualTestRequest request, CancellationToken ct);
    Task<ManualTestResponse> RuleAsync(Guid id, ManualTestRequest request, CancellationToken ct);
}
public sealed class ManualTestService(IConfigurationRepository configs, IDeliveryRepository deliveries,
    ITelegramGateway telegram, IBarkGateway bark, IRuleValidationService validation, TimeProvider clock) : IManualTestService
{
    public async Task<ManualTestResponse> ScheduleAsync(Guid id, ManualTestRequest request, CancellationToken ct)
    {
        var resource = await Required(id, ResourceKind.Schedules, request, ct);
        var settings = resource.Read<ScheduleSettings>(); ConfigurationPolicy.Validate(resource);
        var delivery = await Dispatch($"test:telegram:{id}:{request.RequestId}", "Telegram", id, async token =>
            await telegram.SendAsync(settings.AccountId, settings.Recipient, settings.Message, token), ct);
        return new([delivery]);
    }
    public async Task<ManualTestResponse> RuleAsync(Guid id, ManualTestRequest request, CancellationToken ct)
    {
        var resource = await Required(id, ResourceKind.Rules, request, ct);
        var check = await validation.ValidateAsync(new() { Name = resource.Name, Enabled = resource.Enabled, Settings = resource.Settings }, ct);
        if (!check.Valid) throw new ArgumentException("The notification rule is invalid. Validate and correct it before testing.");
        var rule = resource.Read<RuleSettings>();
        var destination = (await configs.FindAsync(rule.DestinationId, ct))!.Read<DestinationSettings>();
        var target = rule.TargetId is { } targetId ? await configs.FindAsync(targetId, ct) : null;
        var value = RuleValidationService.TestEvent(rule, target, clock.GetUtcNow());
        var results = new List<DeliveryResponse>();
        foreach (var key in destination.DeviceKeys.Distinct())
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
            results.Add(await Dispatch($"test:bark:{id}:{request.RequestId}:{hash}", "Bark", id, async token =>
            {
                await bark.SendAsync(destination with { DeviceKeys = [key] }, rule, value, token);
                return new("", "Sent");
            }, ct));
        }
        return new(results);
    }
    private async Task<DeliveryResponse> Dispatch(string id, string channel, Guid resourceId,
        Func<CancellationToken, Task<SendReceipt>> send, CancellationToken ct)
    {
        var at = clock.GetUtcNow();
        if (!await deliveries.ClaimAsync(new(id, channel, resourceId, at, "Sending", null, null, at), ct))
            return (await deliveries.FindAsync(id, ct))!;
        try
        {
            var receipt = await send(ct);
            await deliveries.CompleteAsync(id, receipt.Status, string.IsNullOrEmpty(receipt.MessageId) ? null : receipt.MessageId, null, clock.GetUtcNow(), CancellationToken.None);
        }
        catch (Exception ex)
        {
            await deliveries.CompleteAsync(id, ex is TimeoutException or OperationCanceledException ? "Uncertain" : "Failed",
                null, ex.GetType().Name, clock.GetUtcNow(), CancellationToken.None);
        }
        return (await deliveries.FindAsync(id, CancellationToken.None))!;
    }
    private async Task<Resource> Required(Guid id, ResourceKind kind, ManualTestRequest request, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty) throw new ArgumentException("A non-empty requestId is required for a manual test.");
        return await configs.FindAsync(id, ct) is { } resource && resource.Kind == kind
            ? resource : throw new KeyNotFoundException("Configuration not found.");
    }
}
