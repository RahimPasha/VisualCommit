using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>What the name dialog was confirmed with.</summary>
/// <param name="Name">The name as typed (empty for the stash dialog, which has no name box).</param>
/// <param name="Message">The message as typed, or empty (the tag's and the stash's message).</param>
/// <param name="IsOptionChecked">The check box: "Check out the new branch" or "Include untracked files"; false without one.</param>
public sealed record NameDialogResult(string Name, string Message, bool IsOptionChecked);

/// <summary>
/// The name dialog (<c>NameDialog</c>, docs/phase-3-design.md 4f): a context line, a name box
/// with its live message (<c>NameBox</c>, <c>NameError</c>, D96), a message box, a check box
/// (<c>NameDialogOption</c>), Cancel and the confirm button. The branch, tag and stash dialogs
/// are its kinds (<see cref="BranchNameDialogViewModel"/>, <see cref="TagDialogViewModel"/>,
/// <see cref="StashDialogViewModel"/>); each shows the parts it has. The name box shows no message
/// until it has been edited (D114); the confirm button is disabled while the name is not valid.
/// Answers null when cancelled.
/// </summary>
public abstract partial class NameDialogViewModel : DialogViewModel<NameDialogResult?>
{
    private readonly bool _ready;
    private bool _nameEdited;

    /// <param name="title">The card's title.</param>
    /// <param name="confirmText">The confirm button's label.</param>
    /// <param name="initialName">What the name box holds when the dialog opens.</param>
    protected NameDialogViewModel(string title, string confirmText, string initialName = "")
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(confirmText);
        ArgumentNullException.ThrowIfNull(initialName);
        Title = title;
        ConfirmText = confirmText;
        Name = initialName;
        _ready = true;
    }

    public override string Title { get; }

    /// <summary>The confirm button's label.</summary>
    public string ConfirmText { get; }

    /// <summary>The line under the title: what the dialog starts from (<c>NameDialogContext</c>), or null.</summary>
    public string? Context { get; protected init; }

    public bool ShowsContext => Context is not null;

    /// <summary>The dialog has a name box.</summary>
    public bool ShowsName { get; protected init; }

    /// <summary>The name box's placeholder: "Branch name", "Tag name".</summary>
    public string NamePlaceholder { get; protected init; } = string.Empty;

    /// <summary>The name box selects what it holds when the dialog opens (renaming: the old name).</summary>
    public bool SelectsNameOnOpen { get; protected init; }

    /// <summary>The name as typed.</summary>
    [ObservableProperty]
    public partial string Name { get; set; }

    /// <summary>The message under the name box (danger colour), empty while there is none to show.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNameError))]
    public partial string NameError { get; private set; } = string.Empty;

    public bool HasNameError => NameError.Length > 0;

    /// <summary>The dialog has a message box.</summary>
    public bool ShowsMessage { get; protected init; }

    /// <summary>The message box's placeholder.</summary>
    public string MessagePlaceholder { get; protected init; } = string.Empty;

    /// <summary>The message box has three lines and takes Enter as a new line (the tag's message).</summary>
    public bool IsMessageMultiline { get; protected init; }

    /// <summary>The message as typed.</summary>
    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    /// <summary>The dialog has the check box.</summary>
    public bool ShowsOption => OptionText is not null;

    /// <summary>The check box's label, or null for none.</summary>
    public string? OptionText { get; protected init; }

    [ObservableProperty]
    public partial bool IsOptionChecked { get; set; }

    /// <summary>The confirm button is enabled.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool CanConfirm { get; private set; }

    protected override NameDialogResult? CancelledResult => null;

    /// <summary>
    /// What the name box says about <paramref name="name"/>, or null when it may be confirmed.
    /// Dialogs without a name box answer null.
    /// </summary>
    protected abstract string? ValidateName(string name);

    /// <summary>Whether the confirm button is enabled for a name that <see cref="ValidateName"/> accepts (renaming: only once it changed).</summary>
    protected virtual bool MayConfirm(string name) => true;

    /// <summary>Checks the name again: the subclass calls it once its own state is set.</summary>
    protected void Revalidate()
    {
        var error = ShowsName ? ValidateName(Name) : null;
        NameError = _nameEdited && error is not null ? error : string.Empty;
        CanConfirm = error is null && MayConfirm(Name);
    }

    partial void OnNameChanged(string value)
    {
        if (!_ready)
        {
            return;
        }

        _nameEdited = true;
        Revalidate();
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => Complete(new NameDialogResult(Name, Message, IsOptionChecked));
}

/// <summary>
/// The branch name dialog (2.4, 2.5): "Create branch" with "Check out the new branch" ticked, or
/// "Rename branch" with the old name filled in and selected. Validation by
/// <see cref="DialogText.BranchNameError"/> against the local branches.
/// </summary>
public sealed class BranchNameDialogViewModel : NameDialogViewModel
{
    private readonly IReadOnlyList<string> _existing;
    private readonly string? _oldName;
    private readonly bool _ignoreCase;

    private BranchNameDialogViewModel(string title, string confirmText, string initialName, IEnumerable<string> existing, string? oldName, bool? ignoreCase)
        : base(title, confirmText, initialName)
    {
        ArgumentNullException.ThrowIfNull(existing);
        _oldName = oldName;
        _existing = [.. existing.Where(name => !string.Equals(name, oldName, StringComparison.Ordinal))];
        _ignoreCase = ignoreCase ?? RefNameRules.NamesIgnoreCase;
        ShowsName = true;
        NamePlaceholder = "Branch name";
    }

    /// <summary>"Create branch": an empty name box, "Check out the new branch" ticked.</summary>
    /// <param name="context">What it starts from: <c>From a1b2c3d "Add core"</c>, <c>From origin/feature/x</c>, <c>From v1.0</c>.</param>
    /// <param name="existing">The local branches' short names.</param>
    /// <param name="ignoreCase">Compare ignoring case; null for this platform's rule (<see cref="RefNameRules.NamesIgnoreCase"/>).</param>
    public static BranchNameDialogViewModel Create(string context, IEnumerable<string> existing, bool? ignoreCase = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var dialog = new BranchNameDialogViewModel("Create branch", "Create branch", string.Empty, existing, null, ignoreCase)
        {
            Context = context,
            OptionText = "Check out the new branch",
            IsOptionChecked = true,
        };
        dialog.Revalidate();
        return dialog;
    }

    /// <summary>"Rename branch": the old name filled in and selected; "Rename" is disabled until the name changes.</summary>
    /// <param name="branch">The branch's short name.</param>
    /// <param name="existing">The local branches' short names (the branch itself among them or not).</param>
    /// <param name="ignoreCase">Compare ignoring case; null for this platform's rule.</param>
    public static BranchNameDialogViewModel Rename(string branch, IEnumerable<string> existing, bool? ignoreCase = null)
    {
        ArgumentNullException.ThrowIfNull(branch);
        var dialog = new BranchNameDialogViewModel("Rename branch", "Rename", branch, existing, branch, ignoreCase)
        {
            Context = $"Rename {branch}",
            SelectsNameOnOpen = true,
        };
        dialog.Revalidate();
        return dialog;
    }

    /// <summary>The dialog renames a branch.</summary>
    public bool IsRename => _oldName is not null;

    protected override string? ValidateName(string name) => DialogText.BranchNameError(name, _existing, _ignoreCase);

    protected override bool MayConfirm(string name) => !string.Equals(name, _oldName, StringComparison.Ordinal);
}

/// <summary>
/// The tag dialog (2.27): "Create tag", a name box and an optional message (a message makes an
/// annotated tag). Validation by <see cref="DialogText.TagNameError"/> against the tags.
/// </summary>
public sealed class TagDialogViewModel : NameDialogViewModel
{
    private readonly IReadOnlyList<string> _existing;
    private readonly bool _ignoreCase;

    /// <param name="context">Where the tag goes: <c>At a1b2c3d "Add core"</c>.</param>
    /// <param name="existing">The tags' names.</param>
    /// <param name="ignoreCase">Compare ignoring case; null for this platform's rule.</param>
    public TagDialogViewModel(string context, IEnumerable<string> existing, bool? ignoreCase = null)
        : base("Create tag", "Create tag")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(existing);
        _existing = [.. existing];
        _ignoreCase = ignoreCase ?? RefNameRules.NamesIgnoreCase;
        Context = context;
        ShowsName = true;
        NamePlaceholder = "Tag name";
        ShowsMessage = true;
        IsMessageMultiline = true;
        MessagePlaceholder = "Message (optional: makes an annotated tag)";
        Revalidate();
    }

    protected override string? ValidateName(string name) => DialogText.TagNameError(name, _existing, _ignoreCase);
}

/// <summary>
/// The stash dialog (2.24): an optional one-line message and "Include untracked files", which
/// starts with the last confirmed choice (6.2). "Stash" is always enabled.
/// </summary>
public sealed class StashDialogViewModel : NameDialogViewModel
{
    /// <param name="includeUntracked">The check box's first state: the last confirmed choice (<c>AppSettings.StashIncludeUntracked</c>).</param>
    public StashDialogViewModel(bool includeUntracked)
        : base("Stash changes", "Stash")
    {
        ShowsMessage = true;
        MessagePlaceholder = "Message (optional)";
        OptionText = "Include untracked files";
        IsOptionChecked = includeUntracked;
        Revalidate();
    }

    protected override string? ValidateName(string name) => null;
}
