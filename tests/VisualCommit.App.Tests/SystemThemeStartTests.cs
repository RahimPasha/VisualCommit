using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Whole app starts with the theme set to System (D194, docs/phase-6-design.md 11.5, 11.6, 11.15)
/// through the harness's fake system (<see cref="ShellDriver.SystemTheme"/>): the first frame
/// already has the system's mode, the window and the Theme switch follow the system while System
/// is chosen, Dark and Light ignore it, and System with the system light draws Light's pixels,
/// so that earlier Light pictures stay valid for System. The machine's own mode is never read.
/// </summary>
public class SystemThemeStartTests
{
    /// <summary>Spots clear of text, lanes and rows (report S15): the graph area, the left panel and the tab strip's chrome.</summary>
    private static readonly Point WindowSpot = new(480, 600);

    private static readonly Point PanelSpot = new(130, 600);

    private static readonly Point ChromeSpot = new(1070, 18);

    [AvaloniaFact]
    public async Task A_session_with_the_system_theme_starts_in_the_systems_mode()
    {
        using var data = new TempDirectory("data");
        WriteTheme(data, AppTheme.System);

        using var app = ShellDriver.Start(data.Path, systemDark: false);

        // Right after the start, before any wait: the variant was resolved before the window was made.
        Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
        Assert.Equal(ThemeVariant.Light, app.Window.ActualThemeVariant);
        Assert.False(app.Session.Shell.IsDarkTheme);
        Assert.Equal(AppTheme.System, app.Session.Shell.Theme);
        var first = app.Capture();
        AssertSpots(first, window: "#FFFFFF", panel: "#F5F6F8", chrome: "#EBEDF1");

        await app.WaitUntilReadyAsync();
        var themeSwitch = app.Find<ToolbarButton>("ThemeSwitch");
        Assert.Contains("light", themeSwitch.Classes);
        Assert.Equal("Switch to the dark theme (stops following the system)", ToolTip.GetTip(themeSwitch));
        Assert.Equal("Switch to the dark theme (stops following the system)", AutomationProperties.GetHelpText(themeSwitch));
        Assert.Equal(AppTheme.System, SavedTheme(app));
    }

    [AvaloniaFact]
    public async Task The_window_follows_the_system_while_System_is_chosen()
    {
        using var data = new TempDirectory("data");
        WriteTheme(data, AppTheme.System);
        var written = SettingsText(data);
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var themeSwitch = app.Find<ToolbarButton>("ThemeSwitch");
        Assert.True(app.ShowsDarkTheme);
        Assert.DoesNotContain("light", themeSwitch.Classes);
        Assert.Equal("Switch to the light theme (stops following the system)", ToolTip.GetTip(themeSwitch));
        AssertSpots(app.Capture(), window: "#14161B", panel: "#1A1D24", chrome: "#20242C");

        await app.SetSystemThemeAsync(dark: false);

        Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
        Assert.Equal(ThemeVariant.Light, app.ShownThemeVariant);
        Assert.False(app.Session.Shell.IsDarkTheme);
        Assert.Contains("light", themeSwitch.Classes);
        Assert.Equal("Switch to the dark theme (stops following the system)", ToolTip.GetTip(themeSwitch));
        Assert.Equal("Switch to the dark theme (stops following the system)", AutomationProperties.GetHelpText(themeSwitch));
        AssertSpots(app.Capture(), window: "#FFFFFF", panel: "#F5F6F8", chrome: "#EBEDF1");
        Assert.Equal(Color.Parse("#EBEDF1"), app.ThemeColour("VcChromeBackgroundBrush"));
        Assert.Equal(1, app.CountLogLines(AppLogFile.Containing("The system's theme changed to light; the app follows it.")));

        await app.SetSystemThemeAsync(dark: true);

        Assert.True(app.ShowsDarkTheme);
        Assert.DoesNotContain("light", themeSwitch.Classes);
        Assert.Equal("Switch to the light theme (stops following the system)", ToolTip.GetTip(themeSwitch));
        AssertSpots(app.Capture(), window: "#14161B", panel: "#1A1D24", chrome: "#20242C");
        Assert.Equal(1, app.CountLogLines(AppLogFile.Containing("The system's theme changed to dark; the app follows it.")));

        // Following the system saves nothing: settings.json is as written.
        Assert.Equal(written, SettingsText(data));
        Assert.Equal(0, app.CountLogLines(AppLogFile.Containing("Theme changed to")));
    }

    [AvaloniaTheory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.Light)]
    public async Task Dark_and_Light_ignore_the_system(AppTheme theme)
    {
        using var data = new TempDirectory("data");
        using var pictures = new TempDirectory("pictures");
        WriteTheme(data, theme);
        var written = SettingsText(data);
        var dark = theme == AppTheme.Dark;
        var kept = dark ? "dark" : "light";

        // The system starts in the other mode, then turns to the theme's own and back.
        using var app = ShellDriver.Start(data.Path, systemDark: !dark);
        await app.WaitUntilReadyAsync();
        app.MoveToNeutral();
        var before = Png(app, pictures, "before");
        await Assert.ThrowsAsync<InvalidOperationException>(() => app.SetSystemThemeAsync(dark));

        app.SystemTheme.Set(dark);
        await app.WaitForLogLineAsync(AppLogFile.Containing($"The system's theme changed to {(dark ? "dark" : "light")}; the app keeps the {kept} theme."));
        Assert.Equal(dark, app.ShowsDarkTheme);
        app.SystemTheme.Set(!dark);
        await app.WaitForLogLineAsync(AppLogFile.Containing($"The system's theme changed to {(dark ? "light" : "dark")}; the app keeps the {kept} theme."));

        Assert.Equal(dark, app.ShowsDarkTheme);
        Assert.Equal(dark, app.Session.Shell.IsDarkTheme);
        Assert.Equal(theme, app.Session.Shell.Theme);
        Assert.Equal(dark ? ThemeVariant.Dark : ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
        Assert.Equal(before, Png(app, pictures, "after"));
        Assert.Equal(written, SettingsText(data));
    }

    [AvaloniaFact]
    public async Task System_and_Light_draw_the_same_pixels()
    {
        using var repo = await Scenarios.GraphAsync();
        using var pictures = new TempDirectory("pictures");

        async Task<byte[]> DrawAsync(AppTheme theme)
        {
            using var data = new TempDirectory("data");
            WriteTheme(data, theme);
            using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], systemDark: false);
            await app.WaitForRepositoryAsync();
            app.MoveToNeutral();
            Assert.Equal(ThemeVariant.Light, app.ShownThemeVariant);
            return Png(app, pictures, theme.ToString());
        }

        var light = await DrawAsync(AppTheme.Light);
        var system = await DrawAsync(AppTheme.System);

        Assert.Equal(light, system);
    }

    private static string SettingsFile(TempDirectory data) => Path.Combine(data.Path, "settings.json");

    /// <summary>settings.json as it is on disk; null while there is none (Dark, the default, is not written).</summary>
    private static string? SettingsText(TempDirectory data) => File.Exists(SettingsFile(data)) ? File.ReadAllText(SettingsFile(data)) : null;

    private static void WriteTheme(TempDirectory data, AppTheme theme) =>
        new JsonSettingsStore(SettingsFile(data)).Update(settings => settings with { Theme = theme });

    private static AppTheme SavedTheme(ShellDriver app) => new JsonSettingsStore(app.Session.Paths.SettingsFile).Current.Theme;

    /// <summary>The window as a PNG file's bytes: two captures with the same pixels give the same bytes.</summary>
    private static byte[] Png(ShellDriver app, TempDirectory folder, string name)
    {
        var path = app.Capture().Save(Path.Combine(folder.Path, name + ".png"));
        TestPictures.Save(app, "system-theme-start-" + name.ToLowerInvariant());
        return File.ReadAllBytes(path);
    }

    private static void AssertSpots(Screenshot shot, string window, string panel, string chrome)
    {
        Assert.Equal(Color.Parse(window), shot.PixelAt(WindowSpot));
        Assert.Equal(Color.Parse(panel), shot.PixelAt(PanelSpot));
        Assert.Equal(Color.Parse(chrome), shot.PixelAt(ChromeSpot));
    }
}
