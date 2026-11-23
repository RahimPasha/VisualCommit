using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>The pictures of the reference: the About section (<c>reference/about.md</c>).</summary>
public sealed class ReferenceShots
{
    /// <summary>Settings, About: the name and icon, who made it, the version, the license, the notices and the links.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(GuideCamera.Themes), MemberType = typeof(GuideCamera))]
    public Task About_section(GuideTheme theme) =>
        GuideCamera.ShootAsync(GuideShotCatalogue.AboutSection, theme, GuideStart.WeatherStation, async run =>
        {
            var app = run.App;
            await app.OpenSettingsAsync();
            app.ChooseSection(SettingsSection.About);
            await app.WaitForAsync(() => app.FindByAutomationId("SettingsAbout").IsEffectivelyVisible, "the About section");
            app.Settle();
            app.MoveToNeutral();
            return GuideFrame.Of(GuideSteps.DialogCard(app));
        });
}
