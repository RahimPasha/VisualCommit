// The pages' actions: docs/phase-5-design.md 4.4 (File history and Blame in the file menus),
// 5.1 (Show reflog, Show backups) and 11.3 (Show in graph). Opening a page makes its view model
// (work package WP6: FileHistoryViewModel, BlameViewModel, ReflogViewModel) and hands it to
// RepositoryViewModel.OpenPageAsync; until WP6 builds them, opening throws. None is an operation
// of the queue: a page only reads.

using VisualCommit.App.ViewModels.Pages;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <inheritdoc cref="IRepositoryActions.ShowFileHistoryAsync"/>
    public Task ShowFileHistoryAsync(FileHistoryRequest request) =>
        throw new NotImplementedException("The file history page is work package WP6's (docs/phase-5-design.md 4.1).");

    /// <inheritdoc cref="IRepositoryActions.ShowBlameAsync"/>
    public Task ShowBlameAsync(BlameRequest request) =>
        throw new NotImplementedException("The blame page is work package WP6's (docs/phase-5-design.md 4.2).");

    /// <inheritdoc cref="IRepositoryActions.ShowReflogAsync"/>
    public Task ShowReflogAsync(string? branchRef) =>
        throw new NotImplementedException("The reflog page is work package WP6's (docs/phase-5-design.md 5.2).");

    /// <inheritdoc cref="IRepositoryActions.ShowBackupsAsync"/>
    public Task ShowBackupsAsync() =>
        throw new NotImplementedException("The backups list is work package WP6's (docs/phase-5-design.md 5.3).");

    /// <inheritdoc cref="IRepositoryActions.ShowInGraphAsync"/>
    public Task ShowInGraphAsync(string sha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);
        if (!IsClosed)
        {
            Repository.ShowInGraph(sha);
        }

        return Task.CompletedTask;
    }
}
