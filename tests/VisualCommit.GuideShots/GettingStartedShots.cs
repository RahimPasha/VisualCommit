using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Git;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>
/// The pictures of "Getting started": the first-run screen in its states (<c>first-run.md</c>, which
/// <c>troubleshooting/git.md</c> shows too), the status bar's Git box, the welcome page and the
/// clone form (<c>open-clone-create.md</c>). The first-run states come from a fake search for git,
/// each platform's guidance fixed (ruling 48), its git at Git for Windows' path or
/// <c>/usr/bin/git</c>.
/// </summary>
public sealed class GettingStartedShots
{
    /// <summary>Windows, no Git found, winget there: the install button, the download and Check again.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task First_run_missing(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.FirstRunMissing, theme, FirstRun(FakeGitSetupProbe.Windows(), FakeGitAnswer.Missing), async run =>
        {
            await run.App.WaitForGitSetupAsync(winget: true);
            return FirstRunFrame(run.App);
        });

    /// <summary>macOS, no Git and no Command Line Tools: the tools' button and Homebrew's command.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task First_run_missing_macos(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.FirstRunMissingMacOS, theme, FirstRun(FakeGitSetupProbe.MacOS, FakeGitAnswer.CommandLineToolsMissing), async run =>
        {
            await run.App.WaitForGitSetupAsync(command: true);
            return FirstRunFrame(run.App);
        });

    /// <summary>Linux (Ubuntu), no Git: the package manager's command.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task First_run_missing_linux(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.FirstRunMissingLinux, theme, FirstRun(FakeGitSetupProbe.Linux(FakeGitSetupProbe.Ubuntu), FakeGitAnswer.Missing), async run =>
        {
            await run.App.WaitForGitSetupAsync(command: true);
            return FirstRunFrame(run.App);
        });

    /// <summary>Windows, a Git older than VisualCommit needs at Git for Windows' path.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task First_run_too_old(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.FirstRunTooOld, theme, FirstRun(FakeGitSetupProbe.Windows(), FakeGitAnswer.TooOld(FakeGitSetupProbe.WindowsGit, GitSource.Path)), async run =>
        {
            await run.App.WaitForGitSetupAsync(winget: true);
            return FirstRunFrame(run.App);
        });

    /// <summary>Windows, a Git that does not work because a line of the global configuration is broken: git's own words and the file's line.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task First_run_not_working(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.FirstRunNotWorking, theme, FirstRun(FakeGitSetupProbe.Windows(), FakeGitAnswer.ConfigFault(FakeGitSetupProbe.WindowsGit, FakeGitSetupProbe.DemoGitConfig, 3)), async run =>
        {
            await run.App.WaitForGitSetupAsync();
            await run.App.WaitForAsync(() => run.App.GitSetupPart("GitSetupGitSaid") is { IsEffectivelyVisible: true }, "git's own words on the first-run screen");
            return FirstRunFrame(run.App);
        });

    /// <summary>The confirm that "Install Git with winget" asks before it starts winget in a terminal.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task First_run_winget_confirm(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.FirstRunWingetConfirm, theme, FirstRun(FakeGitSetupProbe.Windows(), FakeGitAnswer.Missing), async run =>
        {
            var app = run.App;
            await app.WaitForGitSetupAsync(winget: true);
            app.Click(app.GitSetupPart("GitSetupWingetButton")!);
            await app.WaitForDialogAsync<ConfirmationViewModel>();
            app.MoveToNeutral();
            return GuideFrame.Card(GuideSteps.DialogCard(app));
        });

    /// <summary>
    /// The status bar with its Git box, which names the Git in use (its tooltip, the path and where
    /// it came from, opens in a window of its own, which a capture of the main window leaves out).
    /// </summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Status_bar_git(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.StatusBarGit, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            await app.WaitForAsync(() => app.FindByAutomationId("GitStatus") is TextBlock { Text.Length: > 0 }, "the Git box's text");
            app.MoveToNeutral();
            return GuideFrame.Of(app.FindByAutomationId("StatusBar"));
        });

    /// <summary>An empty tab: the welcome page with Open, Clone and Create, the recent repositories and the workspaces.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Welcome_page(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.WelcomePage, theme, GuideStart.Welcome, run =>
            Task.FromResult(GuideFrame.Of(Welcome(run.App))));

    /// <summary>The welcome page's clone form, filled in.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Clone_form(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.CloneForm, theme, GuideStart.Welcome, async run =>
        {
            var app = run.App;
            app.Click(app.FindByAutomationId("CloneRepoButton"));
            await app.WaitForAsync(() => app.FindByAutomationId("CloneUrlBox").IsEffectivelyVisible, "the clone form");
            SetText(app, "CloneParentBox", run.Scenario.Projects);
            SetText(app, "CloneUrlBox", "https://github.com/" + GuideScenario.Owner + "/garden-lights.git");
            app.Settle();

            // The form filled in and nothing typed any more: the focus on Clone, as by the pointer, so no caret and no frame.
            app.FindByAutomationId("CloneStartButton").Focus(NavigationMethod.Pointer);
            app.Settle();
            app.MoveToNeutral();
            return GuideFrame.Of(Welcome(app));
        });

    private static GuideStart FirstRun(GitGuidance guidance, FakeGitAnswer answer) =>
        new() { Probe = () => new FakeGitSetupProbe(guidance, answer) };

    /// <summary>The whole window: the first-run screen in the graph's place, and the status bar's Git box.</summary>
    private static GuideFrame FirstRunFrame(ShellDriver app)
    {
        app.MoveToNeutral();
        return GuideFrame.Window;
    }

    private static WelcomeView Welcome(ShellDriver app) =>
        app.Window.GetVisualDescendants().OfType<WelcomeView>().First(view => view.IsEffectivelyVisible);

    /// <summary>Replaces a text box's text by typing, as a user does: the box focused, its text selected, the new text typed.</summary>
    private static void SetText(ShellDriver app, string automationId, string text)
    {
        var box = (TextBox)app.FindByAutomationId(automationId);
        app.Click(box);
        box.SelectAll();
        app.Settle();
        app.Type(text);
    }
}
