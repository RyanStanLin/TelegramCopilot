using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TelegramCopilot.Infrastructure.Telegram;

namespace TelegramCopilot.Api.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            KeyNotFoundException => (404, "Resource not found"),
            ArgumentException or JsonException or TimeZoneNotFoundException or InvalidTimeZoneException => (400, "Invalid request"),
            InvalidOperationException => (409, "Operation cannot be completed"),
            TelegramException te when te.Code == 429 => (429, "Telegram rate limit"),
            TelegramException => (502, "Telegram request failed"),
            TimeoutException => (504, "Telegram request timed out"),
            _ => (500, "Service error")
        };
        logger.LogWarning("API request {Path} failed with {Type}", context.Request.Path, exception.GetType().Name);
        context.Response.StatusCode = status;
        var detail = exception is ArgumentException or InvalidOperationException or KeyNotFoundException ? exception.Message : title;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title, Detail = detail,
            Instance = context.Request.Path }, cancellationToken: ct);
        return true;
    }
}
