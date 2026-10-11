using Avalonia.Controls;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// The in-window context menu's layer (docs/phase-3-design.md 4a). A placeholder of work package
/// WP0: WP5 owns this file, draws the menu here and registers its Esc handler with
/// <see cref="MainWindow.AddEscapeHandler"/>.
/// </summary>
public partial class MenuLayer : UserControl
{
    public MenuLayer()
    {
        InitializeComponent();
    }
}
