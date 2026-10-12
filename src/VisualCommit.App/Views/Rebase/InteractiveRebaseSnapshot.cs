using Avalonia;
using Avalonia.Media;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Git;

namespace VisualCommit.App.Views.Rebase;

/// <summary>
/// The interactive list as <see cref="InteractiveRebaseView"/> draws it now (its test surface,
/// docs/phase-4-design.md 13.2): what the scripted checks and the view tests read instead of
/// pixels. Bounds are in the coordinates of the visual the snapshot was taken for (the window,
/// for the driver), and null for what is not drawn (a row the virtualising list has not
/// realised, a line that is not shown).
/// </summary>
/// <param name="Header">The header's text (<c>RebaseListHeader</c>), whole.</param>
/// <param name="HeaderToolTip">The header's tooltip: its whole text while it is cut with "…", null while it fits.</param>
/// <param name="Count">The count's text (<c>RebaseListCount</c>).</param>
/// <param name="CountShown">Whether the count is shown (not for a list that cannot be made).</param>
/// <param name="Message">The text that stands in the rows' place (<c>RebaseListMessage</c>), or null.</param>
/// <param name="Rows">The commit rows, top to bottom.</param>
/// <param name="BaseRow">The base row, or null for a list that cannot be made.</param>
/// <param name="JoinLines">Each squash or fixup row's join line, top to bottom, while both ends are drawn.</param>
/// <param name="DragGap">During a row drag, the gap the line marks (gap k is above row k; the row count is the gap above the base row); null otherwise.</param>
/// <param name="DragLine">During a row drag, the 2-px line on that gap; null otherwise.</param>
/// <param name="Bounds">The whole list.</param>
/// <param name="HeaderBounds">The header bar (36 high).</param>
/// <param name="FooterBounds">The footer (44 high).</param>
/// <param name="CanStart">Whether <c>Start rebase</c> is enabled.</param>
/// <param name="StartToolTip">Start's tooltip (its disabled reason), or null.</param>
/// <param name="StartShown">Whether <c>Start rebase</c> is shown (not for a list that cannot be made).</param>
/// <param name="StartBounds">Where <c>Start rebase</c> is.</param>
/// <param name="CancelBounds">Where <c>Cancel</c> is.</param>
/// <param name="IsInteractive">Whether the rows and buttons take input (not while Start's operation waits or runs).</param>
/// <param name="ShowsAuthor">Whether the author column is shown (the list is at least 520 wide).</param>
public sealed record InteractiveRebaseSnapshot(
    string Header,
    string? HeaderToolTip,
    string Count,
    bool CountShown,
    string? Message,
    IReadOnlyList<InteractiveRebaseRowSnapshot> Rows,
    InteractiveRebaseBaseRowSnapshot? BaseRow,
    IReadOnlyList<InteractiveRebaseJoinLineSnapshot> JoinLines,
    int? DragGap,
    Rect? DragLine,
    Rect Bounds,
    Rect HeaderBounds,
    Rect FooterBounds,
    bool CanStart,
    string? StartToolTip,
    bool StartShown,
    Rect StartBounds,
    Rect CancelBounds,
    bool IsInteractive,
    bool ShowsAuthor)
{
    /// <summary>The row of the commit whose subject is <paramref name="subject"/>. Throws when there is none or more than one.</summary>
    public InteractiveRebaseRowSnapshot Row(string subject) =>
        Rows.Where(row => row.Subject == subject).ToList() switch
        {
            [var row] => row,
            [] => throw new InvalidOperationException($"The list has no row \"{subject}\"; it has: {string.Join(", ", Rows.Select(row => row.Subject))}."),
            _ => throw new InvalidOperationException($"The list has more than one row \"{subject}\"."),
        };

    /// <summary>The selected row, or null.</summary>
    public InteractiveRebaseRowSnapshot? Selected => Rows.SingleOrDefault(row => row.IsSelected);

    /// <summary>The rows' subjects, top to bottom.</summary>
    public IReadOnlyList<string> Subjects => [.. Rows.Select(row => row.Subject)];
}

/// <summary>One commit row as it is drawn.</summary>
/// <param name="Index">Its place from the top, from 0.</param>
/// <param name="Sha">The commit's full id.</param>
/// <param name="ShortSha">The short SHA the row shows.</param>
/// <param name="Subject">The subject, whole.</param>
/// <param name="Name">The row's UI Automation name (<c>RebaseRow</c>): <c>{short} {subject}</c>.</param>
/// <param name="Action">Its action.</param>
/// <param name="ActionText">The action button's text as drawn.</param>
/// <param name="Indent">How far its content stands to the right: 16 for a squash or fixup row, 0 otherwise.</param>
/// <param name="Opacity">Its opacity: 0.5 while it is dragged, 1 otherwise.</param>
/// <param name="IsStruckThrough">Its SHA and subject are struck through (a dropped row).</param>
/// <param name="IsSelected">It is the selected row.</param>
/// <param name="HasKeyboardFocus">It has the keyboard focus.</param>
/// <param name="SecondLine">Its second line's text (<c>RebaseRowSecondLine</c>), or null for none.</param>
/// <param name="JoinTargetSha">For a squash or fixup row, the full id of the row it joins; null otherwise.</param>
/// <param name="JoinLine">The part of a join line drawn in the row.</param>
/// <param name="Bounds">The 30-high row, or null when it is not realised.</param>
/// <param name="GripBounds">The grip icon (14 × 14).</param>
/// <param name="ActionButtonBounds">The action button (84 × 22).</param>
/// <param name="ShaBounds">The short SHA's text block (64 wide).</param>
/// <param name="SubjectBounds">The subject's text block.</param>
/// <param name="AuthorBounds">The author's text block (140 wide), or null while the author column is hidden.</param>
/// <param name="SecondLineBounds">The second line (22 high), or null for none.</param>
/// <param name="EditButtonBounds">The second line's <c>Edit…</c> button, or null for none.</param>
/// <param name="Background">The row's background colour: the selection's, the hover's, or null when transparent.</param>
/// <param name="ShaColor">The colour the short SHA is drawn in.</param>
/// <param name="SubjectColor">The colour the subject is drawn in.</param>
/// <param name="GripColor">The colour the grip is drawn in.</param>
/// <param name="SubjectToolTip">The subject's tooltip: its whole text while it is cut with "…", null while it fits.</param>
public sealed record InteractiveRebaseRowSnapshot(
    int Index,
    string Sha,
    string ShortSha,
    string Subject,
    string Name,
    RebaseAction Action,
    string ActionText,
    double Indent,
    double Opacity,
    bool IsStruckThrough,
    bool IsSelected,
    bool HasKeyboardFocus,
    string? SecondLine,
    string? JoinTargetSha,
    RebaseJoinLine JoinLine,
    Rect? Bounds,
    Rect? GripBounds,
    Rect? ActionButtonBounds,
    Rect? ShaBounds,
    Rect? SubjectBounds,
    Rect? AuthorBounds,
    Rect? SecondLineBounds,
    Rect? EditButtonBounds,
    Color? Background,
    Color? ShaColor,
    Color? SubjectColor,
    Color? GripColor,
    string? SubjectToolTip);

/// <summary>The base row as it is drawn.</summary>
/// <param name="ShortSha">The base commit's short SHA.</param>
/// <param name="Subject">Its subject.</param>
/// <param name="Name">The row's UI Automation name (<c>RebaseBaseRow</c>).</param>
/// <param name="Bounds">The 30-high row, or null when it is not realised.</param>
/// <param name="LabelColor">The colour <c>Base</c> is drawn in.</param>
public sealed record InteractiveRebaseBaseRowSnapshot(string ShortSha, string Subject, string Name, Rect? Bounds, Color? LabelColor);

/// <summary>A join line (2.2): from a squash or fixup row's vertical centre to the top edge of its target row.</summary>
/// <param name="MemberSha">The squash or fixup row's commit.</param>
/// <param name="TargetSha">The commit it joins.</param>
/// <param name="Bounds">The line: 2 wide at x 12 of the list, from the member's centre to the target's top edge.</param>
/// <param name="Color">The colour it is drawn in.</param>
public sealed record InteractiveRebaseJoinLineSnapshot(string MemberSha, string TargetSha, Rect Bounds, Color? Color);
