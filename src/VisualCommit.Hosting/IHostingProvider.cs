using VisualCommit.Core.Hosting;

namespace VisualCommit.Hosting;

/// <summary>
/// One hosting service (docs/phase-7a-design.md 1.4): GitHub in phase 7a, Azure DevOps and GitLab
/// in 7b. Recognition makes no request; every request takes the account's token, which the
/// provider never keeps.
/// </summary>
public interface IHostingProvider
{
    HostingProvider Kind { get; }

    GitHubEndpoints Endpoints { get; }

    /// <summary>The repository a remote's URL names, without the network; null for another host.</summary>
    HostedRepository? Recognise(string url);

    /// <summary>The push guard (ruling 8): false only for <c>refs/pull/&lt;x&gt;/head</c> or <c>/merge</c> with <c>&lt;x&gt;</c> not all digits.</summary>
    bool SourceCanExist(string remoteRef);

    /// <summary>A1: the token's account.</summary>
    Task<HostingResult<HostingAccount>> CheckTokenAsync(string token, CancellationToken ct);

    /// <summary>A2: the account's repositories.</summary>
    Task<HostingResult<RepositoryList>> ListRepositoriesAsync(string token, CancellationToken ct);

    /// <summary>A3: one repository.</summary>
    Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(string token, HostedRepository repository, CancellationToken ct);

    /// <summary>A4: a repository's open pull requests.</summary>
    Task<HostingResult<PullRequestList>> ListPullRequestsAsync(string token, HostedRepository repository, CancellationToken ct);

    /// <summary>A5: creates a pull request.</summary>
    Task<HostingResult<PullRequest>> CreatePullRequestAsync(string token, HostedRepository baseRepository, NewPullRequest request, CancellationToken ct);

    /// <summary>
    /// A6: commit authors by e-mail, <paramref name="branch"/>'s commits page read in
    /// <paramref name="repository"/>; each of <paramref name="lookups"/> is looked up from its own
    /// commit in its own repository (D339). A null <paramref name="branch"/> asks the lookups only
    /// (no commits page); at most 20 lookups a call.
    /// </summary>
    Task<HostingResult<AuthorMap>> ReadAuthorsAsync(string token, HostedRepository repository, string? branch, IReadOnlyCollection<AuthorCommit> lookups, CancellationToken ct);
}
