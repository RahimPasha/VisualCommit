// Undo and redo: docs/phase-5-design.md section 7 (D154, D155, D163, D165 to D172). The undo
// history's recording inside every recorded operation (7.7), the toolbar's Undo and Redo as one
// operation each with their states (7.4, 7.5), what each status read does to the history (7.2,
// 7.4), the restore bar's tie (7.9) and the backups list's restores (7.10). Work package WP3
// builds them here; until then nothing is recorded, Undo and Redo stay disabled, the restore bar
// is as in phase 4, and Undo, Redo and the restores, which nothing offers yet, throw.

using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Undo;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <summary>The toolbar's Undo and its key (7.4, 7.5, D163): the entry before the history's cursor, applied backwards as one operation.</summary>
    public Task UndoAsync() =>
        throw new NotImplementedException("Undo is work package WP3's (docs/phase-5-design.md 7.5).");

    /// <summary>The toolbar's Redo and its key: the entry after the history's cursor, applied forwards as one operation.</summary>
    public Task RedoAsync() =>
        throw new NotImplementedException("Redo is work package WP3's (docs/phase-5-design.md 7.5).");

    /// <inheritdoc cref="IRepositoryActions.RestoreBackupAsync"/>
    public Task RestoreBackupAsync(BackupInfo backup) =>
        throw new NotImplementedException("The backups list's restores are work package WP3's (docs/phase-5-design.md 7.10).");

    /// <summary>
    /// The toolbar's Undo and Redo as they stand (1.1, 7.4): each enabled with <c>Undo {text}</c>
    /// (<c>Redo {text}</c>) when its entry passes the checks made after the last refresh, otherwise
    /// disabled with the first reason that holds. The toolbar reads it with its other states and
    /// again on <see cref="ToolbarViewModel.UpdateUndoStates"/>. Both disabled, without a tooltip,
    /// until WP3 builds it.
    /// </summary>
    public UndoAvailability ReadUndoAvailability() => UndoAvailability.Disabled;

    /// <summary>
    /// Hook (7.2, 7.4): the repository shows a newly read status, after a refresh's refs too
    /// (<see cref="RepositoryViewModel"/>'s <c>OnStatusApplied</c>), on the UI thread. WP3 removes a
    /// pending entry whose stopped operation ended outside the app, makes the checks that need no
    /// git and gives the toolbar its states. Nothing until then.
    /// </summary>
    public void NoteUndoState(WorkingTreeStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
    }

    /// <summary>
    /// Hook (7.9): the restore bar's Restore of <paramref name="snapshot"/>. While the discard that
    /// saved it is the entry before the history's cursor, the Restore is that entry's Undo: WP3
    /// runs it and gives true. Otherwise false, and the Restore runs as in phase 4, recorded as an
    /// entry of its own. False until WP3 builds it.
    /// </summary>
    public Task<bool> TryRestoreAsUndoAsync(DiscardSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Task.FromResult(false);
    }

    internal partial Task<UndoRecording?> BeginUndoRecordingAsync(UndoScope scope, OperationContext context) =>
        Task.FromResult<UndoRecording?>(null);

    internal partial Task EndUndoRecordingAsync(UndoRecording? recording, UndoCaptured captured, OperationReport? report) =>
        Task.CompletedTask;

    /// <summary>What the recording read before an operation's work, for the read after it (7.7): WP3 gives it its fields.</summary>
    internal sealed class UndoRecording;
}
