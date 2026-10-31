namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// The repository browser "Clone from GitHub" (<c>CloneFromGitHubDialog</c>, docs/phase-7a-design.md
/// 9.4, D301), laid out as the pick dialog: the filter box (<c>CloneFromGitHubFilter</c>) takes the
/// focus, the list (<c>CloneFromGitHubList</c>, items <c>CloneFromGitHubItem</c> named by their full
/// names) shows a row's detail after its name, and the lines of the list's state follow it.
/// </summary>
public partial class CloneFromGitHubDialogView : DialogView
{
    public CloneFromGitHubDialogView()
    {
        InitializeComponent();
    }

    protected override Avalonia.Controls.TextBox? InitialTextBox => CloneFromGitHubFilter;
}
