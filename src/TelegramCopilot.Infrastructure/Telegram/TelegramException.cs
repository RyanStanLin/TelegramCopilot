using System.Text.RegularExpressions;

namespace TelegramCopilot.Infrastructure.Telegram;

public sealed class TelegramException(int code, string? message = null)
    : Exception($"Telegram request failed: {SafeError(code, message)} (code {code}).")
{
    public int Code { get; } = code;
    public string ErrorCode { get; } = SafeError(code, message);
    public int? RetryAfterSeconds { get; } = RetryAfter(code, message);

    internal static string SafeError(int code, string? message)
    {
        // TDLib explicitly prohibits processing or displaying messages for error 406.
        if (code == 406) return "TELEGRAM_ERROR_406";
        if (code == 429) return "RATE_LIMITED";
        return message switch
        {
            "PHONE_CODE_INVALID" or "PHONE_CODE_EMPTY" or "PHONE_CODE_EXPIRED" or "PASSWORD_HASH_INVALID"
                or "PHONE_NUMBER_INVALID" or "PHONE_NUMBER_BANNED" or "PHONE_NUMBER_FLOOD"
                or "API_ID_INVALID" or "API_ID_PUBLISHED_FLOOD" or "UPDATE_APP_TO_LOGIN"
                or "EMAIL_INVALID" or "EMAIL_NOT_ALLOWED" or "EMAIL_CODE_INVALID" or "EMAIL_CODE_EXPIRED"
                or "LOGIN_STATE_CHANGED" or "AUTH_RESTART" => message,
            _ when message?.StartsWith("Call to ", StringComparison.Ordinal) == true
                && message.EndsWith(" unexpected", StringComparison.Ordinal) => "LOGIN_STATE_CHANGED",
            _ => $"TELEGRAM_ERROR_{code}"
        };
    }

    internal static int? RetryAfter(int code, string? message)
    {
        if (code != 429) return null;
        var match = Regex.Match(message ?? "", @"(?:retry after |FLOOD_WAIT_)(\d+)", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var seconds) ? Math.Max(1, seconds) : 60;
    }
}
