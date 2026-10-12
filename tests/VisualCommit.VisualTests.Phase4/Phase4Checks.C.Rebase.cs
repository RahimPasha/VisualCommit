// Check C11 (docs/test-reports/phase-4.md, part C): a rebase with two conflicting commits, started
// from the app's branch menu: the first stop's banner, the panes with the rebase's titles, Use
// theirs, Mark resolved, Continue to the second stop, Use both, Mark resolved, Continue to the end,
// with the graph and the backup.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksC
{
    /// <summary>
    /// Check C11, one app start on a "conflicts" copy with feature/rebase checked out (as a
    /// terminal would): the rebase from main's branch menu stops at First change and, after it is
    /// resolved and continued, at Second change; both are resolved in the resolver and continued.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_C11_rebase_with_two_conflicting_commits()
    {
        using var repo = await Scenarios.ConflictsAsync();
        await repo.CheckoutAsync("feature/rebase");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: GC0r, nothing in progress.
        Assert.Null(app.BannerBounds);
        AssertGraph(app, Gc0r, 5, working: null, showsDate: false, picture: app.Capture());
        AssertNothingSelected(app);

        // Step 2: Rebase feature/rebase onto main from main's menu in the left panel.
        var mainRow = await app.RefRowAsync("Local branches", "main");
        app.RightClick(mainRow);
        app.Menu();
        AssertNoLeftPanelSelected(app);
        AssertNothingSelected(app);
        var logMark = app.LogMark();
        var callMark = app.GitCalls().Count;
        app.ClickMenuItem("Rebase feature/rebase onto main");
        await WaitForOperationAsync(app, repo, RebaseOperation, logMark);
        await AssertNoDialogAsync(app);
        var shot = Shot(app, "c11a-first-stop");
        var backups = await BackupsAsync(repo);
        var backup = Assert.Single(backups);
        Assert.Equal(("rebase", false, "feature/rebase"), (backup.Ref.Kind, backup.Ref.IsChanges, backup.Ref.Name));
        Assert.Equal(StopSha.SecondChange, backup.Sha);
        var rebase = AssertCalled(app, callMark, RebaseOntoMainCall);
        Assert.Equal(1, rebase.ExitCode);
        await AssertGitStandardOutputAsync(rebase, AutoMergingNotes, ConflictInNotes);
        AssertCalledInOrder(app, callMark, BackupWriteCall, RebaseOntoMainCall);
        AssertStatus(app, RebaseStoppedStatus, OperationStatusTone.Warning);
        var resolveFirst = BannerButton.ContinueDisabled(ResolveFirstOneFile);
        AssertStopBanner(app, shot, RebaseFirstBanner, AbortRebase, resolveFirst, BannerButton.Skip);
        AssertGraph(app, GcRebaseFirst, 5, WorkingOneConflict, showsDate: false, picture: shot);
        await AssertLocalBranchesAsync(app, mainIsHead: false);
        AssertBranch(app, "Detached at da31c97");
        await AssertRebaseFirstStopAsync(repo);

        // Step 3: the banner's tooltip.
        var pointer = app.BoundsOf(app.Find<TextBlock>("BannerText")).Center;
        var tip = await app.RestPointerOnAsync(app.Find<TextBlock>("BannerText"));
        Shot(app, "c11b-banner-tooltip", keepPointer: true);
        Assert.Equal(RebaseBannerTip, tip);
        Assert.Equal(RebaseBannerTip, BannerTip(app));
        AssertTipInsideWindow(app, RebaseBannerTip, pointer);

        // Step 4: the stage panel at the rebase's stop.
        app.MoveToNeutral();
        await ClickWorkingRowAsync(app);
        await AssertConflictedNotesRowAsync(app);
        shot = Shot(app, "c11c-stage-panel");
        AssertWorkingRowSelected(app, shot);
        await AssertStagePanelAsync(app, UnstagedOneConflict, ["U notes.txt"], StagedNone, [], "First change", ContinueRebaseButton, commitEnabled: false, ResolveFirstOneFile);
        AssertAmendDisabled(app, FinishRebaseFirst);

        // Step 5: a click on the file opens the resolver.
        app.Click(await app.FileRowAsync("UnstagedFileList", ResolverFile));
        await app.WaitForResolverAsync(ResolverFile);
        await app.WaitForIdleAsync();
        shot = Shot(app, "c11d-resolver-first");
        AssertResolverLayout(app);
        AssertStopBanner(app, shot, RebaseFirstBanner, AbortRebase, resolveFirst, BannerButton.Skip, overResolver: true);
        AssertResolverState(app, shot, RebaseFirstTitles, C11FirstOpened);
        AssertNothingScrolled(app);

        // Step 6: Use theirs.
        await ChooseAsync(app, 0, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs);
        shot = Shot(app, "c11e-first-theirs");
        AssertResolverState(app, shot, RebaseFirstTitles, C11FirstTheirs);

        // Step 7: Mark resolved; the banner offers Continue.
        callMark = app.GitCalls().Count;
        await MarkResolvedAsync(app, repo);
        shot = Shot(app, "c11f-first-resolved");
        await AssertNoDialogAsync(app);
        Assert.True(AssertCalled(app, callMark, AddNotes).Succeeded);
        AssertStatus(app, ResolvedStatus);
        await AssertResolverClosedAsync(app);
        AssertStopBanner(app, shot, RebaseFirstResolvedBanner, AbortRebase, BannerButton.Continue, BannerButton.Skip);
        AssertGraph(app, GcRebaseFirst, 5, WorkingNoConflict, showsDate: false, picture: shot);
        await AssertLocalBranchesAsync(app, mainIsHead: false);
        await AssertStagePanelAsync(app, UnstagedNone, [], StagedOne, ["M notes.txt"], "First change", ContinueRebaseButton, commitEnabled: true);
        Assert.Equal(["M  notes.txt"], await PorcelainAsync(repo));
        Assert.Equal(NotesRebaseFirstTheirs, WorkingFile(repo, ResolverFile));
        Assert.Equal("1\n", GitFile(repo, "rebase-merge/msgnum"));
        Assert.Equal(StopSha.FirstChange + "\n", GitFile(repo, "rebase-merge/stopped-sha"));

        // Step 8: Continue commits First change with its author and goes on to the second stop.
        logMark = app.LogMark();
        callMark = app.GitCalls().Count;
        app.ClickBannerContinue();
        await WaitForOperationAsync(app, repo, ContinueRebaseOperation, logMark);
        await AssertNoDialogAsync(app);
        shot = Shot(app, "c11g-second-stop");
        AssertCalledInOrder(app, callMark, RebaseCommitCall, RebaseContinueCall);
        Assert.True(AssertCalled(app, callMark, RebaseCommitCall).Succeeded);
        var continued = AssertCalled(app, callMark, RebaseContinueCall);
        Assert.Equal(1, continued.ExitCode);
        await AssertGitPrintedAsync(continued, ConflictInNotes);
        AssertStatus(app, RebaseStoppedStatus, OperationStatusTone.Warning);
        AssertStopBanner(app, shot, RebaseSecondBanner, AbortRebase, resolveFirst, BannerButton.Skip);
        AssertGraph(app, GcRebaseSecond, 5, WorkingOneConflict, showsDate: false, picture: shot);
        AssertBranch(app, "Detached at 99070f8");
        Assert.Equal(["UU notes.txt"], await PorcelainAsync(repo));
        Assert.Equal(StopSha.NewFirst, await repo.HeadAsync());
        await AssertCommitAsync(repo, StopSha.NewFirst, ScenarioTime(3), AppCommitTime, StopSha.OursEdits);
        Assert.Equal("First change\n", await CommitMessageAsync(repo, StopSha.NewFirst));
        Assert.Equal(NotesRebaseFirstTheirs, await FileAtAsync(repo, StopSha.NewFirst, ResolverFile));
        AssertRebaseFiles(repo, RebaseSecondStopFiles);
        Assert.Equal(NotesRebaseSecond, WorkingFile(repo, ResolverFile));
        await AssertBranchesAsync(repo);

        // Step 9: the second stop in the resolver.
        await ClickWorkingRowAsync(app);
        await WaitForStageTitlesAsync(app, UnstagedOneConflict, StagedNone);
        Assert.Equal("Second change", BoxText(app, "CommitSummary"));
        app.Click(await app.FileRowAsync("UnstagedFileList", ResolverFile));
        await app.WaitForResolverAsync(ResolverFile);
        await app.WaitForIdleAsync();
        shot = Shot(app, "c11h-resolver-second");
        AssertResolverLayout(app);
        AssertStopBanner(app, shot, RebaseSecondBanner, AbortRebase, resolveFirst, BannerButton.Skip, overResolver: true);
        AssertResolverState(app, shot, RebaseSecondTitles, C11SecondOpened);
        AssertBarsInView(app, 0);
        AssertResultLinesInView(app, 10, 14);

        // Step 10: Use both from the theirs pane.
        await ChooseAsync(app, 0, ConflictPaneSide.Theirs, ConflictChoice.UseBothTheirsFirst);
        shot = Shot(app, "c11i-second-both");
        AssertResolverState(app, shot, RebaseSecondTitles, C11SecondBoth);

        // Step 11: Mark resolved, then Continue to the end of the rebase.
        callMark = app.GitCalls().Count;
        await MarkResolvedAsync(app, repo);
        await AssertNoDialogAsync(app);
        Assert.True(AssertCalled(app, callMark, AddNotes).Succeeded);
        AssertStatus(app, ResolvedStatus);
        logMark = app.LogMark();
        callMark = app.GitCalls().Count;
        app.ClickBannerContinue();
        await WaitForOperationAsync(app, repo, ContinueRebaseOperation, logMark);
        await AssertNoDialogAsync(app);
        await ClickCommitRowAsync(app, 0, "Second change");
        shot = Shot(app, "c11j-rebased");
        AssertCalledInOrder(app, callMark, RebaseCommitCall, RebaseContinueCall);
        Assert.True(AssertCalled(app, callMark, RebaseCommitCall).Succeeded);
        var finished = AssertCalled(app, callMark, RebaseContinueCall);
        Assert.True(finished.Succeeded);
        if (await IsReportGitAsync())
        {
            Assert.EndsWith(SuccessfullyRebased, finished.StandardError.TrimEnd(), StringComparison.Ordinal);
        }

        Assert.Null(app.BannerBounds);
        AssertStatus(app, RebasedStatus);
        AssertBranch(app, "feature/rebase");
        AssertGraph(app, GcRebased, 4, working: null, showsDate: false, picture: shot);
        AssertSelectedCommits(app, shot, "2083808");
        await AssertCommitDetailsAsync(app, "Second change", body: null, "2026-01-01 12:04", AppDate, ["99070f8"], "M notes.txt");
        Assert.Equal(["## feature/rebase"], await LinesOfGitAsync(repo, "status", "--short", "--branch"));
        Assert.Equal(["2083808 Second change", "99070f8 First change", "da31c97 Ours edits"], await LinesOfGitAsync(repo, "log", "--format=%h %s", "-3", "feature/rebase"));
        Assert.Equal(StopSha.NewSecond, await FullShaAsync(repo, "refs/heads/feature/rebase"));
        await AssertCommitAsync(repo, StopSha.NewSecond, ScenarioTime(4), AppCommitTime, StopSha.NewFirst);
        Assert.Equal("Second change\n", await CommitMessageAsync(repo, StopSha.NewSecond));
        Assert.Equal(NotesRebaseSecondBoth, await FileAtAsync(repo, StopSha.NewSecond, ResolverFile));
        Assert.False(GitHas(repo, "rebase-merge"), "rebase-merge/ is still there.");
        await AssertBranchesAsync(repo, ("refs/heads/feature/rebase", "2083808"));
        backup = Assert.Single(await BackupsAsync(repo));
        Assert.Equal(("rebase", false, "feature/rebase"), (backup.Ref.Kind, backup.Ref.IsChanges, backup.Ref.Name));
        Assert.Equal(StopSha.SecondChange, backup.Sha);
    }
}
