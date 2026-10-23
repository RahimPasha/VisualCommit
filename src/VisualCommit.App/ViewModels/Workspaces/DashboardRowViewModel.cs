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
/// checks (the <c>directoryExists</c> seam) run on the thread pool and, like its wait for a closed
/// queue on the worktree, hold no read slot; its git calls (the probe's opening, the summary read)
/// hold one of the dashboard's four. After each await it applies its result only while the row still holds the
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
    private string? _workingDirectory;
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
        Name = WorkspaceTexts.FolderName(path);
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

    /// <summary>Why its view model could not read it (a failed summary read, a followed tab's failed first read: its <c>ErrorText</c> or <c>StatusErrorText</c>), or null.</summary>
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

    /// <summary>
    /// The working tree of the row's repository (1.2's <c>WorkspaceMembership</c>, 2.10): its view
    /// model's, kept when the view model goes (the followed tab closed, 3.6), so that a saved path
    /// that reaches the repository another way still matches; null before the row's repository was
    /// opened, and again in a folder state or after Locate… replaced the folder.
    /// </summary>
    public string? WorkingDirectory => Repository?.Path ?? _workingDirectory;

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
    /// What a bulk run shows in the Operation cell in place of the status (4.2, 4.5:
    /// <c>Waiting to fetch…</c>, a left-out row's reason, <c>Fetch cancelled</c>); null while no run
    /// holds the row, and while its operation runs or after it ended, when the status shows it.
    /// The runs (<see cref="BulkRun"/>) set it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Operation), nameof(ShowsCancel), nameof(ShowsDetails))]
    public partial DashboardCellText? BulkText { get; set; }

    /// <summary>
    /// The dialog a bulk run kept for the row's <c>Details…</c> and <c>Error details…</c> (4.6,
    /// D195); null for none. It goes when the row's queue starts its next operation, when the
    /// dashboard's next run starts, or when the row's view model changes (3.6).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsDetails))]
    public partial DeferredDialog? KeptDialog { get; set; }

    /// <summary>
    /// While a running bulk run holds the row (in line, reading or running, 4.7): <c>Wait for Fetch
    /// all to finish</c> / <c>Wait for Pull all to finish</c>, the reason of its menu's Fetch, Pull
    /// and Remove; null otherwise. The runs set it.
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

    /// <summary>The view model that ran the operation whose dialog the row keeps: it opens the dialog (4.6).</summary>
    private RepositoryViewModel? _keptBy;

    /// <summary>A run's text and kept dialog go when the row's queue starts its next operation (4.2, 4.5, 4.6).</summary>
    private bool _clearsBulkOnNextOperation;

    /// <summary>A new run starts (4.6): what the dashboard's last run left in the row goes.</summary>
    internal void ClearBulk()
    {
        _clearsBulkOnNextOperation = false;
        BulkText = null;
        HeldByRunReason = null;
        Forget();
    }

    /// <summary>
    /// What a run shows in the row (4.2, 4.5): <paramref name="text"/> in place of the status (null:
    /// the status), the menu's reason while the run holds the row, and whether both go when the
    /// row's queue starts its next operation (a left-out row's reason, <c>Fetch cancelled</c>, a kept
    /// result) or stay while the run holds the row.
    /// </summary>
    internal void ShowBulk(DashboardCellText? text, string? heldReason, bool clearsOnNextOperation)
    {
        _clearsBulkOnNextOperation = clearsOnNextOperation;
        BulkText = text;
        HeldByRunReason = heldReason;
    }

    /// <summary>The run ended (4.7): it holds the row no longer.</summary>
    internal void ShowBulkHeld(string? heldReason) => HeldByRunReason = heldReason;

    /// <summary>Keeps <paramref name="dialog"/> for Details…, opened by <paramref name="repository"/>, which ran its operation (4.6).</summary>
    internal void Keep(DeferredDialog dialog, RepositoryViewModel repository)
    {
        _keptBy = repository;
        KeptDialog = dialog;
    }

    /// <summary>The row's queue started its next operation as the run's ended (4.6): the dialog the run kept goes.</summary>
    internal void ForgetKeptDialog() => Forget();

    private void Forget()
    {
        _keptBy = null;
        KeptDialog = null;
    }

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

        var read = new DashboardRowRead(_dashboard.ReadClock);
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
    /// unregistered, the tab that adopted the row's summary among them. The row follows the first
    /// registered tab on its worktree (an adopted summary: the same object, with what the row keeps
    /// of it), keeps its own summary while it holds it, and otherwise keeps its values, counts as
    /// never read and waits for the dashboard's next trigger. It starts no read.
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
            if (ReferenceEquals(tab, Repository) && _owns)
            {
                // A tab adopted the row's summary (3.3): the same object, which the row goes on
                // using (3.5) with what it keeps of it, a run's kept dialog included (4.6); the
                // registry holds it for the dashboard no more, so there is nothing to release.
                _owns = false;
                OnPropertyChanged(nameof(FollowsTab));
                CopyFrom(tab);
            }
            else if (!ReferenceEquals(tab, Repository))
            {
                // A tab registered first on the worktree.
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
            // The tab the row followed closed with no other on the worktree (3.6), or the registry
            // let the summary go: the row keeps its values, its Operation cell empties and it
            // counts as never read. (An adoption raises no change of its own: the adopting tab's
            // registration, above, keeps the row on the same object.)
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
        _workingDirectory = null;
        LastRead = null;
        (Refs, Status, FolderState, ErrorText) = (null, null, null, null);
        Path = path;
        Name = WorkspaceTexts.FolderName(path);
        RaiseCells();
    }

    /// <summary>
    /// Opens the kept dialog (<c>Details…</c>, <c>Error details…</c>, 4.6): the identical dialog the
    /// operation would have opened, in the view model that ran it, as often as asked; nothing once
    /// that view model closed.
    /// </summary>
    [RelayCommand]
    private Task ShowDetails()
    {
        if (KeptDialog is not { } dialog || _keptBy is not { IsClosed: false } repository)
        {
            return Task.CompletedTask;
        }

        return repository.Operations.ShowDeferredAsync(dialog);
    }

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

    /// <summary>
    /// The read (2.10). It counts in its pass's <c>{n}</c> when it read the row: its own view model's
    /// summary read, a folder state found, a failure; not when it ended by following a tab, which
    /// the dashboard never reads (WD27, WD35), nor when it was cancelled.
    /// </summary>
    private async Task RunReadAsync(DashboardRowRead read, CancellationToken token)
    {
        var path = Path;
        var registry = _dashboard.Registry;
        var exists = _dashboard.DirectoryExists;
        var counted = false;
        try
        {
            // Step 2: a row with its own view model checks its folder and its git folder first, on
            // the thread pool and before it takes a slot, so that a .git deleted inside another
            // repository is never read as that one (0.1, 0.8).
            if (_owns && Repository is { } own)
            {
                var gitDirectory = own.Repository.GitDirectory;
                var present = await read.CheckAsync(() => exists(path) && exists(gitDirectory), token);
                if (token.IsCancellationRequested || !OwnsSummary(own))
                {
                    // Removed or replaced; or a tab adopted the summary during the check, and the
                    // row follows it now: a followed row is never read by the dashboard.
                    return;
                }

                if (present)
                {
                    counted = await ReadOwnAsync(own, read, token);
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
                counted = true;
                return;
            }

            // The probe's git calls (the open's rev-parse, the top's check) hold one of the
            // dashboard's four slots, as every git call of a read does (2.10).
            WorkspaceFolderProbe probe;
            await _dashboard.ReadSlots.WaitAsync(token);
            try
            {
                using (read.GitCall())
                {
                    probe = await WorkspaceFolderProbe.ProbeAsync(_dashboard.Services.Repositories, path, token, exists);
                }
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
                    counted = true;
                }

                return;
            }
            finally
            {
                _dashboard.ReadSlots.Release();
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (probe.State != WorkspaceFolderState.Ready || probe.Repository is not { } repository)
            {
                ShowFolderState(probe);
                counted = true;
                return;
            }

            var key = WorktreeKey.Of(repository.GitDirectory);
            _key = key;
            if (registry.TabFor(key) is { } registered)
            {
                Follow(registered);
                return;
            }

            // A closed queue on the worktree (a closed tab's merge tool, 3.3): the read waits for
            // it holding nothing, as in a folder check, and its pass ends without it (2.10).
            while (registry.WhenIdleAsync(key) is { IsCompleted: false } idle)
            {
                await read.WaitAsync(idle, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }
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
            counted = await ReadOwnAsync(acquired, read, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Removed, replaced or closed: nothing is applied.
        }
        catch (Exception ex)
        {
            _dashboard.Services.Log.Error($"{_dashboard.Name}: reading {path} failed.", ex);
            counted = true;
        }
        finally
        {
            var current = !token.IsCancellationRequested && !_disposed;
            if (current)
            {
                LastRead = _dashboard.ReadClock.GetUtcNow();
            }

            if (ReferenceEquals(_read, read))
            {
                IsReading = false;
            }

            read.Complete(counted && current);
        }
    }

    /// <summary>
    /// The summary read in one of the dashboard's slots: the first, or a refresh of one read before
    /// (another dashboard's included). Reads nothing, and returns false, when the row no longer owns
    /// <paramref name="own"/> once it has its slot: a tab adopted it meanwhile, and a followed row is
    /// never read by the dashboard (2.10). Its git calls are <paramref name="read"/>'s: one that
    /// runs on past the patience leaves the pass to end without the row.
    /// </summary>
    private async Task<bool> ReadOwnAsync(RepositoryViewModel own, DashboardRowRead read, CancellationToken token)
    {
        await _dashboard.ReadSlots.WaitAsync(token);
        try
        {
            if (token.IsCancellationRequested || own.IsClosed || !OwnsSummary(own))
            {
                return false;
            }

            using (read.GitCall())
            {
                await (own.HasFirstRead ? own.RefreshAsync() : own.StartAsync());
            }
        }
        finally
        {
            _dashboard.ReadSlots.Release();
        }

        if (!token.IsCancellationRequested && ReferenceEquals(Repository, own))
        {
            CopyFrom(own);
        }

        return true;
    }

    /// <summary>
    /// A bulk run's turn for the row (4.3, D207): before its operation, the row's folder and git
    /// folder are checked again off the UI thread, as a read's step 2 checks them, so that a nested
    /// repository whose <c>.git</c> was deleted while the row waited in line is never fetched or
    /// pulled as the outer one (D204: <c>git -C outer/inner</c> would find the outer). Both there,
    /// the folder is still its repository's top (git finds its own <c>.git</c> first), with no git
    /// call: null. Otherwise what the folder is now, by <see cref="WorkspaceFolderProbe.ProbeAsync"/>
    /// (its open and <see cref="IGitRepository.OpenedAtTopAsync"/>; an outer repository it finds
    /// dropped unread): for the row's own summary the row shows that folder state from now on and
    /// lets the summary go, or, when the folder is a repository's top again
    /// (<see cref="WorkspaceFolderState.Ready"/>, its git folder replaced), lets it go and counts as
    /// never read, to be read anew. A followed tab's view model stays the tab's.
    /// </summary>
    internal async Task<WorkspaceFolderProbe?> CheckAtTurnAsync(RepositoryViewModel repository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var path = Path;
        var gitDirectory = repository.Repository.GitDirectory;
        var exists = _dashboard.DirectoryExists;
        if (await Task.Run(() => exists(path) && exists(gitDirectory), CancellationToken.None).WaitAsync(cancellationToken))
        {
            return null;
        }

        var probe = await WorkspaceFolderProbe.ProbeAsync(_dashboard.Services.Repositories, path, cancellationToken, exists);
        if (_disposed || !ReferenceEquals(Repository, repository) || !_owns)
        {
            return probe.State == WorkspaceFolderState.Ready ? null : probe;
        }

        if (probe.State == WorkspaceFolderState.Ready)
        {
            ReleaseOwn();
            Detach();
            LastRead = null;
            return probe;
        }

        ShowFolderState(probe);
        return probe;
    }

    /// <summary>The row still holds <paramref name="own"/> as its own summary-mode view model.</summary>
    private bool OwnsSummary(RepositoryViewModel own) => _owns && ReferenceEquals(Repository, own) && own.IsSummary;

    private void ShowFolderState(WorkspaceFolderProbe probe)
    {
        ReleaseOwn();
        Detach();
        _workingDirectory = null;
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
        _workingDirectory = repository.Path;
        repository.PropertyChanged += OnRepositoryChanged;
        _status = repository.OperationStatus;
        _status.PropertyChanged += OnStatusChanged;
        Name = repository.Name;
        Repository = repository;
        RaiseOperation();
    }

    /// <summary>Stops listening to the view model, keeping the values it last showed (2.6, 3.6); a dialog a run kept goes with it (4.6).</summary>
    private void Detach()
    {
        if (_repository is { } repository)
        {
            repository.PropertyChanged -= OnRepositoryChanged;
            Forget();
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

        // A followed tab's first status read that failed shows as the row's error (3.2): the row
        // never shows a status that was not read.
        ErrorText = repository.ErrorText is { Length: > 0 } error ? error
            : repository.StatusErrorText is { Length: > 0 } status ? status
            : null;
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
            case nameof(RepositoryViewModel.StatusErrorText):
            case nameof(RepositoryViewModel.HasFirstRead):
                CopyFrom(repository);
                break;
        }
    }

    private void OnStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationStatusViewModel.RunningTitle) && _clearsBulkOnNextOperation && _status?.RunningTitle is not null)
        {
            // The row's next operation started (4.2, 4.5, 4.6): a run's text and kept dialog go.
            _clearsBulkOnNextOperation = false;
            BulkText = null;
            Forget();
        }

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
/// One read of a row (2.10), as its pass sees it: whether it has ended and whether it counts in the
/// pass's <c>{n}</c>, and whether it sits in a wait that holds nothing: a folder check (queued for
/// the thread pool, running there, or returned and waiting for the UI thread) or the wait for a
/// closed queue on its worktree. Its pass ends without it once only such waits have been left for
/// the dashboard's <see cref="DashboardViewModel.CheckPatience"/>. A read whose git call has run
/// for <see cref="DashboardViewModel.GitCallPatience"/> by the read clock is overdue: it holds its
/// pass no more (it keeps its slot and ends later), so its pass ends without it at once.
/// </summary>
/// <param name="clock">The dashboard's read clock, which times a git call's patience.</param>
internal sealed class DashboardRowRead(TimeProvider clock)
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private int _waits;
    private int _overdue;

    /// <summary>The read sits in a folder check or waits for a closed queue: it holds nothing.</summary>
    public bool IsWaiting => Volatile.Read(ref _waits) > 0;

    /// <summary>
    /// A git call of the read ran for <see cref="DashboardViewModel.GitCallPatience"/> by the read
    /// clock (a share that stopped answering after its folder check): from then on the read holds
    /// its pass no more, whatever it does next (2.10, the lead's ruling at status step 22).
    /// </summary>
    public bool IsOverdue => Volatile.Read(ref _overdue) != 0;

    /// <summary>
    /// A git call of the read begins, in one of the dashboard's slots: should it still run after
    /// <see cref="DashboardViewModel.GitCallPatience"/> by the read clock, the read becomes
    /// <see cref="IsOverdue"/> and its pass looks again. Dispose the handle when the call ends.
    /// </summary>
    public IDisposable GitCall()
    {
        var call = new GitCallPatience(this);
        call.Start(clock);
        return call;
    }

    /// <summary>The read has ended.</summary>
    public bool IsCompleted => _completion.Task.IsCompleted;

    /// <summary>The read has ended having read the row (WD27): not by following a tab, nor cancelled.</summary>
    public bool Counted { get; private set; }

    /// <summary>Completes when the read has ended.</summary>
    public Task Completion => _completion.Task;

    /// <summary>Raised on the UI thread when a wait starts or ends and when the read ends: its pass looks again.</summary>
    public event EventHandler? Progressed;

    /// <summary>
    /// Runs <paramref name="check"/> on the thread pool, as a folder check of this read: from its
    /// start until its result is back on the caller's context the read counts as waiting.
    /// </summary>
    public async Task<bool> CheckAsync(Func<bool> check, CancellationToken cancellationToken)
    {
        BeginWait();
        try
        {
            return await Task.Run(check, CancellationToken.None).WaitAsync(cancellationToken);
        }
        finally
        {
            EndWait();
        }
    }

    /// <summary>Waits for <paramref name="task"/> (a closed queue's idle task, 3.3), counting as waiting meanwhile.</summary>
    public async Task WaitAsync(Task task, CancellationToken cancellationToken)
    {
        BeginWait();
        try
        {
            await task.WaitAsync(cancellationToken);
        }
        finally
        {
            EndWait();
        }
    }

    /// <summary>The read has ended; <paramref name="counted"/> when it read the row.</summary>
    public void Complete(bool counted)
    {
        Counted = counted;
        _completion.TrySetResult();
        Progressed?.Invoke(this, EventArgs.Empty);
    }

    private void BeginWait()
    {
        Interlocked.Increment(ref _waits);
        Progressed?.Invoke(this, EventArgs.Empty);
    }

    private void EndWait()
    {
        Interlocked.Decrement(ref _waits);
        Progressed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The git call's patience ran out with the call still running: on the UI thread, the read is overdue and its pass looks again.</summary>
    private void BecomeOverdue()
    {
        void Mark()
        {
            if (Interlocked.Exchange(ref _overdue, 1) == 0 && !IsCompleted)
            {
                Progressed?.Invoke(this, EventArgs.Empty);
            }
        }

        if (_ui is null)
        {
            Mark();
        }
        else
        {
            _ui.Post(_ => Mark(), null);
        }
    }

    /// <summary>One git call's patience: a timer on the read clock, disposed when the call ends.</summary>
    private sealed class GitCallPatience(DashboardRowRead read) : IDisposable
    {
        private readonly Lock _gate = new();
        private ITimer? _timer;
        private bool _ended;

        public void Start(TimeProvider clock)
        {
            var timer = clock.CreateTimer(_ => Expire(), null, DashboardViewModel.GitCallPatience, Timeout.InfiniteTimeSpan);
            lock (_gate)
            {
                if (_ended)
                {
                    timer.Dispose();
                    return;
                }

                _timer = timer;
            }
        }

        public void Dispose()
        {
            ITimer? timer;
            lock (_gate)
            {
                _ended = true;
                (timer, _timer) = (_timer, null);
            }

            timer?.Dispose();
        }

        private void Expire()
        {
            lock (_gate)
            {
                if (_ended)
                {
                    return;
                }
            }

            read.BecomeOverdue();
        }
    }
}
