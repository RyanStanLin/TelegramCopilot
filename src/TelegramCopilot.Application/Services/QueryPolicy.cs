namespace TelegramCopilot.Application.Services;

public static class QueryPolicy
{
    public static void Page(int page, int pageSize)
    {
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 500)
            throw new ArgumentException("page must be 1 to 1000000 and pageSize must be 1 to 500.");
    }
    public static void Range(DateTimeOffset from, DateTimeOffset to)
    {
        if (from >= to || (to - from).TotalDays > 366 * 10)
            throw new ArgumentException("Use from < to and a range of at most 3660 days.");
    }
}
