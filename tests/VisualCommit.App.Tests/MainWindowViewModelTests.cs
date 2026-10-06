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
            themes ?? new FakeThemes { Current = theme },
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
        Assert.Equal(AppTheme.Light, themes.Current);
        Assert.Equal(AppTheme.Light, settings.Current.Theme);
        Assert.Contains(nameof(MainWindowViewModel.Theme), changed);
        Assert.Contains(nameof(MainWindowViewModel.IsDarkTheme), changed);
        Assert.Contains(nameof(MainWindowViewModel.ThemeSwitchToolTip), changed);

        viewModel.ToggleThemeCommand.Execute(null);

        Assert.Equal(AppTheme.Dark, viewModel.Theme);
        Assert.Equal(AppTheme.Dark, themes.Current);
        Assert.Equal(AppTheme.Dark, settings.Current.Theme);
        Assert.Equal([AppTheme.Light, AppTheme.Dark], themes.Applied);
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

    internal sealed class FakeSettings : ISettingsStore
    {
        public AppSettings Current { get; private set; } = new();

        public void Update(Func<AppSettings, AppSettings> change) => Current = change(Current);
    }

    internal sealed class FakeThemes : IThemeService
    {
        public List<AppTheme> Applied { get; } = [];

        public AppTheme Current { get; set; } = AppTheme.Dark;

        public void Apply(AppTheme theme)
        {
            Applied.Add(theme);
            Current = theme;
        }
    }
}
