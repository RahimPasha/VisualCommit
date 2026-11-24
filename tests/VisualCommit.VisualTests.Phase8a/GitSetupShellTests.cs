using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
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

    /// <summary>
    /// A hovered link's text takes the accent made for a hovered background, and the accent again
    /// once the pointer has left (design 1.5: the accent is never text on a hover).
    /// </summary>
    [AvaloniaFact]
    public async Task A_hovered_links_text_takes_the_accent_made_for_the_hover_background()
    {
        using var data = new TempDirectory("phase8a gitsetup");
        var (probe, helper, _, _, guidance) = Setup("missing on macos without the tools");
        using var app = await StartEmptyAsync(data, gitProbe: probe, askPassHelper: helper);
        await app.WaitForGitSetupAsync(guidance: guidance);

        foreach (var id in new[] { F.MoreLinkId, F.GuideLinkId })
        {
            var link = (Button)app.GitSetupPart(id)!;
            var text = link.GetVisualDescendants().OfType<TextBlock>().First(block => block.IsEffectivelyVisible);
            Assert.Equal(F.AccentDark, ((ISolidColorBrush)text.Foreground!).Color);

            app.MoveMouse(link);
            app.Settle();
            Assert.True(link.IsPointerOver, $"{id} is not hovered.");
            Assert.Equal(F.AccentOnRowDark, ((ISolidColorBrush)text.Foreground!).Color);

            app.MoveToNeutral();
            app.Settle();
            Assert.Equal(F.AccentDark, ((ISolidColorBrush)text.Foreground!).Color);
        }
    }

    /// <summary>
    /// The texts of the screen that change are polite live regions (D435; design 9.3.9, step 21): a
    /// new text raises one Name change on its peer, which Windows speaks. "Check again"'s text says
    /// "Checking…" while its search runs and "Check again" once it ends; the title says the new
    /// state's title once a search ends on another state, and nothing when it ends on the same one.
    /// Before, neither had a live setting, and nothing of a search was spoken.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_agains_text_and_the_title_are_polite_live_regions_that_speak_each_change()
    {
        using var data = new TempDirectory("phase8a gitsetup");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.Missing, FakeGitAnswer.TooOld(F.WindowsGit));
        using var app = await StartEmptyAsync(data, gitProbe: probe);
        await app.WaitForGitSetupAsync(guidance: F.MissingGuidanceWindows);
        var tooOld = string.Format(System.Globalization.CultureInfo.InvariantCulture, F.TooOldTitleFormat, F.TooOldVersion);
        var title = (TextBlock)app.GitSetupPart(F.TitleId)!;
        var button = (Button)app.GitSetupPart(F.CheckAgainId)!;
        var buttonText = button.GetVisualDescendants().OfType<TextBlock>().Single();
        var titleNames = NameChanges(title);
        var buttonNames = NameChanges(buttonText);
        Assert.Equal(AutomationLiveSetting.Polite, ControlAutomationPeer.CreatePeerForElement(title).GetLiveSetting());
        Assert.Equal(AutomationLiveSetting.Polite, ControlAutomationPeer.CreatePeerForElement(buttonText).GetLiveSetting());
        Assert.Equal(F.CheckAgain, ControlAutomationPeer.CreatePeerForElement(buttonText).GetName());

        // A search that ends on another state: "Checking…" while it runs, then the new title.
        var hold = probe.HoldNext();
        app.Click(button);
        await hold.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
        app.Settle();
        Assert.Equal([((object?)F.CheckAgain, (object?)F.Checking)], buttonNames);
        Assert.Empty(titleNames);
        hold.Release();
        await CheckAgainEndedAsync(app, probe, 2, tooOld);

        Assert.Equal([((object?)F.CheckAgain, (object?)F.Checking), (F.Checking, F.CheckAgain)], buttonNames);
        Assert.Equal([((object?)F.MissingTitle, (object?)tooOld)], titleNames);

        // A search that ends on the same state: its button speaks, the title does not.
        app.Click(button);
        await CheckAgainEndedAsync(app, probe, 3, tooOld);

        Assert.Equal(4, buttonNames.Count);
        Assert.Single(titleNames);
    }

    /// <summary>
    /// The error line, which says Choose's "Checking…" and then why the file was refused, is a polite
    /// live region (D435): each of its texts raises one Name change on its peer. Before, it had no
    /// live setting, and a refused Choose was silent.
    /// </summary>
    [AvaloniaFact]
    public async Task The_error_line_is_a_polite_live_region_that_speaks_Chooses_checking_and_its_refusal()
    {
        using var data = new TempDirectory("phase8a gitsetup");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(wingetPath: null), FakeGitAnswer.Missing)
            .AnswerFor(F.Old, FakeGitAnswer.TooOld(F.Old, GitSource.Setting, F.TooOldVersion));
        using var app = await StartEmptyAsync(data, gitProbe: probe);
        await app.WaitForGitSetupAsync(guidance: F.MissingGuidanceWindows, choose: F.ChooseGitExe);
        var error = app.GitSetupView().GetVisualDescendants().OfType<TextBlock>().Single(block => AutomationProperties.GetAutomationId(block) == F.ErrorId);
        Assert.False(error.IsEffectivelyVisible);
        var names = NameChanges(error);
        Assert.Equal(AutomationLiveSetting.Polite, ControlAutomationPeer.CreatePeerForElement(error).GetLiveSetting());

        app.Folders.AnswerFile(F.Old);
        var hold = probe.HoldNext();
        app.Click(app.GitSetupPart(F.ChooseId)!);
        await hold.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
        app.Settle();
        Assert.Equal(F.Checking, app.GitSetupText(F.ErrorId));
        hold.Release();
        await app.WaitForAsync(() => app.GitSetupText(F.ErrorId) is { } text && text != F.Checking, "the refusal");
        app.Settle();

        Assert.Equal([F.Checking, app.GitSetupText(F.ErrorId)], names.Select(change => change.New as string));
    }

    /// <summary>Records the Name changes the peer of <paramref name="text"/> raises, as the Windows bridge receives them.</summary>
    private static List<(object? Old, object? New)> NameChanges(TextBlock text)
    {
        var names = new List<(object? Old, object? New)>();
        ControlAutomationPeer.CreatePeerForElement(text).PropertyChanged += (_, change) =>
        {
            if (change.Property == AutomationElementIdentifiers.NameProperty)
            {
                names.Add((change.OldValue, change.NewValue));
            }
        };

        return names;
    }

    /// <summary>Waits until the <paramref name="lookups"/>th search has ended on the state titled <paramref name="title"/>, "Check again" enabled again.</summary>
    private static async Task CheckAgainEndedAsync(ShellDriver app, FakeGitSetupProbe probe, int lookups, string title)
    {
        await app.WaitForAsync(
            () => probe.Lookups.Count == lookups && !app.Session.Shell.CheckGitAgainCommand.IsRunning
                && app.GitSetupText(F.TitleId) == title && app.GitSetupPart(F.CheckAgainId) is { IsEffectivelyEnabled: true },
            $"Check again to end on \"{title}\"");
        app.Settle();
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
