using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The first-run screen in the shell, no picture (docs/phase-8a-design.md 5.4): in each state, at
/// 1100×700 and at 1000×560, no text is cut off, and the screen's automation name and help text are
/// the state's title and body, which a screen reader speaks when the focus lands on its first button.
/// One app start per state, the fake probe's answer and guidance the state's.
/// </summary>
public sealed class GitSetupShellTests : Phase8aChecks
{
    public static TheoryData<string> States =>
    [
        "missing on windows", "missing on macos without the tools", "missing on ubuntu", "missing on alpine", "chosen gone", "chosen gone, no candidate",
        "too old on windows", "too old on macos", "config fault", "not started", "macos tools broken", "helper missing",
    ];

    [AvaloniaTheory]
    [MemberData(nameof(States))]
    public async Task Each_state_fits_at_both_sizes_and_names_the_screen_by_its_title_and_body(string state)
    {
        using var data = new TempDirectory("phase8a gitsetup");
        var (probe, helper, title, body, guidance) = Setup(state);
        if (state.StartsWith("chosen", StringComparison.Ordinal))
        {
            new VisualCommit.App.Services.JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { GitExecutable = F.Chosen });
        }

        using var app = await StartEmptyAsync(data, gitProbe: probe, askPassHelper: helper);
        await app.WaitForGitSetupAsync(guidance: guidance);

        var view = app.GitSetupView();
        Assert.Equal(title, AutomationProperties.GetName(view));
        Assert.Equal(body, AutomationProperties.GetHelpText(view));
        Assert.Equal(title, app.GitSetupText(F.TitleId));
        Assert.Equal(body, app.GitSetupText(F.BodyId));
        Assert.False(view.Focusable);
        AssertNoClippedText(app);
        AssertInsideTheScreen(app);

        app.Window.Width = 1000;
        app.Window.Height = 560;
        app.Settle();
        app.MoveToNeutral();
        app.Settle();
        AssertNoClippedText(app);
        AssertInsideTheScreen(app);
    }

    /// <summary>Every part of the screen shown lies inside its width: nothing is pushed out of the graph area, where it would be cut off.</summary>
    private static void AssertInsideTheScreen(ShellDriver app)
    {
        var screen = app.BoundsOf(app.GitSetupView());
        foreach (var part in app.GitSetupView().GetVisualDescendants().OfType<StackPanel>().First().GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible && control.Bounds.Width > 0))
        {
            var bounds = app.BoundsOf(part);
            Assert.True(bounds.Left >= screen.Left - 0.5 && bounds.Right <= screen.Right + 0.5, $"{part.GetType().Name} {part.Name} at {bounds} lies outside the screen at {screen}.");
        }
    }

    private static (FakeGitSetupProbe Probe, string? Helper, string Title, string Body, string Guidance) Setup(string state)
    {
        var windows = FakeGitSetupProbe.Windows();
        var tooOld = string.Format(System.Globalization.CultureInfo.InvariantCulture, F.TooOldTitleFormat, F.TooOldVersion);
        var tooOldBody = string.Format(System.Globalization.CultureInfo.InvariantCulture, F.TooOldBodyFormat, GitVersion.Minimum, F.TooOldVersion);
        return state switch
        {
            "missing on windows" => (new(windows, FakeGitAnswer.Missing), null, F.MissingTitle, F.MissingBodyWindows, F.MissingGuidanceWindows),
            "missing on macos without the tools" => (new(FakeGitSetupProbe.MacOS, FakeGitAnswer.CommandLineToolsMissing), null, F.MissingTitle, F.MissingBodyMacOS + F.MissingBodyMacOSToolsMissing, F.MissingGuidanceMacOSToolsMissing),
            "missing on ubuntu" => (new(FakeGitSetupProbe.Linux(FakeGitSetupProbe.Ubuntu), FakeGitAnswer.Missing), null, F.MissingTitle, F.MissingBodyLinux, string.Format(System.Globalization.CultureInfo.InvariantCulture, F.MissingGuidanceLinuxKnownFormat, F.UbuntuPrettyName)),
            "missing on alpine" => (new(FakeGitSetupProbe.Linux(FakeGitSetupProbe.Alpine), FakeGitAnswer.Missing), null, F.MissingTitle, F.MissingBodyLinux, F.MissingGuidanceLinuxUnknown),
            "chosen gone" => (new(windows, FakeGitAnswer.ChosenMissing(F.WindowsGit)), null, F.ChosenMissingTitle, F.ChosenMissingBody, string.Format(System.Globalization.CultureInfo.InvariantCulture, F.ChosenMissingGuidanceFormat, F.WindowsGit)),
            "chosen gone, no candidate" => (new(windows, FakeGitAnswer.ChosenMissing(null)), null, F.ChosenMissingTitle, F.ChosenMissingBody, F.ChosenMissingGuidanceNone),
            "too old on windows" => (new(windows, FakeGitAnswer.TooOld(F.WindowsGit)), null, tooOld, tooOldBody, F.TooOldGuidanceWindows),
            "too old on macos" => (new(FakeGitSetupProbe.MacOS, FakeGitAnswer.TooOld(F.UnixGit)), null, tooOld, tooOldBody, F.TooOldGuidanceMacOS),
            "config fault" => (new(windows, FakeGitAnswer.ConfigFault(F.WindowsGit, F.DemoGitConfig, 1)), null, F.NotWorkingTitle, F.NotWorkingBody, string.Format(System.Globalization.CultureInfo.InvariantCulture, F.ConfigFaultGuidanceFormat, 1, F.DemoGitConfig)),
            "not started" => (new(windows, FakeGitAnswer.NotStarted(F.WindowsGit)), null, F.NotWorkingTitle, string.Format(System.Globalization.CultureInfo.InvariantCulture, F.NotStartedBodyFormat, F.NotStartedMessage), F.NotWorkingGuidanceWindows),
            "macos tools broken" => (new(FakeGitSetupProbe.MacOS, FakeGitAnswer.NotWorking(F.UnixGit, F.ToolsBrokenWords, 1)), null, F.NotWorkingTitle, F.NotWorkingBody, F.NotWorkingGuidanceMacOSToolsBroken),
            _ => (new(windows, FakeGitAnswer.RealAt(F.WindowsGit)), F.MissingHelper, F.HelperMissingTitle, F.HelperMissingBody, F.HelperMissingGuidance),
        };
    }
}
