using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;
using P = VisualCommit.VisualTests.Phase7b.Phase7bExpectations.PartP;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// Part P, requests in the left panel and the graph (docs/test-reports/phase-7b.md, part P, checks
/// P1 to P14; design 12.8): one method per app start, as the report's methods table gives them. This
/// file holds methods 1 and 2 (each provider's rows, badge, menus, copy link, open in browser, branch
/// rows; GitLab's filter), 3 and 4 (signed out, failed, loading; D356's row) and 5 (ruling 5's listed
/// remote, more than 300, none); <c>Phase7bChecks.P.Checkout.cs</c> methods 6 and 7.
/// </summary>
public sealed partial class Phase7bChecksP : Phase7bChecks
{
    private const HostingProvider GitLab = HostingProvider.GitLab;
    private const HostingProvider Azure = HostingProvider.AzureDevOps;

    [AvaloniaFact]
    public async Task Check_P1_P10_P12_P13_P14_P7_gitlab_rows_badge_menus_and_filter()
    {
        await using var served = await ServeAsync();
        await PrepareRequestsAsync(served.MobileApp, GitLab);
        using var data = new TempDirectory("phase7b p1 gitlab");
        var app = await OpenAsync(data, served, WebApp, signIn: []);
        try
        {
            var webApp = app.Session.Shell.ActiveTab.Repository!;
            var ended = EndedOperations(app);

            // P1 step 1: signed in through Settings, Accounts; the list.
            var mark = Mark(served);
            var stored = app.Tokens.Calls.Count;
            var logMark = app.LogMark();
            await SignInThroughSettingsAsync(app, GitLab, served.GitLabToken);
            await WaitForListedAsync(app, WebApp, 3, P.GitLabListed, logMark);
            await AssertNoDialogAsync(app);
            ShotP(app, P.P01a);
            AssertGitLabSignInList(GitLabSince(served, mark));
            Assert.Empty(AzureSince(served, mark));
            Assert.Empty(GitHubSince(served, mark));
            Assert.Equal(["probe", "save " + WithPorts(P.GitLabKey, served)], StoreCallsSince(app, stored));
            Assert.Equal(AccountState.SignedIn, app.Session.Services.Hosting.AccountOf(GitLab)?.State);
            AssertLoaded(webApp, served, GitLab);
            AssertRows(app, GitLab, P.GitLabRows);
            Assert.Equal(["feature", "local-work", "main", "topic"], LocalBranches(app).Order(StringComparer.Ordinal));
            var topic = LocalBranchRows(app).Single(row => row.Text == "topic");
            Assert.Equal((true, false), (topic.IsContainer, topic.IsOpen));
            AssertBadges(app, P.GitLabRequestLabel, P.GitLabRequestBadge, P.GitLabDraftLabel, P.GitLabDraftBadge);
            Assert.DoesNotContain(app.LogLinesSince(0), line => line.Contains("showing GitHub avatars", StringComparison.Ordinal));
            var afterStep1 = Mark(served);

            // P10 step 2: the section's menu.
            await SectionMenuAsync(app, P.SectionMenu(GitLab), P.GitLabRefresh, P.SectionMenuHeight);
            ShotP(app, P.P10a, keepPointer: true);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            AssertRows(app, GitLab, P.GitLabRows);

            // P10 step 3: the requests' menus.
            var selection = webApp.Selection;
            foreach (var row in P.GitLabRows)
            {
                app.RightClick(await RequestAsync(app, row.Number));
                AssertWholeMenu(app, P.RequestMenu(row), P.OpenInBrowser, P.RequestMenuHeight);
                if (row.Number == 23)
                {
                    ShotP(app, P.P10b);
                }

                Assert.False(PullRequestRows(app).Single(pull => pull.Number == row.Number).IsSelected);
                Assert.Equal(selection, webApp.Selection);
                if (row.Number != 21)
                {
                    app.PressKey(Key.Escape);
                }
            }

            // P12 step 4: Copy link in !21's menu.
            app.ClickMenuItem(P.CopyLink);
            await app.WaitForAsync(() => app.Clipboard.Texts.Count > 0, "the clipboard");
            await AssertNoMenuAsync(app, null);
            Assert.Equal(WithPorts(P.GitLabRequestUrl(21), served), app.Clipboard.Texts[^1]);
            Assert.Empty(app.Launcher.Requests);
            Assert.Empty(ended);
            Assert.Equal(selection, webApp.Selection);
            Assert.DoesNotContain(webApp.LeftPanel.Rows, row => row is RefItemRow { IsSelected: true } or PullRequestRow { IsSelected: true });

            // P13 step 5: open in browser, the request's and the section's.
            await OpenRequestAndPageAsync(app, served, GitLab, 21);

            // P1 step 6: the graph's tooltips.
            Assert.Equal(P.GitLabRequestRowTip, await app.RestPointerAtAsync(LabelBounds(app, P.RequestRow, P.GitLabRequestLabel).Center));
            ShotP(app, P.P01b, keepPointer: true);
            Assert.Equal(P.GitLabDraftRowTip, await app.RestPointerAtAsync(LabelBounds(app, P.DraftRow, P.GitLabDraftLabel).Center));
            AssertNoRequests(served, afterStep1);

            // P14 step 7: the branch rows' !21 and the branch's menu.
            app.Click(await HeaderAsync(app));
            Assert.Equal([$"> {P.GitLabTitle} 3"], Section(app));
            await OpenBranchFoldersAsync(app);
            var search = await app.RefRowAsync("Local branches", "feature", "search");
            var number = Part(search, "RefPullRequest");
            Assert.Equal("!21", number.Text);
            Assert.Contains("secondary", number.Classes);
            Assert.True(app.BoundsOf(number).Left >= app.BoundsOf(Part(search, "RefName")).Right - 0.5, "The local search row's !21 is not right of its name.");
            Assert.Equal(P.OfBranch(GitLab, 21, "Add the search box"), await app.RestPointerOnAsync(number));
            ShotP(app, P.P14a, keepPointer: true);
            await AssertBranchNumbersAsync(app, ("search", 21), ("readme", 22), [["Remotes", "origin", "topic"], ["Remotes", "upstream"], ["Remotes", "upstream", "main"]]);
            Assert.True(((RefItemRow)app.RefRowModel("Local branches", "main")).IsHead, "main is not in the accent.");
            await BranchMenuOpensRequestAsync(app, served, GitLab, "search", 21, launches: 3);
            app.Click(await HeaderAsync(app));
            AssertRows(app, GitLab, P.GitLabRows);

            // P7 step 8: the filter on !21, 21, #21 and ada.
            var before = Panel(app);
            var filter = app.Find<TextBox>("FilterBox");
            app.Click(filter);
            app.Type("!21");
            string[] only21 = ["Local branches 0", "Remotes 0", $"{P.GitLabTitle} 1", "  !21 Add the search box", "Tags 0", "Stashes 0"];
            Assert.Equal(only21, Panel(app));
            ShotP(app, P.P07a);
            app.PressKey(Key.A, RawInputModifiers.Control);
            app.Type("21");
            Assert.Equal(only21, Panel(app));
            app.PressKey(Key.A, RawInputModifiers.Control);
            app.Type("#21");
            Assert.Equal(["Local branches 0", "Remotes 0", $"{P.GitLabTitle} 0", "Tags 0", "Stashes 0"], Panel(app));
            app.PressKey(Key.A, RawInputModifiers.Control);
            app.Type("ada");
            Assert.Equal(["Local branches 0", "Remotes 0", $"{P.GitLabTitle} 2", "  !23 Fix a typo in the footer", "  !21 Add the search box", "Tags 0", "Stashes 0"], Panel(app));
            app.PressKey(Key.A, RawInputModifiers.Control);
            app.PressKey(Key.Back);
            Assert.Equal(string.Empty, filter.Text ?? string.Empty);
            Assert.Equal(P.FilterPlaceholder, filter.PlaceholderText);
            Assert.Equal(before, Panel(app));
            AssertNoRequests(served, afterStep1);
            Assert.Empty(ended);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, [GitLabToken], [P.GitLabRequestUrl(21), P.GitLabRequestsPage, P.GitLabRequestUrl(21)], [P.GitLabRequestUrl(21)]);
    }

    [AvaloniaFact]
    public async Task Check_P1_P10_P12_P13_P14_azure_rows_badge_and_menus()
    {
        await using var served = await ServeAsync();
        await PrepareRequestsAsync(served.MobileApp, Azure);
        using var data = new TempDirectory("phase7b p1 azure");
        var app = await OpenAsync(data, served, signIn: []);
        try
        {
            var mobileApp = app.Session.Shell.ActiveTab.Repository!;
            var ended = EndedOperations(app);

            // P1 step 1: signed in through Settings, Accounts, the organization prefilled.
            var mark = Mark(served);
            var stored = app.Tokens.Calls.Count;
            var logMark = app.LogMark();
            await SignInThroughSettingsAsync(app, Azure, served.AzureDevOpsToken);
            await WaitForListedAsync(app, MobileApp, 3, P.AzureDevOpsListed, logMark);
            await AssertNoDialogAsync(app);
            ShotP(app, P.P01c);
            AssertAzureSignInList(served, AzureSince(served, mark));
            Assert.Empty(GitLabSince(served, mark));
            Assert.Empty(GitHubSince(served, mark));
            Assert.Equal(["probe", "save " + WithPorts(P.AzureDevOpsKey, served)], StoreCallsSince(app, stored));
            AssertLoaded(mobileApp, served, Azure);
            AssertRows(app, Azure, P.AzureDevOpsRows);
            AssertBadges(app, P.AzureDevOpsRequestLabel, P.AzureDevOpsRequestBadge, P.AzureDevOpsDraftLabel, P.AzureDevOpsDraftBadge);
            var afterStep1 = Mark(served);

            // P10 step 2: the section's menu.
            await SectionMenuAsync(app, P.SectionMenu(Azure), P.AzureDevOpsRefresh, P.SectionMenuHeight);
            ShotP(app, P.P10c, keepPointer: true);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);

            // P10 step 3: !13's (its checkout disabled with its reason), !12's and !11's menus.
            var selection = mobileApp.Selection;
            foreach (var row in P.AzureDevOpsRows)
            {
                app.RightClick(await RequestAsync(app, row.Number));
                AssertWholeMenu(app, P.RequestMenu(row), P.OpenInBrowser, P.RequestMenuHeight);
                Assert.Equal(selection, mobileApp.Selection);
                if (row.Number != 11)
                {
                    app.PressKey(Key.Escape);
                }
            }

            // P12 step 4: Copy link in !11's menu.
            app.ClickMenuItem(P.CopyLink);
            await app.WaitForAsync(() => app.Clipboard.Texts.Count > 0, "the clipboard");
            await AssertNoMenuAsync(app, null);
            Assert.Equal(WithPorts(P.AzureDevOpsRequestUrl(11), served), app.Clipboard.Texts[^1]);
            Assert.Empty(app.Launcher.Requests);
            Assert.Empty(ended);
            Assert.Equal(selection, mobileApp.Selection);
            Assert.DoesNotContain(mobileApp.LeftPanel.Rows, row => row is RefItemRow { IsSelected: true } or PullRequestRow { IsSelected: true });

            // P13 step 5.
            await OpenRequestAndPageAsync(app, served, Azure, 11);

            // P1 step 6: the graph's tooltips.
            Assert.Equal(P.AzureDevOpsRequestRowTip, await app.RestPointerAtAsync(LabelBounds(app, P.RequestRow, P.AzureDevOpsRequestLabel).Center));
            Assert.Equal(P.AzureDevOpsDraftRowTip, await app.RestPointerAtAsync(LabelBounds(app, P.DraftRow, P.AzureDevOpsDraftLabel).Center));
            AssertNoRequests(served, afterStep1);

            // P14 step 7.
            await OpenBranchFoldersAsync(app);
            var signIn = await app.RefRowAsync("Local branches", "feature", "sign-in");
            Assert.Equal("!11", Part(signIn, "RefPullRequest").Text);
            Assert.Equal(P.OfBranch(Azure, 11, "Add the sign-in page"), await app.RestPointerOnAsync(Part(signIn, "RefPullRequest")));
            await AssertBranchNumbersAsync(app, ("sign-in", 11), ("guide", 12), null);
            await BranchMenuOpensRequestAsync(app, served, Azure, "sign-in", 11, launches: 3);
            Assert.Empty(ended);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, [AzureDevOpsToken], [P.AzureDevOpsRequestUrl(11), P.AzureDevOpsRequestsPage, P.AzureDevOpsRequestUrl(11)], [P.AzureDevOpsRequestUrl(11)]);
    }

    [AvaloniaFact]
    public async Task Check_P2_P10_P13_P4_P3_gitlab_signed_out_failed_and_loading()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b p2 gitlab");
        var app = await OpenAsync(data, served, WebApp, signIn: []);
        try
        {
            // P2 step 1: signed out.
            var webApp = app.Session.Shell.ActiveTab.Repository!;
            await WaitForStateAsync(app, webApp, PullRequestsState.SignedOut, PullRequestMessageKind.SignIn);
            ShotP(app, P.P02a);
            AssertSignedOut(app, served, webApp, GitLab);

            // P10 step 2 and P13 step 3: the signed-out menu; open in browser asks nothing.
            var mark = Mark(served);
            await SectionMenuAsync(app, P.SectionMenuSignedOut(GitLab), P.GitLabSignInItem, P.SectionMenuHeight);
            Assert.Equal([$"v {P.GitLabTitle} 0", "[SignIn] " + P.GitLabSignInToSee], Section(app));
            app.ClickMenuItem(P.GitLabOpenInBrowser);
            await app.WaitForAsync(() => OpenedUrls(app).Count == 1, "the launcher's address");
            Assert.Equal([WithPorts(P.GitLabRequestsPage, served)], OpenedUrls(app));
            AssertNoRequests(served, mark);
            Assert.Null(app.Session.Services.Hosting.AccountOf(GitLab));

            // P2 step 4: the row opens GitLab's dialog over the window.
            mark = Mark(served);
            var stored = app.Tokens.Calls.Count;
            app.Click(await MessageAsync(app));
            var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
            await app.WaitForAsync(() => app.FocusedAutomationId() == "SignInToken", "the focus in the token's box");
            Assert.Equal([dialog], app.Session.Services.DialogHost.Open.Cast<DialogViewModel>());
            Assert.Equal((GitLab, "Add GitLab account", string.Empty), (dialog.Provider, dialog.Title, dialog.Token));
            Assert.Equal(["probe"], StoreCallsSince(app, stored));
            AssertNoRequests(served, mark);
            Assert.Equal([$"v {P.GitLabTitle} 0", "[SignIn] " + P.GitLabSignInToSee], Section(app));

            // P4 step 5: GitLab refuses the list with 403 insufficient_granular_scope.
            var logMark = app.LogMark();
            served.GitLab.FailNext(P.GitLabListRoute, FakeGitLabFault.InsufficientGranular);
            app.TypeToken(served.GitLabToken);
            app.PressKey(Key.Enter);
            await app.WaitForAsync(() => dialog.IsClosed, "the dialog to close");
            await WaitForStateAsync(app, webApp, PullRequestsState.Failed, PullRequestMessageKind.Failed);
            await app.WaitForAsync(() => TokenStateOf(data) == "keychain", "accounts.json's tokenState keychain");
            await AssertNoDialogAsync(app);
            ShotP(app, P.P04a);
            var gitLab = GitLabSince(served, mark);
            Assert.Equal([P.GitLabUserRoute + " 200", P.GitLabProjectRoute + " 200", P.GitLabListRoute + " 403"], gitLab.Select(request => $"{request.Route} {request.Status}"));
            Assert.All(gitLab, request => Assert.Equal(("GET", FakeAuthorizationForm.Bearer), (request.Method, request.Authorization)));
            Assert.Equal(AdaLogin, gitLab[0].Login);
            AssertRequest(gitLab[2], P.GitLabListPath, P.GitLabListRoute, P.GitLabListQuery, FakeAuthorizationForm.Bearer, 403, null);
            Assert.Empty(AzureSince(served, mark));
            Assert.Empty(GitHubSince(served, mark));
            Assert.Equal(["probe", "save " + WithPorts(P.GitLabKey, served)], StoreCallsSince(app, stored));
            var account = app.Session.Services.Hosting.AccountOf(GitLab);
            Assert.Equal((AccountState.SignedIn, TokenPlace.Keychain, AccountNoteKind.None), (account?.State, account?.Place, account?.NoteKind));
            var failure = webApp.HostingState.Failure;
            Assert.Equal(
                (HostingFailureKind.Forbidden, GitLab, P.GitLabForbiddenMessage, P.GitLabForbiddenDetail),
                (failure?.Kind, failure?.Provider, failure?.Message, failure?.Detail));
            Assert.Null(webApp.HostingState.List);
            Assert.Empty(webApp.HostingState.ByRef);
            Assert.Contains(webApp.Hosting.Bindings, binding => binding.Url == WithPorts(WebAppUrl, served) && binding.Account.Provider == GitLab);
            Assert.All(webApp.Hosting.Bindings, binding => Assert.Equal(GitLab, binding.Account.Provider));
            Assert.Equal([$"v {P.GitLabTitle} 0", "[Failed] " + P.GitLabForbiddenText], Section(app));
            var lines = LogSince(app, logMark);
            var id = RequestIdOf(lines, $"GitLab: GET {P.GitLabListPath} failed:");

            // {id} the refused answer's: the fake's X-Request-Id form (part B's), never a counter of the app's.
            Assert.Matches(PartB.GitLabRequestIdPattern, id);
            AssertWarningOnce(lines, P.GitLabRefusedWarning(id));
            AssertWarningOnce(lines, P.GitLabListFailedWarning(id));
            Assert.DoesNotContain(lines, line => line.Message.StartsWith("web-app: listed", StringComparison.Ordinal));

            // P4 step 6: the failed row's tooltip.
            var text = Part(await MessageAsync(app), "PullRequestMessageText");
            Assert.Equal(P.GitLabForbiddenText + "\n" + P.GitLabForbiddenDetail, await app.RestPointerOnAsync(text));
            ShotP(app, P.P04b, keepPointer: true);

            // P3 step 7: a refresh held at the fake shows Loading.
            mark = Mark(served);
            logMark = app.LogMark();
            var hold = served.GitLab.HoldNext(P.GitLabListRoute);
            try
            {
                app.RightClick(await HeaderAsync(app));
                app.ClickMenuItem(P.GitLabRefresh);
                await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
                await WaitForStateAsync(app, webApp, PullRequestsState.Loading, PullRequestMessageKind.Loading);
                ShotP(app, P.P03a);
                AssertNoRequests(served, mark);
                Assert.Equal([$"v {P.GitLabTitle} 0", "[Loading] " + P.GitLabLoading], Section(app));
                Assert.Null(webApp.HostingState.List);
                Assert.Null(webApp.HostingState.Failure);
                Assert.DoesNotContain(webApp.LeftPanel.Rows.OfType<RefItemRow>(), row => row.ShowsPullRequest);

                // P10 step 8: the refresh disabled while loading.
                app.RightClick(await HeaderAsync(app));
                var loading = app.Menu();
                AssertMenu(loading, P.SectionMenuLoading(GitLab));
                AssertHighlighted(loading, P.GitLabOpenInBrowser);
                Assert.Equal(P.GitLabLoadingReason, await app.RestPointerOnMenuItemAsync(P.GitLabRefresh));
                app.PressKey(Key.Escape);
                await AssertNoMenuAsync(app, null);
            }
            finally
            {
                hold.Dispose();
            }

            // P3 step 9: the answer fills the section.
            await WaitForListedAsync(app, WebApp, 3, P.GitLabListed, logMark);
            var refreshed = GitLabSince(served, mark);
            Assert.Equal([P.GitLabListRoute + " 200", P.GitLabForkRoute + " 200"], refreshed.Select(request => $"{request.Route} {request.Status}"));
            AssertRequest(refreshed[0], P.GitLabListPath, P.GitLabListRoute, P.GitLabListQuery, FakeAuthorizationForm.Bearer, 200, AdaLogin);
            Assert.Equal((FakeAuthorizationForm.Bearer, AdaLogin), (refreshed[1].Authorization, refreshed[1].Login));
            Assert.Equal(PullRequestsState.Loaded, webApp.HostingState.PullRequests);
            Assert.Equal([$"v {P.GitLabTitle} 3", .. P.GitLabRows.Select(row => row.Name)], Section(app));
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, [GitLabToken], [P.GitLabRequestsPage], []);
    }

    [AvaloniaFact]
    public async Task Check_P2_P10_P13_P4_P3_P8_azure_signed_out_failed_loading_and_other_organization()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b p2 azure");
        var app = ShellDriver.StartWithTabs(data.Path, [served.MobileApp.Path, served.Manifest.Path], activeTab: 0, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        try
        {
            // P2 step 1: signed out.
            var mobileApp = await app.WaitForRepositoryAsync();
            await WaitForStateAsync(app, mobileApp, PullRequestsState.SignedOut, PullRequestMessageKind.SignIn);
            app.MoveToNeutral();
            ShotP(app, P.P02b);
            Assert.Equal([MobileApp, Manifest], app.TabStrip().Tabs.Select(tab => tab.Title));
            AssertSignedOut(app, served, mobileApp, Azure);

            // P10 step 2 and P13 step 3.
            var mark = Mark(served);
            await SectionMenuAsync(app, P.SectionMenuSignedOut(Azure), P.AzureDevOpsSignInItem, P.SectionMenuHeight);
            app.ClickMenuItem(P.AzureDevOpsOpenInBrowser);
            await app.WaitForAsync(() => OpenedUrls(app).Count == 1, "the launcher's address");
            Assert.Equal([WithPorts(P.AzureDevOpsRequestsPage, served)], OpenedUrls(app));
            AssertNoRequests(served, mark);
            Assert.Null(app.Session.Services.Hosting.AccountOf(Azure));

            // P2 step 4: Azure DevOps' dialog prefilled with the tab's organization.
            mark = Mark(served);
            var stored = app.Tokens.Calls.Count;
            app.Click(await MessageAsync(app));
            var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
            await app.WaitForAsync(() => app.FocusedAutomationId() == "SignInToken", "the focus in the token's box");
            Assert.Equal((Azure, "Add Azure DevOps account", Organization), (dialog.Provider, dialog.Title, dialog.Organization));
            Assert.Equal(Organization, ((TextBox)app.FindByAutomationId(app.SignInDialogView(), "SignInOrganization")).Text);
            Assert.True(app.FindByAutomationId(app.SignInDialogView(), "SignInTokenLink").IsEffectivelyEnabled);
            Assert.False(app.FindByAutomationId(app.SignInDialogView(), "ConfirmButton").IsEffectivelyEnabled);
            Assert.Equal(["probe"], StoreCallsSince(app, stored));
            AssertNoRequests(served, mark);

            // P4 step 5: Azure DevOps answers the list with 500.
            var logMark = app.LogMark();
            served.AzureDevOps.FailNext(P.AzureDevOpsListRoute, FakeAzureDevOpsFault.ServerError);
            app.TypeToken(served.AzureDevOpsToken);
            app.PressKey(Key.Enter);
            await app.WaitForAsync(() => dialog.IsClosed, "the dialog to close");
            await WaitForStateAsync(app, mobileApp, PullRequestsState.Failed, PullRequestMessageKind.Failed);
            await AssertNoDialogAsync(app);
            ShotP(app, P.P04c);
            var azure = AzureSince(served, mark);
            Assert.Equal(
                [P.AzureDevOpsConnectionRoute + " 200", P.AzureDevOpsAreasRoute + " 200", P.AzureDevOpsRepositoryRoute + " 200", P.AzureDevOpsListRoute + " 500"],
                azure.Select(request => $"{request.Route} {request.Status}"));
            Assert.All(azure, request => Assert.Equal(("GET", FakeAuthorizationForm.BasicEmptyUser), (request.Method, request.Authorization)));
            Assert.Equal(AdaAzureLogin(served), azure[0].Login);
            AssertRequest(azure[3], P.AzureDevOpsListPath, P.AzureDevOpsListRoute, P.AzureDevOpsListQuery, FakeAuthorizationForm.BasicEmptyUser, 500, null);
            Assert.DoesNotContain(served.AzureDevOps.Requests, IsHarborCo);
            Assert.Empty(GitLabSince(served, mark));
            Assert.Empty(GitHubSince(served, mark));
            Assert.Equal(["probe", "save " + WithPorts(P.AzureDevOpsKey, served)], StoreCallsSince(app, stored));
            var account = app.Session.Services.Hosting.AccountOf(Azure);
            Assert.Equal((AccountState.SignedIn, TokenPlace.Keychain), (account?.State, account?.Place));
            var failure = mobileApp.HostingState.Failure!;
            Assert.Equal((HostingFailureKind.ServerError, Azure, (string?)null), (failure.Kind, failure.Provider, failure.Detail));
            Assert.Null(mobileApp.HostingState.List);
            Assert.Empty(mobileApp.HostingState.ByRef);
            // Ada's account's binding for MobileAppUrl in its bound form, git's URL without its user (CredentialBinding).
            var bound = WithPorts(MobileAppUrl, served).Replace("//" + Organization + "@", "//", StringComparison.Ordinal);
            Assert.Contains(mobileApp.Hosting.Bindings, binding => binding.Url == bound && binding.Account == account!.Account.Key);
            Assert.All(mobileApp.Hosting.Bindings, binding => Assert.Equal(account!.Account.Key, binding.Account));
            Assert.Equal([$"v {P.AzureDevOpsTitle} 0", "[Failed] " + P.AzureDevOpsServerErrorText], Section(app));
            Assert.Equal(P.AzureDevOpsServerErrorText, await app.RestPointerOnAsync(Part(await MessageAsync(app), "PullRequestMessageText")));
            var lines = LogSince(app, logMark);
            var id = RequestIdOf(lines, $"Azure DevOps: GET {P.AzureDevOpsListPath} failed:");

            // {id} the refused answer's ActivityId (part B's form), and the fake's TF400898 message names
            // the same {activity} (its body and its header carry one value), whole with its full stop.
            Assert.Matches(PartB.AzureDevOpsActivityIdPattern, id);
            var serverError = P.AzureDevOpsServerErrorStart + id + ".";
            Assert.Equal(serverError, failure.Message);
            AssertWarningOnce(lines, P.AzureDevOpsRefusedWarning(id));
            AssertWarningOnce(lines, P.AzureDevOpsListFailedWarning(id, serverError));
            Assert.DoesNotContain(lines, line => line.Message.StartsWith("mobile-app: listed", StringComparison.Ordinal));

            // P3 step 6: a refresh held at the fake.
            mark = Mark(served);
            logMark = app.LogMark();
            var hold = served.AzureDevOps.HoldNext(P.AzureDevOpsListRoute);
            try
            {
                app.RightClick(await HeaderAsync(app));
                app.ClickMenuItem(P.AzureDevOpsRefresh);
                await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
                await WaitForStateAsync(app, mobileApp, PullRequestsState.Loading, PullRequestMessageKind.Loading);
                AssertNoRequests(served, mark);
                Assert.Equal([$"v {P.AzureDevOpsTitle} 0", "[Loading] " + P.AzureDevOpsLoading], Section(app));
            }
            finally
            {
                hold.Dispose();
            }

            await WaitForListedAsync(app, MobileApp, 3, P.AzureDevOpsListed, logMark);
            var refreshed = Assert.Single(AzureSince(served, mark));
            AssertRequest(refreshed, P.AzureDevOpsListPath, P.AzureDevOpsListRoute, P.AzureDevOpsListQuery, FakeAuthorizationForm.BasicEmptyUser, 200, null);
            Assert.Equal(PullRequestsState.Loaded, mobileApp.HostingState.PullRequests);
            Assert.Equal([$"v {P.AzureDevOpsTitle} 3", .. P.AzureDevOpsRows.Select(row => row.Name)], Section(app));

            // P8 step 7: manifest, D356's row; nothing asked.
            mark = Mark(served);
            var gitRequests = served.AzureDevOps.GitRequests.Count;
            logMark = app.LogMark();
            app.Click(app.TabStrip().Tabs[1].Bounds.Center);
            var manifest = await app.WaitForRepositoryAsync();
            await WaitForStateAsync(app, manifest, PullRequestsState.OtherOrganization, PullRequestMessageKind.OtherOrganization);
            app.MoveToNeutral();
            ShotPAuditAtEnd(app, P.P08a);
            var harbor = new HostedRepository(Azure, AzureHost(served), $"{OtherOrganization}/Docks", Manifest);
            var state = manifest.HostingState;
            Assert.Equal([("origin", harbor)], state.Remotes.Select(pair => (pair.Key, pair.Value)));
            Assert.Equal(harbor, state.Repository);
            Assert.Null(state.List);
            Assert.Null(state.Failure);
            Assert.Empty(state.ByRef);
            Assert.Empty(state.RequestRemotes);
            Assert.DoesNotContain(manifest.Hosting.Bindings, binding => binding.Url.Contains("harbor-co", StringComparison.Ordinal));
            Assert.Equal([$"v {P.AzureDevOpsTitle} 0", "[OtherOrganization] " + P.OtherOrganizationRow], Section(app));
            var row = await MessageAsync(app);
            Assert.False(row.IsEffectivelyEnabled, "D356's row is not a disabled button.");
            Assert.DoesNotContain(manifest.LeftPanel.Rows, candidate => candidate is PullRequestRow);
            Assert.DoesNotContain(manifest.LeftPanel.Rows.OfType<RefItemRow>(), candidate => candidate.ShowsPullRequest);
            AssertNoBadge(app);
            AssertNoRequests(served, mark);
            Assert.DoesNotContain(served.AzureDevOps.Requests, IsHarborCo);
            Assert.DoesNotContain(served.AzureDevOps.GitRequests.Skip(gitRequests), request => request.Path.Contains("harbor-co/Docks/_git/manifest", StringComparison.Ordinal));
            Assert.DoesNotContain(app.LogLinesSince(logMark), line => line.Contains("manifest: listed", StringComparison.Ordinal));
            Assert.Equal(P.OtherOrganizationToolTip, await app.RestPointerOnAsync(Part(row, "PullRequestMessageText")));
            ShotPAuditAtEnd(app, P.P08b, keepPointer: true);
            var launches = app.Launcher.Requests.Count;
            app.Click(row);
            await app.WaitForIdleAsync();
            await AssertNoDialogAsync(app);
            Assert.Equal(launches, app.Launcher.Requests.Count);
            Assert.DoesNotContain(manifest.LeftPanel.Rows, candidate => candidate is RefItemRow { IsSelected: true } or PullRequestRow { IsSelected: true });
            app.Click(await HeaderAsync(app));
            Assert.Equal([$"> {P.AzureDevOpsTitle} 0"], Section(app));
            app.Click(await HeaderAsync(app));
            Assert.Equal([$"v {P.AzureDevOpsTitle} 0", "[OtherOrganization] " + P.OtherOrganizationRow], Section(app));
            AssertNoRequests(served, mark);

            // P10 step 8 and P13 step 9: manifest's menu, open in browser alone.
            await SectionMenuAsync(app, P.SectionMenuOtherOrganization, P.AzureDevOpsOpenInBrowser, P.OtherOrganizationMenuHeight);
            ShotPAuditAtEnd(app, P.P10d, keepPointer: true);
            app.ClickMenuItem(P.AzureDevOpsOpenInBrowser);
            await app.WaitForAsync(() => OpenedUrls(app).Count == 2, "the launcher's address");
            Assert.Equal(WithPorts(P.ManifestRequestsPage, served), OpenedUrls(app)[^1]);

            // P8 step 10: main's menu offers no request.
            app.RightClick(await app.RefRowAsync("Local branches", "main"));
            var menu = app.Menu();
            Assert.Equal("main", menu.Lines()[0]);
            Assert.DoesNotContain(menu.Lines(), line => line.StartsWith("Open pull request", StringComparison.Ordinal) || line == P.AzureDevOpsCreate);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            AssertNoRequests(served, mark);
            Assert.DoesNotContain(served.AzureDevOps.Requests, IsHarborCo);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, [AzureDevOpsToken], [P.AzureDevOpsRequestsPage, P.ManifestRequestsPage], []);
    }

    [AvaloniaFact]
    public async Task Check_P9_P6_P5_gitlab_listed_remote_more_than_300_and_none()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b p9");
        var app = await OpenAsync(data, served, WebApp, signIn: [GitLab, HostingProvider.GitHub]);
        try
        {
            // P9 step 1: GitLab's origin listed; GitHub asked nothing for the upstream.
            var webApp = app.Session.Shell.ActiveTab.Repository!;
            await WaitForListedAsync(app, WebApp, 3, P.GitLabListed, 0);
            ShotP(app, P.P09a);
            var start = new Marks(0, 0, 0);
            AssertGitLabSignInList(GitLabSince(served, start));
            var gitHub = Assert.Single(GitHubSince(served, start));
            Assert.Equal(("GET", "user", 200, OctoLogin), (gitHub.Method, gitHub.Route, gitHub.Status, gitHub.Login));
            Assert.Empty(AzureSince(served, start));
            AssertLoaded(webApp, served, GitLab);
            Assert.Equal(
                [("origin", new HostedRepository(GitLab, GitLabHost(served), "northfield/platform", WebApp)), ("upstream", new HostedRepository(HostingProvider.GitHub, $"127.0.0.1:{served.GitHub.Port}", OctoLogin, WebApp))],
                webApp.HostingState.Remotes.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, pair.Value)));
            Assert.Contains(webApp.Hosting.Bindings, binding => binding.Url == WithPorts(WebAppUrl, served) && binding.Account.Provider == GitLab);
            Assert.Contains(webApp.Hosting.Bindings, binding => binding.Url == WithPorts(WebAppUpstreamUrl, served) && binding.Account.Provider == HostingProvider.GitHub);
            AssertRows(app, GitLab, P.GitLabRows);
            Assert.False(Part(await app.RefRowAsync("Remotes", "upstream", "main"), "RefPullRequest").IsVisible);
            Assert.Empty(webApp.Avatars.FileByEmail);
            Assert.DoesNotContain(app.LogLinesSince(0), line => line.Contains("showing GitHub avatars", StringComparison.Ordinal));

            // P9 step 2: upstream/main's and origin/topic/no-request's menus.
            var mark = Mark(served);
            app.RightClick(await app.RefRowAsync("Remotes", "upstream", "main"));
            var upstream = app.Menu().Lines();
            Assert.Equal("upstream/main", upstream[0]);
            Assert.DoesNotContain(upstream, IsRequestLine);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            app.Click(await app.RefRowAsync("Remotes", "origin", "topic"));
            app.RightClick(await app.RefRowAsync("Remotes", "origin", "topic", "no-request"));
            var topic = app.Menu().Lines();
            Assert.Equal([P.GitLabCreate], topic.Where(IsRequestLine));
            // In the group of its own after the delete group, where 7a's P12 step 16 had Create pull request….
            var create = topic.ToList().IndexOf(P.GitLabCreate);
            Assert.True(create >= 2 && create + 1 < topic.Count, $"Create merge request… is not inside the menu: {string.Join(" | ", topic)}");
            Assert.Equal("—", topic[create - 1]);
            Assert.StartsWith("Delete topic/no-request on origin", topic[create - 2], StringComparison.Ordinal);
            Assert.Equal("—", topic[create + 1]);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            AssertNoRequests(served, mark);

            // P6 step 3: 298 more; three pages; 300 listed.
            var project = served.GitLab.Project(Scenarios.Hosting7bWebApp);
            for (var n = P.FirstAdded; n <= P.LastAdded; n++)
            {
                served.GitLab.AddMergeRequest(project, P.AddedTitle(n), OctoLogin, P.AddedHead, "main");
            }

            mark = Mark(served);
            var logMark = app.LogMark();
            app.RightClick(await HeaderAsync(app));
            app.ClickMenuItem(P.GitLabRefresh);
            await WaitForListedAsync(app, WebApp, 300, P.GitLabListed, logMark);
            var pages = GitLabSince(served, mark);
            Assert.Equal(3, pages.Count);
            Assert.All(pages, page => Assert.Equal(("GET", P.GitLabListPath, P.GitLabListRoute, FakeAuthorizationForm.Bearer, 200), (page.Method, page.Path, page.Route, page.Authorization, page.Status)));
            Assert.Equal(P.GitLabListQuery, pages[0].Query);
            for (var page = 2; page <= 3; page++)
            {
                var parameters = pages[page - 1].Query.Split('&');
                Assert.Contains($"page={page}", parameters);
                Assert.Contains("state=opened", parameters);
                Assert.Contains("per_page=100", parameters);
            }

            Assert.Empty(AzureSince(served, mark));
            Assert.Empty(GitHubSince(served, mark));
            Assert.Empty(served.GitLab.Errors);
            Assert.True(webApp.HostingState.List!.MoreExist);
            Assert.Equal([.. Enumerable.Range(P.FirstAdded, P.LastAdded - P.FirstAdded + 1).Reverse(), 23, 22], webApp.HostingState.List.PullRequests.Select(pull => pull.Number));
            Assert.Equal(
                [("refs/remotes/origin/docs/readme", 22), ("refs/remotes/origin/topic/no-request", 24)],
                webApp.HostingState.ByRef.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, pair.Value.Number)));
            Assert.Equal(300, PullRequestRows(app).Count);
            var section = Section(app);
            Assert.Equal((302, $"v {P.GitLabTitle} 300", "[More] " + P.GitLabMore), (section.Count, section[0], section[^1]));
            var list = app.Find<ItemsControl>("RefList");
            list.ScrollIntoView(webApp.LeftPanel.Rows.Count - 1);
            app.Settle();
            await app.WaitForAsync(() => list.GetLogicalDescendants().OfType<Control>().Any(control => Avalonia.Automation.AutomationProperties.GetName(control) == "Stashes"), "the list's end");
            // P6 step 3 foresees no cut of this row ("no text cut but by an ellipsis"): GitLab's sentence,
            // one character longer than 7a's, which fits, overflows the row at the panel's default 260
            // while its tooltip is the action (step 4), so the audit fails closed (ShotPAuditAtEnd).
            ShotPAuditAtEnd(app, P.P06a);
            var (extent, viewport) = PanelScroll(app);
            Assert.True(extent > viewport + 1, $"The panel's list shows no scroll bar: extent {extent}, viewport {viewport}.");
            Assert.Equal(
                ["  !25 More work 25", "  !24 More work 24", "  !23 Fix a typo in the footer", "  !22 Draft: Update the readme", "  [More] " + P.GitLabMore, "Tags 0", "Stashes 0"],
                Panel(app)[^7..]);
            Assert.Equal(
                [(25, OctoLogin, false), (24, OctoLogin, false), (23, AdaLogin, false), (22, OctoLogin, true)],
                PullRequestRows(app).TakeLast(4).Select(row => (row.Number, row.Author, row.IsDraft)));

            // P6 steps 4 and 5: the row's tooltip and click.
            var more = await MessageAsync(app);
            Assert.Equal(P.GitLabOpenInBrowser, await app.RestPointerOnAsync(more));
            mark = Mark(served);
            var selection = webApp.Selection;
            app.Click(more);
            await app.WaitForAsync(() => OpenedUrls(app).Count == 1, "the launcher's address");
            Assert.Equal([WithPorts(P.GitLabRequestsPage, served)], OpenedUrls(app));
            AssertNoRequests(served, mark);
            Assert.Equal(selection, webApp.Selection);

            // P5 step 6: every open merge request closed; the refresh lists none.
            foreach (var request in served.GitLab.MergeRequests(project).Where(request => request.State == "opened"))
            {
                request.State = "closed";
            }

            mark = Mark(served);
            logMark = app.LogMark();
            app.RightClick(await HeaderAsync(app));
            app.ClickMenuItem(P.GitLabRefresh);
            await WaitForListedAsync(app, WebApp, 0, P.GitLabListed, logMark);
            list.ScrollIntoView(0);
            app.Settle();
            ShotP(app, P.P05a);
            var none = Assert.Single(GitLabSince(served, mark));
            AssertRequest(none, P.GitLabListPath, P.GitLabListRoute, P.GitLabListQuery, FakeAuthorizationForm.Bearer, 200, AdaLogin);
            Assert.Empty(AzureSince(served, mark));
            Assert.Empty(GitHubSince(served, mark));
            Assert.Equal(PullRequestsState.Loaded, webApp.HostingState.PullRequests);
            Assert.Empty(webApp.HostingState.List!.PullRequests);
            Assert.False(webApp.HostingState.List.MoreExist);
            Assert.Empty(webApp.HostingState.ByRef);
            Assert.Equal([$"v {P.GitLabTitle} 0", "[None] " + P.GitLabNone], Section(app));
            Assert.All(webApp.LeftPanel.Rows.OfType<RefItemRow>(), row => Assert.Equal(string.Empty, row.PullRequestText));
            AssertNoBadge(app);
            (extent, viewport) = PanelScroll(app);
            Assert.True(extent <= viewport + 0.5, $"The panel's list shows a scroll bar: extent {extent}, viewport {viewport}.");
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, [GitLabToken, GitHubToken], [P.GitLabRequestsPage], []);
    }

    private static bool IsRequestLine(string line) =>
        line.StartsWith("Open merge request", StringComparison.Ordinal) || line.StartsWith("Open pull request", StringComparison.Ordinal)
        || line is P.GitLabCreate or P.AzureDevOpsCreate;

    private static bool IsHarborCo(FakeServiceRequest request) =>
        request.Path.StartsWith("/harbor-co/", StringComparison.OrdinalIgnoreCase) || (request.Route?.Contains("harbor-co", StringComparison.Ordinal) ?? false);

    private static string GitLabHost(ServedHosting7b served) => $"127.0.0.1:{served.GitLab.Port}";

    private static string AzureHost(ServedHosting7b served) => $"127.0.0.1:{served.AzureDevOps.Port}";

    /// <summary>P1 and P9 step 1: the hosting <c>Loaded</c> with the provider's repository, <c>origin</c> alone a request remote, the scenario's three requests and <c>ByRef</c>.</summary>
    private static void AssertLoaded(RepositoryViewModel repository, ServedHosting7b served, HostingProvider provider)
    {
        var state = repository.HostingState;
        var expected = provider == GitLab
            ? new HostedRepository(GitLab, GitLabHost(served), "northfield/platform", WebApp)
            : new HostedRepository(Azure, AzureHost(served), $"{Organization}/{Project}", MobileApp);
        Assert.Equal(PullRequestsState.Loaded, state.PullRequests);
        Assert.Equal(expected, state.Repository);
        Assert.Equal(["origin"], state.RequestRemotes.Keys);
        var rows = P.RowsOf(provider);
        var list = state.List!;
        Assert.False(list.MoreExist);
        Assert.Equal(rows.Select(row => (row.Number, row.Title, row.Author, row.Draft, "main")), list.PullRequests.Select(pull => (pull.Number, pull.Title, pull.Author ?? string.Empty, pull.IsDraft, pull.BaseRef)));
        Assert.Equal(rows.Select(row => WithPorts(P.RequestUrlOf(provider, row.Number), served)), list.PullRequests.Select(pull => pull.HtmlUrl.AbsoluteUri));
        Assert.Equal(rows.Select(row => row.HeadLabel[(row.HeadLabel.IndexOf(':', StringComparison.Ordinal) + 1)..]), list.PullRequests.Select(pull => pull.HeadRef));
        Assert.All(list.PullRequests, pull => Assert.Equal(expected, pull.BaseRepository));
        var fork = list.PullRequests[0].HeadRepository;
        Assert.Equal(provider == GitLab ? WebAppFork : $"{Organization}/{Project}/mobile-app-fork", fork?.FullName);
        Assert.All(list.PullRequests.Skip(1), pull => Assert.Equal(expected, pull.HeadRepository));
        var byRef = provider == GitLab ? P.GitLabByRef : P.AzureDevOpsByRef;
        Assert.Equal(
            byRef.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, pair.Value)),
            state.ByRef.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, pair.Value.Number)));
    }

    /// <summary>P2 step 1: signed out, no request, no rewrites read; the section's one sign-in row; no number on a branch row and no badge.</summary>
    private static void AssertSignedOut(ShellDriver app, ServedHosting7b served, RepositoryViewModel repository, HostingProvider provider)
    {
        foreach (var each in new[] { GitLab, Azure, HostingProvider.GitHub })
        {
            Assert.Null(app.Session.Services.Hosting.AccountOf(each));
        }

        AssertNoRequests(served, new Marks(0, 0, 0));
        Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.Contains("--get-regexp ^url", StringComparison.Ordinal));
        var state = repository.HostingState;
        Assert.Equal(PullRequestsState.SignedOut, state.PullRequests);
        Assert.Equal(provider, state.Repository?.Provider);
        Assert.Equal(provider == GitLab ? WebAppPath : $"{Organization}/{Project}/{MobileApp}", state.Repository?.FullName);
        Assert.Null(state.List);
        Assert.Empty(state.ByRef);
        Assert.Empty(repository.Hosting.Bindings);
        Assert.Equal([$"v {P.TitleOf(provider)} 0", "[SignIn] " + P.SignInToSeeOf(provider)], Section(app));
        Assert.DoesNotContain(repository.LeftPanel.Rows, row => row is PullRequestRow);
        Assert.DoesNotContain(repository.LeftPanel.Rows.OfType<RefItemRow>(), row => row.ShowsPullRequest);
        AssertNoBadge(app);
    }

    /// <summary><c>Local branches</c>' rows at depth 1 (their names), between its header and the next.</summary>
    private static List<string> LocalBranches(ShellDriver app) => [.. LocalBranchRows(app).Select(row => row.Text)];

    /// <summary><c>Local branches</c>' rows at depth 1, between its header and the next.</summary>
    private static List<RefItemRow> LocalBranchRows(ShellDriver app)
    {
        var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
        var start = rows.FindIndex(row => row is RefSectionRow { Section: RefSection.LocalBranches });
        return [.. rows.Skip(start + 1).TakeWhile(row => row is not RefSectionRow).OfType<RefItemRow>().Where(row => row.Depth == 1)];
    }

    /// <summary>P14 step 7: opens <c>Local branches</c> › <c>feature</c>, <c>Remotes</c> › <c>origin</c> › <c>feature</c> and › <c>docs</c>.</summary>
    private static async Task OpenBranchFoldersAsync(ShellDriver app)
    {
        app.Click(await app.RefRowAsync("Local branches", "feature"));
        app.Click(await app.RefRowAsync("Remotes", "origin", "feature"));
        app.Click(await app.RefRowAsync("Remotes", "origin", "docs"));
    }

    /// <summary>
    /// P14: the request's local and remote branch rows show its number, the draft's remote branch its
    /// own; the other branch rows and the folders none.
    /// </summary>
    private static async Task AssertBranchNumbersAsync(ShellDriver app, (string Name, int Number) request, (string Name, int Number) draft, string[][]? alsoWithout)
    {
        Assert.Equal("!" + request.Number, Part(await app.RefRowAsync("Local branches", "feature", request.Name), "RefPullRequest").Text);
        var remote = Part(await app.RefRowAsync("Remotes", "origin", "feature", request.Name), "RefPullRequest");
        Assert.Equal(("!" + request.Number, true), (remote.Text, remote.IsEffectivelyVisible));
        var drafted = Part(await app.RefRowAsync("Remotes", "origin", "docs", draft.Name), "RefPullRequest");
        Assert.Equal(("!" + draft.Number, true), (drafted.Text, drafted.IsEffectivelyVisible));
        List<string[]> without =
        [
            ["Local branches", "local-work"], ["Local branches", "main"], ["Local branches", "feature"], ["Local branches", "topic"],
            ["Remotes", "origin"], ["Remotes", "origin", "main"], ["Remotes", "origin", "feature"], ["Remotes", "origin", "docs"],
        ];
        if (alsoWithout is not null)
        {
            without.AddRange(alsoWithout);
        }

        foreach (var path in without)
        {
            Assert.False(Part(await app.RefRowAsync(path), "RefPullRequest").IsVisible, $"{string.Join(" › ", path)} shows a number.");
        }
    }

    /// <summary>P14 step 7: the local request branch's whole menu, then its <c>Open … request</c> item, which hands the request's page to the launcher and asks nothing.</summary>
    private static async Task BranchMenuOpensRequestAsync(ShellDriver app, ServedHosting7b served, HostingProvider provider, string name, int number, int launches)
    {
        var mark = Mark(served);
        app.RightClick(await app.RefRowAsync("Local branches", "feature", name));
        AssertWholeMenu(app, P.RequestBranchMenu(provider), $"Check out feature/{name}", P.RequestBranchMenuHeight);
        app.ClickMenuItem(P.OpenRequest(provider, number));
        await app.WaitForAsync(() => OpenedUrls(app).Count == launches, "the launcher's address");
        await AssertNoMenuAsync(app, null);
        Assert.Equal(WithPorts(P.RequestUrlOf(provider, number), served), OpenedUrls(app)[^1]);
        AssertNoRequests(served, mark);
    }

    /// <summary>P13 step 5: a request's <c>Open in browser</c>, then the section's <c>Open … in browser</c>: two launches, nothing asked, no operation, no Warning.</summary>
    private static async Task OpenRequestAndPageAsync(ShellDriver app, ServedHosting7b served, HostingProvider provider, int number)
    {
        var mark = Mark(served);
        var warnings = app.LogMark();
        app.RightClick(await RequestAsync(app, number));
        app.ClickMenuItem(P.OpenInBrowser);
        await app.WaitForAsync(() => OpenedUrls(app).Count == 1, "the launcher's address");
        app.RightClick(await HeaderAsync(app));
        app.ClickMenuItem(P.OpenInBrowserOf(provider));
        await app.WaitForAsync(() => OpenedUrls(app).Count == 2, "the launcher's second address");
        await app.WaitForIdleAsync();
        Assert.Equal([WithPorts(P.RequestUrlOf(provider, number), served), WithPorts(P.RequestsPageOf(provider), served)], OpenedUrls(app));
        AssertNoRequests(served, mark);
        Assert.DoesNotContain(app.LogLinesSince(warnings), line => line.Contains("[WRN]", StringComparison.Ordinal));
    }

    /// <summary>P1 method 2 step 1: Azure DevOps' sign-in and list, in order, each Basic with an empty user.</summary>
    private static void AssertAzureSignInList(ServedHosting7b served, IReadOnlyList<FakeServiceRequest> requests)
    {
        Assert.Equal(P.SignInRoutesOf(Azure).Select(route => route + " 200"), requests.Select(request => $"{request.Route} {request.Status}"));
        Assert.All(requests, request => Assert.Equal(FakeAuthorizationForm.BasicEmptyUser, request.Authorization));
        AssertRequest(requests[0], P.AzureDevOpsConnectionPath, P.AzureDevOpsConnectionRoute, P.AzureDevOpsConnectionQuery, FakeAuthorizationForm.BasicEmptyUser, 200, AdaAzureLogin(served));
        AssertRequest(requests[2], P.AzureDevOpsRepositoryPath, P.AzureDevOpsRepositoryRoute, P.AzureDevOpsRepositoryQuery, FakeAuthorizationForm.BasicEmptyUser, 200, null);
        AssertRequest(requests[3], P.AzureDevOpsListPath, P.AzureDevOpsListRoute, P.AzureDevOpsListQuery, FakeAuthorizationForm.BasicEmptyUser, 200, null);
    }
}
