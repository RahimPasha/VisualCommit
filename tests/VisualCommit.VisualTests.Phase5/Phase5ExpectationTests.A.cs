using Avalonia;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

/// <summary>
/// Part A's expectations (Phase5Expectations.A.cs) held against each other and against the
/// report's numbers: the panel's geometry, the graphs, the backups, the texts built from parts,
/// the toolbar's states and the pictures' names. No app: a mistake copying the report shows here,
/// apart from the app.
/// </summary>
public sealed class Phase5ExpectationTestsA : Phase5Checks
{
    /// <summary>The panel's places at 1100×700 that the report states (part A's conventions, A2 step 1, A3).</summary>
    [Theory]
    [InlineData(200, 474, 469)]
    [InlineData(300, 374, 369)]
    [InlineData(320, 354, 349)]
    [InlineData(421, 253, 248)]
    [InlineData(120, 554, 549)]
    public void The_panel_and_its_splitter_stand_where_the_report_puts_them(double height, double panelTop, double splitterTop)
    {
        Assert.Equal(new Rect(260, panelTop, 440, height), PartA.PanelAt(height));
        Assert.Equal(new Rect(260, splitterTop, 440, 5), PartA.SplitterAt(height));
        Assert.Equal(674, PartA.PanelAt(height).Bottom);
    }

    /// <summary>The maximum is the graph area's height less 165 (AD1), the drags of AD9 reach it and the minimum.</summary>
    [Fact]
    public void The_panels_limits_are_AD1s_and_A3s_drags_reach_them()
    {
        Assert.Equal(PartA.MaximumHeight, PartA.GraphArea.Height - 165);
        Assert.Equal(PartA.DefaultHeight - PartA.FirstDrag, PartA.DraggedHeight);
        Assert.True(PartA.DraggedHeight - PartA.DragToMaximum >= PartA.MaximumHeight, "The second drag does not reach the maximum.");
        Assert.True(PartA.MaximumHeight - PartA.DragToMinimum <= PartA.MinimumHeight, "The third drag does not reach the minimum.");
    }

    /// <summary>H0 has 14 rows and H-A1 15, the commit on top of H0's; "conflicts" 10, its Base where lanes 1 to 4 end.</summary>
    [Fact]
    public void The_graphs_have_the_reports_rows()
    {
        Assert.Equal(14, PartA.H0.Count);
        Assert.Equal(15, PartA.HA1.Count);
        Assert.Equal(PartA.H0.Skip(1), PartA.HA1.Skip(2));
        Assert.Equal(["main ✓"], PartA.HA1[0].Labels);
        Assert.Equal(["origin/main"], PartA.HA1[1].Labels);
        Assert.Equal(PartA.DescribeTheLinesSha[..7], PartA.HA1[0].ShortSha);
        Assert.Equal(10, PartA.ConflictsGraph.Count);
        Assert.Equal([1, 2, 3, 4], PartA.ConflictsGraph[8].Ends);
        Assert.Equal("Kinds edits", PartA.ConflictsGraph[PartA.KindsEditsRow].Message);
    }

    /// <summary>The clean-up at opening leaves reset-20251220T090002000Z to …051000Z; the abort's leaves its own and drops the oldest.</summary>
    [Fact]
    public void The_backups_left_are_the_reports()
    {
        Assert.Equal(50, PartA.BackupsAfterOpening.Count);
        Assert.Equal("reset-20251220T090002000Z", PartA.BackupsAfterOpening[0]);
        Assert.Equal(PartA.NewestBackup, PartA.BackupsAfterOpening[^1]);
        Assert.Equal(PartA.OldestBackup, PartA.BackupsAfterOpening[0]);
        Assert.Equal(50, PartA.BackupsAfterAbort.Count);
        Assert.DoesNotContain(PartA.OldestBackup, PartA.BackupsAfterAbort);
        Assert.Contains("reset-20251220T090002000Z", PartA.AbortNote, StringComparison.Ordinal);
        Assert.All(PartA.CleanUpInput, line => Assert.EndsWith(" " + PartA.OursEditsSha, line, StringComparison.Ordinal));
        Assert.Equal(
            ["create refs/visualcommit/backup/abort-20260102T090001000Z " + PartA.OursEditsSha, "create refs/visualcommit/backup/abort-20260102T090001000Z-changes 1234"],
            PartA.AbortBackupInput("1234"));
        Assert.Equal(PartA.AbortCalls.Count, PartA.AbortCallsOnTemporaryIndex.Count);
    }

    /// <summary>The undo's and the redo's texts, built from the commit's, are the report's word for word.</summary>
    [Fact]
    public void The_undo_texts_are_the_reports()
    {
        Assert.Equal("Undo commit \"Describe the lines\"", PartA.UndoCommit);
        Assert.Equal("Redo commit \"Describe the lines\"", PartA.RedoCommit);
        Assert.Equal("Undoing commit \"Describe the lines\"…", PartA.UndoingCommit);
        Assert.Equal("Undid commit \"Describe the lines\"", PartA.UndidCommit);
        Assert.Equal("Redid commit \"Describe the lines\"", PartA.RedidCommit);
        Assert.Equal("git update-ref -m \"VisualCommit: undo commit \\\"Describe the lines\\\"\" --stdin", PartA.UndoUpdateRef);
        Assert.Equal("update refs/heads/main c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9 d1e23a37b6b08f4dc9662fab930f232a66ecd8fc", PartA.UndoInput);
        Assert.Equal("update refs/heads/main d1e23a37b6b08f4dc9662fab930f232a66ecd8fc c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9", PartA.RedoInput);
        Assert.Equal("Search commits (Ctrl+F)", PartA.SearchToolTip(commandIsMeta: false));
        Assert.Equal("Search commits (Cmd+F)", PartA.SearchToolTip(commandIsMeta: true));
    }

    /// <summary>The toolbar's states list every button once, in the toolbar's order; only Theme is enabled on an empty tab.</summary>
    [Fact]
    public void The_toolbars_states_cover_every_button()
    {
        foreach (var states in new[] { PartA.EmptyTabToolbar, PartA.OpenToolbar(false), PartA.CommittedToolbar(false) })
        {
            Assert.Equal(PartA.ToolbarButtons, states.Select(state => state.Id));
        }

        Assert.Equal(["ThemeSwitch"], PartA.EmptyTabToolbar.Where(state => state.Enabled).Select(state => state.Id));
        Assert.All(PartA.EmptyTabToolbar.Take(9), state => Assert.Null(state.ToolTip));
        Assert.Equal((true, PartA.UndoCommit + " (Ctrl+Z)"), (PartA.CommittedToolbar(false)[0].Enabled, PartA.CommittedToolbar(false)[0].ToolTip));
        Assert.Equal("Undo commit \"Describe the lines\" (Cmd+Z)", PartA.CommittedToolbar(true)[0].ToolTip);
    }

    /// <summary>R1 is 16 lines with one block; FM's merge markers name feature/theirs as a terminal's merge writes them.</summary>
    [Fact]
    public void R1_has_sixteen_lines_and_one_block()
    {
        var lines = PartA.NotesR1.TrimEnd('\n').Split('\n');
        Assert.Equal(16, lines.Length);
        Assert.Single(lines, line => line.StartsWith("<<<<<<<", StringComparison.Ordinal));
        Assert.Equal(">>>>>>> feature/theirs", lines[13]);
    }

    /// <summary>Every picture of part A has a name of the report's form, each once, as many as the Screenshots column lists.</summary>
    [Fact]
    public void Part_As_pictures_have_the_reports_names()
    {
        Assert.Equal(27, PartA.Pictures.Count);
        Assert.Equal(PartA.Pictures.Count, PartA.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PartA.Pictures, name => Assert.Matches(PictureName(), name));
        Assert.All(PartA.Pictures, name => Assert.StartsWith("a", name, StringComparison.Ordinal));
    }
}
