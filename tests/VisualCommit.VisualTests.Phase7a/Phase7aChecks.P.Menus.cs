using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using P = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartP;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>Part P's method 2 (docs/test-reports/phase-7a.md, P6, P11, P14, P15, P9, P10, P8, P12): rows, menus and selection on hello, signed in through the Accounts screen.</summary>
public sealed partial class Phase7aChecksP
{
    [AvaloniaFact]
    public async Task Check_P6_P11_P14_P15_P9_P10_P8_P12_rows_menus_and_selection()
    {
        // P6 step 1: signed in as the real window does (open point 30): the dialog's check ends
        // before Esc, and the screen has closed before the wait for the list.
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a p6");
        var port = served.GitHub.Port;
        var app = await OpenAsync(data, served, signIn: false);
        try
        {
            var hello = app.Session.Shell.ActiveTab.Repository!;
            var operations = StartedOperations(app);
            var requests = served.GitHub.Requests.Count;
            var logMark = app.LogMark();
            var screen = await app.OpenAccountsAsync();
            var dialog = await app.OpenSignInDialogAsync();
            app.TypeToken(served.OctoToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            Assert.True(dialog.IsClosed, "The sign-in dialog did not close after the sign-in.");
            await app.WaitForAccountAsync(account => account is { State: AccountState.SignedIn });
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
            await WaitForListedAsync(app, "hello", 4, "octo/hello", logMark);
            await AssertNoDialogAsync(app);
            ShotP(app, P.P06a);
            var api = ApiSince(served, requests);
            Assert.Equal(["user 200", "repo:octo/hello 200", "pulls:octo/hello 200"], api.Select(request => $"{request.Route} {request.Status}"));
            Assert.All(api, request => Assert.Equal(("GET", true, "octo"), (request.Method, request.TokenPresent, request.Login)));
            AssertPullsRequest(api[2], 200);
            Assert.Equal(AccountState.SignedIn, app.Session.Services.Hosting.Account?.State);
            Assert.Equal(PullRequestsState.Loaded, hello.HostingState.PullRequests);
            Assert.Equal([5, 3, 2, 1], hello.HostingState.List!.PullRequests.Select(pull => pull.Number));
            Assert.False(hello.HostingState.List.MoreExist);
            Assert.Equal(
                P.HelloByRef.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                hello.HostingState.ByRef.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Number)).OrderBy(pair => pair.Key, StringComparer.Ordinal));
            Assert.Equal(["v Pull requests 4", .. P.HelloRows.Select(row => row.Name)], Section(app));
            Assert.Equal(P.HelloRows.Select(row => (row.Author, row.Draft)), PullRequestRows(app).Select(row => (row.Author, row.IsDraft)));
            Assert.Equal(P.HelloRows.Select(row => P.HelloToolTips[int.Parse(row.Name[1..row.Name.IndexOf(' ', StringComparison.Ordinal)], System.Globalization.CultureInfo.InvariantCulture)]), PullRequestRows(app).Select(row => row.ToolTip));
            Assert.DoesNotContain(PullRequestRows(app), row => row.IsSelected);
            AssertHelloRowControls(app);

            // P11 step 2: the section's menu.
            requests = served.GitHub.Requests.Count;
            var header = await HeaderAsync(app);
            var pointer = app.BoundsOf(header).Center;
            app.RightClick(header);
            var section = app.Menu();
            AssertMenu(section, P.SectionMenu);
            AssertHighlighted(section, P.RefreshPullRequests);
            AssertMenuShape(app, section);
            AssertMenuHeight(section, P.SectionMenuHeight);
            AssertMenuAtPointer(section, pointer);
            ShotP(app, P.P11c, keepPointer: true);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            Assert.Empty(ApiSince(served, requests));

            // P11 step 3 and P14 step 4: #1's menu, then Copy link.
            var selection = hello.Selection;
            app.RightClick(await PullRequestAsync(app, 1));
            var one = app.Menu();
            AssertMenu(one, P.PullRequestMenu(1, "Add the login form", P.CheckOutName("feature/login")));
            AssertHighlighted(one, P.OpenInBrowser);
            AssertMenuShape(app, one);
            AssertMenuHeight(one, P.PullRequestMenuHeight);
            ShotP(app, P.P11d);
            Assert.False(PullRequestRows(app).Single(row => row.Number == 1).IsSelected);
            Assert.Equal(selection, hello.Selection);
            app.ClickMenuItem(P.CopyLink);
            await app.WaitForAsync(() => app.Clipboard.Texts.Count > 0, "the clipboard");
            await AssertNoMenuAsync(app, null);
            Assert.Equal(P.PullRequestUrl(1, port), app.Clipboard.Texts[^1]);
            Assert.Empty(app.Launcher.Requests);
            Assert.Empty(operations);
            Assert.Equal(selection, hello.Selection);
            Assert.DoesNotContain(hello.LeftPanel.Rows, row => row is RefItemRow { IsSelected: true } or PullRequestRow { IsSelected: true });

            // P11 step 5: #5's and #2's menus.
            app.RightClick(await PullRequestAsync(app, 5));
            var five = app.Menu();
            AssertMenu(five, P.PullRequestMenu(5, "Fix a typo in the greeting", P.CheckOutName("ada:fix-typo")));
            AssertHighlighted(five, P.OpenInBrowser);
            app.PressKey(Key.Escape);
            app.RightClick(await PullRequestAsync(app, 2));
            var two = app.Menu();
            AssertMenu(two, P.PullRequestMenu(2, "Update the readme", P.CheckOutName("docs/readme")));
            AssertHighlighted(two, P.OpenInBrowser);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            Assert.Empty(ApiSince(served, requests));

            // P15 step 6: open in browser.
            requests = served.GitHub.Requests.Count;
            var warnings = app.LogMark();
            app.RightClick(await PullRequestAsync(app, 1));
            app.ClickMenuItem(P.OpenInBrowser);
            await app.WaitForAsync(() => OpenedUrls(app).Count == 1, "the launcher's address");
            await app.WaitForIdleAsync();
            Assert.Equal([P.PullRequestUrl(1, port)], OpenedUrls(app));
            Assert.Single(app.Launcher.Requests);
            Assert.Empty(ApiSince(served, requests));
            Assert.Empty(operations);
            Assert.DoesNotContain(app.LogLinesSince(warnings), line => line.Contains("[WRN]", StringComparison.Ordinal));

            // P9 steps 7 and 8: a click selects the head commit; #5's selects nothing.
            requests = served.GitHub.Requests.Count;
            var loginSha = (await served.Hello.GitAsync("rev-parse", "refs/heads/feature/login")).StandardOutput.Trim();
            app.Click(await PullRequestAsync(app, 1));
            await app.WaitForAsync(() => hello.Selection.Single is not null, "the head selected");
            await app.WaitForAsync(() => hello.Details.Sha == loginSha, "the details of the head");
            ShotP(app, P.P09a);
            Assert.Equal(loginSha, hello.Selection.Single);
            Assert.Equal([1], PullRequestRows(app).Where(row => row.IsSelected).Select(row => row.Number));
            Assert.DoesNotContain(hello.LeftPanel.Rows.OfType<RefItemRow>(), row => row.IsSelected);
            Assert.Empty(ApiSince(served, requests));
            app.Click(await PullRequestAsync(app, 5));
            await app.WaitForAsync(() => hello.Selection.Single is null && hello.Details.ShowsPlaceholder, "nothing selected");
            ShotP(app, P.P09b);
            Assert.DoesNotContain(PullRequestRows(app), row => row.IsSelected);
            Assert.DoesNotContain(hello.LeftPanel.Rows.OfType<RefItemRow>(), row => row.IsSelected);
            Assert.False(hello.IsWorkingRowSelected);
            Assert.Empty(ApiSince(served, requests));
            Assert.Empty(operations);

            // P10 step 9: the section closes keeping its count; the branch rows' #n.
            app.Click(await HeaderAsync(app));
            Assert.Equal(["> Pull requests 4"], Section(app));
            app.Click(await app.RefRowAsync("Local branches", "feature"));
            app.Click(await app.RefRowAsync("Remotes", "origin", "feature"));
            var login = await app.RefRowAsync("Local branches", "feature", "login");
            var number = Part(login, "RefPullRequest");
            Assert.Equal("#1", number.Text);
            Assert.Contains("secondary", number.Classes);
            Assert.True(app.BoundsOf(number).Left >= app.BoundsOf(Part(login, "RefName")).Right - 0.5, "The local login row's #1 is not right of its name.");
            Assert.Equal(P.PullRequestOfBranch(1, "Add the login form"), await app.RestPointerOnAsync(number));
            ShotP(app, P.P10a, keepPointer: true);
            var longRow = await app.RefRowAsync("Remotes", "origin", "feature", P.LongBranch["feature/".Length..]);
            Assert.Contains(Part(longRow, "RefName").TextLayout.TextLines, line => line.HasCollapsed);
            var three = Part(longRow, "RefPullRequest");
            Assert.True(three.IsEffectivelyVisible, "The long branch's #3 is not shown.");
            Assert.Equal("#3", three.Text);
            Assert.True(
                three.TextLayout.WidthIncludingTrailingWhitespace <= three.Bounds.Width + 0.5 && !three.TextLayout.TextLines.Any(line => line.HasCollapsed),
                "The long branch's #3 is cut.");
            var remoteLogin = Part(await app.RefRowAsync("Remotes", "origin", "feature", "login"), "RefPullRequest");
            Assert.Equal(("#1", true), (remoteLogin.Text, remoteLogin.IsEffectivelyVisible));
            string[][] without =
            [
                ["Local branches", "local-work"], ["Local branches", "main"], ["Local branches", "feature"],
                ["Remotes", "origin"], ["Remotes", "origin", "main"], ["Remotes", "origin", "feature"], ["Remotes", "origin", "docs"], ["Remotes", "origin", "topic"],
            ];
            foreach (var path in without)
            {
                Assert.False(Part(await app.RefRowAsync(path), "RefPullRequest").IsVisible, $"{string.Join(" › ", path)} shows a #n.");
            }

            app.Click(await HeaderAsync(app));
            Assert.Equal(["v Pull requests 4", .. P.HelloRows.Select(row => row.Name)], Section(app));

            // P8 steps 10 to 13: the filter keeps pull requests beside the branches it keeps.
            requests = served.GitHub.Requests.Count;
            var before = Panel(app);
            var filter = app.Find<TextBox>("FilterBox");
            app.Click(filter);
            app.Type("ADA");
            Assert.Equal(["Local branches 0", "Remotes 0", "Pull requests 2", "  #5 Fix a typo in the greeting", "  #1 Add the login form", "Tags 0", "Stashes 0"], Panel(app));
            Assert.Equal(["v Pull requests 2", "#5 Fix a typo in the greeting", "#1 Add the login form"], Section(app));
            ShotP(app, P.P08a);
            app.PressKey(Key.A, RawInputModifiers.Control);
            app.Type("login");
            Assert.Equal(
                ["Local branches 1", "  feature", "    login #1", "Remotes 1", "  origin", "    feature", "      login #1", "Pull requests 1", "  #1 Add the login form", "Tags 0", "Stashes 0"],
                Panel(app));
            ShotP(app, P.P08b);
            app.PressKey(Key.A, RawInputModifiers.Control);
            app.Type("#2");
            Assert.Equal(["Local branches 0", "Remotes 0", "Pull requests 1", "  #2 Update the readme", "Tags 0", "Stashes 0"], Panel(app));
            app.PressKey(Key.A, RawInputModifiers.Control);
            app.PressKey(Key.Back);
            Assert.Equal(string.Empty, filter.Text ?? string.Empty);
            Assert.Equal(P.FilterPlaceholder, filter.PlaceholderText);
            Assert.Equal(before, Panel(app));
            Assert.Empty(ApiSince(served, requests));

            // P12 step 14: feature/login's menu with "Open pull request #1".
            requests = served.GitHub.Requests.Count;
            app.RightClick(await app.RefRowAsync("Local branches", "feature", "login"));
            AssertWholeMenu(app, P.LoginBranchMenu, "Check out feature/login", P.LoginBranchMenuHeight);
            ShotP(app, P.P12a);
            app.ClickMenuItem(P.OpenPullRequest(1));
            await app.WaitForAsync(() => OpenedUrls(app).Count == 2, "the launcher's address");
            await AssertNoMenuAsync(app, null);
            Assert.Equal(P.PullRequestUrl(1, port), OpenedUrls(app)[^1]);
            Assert.Empty(ApiSince(served, requests));

            // P12 steps 15 and 16: local-work's, origin/docs/readme's, origin/topic/no-pr's.
            var git = await GitStateAsync(served.Hello);
            app.RightClick(await app.RefRowAsync("Local branches", "local-work"));
            AssertWholeMenu(app, P.LocalWorkMenu, "Check out local-work", P.LocalWorkMenuHeight);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            app.Click(await app.RefRowAsync("Remotes", "origin", "docs"));
            app.RightClick(await app.RefRowAsync("Remotes", "origin", "docs", "readme"));
            AssertWholeMenu(app, P.RemoteBranchMenu("docs/readme", P.OpenPullRequest(2)), "Check out origin/docs/readme", P.RemoteBranchMenuHeight);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            app.Click(await app.RefRowAsync("Remotes", "origin", "topic"));
            app.RightClick(await app.RefRowAsync("Remotes", "origin", "topic", "no-pr"));
            AssertWholeMenu(app, P.RemoteBranchMenu("topic/no-pr", P.CreatePullRequest), "Check out origin/topic/no-pr", P.RemoteBranchMenuHeight);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);

            // P12 steps 17 and 18: drops.
            await DragAsync(app, ["Local branches", "feature", "login"], ["Local branches", "main"]);
            AssertWholeMenu(app, P.LoginOntoMainDrop, "Merge feature/login into main", P.LoginOntoMainDropHeight);
            ShotP(app, P.P12b);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            await DragAsync(app, ["Local branches", "local-work"], ["Remotes", "origin", "main"]);
            AssertWholeMenu(app, P.LocalWorkOntoOriginMainDrop, "Push local-work to origin/main", P.LocalWorkOntoOriginMainDropHeight);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            Assert.Equal(git, await GitStateAsync(served.Hello));
            Assert.Empty(ApiSince(served, requests));
            Assert.Empty(operations);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, [P.PullRequestUrl(1, port), P.PullRequestUrl(1, port)]);
    }

    /// <summary>The open menu is <paramref name="lines"/> whole, <paramref name="highlighted"/> highlighted, 4a's shape and the report's height.</summary>
    private static void AssertWholeMenu(ShellDriver app, string[] lines, string highlighted, double height)
    {
        var menu = app.Menu();
        AssertMenu(menu, lines);
        AssertHighlighted(menu, highlighted);
        AssertMenuShape(app, menu);
        AssertMenuHeight(menu, height);
    }

    /// <summary>The titles of the operations the session starts from now on (part P: a click, a menu or a drop starts none but P13's).</summary>
    private static List<string> StartedOperations(ShellDriver app)
    {
        var started = new List<string>();
        app.Session.Operations.Started += (_, start) =>
        {
            lock (started)
            {
                started.Add(start.Title);
            }
        };
        return started;
    }

    /// <summary>A drag of a branch row onto another, as phase 3's checks make it: press, move held until the drag starts, release over the row; then the drop's menu.</summary>
    private static async Task DragAsync(ShellDriver app, string[] from, string[] to)
    {
        var source = app.BoundsOf(await app.RefRowAsync(from)).Center;
        var target = app.BoundsOf(await app.RefRowAsync(to)).Center;
        app.PressMouse(source);
        app.MoveMouseHeld(source + new Avalonia.Point(0, 12), steps: 3);
        app.MoveMouseHeld(target, steps: 6);
        app.ReleaseMouse(target);
        await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "the drop's menu");
    }
}
