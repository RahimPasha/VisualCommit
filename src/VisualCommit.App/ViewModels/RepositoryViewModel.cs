using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Settings;
using VisualCommit.Core.Undo;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// One repository open in a tab (D45): its refs, its graph, its left panel and commit details,
/// its working-tree status with the stage panel and the open diff, its watcher, and everything
/// that keeps them up to date. Every write the app makes to the repository runs through its
/// operation queue (<see cref="Queue"/>, D81), with the watcher paused (D71). Closing the tab
/// disposes it, which stops all of its work but a local operation that has started (1.8).
/// </summary>
public sealed partial class RepositoryViewModel : ObservableObject, IDisposable, IWorkingChangesHost, IDiffHost
{
    /// <summary>The height of a graph row: the working-changes row moves the rows below it by this much.</summary>
    private const double GraphRowHeight = 26;

    private const string SnapshotNote = "A snapshot is saved first, so the changes can be restored.";

    private readonly CancellationTokenSource _lifetime = new();
    private readonly SynchronizationContext? _ui;
    private readonly IAppLog _log;
    private readonly ISettingsStore _settings;
    private readonly IDialogService? _dialogs;
    private readonly TabServices? _services;
    private readonly TaskCompletionSource _firstRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _watcherGate = new();
    private Stopwatch _sinceOpening;
    private readonly List<double> _frameTimes = [];
    private readonly List<double> _loadingFrameTimes = [];
    private IRepositoryWatcher? _watcher;
    private string? _selectedSha;
    private string? _pendingSelection;
    private string? _fingerprint;
    private string? _headSha;
    private readonly Lock _refreshGate = new();
    private readonly Lock _statusGate = new();
    private readonly OperationQueue _queue;
    private bool _firstRowsLogged;
    private bool _syncingSelection;
    private Task? _refreshing;
    private bool _refreshAgain;
    private Task? _statusRefreshing;
    private bool _statusAgain;
    private bool _restoringSelection;
    private bool _started;
    private bool _changedWhileStarting;
    private bool _disposed;

    // Summary mode and promotion (docs/phase-6-design.md 3.1 to 3.3).
    private bool _promoted;
    private int _readGeneration;
    private Task? _summaryStart;

    // The watcher's pause belongs to the view model (3.3): the queue's operation pauses it once,
    // also a watcher made while that operation runs.
    private int _operationPauses;
    private IDisposable? _watcherPause;

    /// <summary>The watcher's Changed events posted to the UI thread whose refresh has not been asked for yet.</summary>
    private int _changesPosted;
    private DiscardSnapshot? _lastSnapshot;
    private Dictionary<string, string?>? _afterDiscard;

    /// <param name="repository">The repository.</param>
    /// <param name="settings">The settings: the file list's flat or tree view, the diff mode.</param>
    /// <param name="dates">How dates are shown.</param>
    /// <param name="log">The app's log, which also gets the Q1 measurements (D50).</param>
    /// <param name="sinceOpening">Started when opening the repository began; the time to the first graph rows is measured from it.</param>
    /// <param name="dialogs">Where a discard asks for confirmation (D62, D69). Without it, discards go ahead unasked: tests only.</param>
    /// <param name="services">
    /// The app's services for phase 3's operations: the operation log and the prompt channel the
    /// queue uses, and what <see cref="Operations"/> needs. Null in view-model tests that need none.
    /// </param>
    public RepositoryViewModel(
        IGitRepository repository,
        ISettingsStore settings,
        DateDisplay dates,
        IAppLog? log = null,
        Stopwatch? sinceOpening = null,
        IDialogService? dialogs = null,
        TabServices? services = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dates);
        Repository = repository;
        Dates = dates;
        _settings = settings;
        _dialogs = dialogs;
        _services = services;
        _log = log ?? NullAppLog.Instance;
        _sinceOpening = sinceOpening ?? Stopwatch.StartNew();
        _ui = SynchronizationContext.Current;

        // Every write to the repository runs through its queue, one at a time (D76, D81), with the
        // watcher paused while it runs (D71; docs/phase-6-design.md 3.3: PauseWatcher).
        _queue = new OperationQueue(
            repository.Name,
            repository.GitDirectory,
            PauseWatcher,
            scope => scope.HasFlag(RefreshScope.Refs) ? RefreshAsync()
                : scope.HasFlag(RefreshScope.Status) ? RefreshStatusAsync()
                : Task.CompletedTask,
            services?.Prompts,
            services?.Operations,
            _log,
            since => LockFiles.FindSince([repository.GitDirectory, repository.CommonDirectory], since),
            services?.RecordClock);
        OperationStatus = new OperationStatusViewModel(_queue, services?.DialogHost.Notices);
        Operations = new RepositoryOperations(this, _queue, services, _log);
        Toolbar = new ToolbarViewModel(this);

        // The session's history of this worktree, which a tab on the same worktree shares and
        // which outlives the tab (docs/phase-5-design.md 7.1); a history of its own without services.
        UndoHistory = services?.UndoHistories.For(repository.GitDirectory, repository.CommonDirectory) ?? new UndoHistory();

        LeftPanel = new LeftPanelViewModel();
        LeftPanel.RefActivated += (_, sha) => Select(sha, reveal: true);
        ConnectLeftPanelFilters();
        Details = new CommitDetailsViewModel(repository, settings, dates, _log);
        Details.ParentActivated += (_, sha) => Select(sha, reveal: true);
        Details.FileActivated += (_, file) => OpenCommitDiff(file);

        // "All files": a file the shown commit did not change opens its blame (docs/phase-5-design.md 4.5).
        Details.BlameRequested += (_, request) => _ = MenuActions.ShowBlameAsync(request);

        Changes = new WorkingChangesViewModel(this, settings);

        // A save of git's settings tells every open view model (docs/phase-6-design.md 8.11); the
        // subscription goes in Dispose, so that the session-long services keep no closed one alive.
        if (services is not null)
        {
            services.GitSettingsChanged += OnGitSettingsChanged;
        }
    }

    /// <summary>
    /// A dashboard row's view model in summary mode (docs/phase-6-design.md 3.1, D209): the usual
    /// view model with its queue, operations, toolbar, status and undo history, whose
    /// <see cref="StartAsync"/> reads only the refs and the status, without a watcher, a history, the
    /// session's graph filter, the clean-up at opening or Q1's lines, until a tab adopts it
    /// (<see cref="Promote"/>).
    /// </summary>
    public static RepositoryViewModel ForSummary(IGitRepository repository, TabServices services)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(services);
        return new RepositoryViewModel(repository, services.Settings, services.Dates, services.Log, null, services.Dialogs, services)
        {
            IsSummary = true,
        };
    }

    /// <summary>The view model is in summary mode (3.1): true from <see cref="ForSummary"/> until <see cref="Promote"/>.</summary>
    public bool IsSummary { get; private set; }

    /// <summary>
    /// Completes once <see cref="StartAsync"/> has applied both the refs and the status, or has
    /// failed (<see cref="ErrorText"/> set; in full mode a failed status read sets
    /// <see cref="StatusErrorText"/>), in either mode (3.2); also when the view model closes,
    /// so that nothing waits for it for good. A summary read that a promotion discards does not
    /// complete it: the full start does. A dashboard row that follows a tab counts as not read until
    /// then, and a bulk run waits for it (2.10, 4.2).
    /// </summary>
    public Task FirstRead => _firstRead.Task;

    /// <summary><see cref="FirstRead"/> has completed; raised in <c>PropertyChanged</c> when it does.</summary>
    public bool HasFirstRead => _firstRead.Task.IsCompleted;

    /// <summary>
    /// A tab adopts this summary-mode view model (3.3): it leaves summary mode, a summary read still
    /// running applies nothing when it ends (the read generation), and <paramref name="sinceOpening"/>,
    /// the tab's stopwatch, becomes the opening's, so that Q1's lines and auto-fetch's first interval
    /// count from the user's open. Then the tab calls <see cref="StartAsync"/>, which runs in full
    /// mode and puts off a refresh asked for during its first load until its end. No I/O.
    /// </summary>
    public void Promote(Stopwatch sinceOpening)
    {
        ArgumentNullException.ThrowIfNull(sinceOpening);
        Debug.Assert(!_started, "Summary mode never sets _started (3.1).");
        IsSummary = false;
        _promoted = true;
        _sinceOpening = sinceOpening;
        _readGeneration++;

        // A summary refresh still running ends without applying anything; the next refresh, put
        // off until the full start ends, starts a run of its own.
        lock (_refreshGate)
        {
            _refreshing = null;
            _refreshAgain = false;
        }

        lock (_statusGate)
        {
            _statusRefreshing = null;
            _statusAgain = false;
        }
    }

    /// <summary>
    /// The queue's watcher pause (D71, 3.3): the first operation's pause takes the watcher's, under
    /// a lock, and the handle gives it back when the count is at 0 again. A watcher the full start
    /// makes while an adopted operation runs is paused from its creation until that run ends.
    /// </summary>
    private IDisposable PauseWatcher()
    {
        lock (_watcherGate)
        {
            _operationPauses++;
            if (_operationPauses == 1)
            {
                _watcherPause = _watcher?.Pause();
            }
        }

        return new WatcherPause(this);
    }

    private void ResumeWatcher()
    {
        IDisposable? pause = null;
        lock (_watcherGate)
        {
            _operationPauses--;
            if (_operationPauses == 0)
            {
                (pause, _watcherPause) = (_watcherPause, null);
            }
        }

        pause?.Dispose();
    }

    /// <summary>Sets the watcher the full start made, paused at once while an operation's pause is out (3.3).</summary>
    private void UseWatcher(IRepositoryWatcher watcher)
    {
        lock (_watcherGate)
        {
            _watcher = watcher;
            if (_operationPauses > 0 && _watcherPause is null)
            {
                _watcherPause = watcher.Pause();
            }
        }
    }

    private void CompleteFirstRead()
    {
        if (_firstRead.TrySetResult())
        {
            OnPropertyChanged(nameof(HasFirstRead));
        }
    }

    /// <summary>One operation's watcher pause; disposing it twice gives it back once.</summary>
    private sealed class WatcherPause(RepositoryViewModel owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.ResumeWatcher();
            }
        }
    }

    public IGitRepository Repository { get; }

    /// <summary>The repository's operation queue (docs/phase-3-design.md 1, D81).</summary>
    public IOperationQueue Queue => _queue;

    /// <summary>
    /// Whether nothing the watcher saw is still on its way to the screen (D52): no change waits for
    /// the watcher's quiet period or is being raised (<see cref="IRepositoryWatcher.IsQuiet"/>), none
    /// waits for the UI thread, and no refresh or status read runs, whoever asked for it. A change
    /// the system has not reported yet is not known: macOS reports a write a while after it, so its
    /// refresh can still start later. The harnesses wait for it before they mark the git calls of a
    /// check's next step, so that a refresh's calls never straddle the mark.
    /// </summary>
    public bool IsWatcherQuiet =>
        (_watcher?.IsQuiet ?? true)
        && Volatile.Read(ref _changesPosted) == 0
        && Volatile.Read(ref _refreshing) is not { IsCompleted: false }
        && Volatile.Read(ref _statusRefreshing) is not { IsCompleted: false };

    /// <summary>What the status bar shows of the operations (4e).</summary>
    public OperationStatusViewModel OperationStatus { get; }

    /// <summary>Builds and starts the operations of section 2 (WP7 fills it).</summary>
    public RepositoryOperations Operations { get; }

    /// <summary>The toolbar's buttons for this repository (4d).</summary>
    public ToolbarViewModel Toolbar { get; }

    /// <summary>The undo history of the repository's worktree (docs/phase-5-design.md 7.1): what the toolbar's Undo and Redo step through.</summary>
    public UndoHistory UndoHistory { get; }

    /// <summary>
    /// The commits selected in the graph, as SHAs (4h, D88). One selected commit is also
    /// <see cref="SelectedIndex"/>; several leave <see cref="SelectedIndex"/> at -1. The graph
    /// control binds to it both ways.
    /// </summary>
    [ObservableProperty]
    public partial GraphSelection Selection { get; set; } = GraphSelection.Empty;

    /// <summary>
    /// What the details panel's slot shows instead of one commit's details (4h): the list of three
    /// or more selected commits, or a comparison (docs/phase-5-design.md 6: two selected commits,
    /// or branches from their menus, <see cref="Comparison"/>); null otherwise.
    /// </summary>
    [ObservableProperty]
    public partial object? MultiSelection { get; private set; }

    /// <summary>What the working-changes row says (D60); WP9 adds the count of conflicts (section 5).</summary>
    [ObservableProperty]
    public partial string WorkingRowText { get; private set; } = "Working changes";

    /// <summary>The name the tab shows.</summary>
    public string Name => Repository.Name;

    /// <summary>The working tree's folder.</summary>
    public string Path => Repository.WorkingDirectory;

    public DateDisplay Dates { get; }

    public LeftPanelViewModel LeftPanel { get; }

    public CommitDetailsViewModel Details { get; }

    /// <summary>The stage panel, shown while the working-changes row is selected.</summary>
    public WorkingChangesViewModel Changes { get; }

    /// <summary>The commits the graph draws. Replaced whole when a refresh loads the history again (D52).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoCommits))]
    public partial CommitGraphData Graph { get; private set; } = CommitGraphData.Empty;

    /// <summary>The selected commit's row of the graph, or -1. The graph control binds to it both ways.</summary>
    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    /// <summary>The working-changes row is selected (and <see cref="SelectedIndex"/> is -1). The graph control binds to it both ways.</summary>
    [ObservableProperty]
    public partial bool IsWorkingRowSelected { get; set; }

    /// <summary>The graph's vertical scroll offset, kept here so that each tab keeps its own.</summary>
    [ObservableProperty]
    public partial double ScrollOffset { get; set; }

    /// <summary>What the status bar shows: the branch HEAD is on, or "Detached at" and its short id.</summary>
    [ObservableProperty]
    public partial string BranchText { get; private set; } = string.Empty;

    /// <summary>
    /// The refs as last read: HEAD, the branches with their upstreams, the stashes and the
    /// remotes; null before the first read. Set by every refresh, also when only an upstream or
    /// its counts changed (which <see cref="Graph"/>'s refs do not follow). The toolbar's states
    /// follow it (docs/phase-3-design.md 4d).
    /// </summary>
    [ObservableProperty]
    public partial RepoRefs? Refs { get; private set; }

    /// <summary>Why the repository could not be read, or null.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>
    /// Why the full start's first status read failed, until a status read is applied (3.2): the
    /// tab shows its graph as before, and nothing of it binds this; a dashboard row that follows the
    /// tab shows <c>Git could not open it</c> with it, so that the row never shows a status that was
    /// never read and a bulk run leaves it out. Null otherwise (summary mode fails in
    /// <see cref="ErrorText"/>).
    /// </summary>
    [ObservableProperty]
    public partial string? StatusErrorText { get; private set; }

    /// <summary>The staged, unstaged and untracked files, as last read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWorkingChanges))]
    public partial WorkingTreeStatus Status { get; private set; } = WorkingTreeStatus.Clean;

    /// <summary>The graph shows the working-changes row (D60).</summary>
    public bool HasWorkingChanges => Status.HasChanges;

    /// <summary>The diff open in place of the graph, or null (D63).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDiffOpen))]
    [NotifyPropertyChangedFor(nameof(ShowsGraph))]
    [NotifyPropertyChangedFor(nameof(ShowsInteractiveRebase))]
    [NotifyPropertyChangedFor(nameof(ShowsPage))]
    [NotifyPropertyChangedFor(nameof(ShowsDiff))]
    public partial DiffViewModel? Diff { get; private set; }

    public bool IsDiffOpen => Diff is not null;

    /// <summary>
    /// The history is loaded and has no commits: the graph area says "No commits yet". Not while
    /// the refs are still being read: the empty graph shown until then is no history yet.
    /// </summary>
    public bool HasNoCommits =>
        !ReferenceEquals(Graph, CommitGraphData.Empty) && Graph.IsComplete && Graph.Count == 0 && ErrorText is null;

    /// <summary>HEAD has a commit.</summary>
    public bool HasHead => _headSha is not null;

    /// <summary>Raised when a row should be scrolled into the middle of the view: after a ref or a parent was activated.</summary>
    public event EventHandler<int>? RevealRequested;

    /// <summary>
    /// Reads the refs and the working-tree status, loads the history page by page into
    /// <see cref="Graph"/> and starts watching for outside changes. Completes when the whole
    /// history is loaded. Failures are shown in <see cref="ErrorText"/> and logged, not thrown.
    /// </summary>
    public Task StartAsync()
    {
        if (IsSummary)
        {
            // Summary mode (3.1): one read of the refs and the status, shared by every dashboard
            // that holds the view model.
            return _summaryStart ??= StartSummaryAsync();
        }

        return StartFullAsync();
    }

    private async Task StartFullAsync()
    {
        var cancellationToken = _lifetime.Token;
        try
        {
            // Watching starts first: a change made while the history loads (a commit in a
            // terminal, an editor's save) is caught and shown once the load is done. It starts
            // off the UI thread: on Linux it walks every folder of the working tree.
            var watcher = Repository.CreateWatcher();
            UseWatcher(watcher);
            watcher.Changed += (_, e) =>
            {
                // Counted until its refresh has been asked for on the UI thread (IsWatcherQuiet).
                Interlocked.Increment(ref _changesPosted);
                OnUiThread(() =>
                {
                    try
                    {
                        OnRepositoryChanged(e);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _changesPosted);
                    }
                });
            };
            var watching = Task.Run(watcher.Start, CancellationToken.None);

            // The refs, the history and the status are read side by side: git log does not need
            // the refs, and starting it at once brings the first graph forward by the time they
            // take (Q1). The status is small next to them.
            _log.Debug(string.Create(CultureInfo.InvariantCulture, $"{Name}: opened after {_sinceOpening.ElapsedMilliseconds} ms"));
            var reading = Repository.ReadRefsAsync(cancellationToken);
            var status = Repository.ReadStatusAsync(cancellationToken);

            // What the graph hides or solos comes from the session, so that git starts before the
            // refs are read, as without a filter (docs/phase-5-design.md 8.2, 8.4).
            var filter = LoadSessionFilter();
            var data = new Lazy<CommitGraphData>(() => new CommitGraphData(reading.Result, filter), LazyThreadSafetyMode.ExecutionAndPublication);
            var loading = LoadAsync(reading, data, filter, cancellationToken);

            RepoRefs refs;
            try
            {
                refs = await reading;
            }
            catch
            {
                // The load fails with the same error; it is observed here so it is not reported twice.
                _ = loading.ContinueWith(failed => _ = failed.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                _ = status.ContinueWith(failed => _ = failed.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                throw;
            }

            _log.Debug(string.Create(CultureInfo.InvariantCulture, $"{Name}: refs read after {_sinceOpening.ElapsedMilliseconds} ms"));
            if (CorrectFilters(refs, firstRead: true))
            {
                // Names that no longer exist went from the filter: the graph loads again once loaded.
                _changedWhileStarting = true;
            }

            ApplyRefs(refs);
            _loadedFilterKey = filter?.Key;
            Graph = data.Value;
            var statusShown = ShowStatusAsync(status, firstRead: true);
            await loading;
            await statusShown;
            await watching;
            OnPropertyChanged(nameof(HasNoCommits));

            // A promoted view model put off the refreshes asked for during its first load (3.3).
            _promoted = false;
            _started = true;
            OnFirstLoadCompleted();
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
            _promoted = false;
            CompleteFirstRead();
        }
    }

    /// <summary>
    /// Summary mode's start (3.1): the refs and the status read side by side with the view model's
    /// lifetime, applied as a tab applies them but for what nothing shows (the left panel, the
    /// stage panel, the resolver), then <see cref="FirstRead"/>; a failure is shown in
    /// <see cref="ErrorText"/>, as in full mode. A promotion meanwhile (the read generation) makes
    /// it apply nothing and leave <see cref="FirstRead"/> to the full start.
    /// </summary>
    private async Task StartSummaryAsync()
    {
        var cancellationToken = _lifetime.Token;
        var generation = _readGeneration;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var reading = Repository.ReadRefsAsync(cancellationToken);
            var status = Repository.ReadStatusAsync(cancellationToken);
            RepoRefs refs;
            try
            {
                refs = await reading;
            }
            catch
            {
                _ = status.ContinueWith(failed => _ = failed.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                throw;
            }

            if (generation != _readGeneration || _disposed)
            {
                _ = status.ContinueWith(failed => _ = failed.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                return;
            }

            ApplyRefs(refs);
            var read = await status;
            if (generation != _readGeneration || _disposed)
            {
                return;
            }

            ApplyStatus(read);
            CompleteFirstRead();
            _log.Debug(DashboardTexts.SummaryReadLine(Name, stopwatch.ElapsedMilliseconds));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Released, or the window closed.
        }
        catch (Exception ex)
        {
            if (generation != _readGeneration || _disposed)
            {
                return;
            }

            _log.Error($"{Name}: the repository could not be read.", ex);
            ErrorText = ex is GitException git ? git.StandardError.Trim() : ex.Message;
            CompleteFirstRead();
        }
    }

    /// <summary>
    /// Reads the refs again, after an outside change or a commit, and loads the history again when
    /// the refs changed what the graph shows (D52); then reads the working-tree status. The new
    /// graph replaces the old one once it is loaded, with the same commit selected. A call that
    /// comes while a refresh runs makes that refresh run once more and returns its task, so the
    /// caller always waits for a refresh that started after its call.
    /// </summary>
    public Task RefreshAsync()
    {
        // A promoted view model puts off every refresh asked for during its first load (the
        // adopted operation's end, a watcher's event) until that load's end, where a refresh here
        // would load a second history beside the first (docs/phase-6-design.md 3.3).
        if (_promoted)
        {
            _changedWhileStarting = true;
            return Task.CompletedTask;
        }

        lock (_refreshGate)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            if (_refreshing is { IsCompleted: false })
            {
                _refreshAgain = true;
                return _refreshing;
            }

            _refreshing = RunRefreshesAsync();
            return _refreshing;
        }
    }

    /// <summary>
    /// Reads the working-tree status again, and the open diff: after a change in the working tree
    /// or a write of the app's (D71). Calls that come while one runs are folded into one more run,
    /// as <see cref="RefreshAsync"/> does.
    /// </summary>
    public Task RefreshStatusAsync()
    {
        // As RefreshAsync: put off during a promoted view model's first load, whose refresh at its
        // end reads the status too (3.3).
        if (_promoted)
        {
            _changedWhileStarting = true;
            return Task.CompletedTask;
        }

        lock (_statusGate)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            if (_statusRefreshing is { IsCompleted: false })
            {
                _statusAgain = true;
                return _statusRefreshing;
            }

            _statusRefreshing = RunStatusRefreshesAsync();
            return _statusRefreshing;
        }
    }

    private async Task RunRefreshesAsync()
    {
        if (IsSummary)
        {
            await RunSummaryRefreshesAsync();
            return;
        }

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

                // A filter's change joins the refs' in the comparison (docs/phase-5-design.md 8.2):
                // the new graph loads off screen and is swapped in when complete.
                CorrectFilters(refs, firstRead: false);
                var filter = GraphFilter;
                if (refs.Fingerprint == _fingerprint && filter?.Key == _loadedFilterKey)
                {
                    // Upstreams and their counts can change without moving a ref.
                    ApplyRefs(refs);
                }
                else
                {
                    var data = new CommitGraphData(refs, filter);
                    await LoadAsync(Task.FromResult(refs), new Lazy<CommitGraphData>(data), filter, cancellationToken);
                    ApplyRefs(refs);
                    _loadedFilterKey = filter?.Key;
                    SwapGraph(data);
                }

                IsReloadingGraph = GraphFilter?.Key != _loadedFilterKey;

                // The index lives in the git folder: a change there may be a change of the status.
                await RefreshStatusAsync();
            }
            while (RefreshRequestedAgain() && !cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: refreshing after an outside change failed.", ex);
            IsReloadingGraph = false;
        }
    }

    /// <summary>
    /// Summary mode's refresh (3.2): the refs and, side by side, the status, applied as
    /// <see cref="StartSummaryAsync"/> applies them, never a history; calls during a run fold into
    /// one more, as in full mode. A promotion meanwhile ends it without applying anything.
    /// </summary>
    private async Task RunSummaryRefreshesAsync()
    {
        var cancellationToken = _lifetime.Token;
        var generation = _readGeneration;
        try
        {
            do
            {
                lock (_refreshGate)
                {
                    _refreshAgain = false;
                }

                var stopwatch = Stopwatch.StartNew();
                var reading = Repository.ReadRefsAsync(cancellationToken);
                var status = Repository.ReadStatusAsync(cancellationToken);
                RepoRefs refs;
                WorkingTreeStatus read;
                try
                {
                    refs = await reading;
                }
                catch
                {
                    _ = status.ContinueWith(failed => _ = failed.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                    throw;
                }

                read = await status;
                if (generation != _readGeneration || _disposed)
                {
                    return;
                }

                ApplyRefs(refs);
                ApplyStatus(read);
                ErrorText = null;
                _log.Debug(DashboardTexts.SummaryReadLine(Name, stopwatch.ElapsedMilliseconds));
            }
            while (generation == _readGeneration && RefreshRequestedAgain() && !cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (generation != _readGeneration || _disposed)
            {
                return;
            }

            _log.Error($"{Name}: the repository could not be read.", ex);
            ErrorText = ex is GitException git ? git.StandardError.Trim() : ex.Message;
        }
    }

    private bool RefreshRequestedAgain()
    {
        lock (_refreshGate)
        {
            return _refreshAgain;
        }
    }

    private async Task RunStatusRefreshesAsync()
    {
        var cancellationToken = _lifetime.Token;
        var generation = _readGeneration;
        try
        {
            bool again;
            do
            {
                lock (_statusGate)
                {
                    _statusAgain = false;
                }

                var status = await Repository.ReadStatusAsync(cancellationToken);
                if (generation != _readGeneration)
                {
                    // A summary's read that a promotion overtook (3.3): the full start reads again.
                    return;
                }

                ApplyStatus(status);
                await ReloadDiffAsync();

                lock (_statusGate)
                {
                    again = _statusAgain;
                }
            }
            while (again && !cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: reading the working tree's status failed.", ex);
        }
    }

    /// <summary>Selects the commit <paramref name="sha"/> in the graph; with <paramref name="reveal"/>, scrolls it into the middle of the view. An open diff closes.</summary>
    public void Select(string sha, bool reveal)
    {
        CloseDiff(returnFocus: false);
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

    /// <summary>
    /// The graph control drew a frame in <paramref name="duration"/>: kept for the statistics
    /// logged when the tab closes (D50). D50 judges smoothness over scrolling, so frames drawn
    /// once the history has loaded are counted apart from those drawn while it loads (the first
    /// frame, and one per arriving page, while the loader also keeps the machine busy).
    /// </summary>
    public void OnFrameDrawn(TimeSpan duration) =>
        (Graph.IsComplete ? _frameTimes : _loadingFrameTimes).Add(duration.TotalMilliseconds);

    /// <summary>
    /// Opens a working-tree file's diff: unstaged or staged (D63). A conflicted file opens the
    /// conflict resolver instead (docs/phase-4-design.md 11.1, D148); while the interactive list or
    /// the resolver is open, they are left first (asking when changed).
    /// </summary>
    public void OpenDiff(ChangedFile file, bool staged)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!staged && file.Kind == FileChangeKind.Conflicted)
        {
            OpenResolver(file);
            return;
        }

        // A page is left too, as the list is (docs/phase-5-design.md 11.1).
        if (IsInteractiveRebaseOpen || IsResolverOpen || Page is not null)
        {
            _ = OpenDiffAfterLeavingAsync(file, staged);
            return;
        }

        ShowWorkingTreeDiff(file, staged);
    }

    private void ShowWorkingTreeDiff(ChangedFile file, bool staged)
    {
        var target = new DiffTarget(
            staged ? DiffSide.Staged : DiffSide.Unstaged,
            file.Path,
            file.Kind,
            staged ? file.OldPath : null,
            IsUntracked: !staged && Status.IsUntracked(file.Path));
        ShowDiff(target, staged ? "Staged" : "Unstaged");
        Details.SelectedFilePath = null;
        Changes.ShowSelection(file.Path, staged);
    }

    /// <summary>
    /// Closes the diff view and shows the graph again, or the interactive rebase's list or the page
    /// it was opened over, which takes the keyboard focus back (2.2, docs/phase-5-design.md 11.1);
    /// the file row loses its selection.
    /// </summary>
    public void CloseDiff() => CloseDiff(returnFocus: true);

    private void CloseDiff(bool returnFocus)
    {
        if (Diff is { } diff)
        {
            Diff = null;
            diff.Dispose();
            if (returnFocus)
            {
                InteractiveRebase?.RequestFocus();
                Page?.RequestFocus();
            }
        }

        Changes.ShowSelection(null, staged: false);
        Details.SelectedFilePath = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // The session-long services keep no closed view model alive, and no handler reads the
        // token of a disposed lifetime (docs/phase-6-design.md 8.11).
        if (_services is not null)
        {
            _services.GitSettingsChanged -= OnGitSettingsChanged;
        }

        // What waits is dropped and a network operation is stopped; a local one that has started
        // runs all its steps to the end, without the refresh (1.8).
        _queue.Dispose();
        OperationStatus.Dispose();
        _lifetime.Cancel();

        // Nothing waits for a first read that will never come (a dashboard row, a bulk run).
        CompleteFirstRead();
        _watcher?.Dispose();
        Diff?.Dispose();
        Page?.Dispose();
        _resolver?.Dispose();
        LogFrameStatistics();
        _lifetime.Dispose();
    }

    // The stage panel's and the diff view's writes (IWorkingChangesHost, IDiffHost).

    public Task<string?> StageAsync(IReadOnlyList<ChangedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        // Staging prepares a commit and is undone in the stage panel: not recorded (docs/phase-5-design.md 7.2).
        return files.Count == 0
            ? Task.FromResult<string?>(null)
            : WriteAsync("Stage", "Staging…", cancellationToken => Repository.StageAsync([.. files.Select(file => file.Path)], cancellationToken), UndoScope.None);
    }

    public Task<string?> UnstageAsync(IReadOnlyList<ChangedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        // A rename unstages both its paths: the new one leaves the index, the old one comes back.
        var paths = files.SelectMany(file => file.OldPath is { } old ? new[] { file.Path, old } : [file.Path]).Distinct().ToList();
        return paths.Count == 0
            ? Task.FromResult<string?>(null)
            : WriteAsync("Unstage", "Unstaging…", cancellationToken => Repository.UnstageAsync(paths, cancellationToken), UndoScope.None);
    }

    public async Task<string?> DiscardAsync(IReadOnlyList<ChangedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0)
        {
            return null;
        }

        // A conflicted file has no unstaged version to go back to: it is resolved, not discarded.
        // An untracked folder that git lists with a slash is a repository of its own: deleting
        // it would delete its history too.
        var leftOut = files.Where(file => file.Kind == FileChangeKind.Conflicted || file.Path.EndsWith('/')).ToList();
        var paths = files.Except(leftOut).Select(file => file.Path).ToList();
        if (paths.Count == 0)
        {
            Changes.ErrorText = leftOut.Count == 1
                ? leftOut[0].Kind == FileChangeKind.Conflicted
                    ? $"{DisplayName(leftOut[0])} has a conflict: resolve it before discarding."
                    : $"{DisplayName(leftOut[0])} is a repository of its own: it is not discarded."
                : "These files have conflicts or are repositories of their own: they are not discarded.";
            return Changes.ErrorText;
        }

        var question = paths.Count == 1
            ? $"Discard the changes to {WorkingChangesViewModel.FileName(paths[0])}?"
            : string.Create(CultureInfo.InvariantCulture, $"Discard the changes to {paths.Count} files?");
        if (leftOut.Count > 0)
        {
            question += leftOut.Count == 1
                ? $" {DisplayName(leftOut[0])} is left as it is."
                : string.Create(CultureInfo.InvariantCulture, $" {leftOut.Count} files that cannot be discarded are left as they are.");
        }

        if (!await ConfirmDiscardAsync(question))
        {
            return null;
        }

        var untracked = new HashSet<string>(paths.Where(Status.IsUntracked), StringComparer.Ordinal);
        return await WriteAsync(
            "Discard",
            "Discarding…",
            async cancellationToken =>
            {
                var snapshot = await Repository.SaveSnapshotAsync(paths, cancellationToken);
                UndoCapture.Snapshot(snapshot);
                await Repository.DiscardAsync(paths, untracked, cancellationToken);
                await RememberDiscardAsync(snapshot, cancellationToken);

                // The snapshot is a backup (design 9, D139): the clean-up follows, keeping it as
                // the restore bar's (the review's finding 22).
                await Operations.CleanUpBackupsAsync();
            },
            UndoScope.Discard(paths));
    }

    public async Task<string?> RestoreAsync()
    {
        if (_disposed || _lastSnapshot is not { } snapshot)
        {
            return null;
        }

        // While the discard is the entry before the undo history's cursor, Restore is its Undo
        // (docs/phase-5-design.md 7.9), which shows its own failure.
        if (await Operations.TryRestoreAsUndoAsync(snapshot))
        {
            return null;
        }

        // Work done on the files since the discard is not lost without a word: the user is asked,
        // and it goes into a snapshot of its own first (D75).
        var changed = await ChangedSinceDiscardAsync(_lifetime.Token);
        if (changed.Count > 0)
        {
            var question = changed.Count == 1
                ? $"{WorkingChangesViewModel.FileName(changed[0])} has changed since the discard. Put back the version from before the discard?"
                : string.Create(CultureInfo.InvariantCulture, $"{changed.Count} files have changed since the discard. Put back the versions from before the discard?");
            var request = new ConfirmationRequest("Restore discarded changes?", question, "A snapshot of the newer changes is saved first.", "Restore");
            if (!await AskAsync(request))
            {
                return null;
            }
        }

        // What is saved is decided inside the operation (docs/phase-5-design.md 15.9): a file
        // edited while the question was open, or while the restore waited in the queue, is saved
        // too, also one the question did not name.
        var backupSaved = new StrongBox<bool>();
        return await WriteAsync(
            "Restore",
            "Restoring…",
            async cancellationToken =>
            {
                var newer = await ChangedSinceDiscardAsync(cancellationToken);
                if (newer.Count > 0)
                {
                    // The newer changes' snapshot is a backup too (D139): the clean-up follows the
                    // recording, whose entry keeps the snapshot restored from (7.8).
                    await Repository.SaveSnapshotAsync(newer, cancellationToken);
                    backupSaved.Value = true;
                }

                await Repository.RestoreSnapshotAsync(snapshot, cancellationToken);
                UndoCapture.Snapshot(snapshot);
                _lastSnapshot = null;
                _afterDiscard = null;
                Changes.HideRestore();
            },
            UndoScope.Restore(snapshot.Paths),
            backupSaved: backupSaved);
    }

    public Task<string?> CommitAsync(string message, bool amend) => CommitAsync(message, amend, amended: null);

    /// <summary>HEAD as the refs were last read (the stage panel's ticked Amend is for it).</summary>
    public HeadState? Head => Refs?.Head;

    public async Task<string?> CommitAsync(string message, bool amend, HeadState? amended)
    {
        string? commit = null;
        var subject = message.Split('\n', 2)[0].TrimEnd('\r');
        var error = await WriteAsync(
            "Commit",
            "Committing…",
            async cancellationToken => commit = await Repository.CommitAsync(message, amend, cancellationToken),
            UndoScope.Commit(subject, amend, Refs?.Head.BranchName),
            refsChange: true,
            refuse: amend && amended is not null ? cancellationToken => RefuseStaleAmendAsync(amended, cancellationToken) : null);

        // With nothing left to commit, the working-changes row is gone: the new commit is selected.
        if (error is null && commit is not null && !HasWorkingChanges)
        {
            Select(commit, reveal: true);
        }

        return error;
    }

    public async Task<(string Subject, string Body)?> ReadHeadMessageAsync()
    {
        if (_headSha is not { } head)
        {
            return null;
        }

        try
        {
            var details = await Repository.ReadCommitDetailsAsync(head, _lifetime.Token);
            return (details.Subject, details.Body);
        }
        catch (Exception ex) when (ex is GitException or OperationCanceledException)
        {
            return null;
        }
    }

    public Task<FileDiff> ReadDiffAsync(DiffTarget target, CancellationToken cancellationToken) =>
        Repository.ReadDiffAsync(target, cancellationToken);

    /// <summary>The open diff's check of its working-tree file (5.5), on the pool: a file or, for an untracked folder, a folder.</summary>
    public Task<bool> ExistsInWorkingTreeAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        var full = System.IO.Path.Combine(Path, path.TrimEnd('/'));
        var fileExists = _services?.FileExists ?? File.Exists;
        return Task.Run(() => fileExists(full) || Directory.Exists(full), cancellationToken);
    }

    public Task<byte[]?> ReadFileAsync(FileVersion version, long maxBytes, CancellationToken cancellationToken) =>
        Repository.ReadFileAsync(version, maxBytes, cancellationToken);

    public Task<long?> ReadFileSizeAsync(FileVersion version, CancellationToken cancellationToken) =>
        Repository.ReadFileSizeAsync(version, cancellationToken);

    public async Task<string?> ApplyLinesAsync(DiffTarget target, FileDiff diff, IReadOnlySet<DiffLineRef> lines, LineAction action, bool wholeHunk)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(lines);
        var chosen = lines.Where(line => line.Hunk < diff.Hunks.Count && line.Line < diff.Hunks[line.Hunk].Lines.Count && diff.Hunks[line.Hunk].Lines[line.Line].IsChange).ToHashSet();
        if (chosen.Count == 0)
        {
            return null;
        }

        if (action == LineAction.Discard)
        {
            var name = WorkingChangesViewModel.FileName(target.Path);
            var question = wholeHunk ? $"Discard this hunk of {name}?"
                : chosen.Count == 1 ? $"Discard 1 selected line of {name}?"
                : string.Create(CultureInfo.InvariantCulture, $"Discard {chosen.Count} selected lines of {name}?");
            if (!await ConfirmDiscardAsync(question))
            {
                return null;
            }
        }

        return await WriteAsync(
            "Apply lines",
            "Applying…",
            async cancellationToken =>
            {
                // The whole file goes into a discard's snapshot, so Restore puts all of it back (D67).
                var snapshot = action == LineAction.Discard ? await Repository.SaveSnapshotAsync([target.Path], cancellationToken) : null;
                if (snapshot is not null)
                {
                    UndoCapture.Snapshot(snapshot);
                }

                if (snapshot is not null && target.IsUntracked && chosen.Count == diff.AddedCount)
                {
                    // Every line of an untracked file: the file goes, with the folders it leaves empty.
                    await Repository.DiscardAsync([target.Path], new HashSet<string>(StringComparer.Ordinal) { target.Path }, cancellationToken);
                }
                else
                {
                    var direction = action == LineAction.Stage ? PatchDirection.Forward : PatchDirection.Reverse;
                    var patch = PatchBuilder.Build(diff, chosen, direction);
                    if (patch is not null)
                    {
                        await Repository.ApplyPatchAsync(patch, toIndex: action != LineAction.Discard, direction, cancellationToken);
                    }
                }

                if (snapshot is not null)
                {
                    await RememberDiscardAsync(snapshot, cancellationToken);
                    await Operations.CleanUpBackupsAsync(); // As after a discard (D139).
                }
            },

            // A discard of lines is recorded; staging and unstaging them are not (docs/phase-5-design.md 7.2).
            action == LineAction.Discard ? UndoScope.DiscardLines(target.Path) : UndoScope.None);
    }

    partial void OnSelectedIndexChanged(int value)
    {
        if (value >= 0)
        {
            IsWorkingRowSelected = false;
        }

        var sha = value >= 0 && value < Graph.Count ? Graph.CommitAt(value).Sha : null;
        if (!_syncingSelection && (sha is null ? !Selection.IsMultiple : Selection.Single != sha))
        {
            // One selected row is the selection; -1 ends a single selection, not a multi-selection
            // (which keeps SelectedIndex at -1 itself).
            SetSelectionQuietly(sha is null ? GraphSelection.Empty : GraphSelection.Of(sha));
        }

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

    partial void OnSelectionChanged(GraphSelection value)
    {
        if (!_syncingSelection)
        {
            // A selection set from outside (the graph control's multi-select, WP8): exactly one
            // commit is also SelectedIndex, which phases 1 and 2 bind to; none or several leave it at -1.
            _syncingSelection = true;
            try
            {
                if (value.Single is { } sha)
                {
                    var index = Graph.IndexOf(sha);
                    if (index >= 0)
                    {
                        SelectedIndex = index;
                    }
                    else
                    {
                        _pendingSelection = Graph.IsComplete ? null : sha;
                    }
                }
                else if (SelectedIndex >= 0)
                {
                    SelectedIndex = -1;
                }
            }
            finally
            {
                _syncingSelection = false;
            }
        }

        OnGraphSelectionChanged(value);
    }

    /// <summary>
    /// Hook (docs/phase-3-design.md 6.3, section 8): the graph's selection changed. WP9 implements
    /// it in a partial file to make the left panel follow the single selected commit (D98).
    /// </summary>
    partial void OnGraphSelectionChanged(GraphSelection selection);

    /// <summary>
    /// Hook (4h, section 8): a reloaded history replaced <see cref="Graph"/>. WP8 implements it in a
    /// partial file to map a multi-selection onto the new graph by its SHAs.
    /// </summary>
    partial void OnGraphSwapped(CommitGraphData data);

    /// <summary>
    /// Hook (section 5, section 8): a newly read status was shown. WP9 implements it in a partial
    /// file for the conflict banner and the working-changes row's text.
    /// </summary>
    partial void OnStatusApplied(WorkingTreeStatus status);

    /// <summary>Sets <see cref="Selection"/> without mapping it back onto <see cref="SelectedIndex"/>, which it follows here.</summary>
    private void SetSelectionQuietly(GraphSelection selection)
    {
        _syncingSelection = true;
        try
        {
            Selection = selection;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    partial void OnIsWorkingRowSelectedChanged(bool value)
    {
        if (value)
        {
            _pendingSelection = null;
            SelectedIndex = -1;
            Changes.SyncFileList();
        }
    }

    /// <summary>
    /// Shows a newly read status. When the working-changes row appears or goes while the graph is
    /// scrolled away from the top, the offset moves with it so that the rows in view stay put (D60).
    /// </summary>
    private void ApplyStatus(WorkingTreeStatus status)
    {
        // A status read now: the first read's failure no longer holds (3.2).
        StatusErrorText = null;
        if (IsSummary)
        {
            // Summary mode (3.1): the status and what follows it that a row or an operation reads
            // (the working-changes row's text, the operations' notes, the banner); nothing shows
            // the stage panel, the resolver or the merge tool, and no Q1 line is logged.
            Status = status;
            OnStatusApplied(status);
            return;
        }

        // The offset as it was: once the row goes, the graph control clamps the offset to its
        // shorter extent at once, and moving the clamped offset would move the rows a row too far.
        var had = HasWorkingChanges;
        var offset = ScrollOffset;
        Status = status;

        // The real-window pass, which cannot see the graph's rows, waits for this line.
        _log.Debug(string.Create(CultureInfo.InvariantCulture, $"{Name}: working tree: {status.Staged.Count} staged, {status.Unstaged.Count} unstaged"));
        if (had != status.HasChanges && offset > 0)
        {
            ScrollOffset = Math.Max(0, offset + (status.HasChanges ? GraphRowHeight : -GraphRowHeight));
        }

        if (!status.HasChanges && IsWorkingRowSelected)
        {
            IsWorkingRowSelected = false;
            _ = Details.ShowAsync(null, null, _lifetime.Token);
        }

        Changes.Update(status, HasHead);
        OnStatusApplied(status);
        FollowStatusInGraphArea(status);
    }

    private async Task ShowStatusAsync(Task<WorkingTreeStatus> reading, bool firstRead = false)
    {
        try
        {
            ApplyStatus(await reading);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: reading the working tree's status failed.", ex);
            if (firstRead)
            {
                // The first read has failed (3.2): said where a row looks, not in the tab.
                StatusErrorText = ex is GitException git && git.StandardError.Trim() is { Length: > 0 } output ? output : ex.Message;
            }
        }
        finally
        {
            // The full start's first read (3.2): the refs were applied before the status, which was
            // applied or failed (StatusErrorText) by now.
            if (firstRead)
            {
                CompleteFirstRead();
            }
        }
    }

    /// <summary>
    /// Reads the open diff again after a write or an outside change, for the file as the status
    /// now lists it. A working-tree diff whose file has left its list closes. A commit's diff
    /// never changes.
    /// </summary>
    private async Task ReloadDiffAsync()
    {
        if (Diff is not { } diff || diff.Target.Side == DiffSide.Commit)
        {
            return;
        }

        var staged = diff.Target.Side == DiffSide.Staged;
        var list = staged ? Status.Staged : Status.Unstaged;
        var file = list.FirstOrDefault(candidate => candidate.Path == diff.Target.Path);
        if (file is null || file.Kind == FileChangeKind.Conflicted)
        {
            // Gone from its list, or conflicted now: a conflicted file is the resolver's (11.1).
            CloseDiff();
            return;
        }

        var target = diff.Target with
        {
            Kind = file.Kind,
            OldPath = staged ? file.OldPath : null,
            IsUntracked = !staged && Status.IsUntracked(file.Path),
        };
        await diff.LoadAsync(target);
    }

    private void OpenCommitDiff(ChangedFile file)
    {
        if (Details.Details is not { } commit)
        {
            return;
        }

        var target = new DiffTarget(DiffSide.Commit, file.Path, file.Kind, file.OldPath, Commit: commit.Sha, Parent: commit.Parents.FirstOrDefault());
        ShowDiff(target, commit.Sha[..Math.Min(7, commit.Sha.Length)]);
        Changes.ShowSelection(null, staged: false);
        Details.SelectedFilePath = file.Path;
    }

    private void ShowDiff(DiffTarget target, string sideLabel)
    {
        // A commit still waiting to be selected when it loads would close this diff: the newer
        // action wins.
        _pendingSelection = null;
        Diff?.Dispose();
        var diff = new DiffViewModel(this, target, sideLabel, _settings, _log);
        Diff = diff;
        _ = diff.LoadAsync();
    }

    /// <summary>A file's name as dialogs and messages give it; a folder's without its slash.</summary>
    private static string DisplayName(ChangedFile file) => WorkingChangesViewModel.FileName(file.Path.TrimEnd('/'));

    private Task<bool> ConfirmDiscardAsync(string question) =>
        AskAsync(new ConfirmationRequest("Discard changes?", question, SnapshotNote, "Discard"));

    /// <summary>
    /// Asks <paramref name="request"/> in the window's confirmation, naming this repository under
    /// its title by the window's rule, as every dialog of an operation does (4f,
    /// <see cref="RepositoryOperations.NamesRepositoryInDialogs"/>). True without a dialog service
    /// (tests only: the change goes ahead unasked).
    /// </summary>
    private async Task<bool> AskAsync(ConfirmationRequest request) =>
        _dialogs is null || await _dialogs.ConfirmAsync(request with { RepositoryName = Operations.NamesRepositoryInDialogs ? Name : null });

    /// <summary>
    /// After a discard that succeeded: keeps its snapshot for Restore, and what the files hold now,
    /// so that Restore can tell whether they changed since (D75). Shows the restore bar.
    /// </summary>
    private async Task RememberDiscardAsync(DiscardSnapshot snapshot, CancellationToken cancellationToken)
    {
        _lastSnapshot = snapshot;
        _afterDiscard = await FingerprintAsync(snapshot.Paths, cancellationToken);
        Changes.ShowRestore(snapshot.Paths);
    }

    /// <summary>The last discard's files whose content is no longer what the discard left.</summary>
    private async Task<List<string>> ChangedSinceDiscardAsync(CancellationToken cancellationToken)
    {
        if (_lastSnapshot is not { } snapshot || _afterDiscard is not { } before)
        {
            return [];
        }

        var now = await FingerprintAsync(snapshot.Paths, cancellationToken);
        return [.. snapshot.Paths.Where(path => !string.Equals(before.GetValueOrDefault(path), now.GetValueOrDefault(path), StringComparison.Ordinal))];
    }

    /// <summary>
    /// A hash of each working-tree file's bytes, null for a file that does not exist; a file that
    /// cannot be read gets a value of its own, so that it counts as changed, and so does a path
    /// where a folder holding files or a file in a folder's place now stands (docs/phase-5-design.md
    /// 15.9), which the restore then refuses rather than delete.
    /// </summary>
    private Task<Dictionary<string, string?>> FingerprintAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var root = Repository.WorkingDirectory;
        return Task.Run(
            () => paths.Distinct(StringComparer.Ordinal).ToDictionary(
                path => path,
                path =>
                {
                    var full = System.IO.Path.Combine(root, path.Replace('/', System.IO.Path.DirectorySeparatorChar));
                    try
                    {
                        if (!File.Exists(full))
                        {
                            return WorkingTreeFiles.IsInTheWayOfFile(root, path) ? RecordedFile.InTheWay : null;
                        }

                        using var stream = File.OpenRead(full);
                        return Convert.ToHexString(SHA256.HashData(stream));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        return (string?)("unreadable " + Guid.NewGuid().ToString("N"));
                    }
                },
                StringComparer.Ordinal),
            cancellationToken);
    }

    /// <summary>
    /// D124 for a ticked Amend: HEAD read inside the commit's operation against <paramref name="amended"/>,
    /// where it was when Amend was ticked. Another branch, or another commit on the same one, refuses
    /// in D124's words ("main moved while this waited: it was at a1b2c3d and is at e4f5a6b now.
    /// Nothing was amended."); null when HEAD is where it was.
    /// </summary>
    private async Task<string?> RefuseStaleAmendAsync(HeadState amended, CancellationToken cancellationToken)
    {
        var now = await Repository.ReadHeadAsync(cancellationToken);
        string? summary = null;
        if (!string.Equals(now.BranchName, amended.BranchName, StringComparison.Ordinal))
        {
            summary = OperationTexts.HeadMoved(amended, now, OperationTexts.NothingAmended);
        }
        else if (!string.Equals(now.Sha, amended.Sha, StringComparison.Ordinal))
        {
            summary = OperationTexts.HeadCommitMoved(amended.BranchName ?? "HEAD", amended.Sha ?? string.Empty, now.Sha ?? string.Empty, OperationTexts.NothingAmended);
        }

        if (summary is not null)
        {
            _log.Info($"{Name}: Commit: amend refused, {summary}");
        }

        return summary;
    }

    /// <summary>
    /// Runs one of the stage panel's and diff view's writes (phase 2) as an operation of the
    /// queue (docs/phase-3-design.md 1.4): one at a time, after those queued before it (D76), with
    /// the watcher paused, then reading what changed: the status (and the open diff), and with
    /// <paramref name="refsChange"/> the refs and history too (D71), whether or not it succeeded.
    /// Returns git's message when the write failed, and shows it in the stage panel, not in a
    /// dialog. A write asked for after the tab closed (a confirmation was still open) does
    /// nothing. A write that has started runs to its end, also when the tab closes (1.8). The undo
    /// history records it as <paramref name="undo"/> says, as every operation of
    /// <see cref="RepositoryOperations.RunAsync"/> (docs/phase-5-design.md 7.7). When the write
    /// sets <paramref name="backupSaved"/>, the backups' clean-up follows the recording, as it
    /// follows that of an operation that saved a backup, so that it keeps what the entry needs.
    /// </summary>
    /// <param name="refuse">A check inside the operation before anything is written or recorded: the stage panel's text when it refuses, or null to go on (D124).</param>
    private async Task<string?> WriteAsync(string title, string runningText, Func<CancellationToken, Task> write, UndoScope undo, bool refsChange = false, StrongBox<bool>? backupSaved = null, Func<CancellationToken, Task<string?>>? refuse = null)
    {
        var result = await _queue.RunAsync(new GitOperation(
            title,
            runningText,
            OperationKind.Local,
            refsChange ? RefreshScope.Refs : RefreshScope.Status,
            async context =>
            {
                if (refuse is not null && await refuse(context.CancellationToken) is { } refusal)
                {
                    // The status bar says "Commit failed", the stage panel why (as git's output).
                    return OperationReport.Failed($"{title} failed", new GitFailure(GitFailureKind.Other, $"{title} failed.", refusal, string.Empty, null));
                }

                var recording = await Operations.BeginRecordingAsync(undo, context, title);
                OperationReport? report = null;
                using (UndoCapture.Begin(out var capture))
                {
                    try
                    {
                        await write(context.CancellationToken);
                        report = OperationReport.Done(string.Empty);
                        return report;
                    }
                    finally
                    {
                        await Operations.EndRecordingAsync(recording, capture.Captured, report, title);
                        if (backupSaved?.Value == true)
                        {
                            await Operations.CleanUpBackupsAsync();
                        }
                    }
                }
            })
        {
            Errors = ErrorDisplay.StagePanel,
            Prompts = PromptMode.None,
        });
        var error = result.Outcome == OperationOutcome.Failed ? result.Failure!.GitOutput : null;
        Changes.ErrorText = error;
        return error;
    }

    private void OnRepositoryChanged(RepositoryChangedEventArgs change)
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

        _ = change.GitFolder ? RefreshAsync() : RefreshStatusAsync();
    }

    /// <summary>
    /// Loads the history into <paramref name="lazyData"/>, which is made from <paramref name="refs"/>
    /// once they are known, with what <paramref name="filter"/> hides or solos (docs/phase-5-design.md
    /// 8.2; null loads everything). Git starts at once; no page arrives before the refs (the contract of
    /// <see cref="IGitRepository.LoadCommitsAsync(Task{RepoRefs}, Action{IReadOnlyList{CommitInfo}}, CancellationToken)"/>).
    /// </summary>
    private async Task LoadAsync(Task<RepoRefs> refs, Lazy<CommitGraphData> lazyData, GraphFilter? filter, CancellationToken cancellationToken)
    {
        var layout = new GraphLayout();
        var stopwatch = Stopwatch.StartNew();
        await Repository.LoadCommitsAsync(
            refs,
            filter,
            page =>
            {
                var data = lazyData.Value;
                if (layout.RowCount == 0)
                {
                    _log.Debug(string.Create(CultureInfo.InvariantCulture, $"{Name}: first commits from git after {_sinceOpening.ElapsedMilliseconds} ms"));
                }

                // On a background thread: lay the page out here, add it on the UI thread.
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
        var data = lazyData.Value;
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
        _headSha = refs.Head.Sha;
        BranchText = refs.Head switch
        {
            { BranchName: { } branch } => branch,
            { Sha: { } sha } => $"Detached at {sha[..Math.Min(7, sha.Length)]}",
            _ => string.Empty,
        };

        // Summary mode shows no left panel (3.1); the list and a page are null there.
        if (!IsSummary)
        {
            LeftPanel.Update(refs);
        }

        Refs = refs;
        InteractiveRebase?.ObserveRefs(refs);
        Page?.OnRefsRead(refs);
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
            // The selected commit is gone (a reset, a deleted branch, an amend): show the placeholder.
            _selectedSha = null;
            _ = Details.ShowAsync(null, null, _lifetime.Token);
        }
        else if (selected is not null && data.Refs.Stashes.FirstOrDefault(entry => entry.Sha == selected) is { } stash)
        {
            // A selected stash keeps its commit when stashes are pushed or dropped, but not its
            // name: stash@{0} becomes stash@{1}. Show it again under its new name.
            _ = Details.ShowAsync(selected, stash, _lifetime.Token);
        }

        OnGraphSwapped(data);
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
        Log("graph frames while loading", _loadingFrameTimes);
        Log("graph frames", _frameTimes);

        void Log(string what, List<double> times)
        {
            if (times.Count == 0)
            {
                return;
            }

            var sorted = times.Order().ToList();
            var p95 = sorted[(int)Math.Ceiling(sorted.Count * 0.95) - 1];
            _log.Info(string.Create(
                CultureInfo.InvariantCulture,
                $"{Name}: {what}: {sorted.Count} drawn, 95th percentile {p95:F1} ms, longest {sorted[^1]:F1} ms"));
        }
    }
}
