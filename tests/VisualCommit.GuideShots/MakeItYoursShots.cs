using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>
/// The pictures of "Make it yours": the settings screen's General and Git sections and the command
/// palette (<c>settings.md</c>).
/// </summary>
public sealed class MakeItYoursShots
{
    /// <summary>Settings, General: the theme, the editor, the merge tool and auto-fetch.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Settings_general(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.SettingsGeneral, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            await app.OpenSettingsAsync();
            app.MoveToNeutral();
            return GuideFrame.Of(GuideSteps.DialogCard(app));
        });

    /// <summary>Settings, Git: the identity and pull's settings, global and this repository's, and the Git program row.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Settings_git(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.SettingsGit, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            await app.OpenSettingsAsync();
            app.ChooseSection(SettingsSection.Git);
            await app.WaitForSettingsReadsAsync();
            app.MoveToNeutral();
            return GuideFrame.Of(GuideSteps.DialogCard(app));
        });

    /// <summary>The command palette with "branch" typed: the commands and the branches that match.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task Palette(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.Palette, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            app.PressPalette();
            await app.WaitForAsync(() => app.Session.Shell.OpenPalette is not null, "the command palette");
            app.TypeInPalette("branch");
            await app.WaitForIdleAsync();
            app.Settle();
            app.MoveToNeutral();
            return GuideFrame.Of(app.FindByAutomationId("CommandPaletteCard"));
        });
}
