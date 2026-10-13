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
        var snapshot = new DiscardSnapshot("refs/visualcommit/backup/reset-1-changes", T, ["a.txt"], new HashSet<string> { "a.txt" });
        var after = new UndoState { Head = Head("main", N), Files = [new RecordedFile("a.txt", "blob", "hash"), new RecordedFile("other.txt", "blob2", "hash2")] };
        var hard = new UndoEntry(UndoKind.Reset, "reset", "op-1", new UndoState { Head = Head("main", O) }, after, new UndoCaptured { ResetMode = ResetMode.Hard, ResetIndex = "index", Snapshot = snapshot });
        var undo = Undo(hard);
        Assert.Equal(3, undo.Count);
        Assert.Equal(new MoveHead("main", N, O, MoveMode.Keep), undo[0]);
        Assert.Equal(new IndexTo("index", N), undo[1]);
        var restore = Assert.IsType<RestoreSnapshot>(undo[2]);
        Assert.Same(snapshot, restore.Snapshot);
        Assert.Equal([new RecordedFile("a.txt", "blob", "hash")], restore.From);
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
        Assert.Equal([new StashPop(T, "after")], Undo(stash));
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
        Assert.Equal([new Switch(Head(null, T), Head("main", O)), new StashPop(N, "clean")], Undo(entry));
        Assert.Equal([new StashPush(true, "Auto stash before checking out a1b2c3d", "dirty"), new Switch(Head("main", O), Head(null, T))], Redo(entry));
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
    public void A_pending_entry_or_one_without_its_values_has_no_sequence()
    {
        var pending = Entry(UndoKind.Merge, Head("main", O), Head("main", N)) with { State = UndoEntryState.Pending };
        Assert.Throws<InvalidOperationException>(() => Undo(pending));

        var reset = Entry(UndoKind.Reset, Head("main", O), Head("main", N));
        Assert.Throws<InvalidOperationException>(() => Undo(reset));
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
