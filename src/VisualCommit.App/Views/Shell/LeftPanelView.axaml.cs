using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Panels;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// The left panel of the active tab (see <see cref="LeftPanelViewModel"/>). The code-behind adds
/// what bindings cannot: the menus of its rows (docs/phase-3-design.md 4b: a right-click, the Menu
/// key or Shift+F10 opens the row's menu only, selecting and highlighting nothing, D114), the
/// double-clicks of 4c, the row lookup the branch drag uses for its drop targets (4g; the drag's
/// sources are the rows' <see cref="RefDrag.SourceProperty"/>), and the focus a branch row's eye or
/// solo button gives to its row when it hides itself.
/// </summary>
public partial class LeftPanelView : UserControl
{
    /// <summary>The eye or solo button of a branch row that has the focus, and whether it came with the keyboard's frame; null while the focus is elsewhere.</summary>
    private (Button Toggle, bool Framed)? _focusedToggle;

    public LeftPanelView()
    {
        InitializeComponent();
        RefList.AddHandler(ContextRequestedEvent, OnContextRequested);
        RefList.AddHandler(DoubleTappedEvent, OnDoubleTapped, handledEventsToo: true);
        RefList.AddHandler(Button.ClickEvent, OnRowClicked);
        RefList.AddHandler(GotFocusEvent, OnFocusInList);
        RefList.AddHandler(LostFocusEvent, OnFocusLeavingList);
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

    /// <summary>
    /// The row button a request or a tap came from, with its row. With <paramref name="skipToggles"/>
    /// null for a toggle of a hidden or soloed row (<see cref="RefDrag.IsExcludedProperty"/>,
    /// docs/phase-5-design.md 8.3), so that a double-click on it checks nothing out; a menu asked
    /// for on a toggle is its row's (4b).
    /// </summary>
    private static (Button Button, RefListRow Row)? RowOf(object? source, bool skipToggles = false) =>
        ContextMenuRequests.Find<Button>(source, button => button.DataContext is RefListRow && !RefDrag.GetIsExcluded(button)) is { DataContext: RefListRow row } button
            && !(skipToggles && RefDrag.IsInExcluded(source, button))
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
    /// A click on the sign-in or "more" row of the "Pull requests" section (docs/phase-7a-design.md
    /// 10.3): the repository signs in or opens the list in the browser. Its command has already
    /// done the panel's part (nothing); the other rows' clicks are the panel's alone.
    /// </summary>
    private void OnRowClicked(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { DataContext: PullRequestMessageRow { CanActivate: true } row }
            && ContextMenuRequests.ActiveRepository(this) is (_, { } repository)
            && ReferenceEquals(repository.LeftPanel, DataContext))
        {
            repository.ActivatePullRequestMessage(row);
        }
    }

    /// <summary>A row's eye or solo button takes the focus: remembered, with whether the focus came as Tab or the arrows give it, with the frame.</summary>
    private void OnFocusInList(object? sender, FocusChangedEventArgs e) =>
        _focusedToggle = e.Source is Button { Name: "RefHiddenToggle" or "RefSoloToggle" } toggle
            ? (toggle, e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
            : null;

    /// <summary>
    /// A row's eye or solo button that had the focus has hidden itself, its branch shown again or its
    /// solo ended, and Avalonia gives the focus to nothing then: once the layout has followed, the
    /// branch's own row takes it, with the keyboard's frame when the button had it. Only when the
    /// focus is still lost, the panel shows the same tab and the row still stands for that branch.
    /// </summary>
    private void OnFocusLeavingList(object? sender, FocusChangedEventArgs e)
    {
        if (_focusedToggle is not ({ } toggle, var framed) || !ReferenceEquals(e.Source, toggle))
        {
            return;
        }

        _focusedToggle = null;
        if (toggle.IsVisible
            || toggle.GetVisualAncestors().OfType<Button>().FirstOrDefault() is not { DataContext: RefItemRow { Key: var key } } row)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        var armed = DataContext;
        var lost = FocusReturn.LostFrom(topLevel, toggle, static () => false);
        Dispatcher.UIThread.Post(
            () =>
            {
                if (ReferenceEquals(DataContext, armed)
                    && row is { DataContext: RefItemRow { Key: var shown }, IsEffectivelyVisible: true, IsEffectivelyEnabled: true }
                    && shown == key
                    && lost(topLevel?.FocusManager?.GetFocusedElement()))
                {
                    row.Focus(framed ? NavigationMethod.Tab : NavigationMethod.Unspecified);
                }
            },
            DispatcherPriority.Loaded);
    }

    /// <summary>
    /// A double-click on a row (4c): a branch checks out, a stash applies; the first click has
    /// selected as a click does, and the button's command (selecting) is harmless twice.
    /// </summary>
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        // The press after a drag that started on the press before is no double-click (4c).
        if (RowOf(e.Source, skipToggles: true) is (_, { } row)
            && BranchDragService.For(this) is not { PressFollowsDrag: true }
            && ContextMenuRequests.ActiveRepository(this) is (_, { } repository)
            && ReferenceEquals(repository.LeftPanel, DataContext))
        {
            repository.DoubleTapRefRow(row);
        }
    }
}
