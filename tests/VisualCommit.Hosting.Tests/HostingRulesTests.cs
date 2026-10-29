using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting.GitHub;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The hosting's rules for one repository (docs/phase-7a-design.md 7.8, 7.13): the remotes
/// recognised and the listed candidate's order, the URLs bound to the account (D278, D290), and
/// the refs that have an open pull request (D313).
/// </summary>
public sealed class HostingRulesTests
{
    private static readonly AccountKey Octo = new(HostingProvider.GitHub, "github.com", "OctoCat");

    private static readonly Uri WebBase = GitHubEndpoints.GitHubCom.WebBase;

    [Fact]
    public void The_candidate_is_origin_then_upstream_then_the_first_by_ordinal_name()
    {
        static HostedRepository? Candidate(params string[] names) =>
            HostingRules.ListedCandidate(names.ToDictionary(name => name, name => Repository("o", name), StringComparer.Ordinal));

        Assert.Equal(Repository("o", "origin"), Candidate("zeta", "upstream", "origin", "Alpha"));
        Assert.Equal(Repository("o", "upstream"), Candidate("zeta", "upstream", "Alpha"));
        Assert.Equal(Repository("o", "Alpha"), Candidate("zeta", "beta", "Alpha"));
        Assert.Null(Candidate());
    }

    [Fact]
    public void A_remote_is_recognised_by_its_first_rewritten_fetch_url_else_by_its_first_url()
    {
        RemoteUrl[] configured =
        [
            new("origin", "gh:octo/hello", false),
            new("upstream", "https://github.com/acme/tool.git", false),
            new("upstream", "https://github.com/acme/other.git", false),
            new("ssh", "git@github.com:octo/ssh.git", false),
            new("elsewhere", "https://gitlab.com/octo/hello.git", false),
            new("pushonly", "https://github.com/octo/push.git", true),
        ];
        var rewrites = new UrlRewrites([new UrlRewrite("https://github.com/", "gh:", ForPush: false)]);

        var remotes = HostingRules.Remotes(configured, rewrites.Apply(configured), Recognise);

        Assert.Equal(["origin", "ssh", "upstream"], remotes.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(Repository("octo", "hello"), remotes["origin"]);
        Assert.Equal(Repository("acme", "tool"), remotes["upstream"]);
        Assert.Equal(Repository("octo", "ssh"), remotes["ssh"]);

        // Without the rules, the configured url is all there is to recognise.
        Assert.False(HostingRules.Remotes(configured, [], Recognise).ContainsKey("origin"));
    }

    [Fact]
    public void Fetch_and_push_urls_are_bound_and_a_url_naming_the_login_loses_its_user()
    {
        RemoteEffectiveUrls[] effective =
        [
            new("origin", ["https://github.com/o/r.git"], ["https://github.com/o/r-push.git"]),
            new("upstream", ["https://OctoCat@github.com/o/up"], ["https://octocat@github.com/o/up"]),
        ];

        var bindings = HostingRules.Bindings(SignedIn(), hasToken: true, rulesKnown: true, effective, WebBase);

        Assert.Equal(
            [new CredentialBinding("https://github.com/o/r.git", Octo), new CredentialBinding("https://github.com/o/r-push.git", Octo), new CredentialBinding("https://github.com/o/up", Octo)],
            bindings);
    }

    [Fact]
    public void Another_users_url_drops_the_bindings_at_and_above_its_path()
    {
        RemoteEffectiveUrls[] effective =
        [
            new("origin", ["https://github.com/o/r"], ["https://github.com/o/r"]),
            new("sub", ["https://github.com/o/r/sub"], ["https://github.com/o/r/sub"]),
            new("alice", ["https://alice@github.com/o/r/sub"], ["https://alice@github.com/o/r/sub"]),
            new("other", ["https://github.com/o/rx"], ["https://github.com/o/rx"]),
        ];

        var bindings = HostingRules.Bindings(SignedIn(), hasToken: true, rulesKnown: true, effective, WebBase);

        Assert.Equal([new CredentialBinding("https://github.com/o/rx", Octo)], bindings);
    }

    [Fact]
    public void Ssh_git_and_other_hosts_are_not_bound_and_the_fakes_port_is()
    {
        RemoteEffectiveUrls[] effective =
        [
            new("ssh", ["git@github.com:o/r.git", "ssh://git@github.com/o/r.git"], []),
            new("git", ["git://github.com/o/r.git"], []),
            new("http", ["http://github.com/o/r.git"], []),
            new("gitlab", ["https://gitlab.com/o/r.git"], []),
            new("fake", ["http://127.0.0.1:47395/octo/hello.git"], ["http://127.0.0.1:47391/octo/hello.git"]),
        ];

        Assert.Empty(HostingRules.Bindings(SignedIn(), hasToken: true, rulesKnown: true, effective, WebBase));

        var fake = new AccountKey(HostingProvider.GitHub, "127.0.0.1:47395", "octo");
        var onTheFake = HostingRules.Bindings(SignedIn(fake), hasToken: true, rulesKnown: true, effective, GitHubEndpoints.Local(47395).WebBase);
        Assert.Equal([new CredentialBinding("http://127.0.0.1:47395/octo/hello.git", fake)], onTheFake);
    }

    [Fact]
    public void Nothing_is_bound_signed_out_switched_off_refused_without_the_token_or_before_the_rules_are_known()
    {
        RemoteEffectiveUrls[] effective = [new("origin", ["https://github.com/o/r.git"], ["https://github.com/o/r.git"])];

        Assert.Single(HostingRules.Bindings(SignedIn(), hasToken: true, rulesKnown: true, effective, WebBase));
        Assert.Empty(HostingRules.Bindings(null, hasToken: false, rulesKnown: true, effective, WebBase));
        Assert.Empty(HostingRules.Bindings(SignedIn(useForGit: false), hasToken: true, rulesKnown: true, effective, WebBase));
        Assert.Empty(HostingRules.Bindings(Refused(), hasToken: false, rulesKnown: true, effective, WebBase));
        Assert.Empty(HostingRules.Bindings(Refused(), hasToken: true, rulesKnown: true, effective, WebBase));
        Assert.Empty(HostingRules.Bindings(SignedIn(), hasToken: false, rulesKnown: true, effective, WebBase));
        Assert.Empty(HostingRules.Bindings(SignedIn(), hasToken: true, rulesKnown: false, effective, WebBase));
    }

    [Fact]
    public void ByRef_maps_local_and_remote_branches_of_heads_on_github_and_lowest_number_wins()
    {
        var hello = Repository("octo", "hello");
        var fork = Repository("ada", "hello");
        var remotes = new Dictionary<string, HostedRepository>(StringComparer.Ordinal) { ["origin"] = hello, ["ada"] = fork };
        var refs = Refs(
            Local("feature/login", "origin", "refs/heads/feature/login"),
            Local("main", "origin", "refs/heads/main"),
            Local("topic", "origin", "refs/heads/main"),
            Local("fix", "ada", "refs/heads/fix-typo"),
            Local("local-work"),
            Remote("origin/feature/login"),
            Remote("origin/main"),
            Remote("ada/fix-typo"),
            Remote("ada/idea"),
            Remote("gitlab/feature/login"));
        var list = new PullRequestList(
            [
                Pull(9, "feature/login", hello),
                Pull(1, "feature/login", Repository("Octo", "Hello")),
                Pull(2, "main", hello),
                Pull(5, "fix-typo", fork),
                Pull(8, "idea", null),
            ],
            MoreExist: false);

        var byRef = HostingRules.ByRef(refs, remotes, list);

        Assert.Equal(
            new Dictionary<string, int>
            {
                ["refs/heads/feature/login"] = 1,
                ["refs/heads/main"] = 2,
                ["refs/remotes/origin/feature/login"] = 1,
                ["refs/remotes/origin/main"] = 2,
                ["refs/remotes/ada/fix-typo"] = 5,
            },
            byRef.ToDictionary(pair => pair.Key, pair => pair.Value.Number));

        // A local branch that tracks another name (topic on origin/main) and one on the fork under
        // another name (fix on ada/fix-typo) map nothing (D313); a deleted fork's head maps nothing.
        Assert.False(byRef.ContainsKey("refs/heads/topic"));
        Assert.False(byRef.ContainsKey("refs/heads/fix"));
        Assert.False(byRef.ContainsKey("refs/remotes/ada/idea"));
        Assert.Empty(HostingRules.ByRef(refs, remotes, null));
        Assert.Empty(HostingRules.ByRef(refs, new Dictionary<string, HostedRepository>(), list));
    }

    private static HostedRepository? Recognise(string url) => GitHubUrls.Recognise(GitHubEndpoints.GitHubCom, url);

    private static HostedRepository Repository(string owner, string name) => new(HostingProvider.GitHub, "github.com", owner, name);

    private static AccountStatus SignedIn(AccountKey? key = null, bool useForGit = true) =>
        new(new HostingAccount(key ?? Octo, "OctoCat", 583231, null, null, useForGit), AccountState.SignedIn, TokenPlace.Keychain, AccountNoteKind.None, null);

    private static AccountStatus Refused() =>
        new(new HostingAccount(Octo, "OctoCat", 583231, null, null, true), AccountState.NeedsSignIn, null, AccountNoteKind.Refused, null);

    private static PullRequest Pull(int number, string headRef, HostedRepository? head) =>
        new(number, $"#{number}", "octo", false, new Uri($"https://github.com/octo/hello/pull/{number}"), headRef, "abc", head, "main", Repository("octo", "hello"), DateTimeOffset.UnixEpoch);

    private static GitRef Local(string name, string? remote = null, string? remoteRef = null) =>
        new($"refs/heads/{name}", name, RefKind.LocalBranch, "abc", UpstreamRemote: remote, UpstreamRemoteRef: remoteRef);

    private static GitRef Remote(string name) =>
        new($"refs/remotes/{name}", name, RefKind.RemoteBranch, "abc", RemoteName: name[..name.IndexOf('/', StringComparison.Ordinal)]);

    private static RepoRefs Refs(params GitRef[] refs) =>
        new(new HeadState("main", "abc"), refs, [], ["ada", "gitlab", "origin"]);
}
