using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace VisualCommit.App.Views.Shell;

/// <summary>The conflict banner above the graph (docs/phase-3-design.md section 5, docs/phase-4-design.md 5.2); its view model is <see cref="ViewModels.Operations.ConflictBannerViewModel"/>.</summary>
public partial class ConflictBanner : UserControl
{
    public ConflictBanner()
    {
        InitializeComponent();
    }

    /// <summary>
    /// A control element of its own in UI Automation, so that the real-window pass can wait for
    /// <c>ConflictBanner</c> by id, as it waits for the dialogs (4f).
    /// </summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new ConflictBannerAutomationPeer(this);

    private sealed class ConflictBannerAutomationPeer(ConflictBanner owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// The banner's layout (docs/phase-4-design.md 5.2, amending D122): its first child holds the icon
/// and the text, its second the buttons. The buttons stand right of the text, the last one 8 from
/// the right edge, while the text itself keeps at least <see cref="MinimumTextWidth"/>; otherwise
/// they go on a row of their own under the text, right-aligned, 8 below the text and 8 above the
/// banner's bottom. A banner whose text fits on one line beside its buttons is 36 high: the text
/// block's own margins (9 above and below) make it so.
/// </summary>
public sealed class BannerLayoutPanel : Panel
{
    /// <summary>The width the text keeps before the buttons go under it (5.2).</summary>
    public const double MinimumTextWidth = 240;

    /// <summary>The icon's column: 12 before it, 16 for it, 8 after it.</summary>
    public const double IconColumn = 36;

    /// <summary>The text's margin on its right.</summary>
    public const double TextRightMargin = 8;

    /// <summary>The text's margin above and below it.</summary>
    public const double TextVerticalMargin = 9;

    /// <summary>The last button's distance from the right edge, and the gap above and below a row of buttons.</summary>
    public const double ButtonsMargin = 8;

    /// <summary>Defines <see cref="ButtonsBelow"/>.</summary>
    public static readonly DirectProperty<BannerLayoutPanel, bool> ButtonsBelowProperty =
        AvaloniaProperty.RegisterDirect<BannerLayoutPanel, bool>(nameof(ButtonsBelow), panel => panel.ButtonsBelow);

    private bool _buttonsBelow;

    /// <summary>The buttons stand on a row of their own under the text (the last measure decided it).</summary>
    public bool ButtonsBelow
    {
        get => _buttonsBelow;
        private set => SetAndRaise(ButtonsBelowProperty, ref _buttonsBelow, value);
    }

    /// <summary>Whether a banner <paramref name="width"/> wide puts buttons <paramref name="buttonsWidth"/> wide (0 for none) under its text.</summary>
    public static bool PutsButtonsBelow(double width, double buttonsWidth) =>
        buttonsWidth > 0 && !double.IsInfinity(width) && width - IconColumn - TextRightMargin - buttonsWidth - ButtonsMargin < MinimumTextWidth;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return base.MeasureOverride(availableSize);
        }

        var (info, buttons) = (Children[0], Children[1]);
        buttons.Measure(Size.Infinity);
        var buttonsWidth = ButtonsWidth(buttons);
        var below = PutsButtonsBelow(availableSize.Width, buttonsWidth);
        ButtonsBelow = below;
        if (below)
        {
            info.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            var height = info.DesiredSize.Height - TextVerticalMargin + ButtonsMargin + buttons.DesiredSize.Height + ButtonsMargin;
            return new Size(Math.Max(info.DesiredSize.Width, buttonsWidth + ButtonsMargin), height);
        }

        var room = buttonsWidth > 0 ? buttonsWidth + ButtonsMargin : 0;
        info.Measure(new Size(Math.Max(0, availableSize.Width - room), double.PositiveInfinity));
        return new Size(info.DesiredSize.Width + room, Math.Max(info.DesiredSize.Height, buttons.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return base.ArrangeOverride(finalSize);
        }

        var (info, buttons) = (Children[0], Children[1]);
        var buttonsWidth = ButtonsWidth(buttons);
        var buttonsHeight = buttons.DesiredSize.Height;
        if (ButtonsBelow)
        {
            var infoHeight = info.DesiredSize.Height;
            info.Arrange(new Rect(0, 0, finalSize.Width, infoHeight));
            var top = infoHeight - TextVerticalMargin + ButtonsMargin;
            buttons.Arrange(new Rect(finalSize.Width - ButtonsMargin - buttonsWidth, top, buttonsWidth, buttonsHeight));
            return finalSize;
        }

        var room = buttonsWidth > 0 ? buttonsWidth + ButtonsMargin : 0;
        info.Arrange(new Rect(0, 0, Math.Max(0, finalSize.Width - room), finalSize.Height));
        buttons.Arrange(new Rect(finalSize.Width - room, (finalSize.Height - buttonsHeight) / 2, buttonsWidth, buttonsHeight));
        return finalSize;
    }

    private static double ButtonsWidth(Control buttons) => buttons.IsVisible ? buttons.DesiredSize.Width : 0;
}
