using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Session;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>
/// The pictures of "Combine and rewrite": merging by a branch dragged onto another
/// (<c>merge.md</c>), the interactive rebase's list (<c>rebase.md</c>), a commit's menu and the
/// reset's question (<c>pick-revert-reset.md</c>), and the conflict resolver (<c>conflicts.md</c>).
/// </summary>
public sealed class CombineAndRewriteShots
{
    /// <summary>fix/rounding's label dragged onto main's and dropped: the menu of what the drop can do.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Drag_menu(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.DragMenu, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            var rounding = app.GraphLabelBounds(GuideSteps.RoundHalfUpRow, "fix/rounding");
            var main = app.GraphLabelBounds(GuideSteps.MainRow, "main");
            app.PressMouse(rounding.Center);
            app.MoveMouseHeld(main.Center, steps: 5);
            app.ReleaseMouse(main.Center);
            app.Menu();
            return GuideFrame.Of(GuideSteps.GraphArea(app), GuideSteps.OpenMenu(app));
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

    /// <summary>A commit's menu in the graph.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Commit_menu(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.CommitMenu, theme, GuideStart.WeatherStation, run =>
        {
            var app = run.App;
            var menu = GuideSteps.OpenCommitMenu(app, GuideSteps.RoundToDigitsRow);
            return Task.FromResult(GuideFrame.Of(GuideSteps.GraphArea(app), menu));
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
            return GuideFrame.Of(GuideSteps.DialogCard(app));
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
