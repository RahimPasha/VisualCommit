// Part A of phase 5's scripted walk-through (docs/test-reports/phase-5.md, "Part A: the shell and
// the activity log"): checks A1 to A4 of design 15.4. This class is part A's alone; its expected
// texts are in Phase5Expectations.A.cs (Phase5Expectations.PartA). One test method per app start,
// named as "Test methods and app starts (part A)" names them. This file holds A1 (the toolbar and
// the status bar); Phase5Checks.A.Log.cs A2 (the activity log), Phase5Checks.A.Height.cs A3 (the
// panel's height across a restart), Phase5Checks.A.Keys.cs A4 (the keys), and
// Phase5Checks.A.Helpers.cs what they share.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

/// <summary>The checks of part A: the shell (the toolbar, the status bar, the keys) and the activity log.</summary>
public sealed partial class Phase5ChecksA : Phase5Checks
{
    /// <summary>
    /// Check A1: the toolbar and the status bar on an empty tab (only Theme enabled, nothing with
    /// a tooltip), with "history" open (Search and the Activity log enabled, Undo and Redo
    /// disabled with their reasons), and after a commit, Undo enabled at once with its text; the
    /// toolbar follows the active tab.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A1_toolbar_and_status_bar()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var meta = CommandIsMeta(app);

        // Step 1: a second, empty tab.
        app.Click(app.FindByAutomationId("AddRepoButton"));
        app.Settle();
        var shot = Shot(app, PartA.A1EmptyTab);
        AssertTabs(app, PartA.NewTab, PartA.HistoryRepo, PartA.NewTab);
        AssertEmptyTab(app);
        AssertToolbar(app, PartA.EmptyTabToolbar);
        await AssertStatusBarTextsAsync(app, PartA.NoRepository, PartA.Ready);
        AssertToggle(app, enabled: false, selected: false, shot);
        Assert.False(app.ActivityLogView.IsEffectivelyVisible, "The activity log is shown on an empty tab.");
        AssertNoClippedText(app);

        // Step 2: back to "history": H0, nothing selected, Undo and Redo with nothing to act on.
        app.Click(TabButton(app, PartA.HistoryRepo));
        await app.WaitForIdleAsync();
        shot = Shot(app, PartA.A1RepositoryOpen);
        AssertTabs(app, PartA.HistoryRepo, PartA.HistoryRepo, PartA.NewTab);
        AssertGraph(app, PartA.H0, PartA.HistoryLanes, PartA.Working, showsDate: true, picture: shot);
        AssertNothingSelected(app);
        AssertBranch(app, "main");
        AssertStatus(app, PartA.Ready);
        AssertToolbar(app, PartA.OpenToolbar(meta));
        await AssertStatusBarTextsAsync(app, "main", PartA.Ready);
        AssertToggle(app, enabled: true, selected: false, shot);
        AssertNoPanel(app);
        AssertHistoryRowsInPlace(app, workingRow: true);
        AssertNoClippedText(app);

        // Step 3: the tooltips of Undo, Redo, Search and the toggle, each rested on in turn.
        var operations = app.Session.Operations.Snapshot().Count;
        var undo = ToolbarButton(app, ShellDriver.UndoButtonId);
        Assert.Equal(PartA.NothingToUndo, await app.RestPointerOnAsync(undo));
        var pointer = app.BoundsOf(undo).Center;
        Shot(app, PartA.A1UndoTooltipNothing, keepPointer: true);
        AssertTipBelowPointer(app, PartA.NothingToUndo, pointer);
        Assert.False(undo.IsEffectivelyEnabled, "Undo is enabled.");
        app.MoveToNeutral();
        Assert.Equal(PartA.NothingToRedo, await app.RestPointerOnAsync(ToolbarButton(app, ShellDriver.RedoButtonId)));
        app.MoveToNeutral();
        var search = ToolbarButton(app, "SearchButton");
        Assert.Equal(PartA.SearchToolTip(meta), await app.RestPointerOnAsync(search));
        pointer = app.BoundsOf(search).Center;
        Shot(app, PartA.A1SearchTooltip, keepPointer: true);
        AssertTipBelowPointer(app, PartA.SearchToolTip(meta), pointer);
        app.MoveToNeutral();
        Assert.Equal(PartA.ShowActivityLog, await app.RestPointerOnAsync(app.FindByAutomationId("ActivityLogToggle")));
        app.MoveToNeutral();
        await AssertNoMenuAsync(app, logMark: null);
        await AssertNoDialogAsync(app);
        Assert.Equal(operations, app.Session.Operations.Snapshot().Count);
        AssertNothingSelected(app);
        AssertToolbar(app, PartA.OpenToolbar(meta));

        // Step 4: Stage all and a commit from the stage panel.
        var mark = app.Session.Operations.Snapshot().Count;
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (1)", "Staged files (0)");
        app.Click(app.FindByAutomationId("StageAllButton"));
        await app.WaitForIdleAsync();
        await WaitForStagePanelAsync(app, "Unstaged files (0)", "Staged files (1)");
        app.TypeInto("CommitSummary", PartA.CommitSummary);
        app.Click(app.FindByAutomationId("CommitButton"));
        await app.WaitForIdleAsync();

        // The toolbar at once, read right after the wait with no other refresh awaited.
        AssertToolbar(app, PartA.CommittedToolbar(meta));
        shot = Shot(app, PartA.A1Committed);
        Assert.Equal(["Stage", "Commit"], app.Session.Operations.Snapshot().Skip(mark).Select(record => record.Title));
        AssertStatus(app, PartA.Ready);
        await AssertNoDialogAsync(app);
        await AssertCommittedAsync(repo);
        AssertGraph(app, PartA.HA1, PartA.HistoryLanes, working: null, showsDate: true, picture: shot);
        await AssertNewCommitSelectedAsync(app);
        await AssertMainAheadAsync(app, 1);
        AssertToolbar(app, PartA.CommittedToolbar(meta));
        await AssertStatusBarTextsAsync(app, "main", PartA.Ready);
        AssertToggle(app, enabled: true, selected: false, shot);
        AssertHistoryRowsInPlace(app, workingRow: false);

        // Step 5: Undo's tooltip names the commit; resting runs nothing.
        undo = ToolbarButton(app, ShellDriver.UndoButtonId);
        Assert.Equal(PartA.UndoCommitToolTip(meta), await app.RestPointerOnAsync(undo));
        pointer = app.BoundsOf(undo).Center;
        Shot(app, PartA.A1UndoTooltip, keepPointer: true);
        AssertTipBelowPointer(app, PartA.UndoCommitToolTip(meta), pointer, app.BoundsOf(undo).Bottom);
        app.MoveToNeutral();
        Assert.Equal(PartA.DescribeTheLinesSha, await repo.HeadAsync());
        Assert.DoesNotContain(app.Session.Operations.Snapshot(), record => record.Title == PartA.UndoCommit);

        // Step 6: the empty tab: the toolbar acts on the active tab.
        app.Click(TabButton(app, PartA.NewTab));
        app.Settle();
        AssertTabs(app, PartA.NewTab, PartA.HistoryRepo, PartA.NewTab);
        AssertToolbar(app, PartA.EmptyTabToolbar);
        await AssertStatusBarTextsAsync(app, PartA.NoRepository, PartA.Ready);
        AssertToggle(app, enabled: false, selected: false);

        // Step 7: back to "history": the undo history was kept (7.1).
        app.Click(TabButton(app, PartA.HistoryRepo));
        await app.WaitForIdleAsync();
        AssertTabs(app, PartA.HistoryRepo, PartA.HistoryRepo, PartA.NewTab);
        AssertToolbar(app, PartA.CommittedToolbar(meta));
        AssertGraph(app, PartA.HA1, PartA.HistoryLanes, working: null, showsDate: true);
        await AssertNewCommitSelectedAsync(app);
        AssertToggle(app, enabled: true, selected: false);
        Assert.Equal(PartA.DescribeTheLinesSha, await repo.HeadAsync());
    }

    /// <summary>
    /// After A1's commit left no changes, the new commit is selected and its details shown (phase
    /// 2's rule, docs/test-reports/phase-5.md's "Changed in this report after its first commit").
    /// </summary>
    private static async Task AssertNewCommitSelectedAsync(ShellDriver app)
    {
        var graph = Graph(app);
        await app.WaitForAsync(
            () => graph.SelectedIndex == 0 && TextsIn(app.FindByAutomationId("RightPanel")).Contains(PartA.CommitSummary),
            "the new commit selected and its details shown");
        Assert.False(graph.IsWorkingRowSelected, "The working-changes row is selected.");
        Assert.Equal([PartA.DescribeTheLinesSha], graph.Selection.Shas);
        Assert.Equal(PartA.DescribeTheLinesSha, ((SelectableTextBlock)app.FindByAutomationId("DetailsSha")).Text);
    }

    /// <summary>
    /// An empty tab's graph area, left and right panels (phase 0's shell, phase 1's welcome page):
    /// the welcome page, the five sections each <c>0</c>, <c>Commit details</c> with its placeholder.
    /// </summary>
    private static void AssertEmptyTab(ShellDriver app)
    {
        app.Settle();
        Assert.Equal(PartA.WelcomeTexts, TextsIn(app.FindByAutomationId("CommitGraph")));
        Assert.Equal(
            PartA.LeftPanelSections.SelectMany(section => new[] { section, "0" }),
            TextsIn(app.FindByAutomationId("LeftPanel")).Where(text => text != "Filter"));
        Assert.Equal(PartA.NothingSelectedTexts, TextsIn(app.FindByAutomationId("RightPanel")));
    }

    /// <summary>
    /// "history"'s rows where H0 and H-A1 put them at 1100×700 with no panel and no search bar:
    /// the graph's rows from y 116 to the status bar's top; with the working-changes row, its centre
    /// at y 129 and commit row r's at 155 + 26 r, without it row r's at 129 + 26 r.
    /// </summary>
    private static void AssertHistoryRowsInPlace(ShellDriver app, bool workingRow)
    {
        var graph = app.BoundsOf(Graph(app));
        Assert.Equal(WindowLayout.RowsTop, graph.Top, 0.5);
        Assert.Equal(PartA.GraphArea.Bottom, graph.Bottom, PartA.Tolerance);
        var first = workingRow ? 1 : 0;
        if (workingRow)
        {
            Assert.Equal(WindowLayout.RowCentreY(0), app.WorkingRowPoint().Y, PartA.Tolerance);
        }

        Assert.Equal(WindowLayout.RowCentreY(first), app.GraphRowPoint(0).Y, PartA.Tolerance);
        Assert.Equal(WindowLayout.RowCentreY(first + 13), app.GraphRowPoint(13).Y, PartA.Tolerance);
        Assert.Equal(WindowLayout.MessageColumnX(PartA.HistoryLanes) + WindowLayout.MessageClickInset, app.GraphRowPoint(0).X, 0.5);
    }

    /// <summary>
    /// The open tooltip with <paramref name="text"/> is drawn inside the window and under the
    /// pointer that rests at <paramref name="pointer"/> (its top at or below the pointer), and
    /// below <paramref name="below"/> when given (a button's bottom, ±2).
    /// </summary>
    private static void AssertTipBelowPointer(ShellDriver app, string text, Avalonia.Point pointer, double? below = null)
    {
        var tips = app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible && tip.Bounds.Width > 0).ToList();
        var tip = Assert.Single(tips, candidate => candidate.Content as string == text);
        var bounds = app.BoundsOf(tip);
        var size = app.Window.ClientSize;
        Assert.True(
            bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= size.Width && bounds.Bottom <= size.Height,
            $"The tooltip \"{text}\" at {bounds} is not inside the {size.Width}x{size.Height} window.");
        Assert.True(bounds.Top >= pointer.Y, $"The tooltip \"{text}\" at {bounds} is not under the pointer at {pointer}.");
        Assert.True(below is null || bounds.Top >= below - PartA.Tolerance, $"The tooltip \"{text}\" at {bounds} is not below {below}.");
    }

    /// <summary>
    /// Git after A1's commit (H-A1): main at <c>d1e23a3</c>, the tree clean and one ahead of
    /// origin/main, HEAD's newest reflog entry the commit's.
    /// </summary>
    private static async Task AssertCommittedAsync(TempRepo repo)
    {
        Assert.Equal(PartA.DescribeTheLinesSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [ahead 1]"], await GitLinesAsync(repo, "status", "-sb"));
        Assert.Equal([PartA.CommitReflog], await ReflogAsync(repo, "HEAD", 1));
    }

    /// <summary>The left panel's <c>main</c>: the branch HEAD is on, <paramref name="ahead"/> ahead of origin/main and not behind.</summary>
    private static async Task AssertMainAheadAsync(ShellDriver app, int ahead)
    {
        var model = (VisualCommit.App.ViewModels.Panels.RefItemRow)app.RefRowModel("Local branches", "main");
        Assert.True((model.Ahead, model.Behind, model.IsHead) == (ahead, 0, true), $"main shows ↑{model.Ahead} ↓{model.Behind}{(model.IsHead ? " (head)" : string.Empty)}.");
        var button = await app.RefRowAsync("Local branches", "main");
        Assert.Equal(ahead > 0, TextsIn(button).Contains(model.AheadText));
    }
}
