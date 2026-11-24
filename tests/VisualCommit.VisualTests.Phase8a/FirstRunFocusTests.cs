using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The keyboard focus on the first-run screen and the settings screen's Git program row, no
/// picture (docs/phase-8a-design.md 5.3, 6; the review before the gate): a button pressed with
/// Enter keeps the focus, with its keyboard frame, while its launch or copy answers late and after
/// it; "Choose git.exe…" and the row's "Browse…", disabled while the file dialog is open and while
/// the chosen file is probed, get the focus back when the pick is cancelled or the file refused.
/// The launcher, the clipboard and the file dialog are the harness's fakes, held until the test
/// lets them answer.
/// </summary>
public sealed class FirstRunFocusTests : Phase8aChecks
{
    public static TheoryData<string> LateButtons => [F.ShowFileId, F.CltId, F.CopyId, F.DownloadId, F.MoreLinkId, F.GuideLinkId];

    [AvaloniaTheory]
    [MemberData(nameof(LateButtons))]
    public async Task A_button_pressed_with_Enter_keeps_the_focus_while_its_launch_or_copy_answers_late(string id)
    {
        using var data = new TempDirectory("phase8a focus");
        var probe = id switch
        {
            F.ShowFileId => new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.ConfigFault(F.WindowsGit, F.DemoGitConfig, 1)),
            F.DownloadId or F.GuideLinkId => new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.Missing),
            _ => new FakeGitSetupProbe(FakeGitSetupProbe.MacOS, FakeGitAnswer.CommandLineToolsMissing),
        };
        using var app = await StartEmptyAsync(data, gitProbe: probe);
        await app.WaitForGitSetupAsync();
        var button = (Button)app.GitSetupPart(id)!;
        var command = (IAsyncRelayCommand)button.Command!;
        var held = new TaskCompletionSource();
        app.Launcher.Hold = held.Task;
        app.Clipboard.Hold = held.Task;
        FocusByTab(app, button, id);

        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => app.Launcher.Requests.Count + app.Clipboard.Texts.Count == 1, $"{id}'s request");

        Assert.True(command.IsRunning, $"{id}'s command does not wait for its answer.");
        AssertFocusedWithFrame(app, button, id, "while its answer is held");
        held.SetResult();
        await app.WaitForAsync(() => !command.IsRunning, $"{id}'s command to end");
        app.Settle();
        AssertFocusedWithFrame(app, button, id, "after its answer");
        Assert.Equal(1, app.Launcher.Requests.Count + app.Clipboard.Texts.Count);
    }

    [AvaloniaFact]
    public async Task Choose_gets_the_focus_back_when_the_pick_is_cancelled()
    {
        using var data = new TempDirectory("phase8a focus");
        using var app = await StartEmptyAsync(data, gitProbe: new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.Missing));
        await app.WaitForGitSetupAsync(choose: F.ChooseGitExe);
        var choose = (Button)app.GitSetupPart(F.ChooseId)!;
        var held = new TaskCompletionSource();
        app.Folders.FileHold = held.Task;
        FocusByTab(app, choose, F.ChooseId);

        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => app.Folders.FileTitles.Count == 1, "the file dialog");

        AssertDisabledWithoutFocus(app, choose, F.ChooseId);
        held.SetResult();
        await app.WaitForAsync(() => !app.GitSetupScreen.ChooseCommand.IsRunning, "Choose to end");
        app.Settle();
        AssertFocusedWithFrame(app, choose, F.ChooseId, "after the cancelled pick");
        Assert.Null(app.GitSetupScreen.ErrorText);
    }

    [AvaloniaFact]
    public async Task Choose_gets_the_focus_back_when_the_chosen_file_is_refused()
    {
        using var data = new TempDirectory("phase8a focus");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.Missing)
            .AnswerFor(F.Old, FakeGitAnswer.TooOld(F.Old, GitSource.Setting, F.TooOldVersion));
        using var app = await StartEmptyAsync(data, gitProbe: probe);
        await app.WaitForGitSetupAsync(choose: F.ChooseGitExe);
        var choose = (Button)app.GitSetupPart(F.ChooseId)!;
        app.Folders.AnswerFile(F.Old);
        var hold = probe.HoldNext();
        FocusByTab(app, choose, F.ChooseId);

        app.PressKey(Key.Enter);
        await hold.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
        app.Settle();

        AssertDisabledWithoutFocus(app, choose, F.ChooseId);
        hold.Release();
        var refusal = string.Format(System.Globalization.CultureInfo.InvariantCulture, F.ChooseTooOldFormat, F.TooOldVersion, F.Minimum);
        await app.WaitForAsync(() => app.GitSetupText(F.ErrorId) == refusal && !app.GitSetupScreen.ChooseCommand.IsRunning, "the refusal");
        app.Settle();
        AssertFocusedWithFrame(app, choose, F.ChooseId, "after the refusal");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_rows_Browse_gets_the_focus_back_when_the_pick_is_cancelled_or_the_file_refused(bool refused)
    {
        using var data = new TempDirectory("phase8a focus");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.RealAt(F.WindowsGit, GitSource.Path))
            .AnswerFor(F.Old, FakeGitAnswer.TooOld(F.Old, GitSource.Setting, F.TooOldVersion));
        using var app = await StartEmptyAsync(data, gitProbe: probe);
        var dialog = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Git);
        await app.WaitForSettingsReadsAsync(dialog);
        var browse = (Button)app.FindByAutomationId(app.TopDialogView!, F.ProgramBrowseId);
        browse.BringIntoView();
        app.Settle();
        var held = new TaskCompletionSource();
        ProbeHold? hold = null;
        if (refused)
        {
            app.Folders.AnswerFile(F.Old);
            hold = probe.HoldNext();
        }
        else
        {
            app.Folders.FileHold = held.Task;
        }

        FocusByTab(app, browse, F.ProgramBrowseId);
        app.PressKey(Key.Enter);
        if (hold is not null)
        {
            await hold.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
            app.Settle();
        }
        else
        {
            await app.WaitForAsync(() => app.Folders.FileTitles.Count == 1, "the file dialog");
        }

        AssertDisabledWithoutFocus(app, browse, F.ProgramBrowseId);
        held.TrySetResult();
        hold?.Release();
        await app.WaitForAsync(() => !dialog.GitProgram.BrowseCommand.IsRunning, "Browse to end");
        app.Settle();
        AssertFocusedWithFrame(app, browse, F.ProgramBrowseId, refused ? "after the refusal" : "after the cancelled pick");
        Assert.Equal(refused, dialog.GitProgram.IsStatusRefusal);
        Assert.Null(dialog.GitProgram.Choice);
    }

    /// <summary>Gives <paramref name="button"/> the focus as Tab does, with the keyboard's frame.</summary>
    private static void FocusByTab(ShellDriver app, Button button, string id)
    {
        Assert.True(button.Focus(NavigationMethod.Tab), $"{id} takes no focus.");
        app.Settle();
        Assert.True(app.FocusedAutomationId() == id && app.FocusVisible, $"{id} is not focused with the keyboard's frame before Enter.");
    }

    /// <summary>The premise of a give-back: the run disabled the button and the focus left it.</summary>
    private static void AssertDisabledWithoutFocus(ShellDriver app, Button button, string id)
    {
        Assert.False(button.IsEffectivelyEnabled, $"{id} is enabled while it runs.");
        Assert.NotEqual(id, app.FocusedAutomationId());
    }

    private static void AssertFocusedWithFrame(ShellDriver app, Button button, string id, string when)
    {
        Assert.True(button.IsEffectivelyEnabled, $"{id} is disabled {when}.");
        Assert.True(app.FocusedAutomationId() == id, $"The focus is on {app.FocusedAutomationId() ?? "nothing"}, not {id}, {when}.");
        Assert.True(app.FocusVisible, $"{id} has the focus without the keyboard's frame {when}.");
        Assert.Equal(id, AutomationProperties.GetAutomationId(button));
    }
}
