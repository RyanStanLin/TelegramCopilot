using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;
using TelegramCopilot.Infrastructure.Workers;

namespace TelegramCopilot.Tests;

public sealed class MonitorWorkerTests
{
    [Fact]
    public async Task QueuedPushes_ArePreservedBeforeCacheReconciliationAndDisable()
    {
        await using var store = new TestStore();
        await store.Initialize();
        var account = await store.Account();
        var target = await store.Target(account.Id);
        var at = store.Clock.Now;
        var input = new PausedObservations();
        var telegram = new PulseGateway(input, at);
        using var worker = new MonitorWorker(store.Configs, store.Telemetry, telegram, input,
            store.Clock, NullLogger<MonitorWorker>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await worker.StartAsync(timeout.Token);
        try
        {
            await input.CacheQueued.Task.WaitAsync(timeout.Token);
            input.Resume.TrySetResult();
            await Until(async () => (await store.Telemetry.IntervalsAsync(target.Id, at, at.AddSeconds(50), default))
                .LastOrDefault()?.ObservedAt == at.AddSeconds(30), timeout.Token);
            store.Clock.Now = at.AddSeconds(40);
            await store.Configuration.SaveAsync(ResourceKind.Targets, target.Id,
                new() { Name = target.Name, Enabled = false, Settings = target.Settings, Revision = target.Revision }, default);
            await Until(async () => (await store.Telemetry.IntervalsAsync(target.Id, at, at.AddSeconds(50), default))
                .LastOrDefault()?.Reason == "MonitoringDisabled", timeout.Token);
        }
        finally { await worker.StopAsync(CancellationToken.None); }
        var rows = await store.Telemetry.IntervalsAsync(target.Id, at, at.AddSeconds(50), default);
        var online = Assert.Single(rows, value => value.State == PresenceState.Online);
        Assert.Equal(at.AddSeconds(10), online.Start);
        Assert.Equal(at.AddSeconds(20), online.End);
        Assert.True(online.StartKnown && online.EndKnown);
        Assert.Equal(10, online.Seconds(at.AddSeconds(50)));
        var events = await store.Telemetry.EventsAsync(target.Id, "Offline", at, at.AddSeconds(50), 1, 100, default);
        Assert.Equal(10, Assert.Single(events.Items, value => value.OnlineSeconds is not null).OnlineSeconds);
    }

    private static async Task Until(Func<Task<bool>> condition, CancellationToken ct)
    {
        while (!await condition()) await Task.Delay(10, ct);
    }

    private sealed class PausedObservations : IObservationStream
    {
        private readonly ObservationStream _stream = new();
        public TaskCompletionSource CacheQueued { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Publish(Observation observation)
        {
            _stream.Publish(observation);
            if (observation.Reason == "CachedReconciliation") CacheQueued.TrySetResult();
        }
        public async IAsyncEnumerable<Observation> ReadAllAsync([EnumeratorCancellation] CancellationToken ct)
        {
            await Resume.Task.WaitAsync(ct);
            await foreach (var observation in _stream.ReadAllAsync(ct)) yield return observation;
        }
    }

    private sealed class PulseGateway(IObservationStream stream, DateTimeOffset at) : ITelegramGateway
    {
        private int _started;
        public bool NativeAvailable => true;
        public string? NativeError => null;
        public Task ReconcileAccountsAsync(IReadOnlyList<Resource> accounts, CancellationToken ct) => Task.CompletedTask;
        public Task<AccountStatusResponse> StatusAsync(Guid id, CancellationToken ct) => Task.FromResult(
            new AccountStatusResponse(id, "authorizationStateReady", "connectionStateReady", null));
        public Task<AccountStatusResponse> LoginAsync(Guid id, LoginRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<SendReceipt> SendAsync(Guid id, string recipient, string message, CancellationToken ct) => throw new NotSupportedException();
        public Task<Observation> ObserveAsync(Resource target, CancellationToken ct)
        {
            if (Interlocked.Exchange(ref _started, 1) == 0)
            {
                stream.Publish(new(target.Id, PresenceState.Offline, at, "TelegramUpdate"));
                stream.Publish(new(target.Id, PresenceState.Online, at.AddSeconds(10), "TelegramUpdate"));
                stream.Publish(new(target.Id, PresenceState.Offline, at.AddSeconds(20), "TelegramUpdate"));
            }
            return Task.FromResult(new Observation(target.Id, PresenceState.Offline, at.AddSeconds(30), "CachedReconciliation"));
        }
    }
}
