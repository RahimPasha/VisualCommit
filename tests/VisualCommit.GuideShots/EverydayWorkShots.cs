using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>
/// The pictures of "Everyday work": changes and diffs (<c>see-changes.md</c>), staging and
/// committing (<c>stage-and-commit.md</c>), branches (<c>branches.md</c>), syncing with a remote
/// (<c>sync.md</c>), stashes and tags (<c>stash-and-tags.md</c>), all on the weather station.
/// </summary>
public sealed class EverydayWorkShots
{
    /// <summary>A changed file's diff, the old text beside the new.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Diff_side_by_side(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.DiffSideBySide, theme, GuideStart.WeatherStationWide, async run =>
        {
            var app = run.App;
            var diff = await GuideSteps.OpenDiffAsync(app, GuideScenario.UnitsFile);
            app.Click(app.FindByAutomationId("SideBySideDiffButton"));
            await GuideSteps.WaitForDiffTextAsync(app, GuideScenario.UnitsFile);
            app.MoveToNeutral();
            return GuideSteps.DiffFrame(app, diff);
        });

    /// <summary>The same diff in one column, the removed lines above the added.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Diff_inline(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.DiffInline, theme, GuideStart.WeatherStationWide, async run =>
        {
            var app = run.App;
            var diff = await GuideSteps.OpenDiffAsync(app, GuideScenario.UnitsFile);
            app.Click(app.FindByAutomationId("InlineDiffButton"));
            await GuideSteps.WaitForDiffTextAsync(app, GuideScenario.UnitsFile);
            app.MoveToNeutral();
            return GuideSteps.DiffFrame(app, diff);
        });

    /// <summary>The working changes: the unstaged and staged files, the commit's message and the Commit button.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Working_changes(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.WorkingChanges, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            var panel = await GuideSteps.OpenWorkingChangesAsync(app);
            app.Click(app.FindByAutomationId("CommitSummary"));
            app.Type("Explain the units in the readme");
            app.MoveToNeutral();
            return GuideFrame.Of(panel);
        });

    /// <summary>A diff's hunk with its own Stage button, the pointer on it.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Stage_hunk(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.StageHunk, theme, GuideStart.WeatherStationWide, async run =>
        {
            var app = run.App;
            var diff = await GuideSteps.OpenDiffAsync(app, GuideScenario.UnitsFile);
            app.Click(app.FindByAutomationId("InlineDiffButton"));
            await GuideSteps.WaitForDiffTextAsync(app, GuideScenario.UnitsFile);

            // The hunks' bars are made as the text is drawn.
            Button? stage = null;
            await app.WaitForAsync(
                () =>
                {
                    app.Capture();
                    stage = app.Window.GetVisualDescendants().OfType<Button>()
                        .FirstOrDefault(button => button.IsEffectivelyVisible && AutomationProperties.GetAutomationId(button) == "StageHunkButton");
                    return stage is not null;
                },
                "the first hunk's Stage hunk button");
            app.MoveMouse(stage!);
            app.Settle();
            return GuideSteps.DiffFrame(app, diff);
        });

    /// <summary>A local branch's menu in the left panel.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Branch_menu(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.BranchMenu, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            app.Click(await app.RefRowAsync("Local branches", "feature"));
            var forecast = await app.RefRowAsync("Local branches", "feature", "forecast");
            app.RightClick(app.BoundsOf(forecast).Center);
            app.Menu();
            return GuideFrame.Of(app.FindByAutomationId("LeftPanel"), GuideSteps.OpenMenu(app));
        });

    /// <summary>"Create branch here…" of a commit: the branch's name typed, "Check out the new branch" ticked.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Create_branch(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.CreateBranch, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            GuideSteps.OpenCommitMenu(app, GuideSteps.MainRow);
            app.ClickMenuItem("Create branch here…");
            await app.WaitForDialogAsync<NameDialogViewModel>();
            app.TypeInto("NameBox", "feature/alerts");
            app.MoveToNeutral();
            return GuideFrame.Card(GuideSteps.DialogCard(app));
        });

    /// <summary>main one commit ahead of origin/main and one behind it: the left panel's arrows beside main, and origin's branches below.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Ahead_behind(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.AheadBehind, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            var header = app.BoundsOf(await app.RefRowAsync("Local branches"));
            var originMain = app.BoundsOf(await app.RefRowAsync("Remotes", "origin", "main"));
            var panel = app.BoundsOf(app.FindByAutomationId("LeftPanel"));
            return GuideFrame.Area(new Rect(panel.Left, header.Top - 4, panel.Width, originMain.Bottom + 4 - (header.Top - 4)));
        });

    /// <summary>The Remotes dialog, from the Remotes section's menu.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Manage_remotes(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.ManageRemotes, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            app.RightClick(await app.RefRowAsync("Remotes"));
            app.Menu();
            app.ClickMenuItem("Manage remotes…");
            await app.WaitForDialogAsync<RemotesDialogViewModel>();
            await app.WaitForIdleAsync();
            app.MoveToNeutral();
            return GuideFrame.Card(GuideSteps.DialogCard(app));
        });

    /// <summary>The stash in the graph, on the commit it was made on, selected, with its details.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Stash_in_graph(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.StashInGraph, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            app.Click(app.GraphRowPoint(GuideSteps.StashRow));
            await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Repository?.Details?.Subject?.Contains(GuideScenario.StashMessage, StringComparison.Ordinal) == true, "the stash's details");
            app.MoveToNeutral();
            var graph = app.BoundsOf(GuideSteps.GraphArea(app));
            var right = app.BoundsOf(app.FindByAutomationId("RightPanel"));
            var bottom = app.GraphRowPoint(GuideSteps.CheckWiringRow).Y + 14;
            return GuideFrame.Area(new Rect(graph.Left, graph.Top, right.Right - graph.Left, bottom - graph.Top));
        });

    /// <summary>"Create tag here…" of a commit: the tag's name and its message.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Create_tag(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.CreateTag, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            GuideSteps.OpenCommitMenu(app, GuideSteps.MainRow);
            app.ClickMenuItem("Create tag here…");
            await app.WaitForDialogAsync<NameDialogViewModel>();
            app.TypeInto("NameBox", "v1.2");
            app.MoveToNeutral();
            return GuideFrame.Card(GuideSteps.DialogCard(app));
        });
}
