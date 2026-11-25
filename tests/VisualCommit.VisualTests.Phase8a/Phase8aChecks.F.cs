// Part F of phase 8a's walk-through, first run (docs/test-reports/phase-8a.md, "Part F"; docs/phase-8a-design.md
// 5.3, 5.5; R4, D405, D423, D424, D449, D453): the first-run screen in each state and on each platform, its
// winget offer, the download, "Choose git.exe…", "Check again", the Command Line Tools, the distribution's
// package, the missing helper, the settings screen's Git program row and the Accounts section's netrc note.
// Fifteen methods, fifteen starts, as the report's methods table has them; methods 1, 4, 5 and 6 are the
// twins of the real window's launches 7 to 10. Every start but method 6's gives a fake probe; method 6's
// searches the machine for git, its guidance fixed. Nothing is started: the launcher is the harness's
// fake. The helpers are in Phase8aChecks.F.Helpers.cs.
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Settings;
using VisualCommit.Core.Git;
using VisualCommit.Hosting;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>Part F: first run (docs/test-reports/phase-8a.md; design 5.5).</summary>
public sealed partial class Phase8aChecksF : Phase8aChecks
{
    private static readonly (string, string)[] MissingWithWinget =
        [(F.WingetId, F.InstallWithWinget), (F.DownloadId, F.DownloadGitForWindows), (F.ChooseId, F.ChooseGitExe), (F.CheckAgainId, F.CheckAgain)];

    private static readonly (string, string)[] GuideOnly = [(F.GuideLinkId, F.HowVisualCommitFindsGit)];

    private static readonly (string, string)[] MoreAndGuide = [(F.MoreLinkId, F.OtherWaysToInstallGit), (F.GuideLinkId, F.HowVisualCommitFindsGit)];

    /// <summary>
    /// Method 1, F1 dark, F2 (the confirm, Cancel), F13, F4 (the twin of the real window's launch 7):
    /// one tab restored on a fresh "graph" while git is missing; the screen, the winget confirm and
    /// its Cancel, the screen by keys and Enter on "Check again", then "Check again" finding git.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_F1_F2_F13_F4_missing_offers_keys_and_found_again()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        var exact = await repo.ExactStateAsync();
        var tools = new HeldMergeTools();
        tools.Release();
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.Missing, FakeGitAnswer.Missing, FakeGitAnswer.RealAt(F.WindowsGit, GitSource.Path));
        using var app = await StartFirstRunAsync(data, probe, [repo.Path], tools: tools.Catalogues);

        // F1 step 1.
        await app.WaitForGitSetupAsync(guidance: F.MissingGuidanceWindows, choose: F.ChooseGitExe, winget: true);
        Picture(app, F.F1aMissingWindows);
        AssertF1(app, probe, repo.Path, dark: true);
        Assert.Null(SettingsText(app));

        // F2 step 2: the confirm.
        app.Click(app.GitSetupPart(F.WingetId)!);
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        Picture(app, F.F2aWingetConfirm);
        AssertConfirm(app, F.InstallConfirmTitle, F.InstallConfirmDetail, F.Install);
        Assert.Empty(app.Launcher.Requests);

        // F2 step 3: Cancel.
        await CloseConfirmAsync(app, "CancelButton");
        Assert.Empty(app.Launcher.Requests);
        AssertNotLogged(app, "Started in a terminal:");
        AssertScreen(app, F.MissingTitle, F.MissingBodyWindows, null, null, F.MissingGuidanceWindows, null);
        AssertGitStatus(app, F.GitNotFound, null, F.SetUpGit);
        Assert.Single(probe.Lookups);

        // F13 step 4: F6 once without looking, then while the winget button has not the focus, at most four.
        var presses = 0;
        do
        {
            app.PressF6();
            app.Settle();
            presses++;
        }
        while (app.FocusedAutomationId() != F.WingetId && presses < F.MostF6Presses);
        Assert.True(presses >= 1);
        Assert.Equal(F.WingetId, app.FocusedAutomationId());
        Assert.True(HasFocusVisible(app), "The winget button has no keyboard outline.");
        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Empty(app.Launcher.Requests);
        Assert.Single(probe.Lookups);

        // F13 steps 5 to 7: Tab through the buttons; no Tab acts.
        foreach (var id in F.FocusOrderWithWinget.Skip(1))
        {
            app.PressKey(Key.Tab);
            app.Settle();
            Assert.Equal(id, app.FocusedAutomationId());
            Assert.True(HasFocusVisible(app), $"{id} has no keyboard outline.");
        }

        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Empty(app.Launcher.Requests);
        Assert.Single(probe.Lookups);

        // F13 step 8: Enter on Check again; the search held, then let go.
        var hold = probe.HoldNext();
        app.PressKey(Key.Enter);
        await hold.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
        app.Settle();
        AssertSearching(app);
        hold.Release();
        await app.WaitForAsync(
            () => probe.Lookups.Count == 2 && !app.Session.Shell.CheckGitAgainCommand.IsRunning
                && app.GitSetupPart(F.CheckAgainId) is { IsEffectivelyEnabled: true } && app.GitSetupText(F.CheckAgainId) == F.CheckAgain,
            "Check again to end");
        app.Settle();
        Assert.Null(probe.Lookups[1].ChosenPath);
        Assert.Equal(GitSetupState.Missing, app.GitSetupScreen.State);
        AssertScreen(app, F.MissingTitle, F.MissingBodyWindows, null, null, F.MissingGuidanceWindows, null);
        AssertGitStatus(app, F.GitNotFound, null, F.SetUpGit);
        Assert.Equal(F.CheckAgainId, app.FocusedAutomationId());
        Assert.True(HasFocusVisible(app), "Check again has no keyboard outline after its search.");
        Assert.Empty(app.Launcher.Requests);
        Assert.Empty(app.GitCalls());

        // F4 step 9: Check again finds git; the failed restore is opened again.
        var requests = tools.Requests;
        hold = probe.HoldNext();
        app.Click(app.GitSetupPart(F.CheckAgainId)!);
        await hold.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
        app.Settle();
        AssertSearching(app);
        hold.Release();
        await app.WaitForAsync(() => !app.Session.Shell.CheckGitAgainCommand.IsRunning && !app.ShowsGitSetupView(), "Check again to end with the screen gone");
        await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();
        Picture(app, F.F4aFoundAgain);
        Assert.Equal(3, probe.Lookups.Count);
        Assert.Equal(Path.GetFileName(repo.Path), Assert.Single(app.Session.Shell.Tabs).Title);
        Assert.NotNull(app.Session.Shell.ActiveTab.Repository);
        Assert.Empty(app.Session.Shell.Tabs[0].Welcome.ErrorText ?? string.Empty);
        Assert.Empty(new JsonSessionStore(app.Session.Paths.SessionFile).Current.Recent);
        AssertGitStatus(app, Format(F.GitFormat, MachineVersion(app)), OnPath(F.WindowsGit), null);
        Assert.Equal(requests + 1, tools.Requests);
        Assert.Null(SavedGitExecutable(app));

        // F4 step 10.
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>Method 2, F1 light and F2 (Install, Download): as method 1's start, light; the confirm's Install hands the fake launcher git-scm.com's line, winget by its full path, in its folder.</summary>
    [AvaloniaFact]
    public async Task Check_F1_F2_light_install_and_download()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        var exact = await repo.ExactStateAsync();
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.Missing);

        // The theme written before the start, and the file read then: what the start was given.
        var file = Path.Combine(data.Path, "settings.json");
        new JsonSettingsStore(file).Update(current => current with { Theme = AppTheme.Light });
        var settings = File.ReadAllText(file);
        using var app = await StartFirstRunAsync(data, probe, [repo.Path]);

        // F1 step 1, light.
        await app.WaitForGitSetupAsync(guidance: F.MissingGuidanceWindows, choose: F.ChooseGitExe, winget: true);
        Picture(app, F.F1bMissingWindowsLight);
        Assert.Equal(ThemeVariant.Light, app.Window.ActualThemeVariant);
        AssertF1(app, probe, repo.Path, dark: false);
        Assert.Equal(settings, SettingsText(app));

        // F2 step 2: Install.
        app.Click(app.GitSetupPart(F.WingetId)!);
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        await CloseConfirmAsync(app, "ConfirmButton");
        await app.WaitForAsync(() => app.Launcher.Starts.Count == 1, "the launcher to hold one start");
        var start = Assert.Single(app.Launcher.Starts);
        Assert.Equal(("cmd.exe", F.WingetCommandLine, F.WingetFolder, true), (start.FileName, start.CommandLine, start.WorkingDirectory, start.InNewTerminal));
        Assert.Empty(start.Arguments);
        AssertScreen(app, F.MissingTitle, F.MissingBodyWindows, null, null, F.MissingGuidanceWindows, null);
        Assert.Null(app.GitSetupPart(F.ErrorId));
        Assert.Single(probe.Lookups);
        Assert.Equal(1, Logged(app, Format(F.StartedInTerminalLineFormat, F.WingetDisplayText)));

        // F2 step 3: Download.
        app.Click(app.GitSetupPart(F.DownloadId)!);
        await app.WaitForAsync(() => app.Launcher.Requests.Count == 2, "the launcher to hold one address");
        Assert.Equal(new FileLaunch(FileLaunchKind.OpenUrl, F.DownloadForWindows), app.Launcher.Requests[1]);
        AssertScreen(app, F.MissingTitle, F.MissingBodyWindows, null, null, F.MissingGuidanceWindows, null);

        // F2 step 4.
        Assert.Equal(exact, await repo.ExactStateAsync());
        Assert.Empty(app.GitCalls());
    }

    /// <summary>Method 3, F3: a chosen git that is gone, another found; "Use the Git it found" removes the setting and searches again.</summary>
    [AvaloniaFact]
    public async Task Check_F3_chosen_gone_use_the_git_it_found()
    {
        using var data = new TempDirectory("data");
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { GitExecutable = F.Chosen });
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.ChosenMissing(F.WindowsGit), FakeGitAnswer.RealAt(F.WindowsGit, GitSource.Path));
        using var app = await StartFirstRunAsync(data, probe);
        var guidance = Format(F.ChosenMissingGuidanceFormat, F.WindowsGit);

        // Step 1.
        await app.WaitForGitSetupAsync(guidance: guidance, choose: F.ChooseGitExe);
        Picture(app, F.F3aChosenGone);
        Assert.Equal(Paths(F.Chosen), ChosenPaths(probe));
        AssertScreen(app, F.ChosenMissingTitle, F.ChosenMissingBody, F.Chosen, null, guidance, null);
        AssertOffers(app, [(F.ChooseId, F.ChooseGitExe), (F.UsePathId, F.UseTheGitItFound), (F.CheckAgainId, F.CheckAgain)], GuideOnly);
        AssertGitStatus(app, F.ChosenGitNotFound, null, F.SetUpGit);
        Assert.Empty(app.GitCalls());
        var before = SettingsText(app)!;

        // Step 2.
        app.Click(app.GitSetupPart(F.UsePathId)!);
        await app.WaitForAsync(() => !app.ShowsGitSetupView() && !app.Session.Shell.CheckGitAgainCommand.IsRunning, "the screen to go");
        app.Settle();
        var after = SettingsText(app)!;
        Assert.DoesNotContain("\"gitExecutable\"", after, StringComparison.Ordinal);
        Assert.Equal(PropertiesBut(before, "gitExecutable"), PropertiesBut(after, "gitExecutable"));
        Assert.Equal(Paths(F.Chosen, null), ChosenPaths(probe));
        Assert.True(app.Session.Shell.ActiveTab.ShowsWelcome);
        Assert.False(app.FindByAutomationId("WelcomeError").IsEffectivelyVisible);
        AssertGitStatus(app, Format(F.GitFormat, MachineVersion(app)), OnPath(F.WindowsGit), null);
    }

    /// <summary>Method 4, F5 (the twin of the real window's launch 8 to <c>f5a</c>): too old on Windows, found on the machine; the update confirm and its Cancel.</summary>
    [AvaloniaFact]
    public async Task Check_F5_too_old_update_offer()
    {
        using var data = new TempDirectory("data");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.TooOld(F.WindowsGit, GitSource.Path, F.TooOldVersion));
        using var app = await StartFirstRunAsync(data, probe);
        var title = Format(F.TooOldTitleFormat, F.TooOldVersion);
        var body = Format(F.TooOldBodyFormat, F.Minimum, F.TooOldVersion);

        // Step 1.
        await app.WaitForGitSetupAsync(guidance: F.TooOldGuidanceWindows, choose: F.ChooseGitExe, winget: true);
        Picture(app, F.F5aTooOld, atRest: true);
        AssertTooOldOnWindows(app, title, body);
        Assert.Empty(app.GitCalls());

        // Step 2: the update confirm.
        app.Click(app.GitSetupPart(F.WingetId)!);
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        Picture(app, F.F5bWingetUpdateConfirm);
        AssertConfirm(app, F.UpdateConfirmTitle, F.UpdateConfirmDetail, F.Update);

        // Step 3: Cancel.
        await CloseConfirmAsync(app, "CancelButton");
        Assert.Empty(app.Launcher.Requests);
        AssertNotLogged(app, "Started in a terminal:");
        AssertTooOldOnWindows(app, title, body);
    }

    /// <summary>Method 5, F6 (the twin of the real window's launch 9): a broken configuration names its line; "Show the file" reveals it.</summary>
    [AvaloniaFact]
    public async Task Check_F6_config_fault_show_the_file()
    {
        using var data = new TempDirectory("data");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.ConfigFault(F.WindowsGit, F.DemoGitConfig, 1));
        using var app = await StartFirstRunAsync(data, probe);
        var guidance = Format(F.ConfigFaultGuidanceFormat, 1, F.DemoGitConfig);

        // Step 1.
        await app.WaitForGitSetupAsync(guidance: guidance, choose: F.ChooseGitExe);
        Picture(app, F.F6aConfigFault);
        AssertScreen(app, F.NotWorkingTitle, F.NotWorkingBody, F.WindowsGit, F.ConfigFaultText, guidance, null);
        AssertOffers(app, [(F.DownloadId, F.DownloadGitForWindows), (F.ShowFileId, F.ShowTheFile), (F.ChooseId, F.ChooseGitExe), (F.CheckAgainId, F.CheckAgain)], GuideOnly);
        AssertGitStatus(app, F.GitNotWorking, OnPath(F.WindowsGit), F.SetUpGit);
        var said = (SelectableTextBlock)app.GitSetupPart(F.GitSaidId)!;
        Assert.True(said.TryFindResource("VcCodeFontFamily", out var code) && Equals(code, said.FontFamily), "Git's words are not in the code font.");
        var border = said.GetVisualAncestors().OfType<Border>().First(candidate => candidate.BorderThickness.Left > 0);
        Assert.Equal(F.BorderDark, ((ISolidColorBrush)border.BorderBrush!).Color);
        Assert.Single(said.Text!.Split('\n'));

        // Step 2.
        app.Click(app.GitSetupPart(F.ShowFileId)!);
        await app.WaitForAsync(() => app.Launcher.Requests.Count == 1, "the launcher to hold one request");
        Assert.Equal([F.DemoGitConfig], app.Launcher.Revealed);
        AssertScreen(app, F.NotWorkingTitle, F.NotWorkingBody, F.WindowsGit, F.ConfigFaultText, guidance, null);
        Assert.Single(probe.Lookups);
    }

    /// <summary>
    /// Method 6, F6b (the twin of the real window's launch 10): real git with <c>[user</c> on line 1
    /// of the session's global file, found by the machine's search, the guidance fixed (no winget, no
    /// distribution). No picture.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_F6b_real_git_broken_config()
    {
        using var data = new TempDirectory("data");
        ShellDriver.SeedGlobalGitConfig(data.Path, F.BrokenConfigLine + "\n");
        using var app = await StartFirstRunAsync(data, MachineSearchProbe.Instance);

        // Step 1.
        await app.WaitForGitSetupAsync(guidancePrefix: F.ConfigFaultGuidancePrefix);
        Assert.Equal(F.NotWorkingTitle, app.GitSetupText(F.TitleId));
        var said = app.GitSetupText(F.GitSaidId)!;
        Assert.StartsWith(F.ConfigFaultPrefix, said, StringComparison.Ordinal);
        var file = said[F.ConfigFaultPrefix.Length..].Split('\n')[0].TrimEnd('\r');
        Assert.True(GitFileNames.SamePath(GitIsolation.SessionGlobalConfigFile(data.Path), file), $"Git named {file}, not the session's global file.");
        Assert.Equal(Format(F.ConfigFaultGuidanceFormat, 1, file), app.GitSetupText(F.GuidanceId));
        Assert.NotNull(app.GitSetupPart(F.ShowFileId));

        // The git the machine's search found, by the harness's own search (TempRepo's), not the screen's.
        var machineGit = VisualCommit.Git.GitLocator.FindExecutable(VisualCommit.Git.GitSearchContext.FromSystem());
        Assert.NotNull(machineGit);
        Assert.Equal(machineGit, app.GitSetupText(F.PathId));
        AssertGitStatus(app, F.GitNotWorking, app.Session.Shell.GitStatusToolTip, F.SetUpGit);
        Assert.Contains(app.LogLines(), line => line.Contains("[WRN]", StringComparison.Ordinal) && line.Contains(F.ConfigFaultPrefix + file, StringComparison.Ordinal));
    }

    /// <summary>Method 7, F7: a git that could not be started; its message in the body, no "Git said:" (the report's open point 13).</summary>
    [AvaloniaFact]
    public async Task Check_F7_not_started()
    {
        using var data = new TempDirectory("data");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.NotStarted(F.WindowsGit));
        using var app = await StartFirstRunAsync(data, probe);

        await app.WaitForGitSetupAsync(guidance: F.NotWorkingGuidanceWindows, choose: F.ChooseGitExe);
        Picture(app, F.F7aNotStarted);
        AssertScreen(app, F.NotWorkingTitle, Format(F.NotStartedBodyFormat, F.NotStartedMessage), F.WindowsGit, null, F.NotWorkingGuidanceWindows, null);
        AssertOffers(app, [(F.DownloadId, F.DownloadGitForWindows), (F.ChooseId, F.ChooseGitExe), (F.CheckAgainId, F.CheckAgain)], GuideOnly);
        AssertGitStatus(app, F.GitNotWorking, OnPath(F.WindowsGit), F.SetUpGit);
    }

    /// <summary>Method 8, F8: "Choose git.exe…" refuses a too-old git and leaves the settings, then writes one that works and searches again.</summary>
    [AvaloniaFact]
    public async Task Check_F8_choose_too_old_then_found()
    {
        using var data = new TempDirectory("data");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(wingetPath: null), FakeGitAnswer.Missing)
            .AnswerFor(F.Old, FakeGitAnswer.TooOld(F.Old, GitSource.Setting, F.TooOldVersion))
            .AnswerFor(F.Chosen, FakeGitAnswer.RealAt(F.Chosen, GitSource.Setting));
        using var app = await StartFirstRunAsync(data, probe);

        // Step 1: Missing on Windows without winget.
        await app.WaitForGitSetupAsync(guidance: F.MissingGuidanceWindows, choose: F.ChooseGitExe);
        Assert.Equal(F.NewTab, Assert.Single(app.Session.Shell.Tabs).Title);
        AssertScreen(app, F.MissingTitle, F.MissingBodyWindows, null, null, F.MissingGuidanceWindows, null);
        AssertOffers(app, [(F.DownloadId, F.DownloadGitForWindows), (F.ChooseId, F.ChooseGitExe), (F.CheckAgainId, F.CheckAgain)], GuideOnly);
        AssertGitStatus(app, F.GitNotFound, null, F.SetUpGit);
        Assert.Single(probe.Lookups);

        // Step 2: the too-old file; Checking… while the probe is held.
        app.Folders.AnswerFile(F.Old);
        var hold = probe.HoldNext();
        app.Click(app.GitSetupPart(F.ChooseId)!);
        await hold.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
        app.Settle();
        Assert.Equal(F.Checking, app.GitSetupText(F.ErrorId));
        hold.Release();
        var refusal = Format(F.ChooseTooOldFormat, F.TooOldVersion, F.Minimum);
        await app.WaitForAsync(() => app.GitSetupText(F.ErrorId) == refusal, "the refusal");
        Picture(app, F.F8aChooseTooOld);
        Assert.Equal([F.ChooseGitExeTitle], app.Folders.FileTitles);
        Assert.Null(SettingsText(app));
        Assert.Equal(GitSetupState.Missing, app.GitSetupScreen.State);
        Assert.Equal(Paths(null, F.Old), ChosenPaths(probe));
        Assert.Empty(app.GitCalls());

        // Step 3: the working file.
        app.Folders.AnswerFile(F.Chosen);
        app.Click(app.GitSetupPart(F.ChooseId)!);
        await app.WaitForAsync(() => !app.ShowsGitSetupView() && !app.Session.Shell.CheckGitAgainCommand.IsRunning, "the screen to go");
        app.Settle();
        Assert.Equal([F.ChooseGitExeTitle, F.ChooseGitExeTitle], app.Folders.FileTitles);
        Assert.Equal(F.Chosen, SavedGitExecutable(app));
        Assert.Equal(Paths(null, F.Old, F.Chosen, F.Chosen), ChosenPaths(probe));
        Assert.True(app.Session.Shell.ActiveTab.ShowsWelcome);
        Assert.False(app.FindByAutomationId("WelcomeError").IsEffectivelyVisible);
        AssertGitStatus(app, Format(F.GitFormat, MachineVersion(app)), Format(F.ChosenInSettingsTipFormat, F.Chosen), null);
    }

    /// <summary>Method 9, F9: macOS with the Command Line Tools missing, their install without a confirm, Copy, then the tools damaged.</summary>
    [AvaloniaFact]
    public async Task Check_F9_macos_tools_missing_then_broken()
    {
        using var data = new TempDirectory("data");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.MacOS, FakeGitAnswer.CommandLineToolsMissing, FakeGitAnswer.NotWorking(F.UnixGit, F.ToolsBrokenWords, exitCode: 1));
        using var app = await StartFirstRunAsync(data, probe);

        // Step 1.
        await app.WaitForGitSetupAsync(guidance: F.MissingGuidanceMacOSToolsMissing, choose: F.ChooseGitProgram, command: true);
        Picture(app, F.F9aMacOSMissing);
        AssertScreen(app, F.MissingTitle, F.MissingBodyMacOS + F.MissingBodyMacOSToolsMissing, null, null, F.MissingGuidanceMacOSToolsMissing, F.BrewCommand);
        AssertOffers(app, [(F.CltId, F.InstallCommandLineTools), (F.ChooseId, F.ChooseGitProgram), (F.CheckAgainId, F.CheckAgain)], MoreAndGuide);
        AssertGitStatus(app, F.GitNotFound, null, F.SetUpGit);

        // Step 2: the tools' install, no confirm of the app's.
        app.Click(app.GitSetupPart(F.CltId)!);
        await app.WaitForAsync(() => app.Launcher.Starts.Count == 1, "the launcher to hold one start");
        Assert.Null(app.Session.Services.DialogHost.Current);
        var start = Assert.Single(app.Launcher.Starts);
        Assert.Equal((F.CommandLineToolsProgram, F.CommandLineToolsArgument, (string?)null, false), (start.FileName, Assert.Single(start.Arguments), start.CommandLine, start.InNewTerminal));
        Assert.Equal(1, Logged(app, Format(F.StartedLineFormat, F.CommandLineToolsProgram + " " + F.CommandLineToolsArgument)));
        AssertScreen(app, F.MissingTitle, F.MissingBodyMacOS + F.MissingBodyMacOSToolsMissing, null, null, F.MissingGuidanceMacOSToolsMissing, F.BrewCommand);

        // Step 3: Copy.
        app.Click(app.GitSetupPart(F.CopyId)!);
        await app.WaitForAsync(() => app.Clipboard.Texts.Count == 1, "the clipboard");
        Assert.Equal([F.BrewCommand], app.Clipboard.Texts);

        // Step 4: Check again finds the damaged tools.
        app.Click(app.GitSetupPart(F.CheckAgainId)!);
        await app.WaitForAsync(() => probe.Lookups.Count == 2 && !app.Session.Shell.CheckGitAgainCommand.IsRunning && app.GitSetupText(F.TitleId) == F.NotWorkingTitle, "Check again to end on a git that does not work");
        await app.WaitForGitSetupAsync(guidance: F.NotWorkingGuidanceMacOSToolsBroken, choose: F.ChooseGitProgram, command: true);
        Picture(app, F.F9bMacOSBroken);
        AssertScreen(app, F.NotWorkingTitle, F.NotWorkingBody, F.UnixGit, F.ToolsBrokenWords, F.NotWorkingGuidanceMacOSToolsBroken, F.SelectToolsCommand);
        AssertOffers(app, [(F.CltId, F.InstallCommandLineTools), (F.ChooseId, F.ChooseGitProgram), (F.CheckAgainId, F.CheckAgain)], GuideOnly);
        AssertGitStatus(app, F.GitNotWorking, OnPath(F.UnixGit), F.SetUpGit);
        Assert.Single(app.Launcher.Starts);
    }

    /// <summary>Method 10, F10 a: Ubuntu, git missing: its package command with Copy.</summary>
    [AvaloniaFact]
    public async Task Check_F10_ubuntu_missing()
    {
        using var data = new TempDirectory("data");
        using var app = await StartFirstRunAsync(data, new FakeGitSetupProbe(FakeGitSetupProbe.Linux(FakeGitSetupProbe.Ubuntu), FakeGitAnswer.Missing));
        var guidance = Format(F.MissingGuidanceLinuxKnownFormat, F.UbuntuPrettyName);

        await app.WaitForGitSetupAsync(guidance: guidance, choose: F.ChooseGitProgram, command: true);
        Picture(app, F.F10aUbuntuMissing);
        AssertScreen(app, F.MissingTitle, F.MissingBodyLinux, null, null, guidance, F.AptCommand);
        AssertOffers(app, [(F.ChooseId, F.ChooseGitProgram), (F.CheckAgainId, F.CheckAgain)], MoreAndGuide);
        AssertGitStatus(app, F.GitNotFound, null, F.SetUpGit);
    }

    /// <summary>Method 11, F10 b: Alpine, git missing: the generic text, no command.</summary>
    [AvaloniaFact]
    public async Task Check_F10_alpine_missing()
    {
        using var data = new TempDirectory("data");
        using var app = await StartFirstRunAsync(data, new FakeGitSetupProbe(FakeGitSetupProbe.Linux(FakeGitSetupProbe.Alpine), FakeGitAnswer.Missing));

        await app.WaitForGitSetupAsync(guidance: F.MissingGuidanceLinuxUnknown, choose: F.ChooseGitProgram);
        Picture(app, F.F10bAlpineMissing);
        AssertScreen(app, F.MissingTitle, F.MissingBodyLinux, null, null, F.MissingGuidanceLinuxUnknown, null);
        AssertOffers(app, [(F.ChooseId, F.ChooseGitProgram), (F.CheckAgainId, F.CheckAgain)], MoreAndGuide);
        AssertGitStatus(app, F.GitNotFound, null, F.SetUpGit);
    }

    /// <summary>Method 12, F10 c: Ubuntu, git too old: the generic Linux text, no command or Copy, no third-party source (D449).</summary>
    [AvaloniaFact]
    public async Task Check_F10_ubuntu_too_old()
    {
        using var data = new TempDirectory("data");
        using var app = await StartFirstRunAsync(data, new FakeGitSetupProbe(FakeGitSetupProbe.Linux(FakeGitSetupProbe.Ubuntu), FakeGitAnswer.TooOld(F.UnixGit, GitSource.Path, F.TooOldVersion)));

        await app.WaitForGitSetupAsync(guidance: F.TooOldGuidanceLinux, choose: F.ChooseGitProgram);
        Picture(app, F.F10cUbuntuTooOld, atRest: true);
        AssertScreen(app, Format(F.TooOldTitleFormat, F.TooOldVersion), Format(F.TooOldBodyFormat, F.Minimum, F.TooOldVersion), F.UnixGit, null, F.TooOldGuidanceLinux, null);
        AssertOffers(app, [(F.ChooseId, F.ChooseGitProgram), (F.CheckAgainId, F.CheckAgain)], MoreAndGuide);
        AssertGitStatus(app, Format(F.GitTooOldStatusFormat, F.TooOldVersion, F.Minimum), OnPath(F.UnixGit), F.SetUpGit);
    }

    /// <summary>Method 13, F11 (scripted only): the askpass helper missing with git working; "Continue without it", then "Details…".</summary>
    [AvaloniaFact]
    public async Task Check_F11_helper_missing_continue_and_details()
    {
        using var data = new TempDirectory("data");
        using var app = await StartFirstRunAsync(data, new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.RealAt(F.WindowsGit, GitSource.Path)), askPassHelper: F.MissingHelper);
        var status = Format(F.HelperMissingStatusFormat, MachineVersion(app));

        // Step 1.
        await app.WaitForGitSetupAsync(guidance: F.HelperMissingGuidance);
        Picture(app, F.F11aHelperMissing);
        AssertHelperMissing(app, status);

        // Step 2: Continue without it.
        app.Click(app.GitSetupPart(F.ContinueId)!);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.ShowsWelcome && !app.ShowsGitSetupView(), "the welcome page");
        app.Settle();
        Assert.Contains(F.NoRepositoryOpen, TextsIn(app.Window));
        Assert.False(app.FindByAutomationId("WelcomeError").IsEffectivelyVisible);
        AssertGitStatus(app, status, OnPath(F.WindowsGit), F.Details);
        Assert.Null(SettingsText(app));

        // Step 3: Details… brings the screen back in the same tab.
        app.Click(app.FindByAutomationId(F.SetUpButtonId));
        await app.WaitForGitSetupAsync(guidance: F.HelperMissingGuidance);
        AssertHelperMissing(app, status);
        Assert.Single(app.Session.Shell.Tabs);
    }

    /// <summary>Method 14, F12: the settings screen's Git program row on "graph": Browse, Save, reopen, "Use the Git on the PATH", Save, a too-old file refused.</summary>
    [AvaloniaFact]
    public async Task Check_F12_settings_row()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.RealAt(F.WindowsGit, GitSource.Path))
            .AnswerFor(F.Chosen, FakeGitAnswer.RealAt(F.Chosen, GitSource.Setting))
            .AnswerFor(F.Old, FakeGitAnswer.TooOld(F.Old, GitSource.Setting, F.TooOldVersion));
        using var app = await OpenAsync(data, repo.Path, gitProbe: probe);
        var exact = await repo.ExactStateAsync();
        var globalFile = GitIsolation.SessionGlobalConfigFile(data.Path);
        var global = File.Exists(globalFile) ? File.ReadAllText(globalFile) : null;
        var title = OperatingSystem.IsWindows() ? F.ChooseGitExeTitle : F.ChooseGitProgramTitle;

        // Step 1.
        await OpenGitSectionAsync(app);
        AssertRowAsFound(app);
        AssertRowIsLastOfTheGlobalScope(app);
        Assert.Single(probe.Lookups);

        // Step 2: not on "This repository"; back on the global scope as before.
        app.ChooseScope(SettingsScope.Repository);
        Assert.False(app.Find<Grid>("GitProgramRow").IsEffectivelyVisible);
        Assert.False(app.FindByAutomationId(F.ProgramPathId).IsEffectivelyVisible);
        app.ChooseScope(SettingsScope.Global);
        AssertRowAsFound(app);

        // Step 3: Browse gives the chosen git.
        app.Folders.AnswerFile(F.Chosen);
        app.Click(app.FindByAutomationId(F.ProgramBrowseId));
        await app.WaitForAsync(() => ((TextBlock)app.FindByAutomationId(F.ProgramPathId)).Text == F.Chosen, "the row to show the chosen git");
        Picture(app, F.F12aRowNextStart);
        Assert.Equal([title], app.Folders.FileTitles);
        AssertRow(app, F.Chosen, F.ChosenHereNote + F.NextStartSuffix, F.UseTheGitOnThePath, null);
        Assert.Null(SettingsText(app));
        Assert.Equal(Paths(null, F.Chosen), ChosenPaths(probe));

        // Step 4: Save writes the key, the other keys as they were (the defaults: no file before it); the git in use stays (ruling 42).
        await SaveAndCloseAsync(app);
        Assert.Equal(F.Chosen, SavedGitExecutable(app));
        var step4 = SettingsText(app)!;
        Assert.Equal(DefaultPropertiesBut("gitExecutable"), PropertiesBut(step4, "gitExecutable"));
        Assert.Equal(2, probe.Lookups.Count);
        AssertGitStatus(app, Format(F.GitFormat, MachineVersion(app)), OnPath(F.WindowsGit), null);

        // Step 5: reopened, the saved choice still differs from the git in use.
        await OpenGitSectionAsync(app);
        AssertRow(app, F.Chosen, F.ChosenHereNote + F.NextStartSuffix, F.UseTheGitOnThePath, null);

        // Step 6: Use the Git on the PATH.
        app.Click(app.FindByAutomationId(F.ProgramUsePathId));
        app.Settle();
        AssertRow(app, F.WindowsGit, F.FoundOnPathNote, null, null);

        // Step 7: Save removes the key.
        await SaveAndCloseAsync(app);
        Assert.Null(SavedGitExecutable(app));
        Assert.Equal(2, probe.Lookups.Count);
        var saved = SettingsText(app)!;
        Assert.DoesNotContain("\"gitExecutable\"", saved, StringComparison.Ordinal);
        Assert.Equal(PropertiesBut(step4, "gitExecutable"), PropertiesBut(saved, "gitExecutable"));

        // Step 8: reopened, a too-old file refused.
        await OpenGitSectionAsync(app);
        app.Folders.AnswerFile(F.Old);
        app.Click(app.FindByAutomationId(F.ProgramBrowseId));
        var refusal = Format(F.ChooseTooOldFormat, F.TooOldVersion, F.Minimum);
        await app.WaitForAsync(() => ((TextBlock)app.FindByAutomationId(F.ProgramStatusId)).Text == refusal, "the row's refusal");
        Picture(app, F.F12bRowTooOld);
        Assert.Equal([title, title], app.Folders.FileTitles);
        AssertRow(app, F.WindowsGit, F.FoundOnPathNote, null, refusal);
        Assert.Equal(Paths(null, F.Chosen, F.Old), ChosenPaths(probe));

        // Step 9: Esc closes unsaved.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.SettingsDialog is null, "the settings screen to close");
        Assert.Equal(saved, SettingsText(app));
        Assert.Equal(exact, await repo.ExactStateAsync());
        Assert.Equal(global, File.Exists(globalFile) ? File.ReadAllText(globalFile) : null);
    }

    /// <summary>Method 15, F14: the netrc note in the Accounts section, signed in with "Use for git over HTTPS" on and git at 2.43.0; dismissed for the session.</summary>
    [AvaloniaFact]
    public async Task Check_F14_netrc_note()
    {
        var copy = await Scenarios.HostingAsync();
        await using var served = await Scenarios.ServeHostingAsync(copy, inProcess: true, ct: TestContext.Current.CancellationToken, ownsCopy: true);
        using var data = new TempDirectory("data");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.RealAt(F.WindowsGit, GitSource.Path, F.NetrcVersion));
        using var app = await StartFirstRunAsync(data, probe, hosting: new HostingOptions(served.GitHub.Endpoints, served.GitHub.CreateHandler));
        var home = GitIsolation.SessionHomeFolder(data.Path);
        Assert.Empty(Directory.EnumerateFileSystemEntries(home));

        // Step 1: signed in; the account used for git.
        await app.SignInAsync(served.OctoToken, TestContext.Current.CancellationToken);
        await app.WaitForAsync(() => AccountRecords(app) is [var only] && only.TryGetProperty("useForGit", out var used) && used.ValueKind == JsonValueKind.True, "accounts.json's one record with useForGit on");
        var record = Assert.Single(AccountRecords(app)!);
        Assert.Equal(("github", "octo", true), (record.GetProperty("provider").GetString(), record.GetProperty("login").GetString(), record.GetProperty("useForGit").GetBoolean()));
        Assert.Null(app.SettingsDialog);
        Assert.Equal(Format(F.GitFormat, F.NetrcVersion), app.Session.Shell.GitStatusText);

        // Step 2: no file, no note.
        await OpenAccountsAsync(app);
        AssertAccount(app);
        AssertNoNote(app);
        await CloseUnsavedAsync(app);

        // Step 3: an empty _netrc in the session's home.
        File.WriteAllText(Path.Combine(home, "_netrc"), string.Empty);
        await OpenAccountsAsync(app);
        await app.WaitForAsync(() => app.FindByAutomationId(F.NetrcNoteId).IsEffectivelyVisible, "the netrc note");
        Picture(app, F.F14aNetrcNote);
        var note = (TextBlock)app.FindByAutomationId(F.NetrcNoteId);
        Assert.Equal(Format(F.NetrcNoteFormat, F.NetrcVersion), note.Text);
        Assert.Contains("settingsNote", note.Classes);
        Assert.Contains("notice", note.Classes);
        Assert.Equal(F.WarningDark, ((ISolidColorBrush)note.Foreground!).Color);
        Assert.Equal(F.Dismiss, AutomationProperties.GetName(app.FindByAutomationId(F.NetrcDismissId)));
        var settings = SettingsText(app);

        // Step 4: Dismiss.
        app.Click(app.FindByAutomationId(F.NetrcDismissId));
        app.Settle();
        AssertNoNote(app);
        Assert.Equal(settings, SettingsText(app));
        Assert.True(File.Exists(Path.Combine(home, "_netrc")));

        // Step 5: dismissed for the session.
        await CloseUnsavedAsync(app);
        await OpenAccountsAsync(app);
        AssertAccount(app);
        AssertNoNote(app);
        await CloseUnsavedAsync(app);
    }

    /// <summary>F1's expected results (methods 1 and 2) after their step 1.</summary>
    private static void AssertF1(ShellDriver app, FakeGitSetupProbe probe, string folder, bool dark)
    {
        var tab = Assert.Single(app.Session.Shell.Tabs);
        Assert.Equal(F.NewTab, tab.Title);
        Assert.Equal(folder, tab.FailedPath);
        Assert.Equal(folder, Assert.Single(new JsonSessionStore(app.Session.Paths.SessionFile).Current.Tabs).RepositoryPath);
        AssertScreen(app, F.MissingTitle, F.MissingBodyWindows, null, null, F.MissingGuidanceWindows, null);
        AssertOffers(app, MissingWithWinget, GuideOnly);
        AssertLook(app, dark);
        AssertGitStatus(app, F.GitNotFound, null, F.SetUpGit);
        Assert.Empty(app.GitCalls());
        Assert.Equal([new GitLookup(null, null)], probe.Lookups);
        Assert.Empty(app.Launcher.Requests);
    }

    /// <summary>F5's expected results after its step 1, and after its Cancel.</summary>
    private static void AssertTooOldOnWindows(ShellDriver app, string title, string body)
    {
        AssertScreen(app, title, body, F.WindowsGit, null, F.TooOldGuidanceWindows, null);
        AssertOffers(app, [(F.WingetId, F.UpdateWithWinget), (F.DownloadId, F.DownloadGitForWindows), (F.ChooseId, F.ChooseGitExe), (F.CheckAgainId, F.CheckAgain)], GuideOnly);
        AssertGitStatus(app, Format(F.GitTooOldStatusFormat, F.TooOldVersion, F.Minimum), OnPath(F.WindowsGit), F.SetUpGit);
    }

    /// <summary>F11's screen.</summary>
    private static void AssertHelperMissing(ShellDriver app, string status)
    {
        AssertScreen(app, F.HelperMissingTitle, F.HelperMissingBody, F.MissingHelper, null, F.HelperMissingGuidance, null);
        AssertOffers(app, [(F.CheckAgainId, F.CheckAgain), (F.ContinueId, F.ContinueWithoutIt)], [(F.GuideLinkId, F.WhatTheHelperDoes)]);
        AssertGitStatus(app, status, OnPath(F.WindowsGit), F.Details);
    }

    /// <summary>While a search runs (F4, F13): Check again reads <c>Checking…</c> and is disabled, the status bar <c>Looking for Git...</c>.</summary>
    private static void AssertSearching(ShellDriver app)
    {
        var button = app.GitSetupPart(F.CheckAgainId)!;
        Assert.Equal(F.Checking, AutomationProperties.GetName(button));
        Assert.False(button.IsEffectivelyEnabled, "Check again is enabled while it searches.");
        Assert.Equal(F.LookingForGit, ((TextBlock)app.FindByAutomationId(F.StatusId)).Text);
    }

    /// <summary>Ctrl+, then the Git section on its global scope, its reads ended.</summary>
    private static async Task OpenGitSectionAsync(ShellDriver app)
    {
        var dialog = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Git);
        await app.WaitForSettingsReadsAsync(dialog);
        Assert.True(dialog.Git.IsGlobalScope);
    }

    /// <summary>Ctrl+, then Accounts, the screen's reads (the netrc question among them) ended.</summary>
    private static async Task OpenAccountsAsync(ShellDriver app)
    {
        var dialog = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Accounts);
        await app.WaitForSettingsReadsAsync(dialog);
    }

    /// <summary>Esc, and the screen closed.</summary>
    private static async Task CloseUnsavedAsync(ShellDriver app)
    {
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.SettingsDialog is null, "the settings screen to close");
    }

    /// <summary>Save, and the screen closed.</summary>
    private static async Task SaveAndCloseAsync(ShellDriver app)
    {
        var dialog = app.SettingsDialog!;
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await app.WaitForAsync(() => dialog.IsClosed && app.SettingsDialog is null, "the settings screen to close after Save");
        app.Settle();
    }

    /// <summary>The row as F12's step 1 has it.</summary>
    private static void AssertRowAsFound(ShellDriver app)
    {
        AssertRow(app, F.WindowsGit, F.FoundOnPathNote, null, null);
        var row = app.Find<Grid>("GitProgramRow");
        Assert.Equal(F.GitProgramLabel, row.Children.OfType<TextBlock>().First().Text);
        Assert.Equal([new GridLength(132), new GridLength(12), new GridLength(1, GridUnitType.Star)], row.ColumnDefinitions.Select(column => column.Width));
        Assert.Equal(18, row.Margin.Top);
        var path = (TrimmedTextBlock)app.FindByAutomationId(F.ProgramPathId);
        Assert.Equal(TextTrimming.LeadingCharacterEllipsis, path.TextTrimming);
        Assert.Equal(F.Browse, AutomationProperties.GetName(app.FindByAutomationId(F.ProgramBrowseId)));
    }

    /// <summary>The row's path, note, the drop button's text (null: not shown) and its status (null: none).</summary>
    private static void AssertRow(ShellDriver app, string path, string note, string? usePath, string? status)
    {
        Assert.True(app.Find<Grid>("GitProgramRow").IsEffectivelyVisible);
        Assert.Equal(path, ((TextBlock)app.FindByAutomationId(F.ProgramPathId)).Text);
        Assert.Equal(note, ((TextBlock)app.FindByAutomationId(F.ProgramNoteId)).Text);
        var drop = app.FindByAutomationId(F.ProgramUsePathId);
        Assert.Equal(usePath is not null, drop.IsEffectivelyVisible);
        if (usePath is not null)
        {
            Assert.Equal(usePath, AutomationProperties.GetName(drop));
        }

        var shown = app.FindByAutomationId(F.ProgramStatusId);
        Assert.Equal(status is not null, shown.IsEffectivelyVisible);
        if (status is not null)
        {
            Assert.Equal(status, ((TextBlock)shown).Text);
        }
    }

    /// <summary>The row is the last thing of the global scope, below the merge tool's note.</summary>
    private static void AssertRowIsLastOfTheGlobalScope(ShellDriver app)
    {
        var row = app.Find<Grid>("GitProgramRow");
        var note = app.FindByAutomationId("MergeToolNote");
        Assert.True(app.BoundsOf(row).Top >= app.BoundsOf(note).Bottom, "The Git program row is not below the merge tool's note.");
        var section = app.TopDialogView!.GetVisualDescendants().OfType<StackPanel>().First(panel => panel.Name == "SettingsGit");
        var lowest = section.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && control.Bounds.Height > 0 && control is TextBlock or Button)
            .MaxBy(control => app.BoundsOf(control).Bottom)!;
        Assert.True(lowest.GetSelfAndVisualAncestors().Contains(row), $"The lowest control of the Git section is {lowest.Name}, outside the Git program row.");
    }

    /// <summary>GitHub's block shows octo, used for git (F14 steps 2 and 5).</summary>
    private static void AssertAccount(ShellDriver app)
    {
        var block = app.FindByAutomationId("AccountsGitHub");
        Assert.True(block.IsEffectivelyVisible);
        Assert.Equal("octo", ((TextBlock)app.FindByAutomationId("AccountLogin")).Text);
        var useForGit = (CheckBox)app.FindByAutomationId("AccountUseForGit");
        Assert.Equal(F.UseForGit, AutomationProperties.GetName(useForGit));
        Assert.True(useForGit.IsChecked);
    }

    private static void AssertNoNote(ShellDriver app)
    {
        Assert.False(app.FindByAutomationId(F.NetrcNoteId).IsEffectivelyVisible);
        Assert.False(app.FindByAutomationId(F.NetrcDismissId).IsEffectivelyVisible);
    }
}
