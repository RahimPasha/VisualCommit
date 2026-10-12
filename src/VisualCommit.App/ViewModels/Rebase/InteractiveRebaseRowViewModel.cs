using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Rebase;

/// <summary>
/// Which part of a join line (docs/phase-4-design.md 2.2) a row of the interactive list draws: the
/// 2-px line at x 12 that runs from a squash or fixup row's vertical centre to the top edge of its
/// target row. Each row draws its own part, so that a virtualising list draws the whole line.
/// </summary>
public enum RebaseJoinLine
{
    /// <summary>No line crosses the row: a row that joins nothing and lies outside every group, or a target (the line ends at its top edge).</summary>
    None,

    /// <summary>A squash or fixup row with no line above it: the line starts at its vertical centre and runs to its bottom edge.</summary>
    FromCentre,

    /// <summary>A row the line passes: a member below another member of its group, or a dropped row between a member and its target.</summary>
    Through,
}

/// <summary>
/// One commit row of the interactive rebase's list (docs/phase-4-design.md 2.2): the commit, its
/// action, how it is drawn (indented, struck through, the join line's part, the second line), and
/// a reword's or a group's new message. The list (<see cref="InteractiveRebaseViewModel"/>) sets
/// everything but the commit; a row never changes by itself.
/// </summary>
public sealed partial class InteractiveRebaseRowViewModel : ObservableObject
{
    /// <summary>How far a squash or fixup row's whole content (its grip, button, SHA and subject) stands to the right (2.2).</summary>
    public const double JoinedIndent = 16;

    internal InteractiveRebaseRowViewModel(RebaseListEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entry = entry;
    }

    /// <summary>The commit as the list read it (2.3).</summary>
    public RebaseListEntry Entry { get; }

    /// <summary>The commit's full id.</summary>
    public string Sha => Entry.Sha;

    /// <summary>The first 7 characters of its id, as the row shows it.</summary>
    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;

    /// <summary>The commit's subject.</summary>
    public string Subject => Entry.Subject;

    /// <summary>The commit's author's name (the author column, shown only when the list is at least 520 wide).</summary>
    public string AuthorName => Entry.AuthorName;

    /// <summary>
    /// <c>{short} {subject}</c>: the row's name for UI Automation (<c>RebaseRow</c>, 7.4) and its
    /// menu's header (2.2, 7.1).
    /// </summary>
    public string Name => $"{ShortSha} {Subject}";

    /// <summary>What the rebase does with the commit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionText), nameof(IsJoined), nameof(IsDropped), nameof(Indent))]
    public partial RebaseAction Action { get; internal set; }

    /// <summary>The action button's text: <c>Pick</c>, <c>Reword</c>, <c>Squash</c>, <c>Fixup</c> or <c>Drop</c> (14.3).</summary>
    public string ActionText => InteractiveRebaseViewModel.ActionName(Action);

    /// <summary>A squash or fixup row: it joins its <see cref="JoinTarget"/> and stands 16 to the right.</summary>
    public bool IsJoined => Action is RebaseAction.Squash or RebaseAction.Fixup;

    /// <summary>A dropped row: its SHA and subject are struck through in the disabled colour.</summary>
    public bool IsDropped => Action == RebaseAction.Drop;

    /// <summary>How far the row's content stands to the right: <see cref="JoinedIndent"/> for a squash or fixup row, 0 otherwise.</summary>
    public double Indent => IsJoined ? JoinedIndent : 0;

    /// <summary>The selected row: the selection background, the keyboard focus, and the commit the right panel shows.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; internal set; }

    /// <summary>The row being dragged: drawn in its place at half opacity until the drop (2.2).</summary>
    [ObservableProperty]
    public partial bool IsDragged { get; internal set; }

    /// <summary>The part of a join line the row draws.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJoinLineFromCentre), nameof(IsJoinLineThrough))]
    public partial RebaseJoinLine JoinLine { get; internal set; }

    /// <summary>The join line starts at the row's vertical centre.</summary>
    public bool IsJoinLineFromCentre => JoinLine == RebaseJoinLine.FromCentre;

    /// <summary>The join line crosses the whole row.</summary>
    public bool IsJoinLineThrough => JoinLine == RebaseJoinLine.Through;

    /// <summary>For a squash or fixup row, the row it joins: the nearest Pick or Reword below it; null otherwise.</summary>
    [ObservableProperty]
    public partial InteractiveRebaseRowViewModel? JoinTarget { get; internal set; }

    /// <summary>
    /// The second line under the row (<c>RebaseRowSecondLine</c>, 22 high), or null for none:
    /// <c>Message: {subject}</c> for the target of a group with a squash, <c>New message:
    /// {subject}</c> for a reworded row (2.2).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSecondLine))]
    public partial string? SecondLine { get; internal set; }

    /// <summary>The row has a second line, with its <c>Edit…</c> button.</summary>
    public bool HasSecondLine => SecondLine is not null;

    /// <summary>A reworded row's new message, as the message dialog joined it; null for any other action.</summary>
    public string? NewMessage { get; internal set; }

    /// <summary>
    /// The message of the group this row is the target of, as the user edited it with
    /// <c>Edit…</c>; null while it is git's own. Kept while the row stays the target of a group
    /// with a squash (2.2).
    /// </summary>
    public string? EditedGroupMessage { get; internal set; }

    /// <summary>The row is the target of a group that has at least one squash: the group has a message of its own.</summary>
    public bool IsSquashTarget { get; internal set; }

    /// <summary>The message the commit gets from a reword, or its own: what a group it is the target of starts from (2.2).</summary>
    internal string OwnOrNewMessage => NewMessage ?? Entry.Message;

    /// <summary>The first line of a message, as the message dialog takes its summary.</summary>
    internal static string SubjectOf(string message) => MessageDialogViewModel.Split(message).Summary;

    partial void OnActionChanged(RebaseAction value)
    {
        if (value != RebaseAction.Reword)
        {
            NewMessage = null;
        }
    }
}

/// <summary>The base row under the list's commits (2.2, <c>RebaseBaseRow</c>): the commit the rebase goes onto. Not selectable, never moved.</summary>
public sealed class InteractiveRebaseBaseRow
{
    internal InteractiveRebaseBaseRow(string sha, string subject)
    {
        Sha = sha;
        Subject = subject;
    }

    /// <summary>The word in the action button's place, in the secondary colour.</summary>
    public const string Label = "Base";

    /// <summary>The base commit's full id.</summary>
    public string Sha { get; }

    /// <summary>The first 7 characters of its id.</summary>
    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;

    /// <summary>The base commit's subject (empty when the graph had not read it).</summary>
    public string Subject { get; }

    /// <summary>The base row's name for UI Automation: <c>Base {short} {subject}</c>.</summary>
    public string Name => $"{Label} {ShortSha} {Subject}".TrimEnd();
}
