using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// The activity log's panel (docs/phase-5-design.md 2.1, 2.2): see <see cref="ActivityLogViewModel"/>.
/// The code-behind does what bindings cannot:
/// <list type="bullet">
/// <item>the list keeps to its end while it grows, a new row or an opened one, unless the user
/// scrolled up (AD15); each worktree's list comes back where it was left;</item>
/// <item>a click on an operation's or a call's row opens or closes it; Right and Left do the same
/// with the keyboard, Up, Down, Home and End move the focus between the rows;</item>
/// <item>a row's menu (2.2) through the window's menu layer;</item>
/// <item>Esc (<see cref="EscapeOrder.ActivityLog"/>) closes the panel, but only while the focus is
/// inside it;</item>
/// <item>the test surface (<see cref="Snapshot"/>).</item>
/// </list>
/// </summary>
public partial class ActivityLogView : UserControl
{
    private ActivityLogViewModel? _viewModel;
    private ActivityLogWorktree? _shown;
    private IDisposable? _escape;
    private bool _keepAtEnd = true;
    private bool _restoring;
    private bool _scrollingToRow;

    public ActivityLogView()
    {
        InitializeComponent();
        ActivityLogList.ScrollChanged += OnScrollChanged;
        AddHandler(PointerPressedEvent, OnPointerPressed);

        // Keys are seen on their way down, before the list's scrolling takes the arrows.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(ContextRequestedEvent, OnContextRequested);
    }

    /// <summary>Whether the keyboard focus is in the panel.</summary>
    public bool HasFocusWithin =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused
        && focused.GetSelfAndVisualAncestors().Contains(this);

    /// <summary>
    /// The panel as it is drawn now (the test surface, 15.2 and AD12): its parts and its rows top
    /// to bottom, every row the list holds, each, while the list has realised it, with its bounds in
    /// <paramref name="relativeTo"/>'s coordinates (this view's when null) and the parts, texts and
    /// colours its controls draw. It lets the UI thread run nothing: settle first.
    /// </summary>
    public ActivityLogSnapshot Snapshot(Visual? relativeTo = null)
    {
        var to = relativeTo ?? this;
        var rows = new List<ActivityLogRowSnapshot>();
        var listBounds = BoundsIn(ActivityLogList, to);
        var items = _viewModel?.Current?.Rows ?? [];
        for (var index = 0; index < items.Count; index++)
        {
            rows.Add(RowSnapshot(items[index], index, to, listBounds));
        }

        return new ActivityLogSnapshot(
            BoundsIn(this, to),
            BoundsIn(ActivityLogHeader, to),
            listBounds,
            ActivityLogTitle.Text ?? string.Empty,
            ActivityLogRepository.Text ?? string.Empty,
            ColorOf(ActivityLogRepository.Foreground),
            ActivityLogShowReads.IsChecked == true,
            BoundsIn(ActivityLogShowReads, to),
            BoundsIn(ActivityLogClose, to),
            rows);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ActivityLogAutomationPeer(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is MainWindow window)
        {
            _escape = window.AddEscapeHandler(new EscapeHandler(HandleEscape), EscapeOrder.ActivityLog);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _escape?.Dispose();
        _escape = null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as ActivityLogViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ShowWorktree(_viewModel?.Current);
        base.OnDataContextChanged(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActivityLogViewModel.Current))
        {
            ShowWorktree(_viewModel?.Current);
        }
        else if (e.PropertyName == nameof(ActivityLogViewModel.IsShown) && _viewModel is { IsShown: true })
        {
            // Opened again: where the list was, at its end unless the user had scrolled up.
            ShowWorktree(_viewModel.Current, force: true);
        }
    }

    /// <summary>The list shows another worktree's rows: it comes back where that worktree's list was left (2.1).</summary>
    private void ShowWorktree(ActivityLogWorktree? worktree, bool force = false)
    {
        if (ReferenceEquals(worktree, _shown) && !force)
        {
            return;
        }

        _shown = worktree;
        if (worktree is null)
        {
            return;
        }

        _restoring = true;
        _keepAtEnd = worktree.IsScrolledToEnd;
        var offset = worktree.ScrollOffset;
        Dispatcher.UIThread.Post(
            () =>
            {
                if (!ReferenceEquals(_shown, worktree))
                {
                    return;
                }

                if (_keepAtEnd)
                {
                    ScrollToEnd();
                }
                else
                {
                    ActivityLogList.Offset = new Vector(0, offset);
                }

                _restoring = false;
            },
            DispatcherPriority.Loaded);
    }

    /// <summary>
    /// While the list is at its end, anything that makes it longer keeps it there (2.2, AD15);
    /// a scroll by the user decides whether it is at its end, and so does a list that became
    /// shorter after one: closed rows can bring its end back into view. The worktree keeps where
    /// it is.
    /// </summary>
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scrollingToRow)
        {
            // FocusRow decides once the list is at the row.
            return;
        }

        if (e.ExtentDelta.Y != 0 || e.ViewportDelta.Y != 0)
        {
            if (_keepAtEnd)
            {
                ScrollToEnd();
            }
            else if (!_restoring)
            {
                _keepAtEnd = IsAtEnd();
            }
        }
        else if (e.OffsetDelta.Y != 0 && !_restoring)
        {
            _keepAtEnd = IsAtEnd();
        }

        RememberScroll();
    }

    /// <summary>The worktree keeps where its list is (2.1), unless the list is being put back there.</summary>
    private void RememberScroll()
    {
        if (_shown is { } shown && !_restoring)
        {
            shown.IsScrolledToEnd = _keepAtEnd;
            shown.ScrollOffset = ActivityLogList.Offset.Y;
        }
    }

    private bool IsAtEnd() =>
        ActivityLogList.Offset.Y >= ActivityLogList.Extent.Height - ActivityLogList.Viewport.Height - 1;

    private void ScrollToEnd()
    {
        var end = Math.Max(0, ActivityLogList.Extent.Height - ActivityLogList.Viewport.Height);
        if (Math.Abs(ActivityLogList.Offset.Y - end) > 0.5)
        {
            ActivityLogList.Offset = new Vector(0, end);
        }
    }

    /// <summary>Esc (2.1): closes the panel while the focus is inside it; otherwise it is for the resolver, the list or a page.</summary>
    private bool HandleEscape()
    {
        if (_viewModel is not { IsShown: true } viewModel || !IsEffectivelyVisible || !HasFocusWithin)
        {
            return false;
        }

        viewModel.Close();
        return true;
    }

    /// <summary>A click on an operation's or a call's row opens or closes it (2.2); any row of the list takes the focus.</summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is not { } viewModel || RowPanelOf(e.Source) is not { DataContext: ActivityLogRow row } panel)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || row is ActivityLogOutputRow)
        {
            return;
        }

        FocusPanel(panel, NavigationMethod.Pointer);
        if (row is ActivityLogOperationRow or ActivityLogCallRow)
        {
            viewModel.ToggleRow(row);
            FocusRow(row, NavigationMethod.Pointer);
        }

        e.Handled = true;
    }

    /// <summary>Up, Down, Home and End move the focus between the rows; Right opens a row, Left closes it or goes to the row it is under (2.2).</summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || _viewModel is not { Current: { } current } viewModel
            || RowPanelOf(e.Source) is not { DataContext: ActivityLogRow row })
        {
            return;
        }

        var rows = current.Rows;
        var index = rows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Up:
                FocusNearest(rows, index - 1, -1);
                break;
            case Key.Down:
                FocusNearest(rows, index + 1, 1);
                break;
            case Key.Home:
                FocusNearest(rows, 0, 1);
                break;
            case Key.End:
                FocusNearest(rows, rows.Count - 1, -1);
                break;
            case Key.Right when row is ActivityLogOperationRow or ActivityLogCallRow:
                viewModel.SetOpen(row, open: true);
                FocusRow(row, NavigationMethod.Directional);
                break;
            case Key.Left when row is ActivityLogOperationRow { IsOpen: true } or ActivityLogCallRow { IsOpen: true }:
                viewModel.SetOpen(row, open: false);
                FocusRow(row, NavigationMethod.Directional);
                break;
            case Key.Left when row.Depth > 0:
                var parent = rows.Take(index).LastOrDefault(candidate => candidate.Depth < row.Depth);
                if (parent is not null)
                {
                    FocusRow(parent, NavigationMethod.Directional);
                }

                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>A row's menu (2.2): copy its command, its output or its title.</summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (RowPanelOf(e.Source) is not { DataContext: ActivityLogRow row } panel
            || ActivityLogViewModel.MenuTargetOf(row) is not { } target
            || ContextMenuRequests.ActiveRepository(this) is not { } active)
        {
            return;
        }

        var (layer, repository) = active;
        var (position, openedBy) = ContextMenuRequests.Where(e, panel);
        e.Handled = repository.OpenMenu(layer, RepositoryMenus.ForActivityLogRow(repository.MenuContext, target, repository.MenuActions), position, openedBy);
    }

    /// <summary>Gives the focus to the first row from <paramref name="index"/> on, going by <paramref name="step"/>, that takes it.</summary>
    private void FocusNearest(IReadOnlyList<ActivityLogRow> rows, int index, int step)
    {
        for (var at = index; at >= 0 && at < rows.Count; at += step)
        {
            if (rows[at] is not ActivityLogOutputRow)
            {
                FocusRow(rows[at], NavigationMethod.Directional);
                return;
            }
        }
    }

    /// <summary>
    /// Scrolls <paramref name="row"/> into view and gives its panel the focus, once the list has
    /// made it. While the list scrolls to a row it does not keep to its end (AD15): the new
    /// estimates of its length on the way would take it back there; whether it is at its end is
    /// decided once it is at the row.
    /// </summary>
    private void FocusRow(ActivityLogRow row, NavigationMethod method)
    {
        var index = _viewModel?.Current?.Rows.IndexOf(row) ?? -1;
        if (index < 0)
        {
            return;
        }

        if (!IsWhollyInView(index))
        {
            _scrollingToRow = true;
            try
            {
                ActivityLogRows.ScrollIntoView(index);
            }
            finally
            {
                _scrollingToRow = false;
            }

            _keepAtEnd = IsAtEnd();
            RememberScroll();
        }

        if (RowPanelAt(index) is { } panel)
        {
            FocusPanel(panel, method);
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                var at = _viewModel?.Current?.Rows.IndexOf(row) ?? -1;
                if (at >= 0 && RowPanelAt(at) is { } later)
                {
                    FocusPanel(later, method);
                }
            },
            DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Gives a row's panel the focus, then makes its container the list's active one, which the
    /// list's virtualising panel keeps while it scrolls: any other container it may hand to
    /// another row, and the focus would go with it (the scroll to the list's end, AD15, after a
    /// box opened under the last call did). The focus moves first: the panel lets go of the
    /// container it kept until then, which must no longer hold the focus.
    /// </summary>
    private void FocusPanel(ActivityLogRowPanel panel, NavigationMethod method)
    {
        panel.Focus(method);
        if (panel.DataContext is { } row && ActivityLogRows.ContainerFromItem(row) is { } container)
        {
            KeyboardNavigation.SetTabOnceActiveElement(ActivityLogRows, container);
        }
    }

    /// <summary>Whether the row at <paramref name="index"/> is made and lies whole inside the list's view.</summary>
    private bool IsWhollyInView(int index) =>
        RowPanelAt(index) is { IsEffectivelyVisible: true } panel
        && panel.TranslatePoint(default, ActivityLogList) is { } top
        && top.Y >= -0.5
        && top.Y + panel.Bounds.Height <= ActivityLogList.Viewport.Height + 0.5;

    private ActivityLogRowPanel? RowPanelAt(int index) =>
        ActivityLogRows.ContainerFromIndex(index) switch
        {
            ContentPresenter { Child: ActivityLogRowPanel panel } => panel,
            ActivityLogRowPanel panel => panel,
            var container => container?.GetVisualDescendants().OfType<ActivityLogRowPanel>().FirstOrDefault(),
        };

    private static ActivityLogRowPanel? RowPanelOf(object? source) => ContextMenuRequests.Find<ActivityLogRowPanel>(source);

    /// <summary>
    /// One row as it is drawn (the report's part A, "Reading the log"): every part is read from the
    /// row's realised controls, its texts from the named text blocks, its chevron and glyph from the
    /// icons' outlines, its indent from where its content starts, so that a binding gone wrong shows.
    /// A row the list has not realised has its place, kind, depth, open state and name from the list
    /// only, and no part.
    /// </summary>
    private ActivityLogRowSnapshot RowSnapshot(ActivityLogRow row, int index, Visual to, Rect listBounds)
    {
        var panel = RowPanelAt(index) is { IsEffectivelyVisible: true } realised && ReferenceEquals(realised.DataContext, row) ? realised : null;
        Rect? bounds = panel is null ? null : BoundsIn(panel, to);
        var inView = bounds is { } shown && shown.Top >= listBounds.Top - 0.5 && shown.Bottom <= listBounds.Bottom + 0.5;
        T? Named<T>(string name)
            where T : Control =>
            panel?.GetVisualDescendants().OfType<T>().FirstOrDefault(control => control.Name == name);
        string? TextOf(string name) => Named<TextBlock>(name)?.Text;
        Color? ColorNamed(string name) => ColorOf(Named<TextBlock>(name)?.Foreground);

        var glyph = Named<Icon>("ActivityLogOutcome") is { IsEffectivelyVisible: true } shownGlyph ? shownGlyph : null;
        var box = Named<ActivityLogOutputBox>("ActivityLogOutput");

        // The content starts after the row's padding: the chevron of an operation or a call, a
        // line's text, the output box; the spacer before it is the indent.
        var content = (Control?)Named<Icon>("ActivityLogChevron") ?? (Control?)Named<TextBlock>("ActivityLogLineText") ?? box;
        var grid = panel?.Children.OfType<Grid>().FirstOrDefault();
        var indent = content is not null && grid is not null ? content.TranslatePoint(default, grid)?.X : null;

        return new ActivityLogRowSnapshot(
            index,
            row.Kind,
            row.Depth,
            panel is null ? row.AutomationName : AutomationProperties.GetName(panel) ?? string.Empty,
            row is ActivityLogOperationRow { IsOpen: true } or ActivityLogCallRow { IsOpen: true },
            bounds,
            inView)
        {
            Indent = indent,
            Chevron = IconName(Named<Icon>("ActivityLogChevron")),
            Glyph = IconName(glyph),
            GlyphColor = ColorOf(glyph?.Foreground),
            Time = TextOf("ActivityLogTime"),
            Title = TextOf("ActivityLogOperationTitle"),
            Message = TextOf("ActivityLogMessage"),
            MessageColor = ColorNamed("ActivityLogMessage"),
            Duration = TextOf("ActivityLogDuration"),
            ExitText = TextOf("ActivityLogExit"),
            ExitColor = ColorNamed("ActivityLogExit"),
            Command = TextOf("ActivityLogCommand"),
            OnTemporaryIndex = Named<TextBlock>("ActivityLogTemporaryIndex")?.IsEffectivelyVisible,
            Text = box is not null ? box.Text : TextOf("ActivityLogLineText"),
            TextColor = ColorNamed("ActivityLogLineText"),
            Parts = box is null ? null : DrawnParts(box),
        };
    }

    /// <summary>The output box's parts as drawn: each part's title over its text, top to bottom; none when it says <c>No output</c>.</summary>
    private static List<ActivityLogOutputPart> DrawnParts(ActivityLogOutputBox box)
    {
        var blocks = box.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).ToList();
        var titles = blocks.Where(block => block.Name == "ActivityLogPartTitle").ToList();
        var texts = blocks.Where(block => block.Name == "ActivityLogPartText").ToList();
        return [.. titles.Zip(texts, (title, text) => new ActivityLogOutputPart(title.Text ?? string.Empty, text.Text ?? string.Empty))];
    }

    /// <summary>The icons a row of the log draws (2.2), by their keys in Theme/Icons.axaml.</summary>
    private static readonly string[] RowIcons = ["IconChevronRight", "IconChevronDown", "IconCheck", "IconAlert", "IconClose", "IconMinus"];

    /// <summary>
    /// The key of the outline <paramref name="icon"/> draws (one of <see cref="RowIcons"/>), "?" for
    /// any other outline, or null when it draws none or is not shown.
    /// </summary>
    private string? IconName(Icon? icon)
    {
        if (icon is not { IsEffectivelyVisible: true, Data: { } data })
        {
            return null;
        }

        foreach (var key in RowIcons)
        {
            if (this.TryFindResource(key, ActualThemeVariant, out var outline) && ReferenceEquals(outline, data))
            {
                return key;
            }
        }

        return "?";
    }

    private static Rect BoundsIn(Visual visual, Visual to) =>
        visual.TranslatePoint(default, to) is { } topLeft ? new Rect(topLeft, visual.Bounds.Size) : default;

    private static Color? ColorOf(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    /// <summary>The panel as UI Automation sees it (<c>ActivityLog</c>): a pane, a control element.</summary>
    private sealed class ActivityLogAutomationPeer(ActivityLogView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>The activity log's panel as it is drawn (<see cref="ActivityLogView.Snapshot"/>).</summary>
/// <param name="Bounds">The panel (<c>ActivityLog</c>).</param>
/// <param name="HeaderBounds">Its header (<c>ActivityLogHeader</c>), 30 high.</param>
/// <param name="ListBounds">Its list (<c>ActivityLogList</c>): the part of the rows in view.</param>
/// <param name="Title">The header's <c>Activity log</c>.</param>
/// <param name="RepositoryName">The header's repository name.</param>
/// <param name="RepositoryNameColor">Its colour: the secondary text.</param>
/// <param name="ShowReads">Whether <c>Show reads</c> is checked.</param>
/// <param name="ShowReadsBounds">The check box (<c>ActivityLogShowReads</c>).</param>
/// <param name="CloseBounds">The close button (<c>ActivityLogClose</c>).</param>
/// <param name="Rows">Every row of the list, top to bottom.</param>
public sealed record ActivityLogSnapshot(
    Rect Bounds,
    Rect HeaderBounds,
    Rect ListBounds,
    string Title,
    string RepositoryName,
    Color? RepositoryNameColor,
    bool ShowReads,
    Rect ShowReadsBounds,
    Rect CloseBounds,
    IReadOnlyList<ActivityLogRowSnapshot> Rows);

/// <summary>
/// One row of the activity log as it is drawn (the report's part A, "Reading the log"): its place
/// in the list, and while the list has realised it, where it is and each part its controls draw.
/// Every part is null for a row that is not realised and for a part the row does not have.
/// </summary>
/// <param name="Index">Its place in the list, from 0 at the top.</param>
/// <param name="Kind">An operation, a failure's summary, a note, "no longer kept", a call or an output box.</param>
/// <param name="Depth">0 for an operation and a read, 1 under an operation, 2 for an operation's call's box.</param>
/// <param name="AutomationName">Its name to UI Automation: the title, the command, the line's text, "Output"; the realised row's own, or for a row not realised the one it will have.</param>
/// <param name="IsOpen">An operation or a call that shows what it opens to: its lines and calls, or its box, follow it in the list.</param>
/// <param name="Bounds">Where it is drawn, or null while the list has not realised it.</param>
/// <param name="IsInView">It lies whole inside the list's visible part.</param>
public sealed record ActivityLogRowSnapshot(
    int Index,
    ActivityLogRowKind Kind,
    int Depth,
    string AutomationName,
    bool IsOpen,
    Rect? Bounds,
    bool IsInView)
{
    /// <summary>How far its content (the chevron, a line's text, the box) starts after the row's padding: 24 per level.</summary>
    public double? Indent { get; init; }

    /// <summary>An operation's or a call's chevron: <c>IconChevronRight</c>, or <c>IconChevronDown</c> while open.</summary>
    public string? Chevron { get; init; }

    /// <summary>
    /// An operation's outcome glyph as drawn: <c>IconCheck</c>, <c>IconAlert</c>, <c>IconClose</c> or
    /// <c>IconMinus</c>, "?" for another outline, null while it runs (no glyph).
    /// </summary>
    public string? Glyph { get; init; }

    /// <summary>The glyph's colour as drawn, or null without one.</summary>
    public Color? GlyphColor { get; init; }

    /// <summary>An operation's time, <c>HH:mm:ss</c>.</summary>
    public string? Time { get; init; }

    /// <summary>An operation's title, whole (a cut one shows it in its tooltip).</summary>
    public string? Title { get; init; }

    /// <summary>An operation's message: the status bar's text, <c>Running…</c>, or empty.</summary>
    public string? Message { get; init; }

    /// <summary>The message's colour as drawn.</summary>
    public Color? MessageColor { get; init; }

    /// <summary>An operation's or a call's duration; empty while an operation runs.</summary>
    public string? Duration { get; init; }

    /// <summary>A call's <c>exit 0</c>, <c>exit 1</c>, <c>cancelled</c> or <c>failed to start</c>.</summary>
    public string? ExitText { get; init; }

    /// <summary>The exit text's colour as drawn.</summary>
    public Color? ExitColor { get; init; }

    /// <summary>A call's command, whole.</summary>
    public string? Command { get; init; }

    /// <summary>Whether <c>(on a temporary index)</c> shows after a call's command.</summary>
    public bool? OnTemporaryIndex { get; init; }

    /// <summary>A line's text, or an output box's whole text (its value to UI Automation).</summary>
    public string? Text { get; init; }

    /// <summary>A line's colour as drawn.</summary>
    public Color? TextColor { get; init; }

    /// <summary>An output box's titled parts as drawn, empty when it says <c>No output</c>.</summary>
    public IReadOnlyList<ActivityLogOutputPart>? Parts { get; init; }
}
