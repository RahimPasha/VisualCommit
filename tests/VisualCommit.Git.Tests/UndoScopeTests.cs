using VisualCommit.Core.Git;
using VisualCommit.Core.Undo;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// What a recorded action names (docs/phase-5-design.md 7.6, 7.7): its scope's refs, prefixes and
/// configuration keys, its entry text, and the capture that collects what the reads cannot see,
/// which flows with the action's awaits and does nothing outside one. No git.
/// </summary>
public class UndoScopeTests
{
    [Fact]
    public void A_scope_covers_its_refs_exactly_and_its_prefixes_by_whole_components()
    {
        var scope = UndoScope.AddRemote("origin");
        Assert.True(scope.Covers("refs/remotes/origin/main"));
        Assert.True(scope.Covers("refs/remotes/origin/feature/x"));
        Assert.False(scope.Covers("refs/remotes/origin2/main"));
        Assert.False(scope.Covers("refs/remotes/origin"));

        var rebase = UndoScope.Rebase("topic", "main");
        Assert.True(rebase.Covers("refs/heads/anything"));
        Assert.False(rebase.Covers("refs/tags/v1"));

        var commit = UndoScope.Commit("Fix the parser", amend: false, "main");
        Assert.True(commit.Covers("refs/heads/main"));
        Assert.False(commit.Covers("refs/heads/main2"));
        Assert.True(commit.IsRecorded);
        Assert.False(UndoScope.None.IsRecorded);

        // A detached HEAD's name in texts is no branch: HEAD itself is always read.
        Assert.Empty(UndoScope.Reset("HEAD", new string('a', 40), ResetMode.Soft).Refs);
        Assert.Empty(UndoScope.CherryPick([new string('a', 40)], null).Refs);
        Assert.Equal(["refs/heads/topic", "refs/heads/main"], UndoScope.CreateBranch("topic", "main").Refs);
    }

    [Fact]
    public void Configuration_keys_are_covered_by_section_and_subsection()
    {
        Assert.Equal(("branch", "feature/X"), (new ConfigEntry("branch.feature/X.remote", "origin").Section, new ConfigEntry("branch.feature/X.remote", "origin").Subsection));
        Assert.Null(new ConfigEntry("remote.pushdefault", "origin").Subsection);

        var branch = ConfigScope.Branch("feat");
        Assert.True(branch.Covers(new ConfigEntry("branch.feat.remote", "origin")));
        Assert.False(branch.Covers(new ConfigEntry("branch.feat.x.remote", "origin")));
        Assert.False(branch.Covers(new ConfigEntry("branch.Feat.remote", "origin")));

        var remotes = UndoScope.RemoveRemote("origin");
        Assert.True(remotes.Covers(new ConfigEntry("remote.pushdefault", "origin")));
        Assert.True(remotes.Covers(new ConfigEntry("branch.main.merge", "refs/heads/main")));
        Assert.False(remotes.Covers(new ConfigEntry("core.bare", "false")));
    }

    [Fact]
    public void Entry_texts_name_the_action_as_the_user_knows_it()
    {
        var sha = new string('a', 40);
        Assert.Equal("commit \"Fix the parser\"", UndoScope.Commit("Fix the parser", amend: false, "main").Text);
        Assert.Equal("amend \"Fix the parser\"", UndoScope.Commit("Fix the parser", amend: true, "main").Text);
        Assert.Equal("checkout of origin/feature/x", UndoScope.Checkout("origin/feature/x", "main", "feature/x").Text);
        Assert.Equal("reset of main to aaaaaaa (hard)", UndoScope.Reset("main", sha, ResetMode.Hard).Text);
        Assert.Equal("cherry-pick of aaaaaaa", UndoScope.CherryPick([sha], "main").Text);
        Assert.Equal("revert of 3 commits", UndoScope.Revert([sha, sha, sha], "main").Text);
        Assert.Equal("squash of 2 commits", UndoScope.Squash(2, "main").Text);
        Assert.Equal("merge of feature/alpha into main", UndoScope.CheckoutAndMerge("main", "feature/alpha", "other").Text);
        Assert.Equal("discard of notes.txt", UndoScope.Discard(["docs/notes.txt"]).Text);
        Assert.Equal("discard of 2 files", UndoScope.Discard(["a", "b"]).Text);
        Assert.Equal("discard of lines in notes.txt", UndoScope.DiscardLines("docs/notes.txt").Text);
        Assert.Equal("pop of stash \"On main: wip\"", UndoScope.PopStash(sha, "On main: wip").Text);
        Assert.Equal("deletion of tag v1 on origin", UndoScope.DeleteRemoteTag("v1", "origin").Text);
        Assert.Equal("force push of topic to origin", UndoScope.Push("topic", "origin", force: true).Text);
        Assert.Equal(UndoKind.ForcePush, UndoScope.Push("topic", "origin", force: true).Kind);
        Assert.Equal("URL change of origin", UndoScope.SetRemoteUrl("origin").Text);
    }

    [Fact]
    public async Task A_capture_collects_from_the_actions_flow_and_its_pool_threads_and_nothing_outside_one()
    {
        // Outside a capture the call points do nothing.
        UndoCapture.Stash("ignored", includedUntracked: true, message: null);
        Assert.Null(UndoCapture.Current);

        UndoCapture capture;
        using (UndoCapture.Begin(out capture))
        {
            UndoCapture.Reset(ResetMode.Hard, "snapshot", snapshotIndex: "index");
            UndoCapture.IndexRestored("restored");
            await Task.Run(() => UndoCapture.Backups(["refs/visualcommit/backup/rebase-1/main"]), TestContext.Current.CancellationToken);
            UndoCapture.Backups(["refs/visualcommit/backup/rebase-1/main", "refs/visualcommit/backup/reset-1"]);
            UndoCapture.Push("origin", new PushOutcome([new PushedRef("refs/heads/topic", null, "new")]));
            UndoCapture.FilesChanged(["a.txt"]);
            UndoCapture.FilesChanged(["a.txt", "b.txt"]);
            Assert.Same(capture, UndoCapture.Current);
        }

        Assert.Null(UndoCapture.Current);
        var captured = capture.Captured;
        Assert.Equal((ResetMode.Hard, "snapshot", "index", "restored"), (captured.ResetMode, captured.ResetSnapshot, captured.ResetIndex, captured.IndexSource));
        Assert.Null(captured.Snapshot);
        Assert.Equal(["refs/visualcommit/backup/rebase-1/main", "refs/visualcommit/backup/reset-1"], captured.Backups);
        Assert.Equal("origin", captured.Remote);
        Assert.Equal(["a.txt", "b.txt"], captured.ChangedFiles);
        Assert.Null(captured.Stash);
    }

    [Fact]
    public void A_capture_inside_another_gives_the_flow_back_the_outer_one()
    {
        using (UndoCapture.Begin(out var outer))
        {
            using (UndoCapture.Begin(out var inner))
            {
                UndoCapture.Stash("inner", includedUntracked: false, message: "m");
                Assert.Equal("inner", inner.Captured.Stash);
            }

            Assert.Same(outer, UndoCapture.Current);
            Assert.Null(outer.Captured.Stash);
        }
    }
}
