using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Operations;
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
        string head;
        try
        {
            var hello = await app.WaitForRepositoryAsync();
            await app.SignInAsync(served.OctoToken, Cancelled);

            // The sign-in lists every open tab with a GitHub remote (design 7.8, D289) through the one
            // client, one request at a time (D285): tool's list, in the background, can be answered
            // after hello's line, so step 2 counts the requests only once both are in (CI run
            // 40982132445, macOS). tool is not shown, so its avatars' line comes only in step 4.
            await WaitForListLineAsync(app, "tool", 2, "acme/tool", 0);
            await WaitForListedAsync(app, "hello", 4, "octo/hello", 0);
            configBefore = (await served.Hello.GitAsync("config", "--local", "--list")).StandardOutput;
            refsBefore = (await served.Hello.GitAsync("for-each-ref", "--format=%(refname)")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var ended = EndedOperations(app);
            var dialogs = 0;
            app.Session.Services.DialogHost.Open.CollectionChanged += (_, change) => dialogs += change.NewItems?.Count ?? 0;

            // P13 step 2: a double-click on #1 checks out feature/login, no fetch.
            var requests = served.GitHub.Requests.Count;
            var mark = await app.QuietCallMarkAsync();
            app.DoubleClick(await PullRequestAsync(app, 1));
            await app.WaitForAsync(() => hello.Refs?.Head.BranchName == "feature/login", "feature/login checked out");
            await app.WaitForIdleAsync();
            await app.WaitForWatchersQuietAsync();
            AssertStatus(app, P.CheckedOut("feature/login"));
            Assert.Equal([(OperationOutcome.Succeeded, P.CheckedOut("feature/login"))], ended.Select(record => (record.Outcome, record.Message)));
            AssertCalled(app, mark, "git switch --no-guess --no-overwrite-ignore feature/login");
            Assert.DoesNotContain(CallsSince(app, mark), call => call.StartsWith("git fetch", StringComparison.Ordinal) || call.StartsWith("git branch", StringComparison.Ordinal));
            Assert.Empty(ApiSince(served, requests));
            Assert.Equal("refs/heads/feature/login", (await served.Hello.GitAsync("symbolic-ref", "HEAD")).StandardOutput.Trim());
            app.RightClick(await PullRequestAsync(app, 1));
            Assert.Contains($"{P.CheckOutName("feature/login")} (disabled: \"{P.AlreadyCheckedOut("feature/login")}\")", app.Menu().Lines());
            app.PressKey(Key.Escape);

            // P13 step 3: #5 through FETCH_HEAD.
            requests = served.GitHub.Requests.Count;
            var gitRequests = served.GitHub.GitRequests.Count;
            var logMark = app.LogMark();
            var operations = ended.Count;
            mark = await app.QuietCallMarkAsync();
            head = (await served.Hello.GitAsync("--git-dir", served.GitHub.Repository("octo/hello").BarePath!, "rev-parse", "refs/pull/5/head")).StandardOutput.Trim();
            app.RightClick(await PullRequestAsync(app, 5));
            app.ClickMenuItem(P.CheckOutName("ada:fix-typo"));
            await app.WaitForAsync(() => hello.Refs?.Head.BranchName == "fix-typo", "fix-typo checked out");
            await app.WaitForIdleAsync();
            await WaitForListedAsync(app, "hello", 4, "octo/hello", logMark);
            await AssertNoDialogAsync(app);
            ShotP(app, P.P13a);
            AssertStatus(app, P.CheckedOut("fix-typo"));
            Assert.Equal(
                [(P.FetchPullRequestTitle(5), OperationOutcome.Succeeded, P.FetchedPullRequest(5, "fix-typo")), (ended[^1].Title, OperationOutcome.Succeeded, P.CheckedOut("fix-typo"))],
                ended.Skip(operations).Select(record => (record.Title, record.Outcome, record.Message)));
            Assert.Equal(0, dialogs);
            AssertCalledInOrder(
                app,
                mark,
                "git fetch --progress --no-tags --refmap= --write-fetch-head origin refs/pull/5/head",
                $"git branch --no-track fix-typo {head}",
                "git switch --no-guess --no-overwrite-ignore fix-typo");
            var calls = CallsSince(app, mark);
            Assert.Single(calls, call => call.StartsWith("git fetch", StringComparison.Ordinal));
            Assert.DoesNotContain(calls, call => call.Contains("FETCH_HEAD", StringComparison.Ordinal));
            Assert.DoesNotContain(calls, call => call.StartsWith("git config", StringComparison.Ordinal) && !call.Contains("--get", StringComparison.Ordinal) && !call.Contains("--list", StringComparison.Ordinal));
            var git = served.GitHub.GitRequests.Skip(gitRequests).Where(request => request.Path.Contains("octo/hello", StringComparison.Ordinal)).ToList();
            Assert.Contains(git, request => request.GivenUserName == "x-access-token" && request.Status == 200);
            Assert.DoesNotContain(git, request => request.GivenUserName is { } user && user != "x-access-token");
            Assert.DoesNotContain(git, request => request.GivenUserName is not null && request.Status == 401);
            var api = ApiSince(served, requests);
            AssertPullsRequest(Assert.Single(api), 304, conditional: true);
            Assert.Equal(["v Pull requests 4", .. P.HelloRows.Select(row => row.Name)], Section(app));
            var top = hello.Graph.CommitAt(0);
            Assert.Equal((head, "Fix a typo in the greeting", "Ada Lovelace"), (top.Sha, top.Subject, top.AuthorName));
            var fixTypo = (RefItemRow)app.RefRowModel("Local branches", "fix-typo");
            Assert.Equal((true, false), (fixTypo.IsHead, fixTypo.ShowsPullRequest));
            app.RightClick(await PullRequestAsync(app, 5));
            Assert.Contains($"{P.CheckOutName("ada:fix-typo")} (disabled: \"{P.AlreadyCheckedOut("fix-typo")}\")", app.Menu().Lines());
            app.PressKey(Key.Escape);

            // P6 steps 4 and 5: tool, a fork, lists its parent's.
            app.Click(app.TabStrip().Tabs[1].Bounds.Center);
            var tool = await app.WaitForRepositoryAsync();
            await WaitForListedAsync(app, "tool", 2, "acme/tool", 0);
            ShotP(app, P.P06b);
            var repo = Assert.Single(served.GitHub.Requests, request => request.Route == "repo:octo/tool");
            var pulls = Assert.Single(served.GitHub.Requests, request => request.Route == "pulls:acme/tool");
            Assert.All([repo, pulls], request => Assert.Equal(("GET", true, 200), (request.Method, request.TokenPresent, request.Status)));
            Assert.DoesNotContain(served.GitHub.Requests, request => request.Route == "pulls:octo/tool");
            Assert.Equal([("origin", "octo/tool"), ("upstream", "acme/tool")], tool.HostingState.Remotes.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, pair.Value.FullName)));
            Assert.Equal("acme/tool", tool.HostingState.Repository?.FullName);
            Assert.Equal([8, 7], tool.HostingState.List!.PullRequests.Select(pull => pull.Number));
            Assert.Equal(
                [("refs/heads/fix-typo", 7), ("refs/remotes/origin/fix-typo", 7)],
                tool.HostingState.ByRef.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, pair.Value.Number)));
            Assert.Equal(["v Pull requests 2", .. P.ToolRows.Select(row => row.Name)], Section(app));
            Assert.Equal(P.ToolRows.Select(row => (row.Author, row.Draft)), PullRequestRows(app).Select(row => (row.Author, row.IsDraft)));
            Assert.Equal("#7", ((RefItemRow)app.RefRowModel("Local branches", "fix-typo")).PullRequestText);
            app.RightClick(await PullRequestAsync(app, 7));
            var seven = app.Menu();
            AssertMenu(seven, P.PullRequestMenu(7, "Fix a typo in the readme", P.CheckOutName("fix-typo")));
            AssertHighlighted(seven, P.OpenInBrowser);
            app.PressKey(Key.Escape);
            app.RightClick(await PullRequestAsync(app, 8));
            var eight = app.Menu();
            AssertMenu(eight, P.PullRequestMenu(8, "An old idea", P.CheckOutName("idea")));
            AssertHighlighted(eight, P.OpenInBrowser);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
        }
        finally
        {
            End(app, data, served, []);
        }

        // P13 step 6: git's state after the app closed.
        Assert.Equal("refs/heads/fix-typo", (await served.Hello.GitAsync("symbolic-ref", "HEAD")).StandardOutput.Trim());
        Assert.Equal(head, (await served.Hello.GitAsync("rev-parse", "HEAD")).StandardOutput.Trim());
        var refs = (await served.Hello.GitAsync("for-each-ref", "--format=%(refname)")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(refsBefore.Append("refs/heads/fix-typo").Order(StringComparer.Ordinal), refs.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(refs, name => name.StartsWith("refs/pull/", StringComparison.Ordinal) || name.StartsWith("refs/remotes/origin/pr/", StringComparison.Ordinal));
        Assert.Equal("+refs/heads/*:refs/remotes/origin/*", (await served.Hello.GitAsync("config", "--get-all", "remote.origin.fetch")).StandardOutput.Trim());
        var tracking = await served.Hello.GitAllowingFailureAsync("config", "--get-regexp", "^branch\\.fix-typo\\.");
        Assert.Equal((1, string.Empty), (tracking.ExitCode, tracking.StandardOutput));
        Assert.Equal(configBefore, (await served.Hello.GitAsync("config", "--local", "--list")).StandardOutput);
        var fetchHead = File.ReadAllText(Path.Combine(served.Hello.Path, ".git", "FETCH_HEAD"));
        Assert.Single(fetchHead.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.StartsWith($"{head}\t\t'refs/pull/5/head' of ", fetchHead, StringComparison.Ordinal);
        Assert.Equal(string.Empty, (await served.Hello.GitAsync("status", "--porcelain")).StandardOutput);
    }

    /// <summary>The records of the operations that end in the session from now on, in the order the operation log adds them.</summary>
    private static List<OperationRecord> EndedOperations(ShellDriver app)
    {
        var ended = new List<OperationRecord>();
        app.Session.Operations.Added += (_, record) =>
        {
            lock (ended)
            {
                ended.Add(record);
            }
        };
        return ended;
    }
}
