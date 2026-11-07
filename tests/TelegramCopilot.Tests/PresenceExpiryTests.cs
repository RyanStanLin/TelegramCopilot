using TelegramCopilot.Core.Presence;
using TelegramCopilot.Infrastructure.Persistence;

namespace TelegramCopilot.Tests;

public sealed class PresenceExpiryTests
{
    [Fact]
    public async Task ExpiredOnlineCoverage_IsCappedAndDelayedOfflineHasAnUnknownGap()
    {
        await using var s = new TestStore(); await s.Initialize(); var id = Guid.NewGuid(); var at = s.Clock.Now;
        await s.Telemetry.RecordAsync(new(id, PresenceState.Offline, at, "Push"), "Alice", null, default);
        await s.Telemetry.RecordAsync(new(id, PresenceState.Online, at.AddSeconds(10), "Push", ExpiresAt: at.AddSeconds(20)), "Alice", null, default);
        await s.Telemetry.CheckpointAsync(at.AddSeconds(30), default);
        var before = await s.Telemetry.IntervalsAsync(id, at, at.AddSeconds(30), default);
        Assert.Equal(at.AddSeconds(20), Assert.Single(before, row => row.State == PresenceState.Online).End);
        await s.Telemetry.RecordAsync(new(id, PresenceState.Offline, at.AddSeconds(25), "Push"), "Alice", null, default);
        var rows = await s.Telemetry.IntervalsAsync(id, at, at.AddSeconds(30), default);
        var unknown = Assert.Single(rows, row => row.State == PresenceState.Unknown);
        Assert.Equal(at.AddSeconds(20), unknown.Start);
        Assert.Equal(at.AddSeconds(25), unknown.End);
        Assert.Equal("ExpiredOnlineStatus", unknown.Reason);
        Assert.False(Assert.Single(rows, row => row.State == PresenceState.Online).EndKnown);
        var events = await s.Telemetry.EventsAsync(id, "Offline", at, at.AddSeconds(30), 1, 100, default);
        Assert.All(events.Items, value => Assert.Null(value.OnlineSeconds));
    }

    [Fact]
    public async Task TimelyOnlineRenewal_ExtendsExpiryWithoutAnotherSession()
    {
        await using var s = new TestStore(); await s.Initialize(); var id = Guid.NewGuid(); var at = s.Clock.Now;
        await s.Telemetry.RecordAsync(new(id, PresenceState.Online, at, "Push", ExpiresAt: at.AddSeconds(10)), "Alice", null, default);
        await s.Telemetry.RecordAsync(new(id, PresenceState.Online, at.AddSeconds(5), "Push", ExpiresAt: at.AddSeconds(20)), "Alice", null, default);
        await s.Telemetry.CheckpointAsync(at.AddSeconds(25), default);
        var row = Assert.Single(await s.Telemetry.IntervalsAsync(id, at, at.AddSeconds(25), default));
        Assert.Equal(at.AddSeconds(20), row.End);
        var events = await s.Telemetry.EventsAsync(id, "Online", at, at.AddSeconds(25), 1, 100, default);
        Assert.Single(events.Items);
    }

    [Fact]
    public async Task Recovery_DistinguishesExpiredCoverageFromProcessDowntime()
    {
        await using var s = new TestStore(); await s.Initialize(); var id = Guid.NewGuid(); var at = s.Clock.Now;
        await s.Telemetry.RecordAsync(new(id, PresenceState.Online, at, "Push", ExpiresAt: at.AddSeconds(10)), "Alice", null, default);
        await s.Telemetry.CheckpointAsync(at.AddSeconds(20), default);
        await s.Telemetry.RecoverAsync(at.AddSeconds(40), default);
        var rows = await s.Telemetry.IntervalsAsync(id, at, at.AddSeconds(50), default);
        Assert.Equal(new[] { PresenceState.Online, PresenceState.Unknown, PresenceState.Unknown }, rows.Select(row => row.State));
        Assert.Equal(at.AddSeconds(10), rows[0].End);
        Assert.Equal("ExpiredOnlineStatus", rows[1].Reason);
        Assert.Equal(at.AddSeconds(10), rows[1].Start);
        Assert.Equal(at.AddSeconds(20), rows[1].End);
        Assert.Equal("WorkerUnavailable", rows[2].Reason);
        Assert.Equal(at.AddSeconds(20), rows[2].Start);
    }

    [Fact]
    public async Task ExpiryObservation_DoesNotEmitDuplicateUnknownEvents()
    {
        await using var s = new TestStore(); await s.Initialize(); var id = Guid.NewGuid(); var at = s.Clock.Now;
        await s.Telemetry.RecordAsync(new(id, PresenceState.Online, at, "Push", ExpiresAt: at.AddSeconds(10)), "Alice", null, default);
        await s.Telemetry.RecordAsync(new(id, PresenceState.Unknown, at.AddSeconds(20), "ExpiredOnlineStatus"), "Alice", null, default);
        var rows = await s.Telemetry.IntervalsAsync(id, at, at.AddSeconds(30), default);
        var unknown = Assert.Single(rows, value => value.State == PresenceState.Unknown);
        Assert.Equal(at.AddSeconds(10), unknown.Start);
        Assert.Equal(at.AddSeconds(20), unknown.ObservedAt);
        Assert.Single((await s.Telemetry.EventsAsync(id, "Unknown", at, at.AddSeconds(30), 1, 100, default)).Items);
    }

    [Fact]
    public async Task LateOnlineRenewal_PreservesTheUnobservedGap()
    {
        await using var s = new TestStore(); await s.Initialize(); var id = Guid.NewGuid(); var at = s.Clock.Now;
        await s.Telemetry.RecordAsync(new(id, PresenceState.Online, at, "Push", ExpiresAt: at.AddSeconds(10)), "Alice", null, default);
        await s.Telemetry.RecordAsync(new(id, PresenceState.Online, at.AddSeconds(15), "Push", ExpiresAt: at.AddSeconds(30)), "Alice", null, default);
        await s.Telemetry.CheckpointAsync(at.AddSeconds(20), default);
        var rows = await s.Telemetry.IntervalsAsync(id, at, at.AddSeconds(20), default);
        Assert.Equal(new[] { PresenceState.Online, PresenceState.Unknown, PresenceState.Online }, rows.Select(value => value.State));
        Assert.Equal(5, rows[1].Seconds(at.AddSeconds(20)));
        Assert.False(rows[0].EndKnown);
        Assert.False(rows[2].StartKnown);
    }

    [Fact]
    public async Task VersionOneDatabase_IsUpgradedWithoutLosingIntervals()
    {
        await using var s = new TestStore(); var id = Guid.NewGuid(); var at = s.Clock.Now;
        await using (var connection = await s.Database.Open(default))
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE intervals(id INTEGER PRIMARY KEY AUTOINCREMENT,target_id TEXT,state TEXT,start INTEGER,end INTEGER,
                    reason TEXT,observed INTEGER,telegram_at INTEGER,start_known INTEGER,end_known INTEGER DEFAULT 0);
                INSERT INTO intervals(target_id,state,start,reason,observed,start_known) VALUES($target,'Online',$at,'Push',$at,0);
                """;
            command.Parameters.AddWithValue("$target", id.ToString());
            command.Parameters.AddWithValue("$at", at.ToUnixTimeMilliseconds());
            await command.ExecuteNonQueryAsync();
        }
        await s.Initialize();
        var row = Assert.Single(await s.Telemetry.IntervalsAsync(id, at, at.AddSeconds(10), default));
        Assert.Equal(PresenceState.Online, row.State);
        Assert.Equal(at, row.Start);
        Assert.Null(row.ExpiresAt);
        await using var upgraded = await s.Database.Open(default);
        using var version = upgraded.CreateCommand(); version.CommandText = "SELECT MAX(version) FROM schema_version";
        Assert.Equal(2L, await version.ExecuteScalarAsync());
    }
}
