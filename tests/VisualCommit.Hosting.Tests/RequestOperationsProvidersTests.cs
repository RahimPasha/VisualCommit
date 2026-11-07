using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// A request's actions per provider with real git (docs/phase-7b-design.md 12.5, 12.7; D334, D361,
/// D376), the app started through <see cref="ShellDriver"/> with "hosting7b"'s fakes on port 0
/// (<see cref="Scenarios.ServeHosting7bAsync"/>): GitLab's !21 tracks <c>origin/feature/search</c>;
/// !23 through <c>FETCH_HEAD</c> from <c>refs/merge-requests/23/head</c> adds only its branch, the
/// local configuration unchanged; Azure DevOps' !11 tracks <c>origin/feature/sign-in</c>, fetched first
/// when unread; !13 starts no git call; open in browser, copy link and refresh ask only the listed
/// fake; <c>manifest/</c> asks nothing after the sign-in; signed out, no request. A second start of
/// !23's checkout while the first runs starts nothing (one fetch, no dialog), and a sign-out while
/// GitLab's list is read sends no further page or fork lookup with its token (12's As built). On 0.4's
/// lagged list.
/// </summary>
public sealed class RequestOperationsProvidersTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [AvaloniaFact]
    public async Task GitLab_s_request_of_the_repository_itself_checks_out_its_tracking_branch_without_a_fetch()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("requests providers");
        using var app = await OpenAsync(data, served, served.WebApp, HostingProvider.GitLab);
        var repository = app.Session.Shell.ActiveTab.Repository!;

        await repository.Operations.CheckOutPullRequestAsync(Request(repository, 21));
        await app.WaitForIdleAsync();

        Assert.Equal("feature/search", await GitAsync(served.WebApp, "symbolic-ref", "--short", "HEAD"));
        Assert.Equal("origin", await GitAsync(served.WebApp, "config", "branch.feature/search.remote"));
        Assert.Equal("refs/heads/feature/search", await GitAsync(served.WebApp, "config", "branch.feature/search.merge"));
        Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.Contains("git fetch", StringComparison.Ordinal));
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task GitLab_s_fork_request_is_fetched_from_its_head_ref_into_fetch_head_alone()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("requests providers");
        using var app = await OpenAsync(data, served, served.WebApp, HostingProvider.GitLab);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var refsBefore = await RefsAsync(served.WebApp);
        var configBefore = await GitAsync(served.WebApp, "config", "--local", "--list");
        var bare = served.GitLab.Project(Scenarios.Hosting7bWebApp).BarePath!;
        var head = await GitAsync(served.WebApp, "--git-dir", bare, "rev-parse", "refs/merge-requests/23/head");
        var gitRequests = served.GitLab.GitRequests.Count;

        await repository.Operations.CheckOutPullRequestAsync(Request(repository, 23));
        await app.WaitForIdleAsync();

        Assert.Equal("fix-typo", await GitAsync(served.WebApp, "symbolic-ref", "--short", "HEAD"));
        Assert.Equal(head, await GitAsync(served.WebApp, "rev-parse", "HEAD"));
        Assert.Equal(refsBefore.Append("refs/heads/fix-typo").Order(StringComparer.Ordinal), await RefsAsync(served.WebApp));
        Assert.Equal(configBefore, await GitAsync(served.WebApp, "config", "--local", "--list"));
        Assert.Contains(app.GitCalls(), call => call.CommandText == "git fetch --progress --no-tags --refmap= --write-fetch-head origin refs/merge-requests/23/head");
        Assert.Contains(app.GitCalls(), call => call.CommandText == $"git branch --no-track fix-typo {head}");
        var git = served.GitLab.GitRequests.Skip(gitRequests).ToList();
        Assert.Contains(git, request => request.GivenUserName == "x-access-token" && request.Status == 200);
        Assert.DoesNotContain(git, request => request.GivenUserName is { } user && user != "x-access-token");
        var fetchHead = File.ReadAllText(Path.Combine(served.WebApp.Path, ".git", "FETCH_HEAD"));
        Assert.StartsWith($"{head}\t\t'refs/merge-requests/23/head' of ", fetchHead, StringComparison.Ordinal);
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_second_checkout_of_a_request_while_its_first_runs_starts_nothing()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("requests providers");
        using var app = await OpenAsync(data, served, served.WebApp, HostingProvider.GitLab);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var bare = served.GitLab.Project(Scenarios.Hosting7bWebApp).BarePath!;
        var head = await GitAsync(served.WebApp, "--git-dir", bare, "rev-parse", "refs/merge-requests/23/head");
        var refsBefore = await RefsAsync(served.WebApp);
        var logMark = app.LogMark();
        var twentyThree = Request(repository, 23);

        // Two starts in one UI turn, as two double-clicks on !23's row before its fetch ends: both
        // would plan fix-typo, and a second fetch's branch would exist by then.
        var first = repository.Operations.CheckOutPullRequestAsync(twentyThree);
        var second = repository.Operations.CheckOutPullRequestAsync(twentyThree);
        await app.WaitForAsync(() => (first.IsCompleted && second.IsCompleted) || app.Session.Services.DialogHost.Current is not null, "both checkouts to end or a dialog");
        Assert.Null(app.Session.Services.DialogHost.Current);
        await Task.WhenAll(first, second);
        await app.WaitForIdleAsync();

        Assert.Single(app.GitCalls(), call => call.CommandText == "git fetch --progress --no-tags --refmap= --write-fetch-head origin refs/merge-requests/23/head");
        Assert.Single(app.GitCalls(), call => call.CommandText == $"git branch --no-track fix-typo {head}");
        Assert.Equal("fix-typo", await GitAsync(served.WebApp, "symbolic-ref", "--short", "HEAD"));
        Assert.Equal(refsBefore.Append("refs/heads/fix-typo").Order(StringComparer.Ordinal), await RefsAsync(served.WebApp));
        var lines = app.LogLinesSince(logMark);
        Assert.Single(lines, line => line.Contains("[INF] web-app: checking out merge request !23: not started, its checkout is running.", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("[WRN]", StringComparison.Ordinal));
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_sign_out_while_the_list_is_read_sends_nothing_more_with_its_token()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("requests providers");
        using var app = await OpenAsync(data, served, served.WebApp, HostingProvider.GitLab);
        var webApp = app.Session.Shell.ActiveTab.Repository!;

        // Three pages now (298 more), and !23's fork looked up again after the sign-out's ForgetKept:
        // a run that went on would send pages 2 and 3 and the lookup with the signed-out token.
        var project = served.GitLab.Project(Scenarios.Hosting7bWebApp);
        for (var n = 1; n <= 298; n++)
        {
            served.GitLab.AddMergeRequest(project, $"Added request {n}", "octo", "topic/no-request", "main");
        }

        var hold = served.GitLab.HoldNext("mrs:northfield/platform/web-app");
        Task refresh;
        int mark;
        try
        {
            refresh = webApp.Operations.RefreshPullRequestsAsync();
            await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
            mark = served.GitLab.Requests.Count;
            await app.Session.Services.Hosting.SignOutAsync(HostingProvider.GitLab, Cancelled);
            await app.WaitForAsync(() => webApp.HostingState.PullRequests == PullRequestsState.SignedOut, "web-app signed out");
        }
        finally
        {
            hold.Dispose();
        }

        await refresh.WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
        await app.WaitForAsync(() => served.GitLab.Requests.Count > mark, "the held page's answer");
        await app.WaitForIdleAsync();
        await app.WaitForWatchersQuietAsync();

        Assert.Equal(["mrs:northfield/platform/web-app"], served.GitLab.Requests.Skip(mark).Select(request => request.Route));
        Assert.Equal(PullRequestsState.SignedOut, webApp.HostingState.PullRequests);
        Assert.Null(webApp.HostingState.List);
        Assert.Empty(served.AzureDevOps.Requests);
        Assert.Empty(served.GitHub.Requests);
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Azure_DevOps_request_of_the_repository_itself_checks_out_its_tracking_branch()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("requests providers");
        using var app = await OpenAsync(data, served, served.MobileApp, HostingProvider.AzureDevOps);
        var repository = app.Session.Shell.ActiveTab.Repository!;

        await repository.Operations.CheckOutPullRequestAsync(Request(repository, 11));
        await app.WaitForIdleAsync();

        Assert.Equal("feature/sign-in", await GitAsync(served.MobileApp, "symbolic-ref", "--short", "HEAD"));
        Assert.Equal("origin", await GitAsync(served.MobileApp, "config", "branch.feature/sign-in.remote"));
        Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.Contains("git fetch", StringComparison.Ordinal));
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Azure_DevOps_request_whose_branch_is_unread_is_fetched_first_and_never_a_request_ref()
    {
        await using var served = await ServeAsync();
        await served.MobileApp.GitAsync("branch", "-D", "feature/sign-in");
        await served.MobileApp.GitAsync("update-ref", "-d", "refs/remotes/origin/feature/sign-in");
        using var data = new TempDirectory("requests providers");
        using var app = await OpenAsync(data, served, served.MobileApp, HostingProvider.AzureDevOps);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        Assert.DoesNotContain(repository.Refs!.Refs, gitRef => gitRef.FullName == "refs/remotes/origin/feature/sign-in");

        await repository.Operations.CheckOutPullRequestAsync(Request(repository, 11));
        await app.WaitForIdleAsync();

        Assert.Equal("feature/sign-in", await GitAsync(served.MobileApp, "symbolic-ref", "--short", "HEAD"));
        Assert.Equal("refs/heads/feature/sign-in", await GitAsync(served.MobileApp, "config", "branch.feature/sign-in.merge"));
        Assert.Contains(app.GitCalls(), call => call.CommandText.StartsWith("git fetch --progress origin", StringComparison.Ordinal));
        Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.Contains("refs/pull/", StringComparison.Ordinal));
        Assert.DoesNotContain(await RefsAsync(served.MobileApp), name => name.StartsWith("refs/pull/", StringComparison.Ordinal));
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Azure_DevOps_request_from_a_fork_that_is_no_remote_starts_no_git_call()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("requests providers");
        using var app = await OpenAsync(data, served, served.MobileApp, HostingProvider.AzureDevOps);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var mark = await app.QuietCallMarkAsync();
        var (azure, gitLab) = (served.AzureDevOps.Requests.Count, served.GitLab.Requests.Count);

        await repository.Operations.CheckOutPullRequestAsync(Request(repository, 13));
        await app.WaitForIdleAsync();
        await app.WaitForWatchersQuietAsync();

        GitCallBudget.AssertAtMost([.. app.GitCalls().Where(call => call.Sequence > mark)], 0, "!13's checkout");
        Assert.Equal("main", await GitAsync(served.MobileApp, "symbolic-ref", "--short", "HEAD"));
        Assert.Equal((azure, gitLab), (served.AzureDevOps.Requests.Count, served.GitLab.Requests.Count));
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Open_copy_and_refresh_ask_only_the_listed_fake_per_provider()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("requests providers");
        using var app = ShellDriver.StartWithTabs(data.Path, [served.WebApp.Path, served.MobileApp.Path], activeTab: 0, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        await app.WaitForRepositoryAsync();
        await app.SignInAsync(HostingProvider.GitLab, served.GitLabToken, cancellationToken: Cancelled);
        await app.SignInAsync(HostingProvider.AzureDevOps, served.AzureDevOpsToken, served.Organization, Cancelled);
        var webApp = app.Session.Shell.Tabs[0].Repository!;
        await app.WaitForAsync(() => webApp.HostingState.List?.PullRequests.Count == 3, "web-app's three merge requests");
        app.Click(app.TabStrip().Tabs[1].Bounds.Center);
        var mobileApp = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => mobileApp.HostingState.List?.PullRequests.Count == 3, "mobile-app's three pull requests");
        await app.WaitForWatchersQuietAsync();
        var gitLabPort = served.GitLab.Port;
        var azurePort = served.AzureDevOps.Port;
        var (azure, gitLab, gitHub) = (served.AzureDevOps.Requests.Count, served.GitLab.Requests.Count, served.GitHub.Requests.Count);

        var eleven = Request(mobileApp, 11);
        await mobileApp.Operations.OpenPullRequestAsync(eleven);
        await mobileApp.Operations.CopyPullRequestLinkAsync(eleven);
        await mobileApp.Operations.OpenPullRequestsInBrowserAsync();
        var twentyOne = Request(webApp, 21);
        await webApp.Operations.OpenPullRequestAsync(twentyOne);
        await webApp.Operations.CopyPullRequestLinkAsync(twentyOne);
        await webApp.Operations.OpenPullRequestsInBrowserAsync();

        var azureUrl = $"http://127.0.0.1:{azurePort}/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/11";
        var gitLabUrl = $"http://127.0.0.1:{gitLabPort}/northfield/platform/web-app/-/merge_requests/21";
        Assert.Equal(azureUrl, eleven.HtmlUrl.AbsoluteUri);
        Assert.Equal(gitLabUrl, twentyOne.HtmlUrl.AbsoluteUri);
        Assert.Equal(
            [azureUrl, $"http://127.0.0.1:{azurePort}/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequests", gitLabUrl, $"http://127.0.0.1:{gitLabPort}/northfield/platform/web-app/-/merge_requests"],
            OpenedUrls(app));
        Assert.Equal([azureUrl, gitLabUrl], app.Clipboard.Texts.TakeLast(2));
        Assert.Equal((azure, gitLab, gitHub), (served.AzureDevOps.Requests.Count, served.GitLab.Requests.Count, served.GitHub.Requests.Count));

        await mobileApp.Operations.RefreshPullRequestsAsync();
        Assert.Contains(served.AzureDevOps.Requests.Skip(azure), request => request.Route == "pulls:fernhill-labs/mobile app/mobile-app");
        Assert.Equal((gitLab, gitHub), (served.GitLab.Requests.Count, served.GitHub.Requests.Count));
        azure = served.AzureDevOps.Requests.Count;
        await webApp.Operations.RefreshPullRequestsAsync();
        Assert.Contains(served.GitLab.Requests.Skip(gitLab), request => request.Route == "mrs:northfield/platform/web-app");
        Assert.Equal((azure, gitHub), (served.AzureDevOps.Requests.Count, served.GitHub.Requests.Count));
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Another_organization_s_repository_asks_nothing_after_the_sign_in()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("requests providers");
        using var app = ShellDriver.StartWithTabs(data.Path, [served.Manifest.Path], gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        var manifest = await app.WaitForRepositoryAsync();
        await app.SignInAsync(HostingProvider.AzureDevOps, served.AzureDevOpsToken, served.Organization, Cancelled);
        await app.WaitForAsync(() => manifest.HostingState.PullRequests == PullRequestsState.OtherOrganization, "D356's state");
        await app.WaitForWatchersQuietAsync();
        var requests = served.AzureDevOps.Requests.Count;

        await manifest.Operations.RefreshPullRequestsAsync();
        await manifest.Operations.OpenPullRequestsInBrowserAsync();
        await manifest.Operations.SignInToHostAsync();
        await app.WaitForIdleAsync();

        Assert.Equal(requests, served.AzureDevOps.Requests.Count);
        Assert.DoesNotContain(served.AzureDevOps.Requests, request => request.Path.StartsWith("/harbor-co/", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(served.GitLab.Requests);
        Assert.Empty(served.GitHub.Requests);
        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Equal([$"http://127.0.0.1:{served.AzureDevOps.Port}/harbor-co/Docks/_git/manifest/pullrequests"], OpenedUrls(app));
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Signed_out_nothing_is_asked_of_either_provider()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("requests providers");
        using var app = ShellDriver.StartWithTabs(data.Path, [served.WebApp.Path, served.MobileApp.Path], activeTab: 0, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        var webApp = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => webApp.HostingState.PullRequests == PullRequestsState.SignedOut, "web-app signed out");
        app.Click(app.TabStrip().Tabs[1].Bounds.Center);
        var mobileApp = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => mobileApp.HostingState.PullRequests == PullRequestsState.SignedOut, "mobile-app signed out");

        await webApp.Operations.RefreshPullRequestsAsync();
        await webApp.Operations.OpenPullRequestsInBrowserAsync();
        await mobileApp.Operations.RefreshPullRequestsAsync();
        await mobileApp.Operations.OpenPullRequestsInBrowserAsync();

        Assert.Empty(served.GitLab.Requests);
        Assert.Empty(served.AzureDevOps.Requests);
        Assert.Empty(served.GitHub.Requests);
        Assert.Equal(
            [$"http://127.0.0.1:{served.GitLab.Port}/northfield/platform/web-app/-/merge_requests", $"http://127.0.0.1:{served.AzureDevOps.Port}/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequests"],
            OpenedUrls(app));
        End(app, data, served);
    }

    /// <summary>"hosting7b" served on port 0: the clones' URLs are rewritten to the fakes', so the app recognises them.</summary>
    private static async Task<ServedHosting7b> ServeAsync()
    {
        var copy = await Scenarios.Hosting7bAsync();
        try
        {
            return await Scenarios.ServeHosting7bAsync(copy, azureDevOpsPort: 0, gitLabPort: 0, gitHubPort: 0, ownsCopy: true, ct: Cancelled);
        }
        catch
        {
            copy.Dispose();
            throw;
        }
    }

    /// <summary>One tab on <paramref name="clone"/>, signed in to <paramref name="provider"/>, once its three requests are listed and the watchers are quiet.</summary>
    private static async Task<ShellDriver> OpenAsync(TempDirectory data, ServedHosting7b served, TempRepo clone, HostingProvider provider)
    {
        var app = ShellDriver.StartWithTabs(data.Path, [clone.Path], gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        try
        {
            var repository = await app.WaitForRepositoryAsync();
            if (provider == HostingProvider.AzureDevOps)
            {
                await app.SignInAsync(provider, served.AzureDevOpsToken, served.Organization, Cancelled);
            }
            else
            {
                await app.SignInAsync(provider, served.GitLabToken, cancellationToken: Cancelled);
            }

            await app.WaitForAsync(() => repository.HostingState.List?.PullRequests.Count == 3, "three requests listed");
            await app.WaitForWatchersQuietAsync();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    private static PullRequest Request(RepositoryViewModel repository, int number) =>
        repository.HostingState.List!.PullRequests.Single(pull => pull.Number == number);

    private static List<string> OpenedUrls(ShellDriver app) =>
        [.. app.Launcher.Requests.Where(request => request.Kind == FileLaunchKind.OpenUrl).Select(request => request.FullPath)];

    private static async Task<List<string>> RefsAsync(TempRepo repo) =>
        [.. (await GitAsync(repo, "for-each-ref", "--format=%(refname)")).Split('\n', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal)];

    private static async Task<string> GitAsync(TempRepo repo, params string[] arguments) => (await repo.GitAsync(arguments)).StandardOutput.Trim();

    /// <summary>Closes the app, then checks that no token reached the data folder or the clones and that no fake saw anything wrong.</summary>
    private static void End(ShellDriver app, TempDirectory data, ServedHosting7b served)
    {
        var calls = app.Session.GitCalls;
        app.Dispose();
        foreach (var token in new[] { served.AzureDevOpsToken, served.GitLabToken, served.GitLabLegacyToken, served.GitHubToken })
        {
            TokenTripwire.AssertAbsent(token, [data.Path, served.MobileApp.Path, served.Manifest.Path, served.WebApp.Path], calls);
        }

        Assert.Empty(served.AzureDevOps.Errors);
        Assert.Empty(served.GitLab.Errors);
        Assert.Empty(served.GitHub.Errors);
        Assert.Empty(app.RefusedRequests.Addresses);
    }
}
