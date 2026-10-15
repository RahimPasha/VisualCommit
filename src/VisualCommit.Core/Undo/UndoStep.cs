namespace VisualCommit.Core.Undo;

/// <summary>
/// One undo or redo step for the git layer to run (docs/phase-5-design.md 7.5,
/// <see cref="Git.IGitRepository.RunUndoStepAsync"/>): the entry, the direction, and its effects in
/// order (<see cref="UndoSequence.For"/>). The git layer reads what the effects check, simulates
/// the sequence (each check against the state the effects before it leave), applies the effects
/// in their groups, reverses what it applied when a later effect refuses, and rewrites the entry
/// with the objects it made.
/// </summary>
/// <param name="Entry">The entry undone or redone.</param>
/// <param name="Direction">Which way.</param>
/// <param name="Effects">The effects, in order.</param>
public sealed record UndoStep(UndoEntry Entry, UndoDirection Direction, IReadOnlyList<UndoEffect> Effects)
{
    /// <summary>The step for <paramref name="entry"/> in <paramref name="direction"/>, with its kind's effects.</summary>
    public static UndoStep For(UndoEntry entry, UndoDirection direction) =>
        new(entry, direction, UndoSequence.For(entry, direction));

    /// <summary>The step changes a remote (a <see cref="UndoEffect.RemoteRef"/>): a network operation that can be cancelled and asks first (D155, D170).</summary>
    public bool ChangesRemote => Effects.Any(effect => effect is UndoEffect.RemoteRef);

    /// <summary>
    /// The reflog message of the refs the step writes; null for 7.3's <c>VisualCommit: undo
    /// &lt;text&gt;</c> or <c>VisualCommit: redo &lt;text&gt;</c>. The backups list's restores run
    /// their effects as a step of their own and write <c>VisualCommit: &lt;entry text&gt;</c>
    /// (7.10, the report's BD17): <c>VisualCommit: restore of branch doomed</c>.
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>How a step ended.</summary>
/// <param name="Refusal">Why it was refused, changing nothing but <c>update-index --refresh</c>'s stat data; null when it was done.</param>
/// <param name="Entry">The entry as the history keeps it from now on: rewritten with the objects the step made (a new stash, a new snapshot, a <c>tag-undone</c> backup), or as it was.</param>
public sealed record UndoStepResult(UndoRefusal? Refusal, UndoEntry Entry)
{
    public bool IsDone => Refusal is null;
}

/// <summary>What a refused step's check found (7.6's second table); the app words it ("Can't undo {text}: …").</summary>
public enum UndoRefusalKind
{
    /// <summary>An operation is in progress (the app's own check: <c>update-ref</c> refuses nothing).</summary>
    InProgress,

    /// <summary>
    /// The index has conflicts and no operation is in progress (7.4's "Resolve the conflicts
    /// first"), other than those an apply's or a pop's undo puts back as they were (<see cref="UndoRefusal.Paths"/>: the conflicted files).
    /// </summary>
    Conflicts,

    /// <summary>HEAD is no longer on the branch (<see cref="UndoRefusal.Name"/>).</summary>
    HeadNotOnBranch,

    /// <summary>HEAD is no longer at the commit (<see cref="UndoRefusal.Sha"/>).</summary>
    HeadNotAtCommit,

    /// <summary>A branch moved since (<see cref="UndoRefusal.Name"/>, now at <see cref="UndoRefusal.Sha"/>).</summary>
    BranchMoved,

    /// <summary>A ref other than a branch changed since (<see cref="UndoRefusal.Name"/>: its full name).</summary>
    RefChanged,

    /// <summary>A branch of the name exists again.</summary>
    BranchExists,

    /// <summary>A tag of the name exists again.</summary>
    TagExists,

    /// <summary>
    /// A branch's or a remote's settings (<see cref="UndoRefusal.Name"/>) are no longer those the
    /// step leaves: an upstream set or stopped since, a remote's keys or URL changed, a remote gone.
    /// </summary>
    ConfigChanged,

    /// <summary>A remote of the name (<see cref="UndoRefusal.Name"/>) exists again.</summary>
    RemoteExists,

    /// <summary>The branch is checked out in another worktree (<see cref="UndoRefusal.Name"/>, its folder in <see cref="UndoRefusal.Paths"/>).</summary>
    CheckedOutElsewhere,

    /// <summary>Local changes are in the way of a move or a switch: <see cref="UndoRefusal.Paths"/> as the app counted them (git names only the first).</summary>
    LocalChangesInTheWay,

    /// <summary>Ignored files are in the way of a move (<see cref="UndoRefusal.Paths"/>).</summary>
    IgnoredFilesInTheWay,

    /// <summary>The index has staged changes since.</summary>
    IndexChanged,

    /// <summary>Files changed since (<see cref="UndoRefusal.Paths"/>).</summary>
    FilesChanged,

    /// <summary>The stash is no longer in the list.</summary>
    StashGone,

    /// <summary>The stash is in the list again.</summary>
    StashListed,

    /// <summary>The changes have changed since the stash (the whole status differs).</summary>
    StatusChanged,

    /// <summary>The commits no longer exist (<c>cat-file -e</c> failed: an outside <c>gc</c>).</summary>
    CommitsGone,

    /// <summary>The ref on the remote changed since (the lease): <see cref="UndoRefusal.Name"/> (its short name) on <see cref="UndoRefusal.Remote"/>.</summary>
    RemoteChanged,

    /// <summary>A file has a flag git cannot restore: intent-to-add or skip-worktree (<see cref="UndoRefusal.Paths"/>).</summary>
    FlagNotRestorable,

    /// <summary>Git refused for a reason no check foresaw; <see cref="UndoRefusal.GitOutput"/> has what it said.</summary>
    GitRefused,
}

/// <summary>Why a step was refused, with what the app's text names.</summary>
/// <param name="Kind">The check that refused.</param>
/// <param name="Name">The branch, tag, ref or remote the text names, when it names one.</param>
/// <param name="Sha">The commit the text names (where a branch is now; null when it no longer exists), when it names one.</param>
/// <param name="Paths">The files (or the folder) the text names or counts.</param>
/// <param name="GitOutput">Git's own output, shown below the summary, when git refused.</param>
/// <param name="Remote">The remote whose ref changed (<see cref="UndoRefusalKind.RemoteChanged"/>).</param>
public sealed record UndoRefusal(UndoRefusalKind Kind, string? Name = null, string? Sha = null, IReadOnlyList<string>? Paths = null, string? GitOutput = null, string? Remote = null);
