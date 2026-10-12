using Avalonia.Controls;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// "Unknown host" (4f). It has no box: Cancel takes the keyboard focus, as in the confirmation,
/// so that Enter does not trust a host by accident (a focused button answers Enter itself).
/// </summary>
public partial class HostKeyView : DialogView
{
    public HostKeyView()
    {
        InitializeComponent();
    }

    protected override Control? InitialFocus => CancelButton;
}
