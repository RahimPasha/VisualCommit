// Check S15 (docs/test-reports/phase-6.md, part S): the System theme followed live (11.1 to
// 11.7, D194, D223): a start with System and the fake system dark, the fake turned light, a click
// on Theme leaving System, two flips that then change nothing (SD11), the palette's "Follow the
// system's theme", and a restart whose first frame already has the system's theme (SD12, SD24).
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksS
{
    /// <summary>
    /// Check S15, part A (steps 1 to 5): System at the start with the fake system dark; the fake
    /// turned light without a restart; Theme's click leaving System for Dark; two flips of the
    /// fake that change nothing (byte-identical captures, SD11); the palette's "Follow the system's
    /// theme", the theme chosen now left out of the palette.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_S15_system_theme_followed()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");

        // Step 1: settings.json { "theme": "System" } before the start; the fake system dark.
        File.WriteAllText(SettingsFile(data), PartS.SystemThemeFile);
        using var app = await StartAsync(data, repo);
        var shot = Shot(app, "s15a-system-start-dark");
        AssertVariant(app, dark: true, AppTheme.System);
        AssertSpots(shot, Dark);
        AssertGr0(app, shot);
        AssertThemeSwitch(app, dark: true, PartS.ToLightLeavingSystem);
        Assert.Equal(PartS.SystemThemeFile, File.ReadAllText(SettingsFile(data)));
        Assert.Equal(1, app.CountLogLines(AppLogFile.Containing(PartS.ThemeSystemAtStart(dark: true))));

        // Step 2: the fake system light: the window follows without a restart, the setting stays System.
        var mark = app.LogMark();
        await app.SetSystemThemeAsync(false);
        shot = Shot(app, "s15b-system-light");
        await AssertNoDialogAsync(app);
        AssertVariant(app, dark: false, AppTheme.System);
        AssertSpots(shot, Light);
        AssertGr0(app, shot);
        AssertRegions(app, shot, Light);
        AssertThemeSwitch(app, dark: false, PartS.ToDarkLeavingSystem);
        Assert.Equal(PartS.SystemThemeFile, File.ReadAllText(SettingsFile(data)));
        Assert.Equal(1, app.CountLogLines(AppLogFile.Containing(PartS.SystemFollowed(dark: false)), mark));

        // Step 3: a click on Theme: Dark, System left (11.6).
        mark = app.LogMark();
        app.Click(app.FindByAutomationId("ThemeSwitch"));
        await app.WaitForAsync(() => app.ShowsDarkTheme, "the window to show the dark theme after Theme's click");
        var clicked = Shot(app, "s15c-theme-click-dark");
        AssertVariant(app, dark: true, AppTheme.Dark);
        AssertSpots(clicked, Dark);
        AssertThemeSwitch(app, dark: true, PartS.ToLight);
        Assert.Equal("\"Dark\"", SavedSetting(data, "theme"));
        Assert.Equal(1, app.CountLogLines(AppLogFile.Containing(PartS.ThemeChanged(PartS.ThemeDark)), mark));

        // Step 4: the fake flipped twice on itself (SD11): each flip logged, nothing changes.
        mark = app.LogMark();
        app.SystemTheme.Set(true);
        await app.WaitForLogLineAsync(AppLogFile.Containing(PartS.SystemKept(dark: true, "dark")), mark: mark);
        app.Settle();
        app.MoveToNeutral();
        AssertSamePixels(clicked, app.Capture(), "the capture after the fake turned dark");
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
        Assert.True(app.Session.Shell.IsDarkTheme);
        app.SystemTheme.Set(false);
        await app.WaitForLogLineAsync(AppLogFile.Containing(PartS.SystemKept(dark: false, "dark")), mark: mark);
        app.Settle();
        app.MoveToNeutral();
        AssertSamePixels(clicked, app.Capture(), "the capture after the fake turned light");
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
        Assert.True(app.Session.Shell.IsDarkTheme);
        Assert.Equal("\"Dark\"", SavedSetting(data, "theme"));
        var lines = app.LogLinesSince(mark).Where(line => line.Contains("The system's theme changed to", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Contains(PartS.SystemKept(dark: true, "dark"), lines[0], StringComparison.Ordinal);
        Assert.Contains(PartS.SystemKept(dark: false, "dark"), lines[1], StringComparison.Ordinal);
        Assert.Equal(0, app.CountLogLines(AppLogFile.Containing("Theme changed"), mark));

        // Step 5: the palette: the theme chosen now left out; "Follow the system's theme" by Enter.
        mark = app.LogMark();
        app.PressKey(Key.P, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => app.IsPaletteOpen, "the command palette to open");
        app.TypeInPalette("theme");
        var rows = app.PaletteSnapshot().Rows;
        foreach (var label in new[] { PartS.PaletteLightTheme, PartS.PaletteFollowSystem })
        {
            var row = rows.SingleOrDefault(candidate => candidate.Label == label)
                ?? throw new Xunit.Sdk.XunitException($"The palette lists no \"{label}\" for \"theme\"; it lists: {string.Join(" | ", rows.Select(candidate => candidate.Label))}.");
            Assert.Null(row.Detail);
            Assert.Null(row.KeyText);
        }

        Assert.DoesNotContain(rows, row => row.Label == PartS.PaletteDarkTheme);
        app.TypeInPalette("follow the system");
        var follow = Assert.Single(app.PaletteSnapshot().Rows);
        Assert.Equal((PartS.PaletteFollowSystem, true), (follow.Label, follow.IsHighlighted));
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !app.IsPaletteOpen && !app.ShowsDarkTheme, "the palette to close and the window to show the light theme");
        app.MoveToNeutral();
        AssertVariant(app, dark: false, AppTheme.System);
        AssertSpots(app.Capture(), Light);
        AssertThemeSwitch(app, dark: false, PartS.ToDarkLeavingSystem);
        Assert.Equal("\"System\"", SavedSetting(data, "theme"));
        Assert.Equal(1, app.CountLogLines(AppLogFile.Containing(PartS.ThemeChanged(PartS.ThemeSystem)), mark));
        Assert.Equal(1, app.CountLogLines(AppLogFile.Containing(PartS.PaletteRan(PartS.Graph, PartS.PaletteFollowSystem)), mark));
    }

    /// <summary>
    /// Check S15, part B (steps 6 to 8), a restart: the first start chooses System on the settings
    /// screen (Light clicked first, no live preview, SD12) and saves; the second, with the fake
    /// system light, has the light theme from its first frame (11.5, SD24).
    /// </summary>
    [Fact]
    public async Task Check_S15_system_theme_after_a_restart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var assembly = typeof(Phase6ChecksS).Assembly;
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");

        // First start (step 6): no settings.json, the fake dark; System chosen on the screen and saved.
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = await StartAsync(data, repo);
                var start = app.CallMark();
                AssertVariant(app, dark: true, AppTheme.Dark);
                var mark = app.LogMark();
                var screen = await app.OpenSettingsAsync(SettingsOpening.Key);
                Assert.Equal(SettingsSection.General, screen.SelectedSection);
                AssertChecked(app.SettingsSnapshot(), "SettingsThemeDark", true);
                ClickField(app, "SettingsThemeLight");
                AssertChecked(app.SettingsSnapshot(), "SettingsThemeLight", true);
                Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
                Assert.True(app.ShowsDarkTheme, "Light was previewed before Save (7.8).");
                AssertThemeSwitch(app, dark: true, PartS.ToLight);
                ClickField(app, "SettingsThemeSystem");
                AssertChecked(app.SettingsSnapshot(), "SettingsThemeSystem", true);
                Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
                await WaitForScreenClosedAsync(app, screen);
                AssertVariant(app, dark: true, AppTheme.System);
                AssertThemeSwitch(app, dark: true, PartS.ToLightLeavingSystem);
                AssertSettingsJson(data, ("theme", "\"System\""));
                var themeLines = app.LogLines().Where(line => line.Contains("Theme: ", StringComparison.Ordinal) || line.Contains("Theme changed to", StringComparison.Ordinal)).ToList();
                Assert.Equal(2, themeLines.Count);
                Assert.Contains(PartS.ThemeDarkAtStart, themeLines[0], StringComparison.Ordinal);
                Assert.Contains(PartS.ThemeChanged(PartS.ThemeSystem), themeLines[1], StringComparison.Ordinal);
                Assert.Equal(1, app.CountLogLines(AppLogFile.Containing(PartS.ThemeChanged(PartS.ThemeSystem)), mark));
                AssertNoWrite(app, start);
                return true;
            },
            cancellationToken);

        var secondStart = AppLogFile.ReadLines(data.Path).Count;

        // Second start (steps 7 and 8): the same data folder, the fake system light.
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = ShellDriver.Start(data.Path, systemDark: false);

                // Step 7: at once, before any wait.
                var first = Shot(app, "s15d-restarted-first-frame-light");
                Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
                Assert.Equal(ThemeVariant.Light, app.Window.ActualThemeVariant);
                Assert.False(app.Session.Shell.IsDarkTheme);
                AssertSpots(first, Light);

                // Step 8: GR0 drawn light.
                await app.WaitForRepositoryAsync(OperationChainTimeout);
                Rest(app);
                var shot = Shot(app, "s15e-restarted-light");
                AssertVariant(app, dark: false, AppTheme.System);
                AssertSpots(shot, Light);
                AssertGr0(app, shot);
                AssertRegions(app, shot, Light);
                AssertThemeSwitch(app, dark: false, PartS.ToDarkLeavingSystem);
                Assert.Equal("\"System\"", SavedSetting(data, "theme"));
                Assert.Single(AppLogFile.ReadLines(data.Path).Skip(secondStart), line => line.Contains(PartS.ThemeSystemAtStart(dark: false), StringComparison.Ordinal));
                return true;
            },
            cancellationToken);
    }

    /// <summary>The theme as S15 states it: the application's requested and the window's actual variant, the shell's <c>IsDarkTheme</c> and <c>Theme</c>.</summary>
    private static void AssertVariant(ShellDriver app, bool dark, AppTheme theme)
    {
        var variant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        Assert.Equal(variant, Application.Current!.RequestedThemeVariant);
        Assert.Equal(variant, app.Window.ActualThemeVariant);
        Assert.Equal(dark, app.Session.Shell.IsDarkTheme);
        Assert.Equal(theme, app.Session.Shell.Theme);
    }

    /// <summary>S15's three spots (part S's "graph"): the graph area, the left panel, the tab strip's chrome, in <paramref name="palette"/>'s Window, Panel and Chrome.</summary>
    private static void AssertSpots(Screenshot shot, Palette palette)
    {
        AssertColour(palette.Window, shot, PartS.WindowSpot, "graph area's background");
        AssertColour(palette.Panel, shot, PartS.PanelSpot, "left panel's background");
        AssertColour(palette.Chrome, shot, PartS.ChromeSpot, "tab strip's chrome");
    }

    /// <summary>GR0 as phase 1 draws it (part S's "graph"), its nodes and the stash's ring in the lane colours of the theme shown, the stash's line dashed in lane 0's colour (FD15's rule).</summary>
    private static void AssertGr0(ShellDriver app, Screenshot shot)
    {
        AssertGraph(app, PartS.Gr0, PartS.GraphLanes, working: null, showsDate: true, picture: shot);
        var palette = PaletteOf(app);
        ShellDriver.AssertDashedPixels(shot, PartS.StashLineX, PartS.StashLineTop, PartS.StashLineBottom, palette.Lane(0), palette.Window, PartS.StashLineDashes, "the stash's line from its ring into Add settings page");
    }

    /// <summary>The window's regions in <paramref name="palette"/>'s tokens (S15 step 2's "every view"): the toolbar's and the status bar's chrome, the details panel's background.</summary>
    private static void AssertRegions(ShellDriver app, Screenshot shot, Palette palette)
    {
        AssertColour(palette.Chrome, shot, PartS.ToolbarSpot, "toolbar's background");
        AssertColour(palette.Chrome, shot, ShellDriver.NeutralPoint, "status bar's background");
        AssertColour(palette.Panel, shot, PartS.DetailsSpot, "details panel's background");
        Assert.Equal(palette.Chrome, app.ThemeColour("VcChromeBackgroundBrush"));
    }

    /// <summary>Two captures are the same, pixel for pixel (SD11: compared in memory, not saved).</summary>
    private static void AssertSamePixels(Screenshot expected, Screenshot actual, string what)
    {
        Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                if (expected.PixelAt(x, y) != actual.PixelAt(x, y))
                {
                    throw new Xunit.Sdk.XunitException($"{what} differs from s15c-theme-click-dark at ({x}, {y}): {actual.PixelAt(x, y)}, not {expected.PixelAt(x, y)}.");
                }
            }
        }
    }

    /// <summary>A property of settings.json as its JSON text, or null without the file or the property.</summary>
    private static string? SavedSetting(TempDirectory data, string name)
    {
        if (!File.Exists(SettingsFile(data)))
        {
            return null;
        }

        using var stream = new FileStream(SettingsFile(data), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var json = System.Text.Json.JsonDocument.Parse(stream);
        return json.RootElement.TryGetProperty(name, out var value) ? value.GetRawText() : null;
    }
}
