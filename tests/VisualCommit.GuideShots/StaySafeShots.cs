using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Pages;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>
/// The pictures of "Stay safe": an operation undone (<c>undo-and-reflog.md</c>), the reflog's page,
/// a checkout that git refuses (<c>protection.md</c>) and the activity log (<c>activity-log.md</c>).
/// </summary>
public sealed class StaySafeShots
{
    /// <summary>A branch made and undone: the status bar says what was undone, and Redo offers it back.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Undo_done(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.UndoDone, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            await CreateBranchAsync(app, "try/larger-font");
            await app.ClickUndoAsync();
            await app.WaitForAsync(() => app.UndoRedoState() is { RedoEnabled: true }, "Redo to offer the undone branch back");
            await app.WaitForIdleAsync();
            app.MoveToNeutral();
            return GuideFrame.Window;
        });

    /// <summary>
    /// "Show reflog" of the Local branches section: where HEAD has been, the newest first. In a
    /// wider window of the usual height, so that a checkout's row shows where it moved to and the
    /// rows stay HEAD@{0} to HEAD@{20}.
    /// </summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Reflog(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.Reflog, theme, GuideStart.WeatherStation with { Width = GuideStart.WeatherStationWide.Width }, async run =>
        {
            var app = run.App;
            app.RightClick(await app.RefRowAsync("Local branches"));
            app.Menu();
            app.ClickMenuItem("Show reflog");
            var page = await app.WaitForPageAsync<ReflogView>();
            await app.WaitForAsync(() => app.ReflogSnapshot() is { IsLoading: false } reflog && reflog.Rows.Count > 0, "the reflog's rows");
            await app.WaitForIdleAsync();
            app.MoveToNeutral();
            return GuideFrame.Of(page);
        });

    /// <summary>A double-click on fix/rounding while units.py has changes that its checkout would overwrite: git refuses, and nothing changes.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Refused_operation(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.RefusedOperation, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            app.Click(await app.RefRowAsync("Local branches", "fix"));
            app.DoubleClick(await app.RefRowAsync("Local branches", "fix", "rounding"));
            await app.WaitForDialogAsync<ErrorDialogViewModel>();
            await app.WaitForIdleAsync();
            app.MoveToNeutral();
            return GuideFrame.Window;
        });

    /// <summary>The activity log under the graph after a branch was made: the operation and git's commands.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Activity_log(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.ActivityLog, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            await CreateBranchAsync(app, "try/larger-font");
            app.Click(app.FindByAutomationId("ActivityLogToggle"));
            await app.WaitForAsync(() => app.FindByAutomationId("ActivityLog").IsEffectivelyVisible, "the activity log");
            app.Settle();
            app.MoveToNeutral();
            return GuideFrame.Of(GuideSteps.GraphArea(app));
        });

    /// <summary>"Create branch here…" on main's last commit, the branch not checked out, and the operation's end.</summary>
    private static async Task CreateBranchAsync(Testing.Headless.ShellDriver app, string name)
    {
        GuideSteps.OpenCommitMenu(app, GuideSteps.MainRow);
        app.ClickMenuItem("Create branch here…");
        await app.WaitForDialogAsync<NameDialogViewModel>();
        app.TypeInto("NameBox", name);
        var option = (CheckBox)app.FindByAutomationId(app.TopDialogView!, "NameDialogOption");
        if (option.IsChecked == true)
        {
            app.Click(option);
        }

        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await app.WaitForAsync(() => app.TopDialogView is null, "the name dialog to close");
        await app.WaitForIdleAsync();
        var folder = name.Split('/')[0];
        await app.WaitForAsync(() => HasRefRow(app, "Local branches", folder), $"the folder {folder} under Local branches");
        app.Settle();
    }

    private static bool HasRefRow(Testing.Headless.ShellDriver app, params string[] path)
    {
        try
        {
            app.RefRowModel(path);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
