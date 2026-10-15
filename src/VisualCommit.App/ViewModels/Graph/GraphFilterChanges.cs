using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Core.Session;

namespace VisualCommit.App.ViewModels.Graph;

/// <summary>
/// What hiding and soloing do to a repository's <see cref="RepositoryGraphFilter"/>
/// (docs/phase-5-design.md 8.1, 8.4, D159, D178), as pure functions of the filter and the refs
/// read: a ref is never both hidden and soloed (soloing a hidden ref shows it, hiding a soloed one
/// ends its solo); a local branch's remote-tracking upstream follows it, recorded so that showing
/// it again (or ending its solo) undoes both, unless the upstream was shown or hidden on its own
/// since; a hidden remote hides every branch of it, expanded to its branches at each read. A
/// filter that hides and solos nothing is null (FD14). The path and the time are the store's
/// (<c>GraphFilterStore</c>).
/// </summary>
public static class GraphFilterChanges
{
    private const string HeadsPrefix = "refs/heads/";
    private const string RemotesPrefix = "refs/remotes/";

    /// <summary>What the graph loads for <paramref name="filter"/> (8.2): null when it hides and solos nothing, so that the command is today's.</summary>
    public static GraphFilter? ToGraphFilter(RepositoryGraphFilter? filter)
    {
        if (filter is null || (filter.Hidden.Count == 0 && filter.Soloed.Count == 0))
        {
            return null;
        }

        var graphFilter = new GraphFilter(filter.Hidden, filter.Soloed, filter.Remotes);
        return graphFilter.IsEmpty ? null : graphFilter;
    }

    /// <summary>Whether <paramref name="filter"/> hides or solos anything: an entry of the session at all (FD14).</summary>
    public static bool IsEmpty(RepositoryGraphFilter? filter) =>
        filter is null || (filter.Hidden.Count == 0 && filter.Soloed.Count == 0 && filter.HiddenRemotes.Count == 0);

    /// <summary>"Hide {b}" (8.1): the branch, and a local branch's remote-tracking upstream with it; a soloed one's solo ends.</summary>
    public static RepositoryGraphFilter? Hide(RepositoryGraphFilter? filter, RepoRefs refs, string branchRef)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        var edit = new Edit(filter, refs);
        if (edit.Soloed.Contains(branchRef))
        {
            edit.StopSoloing(branchRef);
        }

        edit.Hidden.Add(branchRef);
        if (UpstreamOf(refs, branchRef) is { } upstream)
        {
            edit.LeaveOnItsOwn(upstream);
            edit.Soloed.Remove(upstream);
            edit.Hidden.Add(upstream);
            edit.Upstreams[branchRef] = upstream;
        }
        else
        {
            edit.Upstreams.Remove(branchRef);
            edit.LeaveOnItsOwn(branchRef);
        }

        return edit.Result();
    }

    /// <summary>"Show {b}" and the eye button (8.1, 8.3): the branch, and the upstream that was hidden with it.</summary>
    public static RepositoryGraphFilter? Show(RepositoryGraphFilter? filter, RepoRefs refs, string branchRef)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        var edit = new Edit(filter, refs);
        edit.Show(branchRef);
        if (branchRef.StartsWith(RemotesPrefix, StringComparison.Ordinal))
        {
            // Shown on its own: the remote is no longer hidden as a whole.
            edit.LeaveOnItsOwn(branchRef);
            edit.HiddenRemotes.Remove(edit.RemoteOf(branchRef));
        }

        return edit.Result();
    }

    /// <summary>"Solo {b}" (8.1): the branch, and a local branch's remote-tracking upstream with it; a hidden one is shown.</summary>
    public static RepositoryGraphFilter? Solo(RepositoryGraphFilter? filter, RepoRefs refs, string branchRef)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        var edit = new Edit(filter, refs);
        if (edit.Hidden.Contains(branchRef))
        {
            edit.Show(branchRef);
        }

        edit.Soloed.Add(branchRef);
        if (UpstreamOf(refs, branchRef) is { } upstream)
        {
            edit.LeaveOnItsOwn(upstream);
            edit.Hidden.Remove(upstream);
            edit.Soloed.Add(upstream);
            edit.Upstreams[branchRef] = upstream;
        }
        else
        {
            edit.Upstreams.Remove(branchRef);
            edit.LeaveOnItsOwn(branchRef);
        }

        return edit.Result();
    }

    /// <summary>"Stop soloing {b}" and the solo button (8.1, 8.3): the branch, and the upstream that was soloed with it.</summary>
    public static RepositoryGraphFilter? StopSoloing(RepositoryGraphFilter? filter, RepoRefs refs, string branchRef)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        var edit = new Edit(filter, refs);
        edit.StopSoloing(branchRef);
        if (branchRef.StartsWith(RemotesPrefix, StringComparison.Ordinal))
        {
            edit.LeaveOnItsOwn(branchRef);
        }

        return edit.Result();
    }

    /// <summary>The sections' "Show all hidden branches" (8.3): nothing hidden, no remote hidden; solo mode stays.</summary>
    public static RepositoryGraphFilter? ShowAllHidden(RepositoryGraphFilter? filter, RepoRefs refs)
    {
        ArgumentNullException.ThrowIfNull(refs);
        var edit = new Edit(filter, refs);
        foreach (var branch in edit.Upstreams.Keys.Where(branch => !edit.Soloed.Contains(branch)).ToList())
        {
            edit.Upstreams.Remove(branch);
        }

        edit.Hidden.Clear();
        edit.HiddenRemotes.Clear();
        return edit.Result();
    }

    /// <summary>The sections' "Stop soloing" (8.3): solo mode ends; what is hidden applies again.</summary>
    public static RepositoryGraphFilter? StopSoloingAll(RepositoryGraphFilter? filter, RepoRefs refs)
    {
        ArgumentNullException.ThrowIfNull(refs);
        var edit = new Edit(filter, refs);
        foreach (var branch in edit.Upstreams.Keys.Where(branch => !edit.Hidden.Contains(branch)).ToList())
        {
            edit.Upstreams.Remove(branch);
        }

        edit.Soloed.Clear();
        return edit.Result();
    }

    /// <summary>"Hide every branch of {r}" (8.1): its branches now, and those a later fetch brings (<see cref="Correct"/>).</summary>
    public static RepositoryGraphFilter? HideRemote(RepositoryGraphFilter? filter, RepoRefs refs, string remote)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);
        var edit = new Edit(filter, refs);
        edit.HiddenRemotes.Add(remote);
        foreach (var branch in BranchesOf(refs, remote))
        {
            edit.LeaveOnItsOwn(branch);
            edit.Soloed.Remove(branch);
            edit.Hidden.Add(branch);
        }

        return edit.Result();
    }

    /// <summary>"Show every branch of {r}" (8.1): the remote and its branches' names go.</summary>
    public static RepositoryGraphFilter? ShowRemote(RepositoryGraphFilter? filter, RepoRefs refs, string remote)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);
        var edit = new Edit(filter, refs);
        edit.HiddenRemotes.Remove(remote);
        foreach (var branch in edit.Hidden.Where(name => edit.IsOfRemote(name, remote)).ToList())
        {
            edit.LeaveOnItsOwn(branch);
            edit.Hidden.Remove(branch);
        }

        return edit.Result();
    }

    /// <summary>
    /// The app renamed a branch (8.4): its entries move to the new name, and an upstream recorded
    /// with it follows. A local branch's rename keeps its upstream's name.
    /// </summary>
    public static RepositoryGraphFilter? Renamed(RepositoryGraphFilter? filter, string oldRef, string newRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(newRef);
        if (filter is null)
        {
            return null;
        }

        static List<string> Moved(IEnumerable<string> names, string from, string to) =>
            [.. names.Select(name => string.Equals(name, from, StringComparison.Ordinal) ? to : name).Distinct(StringComparer.Ordinal)];

        return filter with
        {
            Hidden = Moved(filter.Hidden, oldRef, newRef),
            Soloed = Moved(filter.Soloed, oldRef, newRef),
            Upstreams = [.. filter.Upstreams.Select(pair => string.Equals(pair.Branch, oldRef, StringComparison.Ordinal) ? pair with { Branch = newRef } : pair)],
        };
    }

    /// <summary>The app deleted a branch (8.4): its entries go, and the record of an upstream that came with it.</summary>
    public static RepositoryGraphFilter? Deleted(RepositoryGraphFilter? filter, string branchRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        if (filter is null)
        {
            return null;
        }

        var result = filter with
        {
            Hidden = [.. filter.Hidden.Where(name => !string.Equals(name, branchRef, StringComparison.Ordinal))],
            Soloed = [.. filter.Soloed.Where(name => !string.Equals(name, branchRef, StringComparison.Ordinal))],
            Upstreams = [.. filter.Upstreams.Where(pair => !string.Equals(pair.Branch, branchRef, StringComparison.Ordinal))],
        };
        return IsEmpty(result) ? null : result;
    }

    /// <summary>The app created a branch while solo mode is on (8.1): it is soloed too, so that it does not vanish.</summary>
    public static RepositoryGraphFilter? Created(RepositoryGraphFilter? filter, string branchRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        return filter is null || filter.Soloed.Count == 0 || filter.Soloed.Contains(branchRef, StringComparer.Ordinal)
            ? filter
            : filter with { Soloed = [.. filter.Soloed, branchRef] };
    }

    /// <summary>
    /// The filter as the refs read say it is (8.1, 8.4): a hidden remote's branches are its branches
    /// now (a fetch may have brought more); with <paramref name="dropMissing"/> (a repository's first
    /// read) names that no longer exist are dropped, and solo mode ends when no soloed name exists.
    /// The remotes are the refs' own. The same instance when nothing changed.
    /// </summary>
    public static RepositoryGraphFilter? Correct(RepositoryGraphFilter? filter, RepoRefs refs, bool dropMissing)
    {
        ArgumentNullException.ThrowIfNull(refs);
        if (filter is null)
        {
            return null;
        }

        var edit = new Edit(filter, refs);
        var known = refs.Refs.Select(gitRef => gitRef.FullName).ToHashSet(StringComparer.Ordinal);
        if (dropMissing)
        {
            edit.Hidden.RemoveWhere(name => !known.Contains(name));
            edit.Soloed.RemoveWhere(name => !known.Contains(name));
            edit.HiddenRemotes.RemoveWhere(remote => !refs.Remotes.Contains(remote, StringComparer.Ordinal));
            foreach (var (branch, upstream) in edit.Upstreams.ToList())
            {
                if (!known.Contains(branch) || !known.Contains(upstream))
                {
                    edit.Upstreams.Remove(branch);
                }
            }
        }

        foreach (var remote in edit.HiddenRemotes)
        {
            edit.Hidden.RemoveWhere(name => edit.IsOfRemote(name, remote) && !known.Contains(name));
            edit.Hidden.UnionWith(BranchesOf(refs, remote));
        }

        var corrected = edit.Result();
        return Same(filter, corrected) ? filter : corrected;
    }

    /// <summary>Whether two filters hold the same names (the path and the time aside).</summary>
    public static bool Same(RepositoryGraphFilter? one, RepositoryGraphFilter? other)
    {
        if (one is null || other is null)
        {
            return one is null && other is null;
        }

        static bool SameSet(IEnumerable<string> a, IEnumerable<string> b) => a.ToHashSet(StringComparer.Ordinal).SetEquals(b);

        return SameSet(one.Hidden, other.Hidden)
            && SameSet(one.Soloed, other.Soloed)
            && SameSet(one.HiddenRemotes, other.HiddenRemotes)
            && SameSet(one.Remotes, other.Remotes)
            && one.Upstreams.ToHashSet().SetEquals(other.Upstreams);
    }

    /// <summary>
    /// The remote-tracking upstream that follows a local branch (8.1): <c>refs/remotes/…</c> only, when
    /// it exists; a local upstream (D101) never follows. Null for a remote branch.
    /// </summary>
    public static string? UpstreamOf(RepoRefs refs, string branchRef)
    {
        ArgumentNullException.ThrowIfNull(refs);
        if (!branchRef.StartsWith(HeadsPrefix, StringComparison.Ordinal)
            || refs.Refs.FirstOrDefault(gitRef => string.Equals(gitRef.FullName, branchRef, StringComparison.Ordinal)) is not { } branch
            || branch.HasLocalUpstream
            || branch.UpstreamRef is not { } upstream
            || !upstream.StartsWith(RemotesPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return refs.Refs.Any(gitRef => string.Equals(gitRef.FullName, upstream, StringComparison.Ordinal)) ? upstream : null;
    }

    /// <summary>The full names of the remote's branches as read.</summary>
    private static IEnumerable<string> BranchesOf(RepoRefs refs, string remote) =>
        refs.Refs.Where(gitRef => gitRef.Kind == RefKind.RemoteBranch && string.Equals(gitRef.RemoteName, remote, StringComparison.Ordinal)).Select(gitRef => gitRef.FullName);

    /// <summary>A filter being changed: sets of names, and each local branch's upstream that came with it.</summary>
    private sealed class Edit
    {
        private readonly RepositoryGraphFilter? _original;

        public Edit(RepositoryGraphFilter? filter, RepoRefs refs)
        {
            _original = filter;
            Hidden = new HashSet<string>(filter?.Hidden ?? [], StringComparer.Ordinal);
            Soloed = new HashSet<string>(filter?.Soloed ?? [], StringComparer.Ordinal);
            HiddenRemotes = new HashSet<string>(filter?.HiddenRemotes ?? [], StringComparer.Ordinal);
            Upstreams = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in filter?.Upstreams ?? [])
            {
                Upstreams[pair.Branch] = pair.Upstream;
            }

            Remotes = refs.Remotes;
        }

        public HashSet<string> Hidden { get; }

        public HashSet<string> Soloed { get; }

        public HashSet<string> HiddenRemotes { get; }

        public Dictionary<string, string> Upstreams { get; }

        public IReadOnlyList<string> Remotes { get; }

        /// <summary>Shows a branch and the upstream hidden with it, unless another hidden branch holds it there too.</summary>
        public void Show(string branchRef)
        {
            Hidden.Remove(branchRef);
            if (Upstreams.Remove(branchRef, out var upstream) && !Upstreams.Any(pair => pair.Value == upstream && Hidden.Contains(pair.Key)))
            {
                Hidden.Remove(upstream);
            }
        }

        /// <summary>Ends a branch's solo and that of the upstream soloed with it, unless another soloed branch holds it there too.</summary>
        public void StopSoloing(string branchRef)
        {
            Soloed.Remove(branchRef);
            if (Upstreams.Remove(branchRef, out var upstream) && !Upstreams.Any(pair => pair.Value == upstream && Soloed.Contains(pair.Key)))
            {
                Soloed.Remove(upstream);
            }
        }

        /// <summary>A remote branch shown, hidden or soloed on its own: it no longer follows a local branch.</summary>
        public void LeaveOnItsOwn(string remoteBranchRef)
        {
            foreach (var branch in Upstreams.Where(pair => pair.Value == remoteBranchRef).Select(pair => pair.Key).ToList())
            {
                Upstreams.Remove(branch);
            }
        }

        /// <summary>The remote a remote branch belongs to: the longest remote name it starts with (0.5).</summary>
        public string RemoteOf(string remoteBranchRef) => GraphFilter.RemoteOf(remoteBranchRef[RemotesPrefix.Length..], Remotes);

        public bool IsOfRemote(string name, string remote) =>
            name.StartsWith(RemotesPrefix, StringComparison.Ordinal) && string.Equals(RemoteOf(name), remote, StringComparison.Ordinal);

        /// <summary>The changed filter, its names sorted; null when it hides and solos nothing (FD14).</summary>
        public RepositoryGraphFilter? Result()
        {
            var result = new RepositoryGraphFilter(
                _original?.RepositoryPath ?? string.Empty,
                [.. Hidden.Order(StringComparer.Ordinal)],
                [.. Soloed.Order(StringComparer.Ordinal)],
                [.. HiddenRemotes.Order(StringComparer.Ordinal)],
                [.. Upstreams.Where(pair => Hidden.Contains(pair.Key) || Soloed.Contains(pair.Key)).OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new BranchUpstream(pair.Key, pair.Value))],
                [.. Remotes],
                _original?.LastUsed ?? default);
            return IsEmpty(result) ? null : result;
        }
    }
}
