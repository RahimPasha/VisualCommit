using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Panels;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// The left panel of the active tab (see <see cref="LeftPanelViewModel"/>). The code-behind adds
/// what bindings cannot: the menus of its rows (docs/phase-3-design.md 4b: a right-click, the Menu
/// key or Shift+F10 opens the row's menu only, selecting and highlighting nothing, D114), the
/// double-clicks of 4c, and the row lookup the branch drag uses for its drop targets (4g; the
/// drag's sources are the rows' <see cref="RefDrag.SourceProperty"/>).
/// </summary>
public partial class LeftPanelView : UserControl
{
    public LeftPanelView()
    {
        InitializeComponent();
        RefList.AddHandler(ContextRequestedEvent, OnContextRequested);
        RefList.AddHandler(DoubleTappedEvent, OnDoubleTapped, handledEventsToo: true);
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

    /// <summary>The row button a request or a tap came from, with its row.</summary>
    private static (Button Button, RefListRow Row)? RowOf(object? source) =>
        ContextMenuRequests.Find<Button>(source, button => button.DataContext is RefListRow) is { DataContext: RefListRow row } button
            ? (button, row)
            : null;

    /// <summary>A menu for the row (4b): opened at the pointer, or from the keyboard at the row's bottom-left corner.</summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled
            || RowOf(e.Source) is not ({ } button, { } row)
            || ContextMenuRequests.ActiveRepository(this) is not ({ } layer, { } repository)
            || !ReferenceEquals(repository.LeftPanel, DataContext))
        {
            return;
        }

        var (position, openedBy) = ContextMenuRequests.Where(e, button);
        e.Handled = repository.OpenRefRowMenu(layer, row, position, openedBy);
    }

    /// <summary>
    /// A double-click on a row (4c): a branch checks out, a stash applies; the first click has
    /// selected as a click does, and the button's command (selecting) is harmless twice.
    /// </summary>
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        // The press after a drag that started on the press before is no double-click (4c).
        if (RowOf(e.Source) is (_, { } row)
            && BranchDragService.For(this) is not { PressFollowsDrag: true }
            && ContextMenuRequests.ActiveRepository(this) is (_, { } repository)
            && ReferenceEquals(repository.LeftPanel, DataContext))
        {
            repository.DoubleTapRefRow(row);
        }
    }
}
