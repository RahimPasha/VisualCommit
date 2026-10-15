using VisualCommit.Core.Git;
using VisualCommit.Core.Undo;
using Xunit;
using static VisualCommit.Core.Undo.UndoEffect;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Each kind's undo and redo sequences (docs/phase-5-design.md 7.3) as <see cref="UndoSequence"/>
/// makes them from an entry's recorded states: the effects, their order and the values their checks
/// compare. No git: the git layer's run of them is tested on real repositories.
/// </summary>
public class UndoSequenceTests
{
    private static readonly string O = new('a', 40);
    private static readonly string N = new('b', 40);
    private static readonly string T = new('c', 40);

    [Fact]
    public void A_commit_moves_its_branch_back_softly_and_the_initial_commit_deletes_it()
    {
        var entry = Entry(UndoKind.Commit, Head("main", O), Head("main", N));
        Assert.Equal([new MoveHead("main", N, O, MoveMode.Soft)], Undo(entry));
        Assert.Equal([new MoveHead("main", O, N, MoveMode.Soft)], Redo(entry));

        var initial = Entry(UndoKind.Commit, Head("main", null), Head("main", N));
        Assert.Equal([new MoveHead("main", N, null, MoveMode.Soft)], Undo(initial));
        Assert.Equal([new MoveHead("main", null, N, MoveMode.Soft)], Redo(initial));

        var detached = Entry(UndoKind.Amend, Head(null, O), Head(null, N));
        Assert.Equal([new MoveDetachedHead(N, O, MoveMode.Soft)], Undo(detached));
    }

    [Fact]
    public void Resets_follow_their_mode()
    {
        var soft = Entry(UndoKind.Reset, Head("main", O), Head("main", N), captured: new UndoCaptured { ResetMode = ResetMode.Soft });
        Assert.Equal([new MoveHead("main", N, O, MoveMode.Soft)], Undo(soft));

        // Mixed: the index from its snapshot, or from the commit moved back to when none was saved.
        var mixed = Entry(UndoKind.Reset, Head("main", O), Head("main", N), captured: new UndoCaptured { ResetMode = ResetMode.Mixed, ResetSnapshot = T });
        Assert.Equal([new MoveHead("main", N, O, MoveMode.Soft), new IndexTo(T, N)], Undo(mixed));
        Assert.Equal([new IndexTo(N, T), new MoveHead("main", O, N, MoveMode.Soft)], Redo(mixed));
        var clean = mixed with { Captured = new UndoCaptured { ResetMode = ResetMode.Mixed } };
        Assert.Equal([new MoveHead("main", N, O, MoveMode.Soft), new IndexTo(O, N)], Undo(clean));

        // Hard: the move keeping files, the index, then the snapshot's files; the redo resets again.
        // The Keep move leaves the index at O, which the index's check compares with.
        var snapshot = new DiscardSnapshot("refs/visualcommit/backup/reset-1-changes", T, ["a.txt"], new HashSet<string> { "a.txt" });
        var after = new UndoState { Head = Head("main", N), Files = [new RecordedFile("a.txt", "blob", "hash"), new RecordedFile("other.txt", "blob2", "hash2")] };
        var hard = new UndoEntry(UndoKind.Reset, "reset", "op-1", new UndoState { Head = Head("main", O) }, after, new UndoCaptured { ResetMode = ResetMode.Hard, ResetSnapshot = T, ResetIndex = "index", Snapshot = snapshot });
        var undo = Undo(hard);
        Assert.Equal(3, undo.Count);
        Assert.Equal(new MoveHead("main", N, O, MoveMode.Keep), undo[0]);
        Assert.Equal(new IndexTo("index", O), undo[1]);
        var restore = Assert.IsType<RestoreSnapshot>(undo[2]);
        Assert.Same(snapshot, restore.Snapshot);
        Assert.Equal([new RecordedFile("a.txt", "blob", "hash")], restore.From);
        Assert.Equal([new HardReset("main", O, N)], Redo(hard));

        // An index with conflicts has no snapshot of its own: the files alone come back after the move.
        var conflicted = hard with { Captured = hard.Captured with { ResetIndex = null } };
        Assert.Equal([typeof(MoveHead), typeof(RestoreSnapshot)], Undo(conflicted).Select(effect => effect.GetType()));

        // A hard reset that discarded nothing saved no snapshot: the move alone.
        var nothingDiscarded = hard with { Captured = new UndoCaptured { ResetMode = ResetMode.Hard } };
        Assert.Equal([new MoveHead("main", N, O, MoveMode.Keep)], Undo(nothingDiscarded));
    }

    [Fact]
    public void A_hard_reset_with_a_snapshot_whose_files_were_not_recorded_has_no_undo()
    {
        // Without the snapshot's files the undo would move the branch back and leave the
        // discarded work lost, without a word.
        var hard = Entry(UndoKind.Reset, Head("main", O), Head("main", N), captured: new UndoCaptured { ResetMode = ResetMode.Hard, ResetSnapshot = T, ResetIndex = "index" });
        Assert.Throws<InvalidOperationException>(() => Undo(hard));

        var other = new DiscardSnapshot("refs/visualcommit/backup/discard-1", N, ["a.txt"], new HashSet<string>());
        Assert.Throws<InvalidOperationException>(() => Undo(hard with { Captured = hard.Captured with { Snapshot = other } }));

        // Its redo resets again and needs nothing recorded of the snapshot.
        Assert.Equal([new HardReset("main", O, N)], Redo(hard));
    }

    [Fact]
    public void A_merge_moves_its_branch_back_keeping_work_and_a_rebase_sets_back_the_other_branches_it_moved()
    {
        var merge = Entry(UndoKind.Merge, Head("main", O), Head("main", N));
        Assert.Equal([new MoveHead("main", N, O, MoveMode.Keep)], Undo(merge));
        Assert.Equal([new MoveHead("main", O, N, MoveMode.Keep)], Redo(merge));

        // A user's rebase.updateRefs moved feature/b too (7.7 step 1).
        var rebase = Entry(
            UndoKind.Rebase,
            Head("topic", O),
            Head("topic", N),
            beforeRefs: new() { ["refs/heads/topic"] = O, ["refs/heads/feature/b"] = O, ["refs/heads/main"] = T },
            afterRefs: new() { ["refs/heads/topic"] = N, ["refs/heads/feature/b"] = N, ["refs/heads/main"] = T });
        Assert.Equal([new MoveHead("topic", N, O, MoveMode.Keep), new SetRef("refs/heads/feature/b", N, O)], Undo(rebase));
        Assert.Equal([new SetRef("refs/heads/feature/b", O, N), new MoveHead("topic", O, N, MoveMode.Keep)], Redo(rebase));
    }

    [Fact]
    public void Check_out_and_merge_moves_the_branch_back_while_on_it_then_switches_back()
    {
        var entry = Entry(
            UndoKind.CheckoutAndMerge,
            Head("main", T),
            Head("feature", N),
            beforeRefs: new() { ["refs/heads/feature"] = O, ["refs/heads/main"] = T },
            afterRefs: new() { ["refs/heads/feature"] = N, ["refs/heads/main"] = T });
        Assert.Equal([new MoveHead("feature", N, O, MoveMode.Keep), new Switch(Head("feature", O), Head("main", T))], Undo(entry));
        Assert.Equal([new Switch(Head("main", T), Head("feature", O)), new MoveHead("feature", O, N, MoveMode.Keep)], Redo(entry));

        // The merge failed after the checkout: the checkout alone is recorded (7.2).
        var failed = Entry(
            UndoKind.CheckoutAndMerge,
            Head("main", T),
            Head("feature", O),
            beforeRefs: new() { ["refs/heads/feature"] = O },
            afterRefs: new() { ["refs/heads/feature"] = O });
        Assert.Equal([new Switch(Head("feature", O), Head("main", T))], Undo(failed));
    }

    [Fact]
    public void A_checkout_switches_back_and_deletes_a_tracking_branch_it_created_with_its_keys()
    {
        var plain = Entry(UndoKind.Checkout, Head("main", O), Head(null, T));
        Assert.Equal([new Switch(Head(null, T), Head("main", O))], Undo(plain));
        Assert.Equal([new Switch(Head("main", O), Head(null, T))], Redo(plain));

        IReadOnlyList<ConfigEntry> keys = [new("branch.feature/x.remote", "origin"), new("branch.feature/x.merge", "refs/heads/feature/x")];
        var tracking = new UndoEntry(
            UndoKind.Checkout,
            "checkout of origin/feature/x",
            "op-1",
            new UndoState { Head = Head("main", O), Refs = Refs(new() { ["refs/heads/main"] = O }) },
            new UndoState { Head = Head("feature/x", T), Refs = Refs(new() { ["refs/heads/main"] = O, ["refs/heads/feature/x"] = T }), Config = keys },
            UndoCaptured.None);
        Assert.Equal(
            [new Switch(Head("feature/x", T), Head("main", O)), new SetRef("refs/heads/feature/x", T, null), new BranchConfig("feature/x", keys, [])],
            Undo(tracking),
            EffectComparer.Instance);
        Assert.Equal(
            [new SetRef("refs/heads/feature/x", null, T), new BranchConfig("feature/x", [], keys), new Switch(Head("main", O), Head("feature/x", T))],
            Redo(tracking),
            EffectComparer.Instance);

        // "Create branch" without its checkout: the branch alone.
        var created = tracking with { Kind = UndoKind.CreateBranch, After = tracking.After with { Head = Head("main", O) } };
        Assert.Equal([new SetRef("refs/heads/feature/x", T, null), new BranchConfig("feature/x", keys, [])], Undo(created), EffectComparer.Instance);
    }

    [Fact]
    public void Branches_are_renamed_back_brought_back_with_a_reflog_and_set_back()
    {
        var rename = Entry(UndoKind.RenameBranch, Head("main", O), Head("main", O), beforeRefs: new() { ["refs/heads/old"] = T }, afterRefs: new() { ["refs/heads/new"] = T });
        Assert.Equal([new UndoEffect.RenameBranch("new", "old")], Undo(rename));
        Assert.Equal([new UndoEffect.RenameBranch("old", "new")], Redo(rename));

        var delete = Entry(UndoKind.DeleteBranch, Head("main", O), Head("main", O), beforeRefs: new() { ["refs/heads/gone"] = T }, afterRefs: new());
        var undo = Undo(delete);
        Assert.Equal(new SetRef("refs/heads/gone", null, T) { CreateReflog = true }, undo[0]);
        Assert.Equal(new BranchConfig("gone", [], []), undo[1], EffectComparer.Instance);

        var fastForward = Entry(UndoKind.FastForwardBranch, Head("main", O), Head("main", O), beforeRefs: new() { ["refs/heads/side"] = O }, afterRefs: new() { ["refs/heads/side"] = N });
        Assert.Equal([new SetRef("refs/heads/side", N, O)], Undo(fastForward));
        Assert.Equal([new SetRef("refs/heads/side", O, N)], Redo(fastForward));
    }

    [Fact]
    public void Stashes_are_popped_stored_and_dropped_by_their_commit()
    {
        var stash = new UndoEntry(
            UndoKind.Stash,
            "stash \"wip\"",
            "op-1",
            new UndoState { Head = Head("main", O), Status = "before" },
            new UndoState { Head = Head("main", O), Status = "after" },
            new UndoCaptured { Stash = T, StashIncludedUntracked = true, StashMessage = "wip" });
        Assert.Equal([new StashPop(T, "after") { Onto = Head("main", O) }], Undo(stash));
        Assert.Equal([new StashPush(true, "wip", "before")], Redo(stash));

        var listed = new UndoStash(T, "On main: wip");
        var drop = new UndoEntry(UndoKind.DropStash, "drop", "op-1", new UndoState { Head = Head("main", O), Stashes = [listed] }, new UndoState { Head = Head("main", O) }, UndoCaptured.None);
        Assert.Equal([new StashStore(T, "On main: wip")], Undo(drop));
        Assert.Equal([new StashDrop(T)], Redo(drop));

        // A pop stores its stash first and drops it last; one that kept its stash is an apply.
        var files = new UndoState { Head = Head("main", O), Stashes = [listed], Files = [new RecordedFile("a.txt", null, null)], IndexEntries = ["before"] };
        var popped = new UndoState { Head = Head("main", O), Files = [new RecordedFile("a.txt", "blob", "hash")], IndexEntries = ["after"] };
        var pop = new UndoEntry(UndoKind.PopStash, "pop", "op-1", files, popped, UndoCaptured.None);
        var undo = Undo(pop);
        Assert.Equal([typeof(StashStore), typeof(IndexEntries), typeof(FilesTo)], undo.Select(effect => effect.GetType()));
        Assert.Equal(["after"], ((IndexEntries)undo[1]).From);
        Assert.Equal(["before"], ((IndexEntries)undo[1]).To);
        Assert.Equal([typeof(IndexEntries), typeof(FilesTo), typeof(StashDrop)], Redo(pop).Select(effect => effect.GetType()));
        var kept = pop with { After = popped with { Stashes = [listed] } };
        Assert.Equal([typeof(IndexEntries), typeof(FilesTo)], Undo(kept).Select(effect => effect.GetType()));
    }

    [Fact]
    public void The_automatic_stash_switches_back_and_pops_and_its_redo_stashes_again_first()
    {
        var entry = new UndoEntry(
            UndoKind.AutoStashCheckout,
            "checkout of a1b2c3d",
            "op-1",
            new UndoState { Head = Head("main", O), Status = "dirty" },
            new UndoState { Head = Head(null, T), Status = "clean" },
            new UndoCaptured { Stash = N, StashMessage = "Auto stash before checking out a1b2c3d" });
        Assert.Equal([new Switch(Head(null, T), Head("main", O)), new StashPop(N, "clean") { Onto = Head("main", O) }], Undo(entry));
        Assert.Equal([new StashPush(true, "Auto stash before checking out a1b2c3d", "dirty"), new Switch(Head("main", O), Head(null, T))], Redo(entry));
    }

    [Fact]
    public void A_stash_is_popped_only_onto_the_commit_it_was_made_on()
    {
        // A commit made since leaves the status as it was: only HEAD's commit tells (0.5's conflicts).
        var stash = new UndoEntry(
            UndoKind.Stash,
            "stash \"wip\"",
            "op-1",
            new UndoState { Head = Head("main", O), Status = "1 .M x\0" },
            new UndoState { Head = Head("main", O), Status = string.Empty },
            new UndoCaptured { Stash = T });
        var listed = new UndoState { Head = Head("main", O), Stashes = [new UndoStash(T, "On main: wip")], Status = string.Empty };
        Assert.Null(UndoChecks.FirstRefusal(Step(stash, UndoDirection.Undo), listed));
        Assert.Equal(
            new UndoRefusal(UndoRefusalKind.BranchMoved, Name: "main", Sha: N),
            UndoChecks.FirstRefusal(Step(stash, UndoDirection.Undo), listed with { Head = Head("main", N) }));
        Assert.Equal(
            new UndoRefusal(UndoRefusalKind.HeadNotAtCommit, Sha: O),
            UndoChecks.FirstRefusal(Step(stash, UndoDirection.Undo), listed with { Head = Head(null, N) }));

        // Another branch at the same commit takes the stash as it was made.
        Assert.Null(UndoChecks.FirstRefusal(Step(stash, UndoDirection.Undo), listed with { Head = Head("other", O) }));

        // The automatic stash's undo pops after its switch back: main must be where it was, in memory too.
        var auto = new UndoEntry(
            UndoKind.AutoStashCheckout,
            "checkout of a1b2c3d",
            "op-1",
            new UndoState { Head = Head("main", O), Status = "1 .M x\0" },
            new UndoState { Head = Head(null, T), Status = string.Empty },
            new UndoCaptured { Stash = N });
        var moved = new UndoState { Head = Head(null, T), Refs = Refs(new() { ["refs/heads/main"] = T }), Stashes = [new UndoStash(N, "On main: auto")], Status = string.Empty };
        Assert.Equal(new UndoRefusal(UndoRefusalKind.BranchMoved, Name: "main", Sha: T), UndoChecks.FirstRefusal(Step(auto, UndoDirection.Undo), moved));
        Assert.Null(UndoChecks.FirstRefusal(Step(auto, UndoDirection.Undo), moved with { Refs = Refs(new() { ["refs/heads/main"] = O }) }));
        var refs = new RepoRefs(Head(null, T), [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, T)], [new StashEntry(0, N, O, "On main: auto", "A", "a@example.com", default, default)], []);
        Assert.Equal(new UndoRefusal(UndoRefusalKind.BranchMoved, Name: "main", Sha: T), UndoChecks.FirstRefusalInMemory(Step(auto, UndoDirection.Undo), refs, WorkingTreeStatus.Clean, []));

        // D91's pops land where its switch goes: the undo's on main, the redo's on topic.
        var d91 = new UndoEntry(
            UndoKind.StashAndCheckout,
            "checkout of topic",
            "op-1",
            new UndoState { Head = Head("main", O), Refs = Refs(new() { ["refs/heads/main"] = O, ["refs/heads/topic"] = T }), Status = "1 .M x\0" },
            new UndoState { Head = Head("topic", T), Refs = Refs(new() { ["refs/heads/main"] = O, ["refs/heads/topic"] = T }), Status = "1 .M x\0" },
            UndoCaptured.None);
        Assert.Equal(Head("main", O), Assert.IsType<StashPop>(Undo(d91)[^1]).Onto);
        Assert.Equal(Head("topic", T), Assert.IsType<StashPop>(Redo(d91)[^1]).Onto);
        var onTopic = Now(Head("topic", T), new() { ["refs/heads/main"] = N, ["refs/heads/topic"] = T }) with { Status = "1 .M x\0" };
        Assert.Equal(new UndoRefusal(UndoRefusalKind.BranchMoved, Name: "main", Sha: N), UndoChecks.FirstRefusal(Step(d91, UndoDirection.Undo), onTopic));
    }

    [Fact]
    public void Tags_keep_their_object_and_a_created_ones_undo_backs_it_up_first()
    {
        var created = Entry(UndoKind.CreateTag, Head("main", O), Head("main", O), beforeRefs: new(), afterRefs: new() { ["refs/tags/v1"] = T });
        Assert.Equal([new KeepTag("v1", T), new TagRef("v1", T, Present: false)], Undo(created));
        Assert.Equal([new TagRef("v1", T, Present: true)], Redo(created));

        var deleted = Entry(UndoKind.DeleteTag, Head("main", O), Head("main", O), beforeRefs: new() { ["refs/tags/v1"] = T }, afterRefs: new());
        Assert.Equal([new TagRef("v1", T, Present: true)], Undo(deleted));
        Assert.Equal([new TagRef("v1", T, Present: false)], Redo(deleted));
    }

    [Fact]
    public void A_push_is_set_back_on_the_remote_with_a_lease_first_then_its_upstream_keys()
    {
        IReadOnlyList<ConfigEntry> upstream = [new("branch.topic.remote", "origin"), new("branch.topic.merge", "refs/heads/topic")];
        var push = new UndoEntry(
            UndoKind.Push,
            "push of topic to origin",
            "op-1",
            new UndoState { Head = Head("topic", N) },
            new UndoState { Head = Head("topic", N), Config = upstream },
            new UndoCaptured { Remote = "origin", Push = new PushOutcome([new PushedRef("refs/heads/topic", null, N)]) });
        Assert.Equal(
            [new RemoteRef("origin", "refs/heads/topic", N, null), new BranchConfig("topic", upstream, [])],
            Undo(push),
            EffectComparer.Instance);
        Assert.Equal(
            [new RemoteRef("origin", "refs/heads/topic", null, N), new BranchConfig("topic", [], upstream)],
            Redo(push),
            EffectComparer.Instance);
        Assert.True(UndoStep.For(push, UndoDirection.Undo).ChangesRemote);
    }

    [Fact]
    public void A_removed_remote_comes_back_with_its_keys_first_then_its_branches()
    {
        IReadOnlyList<ConfigEntry> remote = [new("remote.origin.url", "https://example.com/r.git"), new("remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*"), new("remote.pushdefault", "origin")];
        IReadOnlyList<ConfigEntry> branch = [new("branch.main.remote", "origin"), new("branch.main.merge", "refs/heads/main")];
        var entry = new UndoEntry(
            UndoKind.RemoveRemote,
            "removal of remote origin",
            "op-1",
            new UndoState { Head = Head("main", O), Refs = Refs(new() { ["refs/remotes/origin/main"] = O }), Config = [.. remote, .. branch] },
            new UndoState { Head = Head("main", O) },
            UndoCaptured.None);
        Assert.Equal(
            [new RemoteConfig("origin", [], remote), new BranchConfig("main", [], branch), new SetRef("refs/remotes/origin/main", null, O)],
            Undo(entry),
            EffectComparer.Instance);
        Assert.Equal(
            [new SetRef("refs/remotes/origin/main", O, null), new BranchConfig("main", branch, []), new RemoteConfig("origin", remote, [])],
            Redo(entry),
            EffectComparer.Instance);
    }

    [Fact]
    public void The_backups_lists_restores_are_undone_by_their_kinds()
    {
        // "Restore stash": the stash stored again is dropped; the redo stores it again.
        var listed = new UndoStash(T, "On main: wip");
        var stash = new UndoEntry(UndoKind.RestoreStash, "restore of stash \"On main: wip\"", "op-1", new UndoState { Head = Head("main", O) }, new UndoState { Head = Head("main", O), Stashes = [listed] }, UndoCaptured.None);
        Assert.Equal([new StashDrop(T)], Undo(stash));
        Assert.Equal([new StashStore(T, "On main: wip")], Redo(stash));

        // "Restore the staged changes…": the index goes back to HEAD's commit, and to the snapshot again.
        var staged = Entry(UndoKind.RestoreStagedChanges, Head("main", O), Head("main", O), captured: new UndoCaptured { IndexSource = T });
        Assert.Equal([new IndexTo(O, T)], Undo(staged));
        Assert.Equal([new IndexTo(T, O)], Redo(staged));
        Assert.Throws<InvalidOperationException>(() => Undo(staged with { Captured = UndoCaptured.None }));

        // "Restore the changes…" that put a hard reset's index back first: the files, then the index.
        var snapshot = new DiscardSnapshot("refs/visualcommit/backup/reset-1-changes", T, ["a.txt"], new HashSet<string> { "a.txt" });
        IReadOnlyList<RecordedFile> before = [new RecordedFile("a.txt", null, null)];
        IReadOnlyList<RecordedFile> after = [new RecordedFile("a.txt", "blob", "hash")];
        var changes = new UndoEntry(
            UndoKind.Restore,
            "restore of the changes",
            "op-1",
            new UndoState { Head = Head("main", O), Files = before },
            new UndoState { Head = Head("main", O), Files = after },
            new UndoCaptured { Snapshot = snapshot, IndexSource = N });
        Assert.Equal([new FilesTo(after, before), new IndexTo(O, N)], Undo(changes));
        Assert.Equal([new IndexTo(N, O), new RestoreSnapshot(snapshot, before)], Redo(changes));

        // The restore bar's Restore puts back no index.
        var bar = changes with { Captured = new UndoCaptured { Snapshot = snapshot } };
        Assert.Equal([new FilesTo(after, before)], Undo(bar));
        Assert.Equal([new RestoreSnapshot(snapshot, before)], Redo(bar));

        // The others are recorded as the kinds that undo them, with texts of their own.
        Assert.Equal((UndoKind.CreateBranch, "restore of branch feature/x"), (UndoScope.RestoreBranch("feature/x").Kind, UndoScope.RestoreBranch("feature/x").Text));
        Assert.Equal((UndoKind.CreateTag, "restore of tag v1"), (UndoScope.RestoreTag("v1").Kind, UndoScope.RestoreTag("v1").Text));
        Assert.Equal((UndoKind.Restore, "restore of the discarded files"), (UndoScope.RestoreChanges(["a.txt"], discarded: true).Kind, UndoScope.RestoreChanges(["a.txt"], discarded: true).Text));
        Assert.Equal("restore of the changes", UndoScope.RestoreChanges(["a.txt"], discarded: false).Text);
        Assert.Equal((UndoKind.Push, "push of topic back to origin"), (UndoScope.PushBack("topic", "origin").Kind, UndoScope.PushBack("topic", "origin").Text));
        Assert.Equal((UndoKind.PushTag, "push of tag v1 back to origin"), (UndoScope.PushTagBack("v1", "origin").Kind, UndoScope.PushTagBack("v1", "origin").Text));
    }

    [Fact]
    public void A_pending_entry_or_one_without_its_values_has_no_sequence()
    {
        var pending = Entry(UndoKind.Merge, Head("main", O), Head("main", N)) with { State = UndoEntryState.Pending };
        Assert.Throws<InvalidOperationException>(() => Undo(pending));

        var reset = Entry(UndoKind.Reset, Head("main", O), Head("main", N));
        Assert.Throws<InvalidOperationException>(() => Undo(reset));
    }

    // The simulation of 7.5 step 3 (UndoChecks): each check against the state the effects before it leave.

    [Fact]
    public void A_move_is_refused_when_HEAD_left_its_branch_or_the_branch_moved()
    {
        var commit = Step(Entry(UndoKind.Commit, Head("main", O), Head("main", N)), UndoDirection.Undo);
        Assert.Null(UndoChecks.FirstRefusal(commit, Now(Head("main", N), new() { ["refs/heads/main"] = N })));
        Assert.Equal(
            new UndoRefusal(UndoRefusalKind.BranchMoved, Name: "main", Sha: T),
            UndoChecks.FirstRefusal(commit, Now(Head("main", T), new() { ["refs/heads/main"] = T })));
        Assert.Equal(
            new UndoRefusal(UndoRefusalKind.HeadNotOnBranch, Name: "main"),
            UndoChecks.FirstRefusal(commit, Now(Head("other", N), new() { ["refs/heads/main"] = N, ["refs/heads/other"] = N })));

        var detached = Step(Entry(UndoKind.Commit, Head(null, O), Head(null, N)), UndoDirection.Undo);
        Assert.Equal(new UndoRefusal(UndoRefusalKind.HeadNotAtCommit, Sha: N), UndoChecks.FirstRefusal(detached, Now(Head(null, T), new())));
    }

    [Fact]
    public void A_branch_the_step_deletes_after_switching_away_from_it_is_no_longer_checked_out_but_one_in_another_worktree_is()
    {
        IReadOnlyList<ConfigEntry> keys = [new("branch.topic.remote", "origin"), new("branch.topic.merge", "refs/heads/topic")];
        var tracking = new UndoEntry(
            UndoKind.Checkout,
            "checkout of origin/topic",
            "op-1",
            new UndoState { Head = Head("main", O), Refs = Refs(new() { ["refs/heads/main"] = O }) },
            new UndoState { Head = Head("topic", T), Refs = Refs(new() { ["refs/heads/main"] = O, ["refs/heads/topic"] = T }), Config = keys },
            UndoCaptured.None);
        var undo = Step(tracking, UndoDirection.Undo);

        // HEAD is on topic here: the switch away comes first, so the delete finds it checked out nowhere.
        var here = new UndoState
        {
            Head = Head("topic", T),
            Refs = Refs(new() { ["refs/heads/main"] = O, ["refs/heads/topic"] = T }),
            CheckedOutIn = new Dictionary<string, string> { ["refs/heads/topic"] = "/repo" },
            Config = keys,
        };
        Assert.Null(UndoChecks.FirstRefusal(undo, here));

        // Its redo creates topic again: refused while a branch of that name exists again.
        var redo = Step(tracking, UndoDirection.Redo);
        var back = here with { Head = Head("main", O), Config = [], CheckedOutIn = new Dictionary<string, string>() };
        Assert.Null(UndoChecks.FirstRefusal(redo, back with { Refs = Refs(new() { ["refs/heads/main"] = O }) }));
        Assert.Equal(new UndoRefusal(UndoRefusalKind.BranchExists, Name: "topic"), UndoChecks.FirstRefusal(redo, back));

        // A fast-forward's undo of a branch another worktree has checked out: update-ref would move it under that worktree's files.
        var fastForward = Step(Entry(UndoKind.FastForwardBranch, Head("main", O), Head("main", O), beforeRefs: new() { ["refs/heads/side"] = O }, afterRefs: new() { ["refs/heads/side"] = N }), UndoDirection.Undo);
        var elsewhere = Now(Head("main", O), new() { ["refs/heads/side"] = N }) with { CheckedOutIn = new Dictionary<string, string> { ["refs/heads/side"] = "/worktrees/side" } };
        Assert.Equal(
            new UndoRefusal(UndoRefusalKind.CheckedOutElsewhere, Name: "side", Paths: ["/worktrees/side"]),
            UndoChecks.FirstRefusal(fastForward, elsewhere),
            RefusalComparer.Instance);
    }

    [Fact]
    public void A_stash_step_compares_the_whole_status_and_pops_only_what_its_own_stash_saved()
    {
        // D91's "Stash changes and check out" undone: stash (tracked files only), switch back, pop.
        var entry = new UndoEntry(
            UndoKind.StashAndCheckout,
            "checkout of topic",
            "op-1",
            new UndoState { Head = Head("main", O), Refs = Refs(new() { ["refs/heads/main"] = O, ["refs/heads/topic"] = T }), Status = "1 .M x\0" },
            new UndoState { Head = Head("topic", T), Refs = Refs(new() { ["refs/heads/main"] = O, ["refs/heads/topic"] = T }), Status = "1 .M x\0? u\0" },
            UndoCaptured.None);
        var undo = Step(entry, UndoDirection.Undo);
        var now = Now(Head("topic", T), new() { ["refs/heads/main"] = O, ["refs/heads/topic"] = T }) with { Status = "1 .M x\0? u\0" };
        Assert.Null(UndoChecks.FirstRefusal(undo, now));
        Assert.Equal(new UndoRefusal(UndoRefusalKind.StatusChanged), UndoChecks.FirstRefusal(undo, now with { Status = "1 .M x\0? u\0? v\0" }));

        // Only untracked files left: the stash saves nothing and the pop pops nothing.
        Assert.Null(UndoChecks.FirstRefusal(Step(entry with { After = entry.After with { Status = "? u\0" } }, UndoDirection.Undo), now with { Status = "? u\0" }));

        Assert.Equal("? u\0? w\0", UndoChecks.UntrackedOnly("1 .M N... 100644 100644 100644 a b x\0? u\0" + "2 R. N... 100644 100644 100644 a b R100 y\0old y\0? w\0"));
    }

    [Fact]
    public void Tags_branches_and_stashes_back_in_the_way_refuse()
    {
        var created = Entry(UndoKind.CreateTag, Head("main", O), Head("main", O), beforeRefs: new(), afterRefs: new() { ["refs/tags/v1"] = T });
        Assert.Equal(
            new UndoRefusal(UndoRefusalKind.TagExists, Name: "v1"),
            UndoChecks.FirstRefusal(Step(created, UndoDirection.Redo), Now(Head("main", O), new() { ["refs/tags/v1"] = N })));
        Assert.Equal(
            new UndoRefusal(UndoRefusalKind.RefChanged, Name: "refs/tags/v1"),
            UndoChecks.FirstRefusal(Step(created, UndoDirection.Undo), Now(Head("main", O), new() { ["refs/tags/v1"] = N })));

        var listed = new UndoStash(T, "On main: wip");
        var drop = new UndoEntry(UndoKind.DropStash, "drop", "op-1", new UndoState { Head = Head("main", O), Stashes = [listed] }, new UndoState { Head = Head("main", O) }, UndoCaptured.None);
        Assert.Equal(new UndoRefusal(UndoRefusalKind.StashListed), UndoChecks.FirstRefusal(Step(drop, UndoDirection.Undo), new UndoState { Head = Head("main", O), Stashes = [listed] }));
        Assert.Equal(new UndoRefusal(UndoRefusalKind.StashGone), UndoChecks.FirstRefusal(Step(drop, UndoDirection.Redo), new UndoState { Head = Head("main", O) }));

        var rename = Entry(UndoKind.RenameBranch, Head("main", O), Head("main", O), beforeRefs: new() { ["refs/heads/old"] = T }, afterRefs: new() { ["refs/heads/new"] = T });
        Assert.Equal(
            new UndoRefusal(UndoRefusalKind.BranchExists, Name: "old"),
            UndoChecks.FirstRefusal(Step(rename, UndoDirection.Undo), Now(Head("main", O), new() { ["refs/heads/new"] = T, ["refs/heads/old"] = O })));
    }

    [Fact]
    public void The_toolbars_checks_use_the_refs_and_the_staged_files_and_leave_the_rest_to_the_step()
    {
        var mixed = Entry(UndoKind.Reset, Head("main", O), Head("main", N), captured: new UndoCaptured { ResetMode = ResetMode.Mixed, ResetSnapshot = T });
        var undo = Step(mixed, UndoDirection.Undo);
        var refs = new RepoRefs(Head("main", N), [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, N, IsHead: true)], [], []);
        Assert.Null(UndoChecks.FirstRefusalInMemory(undo, refs, WorkingTreeStatus.Clean, []));

        // Something staged since the reset: its index check refuses already.
        var staged = WorkingTreeStatus.Clean with { Staged = [new ChangedFile("a.txt", FileChangeKind.Modified)] };
        Assert.Equal(new UndoRefusal(UndoRefusalKind.IndexChanged), UndoChecks.FirstRefusalInMemory(undo, refs, staged, []));

        // A branch another worktree has checked out.
        var fastForward = Step(Entry(UndoKind.FastForwardBranch, Head("main", N), Head("main", N), beforeRefs: new() { ["refs/heads/side"] = O }, afterRefs: new() { ["refs/heads/side"] = N }), UndoDirection.Undo);
        var withSide = refs with { Refs = [.. refs.Refs, new GitRef("refs/heads/side", "side", RefKind.LocalBranch, N)] };
        Assert.Null(UndoChecks.FirstRefusalInMemory(fastForward, withSide, WorkingTreeStatus.Clean, []));
        Assert.Equal(
            new UndoRefusal(UndoRefusalKind.CheckedOutElsewhere, Name: "side", Paths: ["/wt"]),
            UndoChecks.FirstRefusalInMemory(fastForward, withSide, WorkingTreeStatus.Clean, [new WorktreeInfo("/wt", "refs/heads/side", IsMain: false)]),
            RefusalComparer.Instance);
    }

    [Fact]
    public void A_step_reads_what_its_effects_check()
    {
        var snapshot = new DiscardSnapshot("refs/visualcommit/backup/discard-1", T, ["a.txt", "b.txt"], new HashSet<string> { "a.txt" });
        var discard = new UndoEntry(
            UndoKind.Discard,
            "discard of 2 files",
            "op-1",
            new UndoState { Head = Head("main", O), Files = [new RecordedFile("a.txt", "x", "X"), new RecordedFile("b.txt", null, null)] },
            new UndoState { Head = Head("main", O), Files = [new RecordedFile("a.txt", null, null), new RecordedFile("b.txt", null, null)] },
            new UndoCaptured { Snapshot = snapshot }) { Scope = UndoScope.Discard(["a.txt", "b.txt"]) };
        var scope = UndoChecks.ScopeOf(Step(discard, UndoDirection.Undo));
        Assert.Equal(["a.txt", "b.txt"], scope.Files);
        Assert.False(scope.Status);

        var stash = Step(new UndoEntry(UndoKind.Stash, "stash", "op-1", new UndoState { Head = Head("main", O) }, new UndoState { Head = Head("main", O) }, new UndoCaptured { Stash = T }), UndoDirection.Undo);
        Assert.True(UndoChecks.ScopeOf(stash).Status);
        Assert.True(UndoChecks.ScopeOf(stash).StashList);

        var checkout = Step(Entry(UndoKind.Checkout, Head("main", O), Head("topic", T)), UndoDirection.Redo);
        Assert.Equal(["refs/heads/main", "refs/heads/topic"], UndoChecks.ScopeOf(checkout).Refs);
    }

    private static UndoStep Step(UndoEntry entry, UndoDirection direction) => UndoStep.For(entry, direction);

    private static UndoState Now(HeadState head, Dictionary<string, string> refs) => new() { Head = head, Refs = refs };

    /// <summary>Compares refusals by their values, the list of paths included.</summary>
    private sealed class RefusalComparer : IEqualityComparer<UndoRefusal?>
    {
        public static RefusalComparer Instance { get; } = new();

        public bool Equals(UndoRefusal? x, UndoRefusal? y) =>
            x is null || y is null
                ? x is null && y is null
                : x with { Paths = null } == y with { Paths = null } && (x.Paths ?? []).SequenceEqual(y.Paths ?? []);

        public int GetHashCode(UndoRefusal? obj) => obj?.Kind.GetHashCode() ?? 0;
    }

    private static IReadOnlyList<UndoEffect> Undo(UndoEntry entry) => UndoSequence.For(entry, UndoDirection.Undo);

    private static IReadOnlyList<UndoEffect> Redo(UndoEntry entry) => UndoSequence.For(entry, UndoDirection.Redo);

    private static HeadState Head(string? branch, string? sha) => new(branch, sha);

    private static IReadOnlyDictionary<string, string> Refs(Dictionary<string, string> refs) => refs;

    private static UndoEntry Entry(
        UndoKind kind,
        HeadState before,
        HeadState after,
        Dictionary<string, string>? beforeRefs = null,
        Dictionary<string, string>? afterRefs = null,
        UndoCaptured? captured = null) =>
        new(
            kind,
            kind.ToString(),
            "op-1",
            new UndoState { Head = before, Refs = beforeRefs ?? new Dictionary<string, string>(StringComparer.Ordinal) },
            new UndoState { Head = after, Refs = afterRefs ?? new Dictionary<string, string>(StringComparer.Ordinal) },
            captured ?? UndoCaptured.None);

    /// <summary>Compares effects by their values, the lists of configuration keys included (records compare lists by reference).</summary>
    private sealed class EffectComparer : IEqualityComparer<UndoEffect>
    {
        public static EffectComparer Instance { get; } = new();

        public bool Equals(UndoEffect? x, UndoEffect? y) => (x, y) switch
        {
            (BranchConfig a, BranchConfig b) => a.Branch == b.Branch && a.From.SequenceEqual(b.From) && a.To.SequenceEqual(b.To),
            (RemoteConfig a, RemoteConfig b) => a.Remote == b.Remote && a.From.SequenceEqual(b.From) && a.To.SequenceEqual(b.To),
            _ => object.Equals(x, y),
        };

        public int GetHashCode(UndoEffect obj) => obj.GetType().GetHashCode();
    }
}
