using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.VisualTests.Phase0;

/// <summary>
/// The scripted walk-through of phase 0. Each test is one numbered check of
/// docs/test-reports/phase-0.md: it performs the check's steps on the whole app in headless
/// mode, saves a screenshot after every step under artifacts/visual/phase-0/scripted/, and
/// asserts the expected result. The screenshots are then inspected by eye for the report.
/// </summary>
public class Phase0Checks
{
    private const int Phase = 0;

    [AvaloniaTheory]
    [InlineData(1, 1100, 700)]
    [InlineData(2, 1920, 1080)]
    public async Task Checks_1_and_2_shell_in_the_dark_theme(int check, int width, int height)
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path, width, height);
        await app.WaitUntilReadyAsync();

        var screenshot = app.Capture();
        screenshot.Save(Phase, $"0{check}-shell-dark-{width}x{height}");

        // Dark without being told to: nothing has written a settings file.
        Assert.False(File.Exists(app.Session.Paths.SettingsFile));
        Assert.Equal(ThemeVariant.Dark, app.Window.ActualThemeVariant);
        await ShellExpectations.AssertShellAsync(app, screenshot, AppTheme.Dark);
    }

    [AvaloniaTheory]
    [InlineData(3, 1100, 700)]
    [InlineData(4, 1920, 1080)]
    public async Task Checks_3_and_4_shell_in_the_light_theme(int check, int width, int height)
    {
        using var data = new TempDirectory("data");
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = AppTheme.Light });

        using var app = ShellDriver.Start(data.Path, width, height);
        await app.WaitUntilReadyAsync();

        var screenshot = app.Capture();
        screenshot.Save(Phase, $"0{check}-shell-light-{width}x{height}");

        Assert.Equal(ThemeVariant.Light, app.Window.ActualThemeVariant);
        await ShellExpectations.AssertShellAsync(app, screenshot, AppTheme.Light);
    }

    [AvaloniaFact]
    public async Task Check_5_clicking_the_theme_switch_changes_the_theme()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var themeSwitch = app.Find<ToolbarButton>("ThemeSwitch");

        var before = app.Capture();
        before.Save(Phase, "05a-theme-switch-before");
        await ShellExpectations.AssertShellAsync(app, before, AppTheme.Dark);

        app.Click(themeSwitch);
        var afterFirstClick = app.Capture();
        afterFirstClick.Save(Phase, "05b-theme-switch-after-first-click");
        await ShellExpectations.AssertShellAsync(app, afterFirstClick, AppTheme.Light);
        Assert.Equal(AppTheme.Light, SavedTheme(data));

        app.Click(themeSwitch);
        var afterSecondClick = app.Capture();
        afterSecondClick.Save(Phase, "05c-theme-switch-after-second-click");
        await ShellExpectations.AssertShellAsync(app, afterSecondClick, AppTheme.Dark);
        Assert.Equal(AppTheme.Dark, SavedTheme(data));
    }

    // Not an [AvaloniaFact]: each FreshApplication.RunAsync below runs with a new Application
    // object, so the second half is a new instance of the app in every sense but the process.
    [Fact]
    public async Task Check_6_the_theme_survives_a_restart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var assembly = typeof(Phase0Checks).Assembly;
        using var data = new TempDirectory("data");

        var firstApplication = await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = ShellDriver.Start(data.Path);
                await app.WaitUntilReadyAsync();
                var dark = app.Capture();
                dark.Save(Phase, "06a-first-start-dark");
                await ShellExpectations.AssertShellAsync(app, dark, AppTheme.Dark);

                app.Click(app.Find<ToolbarButton>("ThemeSwitch"));
                var light = app.Capture();
                light.Save(Phase, "06b-light-before-closing");
                await ShellExpectations.AssertShellAsync(app, light, AppTheme.Light);
                return Application.Current!;
            },
            cancellationToken);

        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                Assert.NotSame(firstApplication, Application.Current);

                using var app = ShellDriver.Start(data.Path);

                // The first frame, before any start-up work has finished, is already light.
                var firstFrame = app.Capture();
                firstFrame.Save(Phase, "06c-restarted-first-frame");
                Assert.Equal(ThemeVariant.Light, app.Window.ActualThemeVariant);
                Assert.Equal(ShellExpectations.Light.Graph, firstFrame.PixelAt(ShellExpectations.GraphSamplePoint(ShellExpectations.LeftPanelWidth)));
                Assert.Equal(ShellExpectations.Light.Panel, firstFrame.PixelAt(new Point(130, 600)));
                Assert.Equal(ShellExpectations.Light.Chrome, firstFrame.PixelAt(new Point(1070, 18)));

                await app.WaitUntilReadyAsync();
                var ready = app.Capture();
                ready.Save(Phase, "06d-restarted-ready");
                await ShellExpectations.AssertShellAsync(app, ready, AppTheme.Light);
                return true;
            },
            cancellationToken);

        Assert.Equal(AppTheme.Light, SavedTheme(data));

        var logFile = Assert.Single(Directory.GetFiles(Path.Combine(data.Path, "logs"), "*.log"));
        var startUpLines = File.ReadAllLines(logFile).Count(line => line.Contains("VisualCommit") && line.Contains("started"));
        Assert.Equal(2, startUpLines);
    }

    [AvaloniaFact]
    public async Task Check_7_dragging_a_panel_edge_resizes_the_panel()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var mainArea = app.BoundsOf(app.Find<Grid>("MainArea"));
        var middle = mainArea.Center.Y;

        app.Capture().Save(Phase, "07a-before-resizing");

        app.Drag(new Point(260, middle), new Point(320, middle));
        var leftDragged = app.Capture();
        leftDragged.Save(Phase, "07b-left-panel-dragged");
        await ShellExpectations.AssertShellAsync(app, leftDragged, AppTheme.Dark, leftPanelWidth: 320);

        app.Drag(new Point(700, middle), new Point(640, middle));
        var rightDragged = app.Capture();
        rightDragged.Save(Phase, "07c-right-panel-dragged");
        await ShellExpectations.AssertShellAsync(app, rightDragged, AppTheme.Dark, leftPanelWidth: 320, rightPanelWidth: 460);
    }

    private static AppTheme SavedTheme(TempDirectory data) =>
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Current.Theme;
}
