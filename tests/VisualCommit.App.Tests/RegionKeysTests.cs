using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.Views;
using VisualCommit.Core.Git;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// F6 and Shift+F6 (docs/phase-6-design.md 6.6, 6.12, D213): round the regions shown, in their
/// order, each first visit on 6.6's target and a region's last focused element winning after, on
/// a repository tab, with the activity log, with the resolver (no right panel; a binary
/// conflict's card), and on a dashboard. The keys share an app start per setup.
/// </summary>
public class RegionKeysTests
{
    [AvaloniaFact]
    public async Task F6_goes_round_the_regions_of_a_repository_tab_each_on_its_target_and_back_to_the_last_element()
    {
        using var repo = await Scenarios.OperationsAsync();
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "src", "core.txt"), "core\nmain work\nkeys\n");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();

        // The working changes selected: the stage panel is the right region.
        app.Click(app.WorkingRowPoint());
        Assert.True(repository.IsWorkingRowSelected);
        Assert.Equal((KeyRegionRole.GraphArea, "GraphRows"), (app.FocusedRegion(), app.FocusedAutomationId()));

        // F6: the stage panel's unstaged list's first row (6.6's mark), not StageFlatButton.
        app.PressF6();
        Assert.Equal(KeyRegionRole.Right, app.FocusedRegion());
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("UnstagedFileList")), "The focus is on UnstagedFileList's row.");

        // Moved as by Tab, so the keyboard's frame shows.
        Assert.True(((StyledElement)app.FocusedElement!).Classes.Contains(":focus-visible"), "The focused row shows the keyboard's frame.");

        // F6: round to the left panel (no activity log shown): RefList's selected or first row, not FilterBox.
        app.PressF6();
        Assert.Equal(KeyRegionRole.Left, app.FocusedRegion());
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("RefList")), "The focus is in RefList.");

        // F6: the graph again; Shift+F6 back.
        app.PressF6();
        Assert.Equal((KeyRegionRole.GraphArea, "GraphRows"), (app.FocusedRegion(), app.FocusedAutomationId()));
        app.PressF6(shift: true);
        Assert.Equal(KeyRegionRole.Left, app.FocusedRegion());

        // A region's last focused element wins: the commit summary in the right region.
        var summary = (TextBox)app.FindByAutomationId("CommitSummary");
        summary.Focus();
        app.Settle();
        foreach (var expected in new[] { KeyRegionRole.Left, KeyRegionRole.GraphArea, KeyRegionRole.Right })
        {
            app.PressF6();
            Assert.Equal(expected, app.FocusedRegion());
        }

        Assert.Same(summary, app.FocusedElement);
        TestPictures.Save(app, "wpk-f6-commit-summary");

        // The activity log, once shown and holding a row, is the last region.
        await app.WaitForAsync(() => repository.Queue.Current is null, "the queue to be idle");
        repository.Toolbar.FetchCommand.Execute(null);
        await app.WaitForIdleAsync();
        app.Session.Shell.ActivityLog.ToggleCommand.Execute(null);
        app.Settle();
        app.Click(app.GraphRowPoint(2));
        app.PressF6(shift: true);
        Assert.Equal(KeyRegionRole.Left, app.FocusedRegion());
        app.PressF6(shift: true);
        Assert.Equal(KeyRegionRole.ActivityLog, app.FocusedRegion());
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("ActivityLogList")), "The focus is on the log's first row.");
        TestPictures.Save(app, "wpk-f6-activity-log");
    }

    [AvaloniaFact]
    public async Task With_the_resolver_open_there_is_no_right_region_and_a_binary_conflicts_card_takes_the_focus()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.MergeKinds);
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => repository.Status.Conflicts.Count > 1, "the conflicts read");
        repository.IsWorkingRowSelected = true;
        repository.OpenDiff(Assert.Single(repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted && file.Path == "new.txt"), staged: false);
        await app.WaitForResolverAsync("new.txt");

        // The text conflict's result is the graph area's target; no right region.
        app.FindByAutomationId("FilterBox").Focus();
        app.Settle();
        app.PressF6();
        Assert.Equal(KeyRegionRole.GraphArea, app.FocusedRegion());
        Assert.True(InResult(app), "The focus is in the result.");
        app.PressF6();
        Assert.Equal(KeyRegionRole.Left, app.FocusedRegion());

        // The binary conflict: its card's first button.
        repository.OpenDiff(Assert.Single(repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted && file.Path == "image.png"), staged: false);
        await app.WaitForResolverAsync("image.png");
        app.FindByAutomationId("FilterBox").Focus();
        app.Settle();
        app.PressF6();
        Assert.Equal(KeyRegionRole.GraphArea, app.FocusedRegion());
        var focused = Assert.IsType<Button>(app.FocusedElement, exactMatch: false);
        Assert.Contains(focused.GetVisualAncestors().OfType<ItemsControl>(), items => items.Name == "CardButtons");
    }

    [AvaloniaFact]
    public async Task On_a_dashboard_F6_goes_to_its_list()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithWorkspace(data.Path, new WorkspaceList { Workspaces = [new Workspace("web", "Web", [new WorkspaceRepository(repo.Path)])] }, openDashboardOf: "web");
        await app.WaitUntilReadyAsync();
        await app.WaitForDashboardReadAsync();
        app.FindByAutomationId("SettingsButton").Focus();
        app.Settle();
        Assert.Equal(KeyRegionRole.None, app.FocusedRegion());

        app.PressF6();
        Assert.Equal(KeyRegionRole.GraphArea, app.FocusedRegion());
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("DashboardList")), "The focus is in the dashboard's list.");
        app.PressF6(shift: true);
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("DashboardList")), "The only region keeps the focus.");
    }

    private static bool InResult(ShellDriver app) =>
        app.FocusedElement is Visual focused && focused.GetSelfAndVisualAncestors().OfType<ConflictResultEditor>().Any();
}
