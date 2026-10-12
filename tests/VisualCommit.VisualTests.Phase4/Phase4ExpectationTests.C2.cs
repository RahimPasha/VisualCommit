// Tests of Phase4Expectations.C2.cs's helpers and of its data against itself and the scenario's
// files (the real-window pass leaves this file out: its name does not match Phase4Expectations*.cs).
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using Tint = VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC2.Tint;

namespace VisualCommit.VisualTests.Phase4;

public sealed class PartC2ExpectationTests
{
    [Fact]
    public void Text_ends_every_line_with_a_line_feed()
    {
        Assert.Equal("a\nb\n", PartC2.Text("a", "b"));
        Assert.Equal(string.Empty, PartC2.Text());
    }

    [Fact]
    public void Tints_are_none_but_for_the_lines_named()
    {
        Assert.Equal([Tint.None, Tint.Marker, Tint.None], PartC2.Tints(3, (2, Tint.Marker)));
        Assert.Equal(20, PartC2.FmTints.Count);
        Assert.Equal(16, PartC2.TypedTints.Count);
        Assert.Equal(6, PartC2.NewConflictedTints.Count);
    }

    [Fact]
    public void The_counted_texts_use_the_singular_for_one()
    {
        Assert.Equal(("No conflicts left", "1 conflict left", "2 conflicts left"), (PartC2.ConflictsLeft(0), PartC2.ConflictsLeft(1), PartC2.ConflictsLeft(2)));
        Assert.Equal(("Resolve the conflicts first (1 file)", "Resolve the conflicts first (3 files)"), (PartC2.ResolveFirst(1), PartC2.ResolveFirst(3)));
        Assert.Equal(("Working changes · 1 conflict", "Working changes · 3 conflicts"), (PartC2.WorkingConflicts(1), PartC2.WorkingConflicts(3)));
        Assert.EndsWith(". 1 file has conflicts.", PartC2.MergeKindsBanner(1), StringComparison.Ordinal);
        Assert.EndsWith(". 2 files have conflicts.", PartC2.MergeKindsBanner(2), StringComparison.Ordinal);
        Assert.Equal(("Unstaged files (1, 1 with conflicts)", "Unstaged files (0)"), (PartC2.UnstagedTitle(1, 1), PartC2.UnstagedTitle(0)));
    }

    [Fact]
    public void The_reports_files_are_the_scenarios_files()
    {
        Assert.Equal(Scenarios.ConflictsFiles.NotesMergeConflicted, PartC2.Fm);
        Assert.Equal(Scenarios.ConflictsFiles.NotesTheirs, PartC2.TheirsNotes);
        Assert.Equal(Scenarios.ConflictsFiles.NewKindsConflicted, PartC2.NewConflicted);
        Assert.Equal(Scenarios.ConflictsFiles.OtherOurs, PartC2.OtherOurs);
        Assert.Equal(Scenarios.ConflictsFiles.FakeMergeToolResult, PartC2.FakeToolResult);
        Assert.Equal(Scenarios.ConflictsFiles.NotesPicksConflicted("01fd976"), PartC2.PicksStopNotes);
        Assert.Equal(20, PartC2.Fm.Count(character => character == '\n'));
        Assert.Equal(16, PartC2.Typed.Count(character => character == '\n'));
        Assert.Equal(12, PartC2.TypedThenTheirs.Count(character => character == '\n'));
    }

    [Fact]
    public void The_panes_of_PM_line_up_row_for_row_with_a_bar_before_each_block()
    {
        var ours = PartC2.PmOurs();
        var theirs = PartC2.PmTheirs();
        Assert.Equal(14, ours.Count);
        Assert.Equal(ours.Select(row => row.IsBar), theirs.Select(row => row.IsBar));
        Assert.Equal([1, 10], ours.Select((row, index) => (row, index)).Where(pair => pair.row.IsBar).Select(pair => pair.index));
        Assert.Equal(Tint.Used, PartC2.PmOurs(Tint.Used)[2].Tint);
        Assert.Equal("line 10 theirs", theirs[11].Text);
    }

    [Fact]
    public void The_graphs_have_their_lanes_and_rows()
    {
        Assert.Equal(10, PartC2.GcM.Count);
        Assert.Equal(11, PartC2.K1.Count);
        Assert.Equal(10, PartC2.K2.Count);
        Assert.Equal(PartC2.ConflictsLanes, PartC2.GcM.Max(row => row.Lane) + 1);
        Assert.Equal(PartC2.K2Lanes, PartC2.K2.Max(row => row.Lane) + 1);
        Assert.Equal(PartC2.GcM, PartC2.K1.Skip(1).Select(row => row with { Labels = [.. row.Labels.Select(label => label.Replace(CurrentMark, string.Empty, StringComparison.Ordinal))] }).ToList(), new RowComparer());
    }

    /// <summary>K1 is GC-M below the replayed commit, with main's check mark gone.</summary>
    private sealed class RowComparer : IEqualityComparer<CommitRow>
    {
        public bool Equals(CommitRow? x, CommitRow? y) =>
            x is not null && y is not null && x.Message == y.Message && x.Lane == y.Lane && x.Colour == y.Colour && x.ShortSha == y.ShortSha
            && x.Labels.Select(label => label.Replace(CurrentMark, string.Empty, StringComparison.Ordinal)).SequenceEqual(y.Labels.Select(label => label.Replace(CurrentMark, string.Empty, StringComparison.Ordinal)));

        public int GetHashCode(CommitRow obj) => obj.Message.GetHashCode(StringComparison.Ordinal);
    }
}
