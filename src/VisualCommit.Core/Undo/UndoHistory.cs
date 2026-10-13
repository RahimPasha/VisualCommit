namespace VisualCommit.Core.Undo;

/// <summary>
/// One worktree's undo history for the app's session (docs/phase-5-design.md 7.1, D154, D167): a
/// list of entries and a cursor between them. Undo applies the entry before the cursor backwards
/// and moves the cursor back; Redo applies the entry after it forwards and moves it on; a new
/// recorded action drops every entry after the cursor, then is added. At most
/// <see cref="Capacity"/> entries are kept, the oldest dropped first. Nothing is written to disk.
/// Use it on one thread: the app's is the UI thread, where operations record and step.
/// </summary>
/// <remarks>
/// At most one entry is pending (7.2): that of an action that stopped (a merge, a pick, a revert
/// or a rebase with the banner showing). It is always the last entry, with the cursor after it:
/// Undo and Redo are off while an operation is in progress (7.4), and an action recorded while it
/// waits (a tag, a branch, a discard) goes before it, since the stopped action ends after that
/// one, when Continue, Skip or the stage panel's Commit completes it. A new pending entry
/// replaces one still pending, whose action must have ended outside the app.
/// </remarks>
public sealed class UndoHistory
{
    /// <summary>How many entries a history keeps (7.1): it is for recent mistakes, and the bound keeps the backups it holds back from the clean-up bounded.</summary>
    public const int Capacity = 100;

    private readonly List<UndoEntry> _entries = [];
    private readonly int _capacity;

    /// <param name="capacity">How many entries to keep; tests give a smaller bound.</param>
    public UndoHistory(int capacity = Capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    /// <summary>Raised after every change of the entries or the cursor.</summary>
    public event EventHandler? Changed;

    /// <summary>The entries, oldest first.</summary>
    public IReadOnlyList<UndoEntry> Entries => _entries;

    /// <summary>How many entries lie before the cursor: those that can be undone, newest last.</summary>
    public int Cursor { get; private set; }

    /// <summary>The entry Undo would apply: the one just before the cursor, or null.</summary>
    public UndoEntry? ToUndo => Cursor > 0 ? _entries[Cursor - 1] : null;

    /// <summary>The entry Redo would apply: the one just after the cursor, or null.</summary>
    public UndoEntry? ToRedo => Cursor < _entries.Count ? _entries[Cursor] : null;

    /// <summary>The entry of the action that stopped and is not finished yet (7.2), or null: the last entry, when it is pending.</summary>
    public UndoEntry? Pending => PendingIndex() is >= 0 and var index ? _entries[index] : null;

    /// <summary>The backup refs every entry needs (7.8): the clean-up keeps them while they are in the history.</summary>
    public IReadOnlySet<string> Backups => _entries.SelectMany(entry => entry.Backups).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Records a new action: every entry after the cursor goes (there is nothing to redo after a
    /// new action), then <paramref name="entry"/> is added and the cursor moves past it; past
    /// <see cref="Capacity"/>, the oldest entry goes. While an entry is pending, a finished one goes
    /// before it, so that the pending one stays last; a new pending one replaces it.
    /// </summary>
    public void Add(UndoEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.RemoveRange(Cursor, _entries.Count - Cursor);
        var pending = PendingIndex();
        if (pending >= 0 && entry.State == UndoEntryState.Pending)
        {
            _entries.RemoveAt(pending);
            pending = -1;
        }

        _entries.Insert(pending >= 0 ? pending : _entries.Count, entry);
        if (_entries.Count > _capacity)
        {
            _entries.RemoveRange(0, _entries.Count - _capacity);
        }

        Cursor = _entries.Count;
        RaiseChanged();
    }

    /// <summary>
    /// <see cref="ToUndo"/> was undone: the cursor moves back over it, which is replaced by
    /// <paramref name="rewritten"/> when the step made a new object (7.5 step 5).
    /// </summary>
    /// <exception cref="InvalidOperationException">There is nothing to undo, or the entry is pending.</exception>
    public void Undone(UndoEntry? rewritten = null)
    {
        if (ToUndo is not { State: UndoEntryState.Done })
        {
            throw new InvalidOperationException("There is no finished entry to undo.");
        }

        if (rewritten is not null)
        {
            _entries[Cursor - 1] = rewritten;
        }

        Cursor--;
        RaiseChanged();
    }

    /// <summary>
    /// <see cref="ToRedo"/> was redone: the cursor moves on over it, which is replaced by
    /// <paramref name="rewritten"/> when the step made a new object (a redo's stash, a redo's
    /// hard-reset snapshot, 7.3).
    /// </summary>
    /// <exception cref="InvalidOperationException">There is nothing to redo.</exception>
    public void Redone(UndoEntry? rewritten = null)
    {
        if (ToRedo is null)
        {
            throw new InvalidOperationException("There is no entry to redo.");
        }

        if (rewritten is not null)
        {
            _entries[Cursor] = rewritten;
        }

        Cursor++;
        RaiseChanged();
    }

    /// <summary>
    /// The stopped action ended (Continue, Skip, or the stage panel's Commit, 7.2): its pending
    /// entry becomes one entry from before the action to <paramref name="after"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No entry is pending.</exception>
    public void Complete(UndoState after)
    {
        ArgumentNullException.ThrowIfNull(after);
        var index = PendingIndex();
        if (index < 0)
        {
            throw new InvalidOperationException("No entry is pending.");
        }

        _entries[index] = _entries[index] with { After = after, State = UndoEntryState.Done };
        RaiseChanged();
    }

    /// <summary>
    /// The stopped action was aborted (D165), or ended outside the app (a terminal): its pending
    /// entry goes. Nothing happens when none is pending.
    /// </summary>
    public void RemovePending()
    {
        var index = PendingIndex();
        if (index < 0)
        {
            return;
        }

        _entries.RemoveAt(index);
        if (index < Cursor)
        {
            Cursor--;
        }

        RaiseChanged();
    }

    /// <summary>Where the pending entry is, or -1: found by its state, though it is kept last.</summary>
    private int PendingIndex() => _entries.FindLastIndex(entry => entry.State == UndoEntryState.Pending);

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
