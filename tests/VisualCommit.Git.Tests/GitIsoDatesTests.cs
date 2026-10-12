using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitIsoDates"/>: the tests read git's strict ISO dates the same whichever way git's
/// version writes UTC ("+00:00" up to at least 2.36, "Z" in 2.55), keeping the instant and the zone.
/// </summary>
public class GitIsoDatesTests
{
    [Theory]
    [InlineData("2026-01-02T09:00:00+00:00")]
    [InlineData("2026-01-02T09:00:00Z")]
    public void Both_ways_of_writing_UTC_give_the_same_instant_and_zone(string text)
    {
        var date = GitIsoDates.Parse(text);

        Assert.Equal((GitIsolation.AppCommitDate, TimeSpan.Zero), (date, date.Offset));
    }

    [Fact]
    public void Another_zone_is_kept()
    {
        var date = GitIsoDates.Parse("2026-01-02T10:30:00+01:30");

        Assert.Equal((GitIsolation.AppCommitDate, TimeSpan.FromMinutes(90)), (date, date.Offset));
    }

    [Theory]
    [InlineData("2026-01-02 09:00:00 +0000")]
    [InlineData("2026-01-02T09:00:00")]
    [InlineData("1767344400 +0000")]
    public void Anything_but_the_strict_form_is_refused(string text) =>
        Assert.Throws<FormatException>(() => GitIsoDates.Parse(text));

    [Fact]
    public void Lines_are_read_one_date_each()
    {
        var dates = GitIsoDates.ParseLines("2026-01-01T12:02:00Z\r\n2026-01-02T09:00:00+00:00\n");

        Assert.Equal([new DateTimeOffset(2026, 1, 1, 12, 2, 0, TimeSpan.Zero), GitIsolation.AppCommitDate], dates);
    }
}
