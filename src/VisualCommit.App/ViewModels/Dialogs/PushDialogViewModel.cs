using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>What the push dialog was confirmed with.</summary>
/// <param name="Remote">The remote to push to.</param>
/// <param name="Branch">The branch's name on the remote.</param>
/// <param name="SetUpstream">"Set as upstream" was ticked (<c>--set-upstream</c>).</param>
public sealed record PushDialogResult(string Remote, string Branch, bool SetUpstream);

/// <summary>
/// The push dialog (<c>PushDialog</c>, docs/phase-3-design.md 2.19, 4f) for a branch without an
/// upstream: "To" the remote (as text when there is one, radio buttons <c>ChoiceOption</c> when
/// there are several, the first checked), the remote branch's name (<c>PushBranchBox</c>, the
/// local name to begin with) and "Set as upstream" (<c>PushSetUpstream</c>, ticked). "Push" is
/// disabled while the name is not a valid branch name; the reason shows under the box once it
/// has been edited (<c>PushBranchError</c>, D114). Answers null when cancelled.
/// </summary>
public sealed partial class PushDialogViewModel : DialogViewModel<PushDialogResult?>
{
    private readonly bool _ready;
    private bool _edited;

    /// <param name="branch">The local branch's short name.</param>
    /// <param name="remotes">The remotes' names, in the order shown; at least one.</param>
    public PushDialogViewModel(string branch, IReadOnlyList<string> remotes)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(remotes);
        if (remotes.Count == 0)
        {
            throw new ArgumentException("A push needs a remote.", nameof(remotes));
        }

        Title = $"Push {branch}";
        Remotes = ChoiceOption.Group(remotes, chosen => Remote = chosen.Text);
        Remote = remotes[0];
        BranchName = branch;
        _ready = true;
        Validate();
    }

    public override string Title { get; }

    /// <summary>The remotes as radio buttons (shown when there are several).</summary>
    public IReadOnlyList<ChoiceOption> Remotes { get; }

    /// <summary>There is one remote: "To" shows its name as text.</summary>
    public bool HasOneRemote => Remotes.Count == 1;

    /// <summary>The remote pushed to.</summary>
    [ObservableProperty]
    public partial string Remote { get; private set; }

    /// <summary>The branch's name on the remote.</summary>
    [ObservableProperty]
    public partial string BranchName { get; set; }

    /// <summary>Why the name cannot be pushed to, once the box was edited; empty otherwise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBranchError))]
    public partial string BranchError { get; private set; } = string.Empty;

    public bool HasBranchError => BranchError.Length > 0;

    /// <summary>"Set as upstream".</summary>
    [ObservableProperty]
    public partial bool SetUpstream { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool CanConfirm { get; private set; }

    protected override PushDialogResult? CancelledResult => null;

    partial void OnBranchNameChanged(string value)
    {
        if (_ready)
        {
            _edited = true;
            Validate();
        }
    }

    private void Validate()
    {
        var error = BranchName.Length == 0 ? DialogText.EnterName
            : RefNameRules.BranchNameProblem(BranchName) is { } problem ? $"This is not a valid branch name: {problem}."
            : null;
        BranchError = _edited && error is not null ? error : string.Empty;
        CanConfirm = error is null;
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => Complete(new PushDialogResult(Remote, BranchName, SetUpstream));
}
