using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.App.Views.Pages;
using VisualCommit.App.Views.Shell;

namespace VisualCommit.App.Views.Workspaces;

/// <summary>
/// A workspace's dashboard (docs/phase-6-design.md 2.4 to 2.7): see <see cref="DashboardViewModel"/>.
/// The code-behind gives the list the keyboard focus when the dashboard asks (it becomes active, a
/// row was removed) or the empty state's <c>Add repository…</c> when the last row went; opens a
/// row's menu (a right-click selects the row first; Shift+F10 and the Menu key open the selected
/// row's at its bottom-left corner) and the header's <c>…</c> menu below its button; opens a row on
/// a double-click or Enter; takes the list's keys: Delete (on macOS also Cmd+Backspace) removes
/// the selected row, Alt+Up and Alt+Down move it (2.7); and starts each row's container without a
/// hover (<see cref="ClearHover"/>).
/// </summary>
public partial class DashboardView : UserControl
{
    /// <summary>The pointer named by the exits <see cref="ClearHover"/> raises: no device's, so that nothing takes them for a move of the real one.</summary>
    private static readonly Pointer NoPointer = new(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: false);

    private DashboardViewModel? _viewModel;

    public DashboardView()
    {
        InitializeComponent();
        DashboardList.AddHandler(ContextRequestedEvent, OnContextRequested);
        DashboardList.AddHandler(DoubleTappedEvent, OnDoubleTapped);
        DashboardList.AddHandler(KeyDownEvent, OnListKeyDown, RoutingStrategies.Tunnel);
        DashboardList.ContainerPrepared += (_, e) => ClearHover(e.Container);
        DashboardMore.Click += OnMoreClick;
    }

    /// <summary>
    /// A container the list prepares for a row starts without a hover (the phase 6 gate's W8: rows
    /// drawn in <c>VcRowHoverBrush</c> with the pointer at the neutral point). Avalonia 12.1.3 keeps
    /// <c>IsPointerOver</c> on a recycled container: when the list is emptied, its panel clears the
    /// container (the row's content leaves it) and takes it out of the tree, and the pointer's exit
    /// from the hovered content stops at that content, which has no parent left, so the container
    /// and its presenter stay hovered. The list is emptied whenever the view's dashboard is replaced
    /// or gone (another tab became active), the row under the pointer included, and the next
    /// showing hands its container to another row. An exit raised on the container and on each
    /// hovered part of it clears that; a row the pointer is really over is entered again at
    /// Avalonia's next look at the pointer (its next move, or the next frame drawn).
    /// </summary>
    private static void ClearHover(Control container)
    {
        foreach (var element in container.GetSelfAndVisualDescendants().OfType<InputElement>().Where(element => element.IsPointerOver).ToList())
        {
            element.RaiseEvent(new PointerEventArgs(PointerExitedEvent, element, NoPointer, null, default, 0, PointerPointProperties.None, KeyModifiers.None));
        }
    }

    /// <summary>
    /// Whose keys the list takes (docs/phase-6-design.md 2.7, 6.4): Cmd+Backspace removes the
    /// selected row only where the command key is Cmd. Null (the app's case): the platform's,
    /// read at each press from its hotkey configuration as the window's router reads it (Cmd where
    /// the command modifier is Meta); a test of macOS's keys sets its own on this view alone.
    /// </summary>
    public CommandKey? KeysPlatform { get; set; }

    protected override AutomationPeer OnCreateAutomationPeer() => new PageAutomationPeer(this);

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested -= OnFocusRequested;
        }

        _viewModel = DataContext as DashboardViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested += OnFocusRequested;
        }

        base.OnDataContextChanged(e);
    }

    /// <summary>Gives the focus where the dashboard asks (2.7), once the layout has shown it.</summary>
    private void OnFocusRequested(object? sender, DashboardFocus target) =>
        Dispatcher.UIThread.Post(
            () =>
            {
                if (_viewModel is not { IsClosed: false } viewModel || !ReferenceEquals(sender, viewModel) || !IsEffectivelyVisible)
                {
                    return;
                }

                if (target == DashboardFocus.EmptyAddRepository || !viewModel.HasRows)
                {
                    DashboardEmptyAddRepository.Focus(NavigationMethod.Unspecified);
                }
                else
                {
                    FocusList();
                }
            },
            DispatcherPriority.Loaded);

    /// <summary>The list takes the focus: the selected row's container when there is one, else the list itself (Up and Down then start from the first row).</summary>
    internal void FocusList()
    {
        if (FileHistoryView.SelectedItem(DashboardList) is { } item)
        {
            item.Focus(NavigationMethod.Unspecified);
        }
        else
        {
            DashboardList.Focus(NavigationMethod.Unspecified);
        }
    }

    /// <summary>
    /// A row's menu (2.7): a right-click on a row selects it and opens its menu at the pointer;
    /// one on the list's empty part opens nothing; the Menu key and Shift+F10 open the selected
    /// row's at its bottom-left corner.
    /// </summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || _viewModel is not { IsClosed: false } viewModel || ContextMenuRequests.Layer(this) is not { } layer)
        {
            return;
        }

        var item = ContextMenuRequests.Find<ListBoxItem>(e.Source) ?? (!e.TryGetPosition(this, out _) ? FileHistoryView.SelectedItem(DashboardList) : null);
        if (item?.DataContext is not DashboardRowViewModel row)
        {
            return;
        }

        var menu = viewModel.RowMenu(row);
        item.Focus(NavigationMethod.Pointer);
        var (position, openedBy) = ContextMenuRequests.Where(e, item);
        layer.Open(menu, position, openedBy);
        e.Handled = true;
    }

    /// <summary>A double-click on a row opens it (2.3); one on its Cancel or Details… does nothing more.</summary>
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel is { IsClosed: false } viewModel
            && ContextMenuRequests.Find<Button>(e.Source) is null
            && ContextMenuRequests.Find<ListBoxItem>(e.Source)?.DataContext is DashboardRowViewModel row)
        {
            viewModel.OpenRow(row);
            e.Handled = true;
        }
    }

    /// <summary>The list's keys (2.7): Enter opens, Delete (and Cmd+Backspace on macOS) removes, Alt+Up and Alt+Down move the selected row.</summary>
    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is not { IsClosed: false } viewModel || viewModel.SelectedRow is null)
        {
            return;
        }

        var mac = (KeysPlatform ?? PlatformCommandKey()) == CommandKey.Cmd;
        switch (e.Key)
        {
            case Key.Enter when e.KeyModifiers == KeyModifiers.None:
                viewModel.OpenRow(viewModel.SelectedRow);
                e.Handled = true;
                break;
            case Key.Delete when e.KeyModifiers == KeyModifiers.None:
            case Key.Back when mac && e.KeyModifiers == KeyModifiers.Meta:
                viewModel.RemoveSelected();
                e.Handled = true;
                break;
            case Key.Up when e.KeyModifiers == KeyModifiers.Alt:
                viewModel.MoveSelected(-1);
                e.Handled = true;
                break;
            case Key.Down when e.KeyModifiers == KeyModifiers.Alt:
                viewModel.MoveSelected(1);
                e.Handled = true;
                break;
        }
    }

    /// <summary><c>DashboardMore</c>'s menu (2.5): below the button, moved (not flipped) to stay inside the window by the menu layer (WD10).</summary>
    private void OnMoreClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not { IsClosed: false } viewModel || ContextMenuRequests.Layer(this) is not { } layer
            || TopLevel.GetTopLevel(this) is not { } topLevel || DashboardMore.TranslatePoint(new Point(0, DashboardMore.Bounds.Height), topLevel) is not { } corner)
        {
            return;
        }

        // A button's menu: its top at the button's bottom; at the window's right edge it moves left
        // to stay 4 inside rather than opening to the left of the corner, as a pointer's would.
        layer.Open(viewModel.HeaderMenu(), new MenuPosition(corner.X, corner.Y), MenuOpenedBy.Button);
        e.Handled = true;
    }

    /// <summary>The platform's command key, from its hotkey configuration as <see cref="KeyRouter"/> reads it: Cmd where the command modifier is Meta, Ctrl otherwise.</summary>
    private CommandKey PlatformCommandKey() =>
        ShortcutKeys.CommandKeyOf(ShortcutKeys.CommandModifierOf(this.GetPlatformSettings()?.HotkeyConfiguration));
}
