// The pages' actions: docs/phase-5-design.md 4.4 (File history and Blame in the file menus),
// 5.1 (Show reflog, Show backups) and 11.3 (Show in graph). Opening a page makes its view model
// (FileHistoryViewModel, BlameViewModel, ReflogViewModel), hands it to
// RepositoryViewModel.OpenPageAsync, which leaves what the graph area shows first, and then lets
// it read. None is an operation of the queue: a page only reads.

using VisualCommit.App.ViewModels.Pages;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <inheritdoc cref="IRepositoryActions.ShowFileHistoryAsync"/>
    public async Task ShowFileHistoryAsync(FileHistoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsClosed && await OpenAsync(new FileHistoryViewModel(Repository, request)) is { } page)
        {
            await page.LoadAsync();
        }
    }

    /// <inheritdoc cref="IRepositoryActions.ShowBlameAsync"/>
    public async Task ShowBlameAsync(BlameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsClosed && await OpenAsync(new BlameViewModel(Repository, request)) is { } page)
        {
            await page.LoadAsync();
        }
    }

    /// <inheritdoc cref="IRepositoryActions.ShowReflogAsync"/>
    public async Task ShowReflogAsync(string? branchRef)
    {
        if (!IsClosed && await OpenAsync(new ReflogViewModel(Repository, branchRef ?? OperationTexts.MenuTexts.HeadSource)) is { } page)
        {
            await page.LoadAsync();
        }
    }

    /// <inheritdoc cref="IRepositoryActions.ShowBackupsAsync"/>
    public async Task ShowBackupsAsync()
    {
        if (!IsClosed && await OpenAsync(new ReflogViewModel(Repository, source: null)) is { } page)
        {
            await page.LoadAsync();
        }
    }

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

    /// <summary>
    /// Opens <paramref name="page"/> in place of the graph (11.1) and returns it, or null when it
    /// did not open (leaving what the graph area showed was refused, or the tab closed): it was
    /// disposed then and reads nothing.
    /// </summary>
    private async Task<T?> OpenAsync<T>(T page)
        where T : class, IGraphAreaPage
    {
        await Repository.OpenPageAsync(page);
        return ReferenceEquals(Repository.Page, page) ? page : null;
    }
}
