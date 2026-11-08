using Microsoft.Extensions.Configuration;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Infrastructure.Notifications;

public sealed class NotificationAvatar(IConfiguration configuration, IConfigurationRepository configs,
    IMonitoringRepository monitoring, TimeProvider clock)
{
    public async Task<string?> ForEventAsync(MonitorEvent value, CancellationToken ct)
    {
        if (value.TargetId is not { } id || !Uri.TryCreate(configuration["PublicApiOrigin"], UriKind.Absolute, out var origin)
            || origin.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(origin.UserInfo)) return null;
        if (await configs.FindAsync(id, ct) is not { Kind: ResourceKind.Targets } target || target.UpdatedAt > value.At) return null;
        var snapshot = await monitoring.GetAsync(target, clock.GetUtcNow(), ct);
        return snapshot.Profile is { HasAvatar: true } profile
            ? new Uri(origin, $"/api/targets/{id}/avatar?v={Uri.EscapeDataString(profile.AvatarVersion ?? "")}").AbsoluteUri : null;
    }
}
