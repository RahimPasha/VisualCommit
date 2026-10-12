using Avalonia.Controls;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// The message dialog (<c>MessageDialog</c>, docs/phase-4-design.md 7.2) of a
/// <see cref="ViewModels.Dialogs.MessageDialogViewModel"/>: the summary takes the focus, with the
/// caret at its end.
/// </summary>
public partial class MessageDialogView : DialogView
{
    public MessageDialogView()
    {
        InitializeComponent();
    }

    protected override TextBox? InitialTextBox => MessageSummary;
}
