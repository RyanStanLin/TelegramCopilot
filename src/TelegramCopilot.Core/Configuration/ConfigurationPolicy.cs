using System.Text;
using System.Text.RegularExpressions;
using TelegramCopilot.Core.Scheduling;

namespace TelegramCopilot.Core.Configuration;

public static partial class ConfigurationPolicy
{
    public static void Validate(Resource resource)
    {
        if (string.IsNullOrWhiteSpace(resource.Name) || resource.Name.Length > 120)
            throw new ArgumentException("Name must contain 1 to 120 characters.");
        switch (resource.Kind)
        {
            case ResourceKind.Accounts:
                var account = resource.Read<AccountSettings>();
                if (account.ApiId <= 0 || !ApiHash().IsMatch(account.ApiHash) || !Phone().IsMatch(account.Phone))
                    throw new ArgumentException("Use a positive apiId, a 32-character API hash and an international phone number.");
                break;
            case ResourceKind.Targets:
                var target = resource.Read<TargetSettings>();
                if (target.AccountId == Guid.Empty || !ValidIdentifier(target.Identifier) || target.RefreshSeconds is < 1 or > 3600)
                    throw new ArgumentException("Target requires an account, username/phone and refreshSeconds from 1 to 3600.");
                break;
            case ResourceKind.Schedules:
                var schedule = resource.Read<ScheduleSettings>();
                if (schedule.AccountId == Guid.Empty || !ValidIdentifier(schedule.Recipient)
                    || string.IsNullOrWhiteSpace(schedule.Message) || schedule.Message.Length > 4096)
                    throw new ArgumentException("Schedule requires an account, recipient and message (1 to 4096 characters).");
                schedule.Validate();
                break;
            case ResourceKind.Destinations:
                ValidateDestination(resource.Read<DestinationSettings>());
                break;
            case ResourceKind.Rules:
                var rule = resource.Read<RuleSettings>();
                if (rule.DestinationId == Guid.Empty || !Enum.IsDefined(rule.Event)
                    || string.IsNullOrWhiteSpace(rule.Title) || string.IsNullOrWhiteSpace(rule.Body))
                    throw new ArgumentException("Rule requires destination, event, title and body.");
                if (rule.Parameters is { ValueKind: not System.Text.Json.JsonValueKind.Object })
                    throw new ArgumentException("Bark parameters must be a JSON object.");
                if (rule.Language is not (null or "zh-CN" or "en"))
                    throw new ArgumentException("Notification language must be zh-CN or en.");
                break;
            case ResourceKind.Settings:
                var system = resource.Read<SystemSettings>();
                TimeZoneInfo.FindSystemTimeZoneById(system.TimeZone);
                if (system.HeartbeatSeconds is < 1 or > 10 || system.StaleAfterSeconds < system.HeartbeatSeconds * 2
                    || system.StaleAfterSeconds > 120 || system.SchedulerGraceSeconds is < 1 or > 3600)
                    throw new ArgumentException("Invalid heartbeat, stale threshold or scheduler grace period.");
                break;
        }
    }

    public static IEnumerable<(ResourceKind Kind, Guid Id)> References(Resource resource) => resource.Kind switch
    {
        ResourceKind.Targets => [(ResourceKind.Accounts, resource.Read<TargetSettings>().AccountId)],
        ResourceKind.Schedules => [(ResourceKind.Accounts, resource.Read<ScheduleSettings>().AccountId)],
        ResourceKind.Rules => RuleReferences(resource.Read<RuleSettings>()),
        _ => []
    };

    private static IEnumerable<(ResourceKind, Guid)> RuleReferences(RuleSettings rule)
    {
        yield return (ResourceKind.Destinations, rule.DestinationId);
        if (rule.TargetId is { } id) yield return (ResourceKind.Targets, id);
    }

    private static void ValidateDestination(DestinationSettings settings)
    {
        if (!Uri.TryCreate(settings.ServerUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            || settings.DeviceKeys is not { Length: > 0 } || settings.DeviceKeys.Length > 100
            || settings.DeviceKeys.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Destination requires an HTTP(S) base URL and 1 to 100 device keys.");
        if (settings.Encryption is { } e && (Encoding.UTF8.GetByteCount(e.Key) is not (16 or 24 or 32)
            || Encoding.UTF8.GetByteCount(e.IV) != 16 || e.Mode is not ("CBC" or "ECB")))
            throw new ArgumentException("Bark encryption requires 16/24/32-byte UTF-8 key, 16-byte IV and CBC/ECB.");
    }

    public static bool ValidIdentifier(string value) => !string.IsNullOrWhiteSpace(value)
        && (Username().IsMatch(value) || Phone().IsMatch(value));
    [GeneratedRegex("^@?[A-Za-z][A-Za-z0-9_]{0,31}$")] private static partial Regex Username();
    [GeneratedRegex("^\\+[1-9][0-9]{5,14}$")] private static partial Regex Phone();
    [GeneratedRegex("^[a-fA-F0-9]{32}$")] private static partial Regex ApiHash();
}
