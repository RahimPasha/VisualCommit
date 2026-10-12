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
    /// Mark resolved (11.7): writes <paramref name="content"/> (the result, in the encoding the
    /// file was read in) to the working-tree file <paramref name="path"/> and stages it
    /// (<see cref="IGitRepository.WriteResolutionAsync"/>). The questions of 7.2 (blocks left, the
    /// file changed on disk) are the resolver's, before it calls this.
    /// </summary>
    Task<bool> MarkResolvedAsync(string path, byte[] content);

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
    /// One side of <paramref name="entry"/> in its working-tree form, for Take ours or Take theirs
    /// (11.6, <see cref="IGitRepository.ReadConflictSideAsync"/>): null when that side has no file
    /// or the read failed.
    /// </summary>
    Task<byte[]?> ReadSideAsync(ConflictEntry entry, ConflictSide side);

    /// <summary>What the resolver reads of <paramref name="entry"/> when it opens it (11.3, <see cref="IGitRepository.ReadConflictAsync"/>).</summary>
    Task<ConflictContent> ReadAsync(ConflictEntry entry);

    /// <summary>Asks a question on the window's dialog layer (7.2); true when the user goes ahead.</summary>
    Task<bool> ConfirmAsync(ConfirmationRequest request);
}
