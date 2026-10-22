using Avalonia.Platform;
using Avalonia.Threading;

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
/// The system's mode over Avalonia's platform settings (11.3, design 0.6): <see cref="IsDark"/>
/// is the theme variant <see cref="IPlatformSettings.GetColorValues"/> reports, followed through
/// <see cref="IPlatformSettings.ColorValuesChanged"/>; no settings at all: Light, as Avalonia
/// answers. High contrast is the variant Avalonia maps its scheme to (Light or Dark). On Linux
/// without a portal it stays Light; a portal that answers after the first frame is an ordinary
/// change (11.9). <c>AppSession.Start</c> makes one when it is given no <see cref="ISystemTheme"/>,
/// and disposes it.
/// </summary>
public sealed class PlatformSystemTheme : ISystemTheme, IDisposable
{
    private readonly IPlatformSettings? _settings;
    private bool _disposed;

    /// <param name="settings">The application's platform settings (<c>Application.PlatformSettings</c>), or null where there are none.</param>
    public PlatformSystemTheme(IPlatformSettings? settings)
    {
        _settings = settings;
        IsDark = IsDarkIn(settings?.GetColorValues());
        if (settings is not null)
        {
            settings.ColorValuesChanged += OnColorValuesChanged;
        }
    }

    public bool IsDark { get; private set; }

    public event EventHandler? Changed;

    /// <summary>
    /// Whether <paramref name="values"/> ask for dark apps: their theme variant is Dark; no values:
    /// not dark. Public for the platform's event and the tests (the app has no
    /// <c>InternalsVisibleTo</c>).
    /// </summary>
    public static bool IsDarkIn(PlatformColorValues? values) => values?.ThemeVariant == PlatformThemeVariant.Dark;

    /// <summary>
    /// The platform's <c>ColorValuesChanged</c> handler, public for the tests: when
    /// <paramref name="values"/> ask for another mode than <see cref="IsDark"/>, sets it and raises
    /// <see cref="Changed"/>; the same mode (an accent colour changed alone) does nothing. Called off
    /// the UI thread it posts itself there (a guard: Avalonia raises the event on it). After
    /// <see cref="Dispose"/> nothing.
    /// </summary>
    public void Update(PlatformColorValues? values)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Update(values));
            return;
        }

        var dark = IsDarkIn(values);
        if (_disposed || dark == IsDark)
        {
            return;
        }

        IsDark = dark;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Unsubscribes from the platform's settings; a later change of the system's mode does nothing.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_settings is not null)
        {
            _settings.ColorValuesChanged -= OnColorValuesChanged;
        }
    }

    private void OnColorValuesChanged(object? sender, PlatformColorValues values) => Update(values);
}
