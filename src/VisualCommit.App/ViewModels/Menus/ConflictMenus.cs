namespace VisualCommit.App.ViewModels.Menus;

/// <summary>What the conflict resolver's result menu acts on (docs/phase-4-design.md 11.12, 14.3).</summary>
/// <param name="CanUndo">Something can be undone.</param>
/// <param name="CanRedo">Something can be redone.</param>
/// <param name="HasSelection">Text is selected.</param>
/// <param name="IsReadOnly">The result is read-only while a write of the resolver runs: Undo, Redo and Cut are disabled (Paste does nothing then).</param>
public sealed record ConflictResultMenuState(bool CanUndo, bool CanRedo, bool HasSelection, bool IsReadOnly = false);

/// <summary>What the result menu's items do: the resolver's undo and redo, the editor's clipboard.</summary>
/// <param name="Undo">The resolver's undo (as the platform's gesture).</param>
/// <param name="Redo">The resolver's redo.</param>
/// <param name="Cut">Cuts the selection to the platform's clipboard.</param>
/// <param name="Copy">Copies the selection.</param>
/// <param name="Paste">Pastes at the caret.</param>
/// <param name="SelectAll">Selects the whole result.</param>
public sealed record ConflictResultMenuActions(Action Undo, Action Redo, Action Cut, Action Copy, Action Paste, Action SelectAll);

/// <summary>
/// The conflict resolver's own menus (docs/phase-4-design.md 11.12, 14.3), as data like every menu
/// (D86): the result's and a pane's. The header's right-click opens the conflicted file's menu,
/// <see cref="RepositoryMenus.ForUnstagedFile"/>, which the repository builds.
/// </summary>
public static class ConflictMenus
{
    /// <summary>Undo and Redo without anything to undo or redo.</summary>
    public const string NothingToUndo = "Nothing to undo";

    /// <summary>Redo without anything to redo.</summary>
    public const string NothingToRedo = "Nothing to redo";

    /// <summary>Cut and Copy without a selection.</summary>
    public const string SelectTextFirst = "Select some text first";

    /// <summary>
    /// The result's menu: header <c>{file} (result)</c>; [Undo, Redo] — [Cut, Copy, Paste, Select
    /// all]. Undo and Redo are disabled with nothing to undo or redo, Cut and Copy without a
    /// selection; Paste and Select all are always enabled (14.3).
    /// </summary>
    public static MenuModel ForConflictResult(string fileName, ConflictResultMenuState state, ConflictResultMenuActions actions)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actions);
        var editable = !state.IsReadOnly;
        return RepositoryMenus.Build(
            new MenuHeader($"{fileName} (result)"),
            [
                new MenuItem("Undo", actions.Undo, state.CanUndo && editable, state.CanUndo ? null : NothingToUndo),
                new MenuItem("Redo", actions.Redo, state.CanRedo && editable, state.CanRedo ? null : NothingToRedo),
            ],
            [
                new MenuItem("Cut", actions.Cut, state.HasSelection && editable, state.HasSelection ? null : SelectTextFirst),
                new MenuItem("Copy", actions.Copy, state.HasSelection, state.HasSelection ? null : SelectTextFirst),
                new MenuItem("Paste", actions.Paste),
                new MenuItem("Select all", actions.SelectAll),
            ]);
    }

    /// <summary>A pane's menu: header its title (<c>Ours: main</c>); [Copy], disabled without a selection (14.3).</summary>
    public static MenuModel ForConflictPane(string title, bool hasSelection, Action copy)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(copy);
        return RepositoryMenus.Build(new MenuHeader(title), [new MenuItem("Copy", copy, hasSelection, hasSelection ? null : SelectTextFirst)]);
    }
}
