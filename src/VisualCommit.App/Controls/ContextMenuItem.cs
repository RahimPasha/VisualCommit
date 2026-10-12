using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace VisualCommit.App.Controls;

/// <summary>
/// An item of the in-window context menu (docs/phase-3-design.md 4a, D105): a stock
/// <see cref="MenuItem"/>, styled as one, whose automation peer also offers the Invoke pattern.
/// The stock peer offers neither Invoke nor ExpandCollapse, so UI Automation (and the real-window
/// pass) could find an item but not use it.
/// </summary>
public sealed class ContextMenuItem : MenuItem
{
    protected override Type StyleKeyOverride => typeof(MenuItem);

    /// <summary>
    /// Does what UI Automation's Invoke does: closes the menu, then clicks the item, so that its
    /// action runs with the menu gone (a raised click alone ran the action and left the menu open,
    /// the spike of 4a found). A disabled item throws and runs nothing.
    /// </summary>
    /// <exception cref="InvalidOperationException">The item is disabled.</exception>
    public void Invoke()
    {
        if (!IsEffectivelyEnabled)
        {
            throw new InvalidOperationException($"The menu item \"{Header}\" is disabled.");
        }

        // The menu is the presenter the item lies in; closing it closes the flyout too.
        this.GetVisualAncestors().OfType<MenuBase>().FirstOrDefault()?.Close();
        RaiseEvent(new RoutedEventArgs(ClickEvent));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new InvokableMenuItemAutomationPeer(this);

    /// <summary>The stock menu item's peer, with the Invoke pattern added.</summary>
    private sealed class InvokableMenuItemAutomationPeer(ContextMenuItem owner) : MenuItemAutomationPeer(owner), IInvokeProvider
    {
        public void Invoke()
        {
            if (!IsEnabled())
            {
                throw new InvalidOperationException("The menu item is disabled.");
            }

            ((ContextMenuItem)Owner).Invoke();
        }
    }
}
