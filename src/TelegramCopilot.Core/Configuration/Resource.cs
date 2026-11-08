using System.Text.Json;
using System.Text.Json.Serialization;

namespace TelegramCopilot.Core.Configuration;

public enum ResourceKind { Accounts, Targets, Schedules, Destinations, Rules, Settings }
public sealed record Resource(Guid Id, ResourceKind Kind, string Name, bool Enabled,
    JsonElement Settings, long Revision, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? DeletedAt = null)
{
    public T Read<T>() => Settings.Deserialize<T>(JsonDefaults.Options)
        ?? throw new ArgumentException("Invalid configuration settings.");
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
}

public sealed record AccountSettings(int ApiId, string ApiHash, string Phone);
public sealed record TargetSettings(Guid AccountId, string Identifier, int RefreshSeconds = 30);
public enum EventType { Online, Offline, Unknown, MonitorRestored, AccountReady, AccountUnavailable,
    WorkerStarted, WorkerStopped, MessageSent, MessageFailed, Error }
public sealed record RuleSettings(Guid DestinationId, EventType Event, Guid? TargetId = null,
    string Title = "{NAME}", string Body = "{EVENT} {TIME_OF_ONLINE}",
    string? Subtitle = null, string? Group = null, JsonElement? Parameters = null, string? Language = null);
public sealed record EncryptionSettings(string Key, string IV, string Mode = "CBC");
public sealed record DestinationSettings(string ServerUrl, string[] DeviceKeys, EncryptionSettings? Encryption = null);
public sealed record SystemSettings(string TimeZone = "Asia/Taipei", int HeartbeatSeconds = 1,
    int StaleAfterSeconds = 5, int SchedulerGraceSeconds = 60);
