using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Conflicts;

/// <summary>
/// What the conflict resolver (<c>ConflictResolverViewModel</c>, docs/phase-4-design.md section
/// 11) asks of its repository: the reads of a conflicted file and the operations that resolve
/// it. The app implements it with the repository's operations (section 14), so that the
/// resolver is tested with a recording fake. A write runs as a queued operation ("Resolve
/// {file}", "Merge tool for {file}", 10.1), shows its failure in the error dialog, never throws
/// for git's failure, and completes with whether the file is resolved. Every member is called on
/// the UI thread.
/// </summary>
public interface IConflictActions
{
    /// <summary>
    /// Mark resolved (11.7): one operation "Resolve {file}" that, just before it writes, reads the
    /// working-tree file of <paramref name="entry"/> as <see cref="ReadAsync"/> does and compares
    /// it with <paramref name="expected"/> (the bytes the resolver read; null: there was no file).
    /// When they are the same, it writes <paramref name="content"/> (the result) to the file and
    /// stages it (<see cref="IGitRepository.WriteResolutionAsync"/>). When they differ (a terminal
    /// or a tool changed the file, perhaps while the operation waited in the queue), it writes
    /// nothing and ends without failing, with <see cref="MarkResolvedOutcome.ChangedOnDisk"/> and
    /// the bytes it found, which the resolver's <c>{file} changed on disk.</c> question (7.2) may
    /// then expect. The markers question is the resolver's, before it calls this.
    /// </summary>
    Task<MarkResolvedResult> MarkResolvedAsync(ConflictEntry entry, byte[] content, byte[]? expected);

    /// <summary>
    /// Resolves <paramref name="path"/> with one side's whole file at once (11.8). Both sides
    /// present (a binary file, a symbolic link: "Use ours", "Use theirs"): <c>git checkout
    /// --ours|--theirs</c>, then <c>git add</c>, saving a snapshot first when the working-tree
    /// file differs from that side (D67's scheme). Only <paramref name="side"/> has the file
    /// (deleted by them or by us, added by us or by them: "Keep the file", "Keep it"): the file
    /// git left is staged as it is, so that the user's edits are kept, and checked out from that
    /// side only when it is missing.
    /// </summary>
    Task<bool> TakeSideAsync(string path, ConflictSide side);

    /// <summary>Resolves <paramref name="path"/> by deleting it (11.8: "Delete it", both deleted): <c>git rm</c>.</summary>
    Task<bool> RemoveAsync(string path);

    /// <summary>
    /// Runs the external merge tool on <paramref name="path"/> (11.9, D149) and waits for it, as
    /// an operation that can be cancelled. Completes with true when git staged the file (the tool
    /// resolved it).
    /// </summary>
    Task<bool> RunMergeToolAsync(string path);

    /// <summary>
    /// The merge tool that runs now: the queue's current operation is "Merge tool for {file}"
    /// (11.9), however it was started (the resolver's <c>Merge tool…</c> or the file menu's
    /// <c>Open in merge tool</c>); null while none runs.
    /// </summary>
    MergeToolRun? RunningMergeTool { get; }

    /// <summary>Raised on the UI thread when a merge tool starts running (<see cref="RunningMergeTool"/> becomes it).</summary>
    event EventHandler<MergeToolRun>? MergeToolStarted;

    /// <summary>
    /// One side of <paramref name="entry"/> in its working-tree form, for Take ours or Take theirs
    /// (11.6, <see cref="IGitRepository.ReadConflictSideAsync"/>): null when that side has no file
    /// or the read failed.
    /// </summary>
    Task<byte[]?> ReadSideAsync(ConflictEntry entry, ConflictSide side);

    /// <summary>What the resolver reads of <paramref name="entry"/> when it opens it (11.3, <see cref="IGitRepository.ReadConflictAsync"/>).</summary>
    Task<ConflictContent> ReadAsync(ConflictEntry entry);

    /// <summary>Asks a question on the window's dialog layer (7.2); true when the user goes ahead.</summary>
    Task<bool> ConfirmAsync(ConfirmationRequest request);

    /// <summary>
    /// For a merge whose message names <paramref name="branch"/> of a URL (<c>Merge branch
    /// '&lt;x&gt;' of &lt;url&gt;</c>: a pull's merge of <c>FETCH_HEAD</c>, 11.4), the
    /// remote-tracking branch it came from, as the refs last read have it: the current branch's
    /// upstream when that is <paramref name="branch"/> on its remote, else a remote branch
    /// <c>&lt;remote&gt;/&lt;branch&gt;</c> at <paramref name="mergeHead"/>; null when neither is.
    /// </summary>
    string? MergedRemoteBranch(string branch, string? mergeHead);
}

/// <summary>A merge tool that runs (<see cref="IConflictActions.RunningMergeTool"/>, 11.9).</summary>
/// <param name="Path">The file it runs on, repository-relative with <c>/</c>.</param>
/// <param name="Tool">The tool (<c>merge.tool</c>), for <c>Waiting for {tool} to close…</c>.</param>
/// <param name="Staged">Completes when its operation has ended, its refresh included: true when git staged the file.</param>
public sealed record MergeToolRun(string Path, string Tool, Task<bool> Staged);

/// <summary>How Mark resolved's operation ended (<see cref="IConflictActions.MarkResolvedAsync"/>, 11.7).</summary>
public enum MarkResolvedOutcome
{
    /// <summary>The result was written and staged.</summary>
    Resolved,

    /// <summary>The operation failed (the error dialog showed it) or did not run: nothing to go on with.</summary>
    Failed,

    /// <summary>The file on disk was not the bytes the resolver expected: nothing was written.</summary>
    ChangedOnDisk,
}

/// <summary>What Mark resolved's operation ended with.</summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="OnDisk">For <see cref="MarkResolvedOutcome.ChangedOnDisk"/>, the working-tree file's bytes the operation found (null: there was no file); otherwise null.</param>
public sealed record MarkResolvedResult(MarkResolvedOutcome Outcome, byte[]? OnDisk = null);
