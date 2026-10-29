using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using P = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartP;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// Part P, pull requests in the left panel (docs/test-reports/phase-7a.md, part P, checks P1 to
/// P15; design 10.7): one method per app start, as the report's methods table gives them. This
/// file holds methods 1 (signed out, failed, loading, not GitHub) and 4 (more than 300, none);
/// <c>Phase7aChecks.P.Menus.cs</c> method 2, <c>Phase7aChecks.P.Checkout.cs</c> method 3.
/// </summary>
public sealed partial class Phase7aChecksP : Phase7aChecks
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [AvaloniaFact]
    public async Task Check_P2_P11_P4_P3_P1_signed_out_failed_loading_and_not_github()
    {
        // P2 step 1: hello and ops, signed out.
        await using var served = await ServeAsync();
        using var ops = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("phase7a p2");
        var port = served.GitHub.Port;
        var app = ShellDriver.StartWithTabs(data.Path, [served.Hello.Path, ops.Path], activeTab: 0, gitHub: served.GitHub);
        try
        {
            var hello = await app.WaitForRepositoryAsync();
            await WaitForStateAsync(app, hello, PullRequestsState.SignedOut, PullRequestMessageKind.SignIn);
            Shot(app, P.P02a);
            Assert.Null(app.Session.Services.Hosting.Account);
            Assert.Empty(served.GitHub.Requests);
            Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.Contains("--get-regexp ^url", StringComparison.Ordinal));
            Assert.Equal(("octo/hello", 0), (hello.HostingState.Repository?.FullName, hello.HostingState.ByRef.Count));
            Assert.Null(hello.HostingState.List);
            Assert.Empty(hello.Hosting.Bindings);
            Assert.Equal(["v Pull requests 0", "[SignIn] " + P.SignInToSeePullRequests], Section(app));
            Assert.DoesNotContain(hello.LeftPanel.Rows.OfType<RefItemRow>(), row => row.ShowsPullRequest);

            // P11 steps 2 and 3: the section's menu signed out; open in browser asks GitHub nothing.
            app.RightClick(await HeaderAsync(app));
            var signedOut = app.Menu();
            AssertMenu(signedOut, P.SectionMenuSignedOut);
            AssertHighlighted(signedOut, P.SignInToGitHubItem);
            AssertMenuShape(app, signedOut);
            Shot(app, P.P11a);
            app.ClickMenuItem(P.OpenPullRequestsInBrowser);
            await app.WaitForAsync(() => OpenedUrls(app).Count == 1, "the launcher's address");
            Assert.Equal([P.PullsUrl(port)], OpenedUrls(app));
            Assert.Empty(served.GitHub.Requests);
            Assert.Equal(["v Pull requests 0", "[SignIn] " + P.SignInToSeePullRequests], Section(app));

            // P2 step 4: the sign-in row opens "Add GitHub account" over hello's window.
            var requests = served.GitHub.Requests.Count;
            app.Click(await MessageAsync(app));
            var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
            Assert.Equal(string.Empty, dialog.Token);
            Assert.Equal(requests, served.GitHub.Requests.Count);

            // P4 step 5: GitHub refuses the list with 403.
            var logMark = app.LogMark();
            served.GitHub.FailNext("pulls:octo/hello", FakeGitHubFault.Forbidden);
            app.TypeToken(served.OctoToken);
            app.PressKey(Key.Enter);
            await app.WaitForAsync(() => dialog.IsClosed, "the dialog to close");
            await WaitForStateAsync(app, hello, PullRequestsState.Failed, PullRequestMessageKind.Failed);
            Shot(app, P.P04a);
            Assert.Equal(
                ["user 200", "repo:octo/hello 200", "pulls:octo/hello 403"],
                ApiSince(served, requests).Select(request => $"{request.Route} {request.Status}"));
            Assert.All(ApiSince(served, requests), request => Assert.True(request.TokenPresent));
            Assert.Equal(AccountState.SignedIn, app.Session.Services.Hosting.Account?.State);
            Assert.Equal((HostingFailureKind.Forbidden, P.ForbiddenDetail), (hello.HostingState.Failure?.Kind, hello.HostingState.Failure?.Detail));
            Assert.Equal(["v Pull requests 0", "[Failed] " + P.ForbiddenText], Section(app));
            Assert.Single(hello.Hosting.Bindings);
            Assert.DoesNotContain(app.LogLinesSince(logMark), line => line.Contains("hello: listed", StringComparison.Ordinal));

            // P4 step 6: the failed row's tooltip.
            var text = ((Control)await MessageAsync(app)).GetLogicalDescendants().OfType<TextBlock>().Single(block => block.Name == "PullRequestMessageText");
            Assert.Equal(P.ForbiddenText + "\n" + P.ForbiddenDetail, await app.RestPointerOnAsync(text));
            Shot(app, P.P04b, keepPointer: true);

            // P3 step 7: a refresh held at the fake shows Loading.
            logMark = app.LogMark();
            requests = served.GitHub.Requests.Count;
            var hold = served.GitHub.HoldNext("pulls:octo/hello");
            try
            {
                app.RightClick(await HeaderAsync(app));
                app.ClickMenuItem(P.RefreshPullRequests);
                await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
                await WaitForStateAsync(app, hello, PullRequestsState.Loading, PullRequestMessageKind.Loading);
                Shot(app, P.P03a);
                Assert.Empty(ApiSince(served, requests));
                Assert.Equal(["v Pull requests 0", "[Loading] " + P.LoadingPullRequests], Section(app));
                Assert.Null(hello.HostingState.Failure);

                // P11 step 8: refresh disabled while loading.
                app.RightClick(await HeaderAsync(app));
                var loading = app.Menu();
                AssertMenu(loading, P.SectionMenuLoading);
                AssertHighlighted(loading, P.OpenPullRequestsInBrowser);
                Assert.Equal(P.PullRequestsLoading, await app.RestPointerOnMenuItemAsync(P.RefreshPullRequests));
                Shot(app, P.P11b, keepPointer: true);
                app.PressKey(Key.Escape);
            }
            finally
            {
                hold.Dispose();
            }

            // P3 step 9: the answer fills the section.
            await WaitForListedAsync(app, "hello", 4, "octo/hello", logMark);
            Assert.Equal(["pulls:octo/hello 200"], ApiSince(served, requests).Select(request => $"{request.Route} {request.Status}"));
            Assert.Equal(PullRequestsState.Loaded, hello.HostingState.PullRequests);
            Assert.Equal(["v Pull requests 4", .. P.HelloRows.Select(row => row.Name)], Section(app));

            // P1 step 10: ops, without a GitHub remote.
            requests = served.GitHub.Requests.Count;
            app.Click(app.TabStrip().Tabs[1].Bounds.Center);
            var opsTab = await app.WaitForRepositoryAsync();
            await app.WaitForWatchersQuietAsync();
            Assert.Equal(PullRequestsState.NotGitHub, opsTab.HostingState.PullRequests);
            Assert.Empty(opsTab.HostingState.Remotes);
            Assert.Empty(opsTab.Hosting.Bindings);
            Assert.Equal(["> Pull requests 0"], Section(app));
            Assert.DoesNotContain(opsTab.LeftPanel.Rows, row => row is PullRequestRow or PullRequestMessageRow);
            Assert.Equal(AccountState.SignedIn, app.Session.Services.Hosting.Account?.State);

            // P1 step 11: no menu; the click leaves it closed.
            var noMenu = app.LogMark();
            app.RightClick(await HeaderAsync(app));
            await AssertNoMenuAsync(app, noMenu);
            Shot(app, P.P01a);
            app.Click(await HeaderAsync(app));
            Assert.Equal(["> Pull requests 0"], Section(app));

            // P1 step 12: ops' current branch's menu has no pull-request item.
            var current = opsTab.Refs!.Head.BranchName!;
            app.RightClick(await app.RefRowAsync("Local branches", current));
            var lines = app.Menu().Lines();
            Assert.DoesNotContain(P.CreatePullRequest, lines);
            Assert.DoesNotContain(lines, line => line.StartsWith("Open pull request #", StringComparison.Ordinal));
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            Assert.Empty(served.GitHub.Requests.Skip(requests));
        }
        finally
        {
            End(app, data, served, ops.Path);
        }
    }

    [AvaloniaFact]
    public async Task Check_P7_P5_more_than_300_and_none()
    {
        // P7 step 1.
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a p7");
        var port = served.GitHub.Port;
        var app = await OpenAsync(data, served);
        try
        {
            var hello = app.Session.Shell.ActiveTab.Repository!;
            await WaitForListedAsync(app, "hello", 4, "octo/hello", 0);

            // P7 step 2: 297 more, a refresh reads three pages.
            for (var n = P.FirstAdded; n <= P.LastAdded; n++)
            {
                served.GitHub.AddPullRequest("octo", "hello", P.AddedTitle(n), "octo", P.AddedHead, "main");
            }

            var requests = served.GitHub.Requests.Count;
            var logMark = app.LogMark();
            app.RightClick(await HeaderAsync(app));
            app.ClickMenuItem(P.RefreshPullRequests);
            await WaitForListedAsync(app, "hello", 300, "octo/hello", logMark);
            var pages = ApiSince(served, requests);
            Assert.Equal(3, pages.Count);
            Assert.All(pages, request => Assert.Equal(("pulls:octo/hello", 200, true), (request.Route, request.Status, request.TokenPresent)));
            Assert.Equal("state=open&per_page=100", pages[0].Query);
            Assert.Contains("page=2", pages[1].Query, StringComparison.Ordinal);
            Assert.Contains("page=3", pages[2].Query, StringComparison.Ordinal);
            Assert.True(hello.HostingState.List!.MoreExist);
            Assert.Equal(300, PullRequestRows(app).Count);
            Assert.Equal([302, 6, 5, 3, 2], PullRequestRows(app).Select(row => row.Number).Where((_, i) => i is 0 or 296 or 297 or 298 or 299));
            Assert.Equal(6, hello.HostingState.ByRef["refs/remotes/origin/topic/no-pr"].Number);
            Assert.False(hello.HostingState.ByRef.ContainsKey("refs/heads/feature/login"));
            Assert.Equal("v Pull requests 300", Section(app)[0]);
            Assert.Equal("[More] " + P.MorePullRequests, Section(app)[^1]);

            // P7 steps 3 to 5: the end of the list, the "more" row's tooltip and click.
            var list = app.Find<ItemsControl>("RefList");
            list.ScrollIntoView(app.Session.Shell.ActiveTab.LeftPanel.Rows.Count - 1);
            app.Settle();
            await app.WaitForAsync(() => list.GetLogicalDescendants().OfType<Control>().Any(c => Avalonia.Automation.AutomationProperties.GetName(c) == "Stashes"), "the list's end");
            Shot(app, P.P07a);
            var more = await MessageAsync(app);
            Assert.Equal(P.OpenPullRequestsInBrowser, await app.RestPointerOnAsync(more));
            requests = served.GitHub.Requests.Count;
            app.Click(more);
            await app.WaitForAsync(() => OpenedUrls(app).Count == 1, "the launcher's address");
            Assert.Equal([P.PullsUrl(port)], OpenedUrls(app));
            Assert.Empty(ApiSince(served, requests));

            // P5 step 6: every open pull request closed on the fake; the refresh lists none.
            foreach (var pull in served.GitHub.PullRequests("octo/hello").Where(pull => pull.State == "open"))
            {
                pull.State = "closed";
            }

            requests = served.GitHub.Requests.Count;
            logMark = app.LogMark();
            app.RightClick(await HeaderAsync(app));
            app.ClickMenuItem(P.RefreshPullRequests);
            await WaitForListedAsync(app, "hello", 0, "octo/hello", logMark);
            list.ScrollIntoView(0);
            app.Settle();
            Shot(app, P.P05a);
            Assert.Equal(["pulls:octo/hello 200"], ApiSince(served, requests).Select(request => $"{request.Route} {request.Status}"));
            Assert.Equal(["v Pull requests 0", "[None] " + P.NoOpenPullRequests], Section(app));
            Assert.Empty(hello.HostingState.ByRef);
            Assert.DoesNotContain(hello.LeftPanel.Rows.OfType<RefItemRow>(), row => row.ShowsPullRequest);
        }
        finally
        {
            End(app, data, served);
        }
    }
}
