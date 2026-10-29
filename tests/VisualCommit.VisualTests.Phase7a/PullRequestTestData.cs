using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// Part P's view-model, menu and plan tests' data (docs/phase-7a-design.md 10.6), built by hand
/// after the scenario "hosting" (2.6): <c>hello</c>'s refs with made-up commit ids (one letter
/// repeated), octo/hello's four open pull requests and the hosting states the section shows.
/// </summary>
internal static class PullRequestTestData
{
    public const string Host = "127.0.0.1:47395";

    public static readonly HostedRepository OctoHello = new(HostingProvider.GitHub, Host, "octo", "hello");
    public static readonly HostedRepository AdaHello = new(HostingProvider.GitHub, Host, "ada", "hello");
    public static readonly HostedRepository OctoTool = new(HostingProvider.GitHub, Host, "octo", "tool");
    public static readonly HostedRepository AcmeTool = new(HostingProvider.GitHub, Host, "acme", "tool");

    public const string LongBranch = "feature/a-very-long-branch-name-that-the-graph-must-cut";

    public static string Sha(char letter) => new(letter, 40);

    /// <summary>#1 Add the login form, ada's, head <c>feature/login</c> of octo/hello.</summary>
    public static readonly PullRequest One = Pull(1, "Add the login form", "ada", "feature/login", 'e', OctoHello);

    /// <summary>#2 Update the readme, octo's draft, head <c>docs/readme</c>.</summary>
    public static readonly PullRequest Two = Pull(2, "Update the readme", "octo", "docs/readme", 'd', OctoHello, isDraft: true);

    /// <summary>#3 Shorten long labels, octo's, head the long branch.</summary>
    public static readonly PullRequest Three = Pull(3, "Shorten long labels", "octo", LongBranch, 'l', OctoHello);

    /// <summary>#5 Fix a typo in the greeting, ada's, head <c>fix-typo</c> of the fork ada/hello.</summary>
    public static readonly PullRequest Five = Pull(5, "Fix a typo in the greeting", "ada", "fix-typo", 'f', AdaHello);

    /// <summary>octo/hello's open pull requests, newest first, as GitHub lists them.</summary>
    public static readonly PullRequestList HelloList = new([Five, Three, Two, One], MoreExist: false);

    public static PullRequest Pull(int number, string title, string? author, string headRef, char headSha, HostedRepository? headRepository, bool isDraft = false, HostedRepository? baseRepository = null) =>
        new(
            number,
            title,
            author,
            isDraft,
            new Uri($"http://{Host}/{(baseRepository ?? OctoHello).FullName}/pull/{number}"),
            headRef,
            Sha(headSha),
            headRepository,
            "main",
            baseRepository ?? OctoHello,
            new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero).AddMinutes(number));

    /// <summary>
    /// <c>hello</c>'s refs: <c>main</c> (HEAD, tracking <c>origin/main</c>), <c>feature/login</c>
    /// (tracking <c>origin/feature/login</c>), <c>local-work</c>; <c>origin</c>'s <c>main</c>,
    /// <c>docs/readme</c>, <c>feature/login</c>, the long branch and <c>topic/no-pr</c>.
    /// </summary>
    public static RepoRefs HelloRefs(string head = "main") => new(
        new HeadState(head, Sha('a')),
        [
            Local("feature/login", 'e', "origin/feature/login", head),
            Local("local-work", 'w', null, head),
            Local("main", 'a', "origin/main", head),
            Remote("origin/docs/readme", 'd'),
            Remote("origin/feature/login", 'e'),
            Remote("origin/" + LongBranch, 'l'),
            Remote("origin/main", 'a'),
            Remote("origin/topic/no-pr", 't'),
        ],
        [],
        ["origin"]);

    public static GitRef Local(string name, char sha, string? upstream, string head = "main") =>
        new(
            "refs/heads/" + name,
            name,
            RefKind.LocalBranch,
            Sha(sha),
            Upstream: upstream,
            IsHead: name == head,
            UpstreamRef: upstream is null ? null : "refs/remotes/" + upstream,
            UpstreamRemote: upstream is null ? null : "origin",
            UpstreamRemoteRef: upstream is null ? null : "refs/heads/" + upstream["origin/".Length..]);

    public static GitRef Remote(string name, char sha) =>
        new("refs/remotes/" + name, name, RefKind.RemoteBranch, Sha(sha), RemoteName: name[..name.IndexOf('/', StringComparison.Ordinal)]);

    public static IReadOnlyDictionary<string, HostedRepository> HelloRemotes { get; } =
        new Dictionary<string, HostedRepository>(StringComparer.Ordinal) { ["origin"] = OctoHello };

    /// <summary>The refs' pull requests as <c>RepositoryHosting</c> maps them for <see cref="HelloList"/> (7.8).</summary>
    public static IReadOnlyDictionary<string, PullRequest> HelloByRef { get; } = new Dictionary<string, PullRequest>(StringComparer.Ordinal)
    {
        ["refs/heads/feature/login"] = One,
        ["refs/remotes/origin/feature/login"] = One,
        ["refs/remotes/origin/docs/readme"] = Two,
        ["refs/remotes/origin/" + LongBranch] = Three,
    };

    private static readonly IReadOnlyDictionary<string, PullRequest> NoPullRequests = new Dictionary<string, PullRequest>(StringComparer.Ordinal);

    public static RepositoryHostingState SignedOut() => new(PullRequestsState.SignedOut, HelloRemotes, OctoHello, null, null, NoPullRequests);

    public static RepositoryHostingState Loading(PullRequestList? list = null, HostedRepository? repository = null) =>
        new(PullRequestsState.Loading, HelloRemotes, repository ?? OctoHello, list, null, NoPullRequests);

    public static RepositoryHostingState Failed(HostingFailure failure, HostedRepository? repository = null) =>
        new(PullRequestsState.Failed, HelloRemotes, repository ?? OctoHello, null, failure, NoPullRequests);

    public static RepositoryHostingState Loaded(PullRequestList? list = null, IReadOnlyDictionary<string, PullRequest>? byRef = null, HostedRepository? repository = null) =>
        new(PullRequestsState.Loaded, HelloRemotes, repository ?? OctoHello, list ?? HelloList, null, byRef ?? (list is null ? HelloByRef : NoPullRequests));

    /// <summary>GitHub's 403 for a token without Pull requests access, as WP-G reads it (part P's P4).</summary>
    public static readonly HostingFailure Forbidden = new(HostingFailureKind.Forbidden, "Resource not accessible by personal access token", "pull_requests=read");
}
