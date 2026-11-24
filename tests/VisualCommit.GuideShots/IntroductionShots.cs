using Avalonia.Headless.XUnit;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>The pictures of the guide's introduction (<c>index.md</c>), which the tour of the window (<c>tour.md</c>) shows too.</summary>
public sealed class IntroductionShots
{
    /// <summary>The main window on the weather station: the tabs, the toolbar, the branches, the graph with the working changes' row, a commit's details and the status bar.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Main_window(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.MainWindow, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            app.Click(app.GraphRowPoint(1));
            await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Repository?.Details?.Subject == "Show the battery level", "the details of the branch's last commit");
            app.MoveToNeutral();
            return GuideFrame.Window;
        });
}
