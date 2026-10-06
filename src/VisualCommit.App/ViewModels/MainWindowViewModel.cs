using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels;

/// <summary>The view model of the main window: the theme switch and what the status bar shows.</summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly IThemeService _themes;
    private readonly Func<CancellationToken, Task<GitDetection>> _detectGit;
    private readonly IAppLog _log;

    public MainWindowViewModel(
        ISettingsStore settings,
        IThemeService themes,
        Func<CancellationToken, Task<GitDetection>> detectGit,
        IAppLog? log = null)
    {
        _settings = settings;
        _themes = themes;
        _detectGit = detectGit;
        _log = log ?? NullAppLog.Instance;
        Theme = themes.Current;
    }

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

    /// <summary>Start-up work that needs the window to exist first: finds git and reports it in the status bar.</summary>
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
        }
        catch (Exception ex)
        {
            _log.Error("Looking for Git failed.", ex);
            GitStatusText = "Git is not working";
        }
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

    private static string Describe(GitDetection git) => git.Availability switch
    {
        GitAvailability.Available => $"Git {git.Version}",
        GitAvailability.TooOld => $"Git {git.Version} is too old (needs {GitVersion.Minimum} or newer)",
        GitAvailability.NotFound => "Git not found",
        _ => "Git is not working",
    };
}
