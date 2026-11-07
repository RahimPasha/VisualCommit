using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// The requests of the scenario "hosting7b" (docs/phase-7b-design.md 2.6, 12.1) as records, for
/// part P's view-model, menu and plan tests (12.7) and WP-N's: GitLab's <c>web-app</c> with !21,
/// !22 and !23, Azure DevOps' <c>mobile-app</c> with !11, !12 and !13, harbor-co's <c>manifest</c>
/// (D356), each clone's refs with made-up commit ids (one character repeated) and the hosting
/// states the section shows. Made-up names only.
/// </summary>
internal static class RequestTestData
{
    public const string AzureHost = "127.0.0.1:47397";

    public const string GitLabHost = "127.0.0.1:47398";

    public const string GitHubHost = "127.0.0.1:47399";

    /// <summary>GitLab's <c>northfield/platform/web-app</c>, web-app's <c>origin</c>.</summary>
    public static readonly HostedRepository WebApp = new(HostingProvider.GitLab, GitLabHost, "northfield/platform", "web-app");

    /// <summary>ada's fork <c>ada/web-app</c>, no remote of web-app.</summary>
    public static readonly HostedRepository AdaWebApp = new(HostingProvider.GitLab, GitLabHost, "ada", "web-app");

    /// <summary>GitHub's <c>octo/web-app</c>, web-app's <c>upstream</c> (ruling 5).</summary>
    public static readonly HostedRepository OctoWebApp = new(HostingProvider.GitHub, GitHubHost, "octo", "web-app");

    /// <summary>Azure DevOps' <c>Fernhill-Labs/Mobile App/mobile-app</c>, mobile-app's <c>origin</c>.</summary>
    public static readonly HostedRepository MobileApp = new(HostingProvider.AzureDevOps, AzureHost, "Fernhill-Labs/Mobile App", "mobile-app");

    /// <summary>Its fork in the same project, no remote of mobile-app.</summary>
    public static readonly HostedRepository MobileAppFork = new(HostingProvider.AzureDevOps, AzureHost, "Fernhill-Labs/Mobile App", "mobile-app-fork");

    /// <summary>harbor-co's <c>Docks/manifest</c>, which no account of the scenario covers (D356).</summary>
    public static readonly HostedRepository Manifest = new(HostingProvider.AzureDevOps, AzureHost, "harbor-co/Docks", "manifest");

    public static string Sha(char letter) => new(letter, 40);

    public static readonly PullRequest MR21 = Request(21, "Add the search box", "ada", "feature/search", '3', WebApp, WebApp);
    public static readonly PullRequest MR22 = Request(22, "Draft: Update the readme", "octo", "docs/readme", '4', WebApp, WebApp, isDraft: true);
    public static readonly PullRequest MR23 = Request(23, "Fix a typo in the footer", "ada", "fix-typo", '9', AdaWebApp, WebApp);

    public static readonly PullRequest PR11 = Request(11, "Add the sign-in page", "Ada Lovelace", "feature/sign-in", '3', MobileApp, MobileApp);
    public static readonly PullRequest PR12 = Request(12, "Write the guide", "Octo Cat", "docs/guide", '4', MobileApp, MobileApp, isDraft: true);
    public static readonly PullRequest PR13 = Request(13, "Fix a typo in the greeting", "Ada Lovelace", "fix-typo", '9', MobileAppFork, MobileApp);

    /// <summary>web-app's open merge requests, newest first as GitLab lists them.</summary>
    public static readonly PullRequestList WebAppList = new([MR23, MR22, MR21], MoreExist: false);

    /// <summary>mobile-app's active pull requests, id descending as Azure DevOps lists them.</summary>
    public static readonly PullRequestList MobileAppList = new([PR13, PR12, PR11], MoreExist: false);

    /// <summary>A request of <paramref name="baseRepository"/> with its provider's web address (7.4, 6.6).</summary>
    public static PullRequest Request(int number, string title, string? author, string headRef, char headSha, HostedRepository? head, HostedRepository baseRepository, bool isDraft = false) =>
        new(
            number,
            title,
            author,
            isDraft,
            PageOf(baseRepository, number),
            headRef,
            Sha(headSha),
            head,
            "main",
            baseRepository,
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero).AddMinutes(number));

    /// <summary>A request's page: GitLab's <c>web_url</c>, Azure DevOps' address built by the provider.</summary>
    public static Uri PageOf(HostedRepository repository, int number) => repository.Provider switch
    {
        HostingProvider.GitLab => new Uri($"http://{repository.Host}/{repository.FullName}/-/merge_requests/{number}"),
        HostingProvider.AzureDevOps => new Uri($"http://{repository.Host}/{repository.Owner.Split('/')[0]}/{Uri.EscapeDataString(repository.Owner.Split('/')[1])}/_git/{repository.Name}/pullrequest/{number}"),
        _ => new Uri($"http://{repository.Host}/{repository.FullName}/pull/{number}"),
    };

    /// <summary>
    /// web-app's refs: <c>main</c> (HEAD, tracking <c>origin/main</c>), <c>feature/search</c>
    /// (tracking), <c>local-work</c>; <c>origin</c>'s <c>main</c>, <c>docs/readme</c>,
    /// <c>feature/search</c> and <c>topic/no-request</c>; <c>upstream</c>'s <c>main</c>.
    /// </summary>
    public static RepoRefs WebAppRefs(string head = "main", IReadOnlyList<GitRef>? extra = null) => WithRefspecs(new(
        new HeadState(head, Sha('2')),
        [
            Local("feature/search", '3', "origin/feature/search", head),
            Local("local-work", '6', null, head),
            Local("main", '2', "origin/main", head),
            Remote("origin/docs/readme", '4'),
            Remote("origin/feature/search", '3'),
            Remote("origin/main", '2'),
            Remote("origin/topic/no-request", '5'),
            Remote("upstream/main", '2'),
            .. extra ?? [],
        ],
        [],
        ["origin", "upstream"]));

    /// <summary>mobile-app's refs, as web-app's with <c>feature/sign-in</c> and <c>docs/guide</c>, no <c>upstream</c>.</summary>
    public static RepoRefs MobileAppRefs(string head = "main", IReadOnlyList<GitRef>? extra = null) => WithRefspecs(new(
        new HeadState(head, Sha('2')),
        [
            Local("feature/sign-in", '3', "origin/feature/sign-in", head),
            Local("local-work", '6', null, head),
            Local("main", '2', "origin/main", head),
            Remote("origin/docs/guide", '4'),
            Remote("origin/feature/sign-in", '3'),
            Remote("origin/main", '2'),
            Remote("origin/topic/no-request", '5'),
            .. extra ?? [],
        ],
        [],
        ["origin"]));

    /// <summary>Each remote's one fetch line, <c>+refs/heads/*:refs/remotes/&lt;r&gt;/*</c> (2.6).</summary>
    public static RepoRefs WithRefspecs(RepoRefs refs) =>
        refs with { Refspecs = FetchRefspecs.Parse(refs.Remotes.Select(remote => (remote, $"+refs/heads/*:refs/remotes/{remote}/*"))) };

    public static GitRef Local(string name, char sha, string? upstream, string head = "main")
    {
        var remote = upstream?[..upstream.IndexOf('/', StringComparison.Ordinal)];
        return new(
            "refs/heads/" + name,
            name,
            RefKind.LocalBranch,
            Sha(sha),
            Upstream: upstream,
            IsHead: name == head,
            UpstreamRef: upstream is null ? null : "refs/remotes/" + upstream,
            UpstreamRemote: remote,
            UpstreamRemoteRef: upstream is null ? null : "refs/heads/" + upstream[(remote!.Length + 1)..]);
    }

    public static GitRef Remote(string name, char sha) =>
        new("refs/remotes/" + name, name, RefKind.RemoteBranch, Sha(sha), RemoteName: name[..name.IndexOf('/', StringComparison.Ordinal)]);

    public static IReadOnlyDictionary<string, HostedRepository> WebAppRemotes { get; } =
        new Dictionary<string, HostedRepository>(StringComparer.Ordinal) { ["origin"] = WebApp, ["upstream"] = OctoWebApp };

    public static IReadOnlyDictionary<string, HostedRepository> MobileAppRemotes { get; } =
        new Dictionary<string, HostedRepository>(StringComparer.Ordinal) { ["origin"] = MobileApp };

    public static IReadOnlyDictionary<string, HostedRepository> ManifestRemotes { get; } =
        new Dictionary<string, HostedRepository>(StringComparer.Ordinal) { ["origin"] = Manifest };

    /// <summary>web-app's refs to their merge requests as <c>RepositoryHosting</c> maps them (9.7): none under <c>upstream</c>.</summary>
    public static IReadOnlyDictionary<string, PullRequest> WebAppByRef { get; } = new Dictionary<string, PullRequest>(StringComparer.Ordinal)
    {
        ["refs/heads/feature/search"] = MR21,
        ["refs/remotes/origin/feature/search"] = MR21,
        ["refs/remotes/origin/docs/readme"] = MR22,
    };

    public static IReadOnlyDictionary<string, PullRequest> MobileAppByRef { get; } = new Dictionary<string, PullRequest>(StringComparer.Ordinal)
    {
        ["refs/heads/feature/sign-in"] = PR11,
        ["refs/remotes/origin/feature/sign-in"] = PR11,
        ["refs/remotes/origin/docs/guide"] = PR12,
    };

    public static readonly IReadOnlyDictionary<string, PullRequest> NoRequests = new Dictionary<string, PullRequest>(StringComparer.Ordinal);

    /// <summary>A state of <paramref name="repository"/> (web-app or mobile-app) with its remotes.</summary>
    public static RepositoryHostingState State(HostedRepository repository, PullRequestsState state, PullRequestList? list = null, HostingFailure? failure = null, IReadOnlyDictionary<string, PullRequest>? byRef = null) =>
        new(state, RemotesOf(repository), repository, list, failure, byRef ?? NoRequests);

    /// <summary>The scenario's list loaded: web-app's or mobile-app's three, with their map.</summary>
    public static RepositoryHostingState Loaded(HostedRepository repository) => repository.Provider == HostingProvider.GitLab
        ? State(repository, PullRequestsState.Loaded, WebAppList, byRef: WebAppByRef)
        : State(repository, PullRequestsState.Loaded, MobileAppList, byRef: MobileAppByRef);

    /// <summary>manifest's D356 state (9.7): its remote's repository, no list, failure or map.</summary>
    public static RepositoryHostingState OtherOrganization() => new(PullRequestsState.OtherOrganization, ManifestRemotes, Manifest, null, null, NoRequests);

    /// <summary>The refs of the clone whose listed repository is <paramref name="repository"/>.</summary>
    public static RepoRefs RefsOf(HostedRepository repository, string head = "main", IReadOnlyList<GitRef>? extra = null) =>
        repository.Provider == HostingProvider.GitLab ? WebAppRefs(head, extra) : MobileAppRefs(head, extra);

    private static IReadOnlyDictionary<string, HostedRepository> RemotesOf(HostedRepository repository) =>
        repository.Equals(WebApp) ? WebAppRemotes : repository.Equals(Manifest) ? ManifestRemotes : MobileAppRemotes;

    /// <summary>GitLab's 403 of the list (the report's P4): <c>Forbidden</c>, GitLab's message, its code as the detail.</summary>
    public static readonly HostingFailure GitLabForbidden = new(
        HostingFailureKind.Forbidden,
        "Access denied: This operation requires a fine-grained personal access token with the following project permissions: [Merge Request: Read].",
        "insufficient_granular_scope")
    { Provider = HostingProvider.GitLab };

    /// <summary>Azure DevOps' 500 of the list (P4): <c>ServerError</c>, no detail.</summary>
    public static readonly HostingFailure AzureServerError = new(HostingFailureKind.ServerError, "TF400898: An Internal Error Occurred. Activity Id: 00000000-0000-0000-0000-000000000001.")
    { Provider = HostingProvider.AzureDevOps };
}
