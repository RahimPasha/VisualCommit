using Avalonia.Controls;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>"Key passphrase" (4f). Its box takes the keyboard focus; Enter unlocks (<see cref="DialogView"/>).</summary>
public partial class PassphraseView : DialogView
{
    public PassphraseView()
    {
        InitializeComponent();
    }

    protected override TextBox? InitialTextBox => PassphraseBox;
}
