using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Tests;

public sealed class DurationTextTests
{
    [Theory]
    [InlineData(0, "0s")]
    [InlineData(1, "1s")]
    [InlineData(0.5, "0.5s")]
    [InlineData(60, "1分钟")]
    [InlineData(65, "1分钟5秒")]
    [InlineData(3600, "1小时")]
    [InlineData(9121, "2小时32分1秒")]
    [InlineData(86460, "1天1分")]
    [InlineData(-1, "unknown")]
    public void Durations_SelectReadableUnits(double seconds, string expected) => Assert.Equal(expected, DurationText.Format(seconds));
}
