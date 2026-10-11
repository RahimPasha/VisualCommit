using Avalonia.Controls;
using Avalonia.VisualTree;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>The remote choice dialog (<c>ChoiceDialog</c>, docs/phase-3-design.md 2.29, 4f). The checked radio button takes the focus.</summary>
public partial class ChoiceDialogView : DialogView
{
    public ChoiceDialogView()
    {
        InitializeComponent();
    }

    protected override Control? InitialFocus =>
        ChoiceOptions.GetVisualDescendants().OfType<RadioButton>().FirstOrDefault(radio => radio.IsChecked == true) ?? (Control)CancelButton;
}
