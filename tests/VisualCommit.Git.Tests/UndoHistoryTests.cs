using VisualCommit.Core.Git;
using VisualCommit.Core.Undo;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The undo history's list and cursor (docs/phase-5-design.md 7.1, 7.2): undo and redo move the
/// cursor, a new action drops what lies after it, the 100-entry bound drops the oldest, a pending
/// entry is completed or removed; and the session's histories, one per worktree, with the backups
/// of a repository's worktrees joined. No git.
/// </summary>
public class UndoHistoryTests
{
    [Fact]
    public void Undo_and_redo_move_the_cursor_over_the_entries()
    {
        var history = new UndoHistory();
        Assert.Null(history.ToUndo);
        Assert.Null(history.ToRedo);

        history.Add(Entry("one"));
        history.Add(Entry("two"));
        Assert.Equal((2, "two"), (history.Cursor, history.ToUndo?.Text));
        Assert.Null(history.ToRedo);

        history.Undone();
        Assert.Equal((1, "one", "two"), (history.Cursor, history.ToUndo?.Text, history.ToRedo?.Text));

        history.Undone();
        Assert.Equal((0, "one"), (history.Cursor, history.ToRedo?.Text));
        Assert.Null(history.ToUndo);
        Assert.Throws<InvalidOperationException>(() => history.Undone());

        history.Redone();
        Assert.Equal((1, "one", "two"), (history.Cursor, history.ToUndo?.Text, history.ToRedo?.Text));
        history.Redone();
        Assert.Throws<InvalidOperationException>(() => history.Redone());
        Assert.Equal(["one", "two"], history.Entries.Select(entry => entry.Text));
    }

    [Fact]
    public void A_new_action_after_undos_drops_every_entry_after_the_cursor()
    {
        var history = new UndoHistory();
        history.Add(Entry("one"));
        history.Add(Entry("two"));
        history.Add(Entry("three"));
        history.Undone();
        history.Undone();

        history.Add(Entry("four"));

        Assert.Equal(["one", "four"], history.Entries.Select(entry => entry.Text));
        Assert.Equal(2, history.Cursor);
        Assert.Null(history.ToRedo);
    }

    [Fact]
    public void Past_the_bound_the_oldest_entries_go()
    {
        var history = new UndoHistory();
        for (var i = 1; i <= UndoHistory.Capacity + 3; i++)
        {
            history.Add(Entry($"action {i}"));
        }

        Assert.Equal(100, UndoHistory.Capacity);
        Assert.Equal(UndoHistory.Capacity, history.Entries.Count);
        Assert.Equal("action 4", history.Entries[0].Text);
        Assert.Equal("action 103", history.ToUndo?.Text);
        Assert.Equal(UndoHistory.Capacity, history.Cursor);

        // Undone entries stay redoable until the bound is reached again by new ones.
        var small = new UndoHistory(capacity: 2);
        small.Add(Entry("a"));
        small.Add(Entry("b"));
        small.Undone();
        small.Add(Entry("c"));
        small.Add(Entry("d"));
        Assert.Equal(["c", "d"], small.Entries.Select(entry => entry.Text));
        Assert.Equal(2, small.Cursor);
    }

    [Fact]
    public void An_undone_or_redone_entry_is_replaced_by_the_one_the_step_rewrote()
    {
        var history = new UndoHistory();
        history.Add(Entry("stash \"x\"", captured: new UndoCaptured { Stash = "old" }));

        history.Undone();
        Assert.Equal("old", history.ToRedo?.Captured.Stash);

        // A redo stashes again: the entry takes the new stash (7.3).
        history.Redone(history.ToRedo! with { Captured = new UndoCaptured { Stash = "new" } });
        Assert.Equal("new", history.ToUndo?.Captured.Stash);

        history.Undone(history.ToUndo! with { Text = "rewritten" });
        Assert.Equal("rewritten", history.ToRedo?.Text);
    }

    [Fact]
    public void A_pending_entry_is_completed_by_continue_or_removed_by_abort()
    {
        var history = new UndoHistory();
        history.Add(Entry("commit"));
        history.Add(Entry("merge of x into main") with { State = UndoEntryState.Pending });

        Assert.Equal("merge of x into main", history.Pending?.Text);

        // Undo is off while it is pending.
        Assert.Throws<InvalidOperationException>(() => history.Undone());

        var after = new UndoState { Head = new HeadState("main", "after") };
        history.Complete(after);
        Assert.Null(history.Pending);
        Assert.Equal((UndoEntryState.Done, "after"), (history.ToUndo?.State, history.ToUndo?.After.Head.Sha));
        Assert.Throws<InvalidOperationException>(() => history.Complete(after));

        history.Add(Entry("rebase") with { State = UndoEntryState.Pending });
        history.RemovePending();
        Assert.Equal(["commit", "merge of x into main"], history.Entries.Select(entry => entry.Text));
        Assert.Equal(2, history.Cursor);

        // Nothing pending: nothing removed.
        history.RemovePending();
        Assert.Equal(2, history.Entries.Count);
    }

    [Fact]
    public void Changed_is_raised_for_every_change_and_the_backups_are_every_entrys()
    {
        var history = new UndoHistory();
        var changes = 0;
        history.Changed += (_, _) => changes++;

        history.Add(Entry("reset", captured: new UndoCaptured { Backups = ["refs/visualcommit/backup/reset-1", "refs/visualcommit/backup/reset-1-changes"] }));
        history.Add(Entry("discard", captured: new UndoCaptured { Snapshot = new DiscardSnapshot("refs/visualcommit/backup/discard-2", "c", ["a.txt"], new HashSet<string>()) }));
        history.Undone();
        history.Redone();

        Assert.Equal(4, changes);
        Assert.Equal(
            ["refs/visualcommit/backup/discard-2", "refs/visualcommit/backup/reset-1", "refs/visualcommit/backup/reset-1-changes"],
            history.Backups.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_session_keeps_one_history_per_worktree_and_joins_the_backups_of_a_repositorys_worktrees()
    {
        var histories = new UndoHistories();
        var root = Path.Combine(Path.GetTempPath(), "undo-histories");
        var main = Path.Combine(root, "repo", ".git");
        var linked = Path.Combine(main, "worktrees", "other");
        var elsewhere = Path.Combine(root, "elsewhere", ".git");

        var first = histories.For(main, main);
        Assert.Same(first, histories.For(main + Path.DirectorySeparatorChar, main));
        Assert.NotSame(first, histories.For(linked, main));
        if (OperatingSystem.IsWindows())
        {
            Assert.Same(first, histories.For(main.ToUpperInvariant(), main));
        }

        first.Add(Entry("a", captured: new UndoCaptured { Backups = ["refs/visualcommit/backup/a"] }));
        histories.For(linked, main).Add(Entry("b", captured: new UndoCaptured { Backups = ["refs/visualcommit/backup/b"] }));
        histories.For(elsewhere, elsewhere).Add(Entry("c", captured: new UndoCaptured { Backups = ["refs/visualcommit/backup/c"] }));

        Assert.Equal(["refs/visualcommit/backup/a", "refs/visualcommit/backup/b"], histories.BackupsOf(main).Order(StringComparer.Ordinal));
        Assert.Equal(["refs/visualcommit/backup/c"], histories.BackupsOf(elsewhere));
    }

    private static UndoEntry Entry(string text, UndoCaptured? captured = null) =>
        new(UndoKind.Commit, text, "op-1", new UndoState { Head = new HeadState("main", "before") }, new UndoState { Head = new HeadState("main", "after") }, captured ?? UndoCaptured.None);
}
