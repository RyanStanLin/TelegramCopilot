using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using TelegramCopilot.Application.Ports;
using TelegramCopilot.Application.Services;
using TelegramCopilot.Infrastructure.Notifications;
using TelegramCopilot.Infrastructure.Persistence;
using TelegramCopilot.Infrastructure.Telegram;
using TelegramCopilot.Infrastructure.Workers;

namespace TelegramCopilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTelegramCopilot(this IServiceCollection services, string dataPath)
    {
        var db = new Database(dataPath);
        services.AddSingleton(db);
        services.AddDataProtection().SetApplicationName("TelegramCopilot")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(db.DirectoryPath, "keys")));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IConfigurationRepository, ConfigurationRepository>();
        services.AddSingleton<ITelemetryRepository, TelemetryRepository>();
        services.AddSingleton<IMonitoringRepository, MonitoringRepository>();
        services.AddSingleton<IDeliveryRepository, DeliveryRepository>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddSingleton<IOperationsService, OperationsService>();
        services.AddSingleton<IMonitoringService, MonitoringService>();
        services.AddSingleton<IRuleValidationService, RuleValidationService>();
        services.AddSingleton<IManualTestService, ManualTestService>();
        services.AddSingleton<IObservationStream, ObservationStream>();
        services.AddSingleton<TdTransport>();
        services.AddSingleton<ITdTransport>(p => p.GetRequiredService<TdTransport>());
        services.AddHostedService(p => p.GetRequiredService<TdTransport>());
        services.AddSingleton<ITelegramGateway, TelegramGateway>();
        services.AddSingleton<IBarkGateway, BarkGateway>();
        services.AddHostedService<MonitorWorker>();
        services.AddHostedService<SchedulerWorker>();
        services.AddHostedService<NotificationWorker>();
        return services;
    }
}
