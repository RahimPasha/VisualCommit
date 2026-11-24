using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The outcome said where the keyboard focus lands (D463 (3); docs/phase-8a-design.md 13.1, ruling
/// 100), no picture. A line that shows with its text in one step has no node on Windows when its
/// text comes (D455), so its live setting reaches no reader; the button that gets the focus back
/// after that outcome is described by it instead (its help text), which a reader is told with the
/// focus event, as a form's field is described by its error: the notice's Try again by its status,
/// the first-run screen's Choose by the error line and its Check again by the title, the settings
/// row's Browse by its status, else by the row's path and note (ruling 101). Each test takes the
/// focus events as Windows' bridge takes them (<see cref="ClientWalk.RecordFocus"/>) and the peers
/// as a client reaches them (<see cref="ClientWalk"/>); the names stay the buttons' own.
/// </summary>
public sealed class OutcomeAtFocusTests : Phase8aChecks
{
    /// <summary>
    /// Try again has no help text as the notice opens and while its first try is held; once the try
    /// went unanswered, the focus comes back to it with "It still did not answer." as its help text,
    /// its name still "Try again".
    /// </summary>
    [AvaloniaFact]
    public async Task Try_again_is_described_by_the_status_when_the_focus_comes_back_after_an_unanswered_try()
    {
        using var host = InstanceNoticeHost.Show(GuidancePlatform.Windows, I.WindowsFolder, ThemeVariant.Dark);
        var tryAgain = host.Find<Button>(I.TryAgainId);
        var status = host.Find<TextBlock>(I.StatusId);
        await host.WaitForAsync(() => ReferenceEquals(host.Focused, tryAgain), "Try again to take the keyboard focus");
        Assert.Equal((I.TryAgain, string.Empty), NameAndHelpText(host.Window, I.TryAgainId));
        using var focus = ClientWalk.RecordFocus(host.Window);

        host.PressKey(Key.Enter);
        await host.WaitForAsync(() => host.Try.IsHeld, "the try to be held");
        Assert.Equal((I.Trying, string.Empty), NameAndHelpText(host.Window, I.TryAgainId));
        host.Try.Answer(NoticeRetry.NoAnswer);
        await host.WaitForAsync(() => status.IsEffectivelyVisible && tryAgain.IsEffectivelyEnabled && ReferenceEquals(host.Focused, tryAgain), "the status to show and Try again to have the focus again");
        InstanceNoticeHost.Settle();

        Assert.Equal(new FocusEvent(I.TryAgainId, I.TryAgain, I.StillNoAnswer), focus.Last);
        Assert.Equal((I.TryAgain, I.StillNoAnswer), NameAndHelpText(host.Window, I.TryAgainId));
    }

    /// <summary>
    /// Choose has no help text before any Choose; while the chosen file is probed it is described by
    /// the error line's "Checking…"; once the file is refused, the focus comes back to it with the
    /// refusal as its help text, its name still "Choose git.exe…".
    /// </summary>
    [AvaloniaFact]
    public async Task Choose_is_described_by_the_error_line_and_gets_the_focus_back_with_the_refusal()
    {
        using var data = new TempDirectory("phase8a outcome");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(wingetPath: null), FakeGitAnswer.Missing)
            .AnswerFor(F.Old, FakeGitAnswer.TooOld(F.Old, GitSource.Setting, F.TooOldVersion));
        using var app = await StartEmptyAsync(data, gitProbe: probe);
        await app.WaitForGitSetupAsync(guidance: F.MissingGuidanceWindows, choose: F.ChooseGitExe);
        var choose = (Button)app.GitSetupPart(F.ChooseId)!;
        Assert.Equal((F.ChooseGitExe, string.Empty), NameAndHelpText(app.Window, F.ChooseId));
        app.Folders.AnswerFile(F.Old);
        var hold = probe.HoldNext();
        FocusByTab(app, choose, F.ChooseId);
        using var focus = ClientWalk.RecordFocus(app.Window);

        app.PressKey(Key.Enter);
        await hold.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
        app.Settle();
        Assert.Equal((F.ChooseGitExe, F.Checking), NameAndHelpText(app.Window, F.ChooseId));
        hold.Release();
        var refusal = string.Format(CultureInfo.InvariantCulture, F.ChooseTooOldFormat, F.TooOldVersion, F.Minimum);
        await app.WaitForAsync(
            () => app.GitSetupText(F.ErrorId) == refusal && !app.GitSetupScreen.ChooseCommand.IsRunning && app.FocusedAutomationId() == F.ChooseId,
            "the refusal, with the focus back on Choose");
        app.Settle();

        Assert.Equal(new FocusEvent(F.ChooseId, F.ChooseGitExe, refusal), focus.Last);
        Assert.Equal((F.ChooseGitExe, refusal), NameAndHelpText(app.Window, F.ChooseId));
    }

    /// <summary>
    /// Check again is described by the screen's title; a search by Enter that ends on another state
    /// gives the focus back to it with that state's title as its help text, its name "Check again"
    /// again.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_again_gets_the_focus_back_described_by_the_title_of_the_state_its_search_ended_on()
    {
        using var data = new TempDirectory("phase8a outcome");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.Missing, FakeGitAnswer.TooOld(F.WindowsGit));
        using var app = await StartEmptyAsync(data, gitProbe: probe);
        await app.WaitForGitSetupAsync(guidance: F.MissingGuidanceWindows);
        var tooOld = string.Format(CultureInfo.InvariantCulture, F.TooOldTitleFormat, F.TooOldVersion);
        var button = (Button)app.GitSetupPart(F.CheckAgainId)!;
        Assert.Equal((F.CheckAgain, F.MissingTitle), NameAndHelpText(app.Window, F.CheckAgainId));
        FocusByTab(app, button, F.CheckAgainId);
        using var focus = ClientWalk.RecordFocus(app.Window);

        var hold = probe.HoldNext();
        app.PressKey(Key.Enter);
        await hold.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
        app.Settle();
        hold.Release();
        await app.WaitForAsync(
            () => probe.Lookups.Count == 2 && !app.Session.Shell.CheckGitAgainCommand.IsRunning && app.GitSetupText(F.TitleId) == tooOld
                && app.FocusedAutomationId() == F.CheckAgainId,
            $"the search to end on \"{tooOld}\", with the focus back on Check again");
        app.Settle();

        Assert.Equal(new FocusEvent(F.CheckAgainId, F.CheckAgain, tooOld), focus.Last);
        Assert.Equal((F.CheckAgain, tooOld), NameAndHelpText(app.Window, F.CheckAgainId));
    }

    /// <summary>
    /// The settings row's Browse is described by the row at all times (D463 (3); ruling 101): by its
    /// status while it has one, else by the row's path and note. Tab onto it reads the git the row
    /// shows and where it came from; while the chosen file is probed it is described by the row's
    /// "Checking…"; once the file is refused, the focus comes back to it with the refusal; once a
    /// second file passes, the focus comes back to it with that file and its note; after "Use the Git
    /// on the PATH" it is described by the row's path and note again. Its name stays "Browse…". The
    /// row's lines (GitProgramPath, GitProgramNote, GitProgramStatus) are no live regions and are not
    /// what a reader is told when the focus comes back: before this, the focus event after a Browse
    /// that passed said "Browse…" alone.
    /// </summary>
    [AvaloniaFact]
    public async Task The_rows_Browse_is_described_by_its_status_or_else_by_the_rows_path_and_note_when_the_focus_comes_to_it()
    {
        using var data = new TempDirectory("phase8a outcome");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.RealAt(F.WindowsGit, GitSource.Path))
            .AnswerFor(F.Old, FakeGitAnswer.TooOld(F.Old, GitSource.Setting, F.TooOldVersion))
            .AnswerFor(F.Chosen, FakeGitAnswer.AvailableAt(F.Chosen));
        using var app = await StartEmptyAsync(data, gitProbe: probe);
        var dialog = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Git);
        await app.WaitForSettingsReadsAsync(dialog);
        var browse = (Button)app.FindByAutomationId(app.TopDialogView!, F.ProgramBrowseId);
        browse.BringIntoView();
        app.Settle();
        var inUse = $"{F.WindowsGit}. {F.FoundOnPathNote}";
        Assert.Equal((F.Browse, inUse), NameAndHelpText(app.Window, F.ProgramBrowseId));
        app.Folders.AnswerFile(F.Old);
        var hold = probe.HoldNext();
        using var focus = ClientWalk.RecordFocus(app.Window);
        FocusByTab(app, browse, F.ProgramBrowseId);
        Assert.Equal(new FocusEvent(F.ProgramBrowseId, F.Browse, inUse), focus.Last);

        app.PressKey(Key.Enter);
        await hold.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
        app.Settle();
        Assert.Equal((F.Browse, F.Checking), NameAndHelpText(app.Window, F.ProgramBrowseId));
        hold.Release();
        var refusal = string.Format(CultureInfo.InvariantCulture, F.ChooseTooOldFormat, F.TooOldVersion, F.Minimum);
        await app.WaitForAsync(
            () => !dialog.GitProgram.BrowseCommand.IsRunning && dialog.GitProgram.StatusText == refusal && app.FocusedAutomationId() == F.ProgramBrowseId,
            "the refusal, with the focus back on Browse");
        app.Settle();

        Assert.Equal(new FocusEvent(F.ProgramBrowseId, F.Browse, refusal), focus.Last);
        Assert.Equal((F.Browse, refusal), NameAndHelpText(app.Window, F.ProgramBrowseId));

        // A second Browse by Enter, a file that passes: the focus comes back with that file and its
        // note. The probe's made-up answer is held, as a real probe, which runs the file, never
        // answers within the key press: Browse is disabled meanwhile and the focus leaves it.
        app.Folders.AnswerFile(F.Chosen);
        var refused = focus.Events.Count;
        hold = probe.HoldNext();
        app.PressKey(Key.Enter);
        await hold.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
        app.Settle();
        Assert.Equal((F.Browse, F.Checking), NameAndHelpText(app.Window, F.ProgramBrowseId));
        hold.Release();
        await app.WaitForAsync(
            () => !dialog.GitProgram.BrowseCommand.IsRunning && dialog.GitProgram.Choice == F.Chosen && app.FocusedAutomationId() == F.ProgramBrowseId && focus.Events.Count > refused,
            "the chosen file, with the focus back on Browse");
        app.Settle();
        var chosen = $"{F.Chosen}. {F.ChosenHereNote}{F.NextStartSuffix}";

        Assert.True(app.FocusVisible, "Browse has the focus without the keyboard's frame after the Browse that passed.");
        Assert.Equal(new FocusEvent(F.ProgramBrowseId, F.Browse, chosen), focus.Last);
        Assert.Equal((F.Browse, chosen), NameAndHelpText(app.Window, F.ProgramBrowseId));

        // "Use the Git on the PATH" drops the choice: the row's path and note again.
        app.Click(app.FindByAutomationId(app.TopDialogView!, F.ProgramUsePathId));
        await app.WaitForAsync(() => dialog.GitProgram.Choice is null, "the choice to be dropped");
        app.Settle();

        Assert.Equal((F.Browse, inUse), NameAndHelpText(app.Window, F.ProgramBrowseId));
    }

    /// <summary>The name and help text of the element <paramref name="automationId"/>, as a client walking down from <paramref name="window"/> reaches it now.</summary>
    private static (string Name, string HelpText) NameAndHelpText(TopLevel window, string automationId)
    {
        var peer = ClientWalk.Find(ClientWalk.Reach(window), automationId)
            ?? throw new InvalidOperationException($"A client walking down from the window does not reach {automationId}.");
        return (peer.GetName(), peer.GetHelpText());
    }

    /// <summary>Gives <paramref name="button"/> the focus as Tab does, with the keyboard's frame.</summary>
    private static void FocusByTab(ShellDriver app, Button button, string id)
    {
        Assert.True(button.Focus(NavigationMethod.Tab), $"{id} takes no focus.");
        app.Settle();
        Assert.True(app.FocusedAutomationId() == id && app.FocusVisible, $"{id} is not focused with the keyboard's frame before Enter.");
    }
}
