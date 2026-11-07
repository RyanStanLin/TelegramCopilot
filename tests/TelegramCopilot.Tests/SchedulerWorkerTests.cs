using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Core.Presence;
using TelegramCopilot.Core.Scheduling;
using TelegramCopilot.Infrastructure.Workers;

namespace TelegramCopilot.Tests;

public sealed class SchedulerWorkerTests
{
    [Fact]
    public async Task IndependentRecipients_PreserveMessagesAndCalendarOccurrenceTimes()
    {
        await using var s = new TestStore(); await s.Initialize(); var account = await s.Account(); var at = s.Clock.Now;
        var first = await s.Configuration.SaveAsync(ResourceKind.Schedules, null,
            TestStore.Request("First", new ScheduleSettings(account.Id, "@first_bot", "first body", Recurrence.Daily, "UTC", "00:00:01")), default);
        var second = await s.Configuration.SaveAsync(ResourceKind.Schedules, null,
            TestStore.Request("Second", new ScheduleSettings(account.Id, "@second_bot", "second body", Recurrence.Daily, "UTC", "00:00:02")), default);
        var repository = new ObservedConfigurations(s.Configs);
        var gateway = new MessageGateway();
        using var worker = new SchedulerWorker(repository, s.Deliveries, gateway, s.Telemetry, s.Clock, NullLogger<SchedulerWorker>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await worker.StartAsync(timeout.Token);
        try
        {
            await repository.SchedulesRead.Task.WaitAsync(timeout.Token);
            s.Clock.Now = at.AddSeconds(3);
            await Until(async () => (await s.Deliveries.ListAsync("Telegram", null, 1, 100, default)).Items.Count(value => value.Status == "Sent") == 2, timeout.Token);
        }
        finally { await worker.StopAsync(CancellationToken.None); }
        Assert.Equal(new[] { ("@first_bot", "first body"), ("@second_bot", "second body") }, gateway.Messages.OrderBy(value => value.Recipient).ToArray());
        var rows = (await s.Deliveries.ListAsync("Telegram", null, 1, 100, default)).Items;
        Assert.Equal(at.AddSeconds(1), Assert.Single(rows, value => value.ResourceId == first.Id).DueAt);
        Assert.Equal(at.AddSeconds(2), Assert.Single(rows, value => value.ResourceId == second.Id).DueAt);
        Assert.All(rows, value => Assert.StartsWith("receipt-", value.ExternalId));
        Assert.Equal(2, (await s.Telemetry.EventsAsync(null, "MessageSent", at, at.AddSeconds(10), 1, 100, default)).Total);
    }

    [Fact]
    public async Task LateOccurrence_IsRecordedAsSkippedWithoutSending()
    {
        await using var s = new TestStore(); await s.Initialize(); var account = await s.Account(); var at = s.Clock.Now;
        var plan = await s.Configuration.SaveAsync(ResourceKind.Schedules, null,
            TestStore.Request("Late", new ScheduleSettings(account.Id, "@test_bot", "body", Recurrence.Daily, "UTC", "00:00:05")), default);
        var repository = new ObservedConfigurations(s.Configs); var gateway = new MessageGateway();
        using var worker = new SchedulerWorker(repository, s.Deliveries, gateway, s.Telemetry, s.Clock, NullLogger<SchedulerWorker>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await worker.StartAsync(timeout.Token);
        try
        {
            await repository.SchedulesRead.Task.WaitAsync(timeout.Token);
            s.Clock.Now = at.AddSeconds(70);
            await Until(async () => (await s.Deliveries.ListAsync("Telegram", plan.Id, 1, 100, default)).Items.Any(value => value.Status == "Skipped"), timeout.Token);
        }
        finally { await worker.StopAsync(CancellationToken.None); }
        Assert.Empty(gateway.Messages);
        var skipped = Assert.Single((await s.Deliveries.ListAsync("Telegram", plan.Id, 1, 100, default)).Items);
        Assert.Equal(at.AddSeconds(5), skipped.DueAt);
        Assert.Equal("Outside scheduler grace period", skipped.Error);
    }

    private static async Task Until(Func<Task<bool>> condition, CancellationToken ct)
    {
        while (!await condition()) await Task.Delay(10, ct);
    }

    private sealed class ObservedConfigurations(IConfigurationRepository inner) : IConfigurationRepository
    {
        public TaskCompletionSource SchedulesRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IReadOnlyList<Resource>> ListAsync(ResourceKind? kind, CancellationToken ct)
        {
            var rows = await inner.ListAsync(kind, ct);
            if (kind == ResourceKind.Schedules) SchedulesRead.TrySetResult();
            return rows;
        }
        public Task<Resource?> FindAsync(Guid id, CancellationToken ct, bool includeDeleted = false) => inner.FindAsync(id, ct, includeDeleted);
        public Task SaveAsync(Resource value, long? revision, CancellationToken ct) => inner.SaveAsync(value, revision, ct);
        public Task DeleteAsync(Guid id, long revision, DateTimeOffset at, CancellationToken ct) => inner.DeleteAsync(id, revision, at, ct);
    }

    private sealed class MessageGateway : ITelegramGateway
    {
        public ConcurrentQueue<(string Recipient, string Message)> Messages { get; } = new();
        public bool NativeAvailable => true;
        public string? NativeError => null;
        public Task ReconcileAccountsAsync(IReadOnlyList<Resource> accounts, CancellationToken ct) => throw new NotSupportedException();
        public Task<AccountStatusResponse> StatusAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<AccountStatusResponse> LoginAsync(Guid id, LoginRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<Observation> ObserveAsync(Resource target, CancellationToken ct) => throw new NotSupportedException();
        public Task<SendReceipt> SendAsync(Guid id, string recipient, string message, CancellationToken ct)
        {
            Messages.Enqueue((recipient, message));
            return Task.FromResult(new SendReceipt("receipt-" + recipient, "Sent"));
        }
    }
}
