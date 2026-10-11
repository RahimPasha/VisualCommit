using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// A row of a dialog's list that holds buttons of its own, such as the Remotes dialog's
/// <c>RemoteRow</c> with its Edit and Remove. A plain panel is not a UI Automation element (design
/// section 0); this one is a list item named by its <c>AutomationProperties.Name</c>, with its
/// buttons under it, so the real-window pass can find a row by name and then its buttons.
/// </summary>
public sealed class DialogRow : Border
{
    protected override AutomationPeer OnCreateAutomationPeer() => new DialogRowAutomationPeer(this);

    private sealed class DialogRowAutomationPeer(DialogRow owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
