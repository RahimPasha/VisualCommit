using Avalonia.Controls;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>"Finishing operations" (<c>FinishingDialog</c>, docs/phase-3-design.md 1.8, 4f). "Keep open" takes the focus.</summary>
public partial class FinishingDialogView : DialogView
{
    public FinishingDialogView()
    {
        InitializeComponent();
    }

    protected override Control? InitialFocus => CancelButton;
}
