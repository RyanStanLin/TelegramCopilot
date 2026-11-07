using TelegramCopilot.Core.Scheduling;

namespace TelegramCopilot.Tests;

public sealed class ScheduleTests
{
    private static ScheduleSettings Plan(Recurrence frequency, string time = "07:59:00", string zone = "Asia/Taipei",
        int week = 1, int day = 1, int month = 1) => new(Guid.NewGuid(), "@alice", "hello", frequency, zone, time, week, day, month);
    [Fact]
    public void Daily_ExactBoundary_GoesToNextDay() => Assert.Equal(DateTimeOffset.Parse("2026-11-08T07:59:00+08:00"),
        Plan(Recurrence.Daily).NextAfter(DateTimeOffset.Parse("2026-11-07T07:59:00+08:00")));
    [Fact]
    public void Weekly_MondayAtSecondPrecision_UsesLocalCalendar() => Assert.Equal(DateTimeOffset.Parse("2026-11-09T07:59:03+08:00"),
        Plan(Recurrence.Weekly, "07:59:03").NextAfter(DateTimeOffset.Parse("2026-11-07T00:00:00Z")));
    [Fact]
    public void Monthly_Day31_SkipsShortMonth() => Assert.Equal(DateTimeOffset.Parse("2027-03-31T07:59:00+08:00"),
        Plan(Recurrence.Monthly, day: 31).NextAfter(DateTimeOffset.Parse("2027-01-31T23:00:00+08:00")));
    [Fact]
    public void Yearly_LeapDay_SkipsNonLeapYears() => Assert.Equal(DateTimeOffset.Parse("2028-02-29T00:00:00Z"),
        Plan(Recurrence.Yearly, "00:00:00", "UTC", day: 29, month: 2).NextAfter(DateTimeOffset.Parse("2025-03-01T00:00:00Z")));
    [Fact]
    public void Yearly_JanuaryFirst_PreservesMidnight() => Assert.Equal(DateTimeOffset.Parse("2027-01-01T00:00:00+08:00"),
        Plan(Recurrence.Yearly, "00:00:00").NextAfter(DateTimeOffset.Parse("2026-11-07T00:00:00Z")));
    [Fact]
    public void Daily_DstGap_SkipsNonexistentTime() => Assert.Equal(DateTimeOffset.Parse("2026-03-09T02:30:00-04:00"),
        Plan(Recurrence.Daily, "02:30:00", "America/New_York").NextAfter(DateTimeOffset.Parse("2026-03-08T00:00:00-05:00")));
    [Fact]
    public void Daily_DstFold_DoesNotRunTwice() => Assert.Equal(DateTimeOffset.Parse("2026-11-02T01:30:00-05:00"),
        Plan(Recurrence.Daily, "01:30:00", "America/New_York").NextAfter(DateTimeOffset.Parse("2026-11-01T01:45:00-04:00")));
    [Theory]
    [InlineData("7:59")]
    [InlineData("25:00:00")]
    public void InvalidClock_IsRejected(string value) => Assert.Throws<ArgumentException>(() => Plan(Recurrence.Daily, value).Validate());
    [Fact]
    public void ImpossibleYearlyDate_IsRejected() => Assert.Throws<ArgumentException>(() => Plan(Recurrence.Yearly, day: 31, month: 4).Validate());
}
