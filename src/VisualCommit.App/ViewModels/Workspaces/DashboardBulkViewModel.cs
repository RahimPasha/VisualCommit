using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// A dashboard's bulk runs as its header, its tab's status bar and its toolbar show them
/// (docs/phase-6-design.md 4.3, 4.9, 4.10, D208, D210): one per dashboard
/// (<see cref="DashboardViewModel.Bulk"/>). It makes a run on the click of Fetch all or Pull all
/// and keeps it as <see cref="Current"/> after it ends, for its result and its rows' Details…,
/// until the next run starts or the dashboard closes; one run at a time.
/// <para>
/// As <see cref="IStatusBarOperation"/> (the status bar's source on a dashboard tab, 4.9): "Ready"
/// before the first run, then the run's text, tone and determinate progress, Cancel = Cancel all
/// while it runs until clicked, "{w} waiting" for the rows in line, and ssh's newest notice in
/// place of the text while one shows. The header shows <see cref="RunText"/>, the run's text
/// without the notice. As <see cref="IDashboardToolbar"/> (4.10): Fetch all and Pull all enabled
/// while rows are being read, disabled with the first reason that holds.
/// </para>
/// </summary>
public sealed partial class DashboardBulkViewModel : ObservableObject, IStatusBarOperation, IDashboardToolbar
{
    private readonly DashboardViewModel _dashboard;
    private readonly PromptNotices _notices;
    private readonly HashSet<DashboardRowViewModel> _watched = [];
    private bool _closed;

    public DashboardBulkViewModel(DashboardViewModel dashboard)
    {
        ArgumentNullException.ThrowIfNull(dashboard);
        _dashboard = dashboard;
        _notices = dashboard.Services.DialogHost.Notices;
        _notices.PropertyChanged += OnNoticesChanged;
        CancelCommand = new RelayCommand(CancelAll, () => CanCancel);
        dashboard.PropertyChanged += OnDashboardChanged;
        dashboard.Rows.CollectionChanged += OnRowsChanged;
        Watch(dashboard.Rows);
        UpdateToolbar();
    }

    /// <summary>The dashboard's last run, running or ended; null before its first.</summary>
    public BulkRun? Current { get; private set; }

    // IStatusBarOperation (4.9).

    /// <summary>The status bar's text: ssh's notice while one shows, else <see cref="RunText"/>, or "Ready" before the first run.</summary>
    public string Text => _notices.Text ?? RunText ?? OperationStatusViewModel.ReadyText;

    public OperationStatusTone Tone => _notices.Text is not null ? OperationStatusTone.Secondary : RunTone;

    public string? ToolTip => _notices.Text ?? RunText;

    /// <summary>A run runs: the progress bar shows.</summary>
    public bool IsRunning => Current is { IsRunning: true };

    /// <summary>The bar is determinate (4.9).</summary>
    public bool IsIndeterminate => false;

    /// <summary><c>⌊100 × {k} / {n}⌋</c> while a run runs; null otherwise.</summary>
    public int? Percent => Current is { IsRunning: true } run ? run.Percent : null;

    /// <summary>Cancel all is offered: while a run runs, until it is clicked (4.5).</summary>
    public bool CanCancel => Current is { IsRunning: true, IsCancelling: false };

    /// <summary>Cancel all (4.5): the header's <c>DashboardCancelAll</c> and the status bar's Cancel.</summary>
    public ICommand CancelCommand { get; }

    /// <summary>"Cancel the rest of Fetch all" / "Cancel the rest of Pull all".</summary>
    public string CancelToolTip => Current is { } run ? OperationTexts.CancelTheRestOf(run.Kind) : string.Empty;

    /// <summary>"4 waiting": the rows in line.</summary>
    public string WaitingText => string.Create(CultureInfo.InvariantCulture, $"{Waiting} waiting");

    public bool HasWaiting => Waiting > 0;

    /// <summary>The rows in line of the running run.</summary>
    public int Waiting => Current is { IsRunning: true } run ? run.InLine : 0;

    /// <summary>A dashboard's bulk runs make no offer (docs/phase-7a-design.md 12.2): its rows keep their compact texts.</summary>
    public string? OfferText => null;

    public bool HasOffer => false;

    public ICommand OfferCommand => NoOffer;

    /// <summary>The offer's command of every dashboard: it never runs.</summary>
    private static readonly ICommand NoOffer = new RelayCommand(() => { }, () => false);

    // The header's run part (2.5).

    /// <summary>The header's <c>DashboardBulkText</c>: the run's text (4.9), null before the first run.</summary>
    public string? RunText => Current?.Text;

    /// <summary>The run's tone: danger when a row failed, warning when one stopped, else secondary.</summary>
    public OperationStatusTone RunTone => Current?.Tone ?? OperationStatusTone.Secondary;

    // IDashboardToolbar (4.10).

    public string WorkspaceName => _dashboard.Name;

    public IAppLog Log => _dashboard.Services.Log;

    public bool CanFetchAll => FetchAllDisabledReason is null && !_closed;

    public string? FetchAllDisabledReason { get; private set; }

    public bool CanPullAll => PullAllDisabledReason is null && !_closed;

    public string? PullAllDisabledReason { get; private set; }

    public Task FetchAllAsync() => RunAsync(BulkKind.Fetch);

    public Task PullAllAsync() => RunAsync(BulkKind.Pull);

    /// <summary>
    /// The running run starts no more rows (4.5, 4.12): its rows in line and still reading end
    /// "cancelled", its running rows go on. The window's close calls it before the queues drop
    /// what waits.
    /// </summary>
    public void StopStarting() => Current?.StopStarting();

    /// <summary>Cancel all (4.5).</summary>
    public void CancelAll() => Current?.CancelAll();

    /// <summary>
    /// The dashboard closes (its tab's close, "Delete workspace…", the window's <c>CloseAll</c>): its
    /// run is cancelled as Cancel all does and abandoned (4.5, 4.6, 4.12); nothing more is watched.
    /// </summary>
    public void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        Current?.Abandon();
        if (Current is { } run)
        {
            run.Changed -= OnRunChanged;
        }

        _notices.PropertyChanged -= OnNoticesChanged;
        _dashboard.PropertyChanged -= OnDashboardChanged;
        _dashboard.Rows.CollectionChanged -= OnRowsChanged;
        foreach (var row in _watched)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        _watched.Clear();
    }

    /// <summary>Starts a run of <paramref name="kind"/> over the dashboard's rows when it can, and completes when it has ended.</summary>
    private Task RunAsync(BulkKind kind)
    {
        if (_closed || (kind == BulkKind.Fetch ? !CanFetchAll : !CanPullAll))
        {
            return Task.CompletedTask;
        }

        if (Current is { } previous)
        {
            previous.Changed -= OnRunChanged;
        }

        var services = _dashboard.Services;
        var run = new BulkRun(kind, _dashboard.Name, [.. _dashboard.Rows], services.BulkSlots, services.OpenRepositories, services.Log);
        Current = run;
        run.Changed += OnRunChanged;
        run.Start();
        RaiseAll();
        return run.Completion;
    }

    private void OnRunChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, Current))
        {
            RaiseAll();
        }
    }

    private void RaiseAll()
    {
        UpdateToolbar();
        foreach (var name in RunProperties)
        {
            OnPropertyChanged(name);
        }

        ((RelayCommand)CancelCommand).NotifyCanExecuteChanged();
    }

    /// <summary>What a run's change can change.</summary>
    private static readonly string[] RunProperties =
    [
        nameof(Current), nameof(RunText), nameof(RunTone), nameof(Text), nameof(Tone), nameof(ToolTip), nameof(IsRunning),
        nameof(Percent), nameof(CanCancel), nameof(CancelToolTip), nameof(Waiting), nameof(WaitingText), nameof(HasWaiting),
    ];

    private void OnNoticesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(PromptNotices.Text))
        {
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(Tone));
            OnPropertyChanged(nameof(ToolTip));
        }
    }

    private void OnDashboardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(DashboardViewModel.Name))
        {
            OnPropertyChanged(nameof(WorkspaceName));
            UpdateToolbar();
        }
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var row in _watched.Where(row => !_dashboard.Rows.Contains(row)).ToList())
        {
            row.PropertyChanged -= OnRowChanged;
            _watched.Remove(row);
        }

        Watch(_dashboard.Rows);
        UpdateToolbar();
    }

    private void Watch(IEnumerable<DashboardRowViewModel> rows)
    {
        foreach (var row in rows)
        {
            if (_watched.Add(row))
            {
                row.PropertyChanged += OnRowChanged;
            }
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null
            or nameof(DashboardRowViewModel.HasRepository)
            or nameof(DashboardRowViewModel.IsNotReadYet)
            or nameof(DashboardRowViewModel.Refs)
            or nameof(DashboardRowViewModel.Status)
            or nameof(DashboardRowViewModel.FolderState)
            or nameof(DashboardRowViewModel.ErrorText)
            or nameof(DashboardRowViewModel.Repository))
        {
            UpdateToolbar();
        }
    }

    /// <summary>
    /// 4.10's states, the first reason that holds: a run of this dashboard runs; the workspace has
    /// no repositories; no row would be acted on and none is being read (a row without a live view
    /// model counts as being read: the run reads it first).
    /// </summary>
    private void UpdateToolbar()
    {
        var (fetch, pull) = (Reason(BulkKind.Fetch), Reason(BulkKind.Pull));
        if (fetch == FetchAllDisabledReason && pull == PullAllDisabledReason)
        {
            return;
        }

        (FetchAllDisabledReason, PullAllDisabledReason) = (fetch, pull);
        OnPropertyChanged(nameof(FetchAllDisabledReason));
        OnPropertyChanged(nameof(CanFetchAll));
        OnPropertyChanged(nameof(PullAllDisabledReason));
        OnPropertyChanged(nameof(CanPullAll));
    }

    private string? Reason(BulkKind kind)
    {
        if (Current is { IsRunning: true } run)
        {
            return OperationTexts.WaitForBulkRun(run.Kind);
        }

        var rows = _dashboard.Rows;
        if (rows.Count == 0)
        {
            return DashboardTexts.NoRepositoriesText(_dashboard.Name);
        }

        return rows.Any(row => WouldAct(row, kind)) ? null
            : kind == BulkKind.Fetch ? OperationTexts.NoRepositoryHasRemote(_dashboard.Name)
            : OperationTexts.NoRepositoryCanBePulled(_dashboard.Name);
    }

    /// <summary>Whether a run of <paramref name="kind"/> started now could act on <paramref name="row"/>: the run's own rule does not leave it out (it would act, or it reads the row first).</summary>
    private static bool WouldAct(DashboardRowViewModel row, BulkKind kind) => BulkRun.Decide(row, kind) is not BulkRun.Decision.Skip;
}
