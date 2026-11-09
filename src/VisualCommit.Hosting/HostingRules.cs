using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;

namespace VisualCommit.Hosting;

/// <summary>
/// The hosting session's rules for one repository (docs/phase-7a-design.md 7.8, docs/phase-7b-design.md
/// 9.6, 9.7): which remotes a provider recognises, which of them is listed, which URLs are bound to an
/// account, and which refs have an open pull request. Pure functions, without git and without the network.
/// </summary>
public static class HostingRules
{
    private const string HeadsPrefix = "refs/heads/";

    /// <summary>
    /// Each remote recognised (7.8 step 2): by its first fetch URL after the user's rewrites
    /// (<paramref name="effective"/>), else by its first configured <c>url</c>. A remote neither
    /// names is left out. SSH URLs are recognised (the API works by owner and name) but never bound.
    /// </summary>
    /// <param name="configured">The remotes' URLs as configured (<see cref="RepoRefs.RemoteUrls"/>).</param>
    /// <param name="effective">The same after git's rewrites (<see cref="UrlRewrites.Apply"/>).</param>
    /// <param name="recognise">The service's recognition of one URL, without the network.</param>
    public static IReadOnlyDictionary<string, HostedRepository> Remotes(
        IReadOnlyList<RemoteUrl> configured,
        IReadOnlyList<RemoteEffectiveUrls> effective,
        Func<string, HostedRepository?> recognise)
    {
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(effective);
        ArgumentNullException.ThrowIfNull(recognise);
        var remotes = new Dictionary<string, HostedRepository>(StringComparer.Ordinal);
        var names = effective.Select(remote => remote.Remote)
            .Concat(configured.Select(url => url.Remote))
            .Distinct(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var fetch = effective.FirstOrDefault(remote => remote.Remote == name)?.Fetch.FirstOrDefault();
            var url = configured.FirstOrDefault(candidate => candidate.Remote == name && !candidate.IsPushUrl)?.Url;
            var repository = (fetch is null ? null : recognise(fetch)) ?? (url is null ? null : recognise(url));
            if (repository is not null)
            {
                remotes[name] = repository;
            }
        }

        return remotes;
    }

    /// <summary>The repository whose pull requests are listed before A3 answers (0.2): the first recognised remote's in <see cref="HostingRemoteOrder"/>, of any provider (ruling 5); null without one.</summary>
    public static HostedRepository? ListedCandidate(IReadOnlyDictionary<string, HostedRepository> remotes)
    {
        ArgumentNullException.ThrowIfNull(remotes);
        return HostingRemoteOrder.Of(remotes.Keys) is [var first, ..] ? remotes[first] : null;
    }

    /// <summary>
    /// The URLs bound to the account (7.8, D290) by 7a's rule on <paramref name="webBase"/>: the
    /// coverage form with <c>key => new GitCoverage(key, webBase)</c>, which <see cref="CredentialBinding.Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/>
    /// reads byte for byte as 7a's (D278).
    /// </summary>
    public static IReadOnlyList<CredentialBinding> Bindings(
        AccountStatus? account,
        bool hasToken,
        bool rulesKnown,
        IReadOnlyList<RemoteEffectiveUrls> effective,
        Uri webBase)
    {
        ArgumentNullException.ThrowIfNull(webBase);
        return Bindings(account, hasToken, rulesKnown, effective, key => new GitCoverage(key, webBase));
    }

    /// <summary>
    /// The URLs bound to one provider's account (7.8, D290; docs/phase-7b-design.md 9.6): none
    /// unless <paramref name="account"/> is signed in with its token in hand
    /// (<paramref name="hasToken"/>), "Use for git over HTTPS" is on and the user's rewrites are
    /// known (<paramref name="rulesKnown"/>); else <see cref="CredentialBinding.Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/>
    /// of <paramref name="effective"/> with the provider's coverage of the account's key (D359).
    /// </summary>
    public static IReadOnlyList<CredentialBinding> Bindings(
        AccountStatus? account,
        bool hasToken,
        bool rulesKnown,
        IReadOnlyList<RemoteEffectiveUrls> effective,
        Func<AccountKey, GitCoverage> coverageOf)
    {
        ArgumentNullException.ThrowIfNull(effective);
        ArgumentNullException.ThrowIfNull(coverageOf);
        return account is { State: AccountState.SignedIn, Account.UseForGit: true } && hasToken && rulesKnown
            ? CredentialBinding.Select(effective, coverageOf(account.Account.Key))
            : [];
    }

    /// <summary>
    /// Each ref with an open pull request (7.8, D313), by its full name: a local branch
    /// <c>refs/heads/x</c> whose upstream is a recognised remote's branch of the same name, and a
    /// remote branch fetched from a recognised remote's branch, when that repository and branch are
    /// an open pull request's head. Heads are keyed by the <see cref="HostedRepository"/> record's
    /// equality (provider, host, owner and name, without case; docs/phase-7b-design.md 9.7), never
    /// by the full name alone: a GitHub mirror of a GitLab head's path maps nothing, and an
    /// <c>&lt;organization&gt;.visualstudio.com</c> remote maps a <c>dev.azure.com</c> head (one
    /// host). A pull request whose fork was deleted has no head and maps nothing; of two on one head,
    /// the lowest number wins. One pass over the refs.
    /// </summary>
    public static IReadOnlyDictionary<string, PullRequest> ByRef(RepoRefs refs, IReadOnlyDictionary<string, HostedRepository> remotes, PullRequestList? list)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentNullException.ThrowIfNull(remotes);
        var byRef = new Dictionary<string, PullRequest>(StringComparer.Ordinal);
        if (list is not { PullRequests.Count: > 0 } || remotes.Count == 0)
        {
            return byRef;
        }

        var heads = new Dictionary<(HostedRepository Repository, string Branch), PullRequest>();
        foreach (var pull in list.PullRequests)
        {
            if (pull.HeadRepository is not { } head)
            {
                continue;
            }

            var key = (head, pull.HeadRef);
            if (!heads.TryGetValue(key, out var kept) || pull.Number < kept.Number)
            {
                heads[key] = pull;
            }
        }

        foreach (var gitRef in refs.Refs)
        {
            var (remote, branch) = gitRef.Kind switch
            {
                RefKind.LocalBranch when gitRef.UpstreamRemote is { } upstream
                    && string.Equals(gitRef.UpstreamRemoteRef, HeadsPrefix + gitRef.Name, StringComparison.Ordinal) => (upstream, gitRef.Name),
                RefKind.RemoteBranch when refs.SourceOf(gitRef) is { IsBranch: true, Remote: { } source, Branch: { } name } => (source, name),
                _ => ((string?)null, (string?)null),
            };
            if (remote is not null
                && branch is not null
                && remotes.TryGetValue(remote, out var repository)
                && heads.TryGetValue((repository, branch), out var pull))
            {
                byRef[gitRef.FullName] = pull;
            }
        }

        return byRef;
    }
}
