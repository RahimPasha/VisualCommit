using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// The view model of the main window: the repo tabs, the theme switch and what the status bar
/// shows. The window's regions show the active tab. Phase 6's parts each live in a file of their
/// own (docs/phase-6-design.md 18): the session's tabs (<c>.Session.cs</c>), the workspaces and
/// their dashboards (<c>.Workspaces.cs</c>), the keys and the palette (<c>.Commands.cs</c>), the
/// settings screen (<c>.Settings.cs</c>) and the theme (<c>.Theme.cs</c>).
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly IThemeService _themes;
    private readonly Func<CancellationToken, Task<GitDetection>> _detectGit;
    private readonly IAppLog _log;
    private readonly ISessionStore _session;
    private readonly TabServices _tabServices;

    /// <summary>
    /// The repositories of tabs that were closed while an operation the user started ran in them
    /// (1.8): a local one goes on to its end, every step of it, so the window waits for it as for
    /// an open tab's. Each is kept until its queue is idle. Only touched on the UI thread.
    /// </summary>
    private readonly List<RepositoryViewModel> _closedButRunning = [];
    private bool _restoring;
    private bool _closeConfirmed;
    private FinishingDialogViewModel? _finishing;

    public MainWindowViewModel(
        ISettingsStore settings,
        IThemeService themes,
        Func<CancellationToken, Task<GitDetection>> detectGit,
        IAppLog? log = null,
        TabServices? tabServices = null,
        ISessionStore? session = null,
        Func<string, string?>? readEnvironment = null)
    {
        _settings = settings;
        _themes = themes;
        _detectGit = detectGit;
        _log = log ?? NullAppLog.Instance;
        _session = session ?? new MemorySessionStore();
        _tabServices = tabServices ?? new TabServices(
            new UnavailableRepositoryProvider(), new NoFolderPicker(), settings, _session, DateDisplay.Resolve(), _log);

        // Every dialog of a repository's operations follows the window's one rule (4f).
        _tabServices.NamesRepositoryInDialogs = () => NamesRepositoryInDialogs;

        // A workspace opens through the window (docs/phase-6-design.md 1.3, 2.2).
        _tabServices.WorkspaceHost = this;

        // The theme shown follows the service: the user's choice, or the system's mode (11.6).
        FollowThemeService();

        // The editor failure's "Open settings…" opens the settings screen through the window (12.7).
        WireSettings();

        // A menu that waited for a read opens only over its own tab, and never over a dialog (4a).
        // On a dashboard the log line names the workspace (docs/phase-6-design.md 2.7).
        Menu = new MenuLayerViewModel(
            _log,
            () => ActiveTab?.Repository?.Name ?? ActiveTab?.Dashboard?.Name,
            owner => ReferenceEquals(ActiveTab?.Repository, owner) && !Dialogs.IsOpen);

        // The dashboards' rows open their repositories through the window (docs/phase-6-design.md 2.3).
        WatchDashboards();

        // The tabs of the last session are there from the first frame, under their folder names;
        // InitializeAsync opens them once git has been found.
        RestoreTabs(_session.Current);
        UpdateCanClose();

        // The activity log reads the session's records from its start, whether its panel is open
        // or not, and shows the active tab's worktree (docs/phase-5-design.md 2.1, D156).
        ActivityLog = new ActivityLogViewModel(_tabServices.Operations, _tabServices.GitCalls, _session, _tabServices.Dates, _log);
        ActivityLog.Follow(this);

        // Auto-fetch looks at the active tab's repository (docs/phase-3-design.md 6.1, D85).
        // Its interval's environment variable is read through the session's reader (docs/phase-6-design.md 7.3).
        AutoFetch = new AutoFetchScheduler(
            () => ActiveTab.Repository,
            () => Dialogs.IsOpen,
            settings,
            _tabServices.Clock,
            _log)
        {
            ReadEnvironment = readEnvironment ?? Environment.GetEnvironmentVariable,
        };
        AutoFetch.Start();
    }

    /// <summary>The window's dialog layer (D69, D104): what the overlay shows, and one of Esc's targets.</summary>
    public DialogHostViewModel Dialogs => _tabServices.DialogHost;

    /// <summary>The window's in-window context menu layer (docs/phase-3-design.md 4a, D86).</summary>
    public MenuLayerViewModel Menu { get; }

    /// <summary>
    /// The one rule of docs/phase-3-design.md 4f: a dialog that an operation of a repository opens
    /// (a confirmation, the name, choice, pick, push, reset, stash, tag and remotes dialogs, git's
    /// and ssh's prompts) names that repository, "In &lt;repo&gt;" under its title, while more than
    /// one tab has a repository. The error dialog names it always. Every tab's operations and the
    /// prompt dialogs read it when a dialog opens (<see cref="TabServices.NamesRepositoryInDialogs"/>).
    /// </summary>
    public bool NamesRepositoryInDialogs => AnyDashboardOpen || Tabs.Count(tab => tab.Repository is not null) > 1;

    /// <summary>Fetches the active tab's repository in the background (6.1, D85).</summary>
    public AutoFetchScheduler AutoFetch { get; }

    /// <summary>
    /// The activity log (docs/phase-5-design.md 2, D156): the panel under the graph area, opened by
    /// the status bar's toggle, for the active tab's worktree. Its being open is the window's.
    /// </summary>
    public ActivityLogViewModel ActivityLog { get; }

    /// <summary>What was found when the app looked for git. Null until the search has finished.</summary>
    [ObservableProperty]
    public partial GitDetection? Git { get; private set; }

    /// <summary>The git part of the status bar, such as "Git 2.47.0".</summary>
    [ObservableProperty]
    public partial string GitStatusText { get; private set; } = "Looking for Git...";

    /// <summary>The open tabs, from left to right. Never empty.</summary>
    public ObservableCollection<RepoTabViewModel> Tabs { get; } = [];

    /// <summary>The tab whose repository the window shows.</summary>
    [ObservableProperty]
    public partial RepoTabViewModel ActiveTab { get; private set; } = null!;

    /// <summary>
    /// Start-up work that needs the window to exist first: finds git and reports it in the status
    /// bar, then opens the repositories of the tabs restored from the last session. Completes
    /// when every restored tab has loaded its history.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var git = await _detectGit(cancellationToken);
            Git = git;
            GitStatusText = Describe(git);
        }
        catch (OperationCanceledException)
        {
            // The app is closing.
            return;
        }
        catch (Exception ex)
        {
            _log.Error("Looking for Git failed.", ex);
            GitStatusText = "Git is not working";
        }

        // A restored active dashboard reads its rows beside the restored tabs' openings
        // (docs/phase-6-design.md 2.10).
        var opening = Tabs
            .Where(tab => tab.PendingPath is not null)
            .Select(tab => tab.OpenAsync(tab.PendingPath!, remember: false))
            .ToList<Task>();

        // The merge tools git knows are looked up once start-up has settled: when the first
        // restored tab's load ends, or at once without one (docs/phase-6-design.md 8.10).
        _ = LookUpMergeToolsAfterAsync(opening);
        await Task.WhenAll(opening.Append(ReadActiveDashboardAtStartAsync()));

        // A restored tab that could not be opened is a "New tab" now; a lone one has no close button.
        UpdateCanClose();
    }

    /// <summary>Opens a new, empty tab after the last one and shows it.</summary>
    [RelayCommand]
    private void NewTab()
    {
        Activate(AddTab());
        UpdateCanClose();
        SaveTabs();
    }

    [RelayCommand]
    private void ActivateTab(RepoTabViewModel? tab)
    {
        if (tab is not null && Tabs.Contains(tab))
        {
            Activate(tab);
        }
    }

    /// <summary>Closes a tab and stops all its work. Closing the last tab leaves one new, empty tab.</summary>
    [RelayCommand]
    private void CloseTab(RepoTabViewModel? tab)
    {
        if (tab is null || !Tabs.Contains(tab))
        {
            return;
        }

        var index = Tabs.IndexOf(tab);
        Tabs.RemoveAt(index);
        tab.RepositoryOpened -= OnRepositoryOpened;
        tab.Dispose();
        if (tab.Repository is { } repository && repository.Queue.Current is { IsBackground: false })
        {
            _ = KeepUntilIdleAsync(repository);
        }

        if (Tabs.Count == 0)
        {
            AddTab();
        }

        if (tab.IsActive)
        {
            Activate(Tabs[Math.Min(index, Tabs.Count - 1)]);
        }

        UpdateCanClose();
        SaveTabs();
    }

    /// <summary>
    /// Stops the work of every tab. The session keeps the tabs for the next start. A clone that
    /// is still running is cancelled, and its half-written folder removed, before this returns
    /// (at most a few seconds), since the process may end right after.
    /// </summary>
    public void CloseAll()
    {
        AutoFetch.Dispose();
        ActivityLog.Dispose();
        var clones = Tabs.Select(tab => tab.Welcome.RunningClone).OfType<Task>().ToArray();
        foreach (var tab in Tabs)
        {
            tab.Dispose();
        }

        // The dashboards released their summaries above (D81's rules); what is left goes now
        // (docs/phase-6-design.md 3.7).
        _tabServices.OpenRepositories.DisposeAll();

        if (clones.Length > 0)
        {
            try
            {
                Task.WaitAll(clones, TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Cancelled or failed: the clone removed its folder either way.
            }
        }
    }

    /// <summary>
    /// The window is closing (docs/phase-3-design.md 1.8): returns true when it may close now.
    /// While a repository runs an operation the user started, the close waits (also for the local
    /// operation of a tab closed meanwhile, which goes on to its end): what waits is
    /// dropped, "Finishing operations" lists what runs, and <paramref name="closeWindow"/> is
    /// called when they have ended, or when the user chooses "Quit now". A background auto-fetch
    /// does not hold the window. Once the close is confirmed, every later call returns true.
    /// <para>
    /// What a close does follows the dialog's own state, not when its answer is acted on (a
    /// continuation that runs later on the UI thread): while the dialog is open, or after "Quit
    /// now" until the window closes, a close only waits; once "Keep open" has closed the dialog,
    /// the next close asks again at once.
    /// </para>
    /// </summary>
    public bool TryClose(Action closeWindow)
    {
        ArgumentNullException.ThrowIfNull(closeWindow);
        if (_closeConfirmed)
        {
            return true;
        }

        // The dashboards' view models too (docs/phase-6-design.md 3.7).
        var listed = RepositoriesToFinish().ToList();
        var running = listed
            .Select(repository => (Repository: repository, Current: repository.Queue.Current))
            .Where(entry => entry.Current is { IsBackground: false })
            .ToList();
        if (running.Count == 0)
        {
            _closeConfirmed = true;
            return true;
        }

        if (_finishing is not { } finishing || (finishing.IsClosed && !QuitNowChosen(finishing)))
        {
            // Bulk runs start no more rows first (4.12), then the queues drop what waits.
            StopBulkRunsStarting();
            foreach (var repository in listed)
            {
                repository.Queue.DropWaiting();
            }

            _ = FinishThenCloseAsync(running.Select(entry => entry.Repository).ToList(), closeWindow);
        }

        return false;
    }

    /// <summary>Keeps a closed tab's repository in <see cref="_closedButRunning"/> until its running operation has ended.</summary>
    private async Task KeepUntilIdleAsync(RepositoryViewModel repository)
    {
        _closedButRunning.Add(repository);
        try
        {
            await repository.Queue.WhenIdleAsync();
        }
        finally
        {
            _closedButRunning.Remove(repository);
        }
    }

    /// <summary>Confirms the close without waiting: the app session ends (its owner disposes it).</summary>
    public void ConfirmClose() => _closeConfirmed = true;

    /// <summary>Whether the user answered <paramref name="dialog"/> with "Quit now".</summary>
    private static bool QuitNowChosen(FinishingDialogViewModel dialog) =>
        dialog.Result.IsCompletedSuccessfully && dialog.Result.Result;

    /// <summary>
    /// Shows "Finishing operations" and closes the window when the operations have ended or the
    /// user chooses "Quit now". <see cref="_finishing"/> keeps the dialog afterwards, also after
    /// "Keep open": <see cref="TryClose"/> goes by its state.
    /// </summary>
    private async Task FinishThenCloseAsync(IReadOnlyList<RepositoryViewModel> repositories, Action closeWindow)
    {
        var items = repositories
            .Select(repository => (Repository: repository, Current: repository.Queue.Current))
            .Where(entry => entry.Current is { IsBackground: false })
            .Select(entry => new FinishingItem(
                $"{entry.Current!.Title} — {entry.Repository.Name}",
                entry.Current.Kind == OperationKind.Local && entry.Current.HasSeveralSteps))
            .ToList();
        var dialog = new FinishingDialogViewModel(items);
        _finishing = dialog;
        try
        {
            var idle = Task.WhenAll(repositories.Select(repository => repository.Queue.WhenIdleAsync()));
            var answer = Dialogs.ShowAsync(dialog);
            var first = await Task.WhenAny(idle, answer);
            if (first == idle)
            {
                dialog.CloseAsFinished();
            }
            else if (!await answer)
            {
                // "Keep open": the window stays and the operations go on.
                return;
            }

            _closeConfirmed = true;
            closeWindow();
        }
        catch (Exception ex)
        {
            _log.Error("Closing the window after the running operations failed.", ex);

            // A dialog that may never close must not keep the next close waiting.
            if (ReferenceEquals(_finishing, dialog))
            {
                _finishing = null;
            }
        }
    }

    private RepoTabViewModel AddTab()
    {
        var tab = new RepoTabViewModel(_tabServices);
        tab.RepositoryOpened += OnRepositoryOpened;
        Tabs.Add(tab);
        return tab;
    }

    private void Activate(RepoTabViewModel tab)
    {
        foreach (var other in Tabs)
        {
            other.IsActive = ReferenceEquals(other, tab);
        }

        ActiveTab = tab;
        if (!_restoring)
        {
            // A dashboard reads its rows when it becomes the active tab (docs/phase-6-design.md 2.10).
            tab.Dashboard?.OnBecameActive();
            SaveTabs();
        }
    }

    private void OnRepositoryOpened(object? sender, EventArgs e)
    {
        UpdateCanClose();
        SaveTabs();
    }

    private void UpdateCanClose()
    {
        foreach (var tab in Tabs)
        {
            tab.CanClose = Tabs.Count > 1 || tab.HasRepository || tab.PendingPath is not null || tab.IsDashboard;
        }
    }

    private void SaveTabs()
    {
        var tabs = SavedTabStates();
        var active = Math.Max(0, Tabs.IndexOf(ActiveTab));
        _session.Update(state => state with { Tabs = tabs, ActiveTab = active });
    }

    private static string Describe(GitDetection git) => git.Availability switch
    {
        GitAvailability.Available => $"Git {git.Version}",
        GitAvailability.TooOld => $"Git {git.Version} is too old (needs {GitVersion.Minimum} or newer)",
        GitAvailability.NotFound => "Git not found",
        _ => "Git is not working",
    };

    /// <summary>A session that is not saved anywhere, for view-model tests that do not need one.</summary>
    private sealed class MemorySessionStore : ISessionStore
    {
        public SessionState Current { get; private set; } = new();

        public void Update(Func<SessionState, SessionState> change) => Current = change(Current);
    }

    /// <summary>Opening repositories is not possible: for view-model tests that do not open any.</summary>
    private sealed class UnavailableRepositoryProvider : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) =>
            throw new GitUnavailableException("No repositories can be opened here.");

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) =>
            throw new GitUnavailableException("No repositories can be created here.");

        public Task<IGitRepository> CloneAsync(
            string url,
            string destination,
            IProgress<CloneProgress>? progress,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string?>? environment = null) =>
            throw new GitUnavailableException("No repositories can be cloned here.");
    }

    private sealed class NoFolderPicker : IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
