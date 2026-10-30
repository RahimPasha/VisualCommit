// Part B's real-window steps (docs/test-reports/phase-7a.md, part B, the R steps in the Notes of B7,
// B3 and B6; docs/phase-7a-design.md 13.4 B): one empty tab, signed out; the palette's "Clone from
// GitHub…" by a real Ctrl+Shift+P, "Add GitHub account" first with the token typed on the real keyboard,
// the browser listed over the form (b03a-listed), octo/hello chosen and cloned by the real process
// through the app's askpass helper run by Git for Windows' sh, and the clone's configuration read back
// with plain git after the app has closed. No preparation (13.4).
using System.Globalization;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase7aRealWindowPass
{
    /// <summary>
    /// Part B (13.4 B steps 1 to 7): the palette's command signed out opens the sign-in dialog first,
    /// then the form and the browser over it, listed in GitHub's order with the filter focused
    /// (<c>b03a-listed</c>); "Choose" fills the form with the plain URL; "Clone" clones through the app's
    /// helper as <c>x-access-token</c> without any credentials dialog; the clone holds the plain URL and
    /// no credential key.
    /// </summary>
    [Fact]
    public async Task Check_B_browse_and_clone()
    {
        using var hello = await Scenarios.HostingAsync();
        await using var served = await Scenarios.ServeHostingAsync(hello, ct: TestContext.Current.CancellationToken);
        using var clones = new TempDirectory("rw7a-clone");
        using var app = LaunchServed(served, emptyTab: true);
        var port = served.GitHub.Port;
        var url = PartB.HelloCloneUrl(port);
        var clone = Path.Combine(clones.Path, PartB.HelloFolder);

        // Step 1 (B7): a real Ctrl+Shift+P, after a click at the neutral point that gives the window the
        // activation; the query; the one row highlighted; Enter.
        app.ClickAt(RealApp.NeutralPoint.X, RealApp.NeutralPoint.Y);
        app.PressPalette();
        app.WaitForPalette(StepTimeout);
        app.WaitFor(() => app.HasFocusOnScreen("CommandPaletteBox"), "the palette's box to have the keyboard focus", StepTimeout);
        app.TypeInPalette(PartB.PaletteQuery, PartB.PaletteQuery, StepTimeout);
        WaitTelling(
            app,
            () => app.PaletteRowsInView() is [{ IsHighlighted: true } row] && row.Label == PartB.PaletteCommand,
            $"the palette's one row \"{PartB.PaletteCommand}\", highlighted",
            () => $"It shows: {app.DescribePaletteRows()}.");
        AssertNoRequests(Recorded(served), "The palette asked GitHub");
        app.PressKey(VirtualKeyShort.RETURN);
        app.WaitForPaletteClosed(StepTimeout);

        // Step 2 (B7): signed out, "Add GitHub account" comes first (D261, D300); the token typed and
        // masked, "Sign in". Settings stays closed, so no AccountLogin shows: step 3's wait is the proof.
        var dialog = app.WaitForSignInDialog(StepTimeout);
        Assert.Equal(PartA.DialogTitle, dialog.Name);
        Assert.False(app.IsSettingsShown, "The settings screen opened for the palette's sign-in (D300).");
        var requests = served.GitHub.Requests.Count;
        app.TypeTokenAndConfirm(served.OctoToken, NetworkTimeout);
        Assert.False(app.IsSettingsShown, "The settings screen opened after the palette's sign-in (D300).");

        // Step 3 (B3): the form with the browser over it, listed, the filter focused; "Choose" disabled.
        app.WaitFor(() => app.ShownElements(RealApp.CloneFromGitHubDialogId).Count == 1, "the repository browser \"Clone from GitHub\"", NetworkTimeout);
        WaitForFake(served, requests, request => request.Route == PartB.ListRoute, "GET api/v3/user/repos", NetworkTimeout);
        IReadOnlyList<string> rows = [.. PartB.Rows.Select(row => row.FullName)];
        WaitTelling(
            app,
            () => app.CloneBrowserRowNames().SequenceEqual(rows) && app.HasFocusOnScreen("CloneFromGitHubFilter"),
            $"the browser's rows {string.Join(", ", rows)} with the filter focused",
            () => $"It shows {string.Join(", ", app.CloneBrowserRowNames())}; the focus is on {app.FocusedAutomationId() ?? "nothing"}.",
            NetworkTimeout);
        var browser = app.ShownElements(RealApp.CloneFromGitHubDialogId)[0];
        Assert.Equal(PartB.DialogTitle, browser.Name);
        Assert.Equal(string.Empty, app.BoxText("CloneFromGitHubFilter"));
        var choose = app.FindIn(browser, "ConfirmButton");
        Assert.Equal((PartB.Choose, false), (choose.Name, choose.IsEnabled));
        Assert.Equal(["user", PartB.ListRoute], Recorded(served, requests).Select(request => request.Route ?? request.Path));
        Assert.Empty(app.ShownElements("AccountLogin"));
        Compare(app, served, PartB.B03aListed);

        // Step 4 (B6): a click on octo/hello, then "Choose": the plain URL and the folder's name.
        var row = app.CloneBrowserRow(OctoHello);
        app.ClickWithMouse(row);
        app.WaitFor(() => RealApp.IsSelected(app.CloneBrowserRow(OctoHello)), $"{OctoHello} to be selected", StepTimeout);
        choose = app.FindIn(app.ShownElements(RealApp.CloneFromGitHubDialogId)[0], "ConfirmButton");
        app.WaitFor(() => choose.IsEnabled, "Choose to be enabled with a row selected", StepTimeout);
        app.ClickWithMouse(choose);
        app.MoveToNeutral();
        app.WaitUntilGone(RealApp.CloneFromGitHubDialogId, StepTimeout);
        WaitTelling(
            app,
            () => app.BoxText("CloneUrlBox") == url && app.BoxText("CloneNameBox") == PartB.HelloFolder,
            $"the form to hold {url} and {PartB.HelloFolder}",
            () => $"It holds \"{Checked(served, app.BoxText("CloneUrlBox"), "The form's URL box")}\" and \"{Checked(served, app.BoxText("CloneNameBox"), "The form's name box")}\".");

        // Step 5 (B6): the parent folder typed; "Clone"; hello opens in the tab within the network's
        // limit, with no credentials dialog; the helper answered git from the account as x-access-token.
        app.ReplaceText("CloneParentBox", clones.Path);
        WaitTelling(
            app,
            () => app.BoxText("CloneParentBox") == clones.Path,
            "the parent folder's box to hold the clones' folder",
            () => $"It holds \"{app.BoxText("CloneParentBox")}\".");
        var gitRequests = served.GitHub.GitRequests.Count;
        var logMark = app.LogMark();
        app.ClickWithMouse(app.Find("CloneStartButton"));
        app.MoveToNeutral();
        try
        {
            app.WaitForLogLine(Loaded(Hello), NetworkTimeout);
            app.WaitForLogLine(AppLogFile.WorkingTreeRead(Hello), NetworkTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} The credentials dialog {(app.ShownElements("CredentialsDialog").Count == 0 ? "does not show" : "shows")}; the clone's error reads \"{Checked(served, string.Join(" | ", app.NamesOnScreen("CloneError")), "The clone's error")}\".", ex);
        }

        Assert.Empty(app.ShownElements("CredentialsDialog"));
        var answered = PartB.AnsweredFromTheAccount(port);
        Assert.True(
            CountLinesSince(app, logMark, line => line.EndsWith(answered, StringComparison.Ordinal)) > 0,
            $"No line of the log since the click on Clone ends with \"{answered}\".");
        var asks = CountLinesSince(app, logMark, line => line.Contains("hello: Clone: git asks (", StringComparison.Ordinal));
        Assert.True(asks == 0, $"{asks} lines of the log since the click on Clone hold \"hello: Clone: git asks (\".");
        var git = served.GitHub.GitRequests.Skip(gitRequests).Where(request => request.Path.StartsWith("/octo/hello.git/", StringComparison.Ordinal)).ToList();
        foreach (var request in git)
        {
            AssertNoToken(served, request.Path, "The path of a git request to the fake");
            AssertNoToken(served, request.Query, "The query of a git request to the fake");
        }

        Assert.Contains(git, request => request is { GivenUserName: "x-access-token", Status: 200 });
        Assert.DoesNotContain(git, request => request.GivenUserName is not null && request.GivenUserName != "x-access-token");

        // Step 6: run.txt names the helper the clone went through and whether its path holds a space.
        Assert.True(File.Exists(RealApp.HelperPath), $"The askpass helper {RealApp.HelperPath} does not exist.");
        Run.Note(string.Create(
            CultureInfo.InvariantCulture,
            $"B: the clone went through {RealApp.HelperPath}; its path {(RealApp.HelperPath.Contains(' ', StringComparison.Ordinal) ? "holds a space" : "holds no space")}."));

        // Step 7: after the close, plain git in the clone: the plain URL, no credential key.
        Assert.Equal(0, app.Close());
        using var home = new TempDirectory("rw7a-clone-home");
        var runner = new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem()) ?? throw new InvalidOperationException("The pass needs git."));
        async Task<GitResult> GitAsync(params string[] arguments) =>
            await runner.RunAsync(new GitCommand(arguments) { WorkingDirectory = clone, Environment = TempRepo.IsolatedEnvironment(home.Path) }, TestContext.Current.CancellationToken);

        var origin = (await GitAsync("config", "--get", "remote.origin.url")).StandardOutput.Trim();
        AssertNoToken(served, origin, "The clone's remote.origin.url");
        Assert.Equal(url, origin);
        var credential = await GitAsync("config", "--get-regexp", "^credential");
        AssertNoToken(served, credential.StandardOutput, "The clone's credential keys");
        Assert.Equal((1, string.Empty), (credential.ExitCode, credential.StandardOutput.Trim()));
        AssertCleanEnd(app, served, clones.Path);
    }
}
