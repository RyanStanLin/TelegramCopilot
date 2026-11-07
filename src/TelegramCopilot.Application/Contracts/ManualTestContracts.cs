using System.ComponentModel.DataAnnotations;

namespace TelegramCopilot.Application.Contracts;

/// <summary>A field-level configuration problem or non-blocking warning.</summary>
public sealed record ConfigurationIssue(string Field, string Code);
/// <summary>A simulated notification preview; contains no destination device keys.</summary>
public sealed record NotificationPreview(string Destination, int DeviceCount, string Title, string Body,
    string? Subtitle, string? Group, string Event, double? ExampleOnlineSeconds);
/// <summary>Checks the submitted draft without saving it or sending a notification.</summary>
public sealed record RuleValidationResponse(bool Valid, IReadOnlyList<ConfigurationIssue> Errors,
    IReadOnlyList<ConfigurationIssue> Warnings, NotificationPreview? Preview);
/// <summary>Caller-generated request identity prevents duplicate manual sends on replay.</summary>
public sealed record ManualTestRequest([property: Required] Guid RequestId);
/// <summary>Audited outcomes of one manual test; partial failures remain visible per destination.</summary>
public sealed record ManualTestResponse(IReadOnlyList<DeliveryResponse> Deliveries);
