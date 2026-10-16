using Avalonia;
using VisualCommit.Testing;
using VisualCommit.VisualTests.Phase5;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5Undo;

/// <summary>
/// Part U's expectations of U16 to U19 (Phase5Expectations.U.cs, their region) held against the
/// numbers and texts the report writes out (docs/test-reports/phase-5.md, part U), so that the
/// tables copied into the expectations and the rules that make them agree: the points the steps
/// click, the backups' names by the harness's clock, a step's texts and calls, and the graphs
/// derived from others. Not compiled by the real-window pass (its file name does not match
/// Phase5Expectations*.cs).
/// </summary>
public class Phase5ExpectationTestsU16ToU19
{
    [Fact]
    public void The_points_the_steps_click_follow_from_the_layout()
    {
        static Point Message(int lanes, int displayRow) =>
            new(WindowLayout.MessageColumnX(lanes) + WindowLayout.MessageClickInset, WindowLayout.RowCentreY(displayRow));

        // U16: the working-changes row and "Read whole lines" (display row 1), two lanes.
        Assert.Equal(new Point(468, 129), PartU.WorkingRowPoint);
        Assert.Equal(PartU.WorkingRowPoint, Message(2, 0));
        Assert.Equal(new Point(468, 155), Message(2, 1));

        // U17 to U19 on "ops", three lanes: Add core (row 6, display row 7 under a working row), Main work (row 3).
        Assert.Equal(new Point(484, 285), Message(PartU.OpsLanes, 6));
        Assert.Equal(new Point(484, 311), Message(PartU.OpsLanes, 7));
        Assert.Equal(new Point(484, 207), Message(PartU.OpsLanes, 3));

        // U19 part H on "interactive", one lane: Base (row 6); the list's row Two in L0 (rows of 30 from y 124).
        Assert.Equal(new Point(468, 285), Message(1, 6));
        Assert.Equal(new Point(600, 124 + (4 * 30) + 15), PartU.ListRowTwoPoint);
        Assert.Equal("938844a Two", PartU.L0Rows[4]);
    }

    [Fact]
    public void The_backups_are_named_by_the_harness_clock_a_second_apart()
    {
        Assert.Equal("discard-20260102T090001000Z", PartU.BackupStem("discard", 1));
        Assert.Equal("refs/visualcommit/backup/discard-20260102T090003000Z", PartU.BackupRefName("discard", 3));
        Assert.Equal("refs/visualcommit/backup/reset-20260102T090002000Z-changes", PartU.BackupRefName("reset", 2, "-changes"));

        var rebase = BackupRef.Parse(PartU.BackupRefName("rebase", 1, "/feature/topic"));
        Assert.Equal(("rebase", "feature/topic", false), (rebase.Kind, rebase.Name, rebase.IsChanges));
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 9, 0, 1, TimeSpan.Zero), rebase.Time);
        Assert.Equal("branch", BackupRef.Parse(PartU.BackupRefName("branch", 1, "/feature/alpha")).Kind);
        Assert.Equal("detached", BackupRef.Parse(PartU.BackupRefName("detached", 1)).Kind);
    }

    [Fact]
    public void A_steps_texts_and_calls_are_the_reports()
    {
        Assert.Equal("Undo commit \"Describe the reader and add Length\"", PartU.Undo.Title(PartU.CommitText));
        Assert.Equal("Undoing commit \"Describe the reader and add Length\"…", PartU.Undo.RunningText(PartU.CommitText));
        Assert.Equal("Undid commit \"Describe the reader and add Length\"", PartU.Undo.DoneText(PartU.CommitText));
        Assert.Equal("Redid reset of main to 8beca34 (soft)", PartU.Redo.DoneText(PartU.ResetText("soft")));
        Assert.Equal("VisualCommit: redo creation of keep", PartU.Redo.Message(PartU.CreateKeepText));

        // The call log writes a command as a user would type it: the message is one argument.
        Assert.Equal(
            "git update-ref -m \"VisualCommit: undo commit \\\"Describe the reader and add Length\\\"\" --stdin",
            PartU.Transaction(PartU.Undo.Message(PartU.CommitText)));
        Assert.Equal(
            "git update-ref -m \"VisualCommit: undo deletion of feature/alpha\" --create-reflog --stdin",
            PartU.Transaction(PartU.Undo.Message(PartU.DeleteAlphaText), createReflog: true));
        Assert.Equal(
            $"git update-ref -m \"VisualCommit: undo pull into main\" refs/heads/main {PartU.LocalWorkSha} {PartU.LocalWorkRebasedSha}",
            PartU.KeepMoveRef(PartU.Undo.Message(PartU.PullText), "main", PartU.LocalWorkSha, PartU.LocalWorkRebasedSha));
        Assert.Equal($"git read-tree -m -u {PartU.FastForwardMeSha} {PartU.MainWorkSha}", PartU.ReadTree(PartU.FastForwardMeSha, PartU.MainWorkSha));
        Assert.Equal("git merge --no-edit --no-log -m \"Merge branch 'feature/ff'\" refs/heads/feature/ff", PartU.MergeFfCall);
        Assert.Equal($"update refs/heads/main {PartU.ReadWholeLinesSha} {PartU.U16CommitSha}", PartU.UpdateLine("refs/heads/main", PartU.ReadWholeLinesSha, PartU.U16CommitSha));
        Assert.Equal($"delete refs/heads/hotfix {PartU.MainWorkSha}", PartU.DeleteLine("refs/heads/hotfix", PartU.MainWorkSha));
        Assert.Equal($"create refs/heads/hotfix {PartU.MainWorkSha}", PartU.CreateLine("refs/heads/hotfix", PartU.MainWorkSha));

        // The summary counter: 72 less the summary's length.
        Assert.Equal(PartU.CommitSummaryCounter, (72 - PartU.CommitSummary.Length).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(PartU.AmendSummaryCounter, (72 - PartU.AmendSummary.Length).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void The_history_graphs_after_the_commit_and_the_amend_derive_from_H0()
    {
        Assert.Equal(14, PartU.H0.Count);
        Assert.Equal(15, PartU.HU1.Count);
        Assert.Equal(15, PartU.HU2.Count);
        Assert.Equal(PartU.H0.Skip(1), PartU.HU1.Skip(2));
        Assert.Equal(["origin/main"], PartU.HU1[1].Labels);

        // The amend: both "Read whole lines" side by side; the side branches' colours one higher.
        Assert.Equal(["bd74e66", "c0bc2dd"], PartU.HU2.Take(2).Select(row => row.ShortSha));
        Assert.Equal([2, 3, 3], PartU.HU2.Where(row => row.Message is "Explain the reader" or "Compare whole lines" or "Add the comparer").Select(row => row.Colour));
        Assert.All(PartU.HU2.Skip(3), row => Assert.Equal(row.Lane, PartU.H0.Single(h0 => h0.ShortSha == row.ShortSha).Lane));
    }

    [Fact]
    public void The_label_only_changes_of_G0_keep_its_rows_lanes_and_colours()
    {
        IReadOnlyList<CommitRow>[] variants =
        [
            PartU.G1, PartU.G6, PartU.G0Hotfix, PartU.G0HotfixCurrent, PartU.G0Conflicting, PartU.G0WithoutAlpha,
            PartU.G0FfCurrent, PartU.G0AlphaCurrent, PartU.G0HeadAtAddCore,
        ];
        foreach (var variant in variants)
        {
            Assert.Equal(
                PartU.G0.Select(row => (row.Message, row.Lane, row.Colour, row.ShortSha)),
                variant.Select(row => (row.Message, row.Lane, row.Colour, row.ShortSha)));
        }

        // The branch HEAD is on carries the check mark, and exactly one row of each graph does.
        Assert.All(variants.Where(variant => variant != PartU.G0HeadAtAddCore), variant => Assert.Single(variant, row => row.Labels.Any(label => label.EndsWith(CurrentMark, StringComparison.Ordinal))));
        Assert.Equal(["HEAD", "v1.0"], PartU.G0HeadAtAddCore[6].Labels);
        Assert.Equal(["main ✓", "hotfix", "origin/main", "v1.1"], PartU.G0Hotfix[3].Labels);
        Assert.Equal(["hotfix ✓", "main", "origin/main", "v1.1"], PartU.G0HotfixCurrent[3].Labels);
    }

    [Fact]
    public void The_graphs_of_the_history_operations_hold_the_commits_the_checks_make()
    {
        Assert.Equal(PartU.Short(PartU.MergeCommitSha), PartU.G2[0].ShortSha);
        Assert.Equal([PartU.Short(PartU.PickedAlphaTwoSha), PartU.Short(PartU.PickedAlphaOneSha)], PartU.G8.Take(2).Select(row => row.ShortSha));
        Assert.Equal(PartU.Short(PartU.RevertSha), PartU.G9[0].ShortSha);
        Assert.Equal([PartU.Short(PartU.TopicTwoOntoMainSha), PartU.Short(PartU.TopicOneOntoMainSha)], PartU.R1.Take(2).Select(row => row.ShortSha));
        Assert.Equal(PartU.Short(PartU.OtherOneOntoMainSha), PartU.R3[0].ShortSha);
        Assert.Equal(PartU.Short(PartU.MergeOtherSha), PartU.R4[0].ShortSha);
        Assert.Equal(PartU.Short(PartU.InteractiveTipSha), PartU.GU19[0].ShortSha);
        Assert.Equal(PartU.Short(PartU.SquashTipSha), PartU.GB8[0].ShortSha);
        Assert.Equal(PartU.Short(PartU.LocalWorkRebasedSha), PartU.S2[0].ShortSha);
        Assert.Equal(PartU.Short(PartU.LocalWorkSha), PartU.S2u[0].ShortSha);

        // G-U19: Five dropped, Two reworded, the others the scenario's subjects in order.
        Assert.Equal(["Four", "Three", "Fix one", PartU.TwoReworded, "One", "Base", "Initial commit"], PartU.GU19.Select(row => row.Message));
        Assert.Equal(PartU.GI.Skip(1).Select(row => row.Message), PartU.GU19.Select(row => row.Message == PartU.TwoReworded ? "Two" : row.Message));
    }
}
