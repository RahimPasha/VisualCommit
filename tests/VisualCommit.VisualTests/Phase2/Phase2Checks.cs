using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase0;
using VisualCommit.VisualTests.Phase1;
using Xunit;

namespace VisualCommit.VisualTests.Phase2;

/// <summary>
/// The scripted walk-through of phase 2. Each test is one numbered check of
/// docs/test-reports/phase-2.md: it performs the check's steps on the whole app in headless mode,
/// saves a screenshot after every step under artifacts/visual/phase-2/scripted/, and asserts the
/// expected result. This part holds the checks of the working-changes row and the stage panel.
/// </summary>
public partial class Phase2Checks
{
    private const int Phase = 2;

    [AvaloniaFact]
    public async Task Check_01_working_changes_row_and_stage_panel()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2);
        var graph = Graph(app);

        var first = app.Capture();
        first.Save(Phase, "01a-working-row");
        Assert.True(graph.ShowsWorkingRow);
        Assert.False(graph.IsWorkingRowSelected);
        Assert.Equal(-1, graph.SelectedIndex);
        Assert.Equal(["Add calculator", "Initial commit"], Subjects(graph));
        Assert.Equal(CommitGraphControl.RowHeight, graph.RowBounds(0).Top - graph.WorkingRowBounds.Top);
        AssertRing(first, WorkingRingCentre(app), Palettes.Dark.Lane(0), "working-changes row's ring");
        AssertDashedLine(app, first, Palettes.Dark.Lane(0));
        Assert.Equal(["Select a commit to see its details."], ShellExpectations.TextsIn(app.FindByAutomationId("RightPanel")).Skip(1));

        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);
        var panel = app.Capture();
        panel.Save(Phase, "01b-stage-panel");
        Assert.True(graph.IsWorkingRowSelected);
        AssertWorkingRowBackground(app, panel, Palettes.Dark.Selection);
        Assert.Equal("Working changes", app.Find<TextBlock>("StagePanelTitle").Text);
        Assert.Contains("selected", app.Find<Button>("StageFlatButton").Classes);
        Assert.Equal("Unstaged files (7)", app.Find<TextBlock>("UnstagedTitle").Text);
        Assert.Equal(Phase2Expectations.UnstagedFlat, FileRows(app, "UnstagedFileList"));
        Assert.Equal("Staged files (3)", app.Find<TextBlock>("StagedTitle").Text);
        Assert.Equal(Phase2Expectations.StagedFlat, FileRows(app, "StagedFileList"));
        Assert.True(app.Find<Button>("StageAllButton").IsEffectivelyVisible);
        Assert.True(app.Find<Button>("DiscardAllButton").IsEffectivelyVisible);
        Assert.True(app.Find<Button>("UnstageAllButton").IsEffectivelyVisible);
        Assert.Equal(string.Empty, app.Find<TextBox>("CommitSummary").Text ?? string.Empty);
        Assert.Equal("72", app.Find<TextBlock>("SummaryCounter").Text);
        Assert.False(app.Find<CheckBox>("AmendCheckBox").IsChecked);
        var commit = app.Find<Button>("CommitButton");
        Assert.Equal("Commit 3 files", commit.Content);
        Assert.False(commit.IsEffectivelyEnabled);
        Phase2Expectations.AssertStagePanelLayout(app, height: 700);
        AssertNoClippedText(app);

        ClickRow(app, 0);
        app.PressKey(Key.Up);
        Assert.True(graph.IsWorkingRowSelected);
        Assert.True(app.Session.Shell.ActiveTab.ShowsWorkingChanges);
        app.PressKey(Key.Down);
        Assert.Equal(0, graph.SelectedIndex);
        Assert.False(graph.IsWorkingRowSelected);
        await WaitForDetailsAsync(app, "Add calculator");
        app.PressKey(Key.Home);
        Assert.True(graph.IsWorkingRowSelected);

        var calculator = await ScrollToFileRowAsync(app, "UnstagedFileList", "src/Calculator.cs");
        app.MoveMouse(calculator);
        var hover = app.Capture();
        hover.Save(Phase, "01c-row-hover");
        var row = RowPanel(calculator);
        Assert.Equal(["RowStageButton", "RowDiscardButton"], VisibleRowButtons(row));
        ShellExpectations.AssertColour(Phase2Expectations.Dark.Hover, hover, app.BoundsOf(row).TopLeft + new Point(4, 3), "the hovered row's background");
    }
}
