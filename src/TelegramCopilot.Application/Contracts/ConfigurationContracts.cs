using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace TelegramCopilot.Application.Contracts;

/// <summary>A versioned configuration resource. Secret values are returned as ***.</summary>
public sealed record ConfigurationResponse(Guid Id, string Kind, string Name, bool Enabled,
    JsonElement Settings, long Revision, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
/// <summary>Create or replace settings; revision is required on updates.</summary>
public sealed record SaveConfigurationRequest
{
    [Required, MaxLength(120)] public required string Name { get; init; }
    public bool Enabled { get; init; } = true;
    public required JsonElement Settings { get; init; }
    public long? Revision { get; init; }
}
/// <summary>A stable, bounded page with an exact total count.</summary>
public sealed record PageResponse<T>(IReadOnlyList<T> Items, long Total, int Page, int PageSize);
/// <summary>One Telegram account's login and connection status; no challenge secrets.</summary>
public sealed record AccountStatusResponse(Guid AccountId, string AuthorizationState, string ConnectionState, string? Error);
/// <summary>Submit a Telegram challenge using code, password, email or emailCode.</summary>
public sealed record LoginRequest([property: Required] string Type, [property: Required] string Value);
/// <summary>Telegram message submission acknowledgement.</summary>
public sealed record SendReceipt(string MessageId, string Status);
