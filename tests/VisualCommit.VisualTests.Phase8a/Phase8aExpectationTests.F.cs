using VisualCommit.App.ViewModels.GitSetup;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Settings;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// Part F's expectations against the app (docs/test-reports/phase-8a.md, "Conventions of part F"):
/// <c>GitSetupTexts</c>' texts are the report's word for word, the fake's made-up values are the
/// report's, and every picture name matches the phase's pattern (design 0.3) and is the report's.
/// </summary>
public sealed partial class Phase8aExpectationTests
{
    [Fact]
    public void Part_F_s_screen_texts_are_the_app_s_word_for_word()
    {
        Assert.Equal(F.MissingTitle, GitSetupTexts.MissingTitle);
        Assert.Equal(F.ChosenMissingTitle, GitSetupTexts.ChosenMissingTitle);
        Assert.Equal(F.TooOldTitleFormat, GitSetupTexts.TooOldTitleFormat);
        Assert.Equal(F.NotWorkingTitle, GitSetupTexts.NotWorkingTitle);
        Assert.Equal(F.HelperMissingTitle, GitSetupTexts.HelperMissingTitle);
        Assert.Equal(F.MissingBodyWindows, GitSetupTexts.MissingBodyWindows);
        Assert.Equal(F.MissingBodyMacOS, GitSetupTexts.MissingBodyMacOS);
        Assert.Equal(F.MissingBodyMacOSToolsMissing, GitSetupTexts.MissingBodyMacOSToolsMissing);
        Assert.Equal(F.MissingBodyLinux, GitSetupTexts.MissingBodyLinux);
        Assert.Equal(F.ChosenMissingBody, GitSetupTexts.ChosenMissingBody);
        Assert.Equal(F.ChosenNotRunnableBody, GitSetupTexts.ChosenNotRunnableBody);
        Assert.Equal(F.TooOldBodyFormat, GitSetupTexts.TooOldBodyFormat);
        Assert.Equal(F.NotWorkingBody, GitSetupTexts.NotWorkingBody);
        Assert.Equal(F.NotWorkingSilentBodyFormat, GitSetupTexts.NotWorkingSilentBodyFormat);
        Assert.Equal(F.NotStartedBodyFormat, GitSetupTexts.NotStartedBodyFormat);
        Assert.Equal(F.HelperMissingBody, GitSetupTexts.HelperMissingBody);
        Assert.Equal(F.GitSaid, GitSetupTexts.GitSaid);
        Assert.Equal(F.MissingGuidanceWindows, GitSetupTexts.MissingGuidanceWindows);
        Assert.Equal(F.MissingGuidanceMacOSToolsMissing, GitSetupTexts.MissingGuidanceMacOSToolsMissing);
        Assert.Equal(F.MissingGuidanceMacOS, GitSetupTexts.MissingGuidanceMacOS);
        Assert.Equal(F.MissingGuidanceLinuxKnownFormat, GitSetupTexts.MissingGuidanceLinuxKnownFormat);
        Assert.Equal(F.MissingGuidanceLinuxUnknown, GitSetupTexts.MissingGuidanceLinuxUnknown);
        Assert.Equal(F.ChosenMissingGuidanceFormat, GitSetupTexts.ChosenMissingGuidanceFormat);
        Assert.Equal(F.ChosenMissingGuidanceNone, GitSetupTexts.ChosenMissingGuidanceNone);
        Assert.Equal(F.TooOldGuidanceWindows, GitSetupTexts.TooOldGuidanceWindows);
        Assert.Equal(F.TooOldGuidanceMacOS, GitSetupTexts.TooOldGuidanceMacOS);
        Assert.Equal(F.TooOldGuidanceLinux, GitSetupTexts.TooOldGuidanceLinux);
        Assert.Equal(F.ConfigFaultGuidanceFormat, GitSetupTexts.ConfigFaultGuidanceFormat);
        Assert.Equal(F.NotWorkingGuidanceWindows, GitSetupTexts.NotWorkingGuidanceWindows);
        Assert.Equal(F.NotWorkingGuidanceMacOSToolsBroken, GitSetupTexts.NotWorkingGuidanceMacOSToolsBroken);
        Assert.Equal(F.NotWorkingGuidanceMacOS, GitSetupTexts.NotWorkingGuidanceMacOS);
        Assert.Equal(F.NotWorkingGuidanceLinux, GitSetupTexts.NotWorkingGuidanceLinux);
        Assert.Equal(F.HelperMissingGuidance, GitSetupTexts.HelperMissingGuidance);
        Assert.Equal(
            [F.BrewCommand, F.SelectToolsCommand, F.AptCommand, F.DnfCommand, F.PacmanCommand, F.ZypperCommand],
            [GitSetupTexts.BrewCommand, GitSetupTexts.SelectToolsCommand, GitSetupTexts.AptCommand, GitSetupTexts.DnfCommand, GitSetupTexts.PacmanCommand, GitSetupTexts.ZypperCommand]);
    }

    [Fact]
    public void Part_F_s_buttons_links_confirms_and_choose_are_the_app_s_word_for_word()
    {
        Assert.Equal(
            [F.InstallWithWinget, F.UpdateWithWinget, F.DownloadGitForWindows, F.InstallCommandLineTools, F.ShowTheFile, F.ChooseGitExe, F.ChooseGitProgram, F.UseTheGitItFound, F.UseTheGitOnThePath, F.CheckAgain, F.Checking, F.ContinueWithoutIt, F.Copy],
            [GitSetupTexts.InstallWithWinget, GitSetupTexts.UpdateWithWinget, GitSetupTexts.DownloadGitForWindows, GitSetupTexts.InstallCommandLineTools, GitSetupTexts.ShowTheFile, GitSetupTexts.ChooseGitExe, GitSetupTexts.ChooseGitProgram, GitSetupTexts.UseTheGitItFound, GitSetupTexts.UseTheGitOnThePath, GitSetupTexts.CheckAgain, GitSetupTexts.Checking, GitSetupTexts.ContinueWithoutIt, GitSetupTexts.Copy]);
        Assert.Equal(
            [F.OtherWaysToInstallGit, F.HowVisualCommitFindsGit, F.WhatTheHelperDoes],
            [GitSetupTexts.OtherWaysToInstallGit, GitSetupTexts.HowVisualCommitFindsGit, GitSetupTexts.WhatTheHelperDoes]);
        Assert.Equal(
            [F.DownloadForWindows, F.InstallOnMacOS, F.InstallOnLinux],
            [GitSetupTexts.DownloadForWindows.AbsoluteUri, GitSetupTexts.InstallOnMacOS.AbsoluteUri, GitSetupTexts.InstallOnLinux.AbsoluteUri]);
        Assert.Equal(
            [F.WingetLine, F.InstallConfirmTitle, F.UpdateConfirmTitle, F.ConfirmQuestion, F.InstallConfirmDetail, F.UpdateConfirmDetail, F.Install, F.Update],
            [GitSetupTexts.WingetLine, GitSetupTexts.InstallConfirmTitle, GitSetupTexts.UpdateConfirmTitle, GitSetupTexts.ConfirmQuestion, GitSetupTexts.InstallConfirmDetail, GitSetupTexts.UpdateConfirmDetail, GitSetupTexts.Install, GitSetupTexts.Update]);
        Assert.Equal(F.Cancel, new VisualCommit.App.ViewModels.Dialogs.ConfirmationRequest("t", "q", "d", "c").CancelText);
        Assert.Equal(
            [F.ChooseGitExeTitle, F.ChooseGitProgramTitle, F.ChooseTooOldFormat, F.ChooseNoVersionFormat, F.ChooseNotStartedFormat, F.StartFailedFormat],
            [GitSetupTexts.ChooseGitExeTitle, GitSetupTexts.ChooseGitProgramTitle, GitSetupTexts.ChooseTooOldFormat, GitSetupTexts.ChooseNoVersionFormat, GitSetupTexts.ChooseNotStartedFormat, GitSetupTexts.StartFailedFormat]);
        Assert.Equal((F.ProgramMissing, F.ProgramNotExe), (OperationTexts.SettingsTexts.ProgramMissing, OperationTexts.SettingsTexts.ProgramNotExe));

        // The ellipsis is the one character U+2026.
        Assert.All([F.InstallWithWinget, F.UpdateWithWinget, F.ChooseGitExe, F.ChooseGitProgram, F.Checking, F.SetUpGit, F.Details, F.Browse], text => Assert.EndsWith("…", text, StringComparison.Ordinal));
    }

    [Fact]
    public void Part_F_s_status_bar_settings_row_netrc_note_and_log_lines_are_the_app_s_word_for_word()
    {
        Assert.Equal(
            [F.LookingForGit, F.GitFormat, F.GitTooOldStatusFormat, F.GitNotFound, F.GitNotWorking, F.ChosenGitNotFound, F.HelperMissingStatusFormat],
            [GitSetupTexts.LookingForGit, GitSetupTexts.GitFormat, GitSetupTexts.GitTooOldStatusFormat, GitSetupTexts.GitNotFound, GitSetupTexts.GitNotWorkingStatus, GitSetupTexts.ChosenGitNotFound, GitSetupTexts.HelperMissingStatusFormat]);
        Assert.Equal(
            [F.FoundOnPathTipFormat, F.FoundInGitForWindowsFolderTipFormat, F.FoundInUsualFolderTipFormat, F.ChosenInSettingsTipFormat, F.StandInTipFormat, F.SetUpGit, F.Details],
            [GitSetupTexts.FoundOnPathTipFormat, GitSetupTexts.FoundInGitForWindowsFolderTipFormat, GitSetupTexts.FoundInUsualFolderTipFormat, GitSetupTexts.ChosenInSettingsTipFormat, GitSetupTexts.StandInTipFormat, GitSetupTexts.SetUpGit, GitSetupTexts.Details]);
        Assert.Equal(
            [F.GitProgramLabel, F.NoneFound, F.FoundOnPathNote, F.FoundInGitForWindowsFolderNote, F.FoundInUsualFolderNote, F.ChosenHereNote, F.ChosenGoneNote, F.ChosenNotRunnableNote, F.StandInNote, F.NoGitFoundNote, F.NextStartSuffix, F.WhenYouSaveSuffix, F.Browse],
            [GitSetupTexts.GitProgramLabel, GitSetupTexts.NoneFound, GitSetupTexts.FoundOnPathNote, GitSetupTexts.FoundInGitForWindowsFolderNote, GitSetupTexts.FoundInUsualFolderNote, GitSetupTexts.ChosenHereNote, GitSetupTexts.ChosenGoneNote, GitSetupTexts.ChosenNotRunnableNote, GitSetupTexts.StandInNote, GitSetupTexts.NoGitFoundNote, GitSetupTexts.NextStartSuffix, GitSetupTexts.WhenYouSaveSuffix, GitSetupTexts.Browse]);
        Assert.Equal((F.NetrcNoteFormat, F.Dismiss), (GitSetupTexts.NetrcNoteFormat, GitSetupTexts.Dismiss));
        Assert.Equal((F.StartedInTerminalLineFormat, F.StartedLineFormat), (GitSetupTexts.StartedInTerminalFormat, GitSetupTexts.StartedFormat));
        Assert.Equal((F.ChosenNotFullPathLineFormat, F.ChosenNotExeLineFormat), (GitSetupTexts.ChosenNotFullPathFormat, GitSetupTexts.ChosenNotExeFormat));
        Assert.Equal("VISUALCOMMIT_GIT_STAND_IN", GitLocator.StandInVariable);
        Assert.StartsWith(GitLocator.StandInVariable + " is set (a test switch)", F.StandInNoneLine, StringComparison.Ordinal);
        Assert.Equal(F.NewTab, VisualCommit.App.ViewModels.RepoTabViewModel.EmptyTitle);
        Assert.Equal(F.GitProgramLabel, new GitProgramViewModel(null, null, null, new FakeFolderPicker(), CancellationToken.None).Label);
    }

    [Fact]
    public void Part_F_s_made_up_values_are_the_fakes_and_the_reports()
    {
        Assert.Equal(
            [F.WindowsGit, F.UnixGit, F.DemoGitConfig, F.WingetPath, F.TooOldVersion, F.NotStartedMessage, F.ToolsBrokenWords],
            [FakeGitSetupProbe.WindowsGit, FakeGitSetupProbe.UnixGit, FakeGitSetupProbe.DemoGitConfig, FakeGitSetupProbe.WingetPath, FakeGitSetupProbe.TooOldVersion, FakeGitSetupProbe.NotStartedMessage, FakeGitSetupProbe.ToolsBrokenWords]);
        Assert.Equal((F.UbuntuId, F.UbuntuLike, F.UbuntuPrettyName), (FakeGitSetupProbe.Ubuntu.Id, Assert.Single(FakeGitSetupProbe.Ubuntu.IdLike), FakeGitSetupProbe.Ubuntu.PrettyName));
        Assert.Equal(F.AlpineId, FakeGitSetupProbe.Alpine.Id);
        Assert.Empty(FakeGitSetupProbe.Alpine.IdLike);
        Assert.Equal(F.ConfigFaultText, FakeGitAnswer.ConfigFaultWords(F.DemoGitConfig, 1));
        Assert.StartsWith(F.ConfigFaultPrefix, F.ConfigFaultText, StringComparison.Ordinal);
        Assert.StartsWith("xcrun: error", F.ToolsBrokenWords, StringComparison.Ordinal);
        Assert.Equal(F.WingetCommandLine, GitSetupViewModel.WingetLaunch(F.WingetPath).CommandLine);
        Assert.Equal(F.WingetFolder, GitSetupViewModel.WingetLaunch(F.WingetPath).WorkingDirectory);
        Assert.Equal(F.WingetDisplayText, GitSetupViewModel.WingetLaunch(F.WingetPath).DisplayText);
        Assert.Equal((F.CommandLineToolsProgram, F.CommandLineToolsArgument), (GitSetupViewModel.CommandLineToolsLaunch.FileName, Assert.Single(GitSetupViewModel.CommandLineToolsLaunch.Arguments)));

        // The chosen paths are full paths on the machine the checks run on, and the lookup runs them.
        Assert.All([F.Chosen, F.Old, F.MissingHelper], path => Assert.True(Path.IsPathFullyQualified(path), path));
        Assert.True(GitLocator.IsRunnablePath(F.Chosen));
        Assert.True(GitLocator.IsRunnablePath(F.Old));
        Assert.Contains(@"\Users\demo\", F.WingetPath, StringComparison.Ordinal);
        Assert.True(GitVersion.TryParse(F.TooOldVersion, out var old) && !old.IsSupported);
        Assert.True(GitVersion.TryParse(F.NetrcVersion, out var netrc) && netrc.IsSupported && netrc < new GitVersion(2, 46, 0));
    }

    [Fact]
    public void Part_F_s_ids_are_the_screens_and_its_buttons_lie_in_the_tables_order()
    {
        Assert.Equal(
            ["GitSetupWingetButton", "GitSetupDownloadButton", "GitSetupCltButton", "GitSetupShowFileButton", "GitSetupChooseButton", "GitSetupUsePathButton", "GitSetupCheckAgainButton", "GitSetupContinueButton"],
            F.ButtonOrder);
        Assert.Equal(["GitSetupMoreLink", "GitSetupGuideLink"], F.LinkOrder);
        Assert.Equal(["GitSetupWingetButton", "GitSetupDownloadButton", "GitSetupChooseButton", "GitSetupCheckAgainButton"], F.FocusOrderWithWinget);
    }

    [Fact]
    public void Part_F_s_picture_names_are_the_report_s_unique_and_follow_the_rule()
    {
        Assert.Equal(
            [
                "f1a-missing-windows", "f2a-winget-confirm", "f4a-found-again", "f1b-missing-windows-light", "f3a-chosen-gone", "f5a-too-old", "f5b-winget-update-confirm",
                "f6a-config-fault", "f7a-not-started", "f8a-choose-too-old", "f9a-macos-missing", "f9b-macos-broken", "f10a-ubuntu-missing", "f10b-alpine-missing",
                "f10c-ubuntu-too-old", "f11a-helper-missing", "f12a-row-next-start", "f12b-row-too-old", "f14a-netrc-note",
            ],
            F.Pictures);
        Assert.Equal(["f1a-missing-windows", "f2a-winget-confirm", "f4a-found-again", "f5a-too-old", "f6a-config-fault"], F.RealWindowPictures);
        Assert.All(F.Pictures, name => Assert.Matches(PictureName(), name));
        Assert.Equal(F.Pictures.Count, F.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(F.RealWindowPictures, name => Assert.Contains(name, F.Pictures));
    }
}
