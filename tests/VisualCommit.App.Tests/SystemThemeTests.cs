using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using VisualCommit.App.Services;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The system's mode over Avalonia's platform settings (<see cref="PlatformSystemTheme"/>,
/// docs/phase-6-design.md 11.3, 11.15): no settings mean light, a change of the theme variant
/// raises <see cref="ISystemTheme.Changed"/> once, the same values or an accent colour alone
/// nothing, and an update from another thread raises on the UI thread. No test reads the
/// machine's own mode: the platform's values are made here.
/// </summary>
public class SystemThemeTests
{
    private static readonly PlatformColorValues Dark = new() { ThemeVariant = PlatformThemeVariant.Dark, AccentColor1 = Colors.SteelBlue };

    private static readonly PlatformColorValues Light = new() { ThemeVariant = PlatformThemeVariant.Light, AccentColor1 = Colors.SteelBlue };

    [Fact]
    public void Without_platform_settings_the_system_is_not_dark()
    {
        using var system = new PlatformSystemTheme(null);

        Assert.False(system.IsDark);
        Assert.False(PlatformSystemTheme.IsDarkIn(null));
        Assert.False(PlatformSystemTheme.IsDarkIn(Light));
        Assert.True(PlatformSystemTheme.IsDarkIn(Dark));
    }

    [AvaloniaFact]
    public void A_change_of_the_variant_raises_once_and_the_same_values_or_another_accent_nothing()
    {
        using var system = new PlatformSystemTheme(null);
        var changes = 0;
        system.Changed += (_, _) => changes++;

        system.Update(Dark);

        Assert.True(system.IsDark);
        Assert.Equal(1, changes);

        system.Update(Dark);
        system.Update(Dark with { AccentColor1 = Colors.OrangeRed });
        system.Update(Dark with { ContrastPreference = ColorContrastPreference.High });

        Assert.True(system.IsDark);
        Assert.Equal(1, changes);

        system.Update(Light);

        Assert.False(system.IsDark);
        Assert.Equal(2, changes);

        // Values the platform could not read count as light, as Avalonia answers without settings.
        system.Update(Dark);
        system.Update(null);

        Assert.False(system.IsDark);
        Assert.Equal(4, changes);
    }

    [AvaloniaFact]
    public async Task An_update_from_another_thread_raises_on_the_UI_thread()
    {
        using var system = new PlatformSystemTheme(null);
        var raisedOnTheUiThread = new List<bool>();
        system.Changed += (_, _) => raisedOnTheUiThread.Add(Dispatcher.UIThread.CheckAccess());

        await Task.Run(() => system.Update(Dark), TestContext.Current.CancellationToken);
        var until = DateTime.UtcNow.AddSeconds(30);
        while (raisedOnTheUiThread.Count == 0)
        {
            Assert.True(DateTime.UtcNow < until, "The update posted from the thread pool was never raised.");
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.Equal([true], raisedOnTheUiThread);
        Assert.True(system.IsDark);
    }

    [AvaloniaFact]
    public void After_Dispose_an_update_changes_nothing()
    {
        var system = new PlatformSystemTheme(null);
        var changes = 0;
        system.Changed += (_, _) => changes++;

        system.Dispose();
        system.Update(Dark);
        system.Dispose();

        Assert.False(system.IsDark);
        Assert.Equal(0, changes);
    }
}
