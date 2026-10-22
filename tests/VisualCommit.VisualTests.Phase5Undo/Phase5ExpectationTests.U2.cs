// Tests of Phase5Expectations.U2.cs's helpers and of its data against itself and the scenarios (the
// real-window pass leaves this file out: its name does not match Phase5Expectations*.cs).
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5Undo;

public sealed class PartU2ExpectationTests
{
    [Fact]
    public void A_command_is_written_as_the_call_log_writes_it()
    {
        Assert.Equal(
            "git update-ref -m \"VisualCommit: undo creation of tag v1.2\" --stdin",
            PartU2.Transaction(PartU2.UndoMessage(PartU2.CreateTagText)));
        Assert.Equal("VisualCommit: redo deletion of tag v1.0", PartU2.RedoMessage(PartU2.DeleteTagText));
        Assert.Equal(PartU2.StashStore, PartU2.Command("stash", "store", "-m", "On main: Experiment", PartU2.Experiment));
        Assert.Equal(PartU2.MergeFf, PartU2.Command("merge", "--no-edit", "--no-log", "-m", "Merge branch 'feature/ff'", "--no-overwrite-ignore", "refs/heads/feature/ff"));
    }

    [Fact]
    public void A_steps_push_leases_what_the_remote_holds_now_both_ways()
    {
        Assert.Equal(
            $"git push --progress --force-with-lease=refs/heads/topic:{PartU2.TopicWork} origin --delete refs/heads/topic",
            PartU2.StepPush("refs/heads/topic", PartU2.TopicWork, null));
        Assert.Equal(
            $"git push --progress --force-with-lease=refs/heads/topic: origin {PartU2.TopicWork}:refs/heads/topic",
            PartU2.StepPush("refs/heads/topic", null, PartU2.TopicWork));
        Assert.Equal(
            $"git push --progress --force-with-lease=refs/tags/v1.1: origin {PartU2.MainWork}:refs/tags/v1.1",
            PartU2.StepPush("refs/tags/v1.1", string.Empty, PartU2.MainWork));
    }

    [Fact]
    public void The_texts_name_the_commits_by_their_short_ids()
    {
        Assert.Contains($"{PartU2.PublishedWork[..7]}...{PartU2.RewrittenWork[..7]} {PartU2.RewrittenWork} ", PartU2.ForceRedoneLine, StringComparison.Ordinal);
        Assert.Contains($"{PartU2.RewrittenWork[..7]}...{PartU2.PublishedWork[..7]} {PartU2.PublishedWork} ", PartU2.ForceUndoneLine, StringComparison.Ordinal);
        Assert.Contains(PartU2.PublishedWork[..7], PartU2.UndoForcePush.Text, StringComparison.Ordinal);
        Assert.Contains(PartU2.RewrittenWork[..7], PartU2.UndoForcePush.Text, StringComparison.Ordinal);
        Assert.Contains(PartU2.UnpushedWork[..7], PartU2.UndoLeasedPush.Text, StringComparison.Ordinal);
        Assert.EndsWith($"{PartU2.TopicWork[..7]}.", PartU2.RedoPushTopic.Text, StringComparison.Ordinal);
        Assert.EndsWith($"{PartU2.MainWork[..7]}.", PartU2.RedoPushTag.Text, StringComparison.Ordinal);
        Assert.Contains(PartU2.TerminalWork[..7], PartU2.MovedSince, StringComparison.Ordinal);
        Assert.Contains(PartU2.PublishedWork, PartU2.Tu2Key, StringComparison.Ordinal);
        Assert.Equal(["To o.git", PartU2.Tu2Key, "error: failed to push some refs to 'o.git'"], PartU2.Tu2("o.git"));
    }

    [Fact]
    public void The_backup_names_are_the_first_of_a_session()
    {
        Assert.Equal("refs/visualcommit/backup/stash-20260102T090001000Z", PartU2.FirstBackup("stash"));
        var undone = BackupRef.Parse(PartU2.FirstBackup("tag-undone", "v1.2"));
        Assert.Equal(("tag-undone", "v1.2", false), (undone.Kind, undone.Name, undone.IsChanges));
        Assert.Equal("remote-tag-20260102T090001000Z", BackupRef.Parse(PartU2.FirstBackup("remote-tag", "origin/v1.1")).Stem);
    }

    [Fact]
    public void The_graphs_are_G0_with_the_changes_the_report_names()
    {
        Assert.Equal(8, PartU2.G0.Count);
        Assert.Equal(PartU2.G0Lanes, PartU2.G0.Max(row => row.Lane) + 1);
        Assert.Equal("Alpha two", PartU2.G0[PartU2.AlphaTwoRow].Message);
        Assert.Equal("Main work", PartU2.G0[PartU2.MainWorkRow].Message);
        Assert.Equal("Fast-forward me", PartU2.G0[PartU2.FastForwardRow].Message);
        Assert.Equal("Conflicting change", PartU2.G0[PartU2.ConflictingRow].Message);
        Assert.Equal(PartU2.G0.Skip(1).Select(row => row.Message), PartU2.G0NoStash.Select(row => row.Message));
        Assert.Equal(9, PartU2.G0TwoStashes.Count);
        Assert.Equal(4, PartU2.G0TwoStashes.Max(row => row.Lane) + 1);
        Assert.Equal(["feature/alpha", "origin/feature/alpha", "v1.2"], PartU2.G0WithV12[PartU2.AlphaTwoRow].Labels);
        Assert.Empty(PartU2.G0WithoutV10[6].Labels);
        Assert.Equal([Current("main"), "feature/ff"], PartU2.G1[PartU2.FastForwardRow].Labels);
        Assert.Equal(["origin/main", "v1.1"], PartU2.G1[PartU2.MainWorkRow].Labels);
        Assert.Equal("Fast-forward me", PartU2.G0Terminal[PartU2.TerminalFastForwardRow].Message);
        Assert.Equal(PartU2.G0.Select(row => (row.Message, row.Lane, row.Colour)), PartU2.G1.Select(row => (row.Message, row.Lane, row.Colour)));
    }

    [Fact]
    public void The_sync_graphs_move_only_the_labels_they_name()
    {
        Assert.Equal(PartU2.SyncLanes, PartU2.Sync.Max(row => row.Lane) + 1);
        Assert.Equal(["topic", "origin/topic"], PartU2.SyncTopicPushed[0].Labels);
        Assert.Equal(["published", "origin/published"], PartU2.SyncPublishedPushed[1].Labels);
        Assert.Empty(PartU2.SyncPublishedPushed[2].Labels);
        Assert.Empty(PartU2.SyncPublishedDeleted[2].Labels);
        Assert.Equal(["upstream/published"], PartU2.SyncRenamed[2].Labels);
        Assert.Equal([Current("main")], PartU2.SyncWithoutRemote[3].Labels);
        Assert.Equal(3, PartU2.SyncRewritten.Max(row => row.Lane) + 1);
        Assert.Equal(4, PartU2.SyncForcePushed.Count);
        Assert.Equal(PartU2.RebaseLanes, PartU2.R1.Max(row => row.Lane) + 1);
        Assert.Equal([PartU2.OtherOne[..7], PartU2.MainMore[..7]], PartU2.R0.Take(2).Select(row => row.ShortSha));
    }

    [Fact]
    public void The_files_are_the_scenarios_files_as_the_checks_change_them()
    {
        Assert.StartsWith(Scenarios.OperationsFiles.CoreMainWork, PartU2.CoreMoreWork, StringComparison.Ordinal);
        Assert.StartsWith(Scenarios.OperationsFiles.CoreMainWork, PartU2.CoreMine, StringComparison.Ordinal);
        Assert.Equal(14, PartU2.Gitignore.Length);
        Assert.Equal($"stash@{{1}} {PartU2.Experiment} On main: Experiment", PartU2.TwoStashes[1]);
        Assert.Equal(PartU2.TwoStashes[1].Replace("stash@{1}", "stash@{0}", StringComparison.Ordinal), PartU2.ExperimentOnly[0]);
    }
}
