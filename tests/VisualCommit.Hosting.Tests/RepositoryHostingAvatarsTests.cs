using System.Globalization;
using System.Text.Json;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// A tab's authors' avatars (docs/phase-7a-design.md 7.9, 7.13; D252, D291, D339, ruling 22): one
/// commits page; the 20 lookups a repository has per app session, shared by its tabs, each from the
/// author's own commit on the remote that holds it (a feature branch's, a fork's); an unknown e-mail
/// not asked again; the map used within 7 days (no page asked) and read again after; images fetched
/// without the token; nothing at all while signed out; the avatars gone with the GitHub remote, a
/// run in flight then applying nothing, and kept when the remote names another GitHub repository.
/// </summary>
public sealed class RepositoryHostingAvatarsTests
{
    private const string NewbieEmail = "newbie@example.com";

    private static readonly HostedRepository OctoHello = new(HostingProvider.GitHub, "127.0.0.1:1", "octo", "hello");

    private static readonly HostedRepository AdaHello = new(HostingProvider.GitHub, "127.0.0.1:1", "ada", "hello");

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task One_commits_page_maps_the_authors_github_knows_and_their_images_come_without_the_token()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await OpenHelloAsync(setup);

        await ShowAsync(setup, hosting, Scenarios.OctoEmail.ToUpperInvariant(), Scenarios.AdaEmail, TempRepo.AuthorEmail);

        Assert.True(File.Exists(hosting.Avatars.FileOf(Scenarios.OctoEmail)));
        Assert.True(File.Exists(hosting.Avatars.FileOf(Scenarios.AdaEmail)));
        Assert.Null(hosting.Avatars.FileOf(TempRepo.AuthorEmail));
        Assert.Equal(2, hosting.Avatars.FileByEmail.Count);
        var page = Assert.Single(Commits(setup));
        Assert.Equal("sha=main&per_page=100", page.Query);
        Assert.All(setup.Fake.Requests.Where(request => request.Route?.StartsWith("avatar:", StringComparison.Ordinal) == true), request => Assert.False(request.TokenPresent));
        Assert.Equal(2, setup.Fake.Requests.Count(request => request.Route?.StartsWith("avatar:", StringComparison.Ordinal) == true));

        // The map, in camelCase (D336), beside the images.
        var map = setup.Paths.AvatarsDirectory + "/authors/github_127.0.0.1_1_octo_hello.json";
        using var json = JsonDocument.Parse(File.ReadAllText(map));
        Assert.Equal(["fetchedAt", "authors"], json.RootElement.EnumerateObject().Select(property => property.Name));
        var ada = json.RootElement.GetProperty("authors").EnumerateArray().Single(author => author.GetProperty("email").GetString() == Scenarios.AdaEmail);
        Assert.Equal(["email", "userId", "login", "avatarUrl", "checkedAt"], ada.EnumerateObject().Select(property => property.Name));
        Assert.Equal((1003L, "ada"), (ada.GetProperty("userId").GetInt64(), ada.GetProperty("login").GetString()));

        // The same set again: everything is known, nothing is asked.
        var requests = setup.Fake.Requests.Count;
        await ShowAsync(setup, hosting, Scenarios.OctoEmail, Scenarios.AdaEmail, TempRepo.AuthorEmail);
        Assert.Equal(requests, setup.Fake.Requests.Count);
    }

    [Fact]
    public async Task Twenty_five_unknown_emails_over_two_tabs_of_one_repository_make_twenty_lookups()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var first = await OpenHelloAsync(setup);
        var second = await OpenHelloAsync(setup);
        var unknown = Enumerable.Range(1, 25).Select(n => string.Create(CultureInfo.InvariantCulture, $"stranger{n:00}@example.com")).ToArray();

        await ShowAsync(setup, first, unknown[..15]);
        Assert.Equal(15, Lookups(setup));
        await ShowAsync(setup, second, unknown);
        Assert.Equal(20, Lookups(setup));
        await ShowAsync(setup, first, unknown);

        Assert.Equal(20, Lookups(setup));
        Assert.Single(Commits(setup), IsPage);
        Assert.Empty(first.Avatars.FileByEmail);

        // Asked once and unknown: not asked again within 7 days, the budget aside.
        var asked = Commits(setup).Where(request => !IsPage(request)).Select(request => request.Query).ToList();
        Assert.Equal(asked.Count, asked.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task The_map_is_used_within_seven_days_and_read_again_after()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await OpenHelloAsync(setup);
        await ShowAsync(setup, hosting, Scenarios.OctoEmail);
        Assert.Single(Commits(setup));

        // The next start reads the map from the disk: within 7 days, no page; a new e-mail is looked up.
        var next = setup.NewSession();
        await next.StartAsync(Cancelled);
        var later = await setup.Thread.RunAsync(() => (RepositoryHosting)next.ForRepository(setup.Served.Hello.Path));
        await setup.Thread.RunAsync(() => later.Update(RepositoryHostingTests.HelloRefs(), UrlRewrites.None));
        setup.Clock.Advance(TimeSpan.FromDays(6));
        await ShowAsync(setup, later, Scenarios.OctoEmail, "newcomer@example.com");

        var main = await MainAsync(setup);
        Assert.Equal(["sha=main&per_page=100", $"sha={main}&author=newcomer%40example.com&per_page=1"], Commits(setup).Select(request => request.Query));
        Assert.NotNull(later.Avatars.FileOf(Scenarios.OctoEmail));

        setup.Clock.Advance(TimeSpan.FromDays(1));
        await ShowAsync(setup, later, Scenarios.OctoEmail);
        Assert.Equal("sha=main&per_page=100", Commits(setup)[^1].Query);
        Assert.Equal(2, Commits(setup).Count(IsPage));
    }

    [Fact]
    public async Task An_author_only_on_a_feature_branch_is_looked_up_from_that_commit_and_found()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await OpenHelloAsync(setup);
        var sha = await PushNewbieAsync(setup, "octo/hello", "feature/newbie");

        await ShowAsync(setup, hosting, new AuthorCommit(NewbieEmail, sha, OctoHello));

        // main's page does not hold the commit; the lookup searches from it, so GitHub finds the author.
        Assert.Equal(["sha=main&per_page=100", $"sha={sha}&author=newbie%40example.com&per_page=1"], Commits(setup).Select(request => request.Query));
        Assert.True(File.Exists(hosting.Avatars.FileOf(NewbieEmail)));
    }

    [Fact]
    public async Task A_fork_tabs_author_is_looked_up_on_the_fork_and_kept_in_the_listed_repositorys_map()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await setup.Thread.RunAsync(() => (RepositoryHosting)setup.Session.ForRepository(setup.Served.Hello.Path));
        await setup.Thread.RunAsync(() => hosting.Update(RepositoryHostingTests.Refs(("origin", "http://127.0.0.1:1/ada/hello.git")), UrlRewrites.None));
        var sha = await PushNewbieAsync(setup, "ada/hello", "newbie-work");

        await ShowAsync(setup, hosting, new AuthorCommit(NewbieEmail, sha, AdaHello));

        // ada/hello is a fork of octo/hello, the listed repository: its main's page, and the lookup
        // on the fork, which alone holds the commit; the map is octo/hello's.
        Assert.Equal(["sha=main&per_page=100"], Commits(setup).Select(request => request.Query));
        Assert.Equal(
            [$"sha={sha}&author=newbie%40example.com&per_page=1"],
            setup.Fake.Requests.Where(request => request.Route == "commits:ada/hello").Select(request => request.Query));
        Assert.True(File.Exists(hosting.Avatars.FileOf(NewbieEmail)));
        var authors = Path.Combine(setup.Paths.AvatarsDirectory, "authors");
        Assert.Equal(["github_127.0.0.1_1_octo_hello.json"], Directory.GetFiles(authors).Select(Path.GetFileName));
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(authors, "github_127.0.0.1_1_octo_hello.json")));
        var newbie = json.RootElement.GetProperty("authors").EnumerateArray().Single(author => author.GetProperty("email").GetString() == NewbieEmail);
        Assert.Equal((1010L, "newbie"), (newbie.GetProperty("userId").GetInt64(), newbie.GetProperty("login").GetString()));
    }

    [Fact]
    public async Task Signed_out_nothing_is_read_or_asked()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.Session.StartAsync(Cancelled);
        var hosting = await OpenHelloAsync(setup);

        await ShowAsync(setup, hosting, Scenarios.OctoEmail, Scenarios.AdaEmail);

        Assert.Same(AuthorAvatars.None, hosting.Avatars);
        Assert.Empty(setup.Fake.Requests);
        Assert.False(Directory.Exists(setup.Paths.AvatarsDirectory));
    }

    [Fact]
    public async Task Avatars_go_when_the_repository_loses_its_github_remote_and_come_back_with_it()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await OpenHelloAsync(setup);
        await ShowAsync(setup, hosting, Scenarios.OctoEmail, Scenarios.AdaEmail);
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "the list");
        await setup.Thread.IdleAsync();
        Assert.Equal(2, hosting.Avatars.FileByEmail.Count);
        var changed = 0;
        await setup.Thread.RunAsync(() => hosting.Changed += (_, _) => changed++);

        // origin set to another server's URL: the tab looks as it does without GitHub (D252).
        await setup.Thread.RunAsync(() => hosting.Update(RepositoryHostingTests.Refs(("origin", "https://gitlab.example/octo/hello.git")), UrlRewrites.None));
        await setup.Thread.IdleAsync();

        Assert.Same(RepositoryHostingState.NotGitHub, hosting.State);
        Assert.Same(AuthorAvatars.None, hosting.Avatars);
        Assert.Equal(1, changed);

        // GitHub's remote again: the set the tab sends again brings them back.
        await setup.Thread.RunAsync(() => hosting.Update(RepositoryHostingTests.HelloRefs(), UrlRewrites.None));
        await ShowAsync(setup, hosting, Scenarios.OctoEmail, Scenarios.AdaEmail);
        Assert.Equal(2, hosting.Avatars.FileByEmail.Count);
    }

    [Fact]
    public async Task An_avatar_run_in_flight_when_the_github_remote_goes_applies_nothing()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await OpenHelloAsync(setup);
        var authors = await OnMainAsync(setup, Scenarios.OctoEmail, Scenarios.AdaEmail);

        Task run;
        using (var hold = setup.Fake.HoldNext("commits:octo/hello"))
        {
            run = await setup.Thread.RunAsync<Task>(() =>
            {
                hosting.ShowAvatarsFor(authors);
                return hosting.AvatarRun;
            });
            await hold.Arrived.WaitAsync(Cancelled);
            await setup.Thread.RunAsync(() => hosting.Update(RepositoryHostingTests.Refs(), UrlRewrites.None));
        }

        await run.WaitAsync(Cancelled);
        await setup.Thread.IdleAsync();
        await setup.Thread.IdleAsync();

        Assert.Same(RepositoryHostingState.NotGitHub, hosting.State);
        Assert.Same(AuthorAvatars.None, hosting.Avatars);
    }

    [Fact]
    public async Task A_change_to_another_github_repository_keeps_the_avatars()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await OpenHelloAsync(setup);
        await ShowAsync(setup, hosting, Scenarios.OctoEmail, Scenarios.AdaEmail);
        var avatars = hosting.Avatars;

        // The tab sends its set again only after NotGitHub or a sign-out (11.3): avatars dropped
        // here would not come back until its authors change.
        await setup.Thread.RunAsync(() => hosting.Update(RepositoryHostingTests.Refs(("origin", "http://127.0.0.1:1/octo/tool.git")), UrlRewrites.None));
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "acme/tool's list");

        Assert.Equal("acme/tool", hosting.State.Repository!.FullName);
        Assert.Same(avatars, hosting.Avatars);
        Assert.Equal(2, hosting.Avatars.FileByEmail.Count);
    }

    internal static async Task<RepositoryHosting> OpenHelloAsync(HostingSessionSetup setup)
    {
        var hosting = await setup.Thread.RunAsync(() => (RepositoryHosting)setup.Session.ForRepository(setup.Served.Hello.Path));
        await setup.Thread.RunAsync(() => hosting.Update(RepositoryHostingTests.HelloRefs(), UrlRewrites.None));
        return hosting;
    }

    /// <summary>Shows <paramref name="emails"/>' avatars, each with origin/main's tip on octo/hello (<see cref="OnMainAsync"/>), and waits for the run and its answer to be applied.</summary>
    internal static async Task ShowAsync(HostingSessionSetup setup, RepositoryHosting hosting, params string[] emails) =>
        await ShowAsync(setup, hosting, await OnMainAsync(setup, emails));

    /// <summary>Shows <paramref name="authors"/>' avatars and waits for the run and its answer to be applied.</summary>
    internal static async Task ShowAsync(HostingSessionSetup setup, RepositoryHosting hosting, params AuthorCommit[] authors)
    {
        var run = await setup.Thread.RunAsync<Task>(() =>
        {
            hosting.ShowAvatarsFor(authors);
            return hosting.AvatarRun;
        });
        await run.WaitAsync(Cancelled);
        await setup.Thread.IdleAsync();
        await setup.Thread.IdleAsync();
    }

    /// <summary><paramref name="emails"/> as a tab of octo/hello tells them: each with origin/main's tip, on octo/hello.</summary>
    internal static async Task<AuthorCommit[]> OnMainAsync(HostingSessionSetup setup, params string[] emails)
    {
        var main = await MainAsync(setup);
        return [.. emails.Select(email => new AuthorCommit(email, main, OctoHello))];
    }

    private static async Task<string> MainAsync(HostingSessionSetup setup) =>
        (await setup.Served.Hello.GitAsync("rev-parse", "refs/remotes/origin/main")).StandardOutput.Trim();

    /// <summary>
    /// A user newbie (1010), linked to <see cref="NewbieEmail"/>, whose one commit, on origin/main,
    /// is pushed to <paramref name="branch"/> of <paramref name="repository"/> only. Returns its SHA.
    /// </summary>
    private static async Task<string> PushNewbieAsync(HostingSessionSetup setup, string repository, string branch)
    {
        setup.Fake.AddUser("newbie", 1010, "New Bie");
        setup.Fake.LinkEmail(NewbieEmail, "newbie");
        var hello = setup.Served.Hello;
        await hello.GitAsync("switch", "--quiet", "--create", "newbie-work", "refs/remotes/origin/main");
        var sha = await hello.CommitFileAsAsync("newbie.txt", "hello\n", "Add the newcomer's file", "New Bie", NewbieEmail);
        await hello.GitAsync("push", "--quiet", setup.Fake.Repository(repository).BarePath!, $"{sha}:refs/heads/{branch}");
        await hello.GitAsync("switch", "--quiet", "main");
        return sha;
    }

    private static bool IsPage(Testing.Hosting.FakeGitHubRequest request) => request.Query.EndsWith("&per_page=100", StringComparison.Ordinal);

    private static List<Testing.Hosting.FakeGitHubRequest> Commits(HostingSessionSetup setup) =>
        [.. setup.Fake.Requests.Where(request => request.Route == "commits:octo/hello")];

    private static int Lookups(HostingSessionSetup setup) => Commits(setup).Count(request => !IsPage(request));
}
