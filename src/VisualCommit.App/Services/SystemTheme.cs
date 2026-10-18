using Avalonia.Platform;

namespace VisualCommit.App.Services;

/// <summary>The operating system's light or dark mode, as the platform reports it (D194, docs/phase-6-design.md 11.3).</summary>
public interface ISystemTheme
{
    /// <summary>The system asks for dark apps now.</summary>
    bool IsDark { get; }

    /// <summary>Raised on the UI thread when <see cref="IsDark"/> changes; never for an accent colour alone.</summary>
    event EventHandler? Changed;
}

/// <summary>
/// The system's mode over Avalonia's platform settings (11.3); no settings at all: Light, as
/// Avalonia answers. <c>AppSession.Start</c> makes one when it is given no <see cref="ISystemTheme"/>.
/// <para>
/// A stub: work package WP-T builds it. Until then it reads the mode once and follows nothing,
/// and nothing reads it (the theme service applies Dark or Light as phase 5 did).
/// </para>
/// </summary>
public sealed class PlatformSystemTheme : ISystemTheme, IDisposable
{
    /// <param name="settings">The application's platform settings, or null where there are none.</param>
    public PlatformSystemTheme(IPlatformSettings? settings)
    {
        IsDark = IsDarkIn(settings?.GetColorValues());
    }

    public bool IsDark { get; private set; }

#pragma warning disable CS0067 // Raised from work package WP-T on.
    public event EventHandler? Changed;
#pragma warning restore CS0067

    /// <summary>Whether <paramref name="values"/> ask for dark apps; public for the platform's event and the tests.</summary>
    public static bool IsDarkIn(PlatformColorValues? values) => values?.ThemeVariant == PlatformThemeVariant.Dark;

    /// <summary>The platform's <c>ColorValuesChanged</c> handler (11.3). Work package WP-T gives it its body; it does nothing yet.</summary>
    public void Update(PlatformColorValues? values)
    {
    }

    /// <summary>Unsubscribes from the platform's settings (nothing is subscribed yet).</summary>
    public void Dispose()
    {
    }
}
