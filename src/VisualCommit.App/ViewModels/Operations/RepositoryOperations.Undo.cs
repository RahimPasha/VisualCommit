// Undo and redo: docs/phase-5-design.md section 7 (D154, D155, D163, D165 to D172). The undo
// history's recording inside every recorded operation (7.7), the toolbar's Undo and Redo as one
// operation each (7.4, 7.5), and the backups list's restores (7.10). Work package WP3 builds them
// here; until then nothing is recorded, and Undo, Redo and the restores, which nothing offers yet,
// throw.

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

    internal partial Task<UndoRecording?> BeginUndoRecordingAsync(UndoScope scope, OperationContext context) =>
        Task.FromResult<UndoRecording?>(null);

    internal partial Task EndUndoRecordingAsync(UndoRecording? recording, UndoCaptured captured, OperationReport? report) =>
        Task.CompletedTask;

    /// <summary>What the recording read before an operation's work, for the read after it (7.7): WP3 gives it its fields.</summary>
    internal sealed class UndoRecording;
}
