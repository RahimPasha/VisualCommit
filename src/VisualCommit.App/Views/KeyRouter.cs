using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.Views.Conflicts;

namespace VisualCommit.App.Views;

/// <summary>
/// The window's keys (docs/phase-6-design.md 6.2, 6.3, D192, D213, D214): the fixed set of
/// <see cref="AppShortcuts"/> on the window's tunnelling pass, before the focused control, and
/// phase 5's undo and redo on the bubbling pass, after it (phase 5 design 1.3, D163). Phase 5's
/// Mod+F (<c>OnSearchKey</c>) and undo and redo (<c>OnGitUndoOrRedo</c>) moved into it.
/// <para>
/// On the tunnelling pass, per press: the key in the app's words (<see cref="ShortcutKeys"/>),
/// the command modifier read at each press from the platform's hotkey configuration (Control
/// when it names none); the command <see cref="AppShortcuts.Route"/> gives, or the key goes on.
/// It declines (goes on, not handled), in order, while a dialog is open (but Mod+Shift+P with the
/// palette the top dialog, which selects its box's text), while a menu is open or a branch drag
/// runs, Ctrl+Tab and Ctrl+Shift+Tab in the conflict resolver's result, and Mod+Enter outside the
/// commit message boxes. Otherwise the window's commands run it when it applies and is enabled,
/// and the key is handled either way: a disabled command's key is consumed, as D163's disabled
/// Undo. The router logs nothing: a key's effect logs itself.
/// </para>
/// </summary>
public sealed class KeyRouter
{
    private readonly MainWindow _window;

    public KeyRouter(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
    }

    /// <summary>The window's tunnelling pass (seen also when a handler before it took the key; such a key is left alone).</summary>
    public void OnTunnel(object? sender, KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Handled || _window.DataContext is not MainWindowViewModel shell)
        {
            return;
        }

        var command = CommandModifier();
        if (ShortcutKeys.FromEvent(e, command) is not { } shortcut
            || AppShortcuts.Route(shortcut, command.HasFlag(KeyModifiers.Meta) ? CommandKey.Cmd : CommandKey.Ctrl) is not { } routed)
        {
            return;
        }

        if (shell.Dialogs.IsOpen)
        {
            // The palette's own Mod+Shift+P selects its query (5.8, KD20); every other key goes on.
            if (routed.Command == AppCommand.CommandPalette && shell.OpenPalette is { } palette)
            {
                palette.RequestSelectQuery();
                e.Handled = true;
            }

            return;
        }

        if (shell.Menu.IsOpen || _window.DragLayer.Service?.IsDragging == true || Declines(routed, shortcut))
        {
            return;
        }

        shell.Commands.Run(routed.Command, routed.Argument);
        e.Handled = true;
    }

    /// <summary>
    /// The window's bubbling pass: the platform's undo (<c>HotkeyConfiguration.Undo</c>) and every
    /// gesture of its redo are the active tab's toolbar's Undo and Redo (phase 5 design 1.3, D163),
    /// seen only when nothing took the key before (a text box with something to undo, the
    /// resolver's own undo). They decline while a dialog or a menu is open, a branch drag runs, or
    /// the focus is in a text box or anywhere in the conflict resolver; a disabled Undo or Redo
    /// takes its key and does nothing. The gestures are read at each press.
    /// </summary>
    public void OnBubble(object? sender, KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Handled || _window.DataContext is not MainWindowViewModel shell || _window.GetPlatformSettings()?.HotkeyConfiguration is not { } keys)
        {
            return;
        }

        var undo = keys.Undo.Any(gesture => gesture.Matches(e));
        if ((!undo && !keys.Redo.Any(gesture => gesture.Matches(e))) || DeclinesGitUndo(shell))
        {
            return;
        }

        var toolbar = shell.ActiveTab.Toolbar;
        var command = undo ? toolbar.UndoCommand : toolbar.RedoCommand;
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }

        e.Handled = true;
    }

    /// <summary>The platform's command modifier, read at each press (Control when it names none).</summary>
    private KeyModifiers CommandModifier() =>
        _window.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers is { } modifiers && modifiers != KeyModifiers.None
            ? modifiers
            : KeyModifiers.Control;

    /// <summary>The router's own declines that depend on where the focus is (6.3, 6.5, 6.10).</summary>
    private bool Declines(RoutedCommand routed, Shortcut shortcut)
    {
        var focused = _window.FocusManager?.GetFocusedElement() as Visual;
        switch (routed.Command)
        {
            case AppCommand.NextTab or AppCommand.PreviousTab when shortcut.Key == ShortcutKey.Tab:
                // In the resolver's result Tab with Control is the navigation's: it leaves the result (phase 4 design 11.10).
                return focused?.GetSelfAndVisualAncestors().OfType<ConflictResultEditor>().Any() == true;

            case AppCommand.Commit:
                // Only in the two message boxes (6.10).
                return focused is not TextBox { Name: "CommitSummary" or "CommitDescription" };

            default:
                return false;
        }
    }

    /// <summary>Whether the undo and redo keys are not the window's now (phase 5 design 1.3): something else takes them, or nothing should.</summary>
    private bool DeclinesGitUndo(MainWindowViewModel shell)
    {
        if (shell.Dialogs.IsOpen || shell.Menu.IsOpen || _window.DragLayer.Service?.IsDragging == true)
        {
            return true;
        }

        var focused = _window.FocusManager?.GetFocusedElement();
        return focused is TextBox
            || (focused is Visual visual && visual.GetSelfAndVisualAncestors().OfType<ConflictResolverView>().Any());
    }
}
