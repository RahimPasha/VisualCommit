using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// The view model of the main window: the repo tabs, the theme switch and what the status bar
/// shows. The window's regions show the active tab.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly IThemeService _themes;
    private readonly Func<CancellationToken, Task<GitDetection>> _detectGit;
    private readonly IAppLog _log;
    private readonly ISessionStore _session;
    private readonly TabServices _tabServices;
    private readonly DialogHostViewModel _fallbackDialogs = new();
    private bool _restoring;

    public MainWindowViewModel(
        ISettingsStore settings,
        IThemeService themes,
        Func<CancellationToken, Task<GitDetection>> detectGit,
        IAppLog? log = null,
        TabServices? tabServices = null,
        ISessionStore? session = null)
    {
        _settings = settings;
        _themes = themes;
        _detectGit = detectGit;
        _log = log ?? NullAppLog.Instance;
        _session = session ?? new MemorySessionStore();
        _tabServices = tabServices ?? new TabServices(
            new UnavailableRepositoryProvider(), new NoFolderPicker(), settings, _session, DateDisplay.Resolve(), _log);
        Theme = themes.Current;

        // The tabs of the last session are there from the first frame, under their folder names;
        // InitializeAsync opens them once git has been found.
        var saved = _session.Current;
        foreach (var tab in saved.Tabs)
        {
            AddTab();
        }

        if (Tabs.Count == 0)
        {
            AddTab();
        }

        for (var i = 0; i < saved.Tabs.Count; i++)
        {
            if (saved.Tabs[i].RepositoryPath is { } path && !string.IsNullOrWhiteSpace(path))
            {
                Tabs[i].ShowPending(path);
            }
        }

        _restoring = true;
        Activate(Tabs[saved.ActiveTab >= 0 && saved.ActiveTab < Tabs.Count ? saved.ActiveTab : 0]);
        _restoring = false;
        UpdateCanClose();
    }

    /// <summary>The window's dialog layer (D69): what the overlay shows, and Esc's first target.</summary>
    public DialogHostViewModel Dialogs => _tabServices.Dialogs as DialogHostViewModel ?? _fallbackDialogs;

    /// <summary>The theme the app is showing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDarkTheme))]
    [NotifyPropertyChangedFor(nameof(ThemeSwitchToolTip))]
    public partial AppTheme Theme { get; private set; }

    public bool IsDarkTheme => Theme == AppTheme.Dark;

    public string ThemeSwitchToolTip => IsDarkTheme ? "Switch to the light theme" : "Switch to the dark theme";

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

        var opening = Tabs
            .Where(tab => tab.PendingPath is not null)
            .Select(tab => tab.OpenAsync(tab.PendingPath!, remember: false))
            .ToList();
        await Task.WhenAll(opening);

        // A restored tab that could not be opened is a "New tab" now; a lone one has no close button.
        UpdateCanClose();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var next = IsDarkTheme ? AppTheme.Light : AppTheme.Dark;
        _themes.Apply(next);
        Theme = next;
        _settings.Update(settings => settings with { Theme = next });
        _log.Info($"Theme changed to {next}.");
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
        var clones = Tabs.Select(tab => tab.Welcome.RunningClone).OfType<Task>().ToArray();
        foreach (var tab in Tabs)
        {
            tab.Dispose();
        }

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
            tab.CanClose = Tabs.Count > 1 || tab.HasRepository || tab.PendingPath is not null;
        }
    }

    private void SaveTabs()
    {
        var tabs = Tabs.Select(tab => new TabState(tab.SavedPath)).ToList();
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

        public Task<IGitRepository> CloneAsync(string url, string destination, IProgress<CloneProgress>? progress, CancellationToken cancellationToken = default) =>
            throw new GitUnavailableException("No repositories can be cloned here.");
    }

    private sealed class NoFolderPicker : IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
