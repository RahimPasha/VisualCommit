using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// The tab strip's panel (docs/phase-6-design.md 13.3, D228): keeps phase 1's layout while the
/// tabs fit at their natural widths, then shrinks the widest first, then scrolls them, the active
/// tab brought whole into view. <c>RepoTabsView</c>'s <c>TabList</c> lays its tabs out with it.
/// <list type="number">
/// <item>Each tab measured with an infinite width has its natural width <c>n = max(120, desired)</c>,
/// today's (a tab is <c>clamp(51 + title, 120, 220)</c>, a dashboard's <c>clamp(69 + title, 120, 220)</c>).</item>
/// <item>The room <c>A</c> is the strip's width less <see cref="Reserve"/> ("+" and its margins).
/// When <c>Σ n ≤ A</c> every tab keeps its natural width.</item>
/// <item>Otherwise, while <c>80 × count ≤ A</c>, the widest shrink first: the cap <c>c</c> is the
/// largest width with <c>Σ min(n, c) = A</c>, each tab <c>min(n, ⌊c⌋)</c>, the pixels left over
/// one each to the leftmost tabs at the cap, so that the tabs fill <c>A</c> and "+" sits at its end.</item>
/// <item>Otherwise every tab is 80 wide and the panel scrolls by <see cref="Offset"/>, clipped.</item>
/// </list>
/// When the active tab, the number of tabs or the room changes, <see cref="Offset"/> moves as
/// little as brings the active tab whole into view; a wheel notch over the strip moves it by 80
/// while the tabs overflow; a tab whose button takes the focus (or asks to be brought into view)
/// is brought into view.
/// </summary>
public sealed class RepoTabStripPanel : Panel
{
    /// <summary>The narrowest a tab's natural width is (phase 1's tab).</summary>
    public const double NaturalFloor = 120;

    /// <summary>The narrowest a tab is shrunk to; below it, the tabs scroll.</summary>
    public const double ShrunkFloor = 80;

    /// <summary>How far one wheel notch moves the tabs.</summary>
    public const double WheelStep = 80;

    /// <summary>The width kept free at the strip's right for "+" (34 wide with 2 either side: 38).</summary>
    public static readonly StyledProperty<double> ReserveProperty =
        AvaloniaProperty.Register<RepoTabStripPanel, double>(nameof(Reserve));

    /// <summary>The active tab's index (<c>MainWindowViewModel.ActiveTabIndex</c>), or −1 for none.</summary>
    public static readonly StyledProperty<int> ActiveIndexProperty =
        AvaloniaProperty.Register<RepoTabStripPanel, int>(nameof(ActiveIndex), -1);

    private readonly List<double> _widths = [];
    private double _room = double.PositiveInfinity;
    private (int Active, int Count, double Room) _lastSeen = (-2, -1, double.NaN);

    static RepoTabStripPanel()
    {
        AffectsMeasure<RepoTabStripPanel>(ReserveProperty, ActiveIndexProperty);
    }

    public RepoTabStripPanel()
    {
        AddHandler(RequestBringIntoViewEvent, OnRequestBringIntoView);
        AddHandler(GotFocusEvent, OnGotFocus);
    }

    public double Reserve
    {
        get => GetValue(ReserveProperty);
        set => SetValue(ReserveProperty, value);
    }

    public int ActiveIndex
    {
        get => GetValue(ActiveIndexProperty);
        set => SetValue(ActiveIndexProperty, value);
    }

    /// <summary>How far the tabs are scrolled to the left: 0 unless they overflow at 80 each, then from 0 to <c>Σ t − A</c>.</summary>
    public double Offset { get; private set; }

    /// <summary>The width each tab was given at the last measure, in tab order.</summary>
    public IReadOnlyList<double> TabWidths => _widths;

    /// <summary>The tabs overflow and scroll (step 4).</summary>
    public bool IsScrolling { get; private set; }

    /// <summary>
    /// The widths of tabs whose natural widths are <paramref name="natural"/> in <paramref name="room"/>,
    /// and whether they scroll: 13.3's steps 2 to 4, pure, for the panel and its tests.
    /// </summary>
    public static (IReadOnlyList<double> Widths, bool Scrolls) Fit(IReadOnlyList<double> natural, double room)
    {
        ArgumentNullException.ThrowIfNull(natural);
        var count = natural.Count;
        if (count == 0 || double.IsInfinity(room) || natural.Sum() <= room)
        {
            return ([.. natural], false);
        }

        if (ShrunkFloor * count > room)
        {
            return ([.. Enumerable.Repeat(ShrunkFloor, count)], true);
        }

        // The cap c: the largest width with Σ min(n, c) = room. With the widths in ascending order,
        // the first k tabs keep their natural widths and the others share what is left.
        var sorted = natural.OrderBy(width => width).ToList();
        var cap = room / count;
        var kept = 0.0;
        for (var k = 0; k < count; k++)
        {
            var candidate = (room - kept) / (count - k);
            if (candidate <= sorted[k])
            {
                cap = candidate;
                break;
            }

            kept += sorted[k];
        }

        var floor = Math.Floor(cap);
        var widths = natural.Select(width => Math.Min(width, floor)).ToList();
        var left = (int)Math.Floor(room - widths.Sum() + 1e-9);
        for (var i = 0; i < count && left > 0; i++)
        {
            // A tab at the cap: one more pixel stays within its natural width.
            if (natural[i] >= widths[i] + 1)
            {
                widths[i]++;
                left--;
            }
        }

        return (widths, false);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = Children;
        var natural = new List<double>(children.Count);
        var height = 0.0;
        foreach (var child in children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            natural.Add(Math.Max(NaturalFloor, child.DesiredSize.Width));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        _room = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, availableSize.Width - Reserve);
        var (widths, scrolls) = Fit(natural, _room);
        _widths.Clear();
        _widths.AddRange(widths);
        IsScrolling = scrolls;
        for (var i = 0; i < children.Count; i++)
        {
            children[i].Measure(new Size(_widths[i], availableSize.Height));
            height = Math.Max(height, children[i].DesiredSize.Height);
        }

        UpdateOffset();
        var total = _widths.Sum();
        return new Size(scrolls ? _room : total, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children;
        var x = 0.0;
        for (var i = 0; i < children.Count; i++)
        {
            var width = i < _widths.Count ? _widths[i] : children[i].DesiredSize.Width;
            children[i].Arrange(new Rect(x - Offset, 0, width, finalSize.Height));
            x += width;
        }

        return finalSize;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Handled || !IsScrolling)
        {
            return;
        }

        // Down or right shows tabs further right: Avalonia's deltas are positive up and left.
        var notches = Math.Abs(e.Delta.Y) >= Math.Abs(e.Delta.X) ? -e.Delta.Y : -e.Delta.X;
        if (notches == 0)
        {
            return;
        }

        ScrollTo(Offset + (notches * WheelStep));
        e.Handled = true;
    }

    /// <summary>The offset that brings the tab at <paramref name="index"/> whole into view with the least move.</summary>
    private double OffsetShowing(int index, double offset)
    {
        if (index < 0 || index >= _widths.Count)
        {
            return offset;
        }

        var left = _widths.Take(index).Sum();
        var right = left + _widths[index];
        if (left < offset)
        {
            return left;
        }

        return right > offset + _room ? right - _room : offset;
    }

    /// <summary>Clamps and applies a new offset, and arranges the tabs again when it moved.</summary>
    private void ScrollTo(double offset)
    {
        var clamped = Clamp(offset);
        if (clamped != Offset)
        {
            Offset = clamped;
            InvalidateArrange();
        }
    }

    private double Clamp(double offset) =>
        IsScrolling ? Math.Clamp(offset, 0, Math.Max(0, _widths.Sum() - _room)) : 0;

    /// <summary>After a measure: the active tab brought into view when it, the count or the room changed; otherwise the offset only clamped.</summary>
    private void UpdateOffset()
    {
        var seen = (ActiveIndex, Children.Count, _room);
        var offset = seen != _lastSeen ? OffsetShowing(ActiveIndex, Offset) : Offset;
        _lastSeen = seen;
        Offset = Clamp(offset);
    }

    private void OnRequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
    {
        if (TabOf(e.TargetObject) is { } index)
        {
            ScrollTo(OffsetShowing(index, Offset));
            e.Handled = true;
        }
    }

    private void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (TabOf(e.Source as Visual) is { } index)
        {
            ScrollTo(OffsetShowing(index, Offset));
        }
    }

    /// <summary>The index of the tab <paramref name="visual"/> lies in, or null.</summary>
    private int? TabOf(Visual? visual)
    {
        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (current is Control control && ReferenceEquals(control.GetVisualParent(), this))
            {
                var index = Children.IndexOf(control);
                return index >= 0 ? index : null;
            }
        }

        return null;
    }
}
