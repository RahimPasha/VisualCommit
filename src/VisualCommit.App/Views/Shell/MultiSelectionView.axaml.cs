using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// The details panel while several commits are selected (docs/phase-3-design.md 4h). Its root is
/// a UI Automation control element (a pane named by its title), so the real-window pass can find
/// it by its automation id, as it finds the dialogs.
/// </summary>
public partial class MultiSelectionView : UserControl
{
    public MultiSelectionView()
    {
        InitializeComponent();

        // The list takes what the title and the hint leave, and scrolls past that.
        Layout.SizeChanged += (_, _) => FitList();
        MultiSelectionHint.SizeChanged += (_, _) => FitList();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MultiSelectionAutomationPeer(this);

    private void FitList()
    {
        var hint = MultiSelectionHint.Bounds.Height + MultiSelectionHint.Margin.Top + MultiSelectionHint.Margin.Bottom;
        var room = Layout.Bounds.Height - Layout.RowDefinitions[0].ActualHeight - MultiSelectionList.Margin.Top - hint;
        MultiSelectionList.MaxHeight = Math.Max(0, room);
    }

    private sealed class MultiSelectionAutomationPeer(MultiSelectionView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
