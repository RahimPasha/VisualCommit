using Avalonia.Headless.XUnit;
using VisualCommit.Core.Session;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>The pictures of "Many repositories": a workspace's dashboard (<c>tabs-and-workspaces.md</c>).</summary>
public sealed class ManyRepositoriesShots
{
    /// <summary>The demo's workspace, "Home projects", on its dashboard: each repository's branch, changes and distance from its upstream.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Dashboard(GuideTheme theme) =>
        GuideCamera.ShootAsync(
            GuideShotCatalogue.Dashboard,
            theme,
            new GuideStart
            {
                Tabs = scenario => [new TabState(null, GuideRun.WorkspaceId), new TabState(scenario.WeatherStation)],
                Workspaces = GuideRun.DemoWorkspace,
            },
            run =>
            {
                var app = run.App;
                app.MoveToNeutral();

                // The tabs, the toolbar, the dashboard's header and its rows, without the empty list below them.
                var last = app.BoundsOf(app.DashboardRowControl(GuideScenario.RecipeBoxName));
                return Task.FromResult(GuideFrame.Area(GuideSteps.Band(app, 0, last.Bottom + 28)));
            });
}
