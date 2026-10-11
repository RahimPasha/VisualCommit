using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace VisualCommit.App.Views.Shell;

/// <summary>The conflict banner above the graph (docs/phase-3-design.md section 5); its view model is <see cref="ViewModels.Operations.ConflictBannerViewModel"/>.</summary>
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
