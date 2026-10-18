// Check U23 of part U (docs/test-reports/phase-5.md): the refusals: Undo after a terminal commit on
// the same branch (the tooltip, the button disabled, the key doing nothing; enabled again once the
// branch is back), during a merge in progress (and after its Abort), a Keep move over two changed
// files in the way (the app's count), an ignored file in the way of a redo; and a Keep undo with
// stale stat data that succeeds; "ops", "rebase", dark, 1100×700. One method per part.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Undo;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase5;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;
using static VisualCommit.VisualTests.Phase5Undo.Phase5ChecksU.U20To23.Direction;
using Steps = VisualCommit.VisualTests.Phase5Undo.Phase5ChecksU.U20To23;

namespace VisualCommit.VisualTests.Phase5Undo;

public sealed partial class Phase5ChecksU
{
    /// <summary>
    /// U23 part A: after "Merge feature/ff into main", a commit made outside on main disables Undo
    /// (the tooltip under the disabled button; the platform's undo key does nothing); reset away
    /// outside, the entry is undoable again and its Undo runs.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U23_terminal_commit()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 1: "Merge feature/ff into main" (it fast-forwards).
        await U23.MergeFeatureFfAsync(app, repo);

        // Step 2: "Terminal work" committed outside on main.
        Assert.Equal(PartU2.TerminalWork, await repo.CommitFileAsync("src/terminal.txt", PartU2.Terminal, "Terminal work"));
        // The terminal's commit writes the file, stages it and commits it: a status read between those
        // steps shows a working-changes row that the watcher's next refresh takes away, so the check
        // waits for the clean tree it asserts as well as for the commit (CI run 39158437982).
        await app.WaitForAsync(() => U23.GraphShows(app, "Terminal work") && !Graph(app).ShowsWorkingRow, "the graph to show \"Terminal work\" without a working-changes row", OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        AssertGraph(app, PartU2.G0Terminal, PartU2.G0Lanes);
        Assert.Equal(PartU2.TerminalWork, await Steps.RefAsync(repo, "refs/heads/main"));
        var refused = new UndoRedoButtons(false, PartU2.MovedSince, false, "Nothing to redo");
        Steps.AssertButtons(app, refused);

        // Step 3: the tooltip under the disabled Undo.
        var tip = await app.RestPointerOnAsync(app.FindByAutomationId(ShellDriver.UndoButtonId));
        Shot(app, "u23a-moved-since", keepPointer: true);
        Assert.Equal(PartU2.MovedSince, tip);
        Steps.AssertToolTipUnder(app, PartU2.MovedSince, ShellDriver.UndoButtonId);

        // Step 4: "Fast-forward me" clicked, then the platform's undo key: handled, nothing done.
        app.Click(app.GraphRowPoint(PartU2.TerminalFastForwardRow));
        await app.WaitForIdleAsync();
        await U23.AssertUndoKeyDoesNothingAsync(app, repo, PartU2.MergeFfText);
        Assert.Equal(PartU2.TerminalWork, await Steps.RefAsync(repo, "refs/heads/main"));
        Assert.Equal(PartU2.TerminalFastForwardRow, Graph(app).SelectedIndex);
        Steps.AssertButtons(app, refused);

        // Step 5: reset away outside: the state the entry left is back, and so is its Undo.
        await repo.GitAsync("reset", "--quiet", "--hard", PartU2.FastForwardMe);
        await app.WaitForAsync(() => !U23.GraphShows(app, "Terminal work") && !Graph(app).ShowsWorkingRow, "\"Terminal work\" to leave the graph, without a working-changes row", OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        AssertGraph(app, PartU2.G1, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.MergeFfText));

        // Step 6: Undo runs as U19's step 2.
        var calls = await Steps.StepAsync(app, repo, Undo, PartU2.MergeFfText);
        var shot = Shot(app, "u23b-undone-after-reset");
        U23.AssertMergeUndoneAsKeepMove(app, calls);
        Assert.Equal(PartU2.MainWork, await Steps.RefAsync(repo, "refs/heads/main"));
        Assert.Equal([PartU2.OnMain], await Steps.StatusAsync(repo));
        Assert.False(Directory.Exists(Path.Combine(repo.Path, "docs")), "docs/ is still there.");
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes, picture: shot);
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.MergeFfText));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// U23 part B: hotfix created, then a merge that stops with a conflict: Undo and Redo disabled
    /// with "Finish or abort the merge first" (the key doing nothing); after the Abort, which records
    /// nothing, the creation of hotfix is undoable and undone.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U23_merge_in_progress()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 7: "Create branch here…" on Main work, hotfix, not checked out.
        Steps.OpenCommitMenu(app, PartU2.MainWorkRow);
        app.ClickMenuItem("Create branch here…");
        await app.WaitForDialogAsync<NameDialogViewModel>();
        app.TypeInto("NameBox", "hotfix");
        var option = (CheckBox)app.FindByAutomationId(app.TopDialogView!, "NameDialogOption");
        app.Click(option);
        Assert.False(option.IsChecked, "The new branch would be checked out.");
        var mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Create branch hotfix", mark);
        await AssertNoDialogAsync(app);
        AssertStatus(app, PartU2.CreatedHotfix);
        AssertGraph(app, PartU2.G0Hotfix, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.CreateHotfixText));

        // Step 8: "Merge feature/conflict into main" stops with a conflict.
        Steps.OpenLabelMenu(app, PartU2.ConflictingRow, "feature/conflict");
        await Steps.RunMenuItemAsync(app, repo, "Merge feature/conflict into main", "Merge feature/conflict");
        await app.WaitForAsync(() => app.BannerBounds is not null, "the conflict banner");
        Assert.Equal(PartU2.MergeBanner, ((TextBlock)app.FindByAutomationId("BannerText")).Text);
        var abort = (Button)app.FindByAutomationId("BannerAbortButton");
        Assert.True(abort.IsEffectivelyVisible && "Abort merge".Equals(abort.Content), "The banner shows no \"Abort merge\".");
        Assert.Equal(["## main...origin/main", "UU src/core.txt"], await Steps.StatusAsync(repo));
        AssertStatus(app, PartU2.MergeStopped, OperationStatusTone.Warning);
        var pending = new[] { (UndoKind.CreateBranch, PartU2.CreateHotfixText, UndoEntryState.Done), (UndoKind.Merge, PartU2.MergeConflictText, UndoEntryState.Pending) };
        U23.AssertHistory(app, pending);
        var merging = new UndoRedoButtons(false, PartU2.FinishOrAbortMerge, false, PartU2.FinishOrAbortMerge);
        Steps.AssertButtons(app, merging);

        // Step 9: the tooltip under the disabled Undo; then "Conflicting change" clicked and the undo key: nothing.
        var tip = await app.RestPointerOnAsync(app.FindByAutomationId(ShellDriver.UndoButtonId));
        Shot(app, "u23c-merging-tooltip", keepPointer: true);
        Assert.Equal(PartU2.FinishOrAbortMerge, tip);
        Steps.AssertToolTipUnder(app, PartU2.FinishOrAbortMerge, ShellDriver.UndoButtonId);
        app.Click(app.GraphRowPoint(PartU2.ConflictingRow));
        await app.WaitForIdleAsync();
        await U23.AssertUndoKeyDoesNothingAsync(app, repo, PartU2.CreateHotfixText);
        Assert.True(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")), "MERGE_HEAD is gone.");
        Assert.Equal(PartU2.MainWork, await Steps.RefAsync(repo, "refs/heads/hotfix"));
        U23.AssertHistory(app, pending);
        Steps.AssertButtons(app, merging);

        // Step 10: the banner's "Abort merge", then "Abort merge" in the dialog.
        app.Click(abort);
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        mark = app.LogMark();
        Steps.ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Abort the merge", mark);
        var shot = Shot(app, "u23d-aborted");
        await AssertNoDialogAsync(app);
        AssertStatus(app, PartU2.AbortedMerge);
        Assert.Null(app.BannerBounds);
        AssertGraph(app, PartU2.G0Hotfix, PartU2.G0Lanes, picture: shot);
        Assert.Equal([PartU2.OnMain], await Steps.StatusAsync(repo));
        var backups = await Steps.BackupLinesAsync(repo);
        Assert.Equal(2, backups.Count);
        Assert.Equal($"{PartU2.FirstBackup("abort")} {PartU2.MainWork}", backups[0]);
        var changes = PartU2.FirstBackup("abort") + "-changes";
        Assert.StartsWith(changes + " ", backups[1], StringComparison.Ordinal);
        Assert.Equal([PartU2.MainWork], (await Steps.GitLinesAsync(repo, "rev-list", "--parents", "-n", "1", changes))[0].Split(' ').Skip(1));
        Assert.Contains("<<<<<<< HEAD", (await repo.GitAsync("show", changes + ":src/core.txt")).StandardOutput, StringComparison.Ordinal);
        U23.AssertHistory(app, (UndoKind.CreateBranch, PartU2.CreateHotfixText, UndoEntryState.Done));
        Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.CreateHotfixText));

        // Step 11: Undo: hotfix deleted (as U18's step 2): no config call, the branch had no keys.
        var calls = await Steps.StepAsync(app, repo, Undo, PartU2.CreateHotfixText);
        var transaction = Steps.AssertSucceeded(app, calls, PartU2.Transaction(PartU2.UndoMessage(PartU2.CreateHotfixText)));
        Assert.Equal($"delete refs/heads/hotfix {PartU2.MainWork}\n", transaction.StandardInput);
        Assert.DoesNotContain(CallsSince(app, calls), U23.IsConfigWrite);
        Assert.Null(await Steps.RefAsync(repo, "refs/heads/hotfix"));
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("reflog", "exists", "refs/heads/hotfix")).ExitCode);
        Assert.Equal([PartU2.OnMain], await Steps.StatusAsync(repo));
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes);
        Assert.Equal(4, Steps.SectionCount(app, "Local branches"));
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.CreateHotfixText));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// U23 part C: after "Rebase feature/topic onto main", both files the Keep move changes are
    /// changed outside: Undo is refused with the app's count of them and git's line; with the files
    /// restored outside, the same Undo runs.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U23_changes_in_the_way()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.R0, PartU2.RebaseLanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 12: "Rebase feature/topic onto main" from main's menu.
        await Steps.OpenRowMenuAsync(app, "Local branches", "main");
        await Steps.RunMenuItemAsync(app, repo, "Rebase feature/topic onto main", "Rebase feature/topic onto main");
        await AssertNoDialogAsync(app);
        AssertStatus(app, PartU2.Rebased);
        AssertGraph(app, PartU2.R1, PartU2.RebaseLanes);
        Assert.Equal((4, 2), Steps.Arrows(app, "feature", "topic"));
        Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.RebaseText));
        var backup = $"{PartU2.FirstBackup("rebase", "feature/topic")} {PartU2.TopicTwo}";
        Assert.Equal([backup], await Steps.BackupLinesAsync(repo));

        // Step 13: both files the move changes changed outside.
        repo.WriteFile("src/core.txt", PartU2.CoreMine).WriteFile("docs/main.txt", PartU2.MainMine);
        await app.WaitForAsync(() => Graph(app).ShowsWorkingRow, "the working-changes row", OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        Assert.Equal([" M docs/main.txt", " M src/core.txt"], (await Steps.StatusAsync(repo)).Skip(1));
        AssertGraph(app, PartU2.R1, PartU2.RebaseLanes, PartU2.Working);
        Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.RebaseText));

        // Step 14: Undo: read-tree refuses; the app counts the paths in the move's way.
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        await app.ClickUndoAsync();
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        await app.WaitForIdleAsync();
        Shot(app, "u23e-changes-in-the-way");
        var readTree = PartU2.Command("read-tree", "-m", "-u", PartU2.TopicTwoOntoMain, PartU2.TopicTwo);
        AssertCalledInOrder(
            app,
            calls,
            PartU2.RefreshIndex,
            readTree,
            PartU2.Command("diff-tree", "-r", "-z", "--no-renames", "--name-only", PartU2.TopicTwoOntoMain, PartU2.TopicTwo));
        Assert.Equal(128, app.LastCall(readTree).ExitCode);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            "Undo failed",
            PartU2.ChangesInTheWaySummary,
            PartU2.RebaseRepo,
            readTree,
            ["Copy output", "Close"],
            [PartU2.Tu1],
            PartU2.Tu1));
        AssertNoClippedText(app);
        AssertStatus(app, $"{Steps.Title(Undo, PartU2.RebaseText)} failed", OperationStatusTone.Danger);
        await app.WaitForLogLineAsync(AppLogFile.OperationEnded(PartU2.RebaseRepo, Steps.Title(Undo, PartU2.RebaseText), Core.Operations.OperationOutcome.Failed), mark: mark);
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git update-ref", StringComparison.Ordinal));
        Assert.Equal(PartU2.TopicTwoOntoMain, await Steps.RefAsync(repo, "refs/heads/feature/topic"));
        Assert.Equal(PartU2.CoreMine, Steps.FileText(repo, "src/core.txt"));
        Assert.Equal(PartU2.MainMine, Steps.FileText(repo, "docs/main.txt"));
        Assert.Equal(["rebase (finish): returning to refs/heads/feature/topic"], await Steps.GitLinesAsync(repo, "log", "-g", "-n", "1", "--format=%gs", "HEAD"));

        // Step 15: Close: the entry stays.
        Steps.ClickDialogButton(app, "ConfirmButton");
        await AssertNoDialogAsync(app);
        Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.RebaseText));

        // Step 16: the files restored outside; the same Undo runs (U19's step 14).
        await repo.GitAsync("restore", "--worktree", "--", "docs/main.txt", "src/core.txt");
        await app.WaitForAsync(() => !Graph(app).ShowsWorkingRow, "the working-changes row to go", OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        Assert.Equal(["## feature/topic...origin/feature/topic [ahead 4, behind 2]"], await Steps.StatusAsync(repo));
        calls = await Steps.StepAsync(app, repo, Undo, PartU2.RebaseText);
        AssertCalledInOrder(
            app,
            calls,
            PartU2.RefreshIndex,
            readTree,
            PartU2.Command("update-ref", "-m", PartU2.UndoMessage(PartU2.RebaseText), "refs/heads/feature/topic", PartU2.TopicTwo, PartU2.TopicTwoOntoMain));
        Assert.Equal(PartU2.TopicTwo, await Steps.RefAsync(repo, "refs/heads/feature/topic"));
        Assert.Equal(["## feature/topic...origin/feature/topic"], await Steps.StatusAsync(repo));
        Assert.Null(Steps.FileText(repo, "docs/main.txt"));
        Assert.Equal(Scenarios.RebaseFiles.Core, Steps.FileText(repo, "src/core.txt"));
        Assert.Equal([PartU2.UndoMessage(PartU2.RebaseText)], await Steps.GitLinesAsync(repo, "log", "-g", "-n", "1", "--format=%gs", "HEAD"));
        Assert.Equal(PartU2.MainMore, await Steps.RefAsync(repo, "refs/heads/main"));
        Assert.Equal(PartU2.OtherOne, await Steps.RefAsync(repo, "refs/heads/feature/other"));
        Assert.Equal([backup], await Steps.BackupLinesAsync(repo));
        AssertGraph(app, PartU2.R0, PartU2.RebaseLanes);
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.RebaseText));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// U23 part D: the merge of feature/ff undone, then docs/ff.txt written outside as an ignored
    /// file: the Redo is refused by the app's own check before git touches it; with the file gone,
    /// the Redo runs.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U23_ignored_file_in_the_way()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 17: the merge, then Undo.
        await U23.MergeFeatureFfAsync(app, repo);
        await Steps.StepAsync(app, repo, Undo, PartU2.MergeFfText);
        Assert.False(Directory.Exists(Path.Combine(repo.Path, "docs")), "docs/ is still there.");
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.MergeFfText));

        // Step 18: docs/ff.txt excluded and written outside: the app reads the status again (the
        // exclude file is in the git folder it watches), and nothing it shows changes.
        var mark = app.LogMark();
        File.AppendAllText(Path.Combine(repo.Path, ".git", "info", "exclude"), "docs/ff.txt\n");
        repo.WriteFile("docs/ff.txt", PartU2.FfNotes);
        await app.WaitForStatusReadAsync(0, 0, mark, timeout: OutsideChangeTimeout);
        Assert.Equal(["## main...origin/main", "!! docs/"], await Steps.GitLinesAsync(repo, "status", "--porcelain", "-b", "--ignored"));
        Assert.False(Graph(app).ShowsWorkingRow, "The working-changes row shows.");
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.MergeFfText));

        // Step 19: Redo: the app's own check finds the ignored file in the move's way.
        var calls = app.GitCalls().Count;
        mark = app.LogMark();
        await app.ClickRedoAsync();
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        await app.WaitForIdleAsync();
        Shot(app, "u23f-ignored-in-the-way");
        AssertCalledInOrder(
            app,
            calls,
            PartU2.Command("diff-tree", "-r", "-z", "--no-renames", "--name-only", "--diff-filter=A", PartU2.MainWork, PartU2.FastForwardMe),
            "git check-ignore --stdin -z");
        Assert.DoesNotContain(
            CallsSince(app, calls),
            call => call.StartsWith("git read-tree", StringComparison.Ordinal) || call.StartsWith("git update-ref", StringComparison.Ordinal));
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog("Redo failed", PartU2.IgnoredInTheWaySummary, PartU2.OpsRepo, null, ["Close"], []));
        AssertNoClippedText(app);
        AssertStatus(app, $"{Steps.Title(Redo, PartU2.MergeFfText)} failed", OperationStatusTone.Danger);
        await app.WaitForLogLineAsync(AppLogFile.OperationEnded(PartU2.OpsRepo, Steps.Title(Redo, PartU2.MergeFfText), Core.Operations.OperationOutcome.Failed), mark: mark);
        Assert.Equal(PartU2.FfNotes, Steps.FileText(repo, "docs/ff.txt"));
        Assert.Equal(PartU2.MainWork, await Steps.RefAsync(repo, "refs/heads/main"));

        // Step 20: Close.
        Steps.ClickDialogButton(app, "ConfirmButton");
        await AssertNoDialogAsync(app);
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.MergeFfText));

        // Step 21: the file deleted outside; the Redo runs.
        repo.DeleteFile("docs/ff.txt");
        await app.WaitForIdleAsync();
        await Steps.StepAsync(app, repo, Redo, PartU2.MergeFfText);
        Assert.Equal(PartU2.FastForwardMe, await Steps.RefAsync(repo, "refs/heads/main"));
        Assert.Equal(Scenarios.OperationsFiles.FastForward, Steps.FileText(repo, "docs/ff.txt"));
        AssertGraph(app, PartU2.G1, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.MergeFfText));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// U23 part E: after the merge of feature/ff, docs/ff.txt rewritten with its own bytes and a
    /// later last-write time, so that the index's stat data is stale (every read here without
    /// optional locks, UD15): the Undo's Keep move refreshes the index first and succeeds.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U23_stale_stat_data()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes);
        Steps.AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 22: the merge.
        await U23.MergeFeatureFfAsync(app, repo);

        // Step 23: the same bytes, an hour later: git's view of the file is stale.
        var path = Path.Combine(repo.Path, "docs", "ff.txt");
        repo.WriteFile("docs/ff.txt", Scenarios.OperationsFiles.FastForward);
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddHours(1));
        await app.WaitForIdleAsync();
        Assert.Equal(["docs/ff.txt"], await U23.StaleReadAsync(repo, "diff-files", "--name-only"));
        Assert.Equal(["## main...origin/main [ahead 1]"], await U23.StaleReadAsync(repo, "status", "--porcelain=v1", "-b", "--untracked-files=all"));
        var dryRun = await repo.GitWithEnvironmentAllowingFailureAsync(U23.NoOptionalLocks, "read-tree", "-n", "-m", "-u", PartU2.FastForwardMe, PartU2.MainWork);
        Assert.Equal(128, dryRun.ExitCode);
        Assert.Contains(PartU2.StaleRefusal, Steps.Lines(dryRun.StandardError));
        Assert.Equal(["docs/ff.txt"], await U23.StaleReadAsync(repo, "diff-files", "--name-only"));
        Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.MergeFfText));

        // Step 24: Undo: the index refreshed before the read-tree, which succeeds.
        var calls = await Steps.StepAsync(app, repo, Undo, PartU2.MergeFfText);
        var shot = Shot(app, "u23g-stale-undone");
        U23.AssertMergeUndoneAsKeepMove(app, calls);
        Assert.Equal(0, app.LastCall(PartU2.Command("read-tree", "-m", "-u", PartU2.FastForwardMe, PartU2.MainWork)).ExitCode);
        Assert.Equal(PartU2.MainWork, await Steps.RefAsync(repo, "refs/heads/main"));
        Assert.Equal([PartU2.OnMain], await U23.StaleReadAsync(repo, "status", "--porcelain=v1", "-b", "--untracked-files=all"));
        Assert.False(Directory.Exists(Path.Combine(repo.Path, "docs")), "docs/ is still there.");
        AssertGraph(app, PartU2.G0, PartU2.G0Lanes, picture: shot);
        Steps.AssertButtons(app, UndoRedoButtons.CanRedo(PartU2.MergeFfText));
        AssertNoClippedText(app);
    }

    /// <summary>The helpers of check U23's methods.</summary>
    private static class U23
    {
        /// <summary>What every git read of U23 part E runs with, as the app's own reads do (UD15): a plain git status would write the refreshed index.</summary>
        public static readonly Dictionary<string, string?> NoOptionalLocks = new(StringComparer.Ordinal) { ["GIT_OPTIONAL_LOCKS"] = "0" };

        /// <summary>A git read of U23 part E, with <c>GIT_OPTIONAL_LOCKS=0</c> as the app's reads run (UD15).</summary>
        public static async Task<List<string>> StaleReadAsync(TempRepo repo, params string[] arguments) =>
            Steps.Lines((await repo.GitWithEnvironmentAsync(NoOptionalLocks, arguments)).StandardOutput);

        /// <summary>
        /// U23's "Merge feature/ff as in step 1" (U19's step 1): the label's menu, phase 3's merge
        /// that fast-forwards, main at Fast-forward me and one ahead, HEAD's reflog's newest entry
        /// git's, G1, T+.
        /// </summary>
        public static async Task MergeFeatureFfAsync(ShellDriver app, TempRepo repo)
        {
            Steps.OpenLabelMenu(app, PartU2.FastForwardRow, "feature/ff");
            var calls = await Steps.RunMenuItemAsync(app, repo, "Merge feature/ff into main", "Merge feature/ff");
            await AssertNoDialogAsync(app);
            AssertStatus(app, PartU2.FastForwarded);
            Steps.AssertSucceeded(app, calls, PartU2.MergeFf);
            Assert.Equal(PartU2.FastForwardMe, await Steps.RefAsync(repo, "refs/heads/main"));
            Assert.Equal([PartU2.OnMainAheadOne], await Steps.StatusAsync(repo));
            Assert.Equal([PartU2.MergeFfReflog], await Steps.GitLinesAsync(repo, "log", "-g", "-n", "1", "--format=%gs", "HEAD"));
            AssertGraph(app, PartU2.G1, PartU2.G0Lanes);
            Steps.AssertButtons(app, UndoRedoButtons.CanUndo(PartU2.MergeFfText));
        }

        /// <summary>
        /// The tab's undo history (7.1, 7.2): its entries, oldest first, as (kind, text, state), and
        /// the cursor after the last of them; and the pending entry, the last one when one is.
        /// </summary>
        public static void AssertHistory(ShellDriver app, params (UndoKind Kind, string Text, UndoEntryState State)[] entries)
        {
            var history = app.Session.Shell.ActiveTab.Repository!.UndoHistory;
            Assert.Equal(entries, history.Entries.Select(entry => (entry.Kind, entry.Text, entry.State)));
            Assert.Equal(entries.Length, history.Cursor);
            if (entries is [.., (_, _, UndoEntryState.Pending)])
            {
                Assert.Same(history.Entries[^1], history.Pending);
            }
            else
            {
                Assert.Null(history.Pending);
            }
        }

        /// <summary>The merge's undo as U19's step 2 runs it: the Keep move, the index refreshed, read-tree, then the compare-and-swap of main.</summary>
        public static void AssertMergeUndoneAsKeepMove(ShellDriver app, int calls) =>
            AssertCalledInOrder(
                app,
                calls,
                PartU2.RefreshIndex,
                PartU2.Command("read-tree", "-m", "-u", PartU2.FastForwardMe, PartU2.MainWork),
                PartU2.Command("update-ref", "-m", PartU2.UndoMessage(PartU2.MergeFfText), "refs/heads/main", PartU2.MainWork, PartU2.FastForwardMe));

        /// <summary>
        /// The platform's undo key with Undo disabled (design 1.3): the key does nothing: no step of
        /// <paramref name="text"/> starts, no dialog opens, the queue stays idle.
        /// </summary>
        public static async Task AssertUndoKeyDoesNothingAsync(ShellDriver app, TempRepo repo, string text)
        {
            var mark = app.LogMark();
            var calls = app.GitCalls().Count;
            app.PressGitUndo();
            await app.WaitForIdleAsync();
            Assert.DoesNotContain(app.LogLinesSince(mark), line => line.Contains($": {Steps.Title(Undo, text)}", StringComparison.Ordinal));
            Assert.DoesNotContain(app.GitCalls().Skip(calls), call => call.OperationId is not null);
            await AssertNoDialogAsync(app);
            Assert.Null(app.Session.Shell.ActiveTab.Repository!.Queue.Current);
        }

        /// <summary>
        /// Whether a call writes configuration: a <c>git config</c> call other than the reads every
        /// refresh and the history's reads run (<c>--list</c>, <c>--get…</c>; 7.7, which no check counts).
        /// </summary>
        public static bool IsConfigWrite(string call) =>
            call.StartsWith("git config ", StringComparison.Ordinal)
            && !call.Split(' ').Any(argument => argument == "--list" || argument.StartsWith("--get", StringComparison.Ordinal));

        /// <summary>Whether the graph has a row whose subject is <paramref name="subject"/>.</summary>
        public static bool GraphShows(ShellDriver app, string subject)
        {
            var rows = Graph(app).Data;
            return rows is not null && rows.IsComplete && Enumerable.Range(0, rows.Count).Any(row => rows.CommitAt(row).Subject == subject);
        }
    }
}
