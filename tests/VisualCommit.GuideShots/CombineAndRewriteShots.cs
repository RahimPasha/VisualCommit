using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Dialogs;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>
/// The pictures of "Combine and rewrite": merging by a branch dragged onto another
/// (<c>merge.md</c>), the interactive rebase's list (<c>rebase.md</c>), a commit's menu and the
/// reset's question (<c>pick-revert-reset.md</c>), and the conflict resolver (<c>conflicts.md</c>).
/// </summary>
public sealed class CombineAndRewriteShots
{
    /// <summary>
    /// Where the commit's menu is asked for, from the graph's area's left edge: in the Graph
    /// column, so that the menu, about 288 wide, ends inside the 440 of the graph's area.
    /// </summary>
    private const double CommitMenuX = 140;

    /// <summary>
    /// fix/rounding's label dragged onto main's and dropped: the menu of what the drop can do. In
    /// the wider window, whose graph holds the whole menu, so that the picture takes in nothing of
    /// the right panel.
    /// </summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Drag_menu(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.DragMenu, theme, GuideStart.WeatherStationWide, async run =>
        {
            var app = run.App;
            var rounding = app.GraphLabelBounds(GuideSteps.RoundHalfUpRow, "fix/rounding");
            var main = app.GraphLabelBounds(GuideSteps.MainRow, "main");
            app.PressMouse(rounding.Center);
            app.MoveMouseHeld(main.Center, steps: 5);
            app.ReleaseMouse(main.Center);
            app.Menu();
            var menu = GuideSteps.OpenMenu(app);
            GuideSteps.ThrowUnlessMenuInGraph(app, menu);
            return GuideFrame.Area(GuideSteps.GraphAreaToLastWholeRow(app)).With(GuideFrame.Card(menu));
        });

    /// <summary>"Interactive rebase of main from here…": the list of main's commits since the chosen one, each with its action.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Interactive_list(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.InteractiveList, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            GuideSteps.OpenCommitMenu(app, GuideSteps.CacheReadingsRow);
            app.ClickMenuItem("Interactive rebase of main from here…");
            var list = await app.WaitForRebaseListAsync();
            app.MoveToNeutral();
            return GuideFrame.Of(list);
        });

    /// <summary>
    /// A commit's menu in the graph, opened from the row's Graph column, so that the menu ends
    /// inside the graph's area and the picture takes in nothing of the right panel.
    /// </summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Commit_menu(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.CommitMenu, theme, GuideStart.WeatherStation, run =>
        {
            var app = run.App;
            var graph = app.BoundsOf(GuideSteps.GraphArea(app));
            var menu = GuideSteps.OpenCommitMenu(app, GuideSteps.RoundToDigitsRow, graph.Left + CommitMenuX);
            GuideSteps.ThrowUnlessMenuInGraph(app, menu);
            return Task.FromResult(GuideFrame.Area(GuideSteps.GraphAreaToLastWholeRow(app)).With(GuideFrame.Card(menu)));
        });

    /// <summary>"Reset main to this commit…": the question with its three kinds of reset.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Reset_question(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.ResetQuestion, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            GuideSteps.OpenCommitMenu(app, GuideSteps.RoundToDigitsRow);
            app.ClickMenuItem("Reset main to this commit…");
            await app.WaitForDialogAsync<ResetDialogViewModel>();
            app.MoveToNeutral();
            return GuideFrame.Card(GuideSteps.DialogCard(app));
        });

    /// <summary>A merge of fix/rounding stopped on a conflict in units.py: the resolver with both sides and the result.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Resolver(GuideTheme theme) =>
        GuideCamera.ShootAsync(
            GuideShotCatalogue.Resolver,
            theme,
            GuideStart.WeatherStation with { Prepare = scenario => scenario.StopMergeOnConflictAsync() },
            async run =>
            {
                var app = run.App;
                app.Click(app.WorkingRowPoint());
                app.Click(await app.FileRowAsync("UnstagedFileList", GuideScenario.UnitsFile));
                await app.WaitForResolverAsync("units.py");
                await app.WaitForIdleAsync();
                app.MoveToNeutral();
                return GuideFrame.Window;
            });
}
