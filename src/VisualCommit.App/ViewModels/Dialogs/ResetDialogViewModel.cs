using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// The reset dialog (<c>ResetDialog</c>, docs/phase-3-design.md 2.11, 4f): Soft, Mixed (chosen
/// when it opens) or Hard, each with a line saying what it keeps. It is the reset's only
/// confirmation: its "Reset" button takes the danger style while Hard is chosen. Answers the mode,
/// or null when cancelled.
/// </summary>
public sealed partial class ResetDialogViewModel : DialogViewModel<ResetMode?>
{
    public const string SoftText = "Keep every change; the changes of the commits after it are staged.";

    public const string MixedText = "Keep every change; nothing is staged.";

    public const string HardText = "Discard every change to tracked files.";

    /// <param name="branch">What moves: the current branch's name, or "HEAD" while HEAD is detached.</param>
    /// <param name="sha">The target commit (full or short id).</param>
    /// <param name="subject">The target commit's subject.</param>
    public ResetDialogViewModel(string branch, string sha, string subject)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(sha);
        ArgumentNullException.ThrowIfNull(subject);
        var shortSha = DialogText.ShortSha(sha);
        Title = $"Reset {branch} to {shortSha}?";
        Text = $"Move {branch} to \"{DialogText.Subject(subject)}\" ({shortSha}).";
    }

    public override string Title { get; }

    /// <summary>"Move main to "Add core" (a1b2c3d)."</summary>
    public string Text { get; }

    /// <summary>The line under the choices (<c>ConfirmDetail</c>).</summary>
    public string Detail => DialogText.BackupDetail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSoft), nameof(IsMixed), nameof(IsHard))]
    public partial ResetMode Mode { get; set; } = ResetMode.Mixed;

    /// <summary>Soft is chosen (the radio button <c>ResetSoft</c>).</summary>
    public bool IsSoft
    {
        get => Mode == ResetMode.Soft;
        set => Choose(ResetMode.Soft, value);
    }

    /// <summary>Mixed is chosen (<c>ResetMixed</c>).</summary>
    public bool IsMixed
    {
        get => Mode == ResetMode.Mixed;
        set => Choose(ResetMode.Mixed, value);
    }

    /// <summary>Hard is chosen (<c>ResetHard</c>): "Reset" is drawn in the danger style.</summary>
    public bool IsHard
    {
        get => Mode == ResetMode.Hard;
        set => Choose(ResetMode.Hard, value);
    }

    protected override ResetMode? CancelledResult => null;

    private void Choose(ResetMode mode, bool chosen)
    {
        if (chosen)
        {
            Mode = mode;
        }
    }

    /// <summary>"Reset": answers the chosen mode.</summary>
    [RelayCommand]
    private void Confirm() => Complete(Mode);
}
