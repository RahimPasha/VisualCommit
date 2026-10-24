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
/// <param name="UpstreamRef">
/// For a local branch with an upstream, the upstream's full ref (<c>%(upstream)</c>):
/// <c>refs/remotes/origin/main</c>, or <c>refs/heads/main</c> for a local upstream. Null without
/// an upstream (D101). Leases, backups and fast-forwards use it.
/// </param>
/// <param name="UpstreamRemote">
/// For a local branch with an upstream, its remote's name (<c>%(upstream:remotename)</c>):
/// <c>origin</c>, or <c>.</c> for a local upstream (<see cref="HasLocalUpstream"/>). Null without one.
/// </param>
/// <param name="UpstreamRemoteRef">
/// For a local branch with an upstream, the branch's name on the remote
/// (<c>%(upstream:remoteref)</c>), such as <c>refs/heads/main</c>. Null without one.
/// </param>
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
    bool IsHead = false,
    string? UpstreamRef = null,
    string? UpstreamRemote = null,
    string? UpstreamRemoteRef = null)
{
    /// <summary>The upstream is a branch of this repository (<c>branch.&lt;b&gt;.remote = .</c>): no push, force push or remote deletion (D101).</summary>
    public bool HasLocalUpstream => UpstreamRemote == ".";
}

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
    private const string RemotesPrefix = "refs/remotes/";
    private const string HeadsPrefix = "refs/heads/";

    /// <summary>
    /// Every remote's fetch lines as the refs read read them (<c>remote.&lt;r&gt;.fetch</c>,
    /// docs/phase-6-design.md 10.2); null when they were not read: the fakes, and every snapshot a
    /// test builds by hand, which then keep phase 5's assumption (<see cref="SourceOf(string)"/>).
    /// </summary>
    public FetchRefspecs? Refspecs { get; init; }

    /// <summary>
    /// Where the remote-tracking ref <paramref name="trackingRef"/> (<c>refs/remotes/…</c>) is
    /// fetched from (10.1): <see cref="FetchRefspecs.SourceOf"/> of <see cref="Refspecs"/>. While
    /// <see cref="Refspecs"/> is null, exactly phase 5's assumption: Mapped to its remote, the
    /// longest of <see cref="Remotes"/> its name starts with (else its first part), and
    /// <c>refs/heads/&lt;the name less "&lt;remote&gt;/"&gt;</c>.
    /// </summary>
    public RemoteSource SourceOf(string trackingRef)
    {
        ArgumentNullException.ThrowIfNull(trackingRef);
        if (Refspecs is { } refspecs)
        {
            return refspecs.SourceOf(trackingRef);
        }

        if (!trackingRef.StartsWith(RemotesPrefix, StringComparison.Ordinal) || trackingRef.Length == RemotesPrefix.Length)
        {
            return new RemoteSource(RemoteSourceKind.NotFetched, null, null, []);
        }

        var name = trackingRef[RemotesPrefix.Length..];
        return AssumedSource(name, RemoteBranchNames.RemoteOf(name, Remotes));
    }

    /// <summary>
    /// Where the remote branch <paramref name="remoteBranch"/> is fetched from (10.1): as
    /// <see cref="SourceOf(string)"/> of its full name, and while <see cref="Refspecs"/> is null
    /// phase 5's assumption from its own <see cref="GitRef.RemoteName"/>. A ref that is not a
    /// remote branch, or one without a remote, is <see cref="RemoteSourceKind.NotFetched"/>.
    /// </summary>
    public RemoteSource SourceOf(GitRef remoteBranch)
    {
        ArgumentNullException.ThrowIfNull(remoteBranch);
        if (remoteBranch.Kind != RefKind.RemoteBranch)
        {
            return new RemoteSource(RemoteSourceKind.NotFetched, null, null, []);
        }

        if (Refspecs is { } refspecs)
        {
            return refspecs.SourceOf(remoteBranch.FullName);
        }

        return remoteBranch.RemoteName is { } remote
            ? AssumedSource(remoteBranch.Name, remote)
            : new RemoteSource(RemoteSourceKind.NotFetched, null, null, []);
    }

    /// <summary>
    /// The local branch a remote branch checks out into (docs/phase-6-design.md 10.6, amending
    /// D93): its branch name on the remote when it is mapped from one; otherwise, for a mapped ref
    /// that is no branch and for one fetched from two refs of one remote, today's rule
    /// (<see cref="GitRef.Name"/> less <c>&lt;RemoteName&gt;/</c>); null for one two remotes fetch,
    /// one no line fetches, and a ref that is not a remote branch. With <see cref="Refspecs"/> null
    /// it is phase 5's name.
    /// </summary>
    public string? LocalNameOf(GitRef remoteBranch)
    {
        ArgumentNullException.ThrowIfNull(remoteBranch);
        if (remoteBranch.Kind != RefKind.RemoteBranch)
        {
            return null;
        }

        return LocalNameFrom(SourceOf(remoteBranch), remoteBranch.Name, remoteBranch.RemoteName);
    }

    /// <summary>
    /// <see cref="LocalNameOf(GitRef)"/> of a remote-tracking ref by its full name, for one the
    /// snapshot may not hold (fetched since it was read): how the fetch lines map it
    /// (<see cref="SourceOf(string)"/>), today's rule taking off the prefix of the remote that
    /// fills it; with <see cref="Refspecs"/> null phase 5's name. Null for a ref outside
    /// <c>refs/remotes/</c>.
    /// </summary>
    public string? LocalNameOf(string trackingRef)
    {
        ArgumentNullException.ThrowIfNull(trackingRef);
        if (!trackingRef.StartsWith(RemotesPrefix, StringComparison.Ordinal) || trackingRef.Length == RemotesPrefix.Length)
        {
            return null;
        }

        var source = SourceOf(trackingRef);
        return LocalNameFrom(source, trackingRef[RemotesPrefix.Length..], source.Remote);
    }

    /// <summary>10.6's rule: the branch's name on the remote; today's (<paramref name="name"/> less <c>&lt;remote&gt;/</c>) for a mapped ref that is no branch and one fetched from two refs; null otherwise.</summary>
    private static string? LocalNameFrom(RemoteSource source, string name, string? remote) => source switch
    {
        { IsBranch: true } => source.Branch,
        { Kind: RemoteSourceKind.Mapped or RemoteSourceKind.AmbiguousRefs } => RemoteBranchNames.WithoutRemote(name, remote),
        _ => null,
    };

    /// <summary>Phase 5's assumption for a remote branch named <paramref name="name"/> of <paramref name="remote"/>: the default fetch line's mapping.</summary>
    private static RemoteSource AssumedSource(string name, string remote) =>
        new(RemoteSourceKind.Mapped, remote, HeadsPrefix + RemoteBranchNames.WithoutRemote(name, remote), []);

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
