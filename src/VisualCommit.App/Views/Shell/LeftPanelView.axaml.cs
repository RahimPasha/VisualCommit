using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Panels;

namespace VisualCommit.App.Views.Shell;

public partial class LeftPanelView : UserControl
{
    public LeftPanelView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The ref row under <paramref name="point"/> (in this view's coordinates), or null: how the
    /// branch drag finds its drop target in the left panel (docs/phase-3-design.md 4g). A hit
    /// test that leaves out controls which take no input, such as the drag layer.
    /// </summary>
    public RefItemRow? RefRowAt(Point point)
    {
        for (var visual = this.InputHitTest(point) as Visual; visual is not null && !ReferenceEquals(visual, this); visual = visual.GetVisualParent())
        {
            if (visual is Button { DataContext: RefItemRow row })
            {
                return row;
            }
        }

        return null;
    }
}
