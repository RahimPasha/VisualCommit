using System.Net;
using VisualCommit.Core;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting.GitHub;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// GitHub as a hosting service (docs/phase-7a-design.md 6.3, 6.5): A1 to A6 against a copy of
/// "hosting" served in process (a fork, a draft, a deleted fork, a closed pull request), A5's
/// bodies and refusals, A6's map and its caps, and avatars on another host dropped. Every request
/// the provider sends, on every route, carries GitHub's four headers (checked as each test ends).
/// </summary>
public sealed class GitHubProviderTests
{
    private const string Host = "127.0.0.1:1";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A1_gives_the_tokens_account_and_refuses_an_empty_token_without_a_request()
    {
        await using var setup = await Setup.StartAsync();
        var provider = setup.Provider;

        var octo = await provider.CheckTokenAsync(setup.Served.OctoToken, Cancelled);

        Assert.True(octo.Succeeded);
        var account = octo.Value!;
        Assert.Equal(("github:127.0.0.1:1:octo", "octo", 1001L, "Octo Cat", "http://127.0.0.1:1/avatars/u/1001?v=4", true),
            (account.Key.Id, account.Login, account.Id, account.Name, account.AvatarUrl!.AbsoluteUri, account.UseForGit));
        Assert.Equal(new AccountKey(HostingProvider.GitHub, Host, "octo"), account.Key);

        foreach (var (token, message) in new[] { ("", "The token is empty."), ("   ", "The token is empty."), ("github_pat_FAKE octo", "Bad credentials"), ("ghp_é", "Bad credentials"), ("ghp_x\r\nX-Evil: 1", "Bad credentials") })
        {
            var refused = await provider.CheckTokenAsync(token, Cancelled);
            Assert.Equal((HostingFailureKind.BadCredentials, message), (refused.Failure!.Kind, refused.Failure.Message));
        }

        Assert.Single(setup.Fake.Requests);

        var unknown = await provider.CheckTokenAsync("github_pat_FAKE-nobody-9", Cancelled);
        Assert.Equal((HostingFailureKind.BadCredentials, "Bad credentials"), (unknown.Failure!.Kind, unknown.Failure.Message));
        setup.Fake.Revoke(setup.Served.AdaToken);
        Assert.Equal(HostingFailureKind.BadCredentials, (await provider.CheckTokenAsync(setup.Served.AdaToken, Cancelled)).Failure!.Kind);
        Assert.All(setup.Fake.Requests, request => Assert.Equal("/api/v3/user", request.Path));
    }

    [Fact]
    public async Task A2_lists_the_accounts_repositories_by_update_without_type()
    {
        await using var setup = await Setup.StartAsync();

        var octo = await setup.Provider.ListRepositoriesAsync(setup.Served.OctoToken, Cancelled);
        var ada = await setup.Provider.ListRepositoriesAsync(setup.Served.AdaToken, Cancelled);

        Assert.Equal((false, false), (octo.Value!.MoreExist, octo.Value.SomeHiddenBySso));
        Assert.Equal(new[] { "octo/hello", "octo/tool", "octo/notes", "octo/website" }, octo.Value.Repositories.Select(info => info.Repository.FullName));
        var hello = octo.Value.Repositories[0];
        Assert.Equal(Repository("octo", "hello"), hello.Repository);
        Assert.Equal(("main", true, false, false, true, "http://127.0.0.1:1/octo/hello.git", "http://127.0.0.1:1/octo/hello", AppClocks.HarnessTime),
            (hello.DefaultBranch, hello.IsPrivate, hello.IsFork, hello.IsArchived, hello.CanPush, hello.CloneUrl.AbsoluteUri, hello.HtmlUrl.AbsoluteUri, hello.UpdatedAt));
        var tool = octo.Value.Repositories[1];
        Assert.Equal((true, (HostedRepository?)null, (string?)null), (tool.IsFork, tool.Parent, tool.ParentDefaultBranch));
        Assert.True(octo.Value.Repositories[3].IsArchived);

        Assert.Equal(new[] { "acme/tool", "ada/hello" }, ada.Value!.Repositories.Select(info => info.Repository.FullName));
        Assert.All(ada.Value.Repositories, info => Assert.True(info.CanPush));
        Assert.All(setup.Fake.Requests, request => Assert.Equal(("/api/v3/user/repos", "per_page=100&sort=updated"), (request.Path, request.Query)));
    }

    [Fact]
    public async Task A2_says_when_single_sign_on_hid_some_and_stops_at_ten_pages()
    {
        await using var setup = await Setup.StartAsync();
        setup.Fake.FailNext("user/repos", FakeGitHubFault.SsoPartialResults);

        var hidden = await setup.Provider.ListRepositoriesAsync(setup.Served.OctoToken, Cancelled);

        Assert.Equal((true, false, 4), (hidden.Value!.SomeHiddenBySso, hidden.Value.MoreExist, hidden.Value.Repositories.Count));

        setup.Fake.AddListedRepositories("octo", 30);
        setup.Fake.MaxPerPage = 2;
        var before = setup.Fake.Requests.Count;
        var capped = await setup.Provider.ListRepositoriesAsync(setup.Served.OctoToken, Cancelled);

        Assert.Equal((20, true, false), (capped.Value!.Repositories.Count, capped.Value.MoreExist, capped.Value.SomeHiddenBySso));
        Assert.Equal(10, setup.Fake.Requests.Count - before);
    }

    [Fact]
    public async Task A3_reads_a_repository_with_a_forks_parent_and_follows_a_move()
    {
        await using var setup = await Setup.StartAsync();
        var provider = setup.Provider;

        var tool = await provider.ReadRepositoryAsync(setup.Served.OctoToken, Repository("octo", "tool"), Cancelled);
        var hello = await provider.ReadRepositoryAsync(setup.Served.OctoToken, Repository("octo", "hello"), Cancelled);
        setup.Fake.FailNext("repo:octo/tool", FakeGitHubFault.Moved);
        var moved = await provider.ReadRepositoryAsync(setup.Served.OctoToken, Repository("Octo", "Tool"), Cancelled);
        var missing = await provider.ReadRepositoryAsync(setup.Served.OctoToken, Repository("octo", "missing"), Cancelled);
        var hidden = await provider.ReadRepositoryAsync(setup.Served.AdaToken, Repository("octo", "notes"), Cancelled);

        Assert.Equal((true, Repository("acme", "tool"), "main", true), (tool.Value!.IsFork, tool.Value.Parent, tool.Value.ParentDefaultBranch, tool.Value.CanPush));
        Assert.Equal((false, (HostedRepository?)null, (string?)null), (hello.Value!.IsFork, hello.Value.Parent, hello.Value.ParentDefaultBranch));
        Assert.Equal(tool.Value, moved.Value);
        Assert.Equal((HostingFailureKind.NotFound, "Not Found"), (missing.Failure!.Kind, missing.Failure.Message));
        Assert.Equal(HostingFailureKind.NotFound, hidden.Failure!.Kind);
        Assert.Contains(setup.Fake.Requests, request => request.Path == "/api/v3/repositories/3004" && request.TokenPresent);
    }

    [Fact]
    public async Task A4_lists_open_pull_requests_with_a_draft_a_fork_and_a_deleted_fork()
    {
        await using var setup = await Setup.StartAsync();
        var login = (await setup.Served.Hello.GitAsync("rev-parse", "origin/feature/login")).StandardOutput.Trim();

        var hello = await setup.Provider.ListPullRequestsAsync(setup.Served.OctoToken, Repository("octo", "hello"), Cancelled);
        var tool = await setup.Provider.ListPullRequestsAsync(setup.Served.OctoToken, Repository("acme", "tool"), Cancelled);

        Assert.False(hello.Value!.MoreExist);
        Assert.Equal(new[] { 5, 3, 2, 1 }, hello.Value.PullRequests.Select(pull => pull.Number));
        var first = hello.Value.PullRequests[3];
        Assert.Equal(new PullRequest(1, "Add the login form", "ada", false, new Uri("http://127.0.0.1:1/octo/hello/pull/1"), "feature/login", login,
            Repository("octo", "hello"), "main", Repository("octo", "hello"), AppClocks.HarnessTime), first);
        Assert.False(first.IsFromFork);
        Assert.Equal((true, "octo"), (hello.Value.PullRequests[2].IsDraft, hello.Value.PullRequests[2].Author));
        var fromFork = hello.Value.PullRequests[0];
        Assert.Equal((Repository("ada", "hello"), true, "fix-typo"), (fromFork.HeadRepository, fromFork.IsFromFork, fromFork.HeadRef));
        Assert.Matches("^[0-9a-f]{40}$", fromFork.HeadSha);

        Assert.Equal(new[] { 8, 7 }, tool.Value!.PullRequests.Select(pull => pull.Number));
        Assert.Equal(((HostedRepository?)null, true, Repository("acme", "tool")), (tool.Value.PullRequests[0].HeadRepository, tool.Value.PullRequests[0].IsFromFork, tool.Value.PullRequests[0].BaseRepository));
        Assert.Equal(Repository("octo", "tool"), tool.Value.PullRequests[1].HeadRepository);
        Assert.All(setup.Fake.Requests, request => Assert.Equal("state=open&per_page=100", request.Query));
    }

    [Fact]
    public async Task A4_stops_at_three_pages_and_says_more_exist_also_when_asked_again()
    {
        await using var setup = await Setup.StartAsync();
        foreach (var title in new[] { "Six", "Seven", "Eight", "Nine" })
        {
            setup.Fake.AddPullRequest("octo", "hello", title, "octo", "feature/login", "main");
        }

        setup.Fake.MaxPerPage = 2;

        var list = await setup.Provider.ListPullRequestsAsync(setup.Served.OctoToken, Repository("octo", "hello"), Cancelled);

        Assert.Equal((6, true), (list.Value!.PullRequests.Count, list.Value.MoreExist));
        Assert.Equal(new[] { 9, 8, 7, 6, 5, 3 }, list.Value.PullRequests.Select(pull => pull.Number));
        Assert.Equal(3, setup.Fake.Requests.Count);

        // As the session asks again after each operation (7.8): every page 304, the same rows, still more.
        var again = await setup.Provider.ListPullRequestsAsync(setup.Served.OctoToken, Repository("octo", "hello"), Cancelled);

        Assert.Equal(list.Value.PullRequests, again.Value!.PullRequests);
        Assert.True(again.Value.MoreExist);
        Assert.Equal(6, setup.Fake.Requests.Count);
        Assert.All(setup.Fake.Requests.Skip(3), request => Assert.Equal((true, 304), (request.Conditional, request.Status)));
    }

    [Fact]
    public async Task A5_in_the_same_repository_sends_the_bare_branch_and_a_draft()
    {
        await using var setup = await Setup.StartAsync();

        var created = await setup.Provider.CreatePullRequestAsync(setup.Served.OctoToken, Repository("octo", "hello"),
            new NewPullRequest("Try the idea", "Please look.", "topic/no-pr", null, "main", Draft: true), Cancelled);

        var pull = created.Value!;
        Assert.Equal((6, "Try the idea", "octo", true, "topic/no-pr", Repository("octo", "hello"), false, "main"),
            (pull.Number, pull.Title, pull.Author, pull.IsDraft, pull.HeadRef, pull.HeadRepository, pull.IsFromFork, pull.BaseRef));
        Assert.Equal("http://127.0.0.1:1/octo/hello/pull/6", pull.HtmlUrl.AbsoluteUri);
        var post = Assert.Single(setup.Fake.Requests);
        Assert.Equal(("POST", "/api/v3/repos/octo/hello/pulls", 201), (post.Method, post.Path, post.Status));
        Assert.Equal("{\"title\":\"Try the idea\",\"body\":\"Please look.\",\"head\":\"topic/no-pr\",\"base\":\"main\",\"draft\":true}", post.Body);
    }

    [Fact]
    public async Task A5_across_forks_names_the_heads_owner_and_lets_maintainers_modify()
    {
        await using var setup = await Setup.StartAsync();

        var created = await setup.Provider.CreatePullRequestAsync(setup.Served.OctoToken, Repository("octo", "hello"),
            new NewPullRequest("Try the idea from ada", "", "topic/no-pr", "ada", "main", Draft: false), Cancelled);

        var pull = created.Value!;
        Assert.Equal((6, false, Repository("ada", "hello"), true), (pull.Number, pull.IsDraft, pull.HeadRepository, pull.IsFromFork));
        var post = Assert.Single(setup.Fake.Requests);
        Assert.Equal("{\"title\":\"Try the idea from ada\",\"body\":\"\",\"head\":\"ada:topic/no-pr\",\"base\":\"main\",\"draft\":false,\"maintainer_can_modify\":true}", post.Body);
    }

    [Fact]
    public async Task A5s_refusals_carry_the_detail_the_form_matches_with_one_post_each()
    {
        await using var setup = await Setup.StartAsync();
        var hello = Repository("octo", "hello");
        (string Token, HostedRepository Repository, NewPullRequest Request, HostingFailureKind Kind, string? Detail)[] cases =
        [
            (setup.Served.OctoToken, hello, new NewPullRequest("", "", "topic/no-pr", null, "main", false), HostingFailureKind.Validation, "title is missing"),
            (setup.Served.OctoToken, hello, new NewPullRequest("A title", "", "topic/no-pr", null, "nope", false), HostingFailureKind.Validation, "base is invalid"),
            (setup.Served.OctoToken, hello, new NewPullRequest("A title", "", "nope", null, "main", false), HostingFailureKind.Validation, "head is invalid"),
            (setup.Served.OctoToken, hello, new NewPullRequest("A title", "", "feature/login", null, "main", false), HostingFailureKind.Validation, "A pull request already exists for octo:feature/login."),
            (setup.Served.OctoToken, hello, new NewPullRequest("A title", "", "main", null, "main", false), HostingFailureKind.Validation, "No commits between main and main"),
            (setup.Served.AdaToken, Repository("octo", "tool"), new NewPullRequest("A title", "", "fix-typo", null, "main", false), HostingFailureKind.Forbidden, "pull_requests=write"),
        ];

        foreach (var (token, repository, request, kind, detail) in cases)
        {
            var refused = await setup.Provider.CreatePullRequestAsync(token, repository, request, Cancelled);
            Assert.Equal((kind, detail), (refused.Failure!.Kind, refused.Failure.Detail));
        }

        Assert.Equal("Resource not accessible by personal access token", (await setup.Provider.CreatePullRequestAsync(setup.Served.AdaToken, Repository("octo", "tool"), cases[^1].Request, Cancelled)).Failure!.Message);
        Assert.Equal(cases.Length + 1, setup.Fake.Requests.Count(request => request.Method == "POST"));
        Assert.Equal(cases.Length + 1, setup.Fake.Requests.Count);
        Assert.DoesNotContain(setup.Fake.PullRequests("octo/hello"), pull => pull.Number > 5);
    }

    [Fact]
    public async Task A6_maps_the_branchs_authors_then_looks_up_the_others_once_each()
    {
        await using var setup = await Setup.StartAsync();

        var authors = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "hello"), "main",
            ["ADA@example.com", "Stranger@Example.com", "", "stranger@example.com"], Cancelled);

        var map = authors.Value!.ByEmail;
        Assert.Equal(new[] { "ada@example.com", "author@example.com", "octo@example.com", "stranger@example.com" }, map.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new AuthorAvatar(1001, "octo", new Uri("http://127.0.0.1:1/avatars/u/1001?v=4")), map["octo@example.com"]);
        Assert.Equal(new AuthorAvatar(1003, "ada", new Uri("http://127.0.0.1:1/avatars/u/1003?v=4")), map["ada@example.com"]);
        Assert.Null(map["author@example.com"]);
        Assert.Null(map["stranger@example.com"]);
        Assert.Equal(new[] { "sha=main&per_page=100", "author=stranger%40example.com&per_page=1" }, setup.Fake.Requests.Select(request => request.Query));

        var branch = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "hello"), "feature/login", [], Cancelled);
        Assert.True(branch.Succeeded);
        Assert.Equal("sha=feature%2Flogin&per_page=100", setup.Fake.Requests[^1].Query);
    }

    [Fact]
    public async Task A6_without_a_branch_asks_no_commits_page_and_at_most_twenty_lookups()
    {
        await using var setup = await Setup.StartAsync();
        string[] emails = [Scenarios.OctoEmail, .. Enumerable.Range(1, 24).Select(n => FormattableString.Invariant($"user{n:D2}@example.com"))];

        var authors = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "hello"), null, emails, Cancelled);

        var map = authors.Value!.ByEmail;
        Assert.Equal(20, map.Count);
        Assert.Equal(new AuthorAvatar(1001, "octo", new Uri("http://127.0.0.1:1/avatars/u/1001?v=4")), map[Scenarios.OctoEmail]);
        Assert.All(emails[1..20], email => Assert.Null(map[email]));
        Assert.DoesNotContain(emails[20], map.Keys);
        Assert.Equal(20, setup.Fake.Requests.Count);
        Assert.All(setup.Fake.Requests, request => Assert.StartsWith("author=", request.Query, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A6_an_empty_repository_is_an_empty_map_and_a_failing_lookup_is_left_out()
    {
        await using var setup = await Setup.StartAsync();

        var empty = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "notes"), "main", [Scenarios.OctoEmail], Cancelled);
        Assert.Equal((true, 0), (empty.Succeeded, empty.Value!.ByEmail.Count));
        Assert.Single(setup.Fake.Requests);

        setup.Fake.FailNext("commits:octo/hello", FakeGitHubFault.ServerError);
        var stopped = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "hello"), null, [Scenarios.OctoEmail, Scenarios.AdaEmail], Cancelled);
        Assert.Equal((true, 0), (stopped.Succeeded, stopped.Value!.ByEmail.Count));
        Assert.Equal(2, setup.Fake.Requests.Count);

        setup.Fake.FailNext("commits:octo/hello", FakeGitHubFault.ServerError);
        var failed = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "hello"), "main", [Scenarios.OctoEmail], Cancelled);
        Assert.Equal(HostingFailureKind.ServerError, failed.Failure!.Kind);
        Assert.Equal(3, setup.Fake.Requests.Count);
    }

    [Fact]
    public async Task A6_an_empty_or_null_author_is_unknown_the_first_commit_wins_and_a_foreign_avatar_is_dropped_once()
    {
        const string page = """
            [
              {"sha":"1","commit":{"author":{"email":"Octo@Example.com"}},"author":{}},
              {"sha":"2","commit":{"author":{"email":"octo@example.com"}},"author":{"login":"octo","id":1001,"avatar_url":"https://avatars.githubusercontent.com/u/1001?v=4"}},
              {"sha":"3","commit":{"author":{"email":"ada@example.com"}},"author":{"login":"ada","id":1003,"avatar_url":"https://avatars.githubusercontent.com/u/1003?v=4"}},
              {"sha":"4","commit":{"author":{"email":"eve@example.com"}},"author":{"login":"eve","id":1004,"avatar_url":"https://evil.example/u/1004"}},
              {"sha":"5","commit":{"author":{"email":"mallory@example.com"}},"author":{"login":"mallory","id":1005,"avatar_url":"http://avatars.githubusercontent.com/u/1005"}},
              {"sha":"6","commit":{"author":{"email":"none@example.com"}},"author":null},
              {"sha":"7","commit":{"author":{}},"author":null}
            ]
            """;
        var recorder = new RecordingHttpHandler(_ => RecordingHttpHandler.Json(HttpStatusCode.OK, page));
        var log = new ListLog();
        using var provider = new GitHubProvider(GitHubEndpoints.GitHubCom, () => recorder, log, new ManualTimeProvider());

        var authors = await provider.ReadAuthorsAsync("github_pat_FAKE-octo-1", new HostedRepository(HostingProvider.GitHub, "github.com", "octo", "hello"), "main", [], Cancelled);

        var map = authors.Value!.ByEmail;
        Assert.Equal(new[] { "ada@example.com", "eve@example.com", "mallory@example.com", "none@example.com", "octo@example.com" }, map.Keys.Order(StringComparer.Ordinal));
        Assert.Null(map["octo@example.com"]);
        Assert.Equal(new AuthorAvatar(1003, "ada", new Uri("https://avatars.githubusercontent.com/u/1003?v=4")), map["ada@example.com"]);
        Assert.Null(map["eve@example.com"]);
        Assert.Null(map["mallory@example.com"]);
        Assert.Null(map["none@example.com"]);
        Assert.Equal("https://api.github.com/repos/octo/hello/commits?sha=main&per_page=100", Assert.Single(recorder.Sent).Url.AbsoluteUri);
        Assert.Equal(
            "GitHub named an avatar on https://evil.example:443, which is not GitHub's avatar host: such authors are shown without one.",
            Assert.Single(log.Lines).Message);
    }

    [Fact]
    public async Task Avatars_off_the_web_address_are_dropped_with_one_warning()
    {
        // The fake in process names its avatars on Local(1); a provider of another port's endpoints does not take them.
        await using var setup = await Setup.StartAsync(GitHubEndpoints.Local(47395));

        var account = await setup.Provider.CheckTokenAsync(setup.Served.OctoToken, Cancelled);
        var authors = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, new HostedRepository(HostingProvider.GitHub, "127.0.0.1:47395", "octo", "hello"), "main", [], Cancelled);

        Assert.Equal(("127.0.0.1:47395", (Uri?)null), (account.Value!.Key.Host, account.Value.AvatarUrl));
        Assert.All(authors.Value!.ByEmail.Values, Assert.Null);
        Assert.Equal(3, authors.Value.ByEmail.Count);
        Assert.Equal(
            "GitHub named an avatar on http://127.0.0.1:1, which is not GitHub's avatar host: such authors are shown without one.",
            Assert.Single(setup.Log.Lines).Message);
    }

    [Fact]
    public async Task Recognition_and_the_push_guard_need_no_request()
    {
        await using var setup = await Setup.StartAsync();

        Assert.Equal((HostingProvider.GitHub, GitHubEndpoints.Local(FakeGitHub.InProcessPort)), (setup.Provider.Kind, setup.Provider.Endpoints));
        Assert.Equal(Repository("octo", "hello"), setup.Provider.Recognise("http://127.0.0.1:1/octo/hello.git"));
        Assert.Null(setup.Provider.Recognise(Scenarios.HostingUrl("octo/hello")));
        Assert.False(setup.Provider.SourceCanExist("refs/pull/fix-typo/head"));
        Assert.True(setup.Provider.SourceCanExist("refs/pull/5/head"));
        Assert.Empty(setup.Fake.Requests);
    }

    private static HostedRepository Repository(string owner, string name) => new(HostingProvider.GitHub, Host, owner, name);

    /// <summary>A copy of "hosting" served in process, and a provider over its handler (on its endpoints unless given).</summary>
    private sealed class Setup : IAsyncDisposable
    {
        private Setup(ServedHosting served, GitHubProvider provider, RecordingHttpHandler recorder, ListLog log)
        {
            Served = served;
            Provider = provider;
            Recorder = recorder;
            Log = log;
        }

        public ServedHosting Served { get; }

        public FakeGitHub Fake => Served.GitHub;

        public GitHubProvider Provider { get; }

        public RecordingHttpHandler Recorder { get; }

        public ListLog Log { get; }

        public static async Task<Setup> StartAsync(GitHubEndpoints? endpoints = null)
        {
            var hello = await Scenarios.HostingAsync();
            ServedHosting served;
            try
            {
                served = await Scenarios.ServeHostingAsync(hello, inProcess: true, ct: Cancelled, ownsCopy: true);
            }
            catch
            {
                hello.Dispose();
                throw;
            }

            var log = new ListLog();
            var recorder = new RecordingHttpHandler(served.GitHub.CreateHandler());
            return new Setup(served, new GitHubProvider(endpoints ?? served.GitHub.Endpoints, () => recorder, log, new ManualTimeProvider(AppClocks.HarnessTime)), recorder, log);
        }

        public async ValueTask DisposeAsync()
        {
            Provider.Dispose();
            Assert.Empty(Fake.Errors);
            Assert.Equal(Fake.Requests.Count, Recorder.Sent.Count);
            Assert.All(Recorder.Sent, sent =>
            {
                Assert.Equal(GitHubApi.MediaType, sent.Header("Accept"));
                Assert.Equal(GitHubApi.Version, sent.Header("X-GitHub-Api-Version"));
                Assert.Equal(GitHubClient.UserAgent, sent.Header("User-Agent"));
                Assert.StartsWith("Bearer ", sent.Header("Authorization"), StringComparison.Ordinal);
                Assert.Equal(sent.Method == "POST" ? "application/json; charset=utf-8" : null, sent.Header("Content-Type"));
            });
            Assert.All(Fake.Requests, request => Assert.DoesNotContain("_FAKE-", request.ToString(), StringComparison.Ordinal));
            await Served.DisposeAsync();
        }
    }
}
