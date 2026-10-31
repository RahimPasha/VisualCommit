// Part N's real-window steps (docs/test-reports/phase-7a.md, part N, the R steps in N1's Notes;
// docs/phase-7a-design.md 13.4 N, 17 item 8): one tab on hello prepared by PartN.PrepareAsync, as its
// twin Check_N1_from_a_branch_and_the_offer is; signed in through Settings; topic/no-pr's "Create pull
// request…" by a real right-click, the form (n01a-form), a real click on Create: the real process's POST
// through its own handler, the operation's end and both "listed" lines (ruling 51), the status bar's
// offer, never clicked (n01b-created).
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase7aRealWindowPass
{
    /// <summary>
    /// Part N (13.4 N steps 1 to 5): the form from <c>topic/no-pr</c>'s menu, Into <c>origin/main</c>, the
    /// focused title <c>Try an idea</c> (<c>n01a-form</c>); Create makes #6 without a push: one
    /// <c>create:octo/hello</c> request answered 201 with the title, head, base and <c>"draft":false</c>,
    /// no receive-pack request, <c>Created pull request #6</c> and "Open in browser" in the status bar,
    /// #6 first in the section (<c>n01b-created</c>).
    /// </summary>
    [Fact]
    public async Task Check_N_new_pull_request()
    {
        using var hello = await Scenarios.HostingAsync();
        await using var served = await Scenarios.ServeHostingAsync(hello, ct: TestContext.Current.CancellationToken);
        await PartN.PrepareAsync(served.Hello);
        using var app = LaunchServed(served, emptyTab: false);

        // Step 1: signed in through Settings; D322's two lines.
        var logMark = app.LogMark();
        app.SignInThroughSettings(served.OctoToken, OctoLogin, NetworkTimeout);
        WaitForHostingSettled(app, logMark);

        // Step 2: a real click on Local branches › topic, a real right-click on its no-pr, then
        // "Create pull request…"; the pointer leaves the form's controls at once.
        app.ClickWithMouse(app.RefRow(LeftPanelViewModel.LocalBranchesTitle, "topic"));
        app.WaitFor(() => app.HasRefRow(LeftPanelViewModel.LocalBranchesTitle, "topic", "no-pr"), "Local branches › topic › no-pr", StepTimeout);
        var requests = served.GitHub.Requests.Count;
        app.RightClickWithMouse(app.RefRow(LeftPanelViewModel.LocalBranchesTitle, "topic", "no-pr"));
        app.ClickMenuItem(PartP.CreatePullRequest);
        app.MoveToNeutral();

        // Step 3: the form, From topic/no-pr Into origin/main, the focused title holding its default.
        app.WaitFor(() => app.ShownElements(RealApp.CreatePullRequestDialogId).Count == 1, $"the form \"{PartN.DialogTitle}\"", StepTimeout);
        var form = app.ShownElements(RealApp.CreatePullRequestDialogId)[0];
        Assert.Equal(PartN.DialogTitle, form.Name);
        app.WaitForText("PullRequestHead", PartN.Topic, StepTimeout);
        app.WaitForText("PullRequestBase", PartN.OriginMain, NetworkTimeout);
        // The form's own box: the left panel's pull-request rows name their titles PullRequestTitle too.
        string TitleBoxText() => app.FindIn(form, "PullRequestTitle").AsTextBox().Text;
        WaitTelling(
            app,
            () => RealApp.HasKeyboardFocus(app.FindIn(form, "PullRequestTitle")) && TitleBoxText() == PartN.TopicTitle,
            $"the focused title box to hold \"{PartN.TopicTitle}\"",
            () => $"It holds \"{TitleBoxText()}\"; the focus is on {app.FocusedAutomationId() ?? "nothing"}.");
        Assert.Empty(app.ShownElements("PullRequestHeadNote"));
        var create = app.FindIn(form, "ConfirmButton");
        Assert.Equal((PartN.Create, true), (create.Name, create.IsEnabled));
        AssertNoRequests(ApiRequestsSince(served, requests), "Opening the form asked GitHub");
        Compare(app, served, PartN.N01a);

        // Step 4: Create. The operation's end, then both "listed" lines (ruling 51) since the mark.
        requests = served.GitHub.Requests.Count;
        var gitRequests = served.GitHub.GitRequests.Count;
        logMark = app.LogMark();
        app.ClickWithMouse(create);
        app.MoveToNeutral();
        var ended = AppLogFile.OperationEnded(Hello, PartN.OperationTitle(PartN.Topic), OperationOutcome.Succeeded);
        var listed = AppLogFile.PullRequestsListed(Hello);
        WaitTelling(
            app,
            () => app.CountLogLinesSince(ended, logMark) > 0,
            $"\"{PartN.OperationTitle(PartN.Topic)}\" to succeed",
            () => $"Since the mark the log says: {Checked(served, string.Join(" | ", app.LogLinesSince(logMark).Where(line => line.Contains(PartN.OperationTitle(PartN.Topic), StringComparison.Ordinal))), "A line of the log about the operation")}; the form reads \"{Checked(served, string.Join(" | ", app.NamesOnScreen("PullRequestError")), "The form's error")}\".",
            OperationTimeout);
        WaitTelling(
            app,
            () => app.CountLogLinesSince(listed, logMark) >= 2,
            "two \"listed\" lines of hello since the create (ruling 51)",
            () => $"Since the mark the log has {app.CountLogLinesSince(listed, logMark)}.",
            NetworkTimeout);
        app.WaitUntilGone(RealApp.CreatePullRequestDialogId, StepTimeout);
        const string listedFive = "listed 5 open pull requests of octo/hello";
        var otherLists = CountLinesSince(app, logMark, line => listed.IsMatch(line) && !line.EndsWith(listedFive, StringComparison.Ordinal));
        Assert.True(otherLists == 0, $"{otherLists} \"listed\" lines of hello since the create do not end with \"{listedFive}\".");

        // The POST: one, 201, with the title, head, base and "draft":false; no push.
        WaitForFake(served, requests, request => request.Route == $"create:{PartN.Repository}", $"POST api/v3/repos/{PartN.Repository}/pulls", NetworkTimeout);
        var post = Assert.Single(Recorded(served, requests), request => request.Route == $"create:{PartN.Repository}");
        Assert.Equal(("POST", true, OctoLogin, 201), (post.Method, post.TokenPresent, post.Login, post.Status));
        AssertNoToken(served, post.Body, "The POST's body");
        using (var body = JsonDocument.Parse(post.Body ?? "{}"))
        {
            var root = body.RootElement;
            Assert.Equal(
                (PartN.TopicTitle, PartN.Topic, PartN.Main, false),
                (root.GetProperty("title").GetString(), root.GetProperty("head").GetString(), root.GetProperty("base").GetString(), root.GetProperty("draft").GetBoolean()));
        }

        Assert.DoesNotContain(served.GitHub.GitRequests.Skip(gitRequests), request => request.Path.EndsWith("git-receive-pack", StringComparison.Ordinal));

        // The status bar's text and its offer, never clicked (13.2); #6 first in the section.
        WaitTelling(
            app,
            () => app.OperationStatusText() == PartN.Created(PartN.NewNumber, draft: false)
                && app.NamesOnScreen("OperationOfferButton").SequenceEqual([PartN.OpenInBrowser]),
            $"the status bar \"{PartN.Created(PartN.NewNumber, draft: false)}\" with \"{PartN.OpenInBrowser}\"",
            () => $"It reads \"{Checked(served, app.OperationStatusText(), "The status bar's text")}\" with \"{string.Join(" | ", app.NamesOnScreen("OperationOfferButton"))}\".");
        WaitTelling(
            app,
            () => app.PullRequestRowNames() is [var first, ..] && first == PartP.PullRequestHeader(PartN.NewNumber, PartN.TopicTitle),
            $"\"{PartP.PullRequestHeader(PartN.NewNumber, PartN.TopicTitle)}\" first in the section",
            () => $"It shows {string.Join(" | ", app.PullRequestRowNames())}.");
        Compare(app, served, PartN.N01b);

        // Step 5.
        AssertCleanEnd(app, served);
    }
}
