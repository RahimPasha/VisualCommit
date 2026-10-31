using Avalonia.Controls;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// The "Create pull request" form (<c>CreatePullRequestDialog</c>, docs/phase-7a-design.md 12.4) of
/// a <see cref="ViewModels.Dialogs.CreatePullRequestDialogViewModel"/>: the title takes the focus,
/// with the caret at its end; Enter there creates and Mod+Enter in the description does
/// (DialogView's rules).
/// </summary>
public partial class CreatePullRequestDialogView : DialogView
{
    public CreatePullRequestDialogView()
    {
        InitializeComponent();
    }

    protected override TextBox? InitialTextBox => PullRequestTitle;
}
