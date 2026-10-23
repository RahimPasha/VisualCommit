// Check W6 of part W (docs/test-reports/phase-6.md): failures kept in their rows, and one sign-in
// for two repositories.
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksW
{
    /// <summary>
    /// Check W6: docs and mobile on the folder server with sign-in: one dialog naming docs, its
    /// answer reused for mobile (4.4); scratch's missing remote: <c>Fetch failed</c> with
    /// <c>Details…</c>, no dialog until clicked, then the error dialog (as often as asked, 4.6);
    /// <c>Fetched 7 repositories; 1 failed; 2 skipped</c> in the danger colour; scratch's own Fetch
    /// from its row menu opens the dialog at once (D195).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_W6_failures_and_one_sign_in()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        await PrepareSameFolderAsync(copy);
        using var data = new TempDirectory("data");

        // Step 1: docs and mobile served with sign-in; scratch's origin gone; mobile's first request held.
        await using var server = await Scenarios.ServeWorkspaceAsync(copy, requireAuth: true, "docs", "mobile");
        using (var scratch = copy.Repo("scratch"))
        {
            await scratch.GitAsync("remote", "set-url", "origin", PartW.MissingRemote);
        }

        var mobileGate = server.HoldNextRequest("mobile");
        try
        {
            using var app = await OpenWorkspaceAsync(data, copy);
            var w0 = W0Of(copy);
            AssertRows(app, copy, [.. w0.Values]);
            AssertTd(app, PartW.Workspace, PartW.Count(10));

            // Step 2: Fetch all; one dialog naming docs, the six others ended; then mobile's request let go and answered 401.
            var opened = new List<DialogViewModel>();
            void OnOpened(object? sender, NotifyCollectionChangedEventArgs e) => opened.AddRange(e.NewItems?.OfType<DialogViewModel>() ?? []);
            app.Session.Services.DialogHost.Open.CollectionChanged += OnOpened;
            var logMark = app.LogMark();
            await app.FetchAllAsync();
            var credentials = await app.WaitForDialogAsync<CredentialsViewModel>(NetworkTimeout);
            var ended = new[] { "api", "web-app", "design-system", "billing", "infra", "scratch" };
            foreach (var name in ended)
            {
                await app.WaitForRowAsync(name, row => row.Operation.Text is PartW.FetchedAll or PartW.FetchFailed, NetworkTimeout);
                await app.WaitForIdleAsync(app.ActiveDashboard!.Rows.Single(row => row.Name == name).Repository, NetworkTimeout);
            }

            await mobileGate.WaitAsync(NetworkTimeout);
            server.ReleaseRequest("mobile");
            await app.WaitForAsync(() => server.Requests.Any(request => request.Path == "/mobile.git/info/refs" && request.Status == 401), "mobile's first request to be answered 401", NetworkTimeout);
            app.Settle();
            Shot(app, PartW.W6aOneSignIn);
            AssertCredentialsDialog(app, credentials, server.Port);
            Assert.Same(credentials, app.Session.Services.DialogHost.Current);
            Assert.Equal([credentials], opened);
            var running = new[] { "docs", "mobile" };
            AssertRows(app, copy, [.. w0.Values.Select(row => row.Name switch
            {
                "docs" or "mobile" => row with { Operation = Shown(PartW.FetchingAll) },
                "scratch" => row with { Operation = StatusCell(app, "scratch", PartW.FetchFailed, PartW.Danger) },
                "notes" => row with { Operation = PartW.Reason(PartW.NoRemotesText) },
                "old-site" => row,

                // Fetched from its file remote: 3 behind already (scenario facts 8 item 4).
                "design-system" => row with { Operation = Shown(PartW.FetchedAll), AheadBehind = PartW.WfAheadBehind["design-system"] },
                _ => row with { Operation = Shown(PartW.FetchedAll) },
            })], cancel: running, details: ["scratch"]);
            AssertRowButton(app, "scratch", "DashboardRowDetails");
            AssertDashboardHeader(app, ExpectedDashboardHeader.Running(PartW.Workspace, PartW.Count(10), BulkKind.Fetch, PartW.Fetching(8, 6), PartW.Percent(6, 8)));
            Assert.Equal(
                [("/docs.git/info/refs", (string?)null, 401), ("/mobile.git/info/refs", null, 401)],
                server.Requests.Select(request => (request.Path, request.UserName, request.Status)));

            // Step 3: alice and s3cret (WD24): the dialog closes and none opens again; mobile is answered from the group.
            app.TypeInto("CredentialUsername", PartW.User);
            app.TypeInto("CredentialPassword", PartW.Password);
            app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
            await app.WaitForBulkRunAsync(NetworkTimeout);
            app.MoveToNeutral();
            Shot(app, PartW.W6bFailureInRow);
            Assert.True(credentials.IsClosed, "The sign-in dialog did not close.");
            Assert.Equal([credentials], opened);
            foreach (var name in running)
            {
                var requests = server.Requests.Where(request => request.Path.StartsWith($"/{name}.git/", StringComparison.Ordinal)).ToList();
                Assert.Equal(
                    PartW.SignedInRequests.Select(request => (request.Method, $"/{name}.git{request.PathEnd}", request.Query, request.Command, request.User, request.Status)),
                    requests.Select(request => (request.Method, request.Path, request.Query, request.Command, request.UserName, request.Status)));
            }

            AssertTd(app, PartW.Workspace, PartW.Count(10), PartW.Fetched7Failed1, OperationStatusTone.Danger);
            AssertRows(app, copy, [.. w0.Values.Select(row => row.Name switch
            {
                "scratch" => row with { Operation = StatusCell(app, "scratch", PartW.FetchFailed, PartW.Danger) },
                "notes" => row with { Operation = PartW.Reason(PartW.NoRemotesText) },
                "old-site" => row,
                _ => row with { Operation = Shown(PartW.FetchedAll), AheadBehind = PartW.WfAheadBehind.GetValueOrDefault(row.Name) ?? row.AheadBehind },
            })], details: ["scratch"]);
            await AssertNoDialogAsync(app);
            Assert.True(LinesWith(app, logMark, PartW.FetchEnded(PartW.Fetched7Failed1)).Count == 1, $"The log has no line \"{PartW.FetchEnded(PartW.Fetched7Failed1)}\".");
            await AssertRefsAsync(copy, PartW.WfRefs);
            using (var scratch = copy.Repo("scratch"))
            {
                Assert.Equal(PartW.MissingRemote, (await scratch.GitWithEnvironmentAsync(OptionalLocksOff, "remote", "get-url", "origin")).StandardOutput.Trim());
            }

            var fetch = Assert.Single(CallsIn(app, copy, 0, "scratch"), call => call.CommandText == PartW.FetchCommand);
            Assert.Equal((GitCallOutcome.Completed, (int?)128), (fetch.Outcome, fetch.ExitCode));

            // Step 4: Details…: the dialog the fetch would have opened, as often as asked.
            await app.ClickRowDetailsAsync("scratch");
            await app.WaitForDialogAsync<ErrorDialogViewModel>();
            Shot(app, PartW.W6cDetails);
            var first = await AssertFetchFailedAsync(app, fetch);
            app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
            await AssertNoDialogAsync(app);
            AssertRows(app, copy, [w0["scratch"] with { Operation = StatusCell(app, "scratch", PartW.FetchFailed, PartW.Danger) }], snapshot: OnlyRow(app, "scratch"), details: ["scratch"]);
            await app.ClickRowDetailsAsync("scratch");
            await app.WaitForDialogAsync<ErrorDialogViewModel>();
            var second = await AssertFetchFailedAsync(app, fetch);
            Assert.Equal((first.Title, first.Summary, first.Repository, first.Output, first.Command), (second.Title, second.Summary, second.Repository, second.Output, second.Command));
            Assert.Equal(first.Buttons, second.Buttons);
            app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
            await AssertNoDialogAsync(app);

            // Step 5: scratch's own Fetch from its row menu: the dialog at once (D195).
            var point = RowPoint(app, "scratch");
            var menu = await app.RowMenuAsync("scratch");
            AssertMenu(menu, PartW.ReadyRowMenu("scratch", pullReason: PartW.ScratchPullReason, errorDetails: true));
            AssertRowMenuPlace(app, menu, point);
            Assert.True(Math.Abs(menu.Bounds.Height - MenuHeight(9, 3)) <= PartW.Tolerance, $"scratch's menu is {menu.Bounds.Height} high, not 293.");
            app.ClickMenuItem(PartW.Fetch);
            await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
            var fetches = CallsIn(app, copy, 0, "scratch").Where(call => call.CommandText == PartW.FetchCommand).ToList();
            Assert.Equal(2, fetches.Count);
            Assert.Equal((GitCallOutcome.Completed, (int?)128), (fetches[1].Outcome, fetches[1].ExitCode));
            await AssertFetchFailedAsync(app, fetches[1]);
            app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
            await AssertNoDialogAsync(app);
            await WaitForAllRowsIdleAsync(app, NetworkTimeout);
            AssertRows(app, copy, [w0["scratch"] with { Operation = StatusCell(app, "scratch", PartW.FetchFailed, PartW.Danger) }], snapshot: OnlyRow(app, "scratch"));
            AssertDashboardHeader(app, ExpectedDashboardHeader.Idle(PartW.Workspace, PartW.Count(10)) with { RunText = PartW.Fetched7Failed1, RunTone = OperationStatusTone.Danger });
            app.Session.Services.DialogHost.Open.CollectionChanged -= OnOpened;
        }
        finally
        {
            server.ReleaseRequest("mobile");
        }
    }

    /// <summary>
    /// The sign-in dialog (W6 step 2; phase 3's with the port and 3.8's line): <c>Sign in to
    /// 127.0.0.1:{port}</c>, its text from git's prompt <c>Username for 'http://127.0.0.1:{port}': </c>,
    /// <c>In docs</c>, the user name box empty with the focus and the password box empty under their
    /// labels, <c>Cancel</c> and <c>Sign in</c>, Sign in disabled while the user name is empty; over
    /// the dashboard, dimmed by the backdrop.
    /// </summary>
    private static void AssertCredentialsDialog(ShellDriver app, CredentialsViewModel dialog, int port)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The sign-in dialog has no view.");
        Assert.Equal(PartW.UsernamePrompt(port), dialog.Prompt.Text);
        Assert.Equal(PartW.SignInTitle(port), ((TextBlock)app.FindByAutomationId(view, "DialogTitle")).Text);
        Assert.Equal(PartW.SignInText(port), ((TextBlock)app.FindByAutomationId(view, "CredentialsText")).Text);
        var repository = (TextBlock)app.FindByAutomationId(view, "DialogRepository");
        Assert.True(repository.IsEffectivelyVisible, "The sign-in dialog shows no repository line (3.8).");
        Assert.Equal("In docs", repository.Text);
        var user = (TextBox)app.FindByAutomationId(view, "CredentialUsername");
        var password = (TextBox)app.FindByAutomationId(view, "CredentialPassword");
        Assert.True(string.IsNullOrEmpty(user.Text) && string.IsNullOrEmpty(password.Text), "The sign-in dialog's boxes are not empty.");
        Assert.True(user.IsFocused, "The user name box does not have the keyboard focus.");
        var labels = view.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Classes.Contains("promptLabel") && text.IsEffectivelyVisible).ToList();
        Assert.Equal(["User name", "Password"], labels.Select(label => label.Text));
        Assert.True(app.BoundsOf(labels[0]).Bottom <= app.BoundsOf(user).Top + 0.5 && app.BoundsOf(labels[1]).Bottom <= app.BoundsOf(password).Top + 0.5, "The labels are not over their boxes.");
        Assert.Equal(["Cancel", "Sign in"], DialogButtons(app, view));
        Assert.False(((Button)app.FindByAutomationId(view, "ConfirmButton")).IsEffectivelyEnabled, "Sign in is enabled while the user name is empty.");
        Assert.Contains(view.GetVisualDescendants().OfType<Panel>(), panel => panel.Classes.Contains("dialogBackdrop") && panel.IsEffectivelyVisible);
    }

    /// <summary>
    /// The error dialog of scratch's failed fetch (W6 steps 4 and 5; D109's <c>FetchAllFailureDisplay</c>):
    /// <c>Fetch failed</c>, <c>Git could not fetch from origin.</c>, <c>In scratch</c>, the command,
    /// <c>Copy output</c> and <c>Close</c>; its output git's standard error cleaned, then the app's
    /// own <c>error: Could not fetch origin</c> last (on every git), word for word on 2.36.
    /// </summary>
    private static async Task<ErrorDialogSnapshot> AssertFetchFailedAsync(ShellDriver app, GitCallRecord call)
    {
        var dialog = app.ErrorDialog();
        Assert.Equal(
            (PartW.FetchFailed, PartW.FetchFailedSummary, "In scratch", PartW.FetchCommand),
            (dialog.Title, dialog.Summary, dialog.Repository, dialog.Command));
        Assert.Equal(["Copy output", "Close"], dialog.Buttons);
        var output = dialog.Output ?? throw new Xunit.Sdk.XunitException("The error dialog shows no output.");
        Assert.Equal(PartW.CouldNotFetchOrigin, dialog.OutputLines[^1]);
        Assert.Equal(ShellDriver.CleanedOutputOf(call), string.Join('\n', dialog.OutputLines.SkipLast(1)));
        AssertGitOutputShown(call, PartW.MissingRemoteKey, output, [PartW.CouldNotFetchOrigin]);
        if (await IsReportGitAsync())
        {
            Assert.Equal([.. PartW.MissingRemoteLines, PartW.CouldNotFetchOrigin], dialog.OutputLines);
        }

        return dialog;
    }

    /// <summary>
    /// An Operation cell the row's view model's status fills (2.6): its text in its tone, its tooltip
    /// <c>CompactToolTip</c>, the status bar's whole text or the status's own tooltip where it has one
    /// (the report names none for a failure's cell).
    /// </summary>
    private static PartW.Cell StatusCell(ShellDriver app, string name, string text, string tone)
    {
        var repository = (app.ActiveDashboard ?? throw new InvalidOperationException("The active tab is no dashboard."))
            .Rows.Single(row => row.Name == name).Repository
            ?? throw new Xunit.Sdk.XunitException($"The row {name} has no view model.");
        return new PartW.Cell(text, tone, repository.OperationStatus.CompactToolTip);
    }

    /// <summary>A snapshot of the active dashboard with only its row <paramref name="name"/>, for asserting one row.</summary>
    private static DashboardSnapshot OnlyRow(ShellDriver app, string name)
    {
        var snapshot = app.DashboardSnapshot();
        return snapshot with { Rows = [snapshot.Row(name) with { IsSelected = false }], SelectedRow = null };
    }
}
