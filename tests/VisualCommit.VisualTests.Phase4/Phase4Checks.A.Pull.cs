// Part A, check A4 (docs/test-reports/phase-4.md): pull with rebase against a file remote, a
// clean one, and one whose rebase stops, then is resolved and continued, or skipped.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksA
{
    /// <summary>
    /// Check A4, part A: "Pull from origin/main (rebase)" with a commit of main's own and one
    /// pushed from elsewhere: before and after, the git calls, the backup, the status ("sync").
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A4_pull_with_rebase()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        Assert.Equal(PartA.ElsewhereWorkSha, await Scenarios.PushFromElsewhereAsync(repo, PartA.ElsewhereWork.Branch, PartA.ElsewhereWork.File, PartA.ElsewhereWork.Content, PartA.ElsewhereWork.Message));
        Assert.Equal(PartA.LocalWorkSha, await repo.CommitFileAsync(PartA.LocalWork.File, PartA.LocalWork.Content, PartA.LocalWork.Message));
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: S1.
        var shot = Shot(app, "a4a-before");
        AssertGraph(app, PartA.S1, 3, showsDate: true, picture: shot);
        AssertNothingSelected(app);
        AssertBranch(app, "main");
        await AssertLeftPanelAsync(app, PartA.S1LeftPanel);
        Assert.Equal(["## main...origin/main [ahead 1]"], await GitLinesAsync(repo, "status", "-sb"));

        // Step 2: "Pull from origin/main (rebase)" from main's menu.
        var (menu, _) = await RightClickRefAsync(app, "Local branches", "main");
        AssertMenuAsReported(app, menu, PartA.SyncMainMenu, "Pull from origin/main", PartA.CurrentBranchWithUpstreamMenuHeight);
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        using (var recorder = app.RecordStatusTexts())
        {
            app.ClickMenuItem("Pull from origin/main (rebase)");
            await WaitForOperationAsync(app, repo, PartA.PullRebaseTitle, mark, NetworkTimeout);
            Assert.Contains(recorder.Texts, text => text.StartsWith(PartA.PullingMain, StringComparison.Ordinal));
        }

        shot = Shot(app, "a4b-pulled-with-rebase");
        await AssertNoDialogAsync(app);
        AssertCalledInOrder(
            app,
            calls,
            PartA.FetchOriginCommand,
            PartA.IsAncestorOfFetchHead(PartA.LocalWorkSha),
            PartA.ForkPoint("origin/main"),
            PartA.CountAll("origin/main"),
            PartA.CountAfterForkPoint("origin/main", PartA.SharedWorkSha),
            "git update-ref --stdin",
            PartA.RebaseWithoutArgumentsCommand);
        var fetch = AssertCall(app, calls, PartA.FetchOriginCommand, 0);
        await AssertLastPrintedAsync(fetch.StandardError, PartA.FetchMainLine, PartA.FetchOriginCommand);
        AssertCall(app, calls, PartA.IsAncestorOfFetchHead(PartA.LocalWorkSha), 1);
        Assert.Equal(PartA.SharedWorkSha, AssertCall(app, calls, PartA.ForkPoint("origin/main"), 0).StandardOutput.Trim());
        Assert.Equal("1", AssertCall(app, calls, PartA.CountAll("origin/main"), 0).StandardOutput.Trim());
        Assert.Equal("1", AssertCall(app, calls, PartA.CountAfterForkPoint("origin/main", PartA.SharedWorkSha), 0).StandardOutput.Trim());
        var rebase = AssertCall(app, calls, PartA.RebaseWithoutArgumentsCommand, 0);
        await AssertGitPrintedAsync(rebase.StandardError, PartA.Ta3("main", 1), PartA.RebaseWithoutArgumentsCommand);
        AssertStatus(app, PartA.PulledAndRebasedMain);
        AssertOperationLine(app, mark, PartA.SyncRepo, PartA.PullRebaseTitle, OperationOutcome.Succeeded, PartA.PulledAndRebasedMain);
        AssertNoBackupRemoved(app, mark);
        AssertGraph(app, PartA.S2, 3, showsDate: true, picture: shot);
        AssertNothingSelected(app);
        await AssertLeftPanelAsync(app, PartA.S1LeftPanel);
        await AssertLocalBranchAsync(app, ahead: 1, behind: 0, head: true, "main");

        // Git: Local work replayed on Elsewhere work; the backup of the old tip, saved through the pull's callback.
        Assert.Equal(PartA.LocalWorkRebasedSha, await RevParseAsync(repo, "refs/heads/main"));
        await AssertCommitAsync(repo, PartA.LocalWorkRebasedSha, "Local work", PartA.ElsewhereWorkSha, ScenarioTime(6), AppCommitTime);
        Assert.Equal("elsewhere\n", (await repo.GitAsync("show", $"{PartA.LocalWorkRebasedSha}:src/elsewhere.txt")).StandardOutput);
        Assert.Equal("local\n", (await repo.GitAsync("show", $"{PartA.LocalWorkRebasedSha}:src/local.txt")).StandardOutput);
        Assert.Equal(["## main...origin/main [ahead 1]"], await GitLinesAsync(repo, "status", "-sb"));
        AssertNoRebaseFolder(repo);
        Assert.Equal(
            ["rebase (finish): returning to refs/heads/main", "rebase (pick): Local work", "rebase (start): checkout refs/remotes/origin/main"],
            await ReflogAsync(repo, 3));
        await AssertOneRebaseBackupAsync(repo, "main", PartA.LocalWorkSha);
        Assert.Equal(PartA.UnpushedWorkSha, await RevParseAsync(repo, "refs/heads/published"));
        Assert.Equal(PartA.TopicWorkSha, await RevParseAsync(repo, "refs/heads/topic"));
        Assert.Equal(PartA.PublishedWorkSha, await RevParseAsync(repo, "refs/remotes/origin/published"));
        Assert.Equal([$"refs/heads/main {PartA.ElsewhereWorkSha}", $"refs/heads/published {PartA.PublishedWorkSha}"], await OriginHeadsAsync(repo));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check A4, part B: "Pull from origin/published (rebase)" whose rebase stops with a conflict:
    /// the banner, the toolbar, the stage panel, the file resolved outside the app and marked
    /// resolved, then Continue ("sync" on published).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A4_pull_with_rebase_stopped_and_continued()
    {
        using var repo = await StoppedSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 3: the pull stops.
        var (mark, start) = await PullPublishedWithRebaseAsync(app, repo);
        var shot = Shot(app, "a4c-pull-stopped");
        await AssertPullStoppedAsync(app, repo, mark, start, shot);

        // Step 4: the working-changes row: the stage panel at the stop (5.3).
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, PartA.StoppedUnstagedTitle, PartA.NoStagedTitle);
        shot = Shot(app, "a4d-stop-stage-panel");
        Assert.True(Repo(app).IsWorkingRowSelected, "The working-changes row is not selected.");
        Assert.Equal(["U published.txt src"], FileRows(app, "UnstagedFileList"));
        var conflicted = await app.FileRowAsync("UnstagedFileList", "src/published.txt");
        AssertTip(conflicted, PartA.ConflictRowToolTip);
        var letter = conflicted.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "U");
        Assert.Equal(PaletteOf(app).Danger, (letter.Foreground as ISolidColorBrush)?.Color);
        Assert.Empty(FileRows(app, "StagedFileList"));
        Assert.Equal(PartA.StoppedSummary, CommitBoxText(app, "CommitSummary"));
        Assert.Equal(string.Empty, CommitBoxText(app, "CommitDescription"));
        var commit = (Button)app.FindByAutomationId("CommitButton");
        Assert.Equal(PartA.ContinueRebaseText, commit.Content);
        Assert.False(commit.IsEffectivelyEnabled, "Continue rebase is enabled while the file conflicts.");
        AssertTip(commit, PartA.ResolveConflictsFirstOneFile);
        var amend = app.FindByAutomationId("AmendCheckBox");
        Assert.False(amend.IsEffectivelyEnabled, "Amend is enabled during a rebase stop.");
        AssertTip(amend, PartA.AmendDuringRebase);
        AssertGraph(app, PartA.S4, 3, PartA.S4Working, picture: shot);

        // Step 5: the file written outside the app; then "Mark as resolved" from its menu.
        var reads = app.CountLogLines(AppLogFile.WorkingTreeRead(PartA.SyncRepo));
        repo.WriteFile("src/published.txt", PartA.PublishedResolved);
        await app.WaitForAsync(
            () => app.CountLogLines(AppLogFile.WorkingTreeRead(PartA.SyncRepo)) > reads,
            "one more read of the working tree after the file was written",
            OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        var read = app.LogLines().Last(line => AppLogFile.WorkingTreeRead(PartA.SyncRepo).IsMatch(line));
        Assert.EndsWith("] sync: working tree: 0 staged, 1 unstaged", read, StringComparison.Ordinal);
        Assert.Equal(["UU src/published.txt"], await GitLinesAsync(repo, "status", "--porcelain"));

        var row = await app.FileRowAsync("UnstagedFileList", "src/published.txt");
        app.RightClick(app.BoundsOf(row).Center);
        var menu = app.Menu();
        AssertMenuAsReported(app, menu, PartA.ConflictedFileMenu, "Resolve conflict", PartA.ConflictedFileMenuHeight);
        Assert.Null(Shown(app, "ConflictResolver"));
        var calls = app.GitCalls().Count;
        app.ClickMenuItem("Mark as resolved");
        await WaitForStagePanelAsync(app, PartA.NoUnstagedTitle, PartA.OneStagedTitle);
        await app.WaitForAsync(() => ((TextBlock)app.FindByAutomationId("BannerText")).Text == PartA.ResolvedBanner, "the banner of the resolved stop", OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        shot = Shot(app, "a4e-resolved");
        await AssertNoDialogAsync(app);
        AssertCall(app, calls, PartA.AddPublishedCommand, 0);
        Assert.Equal(["M  src/published.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        AssertBanner(app, PartA.ResolvedBanner, PartA.AbortRebaseText, BannerButton.Continue, BannerButton.Skip, oneLine: false, placement: BannerButtonPlacement.OwnRow);
        AssertGraph(app, PartA.S4, 3, PartA.S4Resolved, picture: shot);
        Assert.Empty(FileRows(app, "UnstagedFileList"));
        Assert.Equal(["M published.txt src"], FileRows(app, "StagedFileList"));
        Assert.Equal(PartA.StoppedSummary, CommitBoxText(app, "CommitSummary"));
        commit = (Button)app.FindByAutomationId("CommitButton");
        Assert.Equal(PartA.ContinueRebaseText, commit.Content);
        Assert.True(commit.IsEffectivelyEnabled, $"Continue rebase is disabled: \"{ToolTip.GetTip(commit)}\".");
        AssertStatus(app, PartA.Ready);

        // Step 6: Continue: the marker check, the app's commit with the stop's author, git's continue.
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        using (var recorder = app.RecordStatusTexts())
        {
            app.ClickBannerContinue();
            await WaitForOperationAsync(app, repo, PartA.ContinueTitle, mark);
            Assert.Contains(PartA.ContinuingTheRebase, recorder.Texts);
        }

        shot = Shot(app, "a4f-continued");
        await AssertNoDialogAsync(app);
        AssertCalledInOrder(app, calls, PartA.MarkerCheckCommand, PartA.ContinueCommitCommand, PartA.RebaseContinueCommand);
        var check = AssertCall(app, calls, PartA.MarkerCheckCommand, 0);
        Assert.Equal(string.Empty, check.StandardOutput);
        AssertCall(app, calls, PartA.ContinueCommitCommand, 0);
        var rebaseContinue = AssertCall(app, calls, PartA.RebaseContinueCommand, 0);
        await AssertGitPrintedAsync(rebaseContinue.StandardError, ["Successfully rebased and updated refs/heads/published."], PartA.RebaseContinueCommand);
        AssertStatus(app, PartA.RebasedPublished);
        AssertOperationLine(app, mark, PartA.SyncRepo, PartA.ContinueTitle, OperationOutcome.Succeeded, PartA.RebasedPublished);
        Assert.Null(app.BannerBounds);
        AssertGraph(app, PartA.S5, 2, picture: shot);
        AssertBranch(app, "published");
        await AssertLeftPanelAsync(app, PartA.S5LeftPanel);
        await AssertLocalBranchAsync(app, ahead: 1, behind: 0, head: true, "published");
        AssertNothingSelected(app);

        // Git: Unpushed work committed with its author and author date, on Published elsewhere.
        Assert.Equal(PartA.UnpushedWorkContinuedSha, await RevParseAsync(repo, "refs/heads/published"));
        await AssertCommitAsync(repo, PartA.UnpushedWorkContinuedSha, "Unpushed work", PartA.PublishedElsewhereSha, ScenarioTime(3), AppCommitTime);
        Assert.Equal(PartA.PublishedResolved, (await repo.GitAsync("show", $"{PartA.UnpushedWorkContinuedSha}:src/published.txt")).StandardOutput);
        Assert.Equal(["## published...origin/published [ahead 1]"], await GitLinesAsync(repo, "status", "-sb"));
        AssertNoRebaseFolder(repo);
        Assert.Equal(
            ["rebase (continue) (finish): returning to refs/heads/published", "commit: Unpushed work", "rebase (start): checkout refs/remotes/origin/published"],
            await ReflogAsync(repo, 3));
        await AssertOneRebaseBackupAsync(repo, "published", PartA.UnpushedWorkSha);
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check A4, part C: the same stop, then the banner's Skip: its question, then the skip, its
    /// backup and the branch at origin's tip ("sync" on published).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A4_pull_with_rebase_stopped_and_skipped()
    {
        using var repo = await StoppedSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 7: as step 3.
        var (mark, _) = await PullPublishedWithRebaseAsync(app, repo);
        AssertStatus(app, PartA.PullStopped, OperationStatusTone.Warning);
        AssertBanner(app, PartA.StoppedBanner, PartA.AbortRebaseText, BannerButton.ContinueDisabled(PartA.ResolveConflictsFirstOneFile), BannerButton.Skip);
        AssertOperationLine(app, mark, PartA.SyncRepo, PartA.PullRebaseTitle, OperationOutcome.Conflicts, PartA.PullStopped);

        // Step 8: Skip asks first; nothing has run.
        var calls = app.GitCalls().Count;
        app.ClickBannerSkip();
        await AssertConfirmationAsync(app, PartA.SkipQuestionTitle, PartA.SkipQuestion, PartA.BackupDetail, "Skip", destructive: true);
        Shot(app, "a4g-skip-question");
        Assert.DoesNotContain(PartA.RebaseSkipCommand, CallsSince(app, calls));
        AssertNoClippedText(app);

        // Step 9: "Skip" in the dialog: the backup first, then git's skip.
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        using (var recorder = app.RecordStatusTexts())
        {
            app.Click(app.FindByAutomationId("ConfirmButton"));
            await WaitForOperationAsync(app, repo, PartA.SkipTitle, mark);
            Assert.Contains(PartA.Skipping, recorder.Texts);
        }

        var shot = Shot(app, "a4h-skipped");
        await AssertNoDialogAsync(app);
        AssertCalledInOrder(app, calls, "git update-ref --stdin", PartA.RebaseSkipCommand);
        var skip = AssertCall(app, calls, PartA.RebaseSkipCommand, 0);
        await AssertGitPrintedAsync(skip.StandardError, ["Successfully rebased and updated refs/heads/published."], PartA.RebaseSkipCommand);
        AssertStatus(app, PartA.RebasedPublished);
        AssertOperationLine(app, mark, PartA.SyncRepo, PartA.SkipTitle, OperationOutcome.Succeeded, PartA.RebasedPublished);
        Assert.Null(app.BannerBounds);
        AssertGraph(app, PartA.S6, 2, picture: shot);
        AssertBranch(app, "published");
        await AssertLeftPanelAsync(app, PartA.S6LeftPanel);
        await AssertLocalBranchAsync(app, ahead: 0, behind: 0, head: true, "published");
        AssertNothingSelected(app);

        // Git: published at origin's tip; the rebase's backup, Skip's backup of HEAD and its snapshot.
        Assert.Equal(PartA.PublishedElsewhereSha, await RevParseAsync(repo, "refs/heads/published"));
        Assert.Equal(PartA.PublishedElsewhereSha, await RevParseAsync(repo, "refs/remotes/origin/published"));
        Assert.Equal(["## published...origin/published"], await GitLinesAsync(repo, "status", "-sb"));
        Assert.Equal("published\nelsewhere\n", WorkingFile(repo, "src/published.txt"));
        Assert.Equal("rebase (skip) (finish): returning to refs/heads/published", (await ReflogAsync(repo, 1))[0]);
        AssertNoRebaseFolder(repo);
        var backups = await BackupsAsync(repo);
        Assert.Equal(3, backups.Count);
        var rebase = Assert.Single(backups, backup => backup.Ref.Kind == "rebase");
        Assert.Equal(("published", false, PartA.UnpushedWorkSha), (rebase.Ref.Name, rebase.Ref.IsChanges, rebase.Sha));
        var head = Assert.Single(backups, backup => backup.Ref.Kind == "skip" && !backup.Ref.IsChanges);
        var changes = Assert.Single(backups, backup => backup.Ref.Kind == "skip" && backup.Ref.IsChanges);
        Assert.Null(head.Ref.Name);
        Assert.Equal(PartA.PublishedElsewhereSha, head.Sha);
        Assert.Equal(head.Name + "-changes", changes.Name);
        Assert.Equal(PartA.PublishedElsewhereSha, await RevParseAsync(repo, changes.Name + "^"));
        Assert.Equal(PartA.PublishedConflicted, (await repo.GitAsync("show", changes.Name + ":src/published.txt")).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n'));
        Assert.Null(await RevParseAsync(repo, changes.Name + "^2"));
        AssertNoClippedText(app);
    }

    /// <summary>A4 parts B and C before the app starts: "sync" on published, and "Published elsewhere" pushed to origin's published.</summary>
    private static async Task<TempRepo> StoppedSyncAsync()
    {
        var repo = await Scenarios.RemoteSyncAsync();
        try
        {
            await repo.CheckoutAsync("published");
            Assert.Equal(PartA.PublishedElsewhereSha, await Scenarios.PushFromElsewhereAsync(repo, PartA.PublishedElsewhere.Branch, PartA.PublishedElsewhere.File, PartA.PublishedElsewhere.Content, PartA.PublishedElsewhere.Message));
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>
    /// A4 step 3 (and 7): S3 at the start; then published's menu and "Pull from
    /// origin/published (rebase)", and the wait until it ended. Returns the log mark and the count
    /// of git calls read before it.
    /// </summary>
    private static async Task<(int Mark, int Calls)> PullPublishedWithRebaseAsync(ShellDriver app, TempRepo repo)
    {
        AssertGraph(app, PartA.S3, 2, showsDate: true);
        AssertBranch(app, "published");
        await AssertLeftPanelAsync(app, PartA.S3LeftPanel);
        Assert.Equal(["## published...origin/published [ahead 1]"], await GitLinesAsync(repo, "status", "-sb"));
        var (menu, _) = await RightClickRefAsync(app, "Local branches", "published");
        Assert.True(menu.Item("Pull from origin/published (rebase)").IsEnabled, "\"Pull from origin/published (rebase)\" is disabled.");
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem("Pull from origin/published (rebase)");
        await WaitForOperationAsync(app, repo, PartA.PullRebaseTitle, mark, NetworkTimeout);
        return (mark, calls);
    }

    /// <summary>A4 after step 3: the pull's calls, the stop's status, banner, graph, toolbar, left panel and git's state.</summary>
    private static async Task AssertPullStoppedAsync(ShellDriver app, TempRepo repo, int mark, int start, Screenshot shot)
    {
        await AssertNoDialogAsync(app);
        AssertCalledInOrder(
            app,
            start,
            PartA.FetchOriginCommand,
            PartA.IsAncestorOfFetchHead(PartA.UnpushedWorkSha),
            PartA.ForkPoint("origin/published"),
            PartA.CountAll("origin/published"),
            PartA.CountAfterForkPoint("origin/published", PartA.PublishedWorkSha),
            "git update-ref --stdin",
            PartA.RebaseWithoutArgumentsCommand);
        var fetch = AssertCall(app, start, PartA.FetchOriginCommand, 0);
        await AssertLastPrintedAsync(fetch.StandardError, PartA.FetchPublishedLine, PartA.FetchOriginCommand);
        AssertCall(app, start, PartA.IsAncestorOfFetchHead(PartA.UnpushedWorkSha), 1);
        Assert.Equal(PartA.PublishedWorkSha, AssertCall(app, start, PartA.ForkPoint("origin/published"), 0).StandardOutput.Trim());
        Assert.Equal("1", AssertCall(app, start, PartA.CountAll("origin/published"), 0).StandardOutput.Trim());
        Assert.Equal("1", AssertCall(app, start, PartA.CountAfterForkPoint("origin/published", PartA.PublishedWorkSha), 0).StandardOutput.Trim());
        var rebase = AssertCall(app, start, PartA.RebaseWithoutArgumentsCommand, 1);
        await AssertTa4Async(rebase);
        Assert.True(InGitDir(repo, "rebase-merge"), "No rebase-merge folder: the rebase did not stop.");

        // The status bar, the log, the banner.
        AssertStatus(app, PartA.PullStopped, OperationStatusTone.Warning);
        AssertOperationLine(app, mark, PartA.SyncRepo, PartA.PullRebaseTitle, OperationOutcome.Conflicts, PartA.PullStopped);
        var banner = AssertBanner(app, PartA.StoppedBanner, PartA.AbortRebaseText, BannerButton.ContinueDisabled(PartA.ResolveConflictsFirstOneFile), BannerButton.Skip, oneLine: false, placement: BannerButtonPlacement.OwnRow);
        var text = app.Find<TextBlock>("BannerText");
        Assert.Equal(TextTrimming.None, text.TextTrimming);
        Assert.True(text.TextLayout.TextLines.Count > 1, "The banner's text takes one line.");
        Assert.Equal(PartA.BannerToolTip, ToolTip.GetTip(app.Find<Border>("BannerInfo")) as string);
        AssertColour(PaletteOf(app).BannerBackground, shot, new Point(banner.Left + 4, banner.Top + 4), "the banner's background");
        Assert.True(app.Find<Control>("BannerIcon").IsEffectivelyVisible, "The banner shows no icon.");

        // The graph below it, nothing selected, the branch, the left panel, the toolbar.
        AssertGraph(app, PartA.S4, 3, PartA.S4Working, showsDate: true, picture: shot);
        AssertNothingSelected(app);
        AssertBranch(app, PartA.DetachedAtPublishedElsewhere);
        await AssertLeftPanelAsync(app, PartA.S4LeftPanel);
        await AssertLocalBranchAsync(app, ahead: 1, behind: 1, head: false, "published");
        await AssertLocalBranchAsync(app, ahead: 0, behind: 0, head: false, "main");
        await AssertLocalBranchAsync(app, ahead: 0, behind: 0, head: false, "topic");
        foreach (var (button, reason) in PartA.StoppedToolbarDisabled)
        {
            AssertToolbarButton(app, button, reason);
        }

        foreach (var button in PartA.StoppedToolbarEnabled)
        {
            AssertToolbarButton(app, button, null);
        }

        // Git: the stop as git left it.
        Assert.Equal(["UU src/published.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(PartA.PublishedElsewhereSha, await repo.HeadAsync());
        Assert.False((await repo.GitAllowingFailureAsync("symbolic-ref", "--quiet", "HEAD")).Succeeded, "HEAD is not detached.");
        Assert.Equal("refs/heads/published", GitDirFile(repo, "rebase-merge/head-name").Trim());
        Assert.Equal(PartA.PublishedElsewhereSha, GitDirFile(repo, "rebase-merge/onto").Trim());
        Assert.Equal(PartA.UnpushedWorkSha, GitDirFile(repo, "rebase-merge/orig-head").Trim());
        Assert.Equal("1", GitDirFile(repo, "rebase-merge/msgnum").Trim());
        Assert.Equal("1", GitDirFile(repo, "rebase-merge/end").Trim());
        Assert.StartsWith("8e65315", GitDirFile(repo, "rebase-merge/stopped-sha").Trim(), StringComparison.Ordinal);
        Assert.Equal("Unpushed work\n\n", GitDirFile(repo, "rebase-merge/message"));
        Assert.Equal($"pick {PartA.UnpushedWorkSha} Unpushed work", GitDirFile(repo, "rebase-merge/done").Trim());
        Assert.Equal(string.Empty, GitDirFile(repo, "rebase-merge/git-rebase-todo"));
        Assert.Contains("GIT_AUTHOR_DATE='@1767268980 +0000'", GitDirFile(repo, "rebase-merge/author-script").Split('\n'));
        Assert.Equal(PartA.UnpushedWorkSha, GitDirFile(repo, "REBASE_HEAD").Trim());
        Assert.Equal(["Unpushed work", string.Empty, "# Conflicts:", "#\tsrc/published.txt"], GitDirFile(repo, "MERGE_MSG").TrimEnd('\n').Split('\n'));
        Assert.Equal(PartA.PublishedConflicted, WorkingFile(repo, "src/published.txt").TrimEnd('\n').Split('\n'));
        await AssertOneRebaseBackupAsync(repo, "published", PartA.UnpushedWorkSha);
        Assert.IsNotType<ConfirmationViewModel>(app.Session.Services.DialogHost.Current);
    }

    /// <summary>TA4: the pull's <c>git rebase</c> that stopped, word for word on Git 2.36.</summary>
    private static async Task AssertTa4Async(VisualCommit.Core.Git.GitCallRecord rebase)
    {
        if (!await IsReportGitAsync())
        {
            return;
        }

        Assert.Equal(PartA.Ta4Output, Printed(rebase.StandardOutput));
        var error = Printed(rebase.StandardError);
        Assert.True(
            error.Count == PartA.Ta4ErrorStart.Length + PartA.Ta4Hints + 1
                && error.Take(PartA.Ta4ErrorStart.Length).SequenceEqual(PartA.Ta4ErrorStart)
                && error.Skip(PartA.Ta4ErrorStart.Length).Take(PartA.Ta4Hints).All(line => line.StartsWith("hint:", StringComparison.Ordinal))
                && error[^1] == PartA.Ta4ErrorEnd,
            "git rebase's standard error is not TA4:\n  " + string.Join("\n  ", error));
    }

    /// <summary>On Git 2.36, the last line git printed on a stream (a fetch's ref update).</summary>
    private static async Task AssertLastPrintedAsync(string stream, string expected, string what)
    {
        if (await IsReportGitAsync())
        {
            var lines = stream.Replace("\u001b[K", string.Empty, StringComparison.Ordinal).Split(['\r', '\n']).Where(line => line.Trim().Length > 0).ToList();
            Assert.True(lines.Count > 0 && lines[^1].TrimEnd() == expected, $"{what}'s last line is \"{(lines.Count > 0 ? lines[^1] : string.Empty)}\"; the report says \"{expected}\".");
        }
    }
}
