using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC;

namespace VisualCommit.VisualTests.Phase4;

/// <summary>
/// The helpers of Phase4Expectations.C.cs (checks C10 to C13) against the report's own tables
/// ("The conflicted files, the panes and the results"), so that a check that fails points at the
/// app and not at how its expected rows were built. Not compiled by the real-window pass.
/// </summary>
public sealed class Phase4ExpectationTestsC
{
    [Fact]
    public void The_merge_panes_are_PM_twelve_numbered_rows_each_with_a_bar_before_each_block()
    {
        var (ours, theirs) = MergePanes((SideShows.Buttons, SideShows.Buttons), (SideShows.Buttons, SideShows.Buttons));

        Assert.Equal(14, ours.Count);
        Assert.Equal(14, theirs.Count);
        Assert.Equal([1, 10], ours.Select((row, index) => (row, index)).Where(entry => entry.row.IsBar).Select(entry => entry.index).ToArray());
        Assert.Equal(Enumerable.Range(1, 12).Cast<int?>(), ours.Where(row => !row.IsBar).Select(row => row.Number));
        Assert.Equal(new PaneRow("line 2 ours", LineTint.Ours, false, 2, 0, null), ours[2]);
        Assert.Equal(new PaneRow("line 2 theirs", LineTint.Theirs, false, 2, 0, null), theirs[2]);
        Assert.Equal(new PaneRow("line 6 theirs", LineTint.None, false, 6, null, null), ours[6]);
        Assert.Equal(new PaneRow("line 6 theirs", LineTint.None, false, 6, null, null), theirs[6]);
        Assert.Equal(new PaneRow(string.Empty, LineTint.Bar, true, null, 1, null), ours[10]);
        Assert.Equal(new PaneRow("line 10 theirs", LineTint.Theirs, false, 10, 1, null), theirs[11]);
    }

    [Fact]
    public void A_sides_state_gives_its_bar_text_and_its_rows_tint()
    {
        var (ours, theirs) = MergePanes((SideShows.Used, SideShows.NotUsed), (SideShows.Used, SideShows.Used));

        Assert.Equal(UsedInResult, ours[1].BarText);
        Assert.Equal(NotUsed, theirs[1].BarText);
        Assert.Equal(LineTint.Used, ours[2].Tint);
        Assert.Equal(LineTint.None, theirs[2].Tint);
        Assert.Equal(LineTint.Used, ours[11].Tint);
        Assert.Equal(LineTint.Used, theirs[11].Tint);
        Assert.Equal("Used in the result", UsedInResult);
        Assert.Equal("Not used", NotUsed);
    }

    [Fact]
    public void The_results_are_the_reports_R0_to_R3()
    {
        Assert.Equal(20, LinesOf(NotesMerge).Count);
        Assert.Equal(16, LinesOf(NotesMergeR1).Count);
        Assert.Equal(12, LinesOf(NotesMergeR2).Count);
        Assert.Equal(13, LinesOf(NotesMergeR3).Count);
        Assert.Equal(["<<<<<<< HEAD", "line 2 ours", "=======", "line 2 theirs", ">>>>>>> feature/theirs"], LinesOf(NotesMerge).Skip(1).Take(5));
        Assert.Equal(["line 10 ours", "line 10 theirs"], LinesOf(NotesMergeR3).Skip(9).Take(2));
        Assert.Equal(Scenarios.ConflictsFiles.NotesOurs, NotesPicksOurs);
        Assert.Equal("line 2 ours and picked", LinesOf(NotesPicksTyped)[1]);
        Assert.Equal(["line 10 second", "line 10 ours"], LinesOf(NotesRebaseSecondBoth).Skip(9).Take(2));
        Assert.Equal(">>>>>>> 305f7f7 (Second change)", LinesOf(NotesSkippedSecond)[13]);
        Assert.Equal("line 2 ours", LinesOf(NotesSkippedSecond)[1]);
    }

    [Fact]
    public void The_result_tints_mark_each_blocks_lines_as_FM_shows_them()
    {
        var tints = C10Opened.Tints;

        Assert.Equal(20, tints.Count);
        Assert.Equal(
            [LineTint.None, LineTint.Marker, LineTint.Ours, LineTint.Marker, LineTint.Theirs, LineTint.Marker, LineTint.None],
            tints.Take(7));
        Assert.Equal([LineTint.Marker, LineTint.Ours, LineTint.Marker, LineTint.Theirs, LineTint.Marker], tints.Skip(13).Take(5));
        Assert.All(C10Block2Both.Tints, tint => Assert.Equal(LineTint.None, tint));
        Assert.Equal(LineTint.Marker, C10Block1Ours.Tints[9]);
    }
}
