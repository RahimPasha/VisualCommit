using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using P = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartP;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>Part P's method 3 (docs/test-reports/phase-7a.md, P13 and P6's tool): the checkouts of #1 and #5 with git's state checked, then the fork tool listing its parent's.</summary>
public sealed partial class Phase7aChecksP
{
    [AvaloniaFact]
    public async Task Check_P13_P6_checkout_and_a_forks_tab()
    {
        // P13 step 1.
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a p13");
        var app = ShellDriver.StartWithTabs(data.Path, [served.Hello.Path, served.Tool.Path], activeTab: 0, gitHub: served.GitHub);
        string configBefore;
        string[] refsBefore;
        try
        {
            var hello = await app.WaitForRepositoryAsync();
            await app.SignInAsync(served.OctoToken, Cancelled);
            await WaitForListedAsync(app, "hello", 4, "octo/hello", 0);
            configBefore = (await served.Hello.GitAsync("config", "--local", "--list")).StandardOutput;
            refsBefore = (await served.Hello.GitAsync("for-each-ref", "--format=%(refname)")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // P13 step 2: a double-click on #1 checks out feature/login, no fetch.
            var requests = served.GitHub.Requests.Count;
            var mark = await app.QuietCallMarkAsync();
            app.DoubleClick(await PullRequestAsync(app, 1));
            await app.WaitForAsync(() => hello.Refs?.Head.BranchName == "feature/login", "feature/login checked out");
            await app.WaitForIdleAsync();
            await app.WaitForWatchersQuietAsync();
            AssertStatus(app, P.CheckedOut("feature/login"));
            AssertCalled(app, mark, "git switch --no-guess --no-overwrite-ignore feature/login");
            Assert.DoesNotContain(CallsSince(app, mark), call => call.StartsWith("git fetch", StringComparison.Ordinal) || call.StartsWith("git branch", StringComparison.Ordinal));
            Assert.Empty(ApiSince(served, requests));
            app.RightClick(await PullRequestAsync(app, 1));
            Assert.Contains($"{P.CheckOutName("feature/login")} (disabled: \"{P.AlreadyCheckedOut("feature/login")}\")", app.Menu().Lines());
            app.PressKey(Key.Escape);

            // P13 step 3: #5 through FETCH_HEAD.
            requests = served.GitHub.Requests.Count;
            var gitRequests = served.GitHub.GitRequests.Count;
            var logMark = app.LogMark();
            mark = await app.QuietCallMarkAsync();
            var head = (await served.Hello.GitAsync("--git-dir", served.GitHub.Repository("octo/hello").BarePath!, "rev-parse", "refs/pull/5/head")).StandardOutput.Trim();
            app.RightClick(await PullRequestAsync(app, 5));
            app.ClickMenuItem(P.CheckOutName("ada:fix-typo"));
            await app.WaitForAsync(() => hello.Refs?.Head.BranchName == "fix-typo", "fix-typo checked out");
            await app.WaitForIdleAsync();
            await WaitForListedAsync(app, "hello", 4, "octo/hello", logMark);
            Shot(app, P.P13a);
            AssertStatus(app, P.CheckedOut("fix-typo"));
            AssertCalledInOrder(
                app,
                mark,
                "git fetch --progress --no-tags --refmap= --write-fetch-head origin refs/pull/5/head",
                $"git branch --no-track fix-typo {head}",
                "git switch --no-guess --no-overwrite-ignore fix-typo");
            Assert.Single(CallsSince(app, mark), call => call.StartsWith("git fetch", StringComparison.Ordinal));
            Assert.DoesNotContain(CallsSince(app, mark), call => call.Contains("FETCH_HEAD", StringComparison.Ordinal));
            var git = served.GitHub.GitRequests.Skip(gitRequests).ToList();
            Assert.Contains(git, request => request.GivenUserName == "x-access-token");
            Assert.DoesNotContain(git, request => request.GivenUserName is { } user && user != "x-access-token");
            var api = ApiSince(served, requests);
            Assert.Equal(["pulls:octo/hello"], api.Select(request => request.Route));
            Assert.DoesNotContain(api, request => request.Status == 401);
            Assert.Equal(["v Pull requests 4", .. P.HelloRows.Select(row => row.Name)], Section(app));
            Assert.False(((VisualCommit.App.ViewModels.Panels.RefItemRow)app.RefRowModel("Local branches", "fix-typo")).ShowsPullRequest);
            app.RightClick(await PullRequestAsync(app, 5));
            Assert.Contains($"{P.CheckOutName("ada:fix-typo")} (disabled: \"{P.AlreadyCheckedOut("fix-typo")}\")", app.Menu().Lines());
            app.PressKey(Key.Escape);

            // P6 steps 4 and 5: tool, a fork, lists its parent's.
            app.Click(app.TabStrip().Tabs[1].Bounds.Center);
            var tool = await app.WaitForRepositoryAsync();
            await WaitForListedAsync(app, "tool", 2, "acme/tool", 0);
            Shot(app, P.P06b);
            Assert.Equal(1, served.GitHub.Requests.Count(request => request.Route == "repo:octo/tool"));
            Assert.Equal(1, served.GitHub.Requests.Count(request => request.Route == "pulls:acme/tool"));
            Assert.DoesNotContain(served.GitHub.Requests, request => request.Route == "pulls:octo/tool");
            Assert.Equal("acme/tool", tool.HostingState.Repository?.FullName);
            Assert.Equal(["refs/heads/fix-typo", "refs/remotes/origin/fix-typo"], tool.HostingState.ByRef.Keys.Order(StringComparer.Ordinal));
            Assert.Equal(["v Pull requests 2", .. P.ToolRows.Select(row => row.Name)], Section(app));
            app.RightClick(await PullRequestAsync(app, 7));
            AssertMenu(app.Menu(), P.PullRequestMenu(7, "Fix a typo in the readme", P.CheckOutName("fix-typo")));
            app.PressKey(Key.Escape);
            app.RightClick(await PullRequestAsync(app, 8));
            AssertMenu(app.Menu(), P.PullRequestMenu(8, "An old idea", P.CheckOutName("idea")));
            app.PressKey(Key.Escape);
        }
        finally
        {
            End(app, data, served);
        }

        // P13 step 6: git's state after the app closed.
        Assert.Equal("refs/heads/fix-typo", (await served.Hello.GitAsync("symbolic-ref", "HEAD")).StandardOutput.Trim());
        var refs = (await served.Hello.GitAsync("for-each-ref", "--format=%(refname)")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(refsBefore.Append("refs/heads/fix-typo").Order(StringComparer.Ordinal), refs.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(refs, name => name.StartsWith("refs/pull/", StringComparison.Ordinal) || name.StartsWith("refs/remotes/origin/pr/", StringComparison.Ordinal));
        Assert.Equal("+refs/heads/*:refs/remotes/origin/*", (await served.Hello.GitAsync("config", "--get-all", "remote.origin.fetch")).StandardOutput.Trim());
        Assert.Equal(1, (await served.Hello.GitAllowingFailureAsync("config", "--get-regexp", "^branch\\.fix-typo\\.")).ExitCode);
        Assert.Equal(configBefore, (await served.Hello.GitAsync("config", "--local", "--list")).StandardOutput);
        var fetchHead = File.ReadAllText(Path.Combine(served.Hello.Path, ".git", "FETCH_HEAD"));
        Assert.Single(fetchHead.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.StartsWith($"{(await served.Hello.GitAsync("rev-parse", "HEAD")).StandardOutput.Trim()}\t\t'refs/pull/5/head' of ", fetchHead, StringComparison.Ordinal);
        Assert.Equal(string.Empty, (await served.Hello.GitAsync("status", "--porcelain")).StandardOutput);
    }
}
