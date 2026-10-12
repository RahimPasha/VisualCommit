// Part C's real-window steps for checks C10 to C13 (docs/test-reports/phase-4.md, part C, the "Real
// window" column): C10 steps 1 and 4 to 12, and C11 steps 1 to 3, each in a launch of its own on a
// fresh "conflicts" copy at 1100×700, dark. C12 and C13 are "n/a" (the report gives the reasons).
// While the banner shows, graph rows are clicked below the ConflictBanner element (its bottom, the
// column headers' 28 and half a row: RealApp.GraphRowPoint, D122); the resolver's texts are read by
// automation id, the result through its Value pattern, a bar's button by its automation id among
// the bars in view (block 1's being the upper one), and the undo is pressed on the real keyboard.
// What C14 to C16 share with these is in this file too.
using System.Drawing;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC;

namespace VisualCommit.RealWindowTests;

public partial class Phase4RealWindowPass
{
    /// <summary>The conflicts scenario's graphs at its stops have 5 lanes (GC0, GC-M, GC-R1, GC10).</summary>
    private const int ConflictsLanes = 5;

    /// <summary>The resolver's place at 1100×700 with the session's default widths (11.2): over the graph and right-panel columns, x 260 to 1100.</summary>
    private static readonly (float Left, float Width) ResolverPlace = ((float)WindowLayout.LeftPanelWidth, 1100 - (float)WindowLayout.LeftPanelWidth);

    /// <summary>
    /// Check C10, steps 1 and 4 to 12, with the real mouse and keyboard on one launch on a
    /// "conflicts" copy on which <c>git merge feature/theirs</c> stopped in a terminal: the banner
    /// with Continue disabled (<c>c10a-banner</c>); the working-changes row clicked below it and the
    /// pointer resting on notes.txt until its tooltip shows; a real double-click on notes.txt
    /// opening the resolver once, the result unchanged, <c>File 1 of 1</c>
    /// (<c>c10e-resolver-open</c>); the bars' buttons, the next-conflict button
    /// (<c>c10h-block2-theirs</c>), the undo on the real keyboard (<c>c10i-undone</c>), Use both
    /// (<c>c10j-block2-both</c>); Mark resolved (<c>c10k-resolved</c>) and the banner's Continue,
    /// then row 0, the merge commit (<c>c10l-merged</c>). Steps 2 and 3 (the banner's tooltips) are
    /// the scripted run's alone: C11 pictures the banner's tooltip in the real window.
    /// </summary>
    [Fact]
    public async Task Check_C10_merge_conflict_end_to_end()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var app = Launch(repo);
        var name = NameOf(repo);

        // Step 1: the banner over the graph; no dialog; git's stop.
        AssertBanner(app, MergeBanner, ResolveFirstOneFile, skip: false, AbortMerge, besideText: false);
        AssertBannerOverGraph(app);
        AssertNoDialog(app);
        Assert.Equal("Ready", app.OperationStatusText());
        Assert.Equal(["UU notes.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(StopSha.TheirsEdits, File.ReadAllText(Path.Combine(repo.Path, ".git", "MERGE_HEAD")).Trim());
        Assert.Equal(MergeMsg, File.ReadAllText(Path.Combine(repo.Path, ".git", "MERGE_MSG")).Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal(NotesMerge, WorkingFile(repo, ResolverFile));
        Assert.Empty(await repo.BackupRefsAsync());
        Compare(app, "c10a-banner");

        // Step 4: the working-changes row below the banner; the pointer rests on the conflicted
        // notes.txt until its tooltip shows, which opens nothing.
        app.MoveToNeutral();
        ClickWorkingRow(app, UnstagedOneConflict, StagedNone);
        var row = app.StageFileRow("UnstagedFileList", ResolverFile);
        RestUntilToolTip(app, row, ConflictRowTip);
        Assert.False(app.IsResolverShown, "Resting the pointer on notes.txt opened the resolver.");

        // Step 5: a real double-click on notes.txt opens the resolver once and changes nothing.
        app.MoveToNeutral();
        var mark = app.LogMark();
        app.DoubleClickWithMouse(app.StageFileRow("UnstagedFileList", ResolverFile));
        app.WaitForResolver(ResolverFile);
        app.WaitForResultCount(TwoConflictsLeft);
        AssertResolverOpened(app, ResolverFile, BothModified, FileOneOfOne, OursMain, TheirsMerged);
        AssertResolverToolbar(app, ConflictOneOfTwo, mergeTool: false);
        WaitForResult(app, NotesMerge, TwoConflictsLeft, ConflictOneOfTwo);
        AssertBanner(app, MergeBanner, ResolveFirstOneFile, skip: false, AbortMerge, besideText: true);
        AssertBannerOverResolver(app);
        Assert.DoesNotContain(app.LogLinesSince(mark), line => line.Contains($"] {name}: {ResolveOperation}: ", StringComparison.Ordinal));
        Assert.Equal(["UU notes.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Compare(app, "c10e-resolver-open");

        // Step 6: Use ours on block 1 (the upper bar in view); a choice does not move on.
        app.ClickBarButton("ConflictUseOurs", 0);
        WaitForResult(app, NotesMergeR1, OneConflictLeft, ConflictOneOfTwo);
        Assert.Equal([UsedInResult, NotUsed], app.BarStateTexts().Take(2));
        Assert.Equal(NotesMerge, WorkingFile(repo, ResolverFile));

        // Step 7: the next-conflict button: block 2 reached, its bars in view with their buttons.
        app.ClickResolverButton("ResolverNextConflict");
        app.WaitForText("ResolverConflictPosition", ConflictTwoOfTwo);
        Assert.Single(app.ShownElements("ConflictUseOurs"));
        Assert.Single(app.ShownElements("ConflictUseTheirs"));

        // Step 8: Use theirs in block 2's theirs bar (block 1's bars show their texts, no buttons).
        app.ClickWithMouse(Assert.Single(app.ShownElements("ConflictUseTheirs")));
        WaitForResult(app, NotesMergeR2, NoConflictsLeft, NoConflictsLeft);
        AssertNoBarButtons(app);
        Compare(app, "c10h-block2-theirs");

        // Step 9: the platform's undo on the real keyboard: one step back to R1, block 2 open again.
        app.PressUndo();
        WaitForResult(app, NotesMergeR1, OneConflictLeft, ConflictTwoOfTwo);
        Assert.Single(app.ShownElements("ConflictUseOurs"));
        Compare(app, "c10i-undone");

        // Step 10: Use both from the ours pane on block 2.
        app.ClickWithMouse(Assert.Single(app.ShownElements("ConflictUseBothOursFirst")));
        WaitForResult(app, NotesMergeR3, NoConflictsLeft, NoConflictsLeft);
        AssertNoBarButtons(app);
        Compare(app, "c10j-block2-both");

        // Step 11: Mark resolved asks nothing, writes R3 and stages it; the resolver closes and the
        // keyboard focus goes to the banner's Continue.
        var resolve = ExpectOperation(app, name, ResolveOperation);
        app.ClickResolverButton("ResolverMarkResolved");
        resolve.Wait(OperationOutcome.Succeeded, ResolvedStatus);
        app.WaitForResolverClosed(OperationTimeout);
        AssertNoDialog(app);
        WaitForStatus(app, ResolvedStatus);
        AssertBanner(app, MergeResolvedBanner, continueReason: null, skip: false, AbortMerge, besideText: false);
        app.WaitForStageTitles(UnstagedNone, StagedOne);
        WaitForContinueFocus(app);
        Assert.Equal(["M  notes.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.True(InGitDir(repo, "MERGE_HEAD"), "MERGE_HEAD is gone.");
        Assert.Equal(NotesMergeR3, WorkingFile(repo, ResolverFile));
        Assert.Empty(await GitLinesAsync(repo, "diff", "--cached", "--check", "--", ResolverFile));
        Compare(app, "c10k-resolved");

        // Step 12: Continue commits the merge with the stage panel's message; then row 0.
        var resume = ExpectOperation(app, name, ContinueMergeOperation);
        app.ClickBannerContinue();
        resume.Wait(OperationOutcome.Succeeded, MergeCommittedStatus);
        app.WaitFor(() => app.BannerBounds() is null, "the banner to go", OperationTimeout);
        AssertNoDialog(app);
        WaitForStatus(app, MergeCommittedStatus);
        var merged = RowPoint(app, 0, ConflictsLanes);
        app.ClickAt(merged.X, merged.Y);
        WaitForDetails(app, MergeSummary, StopSha.Merged);
        Compare(app, "c10l-merged");

        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(StopSha.Merged, await repo.HeadAsync());
        Assert.Equal(MergeSummary + "\n", await RawMessageAsync(repo, "HEAD"));
        await AssertCommitAsync(repo, "HEAD", AppCommitTime, AppCommitTime, StopSha.OursEdits, StopSha.TheirsEdits);
        Assert.Equal(NotesMergeR3, (await repo.GitAsync("show", "HEAD:" + ResolverFile)).StandardOutput);
        Assert.False(InGitDir(repo, "MERGE_HEAD"), "MERGE_HEAD is still there.");
        Assert.False(InGitDir(repo, "MERGE_MSG"), "MERGE_MSG is still there.");
        Assert.Equal(["## main"], await GitLinesAsync(repo, "status", "--short", "--branch"));
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal(0, app.Close());
    }

    /// <summary>
    /// Check C11, steps 1 to 3, with the real mouse on one launch on a "conflicts" copy with
    /// feature/rebase checked out as a terminal would: main's row in the left panel right-clicked
    /// and "Rebase feature/rebase onto main" clicked by name; the first stop's banner with Continue
    /// disabled, Skip and Abort rebase over the real graph area (<c>c11a-first-stop</c>); the real
    /// pointer resting on the banner's text until its tooltip shows inside the window, below the
    /// pointer (<c>c11b-banner-tooltip</c>). Steps 4 to 11 are the scripted run's alone.
    /// </summary>
    [Fact]
    public async Task Check_C11_rebase_first_stop_and_its_banner()
    {
        using var repo = await Scenarios.ConflictsAsync();
        await repo.CheckoutAsync("feature/rebase");
        using var app = Launch(repo);
        var name = NameOf(repo);

        // Step 1: nothing in progress.
        Assert.Null(app.BannerBounds());

        // Step 2: Rebase feature/rebase onto main from main's menu in the left panel.
        app.RightClickWithMouse(app.RefRow("Local branches", "main"));
        app.WaitForMenu();
        Assert.Contains("Rebase feature/rebase onto main", app.MenuItemNames());
        var rebase = ExpectOperation(app, name, RebaseOperation);
        app.ClickMenuItem("Rebase feature/rebase onto main");
        rebase.Wait(OperationOutcome.Conflicts, RebaseStoppedStatus);
        AssertBanner(app, RebaseFirstBanner, ResolveFirstOneFile, skip: true, AbortRebase, besideText: false);
        AssertBannerOverGraph(app);
        AssertNoDialog(app);
        WaitForStatus(app, RebaseStoppedStatus);
        var backup = Assert.Single(await BackupsAsync(repo));
        Assert.Equal(("rebase", false, "feature/rebase"), (backup.Ref.Kind, backup.Ref.IsChanges, backup.Ref.Name));
        Assert.Equal(StopSha.SecondChange, backup.Sha);
        Assert.Equal(["UU notes.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(StopSha.OursEdits, await repo.HeadAsync());
        Assert.True(InGitDir(repo, "rebase-merge"), "No rebase is in progress.");
        Assert.Equal(StopSha.FirstChange, File.ReadAllText(Path.Combine(repo.Path, ".git", "rebase-merge", "stopped-sha")).Trim());
        Assert.Equal(StopSha.SecondChange, await repo.RefTargetAsync("refs/heads/feature/rebase"));
        Compare(app, "c11a-first-stop");

        // Step 3: the banner's tooltip under the real pointer, inside the window, below the pointer.
        RestUntilToolTip(app, app.Find("BannerText"), RebaseBannerTip, below: true);
        Compare(app, "c11b-banner-tooltip", keepPointer: true);

        app.MoveToNeutral();
        Assert.Equal(0, app.Close());
    }

    /// <summary>
    /// Clicks the working-changes row (display row 0, below the banner while one shows: its bottom,
    /// the column headers and half a row) and waits for the stage panel with these titles.
    /// </summary>
    private static void ClickWorkingRow(RealApp app, string unstagedTitle, string stagedTitle)
    {
        var working = RowPoint(app, 0, ConflictsLanes);
        if (app.BannerBounds() is { } banner)
        {
            Assert.True(
                working.Y > banner.Bottom + (float)WindowLayout.ColumnHeaderHeight,
                $"The working-changes row at {working} is not below the banner ({banner}) and the column headers.");
        }

        app.ClickAt(working.X, working.Y);
        app.WaitForStageTitles(unstagedTitle, stagedTitle, OperationTimeout);
    }

    /// <summary>The banner above the column headers, as wide as the graph area (440) while the graph shows (the part's conventions).</summary>
    private static void AssertBannerOverGraph(RealApp app)
    {
        var banner = app.BannerBounds() ?? throw new InvalidOperationException("No banner is shown.");
        var width = 1100 - (float)WindowLayout.LeftPanelWidth - (float)WindowLayout.RightPanelWidth;
        Assert.True(
            Math.Abs(banner.Left - (float)WindowLayout.LeftPanelWidth) <= 1 && Math.Abs(banner.Width - width) <= 1 && Math.Abs(banner.Top - (float)WindowLayout.GraphAreaTop) <= 1,
            $"The banner is at {banner}; the report puts it at x {WindowLayout.LeftPanelWidth}, y {WindowLayout.GraphAreaTop}, {width} wide.");
    }

    /// <summary>The banner across the resolver's width (840), right above it (11.2).</summary>
    private static void AssertBannerOverResolver(RealApp app)
    {
        var banner = app.BannerBounds() ?? throw new InvalidOperationException("No banner is shown.");
        var resolver = app.BoundsOf(app.Resolver());
        Assert.True(
            Math.Abs(banner.Left - ResolverPlace.Left) <= 1 && Math.Abs(banner.Width - ResolverPlace.Width) <= 1 && Math.Abs(banner.Bottom - resolver.Top) <= 1,
            $"The banner at {banner} is not across the resolver at {resolver}.");
    }

    /// <summary>
    /// The resolver as it opens on a text file (11.2): over the graph and right-panel columns (x 260
    /// to 1100), the right panel hidden; its header (<c>U</c>, the file's name, no folder, the kind,
    /// the file's position); the panes' titles and <c>Result</c>.
    /// </summary>
    private static void AssertResolverOpened(RealApp app, string file, string kind, string filePosition, string oursTitle, string theirsTitle)
    {
        AssertResolverHeader(app, file, kind, filePosition);
        Assert.Equal(oursTitle, app.ResolverText("ConflictOursTitle"));
        Assert.Equal(theirsTitle, app.ResolverText("ConflictTheirsTitle"));
        Assert.Equal(ResultTitle, app.ResolverText("ConflictResultTitle"));
    }

    /// <summary>The resolver's place and its header (11.2): <c>U</c>, the file's name, no folder (every file of part C is at the working tree's top), the kind, the file's position.</summary>
    private static void AssertResolverHeader(RealApp app, string file, string kind, string filePosition)
    {
        var resolver = app.BoundsOf(app.Resolver());
        Assert.True(
            Math.Abs(resolver.Left - ResolverPlace.Left) <= 1 && Math.Abs(resolver.Width - ResolverPlace.Width) <= 1,
            $"The resolver is at {resolver}; the report puts it at x {ResolverPlace.Left}, {ResolverPlace.Width} wide.");
        Assert.Empty(app.ShownElements("StagePanel"));
        Assert.Equal("U", app.ResolverText("ResolverFileStatus"));
        Assert.Equal(file, app.ResolverText("ResolverFileName"));
        Assert.All(app.NamesOnScreen("ResolverFolder"), folder => Assert.True(string.IsNullOrEmpty(folder), $"ResolverFolder reads \"{folder}\" for a file at the working tree's top."));
        Assert.Equal(kind, app.ResolverText("ResolverKind"));
        Assert.Equal(filePosition, app.ResolverText("ResolverFilePosition"));
    }

    /// <summary>
    /// The resolver's toolbar on a text file (11.2): the conflict position, Take ours and Take
    /// theirs enabled, Merge tool… enabled or disabled with its reason (only C16's copy configures a
    /// tool), Mark resolved enabled.
    /// </summary>
    private static void AssertResolverToolbar(RealApp app, string position, bool mergeTool)
    {
        app.WaitForText("ResolverConflictPosition", position);
        Assert.Equal(TakeOurs, app.Find("ResolverTakeOurs").Name);
        Assert.True(app.IsResolverButtonEnabled("ResolverTakeOurs"), "Take ours is disabled.");
        Assert.Equal(TakeTheirs, app.Find("ResolverTakeTheirs").Name);
        Assert.True(app.IsResolverButtonEnabled("ResolverTakeTheirs"), "Take theirs is disabled.");
        var tool = app.Find("ResolverMergeTool");
        Assert.Equal(MergeToolButton, tool.Name);
        Assert.Equal(mergeTool, tool.IsEnabled);
        if (!mergeTool)
        {
            Assert.Equal(SetMergeToolReason, tool.HelpText);
        }

        Assert.Equal(MarkResolved, app.Find("ResolverMarkResolved").Name);
        Assert.True(app.IsResolverButtonEnabled("ResolverMarkResolved"), "Mark resolved is disabled.");
    }

    /// <summary>
    /// Waits until the resolver has parsed the result (its count reads <paramref name="count"/>,
    /// 14.3), then until the result's text (its Value pattern) is <paramref name="text"/> and the
    /// toolbar's position reads <paramref name="position"/>.
    /// </summary>
    private static void WaitForResult(RealApp app, string text, string count, string position)
    {
        app.WaitForResultCount(count);
        try
        {
            app.WaitFor(() => app.ResultText() == text, "the result's text", RealApp.DefaultWaitTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} Expected:\n{text}\nActual:\n{app.ResultText()}", ex);
        }

        app.WaitForText("ResolverConflictPosition", position);
    }

    /// <summary>No block bar in view shows a button: every block in view is resolved (its bars show <c>Used in the result</c> or <c>Not used</c>).</summary>
    private static void AssertNoBarButtons(RealApp app)
    {
        foreach (var id in new[] { "ConflictUseOurs", "ConflictUseBothOursFirst", "ConflictUseTheirs", "ConflictUseBothTheirsFirst" })
        {
            Assert.True(app.ShownElements(id).Count == 0, $"A bar in view still shows {id}.");
        }
    }

    /// <summary>Waits until the banner's Continue has the keyboard focus (11.1: after Mark resolved closes the resolver).</summary>
    private static void WaitForContinueFocus(RealApp app) =>
        app.WaitFor(() => RealApp.HasKeyboardFocus(app.BannerContinueButton()), "the keyboard focus on the banner's Continue", RealApp.DefaultWaitTimeout);
}
