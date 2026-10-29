using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// Part B, browse and clone (docs/test-reports/phase-7a.md, checks B1 to B9; design 9.7): the clone
/// form's "From GitHub…", the repository browser loading, listed, filtered and failed, the 1,000 cap
/// and single sign-on's line, the palette's "Clone from GitHub…" signed out, a cancelled sign-in, and
/// a clone of the private octo/hello through the app's helper. One method per app start, as the
/// report's table of part B's methods lists them. No picture shows an opened GitHub repository.
/// </summary>
public sealed partial class Phase7aChecksB : Phase7aChecks
{
    /// <summary>Method 1: B1, B2 and B4 on one empty tab, signed in through the session after the form opens.</summary>
    [AvaloniaFact]
    public async Task Check_B1_B2_B4_form_loading_and_filter()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a b1");
        var port = served.GitHub.Port;
        var app = StartEmpty(data, served);
        FakeGitHubHold? hold = null;
        try
        {
            // B1 step 1: the form signed out, the button collapsed (D261).
            var requests = served.GitHub.Requests.Count;
            var stored = app.Tokens.Calls.Count;
            await OpenFormAsync(app);
            AssertForm(app, button: false);
            Assert.Equal("CloneUrlBox", app.FocusedAutomationId());
            var session = app.Session.Services.Hosting;
            Assert.False(session.IsOff);
            Assert.Null(session.Account);
            Assert.Empty(RequestsSince(served, requests));
            Assert.Empty(StoreCallsSince(app, stored));

            // B1 step 2: signed in, the button at the URL row's end.
            await app.SignInAsync(OctoToken, Cancelled);
            await app.WaitForAsync(() => GitHubButton(app).IsEffectivelyVisible, "From GitHub… to show");
            AssertUserRequest(Assert.Single(RequestsSince(served, requests)));
            Assert.Equal(["probe", $"save {PartB.OctoKey(port)}"], StoreCallsSince(app, stored));
            Assert.Equal(AccountState.SignedIn, session.Account?.State);
            Assert.True(session.Account?.Account.UseForGit);
            Assert.True(session.BindsGit);
            AssertForm(app, button: true);
            AssertFormTexts(app);
            Assert.Equal("CloneUrlBox", app.FocusedAutomationId());
            ShotB(app, PartB.B01aFormSignedIn);

            // B2 step 3: the browser loading, its list held at the fake.
            requests = served.GitHub.Requests.Count;
            stored = app.Tokens.Calls.Count;
            hold = served.GitHub.HoldNext(PartB.ListRoute);
            app.Click(GitHubButton(app));
            await hold.Arrived.WaitAsync(ShellDriver.DefaultWaitTimeout, Cancelled);
            var browser = await WaitForBrowserAsync(app, loaded: false);
            Assert.False(browser.Load.IsCompleted);
            AssertBrowser(app, string.Empty, chooseEnabled: false, status: PartB.Loading);
            Assert.Empty(browser.Rows);
            Assert.Equal(PartB.ListMinHeight, ListHeight(app), PartB.Tolerance);
            Assert.Empty(RequestsSince(served, requests));
            Assert.Empty(StoreCallsSince(app, stored));
            ShotB(app, PartB.B02aLoading);

            // B4 step 4: a filter typed while loading.
            app.Type("private");
            app.Settle();
            AssertBrowser(app, "private", chooseEnabled: false, status: PartB.Loading);
            Assert.Empty(browser.Rows);

            // B4 step 5: the rows come, filtered.
            hold.Dispose();
            hold = null;
            await app.WaitForAsync(() => browser.Load.IsCompleted, "the browser's load to end");
            await WaitForRequestsAsync(app, served, requests, PartB.ListRoute);
            AssertListRequest(Assert.Single(RequestsSince(served, requests)), 200);
            AssertBrowser(app, "private", chooseEnabled: false);
            AssertRows(app, [PartB.Rows[0], PartB.Rows[2]]);
            Assert.Equal(PartB.ListMinHeight, ListHeight(app), PartB.Tolerance);
            ShotB(app, PartB.B04aFiltered);

            // B4 step 6: a selection the next filter hides is cleared.
            app.Click(Item(app, OctoNotes));
            app.Settle();
            Assert.Equal(OctoNotes, browser.Selected?.FullName);
            Assert.Equal(PaletteOf(app).Selection, BackgroundOf(Item(app, OctoNotes)));
            Assert.True(InBrowser(app, "ConfirmButton").IsEffectivelyEnabled, "Choose is disabled with a row selected.");
            app.Click(InBrowser(app, "CloneFromGitHubFilter"));
            PressSelectAll(app);
            app.Type("tool");
            app.Settle();
            AssertRows(app, [PartB.Rows[1]]);
            Assert.Null(browser.Selected);
            Assert.False(InBrowser(app, "ConfirmButton").IsEffectivelyEnabled);

            // B4 step 7: emptied, the four rows in their order.
            PressSelectAll(app);
            app.PressKey(Key.Back);
            app.Settle();
            AssertBrowser(app, string.Empty, chooseEnabled: false);
            AssertRows(app, PartB.Rows);

            // B4 step 8: Esc leaves the form open, nothing chosen.
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => browser.IsClosed, "the browser to close");
            AssertForm(app, button: true);
            Assert.Null(app.Session.Services.DialogHost.Current);
            AssertListRequest(Assert.Single(RequestsSince(served, requests)), 200);
            Assert.Empty(StoreCallsSince(app, stored));
        }
        finally
        {
            hold?.Dispose();
            app.Dispose();
        }

        AssertCleanEnd(app, data, served);
    }

    /// <summary>Method 2: B7, B3 and B6 on one empty tab, signed in through the palette's dialog; then the clone of octo/hello.</summary>
    [AvaloniaFact]
    public async Task Check_B3_B6_B7_palette_sign_in_listed_and_clone()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a b3");
        using var clones = new TempDirectory("clones");
        var port = served.GitHub.Port;
        var app = StartEmpty(data, served);
        string clone;
        try
        {
            // B7 step 1.
            var requests = served.GitHub.Requests.Count;
            var stored = app.Tokens.Calls.Count;
            var tab = app.Session.Shell.ActiveTab;

            // B7 step 2: the command right after "Clone repository…", without a key.
            app.PressPalette();
            var labels = app.Palette.Shown.Select(entry => entry.Label).ToList();
            var at = labels.IndexOf(PartB.CloneRepositoryCommand);
            Assert.True(at >= 0, string.Join(" | ", labels));
            Assert.Equal([PartB.CloneRepositoryCommand, PartB.PaletteCommand, PartB.InitRepositoryCommand], labels.Skip(at).Take(3));
            var command = app.Palette.Shown[at + 1];
            Assert.True(command.IsEnabled);
            Assert.Null(command.Detail);
            Assert.Null(command.KeyText);
            app.TypeInPalette(PartB.PaletteQuery);
            var snapshot = app.PaletteSnapshot();
            var row = Assert.Single(snapshot.Rows);
            Assert.Equal((PartB.PaletteCommand, (string?)null, (string?)null, true, true), (row.Label, row.Detail, row.KeyText, row.IsEnabled, row.IsHighlighted));
            var (cardX, cardY, cardWidth, cardHeight) = PartB.PaletteCardOneRow;
            Assert.Equal(cardX, snapshot.CardBounds.X, PartB.Tolerance);
            Assert.Equal(cardY, snapshot.CardBounds.Y, PartB.Tolerance);
            Assert.Equal(cardWidth, snapshot.CardBounds.Width, PartB.Tolerance);
            Assert.Equal(cardHeight, snapshot.CardBounds.Height, PartB.Tolerance);
            Assert.Empty(RequestsSince(served, requests));
            Assert.Empty(StoreCallsSince(app, stored));
            ShotB(app, PartB.B07aPaletteCommand);

            // B7 step 3: "Add GitHub account" first, over the welcome page without the form.
            app.PressKey(Key.Enter);
            var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
            var opened = dialog.Opened;
            await app.WaitForAsync(() => opened.IsCompleted, "the sign-in dialog's question to the keychain");
            await app.WaitForAsync(() => app.FocusedAutomationId() == "SignInToken", "the focus in the token box");
            app.Settle();
            Assert.False(app.IsPaletteOpen);
            var signIn = app.SignInDialogView();
            Assert.Equal(HostingAddAccountTitle, ((TextBlock)app.FindByAutomationId(signIn, "DialogTitle")).Text);
            Assert.Equal(string.Empty, ((TextBox)app.FindByAutomationId(signIn, "SignInToken")).Text ?? string.Empty);
            Assert.False(app.FindByAutomationId(signIn, "SignInNoKeychain").IsEffectivelyVisible);
            Assert.True(app.FindByAutomationId(signIn, "CancelButton").IsEffectivelyEnabled);
            Assert.False(app.FindByAutomationId(signIn, "ConfirmButton").IsEffectivelyEnabled);
            Assert.Same(tab, Assert.Single(app.Session.Shell.Tabs));
            Assert.False(tab.Welcome.IsCloneFormOpen);
            Assert.Equal(["OpenRepoButton", "CloneRepoButton", "InitRepoButton"], new[] { "OpenRepoButton", "CloneRepoButton", "InitRepoButton" }.Where(id => app.Find<Button>(id).IsEffectivelyVisible));
            Assert.Null(app.SettingsDialog);
            Assert.Equal(["probe"], StoreCallsSince(app, stored));
            Assert.Empty(RequestsSince(served, requests));
            ShotB(app, PartB.B07bSignInFirst);

            // B7 step 4: the typed token signs in.
            app.TypeToken(OctoToken);
            await app.ConfirmSignInAsync();
            Assert.True(dialog.IsClosed, "The sign-in dialog did not close.");
            var since = RequestsSince(served, requests);
            Assert.NotEmpty(since);
            AssertUserRequest(since[0]);
            Assert.Single(since, request => request.Route == "user");
            Assert.Equal(["probe", $"save {PartB.OctoKey(port)}"], StoreCallsSince(app, stored));
            var session = app.Session.Services.Hosting;
            Assert.Equal((AccountState.SignedIn, TokenPlace.Keychain, AccountNoteKind.None), (session.Account?.State, session.Account?.Place, session.Account?.NoteKind));
            AssertRecord(data, served, "keychain");
            Assert.Same(tab, Assert.Single(app.Session.Shell.Tabs));
            Assert.Null(app.SettingsDialog);

            // B3 step 5: the browser listed, in the state the palette's path reaches.
            var browser = await WaitForBrowserAsync(app, loaded: true);
            await WaitForRequestsAsync(app, served, requests, PartB.ListRoute);
            AssertBrowser(app, string.Empty, chooseEnabled: false);
            AssertRows(app, PartB.Rows);
            Assert.False(ListScrolls(app), "The list of four rows scrolls.");
            since = RequestsSince(served, requests);
            Assert.Equal(["user", PartB.ListRoute], since.Select(request => request.Route));
            AssertUserRequest(since[0]);
            AssertListRequest(since[1], 200);
            Assert.Same(tab, Assert.Single(app.Session.Shell.Tabs));
            Assert.Equal("New tab", tab.Title);
            AssertForm(app, button: true);
            Assert.Null(app.SettingsDialog);
            ShotB(app, PartB.B03aListed);

            // B6 step 6: Choose fills the form; choosing runs no git and asks GitHub nothing.
            requests = served.GitHub.Requests.Count;
            var quiet = await app.QuietCallMarkAsync();
            app.Click(Item(app, OctoHello));
            app.Settle();
            Assert.Equal(OctoHello, browser.Selected?.FullName);
            Assert.True(InBrowser(app, "ConfirmButton").IsEffectivelyEnabled, "Choose is disabled with a row selected.");
            app.Click(InBrowser(app, "ConfirmButton"));
            await app.WaitForAsync(() => browser.IsClosed, "the browser to close");
            AssertForm(app, button: true, url: PartB.HelloCloneUrl(port), name: PartB.HelloFolder);
            Assert.Empty(RequestsSince(served, requests));
            Assert.Empty(await app.QuietCallsSinceAsync(quiet));
            ShotB(app, PartB.B06aChosen);

            // B6 step 7: the parent folder typed, as the real window does.
            app.Click(Box(app, "CloneParentBox"));
            app.Type(clones.Path);
            app.Settle();

            // B6 step 8: Clone, through the app's helper, with no dialog.
            requests = served.GitHub.Requests.Count;
            var gitRequests = served.GitHub.GitRequests.Count;
            stored = app.Tokens.Calls.Count;
            var log = app.LogMark();
            quiet = await app.QuietCallMarkAsync();
            app.Click(app.Find<Button>("CloneStartButton"));
            var repository = await app.WaitForRepositoryAsync();
            await app.WaitForAsync(() => app.LogLinesSince(log).Any(line => line.Contains(PartB.HelloListed, StringComparison.Ordinal)), "hello's pull requests listed");
            await app.WaitForWatchersQuietAsync();

            var lines = LogSince(app, log);
            Assert.DoesNotContain(lines, line => line.Message.Contains("hello: Clone: git asks (", StringComparison.Ordinal));
            Assert.Null(app.Session.Services.DialogHost.Current);
            AssertCalledInOrder(app, quiet, PartB.ConfigRead, PartB.CloneCommand(port));
            var calls = app.GitCalls().Where(call => call.Sequence > quiet).ToList();
            Assert.True(SameFolder(calls.First(call => call.CommandText == PartB.ConfigRead).WorkingDirectory, data.Path), "The configuration read did not run in the data folder.");
            Assert.True(SameFolder(calls.First(call => call.CommandText == PartB.CloneCommand(port)).WorkingDirectory, clones.Path), "The clone did not run in the parent folder.");
            Assert.Contains(lines, line => line.Message == PartB.AnsweredFromTheAccount(port));
            Assert.DoesNotContain(lines, line => line.Message.StartsWith("hello: Clone: git's credential", StringComparison.Ordinal)
                && (line.Message.EndsWith("is not bound.", StringComparison.Ordinal) || line.Message.EndsWith("names another user.", StringComparison.Ordinal) || line.Message.EndsWith("has no token.", StringComparison.Ordinal)));
            Assert.DoesNotContain(lines, line => line.Message.Contains("refused the account's token", StringComparison.Ordinal));
            Assert.DoesNotContain(lines, line => line.Message.Contains(PartB.ConfigNotRead, StringComparison.Ordinal));

            var git = served.GitHub.GitRequests.Skip(gitRequests).Where(request => request.Path.StartsWith("/octo/hello.git/", StringComparison.Ordinal)).ToList();
            Assert.NotEmpty(git);
            Assert.True(git[0] is { GivenUserName: null, Status: 401 }, $"The clone's first request was {git[0]}.");
            Assert.Contains(git, request => request is { GivenUserName: "x-access-token", Status: 200 });
            Assert.DoesNotContain(git, request => request.GivenUserName is not null && request.GivenUserName != "x-access-token");
            Assert.DoesNotContain(git, request => request is { GivenUserName: not null, Status: 401 });

            await app.WaitForAsync(() => RequestsSince(served, requests).Any(request => request.Route == "repo:octo/hello/pulls" || request.Path == "/api/v3/repos/octo/hello/pulls"), "hello's pull requests requested");
            since = RequestsSince(served, requests);
            Assert.DoesNotContain(since, request => request.Route is "user" or PartB.ListRoute);
            foreach (var path in new[] { "/api/v3/repos/octo/hello", "/api/v3/repos/octo/hello/pulls" })
            {
                Assert.True(since.Any(request => request is { Method: "GET", TokenPresent: true, Status: 200 } && request.Path == path), $"No GET {path} with the token answered 200.");
            }

            Assert.DoesNotContain(since, request => request.Status == 401);
            Assert.Empty(StoreCallsSince(app, stored));

            clone = Path.Combine(clones.Path, PartB.HelloFolder);
            Assert.Same(tab, Assert.Single(app.Session.Shell.Tabs));
            Assert.True(SameFolder(repository.Path, clone), $"The tab shows {repository.Path}.");
            Assert.False(tab.Welcome.IsCloneFormOpen);
            Assert.Contains(app.Session.Services.Recent.Items, item => SameFolder(item.Path, clone));
            var binding = Assert.Single(repository.Hosting.Bindings);
            Assert.Equal((PartB.HelloCloneUrl(port), PartB.OctoAccountId(port)), (binding.Url, binding.Account.Id));
            Assert.Equal((AccountState.SignedIn, TokenPlace.Keychain, AccountNoteKind.None), (session.Account?.State, session.Account?.Place, session.Account?.NoteKind));
        }
        finally
        {
            app.Dispose();
        }

        // B6 step 9: the clone holds the plain URL and no credential.
        using var home = new TempDirectory("phase7a b6 home");
        var runner = new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem()) ?? throw new InvalidOperationException("The checks need git."));
        async Task<GitResult> GitAsync(string folder, params string[] arguments) =>
            await runner.RunAsync(new GitCommand(arguments) { WorkingDirectory = folder, Environment = TempRepo.IsolatedEnvironment(home.Path) }, Cancelled);

        Assert.Equal(PartB.HelloCloneUrl(port), (await GitAsync(clone, "config", "--get", "remote.origin.url")).StandardOutput.Trim());
        var credential = await GitAsync(clone, "config", "--get-regexp", "^credential");
        Assert.Equal((1, string.Empty), (credential.ExitCode, credential.StandardOutput.Trim()));
        var local = (await GitAsync(clone, "config", "--local", "--list")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("core.", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(
            [
                $"remote.origin.url={PartB.HelloCloneUrl(port)}",
                "remote.origin.fetch=+refs/heads/*:refs/remotes/origin/*",
                "branch.main.remote=origin",
                "branch.main.merge=refs/heads/main",
            ],
            local);
        Assert.Equal("refs/heads/main", (await GitAsync(clone, "symbolic-ref", "HEAD")).StandardOutput.Trim());
        var head = (await GitAsync(clone, "rev-parse", "HEAD")).StandardOutput.Trim();
        Assert.Equal((await GitAsync(served.Hello.Path, "rev-parse", "origin/main")).StandardOutput.Trim(), head);
        Assert.Equal(PartB.HelloMainSubject, (await GitAsync(clone, "log", "-1", "--format=%s")).StandardOutput.Trim());
        Assert.Equal(string.Empty, (await GitAsync(clone, "status", "--porcelain")).StandardOutput.Trim());
        AssertRecord(data, served, "keychain");
        AssertCleanEnd(app, data, served, clones.Path);
    }

    /// <summary>Method 3: B9 and B5 on one empty tab with 1,001 more repositories of octo, signed in through the session.</summary>
    [AvaloniaFact]
    public async Task Check_B5_B9_cap_single_sign_on_and_failures()
    {
        await using var served = await ServeAsync();
        served.GitHub.AddListedRepositories(OctoLogin, PartB.ListedAdded);
        using var data = new TempDirectory("phase7a b5");
        var port = served.GitHub.Port;
        var app = StartEmpty(data, served);
        try
        {
            // B9 step 1.
            await app.SignInAsync(OctoToken, Cancelled);
            await OpenFormAsync(app);
            await app.WaitForAsync(() => GitHubButton(app).IsEffectivelyVisible, "From GitHub… to show");

            // B9 step 2: ten pages, the cap's and single sign-on's lines.
            var requests = served.GitHub.Requests.Count;
            served.GitHub.FailNext(PartB.ListRoute, FakeGitHubFault.SsoPartialResults);
            app.Click(GitHubButton(app));
            var browser = await WaitForBrowserAsync(app, loaded: true);
            await WaitForRequestsAsync(app, served, requests, PartB.ListRoute, 10);
            var pages = RequestsSince(served, requests);
            Assert.Equal(10, pages.Count);
            AssertListRequest(pages[0], 200);
            for (var page = 2; page <= 10; page++)
            {
                AssertListRequest(pages[page - 1], 200, query: null);
                var query = pages[page - 1].Query.TrimStart('?').Split('&');
                Assert.Contains($"page={page}", query);
                Assert.Contains("per_page=100", query);
                Assert.Contains("sort=updated", query);
            }

            Assert.Equal(PartB.ListedCap, browser.Rows.Count);
            Assert.Equal(PartB.Rows, browser.Rows.Take(4).Select(row => (row.FullName, row.Detail)));
            Assert.Equal(
                Enumerable.Range(1, PartB.ListedCap - 4).Select(index => (PartB.Listed(index), (string?)null)),
                browser.Rows.Skip(4).Select(row => (row.FullName, row.Detail)));
            AssertBrowser(app, string.Empty, chooseEnabled: false, more: PartB.MoreExist, sso: PartB.SomeHiddenBySso);
            Assert.Equal(PartB.ListMaxHeight, ListHeight(app), PartB.Tolerance);
            Assert.True(ListScrolls(app), "The list of 1,000 rows does not scroll.");
            var scroll = InBrowser(app, "CloneFromGitHubList").GetVisualDescendants().OfType<ScrollViewer>().First();
            Assert.Equal(0, scroll.Offset.Y);
            Assert.Equal(Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, ScrollViewer.GetHorizontalScrollBarVisibility(InBrowser(app, "CloneFromGitHubList")));
            var list = app.BoundsOf(InBrowser(app, "CloneFromGitHubList"));
            var whole = Items(app).Where(item => app.BoundsOf(item).Top >= list.Top - 0.5 && app.BoundsOf(item).Bottom <= list.Bottom + 0.5).ToList();
            Assert.Equal(
                [OctoHello, OctoTool, OctoNotes, OctoWebsite, PartB.Listed(1), PartB.Listed(2), PartB.Listed(3), PartB.Listed(4)],
                whole.Select(item => AutomationProperties.GetName(item)));
            Assert.Null(browser.Selected);
            ShotB(app, PartB.B09aCapAndSso);

            // B9 step 3.
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => browser.IsClosed, "the browser to close");
            AssertForm(app, button: true);

            // B5 step 4: a server error in Describe's words; the account kept.
            requests = served.GitHub.Requests.Count;
            var stored = app.Tokens.Calls.Count;
            var log = app.LogMark();
            served.GitHub.FailNext(PartB.ListRoute, FakeGitHubFault.ServerError);
            app.Click(GitHubButton(app));
            browser = await WaitForBrowserAsync(app, loaded: true);
            await WaitForRequestsAsync(app, served, requests, PartB.ListRoute);
            AssertListRequest(Assert.Single(RequestsSince(served, requests)), 500);
            AssertBrowser(app, string.Empty, chooseEnabled: false, error: PartB.ServerErrorText);
            Assert.Empty(browser.Rows);
            Assert.Equal(PartB.ListMinHeight, ListHeight(app), PartB.Tolerance);
            Assert.Equal(AccountState.SignedIn, app.Session.Services.Hosting.Account?.State);
            Assert.Empty(StoreCallsSince(app, stored));
            var lines = LogSince(app, log);
            Assert.Single(lines, line => line.Message.StartsWith(PartB.ListFailedPrefix, StringComparison.Ordinal));
            var id = RequestIdOf(lines, PartB.ListFailedPrefix);
            AssertWarningOnce(lines, Format(PartB.ListFailedServerError, id));
            AssertWarningOnce(lines, Format(PartB.ClientFailed, id, 500));
            AssertWarningOnce(lines, Format(PartB.SessionListFailedServerError, id));
            AssertForm(app, button: true);
            ShotB(app, PartB.B05aFailed);

            // B5 step 5: the API's refusal marks the account at once (D330) and hides the button (D302).
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => browser.IsClosed, "the browser to close");
            requests = served.GitHub.Requests.Count;
            stored = app.Tokens.Calls.Count;
            log = app.LogMark();
            served.GitHub.FailNext(PartB.ListRoute, FakeGitHubFault.BadCredentials);
            app.Click(GitHubButton(app));
            browser = await WaitForBrowserAsync(app, loaded: true);
            await app.WaitForAccountAsync(account => account?.State == AccountState.NeedsSignIn);
            await app.WaitForAsync(() => Record(data) is { } record && record.GetProperty("tokenState").GetString() == "refused", "accounts.json to show the token refused");
            await app.WaitForAsync(() => !GitHubButton(app).IsEffectivelyVisible, "From GitHub… to go");
            await WaitForRequestsAsync(app, served, requests, PartB.ListRoute);
            AssertListRequest(Assert.Single(RequestsSince(served, requests)), 401);
            AssertBrowser(app, string.Empty, chooseEnabled: false, error: PartB.RefusedOcto);
            Assert.Empty(browser.Rows);
            var account = app.Session.Services.Hosting.Account;
            Assert.Equal((AccountState.NeedsSignIn, AccountNoteKind.Refused, OctoLogin), (account?.State, account?.NoteKind, account?.Account.Login));
            AssertRecord(data, served, "refused");
            Assert.Empty(StoreCallsSince(app, stored));
            Assert.True(app.Tokens.Holds(new VisualCommit.Hosting.Tokens.TokenKey("github", PartB.Host(port), OctoLogin)), "The keychain's entry went.");
            lines = LogSince(app, log);
            Assert.Single(lines, line => line.Message.StartsWith(PartB.ListFailedPrefix, StringComparison.Ordinal));
            id = RequestIdOf(lines, PartB.ListFailedPrefix);
            AssertWarningOnce(lines, Format(PartB.ListFailedBadCredentials, id));
            AssertWarningOnce(lines, Format(PartB.ClientFailed, id, 401));
            AssertWarningOnce(lines, Format(PartB.SessionRefusedOcto, id, PartB.Host(port)));
            Assert.DoesNotContain(lines, line => line.Message.StartsWith(PartB.SessionListFailedPrefix, StringComparison.Ordinal));
            AssertForm(app, button: false);
            ShotB(app, PartB.B05bRefused);

            // B5 step 6.
            requests = served.GitHub.Requests.Count;
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => browser.IsClosed, "the browser to close");
            AssertForm(app, button: false);
            Assert.Empty(RequestsSince(served, requests));
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd(app, data, served);
    }

    /// <summary>Method 4: B8 from a tab on hello, signed out: the cancelled sign-in, then a sign-in that opens a new tab.</summary>
    [AvaloniaFact]
    public async Task Check_B8_cancelled_sign_in_and_a_new_tab()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a b8");
        var port = served.GitHub.Port;
        var app = ShellDriver.StartWithTabs(data.Path, [served.Hello.Path], activeTab: 0, gitHub: served.GitHub);
        try
        {
            // B8 step 1.
            var hello = await app.WaitForRepositoryAsync();
            await app.WaitForWatchersQuietAsync();
            var requests = served.GitHub.Requests.Count;
            var stored = app.Tokens.Calls.Count;
            var helloTab = Assert.Single(app.Session.Shell.Tabs);
            Assert.Null(app.Session.Services.Hosting.Account);
            Assert.Empty(served.GitHub.Requests);

            // B8 step 2.
            app.PressPalette();
            app.TypeInPalette(PartB.PaletteQuery);
            var first = app.PaletteSnapshot().Rows[0];
            Assert.Equal((PartB.PaletteCommand, true), (first.Label, first.IsHighlighted));
            app.PressKey(Key.Enter);
            var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
            await app.WaitForAsync(() => dialog.Opened.IsCompleted, "the sign-in dialog's question to the keychain");
            Assert.Equal(["probe"], StoreCallsSince(app, stored));
            Assert.Empty(RequestsSince(served, requests));

            // B8 step 3: Cancel changes nothing (D300).
            app.Click(app.FindByAutomationId(app.SignInDialogView(), "CancelButton"));
            await app.WaitForAsync(() => dialog.IsClosed, "the sign-in dialog to close");
            await AssertNoDialogAsync(app);
            Assert.Null(app.Session.Services.Hosting.Account);
            Assert.False(File.Exists(AccountsFile(data)));
            Assert.Same(helloTab, Assert.Single(app.Session.Shell.Tabs));
            Assert.Same(helloTab, app.Session.Shell.ActiveTab);
            Assert.Same(hello, helloTab.Repository);
            Assert.Empty(RequestsSince(served, requests));
            Assert.Equal(["probe"], StoreCallsSince(app, stored));

            // B8 step 4: signed in from hello's tab, the form opens on a new tab at the end.
            requests = served.GitHub.Requests.Count;
            stored = app.Tokens.Calls.Count;
            var log = app.LogMark();
            app.PressPalette();
            app.TypeInPalette(PartB.PaletteQuery);
            app.PressKey(Key.Enter);
            dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
            await app.WaitForAsync(() => dialog.Opened.IsCompleted, "the sign-in dialog's question to the keychain");
            await app.WaitForAsync(() => app.FocusedAutomationId() == "SignInToken", "the focus in the token box");
            app.TypeToken(OctoToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            var browser = await WaitForBrowserAsync(app, loaded: true);
            await app.WaitForAsync(() => app.LogLinesSince(log).Any(line => line.Contains(PartB.HelloListed, StringComparison.Ordinal)), "hello's pull requests listed");
            await app.WaitForWatchersQuietAsync();
            await WaitForRequestsAsync(app, served, requests, PartB.ListRoute);
            await app.WaitForAsync(() => RequestsSince(served, requests).Any(request => request.Path == "/api/v3/repos/octo/hello/pulls"), "hello's pull requests requested");

            var since = RequestsSince(served, requests);
            AssertUserRequest(since[0]);
            Assert.Single(since, request => request.Route == "user");
            AssertListRequest(Assert.Single(since, request => request.Route == PartB.ListRoute), 200);
            foreach (var path in new[] { "/api/v3/repos/octo/hello", "/api/v3/repos/octo/hello/pulls" })
            {
                Assert.True(since.Any(request => request is { Method: "GET", TokenPresent: true, Status: 200 } && request.Path == path), $"No GET {path} with the token answered 200.");
            }

            Assert.DoesNotContain(since, request => request.Status == 401);
            Assert.Equal([$"save {PartB.OctoKey(port)}"], StoreCallsSince(app, stored));
            var tabs = app.Session.Shell.Tabs;
            Assert.Equal(2, tabs.Count);
            Assert.Same(helloTab, tabs[0]);
            var newTab = tabs[1];
            Assert.Same(newTab, app.Session.Shell.ActiveTab);
            Assert.True(newTab.IsEmpty);
            Assert.Same(hello, helloTab.Repository);
            Assert.True(SameFolder(hello.Path, served.Hello.Path));
            AssertForm(app, button: true);
            AssertBrowser(app, string.Empty, chooseEnabled: false);
            AssertRows(app, PartB.Rows);
            Assert.Same(browser, Browser(app));

            // B8 step 5: Esc on the browser leaves the form open.
            requests = served.GitHub.Requests.Count;
            stored = app.Tokens.Calls.Count;
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => browser.IsClosed, "the browser to close");
            AssertForm(app, button: true);
            Assert.Equal(2, app.Session.Shell.Tabs.Count);
            Assert.Same(newTab, app.Session.Shell.ActiveTab);
            Assert.Empty(RequestsSince(served, requests));
            Assert.Empty(StoreCallsSince(app, stored));
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd(app, data, served);
    }

    /// <summary>"Add GitHub account" (design 1.7's <c>HostingTexts.AddAccountTitle</c>, part A's texts).</summary>
    private const string HostingAddAccountTitle = "Add GitHub account";

    private static bool SameFolder(string? a, string? b) =>
        a is not null && b is not null
        && string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
}
