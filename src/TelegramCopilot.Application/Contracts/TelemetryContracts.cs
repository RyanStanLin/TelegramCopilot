namespace TelegramCopilot.Application.Contracts;

/// <summary>Half-open interval in UTC; unknown coverage is not counted as offline.</summary>
public sealed record IntervalResponse(long Id, Guid TargetId, string State, DateTimeOffset Start,
    DateTimeOffset End, double Seconds, string Reason, DateTimeOffset ObservedAt,
    DateTimeOffset? TelegramAt, bool StartKnown, bool EndKnown, double? GapSincePreviousOnlineSeconds);
/// <summary>Time range query; both bounds use ISO 8601 offsets and to is exclusive.</summary>
public sealed record TimelineQuery(DateTimeOffset From, DateTimeOffset To, int Page = 1,
    int PageSize = 100, string? State = null);
/// <summary>One calendar bucket; count is observed session starts, not overlapping fragments.</summary>
public sealed record StatisticsBucket(DateTimeOffset From, DateTimeOffset To, int Count,
    double OnlineSeconds, double OfflineSeconds, double UnknownSeconds,
    double? AverageSessionSeconds, double CoverageRatio);
/// <summary>Calendar aggregation with range totals and a bounded page of buckets.</summary>
public sealed record StatisticsResponse(Guid TargetId, string Period, string TimeZone,
    StatisticsBucket Totals, PageResponse<StatisticsBucket> Buckets);
/// <summary>Durable system and target event.</summary>
public sealed record EventResponse(long Id, Guid? TargetId, Guid? AccountId, string Name, string Type,
    DateTimeOffset At, double? OnlineSeconds, string Detail);
/// <summary>Persistent scheduler or Bark delivery attempt.</summary>
public sealed record DeliveryResponse(string Id, string Channel, Guid ResourceId, DateTimeOffset DueAt,
    string Status, string? ExternalId, string? Error, DateTimeOffset UpdatedAt);
/// <summary>Worker heartbeat and native library availability.</summary>
public sealed record HealthResponse(string Status, DateTimeOffset Now, DateTimeOffset? Heartbeat,
    bool NativeAvailable, string? NativeError);
