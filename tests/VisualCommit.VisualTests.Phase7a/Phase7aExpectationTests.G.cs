// The name ends in ".G.cs", which the compiler takes for generated code: nullable annotations are on explicitly.
#nullable enable

using VisualCommit.Testing;
using Xunit;
using G = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartG;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// Part G's expectations (docs/test-reports/phase-7a.md, part G's conventions; design 11.5): the pin
/// of <see cref="G.PrepareAsync"/> on the scenario, whose graph order puts the prepared commit first
/// and part G's commit at <see cref="G.Row"/> with its author, and the picture names unique and
/// matching design 0.3's rule.
/// </summary>
public sealed class Phase7aExpectationTestsG
{
    [Fact]
    public async Task PrepareAsync_puts_the_prepared_commit_first_and_part_g_s_row_by_octo()
    {
        var copy = await Scenarios.HostingAsync();
        await using var served = await Scenarios.ServeHostingAsync(copy, inProcess: true, ct: TestContext.Current.CancellationToken, ownsCopy: true);
        await G.PrepareAsync(served.Hello);

        var log = await served.Hello.GitAsync("log", "--date-order", "--format=%H%x09%ae%x09%s", "--branches", "--remotes", "--tags", "HEAD");
        var rows = log.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r').Split('\t')).ToList();
        Assert.Equal(G.Rows.Select(row => (row.Email, row.Subject)), rows.Select(row => (row[1], row[2])));
        Assert.Equal(G.PreparedMessage, rows[0][2]);
        Assert.Equal((G.Author, "Update the readme"), (rows[G.Row][1], rows[G.Row][2]));

        // feature/login one ahead of its upstream: the labels on rows 0 and 5.
        var login = (await served.Hello.GitAsync("rev-parse", "refs/heads/feature/login")).StandardOutput.Trim();
        var upstream = (await served.Hello.GitAsync("rev-parse", "refs/remotes/origin/feature/login")).StandardOutput.Trim();
        Assert.Equal((login, upstream), (rows[0][0], rows[5][0]));
        Assert.Equal("main", (await served.Hello.GitAsync("branch", "--show-current")).StandardOutput.Trim());
        Assert.Equal(G.PreparedContent, (await served.Hello.GitAsync("show", "refs/heads/feature/login:" + G.PreparedPath)).StandardOutput);
    }

    [Fact]
    public void The_picture_names_are_unique_and_match_the_rule()
    {
        Assert.Equal(G.Pictures.Count, G.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(G.Pictures, name => Assert.Matches(@"^[abpgn]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$", name));
        Assert.All(G.RealWindowPictures, name => Assert.Contains(name, G.Pictures));
        Assert.Equal(10, G.Pictures.Count);
        Assert.Equal(G.Rows.Count, G.SignedInLabels.Count);
        Assert.Equal(G.Rows.Count, G.TodayLabels.Count);
    }
}
