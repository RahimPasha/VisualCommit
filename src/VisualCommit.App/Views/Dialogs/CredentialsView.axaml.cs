using Avalonia.Controls;
using VisualCommit.App.ViewModels.Dialogs;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// The credentials dialog (4f). On opening, the user name box takes the keyboard focus, or the
/// password box when git named the user; Enter signs in (<see cref="DialogView"/>).
/// </summary>
public partial class CredentialsView : DialogView
{
    public CredentialsView()
    {
        InitializeComponent();
    }

    protected override TextBox? InitialTextBox =>
        DataContext is CredentialsViewModel { IsUserEditable: false } ? CredentialPassword : CredentialUsername;
}
