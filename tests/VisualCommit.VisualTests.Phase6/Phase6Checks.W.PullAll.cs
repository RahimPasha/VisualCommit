// Check W7 of part W (docs/test-reports/phase-6.md): Pull all, its failures kept for Details…, and
// docs' pull undone from its tab.
using System.Collections.Specialized;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksW
{
    /// <summary>
    /// Check W7: Pull all with <c>pull.ff = only</c> in mobile (design 4.2, 4.6, 4.8, 4.9, 4.11): api and
    /// web-app up to date, docs fast-forwarded by 4 commits, mobile and design-system <c>Pull
    /// failed</c> with <c>Details…</c>, the other five left out with 4.2's reasons, <c>Pulled 3
    /// repositories; 2 failed; 5 skipped</c>; docs opened afterwards from its row offers <c>Undo pull
    /// into main</c> and undoes it.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_W7_pull_all()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        await PrepareSameFolderAsync(copy);
        using var data = new TempDirectory("data");

        // Step 1: pull.ff = only in mobile's .git/config, before the start.
        using (var mobile = copy.Repo("mobile"))
        {
            await mobile.GitAsync("config", "pull.ff", "only");
        }

        using var app = await OpenWorkspaceAsync(data, copy);
        var w0 = W0Of(copy);
        AssertRows(app, copy, [.. w0.Values]);
        AssertTd(app, PartW.Workspace, PartW.Count(10));

        // Step 2: Pull all, to its end; no dialog opened during the run (D195).
        var opened = new List<DialogViewModel>();
        void OnOpened(object? sender, NotifyCollectionChangedEventArgs e) => opened.AddRange(e.NewItems?.OfType<DialogViewModel>() ?? []);
        app.Session.Services.DialogHost.Open.CollectionChanged += OnOpened;
        var logMark = app.LogMark();
        var calls = app.CallMark();
        await app.PullAllAsync();
        await app.WaitForBulkRunAsync(OperationChainTimeout);
        app.Session.Services.DialogHost.Open.CollectionChanged -= OnOpened;
        app.MoveToNeutral();
        Shot(app, PartW.W7aPullAllEnded);
        Assert.Empty(opened);
        Assert.True(LinesWith(app, logMark, PartW.PullStarted).Count == 1, $"The log has no line \"{PartW.PullStarted}\".");
        var wp = w0.Values.Select(row => row.Name switch
        {
            "api" or "web-app" => row with { Operation = Shown(PartW.MainUpToDate) },
            "docs" => row with { AheadBehind = PartW.UpToDate(), Operation = Shown(PartW.PulledDocs) },
            "mobile" => row with { AheadBehind = PartW.Arrows(1, 3), Operation = StatusCell(app, "mobile", PartW.PullFailed, PartW.Danger) },
            "design-system" => row with { AheadBehind = PartW.Arrows(0, 3), Operation = StatusCell(app, "design-system", PartW.PullFailed, PartW.Danger) },
            "billing" => row with { Operation = PartW.Reason(PartW.BillingPullReason) },
            "infra" => row with { Operation = PartW.Reason(PartW.InfraPullReason) },
            "scratch" => row with { Operation = PartW.Reason(PartW.ScratchPullReason) },
            "notes" => row with { Operation = PartW.Reason(PartW.NotesPullReason) },
            _ => row,
        }).ToList();
        AssertRows(app, copy, wp, details: ["mobile", "design-system"]);
        AssertRowButton(app, "mobile", "DashboardRowDetails");
        AssertRowButton(app, "design-system", "DashboardRowDetails");
        AssertTd(app, PartW.Workspace, PartW.Count(10), PartW.Pulled3Failed2, OperationStatusTone.Danger);
        Assert.True(LinesWith(app, logMark, PartW.PullEnded(PartW.Pulled3Failed2)).Count == 1, $"The log has no line \"{PartW.PullEnded(PartW.Pulled3Failed2)}\".");
        var reportGit = await IsReportGitAsync();
        var merges = new Dictionary<string, GitCallRecord>(StringComparer.Ordinal);
        foreach (var name in new[] { "api", "web-app", "docs", "mobile", "design-system" })
        {
            var inFolder = CallsIn(app, copy, calls, name);
            var texts = inFolder.Select(call => call.CommandText).ToList();
            var merge = name == "mobile" ? PartW.FastForwardOnlyCommand : PartW.MergeCommand;
            var order = new[] { PartW.PullFastForwardRead, PartW.FetchCommand, PartW.PullAddedRead, merge }
                .Select(command => texts.IndexOf(command)).ToList();
            Assert.True(order.All(at => at >= 0) && order.SequenceEqual(order.Order()), $"{name}'s pull did not run its reads, fetch, read and merge in this order:\n" + string.Join('\n', texts));
            var added = inFolder.Single(call => call.CommandText == PartW.PullAddedRead);
            Assert.Equal(PartW.AddedByPull[name], added.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries));
            var record = inFolder.Single(call => call.CommandText == merge);
            merges[name] = record;
            var exit = name switch { "mobile" => 128, "design-system" => 1, _ => 0 };
            Assert.True(record.ExitCode == exit, $"{name}'s merge exited {record.ExitCode}, not {exit}.");
            switch (name)
            {
                case "api" or "web-app":
                    Assert.Equal(PartW.AlreadyUpToDate, record.StandardOutput.Trim());
                    break;
                case "docs":
                    Assert.Contains("Fast-forward", record.StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')));
                    if (reportGit)
                    {
                        Assert.Equal(PartW.DocsFastForwardLines, record.StandardOutput.TrimEnd('\n').Split('\n'));
                    }

                    break;
            }
        }

        Assert.DoesNotContain(app.CallsSince(calls), call => call.CommandText.StartsWith("git check-ignore", StringComparison.Ordinal));
        foreach (var name in new[] { "billing", "infra", "scratch", "notes", "old-site" })
        {
            Assert.Empty(CallsIn(app, copy, calls, name));
        }

        await AssertRefsAsync(copy, PartW.WpRefs);
        using (var docs = copy.Repo("docs"))
        {
            Assert.Equal(PartW.DocsPulledFiles, WorkingFiles(docs.Path));
        }

        Assert.False(File.Exists(Path.Combine(copy.PathOf("mobile"), ".git", "MERGE_HEAD")), "mobile has a MERGE_HEAD.");
        AssertStatusLines("design-system", ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -3", .. PartW.W0Status["design-system"][3..]], await StatusLinesAsync(copy, "design-system"));

        // Step 3: mobile's Details…: 4.8's PullNotFastForward.
        await app.ClickRowDetailsAsync("mobile");
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Shot(app, PartW.W7bNotFastForwardDetails);
        await AssertPullFailedAsync(app, merges["mobile"], "mobile", PartW.NotFastForwardSummary, PartW.FastForwardOnlyCommand, PartW.NotFastForwardKey, PartW.NotFastForwardLines);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await AssertNoDialogAsync(app);

        // Step 4: design-system's Details…: git's standard error alone, never the Updating line of its standard output.
        await app.ClickRowDetailsAsync("design-system");
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Shot(app, PartW.W7cLocalChangesDetails);
        var blocked = await AssertPullFailedAsync(app, merges["design-system"], "design-system", PartW.LocalChangesSummary, PartW.MergeCommand, PartW.LocalChangesKey, PartW.LocalChangesLines);
        Assert.DoesNotContain(PartW.DesignSystemUpdating, blocked.OutputLines);
        Assert.Contains(PartW.DesignSystemUpdating, merges["design-system"].StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')));
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await AssertNoDialogAsync(app);

        // Step 5: docs opened by a double-click: its tab adopts the row's view model (3.3) and offers the pull's undo.
        var row = app.ActiveDashboard!.Rows.Single(candidate => candidate.Name == "docs").Repository;
        app.DoubleClick(RowPoint(app, "docs"));
        var repository = await app.WaitForRepositoryAsync(OperationChainTimeout);
        var undo = ToolbarButton(app, "UndoButton");
        var tip = await app.RestPointerOnAsync(undo);
        var picture = Shot(app, PartW.W7dUndoPullToolTip, keepPointer: true);
        Assert.Equal([PartW.Workspace, "docs"], TabTitles(app));
        Assert.Same(app.Session.Shell.Tabs[1], app.Session.Shell.ActiveTab);
        Assert.Same(row, repository);
        Assert.False(repository.IsSummary, "docs' tab holds a summary-mode view model.");
        AssertGraph(app, [.. PartW.DocsPulled.Select(commit => new CommitRow(commit.Message, commit.Lane, commit.Colour, commit.Labels, commit.Sha))], lanes: 1, picture: picture);
        AssertBranch(app, "main");
        AssertStatus(app, PartW.PulledDocs);
        Assert.Equal(PartW.UndoPullToolTip, tip);
        Assert.Equal(PartW.UndoPullToolTip, Avalonia.Automation.AutomationProperties.GetHelpText(undo));
        Assert.Equal(UndoRedoButtons.CanUndo("pull into main"), app.UndoRedoState());

        // Step 6: Undo: the Keep move's calls, no question, no dialog; DU.
        var mark = app.LogMark();
        var count = app.GitCalls().Count;
        await app.ClickUndoAsync(OperationChainTimeout);
        await app.WaitForLogLineAsync(AppLogFile.OperationEnded("docs", PartW.UndoPullTitle, OperationOutcome.Succeeded, PartW.UndidPull), mark: mark, timeout: OperationChainTimeout);
        await app.WaitForIdleAsync(timeout: OperationChainTimeout);
        app.MoveToNeutral();
        picture = Shot(app, PartW.W7ePullUndone);
        await AssertNoDialogAsync(app);
        Assert.Equal(1, app.CountLogLines(AppLogFile.OperationEnded("docs", PartW.UndoPullTitle), mark));
        AssertStatus(app, PartW.UndidPull);
        AssertCalledInOrder(app, count, PartW.UpdateIndexRefresh, PartW.UndoReadTree, PartW.UndoUpdateRef);
        await AssertRefsAsync(copy, PartW.UndoneRefs);
        AssertStatusLines("docs", ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -4"], await StatusLinesAsync(copy, "docs"));
        Assert.Equal(PartW.DocsUndoneFiles, WorkingFiles(copy.PathOf("docs")));
        using (var docs = copy.Repo("docs"))
        {
            foreach (var log in new[] { "HEAD", "refs/heads/main" })
            {
                var newest = (await docs.GitWithEnvironmentAsync(OptionalLocksOff, "log", "-g", "-1", "--format=%gs", log)).StandardOutput.Trim();
                Assert.Equal(PartW.UndoReflogMessage, newest);
            }
        }

        AssertGraph(app, [.. PartW.DocsUndone.Select(commit => new CommitRow(commit.Message, commit.Lane, commit.Colour, commit.Labels, commit.Sha))], lanes: 1, picture: picture);
        Assert.Equal(UndoRedoButtons.CanRedo("pull into main"), app.UndoRedoState());

        // Step 7: the Web tab: docs' row follows its tab (3.5), with no read by the dashboard.
        var logMark7 = app.LogMark();
        app.Click(app.TabStrip().Tabs[0].Bounds.Center);
        app.Settle();
        Assert.NotNull(app.ActiveDashboard);
        AssertRows(app, copy, [wp.Single(candidate => candidate.Name == "docs") with { AheadBehind = PartW.Arrows(0, 4), Operation = Shown(PartW.UndidPull) }], snapshot: OnlyRow(app, "docs"));
        Assert.Empty(LinesWith(app, logMark7, PartW.SummaryRead("docs")));
        AssertNoSummaryFor(app, copy, "docs");
        AssertDashboardHeader(app, ExpectedDashboardHeader.Idle(PartW.Workspace, PartW.Count(10)) with { RunText = PartW.Pulled3Failed2, RunTone = OperationStatusTone.Danger });
        await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
        AssertRows(app, copy, [.. wp.Select(candidate => candidate.Name == "docs" ? candidate with { AheadBehind = PartW.Arrows(0, 4), Operation = Shown(PartW.UndidPull) } : candidate)], selected: "docs", details: ["mobile", "design-system"]);
    }

    /// <summary>
    /// A kept Pull failure's error dialog (W7 steps 3 and 4): <c>Pull failed</c>, the summary,
    /// <c>In {repository}</c>, the command, <c>Copy output</c> and <c>Close</c>; its output
    /// <c>GitOutputCleaner.Clean</c> of the failed merge's own call (its standard error), its key line
    /// on every git and the report's lines word for word on 2.36.
    /// </summary>
    private static async Task<ErrorDialogSnapshot> AssertPullFailedAsync(ShellDriver app, GitCallRecord call, string repository, string summary, string command, string key, IReadOnlyList<string> lines)
    {
        var dialog = app.ErrorDialog();
        Assert.Equal((PartW.PullFailed, summary, "In " + repository, command), (dialog.Title, dialog.Summary, dialog.Repository, dialog.Command));
        Assert.Equal(["Copy output", "Close"], dialog.Buttons);
        var output = dialog.Output ?? throw new Xunit.Sdk.XunitException("The error dialog shows no output.");
        Assert.False(call.Succeeded, $"The call \"{call.CommandText}\" succeeded; the dialog shows a failure.");
        Assert.Equal(ShellDriver.CleanedOutputOf(call), output);
        AssertGitOutputShown(call, key, output);
        if (await IsReportGitAsync())
        {
            Assert.Equal(lines, dialog.OutputLines);
        }

        return dialog;
    }

    /// <summary>The working tree's files outside <c>.git</c> with their texts, in ordinal order.</summary>
    private static (string Path, string Text)[] WorkingFiles(string folder) =>
    [
        .. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(folder, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0].Equals(".git", StringComparison.Ordinal))
            .Select(file => (Path.GetRelativePath(folder, file).Replace('\\', '/'), File.ReadAllText(file)))
            .OrderBy(file => file.Item1, StringComparer.Ordinal),
    ];
}
