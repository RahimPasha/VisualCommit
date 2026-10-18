using VisualCommit.App.ViewModels.Menus;

namespace VisualCommit.App.ViewModels;

// The settings' part of a repository's view model (docs/phase-6-design.md 7, 12.1): the chosen
// editor for the file menus. Work package WP-SET fills it.
public sealed partial class RepositoryViewModel
{
    /// <summary>
    /// The chosen editor as the file menus name it (12.1), from the settings, with no disk access;
    /// null while none is chosen. Null until work package WP-SET reads <c>Settings.Current.Editor</c>,
    /// so the file menus are as in phase 5.
    /// </summary>
    private EditorMenuState? EditorForMenus() => null;
}
