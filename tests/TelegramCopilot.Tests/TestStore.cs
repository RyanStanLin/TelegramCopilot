using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using TelegramCopilot.Application.Contracts;
using TelegramCopilot.Application.Services;
using TelegramCopilot.Core.Configuration;
using TelegramCopilot.Infrastructure.Persistence;

namespace TelegramCopilot.Tests;

internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now = DateTimeOffset.Parse("2026-11-07T00:00:00Z");
    public override DateTimeOffset GetUtcNow() => Now;
}
internal sealed class TestStore : IAsyncDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "tgc-tests-" + Guid.NewGuid().ToString("N"));
    public TestClock Clock { get; } = new();
    public Database Database { get; }
    public ConfigurationRepository Configs { get; }
    public TelemetryRepository Telemetry { get; }
    public DeliveryRepository Deliveries { get; }
    public ConfigurationService Configuration { get; }
    public TelemetryService Analytics { get; }
    public TestStore()
    {
        Database = new(_directory);
        Configs = new(Database, DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(_directory, "keys"))));
        Telemetry = new(Database); Deliveries = new(Database);
        Configuration = new(Configs, Clock); Analytics = new(Configs, Telemetry, Clock);
    }
    public Task Initialize() => Database.Initialize(default);
    public Task<ConfigurationResponse> Account() => Configuration.SaveAsync(ResourceKind.Accounts, null,
        Request("Account", new AccountSettings(12345, "0123456789abcdef0123456789abcdef", "+886912345678")), default);
    public Task<ConfigurationResponse> Target(Guid account) => Configuration.SaveAsync(ResourceKind.Targets, null,
        Request("Alice", new TargetSettings(account, "@alice")), default);
    public static SaveConfigurationRequest Request(string name, object settings, long? revision = null) =>
        new() { Name = name, Settings = JsonSerializer.SerializeToElement(settings, JsonDefaults.Options), Revision = revision };
    public ValueTask DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, true);
        return ValueTask.CompletedTask;
    }
}
