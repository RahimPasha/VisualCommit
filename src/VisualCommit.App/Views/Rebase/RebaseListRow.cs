using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace VisualCommit.App.Views.Rebase;

/// <summary>
/// A row of the interactive rebase's list (docs/phase-4-design.md 2.2, 7.4): a commit row
/// (<c>RebaseRow</c>, 30 high, which takes the keyboard focus) or the base row
/// (<c>RebaseBaseRow</c>, which does not). A panel whose UI Automation peer is a list item named
/// by <see cref="Avalonia.Automation.AutomationProperties.NameProperty"/>, so that the real-window
/// pass finds each row by its automation id and its name (<c>{short} {subject}</c>). It draws no
/// focus frame: the selected row, which has the focus, shows the selection background.
/// </summary>
public sealed class RebaseListRow : Panel
{
    static RebaseListRow()
    {
        FocusableProperty.OverrideDefaultValue<RebaseListRow>(true);
        FocusAdornerProperty.OverrideDefaultValue<RebaseListRow>(null);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowAutomationPeer(this);

    /// <summary>A row as UI Automation sees it: a list item, a control element, named by its automation name.</summary>
    private sealed class RowAutomationPeer(RebaseListRow owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// The root of <see cref="InteractiveRebaseView"/> as UI Automation sees it
/// (<c>InteractiveRebaseView</c>, 7.4): a pane, a control element, so that the real-window pass can
/// wait for it as it waits for a dialog.
/// </summary>
internal sealed class InteractiveRebaseAutomationPeer(Control owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
