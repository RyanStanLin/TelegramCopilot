namespace TelegramCopilot.Core.Presence;

public sealed record TelegramProfile(long UserId, string FirstName, string LastName, string? Username,
    int? PhotoFileId = null, string? PhotoId = null);
