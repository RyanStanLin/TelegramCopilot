namespace TelegramCopilot.Application.Contracts;

/// <summary>Telegram profile and privacy-aware last-seen information, never inferred from local polling.</summary>
public sealed record TelegramProfileResponse(long UserId, string DisplayName, string? Username,
    bool HasAvatar, string? AvatarVersion, string? TelegramStatus, DateTimeOffset? LastSeenAt,
    DateTimeOffset? UpdatedAt);
/// <summary>Durable counters incremented by the background worker, independent of browser refreshes.</summary>
public sealed record TargetMonitoringResponse(Guid TargetId, bool Enabled, long PollCount, long SuccessfulPollCount,
    long FailedPollCount, DateTimeOffset? LastPollStartedAt, DateTimeOffset? LastPollCompletedAt,
    DateTimeOffset? LastSuccessfulPollAt, DateTimeOffset? NextPollAt, string State, string Reason,
    string? Error, bool IsPolling, DateTimeOffset Now, TelegramProfileResponse? Profile);
/// <summary>Worker liveness and the lifetime total of actual scheduled target checks.</summary>
public sealed record MonitoringOverviewResponse(DateTimeOffset Now, HealthResponse Health,
    long PollCount, int EnabledTargets, DateTimeOffset? LastPollAt);
