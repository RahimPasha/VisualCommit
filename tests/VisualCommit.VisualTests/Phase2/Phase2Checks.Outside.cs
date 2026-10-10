using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.Core.Diff;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase0;
using VisualCommit.VisualTests.Phase1;
using Xunit;

namespace VisualCommit.VisualTests.Phase2;

/// <summary>Phase 2's check of changes made outside the app (Q4 for the working tree, D71).</summary>
public partial class Phase2Checks
{
    private static readonly TimeSpan OutsideChange = TimeSpan.FromSeconds(5);

    [AvaloniaFact]
    public async Task Check_20_working_tree_changes_made_outside_the_app_appear()
    {
        using var repo = await Scenarios.LinearAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 3, changes: false);
        var graph = Graph(app);
        var tab = app.Session.Shell.ActiveTab;
        app.Capture().Save(Phase, "20a-clean");
        Assert.False(graph.ShowsWorkingRow);

        repo.WriteFile("notes.txt", "A note\n");
        await app.WaitForAsync(() => tab.Repository!.HasWorkingChanges, "the working-changes row", OutsideChange);
        var appeared = app.Capture();
        appeared.Save(Phase, "20b-row-appeared");
        Assert.True(graph.ShowsWorkingRow);
        Assert.Equal("Describe the project", graph.Data!.CommitAt(0).Subject);
        Assert.Equal("main", Assert.Single(graph.LabelsInRow(0)).FullText);
        AssertDashedLine(app, appeared, Palettes.Dark.Lane(0));

        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 1, staged: 0);
        await OpenDiffAsync(app, "UnstagedFileList", "notes.txt", DiffBody.Text);
        File.AppendAllText(Path.Combine(repo.Path, "notes.txt"), "Another note\n");
        await app.WaitForAsync(
            () => tab.Repository!.Diff is { Diff: { } diff } && diff.Hunks.Count == 1 && diff.Hunks[0].Lines.Count == 2,
            "the diff to show the appended line",
            OutsideChange);
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "20c-diff-followed");
        var lines = tab.Repository!.Diff!.Diff!.Hunks.Single();
        Assert.Equal("@@ -0,0 +1,2 @@", lines.Header);
        Assert.Equal(["A note", "Another note"], lines.Lines.Select(line => line.Text));
        Assert.All(lines.Lines, line => Assert.Equal(DiffLineKind.Added, line.Kind));

        await repo.GitAsync("add", "notes.txt");
        await app.WaitForAsync(() => app.Find<TextBlock>("StagedTitle").Text == "Staged files (1)", "the staged list to have notes.txt", OutsideChange);
        await app.WaitForAsync(() => !tab.Repository!.IsDiffOpen, "the diff view to close: its diff is empty", OutsideChange);
        app.Capture().Save(Phase, "20d-staged-outside");
        Assert.Equal(["A notes.txt"], FileRows(app, "StagedFileList"));
        Assert.True(app.Find<TextBlock>("NoUnstaged").IsEffectivelyVisible);
        Assert.True(Graph(app).IsEffectivelyVisible);

        File.AppendAllText(Path.Combine(repo.Path, ".git", "info", "exclude"), "build/\n");
        repo.WriteFile("build/out.txt", "output\n");
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        app.Settle();
        Assert.Equal("Unstaged files (0)", app.Find<TextBlock>("UnstagedTitle").Text);
        Assert.Equal("Staged files (1)", app.Find<TextBlock>("StagedTitle").Text);

        await repo.GitAsync("rm", "--cached", "--quiet", "notes.txt");
        File.Delete(Path.Combine(repo.Path, "notes.txt"));
        await app.WaitForAsync(() => !tab.Repository!.HasWorkingChanges, "the working-changes row to go", OutsideChange);
        app.Capture().Save(Phase, "20e-row-gone");
        Assert.False(graph.ShowsWorkingRow);
        Assert.False(graph.IsWorkingRowSelected);
        Assert.Equal(["Commit details", "Select a commit to see its details."], ShellExpectations.TextsIn(app.FindByAutomationId("RightPanel")));
    }
}
