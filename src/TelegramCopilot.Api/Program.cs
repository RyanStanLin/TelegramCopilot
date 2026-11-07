using System.Text.Json.Serialization;
using TelegramCopilot.Api.Endpoints;
using TelegramCopilot.Api.Middleware;
using TelegramCopilot.Infrastructure;
using TelegramCopilot.Infrastructure.Persistence;
using TelegramCopilot.Application.Ports;

var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrWhiteSpace(builder.Configuration["Urls"]))
    builder.WebHost.UseUrls("http://127.0.0.1:5080");
var dataPath = builder.Configuration["DATA_PATH"] ?? builder.Configuration["DataPath"] ?? Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataPath);
using var workerLock = new FileStream(Path.Combine(dataPath, "worker.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
builder.Services.AddTelegramCopilot(dataPath);
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));
var app = builder.Build();
await app.Services.GetRequiredService<Database>().Initialize(CancellationToken.None);
await app.Services.GetRequiredService<ITelemetryRepository>().RecoverAsync(DateTimeOffset.UtcNow, CancellationToken.None);
await app.Services.GetRequiredService<IDeliveryRepository>().RecoverAsync(DateTimeOffset.UtcNow, CancellationToken.None);
await app.Services.GetRequiredService<IMonitoringRepository>().RecoverAsync(DateTimeOffset.UtcNow, CancellationToken.None);
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapOpenApi();
app.MapConfigurations();
app.MapTelemetry();
app.MapOperations();
app.MapMonitoring();
app.MapManualTests();
app.Run();

public partial class Program;
