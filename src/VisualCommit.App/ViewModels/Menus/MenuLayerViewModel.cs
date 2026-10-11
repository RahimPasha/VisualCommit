using CommunityToolkit.Mvvm.ComponentModel;

namespace VisualCommit.App.ViewModels.Menus;

/// <summary>
/// The window's menu layer (docs/phase-3-design.md 4a): at most one menu open, at a point of the
/// window. A placeholder of work package WP0 so that <see cref="MainWindowViewModel.Menu"/> and
/// the layer in MainWindow.axaml exist; WP5 owns this file and builds the menu's behaviour here
/// (or replaces it with the adapter of the stock flyout, as the spike of 4a decides).
/// </summary>
public sealed partial class MenuLayerViewModel : ObservableObject
{
    /// <summary>The open menu, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial MenuModel? Current { get; private set; }

    /// <summary>Where the open menu opened, in the window's coordinates.</summary>
    [ObservableProperty]
    public partial MenuPosition Position { get; private set; }

    public bool IsOpen => Current is not null;

    /// <summary>Opens <paramref name="menu"/> at <paramref name="position"/>, closing any other.</summary>
    public void Open(MenuModel menu, MenuPosition position)
    {
        ArgumentNullException.ThrowIfNull(menu);
        Position = position;
        Current = menu;
    }

    /// <summary>Closes the open menu. Returns whether one was open (Esc's chain).</summary>
    public bool Close()
    {
        if (Current is null)
        {
            return false;
        }

        Current = null;
        return true;
    }
}
