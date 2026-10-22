using Avalonia.Controls;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// The "Add repositories" checklist (<c>AddRepositoriesDialog</c>, docs/phase-6-design.md 1.9) of
/// an <see cref="ViewModels.Dialogs.AddRepositoriesDialogViewModel"/>. It has no text box, so
/// Cancel takes the focus when it opens, as a confirmation's does; Tab goes round the boxes and
/// the buttons, and Space toggles the focused box.
/// </summary>
public partial class AddRepositoriesDialogView : DialogView
{
    public AddRepositoriesDialogView()
    {
        InitializeComponent();
    }

    protected override Control? InitialFocus => CancelButton;
}
