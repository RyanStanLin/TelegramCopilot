namespace TelegramCopilot.Core.Scheduling;

public enum Recurrence { Daily, Weekly, Monthly, Yearly }
public sealed record ScheduleSettings(Guid AccountId, string Recipient, string Message,
    Recurrence Frequency, string TimeZone, string LocalTime, int DayOfWeek = 1, int DayOfMonth = 1, int Month = 1)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Frequency) || !TimeOnly.TryParseExact(LocalTime, "HH:mm:ss", out _)
            || DayOfWeek is < 0 or > 6 || DayOfMonth is < 1 or > 31 || Month is < 1 or > 12)
            throw new ArgumentException("Use HH:mm:ss, weekday 0 (Sunday) to 6, day 1 to 31 and month 1 to 12.");
        TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        if (Frequency == Recurrence.Yearly && DayOfMonth > DateTime.DaysInMonth(2000, Month))
            throw new ArgumentException("The yearly calendar date cannot occur.");
    }

    public DateTimeOffset NextAfter(DateTimeOffset after)
    {
        Validate();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(after, zone).DateTime);
        var time = TimeOnly.ParseExact(LocalTime, "HH:mm:ss");
        for (var i = 0; i < 366 * 9; i++, date = date.AddDays(1))
        {
            if (Frequency == Recurrence.Weekly && (int)date.DayOfWeek != DayOfWeek) continue;
            if (Frequency is Recurrence.Monthly or Recurrence.Yearly && date.Day != DayOfMonth) continue;
            if (Frequency == Recurrence.Yearly && date.Month != Month) continue;
            var local = date.ToDateTime(time, DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(local)) continue;
            var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
            var candidate = new DateTimeOffset(local, offset).ToUniversalTime();
            if (candidate > after) return candidate;
        }
        throw new InvalidOperationException("No occurrence found in the next nine years.");
    }
}
