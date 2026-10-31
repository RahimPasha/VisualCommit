using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Where the pointer rests, for phase 3's checks (docs/test-reports/phase-3.md, "Resting the
/// pointer" and "The pointer before a capture"): the neutral point both harnesses move to before a
/// capture, and resting the pointer on a control or a menu item until its tooltip is open. Also
/// the control under a point once the window has drawn it, for a test that hit-tests on its own.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>
    /// The neutral point (<see cref="WindowLayout.NeutralX"/>, <see cref="WindowLayout.NeutralY"/>):
    /// where the pointer rests before a capture that is not about a hover and has no menu open,
    /// in both harnesses (<c>RealApp.NeutralPoint</c> is the same point). Nothing under it has a
    /// tooltip or a hover background.
    /// </summary>
    public static Point NeutralPoint { get; } = new(WindowLayout.NeutralX, WindowLayout.NeutralY);

    /// <summary>
    /// Moves the pointer to <see cref="NeutralPoint"/> in one move, so that it crosses nothing on
    /// its way. Not while a menu is open: the move could cross its items and move the highlight
    /// (D105); a capture with a menu open leaves the pointer where it opened the menu.
    /// </summary>
    /// <exception cref="InvalidOperationException">A context menu is open.</exception>
    public void MoveToNeutral()
    {
        if (IsMenuOpen)
        {
            throw new InvalidOperationException("A context menu is open: moving the pointer to the neutral point could cross its items. Leave the pointer where it opened the menu.");
        }

        MoveMouse(NeutralPoint);
    }

    /// <summary>
    /// The control whose tooltip opens while the pointer is at <paramref name="position"/>: the
    /// control under the pointer or the nearest of its ancestors that has a tooltip, disabled
    /// ones included when they show their tooltip while disabled (<c>ToolTip.ShowOnDisabled</c>).
    /// Null when nothing there has a tooltip.
    /// </summary>
    public Control? ToolTipOwnerAt(Point position)
    {
        Settle();
        var hit = Window.InputHitTest(position, enabledElementsOnly: false) as Visual;
        for (var visual = hit; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Control control
                && ToolTip.GetTip(control) is not null
                && (control.IsEffectivelyEnabled || ToolTip.GetShowOnDisabled(control)))
            {
                return control;
            }
        }

        return null;
    }

    /// <summary>
    /// Waits until a hit test at <paramref name="position"/> finds a control, polled within
    /// <paramref name="timeout"/> (<see cref="DefaultWaitTimeout"/> when null), never for a fixed
    /// time, and returns it: the control that input at that point of the window reaches. Hit
    /// testing reads what the renderer last drew, and before the window's first frame it finds
    /// nothing; <see cref="WaitUntilReadyAsync"/> renders no frame, and on a slow machine none has
    /// come by then (the wheel test failed so in a bare Ubuntu container).
    /// </summary>
    /// <exception cref="TimeoutException">No control was at the position in time.</exception>
    public async Task<Control> WaitForControlAtAsync(Point position, TimeSpan? timeout = null)
    {
        Control? found = null;
        await WaitForAsync(
            () => (found = Window.InputHitTest(position) as Control) is not null,
            $"a control at {position}: the window's first frame",
            timeout);
        return found!;
    }

    /// <summary>
    /// Waits until <paramref name="control"/>'s tooltip is open (<c>ToolTip.GetIsOpen</c>), polled
    /// within <paramref name="timeout"/> (<see cref="DefaultWaitTimeout"/> when null), never for a
    /// fixed time, then lets the window settle once more, and returns the tooltip's text: a text
    /// tip as it is, otherwise the texts of the tooltip's content joined by line feeds. The pointer
    /// must already rest on the control (<see cref="RestPointerOnAsync(Visual)"/> moves it there).
    /// </summary>
    /// <exception cref="TimeoutException">The tooltip did not open in time.</exception>
    public async Task<string> WaitForToolTipAsync(Control control, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        await WaitForAsync(() => ToolTip.GetIsOpen(control), $"the tooltip of the {control.GetType().Name} \"{control.Name}\" to open", timeout);

        // The theme fades a tooltip in after it opens (an opacity transition): a picture taken
        // while it does shows the tooltip half transparent with the text below showing through
        // (seen in check 9's 09b). Wait for the fade to end, a condition and not a fixed time.
        await WaitForAsync(
            () => this.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible).All(tip => tip.Opacity >= 1),
            $"the tooltip of the {control.GetType().Name} \"{control.Name}\" to fade in completely",
            timeout);
        Settle();
        return TipText(ToolTip.GetTip(control));
    }

    /// <summary>
    /// Rests the pointer on <paramref name="target"/> as the report's conventions say: one move
    /// to its centre (crossing nothing), then a wait until the tooltip that opens there is open,
    /// then one more settle. The tooltip is that of the control under the pointer or the nearest
    /// ancestor with one (<see cref="ToolTipOwnerAt"/>): the banner's text is not hit-testable,
    /// and the tooltip it shows is the banner's own. Returns the tooltip's text.
    /// </summary>
    /// <exception cref="InvalidOperationException">Nothing at the target's centre has a tooltip.</exception>
    public Task<string> RestPointerOnAsync(Visual target, TimeSpan? timeout = null) =>
        RestPointerAtAsync(BoundsOf(target).Center, timeout);

    /// <summary>Rests the pointer at a window position, as <see cref="RestPointerOnAsync(Visual, TimeSpan?)"/> does at a control's centre.</summary>
    public async Task<string> RestPointerAtAsync(Point position, TimeSpan? timeout = null)
    {
        MoveMouse(position);
        var owner = ToolTipOwnerAt(position)
            ?? throw new InvalidOperationException($"Nothing at {position} has a tooltip.");
        return await WaitForToolTipAsync(owner, timeout);
    }

    /// <summary>
    /// Rests the pointer on the open menu's item labelled <paramref name="label"/>: one move to the
    /// item's centre, so that it crosses no other item (an enabled item it crossed would take the
    /// highlight, D105), then a wait until the item's tooltip is open. Returns the tooltip's text
    /// (a disabled item's reason).
    /// </summary>
    public async Task<string> RestPointerOnMenuItemAsync(string label, TimeSpan? timeout = null)
    {
        var item = MenuItemControl(label);
        MoveMouse(BoundsOf(item).Center);
        return await WaitForToolTipAsync(item, timeout);
    }

    /// <summary>The text of a tooltip's content: a string as it is, a control's texts in order.</summary>
    private static string TipText(object? tip) => tip switch
    {
        null => string.Empty,
        string text => text,
        ToolTip { Content: string text } => text,
        ToolTip { Content: Visual content } => TextsOf(content),
        Visual visual => TextsOf(visual),
        _ => tip.ToString() ?? string.Empty,
    };

    private static string TextsOf(Visual visual) =>
        string.Join('\n', (visual is TextBlock self ? [self] : visual.GetVisualDescendants().OfType<TextBlock>())
            .Select(text => text.Text)
            .Where(text => !string.IsNullOrEmpty(text)));
}
