// Part P's real-window steps (docs/test-reports/phase-7a.md, part P, the R steps in the Notes of P6,
// P11 and P15; docs/phase-7a-design.md 13.4 P, 13.5; D321): one tab on hello, no preparation (13.4);
// signed in through Settings; the section's rows with the draft marked (p06a-rows), the section's menu
// by a real right-click (p11c-section-menu), #1's menu, and its "Open in browser" by a real click: the
// owner's default browser fetches the fake's page without a token, then a real click on the status bar
// takes the activation back. Last of the pass, so that the browser step is its last input (D319).
using System.Globalization;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase7aRealWindowPass
{
    /// <summary>
    /// Whether the pass clicks "Open in browser" (13.5, D321): the owner's answer of 2026-10-29, that
    /// the pass may open the default browser once, on the fake's page. Set false only where the step
    /// cannot run (no default browser, or one that never fetches the fake's page) and with the owner's
    /// word; the report then moves the step to the checks by hand at acceptance.
    /// </summary>
    private const bool OpensTheBrowser = true;

    /// <summary>
    /// Part P (13.4 P steps 1 to 5, 13.5): octo/hello's four open pull requests newest first, #2 marked
    /// Draft (<c>p06a-rows</c>); the section's menu (<c>p11c-section-menu</c>); #1's menu's labels; its
    /// "Open in browser" handed to the default browser, which fetches <c>octo/hello/pull/1</c> from the
    /// fake without a token.
    /// </summary>
    [Fact]
    public async Task Check_P_pull_requests_and_browser()
    {
        using var hello = await Scenarios.HostingAsync();
        await using var served = await Scenarios.ServeHostingAsync(hello, ct: TestContext.Current.CancellationToken);
        using var app = LaunchServed(served, emptyTab: false);

        // Step 1 (P6): signed in through Settings; D322's two lines; the rows, the draft marked.
        var logMark = app.LogMark();
        app.SignInThroughSettings(served.OctoToken, OctoLogin, NetworkTimeout);
        WaitForHostingSettled(app, logMark);
        IReadOnlyList<string> rows = [.. PartP.HelloRows.Select(row => row.Name)];
        WaitTelling(
            app,
            () => app.PullRequestRowNames().SequenceEqual(rows),
            $"the section's rows {string.Join(", ", rows)}",
            () => $"It shows {string.Join(" | ", app.PullRequestRowNames())}.");
        Assert.Equal(
            PartP.HelloRows.Select(row => (row.Name, row.Draft)),
            PartP.HelloRows.Select(row => (row.Name, RealApp.ShowsDraftMark(app.PullRequestRow(row.Name)))));
        Compare(app, served, PartP.P06a);

        // Step 2 (P11): a real right-click on the section's header: its menu; asked for, it asks GitHub nothing.
        var requests = served.GitHub.Requests.Count;
        app.RightClickWithMouse(app.RefRow(LeftPanelViewModel.PullRequestsTitle));
        AssertMenu(app, PartP.SectionMenu, PartP.SectionMenuHeight);
        Compare(app, served, PartP.P11c, keepPointer: true);
        CloseMenuWithEscape(app);

        // Step 3 (P11): a real right-click on #1: its menu's labels, no capture.
        var one = HelloPullRequests.Single(pull => pull.Number == 1);
        app.RightClickWithMouse(app.PullRequestRow(PartP.PullRequestHeader(one.Number, one.Title)));
        app.WaitForMenu();
        Assert.Equal(ItemLabels(PartP.PullRequestMenu(one.Number, one.Title, PartP.CheckOutName(one.HeadRef))), app.MenuItemNames());
        Assert.Equal(requests, served.GitHub.Requests.Count);

        // Step 4 (P15, 13.5): the browser step, or Esc.
        OpenInTheBrowser(app, served, one.Number, OpensTheBrowser);

        // Step 5.
        AssertCleanEnd(app, served);
    }

    /// <summary>
    /// 13.5 with #<paramref name="number"/>'s menu open. When <paramref name="opens"/>: marks the fake's
    /// records and the log; a real click on "Open in browser"; waits at most <see cref="NetworkTimeout"/>
    /// for a request of route <c>page:octo/hello</c>, path <c>octo/hello/pull/&lt;n&gt;</c>, answered 200,
    /// without a token: the real launcher handed the address to the default browser, which fetched it (a
    /// browser may fetch it more than once); no Warning in the log since the mark; a real click at the
    /// neutral point (the status bar's empty middle) takes the activation back; run.txt says the tab is
    /// left open, for the owner to close. Otherwise Esc, and run.txt says the item was not clicked.
    /// </summary>
    private void OpenInTheBrowser(RealApp app, ServedHosting served, int number, bool opens)
    {
        if (!opens)
        {
            CloseMenuWithEscape(app);
            Run.Note("P: Open in browser not clicked");
            return;
        }

        var url = PartP.PullRequestUrl(number, served.GitHub.Port);
        var path = string.Create(CultureInfo.InvariantCulture, $"octo/hello/pull/{number}");
        var requests = served.GitHub.Requests.Count;
        var logMark = app.LogMark();
        app.ClickMenuItem(PartP.OpenInBrowser);
        WaitForFake(
            served.GitHub,
            requests,
            request => request.Route == "page:octo/hello" && request.Path.TrimStart('/') == path && request.Status == 200 && !request.TokenPresent,
            $"the default browser's request for {url}",
            NetworkTimeout);
        Assert.DoesNotContain(served.GitHub.Requests.Skip(requests), request => (request.Route?.StartsWith("page:", StringComparison.Ordinal) ?? false) && request.TokenPresent);
        Assert.DoesNotContain(app.LogLinesSince(logMark), line => line.Contains("[WRN]", StringComparison.Ordinal));
        app.ClickAt(RealApp.NeutralPoint.X, RealApp.NeutralPoint.Y);
        Run.Note($"P: the browser opened {url}; its tab is left open.");
    }
}
