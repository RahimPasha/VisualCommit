using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// One row of a dashboard: one repository of the workspace (docs/phase-6-design.md 2.6, 2.10, 3.5,
/// 3.6), read by its own summary-mode view model or following a tab's. Its cells show the values it
/// last copied from its view model (on each change of <c>Refs</c>, <c>Status</c> and
/// <c>BranchText</c>), so that they stay while the view model is replaced; its Operation cell
/// follows the view model's status and empties without one.
/// <para>
/// A read (<see cref="StartRead"/>, 2.10) runs on the UI thread between its awaits; its folder
/// checks (the <c>directoryExists</c> seam) and its opening run on the thread pool, at most one
/// outstanding, holding no read slot; only the summary read's git calls hold one of the
/// dashboard's four. After each await it applies its result only while the row still holds the
/// token it started with (the row's <see cref="CancellationTokenSource"/>, linked to the
/// dashboard's lifetime, cancelled when the row is removed, its path replaced or the dashboard
/// closed), and otherwise releases what it acquired.
/// </para>
/// </summary>
public sealed partial class DashboardRowViewModel : ObservableObject, IDisposable
{
    private readonly DashboardViewModel _dashboard;
    private CancellationTokenSource _token;
    private RepositoryViewModel? _repository;
    private OperationStatusViewModel? _status;
    private DashboardRowRead? _read;
    private bool _owns;
    private string? _key;
    private bool _disposed;

    /// <param name="path">The saved folder, as <c>workspaces.json</c> holds it.</param>
    /// <param name="dashboard">The dashboard the row is part of: its services, read slots, clock and seam.</param>
    internal DashboardRowViewModel(string path, DashboardViewModel dashboard)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(dashboard);
        _dashboard = dashboard;
        _token = CancellationTokenSource.CreateLinkedTokenSource(dashboard.Lifetime);
        Path = path;
        Name = DashboardTexts.NameOf(path);
    }

    /// <summary>The saved folder, as <c>workspaces.json</c> holds it; a read never rewrites it. Locate… replaces it (1.10).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NameToolTip))]
    public partial string Path { get; private set; }

    /// <summary>The Repository cell (2.6): the folder's name, <c>IGitRepository.Name</c> once it is open.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; }

    /// <summary>The Repository cell's tooltip: the saved folder (at 1100 the only place it shows).</summary>
    public string NameToolTip => Path;

    /// <summary>The view model behind the row: its own summary-mode one, or the first registered tab's on its worktree (3.5); null while it has none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FollowsTab))]
    public partial RepositoryViewModel? Repository { get; private set; }

    /// <summary>The row uses a tab's view model, which it neither reads nor owns (3.5).</summary>
    public bool FollowsTab => Repository is not null && !_owns;

    /// <summary>The refs the row last showed; null before its first read.</summary>
    [ObservableProperty]
    public partial RepoRefs? Refs { get; private set; }

    /// <summary>The status the row last showed; null before its first read.</summary>
    [ObservableProperty]
    public partial WorkingTreeStatus? Status { get; private set; }

    /// <summary>The folder state a read found (1.10), or null for a repository that opened.</summary>
    [ObservableProperty]
    public partial WorkspaceFolderProbe? FolderState { get; private set; }

    /// <summary>Why its view model could not read it (a failed summary read, a followed tab's failed first read: its <c>ErrorText</c>), or null.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>
    /// The row has a read outstanding: running, waiting for a read slot, or in a folder check that
    /// has not returned (2.10, WD37). A trigger that finds one starts none.
    /// </summary>
    [ObservableProperty]
    public partial bool IsReading { get; private set; }

    /// <summary>When the row's last read ended, failed or not, by the dashboard's read clock; null when never read (or its view model went away, 3.6).</summary>
    public DateTimeOffset? LastRead { get; private set; }

    /// <summary>The worktree's key, once the row knows it (from its view model).</summary>
    public string? Key => _key;

    /// <summary>The working tree of the row's repository while it has a view model (1.2's <c>WorkspaceMembership</c>); null otherwise.</summary>
    public string? WorkingDirectory => Repository?.Path;

    /// <summary>
    /// The row has not been read yet (2.6, 2.10): no values and no folder state or error, or it
    /// follows a tab whose view model has not had its first read (3.2). Its State cell says
    /// <c>Reading…</c>; a bulk run waits for it (4.2).
    /// </summary>
    public bool IsNotReadYet =>
        FolderState is null && ErrorText is null
        && (Repository is { HasFirstRead: false } || (Repository is null && (Refs is null || Status is null)));

    /// <summary>
    /// The row has a repository its operations can act on: a view model that has read it without
    /// an error. Open, Fetch, Pull and a run act on it (2.3, 4.2).
    /// </summary>
    public bool HasRepository => Repository is { HasFirstRead: true, IsClosed: false } && FolderState is null && ErrorText is null;

    // The cells (2.6).

    /// <summary>The Folder cell: the saved folder (secondary), whole in its tooltip when cut.</summary>
    public string FolderText => Path;

    /// <summary>The Branch cell.</summary>
    public string BranchText => ShowsValues && Refs is { } refs && Status is { } status ? DashboardTexts.BranchText(refs, status) : string.Empty;

    /// <summary>The Ahead / behind cell.</summary>
    public DashboardAheadBehind AheadBehind => ShowsValues && Refs is { } refs && Status is { } status
        ? DashboardTexts.AheadBehind(refs, status)
        : DashboardAheadBehind.Empty;

    /// <summary>The Changes cell.</summary>
    public DashboardCellText Changes => ShowsValues && Status is { } status ? DashboardTexts.Changes(status) : DashboardCellText.Empty;

    /// <summary>
    /// The State cell (2.6): <c>Reading…</c> before the first read; a folder state or <c>Git could
    /// not open it</c>; what is in progress; else empty.
    /// </summary>
    public DashboardCellText State =>
        FolderState is { } probe ? DashboardTexts.FolderState(probe)
        : ErrorText is { } error ? DashboardTexts.CouldNotOpen(Path, error)
        : IsNotReadYet ? new DashboardCellText(DashboardTexts.ReadingText, DashboardTone.Secondary, null)
        : Refs is { } refs && Status is { } status ? DashboardTexts.InProgress(refs, status)
        : DashboardCellText.Empty;

    /// <summary>
    /// What a bulk run shows in the Operation cell in place of the status (4.2, 4.5, 4.6:
    /// <c>Waiting to fetch…</c>, a left-out row's reason, <c>Fetch cancelled</c>, a kept result); null
    /// while no run holds the row. Work package WP-B's runs set it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Operation), nameof(ShowsCancel), nameof(ShowsDetails))]
    public partial DashboardCellText? BulkText { get; set; }

    /// <summary>The dialog a bulk run kept for the row's <c>Details…</c> and <c>Error details…</c> (4.6, D195); null for none. Work package WP-B sets it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsDetails))]
    public partial DeferredDialog? KeptDialog { get; set; }

    /// <summary>
    /// While a running bulk run holds the row (in line, reading or running, 4.7): <c>Wait for Fetch
    /// all to finish</c> / <c>Wait for Pull all to finish</c>, the reason of its menu's Fetch, Pull
    /// and Remove; null otherwise. Work package WP-B's runs set it.
    /// </summary>
    [ObservableProperty]
    public partial string? HeldByRunReason { get; set; }

    /// <summary>
    /// The Operation cell (2.6): a bulk run's text (<see cref="BulkText"/>), else the view model's
    /// <c>CompactText</c> in its tone with <c>CompactToolTip</c>; empty without a view model.
    /// </summary>
    public DashboardCellText Operation =>
        BulkText ?? (_status is { CompactText.Length: > 0 } status
            ? new DashboardCellText(status.CompactText, DashboardTexts.ToneOf(status.CompactTone), status.CompactToolTip)
            : DashboardCellText.Empty);

    /// <summary>The row's <c>Cancel</c> (2.6, 4.5): its status's, while it can cancel; never beside <c>Details…</c>.</summary>
    public bool ShowsCancel => _status is { CanCancel: true } && BulkText is null;

    /// <summary>The row's <c>Details…</c> (4.6): while a dialog is kept; never beside <c>Cancel</c>.</summary>
    public bool ShowsDetails => KeptDialog is not null && !ShowsCancel;

    /// <summary><c>Cancel</c>'s tooltip: the status's <c>CancelToolTip</c> (<c>Stop git</c>, <c>Stop git and the merge tool</c>).</summary>
    public string CancelToolTip => _status?.CancelToolTip ?? OperationStatusViewModel.StopGitText;

    /// <summary>The view model's status, for the row menu's <c>Cancel {title}</c> (2.7); null without one.</summary>
    public OperationStatusViewModel? OperationStatus => _status;

    /// <summary>A click on <c>Details…</c> (4.6); the dashboard opens the kept dialog.</summary>
    public event EventHandler? DetailsRequested;

    /// <summary>The cells show the copied values: not before a first read, nor in a folder state or with an error (2.6).</summary>
    private bool ShowsValues => FolderState is null && ErrorText is null && !IsNotReadYet;

    /// <summary>
    /// Starts the row's read (2.10), or returns null when it starts none: the row follows a tab
    /// (never read by the dashboard), or a read of it is outstanding.
    /// </summary>
    internal DashboardRowRead? StartRead()
    {
        if (_disposed || _read is { IsCompleted: false } || FollowsTab)
        {
            return null;
        }

        var read = new DashboardRowRead();
        _read = read;
        IsReading = true;
        _ = RunReadAsync(read, _token.Token);
        return read;
    }

    /// <summary>
    /// Follows a tab registered for the row's folder without a read (2.10 step 3, trigger 6's
    /// rows that are open in tabs): true when there is one.
    /// </summary>
    internal bool TryFollowTabOfFolder()
    {
        if (_disposed || _dashboard.Registry.TabForFolder(Path) is not { } tab)
        {
            return false;
        }

        Follow(tab);
        return true;
    }

    /// <summary>
    /// The registry changed for <paramref name="key"/> (3.5, 3.6): a tab registered or
    /// unregistered, a summary adopted. The row follows the first registered tab on its worktree,
    /// keeps its own summary while it holds it, and otherwise keeps its values, counts as never
    /// read and waits for the dashboard's next trigger. It starts no read.
    /// </summary>
    internal void OnRegistryChanged(string key)
    {
        if (_disposed)
        {
            return;
        }

        var registry = _dashboard.Registry;
        if (_key is not null && !WorktreeKey.Comparer.Equals(key, _key))
        {
            // Another worktree: only a row without a view model looks for a tab of its folder.
            if (Repository is null && FolderState is null && registry.TabForFolder(Path) is { } other)
            {
                Follow(other);
            }

            return;
        }

        if (_key is { } own && registry.TabFor(own) is { } tab)
        {
            if (!ReferenceEquals(tab, Repository) || _owns)
            {
                // A tab adopted the row's summary (the same object: the row goes on using it), or
                // registered first on the worktree.
                ReleaseOwn();
                Follow(tab);
            }

            return;
        }

        if (Repository is null)
        {
            if (FolderState is null && registry.TabForFolder(Path) is { } folderTab)
            {
                Follow(folderTab);
            }

            return;
        }

        if (!_owns || !registry.Holds(Repository, _dashboard))
        {
            // The tab the row followed closed with no other on the worktree (3.6), or the summary
            // was taken away (adopted by a tab that is not registered yet): the row keeps its
            // values, its Operation cell empties and it counts as never read.
            _owns = false;
            Detach();
            LastRead = null;
        }
    }

    /// <summary>
    /// Locate… replaced the row's folder (1.10): the running read is cancelled with the old token,
    /// what the row held is released, and the new folder is read with a new token (trigger 6).
    /// </summary>
    internal void ReplacePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _token.Cancel();
        _token.Dispose();
        _token = CancellationTokenSource.CreateLinkedTokenSource(_dashboard.Lifetime);
        ReleaseOwn();
        Detach();
        _read = null;
        IsReading = false;
        _key = null;
        LastRead = null;
        (Refs, Status, FolderState, ErrorText) = (null, null, null, null);
        Path = path;
        Name = DashboardTexts.NameOf(path);
        RaiseCells();
    }

    /// <summary>Asks for the kept dialog (<c>Details…</c>, <c>Error details…</c>, 4.6).</summary>
    [RelayCommand]
    private void ShowDetails() => DetailsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>The row's own Cancel (4.5): its status's, which cancels what runs in its queue.</summary>
    [RelayCommand]
    private void Cancel()
    {
        if (_status is { } status && status.CancelCommand.CanExecute(null))
        {
            status.CancelCommand.Execute(null);
        }
    }

    /// <summary>
    /// The row was removed from the workspace, or its dashboard closed (2.10): cancels its read,
    /// stops listening to any view model and releases its summary-mode view model, so that a
    /// removed row is never fetched or pulled and holds nothing.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _token.Cancel();
        ReleaseOwn();
        Detach();
        _token.Dispose();
    }

    private async Task RunReadAsync(DashboardRowRead read, CancellationToken token)
    {
        var path = Path;
        var registry = _dashboard.Registry;
        var exists = _dashboard.DirectoryExists;
        try
        {
            // Step 2: a row with its own view model checks its folder and its git folder first, on
            // the thread pool and before it takes a slot, so that a .git deleted inside another
            // repository is never read as that one (0.1, 0.8).
            if (_owns && Repository is { } own)
            {
                var gitDirectory = own.Repository.GitDirectory;
                var present = await read.CheckAsync(() => exists(path) && exists(gitDirectory), token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (present)
                {
                    await ReadOwnAsync(own, token);
                    return;
                }

                // Gone: released, and read as a row without one.
                ReleaseOwn();
                Detach();
            }

            // Step 3: a tab registered for the folder: the row follows it, no git call.
            if (registry.TabForFolder(path) is { } tab)
            {
                Follow(tab);
                return;
            }

            var folderExists = await read.CheckAsync(() => exists(path), token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (!folderExists)
            {
                ShowFolderState(WorkspaceFolderProbe.FolderNotFound(path));
                return;
            }

            WorkspaceFolderProbe probe;
            try
            {
                probe = await WorkspaceFolderProbe.ProbeAsync(_dashboard.Services.Repositories, path, token, exists);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _dashboard.Services.Log.Error($"{_dashboard.Name}: {path} could not be opened.", ex);
                if (!token.IsCancellationRequested)
                {
                    ShowFolderState(WorkspaceFolderProbe.NotOpened(path, ex.Message));
                }

                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (probe.State != WorkspaceFolderState.Ready || probe.Repository is not { } repository)
            {
                ShowFolderState(probe);
                return;
            }

            var key = WorktreeKey.Of(repository.GitDirectory);
            _key = key;
            if (registry.TabFor(key) is { } registered)
            {
                Follow(registered);
                return;
            }

            var acquired = await registry.AcquireSummaryAsync(repository, _dashboard, token);
            if (token.IsCancellationRequested || _disposed)
            {
                if (acquired is not null)
                {
                    registry.ReleaseSummary(acquired, _dashboard);
                }

                return;
            }

            if (acquired is null)
            {
                if (registry.TabFor(key) is { } meanwhile)
                {
                    Follow(meanwhile);
                }

                return;
            }

            Own(acquired);
            await ReadOwnAsync(acquired, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Removed, replaced or closed: nothing is applied.
        }
        catch (Exception ex)
        {
            _dashboard.Services.Log.Error($"{_dashboard.Name}: reading {path} failed.", ex);
        }
        finally
        {
            if (!token.IsCancellationRequested && !_disposed)
            {
                LastRead = _dashboard.ReadClock.GetUtcNow();
            }

            if (ReferenceEquals(_read, read))
            {
                IsReading = false;
            }

            read.Complete();
        }
    }

    /// <summary>The summary read in one of the dashboard's slots: the first, or a refresh of one read before (another dashboard's included).</summary>
    private async Task ReadOwnAsync(RepositoryViewModel own, CancellationToken token)
    {
        await _dashboard.ReadSlots.WaitAsync(token);
        try
        {
            if (token.IsCancellationRequested || own.IsClosed)
            {
                return;
            }

            await (own.HasFirstRead ? own.RefreshAsync() : own.StartAsync());
        }
        finally
        {
            _dashboard.ReadSlots.Release();
        }

        if (!token.IsCancellationRequested && ReferenceEquals(Repository, own))
        {
            CopyFrom(own);
        }
    }

    private void ShowFolderState(WorkspaceFolderProbe probe)
    {
        ReleaseOwn();
        Detach();
        FolderState = probe;
        ErrorText = null;
        RaiseCells();
    }

    /// <summary>Takes a summary-mode view model the dashboard holds (3.4).</summary>
    private void Own(RepositoryViewModel repository)
    {
        Attach(repository);
        _owns = true;
        OnPropertyChanged(nameof(FollowsTab));
    }

    /// <summary>Follows a tab's view model (3.5): its live values, its queue; never read by the dashboard, never owned.</summary>
    private void Follow(RepositoryViewModel tab)
    {
        if (ReferenceEquals(Repository, tab) && !_owns)
        {
            return;
        }

        if (!ReferenceEquals(Repository, tab))
        {
            ReleaseOwn();
            Attach(tab);
        }

        _owns = false;
        FolderState = null;
        OnPropertyChanged(nameof(FollowsTab));
        CopyFrom(tab);
    }

    private void Attach(RepositoryViewModel repository)
    {
        Detach();
        _repository = repository;
        _key = WorktreeKey.Of(repository.Repository.GitDirectory);
        repository.PropertyChanged += OnRepositoryChanged;
        _status = repository.OperationStatus;
        _status.PropertyChanged += OnStatusChanged;
        Name = repository.Name;
        Repository = repository;
        RaiseOperation();
    }

    /// <summary>Stops listening to the view model, keeping the values it last showed (2.6, 3.6).</summary>
    private void Detach()
    {
        if (_repository is { } repository)
        {
            repository.PropertyChanged -= OnRepositoryChanged;
        }

        if (_status is { } status)
        {
            status.PropertyChanged -= OnStatusChanged;
        }

        _repository = null;
        _status = null;
        Repository = null;
        RaiseOperation();
        RaiseCells();
    }

    /// <summary>Gives the summary back to the registry when the row holds it (3.4: the last release disposes it).</summary>
    private void ReleaseOwn()
    {
        if (_owns && _repository is { } own)
        {
            _owns = false;
            _dashboard.Registry.ReleaseSummary(own, _dashboard);
        }

        _owns = false;
    }

    private void CopyFrom(RepositoryViewModel repository)
    {
        if (repository.Refs is { } refs)
        {
            Refs = refs;
        }

        if (repository.HasFirstRead || repository.Refs is not null)
        {
            Status = repository.Status;
        }

        ErrorText = repository.ErrorText is { Length: > 0 } error ? error : null;
        if (repository.HasFirstRead)
        {
            FolderState = null;
        }

        RaiseCells();
    }

    private void OnRepositoryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, _repository) || _repository is not { } repository)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(RepositoryViewModel.Refs):
            case nameof(RepositoryViewModel.Status):
            case nameof(RepositoryViewModel.BranchText):
            case nameof(RepositoryViewModel.ErrorText):
            case nameof(RepositoryViewModel.HasFirstRead):
                CopyFrom(repository);
                break;
        }
    }

    private void OnStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(OperationStatusViewModel.CompactText):
            case nameof(OperationStatusViewModel.CompactTone):
            case nameof(OperationStatusViewModel.CompactToolTip):
            case nameof(OperationStatusViewModel.CanCancel):
            case nameof(OperationStatusViewModel.CancelToolTip):
            case nameof(OperationStatusViewModel.RunningTitle):
            case nameof(OperationStatusViewModel.RunningIsCancellable):
                RaiseOperation();
                break;
        }
    }

    private void RaiseOperation()
    {
        OnPropertyChanged(nameof(Operation));
        OnPropertyChanged(nameof(ShowsCancel));
        OnPropertyChanged(nameof(ShowsDetails));
        OnPropertyChanged(nameof(CancelToolTip));
        OnPropertyChanged(nameof(OperationStatus));
    }

    private void RaiseCells()
    {
        OnPropertyChanged(nameof(IsNotReadYet));
        OnPropertyChanged(nameof(HasRepository));
        OnPropertyChanged(nameof(BranchText));
        OnPropertyChanged(nameof(AheadBehind));
        OnPropertyChanged(nameof(Changes));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(WorkingDirectory));
        _dashboard.OnRowChanged(this);
    }
}

/// <summary>
/// One read of a row (2.10), as its pass sees it: whether it has ended, and whether it sits in a
/// folder check (on the thread pool, or returned and waiting for the UI thread), which holds no
/// slot; its pass ends without it once only such checks are left for the dashboard's
/// <see cref="DashboardViewModel.CheckPatience"/>.
/// </summary>
internal sealed class DashboardRowRead
{
    private const int NoCheck = 0;
    private const int CheckQueued = 1;
    private const int CheckRunning = 2;
    private const int CheckDone = 3;

    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _check;

    /// <summary>The read sits in a folder check: queued for the thread pool, running there, or returned and waiting for the UI thread.</summary>
    public bool IsInCheck => Volatile.Read(ref _check) != NoCheck;

    /// <summary>The read has ended.</summary>
    public bool IsCompleted => _completion.Task.IsCompleted;

    /// <summary>Completes when the read has ended.</summary>
    public Task Completion => _completion.Task;

    /// <summary>Raised on the UI thread when a folder check returned or the read ended: its pass looks again.</summary>
    public event EventHandler? Progressed;

    /// <summary>
    /// Runs <paramref name="check"/> on the thread pool, as a folder check of this read: while it
    /// runs the read counts as sitting in it; its result comes back on the caller's context.
    /// </summary>
    public async Task<bool> CheckAsync(Func<bool> check, CancellationToken cancellationToken)
    {
        Volatile.Write(ref _check, CheckQueued);
        var result = await Task.Run(
            () =>
            {
                Volatile.Write(ref _check, CheckRunning);
                try
                {
                    return check();
                }
                finally
                {
                    Volatile.Write(ref _check, CheckDone);
                }
            },
            CancellationToken.None).WaitAsync(cancellationToken);
        Volatile.Write(ref _check, NoCheck);
        Progressed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    public void Complete()
    {
        _completion.TrySetResult();
        Progressed?.Invoke(this, EventArgs.Empty);
    }
}
