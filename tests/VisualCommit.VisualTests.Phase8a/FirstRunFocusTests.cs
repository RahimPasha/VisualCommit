using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Git;
using VisualCommit.Hosting;
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
/// the chosen file is probed, get the focus back when the pick is cancelled or the file refused;
/// the row's "Use the Git on the PATH", which hides itself with the choice it drops, gives the
/// focus to Browse when it was pressed by the keys (13.1, ruling 101), and the first-run screen's
/// twin, pressed by the keys, gives it with the frame where its search puts it; the Accounts
/// section's netrc note's Dismiss, which hides itself with the note, gives it to the section's
/// button. The launcher, the clipboard, the file dialog and the search are the harness's fakes,
/// held until the test lets them answer.
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

    /// <summary>
    /// The settings row's "Use the Git on the PATH", or "Use the Git it found" while the git in use came
    /// from a usual install folder, pressed by Enter or Space with the keyboard's frame, drops the
    /// choice and so hides itself; the row's Browse then has the focus, with the frame, and is
    /// described by the row's path and note as the focus comes (D462, D463 (3)). Before this the
    /// focus was on nothing.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(Key.Enter, GitSource.Path)]
    [InlineData(Key.Space, GitSource.Path)]
    [InlineData(Key.Enter, GitSource.InstallFolder)]
    public async Task The_rows_drop_button_pressed_by_the_keys_gives_the_focus_to_Browse_with_the_frame(Key key, GitSource source)
    {
        using var data = new TempDirectory("phase8a focus");
        using var app = await StartWithAChoiceOnTheRowAsync(data, source);
        var dialog = app.SettingsDialog!;
        var usePath = (Button)app.FindByAutomationId(app.TopDialogView!, F.ProgramUsePathId);
        var browse = (Button)app.FindByAutomationId(app.TopDialogView!, F.ProgramBrowseId);
        Assert.Equal(source == GitSource.Path ? F.UseTheGitOnThePath : F.UseTheGitItFound, AutomationProperties.GetName(usePath));
        FocusByTab(app, usePath, F.ProgramUsePathId);
        using var focus = ClientWalk.RecordFocus(app.Window);

        app.PressKey(key);
        await app.WaitForAsync(() => dialog.GitProgram.Choice is null, "the choice to be dropped");
        app.Settle();

        Assert.False(usePath.IsEffectivelyVisible, $"{F.ProgramUsePathId} still shows after {key}.");
        AssertFocusedWithFrame(app, browse, F.ProgramBrowseId, $"after {key} on {F.ProgramUsePathId}");
        var note = source == GitSource.Path ? F.FoundOnPathNote : OperatingSystem.IsWindows() ? F.FoundInGitForWindowsFolderNote : F.FoundInUsualFolderNote;
        Assert.Equal(new FocusEvent(F.ProgramBrowseId, F.Browse, $"{F.WindowsGit}. {note}"), focus.Last);
    }

    /// <summary>
    /// A click on the settings row's "Use the Git on the PATH" drops the choice and hides the button as
    /// Enter does, and gives the focus to nobody: a mouse press arms nothing (ruling 101), so the focus
    /// stays where the hidden button left it, as before.
    /// </summary>
    [AvaloniaFact]
    public async Task A_click_on_the_rows_drop_button_gives_the_focus_to_nobody()
    {
        using var data = new TempDirectory("phase8a focus");
        using var app = await StartWithAChoiceOnTheRowAsync(data, GitSource.Path);
        var dialog = app.SettingsDialog!;
        var usePath = (Button)app.FindByAutomationId(app.TopDialogView!, F.ProgramUsePathId);

        app.Click(usePath);
        await app.WaitForAsync(() => dialog.GitProgram.Choice is null, "the choice to be dropped");
        app.Settle();

        Assert.False(usePath.IsEffectivelyVisible, $"{F.ProgramUsePathId} still shows after the click.");
        Assert.True(app.FocusedElement is null, $"After the click the focus is on {app.DescribeFocus()}, not where the hidden button left it (nothing).");
    }

    /// <summary>
    /// The Accounts section's netrc note (F14's set-up: signed in with "Use for git over HTTPS" on, git
    /// at 2.43.0, an empty <c>_netrc</c> in the session's home): its Dismiss, pressed by Enter with the
    /// keyboard's frame, hides the note's row with itself; the section's button then has the focus,
    /// with the frame, where the screen puts its first focus (D462). Before this the focus was on
    /// nothing.
    /// </summary>
    [AvaloniaFact]
    public async Task The_netrc_notes_Dismiss_pressed_by_Enter_gives_the_focus_to_the_Accounts_button_with_the_frame()
    {
        var copy = await Scenarios.HostingAsync();
        await using var served = await Scenarios.ServeHostingAsync(copy, inProcess: true, ct: TestContext.Current.CancellationToken, ownsCopy: true);
        using var data = new TempDirectory("phase8a focus");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.RealAt(F.WindowsGit, GitSource.Path, F.NetrcVersion));
        using var app = ShellDriver.Start(data.Path, hosting: new HostingOptions(served.GitHub.Endpoints, served.GitHub.CreateHandler), gitProbe: probe);
        await app.WaitUntilReadyAsync();
        app.MoveToNeutral();
        await app.SignInAsync(served.OctoToken, TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(GitIsolation.SessionHomeFolder(data.Path), "_netrc"), string.Empty);
        var dialog = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Accounts);
        await app.WaitForSettingsReadsAsync(dialog);
        await app.WaitForAsync(() => app.FindByAutomationId(app.TopDialogView!, F.NetrcNoteId).IsEffectivelyVisible, "the netrc note");
        var dismiss = (Button)app.FindByAutomationId(app.TopDialogView!, F.NetrcDismissId);
        var accounts = (Button)app.FindByAutomationId(app.TopDialogView!, A.NavAccountsId);
        dismiss.BringIntoView();
        app.Settle();
        FocusByTab(app, dismiss, F.NetrcDismissId);

        app.PressKey(Key.Enter);
        app.Settle();

        Assert.False(dismiss.IsEffectivelyVisible, $"{F.NetrcDismissId} still shows after Enter.");
        Assert.False(app.FindByAutomationId(app.TopDialogView!, F.NetrcNoteId).IsEffectivelyVisible, "The netrc note still shows after Enter on Dismiss.");
        AssertFocusedWithFrame(app, accounts, A.NavAccountsId, $"after Enter on {F.NetrcDismissId}");
    }

    /// <summary>
    /// The first-run screen's twin, "Use the Git on the PATH" on a chosen git that is too old, pressed
    /// by Enter with the keyboard's frame, is disabled while its search runs and gone once it has ended
    /// (the setting removed, no state offers it), and the search's end places the focus, with the
    /// keyboard's frame, since the pressed button had it (D462): on "Check again" while the screen
    /// still shows, described by the title of the state the search ended on (D463 (3)), else, the
    /// screen gone, on the graph area's target, the welcome page's Open. The search is held, as a real
    /// one, which runs git, never answers within the key press. Before this the focus came to either
    /// place without the frame, which the search's end gave to Check again only when Check again
    /// itself had been pressed with it.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_first_run_screens_drop_button_pressed_by_Enter_gives_the_focus_where_the_search_puts_it_with_the_frame(bool found)
    {
        using var data = new TempDirectory("phase8a focus");
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { GitExecutable = F.Chosen });
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), found ? FakeGitAnswer.RealAt(F.WindowsGit, GitSource.Path) : FakeGitAnswer.Missing)
            .AnswerFor(F.Chosen, FakeGitAnswer.TooOld(F.Chosen, GitSource.Setting, F.TooOldVersion));
        using var app = await StartEmptyAsync(data, gitProbe: probe);
        await app.WaitForGitSetupAsync(choose: F.ChooseGitExe);
        var usePath = (Button)app.GitSetupPart(F.UsePathId)!;
        Assert.Equal(F.UseTheGitOnThePath, AutomationProperties.GetName(usePath));
        FocusByTab(app, usePath, F.UsePathId);
        using var focus = ClientWalk.RecordFocus(app.Window);
        var hold = probe.HoldNext();

        app.PressKey(Key.Enter);
        await hold.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
        app.Settle();
        AssertDisabledWithoutFocus(app, usePath, F.UsePathId);
        hold.Release();
        await app.WaitForAsync(
            () => probe.Lookups.Count == 2 && !app.GitSetupScreen.UsePathCommand.IsRunning && !app.Session.Shell.CheckGitAgainCommand.IsRunning && app.ShowsGitSetupView() != found,
            found ? "the search to find git and the screen to go" : "the search to end on Missing");
        app.Settle();

        Assert.Null(app.Session.Settings.Current.GitExecutable);
        if (found)
        {
            var target = app.Window.GetVisualDescendants().OfType<CommitGraphView>().Single(view => view.IsEffectivelyVisible).GraphAreaFocusTarget();
            Assert.True(target is not null && ReferenceEquals(app.FocusedElement, target), $"With the screen gone the focus is on {app.DescribeFocus()}, not the graph area's target.");
            Assert.True(app.FocusVisible, "With the screen gone the graph area's target has the focus without the keyboard's frame.");
        }
        else
        {
            Assert.Null(app.GitSetupPart(F.UsePathId));
            Assert.True(app.FocusedAutomationId() == F.CheckAgainId, $"With the screen on Missing the focus is on {app.DescribeFocus()}, not {F.CheckAgainId}.");
            Assert.True(app.FocusVisible, $"With the screen on Missing {F.CheckAgainId} has the focus without the keyboard's frame.");
            Assert.Equal(new FocusEvent(F.CheckAgainId, F.CheckAgain, F.MissingTitle), focus.Last);
        }
    }

    /// <summary>
    /// The settings screen's Git section on an empty tab whose git, at <see cref="F.WindowsGit"/>, came
    /// from <paramref name="source"/>, with <see cref="F.Chosen"/> taken by Browse as the row's
    /// choice, so that the row's drop button shows, in view. Browse's probe of the chosen file is
    /// answered made up, without running git, so that the app's 10 s limit on it is never what a
    /// busy machine meets here.
    /// </summary>
    private static async Task<ShellDriver> StartWithAChoiceOnTheRowAsync(TempDirectory data, GitSource source)
    {
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.RealAt(F.WindowsGit, source))
            .AnswerFor(F.Chosen, FakeGitAnswer.AvailableAt(F.Chosen));
        var app = await StartEmptyAsync(data, gitProbe: probe);
        try
        {
            var dialog = await app.OpenSettingsAsync(SettingsOpening.Key);
            app.ChooseSection(SettingsSection.Git);
            await app.WaitForSettingsReadsAsync(dialog);
            app.Folders.AnswerFile(F.Chosen);
            await dialog.GitProgram.BrowseCommand.ExecuteAsync(null);
            app.Settle();
            Assert.Equal(F.Chosen, dialog.GitProgram.Choice);
            var usePath = app.FindByAutomationId(app.TopDialogView!, F.ProgramUsePathId);
            usePath.BringIntoView();
            app.Settle();
            Assert.True(usePath.IsEffectivelyVisible, $"{F.ProgramUsePathId} does not show with a choice.");
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
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
