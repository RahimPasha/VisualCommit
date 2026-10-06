using Avalonia;
using Avalonia.Styling;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.Services;

/// <summary>Switches the running app between the dark and light themes.</summary>
public interface IThemeService
{
    AppTheme Current { get; }

    void Apply(AppTheme theme);
}

/// <summary>
/// Applies a theme by setting the application's theme variant. Every colour in the views is a
/// DynamicResource from Theme/Tokens.axaml, so the whole window follows at once.
/// </summary>
public sealed class ThemeService(Application application) : IThemeService
{
    public AppTheme Current { get; private set; } = AppTheme.Dark;

    public void Apply(AppTheme theme)
    {
        application.RequestedThemeVariant = theme == AppTheme.Light ? ThemeVariant.Light : ThemeVariant.Dark;
        Current = theme;
    }
}
