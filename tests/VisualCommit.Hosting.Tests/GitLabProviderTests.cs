using System.Globalization;
using System.Net;
using System.Text;
using VisualCommit.Core;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting.GitLab;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// GitLab as a hosting service (docs/phase-7b-design.md 7.4, 7.6), against the fake in process and
/// scripted answers: A1 by one <c>GET user</c> for both token kinds and its refusals, local ones
/// without a request (D378); A2's query, cap and rows; A3 by path, moved and a fork; A4's fields,
/// cap and the forks' lookups (none for a project's own requests, one per fork, a hidden fork null, a
/// failing lookup failing, 20 a call, kept until <see cref="GitLabProvider.ForgetKept"/>); A5 in one
/// project and across forks, its draft titles, its description as typed, each refusal, one POST a
/// call, none after a failed GET, and a moved source.
/// </summary>
public sealed class GitLabProviderTests
{
    private const string Host = "127.0.0.1:3";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A1_gives_the_tokens_account_for_both_kinds_by_one_GET_user_each()
    {
        await using var lab = await GitLabLab.CreateAsync(withGit: false);
        using var provider = lab.Provider();

        var fine = await provider.CheckTokenAsync(lab.Ada, null, Cancelled);
        var legacy = await provider.CheckTokenAsync(lab.AdaLegacy, "ignored", Cancelled);

        foreach (var result in new[] { fine, legacy })
        {
            var account = result.Value!;
            Assert.Equal(("gitlab:127.0.0.1:3:ada", "ada", 2001L, "Ada Lovelace", (Uri?)null, true, (string?)null),
                (account.Key.Id, account.Login, account.Id, account.Name, account.AvatarUrl, account.UseForGit, account.UserId));
            Assert.Equal(new AccountKey(HostingProvider.GitLab, Host, "ada"), account.Key);
        }

        Assert.Equal(
            [("GET", "/api/v4/user", FakeAuthorizationForm.Bearer), ("GET", "/api/v4/user", FakeAuthorizationForm.Bearer)],
            lab.Fake.Requests.Select(request => (request.Method, request.Path, request.Authorization)));
        lab.AssertNoSecret();
    }

    [Fact]
    public async Task A1_keeps_the_logins_case_and_drops_a_name_that_is_an_address()
    {
        await using var lab = await GitLabLab.CreateAsync(withGit: false);
        lab.Fake.AddUser("Carol.Doe", 2010, "carol@example.com");
        lab.Fake.AddUser("dave", 2011, "Dave @ the desk");
        using var provider = lab.Provider();

        var carol = await provider.CheckTokenAsync(lab.Fake.AddToken("Carol.Doe"), null, Cancelled);
        var dave = await provider.CheckTokenAsync(lab.Fake.AddToken("dave"), null, Cancelled);

        Assert.Equal(("Carol.Doe", "gitlab:127.0.0.1:3:carol.doe", (string?)null), (carol.Value!.Login, carol.Value.Key.Id, carol.Value.Name));
        Assert.Equal("Dave @ the desk", dave.Value!.Name);
    }

    [Fact]
    public async Task A1_is_refused_for_an_unknown_revoked_or_expired_token_and_one_without_User_Read()
    {
        await using var lab = await GitLabLab.CreateAsync(withGit: false);
        using var provider = lab.Provider();
        var projectsOnly = lab.Fake.AddToken("ada", permissions: [FakeGitLabPermissions.ProjectRead]);
        lab.Fake.Revoke(lab.Octo);
        lab.Fake.Expire(lab.Bob);

        var unknown = await provider.CheckTokenAsync("glpat-FAKE-nobody-1", null, Cancelled);
        var revoked = await provider.CheckTokenAsync(lab.Octo, null, Cancelled);
        var expired = await provider.CheckTokenAsync(lab.Bob, null, Cancelled);
        var noUser = await provider.CheckTokenAsync(projectsOnly, null, Cancelled);

        Assert.Equal(
            [
                (HostingFailureKind.BadCredentials, "401 Unauthorized", (string?)null),
                (HostingFailureKind.BadCredentials, "Token was revoked. You have to re-authorize from the user.", null),
                (HostingFailureKind.BadCredentials, "Token is expired. You can either do re-authorization or token refresh.", null),
                (HostingFailureKind.Forbidden, "Access denied: This operation requires a fine-grained personal access token with the following user permissions: [User: Read].", "insufficient_granular_scope"),
            ],
            new[] { unknown, revoked, expired, noUser }.Select(result => (result.Failure!.Kind, result.Failure.Message, result.Failure.Detail)));
        Assert.All(new[] { unknown, revoked, expired, noUser }, result => Assert.Equal(HostingProvider.GitLab, result.Failure!.Provider));
        Assert.Equal(4, lab.Fake.Requests.Count);
        Assert.All(lab.Fake.Requests, request => Assert.Equal("/api/v4/user", request.Path));
    }

    [Fact]
    public async Task An_empty_token_one_beyond_visible_ASCII_and_another_providers_are_refused_on_every_method_without_a_request()
    {
        await using var lab = await GitLabLab.CreateAsync(withGit: false);
        using var provider = lab.Provider();
        var repository = new HostedRepository(HostingProvider.GitLab, Host, "g/s", "p");
        var account = new HostingAccount(new AccountKey(HostingProvider.GitLab, Host, "ada"), "ada", 2001, null, null, true);
        var create = new NewPullRequest("T", "", "fix", null, "main", false);
        var cases = new (string Token, string Message, string? Detail)[]
        {
            ("", "The token is empty.", null),
            ("   ", "The token is empty.", null),
            ("glpat-FAKE ada", "401 Unauthorized", null),
            ("glpat-é", "401 Unauthorized", null),
            ("glpat-x\r\nX-Evil: 1", "401 Unauthorized", null),
            ("github_pat_FAKE-octo-1", "401 Unauthorized", "other-provider:github"),
            ("ghp_FAKEtoken", "401 Unauthorized", "other-provider:github"),
            (new string('a', 76) + "AZDO" + "abcd", "401 Unauthorized", "other-provider:azure-devops"),
        };

        foreach (var (token, message, detail) in cases)
        {
            var failures = new[]
            {
                (await provider.CheckTokenAsync(token, null, Cancelled)).Failure,
                (await provider.ListRepositoriesAsync(token, account, Cancelled)).Failure,
                (await provider.ReadRepositoryAsync(token, repository, Cancelled)).Failure,
                (await provider.ListPullRequestsAsync(token, repository, Cancelled)).Failure,
                (await provider.CreatePullRequestAsync(token, repository, create, Cancelled)).Failure,
            };
            Assert.All(failures, failure => Assert.Equal((HostingFailureKind.BadCredentials, message, detail, HostingProvider.GitLab), (failure!.Kind, failure.Message, failure.Detail, failure.Provider)));
        }

        Assert.Empty(lab.Fake.Requests);
        Assert.Empty(lab.Recorder!.Sent);
    }

    [Fact]
    public async Task A2_lists_the_users_projects_by_activity_without_simple_or_archived_ones_with_their_rows()
    {
        await using var lab = await GitLabLab.CreateAsync();
        var fake = lab.Fake;
        var now = AppClocks.HarnessTime;
        var deep = fake.Project("g/s/t/deep");
        var hidden = await fake.AddProjectAsync("g/s/old", ct: Cancelled);
        hidden.IsArchived = true;
        lab.Project.Visibility = "internal";
        lab.Project.Description = "The platform's web app.";
        lab.Project.LastActivityAt = now;
        deep.LastActivityAt = now.AddDays(-1);
        lab.Fork.LastActivityAt = now.AddDays(-2);
        fake.Grant("g/s/t/deep", "ada", 40);
        fake.Grant("g/s/p", "bob", 20);
        using var provider = lab.Provider();
        var ada = (await provider.CheckTokenAsync(lab.Ada, null, Cancelled)).Value!;

        var listed = await provider.ListRepositoriesAsync(lab.Ada, ada, Cancelled);
        var bob = await provider.ListRepositoriesAsync(lab.Bob, ada, Cancelled);

        Assert.Equal((false, false), (listed.Value!.MoreExist, listed.Value.SomeHiddenBySso));
        Assert.Equal(["g/s/p", "g/s/t/deep", "ada/p"], listed.Value.Repositories.Select(info => info.Repository.FullName));
        var (p, d, f) = (listed.Value.Repositories[0], listed.Value.Repositories[1], listed.Value.Repositories[2]);
        Assert.Equal(new HostedRepository(HostingProvider.GitLab, Host, "g/s", "p"), p.Repository);
        Assert.Equal(("main", true, false, false, "The platform's web app.", "http://127.0.0.1:3/g/s/p.git", "http://127.0.0.1:3/g/s/p", now, true),
            (p.DefaultBranch, p.IsPrivate, p.IsFork, p.IsArchived, p.Description, p.CloneUrl.AbsoluteUri, p.HtmlUrl.AbsoluteUri, p.UpdatedAt, p.CanPush));
        Assert.Equal((p.Repository, (HostedRepository?)null, (string?)null), (p.Repository, p.Parent, p.ParentDefaultBranch));
        Assert.Equal((new HostedRepository(HostingProvider.GitLab, Host, "g/s/t", "deep"), "main", true, true), (d.Repository, d.DefaultBranch, d.IsPrivate, d.CanPush));
        Assert.Equal((true, (HostedRepository?)null, true), (f.IsFork, f.Parent, f.CanPush));
        Assert.Equal(["g/s/p"], bob.Value!.Repositories.Select(info => info.Repository.FullName));
        Assert.False(bob.Value.Repositories[0].CanPush);
        Assert.All(lab.Fake.Requests.Where(request => request.Path == "/api/v4/projects"), request =>
            Assert.Equal("membership=true&archived=false&order_by=last_activity_at&sort=desc&per_page=100", request.Query));
        lab.AssertNoSecret();
    }

    [Fact]
    public async Task A2_stops_at_ten_pages_and_says_more_exist()
    {
        await using var lab = await GitLabLab.CreateAsync(withGit: false);
        lab.Fake.AddListedProjects("ada", 30);
        lab.Fake.MaxPerPage = 2;
        using var provider = lab.Provider();

        var listed = await provider.ListRepositoriesAsync(lab.Ada, null!, Cancelled);

        Assert.Equal((20, true), (listed.Value!.Repositories.Count, listed.Value.MoreExist));
        Assert.Equal(GitLabProvider.RepositoryPages, lab.Fake.Requests.Count);
        Assert.All(listed.Value.Repositories, info => Assert.Equal(("main", true), (info.DefaultBranch, info.IsPrivate)));
    }

    [Fact]
    public async Task A2_leaves_out_a_row_it_cannot_read_with_one_warning_naming_its_id_and_reads_a_bare_row()
    {
        const string Rows = """
            [
              {"id": 11, "path_with_namespace": "g/ok", "http_url_to_repo": "https://gitlab.com/g/ok.git", "web_url": "https://gitlab.com/g/ok"},
              {"id": 12, "path_with_namespace": "g/relative", "http_url_to_repo": "/g/relative.git", "web_url": "https://gitlab.com/g/relative"},
              {"id": 13, "path_with_namespace": "g/file", "http_url_to_repo": "https://gitlab.com/g/file.git", "web_url": "file:///g/file"},
              {"id": 14, "path_with_namespace": "noslash", "http_url_to_repo": "https://gitlab.com/noslash.git", "web_url": "https://gitlab.com/noslash"},
              {"id": 15, "path_with_namespace": "g/nourl"},
              {"id": 16, "path_with_namespace": "g/full", "default_branch": "trunk", "visibility": "public", "archived": false,
               "http_url_to_repo": "http://gitlab.com/g/full.git", "web_url": "https://gitlab.com/g/full", "last_activity_at": "2026-10-31T22:32:34.827Z",
               "permissions": {"project_access": null, "group_access": {"access_level": 20, "notification_level": 3}}, "unknown": {"x": [1]}}
            ]
            """;
        var log = new ListLog();
        var recorder = new RecordingHttpHandler(_ => RecordingHttpHandler.Json(HttpStatusCode.OK, Rows));
        using var provider = new GitLabProvider(GitLabEndpoints.GitLabCom, () => recorder, log, TimeProvider.System);

        var listed = await provider.ListRepositoriesAsync("glpat-FAKE-ada-1", null!, Cancelled);

        Assert.Equal(["g/ok", "g/full"], listed.Value!.Repositories.Select(info => info.Repository.FullName));
        var ok = listed.Value.Repositories[0];
        Assert.Equal(("", true, false, false, DateTimeOffset.MinValue, true), (ok.DefaultBranch, ok.IsPrivate, ok.IsFork, ok.IsArchived, ok.UpdatedAt, ok.CanPush));
        var full = listed.Value.Repositories[1];
        Assert.Equal(("trunk", false, new DateTimeOffset(2026, 10, 31, 22, 32, 34, 827, TimeSpan.Zero), false, "http://gitlab.com/g/full.git"),
            (full.DefaultBranch, full.IsPrivate, full.UpdatedAt, full.CanPush, full.CloneUrl.AbsoluteUri));
        Assert.Equal(
            [12, 13, 14, 15],
            log.Lines.Select(line => int.Parse(line.Message.Split(' ')[3], CultureInfo.InvariantCulture)));
        Assert.All(log.Lines, line => Assert.EndsWith("without a path or an address the app can read: it is left out of the list.", line.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A2_and_A4_fail_whole_on_a_row_without_its_required_members()
    {
        var answers = new Queue<HttpResponseMessage>(
        [
            RecordingHttpHandler.Json(HttpStatusCode.OK, "[{\"id\": 1, \"web_url\": \"https://gitlab.com/g/p\"}]"),
            RecordingHttpHandler.Json(HttpStatusCode.OK, "[" + MergeRequestJson(1, 5001, 5001).Replace("\"web_url\": \"https://gitlab.com/g/p/-/merge_requests/1\",", "", StringComparison.Ordinal) + "]"),
            RecordingHttpHandler.Json(HttpStatusCode.OK, "[" + MergeRequestJson(1, 5001, 5001).Replace("https://gitlab.com/g/p/-/merge_requests/1", "/g/p/-/merge_requests/1", StringComparison.Ordinal) + "]"),
        ]);
        var recorder = new RecordingHttpHandler(_ => answers.Dequeue());
        using var provider = new GitLabProvider(GitLabEndpoints.GitLabCom, () => recorder, new ListLog(), TimeProvider.System);
        var repository = new HostedRepository(HostingProvider.GitLab, "gitlab.com", "g", "p");

        var rows = await provider.ListRepositoriesAsync("glpat-FAKE-ada-1", null!, Cancelled);
        var missing = await provider.ListPullRequestsAsync("glpat-FAKE-ada-1", repository, Cancelled);
        var relative = await provider.ListPullRequestsAsync("glpat-FAKE-ada-1", repository, Cancelled);

        Assert.All(new[] { rows.Failure, missing.Failure, relative.Failure }, failure =>
            Assert.Equal((HostingFailureKind.Malformed, "GitLab's answer could not be read.", HostingProvider.GitLab), (failure!.Kind, failure.Message, failure.Provider)));
    }

    [Fact]
    public async Task A3_reads_a_project_by_its_path_a_moved_one_by_its_new_path_and_a_fork_with_its_parent()
    {
        await using var lab = await GitLabLab.CreateAsync();
        using var provider = lab.Provider();
        var p = new HostedRepository(HostingProvider.GitLab, Host, "g/s", "p");
        var fork = new HostedRepository(HostingProvider.GitLab, Host, "ada", "p");

        var read = await provider.ReadRepositoryAsync(lab.Ada, p, Cancelled);
        var forked = await provider.ReadRepositoryAsync(lab.Ada, fork, Cancelled);
        var secret = await provider.ReadRepositoryAsync(lab.Ada, new HostedRepository(HostingProvider.GitLab, Host, "octo", "secret"), Cancelled);
        lab.Fake.Move("g/s/p", "g/renamed/web");
        var moved = await provider.ReadRepositoryAsync(lab.Ada, p, Cancelled);

        Assert.Equal((p, (HostedRepository?)null, (string?)null, "main", false), (read.Value!.Repository, read.Value.Parent, read.Value.ParentDefaultBranch, read.Value.DefaultBranch, read.Value.IsFork));
        Assert.Equal((fork, true, p, "main"), (forked.Value!.Repository, forked.Value.IsFork, forked.Value.Parent, forked.Value.ParentDefaultBranch));
        Assert.Equal((HostingFailureKind.NotFound, "404 Project Not Found"), (secret.Failure!.Kind, secret.Failure.Message));
        Assert.Equal(new HostedRepository(HostingProvider.GitLab, Host, "g/renamed", "web"), moved.Value!.Repository);
        Assert.Equal("http://127.0.0.1:3/g/renamed/web.git", moved.Value.CloneUrl.AbsoluteUri);
        Assert.Equal(
            ["/api/v4/projects/g%2Fs%2Fp", "/api/v4/projects/ada%2Fp", "/api/v4/projects/octo%2Fsecret", "/api/v4/projects/g%2Fs%2Fp", "/api/v4/projects/5001"],
            lab.Fake.Requests.Select(request => request.Path));
    }

    [Fact]
    public async Task A4_lists_open_requests_with_their_fields_asking_no_lookup_for_the_projects_own_and_one_for_a_forks_two()
    {
        await using var lab = await GitLabLab.CreateAsync();
        var fake = lab.Fake;
        var one = fake.AddMergeRequest(lab.Project, "Add feature one", "ada", "feature/one", "main", description: "First.");
        var two = fake.AddMergeRequest(lab.Project, "Draft: Feature two", "octo", "feature/two", "main");
        var closed = fake.AddMergeRequest(lab.Project, "Closed one", "ada", "fix", "main");
        closed.State = "closed";
        using var provider = lab.Provider();
        var p = new HostedRepository(HostingProvider.GitLab, Host, "g/s", "p");

        var own = await provider.ListPullRequestsAsync(lab.Ada, p, Cancelled);

        Assert.Equal(["/api/v4/projects/g%2Fs%2Fp/merge_requests"], fake.Requests.Select(request => request.Path));
        Assert.Equal("state=opened&per_page=100", fake.Requests[0].Query);
        Assert.False(own.Value!.MoreExist);
        Assert.Equal([two.Iid, one.Iid], own.Value.PullRequests.Select(pull => pull.Number));
        var first = own.Value.PullRequests[1];
        Assert.Equal((1, "Add feature one", "ada", false, "http://127.0.0.1:3/g/s/p/-/merge_requests/1", "feature/one", "main", p, p, false),
            (first.Number, first.Title, first.Author, first.IsDraft, first.HtmlUrl.AbsoluteUri, first.HeadRef, first.BaseRef, first.HeadRepository, first.BaseRepository, first.IsFromFork));
        Assert.Equal((fake.MergeRequests(lab.Project)[0].HeadSha, one.CreatedAt), (first.HeadSha, first.CreatedAt));
        Assert.Matches("^[0-9a-f]{40}$", first.HeadSha);
        Assert.Equal(("octo", true), (own.Value.PullRequests[0].Author, own.Value.PullRequests[0].IsDraft));

        fake.AddMergeRequest(lab.Project, "Fix from the fork", "ada", "fix", "main", source: lab.Fork);
        fake.AddMergeRequest(lab.Project, "Two from the fork", "ada", "feature/two", "main", source: lab.Fork);
        var before = fake.Requests.Count;
        var forks = await provider.ListPullRequestsAsync(lab.Ada, p, Cancelled);

        Assert.Equal(["/api/v4/projects/g%2Fs%2Fp/merge_requests", "/api/v4/projects/5002"], fake.Requests.Skip(before).Select(request => request.Path));
        var fromFork = forks.Value!.PullRequests.Where(pull => pull.Title.EndsWith("the fork", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, fromFork.Count);
        Assert.All(fromFork, pull => Assert.Equal((new HostedRepository(HostingProvider.GitLab, Host, "ada", "p"), p, true), (pull.HeadRepository, pull.BaseRepository, pull.IsFromFork)));
        Assert.Equal("refs/merge-requests/4/head", provider.PullRequestHeadRef(fromFork.Single(pull => pull.HeadRef == "fix")));
        lab.AssertNoSecret();
    }

    [Fact]
    public async Task A4_gives_a_hidden_fork_no_head_repository_and_fails_on_another_failing_lookup()
    {
        await using var lab = await GitLabLab.CreateAsync();
        var fake = lab.Fake;
        var octoFork = await fake.AddProjectAsync("octo/p", lab.SourcePath, forkOf: lab.Project, ct: Cancelled);
        fake.AddMergeRequest(lab.Project, "From octo's private fork", "octo", "fix", "main", source: octoFork);
        fake.AddMergeRequest(lab.Project, "From ada's fork", "ada", "fix", "main", source: lab.Fork);
        using var provider = lab.Provider();
        var p = new HostedRepository(HostingProvider.GitLab, Host, "g/s", "p");

        var listed = await provider.ListPullRequestsAsync(lab.Ada, p, Cancelled);

        Assert.Equal(
            [("From ada's fork", new HostedRepository(HostingProvider.GitLab, Host, "ada", "p")), ("From octo's private fork", (HostedRepository?)null)],
            listed.Value!.PullRequests.Select(pull => (pull.Title, pull.HeadRepository)));
        Assert.True(listed.Value.PullRequests[1].IsFromFork);

        provider.ForgetKept();
        fake.FailNext("project:ada/p", FakeGitLabFault.ServerError);
        var failed = await provider.ListPullRequestsAsync(lab.Ada, p, Cancelled);

        Assert.Equal((HostingFailureKind.ServerError, "500 Internal Server Error", "http://127.0.0.1:3/api/v4/projects/5002"), (failed.Failure!.Kind, failed.Failure.Message, failed.Failure.Url!.AbsoluteUri));
    }

    [Fact]
    public async Task A4_looks_up_twenty_forks_a_call_keeps_them_and_asks_again_after_ForgetKept()
    {
        var requests = new List<string>();
        var recorder = new RecordingHttpHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            lock (requests)
            {
                requests.Add(path);
            }

            if (path.EndsWith("/merge_requests", StringComparison.Ordinal))
            {
                var rows = Enumerable.Range(1, 25).Select(i => MergeRequestJson(i, 9000 + i, 5001)).Append(MergeRequestJson(26, 5001, 5001)).Append(MergeRequestJson(27, 9001, 5001));
                return RecordingHttpHandler.Json(HttpStatusCode.OK, "[" + string.Join(",", rows) + "]");
            }

            var id = path["/api/v4/projects/".Length..];
            return RecordingHttpHandler.Json(HttpStatusCode.OK, $"{{\"id\": {id}, \"path_with_namespace\": \"fork{id}/p\"}}");
        });
        using var provider = new GitLabProvider(GitLabEndpoints.GitLabCom, () => recorder, new ListLog(), TimeProvider.System);
        var p = new HostedRepository(HostingProvider.GitLab, "gitlab.com", "g", "p");
        List<string> Lookups()
        {
            lock (requests)
            {
                var looked = requests.Where(path => !path.EndsWith("/merge_requests", StringComparison.Ordinal)).ToList();
                requests.Clear();
                return looked;
            }
        }

        var first = await provider.ListPullRequestsAsync("glpat-FAKE-ada-1", p, Cancelled);
        var firstLookups = Lookups();
        var second = await provider.ListPullRequestsAsync("glpat-FAKE-ada-1", p, Cancelled);
        var secondLookups = Lookups();
        var third = await provider.ListPullRequestsAsync("glpat-FAKE-ada-1", p, Cancelled);
        var thirdLookups = Lookups();
        provider.ForgetKept();
        await provider.ListPullRequestsAsync("glpat-FAKE-ada-1", p, Cancelled);
        var afterForget = Lookups();

        Assert.Equal(Enumerable.Range(9001, 20).Select(id => "/api/v4/projects/" + id), firstLookups);
        Assert.Equal(Enumerable.Range(1, 20).Select(i => (HostedRepository?)new HostedRepository(HostingProvider.GitLab, "gitlab.com", "fork" + (9000 + i), "p")), first.Value!.PullRequests.Take(20).Select(pull => pull.HeadRepository));
        Assert.All(first.Value.PullRequests.Skip(20).Take(5), pull => Assert.Null(pull.HeadRepository));
        Assert.Equal((p, "fork9001/p"), (first.Value.PullRequests[25].HeadRepository, first.Value.PullRequests[26].HeadRepository!.FullName));
        Assert.Equal(Enumerable.Range(9021, 5).Select(id => "/api/v4/projects/" + id), secondLookups);
        Assert.All(second.Value!.PullRequests.Take(25), pull => Assert.NotNull(pull.HeadRepository));
        Assert.Empty(thirdLookups);
        Assert.Equal(27, third.Value!.PullRequests.Count);
        Assert.Equal(GitLabProvider.MaxForkLookups, afterForget.Count);
    }

    [Fact]
    public async Task A4_stops_at_three_pages_and_says_more_exist()
    {
        await using var lab = await GitLabLab.CreateAsync();
        foreach (var (branch, target) in new[] { ("feature/one", "main"), ("feature/two", "main"), ("fix", "main"), ("main", "fix") })
        {
            lab.Fake.AddMergeRequest(lab.Project, "From " + branch, "ada", branch, target);
        }

        lab.Fake.MaxPerPage = 1;
        using var provider = lab.Provider();

        var listed = await provider.ListPullRequestsAsync(lab.Ada, new HostedRepository(HostingProvider.GitLab, Host, "g/s", "p"), Cancelled);

        Assert.Equal((3, true), (listed.Value!.PullRequests.Count, listed.Value.MoreExist));
        Assert.Equal(GitLabProvider.RequestPages, lab.Fake.Requests.Count);
    }

    [Theory]
    [InlineData("Add the search box", false, "Add the search box")]
    [InlineData("Draft: Add the search box", false, "Draft: Add the search box")]
    [InlineData("Add the search box", true, "Draft: Add the search box")]
    [InlineData("Draft: Add the search box", true, "Draft: Add the search box")]
    [InlineData("draft:Add", true, "draft:Add")]
    [InlineData("DRAFT: Add", true, "DRAFT: Add")]
    [InlineData("[Draft] Add", true, "[Draft] Add")]
    [InlineData("[DRAFT]Add", true, "[DRAFT]Add")]
    [InlineData("(draft) Add", true, "(draft) Add")]
    [InlineData("Draft Add", true, "Draft: Draft Add")]
    [InlineData("WIP: Add", true, "Draft: WIP: Add")]
    [InlineData(" Draft: Add", true, "Draft:  Draft: Add")]
    [InlineData("", true, "Draft: ")]
    public void TitleFor_puts_Draft_before_a_title_unless_it_has_one_of_GitLabs_prefixes(string title, bool draft, string sent)
    {
        Assert.Equal(sent, GitLabProvider.TitleFor(title, draft));
    }

    [Fact]
    public async Task A5_in_one_project_reads_it_then_posts_once_by_its_number_with_the_draft_title_and_the_description_as_typed()
    {
        await using var lab = await GitLabLab.CreateAsync();
        using var provider = lab.Provider();
        var p = new HostedRepository(HostingProvider.GitLab, Host, "g/s", "p");

        var created = await provider.CreatePullRequestAsync(lab.Ada, p, new NewPullRequest("Fix it", "Fixes it.\n/close", "fix", "someone", "main", Draft: true), Cancelled);

        var pull = created.Value!;
        Assert.Equal((1, "Draft: Fix it", "ada", true, "fix", "main", p, p, false, "http://127.0.0.1:3/g/s/p/-/merge_requests/1"),
            (pull.Number, pull.Title, pull.Author, pull.IsDraft, pull.HeadRef, pull.BaseRef, pull.HeadRepository, pull.BaseRepository, pull.IsFromFork, pull.HtmlUrl.AbsoluteUri));
        Assert.Equal([("GET", "/api/v4/projects/g%2Fs%2Fp"), ("POST", "/api/v4/projects/5001/merge_requests")], lab.Fake.Requests.Select(request => (request.Method, request.Path)));
        Assert.Equal("{\"source_branch\":\"fix\",\"target_branch\":\"main\",\"title\":\"Draft: Fix it\",\"description\":\"Fixes it.\\n/close\"}", lab.Recorder!.Sent[^1].Body);
        Assert.Equal("Fixes it.\n/close", lab.Fake.MergeRequests(lab.Project).Single().Description);
        lab.AssertNoSecret();
    }

    [Fact]
    public async Task A5_across_forks_reads_both_and_posts_to_the_forks_number_with_the_target_and_collaboration_for_a_public_fork()
    {
        await using var lab = await GitLabLab.CreateAsync();
        var publicFork = await lab.AddForkAsync("ada/open-fork", "public");
        using var provider = lab.Provider();
        var p = new HostedRepository(HostingProvider.GitLab, Host, "g/s", "p");
        var privateFork = new HostedRepository(HostingProvider.GitLab, Host, "ada", "p");
        var open = new HostedRepository(HostingProvider.GitLab, Host, "ada", "open-fork");

        var fromPrivate = await provider.CreatePullRequestAsync(lab.Ada, p, new NewPullRequest("From the fork", "", "fix", null, "main", false) { HeadRepository = privateFork }, Cancelled);
        var privateBody = lab.Recorder!.Sent[^1].Body;
        var fromPublic = await provider.CreatePullRequestAsync(lab.Ada, p, new NewPullRequest("From the open fork", "", "feature/one", null, "main", false) { HeadRepository = open }, Cancelled);

        Assert.Equal(
            [
                ("GET", "/api/v4/projects/ada%2Fp"), ("GET", "/api/v4/projects/g%2Fs%2Fp"), ("POST", "/api/v4/projects/5002/merge_requests"),
                ("GET", "/api/v4/projects/ada%2Fopen-fork"), ("GET", "/api/v4/projects/g%2Fs%2Fp"), ("POST", $"/api/v4/projects/{publicFork.Id}/merge_requests"),
            ],
            lab.Fake.Requests.Select(request => (request.Method, request.Path)));
        Assert.Equal("{\"source_branch\":\"fix\",\"target_branch\":\"main\",\"title\":\"From the fork\",\"description\":\"\",\"target_project_id\":5001}", privateBody);
        Assert.Equal("{\"source_branch\":\"feature/one\",\"target_branch\":\"main\",\"title\":\"From the open fork\",\"description\":\"\",\"target_project_id\":5001,\"allow_collaboration\":true}", lab.Recorder.Sent[^1].Body);
        Assert.Equal((privateFork, p, true), (fromPrivate.Value!.HeadRepository, fromPrivate.Value.BaseRepository, fromPrivate.Value.IsFromFork));
        Assert.Equal((open, 2), (fromPublic.Value!.HeadRepository, fromPublic.Value.Number));
        Assert.Equal([false, true], lab.Fake.MergeRequests(lab.Project).Select(request => request.AllowCollaboration));
    }

    [Fact]
    public async Task A5_gives_each_refusal_its_reason_with_one_POST_a_call()
    {
        await using var lab = await GitLabLab.CreateAsync();
        var fake = lab.Fake;
        fake.AddMergeRequest(lab.Project, "Open already", "ada", "feature/one", "main");
        var closedOff = await fake.AddProjectAsync("g/closed", lab.SourcePath, ct: Cancelled);
        closedOff.MergeRequestsEnabled = false;
        using var provider = lab.Provider();
        var p = new HostedRepository(HostingProvider.GitLab, Host, "g/s", "p");
        NewPullRequest New(string head, string @base) => new("T", "", head, null, @base, false);
        var cases = new (HostedRepository Base, NewPullRequest Request, HostingFailureKind Kind, PullRequestRefusal Refusal)[]
        {
            (p, New("feature/one", "main"), HostingFailureKind.Validation, PullRequestRefusal.AlreadyExists),
            (p, New("nope", "main"), HostingFailureKind.Validation, PullRequestRefusal.HeadMissing),
            (p, New("fix", "nope"), HostingFailureKind.Validation, PullRequestRefusal.BaseMissing),
            (p, New("nope", "nada"), HostingFailureKind.Validation, PullRequestRefusal.BranchesMissing),
            (p, New("main", "main"), HostingFailureKind.Validation, PullRequestRefusal.None),
            (p with { Owner = "g", Name = "closed" }, New("fix", "main"), HostingFailureKind.Forbidden, PullRequestRefusal.None),
        };

        foreach (var (target, request, kind, refusal) in cases)
        {
            var posts = fake.Requests.Count(sent => sent.Method == "POST");
            var refused = await provider.CreatePullRequestAsync(lab.Ada, target, request, Cancelled);

            Assert.Equal((kind, refusal, HostingProvider.GitLab), (refused.Failure!.Kind, refused.Failure.Refusal, refused.Failure.Provider));
            Assert.Equal(posts + 1, fake.Requests.Count(sent => sent.Method == "POST"));
        }

        Assert.Equal("Another open merge request already exists for this source branch: !1", (await provider.CreatePullRequestAsync(lab.Ada, p, New("feature/one", "main"), Cancelled)).Failure!.Message);
        Assert.Single(fake.MergeRequests(lab.Project));
    }

    [Fact]
    public async Task A5_posts_nothing_after_a_failed_GET_and_follows_a_moved_source_to_its_number()
    {
        await using var lab = await GitLabLab.CreateAsync();
        var fake = lab.Fake;
        using var provider = lab.Provider();
        var p = new HostedRepository(HostingProvider.GitLab, Host, "g/s", "p");
        var fork = new HostedRepository(HostingProvider.GitLab, Host, "ada", "p");
        var request = new NewPullRequest("From the fork", "", "fix", null, "main", false) { HeadRepository = fork };

        var hidden = await provider.CreatePullRequestAsync(lab.Ada, new HostedRepository(HostingProvider.GitLab, Host, "octo", "secret"), request, Cancelled);
        fake.FailNext("project:ada/p", FakeGitLabFault.ServerError);
        var failedSource = await provider.CreatePullRequestAsync(lab.Ada, p, request, Cancelled);
        fake.FailNext("project:g/s/p", FakeGitLabFault.Unauthorized);
        var failedBase = await provider.CreatePullRequestAsync(lab.Ada, p, request, Cancelled);

        Assert.Equal(
            [HostingFailureKind.NotFound, HostingFailureKind.ServerError, HostingFailureKind.BadCredentials],
            new[] { hidden, failedSource, failedBase }.Select(result => result.Failure!.Kind));
        Assert.DoesNotContain(fake.Requests, sent => sent.Method == "POST");

        fake.Move("ada/p", "ada/p-renamed");
        var moved = await provider.CreatePullRequestAsync(lab.Ada, p, request, Cancelled);

        Assert.Equal((new HostedRepository(HostingProvider.GitLab, Host, "ada", "p-renamed"), 1), (moved.Value!.HeadRepository, moved.Value.Number));
        Assert.Equal(
            [("GET", "/api/v4/projects/ada%2Fp", 301), ("GET", "/api/v4/projects/5002", 200), ("GET", "/api/v4/projects/g%2Fs%2Fp", 200), ("POST", "/api/v4/projects/5002/merge_requests", 201)],
            fake.Requests.TakeLast(4).Select(sent => (sent.Method, sent.Path, sent.Status)));
    }

    /// <summary>One merge request's JSON with every member A4 needs (r6 4.1's shape, made-up values).</summary>
    private static string MergeRequestJson(int iid, long source, long target) => string.Create(CultureInfo.InvariantCulture, $$"""
        {"id": {{7000 + iid}}, "iid": {{iid}}, "project_id": {{target}}, "title": "Request {{iid}}", "state": "opened",
         "author": {"id": 2001, "username": "ada", "public_email": "ada@example.com"}, "draft": false,
         "web_url": "https://gitlab.com/g/p/-/merge_requests/{{iid}}", "source_branch": "b{{iid}}", "target_branch": "main",
         "sha": null, "created_at": "2026-10-31T22:32:34.827Z", "source_project_id": {{source}}, "target_project_id": {{target}}}
        """);
}
