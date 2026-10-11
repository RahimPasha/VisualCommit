using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// The root of a dialog's view (docs/phase-3-design.md 4f, D69, D104). It gives every dialog the
/// same keyboard and UI Automation behaviour:
/// <list type="bullet">
/// <item>When it opens, the focus goes to its first text box that can be typed into (the caret at
/// the end), or else to the control <see cref="InitialFocus"/> names (the confirmation's Cancel).</item>
/// <item>Enter runs the confirm button (<c>ConfirmButton</c>) while it is enabled, unless the focus
/// is in a box that takes Enter as a new line, or on a button (which handles Enter itself: a click).</item>
/// <item>Its root is a UI Automation control element (a pane named by the dialog's title), so the
/// real-window pass finds <c>ErrorDialog</c> and the others by their automation ids (4a, 4f).</item>
/// </list>
/// Esc is the window's: it goes to the top dialog only (<c>DialogHostViewModel.CancelCurrent</c>).
/// </summary>
public class DialogView : UserControl
{
    public DialogView()
    {
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not null)
            {
                // After the layout pass that makes the dialog visible: a hidden control cannot take the focus.
                Dispatcher.UIThread.Post(FocusOnOpening, DispatcherPriority.Loaded);
            }
        };
        AddHandler(KeyDownEvent, OnKeyDownForEnter, RoutingStrategies.Bubble);
    }

    /// <summary>The control that takes the focus when the dialog has no text box to type into; null for none.</summary>
    protected virtual Control? InitialFocus => null;

    /// <summary>The text box that should take the focus, or null for the first one there is.</summary>
    protected virtual TextBox? InitialTextBox => null;

    /// <summary>Whether the focused text box selects all it holds when the dialog opens (renaming).</summary>
    protected virtual bool SelectsAllOnOpening => false;

    protected override AutomationPeer OnCreateAutomationPeer() => new DialogAutomationPeer(this);

    /// <summary>Moves the focus as the dialog does when it opens; also after its card changed (the Remotes dialog's form).</summary>
    protected void FocusOnOpening()
    {
        var box = InitialTextBox is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } chosen
            ? chosen
            : this.GetVisualDescendants().OfType<TextBox>()
                .FirstOrDefault(candidate => candidate is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, IsReadOnly: false });
        if (box is not null)
        {
            box.Focus(NavigationMethod.Unspecified);
            if (SelectsAllOnOpening)
            {
                box.SelectAll();
            }
            else
            {
                box.ClearSelection();
                box.CaretIndex = box.Text?.Length ?? 0;
            }

            return;
        }

        InitialFocus?.Focus(NavigationMethod.Tab);
    }

    private void OnKeyDownForEnter(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None
            || e.Source is TextBox { AcceptsReturn: true })
        {
            return;
        }

        if (this.FindControl<Button>("ConfirmButton") is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, Command: { } command } confirm
            && command.CanExecute(confirm.CommandParameter))
        {
            command.Execute(confirm.CommandParameter);
            e.Handled = true;
        }
    }

    /// <summary>A dialog's root as UI Automation sees it: a pane, a control element, named by the dialog's title.</summary>
    private sealed class DialogAutomationPeer(DialogView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
