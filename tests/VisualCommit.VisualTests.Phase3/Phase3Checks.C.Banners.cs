// Part C, check 25 (docs/test-reports/phase-3.md, with phase 4's changes: docs/test-reports/
// phase-4.md, "Changes to expected results"): the in-progress banners of states started outside
// the app: a cherry-pick sequence (aborted; then committed in a terminal and aborted with HEAD
// kept, D110; then, on a fresh copy, resolved and staged with the stage panel's "Continue
// cherry-pick"), a revert, a rebase started while the app is open, an empty cherry-pick, and
// stash conflicts. One test method per app start (each numbered step that names a scenario).
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksC
{
    /// <summary>The conflicted src/core.txt of StoppedAsync(CherryPick), markers included.</summary>
    private static readonly string[] PickConflict =
        ["core", "<<<<<<< HEAD", "main work", "=======", "conflict", ">>>>>>> 078ef05 (Conflicting change)"];

    /// <summary>
    /// Check 25, steps 1 to 3: a cherry-pick of three commits stopped at the second, started in a
    /// terminal: the banner of a pick in git's list (phase 4's text, Continue, Skip and Abort on a
    /// row of their own under it), the toolbar, the stage panel's prefill and its "Continue
    /// cherry-pick", and the abort with its sequencer sentence and its two backup refs.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_25_cherry_pick_sequence_and_abort()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.CherryPick);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: the banner and the graph. At 1100×700 the buttons stand under the text, so the
        // banner takes more than one line (phase 4's report, "Phase 3's banner"); nothing is cut.
        var shot = Shot(app, "25a-cherry-pick-sequence");
        AssertConflictsBanner(
            app,
            shot,
            PartC.Check25SequenceBanner,
            PartC.Check25PickBannerTip,
            "Abort cherry-pick",
            continueButton: BannerButton.Disabled(PartC.ResolveConflictsFirstOneFile),
            skipButton: BannerButton.Enabled,
            buttonsBelow: true);
        var text = app.Find<TextBlock>("BannerText");
        Assert.Equal(Avalonia.Media.TextTrimming.None, text.TextTrimming);
        var abort = app.Find<Button>("BannerAbortButton");
        AssertGraph(app, shot, PartC.Check25Sequence, lanes: 4, PartC.WorkingConflict);
        Assert.StartsWith(PartC.PickedAlphaOneShort, Graph(app).Data!.CommitAt(0).Sha, StringComparison.Ordinal);
        await AssertBranchRowAsync(app, ["Local branches", "main"], head: true, ahead: 1);
        AssertToolbar(app, "PullButton", enabled: false, "Finish or abort the cherry-pick first");
        AssertToolbar(app, "StashButton", enabled: false, "Finish or abort the cherry-pick first");

        // Step 2: the stage panel. The commit button of a pick in a list is phase 4's "Continue
        // cherry-pick", disabled as the banner's Continue is, with its reason ("Stage files to
        // commit" before phase 4).
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (1, 1 with conflicts)", "Staged files (0)");
        // The panel fills its message from the operation after a read of its own.
        await app.WaitForAsync(() => CommitBoxText(app, "CommitSummary") == "Conflicting change", "the summary filled from the pick's message", TimeSpan.FromSeconds(30));
        Shot(app, "25b-cherry-pick-stage-panel");
        Assert.Equal(["U core.txt src"], FileRows(app, "UnstagedFileList"));
        await AssertConflictRowAsync(app, "src/core.txt");
        Assert.Empty(FileRows(app, "StagedFileList"));
        Assert.Equal("Conflicting change", CommitBoxText(app, "CommitSummary"));
        Assert.Equal(string.Empty, CommitBoxText(app, "CommitDescription"));
        var commit = app.Find<Button>("CommitButton");
        Assert.Equal(PartC.ContinueCherryPick, commit.Content);
        Assert.False(commit.IsEffectivelyEnabled);
        AssertTip(commit, PartC.ResolveConflictsFirstOneFile);

        // Step 3: Abort, its question, then confirmed.
        var before = await repo.HeadAsync();
        Assert.Equal(before, (await File.ReadAllTextAsync(Path.Combine(repo.Path, ".git", "sequencer", "abort-safety"))).Trim());
        app.Click(abort);
        await AssertConfirmationAsync(app, PartC.AbortPickTitle, PartC.AbortPickSequenceQuestion, PartC.BackupDetail, "Abort cherry-pick");
        Shot(app, "25c-abort-question");
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Abort the cherry-pick", mark);
        shot = Shot(app, "25d-aborted");
        await AssertNoDialogAsync(app);
        var backups = await BackupsAsync(repo);
        Assert.Equal(2, backups.Count);
        var head = Assert.Single(backups, entry => !entry.Ref.IsChanges);
        var changes = Assert.Single(backups, entry => entry.Ref.IsChanges);
        Assert.Equal("abort", head.Ref.Kind);
        Assert.Equal("abort", changes.Ref.Kind);
        Assert.Equal(head.Name + "-changes", changes.Name);
        Assert.StartsWith(PartC.PickedAlphaOneShort, head.Sha, StringComparison.Ordinal);
        Assert.Equal(PartC.MainWorkSha, await RevParseAsync(repo, head.Name + "^"));
        Assert.Equal(head.Sha, await RevParseAsync(repo, changes.Name + "^"));
        Assert.Equal(PickConflict, (await repo.GitAsync("show", changes.Name + ":src/core.txt")).StandardOutput.TrimEnd('\n').Split('\n'));
        Assert.True(AssertCalled(app, calls, "git cherry-pick --abort").Succeeded);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(PartC.MainWorkSha, await repo.HeadAsync());
        Assert.Empty(await StatusAsync(repo));
        Assert.False(InGitDir(repo, "CHERRY_PICK_HEAD"));
        Assert.False(InGitDir(repo, "sequencer"));
        Assert.Null(app.BannerBounds);
        AssertGraph(app, shot, PartC.G0, lanes: 3);
        AssertStatus(app, "Aborted the cherry-pick");
    }

    /// <summary>
    /// Check 25, step 4, as phase 4 changes it (phase 4's report, "Phase 3's check 25 step 4"): a
    /// new stopped cherry-pick sequence resolved outside the app and committed in a terminal (the
    /// stage panel's button no longer commits a pick of a list: it continues the list, D136), the
    /// paused banner with Continue enabled and Skip disabled with its reason (D110: the todo's
    /// lines less one wait), and the Abort that keeps HEAD and the commits made.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_25_cherry_pick_committed_and_abort_keeps_head()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.CherryPick);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var copy = await repo.HeadAsync();

        // Step 4: in a terminal, the file resolved, staged and committed with git's own message.
        repo.WriteFile("src/core.txt", "core\nmain work\nconflict\n");
        await repo.GitAsync("add", "src/core.txt");
        var committed = repo.ClockTime;
        await repo.GitAsync("commit", "--no-edit");
        await app.WaitForAsync(
            () => app.BannerBounds is not null && app.Find<TextBlock>("BannerText").Text == PartC.Check25PausedBanner
                && Graph(app).Data is { IsComplete: true } data && data.Count == 10 && !Graph(app).ShowsWorkingRow,
            "the paused banner and the new commit in the graph",
            OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        var shot = Shot(app, "25f-stopped-between-commits");
        var made = await repo.HeadAsync();
        Assert.Equal(copy, await RevParseAsync(repo, "HEAD^"));
        var picked = await GitLinesAsync(repo, "log", "-1", "--format=%s%n%an <%ae>%n%aI%n%cI", "HEAD");
        Assert.Equal(4, picked.Count);
        Assert.Equal(["Conflicting change", "Test Author <author@example.com>"], [picked[0], picked[1]]);
        AssertGitDate(ScenarioTime(6), picked[2]);
        AssertGitDate(committed, picked[3]);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.False(InGitDir(repo, "CHERRY_PICK_HEAD"));
        var todo = File.ReadAllText(Path.Combine(repo.Path, ".git", "sequencer", "todo")).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("pick 078ef05 Conflicting change", todo, StringComparison.Ordinal);
        Assert.Contains("pick ce5ea6a Alpha two", todo, StringComparison.Ordinal);
        Assert.Equal(copy, File.ReadAllText(Path.Combine(repo.Path, ".git", "sequencer", "abort-safety")).Trim());

        // The paused banner: Continue picks the rest; there is nothing to skip.
        AssertConflictsBanner(
            app,
            shot,
            PartC.Check25PausedBanner,
            null,
            "Abort cherry-pick",
            continueButton: BannerButton.Enabled,
            skipButton: BannerButton.Disabled(PartC.NothingToSkip),
            buttonsBelow: true);
        Assert.False(Graph(app).ShowsWorkingRow);
        AssertSelection(app, shot, null);
        AssertGraph(app, shot, PartC.Check25Committed, lanes: 4);
        await AssertBranchRowAsync(app, ["Local branches", "main"], head: true, ahead: 2);
        AssertStatus(app, "Ready");

        // The abort that keeps HEAD.
        app.Click(app.Find<Button>("BannerAbortButton"));
        await AssertConfirmationAsync(app, PartC.AbortPickTitle, PartC.AbortPickKeepsHeadQuestion, PartC.BackupDetail, "Abort cherry-pick");
        Shot(app, "25g-abort-keeps-head-question");
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Abort the cherry-pick", mark);
        shot = Shot(app, "25h-abort-kept-head");
        await AssertNoDialogAsync(app);
        var backup = Assert.Single(await BackupsAsync(repo));
        Assert.Equal("abort", backup.Ref.Kind);
        Assert.False(backup.Ref.IsChanges);
        Assert.Equal(made, backup.Sha);
        var abort = AssertCalled(app, calls, "git cherry-pick --abort");
        Assert.True(abort.Succeeded);
        if (await IsReportGitAsync())
        {
            Assert.Contains("warning: You seem to have moved HEAD. Not rewinding, check your HEAD!", abort.StandardError, StringComparison.Ordinal);
        }

        Assert.False(InGitDir(repo, "sequencer"));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(made, await repo.HeadAsync());
        Assert.Equal(["Conflicting change", "Alpha one", "Main work"], await GitLinesAsync(repo, "log", "-3", "--format=%s", "main"));
        Assert.Null(app.BannerBounds);
        AssertGraph(app, shot, PartC.Check25Committed, lanes: 4);
        AssertSelection(app, shot, null);
        AssertStatus(app, "Aborted the cherry-pick");
    }

    /// <summary>
    /// Check 25, the new step of phase 4's report ("Phase 3's check 25 step 4": "a new step on a
    /// fresh copy shows the stage panel's Continue cherry-pick"): a new stopped cherry-pick
    /// sequence, the file resolved outside the app and staged in the stage panel (no question: no
    /// conflict markers are left, design 7.1). The banner says the conflicts are resolved, with
    /// Continue, Skip and Abort; the stage panel's commit button is "Continue cherry-pick",
    /// enabled, and Amend is disabled with its reason (design 5.3). Nothing is clicked further.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_25_cherry_pick_resolved_and_staged()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.CherryPick);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var copy = await repo.HeadAsync();

        // The file resolved outside the app, then staged in the stage panel.
        var reads = app.CountLogLines(AppLogFile.WorkingTreeRead(OpsRepo));
        repo.WriteFile("src/core.txt", "core\nmain work\nconflict\n");
        await app.WaitForAsync(() => app.CountLogLines(AppLogFile.WorkingTreeRead(OpsRepo)) > reads, "the app to read the working tree again", OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (1, 1 with conflicts)", "Staged files (0)");
        await ClickFileRowButtonAsync(app, "UnstagedFileList", "src/core.txt", "RowStageButton");
        await WaitForStagePanelAsync(app, "Unstaged files (0)", "Staged files (1)");
        await app.WaitForAsync(() => app.Find<TextBlock>("BannerText").Text == PartC.Check25ResolvedBanner, "the banner to say the conflicts are resolved");
        await app.WaitForIdleAsync();
        var shot = Shot(app, "25e-resolved-staged");
        await AssertNoDialogAsync(app);
        Assert.Equal(["M  src/core.txt"], await StatusAsync(repo));
        Assert.True(InGitDir(repo, "CHERRY_PICK_HEAD"));
        Assert.Equal(copy, await repo.HeadAsync());
        AssertConflictsBanner(
            app,
            shot,
            PartC.Check25ResolvedBanner,
            null,
            "Abort cherry-pick",
            continueButton: BannerButton.Enabled,
            skipButton: BannerButton.Enabled,
            buttonsBelow: true);
        Assert.Equal(PartC.WorkingChanges, Graph(app).WorkingRowText ?? PartC.WorkingChanges);
        var commit = app.Find<Button>("CommitButton");
        Assert.Equal(PartC.ContinueCherryPick, commit.Content);
        Assert.True(commit.IsEffectivelyEnabled);
        AssertTip(commit, null);
        var amend = (CheckBox)app.FindByAutomationId("AmendCheckBox");
        Assert.False(amend.IsEffectivelyEnabled);
        AssertTip(amend, PartC.FinishCherryPickFirst);
        Assert.Equal("Conflicting change", CommitBoxText(app, "CommitSummary"));
        AssertStatus(app, "Ready");
    }

    /// <summary>Check 25, step 5: a revert stopped by a conflict, started in a terminal: its banner, the toolbar's Stash and the stage panel's prefill.</summary>
    [AvaloniaFact]
    public async Task Check_25_revert()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Revert);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // A single revert: Continue (disabled while the file conflicts), Skip and Abort under the
        // text at 1100×700; its text is phase 3's.
        var shot = Shot(app, "25i-revert");
        AssertConflictsBanner(
            app,
            shot,
            PartC.Check25RevertBanner,
            null,
            "Abort revert",
            continueButton: BannerButton.Disabled(PartC.ResolveConflictsFirstOneFile),
            skipButton: BannerButton.Enabled,
            buttonsBelow: true);
        AssertGraph(app, shot, PartC.Check25Revert, lanes: 4, PartC.WorkingConflict);
        Assert.Equal(PartC.MainWorkSha, (await File.ReadAllTextAsync(Path.Combine(repo.Path, ".git", "REVERT_HEAD"))).Trim());
        Assert.False(InGitDir(repo, "sequencer"));
        Assert.Equal(["UU src/core.txt"], await StatusAsync(repo));
        AssertToolbar(app, "StashButton", enabled: false, "Finish or abort the revert first");

        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (1, 1 with conflicts)", "Staged files (0)");
        Shot(app, "25j-revert-stage-panel");
        Assert.Equal(["U core.txt src"], FileRows(app, "UnstagedFileList"));
        await AssertConflictRowAsync(app, "src/core.txt");
        Assert.Equal(PartC.Check25RevertSummary, CommitBoxText(app, "CommitSummary"));
        Assert.Equal(PartC.Check25RevertDescription, CommitBoxText(app, "CommitDescription"));
    }

    /// <summary>Check 25, step 6: a rebase started in a terminal while the app is open: its banner, the detached HEAD, the toolbar's Stash.</summary>
    [AvaloniaFact]
    public async Task Check_25_rebase_from_terminal()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        await repo.GitAsync("switch", "feature/conflict");
        var rebase = await repo.GitAllowingFailureAsync("rebase", "main");
        Assert.False(rebase.Succeeded, "The rebase did not stop.");
        await app.WaitForAsync(() => app.BannerBounds is not null && app.Find<TextBlock>("BannerText").Text == PartC.Check25RebaseBanner, "the rebase's banner", OutsideChangeTimeout);
        await app.WaitForAsync(() => Graph(app).ShowsWorkingRow && ((TextBlock)app.FindByAutomationId("CurrentBranch")).Text == "Detached at 88d6b91", "the detached HEAD and its working-changes row", OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        var shot = Shot(app, "25k-rebase-from-terminal");

        // Phase 4's rebase text, with Continue (disabled while the file conflicts), Skip and Abort
        // under it at 1100×700.
        AssertConflictsBanner(
            app,
            shot,
            PartC.Check25RebaseBanner,
            null,
            "Abort rebase",
            continueButton: BannerButton.Disabled(PartC.ResolveConflictsFirstOneFile),
            skipButton: BannerButton.Enabled,
            buttonsBelow: true);
        AssertBranch(app, "Detached at 88d6b91");
        AssertGraph(app, shot, PartC.Check25Rebase, lanes: 3, PartC.WorkingConflict);
        AssertWorkingRowCutWithToolTip(app);
        Assert.DoesNotContain(app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefItemRow>(), row => row.IsHead);
        Assert.Equal("refs/heads/feature/conflict", File.ReadAllText(Path.Combine(repo.Path, ".git", "rebase-merge", "head-name")).Trim());
        Assert.Equal(PartC.MainWorkSha, File.ReadAllText(Path.Combine(repo.Path, ".git", "rebase-merge", "onto")).Trim());
        Assert.Equal(["UU src/core.txt"], await StatusAsync(repo));
        AssertToolbar(app, "StashButton", enabled: false, "Finish or abort the rebase first");
    }

    /// <summary>
    /// Check 25, step 7: a cherry-pick that applies, then the same change picked again: git stops
    /// with an empty pick ("Cherry-pick stopped" with git's output and command from the call log),
    /// its banner, and its abort, which ends only the empty pick.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_25_empty_cherry_pick()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var alphaOne = await RevParseAsync(repo, "refs/heads/feature/alpha~1");
        var pickCommand = "git cherry-pick " + alphaOne;

        // The first pick.
        app.RightClick(app.GraphRowPoint(5));
        app.Menu();
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        app.ClickMenuItem("Cherry-pick onto main");
        await WaitForOperationAsync(app, repo, "Cherry-pick dff8fbf", mark);
        var shot = Shot(app, "25l-first-pick");
        Assert.True(AssertCalled(app, calls, pickCommand).Succeeded);
        AssertGraph(app, shot, PartC.Check25Sequence, lanes: 4);
        var copy = await repo.HeadAsync();
        Assert.NotEqual(alphaOne, copy);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero), Graph(app).Data!.CommitAt(0).CommitDate);
        AssertStatus(app, "Cherry-picked dff8fbf onto main");

        // The second pick of the original: empty.
        app.RightClick(app.GraphRowPoint(6));
        var menu = app.Menu();
        Assert.True(menu.Item("Cherry-pick onto main").IsEnabled);
        mark = app.LogMark();
        app.ClickMenuItem("Cherry-pick onto main");
        await WaitForOperationAsync(app, repo, "Cherry-pick dff8fbf", mark);
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Shot(app, "25m-empty-pick-dialog");
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            "Cherry-pick stopped",
            PartC.Check25EmptySummary,
            OpsRepo,
            pickCommand,
            ["Copy output", "Close"],
            PartC.T7,
            PartC.T7Key));
        AssertErrorDialogParts(app, extra: null);
        Assert.Equal(1, app.LastCall(pickCommand).ExitCode);
        AssertStatus(app, "Cherry-pick dff8fbf failed", OperationStatusTone.Danger);
        Assert.Equal(alphaOne, (await File.ReadAllTextAsync(Path.Combine(repo.Path, ".git", "CHERRY_PICK_HEAD"))).Trim());
        Assert.Equal(0, (await repo.GitAllowingFailureAsync("diff", "--cached", "--quiet", "HEAD")).ExitCode);
        Assert.Empty(await StatusAsync(repo));

        // Close: the banner of the empty pick in phase 4's words: Continue disabled ("skip it"),
        // Skip and Abort under the text.
        ClickDialogButton(app, "ConfirmButton");
        await AssertNoDialogAsync(app);
        shot = Shot(app, "25n-empty-pick-banner");
        AssertConflictsBanner(
            app,
            shot,
            PartC.Check25EmptyBanner,
            null,
            "Abort cherry-pick",
            continueButton: BannerButton.Disabled(PartC.PickChangesNothing),
            skipButton: BannerButton.Enabled,
            buttonsBelow: true);
        Assert.False(Graph(app).ShowsWorkingRow);

        // Abort: it ends only the empty pick.
        app.Click(app.Find<Button>("BannerAbortButton"));
        await AssertConfirmationAsync(app, PartC.AbortPickTitle, PartC.AbortPickQuestion, PartC.BackupDetail, "Abort cherry-pick");
        calls = app.GitCalls().Count;
        mark = app.LogMark();
        ClickDialogButton(app, "ConfirmButton");
        await WaitForOperationAsync(app, repo, "Abort the cherry-pick", mark);
        shot = Shot(app, "25o-empty-pick-aborted");
        await AssertNoDialogAsync(app);
        var backup = Assert.Single(await BackupsAsync(repo));
        Assert.Equal("abort", backup.Ref.Kind);
        Assert.False(backup.Ref.IsChanges);
        Assert.Equal(copy, backup.Sha);
        Assert.True(AssertCalled(app, calls, "git cherry-pick --abort").Succeeded);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(copy, await repo.HeadAsync());
        Assert.False(InGitDir(repo, "CHERRY_PICK_HEAD"));
        Assert.Null(app.BannerBounds);
        AssertGraph(app, shot, PartC.Check25Sequence, lanes: 4);
        AssertStatus(app, "Aborted the cherry-pick");
    }

    /// <summary>Check 25, step 8: conflicts left by a stash applied in a terminal, without an operation: the banner without Abort, Stash disabled, Pop enabled.</summary>
    [AvaloniaFact]
    public async Task Check_25_stash_conflicts()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.None);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        var shot = Shot(app, "25p-stash-conflicts");
        AssertConflictsBanner(app, shot, PartC.ConflictsBanner, PartC.ConflictsBannerTip);
        AssertGraph(app, shot, PartC.Check25StashConflicts, lanes: 5, PartC.WorkingConflict);
        Assert.Equal(2, SectionRow(app, "Stashes").Count);
        Assert.Equal(["On main: Core experiment", "On main: Experiment"], SectionLines(app, "Stashes"));
        Assert.Equal(["stash@{0}: On main: Core experiment", "stash@{1}: On main: Experiment"], await StashListAsync(repo));
        Assert.Equal(["UU src/core.txt"], await StatusAsync(repo));
        Assert.Equal(
            ["core", "<<<<<<< Updated upstream", "core again", "=======", "main work, stashed", ">>>>>>> Stashed changes"],
            FileLines(repo, "src/core.txt"));
        AssertNoOperationFiles(repo);
        AssertToolbar(app, "StashButton", enabled: false, PartC.ResolveFirst);
        AssertToolbar(app, "PopButton", enabled: true);

        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (1, 1 with conflicts)", "Staged files (0)");
        Shot(app, "25q-stash-conflicts-stage-panel");
        Assert.Equal(["U core.txt src"], FileRows(app, "UnstagedFileList"));
        await AssertConflictRowAsync(app, "src/core.txt");
        Assert.Equal(string.Empty, CommitBoxText(app, "CommitSummary"));
        Assert.Equal(string.Empty, CommitBoxText(app, "CommitDescription"));
    }

    /// <summary>
    /// Moves the pointer onto a file's row in a stage list, so that its buttons show, and clicks
    /// one of them (<c>RowStageButton</c>, <c>RowDiscardButton</c>, <c>RowUnstageButton</c>).
    /// </summary>
    private static async Task ClickFileRowButtonAsync(ShellDriver app, string list, string path, string buttonId)
    {
        var row = await app.FileRowAsync(list, path);
        app.MoveMouse(row);
        Button? button = null;
        await app.WaitForAsync(
            () => (button = row.GetVisualParent()!.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(candidate => candidate.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetAutomationId(candidate) == buttonId)) is not null,
            $"the {buttonId} of {path}");
        app.Click(button!);
    }
}
