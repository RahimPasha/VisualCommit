using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>Where a dashboard's view puts the keyboard focus (2.7).</summary>
public enum DashboardFocus
{
    /// <summary>The list (<c>DashboardList</c>): when the dashboard becomes active, after a removal.</summary>
    List,

    /// <summary>The empty state's <c>Add repository…</c>: when the last row went.</summary>
    EmptyAddRepository,
}

/// <summary>
/// A workspace's dashboard (D187, D205; docs/phase-6-design.md section 2): the rows of its
/// repositories, read as 2.10 says, its header, its bulk runs (<see cref="Bulk"/>, section 4) and
/// its menus, inside a <see cref="RepoTabViewModel"/> (<see cref="RepoTabViewModel.Dashboard"/>).
/// <para>
/// When rows are read (2.10, D207): no file watchers; a row whose worktree is open in a tab follows
/// that tab and is never read here. The others are read when the dashboard becomes the active tab
/// and when the window is activated with it active (rows never read or last read 10 s ago or more
/// by <see cref="TabServices.ReadClock"/>), on F5 and Refresh (every row without a tab), and when
/// the workspace gains rows while it is active. A trigger that finds no row to read starts no pass
/// and logs nothing (WD35); one that comes during a pass folds into one more pass after it. At most
/// four reads run at once (<see cref="ReadSlots"/>), each holding its slot only around its git
/// calls; a pass ends without a row whose folder check has not returned (or that waits for a
/// closed queue on its worktree) once <see cref="CheckPatience"/> has passed, and logs
/// <c>{workspace}: read {n} repositories in {ms} ms</c> with the rows that finished having read
/// their row, none that ended by following a tab (WD27; no line for none, WD35).
/// </para>
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject, IDisposable, IDashboardRowActions, IDashboardHeaderActions
{
    /// <summary>Triggers 1 and 2 read only rows last read this long ago or more (2.10).</summary>
    public static readonly TimeSpan RereadAfter = TimeSpan.FromSeconds(10);

    /// <summary>How many reads of a dashboard run at once (2.10).</summary>
    public const int MaxReads = 4;

    /// <summary>
    /// How long a read pass waits, by the read clock, once only reads that hold nothing are left
    /// (2.10): folder checks that have not returned and waits for a closed queue. A check of a
    /// folder that answers returns in microseconds, one on a share that does not answer can take
    /// minutes, and the pass ends without it.
    /// </summary>
    public static readonly TimeSpan CheckPatience = TimeSpan.FromSeconds(2);

    private readonly TabServices _services;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Func<bool> _isActive;
    private readonly List<(ReadTrigger Trigger, IReadOnlyList<DashboardRowViewModel>? Rows)> _pending = [];
    private Task? _pass;
    private TaskCompletionSource? _pendingDone;
    private bool _deleted;

    /// <param name="workspace">The workspace, as the store holds it now.</param>
    /// <param name="services">The session's services: the store, the registry, the provider, the read clock.</param>
    /// <param name="isActive">Whether the dashboard's tab is the window's active tab (2.10's trigger 6); never without one.</param>
    /// <param name="directoryExists">
    /// The rows' and the probe's folder check (2.10, 1.10), on the thread pool:
    /// <see cref="Directory.Exists(string)"/> unless a test gives one.
    /// </param>
    public DashboardViewModel(Workspace workspace, TabServices services, Func<bool>? isActive = null, Func<string, bool>? directoryExists = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _isActive = isActive ?? (static () => false);
        DirectoryExists = directoryExists ?? Directory.Exists;
        WorkspaceId = workspace.Id;
        Name = workspace.Name;
        Bulk = new DashboardBulkViewModel(this);
        Toolbar = ToolbarViewModel.ForDashboard(Bulk);
        foreach (var repository in workspace.Repositories)
        {
            Rows.Add(new DashboardRowViewModel(repository.Path, this));
        }

        Bulk.PropertyChanged += OnBulkChanged;
        services.Workspaces.Changed += OnStoreChanged;
        services.OpenRepositories.Changed += OnRegistryChanged;

        // Rows whose folders are open in tabs follow them from the first frame (3.5).
        foreach (var row in Rows)
        {
            row.TryFollowTabOfFolder();
        }

        UpdateCommands();
    }

    /// <summary>The workspace's id, by which the session keeps the dashboard (1.11).</summary>
    public string WorkspaceId { get; }

    /// <summary>The workspace's name, following a rename: the tab's title and the header's.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyText), nameof(RefreshToolTip))]
    public partial string Name { get; private set; }

    /// <summary>Its bulk runs: the tab's status bar and toolbar (4.9, 4.10).</summary>
    public DashboardBulkViewModel Bulk { get; }

    /// <summary>The tab's toolbar (2.8, 4.10): Fetch and Pull run Fetch all and Pull all; the header's buttons are its commands.</summary>
    public ToolbarViewModel Toolbar { get; }

    /// <summary>The rows, one per repository of the workspace, in its order.</summary>
    public ObservableCollection<DashboardRowViewModel> Rows { get; } = [];

    /// <summary>The selected row (2.7): single; none when the dashboard first shows; kept in the background, not saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedRepository), nameof(ActivityLogReason))]
    public partial DashboardRowViewModel? SelectedRow { get; set; }

    /// <summary>The selected row's view model, its own or a tab's; null with no row selected or none behind it (2.9: the activity log follows it).</summary>
    public RepositoryViewModel? SelectedRepository => SelectedRow?.Repository;

    /// <summary>
    /// Why the activity log has nothing to show (2.9), the first that holds: <c>{workspace} has no
    /// repositories</c>; <c>Select a repository to see its activity</c>; <c>Wait for {name} to be
    /// read</c>; the selected row's State tooltip. Null while the selected row has a view model.
    /// </summary>
    public string? ActivityLogReason =>
        Rows.Count == 0 ? DashboardTexts.NoRepositoriesText(Name)
        : SelectedRow is not { } row ? DashboardTexts.SelectRepositoryText
        : row.Repository is not null ? null
        : row.FolderState is { ToolTip: { } folderReason } ? folderReason
        : DashboardTexts.WaitToBeRead(row.Name);

    /// <summary>The header's count: <c>1 repository</c>, <c>{n} repositories</c>, <c>No repositories</c>.</summary>
    public string CountText => DashboardTexts.CountText(Rows.Count);

    /// <summary>The workspace has rows: the column headings and the list show; otherwise the empty state (2.5, WD2).</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>The empty state's line: <c>{workspace} has no repositories yet.</c></summary>
    public string EmptyText => DashboardTexts.EmptyText(Name);

    /// <summary>Refresh's tooltip: <c>Read the repositories of {workspace} again ({key})</c>.</summary>
    public string RefreshToolTip => DashboardTexts.RefreshToolTip(Name);

    /// <summary><c>DashboardNotice</c> (2.5): the failed save's notice while the store's last save failed; null otherwise.</summary>
    public string? Notice => _services.Workspaces.SaveFailed ? DashboardTexts.SaveFailedNotice : null;

    /// <summary>The header's <c>Add repository…</c> can run (1.5: not while read-only).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddRepositoryCommand))]
    public partial bool CanAddRepository { get; private set; }

    /// <summary><c>Add repository…</c>'s tooltip: <c>Add a repository to {workspace}</c>, or its reason while disabled.</summary>
    [ObservableProperty]
    public partial string AddRepositoryToolTip { get; private set; } = string.Empty;

    /// <summary>The empty state's <c>Add open tabs</c> can run.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddOpenTabsCommand))]
    public partial bool CanAddOpenTabs { get; private set; }

    /// <summary><c>Add open tabs</c>' reason while disabled (WD21), shown as its tooltip; null while it can run.</summary>
    [ObservableProperty]
    public partial string? AddOpenTabsToolTip { get; private set; }

    /// <summary>The header's run text shows: from the dashboard's first run on (2.5, 4.9: the status bar says "Ready" before it, the header nothing).</summary>
    public bool ShowsBulkText => Bulk.RunText is not null;

    /// <summary>A read pass is under way (WD37): what a trigger decided, read at once on the UI thread.</summary>
    [ObservableProperty]
    public partial bool IsPassRunning { get; private set; }

    /// <summary>The dashboard is closed (its tab closed, or the window): it reacts to nothing any more (2.10).</summary>
    public bool IsClosed { get; private set; }

    /// <summary>The tabs this dashboard opened in this session (2.3: a new one goes after the last still open); not saved.</summary>
    internal List<RepoTabViewModel> OpenedTabs { get; } = [];

    /// <summary>A row asked to open its repository (a double-click, Enter, the row menu's Open, 2.3): the window opens or activates its tab.</summary>
    public event EventHandler<DashboardRowViewModel>? OpenRequested;

    /// <summary>The workspace was deleted (1.6, from <c>IWorkspaceStore.Changed</c>): the window closes the tab.</summary>
    public event EventHandler? WorkspaceDeleted;

    /// <summary>The view should put the keyboard focus there (2.7).</summary>
    public event EventHandler<DashboardFocus>? FocusRequested;

    /// <summary>The dashboard's lifetime, to which every row's token is linked (2.10).</summary>
    internal CancellationToken Lifetime => _lifetime.Token;

    /// <summary>The session's services.</summary>
    internal TabServices Services => _services;

    /// <summary>The registry of the session's view models (3.4).</summary>
    internal OpenRepositories Registry => _services.OpenRepositories;

    /// <summary>The dashboard's read slots: at most <see cref="MaxReads"/> reads at once, each holding one only around its git calls.</summary>
    internal SemaphoreSlim ReadSlots { get; } = new(MaxReads);

    /// <summary>The clock of the 10 s rule.</summary>
    internal TimeProvider ReadClock => _services.ReadClock;

    /// <summary>The folder check (the seam).</summary>
    internal Func<string, bool> DirectoryExists { get; }

    private enum ReadTrigger
    {
        /// <summary>Triggers 1 and 2: rows never read or read 10 s ago or more.</summary>
        Stale,

        /// <summary>Trigger 3, F5 and Refresh: every row without a tab.</summary>
        All,

        /// <summary>Trigger 6: the rows the workspace gained.</summary>
        Rows,
    }

    /// <summary>The dashboard became the active tab (2.10's trigger 1): its list takes the focus, and rows never read or read 10 s ago or more are read.</summary>
    public void OnBecameActive()
    {
        if (IsClosed)
        {
            return;
        }

        UpdateCommands();
        FocusRequested?.Invoke(this, HasRows ? DashboardFocus.List : DashboardFocus.EmptyAddRepository);
        _ = Read(ReadTrigger.Stale);
    }

    /// <summary>The window was activated with the dashboard active (2.10's trigger 2, Q4).</summary>
    public void OnWindowActivated()
    {
        if (!IsClosed)
        {
            _ = Read(ReadTrigger.Stale);
        }
    }

    /// <summary>
    /// The first read of a dashboard that is active at start (2.10, "At start"): the window's
    /// <c>InitializeAsync</c> awaits it after git is found. Completes when the pass has ended
    /// (without a row whose folder check has not returned within <see cref="CheckPatience"/>).
    /// </summary>
    public Task ReadAtStartAsync() => IsClosed ? Task.CompletedTask : Read(ReadTrigger.Stale);

    /// <summary>The working directory of the row whose saved folder is <paramref name="savedPath"/>, when its repository is open; null otherwise (1.2's <c>WorkspaceMembership</c>).</summary>
    public string? OpenedWorkingDirectoryOf(string savedPath)
    {
        ArgumentNullException.ThrowIfNull(savedPath);
        return Rows.FirstOrDefault(row => WorkspacePaths.Same(row.Path, savedPath))?.WorkingDirectory;
    }

    /// <summary>A double-click or Enter on <paramref name="row"/> (2.3): opens it, or does nothing for a row without a repository.</summary>
    public void OpenRow(DashboardRowViewModel? row)
    {
        if (row is not null && !IsClosed && Rows.Contains(row) && OpenReasonOf(row) is null)
        {
            OpenRequested?.Invoke(this, row);
        }
    }

    /// <summary>Delete (and Cmd+Backspace on macOS) on the selected row (2.7): <c>Remove from workspace</c> when it can.</summary>
    public void RemoveSelected()
    {
        if (SelectedRow is { } row && RemoveReasonOf(row) is null)
        {
            Remove(row);
        }
    }

    /// <summary>Alt+Up (−1) and Alt+Down (+1) on the selected row (2.7): <c>Move up</c>, <c>Move down</c> when they can.</summary>
    public void MoveSelected(int delta)
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        if (delta < 0 && MoveUpReasonOf(row) is null)
        {
            MoveUp(row);
        }
        else if (delta > 0 && MoveDownReasonOf(row) is null)
        {
            MoveDown(row);
        }
    }

    /// <summary>A row's menu (2.7): it selects the row first (a right-click selects it).</summary>
    public MenuModel RowMenu(DashboardRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        SelectedRow = row;
        return DashboardMenus.ForRow(row, MenuStateOf(row), this);
    }

    /// <summary>The header's <c>…</c> menu (2.5).</summary>
    public MenuModel HeaderMenu()
    {
        var commands = _services.WorkspaceCommands;
        return DashboardMenus.ForHeader(
            Name,
            commands.AddOpenTabsReason(WorkspaceId),
            commands.RenameReason(WorkspaceId),
            commands.DeleteReason(WorkspaceId),
            this);
    }

    /// <summary>What a row's menu shows and why its items are disabled (2.7).</summary>
    public DashboardRowMenuState MenuStateOf(DashboardRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var readReason = ReadReasonOf(row);
        var states = readReason is null && row.Refs is { } refs && row.Status is { } status ? ToolbarViewModel.StatesFor(refs, status) : null;
        var status2 = row.OperationStatus;
        var commands = _services.WorkspaceCommands;
        return new DashboardRowMenuState(
            row.Name,
            row.Path,
            OpenReasonOf(row),
            readReason ?? states?.Fetch.Reason ?? row.HeldByRunReason,
            readReason ?? states?.Pull.Reason ?? row.HeldByRunReason,
            status2?.RunningTitle,
            status2?.RunningIsCancellable == true,
            status2?.CanCancel == true,
            row.KeptDialog is not null,
            row.FolderState is { State: WorkspaceFolderState.FolderNotFound } ? DashboardTexts.DoesNotExist(row.Path) : null,
            MoveUpReasonOf(row),
            MoveDownReasonOf(row),
            row.FolderState is not null || row.ErrorText is not null,
            commands.LocateReason(WorkspaceId, row.Path),
            RemoveReasonOf(row));
    }

    /// <summary>Closes the dashboard (its tab closed, or the window): cancels its reads and its run, releases its rows' view models and stops listening (2.10).</summary>
    public void Dispose()
    {
        if (IsClosed)
        {
            return;
        }

        IsClosed = true;
        _lifetime.Cancel();

        // Its run is cancelled as Cancel all does and abandoned: a result that comes afterwards
        // opens its dialog in the view model that ran it (4.5, 4.6, 4.12).
        Bulk.Close();
        Bulk.PropertyChanged -= OnBulkChanged;
        _services.Workspaces.Changed -= OnStoreChanged;
        _services.OpenRepositories.Changed -= OnRegistryChanged;
        foreach (var row in Rows)
        {
            row.Dispose();
        }

        _pending.Clear();
        _pendingDone?.TrySetResult();
        _pendingDone = null;
        _lifetime.Dispose();
    }

    // IDashboardRowActions, IDashboardHeaderActions.

    IAppLog IDashboardRowActions.Log => _services.Log;

    IAppLog IDashboardHeaderActions.Log => _services.Log;

    void IDashboardRowActions.Open(DashboardRowViewModel row) => OpenRow(row);

    Task IDashboardRowActions.FetchAsync(DashboardRowViewModel row) =>
        row.Repository is { IsClosed: false } repository ? repository.Operations.FetchAllAsync() : Task.CompletedTask;

    Task IDashboardRowActions.PullAsync(DashboardRowViewModel row) =>
        row.Repository is { IsClosed: false } repository ? repository.Operations.PullAsync() : Task.CompletedTask;

    void IDashboardRowActions.Cancel(DashboardRowViewModel row) => row.CancelCommand.Execute(null);

    void IDashboardRowActions.ShowErrorDetails(DashboardRowViewModel row) => row.ShowDetailsCommand.Execute(null);

    Task IDashboardRowActions.ShowInFolderAsync(DashboardRowViewModel row) =>
        _services.Launcher?.RevealAsync(row.Path) ?? Task.CompletedTask;

    Task IDashboardRowActions.CopyPathAsync(DashboardRowViewModel row) =>
        _services.Clipboard?.SetTextAsync(row.Path) ?? Task.CompletedTask;

    /// <summary><c>Move up</c> (1.10): the row moves, keeps the selection and the focus stays in the list (WD8).</summary>
    public void MoveUp(DashboardRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _services.WorkspaceCommands.MoveUp(WorkspaceId, row.Path);
    }

    /// <summary><c>Move down</c> (1.10).</summary>
    public void MoveDown(DashboardRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _services.WorkspaceCommands.MoveDown(WorkspaceId, row.Path);
    }

    /// <summary>
    /// <c>Locate…</c> (1.10): the workspace's command. A folder that replaced the row's is read at
    /// once by the change it made (trigger 6); the same folder again changes nothing in the
    /// workspace, so the row is read here: the user located it because it showed a state (a drive
    /// mounted since, a <c>safe.directory</c> fixed), and "the located path is read at once".
    /// </summary>
    public async Task LocateAsync(DashboardRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        await _services.WorkspaceCommands.LocateAsync(WorkspaceId, row.Path);
        if (!IsClosed && Rows.Contains(row) && !row.FollowsTab && !row.IsReading)
        {
            _ = Read(ReadTrigger.Rows, [row]);
        }
    }

    Task IDashboardRowActions.LocateAsync(DashboardRowViewModel row) => LocateAsync(row);

    /// <summary><c>Remove from workspace</c> (1.10): at once, no question; no row is selected afterwards (2.7).</summary>
    public void Remove(DashboardRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _services.WorkspaceCommands.Remove(WorkspaceId, row.Path);
    }

    void IDashboardHeaderActions.AddOpenTabs() => AddOpenTabs();

    Task IDashboardHeaderActions.RenameAsync() => _services.WorkspaceCommands.RenameAsync(WorkspaceId);

    Task IDashboardHeaderActions.DeleteAsync() => _services.WorkspaceCommands.DeleteAsync(WorkspaceId);

    /// <summary>A row's cells changed: the activity log's reason may change with the selected row.</summary>
    internal void OnRowChanged(DashboardRowViewModel row)
    {
        if (ReferenceEquals(row, SelectedRow))
        {
            OnPropertyChanged(nameof(SelectedRepository));
            OnPropertyChanged(nameof(ActivityLogReason));
        }
    }

    /// <summary>
    /// F5 and the header's Refresh (2.10's trigger 3): every row without a tab is read. Always
    /// enabled (2.5): a call while a pass runs folds into one more pass after it, and its task
    /// completes with that pass.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task Refresh() => IsClosed ? Task.CompletedTask : Read(ReadTrigger.All);

    /// <summary>
    /// <c>Add repository…</c> (1.7): the workspace's command, then the row of a working tree's top
    /// it appended (or held already) is selected and scrolled into view; the new row was read at once
    /// (trigger 6).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddRepository))]
    private async Task AddRepository()
    {
        var path = await _services.WorkspaceCommands.AddRepositoryAsync(WorkspaceId);
        if (path is not null && !IsClosed && Rows.FirstOrDefault(row => WorkspacePaths.Same(row.Path, path)) is { } added)
        {
            SelectedRow = added;
            FocusRequested?.Invoke(this, DashboardFocus.List);
        }
    }

    /// <summary><c>Add open tabs</c> (1.7): the added rows follow their tabs without a git call, and no row is selected (WD20).</summary>
    [RelayCommand(CanExecute = nameof(CanAddOpenTabs))]
    private void AddOpenTabs() => _services.WorkspaceCommands.AddOpenTabs(WorkspaceId);

    private string? ReadReasonOf(DashboardRowViewModel row) =>
        row.FolderState is { ToolTip: { } folder } ? folder
        : row.ErrorText is { } error ? DashboardTexts.CouldNotOpen(row.Path, error).ToolTip
        : !row.HasRepository ? DashboardTexts.WaitToBeRead(row.Name)
        : null;

    /// <summary><c>Open</c>'s reason (2.7): a row that follows a tab always opens it (its tab activates); otherwise a folder state's, an error's, or <c>Wait for {name} to be read</c>.</summary>
    private string? OpenReasonOf(DashboardRowViewModel row) => row.FollowsTab && row.ErrorText is null ? null : ReadReasonOf(row);

    private string? MoveUpReasonOf(DashboardRowViewModel row) => _services.WorkspaceCommands.MoveUpReason(WorkspaceId, row.Path);

    private string? MoveDownReasonOf(DashboardRowViewModel row) => _services.WorkspaceCommands.MoveDownReason(WorkspaceId, row.Path);

    private string? RemoveReasonOf(DashboardRowViewModel row) =>
        _services.WorkspaceCommands.EditReason ?? row.HeldByRunReason ?? _services.WorkspaceCommands.RemoveReason(WorkspaceId, row.Path);

    /// <summary>
    /// Starts a read pass of the rows <paramref name="trigger"/> selects (2.10), or, while a pass runs,
    /// folds into one more pass after it; completes when that pass has ended. Starts nothing and
    /// logs nothing when no row is to be read (WD35).
    /// </summary>
    private Task Read(ReadTrigger trigger, IReadOnlyList<DashboardRowViewModel>? rows = null)
    {
        if (IsClosed)
        {
            return Task.CompletedTask;
        }

        if (_pass is { IsCompleted: false })
        {
            _pending.Add((trigger, rows));
            return (_pendingDone ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }

        var selected = Select(trigger, rows);
        if (selected.Count == 0)
        {
            return Task.CompletedTask;
        }

        _pass = RunPassesAsync(selected);
        return _pass;
    }

    private List<DashboardRowViewModel> Select(ReadTrigger trigger, IReadOnlyList<DashboardRowViewModel>? rows)
    {
        var now = ReadClock.GetUtcNow();
        return
        [
            .. (rows ?? Rows).Where(row => Rows.Contains(row) && !row.FollowsTab && !row.IsReading && trigger switch
            {
                ReadTrigger.Stale => row.LastRead is not { } last || now - last >= RereadAfter,
                _ => true,
            }),
        ];
    }

    private async Task RunPassesAsync(List<DashboardRowViewModel> rows)
    {
        TaskCompletionSource? folded = null;
        while (true)
        {
            await RunPassAsync(rows);
            folded?.TrySetResult();
            if (IsClosed || _pending.Count == 0)
            {
                return;
            }

            var requests = _pending.ToList();
            _pending.Clear();
            folded = _pendingDone;
            _pendingDone = null;
            rows = [.. requests.SelectMany(request => Select(request.Trigger, request.Rows)).Distinct()];
        }
    }

    /// <summary>
    /// One pass (2.10): every row's read starts; the pass ends when each has ended or has sat for
    /// <see cref="CheckPatience"/> in a wait that holds nothing (a folder check that has not
    /// returned, a closed queue on its worktree), and logs its line with the rows that finished
    /// having read their row (WD27): a read that ended by following a tab is not counted, and a
    /// pass that read none logs nothing (WD35). A row whose read is outstanding starts none and is
    /// left out.
    /// </summary>
    private async Task RunPassAsync(List<DashboardRowViewModel> rows)
    {
        if (rows.Count == 0 || IsClosed)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var reads = rows.Select(row => row.StartRead()).OfType<DashboardRowRead>().ToList();
        if (reads.Count == 0)
        {
            return;
        }

        IsPassRunning = true;
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ui = SynchronizationContext.Current;
        ITimer? patience = null;
        var patienceRound = 0;
        bool OnlyWaitsLeft() => reads.All(read => read.IsCompleted || read.IsWaiting);
        void Expire(int round)
        {
            if (round != patienceRound)
            {
                // A timer that a change since its start replaced.
                return;
            }

            patience?.Dispose();
            patience = null;
            if (OnlyWaitsLeft())
            {
                ended.TrySetResult();
            }
        }

        void Look()
        {
            // Looked at when the pass starts, and when a read's wait starts or ends or the read
            // ends. With every read ended the pass ends. With only waits that hold nothing left, it
            // waits for them CheckPatience by the read clock (a normal folder check returns in
            // microseconds), then ends without them (2.10); anything else running restarts that time.
            if (reads.All(read => read.IsCompleted))
            {
                ended.TrySetResult();
                return;
            }

            if (!OnlyWaitsLeft())
            {
                patience?.Dispose();
                patience = null;
                patienceRound++;
                return;
            }

            if (patience is null)
            {
                var round = ++patienceRound;
                patience = ReadClock.CreateTimer(
                    _ =>
                    {
                        if (ui is null)
                        {
                            Expire(round);
                        }
                        else
                        {
                            ui.Post(_ => Expire(round), null);
                        }
                    },
                    null,
                    CheckPatience,
                    Timeout.InfiniteTimeSpan);
            }
        }

        void OnProgressed(object? sender, EventArgs e) => Look();
        foreach (var read in reads)
        {
            read.Progressed += OnProgressed;
        }

        // The reads started above may have ended, or reached a wait, before the pass listened.
        Look();
        try
        {
            await ended.Task;
        }
        finally
        {
            foreach (var read in reads)
            {
                read.Progressed -= OnProgressed;
            }

            patience?.Dispose();
            IsPassRunning = false;
        }

        var counted = reads.Count(read => read.IsCompleted && read.Counted);
        if (!IsClosed && counted > 0)
        {
            _services.Log.Info(DashboardTexts.ReadLine(Name, counted, stopwatch.ElapsedMilliseconds));
        }
    }

    /// <summary>
    /// The workspaces changed (1.3): a rename follows; rows follow the workspace's list (a row
    /// removed or its folder replaced cancels its read and releases what it held, 2.10); rows the
    /// workspace gained while the dashboard is active are read at once (trigger 6), a row whose
    /// folder is open in a tab following that tab without a git call; the deletion closes the tab.
    /// </summary>
    private void OnStoreChanged(object? sender, EventArgs e)
    {
        if (IsClosed || _deleted)
        {
            return;
        }

        if (_services.Workspaces.Current.Find(WorkspaceId) is not { } workspace)
        {
            _deleted = true;
            WorkspaceDeleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        Name = workspace.Name;
        var gained = SyncRows([.. workspace.Repositories.Select(repository => repository.Path)]);
        OnPropertyChanged(nameof(Notice));
        UpdateCommands();
        if (gained.Count > 0 && _isActive())
        {
            var toRead = gained.Where(row => !row.TryFollowTabOfFolder()).ToList();
            if (toRead.Count > 0)
            {
                _ = Read(ReadTrigger.Rows, toRead);
            }
        }
    }

    /// <summary>Makes <see cref="Rows"/> the workspace's <paramref name="paths"/> in their order; returns the rows that are new or whose folder was replaced.</summary>
    private List<DashboardRowViewModel> SyncRows(IReadOnlyList<string> paths)
    {
        var old = Rows.ToList();
        var next = new DashboardRowViewModel?[paths.Count];
        var used = new HashSet<DashboardRowViewModel>();
        for (var i = 0; i < paths.Count; i++)
        {
            next[i] = old.FirstOrDefault(row => !used.Contains(row) && string.Equals(row.Path, paths[i], StringComparison.Ordinal));
            if (next[i] is { } kept)
            {
                used.Add(kept);
            }
        }

        var gained = new List<DashboardRowViewModel>();
        for (var i = 0; i < paths.Count; i++)
        {
            if (next[i] is not null)
            {
                continue;
            }

            // Locate… replaced the folder at this place (1.10): the row keeps its place and its
            // selection, and reads the new folder with a new token.
            if (i < old.Count && !used.Contains(old[i]) && old.Count == paths.Count)
            {
                old[i].ReplacePath(paths[i]);
                next[i] = old[i];
                used.Add(old[i]);
            }
            else
            {
                next[i] = new DashboardRowViewModel(paths[i], this);
            }

            gained.Add(next[i]!);
        }

        var selected = SelectedRow;
        var removed = old.Where(row => !used.Contains(row)).ToList();
        foreach (var row in removed)
        {
            Rows.Remove(row);
            row.Dispose();
        }

        var moved = false;
        for (var i = 0; i < next.Length; i++)
        {
            var row = next[i]!;
            var at = Rows.IndexOf(row);
            if (at < 0)
            {
                Rows.Insert(i, row);
            }
            else if (at != i)
            {
                Rows.Move(at, i);
                moved = true;
            }
        }

        if (removed.Count > 0)
        {
            // After a removal no row is selected, and the focus stays on the list, or goes to the
            // empty state's Add repository… when the last row went (2.7).
            SelectedRow = null;
            FocusRequested?.Invoke(this, HasRows ? DashboardFocus.List : DashboardFocus.EmptyAddRepository);
        }
        else if (moved && selected is not null && Rows.Contains(selected))
        {
            // A move keeps the moved row selected and the focus in the list, on it (WD8): the list
            // made its row anew, and the focus went with the old one.
            SelectedRow = selected;
            FocusRequested?.Invoke(this, DashboardFocus.List);
        }

        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(ActivityLogReason));
        return gained;
    }

    private void OnRegistryChanged(object? sender, string key)
    {
        if (IsClosed)
        {
            return;
        }

        foreach (var row in Rows.ToList())
        {
            row.OnRegistryChanged(key);
        }

        // "Add open tabs" depends on the open tabs (1.5).
        UpdateCommands();
        OnPropertyChanged(nameof(SelectedRepository));
        OnPropertyChanged(nameof(ActivityLogReason));
    }

    private void OnBulkChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(DashboardBulkViewModel.RunText))
        {
            OnPropertyChanged(nameof(ShowsBulkText));
        }
    }

    private void UpdateCommands()
    {
        var commands = _services.WorkspaceCommands;
        var addReason = commands.AddRepositoryReason(WorkspaceId);
        CanAddRepository = addReason is null;
        AddRepositoryToolTip = addReason ?? DashboardTexts.AddRepositoryToolTip(Name);
        var tabsReason = commands.AddOpenTabsReason(WorkspaceId);
        CanAddOpenTabs = tabsReason is null;
        AddOpenTabsToolTip = tabsReason;
    }

    partial void OnNameChanged(string value) => UpdateCommands();

    partial void OnSelectedRowChanged(DashboardRowViewModel? value)
    {
        if (value is not null && !Rows.Contains(value))
        {
            SelectedRow = null;
        }
    }
}
