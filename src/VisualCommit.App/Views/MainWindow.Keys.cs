using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.Views.Conflicts;

namespace VisualCommit.App.Views;

// The window's keys (docs/phase-5-design.md 1.3, D163): the toolbar's Undo, Redo and Search by
// their keys, moved here unchanged from MainWindow.axaml.cs (docs/phase-6-design.md 18). Work
// package WP-K moves them into the key router with phase 6's fixed set (6.2, 6.3).
public partial class MainWindow
{
    /// <summary>
    /// The platform's undo (<c>HotkeyConfiguration.Undo</c>: Ctrl+Z, Cmd+Z) and every gesture of its
    /// redo (Ctrl+Y and Ctrl+Shift+Z, Cmd+Shift+Z) are the active tab's toolbar's Undo and Redo
    /// (docs/phase-5-design.md 1.3, D163). Seen only on the way up and only when nothing took the
    /// key before (a text box with something to undo, the resolver's own undo, D148); they decline,
    /// so that the key goes on as before, while a dialog or a menu is open, a branch drag runs, or
    /// the focus is in a text box or anywhere in the conflict resolver. A disabled Undo or Redo
    /// takes its key and does nothing. The gestures are read at each press.
    /// </summary>
    private void OnGitUndoOrRedo(object? sender, KeyEventArgs e)
    {
        if (e.Handled || DataContext is not MainWindowViewModel shell || this.GetPlatformSettings()?.HotkeyConfiguration is not { } keys)
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

    /// <summary>Whether the undo and redo keys are not the window's now (1.3): something else takes them, or nothing should.</summary>
    private bool DeclinesGitUndo(MainWindowViewModel shell)
    {
        if (shell.Dialogs.IsOpen || shell.Menu.IsOpen || DragLayer.Service?.IsDragging == true)
        {
            return true;
        }

        var focused = FocusManager?.GetFocusedElement();
        return focused is TextBox
            || (focused is Visual visual && visual.GetSelfAndVisualAncestors().OfType<ConflictResolverView>().Any());
    }

    /// <summary>
    /// The command modifier with F (Ctrl+F; Cmd+F) is the toolbar's Search while it is enabled
    /// (docs/phase-5-design.md 1.3, 3.1), wherever the focus is, unless a dialog or a menu is open;
    /// in the search box it opens the bar again, which selects the box's text. Taken on the way
    /// down, as Shift+F10 is (D106): nothing else uses the key. The modifier is the platform's, as
    /// the graph's (<see cref="CommitGraphControl.CommandModifiers"/>), read at each press.
    /// </summary>
    private void OnSearchKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.F || DataContext is not MainWindowViewModel shell || shell.Dialogs.IsOpen || shell.Menu.IsOpen)
        {
            return;
        }

        var command = this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers is { } modifiers && modifiers != KeyModifiers.None
            ? modifiers
            : KeyModifiers.Control;
        var search = shell.ActiveTab.Toolbar.SearchCommand;
        if (e.KeyModifiers != command || !search.CanExecute(null))
        {
            return;
        }

        search.Execute(null);
        e.Handled = true;
    }
}
