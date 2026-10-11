using Avalonia.Controls;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>The reset dialog (<c>ResetDialog</c>, docs/phase-3-design.md 2.11, 4f). Cancel takes the focus, as in a confirmation.</summary>
public partial class ResetDialogView : DialogView
{
    public ResetDialogView()
    {
        InitializeComponent();
    }

    protected override Control? InitialFocus => CancelButton;
}
