using System.ComponentModel;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.App.Views.Shell;

namespace VisualCommit.App.Views.Rebase;

/// <summary>
/// The interactive rebase's list in the graph area (docs/phase-4-design.md 2.2, D141): see
/// <see cref="InteractiveRebaseViewModel"/>. The code-behind does what bindings cannot:
/// <list type="bullet">
/// <item>the keyboard: Up, Down, Home and End move the selection, Alt+Up and Alt+Down the selected
/// row, git's letters <c>p r s f d</c> set its action; the selected row has the keyboard focus;</item>
/// <item>the pointer: a press selects a row, a drag of more than 4 px moves it (the row stays in its
/// place at half opacity, a 2-px line marks the gap nearest the pointer, Esc cancels);</item>
/// <item>the row's menu (D105) through the window's menu layer: from its action button and from
/// Shift+F10 or the Menu key with its top-left at the button's bottom-left corner, from a
/// right-click 2 right of and 2 below the pointer (14.3);</item>
/// <item>Esc (<see cref="EscapeOrder.InteractiveRebase"/>): a drag is cancelled first, otherwise the
/// list closes, asking when it was changed; while the list is hidden (a diff over it) it declines;</item>
/// <item>the test surface (<see cref="Rows"/>, <see cref="Snapshot"/>).</item>
/// </list>
/// One view serves every tab, as the diff view does: the list's state is its view model's.
/// </summary>
public partial class InteractiveRebaseView : UserControl
{
    /// <summary>The narrowest list that shows the author column (2.2).</summary>
    public const double AuthorColumnMinWidth = 520;

    /// <summary>How far the pointer moves from the press with the button held before a row drag starts (D87's threshold).</summary>
    public const double DragThreshold = BranchDragService.Threshold;

    /// <summary>The class of a commit row's part (<c>RebaseRow</c>).</summary>
    private const string CommitRowClass = "commit";

    private InteractiveRebaseViewModel? _viewModel;
    private IDisposable? _escape;
    private PendingPress? _press;
    private RowDrag? _drag;

    public InteractiveRebaseView()
    {
        InitializeComponent();

        // Keys are seen on their way down, before the list's own directional navigation.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPointerPressed);
        AddHandler(PointerMovedEvent, OnPointerMoved);
        AddHandler(PointerReleasedEvent, OnPointerReleased);
        AddHandler(ContextRequestedEvent, OnContextRequested);
        AddHandler(Button.ClickEvent, OnButtonClick);
        RowsArea.SizeChanged += (_, _) => ShowAuthorColumnIfWide();
    }

    /// <summary>The commit rows as they are drawn now, top to bottom, with bounds in this view's coordinates (the test surface, 13.2).</summary>
    public IReadOnlyList<InteractiveRebaseRowSnapshot> Rows => Snapshot().Rows;

    /// <summary>Whether a row is being dragged now.</summary>
    public bool IsDragging => _drag is not null;

    /// <summary>
    /// The whole list as it is drawn now: header, rows, base row, join lines, the drag's line and
    /// the footer, with bounds in <paramref name="relativeTo"/>'s coordinates (this view's when
    /// null). It lets the UI thread run nothing: settle first.
    /// </summary>
    public InteractiveRebaseSnapshot Snapshot(Visual? relativeTo = null)
    {
        var to = relativeTo ?? this;
        var viewModel = _viewModel;
        var rows = new List<InteractiveRebaseRowSnapshot>();
        InteractiveRebaseBaseRowSnapshot? baseRow = null;
        var joinLines = new List<InteractiveRebaseJoinLineSnapshot>();
        if (viewModel is not null)
        {
            for (var index = 0; index < viewModel.Rows.Count; index++)
            {
                rows.Add(RowSnapshot(viewModel, index, to));
            }

            if (!viewModel.HasMessage)
            {
                var part = RowPart(viewModel.Rows.Count);
                baseRow = new InteractiveRebaseBaseRowSnapshot(
                    viewModel.BaseRow.ShortSha,
                    viewModel.BaseRow.Subject,
                    viewModel.BaseRow.Name,
                    part is null ? null : BoundsIn(part, to),
                    part is null ? null : ColorOf(Named<TextBlock>(part, "RebaseBaseLabel")?.Foreground));
            }

            foreach (var member in rows.Where(row => row.JoinTargetSha is not null))
            {
                var target = rows.First(row => row.Sha == member.JoinTargetSha);
                if (member.Bounds is { } from && target.Bounds is { } top && JoinLineColor(member.Index) is var color)
                {
                    var x = from.X + 12;
                    joinLines.Add(new InteractiveRebaseJoinLineSnapshot(member.Sha, target.Sha, new Rect(x, from.Center.Y, 2, top.Y - from.Center.Y), color));
                }
            }
        }

        Rect? dragLine = _drag is not null && RebaseDragLine.IsVisible ? BoundsIn(RebaseDragLine, to) : null;
        return new InteractiveRebaseSnapshot(
            viewModel?.HeaderText ?? string.Empty,
            ToolTip.GetTip(RebaseListHeader) as string,
            RebaseListCount.Text ?? string.Empty,
            RebaseListCount.IsEffectivelyVisible,
            viewModel?.Message,
            rows,
            baseRow,
            joinLines,
            _drag?.Gap,
            dragLine,
            BoundsIn(this, to),
            BoundsIn(RebaseListHeaderBar, to),
            BoundsIn(RebaseListFooter, to),
            RebaseStartButton.IsEffectivelyEnabled,
            ToolTip.GetTip(RebaseStartButton) as string,
            RebaseStartButton.IsEffectivelyVisible,
            BoundsIn(RebaseStartButton, to),
            BoundsIn(RebaseCancelButton, to),
            viewModel?.IsInteractive ?? false,
            RowsList.Classes.Contains("wide"));
    }

    /// <summary>Gives the keyboard focus to the selected row, scrolling it into view (or to Cancel when the list cannot be made).</summary>
    public void FocusSelectedRow() => FocusSelectedRow(NavigationMethod.Unspecified);

    protected override AutomationPeer OnCreateAutomationPeer() => new InteractiveRebaseAutomationPeer(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is MainWindow window)
        {
            _escape = window.AddEscapeHandler(new EscapeHandler(HandleEscape), EscapeOrder.InteractiveRebase);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        EndDrag();
        _escape?.Dispose();
        _escape = null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested -= OnFocusRequested;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        EndDrag();
        _press = null;
        _viewModel = DataContext as InteractiveRebaseViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested += OnFocusRequested;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // A list that opens has its top row selected with the keyboard focus (2.2), once the
            // layout has made the rows: a hidden control cannot take the focus.
            Dispatcher.UIThread.Post(() => FocusSelectedRow(NavigationMethod.Unspecified), DispatcherPriority.Loaded);
        }

        base.OnDataContextChanged(e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        // The window lost the pointer during a drag: nothing moves.
        EndDrag();
    }

    private void OnFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => FocusSelectedRow(NavigationMethod.Unspecified), DispatcherPriority.Loaded);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InteractiveRebaseViewModel.IsInteractive) && _viewModel is { IsInteractive: false })
        {
            // Start's operation waits: no drag goes on.
            EndDrag();
            _press = null;
        }
    }

    /// <summary>
    /// Esc (2.2): a row drag in progress is cancelled first, and the row stays where it was;
    /// otherwise the list closes, asking first when it was changed. While a diff covers the list,
    /// or no list is open, it declines, so that the diff's own handler closes the diff (14.3).
    /// </summary>
    private bool HandleEscape()
    {
        if (_viewModel is not { IsClosed: false } viewModel || !IsEffectivelyVisible)
        {
            return false;
        }

        if (_drag is not null)
        {
            EndDrag();
            return true;
        }

        _press = null;
        if (viewModel.CancelCommand.CanExecute(null))
        {
            viewModel.CancelCommand.Execute(null);
        }

        return true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _drag is not null || _viewModel is not { IsInteractive: true, HasMessage: false } viewModel || !IsInRows(e.Source))
        {
            return;
        }

        switch (e.Key, e.KeyModifiers)
        {
            case (Key.Up, KeyModifiers.None):
                viewModel.MoveSelection(-1);
                break;
            case (Key.Down, KeyModifiers.None):
                viewModel.MoveSelection(1);
                break;
            case (Key.Home, KeyModifiers.None):
                viewModel.SelectFirst();
                break;
            case (Key.End, KeyModifiers.None):
                viewModel.SelectLast();
                break;
            case (Key.Up, KeyModifiers.Alt):
                viewModel.MoveSelectedRow(-1);
                break;
            case (Key.Down, KeyModifiers.Alt):
                viewModel.MoveSelectedRow(1);
                break;
            case (Key.P or Key.R or Key.S or Key.F or Key.D, KeyModifiers.None):
                _ = viewModel.ApplyLetterAsync(char.ToLowerInvariant(e.Key.ToString()[0]));
                e.Handled = true;
                return;
            default:
                return;
        }

        FocusSelectedRow(NavigationMethod.Directional);
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is not { IsInteractive: true } viewModel || RowOf(e.Source) is not { } row)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            // The press selects the row; moving on with the button held drags it (2.2).
            viewModel.Select(row);
            FocusRow(row, NavigationMethod.Pointer);
            _press = new PendingPress(row, e.Pointer, e.GetPosition(RowsArea));
            e.Handled = true;
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            // A right-click selects the row; its menu opens when the button is released.
            viewModel.Select(row);
            FocusRow(row, NavigationMethod.Pointer);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_drag is { } drag)
        {
            if (ReferenceEquals(e.Pointer, drag.Pointer))
            {
                ShowGap(e.GetPosition(RowsArea).Y);
                e.Handled = true;
            }

            return;
        }

        if (_press is not { } press || !ReferenceEquals(e.Pointer, press.Pointer))
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _viewModel is not { IsInteractive: true })
        {
            _press = null;
            return;
        }

        var position = e.GetPosition(RowsArea);
        var moved = position - press.At;
        if (Math.Sqrt((moved.X * moved.X) + (moved.Y * moved.Y)) > DragThreshold)
        {
            _press = null;
            _drag = new RowDrag(press.Row, e.Pointer);
            press.Row.IsDragged = true;
            e.Pointer.Capture(this);
            ShowGap(position.Y);
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _press = null;
        if (_drag is not { } drag || !ReferenceEquals(e.Pointer, drag.Pointer))
        {
            return;
        }

        var gap = drag.Gap;
        EndDrag();
        if (gap is { } target && _viewModel is { } viewModel)
        {
            viewModel.DropRow(drag.Row, target);
            FocusRow(drag.Row, NavigationMethod.Pointer);
        }

        e.Handled = true;
    }

    /// <summary>
    /// The row's menu (2.2, 14.3): a right-click opens it 2 right of and 2 below the pointer; the
    /// Menu key and Shift+F10, which carry no position, at the selected row's action button's
    /// bottom-left corner. The base row, the header and the footer have none.
    /// </summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || _drag is not null || _viewModel is not { IsInteractive: true, HasMessage: false } viewModel
            || ContextMenuRequests.Layer(this) is not { } layer || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        if (e.TryGetPosition(topLevel, out var pointer))
        {
            if (RowOf(e.Source) is { } row)
            {
                viewModel.Select(row);
                FocusRow(row, NavigationMethod.Pointer);
                layer.Open(viewModel.MenuFor(row), new MenuPosition(pointer.X, pointer.Y), MenuOpenedBy.Pointer);
            }
        }
        else if (viewModel.SelectedRow is { } selected && ActionButtonCorner(selected, topLevel) is { } corner)
        {
            layer.Open(viewModel.MenuFor(selected), new MenuPosition(corner.X, corner.Y), MenuOpenedBy.Keyboard);
        }

        e.Handled = true;
    }

    /// <summary>
    /// The action button selects its row and opens the row's menu with its top-left at the
    /// button's bottom-left corner (14.3); <c>Edit…</c> opens the message dialog (7.2) for its row,
    /// leaving the selection as it is.
    /// </summary>
    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not { IsInteractive: true } viewModel || e.Source is not Button { DataContext: InteractiveRebaseRowViewModel row } button)
        {
            return;
        }

        if (button.Name == "RebaseActionButton")
        {
            if (ContextMenuRequests.Layer(this) is not { } layer || TopLevel.GetTopLevel(this) is not { } topLevel)
            {
                return;
            }

            viewModel.Select(row);
            FocusRow(row, NavigationMethod.Pointer);
            if (button.TranslatePoint(new Point(0, button.Bounds.Height), topLevel) is { } corner)
            {
                // Opened by the pointer (the log says so, D106) and placed with the pointer's gap
                // taken back, so that its top-left lies on the corner as a keyboard menu's does.
                layer.Open(viewModel.MenuFor(row), new MenuPosition(corner.X - MenuLayer.PointerGap, corner.Y - MenuLayer.PointerGap), MenuOpenedBy.Pointer);
            }

            e.Handled = true;
        }
        else if (button.Name == "RebaseEditMessageButton")
        {
            _ = viewModel.EditMessageAsync(row);
            e.Handled = true;
        }
    }

    /// <summary>Shows the drag's line on the gap nearest <paramref name="y"/> (in the rows area): gap k above row k, the last gap above the base row.</summary>
    private void ShowGap(double y)
    {
        if (_drag is not { } drag || _viewModel is not { } viewModel)
        {
            return;
        }

        int? nearest = null;
        var nearestY = 0.0;
        for (var index = 0; index <= viewModel.Rows.Count; index++)
        {
            // Gap k is the top edge of row k's container; the last one the base row's top edge.
            if (RowsList.ContainerFromIndex(index) is not Control { IsVisible: true } container
                || container.TranslatePoint(default, RowsArea) is not { } top)
            {
                continue;
            }

            if (nearest is null || Math.Abs(y - top.Y) < Math.Abs(y - nearestY))
            {
                (nearest, nearestY) = (index, top.Y);
            }
        }

        drag.Gap = nearest;
        RebaseDragLine.IsVisible = nearest is not null;
        if (nearest is not null)
        {
            RebaseDragLine.Width = RowsArea.Bounds.Width;
            Canvas.SetLeft(RebaseDragLine, 0);
            Canvas.SetTop(RebaseDragLine, nearestY - 1);
        }
    }

    /// <summary>Ends a row drag without moving anything (Esc, a lost pointer) or after its drop.</summary>
    private void EndDrag()
    {
        if (_drag is not { } drag)
        {
            return;
        }

        _drag = null;
        drag.Row.IsDragged = false;
        RebaseDragLine.IsVisible = false;
        if (ReferenceEquals(drag.Pointer.Captured, this))
        {
            drag.Pointer.Capture(null);
        }
    }

    private void FocusSelectedRow(NavigationMethod method)
    {
        if (_viewModel is not { } viewModel)
        {
            return;
        }

        if (viewModel.HasMessage)
        {
            RebaseCancelButton.Focus(method);
            return;
        }

        if (viewModel.SelectedRow is { } row)
        {
            FocusRow(row, method);
        }
    }

    /// <summary>Scrolls <paramref name="row"/> into view and gives its row the keyboard focus.</summary>
    private void FocusRow(InteractiveRebaseRowViewModel row, NavigationMethod method)
    {
        var index = _viewModel?.IndexOf(row) ?? -1;
        if (index < 0)
        {
            return;
        }

        RowsList.ScrollIntoView(index);
        if (RowPart(index) is { } part && part.Focus(method))
        {
            return;
        }

        // The row's container is made by the next layout pass (after a move, or a scroll).
        Dispatcher.UIThread.Post(
            () =>
            {
                if (_viewModel?.IndexOf(row) is { } now and >= 0)
                {
                    RowsList.ScrollIntoView(now);
                    RowPart(now)?.Focus(method);
                }
            },
            DispatcherPriority.Loaded);
    }

    /// <summary>The bottom-left corner of <paramref name="row"/>'s action button in <paramref name="topLevel"/>'s coordinates, where its keyboard menu opens.</summary>
    private Point? ActionButtonCorner(InteractiveRebaseRowViewModel row, Visual topLevel)
    {
        var index = _viewModel?.IndexOf(row) ?? -1;
        if (index < 0)
        {
            return null;
        }

        RowsList.ScrollIntoView(index);
        return RowPart(index) is { } part && Named<Button>(part, "RebaseActionButton") is { } button
            ? button.TranslatePoint(new Point(0, button.Bounds.Height), topLevel)
            : null;
    }

    /// <summary>The row part (<see cref="RebaseListRow"/>) of the item at <paramref name="index"/> (the base row at the rows' count), or null when it is not realised.</summary>
    private RebaseListRow? RowPart(int index) =>
        RowsList.ContainerFromIndex(index) is Control container
            ? container.GetVisualDescendants().OfType<RebaseListRow>().FirstOrDefault()
            : null;

    /// <summary>The commit row whose container holds <paramref name="source"/> (its row part or its second line), or null.</summary>
    private InteractiveRebaseRowViewModel? RowOf(object? source)
    {
        for (var visual = source as Visual; visual is not null && !ReferenceEquals(visual, RowsList); visual = visual.GetVisualParent())
        {
            if (visual is StyledElement { DataContext: InteractiveRebaseRowViewModel row } && _viewModel?.IndexOf(row) >= 0)
            {
                return row;
            }
        }

        return null;
    }

    private bool IsInRows(object? source) => source is Visual visual && (ReferenceEquals(visual, RowsList) || RowsList.IsVisualAncestorOf(visual));

    private void ShowAuthorColumnIfWide() => RowsList.Classes.Set("wide", RowsArea.Bounds.Width >= AuthorColumnMinWidth);

    private InteractiveRebaseRowSnapshot RowSnapshot(InteractiveRebaseViewModel viewModel, int index, Visual to)
    {
        var row = viewModel.Rows[index];
        var container = RowsList.ContainerFromIndex(index) as Control;
        var part = RowPart(index);
        var secondLine = container is null ? null : Named<Control>(container, "RebaseSecondLinePart");
        var sha = part is null ? null : Named<TextBlock>(part, "RebaseRowSha");
        var subject = part is null ? null : Named<TextBlock>(part, "RebaseRowSubject");
        var author = part is null ? null : Named<TextBlock>(part, "RebaseRowAuthor");
        var grip = part is null ? null : Named<Icon>(part, "RebaseRowGrip");
        var button = part is null ? null : Named<Button>(part, "RebaseActionButton");
        var edit = secondLine is null ? null : Named<Button>(secondLine, "RebaseEditMessageButton");
        var shown = secondLine is { IsVisible: true };
        return new InteractiveRebaseRowSnapshot(
            index,
            row.Sha,
            row.ShortSha,
            row.Subject,
            row.Name,
            row.Action,
            (button is null ? null : Named<TextBlock>(button, "RebaseActionText")?.Text) ?? row.ActionText,
            row.Indent,
            container?.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault()?.Opacity ?? (row.IsDragged ? 0.5 : 1),
            sha?.TextDecorations is { Count: > 0 } && subject?.TextDecorations is { Count: > 0 },
            row.IsSelected,
            part?.IsFocused ?? false,
            row.SecondLine,
            row.JoinTarget?.Sha,
            row.JoinLine,
            part is null ? null : BoundsIn(part, to),
            grip is null ? null : BoundsIn(grip, to),
            button is null ? null : BoundsIn(button, to),
            sha is null ? null : BoundsIn(sha, to),
            subject is null ? null : BoundsIn(subject, to),
            author is { IsEffectivelyVisible: true } ? BoundsIn(author, to) : null,
            shown ? BoundsIn(secondLine!, to) : null,
            shown && edit is not null ? BoundsIn(edit, to) : null,
            ColorOf(part?.Background),
            ColorOf(sha?.Foreground),
            ColorOf(subject?.Foreground),
            ColorOf(grip?.Foreground),
            subject is null ? null : ToolTip.GetTip(subject) as string);
    }

    private Color? JoinLineColor(int index) =>
        RowPart(index) is { } part && Named<Border>(part, "RebaseJoinLine") is { } line ? ColorOf(line.Background) : null;

    private static T? Named<T>(Visual scope, string name)
        where T : Control =>
        scope.GetVisualDescendants().OfType<T>().FirstOrDefault(control => control.Name == name);

    private static Rect BoundsIn(Visual visual, Visual to) =>
        visual.TranslatePoint(default, to) is { } topLeft ? new Rect(topLeft, visual.Bounds.Size) : default;

    private static Color? ColorOf(IBrush? brush) => brush is ISolidColorBrush { Color.A: > 0 } solid ? solid.Color : null;

    /// <summary>A left press on a row that becomes a drag once the pointer moves past the threshold.</summary>
    private sealed record PendingPress(InteractiveRebaseRowViewModel Row, IPointer Pointer, Point At);

    /// <summary>A row being dragged, and the gap its line marks now.</summary>
    private sealed class RowDrag(InteractiveRebaseRowViewModel row, IPointer pointer)
    {
        public InteractiveRebaseRowViewModel Row { get; } = row;

        public IPointer Pointer { get; } = pointer;

        public int? Gap { get; set; }
    }
}
