using Avalonia;
using Avalonia.Styling;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.Services;

/// <summary>The app's theme: the user's choice, and what the window shows (D194, docs/phase-6-design.md 11.4).</summary>
public interface IThemeService
{
    /// <summary>The theme as chosen: Dark, Light or System.</summary>
    AppTheme Setting { get; }

    /// <summary>The window shows dark: Dark, or System while the system is dark.</summary>
    bool IsDark { get; }

    /// <summary>Raised on the UI thread after <see cref="Setting"/> or <see cref="IsDark"/> changed.</summary>
    event EventHandler? Changed;

    /// <summary>Applies <paramref name="theme"/>; it neither saves nor logs: the caller does.</summary>
    void Apply(AppTheme theme);
}

/// <summary>
/// Applies the theme by setting the application's theme variant (11.4). Every colour in the views
/// is a DynamicResource from Theme/Tokens.axaml and every custom-drawn control redraws when its
/// variant changes, so the whole window follows at once. The app resolves System itself, from
/// <see cref="ISystemTheme"/>, rather than leaving it to Avalonia's <c>ThemeVariant.Default</c>:
/// the harnesses' fake then drives the same path as the platform, and this service alone writes
/// <see cref="Application.RequestedThemeVariant"/>, so that <see cref="IsDark"/> and the
/// application's <c>ActualThemeVariant</c> always agree. Every member is used on the UI thread.
/// </summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    private readonly Application _application;
    private readonly ISystemTheme _system;
    private readonly IAppLog _log;
    private bool _disposed;

    /// <summary>Starts as App.axaml does, Dark shown, and follows <paramref name="system"/>'s changes; nothing else until <see cref="Apply"/>.</summary>
    public ThemeService(Application application, ISystemTheme system, IAppLog log)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _system = system ?? throw new ArgumentNullException(nameof(system));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _system.Changed += OnSystemChanged;
    }

    public AppTheme Setting { get; private set; } = AppTheme.Dark;

    public bool IsDark { get; private set; } = true;

    public event EventHandler? Changed;

    /// <summary>
    /// Makes <paramref name="theme"/> the setting and shows it: Light light, System as the system
    /// is now, anything else (Dark, or a value outside the enum) dark. The variant is set every
    /// time (Avalonia ignores an equal value); <see cref="Changed"/> is raised when the setting or
    /// what shows changed.
    /// </summary>
    public void Apply(AppTheme theme)
    {
        var changed = theme != Setting;
        Setting = theme;
        changed |= Show(Resolve(theme));
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Stops following the system: a later change of its mode does nothing.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _system.Changed -= OnSystemChanged;
    }

    /// <summary>The system's mode changed (11.4, rule 3): System follows it; Dark and Light keep what they show and only log it.</summary>
    private void OnSystemChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        var system = _system.IsDark ? "dark" : "light";
        if (Setting != AppTheme.System)
        {
            _log.Info($"The system's theme changed to {system}; the app keeps the {(IsDark ? "dark" : "light")} theme.");
            return;
        }

        if (Show(Resolve(Setting)))
        {
            _log.Info($"The system's theme changed to {system}; the app follows it.");
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool Resolve(AppTheme theme) => theme switch
    {
        AppTheme.Light => false,
        AppTheme.System => _system.IsDark,
        _ => true,
    };

    /// <summary>Shows dark or light; whether that changed what shows.</summary>
    private bool Show(bool dark)
    {
        _application.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var changed = dark != IsDark;
        IsDark = dark;
        return changed;
    }
}
