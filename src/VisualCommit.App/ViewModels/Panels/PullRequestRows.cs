using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels.Panels;

/// <summary>What a message row of the "Pull requests" section says (docs/phase-7a-design.md 10.3).</summary>
public enum PullRequestMessageKind
{
    /// <summary>Signed out (D257): a click opens "Add GitHub account" (8.5).</summary>
    SignIn,

    Loading,

    /// <summary>GitHub's failure in <c>HostingTexts.Describe</c>'s words (D338).</summary>
    Failed,

    /// <summary>No open pull requests.</summary>
    None,

    /// <summary>More than 300 (ruling 14): a click opens the list in the browser.</summary>
    More,
}

/// <summary>
/// An open pull request under the "Pull requests" section (docs/phase-7a-design.md 10.3; D303,
/// D304): <c>#1</c>, the title, cut first, the author and, for a draft, "Draft". A click selects
/// its head commit, a double-click checks it out, a right-click opens its menu.
/// </summary>
public sealed partial class PullRequestRow : RefListRow
{
    /// <summary>The prefix of a pull request's <see cref="RefListRow.Key"/>: <c>pr:1</c>.</summary>
    public const string KeyPrefix = "pr:";

    internal PullRequestRow(PullRequest pullRequest, Action<RefListRow> activate)
        : base(KeyOf(pullRequest.Number), OperationTexts.PullRequestHeader(pullRequest.Number, pullRequest.Title), 1, isOpen: false, activate)
    {
        PullRequest = pullRequest;
        HeadLabel = HeadLabelOf(pullRequest);
        ToolTip = OperationTexts.PullRequestToolTip(pullRequest.Number, pullRequest.Title, pullRequest.Author, HeadLabel, pullRequest.BaseRef, pullRequest.IsDraft);
    }

    public PullRequest PullRequest { get; }

    public int Number => PullRequest.Number;

    public string NumberText => "#" + Number.ToString(CultureInfo.InvariantCulture);

    public string Title => PullRequest.Title;

    /// <summary>GitHub's login of the author; empty when GitHub gave none.</summary>
    public string Author => PullRequest.Author ?? string.Empty;

    public bool IsDraft => PullRequest.IsDraft;

    public string DraftText => OperationTexts.PullRequestDraft;

    /// <summary>
    /// The head as GitHub names it: the branch (<c>feature/login</c>) of the same repository,
    /// <c>{owner}:{branch}</c> of a fork (<c>ada:fix-typo</c>), or the branch alone when the fork was
    /// deleted.
    /// </summary>
    public string HeadLabel { get; }

    /// <summary>The head commit, which a click selects (D304).</summary>
    public string TargetSha => PullRequest.HeadSha;

    /// <summary>The header, a line break, <c>{author} · {head label} → {base ref}</c> and, for a draft, <c> · Draft</c>.</summary>
    public string ToolTip { get; }

    /// <summary>The pull request the user clicked last: drawn with the selection background.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>The key of pull request <paramref name="number"/>'s row.</summary>
    public static string KeyOf(int number) => KeyPrefix + number.ToString(CultureInfo.InvariantCulture);

    /// <summary>The head's label (<see cref="HeadLabel"/>), which the filter also matches.</summary>
    public static string HeadLabelOf(PullRequest pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        return pullRequest.HeadRepository is { } head && pullRequest.IsFromFork ? $"{head.Owner}:{pullRequest.HeadRef}" : pullRequest.HeadRef;
    }

    public override bool ShowsSameAs(RefListRow other) =>
        other is PullRequestRow row
        && row.Key == Key && row.Text == Text && row.Depth == Depth
        && row.Number == Number && row.Title == Title && row.Author == Author && row.IsDraft == IsDraft
        && row.HeadLabel == HeadLabel && row.TargetSha == TargetSha && row.ToolTip == ToolTip
        && row.IsSelected == IsSelected;
}

/// <summary>
/// A message under the "Pull requests" section (docs/phase-7a-design.md 10.3): signed out,
/// loading, a failure, none open, or more than 300. Only the sign-in and the "more" rows react to
/// a click (<see cref="CanActivate"/>).
/// </summary>
public sealed class PullRequestMessageRow : RefListRow
{
    internal PullRequestMessageRow(PullRequestMessageKind kind, string text, string toolTip, Action<RefListRow> activate)
        : base("pr-message:" + kind, text, 1, isOpen: false, activate, canActivate: kind is PullRequestMessageKind.SignIn or PullRequestMessageKind.More)
    {
        Kind = kind;
        ToolTip = toolTip;
    }

    public PullRequestMessageKind Kind { get; }

    /// <summary>A click starts something: signing in, or the list in the browser.</summary>
    public bool CanActivate => Kind is PullRequestMessageKind.SignIn or PullRequestMessageKind.More;

    /// <summary>
    /// The row's text whole, since the row cuts it with "…"; a failure's adds GitHub's
    /// <see cref="HostingFailure.Detail"/> on a second line; the "more" row's says what a click does.
    /// </summary>
    public string ToolTip { get; }

    public override bool ShowsSameAs(RefListRow other) =>
        other is PullRequestMessageRow row
        && row.Key == Key && row.Kind == Kind && row.Text == Text && row.Depth == Depth && row.ToolTip == ToolTip;
}
