using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.Views.Pages;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>
/// The pictures of "Find history": the graph's labels (<c>graph.md</c>), the search bar
/// (<c>search.md</c>), a file's history and its blame (<c>history-and-blame.md</c>), and two
/// commits compared (<c>compare.md</c>).
/// </summary>
public sealed class FindHistoryShots
{
    /// <summary>The graph with its branches', tags' and the stash's labels and the merged branch's lane, down to its last whole row.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Graph_labels(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.GraphLabels, theme, GuideStart.WeatherStation, run =>
            Task.FromResult(GuideFrame.Area(GuideSteps.GraphAreaToLastWholeRow(run.App))));

    /// <summary>The search bar over the graph, "readme" typed, its matches marked.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Search_bar(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.SearchBar, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            app.Click(app.FindByAutomationId("SearchButton"));
            await app.WaitForAsync(() => app.SearchBar() is not null, "the search bar");
            app.Type("readme");
            await app.WaitForSearchAsync();
            app.MoveToNeutral();
            return GuideFrame.Of(GuideSteps.GraphArea(app));
        });

    /// <summary>units.py's history: the commits that changed it, the latest selected with its diff.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task File_history(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.FileHistory, theme, GuideStart.WeatherStationWide, async run =>
        {
            var app = run.App;
            await GuideSteps.OpenWorkingChangesAsync(app);
            app.RightClick(await app.FileRowAsync("UnstagedFileList", GuideScenario.UnitsFile));
            app.Menu();
            app.ClickMenuItem("File history");
            var page = await app.WaitForPageAsync<FileHistoryView>();

            // The history's rows, then the first one's diff, read after them, with its rows laid out.
            await app.WaitForAsync(
                () =>
                {
                    app.Capture();
                    return app.FileHistorySnapshot() is { IsLoading: false, Rows.Count: > 0, Selected: not null, Diff.ShowsText: true }
                        && page.GetVisualDescendants().OfType<DiffTextView>().Any(view => view.IsEffectivelyVisible && view.Rows.Count > 0 && view.RowBounds(0) is not null);
                },
                "units.py's history with its latest change's diff");
            await app.WaitForIdleAsync();
            app.MoveToNeutral();
            return GuideFrame.Of(page);
        });

    /// <summary>units.py's blame: each line with the commit that last changed it.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Blame(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.Blame, theme, GuideStart.WeatherStationWide, async run =>
        {
            var app = run.App;
            await GuideSteps.OpenWorkingChangesAsync(app);
            app.RightClick(await app.FileRowAsync("UnstagedFileList", GuideScenario.UnitsFile));
            app.Menu();
            app.ClickMenuItem("Blame");
            var page = await app.WaitForPageAsync<BlameView>();
            await app.WaitForAsync(() => app.BlameSnapshot() is { IsLoading: false } blame && blame.Runs.Count > 0 && blame.Runs.All(run => run.Background is not null), "units.py's blame");
            await app.WaitForIdleAsync();
            app.MoveToNeutral();
            return GuideFrame.Of(page);
        });

    /// <summary>Two commits compared: one clicked, the other with the command key; the files that differ between them.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Compare(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.Compare, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            app.Click(app.GraphRowPoint(GuideSteps.MainRow));
            await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Repository?.Details?.Subject == "Show the battery level", "the details of main's last commit");
            app.Click(app.GraphRowPoint(GuideSteps.ReleaseOneOneRow), app.CommandModifier);
            await app.WaitForAsync(() => app.Compare() is not null, "the comparison of the two commits");
            await app.WaitForCompareAsync();
            app.MoveToNeutral();
            return GuideFrame.Window;
        });
}
