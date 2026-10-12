// Check C10 (docs/test-reports/phase-4.md, part C): a merge conflict end to end, from the banner
// with Continue disabled through the resolver (its layout, titles and panes, a choice per block,
// the platform's undo, Use both, Mark resolved) to Continue and the merge commit.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksC
{
    /// <summary>
    /// Check C10, one app start on a "conflicts" copy on which <c>git merge feature/theirs</c>
    /// stopped in a terminal: the banner, its Continue's reason and its tooltip, the stage panel,
    /// the resolver opened by a double-click, Use ours on block 1, the next conflict, Use theirs on
    /// block 2, the platform's undo, Use both on block 2, Mark resolved, and Continue.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_C10_merge_conflict_end_to_end()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: the banner over the graph; no dialog.
        await AssertNoDialogAsync(app);
        var shot = Shot(app, "c10a-banner");
        var resolveFirst = BannerButton.ContinueDisabled(ResolveFirstOneFile);
        AssertStopBanner(app, shot, MergeBanner, AbortMerge, resolveFirst, skipButton: null);
        AssertGraph(app, GcMerge, 5, WorkingOneConflict, showsDate: false, picture: shot);
        AssertNothingSelected(app);
        AssertStatus(app, "Ready");
        AssertBranch(app, "main");
        Assert.Equal(["UU notes.txt"], await PorcelainAsync(repo));
        Assert.Equal(StopSha.TheirsEdits, GitFile(repo, "MERGE_HEAD")?.Trim());
        Assert.Equal(MergeMsg, GitFile(repo, "MERGE_MSG"));
        Assert.Equal(NotesMerge, WorkingFile(repo, ResolverFile));
        Assert.Equal(MergeStages, await StagesOfNotesAsync(repo));
        await AssertBranchesAsync(repo);
        Assert.Empty(await BackupsAsync(repo));

        // Step 2: Continue's reason under the pointer.
        var tip = await app.RestPointerOnAsync(app.BannerContinueButton);
        Shot(app, "c10b-continue-reason", keepPointer: true);
        Assert.Equal(ResolveFirstOneFile, tip);
        AssertTipInsideWindow(app, ResolveFirstOneFile);

        // Step 3: the banner's own tooltip (10.3), the pointer on its text.
        app.MoveToNeutral();
        tip = await app.RestPointerOnAsync(app.Find<TextBlock>("BannerText"));
        Shot(app, "c10c-banner-tooltip", keepPointer: true);
        Assert.Equal(MergeBannerTip, tip);
        Assert.Equal(MergeBannerTip, BannerTip(app));
        AssertTipInsideWindow(app, MergeBannerTip);

        // Step 4: the stage panel; the pointer rests on the conflicted file, which opens nothing.
        app.MoveToNeutral();
        await ClickWorkingRowAsync(app);
        var row = await AssertConflictedNotesRowAsync(app);
        tip = await app.RestPointerOnAsync(row);
        shot = Shot(app, "c10d-stage-panel", keepPointer: true);
        Assert.Equal(ConflictRowTip, tip);
        AssertWorkingRowSelected(app, shot);
        await AssertStagePanelAsync(app, UnstagedOneConflict, ["U notes.txt"], StagedNone, [], MergeSummary, StageFilesToCommit, commitEnabled: false);
        Assert.Null(app.ShownResolver);
        await AssertNoDialogAsync(app);

        // Step 5: a double-click on the file opens the resolver once and changes nothing.
        app.MoveToNeutral();
        var logMark = app.LogMark();
        var callMark = app.GitCalls().Count;
        app.DoubleClick(await app.FileRowAsync("UnstagedFileList", ResolverFile));
        await app.WaitForResolverAsync(ResolverFile);
        await app.WaitForIdleAsync();
        shot = Shot(app, "c10e-resolver-open");
        AssertResolverLayout(app);
        AssertStopBanner(app, shot, MergeBanner, AbortMerge, resolveFirst, skipButton: null, overResolver: true);
        AssertResolverState(app, shot, MergeTitles, C10Opened);
        AssertNothingScrolled(app);
        Assert.DoesNotContain(app.LogLinesSince(logMark), line => line.Contains(ResolveOperation, StringComparison.Ordinal));
        Assert.DoesNotContain(CallsSince(app, callMark), call => call.StartsWith("git add", StringComparison.Ordinal));
        Assert.Equal(["UU notes.txt"], await PorcelainAsync(repo));

        // Step 6: Use ours on block 1; a choice does not move on, and nothing is written.
        await ChooseAsync(app, 0, ConflictPaneSide.Ours, ConflictChoice.UseOurs);
        shot = Shot(app, "c10f-block1-ours");
        AssertResolverState(app, shot, MergeTitles, C10Block1Ours);
        Assert.Equal(NotesMerge, WorkingFile(repo, ResolverFile));
        Assert.Equal(["UU notes.txt"], await PorcelainAsync(repo));

        // Step 7: the next conflict: block 2 reached and in view.
        app.ClickResolverButton("ResolverNextConflict");
        await app.WaitForAsync(() => app.ResultCaret.Line == 10, "the caret at block 2's <<<<<<< line");
        shot = Shot(app, "c10g-conflict-2");
        AssertResolverState(app, shot, MergeTitles, C10Conflict2, resultFocus: null);
        AssertBarsInView(app, 1);
        AssertResultLinesInView(app, 10, 14);

        // Step 8: Use theirs on block 2.
        await ChooseAsync(app, 1, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs);
        shot = Shot(app, "c10h-block2-theirs");
        AssertResolverState(app, shot, MergeTitles, C10Block2Theirs);

        // Step 9: the platform's undo: one step back to R1, block 2 open again.
        app.Undo();
        await app.WaitForResultParsedAsync();
        shot = Shot(app, "c10i-undone");
        AssertResolverState(app, shot, MergeTitles, C10Undone);

        // Step 10: Use both from the ours pane on block 2.
        await ChooseAsync(app, 1, ConflictPaneSide.Ours, ConflictChoice.UseBothOursFirst);
        shot = Shot(app, "c10j-block2-both");
        AssertResolverState(app, shot, MergeTitles, C10Block2Both);

        // Step 11: Mark resolved asks nothing, writes R3 and stages it; the resolver closes.
        callMark = app.GitCalls().Count;
        await MarkResolvedAsync(app, repo);
        shot = Shot(app, "c10k-resolved");
        await AssertNoDialogAsync(app);
        Assert.True(AssertCalled(app, callMark, AddNotes).Succeeded);
        AssertStatus(app, ResolvedStatus);
        await AssertResolverClosedAsync(app);
        AssertGraph(app, GcMerge, 5, WorkingNoConflict, showsDate: false, picture: shot);
        AssertWorkingRowSelected(app, shot);
        AssertStopBanner(app, shot, MergeResolvedBanner, AbortMerge, BannerButton.Continue, skipButton: null);
        await AssertStagePanelAsync(app, UnstagedNone, [], StagedOne, ["M notes.txt"], MergeSummary, CommitOneFile, commitEnabled: true);
        Assert.Equal(["M  notes.txt"], await PorcelainAsync(repo));
        Assert.True(GitHas(repo, "MERGE_HEAD"), "MERGE_HEAD is gone.");
        Assert.Equal(NotesMergeR3, WorkingFile(repo, ResolverFile));
        Assert.Equal(string.Empty, (await repo.GitAsync("diff", "--cached", "--check", "--", ResolverFile)).StandardOutput);

        // Step 12: Continue commits the merge with the stage panel's message.
        logMark = app.LogMark();
        callMark = app.GitCalls().Count;
        app.ClickBannerContinue();
        await WaitForOperationAsync(app, repo, ContinueMergeOperation, logMark);
        await AssertNoDialogAsync(app);
        await ClickCommitRowAsync(app, 0, MergeSummary);
        shot = Shot(app, "c10l-merged");
        Assert.True(AssertCalled(app, callMark, MergeCommitCall).Succeeded);
        Assert.Null(app.BannerBounds);
        AssertStatus(app, MergeCommittedStatus);
        AssertGraph(app, GcMerged, 5, working: null, showsDate: false, picture: shot);
        AssertSelectedCommits(app, shot, "8a12c69");
        await AssertCommitDetailsAsync(app, MergeSummary, body: null, AppDate, commitDate: null, ["da31c97", "5c77ffb"], "M notes.txt");
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(StopSha.Merged, await repo.HeadAsync());
        Assert.Equal(MergeSummary + "\n", await CommitMessageAsync(repo, "HEAD"));
        await AssertCommitAsync(repo, "HEAD", AppCommitTime, AppCommitTime, StopSha.OursEdits, StopSha.TheirsEdits);
        Assert.Equal(NotesMergeR3, await FileAtAsync(repo, "HEAD", ResolverFile));
        Assert.False(GitHas(repo, "MERGE_HEAD"), "MERGE_HEAD is still there.");
        Assert.False(GitHas(repo, "MERGE_MSG"), "MERGE_MSG is still there.");
        Assert.Equal(["## main"], await LinesOfGitAsync(repo, "status", "--short", "--branch"));
        Assert.Empty(await BackupsAsync(repo));
    }
}
