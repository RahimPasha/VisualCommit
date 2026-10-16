// Checks F12 and F13 of part F (docs/test-reports/phase-5.md): the comparison in the right panel
// (design 6, D162, D177): two selected commits compared two-dot at once, a file's diff from the
// list, Search closing it, Swap, the list's file menu, a link, a third commit; and branches
// compared three-dot from a branch's menu and from a drop, with "All differences", a link and
// closing, the stage panel's draft kept meanwhile.
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksF
{
    /// <summary>
    /// Check F12: two selected commits compared two-dot (Rename the parser to reader as From, Read
    /// whole lines as To, FD12), a file's diff, Search closing it, Swap, the file menu, the To link,
    /// a third commit selected and taken out again, a single selection ending it; dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_F12_compare_two_commits()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var refs = await RefsAsync(repo);
        var config = ConfigFile(repo);
        AssertGraph(app, PartF.H0, PartF.HistoryLanes, PartF.Working, showsDate: true);
        AssertNothingSelected(app);

        // Step 1: Rename the parser to reader, then Read whole lines with the command modifier.
        var calls = app.GitCalls().Count;
        app.Click(app.GraphRowPoint(PartF.RenameParserRow));
        await WaitForDetailsAsync(app, PartF.RenameParserDetails.Subject);
        app.Click(app.GraphRowPoint(PartF.ReadWholeLinesRow), app.CommandModifier);
        await app.WaitForAsync(() => app.Compare() is not null, "the comparison of the two commits");
        await app.WaitForCompareAsync();
        var shot = Shot(app, PartF.F12TwoCommits);
        AssertRowsSelected(app, PartF.RenameParserRow, PartF.ReadWholeLinesRow);
        AssertSelectionBackground(app, shot, PartF.RenameParserRow);
        AssertSelectionBackground(app, shot, PartF.ReadWholeLinesRow);
        AssertComparison(app, PartF.F12From, PartF.F12To, counts: null, branches: false, allDifferences: false, PartF.F12Files);
        AssertCompareHeader(app);
        Assert.Null(Shown(app, "MultiSelectionView"));
        var revParse = AssertCall(app, calls, PartF.RevParseCall(PartF.RenameParser, PartF.ReadWholeLines));
        Assert.Equal([PartF.RenameParser, PartF.ReadWholeLines, "--"], OutputLines(revParse));
        AssertCall(app, calls, PartF.DiffTreeCall(PartF.RenameParser, PartF.ReadWholeLines));
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git merge-base", StringComparison.Ordinal) || call.StartsWith("git rev-list --left-right", StringComparison.Ordinal));
        AssertNoClippedText(app);

        // Step 2: Reader.cs's diff from the list, From's tree against To's.
        app.ClickCompareFile("src/Reader.cs");
        await WaitForTextDiffAsync(app, "src/Reader.cs");
        Shot(app, PartF.F12FileDiff);
        AssertCompareDiff(app, "M", "Reader.cs", "src", PartF.ReaderSideLabel, PartF.ReaderDiffRows);
        Assert.Equal("src/Reader.cs", SelectedCompareFile(app));
        Assert.True(app.CompareFileRow("src/Reader.cs").Classes.Contains("selected"), "The row of Reader.cs has no selection background.");
        AssertRowsSelected(app, PartF.RenameParserRow, PartF.ReadWholeLinesRow);
        AssertComparison(app, PartF.F12From, PartF.F12To, counts: null, branches: false, allDifferences: false, PartF.F12Files);

        // Step 3: Search closes the diff (it holds no work) and opens the bar; Esc closes the bar.
        app.Click(app.FindByAutomationId("SearchButton"));
        await app.WaitForAsync(() => app.SearchBar() is not null && !Repo(app).IsDiffOpen, "the diff closed and the search bar open");
        Assert.Null(Shown(app, "DiffView"));
        AssertRowsSelected(app, PartF.RenameParserRow, PartF.ReadWholeLinesRow);
        AssertComparison(app, PartF.F12From, PartF.F12To, counts: null, branches: false, allDifferences: false, PartF.F12Files);
        Assert.Null(SelectedCompareFile(app));
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.SearchBar() is null, "the search bar to close");

        // Step 4: Swap: From and To change places, each change reversed.
        calls = app.GitCalls().Count;
        app.Click(app.FindByAutomationId("CompareSwap"));
        await app.WaitForAsync(() => app.Compare() is { From: PartF.F12SwappedFrom }, "the comparison swapped");
        await app.WaitForCompareAsync();
        Shot(app, PartF.F12Swapped);
        AssertComparison(app, PartF.F12SwappedFrom, PartF.F12SwappedTo, counts: null, branches: false, allDifferences: false, PartF.F12Files);
        revParse = AssertCall(app, calls, PartF.RevParseCall(PartF.ReadWholeLines, PartF.RenameParser));
        Assert.Equal([PartF.ReadWholeLines, PartF.RenameParser, "--"], OutputLines(revParse));
        AssertCall(app, calls, PartF.DiffTreeCall(PartF.ReadWholeLines, PartF.RenameParser));
        AssertRowsSelected(app, PartF.RenameParserRow, PartF.ReadWholeLinesRow);

        // The comparison's file menu on Reader.cs (FD21): its menu only.
        var row = app.CompareFileRow("src/Reader.cs");
        app.RightClick(row);
        var menu = app.Menu();
        Shot(app, PartF.F12CompareFileMenu);
        AssertMenuAsReported(app, menu, PartF.CompareFileMenu, "File history", PartF.CompareFileMenuHeight);
        Assert.Null(SelectedCompareFile(app));
        Assert.False(Repo(app).IsDiffOpen, "The right-click opened a diff.");
        await CloseMenuAsync(app);
        AssertComparison(app, PartF.F12SwappedFrom, PartF.F12SwappedTo, counts: null, branches: false, allDifferences: false, PartF.F12Files);
        Assert.False(Repo(app).IsDiffOpen, "Esc opened a diff.");

        // Step 5: the To link reveals row 7 without selecting it alone; it was in view already.
        app.Click(app.FindByAutomationId("CompareTo"));
        app.Settle();
        Assert.Equal(0, Graph(app).ScrollOffset);
        AssertRowInView(app, PartF.RenameParserRow);
        AssertRowsSelected(app, PartF.RenameParserRow, PartF.ReadWholeLinesRow);
        AssertComparison(app, PartF.F12SwappedFrom, PartF.F12SwappedTo, counts: null, branches: false, allDifferences: false, PartF.F12Files);

        // Step 6: a third commit: phase 3's list; taken out again: a new comparison in the graph's order (FD9).
        app.Click(app.GraphRowPoint(PartF.MergeReadmeRow), app.CommandModifier);
        await AssertThreeSelectedAsync(app);
        AssertRowsSelected(app, PartF.ReadWholeLinesRow, PartF.MergeReadmeRow, PartF.RenameParserRow);
        app.Click(app.GraphRowPoint(PartF.MergeReadmeRow), app.CommandModifier);
        await app.WaitForAsync(() => app.Compare() is not null, "the comparison back");
        await app.WaitForCompareAsync();
        AssertRowsSelected(app, PartF.RenameParserRow, PartF.ReadWholeLinesRow);
        AssertComparison(app, PartF.F12From, PartF.F12To, counts: null, branches: false, allDifferences: false, PartF.F12Files);

        // Step 7: row 3 alone: the comparison ends; its details.
        app.Click(app.GraphRowPoint(PartF.MergeReadmeRow));
        await AssertDetailsAsync(app, PartF.MergeReadmeDetails);
        AssertRowSelected(app, PartF.MergeReadmeRow);
        Assert.Null(app.Compare());
        await AssertGitUnchangedAsync(repo, PartF.HistoryStatus, refs, config);
        await AssertNoDialogAsync(app);
    }

    /// <summary>
    /// Check F13: branches compared three-dot from Local branches › feature › compare's menu while
    /// the working-changes row is selected, with "All differences", the To link and closing (the
    /// stage panel's draft kept), then from a drop of main onto feature/compare (FD11); dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_F13_compare_branches()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var refs = await RefsAsync(repo);
        var config = ConfigFile(repo);

        // Step 1: the working-changes row, and a draft summary.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app);
        app.TypeInto("CommitSummary", PartF.DraftSummary);
        app.Settle();
        AssertStagePanel(app);

        // Step 2: the folder feature opened; compare's menu, which selects nothing (D114).
        await OpenFolderAsync(app, "Local branches", "feature");
        var (menu, _) = await RightClickRefAsync(app, "Local branches", "feature", "compare");
        Shot(app, PartF.F13BranchMenu);
        AssertMenuAsReported(app, menu, PartF.FeatureCompareMenu, "Check out feature/compare", PartF.BranchMenuHeight);
        Assert.True(Repo(app).IsWorkingRowSelected, "The working-changes row is no longer selected.");
        Assert.True(app.FindByAutomationId("StagePanel").IsEffectivelyVisible, "The stage panel is hidden.");
        AssertNoRefHighlighted(app);

        // Step 3: "Compare feature/compare with main": three-dot, From main, To feature/compare.
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(PartF.CompareFeatureWithMain);
        await app.WaitForAsync(() => app.Compare() is not null, "the comparison of the branches");
        await app.WaitForCompareAsync();
        Shot(app, PartF.F13ThreeDot);
        Assert.False(Repo(app).IsWorkingRowSelected, "The working-changes row is still selected.");
        Assert.False(Graph(app).IsWorkingRowSelected, "The graph draws the working-changes row selected.");
        Assert.True(Repo(app).Selection.IsEmpty, "A commit is selected.");
        AssertComparison(app, PartF.F13From, PartF.F13To, PartF.F13Counts, branches: true, allDifferences: false, PartF.F13ThreeDotFiles);
        AssertCompareHeader(app);
        var revParse = AssertCall(app, calls, PartF.RevParseCall(PartF.MainRef, PartF.FeatureCompareRef));
        Assert.Equal([PartF.ReadWholeLines, PartF.CompareWholeLines, "--"], OutputLines(revParse));
        var mergeBase = AssertCall(app, calls, PartF.MergeBaseCall(PartF.ReadWholeLines, PartF.CompareWholeLines));
        Assert.Equal([PartF.AddTracing], OutputLines(mergeBase));
        AssertCall(app, calls, PartF.DiffTreeCall(PartF.AddTracing, PartF.CompareWholeLines));
        var revList = AssertCall(app, calls, PartF.RevListCall(PartF.ReadWholeLines, PartF.CompareWholeLines));
        Assert.Equal([PartF.F13CountsOutput], OutputLines(revList));
        AssertNoClippedText(app);

        // Step 4: README.md's diff from the merge base; Esc closes it.
        app.ClickCompareFile("README.md");
        await WaitForTextDiffAsync(app, "README.md");
        Shot(app, PartF.F13MergeBaseDiff);
        AssertCompareDiff(app, "M", "README.md", string.Empty, PartF.ReadmeSideLabel, PartF.ReadmeDiffRows);
        Assert.Equal("README.md", SelectedCompareFile(app));
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !Repo(app).IsDiffOpen, "the diff to close");
        Assert.True(Graph(app).IsEffectivelyVisible, "The graph does not show.");
        Assert.Null(SelectedCompareFile(app));
        AssertComparison(app, PartF.F13From, PartF.F13To, PartF.F13Counts, branches: true, allDifferences: false, PartF.F13ThreeDotFiles);

        // Step 5: "All differences": the two tips' trees; the counts unchanged.
        calls = app.GitCalls().Count;
        app.Click(app.FindByAutomationId("CompareAllDifferences"));
        await app.WaitForAsync(() => app.Compare() is { AllDifferencesChecked: true, IsLoading: false, Files.Count: 5 }, "every difference listed");
        await app.WaitForCompareAsync();
        Shot(app, PartF.F13AllDifferences);
        AssertComparison(app, PartF.F13From, PartF.F13To, PartF.F13Counts, branches: true, allDifferences: true, PartF.F13AllFiles);
        AssertCall(app, calls, PartF.DiffTreeCall(PartF.ReadWholeLines, PartF.CompareWholeLines));
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git merge-base", StringComparison.Ordinal));

        // Step 6: the To link reveals row 6, in view already, selecting nothing.
        app.Click(app.FindByAutomationId("CompareTo"));
        app.Settle();
        Assert.Equal(0, Graph(app).ScrollOffset);
        AssertRowInView(app, PartF.CompareWholeLinesRow);
        Assert.True(Repo(app).Selection.IsEmpty, "The link selected a commit.");
        AssertComparison(app, PartF.F13From, PartF.F13To, PartF.F13Counts, branches: true, allDifferences: true, PartF.F13AllFiles);

        // Step 7: closing shows the placeholder, the focus on the graph; the working-changes row shows the draft.
        app.Click(app.FindByAutomationId("CompareClose"));
        await app.WaitForAsync(() => app.Compare() is null, "the comparison to close");
        await app.WaitForAsync(() => app.Window.FocusManager?.GetFocusedElement() is CommitGraphControl, "the focus on the graph");
        AssertNothingSelected(app);
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app);
        Shot(app, PartF.F13DraftKept);
        AssertStagePanel(app);

        // Step 8: main dropped onto feature/compare: the press selects row 0; the drop's facts; its menu.
        calls = app.GitCalls().Count;
        var from = app.GraphLabelBounds(PartF.ReadWholeLinesRow, "main").Center;
        var to = app.GraphLabelBounds(PartF.CompareWholeLinesRow, "feature/comp…").Center;
        app.PressMouse(from);
        app.MoveMouseHeld(to, steps: PartF.DragSteps);
        Assert.True(app.BranchDrag.IsDragging, "The moves did not start a drag.");
        app.ReleaseMouse(to);
        menu = app.Menu();
        await WaitForDetailsAsync(app, PartF.ReadWholeLinesDetails.Subject);
        Shot(app, PartF.F13DropMenu);
        AssertRowSelected(app, PartF.ReadWholeLinesRow);
        var ancestor = AssertCall(app, calls, PartF.DropAncestorCall, exitCode: 1);
        Assert.Empty(OutputLines(ancestor));
        AssertMenuAsReported(app, menu, PartF.DropMenu, PartF.DropHighlighted, PartF.DropMenuHeight);

        // Step 9: "Compare main with feature/compare": the other way round.
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartF.CompareMainWithFeature);
        await app.WaitForAsync(() => app.Compare() is not null, "the drop's comparison");
        await app.WaitForCompareAsync();
        Shot(app, PartF.F13DropCompare);
        AssertComparison(app, PartF.F13DropFrom, PartF.F13DropTo, PartF.F13DropCounts, branches: true, allDifferences: false, PartF.F13DropFiles);
        revParse = AssertCall(app, calls, PartF.RevParseCall(PartF.FeatureCompareRef, PartF.MainRef));
        Assert.Equal([PartF.CompareWholeLines, PartF.ReadWholeLines, "--"], OutputLines(revParse));
        mergeBase = AssertCall(app, calls, PartF.MergeBaseCall(PartF.CompareWholeLines, PartF.ReadWholeLines));
        Assert.Equal([PartF.AddTracing], OutputLines(mergeBase));
        AssertCall(app, calls, PartF.DiffTreeCall(PartF.AddTracing, PartF.ReadWholeLines));
        revList = AssertCall(app, calls, PartF.RevListCall(PartF.CompareWholeLines, PartF.ReadWholeLines));
        Assert.Equal([PartF.F13DropCountsOutput], OutputLines(revList));
        AssertRowSelected(app, PartF.ReadWholeLinesRow);
        AssertNoClippedText(app);

        // Step 10: closing shows the graph's single selection.
        app.Click(app.FindByAutomationId("CompareClose"));
        await app.WaitForAsync(() => app.Compare() is null, "the comparison to close");
        await AssertDetailsAsync(app, PartF.ReadWholeLinesDetails);
        AssertRowSelected(app, PartF.ReadWholeLinesRow);
        await AssertGitUnchangedAsync(repo, PartF.HistoryStatus, refs, config);
        await AssertNoDialogAsync(app);
    }

    /// <summary>The comparison's header (6.4): 36 high, its title and swap button centred in it.</summary>
    private static void AssertCompareHeader(ShellDriver app)
    {
        var view = app.BoundsOf(app.FindByAutomationId(ShellDriver.CompareId));
        var title = app.BoundsOf(app.FindByAutomationId("CompareTitle"));
        var swap = app.BoundsOf(app.FindByAutomationId("CompareSwap"));
        Assert.Equal(view.Top + 18, title.Center.Y, PartF.PositionTolerance);
        Assert.Equal(view.Top + 18, swap.Center.Y, PartF.PositionTolerance);
        Assert.True(swap.Bottom <= view.Top + 36, $"The swap button at {swap} is not inside the 36-high header.");
    }

    /// <summary>Phase 3's list of the three selected commits (F12 step 6), in row order, with its hint.</summary>
    private static async Task AssertThreeSelectedAsync(ShellDriver app)
    {
        await app.WaitForAsync(() => Shown(app, "MultiSelectionView") is not null, "the multi-selection view");
        Assert.Null(app.Compare());
        var view = app.FindByAutomationId("MultiSelectionView");
        Assert.Equal($"{PartF.ThreeSelected.Length.ToString(CultureInfo.InvariantCulture)} commits selected", ((TextBlock)app.FindByAutomationId(view, "MultiSelectionTitle")).Text);
        var items = view.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetAutomationId(control) == "MultiSelectionItem")
            .OrderBy(control => app.BoundsOf(control).Y)
            .Select(item => string.Join(' ', TextsIn(item)))
            .ToList();
        Assert.Equal(PartF.ThreeSelected, items);
        Assert.Equal(PartF.MultiSelectionHint, ((TextBlock)app.FindByAutomationId(view, "MultiSelectionHint")).Text);
    }

    /// <summary>Waits until the stage panel shows, its message read.</summary>
    private static Task WaitForStagePanelAsync(ShellDriver app) =>
        app.WaitForAsync(() => Shown(app, "StagePanel") is not null && app.StagePanelSettled, "the stage panel");

    /// <summary>
    /// The stage panel of "history" with F13's draft (steps 1 and 7): one unstaged file, nothing
    /// staged, the summary "Keep this draft" with its counter, and the disabled "Stage files to commit".
    /// </summary>
    private static void AssertStagePanel(ShellDriver app)
    {
        Assert.True(Repo(app).IsWorkingRowSelected, "The working-changes row is not selected.");
        Assert.Equal("Unstaged files (1)", app.Find<TextBlock>("UnstagedTitle").Text);
        Assert.Equal(PartF.UnstagedReader, FileRows(app, "UnstagedFileList"));
        Assert.True(app.Find<TextBlock>("NoStaged").IsEffectivelyVisible, "The stage panel does not say \"No staged changes\".");
        Assert.Equal("No staged changes", app.Find<TextBlock>("NoStaged").Text);
        Assert.Equal(PartF.DraftSummary, ((TextBox)app.FindByAutomationId("CommitSummary")).Text);
        Assert.Equal(PartF.DraftCounter, ((TextBlock)app.FindByAutomationId("SummaryCounter")).Text);
        var commit = (Button)app.FindByAutomationId("CommitButton");
        Assert.Equal("Stage files to commit", commit.Content);
        Assert.False(commit.IsEffectivelyEnabled, "The commit button is enabled.");
    }
}
