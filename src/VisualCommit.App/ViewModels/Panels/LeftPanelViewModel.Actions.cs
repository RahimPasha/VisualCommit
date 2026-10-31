namespace VisualCommit.App.ViewModels.Panels;

/// <summary>What a left-panel row stands for when a menu or a double-click acts on it (docs/phase-3-design.md 4b, 4c).</summary>
public enum RefRowTarget
{
    /// <summary>
    /// Nothing a menu or a double-click acts on: a folder, the "Pull requests" placeholder of a
    /// repository without a GitHub remote, a pull-request message row.
    /// </summary>
    None,

    /// <summary>
    /// A section header with a menu of its own (4b): Local branches, Remotes, Tags, Stashes, and
    /// "Pull requests" with a GitHub remote (docs/phase-7a-design.md 10.4).
    /// </summary>
    Section,

    Remote,
    LocalBranch,
    RemoteBranch,
    Tag,
    Stash,

    /// <summary>An open pull request's row (docs/phase-7a-design.md 10.3, 10.4).</summary>
    PullRequest,
}

/// <summary>
/// The left panel's rows as the menus and the double-clicks see them (docs/phase-3-design.md 4b,
/// 4c, D114): which object a row stands for and its full name. A right-click or a double-click
/// changes nothing in the panel itself: it selects and highlights no row.
/// </summary>
public partial class LeftPanelViewModel
{
    private const string RefKeyPrefix = "ref:";

    /// <summary>What <paramref name="row"/> stands for: a section with a menu, a remote, a branch, a tag, a stash, or nothing.</summary>
    public static RefRowTarget TargetOf(RefListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row switch
        {
            RefSectionRow { Section: RefSection.PullRequests, CanToggle: false } => RefRowTarget.None,
            RefSectionRow { CanToggle: true } => RefRowTarget.Section,
            RefItemRow { Kind: RefItemKind.Remote } => RefRowTarget.Remote,
            RefItemRow { Kind: RefItemKind.LocalBranch } item when RefNameOf(item) is not null => RefRowTarget.LocalBranch,
            RefItemRow { Kind: RefItemKind.RemoteBranch } item when RefNameOf(item) is not null => RefRowTarget.RemoteBranch,
            RefItemRow { Kind: RefItemKind.Tag } item when RefNameOf(item) is not null => RefRowTarget.Tag,
            RefItemRow { Kind: RefItemKind.Stash, TargetSha: not null } => RefRowTarget.Stash,
            PullRequestRow => RefRowTarget.PullRequest,
            _ => RefRowTarget.None,
        };
    }

    /// <summary>The full ref name of a branch, remote branch or tag row (<c>refs/heads/feature/alpha</c>), or null for any other row.</summary>
    public static string? RefNameOf(RefListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row is RefItemRow { Kind: RefItemKind.LocalBranch or RefItemKind.RemoteBranch or RefItemKind.Tag } item
            && item.Key.StartsWith(RefKeyPrefix, StringComparison.Ordinal)
                ? item.Key[RefKeyPrefix.Length..]
                : null;
    }

    /// <summary>The name of a remote's row (<c>origin</c>), or null for any other row.</summary>
    public static string? RemoteNameOf(RefListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row is RefItemRow { Kind: RefItemKind.Remote } item ? item.Text : null;
    }
}
