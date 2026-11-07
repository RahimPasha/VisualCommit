using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// Part B, browse and clone (docs/test-reports/phase-7b.md, checks B1 to B8; design 11.7): the clone
/// form's button per provider and "From an account…", the browser per provider with its switch,
/// listed, loading and failed in its provider's words, the two long names cut at their start, the
/// clones of GitLab's <c>northfield/platform/web-app</c> and Azure DevOps' <c>mobile-app</c> through the
/// app's helper, and the palette's three commands. One method per app start (the report's part B
/// methods table); no picture shows an opened repository of a new provider; every method ends with
/// the tripwire and the fakes' clean end.
/// </summary>
public sealed partial class Phase7bChecksB : Phase7bChecks
{
    private static readonly HostingProvider[] Three = [HostingProvider.GitHub, HostingProvider.GitLab, HostingProvider.AzureDevOps];

    /// <summary>Method 1: B1, B3, B6 and B7 for GitLab, signed in through Settings, Accounts.</summary>
    [AvaloniaFact]
    public Task Check_B1_B3_B6_B7_gitlab_sign_in_list_and_clone() => SignInListAndCloneAsync(HostingProvider.GitLab);

    /// <summary>Method 2: B1, B3, B6 and B7 for Azure DevOps, signed in through Settings, Accounts.</summary>
    [AvaloniaFact]
    public Task Check_B1_B3_B6_B7_azure_sign_in_list_and_clone() => SignInListAndCloneAsync(HostingProvider.AzureDevOps);

    /// <summary>Method 3: B4 and B5 for GitLab.</summary>
    [AvaloniaFact]
    public Task Check_B4_B5_gitlab_loading_and_failed() => LoadingAndFailedAsync(HostingProvider.GitLab);

    /// <summary>Method 4: B4 and B5 for Azure DevOps.</summary>
    [AvaloniaFact]
    public Task Check_B4_B5_azure_loading_and_refused() => LoadingAndFailedAsync(HostingProvider.AzureDevOps);

    private async Task SignInListAndCloneAsync(HostingProvider provider)
    {
        var gitLab = provider == HostingProvider.GitLab;
        await using var served = await ServeAsync();
        using var clones = new TempDirectory("clones");
        using var data = new TempDirectory(gitLab ? "phase7b b1 gitlab" : "phase7b b1 azure");
        var app = await StartEmptyAsync(data, served);
        string clone;
        string? sharedOwner;
        try
        {
            var session = app.Session.Services.Hosting;
            var mark = Mark(served);
            if (gitLab)
            {
                // B1 step 1: the three on, none signed in: no button, the URL box at its full width.
                Assert.All(Three, each => Assert.True(session.IsOn(each)));
                Assert.All(Three, each => Assert.Null(session.AccountOf(each)));
                await OpenFormAsync(app, button: false);
                Assert.Equal("CloneUrlBox", app.FocusedAutomationId());
                AssertForm(app, null);
                AssertNoRequests(served, mark);
                Assert.Empty(app.Tokens.Calls);
                app.Click(app.FindByAutomationId("CloneCloseButton"));
                app.Settle();
                Assert.False(Welcome(app).IsCloneFormOpen);
                Assert.All(new[] { "OpenRepoButton", "CloneRepoButton", "InitRepoButton" }, id => Assert.True(app.FindByAutomationId(id).IsEffectivelyVisible, $"{id} is hidden."));
            }

            // B1 step 2: the sign-in through Settings, Accounts, then Esc.
            var screen = await app.OpenAccountsAsync();
            var dialog = await app.OpenSignInDialogAsync(provider);
            if (!gitLab)
            {
                Assert.Equal(PartA.AzureDevOpsDialogTitle, dialog.Title);
                Assert.Equal(string.Empty, dialog.Organization);
                Assert.Equal("SignInOrganization", app.FocusedAutomationId());
                app.TypeOrganization(PartA.Organization);
                app.Click(app.FindByAutomationId("SignInToken"));
            }

            app.TypeToken(gitLab ? GitLabToken : AzureDevOpsToken);
            await app.ConfirmSignInAsync();
            Assert.True(dialog.IsClosed, "The sign-in dialog did not close.");
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
            Assert.Equal(["probe", "save " + (gitLab ? PartA.GitLabKey(served.GitLab.Port) : PartA.AzureDevOpsKey(served.AzureDevOps.Port))], app.Tokens.Calls);
            if (gitLab)
            {
                var user = Assert.Single(GitLabSince(served, mark));
                Assert.Equal(("GET", "/api/v4/user", "user", FakeAuthorizationForm.Bearer, 200, AdaLogin), (user.Method, user.Path, user.Route, user.Authorization, user.Status, user.Login));
                Assert.Empty(AzureSince(served, mark));
            }
            else
            {
                var signIn = AzureSince(served, mark);
                Assert.Equal(["connection:fernhill-labs", "areas"], signIn.Select(request => request.Route));
                AssertRequest(signIn[0], $"/{Organization}/_apis/connectionData", "connection:fernhill-labs", PartA.AzureDevOpsSignInQuery, FakeAuthorizationForm.BasicEmptyUser, 200, AdaAzureLogin(served));
                AssertRequest(signIn[1], $"/{Organization}/_apis/resourceAreas/{PartA.CoreAreaId}", "areas", PartA.AzureDevOpsSignInQuery, FakeAuthorizationForm.BasicEmptyUser, 200);
                Assert.Empty(GitLabSince(served, mark));
            }

            Assert.Empty(GitHubSince(served, mark));
            var account = session.AccountOf(provider);
            Assert.Equal((AccountState.SignedIn, TokenPlace.Keychain), (account?.State, account?.Place));
            if (!gitLab)
            {
                Assert.Equal(Organization, account?.Account.Login);
            }

            Assert.Single(app.Session.Shell.Tabs);
            Assert.Null(app.Session.Services.DialogHost.Current);

            // B1 step 3: the button names the one signed-in provider.
            mark = Mark(served);
            var stored = app.Tokens.Calls.Count;
            await OpenFormAsync(app);
            Assert.Equal("CloneUrlBox", app.FocusedAutomationId());
            var button = PartB.ButtonOf(provider);
            AssertForm(app, button);
            AssertFormTexts(app);
            ShotB(app, gitLab ? PartB.B01GitLab : PartB.B01Azure);
            AssertNoRequests(served, mark);
            Assert.Empty(StoreCallsSince(app, stored));

            // B3 step 4: the provider's list, its R picture.
            app.Click(BrowseButton(app));
            var browser = await WaitForBrowserAsync(app, loaded: true);
            if (gitLab)
            {
                await WaitForGitLabAsync(app, served, mark, PartB.GitLabListRoute);
                AssertGitLabList(Assert.Single(GitLabSince(served, mark)), 200);
                Assert.Empty(AzureSince(served, mark));
            }
            else
            {
                await WaitForAzureAsync(app, served, mark, PartB.AzureDevOpsListRoute);
                AssertAzureList(served, Assert.Single(AzureSince(served, mark)), 200);
                Assert.Empty(GitLabSince(served, mark));
            }

            Assert.Empty(GitHubSince(served, mark));
            Assert.Empty(StoreCallsSince(app, stored));
            Assert.Equal([provider], browser.Providers);
            Assert.False(browser.ShowsSwitch);
            var rows = PartB.RowsOf(provider);
            var longName = gitLab ? PartB.GitLabLongName : PartB.AzureDevOpsLongName;
            AssertBrowser(app, PartB.TitleOf(provider), string.Empty, chooseEnabled: false);
            AssertRows(app, rows, cut: longName);
            var scroll = InBrowser(app, "CloneFromGitHubList").GetVisualDescendants().OfType<ScrollViewer>().First();
            Assert.True(scroll.Extent.Height <= scroll.Viewport.Height + 0.5, "The list scrolls.");
            var behind = BrowseButton(app);
            Assert.True(behind.IsEffectivelyVisible && !behind.IsEffectivelyEnabled, $"{button} is not drawn disabled under the browser.");
            Assert.Equal(button, AutomationProperties.GetName(behind));
            Assert.DoesNotContain(app.Session.Services.DialogHost.Open, open => open is SettingsDialogViewModel);
            ShotB(app, PartB.ListedPictureOf(provider));

            // B6 step 5: the long name cut at its start, whole in its tooltip.
            var item = Item(app, longName);
            AssertCut(app, item, longName);
            Assert.All(rows.Where(row => row.FullName != longName), row => Assert.DoesNotContain(NameBlock(Item(app, row.FullName)).TextLayout.TextLines, line => line.HasCollapsed));
            var hovered = Mark(served);
            var tip = await app.RestPointerOnAsync(item);
            Assert.Equal(longName + "\n" + PartB.Private, tip);
            Assert.True(item.IsPointerOver, "The long row is not under the pointer.");
            Assert.Equal(PaletteOf(app).Hover, BackgroundOf(item));
            Assert.Null(browser.Selected);
            Assert.False(InBrowser(app, "ConfirmButton").IsEffectivelyEnabled);
            AssertNoRequests(served, hovered);
            ShotB(app, gitLab ? PartB.B06GitLab : PartB.B06Azure, keepPointer: true);
            app.MoveToNeutral();

            // B7 step 6: Choose fills in the URL, as the API gave it.
            mark = Mark(served);
            var quiet = await app.QuietCallMarkAsync();
            var chosen = PartB.ChosenOf(provider);
            app.Click(Item(app, chosen));
            app.Settle();
            Assert.Equal(chosen, browser.Selected?.FullName);
            Assert.Equal(PaletteOf(app).Selection, BackgroundOf(Item(app, chosen)));
            Assert.True(InBrowser(app, "ConfirmButton").IsEffectivelyEnabled, "Choose is disabled with a row selected.");
            app.Click(InBrowser(app, "ConfirmButton"));
            await app.WaitForAsync(() => Browser(app) is null, "the browser to close");
            var url = WithPorts(PartB.CloneUrlOf(provider), served);
            AssertForm(app, button, url: url, name: PartB.FolderOf(provider));
            Assert.Empty(await app.QuietCallsSinceAsync(quiet));
            AssertNoRequests(served, mark);
            ShotB(app, gitLab ? PartB.B07GitLab : PartB.B07Azure);

            // B7 step 7: the parent folder typed.
            app.Click(Box(app, "CloneParentBox"));
            app.Type(clones.Path);
            app.Settle();

            // B7 step 8: Clone, through the app's helper, asking nothing.
            mark = Mark(served);
            var fake = gitLab ? (FakeHostingService)served.GitLab : served.AzureDevOps;
            var gitMark = (fake.GitRequests.Count, (gitLab ? served.AzureDevOps : (FakeHostingService)served.GitLab).GitRequests.Count, served.GitHub.GitRequests.Count);
            stored = app.Tokens.Calls.Count;
            var log = app.LogMark();
            quiet = await app.QuietCallMarkAsync();
            app.Click(app.Find<Button>("CloneStartButton"));
            var repository = await app.WaitForRepositoryAsync();
            var listed = gitLab ? PartB.GitLabListed : PartB.AzureDevOpsListed;
            await app.WaitForAsync(() => app.LogLinesSince(log).Any(line => line.EndsWith(listed, StringComparison.Ordinal)), "the clone's tab to list its requests");
            await app.WaitForWatchersQuietAsync();

            var lines = LogSince(app, log);
            var folder = PartB.FolderOf(provider);
            Assert.DoesNotContain(lines, line => line.Message.StartsWith($"{folder}: Clone: git asks (", StringComparison.Ordinal));
            Assert.Null(app.Session.Services.DialogHost.Current);
            var cloneCall = gitLab ? PartB.GitLabCloneCall : PartB.AzureDevOpsCloneCall;
            AssertCalledInOrder(app, quiet, PartB.ConfigRead, WithPorts(cloneCall, served));
            var calls = app.GitCalls().Where(call => call.Sequence > quiet).ToList();
            Assert.True(SameFolder(calls.First(call => call.CommandText == PartB.ConfigRead).WorkingDirectory, data.Path), "The configuration read did not run in the data folder.");
            Assert.True(SameFolder(calls.First(call => call.CommandText == WithPorts(cloneCall, served)).WorkingDirectory, clones.Path), "The clone did not run in the parent folder.");
            Assert.Contains(lines, line => line.Message == WithPorts(gitLab ? PartB.GitLabAnswered : PartB.AzureDevOpsAnswered, served));
            Assert.DoesNotContain(lines, line => line.Message.StartsWith($"{folder}: Clone: git's credential", StringComparison.Ordinal)
                && (line.Message.EndsWith("is not bound.", StringComparison.Ordinal) || line.Message.EndsWith("names another user.", StringComparison.Ordinal) || line.Message.EndsWith("has no token.", StringComparison.Ordinal)));
            Assert.DoesNotContain(lines, line => line.Message.Contains("refused the account's token", StringComparison.Ordinal));
            Assert.DoesNotContain(lines, line => line.Message.Contains(PartB.ConfigUnread, StringComparison.Ordinal));

            var gitPath = WithPorts(gitLab ? PartB.GitLabGitPath : PartB.AzureDevOpsGitPath, served);
            var git = fake.GitRequests.Skip(gitMark.Item1).Where(request => request.Path.StartsWith(gitPath, StringComparison.Ordinal)).ToList();
            Assert.NotEmpty(git);
            var proactive = app.Session.Shell.Git?.Version >= ProactiveAuthVersion;
            Assert.True(
                proactive
                    ? git[0] is { GivenUserName: "x-access-token", Status: 200 }
                    : gitLab ? git[0] is { GivenUserName: null, Status: 401 } : git[0] is { GivenUserName: Organization, Status: 401 },
                $"The clone's first request was {git[0]}, with git {app.Session.Shell.Git?.Version}.");
            Assert.Contains(git, request => request is { GivenUserName: "x-access-token", Status: 200 });
            Assert.DoesNotContain(git, request => request.Status == 200 && request.GivenUserName is not null && request.GivenUserName != "x-access-token");
            Assert.DoesNotContain(git, request => request is { GivenUserName: "x-access-token", Status: 401 });
            if (gitLab)
            {
                Assert.DoesNotContain(git, request => request.GivenUserName is not null && request.GivenUserName != "x-access-token");
            }
            else
            {
                Assert.DoesNotContain(fake.GitRequests.Skip(gitMark.Item1), request => request.Path.Contains("/harbor-co/", StringComparison.OrdinalIgnoreCase));
            }

            Assert.Equal(gitMark.Item2, (gitLab ? served.AzureDevOps : (FakeHostingService)served.GitLab).GitRequests.Count);
            Assert.Equal(gitMark.Item3, served.GitHub.GitRequests.Count);

            List<FakeServiceRequest> api = gitLab ? GitLabSince(served, mark) : AzureSince(served, mark);
            Assert.DoesNotContain(api, request => request.Route is "user" or PartB.GitLabListRoute or PartB.AzureDevOpsListRoute || (request.Route ?? string.Empty).StartsWith("connection:", StringComparison.Ordinal));
            Assert.DoesNotContain(api, request => request.Status == 401);
            Assert.All(api, request => Assert.Equal(gitLab ? FakeAuthorizationForm.Bearer : FakeAuthorizationForm.BasicEmptyUser, request.Authorization));
            foreach (var route in gitLab
                ? new[] { "project:northfield/platform/web-app", "mrs:northfield/platform/web-app" }
                : new[] { "repo:fernhill-labs/mobile app/mobile-app", "pulls:fernhill-labs/mobile app/mobile-app" })
            {
                Assert.True(api.Any(request => request.Route == route && request.Status == 200), $"The tab's refresh asked no {route}: {string.Join(", ", api.Select(request => request.Route))}.");
            }

            Assert.Empty(gitLab ? AzureSince(served, mark) : GitLabSince(served, mark));
            Assert.Empty(GitHubSince(served, mark));
            Assert.Empty(StoreCallsSince(app, stored));

            clone = Path.Combine(clones.Path, folder);
            var tab = Assert.Single(app.Session.Shell.Tabs);
            Assert.True(SameFolder(repository.Path, clone), $"The tab shows {repository.Path}.");
            Assert.False(tab.Welcome.IsCloneFormOpen);
            Assert.Contains(app.Session.Services.Recent.Items, recent => SameFolder(recent.Path, clone));
            Assert.Equal(PartB.ChosenOf(provider), repository.HostingState.Repository?.FullName);
            var binding = Assert.Single(repository.Hosting.Bindings);
            Assert.Equal(WithPorts(gitLab ? PartB.GitLabBinding : PartB.AzureDevOpsBinding, served), binding.Url);
            Assert.Equal(gitLab ? PartA.GitLabKey(served.GitLab.Port) : PartA.AzureDevOpsKey(served.AzureDevOps.Port), binding.Account.Id);
            sharedOwner = binding.SharedOriginOwner;
            account = session.AccountOf(provider);
            Assert.Equal((AccountState.SignedIn, TokenPlace.Keychain, AccountNoteKind.None), (account?.State, account?.Place, account?.NoteKind));
        }
        finally
        {
            app.Dispose();
        }

        // B7 step 9: the clone holds the URL as chosen and no credential.
        using var home = new TempDirectory("phase7b b7 home");
        var chosenUrl = WithPorts(PartB.CloneUrlOf(provider), served);
        Assert.Equal(chosenUrl, (await GitAsync(home, clone, "config", "--get", "remote.origin.url")).StandardOutput.Trim());
        var credential = await GitAsync(home, clone, "config", "--get-regexp", "^credential");
        Assert.Equal((1, string.Empty), (credential.ExitCode, credential.StandardOutput.Trim()));
        var local = (await GitAsync(home, clone, "config", "--local", "--list")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("core.", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(
            [
                $"remote.origin.url={chosenUrl}",
                "remote.origin.fetch=+refs/heads/*:refs/remotes/origin/*",
                "branch.main.remote=origin",
                "branch.main.merge=refs/heads/main",
            ],
            local);
        Assert.Equal("refs/heads/main", (await GitAsync(home, clone, "symbolic-ref", "HEAD")).StandardOutput.Trim());
        var bare = served.MobileApp.SiblingPath(gitLab ? PartB.GitLabBare : PartB.AzureDevOpsBare);
        Assert.Equal((await GitAsync(home, bare, "rev-parse", "refs/heads/main")).StandardOutput.Trim(), (await GitAsync(home, clone, "rev-parse", "HEAD")).StandardOutput.Trim());
        Assert.Equal(PartB.MainSubject, (await GitAsync(home, clone, "log", "-1", "--format=%s")).StandardOutput.Trim());
        Assert.Equal(string.Empty, (await GitAsync(home, clone, "status", "--porcelain")).StandardOutput.Trim());
        Assert.Equal([(HostingProviders.Id(provider), "keychain", true)], Records(data));
        AssertCleanEnd(app, data, served, clones.Path);

        // B7 step 8's binding: the report expects SharedOriginOwner `Fernhill-Labs` (8.2 step 6);
        // asserted last, as the report gives it, so that every other expected result is checked first.
        Assert.Equal(gitLab ? null : Organization, sharedOwner);
    }

    private async Task LoadingAndFailedAsync(HostingProvider provider)
    {
        var gitLab = provider == HostingProvider.GitLab;
        await using var served = await ServeAsync();
        using var data = new TempDirectory(gitLab ? "phase7b b4 gitlab" : "phase7b b4 azure");
        var app = await StartEmptyAsync(data, served);
        try
        {
            // B4 step 1: signed in through the session; the form with the provider's button.
            await app.SignInAsync(provider, gitLab ? GitLabToken : AzureDevOpsToken, gitLab ? null : Organization, Cancelled);
            Assert.Equal([gitLab ? "user" : "connection:fernhill-labs"], (gitLab ? served.GitLab.Requests : served.AzureDevOps.Requests).Select(request => request.Route).Take(1));
            Assert.Empty(gitLab ? served.AzureDevOps.Requests : served.GitLab.Requests);
            await OpenFormAsync(app);
            var button = PartB.ButtonOf(provider);
            AssertForm(app, button);
            var mark = Mark(served);
            var stored = app.Tokens.Calls.Count;
            var log = app.LogMark();
            var route = gitLab ? PartB.GitLabListRoute : PartB.AzureDevOpsListRoute;
            var fake = gitLab ? (FakeHostingService)served.GitLab : served.AzureDevOps;

            // B4 step 2: the list's answer held.
            using (var hold = fake.HoldNext(route))
            {
                try
                {
                    app.Click(BrowseButton(app));
                    await app.WaitForAsync(() => hold.Arrived.IsCompleted, "the list's request at the hold");
                    var browser = await WaitForBrowserAsync(app, loaded: false);
                    Assert.False(browser.Load.IsCompleted);
                    AssertBrowser(app, PartB.TitleOf(provider), string.Empty, chooseEnabled: false, status: PartB.Loading);
                    AssertRows(app, []);
                    Assert.Equal(PartB.ListMinHeight, ListHeight(app), PartB.Tolerance);
                    Assert.Equal(PartB.NoteSize, ((TextBlock)InBrowser(app, "CloneFromGitHubStatus")).FontSize);
                    AssertNoRequests(served, mark);
                    Assert.Empty(StoreCallsSince(app, stored));
                    Assert.True(BrowseButton(app).IsEffectivelyVisible, $"{button} is not shown behind the browser.");
                    ShotB(app, gitLab ? PartB.B04GitLab : PartB.B04Azure);
                }
                finally
                {
                    hold.Dispose();
                }

                // B4 step 3: released, the rows come.
                var shown = Browser(app)!;
                await app.WaitForAsync(() => shown.Load.IsCompleted, "the browser's load to end");
                if (gitLab)
                {
                    await WaitForGitLabAsync(app, served, mark, route);
                    AssertGitLabList(Assert.Single(GitLabSince(served, mark)), 200);
                }
                else
                {
                    await WaitForAzureAsync(app, served, mark, route);
                    AssertAzureList(served, Assert.Single(AzureSince(served, mark)), 200);
                }

                AssertRows(app, PartB.RowsOf(provider), cut: gitLab ? PartB.GitLabLongName : PartB.AzureDevOpsLongName);
                Assert.False(InBrowser(app, "CloneFromGitHubStatus").IsEffectivelyVisible);
                app.PressKey(Key.Escape);
                await app.WaitForAsync(() => Browser(app) is null, "the browser to close");
                AssertForm(app, button);
            }

            // B5 step 4: the list fails once, in the provider's words.
            mark = Mark(served);
            stored = app.Tokens.Calls.Count;
            log = app.LogMark();
            if (gitLab)
            {
                served.GitLab.FailNext(route, FakeGitLabFault.ServerError);
            }
            else
            {
                served.AzureDevOps.FailNext(route, FakeAzureDevOpsFault.InvalidToken);
            }

            app.Click(BrowseButton(app));
            var failed = await WaitForBrowserAsync(app, loaded: true);
            await app.WaitForAsync(() => InBrowser(app, "CloneFromGitHubError").IsEffectivelyVisible, "the browser's error line");
            if (gitLab)
            {
                await WaitForGitLabAsync(app, served, mark, route);
                AssertGitLabList(Assert.Single(GitLabSince(served, mark)), 500);
                Assert.Empty(AzureSince(served, mark));
            }
            else
            {
                await WaitForAzureAsync(app, served, mark, route);
                await app.WaitForAccountAsync(HostingProvider.AzureDevOps, account => account?.State == AccountState.NeedsSignIn);
                await app.WaitForAsync(() => Records(data) is [(_, "refused", _)], "accounts.json to hold the refusal");
                await app.WaitForAsync(() => !BrowseButton(app).IsEffectivelyVisible, "the browse button to go");
                AssertAzureList(served, Assert.Single(AzureSince(served, mark)), 401);
                Assert.Empty(GitLabSince(served, mark));
            }

            Assert.Empty(GitHubSince(served, mark));
            AssertBrowser(app, PartB.TitleOf(provider), string.Empty, chooseEnabled: false, error: gitLab ? PartB.GitLabServerError : PartB.AzureDevOpsRefused);
            Assert.Empty(failed.Rows);
            Assert.Equal(PartB.ListMinHeight, ListHeight(app), PartB.Tolerance);
            Assert.Empty(StoreCallsSince(app, stored));
            var session = app.Session.Services.Hosting;
            var lines = LogSince(app, log);
            var start = gitLab ? PartB.GitLabListingFailedStart : PartB.AzureDevOpsListingFailedStart;
            Assert.Single(lines, line => line.Message.StartsWith(start, StringComparison.Ordinal));
            var id = RequestIdOf(lines, start);
            if (gitLab)
            {
                Assert.Matches(@"^FAKE-GL-\d{6}$", id);
                Assert.Equal(AccountState.SignedIn, session.AccountOf(HostingProvider.GitLab)?.State);
                AssertWarningOnce(lines, string.Format(System.Globalization.CultureInfo.InvariantCulture, PartB.GitLabListingFailed, id));
                AssertWarningOnce(lines, string.Format(System.Globalization.CultureInfo.InvariantCulture, PartB.GitLabCoreWarning, id));
                AssertWarningOnce(lines, string.Format(System.Globalization.CultureInfo.InvariantCulture, PartB.GitLabSessionWarning, id));
                Assert.True(BrowseButton(app).IsEffectivelyVisible, $"{button} is not shown behind the browser.");
            }
            else
            {
                var account = session.AccountOf(HostingProvider.AzureDevOps);
                Assert.Equal((AccountState.NeedsSignIn, AccountNoteKind.Refused, Organization), (account?.State, account?.NoteKind, account?.Account.Login));
                Assert.Equal([("azure-devops", "refused", true)], Records(data));
                Assert.True(app.Tokens.Holds(VisualCommit.Hosting.Tokens.TokenKey.Of(account!.Account.Key)), "The keychain's entry went.");
                AssertWarningOnce(lines, string.Format(System.Globalization.CultureInfo.InvariantCulture, PartB.AzureDevOpsListingFailed, id));
                AssertWarningOnce(lines, string.Format(System.Globalization.CultureInfo.InvariantCulture, PartB.AzureDevOpsCoreWarning, id));
                AssertWarningOnce(lines, WithPorts(string.Format(System.Globalization.CultureInfo.InvariantCulture, PartB.AzureDevOpsSessionWarning, id), served));
                Assert.DoesNotContain(lines, line => line.Message.StartsWith(PartB.AzureDevOpsSessionListingStart, StringComparison.Ordinal));
                Assert.DoesNotContain(AzureSince(served, mark), request => (request.Route ?? string.Empty).StartsWith("connection:", StringComparison.Ordinal));
                Assert.Equal(PartB.FormRight, app.BoundsOf(Box(app, "CloneUrlBox")).Right, PartB.Tolerance);
            }

            Assert.DoesNotContain(lines, line => line.Message.StartsWith(PartB.GitHubListingFailedStart, StringComparison.Ordinal));
            ShotB(app, gitLab ? PartB.B05GitLab : PartB.B05Azure);

            // B5 step 5: Esc.
            mark = Mark(served);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => Browser(app) is null, "the browser to close");
            AssertForm(app, gitLab ? button : null);
            AssertNoRequests(served, mark);
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd(app, data, served);
    }

    /// <summary>Method 5: B2, two accounts and the switch, then three.</summary>
    [AvaloniaFact]
    public async Task Check_B2_two_accounts_and_the_switch()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b b2");
        var app = await StartEmptyAsync(data, served);
        try
        {
            // B2 step 1: GitLab and Azure DevOps signed in: "From an account…".
            var session = app.Session.Services.Hosting;
            await app.SignInAsync(HostingProvider.GitLab, GitLabToken, null, Cancelled);
            await app.SignInAsync(HostingProvider.AzureDevOps, AzureDevOpsToken, Organization, Cancelled);
            Assert.Equal(AccountState.SignedIn, session.AccountOf(HostingProvider.GitLab)?.State);
            Assert.Equal(AccountState.SignedIn, session.AccountOf(HostingProvider.AzureDevOps)?.State);
            Assert.Null(session.AccountOf(HostingProvider.GitHub));
            Assert.Equal(["user"], served.GitLab.Requests.Select(request => request.Route));
            Assert.Equal(["connection:fernhill-labs", "areas"], served.AzureDevOps.Requests.Select(request => request.Route));
            Assert.Empty(served.GitHub.Requests);
            await OpenFormAsync(app);
            var buttonBounds = app.BoundsOf(BrowseButton(app));
            AssertForm(app, PartB.FromAnAccount);
            ShotB(app, PartB.B02FromAnAccount);

            // B2 step 2: the browser on GitLab, first in D353's order, with the switch.
            var mark = Mark(served);
            app.Click(BrowseButton(app));
            var browser = await WaitForBrowserAsync(app, loaded: true);
            await WaitForGitLabAsync(app, served, mark, PartB.GitLabListRoute);
            Assert.Equal(HostingProvider.GitLab, browser.Provider);
            app.Type("fork");
            app.Settle();
            app.Click(Item(app, PartB.GitLabRows[1].FullName));
            app.Settle();
            string[] two = [PartB.GitLabName, PartB.AzureDevOpsName];
            AssertBrowser(app, PartB.GitLabTitle, "fork", chooseEnabled: true, switchTexts: two, selected: PartB.GitLabName, focus: false);
            AssertRows(app, [PartB.GitLabRows[1]], selected: PartB.GitLabRows[1].FullName);
            AssertGitLabList(Assert.Single(GitLabSince(served, mark)), 200);
            Assert.Empty(AzureSince(served, mark));
            Assert.Empty(GitHubSince(served, mark));
            ShotB(app, PartB.B02SwitchGitLab);

            // B2 step 3: Azure DevOps shown, its list held.
            mark = Mark(served);
            var hold = served.AzureDevOps.HoldNext(PartB.AzureDevOpsListRoute);
            try
            {
                ClickSwitch(app, HostingProvider.AzureDevOps);
                await app.WaitForAsync(() => hold.Arrived.IsCompleted, "Azure DevOps' list at the hold");
                app.Settle();
                AssertNoRequests(served, mark);
                Assert.Equal(HostingProvider.AzureDevOps, browser.Provider);
                Assert.Null(browser.Selected);
                AssertBrowser(app, PartB.AzureDevOpsTitle, "fork", chooseEnabled: false, status: PartB.Loading, switchTexts: two, selected: PartB.AzureDevOpsName, focus: false);
                AssertRows(app, []);
                Assert.Equal(PartB.ListMinHeight, ListHeight(app), PartB.Tolerance);
                ShotB(app, PartB.B02SwitchAzureLoading);

                // B2 step 4: back to GitLab, then the answer for Azure DevOps arrives unseen.
                ClickSwitch(app, HostingProvider.GitLab);
                app.Settle();
                AssertBrowser(app, PartB.GitLabTitle, "fork", chooseEnabled: false, switchTexts: two, selected: PartB.GitLabName, focus: false);
                AssertRows(app, [PartB.GitLabRows[1]]);
            }
            finally
            {
                hold.Dispose();
            }

            await WaitForAzureAsync(app, served, mark, PartB.AzureDevOpsListRoute);
            await app.WaitForAsync(() => browser.LoadOf(HostingProvider.AzureDevOps).IsCompleted, "Azure DevOps' load to end");
            app.Settle();
            Assert.Empty(GitLabSince(served, mark));
            AssertAzureList(served, Assert.Single(AzureSince(served, mark)), 200);
            AssertBrowser(app, PartB.GitLabTitle, "fork", chooseEnabled: false, switchTexts: two, selected: PartB.GitLabName, focus: false);
            AssertRows(app, [PartB.GitLabRows[1]]);

            // B2 step 5: Azure DevOps shown again, its kept answer filtered.
            mark = Mark(served);
            ClickSwitch(app, HostingProvider.AzureDevOps);
            app.Settle();
            AssertBrowser(app, PartB.AzureDevOpsTitle, "fork", chooseEnabled: false, switchTexts: two, selected: PartB.AzureDevOpsName, focus: false);
            AssertRows(app, [PartB.AzureDevOpsRows[2]]);
            AssertNoRequests(served, mark);
            ShotB(app, PartB.B02SwitchAzure);

            // B2 step 6: the filter cleared, then Esc.
            app.Click(InBrowser(app, "CloneFromGitHubFilter"));
            PressSelectAll(app);
            app.PressKey(Key.Back);
            app.Settle();
            AssertRows(app, PartB.AzureDevOpsRows, cut: PartB.AzureDevOpsLongName);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => Browser(app) is null, "the browser to close");
            AssertForm(app, PartB.FromAnAccount);

            // B2 step 7: three accounts; the browser on GitHub.
            await app.SignInAsync(HostingProvider.GitHub, GitHubToken, null, Cancelled);
            mark = Mark(served);
            app.Settle();
            Assert.Equal(buttonBounds, app.BoundsOf(BrowseButton(app)));
            AssertForm(app, PartB.FromAnAccount);
            app.Click(BrowseButton(app));
            browser = await WaitForBrowserAsync(app, loaded: true);
            await WaitForGitHubAsync(app, served, mark, PartB.GitHubListRoute);
            Assert.Equal(HostingProvider.GitHub, browser.Provider);
            AssertBrowser(app, PartB.GitHubTitle, string.Empty, chooseEnabled: false, switchTexts: [PartB.GitHubName, PartB.GitLabName, PartB.AzureDevOpsName], selected: PartB.GitHubName);
            AssertRows(app, PartB.GitHubRows);
            var list = Assert.Single(GitHubSince(served, mark));
            Assert.Equal(("GET", "/api/v3/user/repos", PartB.GitHubListRoute, PartB.GitHubListQuery, true, 200, OctoLogin), (list.Method, list.Path, list.Route, list.Query.TrimStart('?'), list.TokenPresent, list.Status, list.Login));
            Assert.Empty(GitLabSince(served, mark));
            Assert.Empty(AzureSince(served, mark));
            ShotB(app, PartB.B02Three);

            // B2 step 8: Esc.
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => Browser(app) is null, "the browser to close");
            AssertForm(app, PartB.FromAnAccount);
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd(app, data, served);
    }

    /// <summary>Method 6: B8, the palette's three commands.</summary>
    [AvaloniaFact]
    public async Task Check_B8_palette_three_commands()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b b8");
        var app = await StartEmptyAsync(data, served);
        try
        {
            var session = app.Session.Services.Hosting;
            await app.SignInAsync(HostingProvider.GitLab, GitLabToken, null, Cancelled);
            var mark = Mark(served);
            var stored = app.Tokens.Calls.Count;

            // B8 step 2: the three commands after Clone repository…, without keys.
            app.PressPalette();
            var labels = app.Palette.Shown.Select(entry => entry.Label).ToList();
            var at = labels.IndexOf(PartB.CloneRepository);
            Assert.True(at >= 0, "The palette lists no Clone repository….");
            Assert.Equal(PartB.PaletteRun, labels.Skip(at).Take(PartB.PaletteRun.Count));
            Assert.All(app.Palette.Shown.Where(entry => entry.Label.StartsWith(PartB.PaletteQuery, StringComparison.Ordinal)), entry =>
                Assert.Equal((true, null, null), (entry.IsEnabled, entry.Detail, entry.KeyText)));
            app.TypeInPalette(PartB.PaletteQuery);
            var palette = app.PaletteSnapshot();
            Assert.Equal(PartB.PaletteRows, palette.Rows.Select(row => row.Label));
            Assert.Equal([true, false, false], palette.Rows.Select(row => row.IsHighlighted));
            Assert.All(palette.Rows, row => Assert.Equal((true, null, null), (row.IsEnabled, row.Detail, row.KeyText)));
            Assert.Equal((PartB.PaletteLeft, PartB.PaletteRight, PartB.PaletteTop), (palette.CardBounds.Left, palette.CardBounds.Right, palette.CardBounds.Top));
            Assert.Equal(PartB.PaletteThreeRows, palette.CardBounds.Height, PartB.Tolerance);
            AssertNoRequests(served, mark);
            Assert.Empty(StoreCallsSince(app, stored));
            ShotB(app, PartB.B08Commands);

            // B8 step 3: GitLab's, signed in: its browser at once.
            app.PressKey(Key.Escape);
            app.Settle();
            app.PressPalette();
            app.TypeInPalette(PartB.GitLabQuery);
            app.PressKey(Key.Enter);
            var browser = await WaitForBrowserAsync(app, loaded: true);
            await WaitForGitLabAsync(app, served, mark, PartB.GitLabListRoute);
            Assert.Equal(HostingProvider.GitLab, browser.Provider);
            AssertBrowser(app, PartB.GitLabTitle, string.Empty, chooseEnabled: false);
            AssertRows(app, PartB.GitLabRows, cut: PartB.GitLabLongName);
            AssertGitLabList(Assert.Single(GitLabSince(served, mark)), 200);
            Assert.Empty(AzureSince(served, mark));
            Assert.Empty(GitHubSince(served, mark));
            Assert.Empty(StoreCallsSince(app, stored));
            Assert.Single(app.Session.Shell.Tabs);
            Assert.True(BrowseButton(app).IsEffectivelyVisible && !BrowseButton(app).IsEffectivelyEnabled, "The button is not drawn disabled under the palette's browser (open point 21).");
            Assert.DoesNotContain(app.Session.Services.DialogHost.Open, open => open is SettingsDialogViewModel);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => Browser(app) is null, "the browser to close");
            AssertForm(app, PartB.GitLabButton);

            // B8 step 4: Azure DevOps', signed out: its dialog first.
            mark = Mark(served);
            stored = app.Tokens.Calls.Count;
            app.PressPalette();
            app.TypeInPalette(PartB.AzureDevOpsQuery);
            app.PressKey(Key.Enter);
            await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is SignInDialogViewModel { Provider: HostingProvider.AzureDevOps }, "Azure DevOps' sign-in dialog");
            var dialog = (SignInDialogViewModel)app.Session.Services.DialogHost.Current!;
            await app.WaitForAsync(() => dialog.Opened.IsCompleted && app.FocusedAutomationId() == "SignInOrganization", "the focus in the organization box");
            Assert.Equal(PartA.AzureDevOpsDialogTitle, dialog.Title);
            Assert.Equal(string.Empty, dialog.Organization);
            Assert.False(app.FindByAutomationId("SignInTokenLink").IsEffectivelyEnabled, "The token link is enabled without an organization.");
            Assert.False(app.FindByAutomationId("ConfirmButton").IsEffectivelyEnabled, "Sign in is enabled.");
            Assert.Single(app.Session.Shell.Tabs);
            Assert.True(Welcome(app).IsCloneFormOpen, "The form is not open behind the dialog.");
            Assert.DoesNotContain(app.Session.Services.DialogHost.Open, open => open is SettingsDialogViewModel);
            Assert.Null(app.Session.Shell.OpenPalette);
            AssertNoRequests(served, mark);
            Assert.Empty(StoreCallsSince(app, stored));
            ShotB(app, PartB.B08SignInFirst);

            // B8 step 5: the sign-in, then the browser on Azure DevOps with the switch.
            app.TypeOrganization(PartA.Organization);
            app.Click(app.FindByAutomationId("SignInToken"));
            app.TypeToken(AzureDevOpsToken);
            await app.ConfirmSignInAsync();
            browser = await WaitForBrowserAsync(app, loaded: true);
            await WaitForAzureAsync(app, served, mark, PartB.AzureDevOpsListRoute);
            Assert.Equal(HostingProvider.AzureDevOps, browser.Provider);
            var requests = AzureSince(served, mark);
            Assert.Equal(["connection:fernhill-labs", "areas", PartB.AzureDevOpsListRoute], requests.Select(request => request.Route));
            Assert.All(requests, request => Assert.Equal((FakeAuthorizationForm.BasicEmptyUser, 200), (request.Authorization, request.Status)));
            Assert.Equal(AdaAzureLogin(served), requests[0].Login);
            Assert.Empty(GitLabSince(served, mark));
            Assert.Empty(GitHubSince(served, mark));
            Assert.Equal(["save " + PartA.AzureDevOpsKey(served.AzureDevOps.Port)], StoreCallsSince(app, stored));
            Assert.Equal((AccountState.SignedIn, TokenPlace.Keychain), (session.AccountOf(HostingProvider.AzureDevOps)?.State, session.AccountOf(HostingProvider.AzureDevOps)?.Place));
            Assert.Equal([("gitlab", "keychain", true), ("azure-devops", "keychain", true)], Records(data).OrderBy(record => record.Provider == "azure-devops"));
            AssertBrowser(app, PartB.AzureDevOpsTitle, string.Empty, chooseEnabled: false, switchTexts: [PartB.GitLabName, PartB.AzureDevOpsName], selected: PartB.AzureDevOpsName);
            AssertRows(app, PartB.AzureDevOpsRows, cut: PartB.AzureDevOpsLongName);
            Assert.Single(app.Session.Shell.Tabs);
            Assert.Equal(PartB.FromAnAccount, AutomationProperties.GetName(BrowseButton(app)));
            Assert.DoesNotContain(app.Session.Services.DialogHost.Open, open => open is SettingsDialogViewModel);
            ShotB(app, PartB.B08Browser);

            // B8 step 6: GitHub's, signed out and cancelled: nothing more.
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => Browser(app) is null, "the browser to close");
            mark = Mark(served);
            stored = app.Tokens.Calls.Count;
            app.PressPalette();
            app.TypeInPalette(PartB.GitHubQuery);
            app.PressKey(Key.Enter);
            await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is SignInDialogViewModel { Provider: HostingProvider.GitHub }, "GitHub's sign-in dialog");
            var gitHub = (SignInDialogViewModel)app.Session.Services.DialogHost.Current!;
            await app.WaitForAsync(() => gitHub.Opened.IsCompleted && app.FocusedAutomationId() == "SignInToken", "the focus in the token box");
            Assert.Equal(PartA.GitHubDialogTitle, gitHub.Title);
            app.Click(app.FindByAutomationId("CancelButton"));
            await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is null, "no dialog open");
            Assert.Null(session.AccountOf(HostingProvider.GitHub));
            Assert.Single(app.Session.Shell.Tabs);
            Assert.True(Welcome(app).IsCloneFormOpen, "The form closed.");
            Assert.Null(Browser(app));
            AssertNoRequests(served, mark);
            Assert.Empty(StoreCallsSince(app, stored));
            Assert.Equal(2, Records(data).Count);
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd(app, data, served);
    }

    /// <summary>The texts the clone form shows, which phase 1's <c>13a</c> shows too.</summary>
    private static void AssertFormTexts(ShellDriver app)
    {
        var texts = TextsIn(app.Find<Control>("CloneForm"));
        foreach (var text in new[] { "Clone a repository", "Repository URL", "Parent folder", "Browse", "Folder name", "Clone" })
        {
            Assert.Contains(text, texts);
        }
    }
}

