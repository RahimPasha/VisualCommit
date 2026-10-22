using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using VisualCommit.Core.Settings;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 6's theme in the harness (docs/phase-6-design.md 11.3, 11.14, 17.2): the fake system
/// turned dark or light with a wait until the window follows, which theme the window shows, and
/// a token's colour as it shows it. <see cref="SystemTheme"/> is the fake itself.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>
    /// The theme variant the window shows: the application's <c>ActualThemeVariant</c>, which the
    /// window inherits (the theme service writes only the application's request, 11.4).
    /// </summary>
    /// <exception cref="InvalidOperationException">The window shows another variant than the application's.</exception>
    public ThemeVariant ShownThemeVariant
    {
        get
        {
            var application = Application.Current
                ?? throw new InvalidOperationException("No Avalonia application is running.");
            var shown = Window.ActualThemeVariant;
            return shown == application.ActualThemeVariant
                ? shown
                : throw new InvalidOperationException($"The window shows {shown}, the application {application.ActualThemeVariant}: only the theme service sets a variant, the application's.");
        }
    }

    /// <summary>The window shows the dark theme (<see cref="ShownThemeVariant"/> is Dark); false while it shows the light one.</summary>
    public bool ShowsDarkTheme => ShownThemeVariant == ThemeVariant.Dark;

    /// <summary>
    /// Turns the fake system dark or light (<see cref="FakeSystemTheme.Set"/>) and waits until the
    /// window shows that variant: the theme service's <c>IsDark</c>, the application's and the
    /// window's <c>ActualThemeVariant</c>; then lets the layout settle. Only while System is chosen,
    /// since Dark and Light never follow the system: to flip the fake under them, call
    /// <see cref="SystemTheme"/>'s <c>Set</c> and wait for the log line it gives (report S15, SD11).
    /// </summary>
    /// <exception cref="InvalidOperationException">The theme chosen is not System.</exception>
    /// <exception cref="TimeoutException">The window did not follow in time.</exception>
    public async Task SetSystemThemeAsync(bool dark, TimeSpan? timeout = null)
    {
        Dispatcher.UIThread.VerifyAccess();
        var themes = Session.Themes;
        if (themes.Setting != AppTheme.System)
        {
            throw new InvalidOperationException(
                $"The theme chosen is {themes.Setting}, which does not follow the system: flip SystemTheme itself and wait for the log line \"the app keeps the … theme\".");
        }

        SystemTheme.Set(dark);
        var variant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        await WaitForAsync(
            () => themes.IsDark == dark && Window.ActualThemeVariant == variant && Application.Current?.ActualThemeVariant == variant,
            $"the window to show the {(dark ? "dark" : "light")} theme after the system's",
            timeout);
        Settle();
    }

    /// <summary>
    /// The colour of the token <paramref name="key"/> (a solid brush of <c>Theme/Tokens.axaml</c>, or
    /// one of Fluent's keys it sets) in the variant the window shows now (<see cref="ShownThemeVariant"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">No solid brush has that key.</exception>
    public Color ThemeColour(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Window.TryFindResource(key, ShownThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No solid brush {key} in the {ShownThemeVariant} theme.");
    }
}
