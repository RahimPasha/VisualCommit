using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// The registry of the session's repository view models by worktree (docs/phase-6-design.md 3.4,
/// D209): the tabs' full view models, in registration order, and the dashboards' summary-mode
/// ones with their holders, so that a dashboard row and the tab opened from it share one queue.
/// <c>TabServices.OpenRepositories</c>; used on the UI thread only.
/// <para>
/// A stub: work package WP-D builds it. Until then nothing registers, every lookup finds nothing,
/// no summary is made and nothing waits.
/// </para>
/// </summary>
public sealed class OpenRepositories
{
    /// <summary>The summary-mode view models held by dashboards.</summary>
    public IReadOnlyList<RepositoryViewModel> Summaries { get; } = [];

    /// <summary>The summary-mode view models released while an operation of the user ran, already disposed (3.7).</summary>
    public IReadOnlyList<RepositoryViewModel> Closing { get; } = [];

    /// <summary>Raised with the worktree's key when a tab registered or unregistered, or a summary was adopted (3.5, 3.6); it starts no read.</summary>
#pragma warning disable CS0067 // Raised from work package WP-D on.
    public event EventHandler<string>? Changed;
#pragma warning restore CS0067

    /// <summary>Registers a tab's full view model by its worktree.</summary>
    public void RegisterTab(RepositoryViewModel repository)
    {
    }

    /// <summary>Unregisters a tab's view model, keeping its queue's idle task for the worktree until it completes.</summary>
    public void UnregisterTab(RepositoryViewModel repository)
    {
    }

    /// <summary>The first registered tab view model still open for the worktree <paramref name="key"/>, or null.</summary>
    public RepositoryViewModel? TabFor(string key) => null;

    /// <summary>The first registered tab view model whose folder is <paramref name="folder"/> (<c>WorkspacePaths.Same</c>), or null.</summary>
    public RepositoryViewModel? TabForFolder(string folder) => null;

    /// <summary>The worktree's kept idle task (a closed tab's or a released summary's queue), else a completed task.</summary>
    public Task WhenIdleAsync(string key) => Task.CompletedTask;

    /// <summary>The worktree's summary-mode view model, removed with its holders, for a tab to adopt (3.3); or null.</summary>
    public RepositoryViewModel? TryAdopt(string key) => null;

    /// <summary>
    /// The summary-mode view model of <paramref name="repository"/>'s worktree with
    /// <paramref name="holder"/> added, made when there is none; null when a tab holds the
    /// worktree, <paramref name="holderLifetime"/> was cancelled meanwhile, or the registry was disposed.
    /// </summary>
    public Task<RepositoryViewModel?> AcquireSummaryAsync(IGitRepository repository, object holder, CancellationToken holderLifetime) =>
        Task.FromResult<RepositoryViewModel?>(null);

    /// <summary>Removes <paramref name="holder"/>; the last release disposes the view model (D81's rules).</summary>
    public void ReleaseSummary(RepositoryViewModel repository, object holder)
    {
    }

    /// <summary>The first queue held on the common git folder <paramref name="commonKey"/>, other than <paramref name="except"/>, that runs a network operation; or null (4.3).</summary>
    public IOperationQueue? NetworkQueueOn(string commonKey, IOperationQueue? except) => null;

    /// <summary>Disposes every summary-mode view model and refuses every later acquisition (3.7).</summary>
    public void DisposeAll()
    {
    }
}
