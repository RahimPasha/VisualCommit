using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;

namespace VisualCommit.Hosting;

/// <summary>
/// One hosting service (docs/phase-7a-design.md 1.4, docs/phase-7b-design.md 1.4): GitHub, Azure
/// DevOps and GitLab. Recognition makes no request; every request takes the account's token,
/// which the provider never keeps. Each provider keeps its own endpoints; GitHub's A6 (the
/// authors' avatars) stays on <c>GitHubProvider</c> (D349).
/// </summary>
public interface IHostingProvider
{
    HostingProvider Kind { get; }

    /// <summary>Its accounts' <see cref="AccountKey.Host"/>.</summary>
    string Host { get; }

    /// <summary>The web and git origin.</summary>
    Uri WebBase { get; }

    /// <summary>The repository a remote's URL names, of any organization, without an account or the network; null for another host. SSH is recognised, never bound.</summary>
    HostedRepository? Recognise(string url);

    /// <summary>The push guard (ruling 14): <see cref="PullRequestRefs.SourceCanExist"/> of <see cref="Kind"/>.</summary>
    bool SourceCanExist(string remoteRef);

    /// <summary>What <paramref name="account"/>'s token covers for git (D359).</summary>
    GitCoverage CoverageOf(AccountKey account);

    /// <summary>Whether <paramref name="account"/> covers <paramref name="repository"/>: its host; on Azure DevOps the account's organization (D350).</summary>
    bool Covers(HostedRepository repository, AccountKey account);

    /// <summary>The sign-in dialog's link to a new token; Azure DevOps' needs the organization.</summary>
    Uri TokenPage(string? organization);

    /// <summary>The page of <paramref name="repository"/>'s open requests ("Open … in browser").</summary>
    Uri PullRequestsPage(HostedRepository repository);

    /// <summary>The head ref a checkout of <paramref name="pull"/> fetches: <see cref="PullRequestRefs.HeadOf"/> of <see cref="Kind"/>.</summary>
    string? PullRequestHeadRef(PullRequest pull);

    /// <summary>A1: the token's account; <paramref name="organization"/> is Azure DevOps' (others ignore it).</summary>
    Task<HostingResult<HostingAccount>> CheckTokenAsync(string token, string? organization, CancellationToken ct);

    /// <summary>A2: the account's repositories.</summary>
    Task<HostingResult<RepositoryList>> ListRepositoriesAsync(string token, HostingAccount account, CancellationToken ct);

    /// <summary>A3: one repository.</summary>
    Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(string token, HostedRepository repository, CancellationToken ct);

    /// <summary>A4: a repository's open pull requests.</summary>
    Task<HostingResult<PullRequestList>> ListPullRequestsAsync(string token, HostedRepository repository, CancellationToken ct);

    /// <summary>A5: creates a pull request.</summary>
    Task<HostingResult<PullRequest>> CreatePullRequestAsync(string token, HostedRepository baseRepository, NewPullRequest request, CancellationToken ct);
}
