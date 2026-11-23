using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>
/// The pictures of "Hosting accounts": the Accounts section with GitHub signed in and the sign-in
/// dialog (<c>accounts.md</c>), and the weather station's pull requests (<c>pull-requests.md</c>).
/// GitHub is the fake in process, at github.com's addresses, its login <see cref="GuideScenario.Owner"/>.
/// </summary>
public sealed class HostingAccountsShots
{
    /// <summary>Settings, Accounts: GitHub signed in as demo--alex, in the keychain.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Accounts_section(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.AccountsSection, theme, GuideStart.WeatherStationOnGitHub, async run =>
        {
            var app = run.App;
            await app.OpenAccountsAsync();
            await app.WaitForIdleAsync();
            app.MoveToNeutral();
            return GuideFrame.Card(GuideSteps.DialogCard(app));
        });

    /// <summary>The dialog that signs in to GitHub with a token, before one is typed.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Sign_in_dialog(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.SignInDialog, theme, GuideStart.WeatherStationOnGitHub with { SignedIn = false }, async run =>
        {
            var app = run.App;
            await app.OpenAccountsAsync();
            await app.OpenSignInDialogAsync();
            app.MoveToNeutral();
            return GuideFrame.Card(GuideSteps.DialogCard(app));
        });

    /// <summary>The left panel's pull requests of the weather station on GitHub, with the branch the open one comes from.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Pull_requests(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.PullRequests, theme, GuideStart.WeatherStationOnGitHub, async run =>
        {
            var app = run.App;
            await app.WaitForAsync(
                () => app.Window.GetVisualDescendants().OfType<Control>().Any(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == "PullRequestItem"),
                "the pull requests in the left panel");
            await app.WaitForIdleAsync();
            app.MoveToNeutral();

            // The left panel and the graph down to the graph's last whole row; the panel holds
            // nothing that low.
            var panel = app.BoundsOf(app.FindByAutomationId("LeftPanel"));
            var graph = GuideSteps.GraphAreaToLastWholeRow(app);
            var top = Math.Min(panel.Top, graph.Top);
            return GuideFrame.Area(new Rect(panel.Left, top, graph.Right - panel.Left, graph.Bottom - top));
        });
}
