// Check C16 of phase 4's walk-through (docs/test-reports/phase-4.md, the row C16 of "Checks"):
// the external merge tool, with the fake tool of ConflictsStoppedAsync(mergeTool: true), on the
// merge of feature/theirs stopped in a terminal. What checks C14 to C17 share is in
// Phase4Checks.C.EditingSupport.cs; the expected values in Phase4Expectations.C2.cs (PartC2).
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using Tint = VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC2.Tint;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksC
{
    /// <summary>
    /// Check C16: the conflicted file's menu, the question for a modified result, the tool
    /// resolving the file, the resolver closing, the status; dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_C16_merge_tool()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge, mergeTool: true);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var start = app.LogMark();
        var unmergedAtStop = await C14To17.UnmergedAsync(repo);

        // When the app has started: as in C14, with the fake merge tool configured.
        AssertGraph(app, PartC2.GcM, PartC2.ConflictsLanes, PartC2.WorkingRow(PartC2.WorkingConflicts(1)), showsDate: false);
        AssertBanner(app, PartC2.MergeTheirsBanner, PartC2.AbortMerge, BannerButton.ContinueDisabled(PartC2.ResolveFirst(1)), oneLine: false, placement: BannerButtonPlacement.OwnRow);
        Assert.Equal(PartC2.MergeStopStatus, await C14To17.PorcelainAsync(repo));
        Assert.Equal(C14To17.Bytes(PartC2.Fm), C14To17.WorkingFile(repo, "notes.txt"));
        Assert.Equal("fake", (await repo.GitAsync("config", "--get", "merge.tool")).StandardOutput.Trim());
        Assert.Equal("sh \"$(git rev-parse --git-common-dir)/vc-fake-merge-tool.sh\" \"$MERGED\"", (await repo.GitAsync("config", "--get", "mergetool.fake.cmd")).StandardOutput.Trim());
        Assert.Equal("true", (await repo.GitAsync("config", "--get", "mergetool.fake.trustExitCode")).StandardOutput.Trim());

        // Step 1: the working-changes row; a right-click on notes.txt: the conflicted file's menu.
        app.Click(app.WorkingRowPoint());
        await C14To17.WaitForStagePanelAsync(app, PartC2.UnstagedTitle(1, 1), PartC2.StagedTitle(0));
        await app.WaitForIdleAsync();
        var row = await app.FileRowAsync("UnstagedFileList", "notes.txt");
        app.RightClick(row);
        var menu = app.Menu();
        Shot(app, "c16a-file-menu");
        AssertMenu(menu, [.. PartC2.NotesFileMenu]);
        AssertHighlighted(menu, "Resolve conflict");
        Assert.True(app.MenuItemControl("Resolve conflict").IsFocused, $"\"Resolve conflict\" does not have the keyboard focus: {C14To17.Describe(app.Window.FocusManager?.GetFocusedElement())} has it.");
        Assert.Equal(PartC2.NotesFileMenuHeight, menu.Bounds.Height, WindowLayout.MenuHeightTolerance);
        AssertMenuShape(app, menu);
        Assert.False(((ChangedFileRow)row.DataContext!).IsSelected, "notes.txt's row is selected.");
        Assert.Null(app.ShownResolver);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        Assert.Null(app.ShownResolver);

        // Step 2: notes.txt; Use ours in block 1's ours bar; Merge tool… asks first.
        app.Click(await app.FileRowAsync("UnstagedFileList", "notes.txt"));
        await app.WaitForResolverAsync("notes.txt");
        await app.WaitForIdleAsync();
        C14To17.AssertResolverPlace(app);
        AssertBanner(app, PartC2.MergeTheirsBanner, PartC2.AbortMerge, BannerButton.ContinueDisabled(PartC2.ResolveFirst(1)), oneLine: null, placement: BannerButtonPlacement.BesideText);
        var resolver = app.Resolver();
        C14To17.AssertHeader(app, resolver, "notes.txt", PartC2.BothModified, PartC2.FilePosition(1, 1));
        C14To17.AssertTextToolbar(app, resolver, PartC2.ConflictPosition(1, 2), mergeTool: true);
        C14To17.AssertTitles(resolver, PartC2.OursMain, PartC2.TheirsTheirs, 2);
        C14To17.AssertPanes(resolver, PartC2.PmOurs(), PartC2.PmTheirs());
        C14To17.AssertResult(resolver, PartC2.Fm, PartC2.FmTints);
        C14To17.AssertFocusAndCaret(app, 2, 1);

        app.ClickBarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseOurs);
        C16Steps.AssertOursOnBlock1(app);
        app.ClickResolverButton("ResolverMergeTool");
        await AssertConfirmationAsync(app, PartC2.DiscardTitle("notes.txt"), PartC2.MergeToolQuestion, string.Empty, PartC2.Discard, destructive: true, cancelText: PartC2.KeepEditing);
        C14To17.AssertConfirmDanger(app);
        Shot(app, "c16b-tool-question");

        // Step 3: Keep editing: the tool does not start.
        C14To17.ClickDialogButton(app, "CancelButton");
        app.Settle();
        await app.WaitForIdleAsync();
        await AssertNoDialogAsync(app);
        Assert.False(C14To17.Logged(app, repo, PartC2.MergeToolTitle, start), "A Merge tool for notes.txt operation was logged.");
        Assert.Equal(C14To17.Bytes(PartC2.Fm), C14To17.WorkingFile(repo, "notes.txt"));
        Assert.Equal(PartC2.MergeStopStatus, await C14To17.PorcelainAsync(repo));
        Assert.NotNull(app.ShownResolver);
        Assert.Equal(PartC2.OursOnBlock1, app.ResultText);
        Assert.Equal(PartC2.ConflictsLeft(1), app.Resolver().ResultCount);

        // Step 4: Merge tool… again, Discard: the tool resolves the file, the resolver closes.
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        app.ClickResolverButton("ResolverMergeTool");
        await AssertConfirmationAsync(app, PartC2.DiscardTitle("notes.txt"), PartC2.MergeToolQuestion, string.Empty, PartC2.Discard, destructive: true, cancelText: PartC2.KeepEditing);
        using var statusTexts = app.RecordStatusTexts();
        C14To17.ClickDialogButton(app, "ConfirmButton");
        using (HangWatchdogAttribute.WaitingForSlowProgram("git mergetool to run the fake tool"))
        {
            await WaitForOperationAsync(app, repo, PartC2.MergeToolTitle, mark, Scenarios.MergeToolTimeout);
        }

        Assert.Contains(PartC2.MergeToolRunning, statusTexts.Texts);
        Assert.Equal(PartC2.ResolvedInFake, statusTexts.Texts[^1]);
        await app.WaitForResolverClosedAsync();
        await AssertNoDialogAsync(app);
        await C14To17.WaitForStagePanelAsync(app, PartC2.UnstagedTitle(0), PartC2.StagedTitle(1));
        await app.WaitForAsync(() => app.BannerContinueButton.IsFocused, "the keyboard focus on the banner's Continue");
        await app.WaitForIdleAsync();
        Shot(app, "c16c-tool-resolved");
        Assert.Equal(0, AssertCalled(app, calls, PartC2.MergeToolCommand).ExitCode);
        AssertStatus(app, PartC2.ResolvedInFake);
        await app.WaitForLogLineAsync(AppLogFile.OperationEnded(NameOf(repo), PartC2.MergeToolTitle, message: PartC2.ResolvedInFake), mark: mark);
        AssertGraph(app, PartC2.GcM, PartC2.ConflictsLanes, PartC2.WorkingRow(PartC2.Working), showsDate: false);
        AssertBanner(app, PartC2.MergeTheirsResolvedBanner, PartC2.AbortMerge, BannerButton.Continue, oneLine: false, placement: BannerButtonPlacement.OwnRow);
        C14To17.AssertEmptyLists(app, unstagedEmpty: true, stagedEmpty: false);
        Assert.Equal(["M notes.txt"], C14To17.FileRows(app, "StagedFileList"));
        await C14To17.AssertMessageAsync(app, PartC2.MergeTheirsSummary, string.Empty);
        C14To17.AssertCommitButton(app, PartC2.Commit1File, enabled: true);
        Assert.Equal(C14To17.Bytes(PartC2.FakeToolResult), C14To17.WorkingFile(repo, "notes.txt"));
        Assert.StartsWith(PartC2.FakeToolBlob, await C14To17.StagedBlobAsync(repo, "notes.txt"), StringComparison.Ordinal);
        Assert.Equal(["M  notes.txt"], await C14To17.PorcelainAsync(repo));
        Assert.Empty(await C14To17.UnmergedAsync(repo));
        Assert.StartsWith(PartC2.TheirsSha, C14To17.GitFile(repo, "MERGE_HEAD"), StringComparison.Ordinal);
        Assert.StartsWith(PartC2.MainSha, await repo.HeadAsync(), StringComparison.Ordinal);
        Assert.NotEmpty(unmergedAtStop);
        AssertNoClippedText(app);
    }

    /// <summary>What check C16 asserts after its choice.</summary>
    private static class C16Steps
    {
        /// <summary>C16 step 2 after <c>Use ours</c> on block 1: 16 lines, block 2 left, the choice's bars and tints.</summary>
        public static void AssertOursOnBlock1(ShellDriver app)
        {
            var resolver = app.Resolver();
            C14To17.AssertResult(resolver, PartC2.OursOnBlock1, PartC2.TypedTints);
            Assert.Equal((PartC2.ConflictsLeft(1), PartC2.ConflictPosition(1, 2)), (resolver.ResultCount, resolver.ConflictPosition));
            C14To17.AssertBars(app, 0, PartC2.Used, PartC2.NotUsed);
            C14To17.AssertBars(app, 1, null, null);
            C14To17.AssertPanes(resolver, PartC2.PmOurs(Tint.Used, Tint.Ours), PartC2.PmTheirs(Tint.None, Tint.Theirs));
            C14To17.AssertFocusAndCaret(app, 2, 1);
            Assert.True(resolver.IsModified, "The result is not modified.");
        }
    }
}
