using Avalonia.Controls;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>The confirmation dialog (D69). When it opens, its Cancel button takes the keyboard focus.</summary>
public partial class ConfirmationView : DialogView
{
    public ConfirmationView()
    {
        InitializeComponent();
    }

    protected override Control? InitialFocus => CancelButton;
}
