using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using VisualCommit.App.Services;
using VisualCommit.Core.Settings;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The theme service's rules (<see cref="ThemeService"/>, docs/phase-6-design.md 11.4, 11.15) on
/// the headless application with the harness's fake system: Dark and Light show what they say,
/// System follows the system both ways and logs that it follows, Dark and Light ignore it and log
/// that they keep their theme, <see cref="IThemeService.Changed"/> only when the setting or what
/// shows changed, and nothing after <see cref="ThemeService.Dispose"/>. The headless application
/// is shared by every test of the assembly, so each test puts its variant back to Dark, App.axaml's.
/// </summary>
public class ThemeServiceTests
{
    private const string FollowsLight = "The system's theme changed to light; the app follows it.";
    private const string FollowsDark = "The system's theme changed to dark; the app follows it.";

    private static Application App => Application.Current ?? throw new InvalidOperationException("No Avalonia application is running.");

    [AvaloniaFact]
    public void It_starts_as_App_axaml_does_and_changes_nothing_until_Apply()
    {
        using var restore = new DarkAgain();
        App.RequestedThemeVariant = ThemeVariant.Light;
        var log = new RecordingLog();

        using var themes = new ThemeService(App, new FakeSystemTheme(dark: false), log);

        Assert.Equal(AppTheme.Dark, themes.Setting);
        Assert.True(themes.IsDark);
        Assert.Equal(ThemeVariant.Light, App.RequestedThemeVariant);
        Assert.Empty(log.Messages);
    }

    [AvaloniaTheory]
    [InlineData(AppTheme.Dark, true)]
    [InlineData(AppTheme.Light, false)]
    public void Apply_Dark_or_Light_sets_the_variant_and_IsDark_whatever_the_system_says(AppTheme theme, bool dark)
    {
        using var restore = new DarkAgain();
        foreach (var systemDark in new[] { true, false })
        {
            App.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
            var log = new RecordingLog();
            using var themes = new ThemeService(App, new FakeSystemTheme(systemDark), log);

            themes.Apply(theme);

            Assert.Equal(theme, themes.Setting);
            AssertShows(themes, dark);
            Assert.Empty(log.Messages);
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Apply_System_shows_the_systems_mode(bool systemDark)
    {
        using var restore = new DarkAgain();
        using var themes = new ThemeService(App, new FakeSystemTheme(systemDark), new RecordingLog());

        themes.Apply(AppTheme.System);

        Assert.Equal(AppTheme.System, themes.Setting);
        AssertShows(themes, systemDark);
    }

    [AvaloniaFact]
    public void While_System_a_flip_of_the_system_flips_the_variant_raises_Changed_once_and_logs_that_it_follows()
    {
        using var restore = new DarkAgain();
        var system = new FakeSystemTheme(dark: true);
        var log = new RecordingLog();
        using var themes = new ThemeService(App, system, log);
        themes.Apply(AppTheme.System);
        var changes = 0;
        themes.Changed += (_, _) => changes++;

        system.Set(dark: false);

        AssertShows(themes, dark: false);
        Assert.Equal(AppTheme.System, themes.Setting);
        Assert.Equal(1, changes);
        Assert.Equal([FollowsLight], log.Messages);

        system.Set(dark: true);

        AssertShows(themes, dark: true);
        Assert.Equal(2, changes);
        Assert.Equal([FollowsLight, FollowsDark], log.Messages);
    }

    [AvaloniaTheory]
    [InlineData(AppTheme.Dark, true, "dark")]
    [InlineData(AppTheme.Light, false, "light")]
    public void While_Dark_or_Light_a_flip_of_the_system_changes_nothing_raises_nothing_and_logs_that_it_keeps(AppTheme theme, bool dark, string kept)
    {
        using var restore = new DarkAgain();
        var system = new FakeSystemTheme(dark: true);
        var log = new RecordingLog();
        using var themes = new ThemeService(App, system, log);
        themes.Apply(theme);
        var changes = 0;
        themes.Changed += (_, _) => changes++;

        system.Set(dark: false);
        AssertShows(themes, dark);
        system.Set(dark: true);
        AssertShows(themes, dark);

        Assert.Equal(theme, themes.Setting);
        Assert.Equal(0, changes);
        Assert.Equal(
            [
                $"The system's theme changed to light; the app keeps the {kept} theme.",
                $"The system's theme changed to dark; the app keeps the {kept} theme.",
            ],
            log.Messages);
    }

    [AvaloniaTheory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.System)]
    public void Apply_of_the_theme_chosen_raises_nothing(AppTheme theme)
    {
        using var restore = new DarkAgain();
        using var themes = new ThemeService(App, new FakeSystemTheme(dark: false), new RecordingLog());
        themes.Apply(theme);
        var changes = 0;
        themes.Changed += (_, _) => changes++;

        themes.Apply(theme);

        Assert.Equal(0, changes);
        AssertShows(themes, theme == AppTheme.Dark);
    }

    [AvaloniaFact]
    public void Apply_System_while_the_system_shows_what_shows_raises_Changed_and_leaves_the_variant()
    {
        using var restore = new DarkAgain();
        using var themes = new ThemeService(App, new FakeSystemTheme(dark: true), new RecordingLog());
        themes.Apply(AppTheme.Dark);
        var changes = 0;
        themes.Changed += (_, _) => changes++;
        var variants = 0;
        App.ActualThemeVariantChanged += CountVariant;
        try
        {
            themes.Apply(AppTheme.System);
        }
        finally
        {
            App.ActualThemeVariantChanged -= CountVariant;
        }

        Assert.Equal(1, changes);
        Assert.Equal(0, variants);
        Assert.Equal(AppTheme.System, themes.Setting);
        AssertShows(themes, dark: true);

        void CountVariant(object? sender, EventArgs e) => variants++;
    }

    [AvaloniaFact]
    public void A_value_outside_the_enum_shows_dark()
    {
        using var restore = new DarkAgain();
        App.RequestedThemeVariant = ThemeVariant.Light;
        var system = new FakeSystemTheme(dark: false);
        var log = new RecordingLog();
        using var themes = new ThemeService(App, system, log);

        themes.Apply((AppTheme)7);

        Assert.Equal((AppTheme)7, themes.Setting);
        AssertShows(themes, dark: true);

        // It does not follow the system either.
        system.Set(dark: true);
        system.Set(dark: false);
        AssertShows(themes, dark: true);
        Assert.Equal(
            ["The system's theme changed to dark; the app keeps the dark theme.", "The system's theme changed to light; the app keeps the dark theme."],
            log.Messages);
    }

    [AvaloniaFact]
    public void After_Dispose_a_flip_of_the_system_changes_nothing()
    {
        using var restore = new DarkAgain();
        var system = new FakeSystemTheme(dark: true);
        var log = new RecordingLog();
        var themes = new ThemeService(App, system, log);
        themes.Apply(AppTheme.System);
        var changes = 0;
        themes.Changed += (_, _) => changes++;

        themes.Dispose();
        system.Set(dark: false);
        themes.Dispose();

        Assert.True(themes.IsDark);
        Assert.Equal(ThemeVariant.Dark, App.RequestedThemeVariant);
        Assert.Equal(0, changes);
        Assert.Empty(log.Messages);
    }

    /// <summary>The service shows dark or light, and the application agrees (rule 4).</summary>
    private static void AssertShows(ThemeService themes, bool dark)
    {
        var variant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        Assert.Equal(dark, themes.IsDark);
        Assert.Equal(variant, App.RequestedThemeVariant);
        Assert.Equal(variant, App.ActualThemeVariant);
    }

    /// <summary>Puts the shared headless application's variant back to Dark, App.axaml's, when the test ends.</summary>
    private sealed class DarkAgain : IDisposable
    {
        public void Dispose() => App.RequestedThemeVariant = ThemeVariant.Dark;
    }
}
