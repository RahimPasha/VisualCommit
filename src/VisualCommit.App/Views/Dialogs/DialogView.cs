using Avalonia;
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
/// is in a box that takes Enter as a new line, or on a button (which handles Enter itself: a click).
/// In a list of rows to choose from (<see cref="ConfirmsOnEnter"/>) it does so while a row is
/// selected, and in a list of radio buttons (<see cref="ConfirmsOnEnterOnChecked"/>) on the checked
/// one.</item>
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

        // Mod+Enter in a box of several lines, taken on the way down: such a box types a line
        // break for Enter with any modifier (docs/phase-6-design.md 6.10, 0.7).
        AddHandler(KeyDownEvent, OnKeyDownForModEnter, RoutingStrategies.Tunnel);
    }

    /// <summary>The control that takes the focus when the dialog has no text box to type into; null for none.</summary>
    protected virtual Control? InitialFocus => null;

    /// <summary>The text box that should take the focus, or null for the first one there is.</summary>
    protected virtual TextBox? InitialTextBox => null;

    /// <summary>Whether the focused text box selects all it holds when the dialog opens (renaming).</summary>
    protected virtual bool SelectsAllOnOpening => false;

    /// <summary>
    /// Whether the dialog's first text box takes the focus when it opens (every dialog's, the
    /// default); false gives it to <see cref="InitialFocus"/> instead: the settings screen's
    /// section list (docs/phase-6-design.md 7.1).
    /// </summary>
    protected virtual bool FocusesTextBoxOnOpening => true;

    protected override AutomationPeer OnCreateAutomationPeer() => new DialogAutomationPeer(this);

    /// <summary>Moves the focus as the dialog does when it opens; also after its card changed (the Remotes dialog's form).</summary>
    protected void FocusOnOpening()
    {
        var box = !FocusesTextBoxOnOpening ? null
            : InitialTextBox is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } chosen
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

    /// <summary>
    /// Enter with no modifier in <paramref name="list"/>, while a row is selected, runs the confirm
    /// button's command as a click on it does, when the button is enabled and the command can run;
    /// the key is then handled. With no row selected the row's item keeps the key. Taken on the way
    /// down: a list's item takes Enter for itself (it selects its row), so the dialog's own Enter
    /// never sees it there.
    /// </summary>
    protected void ConfirmsOnEnter(ListBox list)
    {
        ArgumentNullException.ThrowIfNull(list);
        ConfirmOnEnterIn(list, _ => list.SelectedItem is not null);
    }

    /// <summary>
    /// Enter with no modifier on a checked radio button of <paramref name="options"/> runs the
    /// confirm button's command as a click on it does, when the button is enabled and the command
    /// can run; the key is then handled. On an option not checked the radio button keeps the key,
    /// which checks it. Taken on the way down: a radio button takes Enter for itself (a click, which
    /// only checks it), so the dialog's own Enter never sees it there.
    /// </summary>
    protected void ConfirmsOnEnterOnChecked(ItemsControl options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ConfirmOnEnterIn(options, e => (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<RadioButton>().FirstOrDefault() is { IsChecked: true });
    }

    /// <summary>Enter with no modifier inside <paramref name="list"/>, on the way down, runs the confirm button when <paramref name="chosen"/> holds.</summary>
    private void ConfirmOnEnterIn(Control list, Func<KeyEventArgs, bool> chosen) =>
        list.AddHandler(
            KeyDownEvent,
            (_, e) =>
            {
                if (e.Handled || e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None || !chosen(e))
                {
                    return;
                }

                if (this.FindControl<Button>("ConfirmButton") is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, Command: { } command } confirm
                    && command.CanExecute(confirm.CommandParameter))
                {
                    FollowFromSource(e, command);
                    command.Execute(confirm.CommandParameter);
                    e.Handled = true;
                }
            },
            RoutingStrategies.Tunnel);

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
            FollowFromSource(e, command);
            command.Execute(confirm.CommandParameter);
            e.Handled = true;
        }
    }

    /// <summary>
    /// The confirm run by a key in <paramref name="e"/>'s source (a box, a row, an option): a run
    /// that keeps the dialog open and disables that source while it lasts (a refused save) gives it
    /// the focus back (D462).
    /// </summary>
    private static void FollowFromSource(KeyEventArgs e, System.Windows.Input.ICommand command)
    {
        if (e.Source is InputElement source)
        {
            FocusReturn.AfterKeyboardAction(source, command);
        }
    }

    /// <summary>
    /// Mod+Enter (Ctrl+Enter; Cmd+Enter on macOS) in a text box of several lines (the message
    /// dialog's <c>MessageDescription</c>) runs the confirm button as Enter does elsewhere
    /// (docs/phase-6-design.md 6.10), handled either way; plain Enter there still types a line
    /// break. The command modifier is the platform's, read at each press.
    /// </summary>
    private void OnKeyDownForModEnter(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter || e.Source is not TextBox { AcceptsReturn: true })
        {
            return;
        }

        var command = ShortcutKeys.CommandModifierOf(this.GetPlatformSettings()?.HotkeyConfiguration);
        if (e.KeyModifiers != command)
        {
            return;
        }

        if (this.FindControl<Button>("ConfirmButton") is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, Command: { } confirm } button
            && confirm.CanExecute(button.CommandParameter))
        {
            FollowFromSource(e, confirm);
            confirm.Execute(button.CommandParameter);
        }

        e.Handled = true;
    }

    /// <summary>A dialog's root as UI Automation sees it: a pane, a control element, named by the dialog's title.</summary>
    private sealed class DialogAutomationPeer(DialogView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
