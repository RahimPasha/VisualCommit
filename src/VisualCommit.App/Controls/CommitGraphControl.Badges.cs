using System.Globalization;
using Avalonia;
using Avalonia.Media;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Hosting;

namespace VisualCommit.App.Controls;

/// <summary>
/// The pull-request badge (docs/phase-7a-design.md 11.2, D309): a branch with an open pull request
/// carries, inside its label and after its name, Lucide's "git-pull-request" icon and <c>#n</c>, in
/// the label's own colours. The name is cut to make room, never the badge; a row's labels name the
/// pull request in their tooltip too.
/// </summary>
public sealed partial class CommitGraphControl
{
    /// <summary>Space between a label's name and its badge's icon.</summary>
    public const double BadgeGap = 6;

    /// <summary>Space between the badge's icon and its <c>#n</c>.</summary>
    public const double BadgeIconGap = 3;

    /// <summary>
    /// The open pull requests by full ref name (<c>refs/heads/x</c> through an upstream of the same
    /// name, <c>refs/remotes/origin/x</c>): the repository's <c>HostingState.ByRef</c>. Null or empty
    /// draws every label as without hosting.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyDictionary<string, PullRequest>?> PullRequestsProperty =
        AvaloniaProperty.Register<CommitGraphControl, IReadOnlyDictionary<string, PullRequest>?>(nameof(PullRequests));

    /// <summary>The open pull requests by full ref name (11.2); null without hosting.</summary>
    public IReadOnlyDictionary<string, PullRequest>? PullRequests
    {
        get => GetValue(PullRequestsProperty);
        set => SetValue(PullRequestsProperty, value);
    }

    /// <summary>
    /// A new map of pull requests: the labels are laid out again only when a ref's pull request
    /// differs in what a label shows or its tooltip says (number, title, draft), or a ref gained or
    /// lost one. An equal map keeps the row cache, as <see cref="SearchProperty"/>'s handling does.
    /// </summary>
    private void OnPullRequestsChanged(IReadOnlyDictionary<string, PullRequest>? old, IReadOnlyDictionary<string, PullRequest>? now)
    {
        if (SameBadges(old, now))
        {
            return;
        }

        _rowCache.Clear();
        InvalidateVisual();
        UpdateHover();
    }

    private static bool SameBadges(IReadOnlyDictionary<string, PullRequest>? a, IReadOnlyDictionary<string, PullRequest>? b)
    {
        var (left, right) = (a ?? EmptyPullRequests, b ?? EmptyPullRequests);
        return left.Count == right.Count
            && left.All(pair => right.TryGetValue(pair.Key, out var other) && BadgeOf(pair.Value) == BadgeOf(other));
    }

    private static readonly IReadOnlyDictionary<string, PullRequest> EmptyPullRequests = new Dictionary<string, PullRequest>(StringComparer.Ordinal);

    private static PullRequestBadge BadgeOf(PullRequest pull) => new(pull.Number, pull.Title, pull.IsDraft);

    /// <summary>A branch label's pull request (a local, current or remote branch whose ref the map holds), or null.</summary>
    private PullRequest? PullRequestOf(RefLabel label) =>
        label.Kind is RefLabelKind.CurrentBranch or RefLabelKind.LocalBranch or RefLabelKind.RemoteBranch
        && label.RefName is { } refName
        && PullRequests is { Count: > 0 } map
        && map.TryGetValue(refName, out var pull)
            ? pull
            : null;

    /// <summary>
    /// The badge of each of a row's labels: on the first label of each pull request only (local
    /// before remote, as the labels come), so a branch level with its upstream shows one badge.
    /// </summary>
    private PullRequestBadge?[] BadgesOf(IReadOnlyList<RefLabel> labels)
    {
        var badges = new PullRequestBadge?[labels.Count];
        if (PullRequests is not { Count: > 0 })
        {
            return badges;
        }

        var seen = new HashSet<int>();
        for (var i = 0; i < labels.Count; i++)
        {
            if (PullRequestOf(labels[i]) is { } pull && seen.Add(pull.Number))
            {
                badges[i] = BadgeOf(pull);
            }
        }

        return badges;
    }

    /// <summary>The badge's text: <c>#</c> and the number (invariant).</summary>
    private static string BadgeText(int number) => "#" + number.ToString(CultureInfo.InvariantCulture);

    /// <summary>What a badge adds to its label's width: the gap, the icon, the gap and <c>#n</c>; 0 without one.</summary>
    private static double BadgeWidth(PullRequestBadge? badge, Typeface typeface) =>
        badge is null ? 0 : BadgeGap + LabelIconSize + BadgeIconGap + MeasureLabelText(BadgeText(badge.Number), typeface);

    /// <summary>
    /// Draws a label's badge from <paramref name="x"/>, the name's right edge: the icon in the label's
    /// icon pen and <c>#n</c> in its text colour. Without the icon resource its space stays empty.
    /// </summary>
    private static void DrawBadge(DrawingContext context, LabelVisual label, Rect bounds, double x, IPen iconPen, Palette palette)
    {
        if (label.BadgeLayout is not { } layout)
        {
            return;
        }

        x += BadgeGap;
        if (palette.PullRequestIcon is { } icon)
        {
            DrawIcon(context, icon, iconPen, new Point(x, bounds.Y + ((LabelHeight - LabelIconSize) / 2)));
        }

        x += LabelIconSize + BadgeIconGap;
        layout.Draw(context, new Point(x, bounds.Y + ((LabelHeight - layout.Height) / 2)));
    }

    /// <summary>
    /// A label's line in the tooltip: its name, followed when the map holds its ref by
    /// " (pull request #1: Add the login form)" or " (draft pull request #2: …)" (11.2).
    /// </summary>
    private string LabelTipLine(RefLabel label) =>
        PullRequestOf(label) is { } pull
            ? string.Create(CultureInfo.InvariantCulture, $"{label.Text} ({(pull.IsDraft ? "draft pull request" : "pull request")} #{pull.Number}: {pull.Title})")
            : label.Text;
}

/// <summary>The pull request a label's badge stands for (docs/phase-7a-design.md 11.2, D309).</summary>
/// <param name="Number">Its number, drawn as <c>#n</c>.</param>
/// <param name="Title">Its title, which the tooltip names.</param>
/// <param name="IsDraft">It is a draft, which the tooltip says.</param>
public sealed record PullRequestBadge(int Number, string Title, bool IsDraft);
