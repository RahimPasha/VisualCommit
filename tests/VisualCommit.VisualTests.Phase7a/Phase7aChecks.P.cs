using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
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
            ShotP(app, P.P02a);
            Assert.Null(app.Session.Services.Hosting.Account);
            Assert.Empty(served.GitHub.Requests);
            Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.Contains("--get-regexp ^url", StringComparison.Ordinal));
            Assert.Equal(("octo/hello", 0), (hello.HostingState.Repository?.FullName, hello.HostingState.ByRef.Count));
            Assert.Null(hello.HostingState.List);
            Assert.Empty(hello.Hosting.Bindings);
            Assert.Equal(["v Pull requests 0", "[SignIn] " + P.SignInToSeePullRequests], Section(app));
            Assert.DoesNotContain(hello.LeftPanel.Rows.OfType<RefItemRow>(), row => row.ShowsPullRequest);

            // P11 steps 2 and 3: the section's menu signed out; open in browser asks GitHub nothing.
            var header = await HeaderAsync(app);
            var pointer = app.BoundsOf(header).Center;
            app.RightClick(header);
            var signedOut = app.Menu();
            AssertMenu(signedOut, P.SectionMenuSignedOut);
            AssertHighlighted(signedOut, P.SignInToGitHubItem);
            AssertMenuShape(app, signedOut);
            AssertMenuHeight(signedOut, P.SectionMenuHeight);
            AssertMenuAtPointer(signedOut, pointer);
            ShotP(app, P.P11a);
            Assert.Equal(["v Pull requests 0", "[SignIn] " + P.SignInToSeePullRequests], Section(app));
            app.ClickMenuItem(P.OpenPullRequestsInBrowser);
            await app.WaitForAsync(() => OpenedUrls(app).Count == 1, "the launcher's address");
            Assert.Equal([P.PullsUrl(port)], OpenedUrls(app));
            Assert.Single(app.Launcher.Requests);
            Assert.Empty(served.GitHub.Requests);
            Assert.Null(app.Session.Services.Hosting.Account);
            Assert.Equal(["v Pull requests 0", "[SignIn] " + P.SignInToSeePullRequests], Section(app));

            // P2 step 4: the sign-in row opens "Add GitHub account" over hello's window, no settings screen.
            var requests = served.GitHub.Requests.Count;
            var stored = app.Tokens.Calls.Count;
            var logMark = app.LogMark();
            app.Click(await MessageAsync(app));
            var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
            await app.WaitForAsync(() => app.FocusedAutomationId() == "SignInToken", "the focus in the token's box");
            Assert.Equal([dialog], app.Session.Services.DialogHost.Open.Cast<DialogViewModel>());
            Assert.Equal(Phase7aExpectations.PartA.DialogTitle, AutomationProperties.GetName(app.SignInDialogView()));
            Assert.Equal(string.Empty, dialog.Token);
            Assert.Equal(["probe"], StoreCallsSince(app, stored));
            Assert.Equal(requests, served.GitHub.Requests.Count);
            Assert.Equal(["v Pull requests 0", "[SignIn] " + P.SignInToSeePullRequests], Section(app));

            // P4 step 5: GitHub refuses the list with 403.
            served.GitHub.FailNext("pulls:octo/hello", FakeGitHubFault.Forbidden);
            app.TypeToken(served.OctoToken);
            app.PressKey(Key.Enter);
            await app.WaitForAsync(() => dialog.IsClosed, "the dialog to close");
            await WaitForStateAsync(app, hello, PullRequestsState.Failed, PullRequestMessageKind.Failed);
            await app.WaitForAsync(() => TokenStateOf(data) == "keychain", "accounts.json's tokenState keychain");
            await AssertNoDialogAsync(app);
            ShotP(app, P.P04a);
            var api = ApiSince(served, requests);
            Assert.Equal(["user 200", "repo:octo/hello 200", "pulls:octo/hello 403"], api.Select(request => $"{request.Route} {request.Status}"));
            Assert.All(api, request => Assert.Equal(("GET", true), (request.Method, request.TokenPresent)));
            Assert.Equal("octo", api[0].Login);
            AssertPullsRequest(api[2], 403);
            Assert.Equal(["probe", "save " + P.OctoKey(port)], StoreCallsSince(app, stored));
            var account = app.Session.Services.Hosting.Account;
            Assert.Equal((AccountState.SignedIn, TokenPlace.Keychain, AccountNoteKind.None), (account?.State, account?.Place, account?.NoteKind));
            Assert.Equal(
                (PullRequestsState.Failed, HostingFailureKind.Forbidden, P.ForbiddenMessage, P.ForbiddenDetail),
                (hello.HostingState.PullRequests, hello.HostingState.Failure?.Kind, hello.HostingState.Failure?.Message, hello.HostingState.Failure?.Detail));
            Assert.Null(hello.HostingState.List);
            Assert.Empty(hello.HostingState.ByRef);
            var binding = Assert.Single(hello.Hosting.Bindings);
            Assert.Equal((P.HelloGitUrl(port), Phase7aExpectations.PartA.Host(port), "octo"), (binding.Url, binding.Account.Host, binding.Account.Login));
            Assert.Equal(["v Pull requests 0", "[Failed] " + P.ForbiddenText], Section(app));
            var lines = LogSince(app, logMark);
            var id = RequestIdOf(lines, "GitHub: GET /api/v3/repos/octo/hello/pulls failed:");
            AssertWarningOnce(lines, P.ListRefusedWarning(id));
            AssertWarningOnce(lines, P.ListFailedWarning(id));
            Assert.DoesNotContain(lines, line => line.Message.StartsWith("hello: listed", StringComparison.Ordinal));

            // P4 step 6: the failed row's tooltip.
            var text = ((Control)await MessageAsync(app)).GetLogicalDescendants().OfType<TextBlock>().Single(block => block.Name == "PullRequestMessageText");
            Assert.Equal(P.ForbiddenText + "\n" + P.ForbiddenDetail, await app.RestPointerOnAsync(text));
            ShotP(app, P.P04b, keepPointer: true);

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
                ShotP(app, P.P03a);
                Assert.Empty(ApiSince(served, requests));
                Assert.Equal(["v Pull requests 0", "[Loading] " + P.LoadingPullRequests], Section(app));
                Assert.Null(hello.HostingState.List);
                Assert.Null(hello.HostingState.Failure);
                Assert.DoesNotContain(hello.LeftPanel.Rows.OfType<RefItemRow>(), row => row.ShowsPullRequest);

                // P11 step 8: refresh disabled while loading.
                app.RightClick(await HeaderAsync(app));
                var loading = app.Menu();
                AssertMenu(loading, P.SectionMenuLoading);
                AssertHighlighted(loading, P.OpenPullRequestsInBrowser);
                Assert.Equal(P.PullRequestsLoading, await app.RestPointerOnMenuItemAsync(P.RefreshPullRequests));
                ShotP(app, P.P11b, keepPointer: true);
                app.PressKey(Key.Escape);
            }
            finally
            {
                hold.Dispose();
            }

            // P3 step 9: the answer fills the section.
            await WaitForListedAsync(app, "hello", 4, "octo/hello", logMark);
            AssertPullsRequest(Assert.Single(ApiSince(served, requests)), 200);
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
            var rows = opsTab.LeftPanel.Rows.ToList();
            Assert.IsType<RefSectionRow>(rows[rows.FindIndex(row => row is RefSectionRow { Section: RefSection.PullRequests }) + 1]);
            Assert.Equal("Tags", rows[rows.FindIndex(row => row is RefSectionRow { Section: RefSection.PullRequests }) + 1].Text);
            Assert.DoesNotContain(opsTab.LeftPanel.Rows, row => row is PullRequestRow or PullRequestMessageRow);
            AssertNoPullRequestControls(app);
            Assert.Equal(AccountState.SignedIn, app.Session.Services.Hosting.Account?.State);
            Assert.Empty(ApiSince(served, requests));

            // P1 step 11: no menu; the click leaves it closed.
            var noMenu = app.LogMark();
            app.RightClick(await HeaderAsync(app));
            await AssertNoMenuAsync(app, noMenu);
            ShotP(app, P.P01a);
            app.Click(await HeaderAsync(app));
            Assert.Equal(["> Pull requests 0"], Section(app));
            Assert.Empty(ApiSince(served, requests));

            // P1 step 12: ops' current branch's menu has no pull-request item.
            requests = served.GitHub.Requests.Count;
            var current = opsTab.Refs!.Head.BranchName!;
            app.RightClick(await app.RefRowAsync("Local branches", current));
            var menu = app.Menu();
            Assert.Equal(current, menu.Lines()[0]);
            Assert.DoesNotContain(P.CreatePullRequest, menu.Lines());
            Assert.DoesNotContain(menu.Lines(), line => line.StartsWith("Open pull request #", StringComparison.Ordinal));
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            Assert.Empty(ApiSince(served, requests));
        }
        finally
        {
            End(app, data, served, [P.PullsUrl(port)], ops.Path);
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
            Assert.All(pages, request => Assert.Equal(("GET", "pulls:octo/hello", 200, true), (request.Method, request.Route, request.Status, request.TokenPresent)));
            AssertPullsRequest(pages[0], 200);
            for (var page = 2; page <= 3; page++)
            {
                var parameters = pages[page - 1].Query.Split('&');
                Assert.Equal(["page=" + page, "per_page=100", "state=open"], parameters.Order(StringComparer.Ordinal));
            }

            Assert.Empty(served.GitHub.Errors);
            Assert.Equal(PullRequestsState.Loaded, hello.HostingState.PullRequests);
            Assert.True(hello.HostingState.List!.MoreExist);
            Assert.Equal([.. Enumerable.Range(P.FirstAdded, P.LastAdded - P.FirstAdded + 1).Reverse(), 5, 3, 2], hello.HostingState.List.PullRequests.Select(pull => pull.Number));
            Assert.Equal(
                new Dictionary<string, int>
                {
                    ["refs/remotes/origin/topic/no-pr"] = 6,
                    ["refs/remotes/origin/docs/readme"] = 2,
                    [$"refs/remotes/origin/{P.LongBranch}"] = 3,
                }.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                hello.HostingState.ByRef.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Number)).OrderBy(pair => pair.Key, StringComparer.Ordinal));
            Assert.Equal(300, PullRequestRows(app).Count);
            var section = Section(app);
            Assert.Equal(302, section.Count);
            Assert.Equal("v Pull requests 300", section[0]);
            Assert.Equal("[More] " + P.MorePullRequests, section[^1]);

            // P7 steps 3 to 5: the end of the list, the "more" row's tooltip and click.
            var list = app.Find<ItemsControl>("RefList");
            list.ScrollIntoView(app.Session.Shell.ActiveTab.LeftPanel.Rows.Count - 1);
            app.Settle();
            await app.WaitForAsync(() => list.GetLogicalDescendants().OfType<Control>().Any(c => AutomationProperties.GetName(c) == "Stashes"), "the list's end");
            ShotP(app, P.P07a);
            var (extent, viewport) = PanelScroll(app);
            Assert.True(extent > viewport + 1, $"The panel's list shows no scroll bar: extent {extent}, viewport {viewport}.");
            Assert.Equal(
                ["  #7 More work 7", "  #6 More work 6", "  #5 Fix a typo in the greeting", "  #3 Shorten long labels", "  #2 Update the readme", "  [More] " + P.MorePullRequests, "Tags 0", "Stashes 0"],
                Panel(app)[^8..]);
            var lastControls = PullRequestControls(app);
            Assert.Equal(P.HelloRows[2].Name, AutomationProperties.GetName(lastControls[^1]));
            Assert.True(Part(lastControls[^1], "PullRequestDraft").IsEffectivelyVisible, "#2 shows no Draft.");
            Assert.Equal("octo", Part(lastControls[^1], "PullRequestAuthor").Text);
            var more = await MessageAsync(app);
            Assert.Equal(P.OpenPullRequestsInBrowser, await app.RestPointerOnAsync(more));
            requests = served.GitHub.Requests.Count;
            var selection = hello.Selection;
            app.Click(more);
            await app.WaitForAsync(() => OpenedUrls(app).Count == 1, "the launcher's address");
            Assert.Equal([P.PullsUrl(port)], OpenedUrls(app));
            Assert.Single(app.Launcher.Requests);
            Assert.Empty(ApiSince(served, requests));
            Assert.Equal(selection, hello.Selection);
            Assert.DoesNotContain(hello.LeftPanel.Rows, row => row is RefItemRow { IsSelected: true } or PullRequestRow { IsSelected: true });

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
            ShotP(app, P.P05a);
            AssertPullsRequest(Assert.Single(ApiSince(served, requests)), 200);
            Assert.Equal(PullRequestsState.Loaded, hello.HostingState.PullRequests);
            Assert.Empty(hello.HostingState.List!.PullRequests);
            Assert.False(hello.HostingState.List.MoreExist);
            Assert.Empty(hello.HostingState.ByRef);
            Assert.Equal(["v Pull requests 0", "[None] " + P.NoOpenPullRequests], Section(app));
            Assert.All(hello.LeftPanel.Rows.OfType<RefItemRow>(), row => Assert.Equal(string.Empty, row.PullRequestText));
            (extent, viewport) = PanelScroll(app);
            Assert.True(extent <= viewport + 0.5, $"The panel's list shows a scroll bar: extent {extent}, viewport {viewport}.");
        }
        finally
        {
            End(app, data, served, [P.PullsUrl(port)]);
        }
    }

    /// <summary>P1: no <c>PullRequestItem</c> or <c>PullRequestMessage</c> control in the panel and no visible <c>RefPullRequest</c>.</summary>
    private static void AssertNoPullRequestControls(ShellDriver app)
    {
        app.Settle();
        var controls = app.Find<ItemsControl>("RefList").GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible).ToList();
        Assert.DoesNotContain(controls, control => AutomationProperties.GetAutomationId(control) is "PullRequestItem" or "PullRequestMessage" or "RefPullRequest");
    }
}
