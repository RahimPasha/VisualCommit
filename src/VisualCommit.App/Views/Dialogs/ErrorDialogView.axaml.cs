using Avalonia.Controls;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>The error dialog (<c>ErrorDialog</c>, docs/phase-3-design.md 4f). "Close" takes the focus: git's output is read-only.</summary>
public partial class ErrorDialogView : DialogView
{
    public ErrorDialogView()
    {
        InitializeComponent();
    }

    protected override Control? InitialFocus => ConfirmButton;
}
