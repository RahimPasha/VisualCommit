using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;
using P = VisualCommit.VisualTests.Phase7b.Phase7bExpectations.PartP;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// Part P's checkouts (P11; design 12.5, D361, ruling 19): method 6, GitLab's <c>!21</c> on its
/// tracking branch and <c>!23</c> through <c>FETCH_HEAD</c> from <c>refs/merge-requests/23/head</c>;
/// method 7, Azure DevOps' <c>!11</c> and <c>!13</c>'s reason, no request ref ever fetched.
/// </summary>
public sealed partial class Phase7bChecksP
{
    [AvaloniaFact]
    public async Task Check_P11_gitlab_checkout_of_21_and_23()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b p11 gitlab");
        var app = await OpenAsync(data, served, WebApp);
        string configBefore;
        string[] refsBefore;
        string head;
        try
        {
            // Step 1.
            var webApp = app.Session.Shell.ActiveTab.Repository!;
            await WaitForListedAsync(app, WebApp, 3, P.GitLabListed, 0);
            configBefore = (await served.WebApp.GitAsync("config", "--local", "--list")).StandardOutput;
            refsBefore = RefsOf((await served.WebApp.GitAsync("for-each-ref", "--format=%(refname)")).StandardOutput);
            var ended = EndedOperations(app);
            var dialogs = 0;
            app.Session.Services.DialogHost.Open.CollectionChanged += (_, change) => dialogs += change.NewItems?.Count ?? 0;

            // Step 2: a double-click on !21 checks out feature/search, no fetch.
            var mark = Mark(served);
            var calls = await app.QuietCallMarkAsync();
            app.DoubleClick(await RequestAsync(app, 21));
            await app.WaitForAsync(() => webApp.Refs?.Head.BranchName == "feature/search", "feature/search checked out");
            await app.WaitForIdleAsync();
            await app.WaitForWatchersQuietAsync();
            AssertStatus(app, P.CheckedOut("feature/search"));
            Assert.Equal([(OperationOutcome.Succeeded, P.CheckedOut("feature/search"))], ended.Select(record => (record.Outcome, record.Message)));
            AssertCalled(app, calls, "git switch --no-guess --no-overwrite-ignore feature/search");
            Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git fetch", StringComparison.Ordinal) || call.StartsWith("git branch", StringComparison.Ordinal));
            AssertNoRequests(served, mark);
            Assert.Equal("refs/heads/feature/search", (await served.WebApp.GitAsync("symbolic-ref", "HEAD")).StandardOutput.Trim());
            Assert.Equal("origin", (await served.WebApp.GitAsync("config", "branch.feature/search.remote")).StandardOutput.Trim());
            Assert.Equal("refs/heads/feature/search", (await served.WebApp.GitAsync("config", "branch.feature/search.merge")).StandardOutput.Trim());
            app.RightClick(await RequestAsync(app, 21));
            Assert.Contains($"{P.CheckOut("feature/search")} (disabled: \"{P.AlreadyCheckedOut("feature/search")}\")", app.Menu().Lines());
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);

            // Step 3: !23 through FETCH_HEAD from GitLab's head ref.
            mark = Mark(served);
            var gitRequests = served.GitLab.GitRequests.Count;
            var logMark = app.LogMark();
            var operations = ended.Count;
            var bare = Path.Combine(served.MobileApp.SiblingPath("gitlab"), "northfield", "platform", "web-app.git");
            head = (await served.WebApp.GitAsync("--git-dir", bare, "rev-parse", "refs/merge-requests/23/head")).StandardOutput.Trim();
            calls = await app.QuietCallMarkAsync();
            app.RightClick(await RequestAsync(app, 23));
            app.ClickMenuItem(P.CheckOut("ada:fix-typo"));
            await app.WaitForAsync(() => webApp.Refs?.Head.BranchName == "fix-typo", "fix-typo checked out");
            await app.WaitForIdleAsync();
            await WaitForListedAsync(app, WebApp, 3, P.GitLabListed, logMark);
            await AssertNoDialogAsync(app);
            ShotP(app, P.P11a);
            AssertStatus(app, P.CheckedOut("fix-typo"));
            Assert.Equal(
                [(P.FetchMergeRequest23.Title, OperationOutcome.Succeeded, P.FetchedMergeRequest23), (ended[^1].Title, OperationOutcome.Succeeded, P.CheckedOut("fix-typo"))],
                ended.Skip(operations).Select(record => (record.Title, record.Outcome, record.Message)));
            Assert.Equal(0, dialogs);
            AssertCalledInOrder(
                app,
                calls,
                "git fetch --progress --no-tags --refmap= --write-fetch-head origin refs/merge-requests/23/head",
                $"git branch --no-track fix-typo {head}",
                "git switch --no-guess --no-overwrite-ignore fix-typo");
            var since = CallsSince(app, calls);
            Assert.Single(since, call => call.StartsWith("git fetch", StringComparison.Ordinal));
            Assert.DoesNotContain(since, call => call.Contains("FETCH_HEAD", StringComparison.Ordinal));
            Assert.DoesNotContain(since, call => call.StartsWith("git config", StringComparison.Ordinal) && !call.Contains("--get", StringComparison.Ordinal) && !call.Contains("--list", StringComparison.Ordinal));
            var git = served.GitLab.GitRequests.Skip(gitRequests).Where(request => request.Path.Contains("northfield/platform/web-app.git", StringComparison.Ordinal)).ToList();
            Assert.Contains(git, request => request.GivenUserName == "x-access-token" && request.Status == 200);
            Assert.DoesNotContain(git, request => request.GivenUserName is { } user && user != "x-access-token");
            Assert.DoesNotContain(git, request => request.GivenUserName is not null && request.Status == 401);
            var api = GitLabSince(served, mark);
            var refresh = Assert.Single(api);
            AssertRequest(refresh, P.GitLabListPath, P.GitLabListRoute, P.GitLabListQuery, FakeAuthorizationForm.Bearer, 304, AdaLogin);
            Assert.Empty(AzureSince(served, mark));
            Assert.Empty(GitHubSince(served, mark));
            var top = webApp.Graph.CommitAt(0);
            Assert.Equal((head, "Fix a typo in the footer", AdaName), (top.Sha, top.Subject, top.AuthorName));
            var fixTypo = (RefItemRow)app.RefRowModel("Local branches", "fix-typo");
            Assert.Equal((true, false), (fixTypo.IsHead, fixTypo.ShowsPullRequest));
            Assert.Equal([$"v {P.GitLabTitle} 3", .. P.GitLabRows.Select(row => row.Name)], Section(app));
            app.RightClick(await RequestAsync(app, 23));
            Assert.Contains($"{P.CheckOut("ada:fix-typo")} (disabled: \"{P.AlreadyCheckedOut("fix-typo")}\")", app.Menu().Lines());
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, [GitLabToken], [], []);

        // Step 4: git's state after the app closed.
        var repo = served.WebApp;
        Assert.Equal("refs/heads/fix-typo", (await repo.GitAsync("symbolic-ref", "HEAD")).StandardOutput.Trim());
        Assert.Equal(head, (await repo.GitAsync("rev-parse", "HEAD")).StandardOutput.Trim());
        var refs = RefsOf((await repo.GitAsync("for-each-ref", "--format=%(refname)")).StandardOutput);
        Assert.Equal(refsBefore.Append("refs/heads/fix-typo").Order(StringComparer.Ordinal), refs.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(refs, name => name.StartsWith("refs/merge-requests/", StringComparison.Ordinal) || name.StartsWith("refs/remotes/origin/mr/", StringComparison.Ordinal));
        Assert.Equal("+refs/heads/*:refs/remotes/origin/*", (await repo.GitAsync("config", "--get-all", "remote.origin.fetch")).StandardOutput.Trim());
        Assert.Equal("+refs/heads/*:refs/remotes/upstream/*", (await repo.GitAsync("config", "--get-all", "remote.upstream.fetch")).StandardOutput.Trim());
        var tracking = await repo.GitAllowingFailureAsync("config", "--get-regexp", "^branch\\.fix-typo\\.");
        Assert.Equal((1, string.Empty), (tracking.ExitCode, tracking.StandardOutput));
        Assert.Equal(configBefore, (await repo.GitAsync("config", "--local", "--list")).StandardOutput);
        var fetchHead = File.ReadAllText(Path.Combine(repo.Path, ".git", "FETCH_HEAD"));
        Assert.Single(fetchHead.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.StartsWith($"{head}\t\t'refs/merge-requests/23/head' of ", fetchHead, StringComparison.Ordinal);
        Assert.Equal(string.Empty, (await repo.GitAsync("status", "--porcelain")).StandardOutput);
    }

    [AvaloniaFact]
    public async Task Check_P11_azure_checkout_of_11_and_13s_reason()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b p11 azure");
        var app = await OpenAsync(data, served);
        var repo = served.MobileApp;
        var fetchHeadFile = Path.Combine(repo.Path, ".git", "FETCH_HEAD");
        string configBefore;
        string[] refsBefore;
        string fetchHeadBefore;
        long stepTwo;
        List<GitCallRecord> log = [];
        try
        {
            // Step 1.
            var mobileApp = app.Session.Shell.ActiveTab.Repository!;
            await WaitForListedAsync(app, MobileApp, 3, P.AzureDevOpsListed, 0);
            configBefore = (await repo.GitAsync("config", "--local", "--list")).StandardOutput;
            refsBefore = RefsOf((await repo.GitAsync("for-each-ref", "--format=%(refname)")).StandardOutput);
            fetchHeadBefore = File.ReadAllText(fetchHeadFile);
            var ended = EndedOperations(app);

            // Step 2: a double-click on !11 checks out feature/sign-in, no fetch.
            var mark = Mark(served);
            stepTwo = await app.QuietCallMarkAsync();
            app.DoubleClick(await RequestAsync(app, 11));
            await app.WaitForAsync(() => mobileApp.Refs?.Head.BranchName == "feature/sign-in", "feature/sign-in checked out");
            await app.WaitForIdleAsync();
            await app.WaitForWatchersQuietAsync();
            AssertStatus(app, P.CheckedOut("feature/sign-in"));
            Assert.Equal([(OperationOutcome.Succeeded, P.CheckedOut("feature/sign-in"))], ended.Select(record => (record.Outcome, record.Message)));
            AssertCalled(app, stepTwo, "git switch --no-guess --no-overwrite-ignore feature/sign-in");
            Assert.DoesNotContain(CallsSince(app, stepTwo), call => call.StartsWith("git fetch", StringComparison.Ordinal) || call.StartsWith("git branch", StringComparison.Ordinal));
            AssertNoRequests(served, mark);
            Assert.Equal("origin", (await repo.GitAsync("config", "branch.feature/sign-in.remote")).StandardOutput.Trim());
            app.RightClick(await RequestAsync(app, 11));
            Assert.Contains($"{P.CheckOut("feature/sign-in")} (disabled: \"{P.AlreadyCheckedOut("feature/sign-in")}\")", app.Menu().Lines());
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);

            // Step 3: !13's reason; its double-click starts nothing.
            mark = Mark(served);
            var operations = ended.Count;
            var calls = await app.QuietCallMarkAsync();
            app.RightClick(await RequestAsync(app, 13));
            AssertWholeMenu(app, P.RequestMenu(P.AzureDevOpsRows[0]), P.OpenInBrowser, P.RequestMenuHeight);
            Assert.Equal(P.AzureDevOpsForkReason, await app.RestPointerOnMenuItemAsync(P.CheckOut("mobile-app-fork:fix-typo")));
            ShotP(app, P.P11b, keepPointer: true);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            app.DoubleClick(await RequestAsync(app, 13));
            await app.WaitForIdleAsync();
            await app.WaitForWatchersQuietAsync();
            Assert.Equal(operations, ended.Count);
            GitCallBudget.AssertAtMost([.. app.GitCalls().Where(call => call.Sequence > calls)], 0, "!13's double-click");
            AssertNoRequests(served, mark);
            Assert.Equal("feature/sign-in", mobileApp.Refs?.Head.BranchName);
            Assert.DoesNotContain(mobileApp.LeftPanel.Rows, row => row is RefItemRow { IsSelected: true } or PullRequestRow { IsSelected: true });
            Assert.Null(mobileApp.Selection.Single);
            log = [.. app.GitCalls()];
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, [AzureDevOpsToken], [], []);

        // Step 4: git's state; no request ref fetched.
        Assert.Equal("refs/heads/feature/sign-in", (await repo.GitAsync("symbolic-ref", "HEAD")).StandardOutput.Trim());
        var refs = RefsOf((await repo.GitAsync("for-each-ref", "--format=%(refname)")).StandardOutput);
        Assert.Equal(refsBefore.Order(StringComparer.Ordinal), refs.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(refs, name => name.StartsWith("refs/pull/", StringComparison.Ordinal));
        Assert.Equal("+refs/heads/*:refs/remotes/origin/*", (await repo.GitAsync("config", "--get-all", "remote.origin.fetch")).StandardOutput.Trim());
        Assert.Equal(configBefore, (await repo.GitAsync("config", "--local", "--list")).StandardOutput);
        Assert.Equal(fetchHeadBefore, File.ReadAllText(fetchHeadFile));
        Assert.DoesNotContain(log, call => call.CommandText.StartsWith("git fetch", StringComparison.Ordinal) && call.CommandText.Contains("refs/pull/", StringComparison.Ordinal));
        Assert.DoesNotContain(log, call => call.Sequence > stepTwo && call.CommandText.StartsWith("git fetch", StringComparison.Ordinal));
        Assert.Equal(string.Empty, (await repo.GitAsync("status", "--porcelain")).StandardOutput);
    }

    private static string[] RefsOf(string output) => output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
