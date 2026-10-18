using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels;

// The theme (D194, docs/phase-6-design.md 11.6): the toolbar's switch and UseTheme, the one way to
// change the theme. Work package WP-T makes Theme the service's Setting and follows its Changed.
public partial class MainWindowViewModel
{
    /// <summary>The theme the app is showing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDarkTheme))]
    [NotifyPropertyChangedFor(nameof(ThemeSwitchToolTip))]
    public partial AppTheme Theme { get; private set; }

    public bool IsDarkTheme => Theme == AppTheme.Dark;

    public string ThemeSwitchToolTip => IsDarkTheme ? "Switch to the light theme" : "Switch to the dark theme";

    /// <summary>
    /// Changes the theme to <paramref name="theme"/> (11.6): applies it, saves it and logs it. The
    /// toolbar's switch, the palette and the settings screen's Save call it.
    /// </summary>
    public void UseTheme(AppTheme theme)
    {
        _themes.Apply(theme);
        Theme = theme;
        _settings.Update(settings => settings with { Theme = theme });
        _log.Info($"Theme changed to {theme}.");
    }

    [RelayCommand]
    private void ToggleTheme() => UseTheme(IsDarkTheme ? AppTheme.Light : AppTheme.Dark);
}
