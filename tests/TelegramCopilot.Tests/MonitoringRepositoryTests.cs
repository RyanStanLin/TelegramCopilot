using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;
using TelegramCopilot.Infrastructure.Persistence;

namespace TelegramCopilot.Tests;

public sealed class MonitoringRepositoryTests
{
    [Fact]
    public async Task PollCounters_PersistAcrossReinitializationAndReadsDoNotIncrementThem()
    {
        await using var s = new TestStore(); await s.Initialize(); var account = await s.Account(); var configured = await s.Target(account.Id);
        var target = (await s.Configs.FindAsync(configured.Id, default))!; var at = s.Clock.Now;
        var repository = new MonitoringRepository(s.Database);
        await repository.StartPollAsync(target, at, at.AddSeconds(30), default);
        Assert.True((await repository.GetAsync(target, at, default)).IsPolling);
        await repository.CompletePollAsync(target, new(target.Id, PresenceState.Unknown, at.AddSeconds(1), "userStatusRecently"), null, default);
        for (var i = 0; i < 3; i++) Assert.Equal(1, (await repository.GetAsync(target, at, default)).PollCount);
        await s.Initialize(); repository = new(s.Database);
        var result = await repository.GetAsync(target, at.AddSeconds(2), default);
        Assert.Equal(1, result.PollCount); Assert.Equal(1, result.SuccessfulPollCount); Assert.Equal(0, result.FailedPollCount);
        Assert.Equal(at.AddSeconds(1), result.LastSuccessfulPollAt); Assert.False(result.IsPolling);
        Assert.Equal((1L, (DateTimeOffset?)at.AddSeconds(1)), await repository.TotalsAsync(default));
    }

    [Fact]
    public async Task FailedAndInterruptedChecks_KeepTheirCountersAndRecoverFromPendingState()
    {
        await using var s = new TestStore(); await s.Initialize(); var account = await s.Account(); var configured = await s.Target(account.Id);
        var target = (await s.Configs.FindAsync(configured.Id, default))!; var at = s.Clock.Now; var repository = new MonitoringRepository(s.Database);
        await repository.StartPollAsync(target, at, at.AddSeconds(30), default);
        await repository.CompletePollAsync(target, new(target.Id, PresenceState.Unknown, at.AddSeconds(1), "AccountNotReady"), null, default);
        var first = await repository.GetAsync(target, at, default);
        Assert.Equal(1, first.FailedPollCount); Assert.Equal("AccountNotReady", first.Error); Assert.Null(first.LastSuccessfulPollAt);
        await repository.StartPollAsync(target, at.AddSeconds(30), at.AddSeconds(60), default);
        await repository.RecoverAsync(at.AddSeconds(40), default);
        var interrupted = await repository.GetAsync(target, at, default);
        Assert.Equal(2, interrupted.PollCount); Assert.Equal(2, interrupted.FailedPollCount);
        Assert.Equal("WorkerInterrupted", interrupted.Error); Assert.False(interrupted.IsPolling);
        await repository.RecoverAsync(at.AddSeconds(50), default);
        Assert.Equal(2, (await repository.GetAsync(target, at, default)).FailedPollCount);
    }

    [Fact]
    public async Task TelegramProfile_PreservesNativeLastSeenAndClearsExactTimeWhenPrivacyBecomesCoarse()
    {
        await using var s = new TestStore(); await s.Initialize(); var account = await s.Account(); var configured = await s.Target(account.Id);
        var target = (await s.Configs.FindAsync(configured.Id, default))!; var at = s.Clock.Now; var repository = new MonitoringRepository(s.Database);
        var seen = at.AddHours(-2); var profile = new TelegramProfile(42, "Alice", "Lee", "alice", 7, "photo-1");
        await repository.ObserveAsync(target, new(target.Id, PresenceState.Offline, at, "CachedReconciliation", seen,
            Profile: profile, TelegramStatus: "userStatusOffline"), default);
        var first = (await repository.GetAsync(target, at, default)).Profile!;
        Assert.Equal("Alice Lee", first.DisplayName); Assert.Equal("alice", first.Username); Assert.True(first.HasAvatar);
        Assert.Equal(seen, first.LastSeenAt); Assert.Equal(at, first.UpdatedAt);
        await repository.ObserveAsync(target, new(target.Id, PresenceState.Unknown, at.AddSeconds(1), "MonitoringDisabled"), default);
        Assert.Equal(at, (await repository.GetAsync(target, at, default)).Profile!.UpdatedAt);
        await repository.ObserveAsync(target, new(target.Id, PresenceState.Unknown, at.AddSeconds(2), "userStatusRecently", TelegramStatus: "userStatusRecently"), default);
        var coarse = (await repository.GetAsync(target, at, default)).Profile!;
        Assert.Null(coarse.LastSeenAt); Assert.Equal("userStatusRecently", coarse.TelegramStatus); Assert.Equal("Alice Lee", coarse.DisplayName);
        await repository.ObserveAsync(target, new(target.Id, PresenceState.Offline, at.AddSeconds(-1), "Delayed", seen,
            TelegramStatus: "userStatusOffline"), default);
        Assert.Null((await repository.GetAsync(target, at, default)).Profile!.LastSeenAt);
    }

    [Fact]
    public async Task ReconfiguredTarget_DoesNotExposePreviousUsersProfileOrAcceptOldPollCompletion()
    {
        await using var s = new TestStore(); await s.Initialize(); var account = await s.Account(); var configured = await s.Target(account.Id);
        var old = (await s.Configs.FindAsync(configured.Id, default))!; var at = s.Clock.Now; var repository = new MonitoringRepository(s.Database);
        await repository.StartPollAsync(old, at, at.AddSeconds(30), default);
        await repository.ObserveAsync(old, new(old.Id, PresenceState.Offline, at, "Cache", at.AddHours(-1), Profile: new(42, "Alice", "", "alice"), TelegramStatus: "userStatusOffline"), default);
        await s.Configuration.SaveAsync(ResourceKind.Targets, old.Id, TestStore.Request("Bob", new TargetSettings(account.Id, "@bob"), 1), default);
        var changed = (await s.Configs.FindAsync(old.Id, default))!;
        Assert.Null((await repository.GetAsync(changed, at, default)).Profile);
        await repository.CompletePollAsync(old, new(old.Id, PresenceState.Offline, at.AddSeconds(1), "CachedReconciliation"), null, default);
        Assert.Equal(0, (await repository.GetAsync(changed, at, default)).SuccessfulPollCount);
        await repository.StartPollAsync(changed, at.AddSeconds(2), at.AddSeconds(32), default);
        Assert.Null((await repository.GetAsync(changed, at, default)).Profile);
        Assert.Equal(2, (await repository.GetAsync(changed, at, default)).PollCount);
    }
}
