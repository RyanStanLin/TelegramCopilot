using System.Text.Json.Serialization;
using TelegramCopilot.Api.Endpoints;
using TelegramCopilot.Api.Middleware;
using TelegramCopilot.Infrastructure;
using TelegramCopilot.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var dataPath = builder.Configuration["DataPath"] ?? Path.Combine(AppContext.BaseDirectory, "data");
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
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapOpenApi();
app.MapConfigurations();
app.MapTelemetry();
app.MapOperations();
app.Run();

public partial class Program;
