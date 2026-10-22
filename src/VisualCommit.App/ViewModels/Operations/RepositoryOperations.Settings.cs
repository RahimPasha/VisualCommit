// The settings screen's writes of "This repository" (docs/phase-6-design.md 8.3, 7.8 step 5): one
// operation of the repository's queue, so that they never run beside another write of the
// repository (D81). Built by work package WP-SET; its tests are
// tests/VisualCommit.App.Tests/RepositoryOperationsSettingsTests.cs.

using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Undo;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <summary>
    /// Writes the settings screen's changed keys of this repository (8.3): <c>git config --local
    /// --replace-all</c> or <c>--unset-all</c> per key, in the working tree, in the order given,
    /// stopping at the first failure, as one local operation of the queue with no undo entry
    /// (<see cref="UndoScope.None"/>, D168) and no refresh (a write to <c>.git/config</c> changes
    /// nothing a refresh shows). <paramref name="cancellation"/> is the screen's Cancel: it drops the
    /// operation while it waits behind another one (4.5, <see cref="RunOptions.Cancellation"/>) and
    /// does nothing once the writes run. A failure, a held <c>.git/config.lock</c> among them (8.5),
    /// opens the error dialog over the screen with its texts (14.1); the result says
    /// <c>Saved the repository's settings</c> (14.2). Never throws.
    /// </summary>
    public Task<OperationResult> SaveRepositorySettingsAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(writes);
        return RunAsync(
            OperationTexts.SaveRepositorySettings,
            OperationKind.Local,
            RefreshScope.None,
            async _ =>
            {
                // Never cancelled half-way: a write cut short would leave git's lock file behind.
                await Git.WriteConfigAsync(writes, CancellationToken.None);
                return OperationReport.Done(OperationTexts.SavedRepositorySettings);
            },
            new RunOptions { Cancellation = cancellation, Undo = UndoScope.None });
    }
}
