using Avalonia.Controls;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// The layer a dragged branch's pill is drawn on (docs/phase-3-design.md 4g, D87): a canvas over
/// everything but the dialog layer that never takes input itself, so that the drop target under
/// the pointer is still found. A placeholder of work package WP0, placed in MainWindow.axaml;
/// WP8 owns this file and the drag service that draws here.
/// </summary>
public class DragLayer : Canvas
{
    public DragLayer()
    {
        IsHitTestVisible = false;
    }
}
