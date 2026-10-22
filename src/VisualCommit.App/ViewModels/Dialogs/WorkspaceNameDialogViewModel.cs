using VisualCommit.App.ViewModels.Workspaces;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// The workspace's name dialog (docs/phase-6-design.md 1.6): <c>New workspace</c> with an empty
/// box, or <c>Rename workspace</c> with the name filled in and selected, drawn by
/// <c>NameDialogView</c> through its base type. Validation by
/// <see cref="DialogText.WorkspaceNameError"/> against the other workspaces' names, shown once the
/// box was edited (D114). No context line on the new one (WD28) and no repository line on either
/// (WD11): it is no operation's dialog.
/// </summary>
public sealed class WorkspaceNameDialogViewModel : NameDialogViewModel
{
    private readonly IReadOnlyList<string> _existing;
    private readonly string? _oldName;

    private WorkspaceNameDialogViewModel(string title, string confirmText, string initialName, IEnumerable<string> existing, string? oldName)
        : base(title, confirmText, initialName)
    {
        ArgumentNullException.ThrowIfNull(existing);
        _existing = [.. existing];
        _oldName = oldName;
        ShowsName = true;
        NamePlaceholder = WorkspaceTexts.NamePlaceholder;
    }

    /// <summary><c>New workspace</c>: an empty box; <c>Create</c> is disabled until the name is valid.</summary>
    /// <param name="existing">Every workspace's name.</param>
    public static WorkspaceNameDialogViewModel Create(IEnumerable<string> existing)
    {
        var dialog = new WorkspaceNameDialogViewModel(WorkspaceTexts.NewWorkspaceTitle, WorkspaceTexts.CreateConfirm, string.Empty, existing, null);
        dialog.Revalidate();
        return dialog;
    }

    /// <summary>
    /// <c>Rename workspace</c>: the name filled in and selected, the context <c>Rename {name}</c>;
    /// <c>Rename</c> is disabled until the trimmed name differs (ordinally: a change of case alone
    /// may be saved).
    /// </summary>
    /// <param name="name">The workspace's name.</param>
    /// <param name="others">The other workspaces' names.</param>
    public static WorkspaceNameDialogViewModel Rename(string name, IEnumerable<string> others)
    {
        ArgumentNullException.ThrowIfNull(name);
        var dialog = new WorkspaceNameDialogViewModel(WorkspaceTexts.RenameWorkspaceTitle, WorkspaceTexts.RenameConfirm, name, others, name)
        {
            Context = WorkspaceTexts.RenameContext(name),
            SelectsNameOnOpen = true,
        };
        dialog.Revalidate();
        return dialog;
    }

    /// <summary>The dialog renames a workspace.</summary>
    public bool IsRename => _oldName is not null;

    protected override string? ValidateName(string name) => DialogText.WorkspaceNameError(name, _existing);

    protected override bool MayConfirm(string name) =>
        _oldName is null || !string.Equals(name.Trim(), _oldName, StringComparison.Ordinal);
}
