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
/// Applies a theme by setting the application's theme variant. Every colour in the views is a
/// DynamicResource from Theme/Tokens.axaml, so the whole window follows at once. Work package
/// WP-T makes System follow <paramref name="system"/> (11.4); until then every theme but Light
/// shows dark, as in phase 5, and <paramref name="system"/> and <paramref name="log"/> are kept
/// unread.
/// </summary>
public sealed class ThemeService(Application application, ISystemTheme system, IAppLog log) : IThemeService
{
    /// <summary>The system's mode, which System follows from work package WP-T on.</summary>
    public ISystemTheme SystemTheme { get; } = system ?? throw new ArgumentNullException(nameof(system));

    /// <summary>Where the system's changes are logged from work package WP-T on.</summary>
    public IAppLog Log { get; } = log ?? throw new ArgumentNullException(nameof(log));

    public AppTheme Setting { get; private set; } = AppTheme.Dark;

    public bool IsDark { get; private set; } = true;

    public event EventHandler? Changed;

    public void Apply(AppTheme theme)
    {
        application.RequestedThemeVariant = theme == AppTheme.Light ? ThemeVariant.Light : ThemeVariant.Dark;
        var changed = theme != Setting || (theme != AppTheme.Light) != IsDark;
        Setting = theme;
        IsDark = theme != AppTheme.Light;
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
