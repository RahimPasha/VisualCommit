using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// A pull request's actions on <c>hello</c> of the scenario "hosting" (docs/phase-7a-design.md 10.5,
/// 10.6; D305, D321, D334), the app started through <see cref="ShellDriver"/> with the fake of GitHub
/// on port 0 and signed in: #1's checkout tracks <c>origin/feature/login</c>; a head not fetched yet
/// is fetched first; #5's through <c>FETCH_HEAD</c> writes no ref but the branch and no
/// configuration; a taken name; a reused branch; open in browser, copy link, refresh; and nothing
/// asked of GitHub signed out.
/// </summary>
public sealed class PullRequestOperationsTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [AvaloniaFact]
    public async Task The_same_repository_s_pull_request_checks_out_its_tracking_branch()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("pull requests");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;

        await repository.Operations.CheckOutPullRequestAsync(PullRequest(repository, 1));
        await app.WaitForIdleAsync();

        Assert.Equal("feature/login", await BranchAsync(served.Hello));
        Assert.Equal("origin", await GitAsync(served.Hello, "config", "branch.feature/login.remote"));
        Assert.Equal("refs/heads/feature/login", await GitAsync(served.Hello, "config", "branch.feature/login.merge"));
        Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.Contains("git fetch", StringComparison.Ordinal));
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_head_not_fetched_yet_is_fetched_first()
    {
        await using var served = await ServeAsync();
        var bare = served.GitHub.Repository("octo/hello").BarePath!;
        var fork = (await GitInAsync(bare, "rev-parse", "refs/pull/5/head")).Trim();
        await GitInAsync(bare, "update-ref", "refs/heads/feature/new", fork);
        served.GitHub.AddPullRequest("octo", "hello", "New work", "octo", "feature/new", "main");
        await served.GitHub.SyncRefsAsync("octo/hello", Cancelled);
        using var data = new TempDirectory("pull requests");
        using var app = await OpenAsync(data, served, count: 5);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        Assert.Null(repository.Refs!.Refs.FirstOrDefault(gitRef => gitRef.FullName == "refs/remotes/origin/feature/new"));

        await repository.Operations.CheckOutPullRequestAsync(PullRequest(repository, 6));
        await app.WaitForIdleAsync();

        Assert.Equal("feature/new", await BranchAsync(served.Hello));
        Assert.Equal(fork, await GitAsync(served.Hello, "rev-parse", "HEAD"));
        Assert.Equal("refs/heads/feature/new", await GitAsync(served.Hello, "config", "branch.feature/new.merge"));
        Assert.Contains(app.GitCalls(), call => call.CommandText.StartsWith("git fetch --progress origin", StringComparison.Ordinal));
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_fork_s_pull_request_is_fetched_into_fetch_head_and_checked_out_without_upstream()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("pull requests");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var refsBefore = await RefsAsync(served.Hello);
        var configBefore = await GitAsync(served.Hello, "config", "--local", "--list");
        var head = (await GitInAsync(served.GitHub.Repository("octo/hello").BarePath!, "rev-parse", "refs/pull/5/head")).Trim();

        await repository.Operations.CheckOutPullRequestAsync(PullRequest(repository, 5));
        await app.WaitForIdleAsync();

        Assert.Equal("fix-typo", await BranchAsync(served.Hello));
        Assert.Equal(head, await GitAsync(served.Hello, "rev-parse", "HEAD"));
        Assert.Equal(refsBefore.Append("refs/heads/fix-typo").Order(StringComparer.Ordinal), await RefsAsync(served.Hello));
        Assert.Equal(configBefore, await GitAsync(served.Hello, "config", "--local", "--list"));
        Assert.Contains(app.GitCalls(), call => call.CommandText == "git fetch --progress --no-tags --refmap= --write-fetch-head origin refs/pull/5/head");
        Assert.Contains(app.GitCalls(), call => call.CommandText == $"git branch --no-track fix-typo {head}");
        Assert.Contains(served.GitHub.GitRequests, request => request.GivenUserName == "x-access-token");
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_taken_name_gives_the_owner_s_and_a_branch_at_the_head_is_reused_without_a_fetch()
    {
        await using var served = await ServeAsync();
        await served.Hello.GitAsync("branch", "fix-typo", "main");
        using var data = new TempDirectory("pull requests");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;

        await repository.Operations.CheckOutPullRequestAsync(PullRequest(repository, 5));
        await app.WaitForIdleAsync();

        Assert.Equal("ada-fix-typo", await BranchAsync(served.Hello));
        Assert.Equal(string.Empty, await GitAsync(served.Hello, "config", "--get-regexp", "^branch\\.ada-fix-typo\\.", allowFailure: true));

        // Back on main, the same pull request reuses ada-fix-typo: a checkout, no fetch.
        await repository.Operations.CheckoutAsync("refs/heads/main");
        await app.WaitForIdleAsync();
        var calls = app.GitCalls().Count;
        await repository.Operations.CheckOutPullRequestAsync(PullRequest(repository, 5));
        await app.WaitForIdleAsync();

        Assert.Equal("ada-fix-typo", await BranchAsync(served.Hello));
        Assert.DoesNotContain(app.GitCalls().Skip(calls), call => call.CommandText.Contains("git fetch", StringComparison.Ordinal) || call.CommandText.StartsWith("git branch", StringComparison.Ordinal));
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task Open_copy_and_refresh()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("pull requests");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var one = PullRequest(repository, 1);
        var requests = served.GitHub.Requests.Count;

        await repository.Operations.OpenPullRequestAsync(one);
        await repository.Operations.CopyPullRequestLinkAsync(one);
        await repository.Operations.OpenPullRequestsInBrowserAsync();

        var launched = app.Launcher.Requests.Where(request => request.Kind == FileLaunchKind.OpenUrl).Select(request => request.FullPath).ToList();
        Assert.Equal([one.HtmlUrl.AbsoluteUri, new Uri(served.GitHub.Endpoints.WebBase, "octo/hello/pulls").AbsoluteUri], launched);
        Assert.Equal(one.HtmlUrl.AbsoluteUri, app.Clipboard.Texts[^1]);
        Assert.Equal(requests, served.GitHub.Requests.Count);

        await repository.Operations.RefreshPullRequestsAsync();

        Assert.Contains(served.GitHub.Requests.Skip(requests), request => request.Route == "pulls:octo/hello");
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task Signed_out_nothing_is_asked_of_GitHub()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("pull requests");
        using var app = ShellDriver.StartWithTabs(data.Path, [served.Hello.Path], gitHub: served.GitHub);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.SignedOut, "the section signed out");

        await repository.Operations.RefreshPullRequestsAsync();
        await repository.Operations.OpenPullRequestsInBrowserAsync();

        Assert.Empty(served.GitHub.Requests);
        Assert.Equal([new Uri(served.GitHub.Endpoints.WebBase, "octo/hello/pulls").AbsoluteUri], app.Launcher.Requests.Where(request => request.Kind == FileLaunchKind.OpenUrl).Select(request => request.FullPath));
        await EndAsync(app, data, served);
    }

    /// <summary>The scenario "hosting" served on port 0: the clones' URLs are the fake's, so the app recognises them (design 16.3).</summary>
    private static async Task<ServedHosting> ServeAsync()
    {
        var copy = await Scenarios.HostingAsync();
        try
        {
            return await Scenarios.ServeHostingAsync(copy, port: 0, ct: Cancelled, ownsCopy: true);
        }
        catch
        {
            copy.Dispose();
            throw;
        }
    }

    /// <summary>One tab on <c>hello</c>, signed in as octo, once its <paramref name="count"/> pull requests are listed and the watchers are quiet.</summary>
    private static async Task<ShellDriver> OpenAsync(TempDirectory data, ServedHosting served, int count = 4)
    {
        var app = ShellDriver.StartWithTabs(data.Path, [served.Hello.Path], gitHub: served.GitHub);
        try
        {
            var repository = await app.WaitForRepositoryAsync();
            await app.SignInAsync(served.OctoToken, Cancelled);
            await app.WaitForAsync(() => repository.HostingState.List?.PullRequests.Count == count, $"{count} pull requests listed");
            await app.WaitForWatchersQuietAsync();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    private static PullRequest PullRequest(RepositoryViewModel repository, int number) =>
        repository.HostingState.List!.PullRequests.Single(pull => pull.Number == number);

    private static async Task<string> BranchAsync(TempRepo repo) => await GitAsync(repo, "symbolic-ref", "--short", "HEAD");

    private static async Task<List<string>> RefsAsync(TempRepo repo) =>
        [.. (await GitAsync(repo, "for-each-ref", "--format=%(refname)")).Split('\n', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal)];

    private static async Task<string> GitAsync(TempRepo repo, string first, params string[] rest) => await GitAsync(repo, [first, .. rest], allowFailure: false);

    private static async Task<string> GitAsync(TempRepo repo, string first, string second, string third, bool allowFailure) =>
        await GitAsync(repo, [first, second, third], allowFailure);

    private static async Task<string> GitAsync(TempRepo repo, string[] arguments, bool allowFailure)
    {
        var result = allowFailure ? await repo.GitAllowingFailureAsync(arguments) : await repo.GitAsync(arguments);
        return result.StandardOutput.Trim();
    }

    private static async Task<string> GitInAsync(string gitDirectory, params string[] arguments)
    {
        using var scratch = await TempRepo.CreateAsync();
        return (await scratch.GitAsync(["--git-dir", gitDirectory, .. arguments])).StandardOutput;
    }

    /// <summary>Closes the app, then checks that no token reached the data folder or the clones and that the fake saw nothing wrong.</summary>
    private static Task EndAsync(ShellDriver app, TempDirectory data, ServedHosting served)
    {
        app.Dispose();
        foreach (var token in new[] { served.OctoToken, served.OctoClassicToken, served.AdaToken })
        {
            TokenTripwire.AssertAbsent(token, [data.Path, served.Hello.Path, served.Tool.Path], null);
        }

        Assert.Empty(served.GitHub.Errors);
        Assert.Empty(app.RefusedRequests.Addresses);
        return Task.CompletedTask;
    }
}
