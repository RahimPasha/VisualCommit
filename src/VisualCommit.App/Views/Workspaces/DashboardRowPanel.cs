using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace VisualCommit.App.Views.Workspaces;

/// <summary>
/// One line of the dashboard's columns (docs/phase-6-design.md 2.6, D206): the column headings and
/// every row lay their cells out with it, by <see cref="DashboardColumns.Compute"/> of their own
/// width, so the two always line up, as <c>GraphColumns</c> does for the graph (D49). A cell's
/// column is the attached <see cref="ColumnProperty"/>; the panel gives it its column less
/// <see cref="DashboardColumns.TextInset"/> on each side, and the cell aligns itself in that room
/// (left: its bounds are its text's). A cell in a hidden column (Folder below 1280 wide) is not shown.
/// </summary>
public sealed class DashboardRowPanel : Panel
{
    /// <summary>The column the cell lies in.</summary>
    public static readonly AttachedProperty<DashboardColumn> ColumnProperty =
        AvaloniaProperty.RegisterAttached<DashboardRowPanel, Control, DashboardColumn>("Column");

    static DashboardRowPanel()
    {
        AffectsParentMeasure<DashboardRowPanel>(ColumnProperty);
    }

    /// <summary>The columns of the last measure.</summary>
    public DashboardColumns Columns { get; private set; } = DashboardColumns.Empty;

    public static DashboardColumn GetColumn(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(ColumnProperty);
    }

    public static void SetColumn(Control control, DashboardColumn value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(ColumnProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? DashboardColumns.FixedWidth + DashboardColumns.MinOperation : availableSize.Width;
        Columns = DashboardColumns.Compute(width);
        var height = 0.0;
        foreach (var child in Children)
        {
            var span = Columns[GetColumn(child)];
            if (GetColumn(child) == DashboardColumn.Folder && child.IsVisible != span.IsVisible)
            {
                // Only the Folder column hides (below 1280 wide); its cells go with it.
                child.SetCurrentValue(IsVisibleProperty, span.IsVisible);
            }

            child.Measure(new Size(Room(span), availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(width, double.IsInfinity(availableSize.Height) ? height : Math.Min(height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Columns = DashboardColumns.Compute(finalSize.Width);
        foreach (var child in Children)
        {
            var span = Columns[GetColumn(child)];
            child.Arrange(new Rect(span.X + DashboardColumns.TextInset, 0, Room(span), finalSize.Height));
        }

        return finalSize;
    }

    /// <summary>A cell's room: its column less the inset on each side.</summary>
    private static double Room(DashboardColumnSpan span) =>
        span.IsVisible ? Math.Max(0, span.Width - (2 * DashboardColumns.TextInset)) : 0;
}

/// <summary>
/// A dashboard cell that UI Automation reads as a text element (docs/phase-6-design.md 15.5): its
/// automation id names the cell (<c>DashboardRowName</c>, …), its name is what it says (the Ahead
/// / behind cell's <c>{a} ahead, {n} behind</c>), and it carries the cell's tooltip, so that the text
/// block inside it, cut with "…", is no target of the pointer of its own.
/// </summary>
public sealed class DashboardCell : Border
{
    protected override Type StyleKeyOverride => typeof(Border);

    protected override AutomationPeer OnCreateAutomationPeer() => new CellAutomationPeer(this);

    private sealed class CellAutomationPeer(Control owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>The parts of the dashboard's header (2.5), for <see cref="DashboardHeaderPanel"/>.</summary>
public enum DashboardHeaderPart
{
    /// <summary><c>IconWorkspace</c>, 14×14, 6 before the title.</summary>
    Icon,

    /// <summary><c>DashboardTitle</c>: at most 240 wide, cut with "…".</summary>
    Title,

    /// <summary><c>DashboardCount</c>, 10 after the title.</summary>
    Count,

    /// <summary>The run's text, bar and <c>Cancel all</c>, right-aligned 16 before the buttons.</summary>
    Run,

    /// <summary>The buttons, from <c>Add repository…</c> to <c>DashboardMore</c>, at the right edge.</summary>
    Buttons,
}

/// <summary>
/// The dashboard's header line (docs/phase-6-design.md 2.5): left the workspace's icon, its name
/// (at most 240) and its count; right the buttons; between them, right-aligned 16 before the
/// buttons, the run's part. The header never wraps; the run's text keeps at least 120 while it is
/// cut, and in a narrower window the title gives way below its 240 first.
/// </summary>
public sealed class DashboardHeaderPanel : Panel
{
    /// <summary>The title's widest (2.5).</summary>
    public const double MaxTitleWidth = 240;

    /// <summary>What the run's text keeps at the least (2.5).</summary>
    public const double MinRunTextWidth = 120;

    /// <summary>The part a child is.</summary>
    public static readonly AttachedProperty<DashboardHeaderPart> PartProperty =
        AvaloniaProperty.RegisterAttached<DashboardHeaderPanel, Control, DashboardHeaderPart>("Part");

    private const double IconGap = 6;
    private const double CountGap = 10;
    private const double RunGap = 16;

    static DashboardHeaderPanel()
    {
        AffectsParentMeasure<DashboardHeaderPanel>(PartProperty);
    }

    /// <summary>The run's text inside the run part: its width counts toward the 120 it keeps.</summary>
    public static readonly StyledProperty<Control?> RunTextProperty =
        AvaloniaProperty.Register<DashboardHeaderPanel, Control?>(nameof(RunText));

    /// <summary>The run's text inside the run part (<c>DashboardBulkText</c>).</summary>
    public Control? RunText
    {
        get => GetValue(RunTextProperty);
        set => SetValue(RunTextProperty, value);
    }

    public static DashboardHeaderPart GetPart(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(PartProperty);
    }

    public static void SetPart(Control control, DashboardHeaderPart value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(PartProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var widths = LayOut(availableSize, measure: true);
        var height = Children.Where(child => child.IsVisible).Select(child => child.DesiredSize.Height).DefaultIfEmpty(0).Max();
        return new Size(double.IsInfinity(availableSize.Width) ? widths.Total : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var widths = LayOut(finalSize, measure: true);
        var x = 0.0;
        Place(DashboardHeaderPart.Icon, ref x, widths.Icon, IconGap);
        Place(DashboardHeaderPart.Title, ref x, widths.Title, CountGap);
        Place(DashboardHeaderPart.Count, ref x, widths.Count, 0);
        var buttonsX = finalSize.Width - widths.Buttons;
        Arrange(DashboardHeaderPart.Buttons, buttonsX, widths.Buttons);
        var runRight = buttonsX - RunGap;
        Arrange(DashboardHeaderPart.Run, Math.Max(x, runRight - widths.Run), widths.Run);
        return finalSize;

        void Place(DashboardHeaderPart part, ref double left, double width, double gap)
        {
            Arrange(part, left, width);
            left += width + (width > 0 ? gap : 0);
        }

        void Arrange(DashboardHeaderPart part, double left, double width)
        {
            foreach (var child in Children.Where(child => GetPart(child) == part))
            {
                child.Arrange(new Rect(left, 0, Math.Max(0, width), finalSize.Height));
            }
        }
    }

    /// <summary>The parts' widths for <paramref name="size"/>: everything at its natural width while it fits; else the run part is cut to what keeps its text 120, then the title gives way.</summary>
    private (double Icon, double Title, double Count, double Run, double Buttons, double Total) LayOut(Size size, bool measure)
    {
        var height = size.Height;
        double Natural(DashboardHeaderPart part, double width = double.PositiveInfinity)
        {
            var natural = 0.0;
            foreach (var child in Children.Where(child => GetPart(child) == part && child.IsVisible))
            {
                if (measure)
                {
                    child.Measure(new Size(width, height));
                }

                natural = Math.Max(natural, child.DesiredSize.Width);
            }

            return natural;
        }

        var icon = Natural(DashboardHeaderPart.Icon);
        var count = Natural(DashboardHeaderPart.Count);
        var buttons = Natural(DashboardHeaderPart.Buttons);
        var title = Math.Min(Natural(DashboardHeaderPart.Title), MaxTitleWidth);
        var run = Natural(DashboardHeaderPart.Run);
        var runText = RunText is { IsVisible: true } text ? text.DesiredSize.Width : 0;
        var left = icon + (icon > 0 ? IconGap : 0) + title + (title > 0 ? CountGap : 0) + count;
        var runRoom = run > 0 ? run + RunGap : 0;
        var total = left + runRoom + buttons;
        if (double.IsInfinity(size.Width) || total <= size.Width)
        {
            if (measure)
            {
                Natural(DashboardHeaderPart.Title, title);
            }

            return (icon, title, count, run, buttons, total);
        }

        // The run part keeps its fixed parts and at least 120 of its text; the title gives way.
        var runMinimum = run > 0 ? run - runText + Math.Min(runText, MinRunTextWidth) : 0;
        var available = size.Width - buttons - (run > 0 ? RunGap : 0);
        var runWidth = Math.Max(runMinimum, Math.Min(run, available - left));
        var titleWidth = Math.Max(0, Math.Min(title, available - runWidth - (left - title)));
        if (measure)
        {
            Natural(DashboardHeaderPart.Title, titleWidth);
            Natural(DashboardHeaderPart.Run, runWidth);
        }

        return (icon, titleWidth, count, runWidth, buttons, size.Width);
    }
}
