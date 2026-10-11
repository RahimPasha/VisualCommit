using Avalonia.Controls;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>"Key passphrase" (4f). Its box takes the keyboard focus; Enter unlocks.</summary>
public partial class PassphraseView : UserControl
{
    public PassphraseView()
    {
        InitializeComponent();
        PromptDialogViewSupport.Attach(this, () => PassphraseBox);
    }
}
