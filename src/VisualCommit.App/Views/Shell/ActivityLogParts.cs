using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// A row of the activity log's list (docs/phase-5-design.md 2.2, 13.4): an operation
/// (<c>ActivityLogOperation</c>, named by its title), a call (<c>ActivityLogCall</c>, named by its
/// command), a line under an operation, or a call's output box. A panel that takes the keyboard
/// focus, whose UI Automation peer is a list item named by its automation name, so that the
/// real-window pass finds each row by its automation id and its name.
/// </summary>
public sealed class ActivityLogRowPanel : Panel
{
    static ActivityLogRowPanel()
    {
        FocusableProperty.OverrideDefaultValue<ActivityLogRowPanel>(true);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowAutomationPeer(this);

    /// <summary>A row as UI Automation sees it: a list item, a control element, named by its automation name.</summary>
    private sealed class RowAutomationPeer(ActivityLogRowPanel owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// Lays out two texts side by side in the width they are given (2.2): an operation's title and
/// message, or a call's command and its <c>(on a temporary index)</c>. When both fit, each takes
/// its own width, the second right after the first. When they do not, the second keeps its own
/// width (<see cref="SecondKeepsItsWidth"/>) or is cut as the first is, each down to an even share
/// at least; a <c>TrimmedTextBlock</c> then shows the whole text in its tooltip.
/// </summary>
public sealed class ActivityLogTextsPanel : Panel
{
    public static readonly StyledProperty<bool> SecondKeepsItsWidthProperty =
        AvaloniaProperty.Register<ActivityLogTextsPanel, bool>(nameof(SecondKeepsItsWidth));

    /// <summary>The widths the two texts want, measured without a limit at the last measure.</summary>
    private (double First, double Second) _wanted;

    static ActivityLogTextsPanel()
    {
        AffectsMeasure<ActivityLogTextsPanel>(SecondKeepsItsWidthProperty);
    }

    /// <summary>The second text is never cut: the first gives way alone.</summary>
    public bool SecondKeepsItsWidth
    {
        get => GetValue(SecondKeepsItsWidthProperty);
        set => SetValue(SecondKeepsItsWidthProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (first, second) = Pair();
        var infinite = new Size(double.PositiveInfinity, availableSize.Height);
        first?.Measure(infinite);
        second?.Measure(infinite);
        _wanted = (first?.DesiredSize.Width ?? 0, second?.DesiredSize.Width ?? 0);
        var (firstWidth, secondWidth) = Widths(availableSize.Width);
        first?.Measure(new Size(firstWidth, availableSize.Height));
        second?.Measure(new Size(secondWidth, availableSize.Height));
        var height = Math.Max(first?.DesiredSize.Height ?? 0, second?.DesiredSize.Height ?? 0);
        return new Size(firstWidth + secondWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (first, second) = Pair();
        var (firstWidth, secondWidth) = Widths(finalSize.Width);
        first?.Arrange(new Rect(0, 0, firstWidth, finalSize.Height));
        second?.Arrange(new Rect(firstWidth, 0, secondWidth, finalSize.Height));
        return finalSize;
    }

    private (Control? First, Control? Second) Pair()
    {
        var visible = Children.Where(child => child.IsVisible).ToList();
        return (visible.ElementAtOrDefault(0), visible.ElementAtOrDefault(1));
    }

    /// <summary>The widths the two texts get out of <paramref name="available"/>, from what they want.</summary>
    private (double First, double Second) Widths(double available)
    {
        var (wantFirst, wantSecond) = _wanted;
        if (double.IsInfinity(available) || wantFirst + wantSecond <= available)
        {
            return (wantFirst, wantSecond);
        }

        if (SecondKeepsItsWidth)
        {
            var kept = Math.Min(wantSecond, available);
            return (Math.Max(0, available - kept), kept);
        }

        // The shorter one keeps its width when it takes no more than half; otherwise both get half.
        var half = available / 2;
        if (wantFirst <= half)
        {
            return (wantFirst, available - wantFirst);
        }

        if (wantSecond <= half)
        {
            return (available - wantSecond, wantSecond);
        }

        return (half, available - half);
    }
}

/// <summary>
/// The box under an opened call (<c>ActivityLogOutput</c>, 2.2): its parts drawn by the view, and
/// its whole text (<see cref="Text"/>) as UI Automation's read-only value, which the real-window
/// pass reads through the Value pattern.
/// </summary>
public sealed class ActivityLogOutputBox : Border
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ActivityLogOutputBox, string?>(nameof(Text));

    /// <summary>What the box holds, each part under its title, or <c>No output</c>.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new OutputAutomationPeer(this);

    /// <summary>The box as UI Automation sees it: a document whose read-only value is its text.</summary>
    private sealed class OutputAutomationPeer(ActivityLogOutputBox owner) : ControlAutomationPeer(owner), IValueProvider
    {
        public bool IsReadOnly => true;

        public string? Value => owner.Text;

        public void SetValue(string? value) => throw new InvalidOperationException("The output box is read-only.");

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
