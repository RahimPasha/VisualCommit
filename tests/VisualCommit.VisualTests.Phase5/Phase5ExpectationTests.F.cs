// Tests of Phase5Expectations.F.cs's data against itself and the report's rules (the real-window
// pass leaves this file out: its name does not match Phase5Expectations*.cs).
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

public sealed class PartFExpectationTests
{
    [Fact]
    public void The_menus_heights_follow_4a_from_their_items_and_separators()
    {
        foreach (var (menu, height) in new[]
        {
            (PartF.ModeMenu, PartF.ModeMenuHeight),
            (PartF.CompareFileMenu, PartF.CompareFileMenuHeight),
            (PartF.FeatureCompareMenu, PartF.BranchMenuHeight),
            (PartF.DropMenu, PartF.DropMenuHeight),
            (PartF.MainMenu, PartF.MainMenuHeight),
            (PartF.FeatureMenu("alpha", hidden: false), PartF.BranchMenuHeight),
            (PartF.FeatureMenu("alpha", hidden: true), PartF.BranchMenuHeight),
            (PartF.FeatureMenu("beta", hidden: false), PartF.BranchMenuHeight),
            (PartF.SectionMenu(soloed: true), PartF.SectionMenuHeight),
            (PartF.SectionMenu(soloed: false), PartF.SectionMenuHeight),
        })
        {
            var separators = menu.Count(line => line == "—");
            Assert.Equal(height, MenuHeight(menu.Length - 1 - separators, separators));
        }
    }

    [Fact]
    public void The_graphs_are_the_reports_tables()
    {
        Assert.Equal(14, PartF.H0.Count);
        Assert.Equal(15, PartF.HF1.Count);
        Assert.Equal(PartF.H0.Skip(1), PartF.HF1.Skip(2));
        Assert.Equal(["origin/main"], PartF.HF1[1].Labels);
        Assert.Equal(15, PartF.FL0.Count);
        Assert.Equal(12, PartF.FL1.Count);
        Assert.Equal(12, PartF.FL2.Count);
        Assert.Equal(PartF.FL1.Select(row => (row.Message, row.Lane, row.Colour)), PartF.FL2.Select(row => (row.Message, row.Lane, row.Colour)));
        Assert.Equal([Current("main")], PartF.FL2[4].Labels);
        Assert.Empty(PartF.FL2[7].Labels);
        Assert.Equal(13, PartF.GS0.Count);
        Assert.Equal(["Alpha one", "Alpha two", "On feature/alpha: Alpha experiment"], PartF.FL0.Select(row => row.Message).Except(PartF.FL1.Select(row => row.Message)).Order(StringComparer.Ordinal));

        // H0's rows by name.
        Assert.Equal("Compare whole lines", PartF.H0[PartF.CompareWholeLinesRow].Message);
        Assert.Equal("Rename the parser to reader", PartF.H0[PartF.RenameParserRow].Message);
        Assert.Equal("Handle empty input", PartF.H0[PartF.HandleEmptyInputRow].Message);
        Assert.Equal("Merge branch 'feature/readme'", PartF.H0[PartF.MergeReadmeRow].Message);
        Assert.Equal("Add the comparer", PartF.H0[PartF.AddTheComparerRow].Message);
        Assert.StartsWith(PartF.H0[PartF.AddTracingRow].ShortSha!, PartF.AddTracing, StringComparison.Ordinal);
        Assert.StartsWith(PartF.H0[PartF.StopTracingRow].ShortSha!, PartF.StopTracing, StringComparison.Ordinal);
    }

    [Fact]
    public void The_searches_matches_are_rows_of_their_graphs_and_the_others_are_dimmed()
    {
        foreach (var matches in new[] { PartF.TokenizerMatches, PartF.JonasMatches, PartF.ShaMatches, PartF.ParserMatches, PartF.ReaderMatches, PartF.ChangesMatches })
        {
            Assert.All(matches, row => Assert.InRange(row, 0, PartF.H0.Count - 1));
            Assert.Equal(PartF.H0.Count - matches.Count, PartF.Others(PartF.H0.Count, matches).Count);
        }

        Assert.Equal(["Trace again", "Stop tracing", "Add tracing"], PartF.ChangesMatchesAfterRefresh.Select(row => PartF.HF1[row].Message));
        Assert.StartsWith(PartF.ShaText, PartF.RenameParser, StringComparison.Ordinal);
    }

    [Fact]
    public void The_positions_follow_the_conventions()
    {
        Assert.Equal(191, PartF.HistoryRowCentre(0, barOpen: true));
        Assert.Equal(337, PartF.HistoryRowCentre(PartF.RenameParserRow, barOpen: false));
        Assert.Equal(PartF.HistoryRowCentre(PartF.CompareWholeLinesRow, barOpen: true), PartF.MarkRow6At.Y);
        Assert.Equal(PartF.HistoryRowCentre(PartF.HandleEmptyInputRow, barOpen: true), PartF.MarkRow10At.Y);
        Assert.Equal(PartF.LaneX(0) + 3, PartF.Row0NodeAt.X);
        Assert.Equal(PartF.LaneX(1) + 3, PartF.Row4NodeAt.X);
        Assert.Equal(PartF.HistoryRowCentre(0, barOpen: false), PartF.Row0NodeClosedAt.Y);
        Assert.Equal(PartF.BarTop + PartF.BarHeight, PartF.RowsTopOpen - 28);
        Assert.Equal(PartF.RowCentre(0), PartF.GS0RingAt.Y);
        Assert.Equal(PartF.RowCentre(1), PartF.FL0Rings[1].At.Y);
    }

    [Fact]
    public void The_dashed_ranges_follow_FD15()
    {
        // From 2 below a ring's bottom (its centre + 7) to 1 above the base node's top (its centre − 6).
        Assert.Equal(PartF.RowCentre(0) + 7, PartF.GS0Stretches[0].Top);
        Assert.Equal(PartF.RowCentre(2) - 6, PartF.GS0Stretches[0].Bottom);
        Assert.Equal(PartF.RowCentre(1) + 7, PartF.FL0Stretches[1].Top);
        Assert.Equal(PartF.RowCentre(2) - 6, PartF.FL0Stretches[1].Bottom);

        // FL0's lane 0 ends 2 below row 11's top, where lane 2's curve comes into Alpha one.
        Assert.Equal(PartF.RowCentre(11) - 13 + 2, PartF.FL0Stretches[0].Bottom);
        Assert.Equal(12, PartF.FL0Dashed.Count(line => line.From == 0));
        Assert.Equal(2, PartF.FL0Dashed.Count(line => line.From == 1));
    }

    [Fact]
    public void The_calls_are_written_as_the_call_log_writes_them()
    {
        Assert.Equal(
            "git log --no-walk=unsorted --stdin --no-show-signature --no-color --encoding=UTF-8 --format=%H -F --regexp-ignore-case --grep=tokenizer --",
            PartF.CommitsCall(PartF.Tokenizer));
        Assert.Equal(
            "git log --no-walk=unsorted --stdin --no-show-signature --no-color --encoding=UTF-8 --format=%x1e%H --first-parent --root --no-renames --name-only -- :(glob,icase)**/*parser* :(glob,icase)**/*parser*/**",
            PartF.FilesCall(PartF.ParserText));
        Assert.Equal(
            "git rev-parse refs/heads/main^{commit} refs/heads/feature/compare^{commit} --",
            PartF.RevParseCall(PartF.MainRef, PartF.FeatureCompareRef));
        Assert.StartsWith(PartF.GraphLogStart, PartF.GraphLogCall(PartF.HideRevisions), StringComparison.Ordinal);
        Assert.EndsWith(" --", PartF.GraphLogCall(PartF.SoloRevisions), StringComparison.Ordinal);
    }
}
