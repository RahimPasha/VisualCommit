using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.Views;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Headless UI tests of the shell's behaviour. What the shell looks like, region by region, is
/// asserted by the scripted walk-through in VisualCommit.VisualTests.
/// </summary>
public class ShellTests
{
    [AvaloniaFact]
    public async Task Starting_a_session_shows_the_shell_and_finds_git()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);

        await app.WaitUntilReadyAsync();

        Assert.True(app.Window.IsVisible);
        Assert.Equal("VisualCommit", app.Window.Title);
        Assert.Equal(new Size(1100, 700), app.Window.ClientSize);
        Assert.Equal(GitAvailability.Available, app.Session.Shell.Git?.Availability);
        Assert.Equal($"Git {app.Session.Shell.Git!.Version}", app.Find<TextBlock>("GitStatus").Text);

        // Finding git went through the runner, so it is in the session's record of git calls.
        Assert.Equal("git --version", Assert.Single(app.Session.GitCalls.Snapshot()).CommandText);
    }

    [AvaloniaFact]
    public async Task A_session_writes_its_start_and_its_end_to_the_log_in_the_data_folder()
    {
        using var data = new TempDirectory("data");
        using (var app = ShellDriver.Start(data.Path))
        {
            await app.WaitUntilReadyAsync();
        }

        var logFile = Assert.Single(Directory.GetFiles(Path.Combine(data.Path, "logs")));
        var log = File.ReadAllText(logFile);
        Assert.Contains($"VisualCommit {AppSession.AppVersion} started. Data folder: {data.Path}.", log);
        Assert.Contains("Using Git ", log);
        Assert.Contains("git --version: exit 0", log);
        Assert.Contains("VisualCommit closed.", log);
    }

    [AvaloniaFact]
    public async Task Clicking_the_theme_switch_changes_the_theme_of_the_application_and_saves_it()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var themeSwitch = app.Find<ToolbarButton>("ThemeSwitch");
        Assert.Equal(ThemeVariant.Dark, Application.Current!.ActualThemeVariant);
        Assert.Equal("Switch to the light theme", ToolTip.GetTip(themeSwitch));

        app.Click(themeSwitch);

        Assert.Equal(ThemeVariant.Light, Application.Current.ActualThemeVariant);
        Assert.Equal(AppTheme.Light, app.Session.Settings.Current.Theme);
        Assert.Equal(AppTheme.Light, new JsonSettingsStore(app.Session.Paths.SettingsFile).Current.Theme);
        Assert.Equal("Switch to the dark theme", ToolTip.GetTip(themeSwitch));
    }

    [AvaloniaFact]
    public void A_session_applies_the_saved_theme_before_it_creates_the_window()
    {
        using var data = new TempDirectory("data");
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = AppTheme.Light });

        using var app = ShellDriver.Start(data.Path);

        Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
        Assert.Equal(ThemeVariant.Light, app.Window.ActualThemeVariant);
        Assert.False(app.Session.Shell.IsDarkTheme);
    }

    [AvaloniaFact]
    public async Task Clicking_a_disabled_toolbar_button_does_nothing()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var before = app.Capture();

        foreach (var button in app.Window.GetVisualDescendants().OfType<ToolbarButton>().Where(button => !button.IsEnabled))
        {
            app.Click(button);
        }

        Assert.Equal(ThemeVariant.Dark, Application.Current!.ActualThemeVariant);
        Assert.False(File.Exists(app.Session.Paths.SettingsFile));
        var after = app.Capture();
        Assert.Equal(before.PixelAt(550, 400), after.PixelAt(550, 400));
    }

    [AvaloniaFact]
    public async Task The_panels_stop_at_their_minimum_and_maximum_widths()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path, 1920, 1080);
        await app.WaitUntilReadyAsync();
        var left = app.FindByAutomationId("LeftPanel");
        var right = app.FindByAutomationId("RightPanel");
        var y = app.BoundsOf(left).Center.Y;

        app.Drag(new Point(260, y), new Point(20, y));
        Assert.Equal(180, app.BoundsOf(left).Width);

        app.Drag(new Point(180, y), new Point(1000, y));
        Assert.Equal(520, app.BoundsOf(left).Width);

        app.Drag(new Point(1920 - 400, y), new Point(1900, y));
        Assert.Equal(280, app.BoundsOf(right).Width);

        app.Drag(new Point(1920 - 280, y), new Point(600, y));
        Assert.Equal(720, app.BoundsOf(right).Width);
    }

    [AvaloniaFact]
    public async Task At_the_smallest_window_size_all_text_is_still_shown_in_full()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();

        app.Window.Width = app.Window.MinWidth;
        app.Window.Height = app.Window.MinHeight;
        app.Settle();

        Assert.Equal(new Size(1000, 560), app.Window.ClientSize);
        Assert.Empty(LayoutAudit.FindClippedText(app.Window));
        Assert.True(app.BoundsOf(app.FindByAutomationId("CommitGraph")).Width >= 320);
    }

    [AvaloniaFact]
    public void The_status_bar_shows_what_the_view_model_reports_about_git()
    {
        var viewModel = new MainWindowViewModel(
            new MainWindowViewModelTests.FakeSettings(),
            new MainWindowViewModelTests.FakeThemes(),
            _ => Task.FromResult(new GitDetection(GitAvailability.NotFound, null, null, null)));
        var window = new MainWindow { DataContext = viewModel, Width = 1100, Height = 700 };
        window.Show();
        try
        {
            var status = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "GitStatus");
            Assert.Equal("Looking for Git...", status.Text);

            viewModel.InitializeAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();

            Assert.Equal("Git not found", status.Text);
            Assert.Empty(LayoutAudit.FindClippedText(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Disposing_a_session_closes_its_window_and_a_second_dispose_is_harmless()
    {
        using var data = new TempDirectory("data");
        var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();

        app.Dispose();
        app.Dispose();

        Assert.False(app.Window.IsVisible);
    }
}
