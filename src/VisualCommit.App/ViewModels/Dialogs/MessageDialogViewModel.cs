using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Panels;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// The message dialog (<c>MessageDialog</c>, docs/phase-4-design.md 7.2): a commit message as a
/// summary and a description, for "Reword {short}", "Message of the squashed commit" and "Squash
/// {N} commits". The summary has the stage panel's counter of what is left of 72 characters; the
/// confirm button ("Save", "Squash") is disabled while the summary is blank, with the reason
/// "Write a summary". The message it is filled with is split as the stage panel splits an
/// operation's message (5.3), with no line removed, and it answers the two joined as
/// <c>{summary}\n\n{description}</c> (the summary alone when the description is empty), or null
/// when it was cancelled.
/// </summary>
public sealed partial class MessageDialogViewModel : DialogViewModel<string?>
{
    /// <summary>The confirm button's tooltip while the summary is blank (10.4).</summary>
    public const string WriteSummaryReason = "Write a summary";

    /// <param name="title">The card's title: <c>Reword {short}</c>, <c>Message of the squashed commit</c>, <c>Squash {N} commits</c>.</param>
    /// <param name="message">The message to start from, split into the summary and the description (<see cref="Split"/>).</param>
    /// <param name="confirmText">The confirm button's label: <c>Save</c> or <c>Squash</c>.</param>
    public MessageDialogViewModel(string title, string message, string confirmText)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(confirmText);
        Title = title;
        ConfirmText = confirmText;
        (Summary, Description) = Split(message);
    }

    public override string Title { get; }

    /// <summary>The confirm button's label.</summary>
    public string ConfirmText { get; }

    /// <summary>The message's first line (<c>MessageSummary</c>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryCounter), nameof(IsSummaryOverLimit), nameof(CanConfirm), nameof(ConfirmToolTip), nameof(Message))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string Summary { get; set; } = string.Empty;

    /// <summary>The rest of the message (<c>MessageDescription</c>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Message))]
    public partial string Description { get; set; } = string.Empty;

    /// <summary>What is left of <see cref="WorkingChangesViewModel.SummaryLimit"/> characters, negative past it (<c>MessageSummaryCounter</c>), as the stage panel's.</summary>
    public string SummaryCounter => (WorkingChangesViewModel.SummaryLimit - Summary.Length).ToString(CultureInfo.InvariantCulture);

    /// <summary>The summary is longer than <see cref="WorkingChangesViewModel.SummaryLimit"/>: the counter takes the warning colour.</summary>
    public bool IsSummaryOverLimit => Summary.Length > WorkingChangesViewModel.SummaryLimit;

    /// <summary>The confirm button is enabled: the summary is not blank.</summary>
    public bool CanConfirm => !string.IsNullOrWhiteSpace(Summary);

    /// <summary>The confirm button's tooltip: <see cref="WriteSummaryReason"/> while it is disabled, null otherwise.</summary>
    public string? ConfirmToolTip => CanConfirm ? null : WriteSummaryReason;

    /// <summary>The message as the dialog would answer it now (<see cref="Join"/>).</summary>
    public string Message => Join(Summary, Description);

    protected override string? CancelledResult => null;

    /// <summary>
    /// A message as the stage panel splits an operation's message (5.3): its first line, after
    /// any blank lines it starts with, is the summary; the rest, without the blank lines that
    /// follow the summary and those at its end, is the description. Line endings become line
    /// feeds and trailing spaces go; no line is removed, a line that starts with <c>#</c> included (D68).
    /// </summary>
    public static (string Summary, string Description) Split(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var lines = message.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .SkipWhile(line => line.Length == 0)
            .ToList();
        if (lines.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        return (lines[0], string.Join('\n', lines.Skip(1)).Trim('\n'));
    }

    /// <summary>
    /// The message a summary and a description make (7.2): <c>{summary}\n\n{description}</c>, or
    /// the summary alone when the description is blank. The description's line ends become line
    /// feeds: a text box types the platform's (<c>\r\n</c> on Windows), git keeps line feeds.
    /// </summary>
    public static string Join(string summary, string description)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(description);
        return string.IsNullOrWhiteSpace(description) ? summary : $"{summary}\n\n{description.ReplaceLineEndings("\n")}";
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => Complete(Message);
}
