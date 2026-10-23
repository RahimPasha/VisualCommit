// Checks W4 and W5 of part W (docs/test-reports/phase-6.md): Fetch all with progress over the
// folder server (W4); Cancel all, and a row's own Cancel, under the fetch hold, in two methods on
// fresh copies (W5, WD36).
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
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
    /// Check W4: Fetch all with progress (design 4.2 to 4.4, 4.9, 4.10; D210, D229): the folder
    /// server holds docs, mobile and design-system at 25, 75 and 45 percent; their compact texts
    /// with Cancel, the five others fetched, notes and old-site left out, <c>Fetching 8
    /// repositories… 5 done</c> with the bar at 62 and Cancel all; released, <c>Fetched 8
    /// repositories; 2 skipped</c> and WF.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_W4_fetch_all_progress()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        await PrepareSameFolderAsync(copy);
        using var data = new TempDirectory("data");

        // Step 1: docs, mobile and design-system served over HTTP, each held at its percentage.
        await using var server = await Scenarios.ServeWorkspaceAsync(copy, requireAuth: false, "docs", "mobile", "design-system");
        var holds = PartW.W4Holds.Select(hold => server.HoldNextFetchPack(hold.Name, hold.Percent)).ToArray();
        try
        {
            using var app = await OpenWorkspaceAsync(data, copy);
            var w0 = W0Of(copy);
            AssertRows(app, copy, [.. w0.Values]);
            foreach (var name in PartW.WithRemote)
            {
                using var repo = copy.Repo(name);
                var url = (await repo.GitWithEnvironmentAsync(OptionalLocksOff, "remote", "get-url", "origin")).StandardOutput.Trim();
                var expected = PartW.W4Holds.Any(hold => hold.Name == name)
                    ? WithPort($"http://127.0.0.1:47393/{name}.git", server.Port)
                    : $"../remotes/{name}.git";
                Assert.Equal(expected, url);
            }

            AssertTd(app, PartW.Workspace, PartW.Count(10));

            // Step 2: Fetch all; the three holds taken, the five others fetched.
            var logMark = app.LogMark();
            await app.FetchAllAsync();
            await Task.WhenAll(holds).WaitAsync(NetworkTimeout);
            foreach (var (name, percent) in PartW.W4Holds)
            {
                await app.WaitForRowPercentAsync(name, percent, NetworkTimeout);
            }

            var done = new[] { "api", "web-app", "billing", "infra", "scratch" };
            foreach (var name in done)
            {
                await app.WaitForRowAsync(name, row => row.Operation.Text == PartW.FetchedAll, NetworkTimeout);
                await app.WaitForIdleAsync(app.ActiveDashboard!.Rows.Single(row => row.Name == name).Repository, NetworkTimeout);
            }

            Shot(app, PartW.W4aHeldAt257545);
            Assert.True(LinesWith(app, logMark, PartW.FetchStarted).Count == 1, $"The log has no line \"{PartW.FetchStarted}\".");
            var rows = w0.Values.Select(row => row.Name switch
            {
                "docs" or "mobile" or "design-system" => row with { Operation = new PartW.Cell(PartW.FetchingAt(PercentOf(row.Name)), PartW.Secondary, PartW.FetchingAtToolTip(PercentOf(row.Name))) },
                "notes" => row with { Operation = PartW.Reason(PartW.NoRemotesText) },
                "old-site" => row,
                _ => row with { Operation = Shown(PartW.FetchedAll) },
            }).ToList();
            AssertRows(app, copy, rows, cancel: ["docs", "mobile", "design-system"]);
            foreach (var (name, _) in PartW.W4Holds)
            {
                AssertRowButton(app, name, "DashboardRowCancel");
            }

            Assert.DoesNotContain(app.DashboardSnapshot().Rows, row => row.Operation.Text == PartW.WaitingToFetch);
            AssertTr(app, PartW.Count(10), PartW.Fetching(8, 5), PartW.Percent(5, 8), waiting: null);
            AssertRunPart(app);
            await AssertNoDialogAsync(app);
            var fetches = app.GitCalls().Where(call => call.CommandText == PartW.FetchCommand).ToList();
            Assert.True(fetches.Count == 5, $"{fetches.Count} fetches are recorded, not 5:\n" + string.Join('\n', fetches.Select(call => call.WorkingDirectory)));
            foreach (var name in done)
            {
                var fetch = Assert.Single(fetches, call => call.WorkingDirectory is { } folder && SameFolder(copy, copy.PathOf(name), folder));
                Assert.Equal((GitCallOutcome.Completed, (int?)0), (fetch.Outcome, fetch.ExitCode));
            }

            await AssertRefsAsync(copy, PartW.W0Refs);

            // Step 3: released: WF, the run's result.
            server.Release();
            await app.WaitForBulkRunAsync(NetworkTimeout);
            app.MoveToNeutral();
            Shot(app, PartW.W4bFetched);
            var wf = w0.Values.Select(row => row.Name switch
            {
                "notes" => row with { Operation = PartW.Reason(PartW.NoRemotesText) },
                "old-site" => row,
                _ => row with { Operation = Shown(PartW.FetchedAll), AheadBehind = PartW.WfAheadBehind.GetValueOrDefault(row.Name) ?? row.AheadBehind },
            }).ToList();
            AssertRows(app, copy, wf);
            AssertTd(app, PartW.Workspace, PartW.Count(10), PartW.Fetched8Skipped2);
            Assert.True(LinesWith(app, logMark, PartW.FetchEnded(PartW.Fetched8Skipped2)).Count == 1, $"The log has no line \"{PartW.FetchEnded(PartW.Fetched8Skipped2)}\".");
            await AssertNoDialogAsync(app);
            fetches = [.. app.GitCalls().Where(call => call.CommandText == PartW.FetchCommand)];
            Assert.Equal(8, fetches.Count);
            foreach (var name in PartW.WithRemote)
            {
                var fetch = Assert.Single(fetches, call => call.WorkingDirectory is { } folder && SameFolder(copy, copy.PathOf(name), folder));
                Assert.Equal((GitCallOutcome.Completed, (int?)0), (fetch.Outcome, fetch.ExitCode));
            }

            await AssertRefsAsync(copy, PartW.WfRefs);
            AssertStatusLines("design-system", ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -3", .. PartW.W0Status["design-system"][3..]], await StatusLinesAsync(copy, "design-system"));
        }
        finally
        {
            server.Release();
        }
    }

    /// <summary>
    /// Check W5, part A (steps 1 to 4): Fetch all under the fetch hold in the eight with a remote:
    /// four running, four <c>Waiting to fetch…</c>, <c>4 waiting</c>; Cancel all: the four in line
    /// cancelled at once, <c>Cancelling: waiting for 4 repositories…</c> recorded (WD12), then
    /// <c>Fetched no repositories; 8 cancelled; 2 skipped</c>, no dialog; after an F5 under the hold
    /// notes still <c>No remote</c> and left out (WD33).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_W5_cancel_all()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        await PrepareSameFolderAsync(copy);
        using var data = new TempDirectory("data");

        // Step 1: the hold in the eight with a remote.
        using var hold = FetchHold.Install(data.Path);
        try
        {
            hold.HoldIn([.. PartW.WithRemote.Select(copy.PathOf)]);
            using var app = await OpenWorkspaceAsync(data, copy);
            var w0 = W0Of(copy);

            // Step 2: Fetch all: four running, four in line.
            using var recorder = new RunTextRecorder(app.ActiveDashboard!.Bulk);
            var logMark = app.LogMark();
            await app.FetchAllAsync();
            await hold.WaitForHeldAsync(4);
            var inLine = new[] { "design-system", "billing", "infra", "scratch" };
            foreach (var name in inLine)
            {
                await app.WaitForRowAsync(name, row => row.Operation.Text == PartW.WaitingToFetch);
            }

            app.Settle();
            Shot(app, PartW.W5aFourRunning);
            var running = new[] { "api", "web-app", "docs", "mobile" };
            Assert.True(LinesWith(app, logMark, PartW.FetchStarted).Count == 1, $"The log has no line \"{PartW.FetchStarted}\".");
            AssertRows(app, copy, [.. w0.Values.Select(row => Running(row, running, inLine))], cancel: running);
            foreach (var name in running)
            {
                AssertRowButton(app, name, "DashboardRowCancel");
            }

            AssertTr(app, PartW.Count(10), PartW.Fetching(8), 0, PartW.Waiting(4));
            AssertActivityToggle(app, enabled: false, PartW.SelectRepository);
            Assert.Equal(4, hold.Held);
            var markers = Directory.GetFiles(hold.Folder, FetchHold.MarkerPrefix + "*").Select(Path.GetFileName).OfType<string>().ToList();
            Assert.Equal(running.Select(name => FetchHold.MarkerPrefix + name + ".git.").Order(StringComparer.Ordinal), markers.Select(marker => marker[..(marker.LastIndexOf('.') + 1)]).Order(StringComparer.Ordinal));

            // Step 3: Cancel all.
            var before = recorder.Texts.Count;
            await app.CancelAllAsync();
            Assert.False(app.DashboardSnapshot().Header!.CancelAll.IsShown, "Cancel all still shows after its click.");
            Assert.False(app.FindByAutomationId("OperationCancelButton").IsEffectivelyVisible, "The status bar's Cancel still shows after Cancel all's click.");
            await app.WaitForBulkRunAsync(NetworkTimeout);
            var mark = app.CallMark();
            app.MoveToNeutral();
            Shot(app, PartW.W5bAllCancelled);
            AssertCancellingTexts(recorder.Texts.Skip(before).ToList(), PartW.FetchedNoneCancelled8);
            AssertRows(app, copy, [.. w0.Values.Select(row => row.Name switch
            {
                "notes" => row with { Operation = PartW.Reason(PartW.NoRemotesText) },
                "old-site" => row,
                _ => row with { Operation = Shown(PartW.FetchCancelled) },
            })]);
            AssertTd(app, PartW.Workspace, PartW.Count(10), PartW.FetchedNoneCancelled8);
            Assert.False(app.DashboardSnapshot().Header!.CancelAll.IsShown, "Cancel all shows after the run.");
            await AssertNoDialogAsync(app);
            Assert.True(LinesWith(app, logMark, PartW.FetchEnded(PartW.FetchedNoneCancelled8)).Count == 1, $"The log has no line \"{PartW.FetchEnded(PartW.FetchedNoneCancelled8)}\".");
            var fetches = app.GitCalls().Where(call => call.CommandText == PartW.FetchCommand).ToList();
            Assert.True(fetches.Count == 4, $"{fetches.Count} fetches are recorded, not 4.");
            foreach (var name in running)
            {
                var fetch = Assert.Single(fetches, call => call.WorkingDirectory is { } folder && SameFolder(copy, copy.PathOf(name), folder));
                Assert.Equal(GitCallOutcome.Cancelled, fetch.Outcome);
            }

            await AssertRefsAsync(copy, PartW.W0Refs);
            Assert.Empty(LockFiles(copy));

            // Step 4: api clicked, so that the list has the focus (WD33); F5 under the hold.
            logMark = app.LogMark();
            app.Click(RowPoint(app, "api"));
            app.PressKey(Key.F5);
            await app.WaitForDashboardReadAsync(since: logMark, timeout: OperationChainTimeout);
            await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
            AssertActivityToggle(app, enabled: true, PartW.ShowActivityLog);
            AssertRows(app, copy, [.. w0.Values.Select(row => row.Name switch
            {
                "notes" => row with { Operation = PartW.Reason(PartW.NoRemotesText) },
                "old-site" => row,
                _ => row with { Operation = Shown(PartW.FetchCancelled) },
            })], selected: "api");
            var since = app.CallsSince(mark);
            Assert.DoesNotContain(since, call => call.CommandText.StartsWith("git fetch", StringComparison.Ordinal));
            AssertOnlyReads(since, "W5's F5");
        }
        finally
        {
            hold.Dispose();
        }
    }

    /// <summary>
    /// Check W5, part B (steps 5 and 6; WD36): on a fresh start under the hold, docs' own Cancel
    /// stops docs only, its slot goes to design-system; released, <c>Fetched 7 repositories; 1
    /// cancelled; 2 skipped</c>.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_W5_row_cancel()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        await PrepareSameFolderAsync(copy);
        using var data = new TempDirectory("data");

        // Step 5.
        using var hold = FetchHold.Install(data.Path);
        try
        {
            hold.HoldIn([.. PartW.WithRemote.Select(copy.PathOf)]);
            using var app = await OpenWorkspaceAsync(data, copy);
            var w0 = W0Of(copy);
            var logMark = app.LogMark();
            await app.FetchAllAsync();
            await hold.WaitForHeldAsync(4);
            app.ClickRowCancel("docs");
            await hold.WaitForHeldAsync(5);
            await app.WaitForRowAsync("docs", row => row.Operation.Text == PartW.FetchCancelled);
            await app.WaitForRowAsync("design-system", row => row.Operation.Text == PartW.FetchingAll);
            app.Settle();
            Shot(app, PartW.W5cRowCancelled);
            Assert.True(LinesWith(app, logMark, PartW.FetchStarted).Count == 1, $"The log has no line \"{PartW.FetchStarted}\".");
            var running = new[] { "api", "web-app", "mobile", "design-system" };
            var inLine = new[] { "billing", "infra", "scratch" };
            AssertRows(app, copy, [.. w0.Values.Select(row => row.Name == "docs" ? row with { Operation = Shown(PartW.FetchCancelled) } : Running(row, running, inLine))], cancel: running);
            foreach (var name in running)
            {
                AssertRowButton(app, name, "DashboardRowCancel");
            }

            AssertTr(app, PartW.Count(10), PartW.Fetching(8, 1), PartW.Percent(1, 8), PartW.Waiting(3));
            AssertActivityToggle(app, enabled: false, PartW.SelectRepository);
            Assert.Equal(5, hold.Held);

            // Step 6: released.
            hold.Release();
            await app.WaitForBulkRunAsync(NetworkTimeout);
            app.MoveToNeutral();
            Shot(app, PartW.W5dRowCancelRunEnded);
            AssertRows(app, copy, [.. w0.Values.Select(row => row.Name switch
            {
                "docs" => row with { Operation = Shown(PartW.FetchCancelled) },
                "notes" => row with { Operation = PartW.Reason(PartW.NoRemotesText) },
                "old-site" => row,
                "mobile" or "design-system" => row with { Operation = Shown(PartW.FetchedAll), AheadBehind = PartW.WfAheadBehind[row.Name] },
                _ => row with { Operation = Shown(PartW.FetchedAll) },
            })]);
            AssertTd(app, PartW.Workspace, PartW.Count(10), PartW.Fetched7Cancelled1);
            await AssertNoDialogAsync(app);
            await AssertRefsAsync(copy, PartW.W5PartBRefs);
            Assert.Empty(LockFiles(copy));
        }
        finally
        {
            hold.Dispose();
        }
    }

    /// <summary>W4's held percentage of <paramref name="name"/>.</summary>
    private static int PercentOf(string name) => PartW.W4Holds.Single(hold => hold.Name == name).Percent;

    /// <summary>A row of W0 during a run under the hold: running (<c>Fetching from all remotes…</c>, no percentage), in line (<c>Waiting to fetch…</c>), notes left out, old-site as it was.</summary>
    private static PartW.Row Running(PartW.Row row, IReadOnlyCollection<string> running, IReadOnlyCollection<string> inLine) => row.Name switch
    {
        "notes" => row with { Operation = PartW.Reason(PartW.NoRemotesText) },
        "old-site" => row,
        _ when running.Contains(row.Name) => row with { Operation = Shown(PartW.FetchingAll) },
        _ when inLine.Contains(row.Name) => row with { Operation = Shown(PartW.WaitingToFetch) },
        _ => throw new ArgumentException($"{row.Name} is neither running nor in line.", nameof(row)),
    };

    /// <summary>
    /// The header's run part (2.5, 4.9): right-aligned 16 before <c>Add repository…</c>, the run's
    /// text, 8, the bar (80×4), 8, <c>Cancel all</c> (named <c>Cancel all</c>) (±2).
    /// </summary>
    private static void AssertRunPart(ShellDriver app)
    {
        var text = app.BoundsOf((Control)app.FindByAutomationId("DashboardBulkText"));
        var bar = app.BoundsOf((Control)app.FindByAutomationId("DashboardBulkProgress"));
        var cancel = (Button)app.FindByAutomationId("DashboardCancelAll");
        var cancelBounds = app.BoundsOf(cancel);
        var add = app.BoundsOf((Control)app.FindByAutomationId("DashboardAddRepository"));
        Assert.True(Math.Abs(bar.Width - 80) <= 0.5 && Math.Abs(bar.Height - 4) <= 0.5, $"The run's bar is {bar.Size}, not 80×4.");
        Assert.True(Math.Abs(bar.Left - text.Right - 8) <= PartW.Tolerance && Math.Abs(cancelBounds.Left - bar.Right - 8) <= PartW.Tolerance, $"The run's text, bar and Cancel all are at {text}, {bar}, {cancelBounds}: not 8 apart.");
        Assert.True(Math.Abs(add.Left - cancelBounds.Right - 16) <= PartW.Tolerance, $"The run's part ends at x {cancelBounds.Right}, not 16 before Add repository… ({add.Left}).");
        Assert.Equal("Cancel all", Avalonia.Automation.AutomationProperties.GetName(cancel));
        Assert.False(((ProgressBar)app.FindByAutomationId("DashboardBulkProgress")).IsIndeterminate, "The run's bar is indeterminate (4.9: determinate).");
    }

    /// <summary>
    /// The header's texts after Cancel all's click (WD12): <c>Cancelling: waiting for 4
    /// repositories…</c> among them; before it at most <c>Fetching 8 repositories… 4 done</c>; after it
    /// only <c>Cancelling: waiting for {r} …</c> with <c>{r}</c> falling below 4; the result last.
    /// </summary>
    private static void AssertCancellingTexts(IReadOnlyList<string> texts, string result)
    {
        var shown = "The run's texts after Cancel all's click:\n  " + string.Join("\n  ", texts);
        var at = texts.ToList().IndexOf(PartW.Cancelling(4));
        Assert.True(at >= 0, shown);
        Assert.True(texts.Take(at).All(text => text == PartW.Fetching(8, 4)), shown);
        Assert.True(texts.Count > at + 1 && texts[^1] == result, shown);
        var last = 4;
        foreach (var text in texts.Skip(at + 1).Take(texts.Count - at - 2))
        {
            var r = Enumerable.Range(1, 3).FirstOrDefault(count => text == PartW.Cancelling(count));
            Assert.True(r > 0 && r < last, shown);
            last = r;
        }
    }
}
