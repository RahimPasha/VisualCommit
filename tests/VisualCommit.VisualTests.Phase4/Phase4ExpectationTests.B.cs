using Avalonia;
using VisualCommit.Core.Git;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartB;

namespace VisualCommit.VisualTests.Phase4;

/// <summary>
/// Part B's expectation helpers (Phase4Expectations.B.cs) held against the numbers the report
/// writes out (docs/test-reports/phase-4.md, part B), so that the tables copied into the
/// expectations and the rules that make them agree: the list's rows and second lines, the join
/// lines, the drag's gaps, the menus' heights, the points the steps click and the counter. Not
/// compiled by the real-window pass (its file name does not match Phase4Expectations*.cs).
/// </summary>
public class Phase4ExpectationTestsB
{
    [Fact]
    public void The_lists_row_positions_follow_from_30_px_rows_and_22_px_second_lines()
    {
        static void Check(IReadOnlyList<ListRow> rows, double baseTop)
        {
            var (tops, below) = RowTops([.. rows.Select(row => row.SecondLine is not null)]);
            Assert.Equal(rows.Select(row => row.Top), tops);
            Assert.Equal(baseTop, below);
        }

        Check(L0, L0BaseTop);
        Check(B7Moved, L0BaseTop);
        Check(B7Fixup, L0BaseTop);
        Check(B7Squash, B7SquashBaseTop);
        Check(B7Edited, B7SquashBaseTop);
        Check(B7Reworded, L7BaseTop);
        Check(L7, L7BaseTop);
        Check(B9MovedDown, L0BaseTop);
        Check(B9Dropped, L0BaseTop);

        // The report's tables: L0 124 to 334, L7's second lines 214 to 236 and 266 to 288, its base row 348 to 378.
        Assert.Equal([124.0, 154, 184, 214, 244, 274], L0.Select(row => row.Top));
        Assert.Equal(304, L0BaseTop);
        Assert.Equal([124.0, 154, 184, 236, 288, 318], L7.Select(row => row.Top));
        Assert.Equal(new Rect(260, 214, 440, 22), L7[2].SecondLineBounds);
        Assert.Equal(new Rect(260, 266, 440, 22), L7[3].SecondLineBounds);
        Assert.Equal(348, L7BaseTop);
        Assert.Equal([124.0, 154, 184, 236, 266, 296], B7Squash.Select(row => row.Top));
        Assert.Equal(326, B7SquashBaseTop);
    }

    [Fact]
    public void The_join_lines_run_from_the_members_centre_to_its_targets_top_edge_at_x_272()
    {
        // B7 step 4: Fix one (244) joined to One (274): y 259 to 274.
        Assert.Equal(new Rect(272, 259, 2, 15), JoinLine(244, 274));

        // Step 5: Four (154) to Three (184), and Fix one moved down 22.
        Assert.Equal(new Rect(272, 169, 2, 15), JoinLine(154, 184));
        Assert.Equal(new Rect(272, 281, 2, 15), JoinLine(266, 296));

        // Step 8 and L7: Fix one moved down 22 more.
        Assert.Equal(new Rect(272, 303, 2, 15), JoinLine(288, 318));
    }

    [Fact]
    public void The_lists_rows_are_named_and_pointed_at_as_the_report_writes_them()
    {
        Assert.Equal(["57f089d Five", "eef5c15 Four", "ff42e7f Three", "ed58902 Fix one", "938844a Two", "fc6c8fd One"], L0.Select(row => row.Name));
        Assert.Equal(new Point(600, 139), L0[0].Point);
        Assert.Equal(new Point(600, 169), L0[1].Point);
        Assert.Equal(new Point(600, 199), L0[2].Point);
        Assert.Equal(new Point(600, 229), L0[3].Point);
        Assert.Equal(new Point(600, 289), L0[5].Point);
        Assert.Equal(new Point(600, 251), B7Edited[3].Point);
        Assert.Equal(new Point(600, 319), BaseRowPoint);
        Assert.Equal(new Point(468, 285), GraphRowPoint(6));
        Assert.Equal(new Point(468, 155), GraphRowPoint(1));
        Assert.Equal(new Point(468, 181), GraphRowPoint(2));
        Assert.All(L0, row => Assert.Equal(row.Sha[..7], Short(row.Sha)));
    }

    [Fact]
    public void The_drags_mark_the_gap_nearest_the_pointer()
    {
        // Gap k is the top edge of row k of L0; the last one is the base row's top.
        double[] gaps = [.. L0.Select(row => row.Top), L0BaseTop];
        static int Nearest(double[] tops, double y) =>
            Enumerable.Range(0, tops.Length).OrderBy(index => Math.Abs(tops[index] - y)).First();

        Assert.Equal(B7DragGap, Nearest(gaps, B7DragTo.Y));
        Assert.Equal(B7DragGapY, gaps[B7DragGap]);
        Assert.Equal(B9DragGap, Nearest(gaps, B9DragTo.Y));
        Assert.Equal(B9DragGapY, gaps[B9DragGap]);

        // B7's first step of five moves 8.2 px, past the 4-px threshold.
        Assert.Equal(8.2, (B7DragTo.Y - L0[3].Point.Y) / DragSteps, 3);
    }

    [Fact]
    public void The_menus_heights_are_4as_formula_for_their_lists()
    {
        static double HeightOf(IReadOnlyList<string> lines) =>
            MenuHeight(lines.Skip(1).Count(line => line != "—"), lines.Count(line => line == "—"));

        Assert.Equal(BaseCommitMenuHeight, HeightOf(BaseCommitMenu));
        Assert.Equal(SeveralCommitsMenuHeight, HeightOf(SeveralCommitsMenu));
        Assert.Equal(RowMenuHeight, HeightOf(RowMenu("ff42e7f Three", RebaseAction.Pick, bottomRow: false)));
    }

    [Fact]
    public void A_rows_menu_leaves_out_its_action_and_disables_squash_and_fixup_on_the_bottom_row()
    {
        Assert.Equal(["ff42e7f Three", "Reword…", "Squash", "Fixup", "Drop"], RowMenu("ff42e7f Three", RebaseAction.Pick, bottomRow: false));
        Assert.Equal(
            [
                "fc6c8fd One",
                "Reword…",
                "Squash (disabled: \"The oldest commit has nothing below it to join\")",
                "Fixup (disabled: \"The oldest commit has nothing below it to join\")",
                "Drop",
            ],
            RowMenu("fc6c8fd One", RebaseAction.Pick, bottomRow: true));
        Assert.Equal(["57f089d Five", "Pick", "Reword…", "Squash", "Fixup"], RowMenu("57f089d Five", RebaseAction.Drop, bottomRow: false));
    }

    [Fact]
    public void The_summary_counter_counts_down_from_72()
    {
        Assert.Equal("67", Counter(SquashSummary));
        Assert.Equal("72", Counter(string.Empty));
        Assert.Equal("58", Counter(EditedSummary));
        Assert.Equal("69", Counter(RewordSummary));
    }

    [Fact]
    public void G_B9_is_Six_on_top_of_G_I_with_Five_unlabelled()
    {
        Assert.Equal(GI.Count + 1, GB9.Count);
        Assert.Equal(["main ✓"], GB9[0].Labels);
        Assert.Empty(GB9[1].Labels);
        Assert.Equal(GI[0].Message, GB9[1].Message);
        Assert.Equal(GI.Skip(1), GB9.Skip(2));
        Assert.Equal("2026-01-01 12:08", GB9[0].Date);
    }
}
