using Avalonia.Controls;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>"Git asks" (4f): any other prompt. Its box takes the keyboard focus; Enter answers (<see cref="DialogView"/>).</summary>
public partial class PromptView : DialogView
{
    public PromptView()
    {
        InitializeComponent();
    }

    protected override TextBox? InitialTextBox => PromptBox;
}
