using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Design 2.33's table as code (<see cref="OperationTexts"/>, D113): a representative set of its
/// titles, running texts, error titles and summaries, the success and conflict messages of
/// section 2 that the checks of docs/test-reports/phase-3.md expect, and the singular for one.
/// </summary>
public class OperationTextsTests
{
    private const string Sha = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";

    [Fact]
    public void Each_row_of_the_table_has_its_title_running_text_error_title_and_summary()
    {
        Assert.Equal(
            new OperationText("Check out feature/addfile", "Checking out feature/addfile…", "Checkout failed", "Git could not check out feature/addfile."),
            OperationTexts.CheckOut("feature/addfile"));
        Assert.Equal(
            new OperationText("Push main", "Pushing main to origin/main…", "Push failed", "Git could not push main to origin/main."),
            OperationTexts.Push("main", "origin/main"));
        Assert.Equal(
            new OperationText("Pull", "Pulling origin/main into main…", "Pull failed", "Git could not pull origin/main into main."),
            OperationTexts.Pull("origin/main", "main"));
        Assert.Equal(
            new OperationText("Fetch", "Fetching from all remotes…", "Fetch failed", "Git could not fetch from all remotes."),
            OperationTexts.FetchAll);
        Assert.Equal(
            new OperationText("Fetch origin", "Fetching from origin…", "Fetch failed", "Git could not fetch from origin."),
            OperationTexts.Fetch("origin"));
        Assert.Equal(
            new OperationText("Pop stash@{0}", "Popping stash@{0}…", "Applying the stash failed", "Git could not apply stash@{0}."),
            OperationTexts.PopStash("stash@{0}"));
        Assert.Equal(
            new OperationText("Delete tag v1.2 on origin", "Deleting tag v1.2 on origin…", "Deleting the remote tag failed", "Git could not delete the tag v1.2 on origin."),
            OperationTexts.DeleteRemoteTag("v1.2", "origin"));
        Assert.Equal(
            new OperationText("Cherry-pick a1b2c3d", "Cherry-picking a1b2c3d…", "Cherry-pick failed", "Git could not cherry-pick a1b2c3d."),
            OperationTexts.CherryPick(Sha));
        Assert.Equal(
            new OperationText("Reset HEAD", "Resetting HEAD to a1b2c3d…", "Reset failed", "Git could not reset HEAD to a1b2c3d."),
            OperationTexts.Reset("HEAD", Sha));
        Assert.Equal(
            new OperationText("Rename feature/alpha", "Renaming feature/alpha to feature/beta…", "Renaming the branch failed", "Git could not rename feature/alpha to feature/beta."),
            OperationTexts.RenameBranch("feature/alpha", "feature/beta"));
        Assert.Equal(
            new OperationText("Fast-forward published", "Fast-forwarding published to origin/published…", "Fast-forward failed", "Git could not fast-forward published to origin/published."),
            OperationTexts.FastForward("published", "origin/published"));
        Assert.Equal(
            new OperationText("Merge feature/alpha", "Merging feature/alpha into main…", "Merge failed", "Git could not merge feature/alpha into main."),
            OperationTexts.Merge("feature/alpha", "main"));
        Assert.Equal(
            new OperationText("Stop tracking origin/main", "Removing the upstream of main…", "Setting the upstream failed", "Git could not remove the upstream of main."),
            OperationTexts.UnsetUpstream("main", "origin/main"));
        Assert.Equal(
            new OperationText("Remove remote backup", "Removing remote backup…", "Removing the remote failed", "Git could not remove the remote backup."),
            OperationTexts.RemoveRemote("backup"));
        Assert.Equal(
            new OperationText("Check out feature/other with your changes", "Checking out feature/other with your changes…", "Checkout failed", "Git could not check out feature/other."),
            OperationTexts.StashAndCheckOut("feature/other"));
        Assert.Equal(
            new OperationText("Abort the cherry-pick", "Aborting the cherry-pick…", "Abort failed", "Git could not abort the cherry-pick."),
            OperationTexts.Abort(InProgressKind.CherryPick));
        Assert.Equal(
            new OperationText("Ignore all *.log files", "Adding *.log to .gitignore…", "Ignoring the file failed", "The file .gitignore could not be written."),
            OperationTexts.Ignore(OperationTexts.IgnoreExtensionLabel("log"), "*.log"));
    }

    [Fact]
    public void The_remotes_form_shows_its_own_errors_so_its_operations_have_no_error_dialog()
    {
        foreach (var text in new[] { OperationTexts.AddRemote("mirror"), OperationTexts.RenameRemote("mirror"), OperationTexts.SetRemoteUrl("mirror") })
        {
            Assert.Equal(string.Empty, text.ErrorTitle);
            Assert.Equal(string.Empty, text.FailureSummary);
        }

        Assert.Equal("Change URL of mirror", OperationTexts.SetRemoteUrl("mirror").Title);
        Assert.Equal("Renaming remote mirror…", OperationTexts.RenameRemote("mirror").RunningText);
    }

    [Fact]
    public void The_success_messages_are_those_the_checks_expect()
    {
        Assert.Equal("Checked out feature/ff", OperationTexts.CheckedOut("feature/ff"));
        Assert.Equal("Created feature/alpha tracking origin/feature/alpha and checked it out", OperationTexts.CreatedTrackingAndCheckedOut("feature/alpha", "origin/feature/alpha"));
        Assert.Equal("HEAD is now detached at 691f8cf", OperationTexts.DetachedAt("691f8cf" + new string('0', 33)));
        Assert.Equal("Created hotfix and checked it out", OperationTexts.CreatedBranch("hotfix", checkedOut: true));
        Assert.Equal("Created release-1.0", OperationTexts.CreatedBranch("release-1.0", checkedOut: false));
        Assert.Equal("Renamed feature/conflict to feature/conflicting", OperationTexts.RenamedBranch("feature/conflict", "feature/conflicting"));
        Assert.Equal("Deleted feature/ff", OperationTexts.DeletedBranch("feature/ff"));
        Assert.Equal("Fast-forwarded main to feature/ff", OperationTexts.MergeFastForwarded("main", "feature/ff"));
        Assert.Equal("Merged origin/feature/alpha into main", OperationTexts.Merged("origin/feature/alpha", "main"));
        Assert.Equal("Reset main to a1b2c3d (hard)", OperationTexts.ResetDone("main", Sha, ResetMode.Hard));
        Assert.Equal("Cherry-picked a1b2c3d onto main", OperationTexts.CherryPicked(Sha, "main"));
        Assert.Equal("Cherry-picked 2 commits onto main", OperationTexts.CherryPickedSeveral(2, "main"));
        Assert.Equal("Reverted 2 commits", OperationTexts.RevertedSeveral(2));
        Assert.Equal("Reverted a1b2c3d", OperationTexts.Reverted(Sha));
        Assert.Equal("Aborted the merge", OperationTexts.Aborted(InProgressKind.Merge));
        Assert.Equal("Fetched from all remotes", OperationTexts.FetchedAll);
        Assert.Equal("Fetched from mirror", OperationTexts.Fetched("mirror"));
        Assert.Equal("Pulled and merged origin/main into main", OperationTexts.PulledMerged("origin/main", "main"));
        Assert.Equal("Pushed topic to origin/topic, and set it as upstream", OperationTexts.Pushed("topic", "origin/topic", setUpstream: true));
        Assert.Equal("Pushed main to origin/main", OperationTexts.Pushed("main", "origin/main", setUpstream: false));
        Assert.Equal("Force-pushed published to origin/published", OperationTexts.ForcePushed("published", "origin/published"));
        Assert.Equal("Deleted published on origin", OperationTexts.DeletedRemoteBranch("published", "origin"));
        Assert.Equal("Renamed remote mirror to backup", OperationTexts.RenamedRemote("mirror", "backup"));
        Assert.Equal("Changed the URL of mirror", OperationTexts.ChangedRemoteUrl("mirror"));
        Assert.Equal("Removed remote backup", OperationTexts.RemovedRemote("backup"));
        Assert.Equal("Stashed the changes", OperationTexts.Stashed);
        Assert.Equal("Applied stash@{1}", OperationTexts.AppliedStash("stash@{1}"));
        Assert.Equal("Dropped stash@{0}", OperationTexts.DroppedStash("stash@{0}"));
        Assert.Equal("Created tag v1.2", OperationTexts.CreatedTag("v1.2"));
        Assert.Equal("Deleted tag v1.0", OperationTexts.DeletedTag("v1.0"));
        Assert.Equal("Pushed tag v1.0 to origin", OperationTexts.PushedTag("v1.0", "origin"));
        Assert.Equal("Deleted tag v1.2 on origin", OperationTexts.DeletedRemoteTag("v1.2", "origin"));
        Assert.Equal("Added /drafts/\\[wip].txt to .gitignore", OperationTexts.Ignored("/drafts/\\[wip].txt"));
        Assert.Equal("Checked out feature/other and re-applied your changes", OperationTexts.CheckedOutWithChanges("feature/other"));
    }

    [Fact]
    public void Counts_use_the_singular_for_one()
    {
        Assert.Equal("Merge stopped: 1 file has conflicts", OperationTexts.MergeStopped(1));
        Assert.Equal("Merge stopped: 3 files have conflicts", OperationTexts.MergeStopped(3));
        Assert.Equal("Pulled 1 commit into main (fast-forward)", OperationTexts.PulledFastForward(1, "main"));
        Assert.Equal("Pulled 4 commits into main (fast-forward)", OperationTexts.PulledFastForward(4, "main"));
        Assert.Equal("Cherry-pick 1 commit", OperationTexts.CherryPickSeveral(1).Title);
        Assert.Equal("Reverting 3 commits…", OperationTexts.RevertSeveral(3).RunningText);
        Assert.Equal("Applying the stash stopped: 1 file has conflicts", OperationTexts.ApplyStashConflicts(1, pop: false));
        Assert.Equal("Applying the stash stopped: 2 files have conflicts. The stash is kept.", OperationTexts.ApplyStashConflicts(2, pop: true));
        Assert.Equal(
            "HEAD is detached on 1 commit that is on no branch. After this checkout it no longer shows in the graph.",
            OperationTexts.ConfirmLeaveDetachedCommits(1).Question);
        Assert.Equal(
            "HEAD is detached on 2 commits that are on no branch. After this checkout they no longer show in the graph.",
            OperationTexts.ConfirmLeaveDetachedCommits(2).Question);
        Assert.Contains("its 1 remote branch?", OperationTexts.ConfirmRemoveRemote("backup", 1, "https://***@host/x.git").Question, StringComparison.Ordinal);
        Assert.Contains("its 2 remote branches?", OperationTexts.ConfirmRemoveRemote("backup", 2, "https://host/x.git").Question, StringComparison.Ordinal);
    }

    [Fact]
    public void Several_commits_name_the_count_the_stopped_commit_and_the_ones_not_picked()
    {
        const string Stopped = "078ef05000000000000000000000000000000000";
        const string Next = "b2c3d4e000000000000000000000000000000000";

        Assert.Equal(
            "Cherry-picked 1 of 2 commits. 078ef05 stopped with conflicts in 1 file.",
            OperationTexts.CherryPickSeveralStopped(1, 2, Stopped, 1, []));
        Assert.Equal(
            "Cherry-picked 0 of 3 commits. 078ef05 stopped with conflicts in 2 files; not picked: b2c3d4e \"Alpha two\", a1b2c3d \"Add core\".",
            OperationTexts.CherryPickSeveralStopped(0, 3, Stopped, 2, [(Next, "Alpha two"), (Sha, "Add core")]));
        Assert.Equal(
            "Reverted 1 of 3 commits. 078ef05 stopped with conflicts in 1 file; not reverted: b2c3d4e \"Alpha two\".",
            OperationTexts.RevertSeveralStopped(1, 3, Stopped, 1, [(Next, "Alpha two")]));
        Assert.Equal("Cherry-picked 2 of 3 commits. Git could not cherry-pick 078ef05.", OperationTexts.CherryPickSeveralFailed(2, 3, Stopped));
        Assert.Equal("Reverted 1 of 2 commits. Git could not revert 078ef05.", OperationTexts.RevertSeveralFailed(1, 2, Stopped));
    }

    [Fact]
    public void Special_failures_have_their_own_dialog_texts()
    {
        Assert.Equal("Checkout blocked", OperationTexts.CheckoutBlockedTitle);
        Assert.Equal("Your changes to these files would be overwritten by checking out feature/other.", OperationTexts.CheckoutBlockedByChanges("feature/other"));
        Assert.Equal(
            "Untracked files are in the way: feature/addfile has files with the same names. Move or delete them, then check out again.",
            OperationTexts.CheckoutBlockedByUntrackedFiles("feature/addfile"));
        Assert.Equal("origin has commits that your main does not have. Pull first, then push again.", OperationTexts.PushRejected("origin", "main"));
        Assert.Equal("origin/published has commits that your published does not include. Pull or look at them before forcing.", OperationTexts.ForcePushStopped("origin/published", "published"));
        Assert.Equal("published on origin has changed since you last fetched. Fetch and look at it before deleting.", OperationTexts.RemoteBranchChanged("published", "origin"));
        Assert.Equal("origin has no tag v1.0, or its v1.0 is not the same as yours.", OperationTexts.RemoteTagDiffers("origin", "v1.0"));
        Assert.Equal("a1b2c3d changes nothing on main: its changes are already there. Abort the cherry-pick to go on.", OperationTexts.CherryPickEmpty(Sha, "main"));
        Assert.Equal("a1b2c3d changes nothing on main: it is already undone.", OperationTexts.RevertEmpty(Sha, "main"));
        Assert.Equal("Git could not fetch from origin and mirror.", OperationTexts.FetchFailed(["origin", "mirror"]));
        Assert.Equal("Git could not fetch from a, b and c.", OperationTexts.FetchFailed(["a", "b", "c"]));
        Assert.Equal("pull.rebase is set to preserve, which this app does not understand.", OperationTexts.PullSettingNotUnderstood("pull.rebase", "preserve"));
        Assert.Equal(
            "Checked out feature/clash. Re-applying your changes stopped with conflicts in 1 file. Your changes are kept in the stash \"VisualCommit: before checking out feature/clash\".",
            OperationTexts.ReappliedWithConflicts("feature/clash", 1));
        Assert.Equal("Checked out feature/clash. Re-applying your changes stopped with conflicts in 1 file.", OperationTexts.ReappliedWithConflictsStatus("feature/clash", 1));
        Assert.Equal(
            "Checked out feature/clash, but your changes could not be re-applied. They are kept in the stash \"VisualCommit: before checking out feature/clash\".",
            OperationTexts.NotReapplied("feature/clash"));
        Assert.Equal("Fetch origin cancelled", OperationTexts.Cancelled("Fetch origin"));
    }

    [Fact]
    public void Confirmations_have_their_titles_questions_backup_detail_and_buttons()
    {
        var delete = OperationTexts.ConfirmDeleteBranch("feature/ff", "main");
        Assert.Equal(("Delete branch?", "Delete the branch feature/ff? It has commits that main does not have.", "A backup is saved first, so this can be undone.", "Delete", true),
            (delete.Title, delete.Question, delete.Detail, delete.ConfirmText, delete.IsDestructive));
        Assert.Equal("Delete the branch feature/ff?", OperationTexts.ConfirmDeleteBranch("feature/ff", null).Question);

        var force = OperationTexts.ConfirmForcePush("published", "origin");
        Assert.Equal("Force push published?", force.Title);
        Assert.Equal("Force push", force.ConfirmText);
        Assert.StartsWith("Replace published on origin with your published? Commits on origin that your branch does not have are dropped there.", force.Question, StringComparison.Ordinal);

        var remote = OperationTexts.ConfirmDeleteRemoteTag("v1.2", "origin");
        Assert.Equal(("Delete v1.2 on origin?", "Delete the tag v1.2 on origin? Everyone who uses origin loses it.", "Delete on origin"), (remote.Title, remote.Question, remote.ConfirmText));
        Assert.Equal("Drop stash@{0} \"On main: Experiment\"?", OperationTexts.ConfirmDropStash("stash@{0}", "On main: Experiment").Question);
        Assert.Equal("Delete the tag v1.0 here? It stays on remotes it was pushed to.", OperationTexts.ConfirmDeleteTag("v1.0").Question);

        var leave = OperationTexts.ConfirmLeaveDetachedCommits(1);
        Assert.Equal(("Leave these commits?", "Check out", false), (leave.Title, leave.ConfirmText, leave.IsDestructive));
    }

    // The Abort question of 2.15 and section 5 is the banner's (ConflictBannerViewModel.AbortConfirmation,
    // ConflictBannerTests): the one text the banner and RepositoryOperations.AbortAsync ask.
}
