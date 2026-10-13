namespace VisualCommit.Core.Undo;

/// <summary>
/// One recorded action of the undo history (docs/phase-5-design.md 7.1): its kind and text, the
/// operation it was recorded in, the states read before and after it and what was captured
/// while it ran, and whether it is finished. Its undo and redo are made from these
/// (<see cref="UndoSequence"/>); a step that makes a new object (a redo's stash, a redo's
/// hard-reset snapshot) rewrites the entry with it (7.5 step 5).
/// </summary>
/// <param name="Kind">What the action was.</param>
/// <param name="Text">How the toolbar and the step name it (7.6): <c>commit "Fix the parser"</c>.</param>
/// <param name="OperationId">The operation it was recorded in (<c>op-&lt;n&gt;</c>).</param>
/// <param name="Before">The state just before the action.</param>
/// <param name="After">The state just after it; for a <see cref="UndoEntryState.Pending"/> entry, as it stopped.</param>
/// <param name="Captured">What the reads could not see.</param>
public sealed record UndoEntry(
    UndoKind Kind,
    string Text,
    string OperationId,
    UndoState Before,
    UndoState After,
    UndoCaptured Captured)
{
    /// <summary>Finished, or stopped with the banner showing (7.2).</summary>
    public UndoEntryState State { get; init; } = UndoEntryState.Done;

    /// <summary>What the action's operation declared (7.7 step 1): a pending entry is completed by reading it again, and a step reads the entry's refs by it (7.5 step 2).</summary>
    public UndoScope Scope { get; init; } = UndoScope.None;

    /// <summary>
    /// The backup refs the entry needs, which the clean-up keeps while the entry is in a history
    /// (7.8, D171): those the action saved, and the snapshot it saved or restored.
    /// </summary>
    public IReadOnlyList<string> Backups =>
        Captured.Snapshot is { } snapshot && !Captured.Backups.Contains(snapshot.Ref, StringComparer.Ordinal)
            ? [.. Captured.Backups, snapshot.Ref]
            : Captured.Backups;
}
