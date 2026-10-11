using VisualCommit.App.Controls.Diff;
using VisualCommit.Core.Diff;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The word-level highlights of whole diffs (<see cref="DiffWordHighlights"/>): the pairing of a
/// change's removed and added lines, against the examples of phase 2's test report.
/// </summary>
public class DiffWordHighlightsTests
{
    [Fact]
    public async Task The_scenario_lines_have_the_highlights_the_report_lists()
    {
        var diffs = await DiffScenario.DiffsAsync();

        // README.md, unstaged: "tests" against "the visual checks".
        var readme = Compute(diffs.ReadmeUnstaged);
        Assert.Equal([new TextRange(23, 5)], readme[new DiffLineRef(0, 2)]);
        Assert.Equal([new TextRange(23, 17)], readme[new DiffLineRef(0, 3)]);
        Assert.Equal(2, readme.Count);

        // src/Calculator.cs, line 15: nothing on the removed line, " + _offset" on the added one.
        var calculator = Compute(diffs.Calculator);
        Assert.False(calculator.ContainsKey(new DiffLineRef(0, 3)));
        Assert.Equal([new TextRange(20, 10)], calculator[new DiffLineRef(0, 4)]);

        // The added lines 32 to 35 have no removed line to pair with.
        Assert.Single(calculator);

        // src/helpers.py, staged, line 1: Small, helpers and for against Helpers, shared and by.
        var helpers = Compute(diffs.Helpers);
        Assert.Equal([new TextRange(3, 5), new TextRange(9, 7), new TextRange(17, 3)], helpers[new DiffLineRef(0, 0)]);
        Assert.Equal([new TextRange(3, 7), new TextRange(11, 6), new TextRange(18, 2)], helpers[new DiffLineRef(0, 1)]);
    }

    [Fact]
    public void The_first_removed_line_is_paired_with_the_first_added_line_and_so_on()
    {
        var diff = DiffScenario.Parse(
            "@@ -1,3 +1,4 @@",
            "-alpha one",
            "-beta one",
            "+alpha two",
            "+beta two",
            "+gamma two",
            " same");

        var highlights = Compute(diff);

        Assert.Equal([new TextRange(6, 3)], highlights[new DiffLineRef(0, 0)]);
        Assert.Equal([new TextRange(5, 3)], highlights[new DiffLineRef(0, 1)]);
        Assert.Equal([new TextRange(6, 3)], highlights[new DiffLineRef(0, 2)]);
        Assert.Equal([new TextRange(5, 3)], highlights[new DiffLineRef(0, 3)]);
        Assert.False(highlights.ContainsKey(new DiffLineRef(0, 4)));
    }

    [Fact]
    public void Past_the_budget_the_remaining_pairs_have_no_highlights()
    {
        // Each pair of lines has 3 words against 3: 9 pairs of words.
        var diff = DiffScenario.Parse(
            "@@ -1,3 +1,3 @@",
            "-a one",
            "-b one",
            "-c one",
            "+a two",
            "+b two",
            "+c two");

        var highlights = DiffWordHighlights.Compute(diff, budget: 18, TestContext.Current.CancellationToken);

        Assert.Equal(
            [new DiffLineRef(0, 0), new DiffLineRef(0, 1), new DiffLineRef(0, 3), new DiffLineRef(0, 4)],
            highlights.Keys.OrderBy(line => line.Hunk).ThenBy(line => line.Line));
        Assert.Equal(6, Compute(diff).Count);
    }

    [Fact]
    public void A_pair_too_long_to_compare_costs_nothing_from_the_budget()
    {
        // The first pair has 1,000 words a side, far over WordDiff's limit: skipped, not charged.
        var longLine = string.Join(' ', Enumerable.Repeat("x", 500));
        var diff = DiffScenario.Parse(
            "@@ -1,2 +1,2 @@",
            "-" + longLine,
            "-a one",
            "+" + longLine + " y",
            "+a two");

        var highlights = DiffWordHighlights.Compute(diff, budget: 9, TestContext.Current.CancellationToken);

        Assert.Equal([new DiffLineRef(0, 1), new DiffLineRef(0, 3)], highlights.Keys.OrderBy(line => line.Hunk).ThenBy(line => line.Line));
    }

    private static IReadOnlyDictionary<DiffLineRef, IReadOnlyList<TextRange>> Compute(FileDiff diff) =>
        DiffWordHighlights.Compute(diff, DiffWordHighlights.SynchronousBudget, TestContext.Current.CancellationToken);
}
