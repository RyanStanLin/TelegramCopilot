using TelegramCopilot.Core.Presence;

namespace TelegramCopilot.Tests;

public sealed class TimelineTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-07T00:00:00Z");
    [Fact]
    public void EmptyHistory_IsEntirelyUnknown()
    {
        var result = PresenceTimeline.Fill([], Guid.Empty, Start, Start.AddHours(1));
        var gap = Assert.Single(result);
        Assert.Equal(PresenceState.Unknown, gap.State);
        Assert.Equal(3600, gap.Seconds(Start.AddHours(1)));
        Assert.False(gap.StartKnown); Assert.False(gap.EndKnown);
    }
    [Fact]
    public void MissingObservation_BecomesExplicitGap()
    {
        var id = Guid.NewGuid();
        var row = new PresenceInterval(1, id, PresenceState.Online, Start.AddSeconds(10), Start.AddSeconds(30), "Push", Start.AddSeconds(10), null, true, true);
        var result = PresenceTimeline.Fill([row], id, Start, Start.AddSeconds(60));
        Assert.Equal(new[] { PresenceState.Unknown, PresenceState.Online, PresenceState.Unknown }, result.Select(x => x.State));
        Assert.Equal(new double[] { 10, 20, 30 }, result.Select(x => x.Seconds(Start.AddSeconds(60))));
    }
    [Fact]
    public void Clipping_DoesNotPretendSessionBoundaryIsKnown()
    {
        var row = new PresenceInterval(1, Guid.Empty, PresenceState.Online, Start, Start.AddSeconds(100), "Push", Start, null, true, true);
        var result = Assert.Single(PresenceTimeline.Fill([row], Guid.Empty, Start.AddSeconds(10), Start.AddSeconds(90)));
        Assert.Equal(80, result.Seconds(Start.AddSeconds(90)));
        Assert.False(result.StartKnown); Assert.False(result.EndKnown);
    }
    [Fact]
    public void TemplateValues_AreNotRecursivelyInterpreted()
    {
        var e = new MonitorEvent(1, null, null, "{EVENT}", Core.Configuration.EventType.Offline, Start, 62, "ok");
        Assert.Equal("{EVENT}: 00:01:02 / 62", TemplateRenderer.Render("{NAME}: {TIME_OF_ONLINE} / {ONLINE_SECONDS}", e));
    }
    [Theory]
    [InlineData("=SUM(A1)", "\"'=SUM(A1)\"")]
    [InlineData("+cmd", "\"'+cmd\"")]
    [InlineData("a,\"b", "\"a,\"\"b\"")]
    public void Csv_QuotesAndNeutralizesFormulaPrefixes(string input, string expected) =>
        Assert.Equal(expected, Application.Services.TelemetryService.CsvCell(input));
}
