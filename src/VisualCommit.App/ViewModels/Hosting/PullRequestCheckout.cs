using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels.Hosting;

/// <summary>How a pull request is checked out (docs/phase-7a-design.md 10.5, D305).</summary>
public enum PullRequestCheckoutKind
{
    /// <summary>Its head is a branch of one of the repository's remotes: that remote branch's existing checkout (tracking branch, D93, D201).</summary>
    RemoteBranch,

    /// <summary>A local branch already at its head: that branch's checkout alone.</summary>
    LocalBranch,

    /// <summary><c>refs/pull/&lt;n&gt;/head</c> fetched into <c>FETCH_HEAD</c> from the base's remote, a new branch without upstream at it, then its checkout.</summary>
    FetchHead,
}

/// <summary>
/// The checkout of a pull request as the menu, the double-click and the operation see it (10.5):
/// the item's label, why it is disabled (null when it is not), and what it runs.
/// </summary>
/// <param name="Remote">The remote it fetches from (<see cref="PullRequestCheckoutKind.RemoteBranch"/>'s remote, or the base's).</param>
/// <param name="RemoteRef"><see cref="PullRequestCheckoutKind.RemoteBranch"/>'s <c>refs/remotes/&lt;r&gt;/&lt;head ref&gt;</c>.</param>
/// <param name="LocalBranch">The local branch it checks out (the remote branch's local name, or the one made or reused).</param>
public sealed record PullRequestCheckoutPlan(
    PullRequestCheckoutKind Kind,
    string Label,
    string? DisabledReason,
    string? Remote,
    string? RemoteRef,
    string? LocalBranch);

/// <summary>
/// Plans a pull request's checkout (docs/phase-7a-design.md 10.5, D305), a pure function of the
/// hosting's state and the refs. Remotes are tried in <see cref="HostingRemoteOrder"/>; two
/// repositories are the same when their host and full name are equal ignoring case
/// (<see cref="HostedRepository.Equals(HostedRepository)"/>).
/// </summary>
public static class PullRequestCheckout
{
    private const string HeadsPrefix = "refs/heads/";

    public static PullRequestCheckoutPlan Plan(RepositoryHostingState state, RepoRefs refs, PullRequest pullRequest)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentNullException.ThrowIfNull(pullRequest);
        var remotes = HostingRemoteOrder.Of(state.Remotes.Keys);

        // 1. The head on a remote: the first remote of the head's repository whose remote-tracking
        //    ref of the head's name its fetch lines map from that branch (the user's fork as
        //    origin included); whether the ref exists yet is the operation's to see.
        if (pullRequest.HeadRepository is { } headRepository)
        {
            foreach (var remote in remotes)
            {
                if (!headRepository.Equals(state.Remotes[remote]))
                {
                    continue;
                }

                var remoteRef = $"refs/remotes/{remote}/{pullRequest.HeadRef}";
                var source = refs.SourceOf(remoteRef);
                if (!source.IsBranch || !string.Equals(source.Remote, remote, StringComparison.Ordinal)
                    || !string.Equals(source.RemoteRef, HeadsPrefix + pullRequest.HeadRef, StringComparison.Ordinal)
                    || refs.LocalNameOf(remoteRef) is not { } local)
                {
                    continue;
                }

                return new PullRequestCheckoutPlan(
                    PullRequestCheckoutKind.RemoteBranch,
                    OperationTexts.MenuTexts.CheckOutName(local),
                    IsCurrent(refs, local) ? OperationTexts.MenuTexts.AlreadyCheckedOut(local) : null,
                    remote,
                    remoteRef,
                    local);
            }
        }

        // 2. Otherwise through FETCH_HEAD from the base's remote, under a free local name or a
        //    local branch already at the head. A deleted fork's owner is pr-<n>.
        var owner = pullRequest.HeadRepository?.Owner ?? $"pr-{pullRequest.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var label = OperationTexts.MenuTexts.CheckOutName(pullRequest.HeadRepository is null ? pullRequest.HeadRef : $"{owner}:{pullRequest.HeadRef}");
        var baseRemote = remotes.FirstOrDefault(remote => pullRequest.BaseRepository.Equals(state.Remotes[remote]));
        if (baseRemote is null)
        {
            return new PullRequestCheckoutPlan(PullRequestCheckoutKind.FetchHead, label, OperationTexts.MenuTexts.AddRemoteFor(pullRequest.BaseRepository.FullName), null, null, null);
        }

        var (name, reused) = LocalNameFor(refs, pullRequest, owner);
        return new PullRequestCheckoutPlan(
            reused ? PullRequestCheckoutKind.LocalBranch : PullRequestCheckoutKind.FetchHead,
            label,
            reused && IsCurrent(refs, name) ? OperationTexts.MenuTexts.AlreadyCheckedOut(name) : null,
            baseRemote,
            null,
            name);
    }

    /// <summary>
    /// The first of <c>{head ref}</c>, <c>{owner}-{head ref}</c>, <c>{owner}-{head ref}-2</c>, …
    /// that is no local branch and conflicts with none as a path (<c>a</c> against <c>a/b</c>), or a
    /// local branch at the head (reused). Names are compared ignoring case, as a file system that
    /// ignores it would refuse two of them.
    /// </summary>
    private static (string Name, bool Reused) LocalNameFor(RepoRefs refs, PullRequest pullRequest, string owner)
    {
        var locals = refs.Refs.Where(gitRef => gitRef.Kind == RefKind.LocalBranch).ToList();
        foreach (var candidate in Candidates(pullRequest.HeadRef, owner))
        {
            if (locals.FirstOrDefault(branch => string.Equals(branch.Name, candidate, StringComparison.Ordinal)) is { } existing)
            {
                if (string.Equals(existing.TargetSha, pullRequest.HeadSha, StringComparison.OrdinalIgnoreCase))
                {
                    return (candidate, true);
                }

                continue;
            }

            if (!locals.Any(branch => Conflicts(branch.Name, candidate)))
            {
                return (candidate, false);
            }
        }

        throw new InvalidOperationException("The candidates never end.");
    }

    private static IEnumerable<string> Candidates(string headRef, string owner)
    {
        yield return headRef;
        yield return $"{owner}-{headRef}";
        for (var i = 2; ; i++)
        {
            yield return $"{owner}-{headRef}-{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }
    }

    /// <summary>Whether a branch <paramref name="existing"/> keeps <paramref name="candidate"/> from being made: the same name in another case, or one is a folder of the other.</summary>
    private static bool Conflicts(string existing, string candidate) =>
        string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase)
        || existing.StartsWith(candidate + "/", StringComparison.OrdinalIgnoreCase)
        || candidate.StartsWith(existing + "/", StringComparison.OrdinalIgnoreCase);

    private static bool IsCurrent(RepoRefs refs, string local) => string.Equals(refs.Head.BranchName, local, StringComparison.Ordinal);
}
