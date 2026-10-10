using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// One repository open in a tab (D45): its refs, its graph, its left panel and commit details,
/// its watcher, and everything that keeps them up to date. Closing the tab disposes it, which
/// stops all of its work.
/// </summary>
public sealed partial class RepositoryViewModel : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SynchronizationContext? _ui;
    private readonly IAppLog _log;
    private readonly Stopwatch _sinceOpening;
    private readonly List<double> _frameTimes = [];
    private IRepositoryWatcher? _watcher;
    private string? _selectedSha;
    private string? _pendingSelection;
    private string? _fingerprint;
    private readonly Lock _refreshGate = new();
    private bool _firstRowsLogged;
    private Task? _refreshing;
    private bool _refreshAgain;
    private bool _restoringSelection;
    private bool _started;
    private bool _changedWhileStarting;
    private bool _disposed;

    /// <param name="repository">The repository.</param>
    /// <param name="settings">The settings, for the file list's flat or tree view.</param>
    /// <param name="dates">How dates are shown.</param>
    /// <param name="log">The app's log, which also gets the Q1 measurements (D50).</param>
    /// <param name="sinceOpening">Started when opening the repository began; the time to the first graph rows is measured from it.</param>
    public RepositoryViewModel(
        IGitRepository repository,
        ISettingsStore settings,
        DateDisplay dates,
        IAppLog? log = null,
        Stopwatch? sinceOpening = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dates);
        Repository = repository;
        Dates = dates;
        _log = log ?? NullAppLog.Instance;
        _sinceOpening = sinceOpening ?? Stopwatch.StartNew();
        _ui = SynchronizationContext.Current;

        LeftPanel = new LeftPanelViewModel();
        LeftPanel.RefActivated += (_, sha) => Select(sha, reveal: true);
        Details = new CommitDetailsViewModel(repository, settings, dates, _log);
        Details.ParentActivated += (_, sha) => Select(sha, reveal: true);
    }

    public IGitRepository Repository { get; }

    /// <summary>The name the tab shows.</summary>
    public string Name => Repository.Name;

    /// <summary>The working tree's folder.</summary>
    public string Path => Repository.WorkingDirectory;

    public DateDisplay Dates { get; }

    public LeftPanelViewModel LeftPanel { get; }

    public CommitDetailsViewModel Details { get; }

    /// <summary>The commits the graph draws. Replaced whole when a refresh loads the history again (D52).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoCommits))]
    public partial CommitGraphData Graph { get; private set; } = CommitGraphData.Empty;

    /// <summary>The selected row of the graph, or -1. The graph control binds to it both ways.</summary>
    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    /// <summary>The graph's vertical scroll offset, kept here so that each tab keeps its own.</summary>
    [ObservableProperty]
    public partial double ScrollOffset { get; set; }

    /// <summary>What the status bar shows: the branch HEAD is on, or "Detached at" and its short id.</summary>
    [ObservableProperty]
    public partial string BranchText { get; private set; } = string.Empty;

    /// <summary>Why the repository could not be read, or null.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>The history is loaded and has no commits: the graph area says "No commits yet".</summary>
    public bool HasNoCommits => Graph.IsComplete && Graph.Count == 0 && ErrorText is null;

    /// <summary>Raised when a row should be scrolled into the middle of the view: after a ref or a parent was activated.</summary>
    public event EventHandler<int>? RevealRequested;

    /// <summary>
    /// Reads the refs, loads the history page by page into <see cref="Graph"/> and starts
    /// watching for outside changes. Completes when the whole history is loaded. Failures are
    /// shown in <see cref="ErrorText"/> and logged, not thrown.
    /// </summary>
    public async Task StartAsync()
    {
        var cancellationToken = _lifetime.Token;
        try
        {
            // Watching starts first: a change made while the history loads (a commit in a
            // terminal, an editor's fetch) is caught and shown once the load is done.
            _watcher = Repository.CreateWatcher();
            _watcher.Changed += (_, _) => OnUiThread(RequestRefresh);
            _watcher.Start();

            var refs = await Repository.ReadRefsAsync(cancellationToken);
            ApplyRefs(refs);
            var data = new CommitGraphData(refs);
            Graph = data;
            await LoadAsync(data, cancellationToken);
            OnPropertyChanged(nameof(HasNoCommits));

            _started = true;
            if (_changedWhileStarting)
            {
                await RefreshAsync();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The tab was closed.
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: the repository could not be read.", ex);
            ErrorText = ex is GitException git ? git.StandardError.Trim() : ex.Message;
            OnPropertyChanged(nameof(HasNoCommits));
        }
    }

    /// <summary>
    /// Reads the refs again, after an outside change, and loads the history again when the refs
    /// changed what the graph shows (D52). The new graph replaces the old one once it is loaded,
    /// with the same commit selected. A call that comes while a refresh runs makes that refresh
    /// run once more and returns its task, so the caller always waits for a refresh that started
    /// after its call.
    /// </summary>
    public Task RefreshAsync()
    {
        lock (_refreshGate)
        {
            if (_refreshing is { IsCompleted: false })
            {
                _refreshAgain = true;
                return _refreshing;
            }

            _refreshing = RunRefreshesAsync();
            return _refreshing;
        }
    }

    private async Task RunRefreshesAsync()
    {
        var cancellationToken = _lifetime.Token;
        try
        {
            do
            {
                lock (_refreshGate)
                {
                    _refreshAgain = false;
                }

                var refs = await Repository.ReadRefsAsync(cancellationToken);
                if (refs.Fingerprint == _fingerprint)
                {
                    // Upstreams and their counts can change without moving a ref.
                    ApplyRefs(refs);
                    continue;
                }

                var data = new CommitGraphData(refs);
                await LoadAsync(data, cancellationToken);
                ApplyRefs(refs);
                SwapGraph(data);
            }
            while (RefreshRequestedAgain() && !cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: refreshing after an outside change failed.", ex);
        }
    }

    private bool RefreshRequestedAgain()
    {
        lock (_refreshGate)
        {
            return _refreshAgain;
        }
    }

    /// <summary>Selects the commit <paramref name="sha"/> in the graph; with <paramref name="reveal"/>, scrolls it into the middle of the view.</summary>
    public void Select(string sha, bool reveal)
    {
        var index = Graph.IndexOf(sha);
        if (index < 0)
        {
            // Not loaded yet: select it when it arrives.
            _pendingSelection = Graph.IsComplete ? null : sha;
            return;
        }

        SelectedIndex = index;
        if (reveal)
        {
            RevealRequested?.Invoke(this, index);
        }
    }

    /// <summary>The graph control drew a frame that showed rows: logs the time since opening began, once (D50).</summary>
    public void OnRowsDrawn()
    {
        if (_firstRowsLogged || Graph.Count == 0)
        {
            return;
        }

        _firstRowsLogged = true;
        _log.Info(string.Create(CultureInfo.InvariantCulture, $"{Name}: first graph rows drawn after {_sinceOpening.ElapsedMilliseconds} ms"));
    }

    /// <summary>The graph control drew a frame in <paramref name="duration"/>: kept for the statistics logged when the tab closes (D50).</summary>
    public void OnFrameDrawn(TimeSpan duration) => _frameTimes.Add(duration.TotalMilliseconds);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _watcher?.Dispose();
        LogFrameStatistics();
        _lifetime.Dispose();
    }

    partial void OnSelectedIndexChanged(int value)
    {
        var sha = value >= 0 && value < Graph.Count ? Graph.CommitAt(value).Sha : null;
        if (sha == _selectedSha)
        {
            return;
        }

        _selectedSha = sha;
        if (!_restoringSelection)
        {
            var stash = sha is null ? null : Graph.Refs.Stashes.FirstOrDefault(entry => entry.Sha == sha);
            _ = Details.ShowAsync(sha, stash, _lifetime.Token);
        }
    }

    private async Task LoadAsync(CommitGraphData data, CancellationToken cancellationToken)
    {
        var layout = new GraphLayout();
        var stopwatch = Stopwatch.StartNew();
        await Repository.LoadCommitsAsync(
            data.Refs,
            page =>
            {
                // On a thread-pool thread: lay the page out here, add it on the UI thread.
                var rows = new GraphRow[page.Count];
                for (var i = 0; i < page.Count; i++)
                {
                    rows[i] = layout.Add(page[i]);
                }

                OnUiThread(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        data.Append(page, rows);
                        ApplyPendingSelection(data);
                    }
                });
            },
            cancellationToken);

        // The pages were posted in order; completing through the same queue keeps it last.
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OnUiThread(() =>
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                data.Complete();
                ApplyPendingSelection(data);
            }

            completed.TrySetResult();
        });
        await completed.Task;
        cancellationToken.ThrowIfCancellationRequested();

        _log.Info(string.Create(CultureInfo.InvariantCulture, $"{Name}: loaded {data.Count} commits in {stopwatch.ElapsedMilliseconds} ms"));
    }

    private void ApplyRefs(RepoRefs refs)
    {
        _fingerprint = refs.Fingerprint;
        BranchText = refs.Head switch
        {
            { BranchName: { } branch } => branch,
            { Sha: { } sha } => $"Detached at {sha[..Math.Min(7, sha.Length)]}",
            _ => string.Empty,
        };
        LeftPanel.Update(refs);
    }

    private void SwapGraph(CommitGraphData data)
    {
        var selected = _selectedSha;
        _restoringSelection = true;
        try
        {
            Graph = data;
            SelectedIndex = selected is null ? -1 : data.IndexOf(selected);
        }
        finally
        {
            _restoringSelection = false;
        }

        if (SelectedIndex < 0 && selected is not null)
        {
            // The selected commit is gone (a reset, a deleted branch): show the placeholder.
            _selectedSha = null;
            _ = Details.ShowAsync(null, null, _lifetime.Token);
        }
    }

    private void ApplyPendingSelection(CommitGraphData data)
    {
        if (_pendingSelection is null || !ReferenceEquals(data, Graph))
        {
            return;
        }

        var sha = _pendingSelection;
        if (data.IndexOf(sha) >= 0 || data.IsComplete)
        {
            _pendingSelection = null;
            Select(sha, reveal: true);
        }
    }

    private void RequestRefresh()
    {
        if (_disposed)
        {
            return;
        }

        if (!_started)
        {
            _changedWhileStarting = true;
            return;
        }

        _ = RefreshAsync();
    }

    private void OnUiThread(Action action)
    {
        if (_ui is null)
        {
            action();
        }
        else
        {
            _ui.Post(_ => action(), null);
        }
    }

    private void LogFrameStatistics()
    {
        if (_frameTimes.Count == 0)
        {
            return;
        }

        var sorted = _frameTimes.Order().ToList();
        var p95 = sorted[(int)Math.Ceiling(sorted.Count * 0.95) - 1];
        _log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"{Name}: graph frames: {sorted.Count} drawn, 95th percentile {p95:F1} ms, longest {sorted[^1]:F1} ms"));
    }
}
