using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using Xunit;
using P = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartP;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>Part P's method 2 (docs/test-reports/phase-7a.md, P6, P11, P14, P15, P9, P10, P8, P12): rows, menus and selection on hello, signed in through the Accounts screen.</summary>
public sealed partial class Phase7aChecksP
{
    [AvaloniaFact]
    public async Task Check_P6_P11_P14_P15_P9_P10_P8_P12_rows_menus_and_selection()
    {
        // P6 step 1: signed in as the real window does (open point 30).
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a p6");
        var port = served.GitHub.Port;
        var app = await OpenAsync(data, served, signIn: false);
        try
        {
            var hello = app.Session.Shell.ActiveTab.Repository!;
            var requests = served.GitHub.Requests.Count;
            var logMark = app.LogMark();
            await app.OpenAccountsAsync();
            await app.OpenSignInDialogAsync();
            app.TypeToken(served.OctoToken);
            app.PressKey(Key.Enter);
            await app.WaitForAccountAsync(account => account is { State: VisualCommit.Hosting.AccountState.SignedIn });
            app.PressKey(Key.Escape);
            await WaitForListedAsync(app, "hello", 4, "octo/hello", logMark);
            Shot(app, P.P06a);
            var api = ApiSince(served, requests);
            Assert.Equal(1, api.Count(request => request.Route == "repo:octo/hello"));
            Assert.Equal(1, api.Count(request => request.Route == "pulls:octo/hello"));
            Assert.DoesNotContain(api, request => request.Status == 401);
            Assert.Null(app.Session.Services.DialogHost.Current);
            Assert.Equal(["v Pull requests 4", .. P.HelloRows.Select(row => row.Name)], Section(app));
            Assert.Equal(P.HelloRows.Select(row => (row.Author, row.Draft)), PullRequestRows(app).Select(row => (row.Author, row.IsDraft)));
            Assert.Equal(P.HelloRows.Select(row => P.HelloToolTips[int.Parse(row.Name[1..row.Name.IndexOf(' ', StringComparison.Ordinal)], System.Globalization.CultureInfo.InvariantCulture)]), PullRequestRows(app).Select(row => row.ToolTip));
            Assert.DoesNotContain(PullRequestRows(app), row => row.IsSelected);
            AssertNoClippedTextButEllipses(app);
            AssertNotWritten(app, served.OctoToken);

            // P11 step 2: the section's menu.
            requests = served.GitHub.Requests.Count;
            app.RightClick(await HeaderAsync(app));
            var section = app.Menu();
            AssertMenu(section, P.SectionMenu);
            AssertHighlighted(section, P.RefreshPullRequests);
            AssertMenuShape(app, section);
            Shot(app, P.P11c, keepPointer: true);
            app.PressKey(Key.Escape);

            // P11 step 3 and P14 step 4: #1's menu, then Copy link.
            var selection = hello.Selection;
            app.RightClick(await PullRequestAsync(app, 1));
            var one = app.Menu();
            AssertMenu(one, P.PullRequestMenu(1, "Add the login form", P.CheckOutName("feature/login")));
            AssertHighlighted(one, P.OpenInBrowser);
            AssertMenuShape(app, one);
            Shot(app, P.P11d);
            Assert.False(PullRequestRows(app).Single(row => row.Number == 1).IsSelected);
            Assert.Equal(selection, hello.Selection);
            app.ClickMenuItem(P.CopyLink);
            await app.WaitForAsync(() => app.Clipboard.Texts.Count > 0, "the clipboard");
            Assert.Equal(P.PullRequestUrl(1, port), app.Clipboard.Texts[^1]);
            Assert.Empty(OpenedUrls(app));

            // P11 step 5: #5's and #2's menus.
            app.RightClick(await PullRequestAsync(app, 5));
            AssertMenu(app.Menu(), P.PullRequestMenu(5, "Fix a typo in the greeting", P.CheckOutName("ada:fix-typo")));
            app.PressKey(Key.Escape);
            app.RightClick(await PullRequestAsync(app, 2));
            AssertMenu(app.Menu(), P.PullRequestMenu(2, "Update the readme", P.CheckOutName("docs/readme")));
            app.PressKey(Key.Escape);

            // P15 step 6: open in browser.
            var warnings = app.LogMark();
            app.RightClick(await PullRequestAsync(app, 1));
            app.ClickMenuItem(P.OpenInBrowser);
            await app.WaitForAsync(() => OpenedUrls(app).Count == 1, "the launcher's address");
            Assert.Equal([P.PullRequestUrl(1, port)], OpenedUrls(app));
            Assert.DoesNotContain(app.LogLinesSince(warnings), line => line.Contains("[WRN]", StringComparison.Ordinal));

            // P9 steps 7 and 8: a click selects the head commit; #5's selects nothing.
            app.Click(await PullRequestAsync(app, 1));
            await app.WaitForAsync(() => hello.Selection.Single is not null, "the head selected");
            Shot(app, P.P09a);
            Assert.Equal((await served.Hello.GitAsync("rev-parse", "refs/heads/feature/login")).StandardOutput.Trim(), hello.Selection.Single);
            Assert.True(PullRequestRows(app).Single(row => row.Number == 1).IsSelected);
            app.Click(await PullRequestAsync(app, 5));
            await app.WaitForAsync(() => hello.Selection.Single is null, "nothing selected");
            Shot(app, P.P09b);
            Assert.DoesNotContain(PullRequestRows(app), row => row.IsSelected);

            // P10 step 9: the section closes keeping its count; the branch rows' #n.
            app.Click(await HeaderAsync(app));
            Assert.Equal(["> Pull requests 4"], Section(app));
            app.Click(await app.RefRowAsync("Local branches", "feature"));
            app.Click(await app.RefRowAsync("Remotes", "origin", "feature"));
            var login = await app.RefRowAsync("Local branches", "feature", "login");
            var number = login.GetLogicalDescendants().OfType<TextBlock>().Single(block => block.Name == "RefPullRequest");
            Assert.Equal("#1", number.Text);
            Assert.Equal(P.PullRequestOfBranch(1, "Add the login form"), await app.RestPointerOnAsync(number));
            Shot(app, P.P10a, keepPointer: true);
            Assert.Equal("#3", ((RefItemRow)app.RefRowModel("Remotes", "origin", "feature", P.LongBranch["feature/".Length..])).PullRequestText);
            Assert.Equal("#1", ((RefItemRow)app.RefRowModel("Remotes", "origin", "feature", "login")).PullRequestText);
            Assert.False(((RefItemRow)app.RefRowModel("Local branches", "main")).ShowsPullRequest);
            app.Click(await HeaderAsync(app));
            Assert.Equal(5, Section(app).Count);

            // P8 steps 10 to 13: the filter.
            requests = served.GitHub.Requests.Count;
            app.Click(app.FindByAutomationId("FilterBox"));
            app.Type("ADA");
            Assert.Equal(["v Pull requests 2", "#5 Fix a typo in the greeting", "#1 Add the login form"], Section(app));
            Shot(app, P.P08a);
            app.PressKey(Key.A, RawInputModifiers.Control);
            app.Type("login");
            Assert.Equal(["v Pull requests 1", "#1 Add the login form"], Section(app));
            Shot(app, P.P08b);
            app.PressKey(Key.A, RawInputModifiers.Control);
            app.Type("#2");
            Assert.Equal(["v Pull requests 1", "#2 Update the readme"], Section(app));
            app.PressKey(Key.A, RawInputModifiers.Control);
            app.PressKey(Key.Back);
            Assert.Equal(5, Section(app).Count);
            Assert.Empty(ApiSince(served, requests));

            // P12 step 14: feature/login's menu with "Open pull request #1".
            app.RightClick(await app.RefRowAsync("Local branches", "feature", "login"));
            var branchMenu = app.Menu();
            Assert.Contains(P.OpenPullRequest(1), branchMenu.Lines());
            Assert.Equal(["Push feature/login to origin/feature/login", "Force push feature/login… (danger)", P.OpenPullRequest(1), "—"], branchMenu.Lines().SkipWhile(line => !line.StartsWith("Push ", StringComparison.Ordinal)).Take(4));
            AssertMenuShape(app, branchMenu);
            Shot(app, P.P12a);
            app.ClickMenuItem(P.OpenPullRequest(1));
            await app.WaitForAsync(() => OpenedUrls(app).Count == 2, "the launcher's address");
            Assert.Equal(P.PullRequestUrl(1, port), OpenedUrls(app)[^1]);

            // P12 steps 15 and 16: local-work's, origin/docs/readme's, origin/topic/no-pr's.
            app.RightClick(await app.RefRowAsync("Local branches", "local-work"));
            Assert.Equal(["Push local-work…", P.CreatePullRequest, "—"], app.Menu().Lines().SkipWhile(line => !line.StartsWith("Push ", StringComparison.Ordinal)).Take(3));
            app.PressKey(Key.Escape);
            app.Click(await app.RefRowAsync("Remotes", "origin", "docs"));
            app.RightClick(await app.RefRowAsync("Remotes", "origin", "docs", "readme"));
            Assert.Equal(["Delete docs/readme on origin… (danger)", "—", P.OpenPullRequest(2), "—"], app.Menu().Lines().SkipWhile(line => !line.StartsWith("Delete ", StringComparison.Ordinal)).Take(4));
            app.PressKey(Key.Escape);
            app.Click(await app.RefRowAsync("Remotes", "origin", "topic"));
            app.RightClick(await app.RefRowAsync("Remotes", "origin", "topic", "no-pr"));
            Assert.Equal(["Delete topic/no-pr on origin… (danger)", "—", P.CreatePullRequest, "—"], app.Menu().Lines().SkipWhile(line => !line.StartsWith("Delete ", StringComparison.Ordinal)).Take(4));
            app.PressKey(Key.Escape);

            // P12 steps 17 and 18: drops.
            await DragAsync(app, ["Local branches", "feature", "login"], ["Local branches", "main"]);
            var drop = app.Menu();
            Assert.Equal("feature/login → main", drop.Lines()[0]);
            Assert.Equal(["—", P.OpenPullRequest(1), "—", "Compare feature/login with main"], drop.Lines().TakeLast(4));
            Shot(app, P.P12b);
            app.PressKey(Key.Escape);
            await DragAsync(app, ["Local branches", "local-work"], ["Remotes", "origin", "main"]);
            Assert.Equal(["—", P.CreatePullRequest, "—", "Compare local-work with origin/main"], app.Menu().Lines().TakeLast(4));
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
        }
        finally
        {
            End(app, data, served);
        }
    }

    /// <summary>A drag of a branch row onto another, as phase 3's checks make it: press, move held until the drag starts, release over the row; then the drop's menu.</summary>
    private static async Task DragAsync(VisualCommit.Testing.Headless.ShellDriver app, string[] from, string[] to)
    {
        var source = app.BoundsOf(await app.RefRowAsync(from)).Center;
        var target = app.BoundsOf(await app.RefRowAsync(to)).Center;
        app.PressMouse(source);
        app.MoveMouseHeld(source + new Avalonia.Point(0, 12), steps: 3);
        app.MoveMouseHeld(target, steps: 6);
        app.ReleaseMouse(target);
        await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "the drop's menu");
    }

    /// <summary>No text is cut but by an ellipsis (part P: the titles and the sign-in row are cut at 260).</summary>
    private static void AssertNoClippedTextButEllipses(VisualCommit.Testing.Headless.ShellDriver app) =>
        Assert.DoesNotContain(VisualCommit.Testing.Headless.LayoutAudit.FindClippedText(app.Window), line => !line.Contains("ellipsis", StringComparison.Ordinal));
}
