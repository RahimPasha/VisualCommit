using CommunityToolkit.Mvvm.Input;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels;

// The theme (D194, docs/phase-6-design.md 11.6): the toolbar's switch and UseTheme, the one way to
// change the theme. What is chosen and what shows are the theme service's; its Changed (the user's
// choice, or the system's mode while System is chosen) tells the view.
public partial class MainWindowViewModel
{
    /// <summary>The theme as chosen: Dark, Light or System (D194).</summary>
    public AppTheme Theme => _themes.Setting;

    /// <summary>The window shows dark: Dark chosen, or System while the system is dark.</summary>
    public bool IsDarkTheme => _themes.IsDark;

    /// <summary>
    /// The Theme switch's tooltip and help text: the theme a click switches to, and while System
    /// is chosen that the click stops following the system (11.6).
    /// </summary>
    public string ThemeSwitchToolTip => (IsDarkTheme, Theme == AppTheme.System) switch
    {
        (true, false) => "Switch to the light theme",
        (false, false) => "Switch to the dark theme",
        (true, true) => "Switch to the light theme (stops following the system)",
        (false, true) => "Switch to the dark theme (stops following the system)",
    };

    /// <summary>
    /// Changes the theme to <paramref name="theme"/> (11.6): applies it, saves it and logs
    /// <c>Theme changed to {theme}.</c>; nothing when it is the theme chosen now. The toolbar's
    /// switch, the palette and the settings screen's Save call it.
    /// </summary>
    public void UseTheme(AppTheme theme)
    {
        if (theme == _themes.Setting)
        {
            return;
        }

        _themes.Apply(theme);
        _settings.Update(settings => settings with { Theme = theme });
        _log.Info($"Theme changed to {theme}.");
    }

    /// <summary>One click always changes what the window shows: dark becomes Light, light becomes Dark, which leaves System.</summary>
    [RelayCommand]
    private void ToggleTheme() => UseTheme(IsDarkTheme ? AppTheme.Light : AppTheme.Dark);

    /// <summary>Called once from the constructor: the service's changes reach the view.</summary>
    private void FollowThemeService() => _themes.Changed += (_, _) =>
    {
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(ThemeSwitchToolTip));
    };
}
