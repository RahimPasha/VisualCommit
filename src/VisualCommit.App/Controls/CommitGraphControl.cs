using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.Views.Graph;
using VisualCommit.Core;

namespace VisualCommit.App.Controls;

/// <summary>
/// The commit graph's rows: ref labels, lanes and nodes, message, author, date and short id of
/// every commit, drawn by hand and only for the rows in view (D49). A list control with one
/// element per row cannot keep 100,000 commits smooth; this control draws a frame from the
/// <see cref="CommitGraphData"/> directly and caches the laid-out text of recently drawn rows.
/// </summary>
/// <remarks>
/// <para>
/// The geometry is fixed by the phase 1 test report: rows <see cref="RowHeight"/> high, columns
/// as <see cref="GraphColumns"/> computes them, lanes 16 apart. UI Automation sees the control
/// as one element; single rows are not exposed in phase 1 (D49).
/// </para>
/// <para>
/// <see cref="SelectedIndex"/> and <see cref="ScrollOffset"/> bind two-way by default, so that a
/// view model can keep both per tab. The control changes them with <c>SetCurrentValue</c>,
/// which keeps those bindings.
/// </para>
/// <para>
/// Phase 3: several rows can be selected (<see cref="Selection"/>, the gestures in
/// CommitGraphControl.Selection.cs), a right-click selects what it is on and asks for a menu
/// (<see cref="ContextRequestedAt"/>), and a branch label can be dragged onto another
/// (CommitGraphControl.Drag.cs, with the window's <c>BranchDragService</c>).
/// </para>
/// <para>
/// Phase 5: a stash's line down to the commit it was made on is dashed, as the working-changes
/// row's is (docs/phase-5-design.md section 10, D161), and a search marks its matches and dims the
/// other rows (3.3, CommitGraphControl.Search.cs).
/// </para>
/// </remarks>
public sealed partial class CommitGraphControl : Control
{
    /// <summary>The height of every row.</summary>
    public const double RowHeight = 26;

    /// <summary>How many rows one notch of the mouse wheel scrolls.</summary>
    public const int RowsPerWheelNotch = 3;

    public static readonly StyledProperty<CommitGraphData?> DataProperty =
        AvaloniaProperty.Register<CommitGraphControl, CommitGraphData?>(nameof(Data));

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<CommitGraphControl, int>(nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The scroll offset as set or bound, not clamped. It has no coercion on purpose: Avalonia
    /// writes a coerced value back through a two-way binding, and when the view switches from
    /// one tab's view model to another's, the new graph arrives before the new offset, so the
    /// old tab's offset would be clamped to the new tab's history and written back into the old
    /// tab. <see cref="ScrollOffset"/> clamps when it is read, and so does drawing.
    /// </summary>
    public static readonly StyledProperty<double> ScrollOffsetProperty =
        AvaloniaProperty.Register<CommitGraphControl, double>(nameof(ScrollOffset), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Whether the working-changes row is drawn above the first commit (D60). It shifts every
    /// commit down by one row; <see cref="SelectedIndex"/> and the methods that take a row still
    /// count commits only.
    /// </summary>
    public static readonly StyledProperty<bool> ShowsWorkingRowProperty =
        AvaloniaProperty.Register<CommitGraphControl, bool>(nameof(ShowsWorkingRow));

    /// <summary>The working-changes row is the selected one; <see cref="SelectedIndex"/> is then -1. Binds two-way by default.</summary>
    public static readonly StyledProperty<bool> IsWorkingRowSelectedProperty =
        AvaloniaProperty.Register<CommitGraphControl, bool>(nameof(IsWorkingRowSelected), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<DateDisplay?> DatesProperty =
        AvaloniaProperty.Register<CommitGraphControl, DateDisplay?>(nameof(Dates));

    /// <summary>
    /// The selected commits as SHAs (docs/phase-3-design.md 4h, D88). Binds two-way by default.
    /// Every row it names is drawn selected while it names two or more; <see cref="SelectedIndex"/>
    /// stays the single selection and is -1 while several are selected.
    /// </summary>
    public static readonly StyledProperty<GraphSelection> SelectionProperty =
        AvaloniaProperty.Register<CommitGraphControl, GraphSelection>(nameof(Selection), GraphSelection.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>What the working-changes row says (D60); WP9 adds the conflicts' count through the view model (section 5).</summary>
    public static readonly StyledProperty<string?> WorkingRowTextProperty =
        AvaloniaProperty.Register<CommitGraphControl, string?>(nameof(WorkingRowText), DefaultWorkingRowText);

    /// <summary>
    /// What a search marks (docs/phase-5-design.md 3.3): while it is active, each matching row is
    /// marked and every other commit row dimmed (CommitGraphControl.Search.cs). Bound to the
    /// repository's <see cref="ViewModels.RepositoryViewModel.SearchMarks"/>.
    /// </summary>
    public static readonly StyledProperty<GraphSearchMarks> SearchProperty =
        AvaloniaProperty.Register<CommitGraphControl, GraphSearchMarks>(nameof(Search), GraphSearchMarks.None);

    /// <summary>The typeface family of every text the control draws: inherited, so it is the app's Inter (D28).</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<CommitGraphControl>();

    public static readonly DirectProperty<CommitGraphControl, double> MaxScrollOffsetProperty =
        AvaloniaProperty.RegisterDirect<CommitGraphControl, double>(nameof(MaxScrollOffset), control => control.MaxScrollOffset);

    public static readonly DirectProperty<CommitGraphControl, double> ViewportHeightProperty =
        AvaloniaProperty.RegisterDirect<CommitGraphControl, double>(nameof(ViewportHeight), control => control.ViewportHeight);

    public static readonly DirectProperty<CommitGraphControl, GraphColumns> ColumnsProperty =
        AvaloniaProperty.RegisterDirect<CommitGraphControl, GraphColumns>(nameof(Columns), control => control.Columns);

    /// <summary>Used when no <see cref="Dates"/> was given, so that the Date column still shows something sensible.</summary>
    private static readonly Lazy<DateDisplay> FallbackDates = new(DateDisplay.Resolve);

    private CommitGraphData? _subscribedData;
    private double _maxScrollOffset;
    private double _viewportHeight;
    private GraphColumns _columns = GraphColumns.Empty;
    /// <summary>The drawn row under the mouse (the working-changes row is row 0 when shown), or -1.</summary>
    private int _hoveredRow = -1;
    private Point? _pointer;
    private int _pendingReveal = -1;

    /// <summary>
    /// A row revealed while the history was still loading whose centring the rows loaded so far
    /// did not allow (it was near their end): revealed again as rows arrive, until it can be
    /// centred, the history is complete, or the user scrolls.
    /// </summary>
    private int _revealWhileLoading = -1;

    static CommitGraphControl()
    {
        FocusableProperty.OverrideDefaultValue<CommitGraphControl>(true);
        ClipToBoundsProperty.OverrideDefaultValue<CommitGraphControl>(true);
        AffectsRender<CommitGraphControl>(SelectedIndexProperty, ScrollOffsetProperty, IsWorkingRowSelectedProperty, SelectionProperty, SearchProperty);
    }

    public CommitGraphControl()
    {
        AutomationProperties.SetAutomationId(this, "GraphRows");
        AutomationProperties.SetName(this, "Commit graph");
        ActualThemeVariantChanged += (_, _) => OnThemeChanged();
        ResourcesChanged += (_, _) => OnThemeChanged();
        AddHandler(ContextRequestedEvent, OnContextRequestedHere);
        AddHandler(DoubleTappedEvent, OnDoubleTappedHere, handledEventsToo: true);
    }

    /// <summary>
    /// Raised when a context menu is asked for over the graph (docs/phase-3-design.md 4a, 4b): a
    /// right-click or the Menu key, or Shift+F10 (the main window raises the request). The
    /// arguments say what is under the point and where the menu opens: the pointer, or from the
    /// keyboard the bottom-left of the selected row's message cell. A handler that opens a menu
    /// sets <see cref="GraphContextRequestedEventArgs.Handled"/>. WP7 handles it.
    /// </summary>
    public event EventHandler<GraphContextRequestedEventArgs>? ContextRequestedAt;

    /// <summary>Raised when a ref label is double-clicked (4c): a branch checks out. WP7 handles it.</summary>
    public event EventHandler<GraphHit>? LabelDoubleTapped;

    /// <summary>Raised after a frame that drew at least one row. The view model logs the first one (D50). Handlers must not change the control.</summary>
    public event EventHandler? RowsDrawn;

    /// <summary>Raised after every frame that drew rows, with the time drawing it took (D50). Handlers must not change the control.</summary>
    public event EventHandler<TimeSpan>? FrameDrawn;

    /// <summary>The commits to draw. A new instance keeps the scroll offset, clamped to its extent.</summary>
    public CommitGraphData? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>The selected row, or -1. A click or the arrow keys change it; it binds two-way by default.</summary>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>
    /// How far the rows are scrolled, in pixels from the top: the value set or bound, clamped to
    /// 0 to <see cref="MaxScrollOffset"/> when read. Binds two-way by default; the control writes
    /// only offsets it has clamped itself (wheel, keys, <see cref="Reveal"/>), so a bound view
    /// model keeps an offset beyond the end of a shorter history until the user scrolls, and the
    /// view returns to it when the rows come back (a reload).
    /// </summary>
    public double ScrollOffset
    {
        get => ClampOffset(GetValue(ScrollOffsetProperty));
        set => SetValue(ScrollOffsetProperty, value);
    }

    /// <summary>Whether the working-changes row is drawn above the first commit (D60).</summary>
    public bool ShowsWorkingRow
    {
        get => GetValue(ShowsWorkingRowProperty);
        set => SetValue(ShowsWorkingRowProperty, value);
    }

    /// <summary>Whether the working-changes row is selected. A click or the arrow keys change it; it binds two-way by default.</summary>
    public bool IsWorkingRowSelected
    {
        get => GetValue(IsWorkingRowSelectedProperty);
        set => SetValue(IsWorkingRowSelectedProperty, value);
    }

    /// <summary>How the Date column formats dates (D43).</summary>
    public DateDisplay? Dates
    {
        get => GetValue(DatesProperty);
        set => SetValue(DatesProperty, value);
    }

    /// <summary>The selected commits (4h); binds two-way by default.</summary>
    public GraphSelection Selection
    {
        get => GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    /// <summary>What the working-changes row says; null says "Working changes".</summary>
    public string? WorkingRowText
    {
        get => GetValue(WorkingRowTextProperty);
        set => SetValue(WorkingRowTextProperty, value);
    }

    /// <summary>What a search marks (3.3); <see cref="GraphSearchMarks.None"/> without a search.</summary>
    public GraphSearchMarks Search
    {
        get => GetValue(SearchProperty);
        set => SetValue(SearchProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>The largest scroll offset: the height of all rows less the height of the view, or 0.</summary>
    public double MaxScrollOffset
    {
        get => _maxScrollOffset;
        private set => SetAndRaise(MaxScrollOffsetProperty, ref _maxScrollOffset, value);
    }

    /// <summary>The height of the view: the control's own height.</summary>
    public double ViewportHeight
    {
        get => _viewportHeight;
        private set => SetAndRaise(ViewportHeightProperty, ref _viewportHeight, value);
    }

    /// <summary>The current column layout, for the control's width and the data's lane count. The column headers follow it.</summary>
    public GraphColumns Columns
    {
        get => _columns;
        private set => SetAndRaise(ColumnsProperty, ref _columns, value);
    }

    /// <summary>The commit row at the top of the view, which may be cut by the view's top edge; -1 while the working-changes row is there.</summary>
    public int FirstVisibleRow => (int)Math.Floor(DrawnOffset / RowHeight) - RowOffset;

    /// <summary>The number of rows the data holds.</summary>
    private int RowCount => Data?.Count ?? 0;

    /// <summary>How many rows the working-changes row adds above the commits: 1 or 0.</summary>
    private int RowOffset => ShowsWorkingRow ? 1 : 0;

    /// <summary>The number of rows drawn: the commits and the working-changes row.</summary>
    private int DisplayCount => RowCount + RowOffset;

    /// <summary>The scroll offset rows are drawn at: whole pixels, so text and the edges of rows stay sharp.</summary>
    private double DrawnOffset => Math.Round(ScrollOffset);

    /// <summary>How many whole rows fit in the view: what Page Up and Page Down move by.</summary>
    private int RowsPerPage => Math.Max(1, (int)Math.Floor(ViewportHeight / RowHeight));

    /// <summary>Where the commit of row <paramref name="index"/> is drawn, in the control's coordinates. It may lie outside the view.</summary>
    public Rect RowBounds(int index) => DisplayRowBounds(index + RowOffset);

    /// <summary>Where the working-changes row is drawn, in the control's coordinates, when it is shown.</summary>
    public Rect WorkingRowBounds => DisplayRowBounds(0);

    private Rect DisplayRowBounds(int display) => new(0, (display * RowHeight) - DrawnOffset, Bounds.Width, RowHeight);

    /// <summary>The x of lane <paramref name="lane"/>'s centre, in the control's coordinates.</summary>
    public double LaneCenterX(int lane) => Columns.LaneCenterX(lane);

    /// <summary>The ref labels of row <paramref name="index"/> as they are drawn, left to right, in the control's coordinates. Empty for a row without refs.</summary>
    public IReadOnlyList<PlacedLabel> LabelsInRow(int index)
    {
        var data = Data;
        if (data is null || index < 0 || index >= data.Count)
        {
            return [];
        }

        var visual = GetRowVisual(data, index);
        if (visual.Labels.Length == 0)
        {
            return [];
        }

        var top = RowBounds(index).Y;
        var placed = new PlacedLabel[visual.Labels.Length];
        for (var i = 0; i < placed.Length; i++)
        {
            var label = visual.Labels[i];
            placed[i] = new PlacedLabel(label.Text, label.FullText, label.Kind, label.Bounds.Translate(new Vector(0, top)), label.IsOverflow, label.RefName);
        }

        return placed;
    }

    /// <summary>
    /// Scrolls so that row <paramref name="index"/> is the row nearest the vertical centre of the
    /// view, as far as the ends of the history allow: how a commit chosen elsewhere (a ref in the
    /// left panel, a parent link) is brought into view. Before the control has a size, the row is
    /// revealed once it has one.
    /// </summary>
    public void Reveal(int index)
    {
        if (index < 0 || index >= RowCount)
        {
            return;
        }

        if (ViewportHeight <= 0)
        {
            _pendingReveal = index;
            return;
        }

        _pendingReveal = -1;
        var target = ((index + RowOffset) * RowHeight) + (RowHeight / 2) - (ViewportHeight / 2);
        _revealWhileLoading = target > MaxScrollOffset && Data is { IsComplete: false } ? index : -1;
        ScrollTo(target);
    }

    /// <summary>Scrolls as little as possible to show row <paramref name="index"/> whole: how a selection made with the keyboard stays in view.</summary>
    public void ScrollIntoView(int index)
    {
        if (index < 0 || index >= RowCount)
        {
            return;
        }

        ScrollDisplayRowIntoView(index + RowOffset);
    }

    private void ScrollDisplayRowIntoView(int display)
    {
        var top = display * RowHeight;
        var bottom = top + RowHeight;
        var offset = ScrollOffset;
        if (top < offset || ViewportHeight < RowHeight)
        {
            ScrollTo(top);
        }
        else if (bottom > offset + ViewportHeight)
        {
            ScrollTo(bottom - ViewportHeight);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CommitGraphAutomationPeer(this);

    protected override Size ArrangeOverride(Size finalSize)
    {
        UpdateExtent(finalSize);
        return finalSize;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeToData(Data);
        OnThemeChanged();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // The data outlives the view when a tab is switched away; do not let it hold the control.
        SubscribeToData(null);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataProperty)
        {
            if (VisualRoot is not null)
            {
                SubscribeToData(Data);
            }

            _revealWhileLoading = -1;
            ClearRowCache();
            UpdateExtent(Bounds.Size);
            InvalidateVisual();

            // The row under the mouse now shows another commit, maybe with other labels.
            UpdateHover();
        }
        else if (change.Property == ScrollOffsetProperty)
        {
            UpdateHover();
        }
        else if (change.Property == ShowsWorkingRowProperty)
        {
            UpdateExtent(Bounds.Size);
            InvalidateVisual();
            UpdateHover();
        }
        else if (change.Property == DatesProperty || change.Property == FontFamilyProperty)
        {
            ClearRowCache();
            InvalidateVisual();
        }
        else if (change.Property == WorkingRowTextProperty)
        {
            _workingRowLayout = null;
            InvalidateVisual();
        }
        else if (change.Property == SearchProperty)
        {
            OnSearchChanged(change.GetOldValue<GraphSearchMarks?>(), change.GetNewValue<GraphSearchMarks?>());
        }
    }

    /// <summary>
    /// What is at <paramref name="point"/> (in the control's coordinates): a ref label (its row's
    /// commit and the label as drawn, the "+N" label included), the rest of a commit's row, the
    /// working-changes row, or nothing (below the last row, outside the control). Phase 1's label
    /// geometry answers it (<see cref="LabelsInRow"/>).
    /// </summary>
    public GraphHit HitTest(Point point)
    {
        var data = Data;
        if (data is null || point.X < 0 || point.X >= Bounds.Width)
        {
            return GraphHit.None;
        }

        var display = DisplayRowAt(point.Y);
        if (display < 0)
        {
            return GraphHit.None;
        }

        if (display < RowOffset)
        {
            return new GraphHit(GraphHitKind.WorkingRow, -1, null, null);
        }

        var row = display - RowOffset;
        var sha = data.CommitAt(row).Sha;
        if (point.X >= Columns.Refs.X && point.X < Columns.Refs.Right)
        {
            foreach (var label in LabelsInRow(row))
            {
                if (label.Bounds.Contains(point))
                {
                    return new GraphHit(GraphHitKind.Label, row, sha, label);
                }
            }
        }

        return new GraphHit(GraphHitKind.Commit, row, sha, null);
    }

    /// <summary>
    /// A context menu is asked for: from the pointer, at its position; from the keyboard, at the
    /// bottom-left of the selected row's message cell, for that row.
    /// </summary>
    private void OnContextRequestedHere(object? sender, ContextRequestedEventArgs e)
    {
        GraphHit hit;
        Point position;
        if (e.TryGetPosition(this, out var pointer))
        {
            // A right-click selects what it is on, keeping a multi-selection it is part of (4b).
            (hit, position) = (HitTest(pointer), pointer);
            if (hit.Kind != GraphHitKind.None)
            {
                Focus(NavigationMethod.Pointer);
                SelectForContext(hit);
            }
        }
        else
        {
            // From the keyboard: the focused row, which is the selected one unless several are.
            var focusRow = FocusRow;
            var display = ShowsWorkingRow && IsWorkingRowSelected ? 0
                : focusRow >= 0 ? focusRow + RowOffset
                : -1;
            var bounds = display >= 0 ? DisplayRowBounds(display) : default;
            var x = Columns.Message.IsVisible ? Columns.Message.X : 0;
            hit = display >= 0 ? HitTest(new Point(x, bounds.Center.Y)) : GraphHit.None;
            position = display >= 0 ? new Point(x, bounds.Bottom) : default;
        }

        if (ContextRequestedAt is not { } handler)
        {
            return;
        }

        var args = new GraphContextRequestedEventArgs(hit, position);
        handler(this, args);
        e.Handled |= args.Handled;
    }

    private void OnDoubleTappedHere(object? sender, TappedEventArgs e)
    {
        // The press after a drag that started on the press before is no double-click (4c).
        if (LabelDoubleTapped is { } handler
            && Views.Shell.BranchDragService.For(this) is not { PressFollowsDrag: true }
            && HitTest(e.GetPosition(this)) is { Kind: GraphHitKind.Label } hit)
        {
            handler(this, hit);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        _revealWhileLoading = -1;
        var display = DisplayRowAt(point.Position.Y);
        if (display >= 0)
        {
            // The press selects first; a branch label pressed may then be dragged (4g).
            PressRow(display, e.KeyModifiers);
            TryBeginDrag(point.Position, e);
        }
        else
        {
            _selectOnRelease = null;
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            ReleaseRow();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        // A drag took the pointer: the press on a multi-selection keeps it (4g).
        _selectOnRelease = null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointer = e.GetPosition(this);
        UpdateHover();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointer = null;
        UpdateHover();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _pointer = e.GetPosition(this);
        _revealWhileLoading = -1;
        ScrollTo(ScrollOffset - (e.Delta.Y * RowsPerWheelNotch * RowHeight));
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _revealWhileLoading = -1;
        var count = DisplayCount;
        if (count == 0)
        {
            return;
        }

        if (e.KeyModifiers == KeyModifiers.Shift && e.Key is Key.Down or Key.Up or Key.PageDown or Key.PageUp or Key.Home or Key.End)
        {
            // Shift extends from the anchor (4h); with nothing to extend from it moves as without.
            if (ExtendWithKey(e.Key))
            {
                e.Handled = true;
                return;
            }
        }
        else if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        var focusRow = FocusRow;
        var current = ShowsWorkingRow && IsWorkingRowSelected ? 0
            : focusRow >= 0 ? focusRow + RowOffset
            : -1;
        var hasSelection = current >= 0;
        int target;
        switch (e.Key)
        {
            case Key.Down:
                target = hasSelection ? current + 1 : 0;
                break;
            case Key.Up:
                target = hasSelection ? current - 1 : 0;
                break;
            case Key.PageDown:
                target = hasSelection ? current + RowsPerPage : 0;
                break;
            case Key.PageUp:
                target = hasSelection ? current - RowsPerPage : 0;
                break;
            case Key.Home:
                target = 0;
                break;
            case Key.End:
                target = count - 1;
                break;
            default:
                return;
        }

        target = Math.Clamp(target, 0, count - 1);
        SelectDisplayRow(target);
        ScrollDisplayRowIntoView(target);
        e.Handled = true;
    }

    /// <summary>Selects one drawn row alone: the working-changes row, or a commit's.</summary>
    private void SelectDisplayRow(int display)
    {
        _selectOnRelease = null;
        if (ShowsWorkingRow && display == 0)
        {
            // The working-changes row never joins a multi-selection (4h).
            if (!Selection.IsEmpty)
            {
                SetCurrentValue(SelectionProperty, GraphSelection.Empty);
            }

            SetCurrentValue(SelectedIndexProperty, -1);
            SetCurrentValue(IsWorkingRowSelectedProperty, true);
        }
        else if (Data is { } data && display - RowOffset is >= 0 and var row && row < data.Count)
        {
            ApplySelection(GraphSelection.Of(data.CommitAt(row).Sha));
        }
    }

    private double ClampOffset(double offset) => double.IsNaN(offset) ? 0 : Math.Clamp(offset, 0, MaxScrollOffset);

    private void ScrollTo(double offset) => SetCurrentValue(ScrollOffsetProperty, ClampOffset(offset));

    /// <summary>The drawn row at a height in the control (the working-changes row is row 0 when shown), or -1 below the last row.</summary>
    private int DisplayRowAt(double y)
    {
        var display = (int)Math.Floor((y + DrawnOffset) / RowHeight);
        return display >= 0 && display < DisplayCount ? display : -1;
    }

    /// <summary>
    /// Brings the viewport, the scroll range and the columns in line with the control's size and
    /// the data's rows and lanes. Runs on every arrange and whenever the data changes.
    /// </summary>
    private void UpdateExtent(Size size)
    {
        var data = Data;
        var offset = ScrollOffset;
        ViewportHeight = size.Height;
        MaxScrollOffset = Math.Max(0, (DisplayCount * RowHeight) - size.Height);
        if (ScrollOffset != offset)
        {
            // The clamped offset moved with the extent: redraw and move the hover with it.
            UpdateHover();
            InvalidateVisual();
        }

        var columns = GraphColumns.Compute(size.Width, data?.MaxLaneCount ?? 0);
        if (columns != Columns)
        {
            Columns = columns;
            ClearRowCache();
            InvalidateVisual();
        }

        if (_pendingReveal >= 0 && ViewportHeight > 0)
        {
            Reveal(_pendingReveal);
        }
    }

    private void SubscribeToData(CommitGraphData? data)
    {
        if (ReferenceEquals(data, _subscribedData))
        {
            return;
        }

        if (_subscribedData is not null)
        {
            _subscribedData.Changed -= OnDataChanged;
        }

        _subscribedData = data;
        if (data is not null)
        {
            data.Changed += OnDataChanged;
        }
    }

    /// <summary>Rows were appended while the history loads: rows already drawn do not change, only the extent and maybe the lane count.</summary>
    private void OnDataChanged(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, Data))
        {
            return;
        }

        UpdateExtent(Bounds.Size);
        InvalidateVisual();
        if (_revealWhileLoading >= 0)
        {
            Reveal(_revealWhileLoading);
        }

        UpdateHover();
    }

    private void OnThemeChanged()
    {
        _palette = null;
        ClearRowCache();
        InvalidateVisual();
    }

    /// <summary>Follows the row under the mouse, for its hover background and the labels' tooltip.</summary>
    private void UpdateHover()
    {
        var display = _pointer is { } pointer && pointer.X >= 0 && pointer.X < Bounds.Width ? DisplayRowAt(pointer.Y) : -1;
        if (display != _hoveredRow)
        {
            _hoveredRow = display;
            InvalidateVisual();
        }

        var row = display < 0 ? -1 : display - RowOffset;
        ToolTip.SetTip(this, display >= 0 && display < RowOffset ? WorkingRowTip() : LabelsTipAt(row) ?? CellTipAt(row));
    }

    /// <summary>
    /// The tooltip over the working-changes row's text when it is cut short with "…" ("Working
    /// changes · 1 conflict" in a narrow Message column): the whole text, as every cut text shows
    /// it. Null over the rest of the row and over a text shown whole.
    /// </summary>
    private string? WorkingRowTip() =>
        _pointer is { } pointer
        && Columns.Message.IsVisible
        && pointer.X >= Columns.Message.X
        && pointer.X < Columns.Message.Right
        && _workingRowLayout is { TextLines.Count: > 0 } layout
        && layout.TextLines[0].HasCollapsed
            ? WorkingRowText ?? DefaultWorkingRowText
            : null;

    /// <summary>
    /// The tooltip over a message or an author cut short with "…": the whole text, as everything
    /// shortened in the app shows it. Null over any other cell, and over text shown whole.
    /// </summary>
    private string? CellTipAt(int row)
    {
        var data = Data;
        if (row < 0 || data is null || _pointer is not { } pointer)
        {
            return null;
        }

        var visual = GetRowVisual(data, row);
        var commit = data.CommitAt(row);
        if (Columns.Message.IsVisible && pointer.X >= Columns.Message.X && pointer.X < Columns.Message.Right && IsCut(visual.Message))
        {
            return commit.Subject;
        }

        if (Columns.Author.IsVisible && pointer.X >= Columns.Author.X && pointer.X < Columns.Author.Right && IsCut(visual.Author))
        {
            return commit.AuthorName;
        }

        return null;

        static bool IsCut(TextLayout? layout) => layout is { TextLines.Count: > 0 } && layout.TextLines[0].HasCollapsed;
    }

    /// <summary>The tooltip over a row's labels: every label in full, one per line. Null when the mouse is not over labels.</summary>
    private string? LabelsTipAt(int row)
    {
        var data = Data;
        if (row < 0 || data is null || _pointer is not { } pointer || data.LabelsAt(row).Count == 0)
        {
            return null;
        }

        var visual = GetRowVisual(data, row);
        var left = Columns.Refs.X;
        var right = visual.Labels.Length > 0 ? visual.Labels[^1].Bounds.Right : left;
        if (pointer.X < left || pointer.X > right)
        {
            return null;
        }

        return string.Join("\n", data.LabelsAt(row).Select(label => label.Text));
    }

    /// <summary>
    /// Exposes the graph to UI Automation as one element with its accessible name, so the
    /// real-window pass can find it by its automation id (D49). The base control's peer hides
    /// a plain control from the control view.
    /// </summary>
    private sealed class CommitGraphAutomationPeer(CommitGraphControl owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>One ref label as the graph control drew it.</summary>
/// <param name="Text">What the label shows: the name, the name cut short and ending in "…", or "+N" for N hidden labels.</param>
/// <param name="FullText">The whole name; for a "+N" label the names it stands for, one per line.</param>
/// <param name="Kind">What the label stands for; for a "+N" label, the kind of the first label it hides.</param>
/// <param name="Bounds">Where it is drawn, in the control's coordinates.</param>
/// <param name="IsOverflow">The label is the "+N" label for the labels that did not fit.</param>
/// <param name="RefName">The full ref name of a branch, remote branch or tag label; null for HEAD, a stash and the "+N" label.</param>
public sealed record PlacedLabel(string Text, string FullText, RefLabelKind Kind, Rect Bounds, bool IsOverflow, string? RefName = null);

/// <summary>What a point of the graph is over (<see cref="CommitGraphControl.HitTest"/>).</summary>
public enum GraphHitKind
{
    /// <summary>Nothing: below the last row (the empty graph area, 4b) or outside the control.</summary>
    None,

    /// <summary>The working-changes row.</summary>
    WorkingRow,

    /// <summary>A commit's row, outside its labels.</summary>
    Commit,

    /// <summary>A ref label, or the "+N" label, of a commit's row.</summary>
    Label,
}

/// <summary>What a point of the graph is over (docs/phase-3-design.md section 8).</summary>
/// <param name="Kind">What kind of place.</param>
/// <param name="Row">The commit's row, or -1.</param>
/// <param name="Sha">The commit's id (a stash's for a stash row), or null.</param>
/// <param name="Label">The label as drawn, for <see cref="GraphHitKind.Label"/>; its <see cref="PlacedLabel.FullText"/> and <see cref="PlacedLabel.Kind"/> say what it stands for.</param>
public sealed record GraphHit(GraphHitKind Kind, int Row, string? Sha, PlacedLabel? Label)
{
    public static GraphHit None { get; } = new(GraphHitKind.None, -1, null, null);
}

/// <summary>A context menu asked for over the graph (<see cref="CommitGraphControl.ContextRequestedAt"/>).</summary>
public sealed class GraphContextRequestedEventArgs(GraphHit hit, Point position) : EventArgs
{
    /// <summary>What is under the point.</summary>
    public GraphHit Hit { get; } = hit;

    /// <summary>Where the menu opens, in the control's coordinates.</summary>
    public Point Position { get; } = position;

    /// <summary>Set by a handler that opened a menu: the request goes no further.</summary>
    public bool Handled { get; set; }
}
