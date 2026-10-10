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
        AffectsRender<CommitGraphControl>(SelectedIndexProperty, ScrollOffsetProperty, IsWorkingRowSelectedProperty);
    }

    public CommitGraphControl()
    {
        AutomationProperties.SetAutomationId(this, "GraphRows");
        AutomationProperties.SetName(this, "Commit graph");
        ActualThemeVariantChanged += (_, _) => OnThemeChanged();
        ResourcesChanged += (_, _) => OnThemeChanged();
    }

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
            placed[i] = new PlacedLabel(label.Text, label.FullText, label.Kind, label.Bounds.Translate(new Vector(0, top)), label.IsOverflow);
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
            SelectDisplayRow(display);
        }

        e.Handled = true;
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
        if (count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        var current = ShowsWorkingRow && IsWorkingRowSelected ? 0
            : SelectedIndex >= 0 && SelectedIndex < RowCount ? SelectedIndex + RowOffset
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

    /// <summary>Selects a drawn row: the working-changes row, or a commit's.</summary>
    private void SelectDisplayRow(int display)
    {
        if (ShowsWorkingRow && display == 0)
        {
            SetCurrentValue(SelectedIndexProperty, -1);
            SetCurrentValue(IsWorkingRowSelectedProperty, true);
        }
        else
        {
            SetCurrentValue(IsWorkingRowSelectedProperty, false);
            SetCurrentValue(SelectedIndexProperty, display - RowOffset);
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
        ToolTip.SetTip(this, LabelsTipAt(row) ?? CellTipAt(row));
    }

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
public sealed record PlacedLabel(string Text, string FullText, RefLabelKind Kind, Rect Bounds, bool IsOverflow);
