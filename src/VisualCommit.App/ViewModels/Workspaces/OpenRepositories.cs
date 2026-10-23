using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// The registry of the session's repository view models by worktree (docs/phase-6-design.md 3.4,
/// D209): the tabs' full view models, in registration order, and the dashboards' summary-mode
/// ones with their holders, so that a dashboard row and the tab opened from it share one queue
/// (D81 stays literal). Keyed by <see cref="WorktreeKey"/> (the worktree's own git folder, compared
/// as the platform compares paths). <c>TabServices.OpenRepositories</c>; used on the UI thread only,
/// and every call is synchronous between awaits, so an adoption and a row's acquisition cannot
/// interleave.
/// </summary>
public sealed class OpenRepositories
{
    private readonly TabServices _services;
    private readonly List<RepositoryViewModel> _tabs = [];
    private readonly Dictionary<string, Summary> _summaries = new(WorktreeKey.Comparer);
    private readonly List<RepositoryViewModel> _closing = [];
    private readonly Dictionary<string, Task> _idle = new(WorktreeKey.Comparer);
    private readonly Dictionary<string, RepositoryViewModel> _waitedFor = new(WorktreeKey.Comparer);
    private bool _disposed;

    /// <param name="services">What a summary-mode view model is made with (<see cref="RepositoryViewModel.ForSummary"/>).</param>
    public OpenRepositories(TabServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <summary>The summary-mode view models held by dashboards.</summary>
    public IReadOnlyList<RepositoryViewModel> Summaries => [.. _summaries.Values.Select(summary => summary.ViewModel)];

    /// <summary>The summary-mode view models released while an operation of the user ran, already disposed (3.7).</summary>
    public IReadOnlyList<RepositoryViewModel> Closing => [.. _closing];

    /// <summary>The tabs' view models in registration order (3.7: the window waits for them as for its tabs').</summary>
    public IReadOnlyList<RepositoryViewModel> Tabs => [.. _tabs];

    /// <summary>Raised with the worktree's key when a tab registered or unregistered (an adopted summary's tab included, 3.5, 3.6); it starts no read.</summary>
    public event EventHandler<string>? Changed;

    /// <summary>The worktree key of <paramref name="repository"/>'s view model.</summary>
    public static string KeyOf(RepositoryViewModel repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return WorktreeKey.Of(repository.Repository.GitDirectory);
    }

    /// <summary>Registers a tab's full view model by its worktree, after the ones registered before it.</summary>
    public void RegisterTab(RepositoryViewModel repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (_tabs.Contains(repository))
        {
            return;
        }

        _tabs.Add(repository);
        Changed?.Invoke(this, KeyOf(repository));
    }

    /// <summary>
    /// Unregisters a tab's view model (its tab replaced it or closed), keeping its queue's idle task
    /// for the worktree until it completes, so that no tab and no summary is made on the worktree
    /// while that queue still runs (3.3, 3.6).
    /// </summary>
    public void UnregisterTab(RepositoryViewModel repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (!_tabs.Remove(repository))
        {
            return;
        }

        var key = KeyOf(repository);
        KeepUntilIdle(key, repository, closing: false);
        Changed?.Invoke(this, key);
    }

    /// <summary>The first registered tab view model still open for the worktree <paramref name="key"/>, or null.</summary>
    public RepositoryViewModel? TabFor(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _tabs.FirstOrDefault(tab => !tab.IsClosed && WorktreeKey.Comparer.Equals(KeyOf(tab), key));
    }

    /// <summary>
    /// The first registered tab view model still open whose working tree is <paramref name="folder"/>
    /// (<see cref="WorkspacePaths.Same"/>, 1.2), or null: a row finds its tab before it opens its folder.
    /// </summary>
    public RepositoryViewModel? TabForFolder(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return _tabs.FirstOrDefault(tab => !tab.IsClosed && WorkspacePaths.Same(tab.Path, folder));
    }

    /// <summary>The summary-mode view model held for the worktree <paramref name="key"/>, or null.</summary>
    public RepositoryViewModel? SummaryFor(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _summaries.TryGetValue(key, out var summary) ? summary.ViewModel : null;
    }

    /// <summary>Whether <paramref name="holder"/> holds <paramref name="repository"/> as a summary.</summary>
    public bool Holds(RepositoryViewModel repository, object holder)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(holder);
        return _summaries.TryGetValue(KeyOf(repository), out var summary)
            && ReferenceEquals(summary.ViewModel, repository)
            && summary.Holders.Contains(holder);
    }

    /// <summary>
    /// The worktree's kept idle task (a closed tab's or a released summary's queue that still runs),
    /// else a completed task: a tab's open (3.3), a row's read and <see cref="AcquireSummaryAsync"/>
    /// wait for it. It may have completed just before its removal ran: callers loop while it has not.
    /// </summary>
    public Task WhenIdleAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _idle.TryGetValue(key, out var idle) ? idle : Task.CompletedTask;
    }

    /// <summary>
    /// The closed view model (a closed tab's or a released summary's) whose queue still runs on the
    /// worktree <paramref name="key"/>, for a waiting tab's text (3.3, <c>Waiting for {title} to
    /// finish…</c>); null when none does.
    /// </summary>
    public RepositoryViewModel? ClosedRunningOn(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _waitedFor.TryGetValue(key, out var repository) ? repository : null;
    }

    /// <summary>
    /// The worktree's summary-mode view model, removed with its holders, for a tab to adopt (3.3):
    /// its queue, a running operation and its undo history carry over; or null. It raises no
    /// <see cref="Changed"/>: the adopting tab registers the view model next
    /// (<see cref="RegisterTab"/>), whose change the rows follow, so that no row sees the summary
    /// gone with no tab for it and lets go of what it keeps of it (3.5, 4.6: a run's kept dialog).
    /// </summary>
    public RepositoryViewModel? TryAdopt(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _summaries.Remove(key, out var summary) ? summary.ViewModel : null;
    }

    /// <summary>
    /// The summary-mode view model of <paramref name="repository"/>'s worktree with
    /// <paramref name="holder"/> (a dashboard) added, made by <see cref="RepositoryViewModel.ForSummary"/>
    /// when there is none: two dashboards share one view model and one queue. It first waits for a
    /// closed queue on the worktree (<see cref="WhenIdleAsync"/>) and looks for a tab again. It
    /// returns null, making and keeping nothing, when a tab holds the worktree (the caller follows
    /// it), when <paramref name="holderLifetime"/> was cancelled meanwhile (the dashboard closed, the
    /// row removed or its path replaced, 2.10), or once <see cref="DisposeAll"/> has run. The view
    /// model is not read here: the row starts its read (<see cref="RepositoryViewModel.StartAsync"/>).
    /// </summary>
    public async Task<RepositoryViewModel?> AcquireSummaryAsync(IGitRepository repository, object holder, CancellationToken holderLifetime)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(holder);
        var key = WorktreeKey.Of(repository.GitDirectory);

        // A kept idle task that has completed holds nothing: its removal is posted to the UI thread
        // and may not have run yet, and awaiting it again would never yield.
        while (!_disposed && !holderLifetime.IsCancellationRequested && _idle.TryGetValue(key, out var idle) && !idle.IsCompleted)
        {
            try
            {
                await idle.WaitAsync(holderLifetime);
            }
            catch (OperationCanceledException) when (holderLifetime.IsCancellationRequested)
            {
                return null;
            }
        }

        if (_disposed || holderLifetime.IsCancellationRequested || TabFor(key) is not null)
        {
            return null;
        }

        if (_summaries.TryGetValue(key, out var summary))
        {
            summary.Holders.Add(holder);
            return summary.ViewModel;
        }

        var created = RepositoryViewModel.ForSummary(repository, _services);
        _summaries[key] = new Summary(created, [holder]);
        return created;
    }

    /// <summary>
    /// Removes one hold of <paramref name="holder"/> on <paramref name="repository"/>. With no holder
    /// left the view model leaves <see cref="Summaries"/> and is disposed (3.2, D81: what waits is
    /// dropped, a running network operation cancelled, a running local one finishes); while an
    /// operation of the user still runs then, it stays in <see cref="Closing"/> and its queue's idle
    /// task is kept for the worktree until that completes, as a closed tab's is (3.7).
    /// </summary>
    public void ReleaseSummary(RepositoryViewModel repository, object holder)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(holder);
        var key = KeyOf(repository);
        if (!_summaries.TryGetValue(key, out var summary) || !ReferenceEquals(summary.ViewModel, repository) || !summary.Holders.Remove(holder))
        {
            return;
        }

        if (summary.Holders.Count > 0)
        {
            return;
        }

        _summaries.Remove(key);
        repository.Dispose();
        if (repository.Queue.Current is { IsBackground: false })
        {
            _closing.Add(repository);
            KeepUntilIdle(key, repository, closing: true);
        }
    }

    /// <summary>
    /// The first queue of a view model the registry holds (a tab's, a summary's, one in
    /// <see cref="Closing"/>) on the common git folder <paramref name="commonKey"/>
    /// (<c>WorktreeKey.Of(CommonDirectory)</c>), other than <paramref name="except"/>, whose running
    /// operation is a network one (background ones included); or null (4.3).
    /// </summary>
    public IOperationQueue? NetworkQueueOn(string commonKey, IOperationQueue? except)
    {
        ArgumentNullException.ThrowIfNull(commonKey);
        return _tabs
            .Concat(_summaries.Values.Select(summary => summary.ViewModel))
            .Concat(_closing)
            .Where(repository => WorktreeKey.Comparer.Equals(WorktreeKey.Of(repository.Repository.CommonDirectory), commonKey))
            .Select(repository => repository.Queue)
            .FirstOrDefault(queue => !ReferenceEquals(queue, except) && queue.Current is { Kind: OperationKind.Network });
    }

    /// <summary>Disposes every summary-mode view model (those in <see cref="Closing"/> are disposed already) and refuses every later acquisition (3.7).</summary>
    public void DisposeAll()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var summaries = _summaries.Values.Select(summary => summary.ViewModel).ToList();
        _summaries.Clear();
        foreach (var summary in summaries)
        {
            summary.Dispose();
        }
    }

    /// <summary>
    /// Keeps <paramref name="repository"/>'s queue's idle task for <paramref name="key"/> (with an
    /// earlier one still pending) until it completes; with <paramref name="closing"/> the view model
    /// stays in <see cref="Closing"/> as long.
    /// </summary>
    private void KeepUntilIdle(string key, RepositoryViewModel repository, bool closing)
    {
        var idle = repository.Queue.WhenIdleAsync();
        if (idle.IsCompleted)
        {
            if (closing)
            {
                _closing.Remove(repository);
            }

            return;
        }

        var kept = _idle.TryGetValue(key, out var earlier) ? Task.WhenAll(earlier, idle) : idle;
        _idle[key] = kept;
        _waitedFor[key] = repository;
        _ = ForgetWhenIdleAsync(key, kept, repository, closing);
    }

    private async Task ForgetWhenIdleAsync(string key, Task kept, RepositoryViewModel repository, bool closing)
    {
        try
        {
            await kept;
        }
        catch (Exception)
        {
            // An idle task never fails; whatever it did, the worktree is no longer held.
        }

        if (_idle.TryGetValue(key, out var current) && ReferenceEquals(current, kept))
        {
            _idle.Remove(key);
            _waitedFor.Remove(key);
        }

        if (closing)
        {
            _closing.Remove(repository);
        }
    }

    /// <summary>One summary-mode view model and the dashboards that hold it, one entry per hold.</summary>
    private sealed record Summary(RepositoryViewModel ViewModel, List<object> Holders);
}
