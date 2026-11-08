using System.Text.Json;
using System.Text.RegularExpressions;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Application.Services;

public interface IRuleValidationService
{
    Task<RuleValidationResponse> ValidateAsync(SaveConfigurationRequest request, CancellationToken ct);
}
public sealed class RuleValidationService(IConfigurationRepository configs, TimeProvider clock) : IRuleValidationService
{
    private static readonly string[] Variables = ["NAME", "EVENT", "TIME_OF_ONLINE", "ONLINE_SECONDS", "AT", "DETAIL", "TARGET_ID", "ACCOUNT_ID"];
    public async Task<RuleValidationResponse> ValidateAsync(SaveConfigurationRequest request, CancellationToken ct)
    {
        var errors = new List<ConfigurationIssue>(); var warnings = new List<ConfigurationIssue>();
        RuleSettings rule;
        try
        {
            var resource = new Resource(Guid.NewGuid(), ResourceKind.Rules, request.Name, request.Enabled, request.Settings, 1, clock.GetUtcNow(), clock.GetUtcNow());
            ConfigurationPolicy.Validate(resource); rule = resource.Read<RuleSettings>();
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException or InvalidOperationException)
        { return new(false, [new("settings", "InvalidRuleConfiguration")], [], null); }
        var destination = await configs.FindAsync(rule.DestinationId, ct);
        if (destination?.Kind != ResourceKind.Destinations) errors.Add(new("destinationId", "MissingDestination"));
        else if (!destination.Enabled) warnings.Add(new("destinationId", "DestinationDisabled"));
        Resource? target = rule.TargetId is { } id ? await configs.FindAsync(id, ct) : null;
        if (rule.TargetId is not null && target?.Kind != ResourceKind.Targets) errors.Add(new("targetId", "MissingTarget"));
        if (!request.Enabled) warnings.Add(new("enabled", "RuleDisabled"));
        foreach (var (field, text) in new[] { ("title", rule.Title), ("body", rule.Body), ("subtitle", rule.Subtitle), ("group", rule.Group) })
            CheckTemplate(field, text, errors);
        if (rule.Parameters is { } parameters)
            foreach (var parameter in parameters.EnumerateObject())
            {
                var field = "parameters." + parameter.Name;
                if (parameter.Name is "device_key" or "device_keys" or "ciphertext" or "iv" or "title" or "body" or "subtitle" or "group")
                    errors.Add(new(field, "ReservedBarkParameter"));
                if (parameter.Value.ValueKind == JsonValueKind.String) CheckTemplate(field, parameter.Value.GetString(), errors);
                if (parameter.Name is "badge" or "ttl" && (parameter.Value.ValueKind != JsonValueKind.Number || !parameter.Value.TryGetInt64(out var number) || number < 0))
                    errors.Add(new(field, "NonNegativeIntegerRequired"));
                if (parameter.Name == "level" && (parameter.Value.ValueKind != JsonValueKind.String
                    || parameter.Value.GetString() is not ("critical" or "active" or "timeSensitive" or "passive")))
                    errors.Add(new(field, "InvalidBarkLevel"));
            }
        if (errors.Count > 0) return new(false, errors, warnings, null);
        var value = TestEvent(rule, target, clock.GetUtcNow());
        return new(true, errors, warnings, new(destination!.Name, destination.Read<DestinationSettings>().DeviceKeys.Distinct().Count(),
            TemplateRenderer.Render(rule.Title, value, rule.Language), TemplateRenderer.Render(rule.Body, value, rule.Language),
            rule.Subtitle is null ? null : TemplateRenderer.Render(rule.Subtitle, value, rule.Language),
            rule.Group is null ? null : TemplateRenderer.Render(rule.Group, value, rule.Language), rule.Event.ToString(), value.OnlineSeconds));
    }
    private static void CheckTemplate(string field, string? text, List<ConfigurationIssue> errors)
    {
        foreach (Match match in Regex.Matches(text ?? "", @"\{([A-Za-z_][A-Za-z0-9_]*)\}"))
            if (!Variables.Contains(match.Groups[1].Value)) errors.Add(new(field, "UnknownTemplateVariable"));
    }
    internal static MonitorEvent TestEvent(RuleSettings rule, Resource? target, DateTimeOffset at) =>
        new(0, rule.TargetId, target?.Read<TargetSettings>().AccountId, target?.Name ?? "TelegramCopilot test",
            rule.Event, at, rule.Event == EventType.Offline ? 9121 : null, "Manual test notification");
}
