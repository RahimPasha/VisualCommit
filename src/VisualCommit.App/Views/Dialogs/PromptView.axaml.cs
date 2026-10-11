using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VisualCommit.App.ViewModels.Dialogs;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>"Git asks" (4f): any other prompt. Its box takes the keyboard focus; Enter answers.</summary>
public partial class PromptView : UserControl
{
    public PromptView()
    {
        InitializeComponent();
        PromptDialogViewSupport.Attach(this, () => PromptBox);
    }
}

/// <summary>
/// What the prompt dialogs' views share (docs/phase-3-design.md 4f): the first control to type
/// into takes the keyboard focus when the dialog opens, and Enter answers as the confirm button
/// does (Esc is the window's: it cancels the top dialog).
/// </summary>
internal static class PromptDialogViewSupport
{
    /// <summary>Gives <paramref name="view"/> the focus on opening and the Enter key.</summary>
    /// <param name="view">The dialog's view.</param>
    /// <param name="focusFirst">The control that takes the focus when the dialog opens.</param>
    public static void Attach(UserControl view, Func<Control> focusFirst)
    {
        view.DataContextChanged += (_, _) =>
        {
            if (view.DataContext is not null)
            {
                // After the layout pass that makes the dialog visible: a hidden control cannot take the focus.
                Dispatcher.UIThread.Post(() => focusFirst().Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
            }
        };

        view.AddHandler(InputElement.KeyDownEvent, (_, e) => OnKeyDown(view, e), RoutingStrategies.Bubble);
    }

    private static void OnKeyDown(UserControl view, KeyEventArgs e)
    {
        // A focused button answers Enter itself (Cancel cancels); anywhere else Enter confirms.
        if (e.Handled || e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None || e.Source is Button)
        {
            return;
        }

        if (view.DataContext is PromptDialogViewModel dialog && dialog.ConfirmCommand.CanExecute(null))
        {
            dialog.ConfirmCommand.Execute(null);
            e.Handled = true;
        }
    }
}
