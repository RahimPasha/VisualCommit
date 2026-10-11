using Avalonia.Controls;
using VisualCommit.App.ViewModels.Dialogs;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// The name dialog (<c>NameDialog</c>, docs/phase-3-design.md 4f) of a
/// <see cref="NameDialogViewModel"/>: the branch, tag and stash dialogs. Its first box takes the
/// focus (the name box, or the stash's message box); renaming selects the old name.
/// </summary>
public partial class NameDialogView : DialogView
{
    public NameDialogView()
    {
        InitializeComponent();
    }

    protected override TextBox? InitialTextBox =>
        DataContext is NameDialogViewModel { ShowsName: false } ? MessageBox : NameBox;

    protected override bool SelectsAllOnOpening => DataContext is NameDialogViewModel { SelectsNameOnOpen: true };
}
