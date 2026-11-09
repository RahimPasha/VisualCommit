using System.Net;
using VisualCommit.Core;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting.GitHub;
using VisualCommit.Hosting.Http;
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

    [Theory]
    [InlineData("A title", "feature/login", "main", false, HostingFailureKind.Validation, "A pull request already exists for octo:feature/login.", PullRequestRefusal.AlreadyExists)]
    [InlineData("A title", "main", "main", false, HostingFailureKind.Validation, "No commits between main and main", PullRequestRefusal.NoCommits)]
    [InlineData("A title", "nope", "main", false, HostingFailureKind.Validation, "head is invalid", PullRequestRefusal.HeadMissing)]
    [InlineData("A title", "topic/no-pr", "nope", false, HostingFailureKind.Validation, "base is invalid", PullRequestRefusal.BaseMissing)]
    [InlineData("", "topic/no-pr", "main", false, HostingFailureKind.Validation, "title is missing", PullRequestRefusal.None)]
    [InlineData("A title", "fix-typo", "main", true, HostingFailureKind.Forbidden, "pull_requests=write", PullRequestRefusal.None)]
    public async Task A5s_refusal_is_the_reason_githubs_detail_stands_for_and_its_words_stay_githubs(
        string title, string head, string @base, bool adaOnTool, HostingFailureKind kind, string detail, PullRequestRefusal refusal)
    {
        await using var setup = await Setup.StartAsync();
        var (token, repository) = adaOnTool ? (setup.Served.AdaToken, Repository("octo", "tool")) : (setup.Served.OctoToken, Repository("octo", "hello"));

        var refused = await setup.Provider.CreatePullRequestAsync(token, repository, new NewPullRequest(title, "", head, null, @base, false), Cancelled);

        var failure = refused.Failure!;
        Assert.Equal((kind, detail, refusal, HostingProvider.GitHub), (failure.Kind, failure.Detail, failure.Refusal, failure.Provider));
        Assert.Equal(adaOnTool ? "Resource not accessible by personal access token" : "Validation Failed", failure.Message);
        Assert.Single(setup.Fake.Requests);
    }

    [Theory]
    [InlineData("A pull request already exists for octo:user@fix.", "A pull request already exists for <e-mail>", PullRequestRefusal.AlreadyExists)]
    [InlineData("No commits between main and user@fix", "No commits between main and <e-mail>", PullRequestRefusal.NoCommits)]
    public void A5s_refusal_holds_when_the_e_mail_screen_took_a_branch_name_with_an_at_sign(string githubs, string screened, PullRequestRefusal refusal)
    {
        // The core screens every failure's detail (D368) before the provider reads it.
        var detail = HostingHttpClient.ScreenEmail(githubs);

        Assert.Equal((screened, refusal), (detail, GitHubProvider.RefusalOf(detail)));
    }

    /// <summary>GitLab's form and Azure DevOps' (84 characters, <c>AZDO</c> at 76), each with the detail that names it (D378).</summary>
    public static TheoryData<string, string> OtherProvidersTokens => new()
    {
        { "glpat-FAKE-ada-1", "other-provider:gitlab" },
        { new string('a', 76) + "AZDO" + "abcd", "other-provider:azure-devops" },
    };

    [Theory]
    [MemberData(nameof(OtherProvidersTokens))]
    public async Task A1_refuses_another_providers_token_without_a_request(string token, string detail)
    {
        await using var setup = await Setup.StartAsync();

        var refused = await setup.Provider.CheckTokenAsync(token, Cancelled);
        var throughTheInterface = await ((IHostingProvider)setup.Provider).CheckTokenAsync(token, null, Cancelled);

        foreach (var failure in new[] { refused.Failure!, throughTheInterface.Failure! })
        {
            Assert.Equal((HostingFailureKind.BadCredentials, "Bad credentials", detail, HostingProvider.GitHub), (failure.Kind, failure.Message, failure.Detail, failure.Provider));
        }

        Assert.Empty(setup.Fake.Requests);
    }

    [Fact]
    public async Task A6_maps_the_branchs_authors_then_looks_up_the_others_once_each()
    {
        await using var setup = await Setup.StartAsync();
        var main = await TipAsync(setup, "octo/hello", "main");

        var authors = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "hello"), "main",
            OnMain(main, "ADA@example.com", "Stranger@Example.com", "", "stranger@example.com"), Cancelled);

        var map = authors.Value!.ByEmail;
        Assert.Equal(new[] { "ada@example.com", "author@example.com", "octo@example.com", "stranger@example.com" }, map.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new AuthorAvatar(1001, "octo", new Uri("http://127.0.0.1:1/avatars/u/1001?v=4")), map["octo@example.com"]);
        Assert.Equal(new AuthorAvatar(1003, "ada", new Uri("http://127.0.0.1:1/avatars/u/1003?v=4")), map["ada@example.com"]);
        Assert.Null(map["author@example.com"]);
        Assert.Null(map["stranger@example.com"]);
        Assert.Equal(new[] { "sha=main&per_page=100", $"sha={main}&author=stranger%40example.com&per_page=1" }, setup.Fake.Requests.Select(request => request.Query));

        var branch = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "hello"), "feature/login", [], Cancelled);
        Assert.True(branch.Succeeded);
        Assert.Equal("sha=feature%2Flogin&per_page=100", setup.Fake.Requests[^1].Query);
    }

    [Fact]
    public async Task A6_without_a_branch_asks_no_commits_page_and_at_most_twenty_lookups()
    {
        await using var setup = await Setup.StartAsync();
        var main = await TipAsync(setup, "octo/hello", "main");
        string[] emails = [Scenarios.OctoEmail, .. Enumerable.Range(1, 24).Select(n => FormattableString.Invariant($"user{n:D2}@example.com"))];

        var authors = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "hello"), null, OnMain(main, emails), Cancelled);

        var map = authors.Value!.ByEmail;
        Assert.Equal(20, map.Count);
        Assert.Equal(new AuthorAvatar(1001, "octo", new Uri("http://127.0.0.1:1/avatars/u/1001?v=4")), map[Scenarios.OctoEmail]);
        Assert.All(emails[1..20], email => Assert.Null(map[email]));
        Assert.DoesNotContain(emails[20], map.Keys);
        Assert.Equal(20, setup.Fake.Requests.Count);
        Assert.All(setup.Fake.Requests, request => Assert.StartsWith($"sha={main}&author=", request.Query, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A6_looks_an_address_up_from_its_own_commit_in_the_repository_that_holds_it()
    {
        await using var setup = await Setup.StartAsync();

        // Ada's fix is only on her fork's fix-typo: listed as octo/hello, looked up on ada/hello from it.
        var fix = await TipAsync(setup, "ada/hello", "fix-typo");
        var authors = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "hello"), null,
            [new AuthorCommit("Ada@Example.com", fix, Repository("ada", "hello"))], Cancelled);

        Assert.Equal(new AuthorAvatar(1003, "ada", new Uri("http://127.0.0.1:1/avatars/u/1003?v=4")), Assert.Single(authors.Value!.ByEmail, pair => pair.Key == Scenarios.AdaEmail).Value);
        var lookup = Assert.Single(setup.Fake.Requests);
        Assert.Equal(("commits:ada/hello", $"sha={fix}&author=ada%40example.com&per_page=1"), (lookup.Route, lookup.Query));
    }

    [Theory]
    [InlineData(404)]
    [InlineData(500)]
    public async Task A6_a_repository_github_does_not_give_is_asked_and_read_once_a_commit_it_lacks_leaves_out_one_address_and_the_others_go_on(int lacking)
    {
        await using var setup = await Setup.StartAsync();
        setup.Fake.UnknownRevisionBesideAuthorAnswers500 = lacking == 500;
        var main = await TipAsync(setup, "octo/hello", "main");

        // A repository GitHub does not have (404, and 404 again when it is read, D395) and an empty
        // one (409), each asked once for its two addresses (ada/gone's second in another case); a
        // commit octo/hello, the listed repository, does not have, which leaves out its address alone
        // without a read: 404 "Not Found" as github.com answered it beside author in the live run of
        // 2026-11-08 (D394), or 500 without a body as it did on 2026-10-30 and 31, a 5xx (D342); then
        // octo on main.
        var authors = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "hello"), null,
            [
                new AuthorCommit(Scenarios.AdaEmail, main, Repository("ada", "gone")),
                new AuthorCommit("author@example.com", main, Repository("Ada", "Gone")),
                new AuthorCommit("first@example.com", main, Repository("octo", "notes")),
                new AuthorCommit("second@example.com", main, Repository("octo", "notes")),
                new AuthorCommit("stranger@example.com", new string('0', 40), Repository("octo", "hello")),
                new AuthorCommit(Scenarios.OctoEmail, main, Repository("octo", "hello")),
            ],
            Cancelled);

        Assert.Equal(new AuthorAvatar(1001, "octo", new Uri("http://127.0.0.1:1/avatars/u/1001?v=4")), Assert.Single(authors.Value!.ByEmail).Value);
        Assert.Equal(
            [("commits:ada/gone", 404), ("repo:ada/gone", 404), ("commits:octo/notes", 409), ("commits:octo/hello", lacking), ("commits:octo/hello", 200)],
            setup.Fake.Requests.Select(request => (request.Route, request.Status)));
    }

    [Theory]
    [InlineData(404)]
    [InlineData(500)]
    public async Task A6_a_commit_a_fork_lacks_leaves_out_its_own_address_alone_the_fork_read_once_when_answered_404(int lacking)
    {
        await using var setup = await Setup.StartAsync();
        setup.Fake.UnknownRevisionBesideAuthorAnswers500 = lacking == 500;
        var fix = await TipAsync(setup, "ada/hello", "fix-typo");

        // Listed as octo/hello; every lookup goes to ada/hello, its fork, which the token reads: from
        // a commit the fork lacks, then Ada's from her fix-typo, then another commit it lacks.
        // github.com answered such a lookup 500 without a body on 2026-10-30 and 31 (D342), and 404
        // "Not Found" on 2026-11-08 (D394; in the listed repository, a fork presumed the same). The
        // first 404 in the fork, not yet known readable, is followed by the fork's read, which
        // succeeds, so the 404 is the commit's (D395): Ada is still asked and found, and the second
        // 404 is taken so without another read.
        var authors = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "hello"), null,
            [
                new AuthorCommit("stranger@example.com", new string('0', 40), Repository("ada", "hello")),
                new AuthorCommit(Scenarios.AdaEmail, fix, Repository("ada", "hello")),
                new AuthorCommit("lost@example.com", new string('1', 40), Repository("ada", "hello")),
            ],
            Cancelled);

        Assert.Equal(new AuthorAvatar(1003, "ada", new Uri("http://127.0.0.1:1/avatars/u/1003?v=4")), Assert.Single(authors.Value!.ByEmail).Value);
        (string?, int)[] sent = lacking == 404
            ? [("commits:ada/hello", 404), ("repo:ada/hello", 200), ("commits:ada/hello", 200), ("commits:ada/hello", 404)]
            : [("commits:ada/hello", 500), ("commits:ada/hello", 200), ("commits:ada/hello", 500)];
        Assert.Equal(sent, setup.Fake.Requests.Select(request => (request.Route, request.Status)));
    }

    [Fact]
    public async Task A6_an_empty_repository_is_an_empty_map_a_server_error_leaves_out_one_lookup_and_a_refusal_stops_them()
    {
        await using var setup = await Setup.StartAsync();

        var main = await TipAsync(setup, "octo/hello", "main");
        var empty = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "notes"), "main", OnMain(main, Scenarios.OctoEmail), Cancelled);
        Assert.Equal((true, 0), (empty.Succeeded, empty.Value!.ByEmail.Count));
        Assert.Single(setup.Fake.Requests);

        // GitHub could not answer one lookup (a 5xx): only that address is left out (D342).
        setup.Fake.FailNext("commits:octo/hello", FakeGitHubFault.ServerError);
        var skipped = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "hello"), null, OnMain(main, Scenarios.OctoEmail, Scenarios.AdaEmail), Cancelled);
        Assert.Equal(new AuthorAvatar(1003, "ada", new Uri("http://127.0.0.1:1/avatars/u/1003?v=4")), Assert.Single(skipped.Value!.ByEmail, pair => pair.Key == Scenarios.AdaEmail).Value);
        Assert.Single(skipped.Value.ByEmail);
        Assert.Equal(3, setup.Fake.Requests.Count);

        // A refusal of the token stops them, the refused one left out too.
        foreach (var refusal in new[] { FakeGitHubFault.PrimaryRateLimit, FakeGitHubFault.BadCredentials })
        {
            setup.Fake.FailNext("commits:octo/hello", refusal);
            var stopped = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "hello"), null, OnMain(main, Scenarios.OctoEmail, Scenarios.AdaEmail), Cancelled);
            Assert.Equal((true, 0), (stopped.Succeeded, stopped.Value!.ByEmail.Count));
        }

        Assert.Equal(5, setup.Fake.Requests.Count);

        setup.Fake.FailNext("commits:octo/hello", FakeGitHubFault.ServerError);
        var failed = await setup.Provider.ReadAuthorsAsync(setup.Served.OctoToken, Repository("octo", "hello"), "main", OnMain(main, Scenarios.OctoEmail), Cancelled);
        Assert.Equal(HostingFailureKind.ServerError, failed.Failure!.Kind);
        Assert.Equal(6, setup.Fake.Requests.Count);
    }

    [Fact]
    public async Task A6_a_lookup_github_cannot_answer_leaves_out_its_address_and_a_404_on_another_repository_that_does_not_read_leaves_out_that_repository()
    {
        // Recorded answers, each standing for one of github.com's. A 422, or a 500 without a body
        // (github.com's answer to a commit it lacks beside author= in the live runs of 2026-10-30
        // and 31, D342), leaves out its own address, on the listed repository or on the fork a lookup
        // goes to; so does a 404 on the listed repository, github.com's answer to such a commit in the
        // live run of 2026-11-08 (D394), and on another repository known readable: the fork, once it
        // has answered a lookup. A 404 on another repository not yet known readable is followed by
        // that repository's read, once (D395): eve/gone's 404 and cal/hello's 500 leave out the rest
        // of that repository's; bob/hello reads, so its 404 was the commit's and its next address is
        // asked. A 401 stops them.
        static string Found(string login, int id) =>
            $$$"""[{"sha":"x","commit":{"author":{"email":"x@example.com"}},"author":{"login":"{{{login}}}","id":{{{id}}},"avatar_url":"https://avatars.githubusercontent.com/u/{{{id}}}?v=4"}}]""";
        const string bobHello = """{"name":"hello","owner":{"login":"bob"},"default_branch":"main","clone_url":"https://github.com/bob/hello.git","html_url":"https://github.com/bob/hello","updated_at":"2026-01-02T03:04:05Z","permissions":{"push":false}}""";
        var recorder = new RecordingHttpHandler(request => (request.RequestUri!.AbsolutePath, request.RequestUri.Query.Split('&')[0]) switch
        {
            ("/repos/eve/gone", _) => RecordingHttpHandler.Json(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}"),
            ("/repos/cal/hello", _) => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            ("/repos/bob/hello", _) => RecordingHttpHandler.Json(HttpStatusCode.OK, bobHello),
            (_, "?sha=1" or "?sha=5") => RecordingHttpHandler.Json(HttpStatusCode.UnprocessableContent, "{\"message\":\"No commit found for SHA\"}"),
            (_, "?sha=2" or "?sha=7" or "?sha=13" or "?sha=15" or "?sha=17") => RecordingHttpHandler.Json(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}"),
            (_, "?sha=3" or "?sha=4") => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            (_, "?sha=6") => RecordingHttpHandler.Json(HttpStatusCode.OK, Found("ada", 1003)),
            (_, "?sha=9") => RecordingHttpHandler.Json(HttpStatusCode.OK, Found("eff", 1006)),
            (_, "?sha=11") => RecordingHttpHandler.Json(HttpStatusCode.Unauthorized, "{\"message\":\"Bad credentials\"}"),
            _ => RecordingHttpHandler.Json(HttpStatusCode.OK, "[]"),
        });
        using var provider = new GitHubProvider(GitHubEndpoints.GitHubCom, () => recorder, new ListLog(), new ManualTimeProvider());
        var listed = new HostedRepository(HostingProvider.GitHub, "github.com", "octo", "hello");
        var fork = new HostedRepository(HostingProvider.GitHub, "github.com", "ada", "hello");
        var gone = new HostedRepository(HostingProvider.GitHub, "github.com", "eve", "gone");
        var other = new HostedRepository(HostingProvider.GitHub, "github.com", "bob", "hello");
        var failing = new HostedRepository(HostingProvider.GitHub, "github.com", "cal", "hello");

        var authors = await provider.ReadAuthorsAsync("github_pat_FAKE-octo-1", listed, null,
            [
                new AuthorCommit("a@example.com", "1", listed),
                new AuthorCommit("b@example.com", "2", listed with { Owner = "Octo", Name = "HELLO" }),
                new AuthorCommit("c@example.com", "3", listed),
                new AuthorCommit("d@example.com", "4", fork),
                new AuthorCommit("e@example.com", "5", fork),
                new AuthorCommit("f@example.com", "6", fork),
                new AuthorCommit("g@example.com", "7", fork),
                new AuthorCommit("h@example.com", "8", fork),
                new AuthorCommit("m@example.com", "13", gone),
                new AuthorCommit("n@example.com", "14", gone),
                new AuthorCommit("o@example.com", "15", other),
                new AuthorCommit("p@example.com", "16", other),
                new AuthorCommit("q@example.com", "17", failing),
                new AuthorCommit("r@example.com", "18", failing),
                new AuthorCommit("i@example.com", "9", listed),
                new AuthorCommit("j@example.com", "10", listed),
                new AuthorCommit("k@example.com", "11", listed),
                new AuthorCommit("l@example.com", "12", listed),
            ],
            Cancelled);

        Assert.True(authors.Succeeded);
        Assert.Equal(new[] { "f@example.com", "h@example.com", "i@example.com", "j@example.com", "p@example.com" }, authors.Value!.ByEmail.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new AuthorAvatar(1003, "ada", new Uri("https://avatars.githubusercontent.com/u/1003?v=4")), authors.Value.ByEmail["f@example.com"]);
        Assert.Equal(new AuthorAvatar(1006, "eff", new Uri("https://avatars.githubusercontent.com/u/1006?v=4")), authors.Value.ByEmail["i@example.com"]);
        Assert.Null(authors.Value.ByEmail["h@example.com"]);
        Assert.Null(authors.Value.ByEmail["j@example.com"]);
        Assert.Null(authors.Value.ByEmail["p@example.com"]);
        Assert.Equal(
            new[]
            {
                "octo/hello 1", "Octo/HELLO 2", "octo/hello 3", "ada/hello 4", "ada/hello 5", "ada/hello 6", "ada/hello 7", "ada/hello 8",
                "eve/gone 13", "read eve/gone", "bob/hello 15", "read bob/hello", "bob/hello 16", "cal/hello 17", "read cal/hello",
                "octo/hello 9", "octo/hello 10", "octo/hello 11",
            },
            recorder.Sent.Select(sent => sent.Url.AbsolutePath.EndsWith("/commits", StringComparison.Ordinal)
                ? $"{sent.Url.AbsolutePath["/repos/".Length..^"/commits".Length]} {sent.Url.Query.Split('&')[0]["?sha=".Length..]}"
                : $"read {sent.Url.AbsolutePath["/repos/".Length..]}"));
    }

    [Theory]
    [InlineData("401", true)]
    [InlineData("403 rate limit", true)]
    [InlineData("429 secondary limit", true)]
    [InlineData("400 version", true)]
    [InlineData("no answer", true)]
    [InlineData("not read", true)]
    [InlineData("403", false)]
    [InlineData("403 single sign-on", false)]
    public async Task A6_a_read_after_a_404_that_the_token_fails_stops_the_lookups_and_one_the_repository_refuses_leaves_that_repository_out(string answer, bool stops)
    {
        // Recorded answers. A lookup in eve/gone, a repository other than the listed one, answered
        // 404, so eve/gone is read (D395), and the read answers as a lookup could. The token refused
        // (401) or limited (403 with no request left, 429 with Retry-After), the API version
        // refused, no answer, or an answer not read stop the lookups, as the same failure of a lookup
        // does: nothing is sent after the read. A 403 of the repository's own (a permission, single
        // sign-on) leaves eve/gone out with its other lookup, and the listed repository's go on, as
        // after a read answered 404 or 500 (the recorded answers above).
        var recorder = new RecordingHttpHandler(request => (request.RequestUri!.AbsolutePath, answer) switch
        {
            ("/repos/eve/gone", "401") => RecordingHttpHandler.Json(HttpStatusCode.Unauthorized, """{"message":"Bad credentials"}"""),
            ("/repos/eve/gone", "403 rate limit") => RecordingHttpHandler.Json(HttpStatusCode.Forbidden, """{"message":"API rate limit exceeded for user ID 1001."}""", ("X-RateLimit-Remaining", "0"), ("X-RateLimit-Reset", "1798761600")),
            ("/repos/eve/gone", "429 secondary limit") => RecordingHttpHandler.Json(HttpStatusCode.TooManyRequests, """{"message":"You have exceeded a secondary rate limit."}""", ("Retry-After", "60")),
            ("/repos/eve/gone", "400 version") => RecordingHttpHandler.Json(HttpStatusCode.BadRequest, """{"message":"Bad Request","errors":"The version you specified in the \"X-GitHub-Api-Version\" request header is not a supported version."}"""),
            ("/repos/eve/gone", "no answer") => throw new HttpRequestException("No such host is known. (api.github.com:443)"),
            ("/repos/eve/gone", "not read") => RecordingHttpHandler.Json(HttpStatusCode.OK, """{"name":"""),
            ("/repos/eve/gone", "403") => RecordingHttpHandler.Json(HttpStatusCode.Forbidden, """{"message":"Resource not accessible by personal access token"}"""),
            ("/repos/eve/gone", "403 single sign-on") => RecordingHttpHandler.Json(HttpStatusCode.Forbidden, """{"message":"Resource protected by organization SAML enforcement."}""", ("X-GitHub-SSO", "required; url=https://github.com/orgs/eve/sso?authorization_request=1")),
            ("/repos/eve/gone/commits", _) => RecordingHttpHandler.Json(HttpStatusCode.NotFound, """{"message":"Not Found"}"""),
            _ => RecordingHttpHandler.Json(HttpStatusCode.OK, "[]"),
        });
        using var provider = new GitHubProvider(GitHubEndpoints.GitHubCom, () => recorder, new ListLog(), new ManualTimeProvider());
        var listed = new HostedRepository(HostingProvider.GitHub, "github.com", "octo", "hello");
        var gone = new HostedRepository(HostingProvider.GitHub, "github.com", "eve", "gone");

        var authors = await provider.ReadAuthorsAsync("github_pat_FAKE-octo-1", listed, null,
            [
                new AuthorCommit("m@example.com", "13", gone),
                new AuthorCommit("n@example.com", "14", gone),
                new AuthorCommit("i@example.com", "9", listed),
                new AuthorCommit("j@example.com", "10", listed),
            ],
            Cancelled);

        string[] found = stops ? [] : ["i@example.com", "j@example.com"];
        string[] requests = stops ? ["eve/gone 13", "read eve/gone"] : ["eve/gone 13", "read eve/gone", "octo/hello 9", "octo/hello 10"];
        Assert.True(authors.Succeeded);
        Assert.Equal(found, authors.Value!.ByEmail.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            requests,
            recorder.Sent.Select(sent => sent.Url.AbsolutePath.EndsWith("/commits", StringComparison.Ordinal)
                ? $"{sent.Url.AbsolutePath["/repos/".Length..^"/commits".Length]} {sent.Url.Query.Split('&')[0]["?sha=".Length..]}"
                : $"read {sent.Url.AbsolutePath["/repos/".Length..]}"));
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

    // Made up, a broken or hostile server's (the review before the gate, tokens-2): an address of
    // another scheme than http or https. On Windows .NET reads //h/s as file://h/s, a share a clone
    // would reach over SMB. The same answers with https addresses are read first, so the scheme alone
    // makes them Malformed: A2 and A3 for clone_url or html_url, A4 and A5 for a pull's html_url,
    // as 7a's unreadable fields do.
    [Theory]
    [InlineData("clone_url", "//h/s")]
    [InlineData("clone_url", "file:///x")]
    [InlineData("clone_url", "ext::sh -c x")]
    [InlineData("html_url", "//h/s")]
    [InlineData("html_url", "file:///x")]
    [InlineData("html_url", "ext::sh -c x")]
    public async Task An_address_of_another_scheme_than_http_or_https_is_unreadable_in_A2_to_A5(string field, string address)
    {
        var bad = false;
        string Of(string name, string https) => bad && name == field ? address : https;
        string Repo() => System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["name"] = "hello",
            ["owner"] = new Dictionary<string, string> { ["login"] = "octo" },
            ["default_branch"] = "main",
            ["clone_url"] = Of("clone_url", "https://github.com/octo/hello.git"),
            ["html_url"] = Of("html_url", "https://github.com/octo/hello"),
            ["updated_at"] = "2026-01-02T03:04:05Z",
            ["permissions"] = new Dictionary<string, bool> { ["push"] = true },
        });
        string Pull() => System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["number"] = 7,
            ["title"] = "T",
            ["user"] = new Dictionary<string, string> { ["login"] = "octo" },
            ["html_url"] = Of("html_url", "https://github.com/octo/hello/pull/7"),
            ["head"] = new Dictionary<string, object?> { ["ref"] = "fix", ["sha"] = new string('a', 40), ["repo"] = null },
            ["base"] = new Dictionary<string, object?> { ["ref"] = "main", ["sha"] = new string('b', 40), ["repo"] = null },
            ["created_at"] = "2026-01-02T03:04:05Z",
        });
        var recorder = new RecordingHttpHandler(request => (request.Method.Method, request.RequestUri!.AbsolutePath) switch
        {
            ("GET", "/user/repos") => RecordingHttpHandler.Json(HttpStatusCode.OK, "[" + Repo() + "]"),
            ("GET", "/repos/octo/hello") => RecordingHttpHandler.Json(HttpStatusCode.OK, Repo()),
            ("GET", "/repos/octo/hello/pulls") => RecordingHttpHandler.Json(HttpStatusCode.OK, "[" + Pull() + "]"),
            ("POST", "/repos/octo/hello/pulls") => RecordingHttpHandler.Json(HttpStatusCode.Created, Pull()),
            _ => RecordingHttpHandler.Json(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}"),
        });
        var log = new ListLog();
        using var provider = new GitHubProvider(GitHubEndpoints.GitHubCom, () => recorder, log, new ManualTimeProvider());
        var hello = new HostedRepository(HostingProvider.GitHub, "github.com", "octo", "hello");
        var newPull = new NewPullRequest("T", "", "fix", null, "main", Draft: false);
        async Task<HostingFailure?[]> AllAsync() =>
        [
            (await provider.ListRepositoriesAsync("github_pat_FAKE-octo-1", Cancelled)).Failure,
            (await provider.ReadRepositoryAsync("github_pat_FAKE-octo-1", hello, Cancelled)).Failure,
            (await provider.ListPullRequestsAsync("github_pat_FAKE-octo-1", hello, Cancelled)).Failure,
            (await provider.CreatePullRequestAsync("github_pat_FAKE-octo-1", hello, newPull, Cancelled)).Failure,
        ];

        Assert.All(await AllAsync(), Assert.Null);
        bad = true;
        var failures = await AllAsync();

        HostingFailureKind? pulls = field == "html_url" ? HostingFailureKind.Malformed : null;
        Assert.Equal([HostingFailureKind.Malformed, HostingFailureKind.Malformed, pulls, pulls], failures.Select(failure => failure?.Kind));
        Assert.All(failures.OfType<HostingFailure>(), failure => Assert.Equal(("GitHub's answer could not be read.", HostingProvider.GitHub), (failure.Message, failure.Provider)));
        Assert.Equal(8, recorder.Sent.Count);
        Assert.All(log.Lines, line => Assert.DoesNotContain("h/s", line.Message, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_avatar_whose_host_has_no_idn_form_is_dropped_with_one_warning_and_nothing_throws(bool local)
    {
        // D380: Uri.IdnHost throws on such a host (measured), on github.com's rule and on the fake's.
        var recorder = new RecordingHttpHandler(_ => RecordingHttpHandler.Json(HttpStatusCode.OK, "{\"login\":\"ada\",\"id\":1003,\"avatar_url\":\"https://a\u200Db.example/u/1003\"}"));
        var log = new ListLog();
        using var provider = new GitHubProvider(local ? GitHubEndpoints.Local(47395) : GitHubEndpoints.GitHubCom, () => recorder, log, new ManualTimeProvider());

        var account = await provider.CheckTokenAsync("github_pat_FAKE-ada-1", Cancelled);

        Assert.Equal(("ada", (Uri?)null), (account.Value!.Login, account.Value.AvatarUrl));
        Assert.Equal(
            "GitHub named an avatar on https://(an unreadable host):443, which is not GitHub's avatar host: such authors are shown without one.",
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

    /// <summary><paramref name="emails"/> to look up, each from <paramref name="main"/> on octo/hello.</summary>
    private static AuthorCommit[] OnMain(string main, params string[] emails) => [.. emails.Select(email => new AuthorCommit(email, main, Repository("octo", "hello")))];

    /// <summary>The tip of <paramref name="branch"/> in the fake's <paramref name="repository"/> (<c>owner/name</c>).</summary>
    private static async Task<string> TipAsync(Setup setup, string repository, string branch) =>
        (await setup.Served.Hello.GitAsync("ls-remote", setup.Fake.Repository(repository).BarePath!, "refs/heads/" + branch)).StandardOutput.Split('\t')[0];

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
