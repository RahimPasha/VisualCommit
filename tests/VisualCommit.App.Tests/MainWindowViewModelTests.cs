using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;
using Xunit;

namespace VisualCommit.App.Tests;

public class MainWindowViewModelTests
{
    private static readonly GitDetection GitFound = new(GitAvailability.Available, "/usr/bin/git", new GitVersion(2, 47, 1), null);

    private static MainWindowViewModel Create(
        AppTheme theme = AppTheme.Dark,
        GitDetection? git = null,
        FakeSettings? settings = null,
        FakeThemes? themes = null,
        IAppLog? log = null) =>
        new(
            settings ?? new FakeSettings(),
            themes ?? new FakeThemes { Setting = theme },
            _ => Task.FromResult(git ?? GitFound),
            log);

    [Fact]
    public void It_starts_in_the_theme_that_is_applied()
    {
        var dark = Create(AppTheme.Dark);
        var light = Create(AppTheme.Light);

        Assert.True(dark.IsDarkTheme);
        Assert.Equal("Switch to the light theme", dark.ThemeSwitchToolTip);
        Assert.False(light.IsDarkTheme);
        Assert.Equal("Switch to the dark theme", light.ThemeSwitchToolTip);
    }

    [Fact]
    public void Toggling_the_theme_applies_it_saves_it_and_tells_the_view()
    {
        var settings = new FakeSettings();
        var themes = new FakeThemes();
        var viewModel = Create(settings: settings, themes: themes);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.ToggleThemeCommand.Execute(null);

        Assert.Equal(AppTheme.Light, viewModel.Theme);
        Assert.Equal(AppTheme.Light, themes.Setting);
        Assert.Equal(AppTheme.Light, settings.Current.Theme);
        Assert.Contains(nameof(MainWindowViewModel.Theme), changed);
        Assert.Contains(nameof(MainWindowViewModel.IsDarkTheme), changed);
        Assert.Contains(nameof(MainWindowViewModel.ThemeSwitchToolTip), changed);

        viewModel.ToggleThemeCommand.Execute(null);

        Assert.Equal(AppTheme.Dark, viewModel.Theme);
        Assert.Equal(AppTheme.Dark, themes.Setting);
        Assert.Equal(AppTheme.Dark, settings.Current.Theme);
        Assert.Equal([AppTheme.Light, AppTheme.Dark], themes.Applied);
    }

    /// <summary>
    /// System chosen (docs/phase-6-design.md 11.6): the switch shows the theme a click switches to
    /// and says that the click stops following the system; the click picks the other of what
    /// shows, Light from a dark window and Dark from a light one, and saves it.
    /// </summary>
    [Theory]
    [InlineData(true, "Switch to the light theme (stops following the system)", AppTheme.Light, "Switch to the dark theme")]
    [InlineData(false, "Switch to the dark theme (stops following the system)", AppTheme.Dark, "Switch to the light theme")]
    public void With_System_chosen_the_switch_says_it_stops_following_and_a_click_saves_the_other_theme(
        bool systemDark, string toolTip, AppTheme clicked, string toolTipAfter)
    {
        var settings = new FakeSettings();
        settings.Update(current => current with { Theme = AppTheme.System });
        var themes = new FakeThemes { Setting = AppTheme.System, SystemIsDark = systemDark };
        var log = new RecordingLog();
        var viewModel = Create(settings: settings, themes: themes, log: log);

        Assert.Equal(AppTheme.System, viewModel.Theme);
        Assert.Equal(systemDark, viewModel.IsDarkTheme);
        Assert.Equal(toolTip, viewModel.ThemeSwitchToolTip);

        viewModel.ToggleThemeCommand.Execute(null);

        Assert.Equal([clicked], themes.Applied);
        Assert.Equal(clicked, viewModel.Theme);
        Assert.Equal(clicked == AppTheme.Dark, viewModel.IsDarkTheme);
        Assert.Equal(clicked, settings.Current.Theme);
        Assert.Equal(toolTipAfter, viewModel.ThemeSwitchToolTip);
        Assert.Equal([$"Theme changed to {clicked}."], ThemeLines(log));
    }

    [Fact]
    public void The_services_change_raises_the_theme_what_shows_and_the_tooltip()
    {
        var themes = new FakeThemes { Setting = AppTheme.System, SystemIsDark = true };
        var settings = new FakeSettings();
        var viewModel = Create(settings: settings, themes: themes);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // The system turns light while System is chosen: the service says so, and nothing is saved.
        themes.SetSystem(dark: false);

        Assert.Equal(
            [nameof(MainWindowViewModel.Theme), nameof(MainWindowViewModel.IsDarkTheme), nameof(MainWindowViewModel.ThemeSwitchToolTip)],
            changed);
        Assert.False(viewModel.IsDarkTheme);
        Assert.Equal(AppTheme.System, viewModel.Theme);
        Assert.Equal("Switch to the dark theme (stops following the system)", viewModel.ThemeSwitchToolTip);
        Assert.Equal(AppTheme.Dark, settings.Current.Theme);
        Assert.Empty(themes.Applied);
    }

    [Fact]
    public void UseTheme_applies_saves_and_logs_a_new_theme_and_does_nothing_for_the_chosen_one()
    {
        var settings = new FakeSettings();
        var themes = new FakeThemes();
        var log = new RecordingLog();
        var viewModel = Create(settings: settings, themes: themes, log: log);

        viewModel.UseTheme(AppTheme.Dark);

        Assert.Empty(themes.Applied);
        Assert.Empty(ThemeLines(log));

        viewModel.UseTheme(AppTheme.System);

        Assert.Equal([AppTheme.System], themes.Applied);
        Assert.Equal(AppTheme.System, viewModel.Theme);
        Assert.Equal(AppTheme.System, settings.Current.Theme);
        Assert.Equal(["Theme changed to System."], ThemeLines(log));

        viewModel.UseTheme(AppTheme.System);

        Assert.Equal([AppTheme.System], themes.Applied);
        Assert.Equal(["Theme changed to System."], ThemeLines(log));

        // System showing dark, Dark chosen (the palette lists it then, 11.7): the window stays
        // dark and Dark is saved.
        viewModel.UseTheme(AppTheme.Dark);

        Assert.Equal([AppTheme.System, AppTheme.Dark], themes.Applied);
        Assert.True(viewModel.IsDarkTheme);
        Assert.Equal(AppTheme.Dark, settings.Current.Theme);
        Assert.Equal(["Theme changed to System.", "Theme changed to Dark."], ThemeLines(log));
    }

    [Fact]
    public async Task The_status_bar_says_it_is_looking_for_git_until_git_is_found()
    {
        var search = new TaskCompletionSource<GitDetection>();
        var viewModel = new MainWindowViewModel(new FakeSettings(), new FakeThemes(), _ => search.Task);

        var initialization = viewModel.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Looking for Git...", viewModel.GitStatusText);
        Assert.Null(viewModel.Git);

        search.SetResult(GitFound);
        await initialization;

        Assert.Equal("Git 2.47.1", viewModel.GitStatusText);
        Assert.Same(GitFound, viewModel.Git);
    }

    [Theory]
    [InlineData(GitAvailability.NotFound, "Git not found")]
    [InlineData(GitAvailability.Broken, "Git is not working")]
    public async Task The_status_bar_says_when_git_is_missing_or_broken(GitAvailability availability, string expected)
    {
        var viewModel = Create(git: new GitDetection(availability, null, null, null));

        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, viewModel.GitStatusText);
    }

    [Fact]
    public async Task The_status_bar_names_the_version_that_is_too_old_and_the_one_that_is_needed()
    {
        var viewModel = Create(git: new GitDetection(GitAvailability.TooOld, "/usr/bin/git", new GitVersion(2, 20, 5), null));

        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Git 2.20.5 is too old (needs 2.30.0 or newer)", viewModel.GitStatusText);
    }

    [Fact]
    public async Task A_failing_search_for_git_is_logged_and_shown_not_thrown()
    {
        var log = new RecordingLog();
        var viewModel = new MainWindowViewModel(
            new FakeSettings(),
            new FakeThemes(),
            _ => Task.FromException<GitDetection>(new InvalidOperationException("boom")),
            log);

        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Git is not working", viewModel.GitStatusText);
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Error && entry.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task Closing_the_app_while_looking_for_git_is_not_an_error()
    {
        var log = new RecordingLog();
        var viewModel = new MainWindowViewModel(
            new FakeSettings(),
            new FakeThemes(),
            cancellationToken => Task.FromCanceled<GitDetection>(cancellationToken),
            log);
        using var closing = new CancellationTokenSource();
        await closing.CancelAsync();

        await viewModel.InitializeAsync(closing.Token);

        Assert.Empty(log.Entries);
    }

    /// <summary>The log's theme lines, among whatever else the view model logs.</summary>
    private static List<string> ThemeLines(RecordingLog log) =>
        [.. log.Messages.Where(message => message.StartsWith("Theme", StringComparison.Ordinal))];

    internal sealed class FakeSettings : ISettingsStore
    {
        public AppSettings Current { get; private set; } = new();

        public void Update(Func<AppSettings, AppSettings> change) => Current = change(Current);
    }

    /// <summary>The theme service's rules (docs/phase-6-design.md 11.4) without an application: System shows <see cref="SystemIsDark"/>.</summary>
    internal sealed class FakeThemes : IThemeService
    {
        public List<AppTheme> Applied { get; } = [];

        public AppTheme Setting { get; set; } = AppTheme.Dark;

        /// <summary>The system's mode, which System shows.</summary>
        public bool SystemIsDark { get; set; } = true;

        public bool IsDark => Setting switch
        {
            AppTheme.Light => false,
            AppTheme.System => SystemIsDark,
            _ => true,
        };

        public event EventHandler? Changed;

        public void Apply(AppTheme theme)
        {
            Applied.Add(theme);
            Setting = theme;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>The system's mode changes: while System is chosen what shows follows, and <see cref="Changed"/> is raised.</summary>
        public void SetSystem(bool dark)
        {
            var changed = Setting == AppTheme.System && dark != SystemIsDark;
            SystemIsDark = dark;
            if (changed)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
