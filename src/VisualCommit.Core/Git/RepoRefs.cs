using System.Text;

namespace VisualCommit.Core.Git;

/// <summary>The kinds of reference the graph and the left panel show. Stashes are <see cref="StashEntry"/>.</summary>
public enum RefKind
{
    /// <summary>A branch under <c>refs/heads/</c>.</summary>
    LocalBranch,

    /// <summary>A remote-tracking branch under <c>refs/remotes/</c>. The symbolic <c>refs/remotes/&lt;remote&gt;/HEAD</c> is left out.</summary>
    RemoteBranch,

    /// <summary>A tag under <c>refs/tags/</c>, lightweight or annotated.</summary>
    Tag,
}

/// <summary>A branch, remote branch or tag.</summary>
/// <param name="FullName">The full ref name, such as <c>refs/heads/feature/login</c>.</param>
/// <param name="Name">The name the user sees: <c>feature/login</c>, <c>origin/main</c>, <c>v1.0</c>.</param>
/// <param name="Kind">Which kind of ref it is.</param>
/// <param name="TargetSha">The object it points to, with an annotated tag followed to the object it tags. For a branch, a commit.</param>
/// <param name="RemoteName">For a remote branch, the remote's name (<c>origin</c>); otherwise null.</param>
/// <param name="Upstream">For a local branch with an upstream, the upstream's short name (<c>origin/main</c>); otherwise null.</param>
/// <param name="Ahead">Commits on the local branch that its upstream lacks. 0 without an upstream.</param>
/// <param name="Behind">Commits on the upstream that the local branch lacks. 0 without an upstream.</param>
/// <param name="UpstreamGone">The branch has an upstream configured, but the remote branch no longer exists.</param>
/// <param name="IsHead">This local branch is the one HEAD points to.</param>
public sealed record GitRef(
    string FullName,
    string Name,
    RefKind Kind,
    string TargetSha,
    string? RemoteName = null,
    string? Upstream = null,
    int Ahead = 0,
    int Behind = 0,
    bool UpstreamGone = false,
    bool IsHead = false);

/// <summary>One entry of <c>git stash list</c>.</summary>
/// <param name="Index">Its position: 0 for the newest.</param>
/// <param name="Sha">The stash commit's id.</param>
/// <param name="BaseSha">The commit the stash was made on (the stash commit's first parent).</param>
/// <param name="Message">The stash's message, such as <c>On main: Work in progress</c>.</param>
/// <param name="AuthorName">Who made the stash: the stash commit's author.</param>
/// <param name="AuthorEmail">The stash commit's author's e-mail address.</param>
/// <param name="AuthorDate">The stash commit's author date.</param>
/// <param name="Date">When the stash was made: the stash commit's commit date.</param>
public sealed record StashEntry(
    int Index,
    string Sha,
    string BaseSha,
    string Message,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset AuthorDate,
    DateTimeOffset Date)
{
    /// <summary>The name git gives it: <c>stash@{0}</c>.</summary>
    public string Name => $"stash@{{{Index}}}";
}

/// <summary>Where HEAD is.</summary>
/// <param name="BranchName">The short name of the branch HEAD is on, or null when HEAD is detached.</param>
/// <param name="Sha">The commit HEAD points to, or null when the branch has no commits yet (an unborn branch).</param>
public sealed record HeadState(string? BranchName, string? Sha)
{
    public bool IsDetached => BranchName is null && Sha is not null;

    public bool IsUnborn => Sha is null;
}

/// <summary>
/// Everything about a repository's refs that the views show, read at one moment: HEAD, the
/// branches, remote branches and tags, the stashes and the remotes.
/// </summary>
/// <param name="Head">Where HEAD is.</param>
/// <param name="Refs">Local branches, remote branches and tags, in the order git lists them (by full name).</param>
/// <param name="Stashes">The stash entries, newest first.</param>
/// <param name="Remotes">The names of all configured remotes, including those without remote branches, sorted.</param>
public sealed record RepoRefs(
    HeadState Head,
    IReadOnlyList<GitRef> Refs,
    IReadOnlyList<StashEntry> Stashes,
    IReadOnlyList<string> Remotes)
{
    /// <summary>
    /// A text that changes whenever something the graph depends on changes: HEAD, any ref's
    /// target, or the stashes. Two snapshots with the same fingerprint draw the same graph, so a
    /// refresh compares fingerprints before it loads the commits again (D52).
    /// </summary>
    public string Fingerprint
    {
        get
        {
            var text = new StringBuilder();
            text.Append("HEAD ").Append(Head.BranchName).Append(' ').Append(Head.Sha).Append('\n');
            foreach (var gitRef in Refs)
            {
                text.Append(gitRef.FullName).Append(' ').Append(gitRef.TargetSha).Append('\n');
            }

            foreach (var stash in Stashes)
            {
                text.Append("stash ").Append(stash.Sha).Append('\n');
            }

            return text.ToString();
        }
    }
}
