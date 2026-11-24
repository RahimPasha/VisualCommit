// Part F's four launches of the 8a pass (docs/test-reports/phase-8a.md, F1, F2, F4, F5, F6, F13 and "The
// real window (part F)"; docs/phase-8a-design.md 1.2, 1.14, 5.3, 12.3.2, 12.3.4 launches 7 to 10, 12.3.5;
// D423, D444, D446, D449, D453): the first-run screen with git missing and winget found, its winget
// confirm cancelled, the screen by real keys and Enter on "Check again", then the machine's git chosen
// through Windows' Open dialog; a too-old git and a git that cannot read its configuration, both through
// the stand-in for git; and the machine's git with a broken global file. Nothing is pressed that leaves
// the app: not the confirm's Install, not Download, Copy, Show the file or a link, no F1 (D444).
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;
using VisualCommit.App.ViewModels.Settings;
using VisualCommit.Core;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;
using Xunit.Sdk;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase8aRealWindowPass
{
    /// <summary>
    /// The Info line each search writes that no setting answers while the stand-in is <c>none</c>, and
    /// nothing else (design 1.17, 12.3.5): F13's "one more search logged" is one more of these.
    /// </summary>
    private const string NotLookedForLine = "Git was not looked for on this computer: " + GitLocator.StandInVariable + " names no git program (" + F.MissingLaunchStandIn + ").";

    /// <summary>The text before the path in the log's Info line of a git in use (design 1.2), which Choose's probe and the search after it both write (design 12.3.5).</summary>
    private const string UsingGitStart = "Using Git ";

    /// <summary>The log's Info line of a git in use, <c>Using Git {version} at {path}.</c> (design 1.2): its version and its path, as the app was handed it.</summary>
    [GeneratedRegex(@"Using Git (?<version>\S+) at (?<path>.+)\.\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex UsingGitLine();

    /// <summary>The log line a winget start writes (design 5.3): none may come in launch 7 (design 12.3.2).</summary>
    private const string StartedInTerminal = "Started in a terminal:";

    /// <summary>The programs a winget start would run, none of which the app may have started in launch 7 (design 12.3.2).</summary>
    private static readonly string[] WingetStartPrograms = ["cmd.exe", "winget.exe"];

    /// <summary>The screen's buttons with git missing and winget found, in their order (F1).</summary>
    private static readonly (string Id, string Text)[] MissingWithWinget =
        [(F.WingetId, F.InstallWithWinget), (F.DownloadId, F.DownloadGitForWindows), (F.ChooseId, F.ChooseGitExe), (F.CheckAgainId, F.CheckAgain)];

    /// <summary>The screen's buttons for a too-old git found on the machine, the stand-in's (F5; D453).</summary>
    private static readonly (string Id, string Text)[] TooOldWithWinget =
        [(F.WingetId, F.UpdateWithWinget), (F.DownloadId, F.DownloadGitForWindows), (F.ChooseId, F.ChooseGitExe), (F.CheckAgainId, F.CheckAgain)];

    /// <summary>The screen's buttons for a broken configuration (F6): no winget for a git that does not work (D453).</summary>
    private static readonly (string Id, string Text)[] ConfigFaultOffers =
        [(F.DownloadId, F.DownloadGitForWindows), (F.ShowFileId, F.ShowTheFile), (F.ChooseId, F.ChooseGitExe), (F.CheckAgainId, F.CheckAgain)];

    /// <summary>The screen's one link on Windows (none to git-scm.com's other ways there, design 5.9).</summary>
    private static readonly (string Id, string Text)[] GuideLinkOnly = [(F.GuideLinkId, F.HowVisualCommitFindsGit)];

    /// <summary>
    /// Launch 7 (design 12.3.4; the twin of part F's method 1, <c>Check_F1_F2_F13_F4_missing_offers_keys_and_found_again</c>),
    /// one tab restored on a fresh "graph", dark, 1100×700, the stand-in <c>none</c> (no git on this computer):
    /// <list type="number">
    /// <item>Before the start, winget looked for as the app looks for it, over the test process's
    /// <c>PATH</c>, which the launch passes on: none fails the method (D449); found, run.txt names it. The
    /// machine's git, D446's, found the same way.</item>
    /// <item>F1: the restore fails as the twin's (one <c>New tab</c>); a wait until the screen shows with
    /// Missing W's guidance, <c>Choose git.exe…</c> and the winget button; the screen's texts, its four
    /// buttons and its link; <c>GitStatus</c> <c>Git not found</c> with no help text, <c>Set up Git…</c>;
    /// the stand-in's Warning for <c>none</c> once; <c>f1a-missing-windows</c>.</item>
    /// <item>F2: a real click on the winget button; the confirm <c>Install Git with winget?</c>, Cancel
    /// focused; <c>f2a-winget-confirm</c>; a real click on Cancel; no confirm within 5 s, else the method
    /// fails before any further key; no <c>Started in a terminal:</c> line, no <c>cmd.exe</c> or
    /// <c>winget.exe</c> started by the app (design 12.3.2); the screen unchanged.</item>
    /// <item>F13: real F6, at least one and at most four, until the winget button has the focus; real Tab
    /// through the buttons, each waited on by the focused element; a real Enter on <c>Check again</c>: one more
    /// search logged, still Missing, and <c>Check again</c> focused again once the search has ended.</item>
    /// <item>F4: a real click on <c>Choose git.exe…</c>; the machine's git through Windows' Open dialog
    /// <c>Choose git.exe</c>, its path set through the file name box's Value pattern and its Enter sent only
    /// while the dialog is the foreground window with that box focused (design 12.3.3); within 60 s the
    /// screen gone and the restored repository opened again; the path the dialog handed the app, read from
    /// the probe's <c>Using Git {v} at {path}.</c>, that git (<c>GitFileNames.SamePath</c>: the dialog may
    /// spell it otherwise, run.txt says whether it did); <c>GitStatus</c> <c>Git {v}</c>, its help text
    /// <c>{path}, chosen in Settings</c> and settings.json's <c>gitExecutable</c> that path exactly;
    /// <c>f4a-found-again</c>. run.txt notes the per-user recent lists the confirmed dialog writes.</item>
    /// </list>
    /// The app closed by its window, exit code 0; the repository as it was.
    /// </summary>
    [Fact]
    public async Task Check_F_missing_offers_keys_and_choose()
    {
        using var notes = NoteLaunch(nameof(Check_F_missing_offers_keys_and_choose));
        AssertDesktopReady();
        WingetOnThisMachine();
        var git = MachineGit();
        var version = await GitWords.VersionAsync();
        Note($"the machine's git's version (git --version, as the app reads it): {version}");
        using var repo = await Scenarios.GraphAsync();
        var name = NameOf(repo);
        Assert.Equal(F.MissingLaunchScenario, name);
        var exact = await repo.ExactStateAsync();
        var data = Run.NewDataDirectory();
        using var app = Launch(data, OneTab(repo.Path), RealApp.StandInEnvironment(F.MissingLaunchStandIn));

        // F1: the restore failed; the screen with its guidance; its texts, buttons and link; the status bar.
        WaitForFirstRunScreen(app, guidance: F.MissingGuidanceWindows, winget: true);
        var tabs = app.StripTabs().Select(tab => tab.Title).ToList();
        Note($"F1: the tab strip holds {string.Join(" | ", tabs)}");
        Assert.Equal([F.NewTab], tabs);
        AssertMissingOnWindows(app, "F1");
        var standInWarnings = CountLogLines(app, F.StandInNoneLine);
        var searches = CountLogLines(app, NotLookedForLine);
        Note($"F1: the log has the stand-in's Warning \"{F.StandInNoneLine}\" {standInWarnings} times and \"{NotLookedForLine}\" {searches} times");
        Assert.Equal(1, standInWarnings);
        Picture(app, F.F1aMissingWindows, keepPointer: false, _ => []);

        // F2: the winget confirm by a real click; f2a; its Cancel by a real click; nothing started.
        Press(app, ShownOne(app, F.WingetId));
        var confirm = WaitForWingetConfirm(app, F.InstallConfirmTitle, F.InstallConfirmDetail, F.Install);
        Picture(app, F.F2aWingetConfirm, keepPointer: false, _ => []);
        Press(app, app.FindIn(confirm, "CancelButton"));
        AssertConfirmClosedAndNothingStarted(app);
        AssertMissingOnWindows(app, "F2, after Cancel");

        // F13: F6 to the screen's first button, Tab through the buttons, Enter on Check again.
        F6ToTheWingetButton(app);
        foreach (var id in F.FocusOrderWithWinget.Skip(1))
        {
            Keys(app, VirtualKeyShort.TAB);
            app.WaitForFocusOn(id, StepTimeout);
            Note($"F13: Tab, the focus on {id}");
        }

        CheckAgainByEnter(app, searches);

        // F4: Choose git.exe…, the machine's git in Windows' Open dialog, its Enter sent only while the
        // dialog is the foreground window; the screen gone, the restored tab opened again; the status bar
        // names the chosen git; settings.json holds it.
        var mark = app.LogMark();
        Press(app, ShownOne(app, F.ChooseId));
        var guard = app.ChooseFileInDialogGuarded(F.NativePickerTitle, git, F.NativePickerWithin);
        Note($"F4: Windows' Open dialog \"{F.NativePickerTitle}\" was given {git} and confirmed: {guard}");
        Note($"F4: confirming Windows' Open dialog records {Path.GetFileName(git)} and its folder in the current user's recent lists of the common file dialog (ComDlg32's OpenSavePidlMRU and LastVisitedPidlMRU), as every confirmed Open dialog does: a per-user record outside the launch's data folder, left after the run");
        var clock = Stopwatch.StartNew();
        var loaded = AppLogFile.Containing($"] {name}: loaded ");
        var workingTree = AppLogFile.WorkingTreeRead(name);
        app.WaitFor(
            () => app.ShownElements(F.TitleId).Count == 0
                && app.LogLinesSince(mark) is var lines
                && lines.Any(loaded.IsMatch)
                && lines.Any(workingTree.IsMatch),
            $"the first-run screen gone and {name}'s \"loaded\" and working-tree lines logged",
            F.FoundAgainWithin);
        Note(string.Create(CultureInfo.InvariantCulture, $"F4: the screen gone and {name} opened again {clock.Elapsed.TotalSeconds:0.0} s after the dialog closed"));

        // The path the Open dialog handed the app, as its probe of the chosen file logs it (design 12.3.5):
        // the help text and settings.json must hold that path exactly; the dialog alone may spell the
        // machine's git otherwise (case, separators), which is noted and allowed by GitFileNames.SamePath.
        var used = app.LogLinesSince(mark).Select(line => UsingGitLine().Match(line)).Where(match => match.Success).ToList();
        var chosen = used.Count > 0 ? used[0].Groups["path"].Value : null;
        Note($"F4: \"{UsingGitStart}… at …\" logged {used.Count} times since the click on Choose (Choose's probe and the search after it, design 12.3.5), naming {string.Join(" | ", used.Select(match => $"{match.Groups["version"].Value} at {match.Groups["path"].Value}").Distinct(StringComparer.Ordinal))}");
        Note($"F4: the path the Open dialog handed the app: {Quoted(chosen)}; the machine's git {git}; spelled the same: {(chosen == git ? "yes" : "no")}");
        if (chosen is null)
        {
            throw new XunitException($"The log has no \"{UsingGitStart}… at …\" line since the click on Choose: the app did not take the chosen file.");
        }

        Assert.True(GitFileNames.SamePath(chosen, git), $"The Open dialog handed the app {chosen}, not {git}.");

        app.WaitForStripTabs(StepTimeout, name);
        var status = string.Format(CultureInfo.InvariantCulture, F.GitFormat, version);
        app.WaitForText(F.StatusId, status, StepTimeout);
        var help = RealApp.HelpTextOf(ShownOne(app, F.StatusId));
        var saved = SavedGitExecutable(data);
        Note($"F4: GitStatus \"{status}\", its help text \"{help}\"; settings.json's gitExecutable \"{saved}\"; GitSetupButton shown: {app.ShownElements(F.SetUpButtonId).Count > 0}");
        Assert.Equal(string.Format(CultureInfo.InvariantCulture, F.ChosenInSettingsTipFormat, chosen), help);
        Assert.Equal(chosen, saved);
        Assert.Empty(app.ShownElements(F.SetUpButtonId));
        Picture(app, F.F4aFoundAgain, keepPointer: false, _ => []);

        Assert.Equal(0, app.Close());
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// Launch 8 (design 12.3.4; the twin of part F's method 4, <c>Check_F5_too_old_update_offer</c>, to
    /// <c>f5a-too-old</c>), no session written (one empty tab), dark, 1100×700, the stand-in answering
    /// <c>git version 2.29.0</c>:
    /// <list type="number">
    /// <item>winget looked for first, as in launch 7 (D449, D453); the stand-in's path named in run.txt,
    /// with whether it holds a space, and asserted one the app runs (a full path to an <c>.exe</c>, D449).</item>
    /// <item>A wait until the screen shows with TooOld W's guidance, <c>Choose git.exe…</c> and the winget
    /// button; the title <c>Git 2.29.0 is too old</c>, the minimum from <c>GitVersion.Minimum</c>,
    /// <c>GitSetupPath</c> the stand-in's path; the buttons with <c>Update with winget…</c> (the stand-in's
    /// git counts as found on the machine, D453) and Download, none pressed; <c>GitStatus</c>'s help text
    /// <c>{path}, from VISUALCOMMIT_GIT_STAND_IN (a test switch)</c>; the stand-in's Warning once.</item>
    /// <item><c>f5a-too-old</c> with the pointer at part F's resting point, off <c>GitStatus</c>'s text and
    /// its tooltip (design 12.3.5); the path differs from the twin's.</item>
    /// </list>
    /// The app closed by its window, exit code 0.
    /// </summary>
    [Fact]
    public void Check_F5_too_old_stand_in()
    {
        using var notes = NoteLaunch(nameof(Check_F5_too_old_stand_in));
        AssertDesktopReady();
        WingetOnThisMachine();
        var standIn = StandIn();
        var data = Run.NewDataDirectory();
        using var app = Launch(data, session: null, RealApp.StandInEnvironment(standIn, version: F.TooOldVersionLine));

        WaitForFirstRunScreen(app, guidance: F.TooOldGuidanceWindows, winget: true);
        AssertFirstRunScreen(
            app,
            "F5",
            string.Format(CultureInfo.InvariantCulture, F.TooOldTitleFormat, F.TooOldVersion),
            string.Format(CultureInfo.InvariantCulture, F.TooOldBodyFormat, F.Minimum, F.TooOldVersion),
            standIn,
            gitSaid: null,
            F.TooOldGuidanceWindows);
        AssertFirstRunOffers(app, "F5", TooOldWithWinget, GuideLinkOnly);
        AssertFirstRunStatus(
            app,
            "F5",
            string.Format(CultureInfo.InvariantCulture, F.GitTooOldStatusFormat, F.TooOldVersion, F.Minimum),
            string.Format(CultureInfo.InvariantCulture, F.StandInTipFormat, standIn),
            F.SetUpGit);

        var lookedOnlyAt = string.Format(CultureInfo.InvariantCulture, F.StandInAtLineFormat, standIn);
        var tooOld = $"Git {F.TooOldVersion} at {standIn} is older than the minimum, {F.Minimum}.";
        var warnings = CountLogLines(app, lookedOnlyAt);
        Note($"F5: the log has \"{lookedOnlyAt}\" {warnings} times and \"{tooOld}\" {CountLogLines(app, tooOld)} times");
        Assert.Equal(1, warnings);

        // f5a at part F's resting point: GitStatus's text covers the neutral point.
        RestAtFirstRunPoint(app, F.F5aTooOld);
        Picture(app, F.F5aTooOld, keepPointer: true, _ => NoToolTip(app, F.F5aTooOld));

        Assert.Equal(0, app.Close());
    }

    /// <summary>
    /// Launch 9 (design 12.3.4; the twin of part F's method 5, <c>Check_F6_config_fault_show_the_file</c>), no
    /// session written (one empty tab), dark, 1100×700, the stand-in failing with git's words for line 1 of
    /// a made-up global file:
    /// <list type="number">
    /// <item>The stand-in's path named in run.txt, with whether it holds a space.</item>
    /// <item>A wait until the screen shows with the fault's guidance and <c>Choose git.exe…</c>; the title
    /// <c>Git is not working</c>; <c>GitSetupGitSaid</c> git's line; the fault's guidance; Download and
    /// <c>Show the file</c> shown, neither pressed (design 12.3.2), no winget (D453); <c>GitStatus</c>
    /// <c>Git is not working</c>, its help text the stand-in's; <c>f6a-config-fault</c>.</item>
    /// </list>
    /// The app closed by its window, exit code 0.
    /// </summary>
    [Fact]
    public void Check_F6_config_fault_stand_in()
    {
        using var notes = NoteLaunch(nameof(Check_F6_config_fault_stand_in));
        AssertDesktopReady();
        var standIn = StandIn();
        var data = Run.NewDataDirectory();
        using var app = Launch(data, session: null, RealApp.StandInEnvironment(standIn, failure: F.ConfigFaultText));
        var guidance = string.Format(CultureInfo.InvariantCulture, F.ConfigFaultGuidanceFormat, 1, F.DemoGitConfig);

        WaitForFirstRunScreen(app, guidance: guidance);
        AssertFirstRunScreen(app, "F6", F.NotWorkingTitle, F.NotWorkingBody, standIn, F.ConfigFaultText, guidance);
        AssertFirstRunOffers(app, "F6", ConfigFaultOffers, GuideLinkOnly);
        AssertFirstRunStatus(app, "F6", F.GitNotWorking, string.Format(CultureInfo.InvariantCulture, F.StandInTipFormat, standIn), F.SetUpGit);

        var noVersion = $"Git at {standIn} did not report its version: exit 128, \"{F.ConfigFaultText}\".";
        var lookedOnlyAt = string.Format(CultureInfo.InvariantCulture, F.StandInAtLineFormat, standIn);
        Note($"F6: the log has \"{noVersion}\" {CountLogLines(app, noVersion)} times and \"{lookedOnlyAt}\" {CountLogLines(app, lookedOnlyAt)} times");
        Picture(app, F.F6aConfigFault, keepPointer: false, _ => []);

        Assert.Equal(0, app.Close());
    }

    /// <summary>
    /// Launch 10 (design 12.3.4; the twin of part F's method 6, <c>Check_F6b_real_git_broken_config</c>), no
    /// session written (one empty tab), dark, 1100×700, no stand-in: <c>[user</c> written on line 1 of the
    /// launch's own global git file (<c>GitIsolation.SessionGlobalConfigFile</c>) before the start, through
    /// <see cref="RealApp.SeedGlobalGitConfig"/>. A wait until the screen shows with guidance that starts <c>Git cannot read line 1 of</c>;
    /// the title <c>Git is not working</c>; <c>GitSetupGitSaid</c> starts <c>fatal: bad config line 1 in
    /// file</c> and names that file (separators and case aside), the guidance names it too; <c>Show the
    /// file</c> shown, not pressed; <c>GitSetupPath</c> the machine's git as the test process finds it, over
    /// the <c>PATH</c> the launch passes on (design 12.3.5); git's line in run.txt. No picture: the path and
    /// the file differ from machine to machine. The app closed by its window, exit code 0.
    /// </summary>
    [Fact]
    public void Check_F6b_real_git_broken_config()
    {
        using var notes = NoteLaunch(nameof(Check_F6b_real_git_broken_config));
        AssertDesktopReady();
        var git = MachineGit();
        var data = Run.NewDataDirectory();
        var file = GitIsolation.SessionGlobalConfigFile(data);
        RealApp.SeedGlobalGitConfig(data, F.BrokenConfigLine + "\n");
        Note($"F6b: \"{F.BrokenConfigLine}\" written on line 1 of {file}");
        using var app = Launch(data, session: null);

        WaitForFirstRunScreen(app, guidancePrefix: F.ConfigFaultGuidancePrefix);
        var title = OneShownName(app, F.TitleId);
        var said = OneShownName(app, F.GitSaidId);
        var guidance = OneShownName(app, F.GuidanceId);
        var path = OneShownName(app, F.PathId);
        Note($"F6b: the title \"{title}\"; git said \"{said}\"; the guidance \"{guidance}\"; GitSetupPath \"{path}\"");
        Assert.Equal(F.NotWorkingTitle, title);
        Assert.NotNull(said);
        Assert.StartsWith(F.ConfigFaultPrefix, said, StringComparison.Ordinal);
        var named = said[F.ConfigFaultPrefix.Length..].Split('\n')[0].TrimEnd('\r');
        Assert.True(GitFileNames.SamePath(file, named), $"Git named {named}, not the launch's global file {file}.");
        Assert.Equal(string.Format(CultureInfo.InvariantCulture, F.ConfigFaultGuidanceFormat, 1, named), guidance);
        Assert.Single(app.ShownElements(F.ShowFileId));
        Assert.Equal(git, path);

        var status = OneShownName(app, F.StatusId);
        var help = RealApp.HelpTextOf(ShownOne(app, F.StatusId));
        var warning = app.LogLines().FirstOrDefault(line => line.Contains("[WRN]", StringComparison.Ordinal) && line.Contains(F.ConfigFaultPrefix + named, StringComparison.Ordinal));
        Note($"F6b: GitStatus \"{status}\", its help text \"{help}\"; the log's Warning: {warning ?? "none"}");
        Assert.Equal(F.GitNotWorking, status);

        Assert.Equal(0, app.Close());
    }

    // ---- Part F's steps ----

    /// <summary>
    /// Looks for winget as the app does (<c>ProgramSearch.FindOnPath("winget.exe", PATH, ';', File.Exists)</c>,
    /// design 1.3, 5.3) over the test process's <c>PATH</c>, which <see cref="RealApp.Launch(string, IReadOnlyDictionary{string, string?}?)"/>
    /// passes on unchanged: none fails the method with <see cref="Phase8aExpectations.F.WingetMissingMessage"/>,
    /// never "Not run" (D449, D453); found, run.txt names its path.
    /// </summary>
    private void WingetOnThisMachine()
    {
        var winget = ProgramSearch.FindOnPath("winget.exe", Environment.GetEnvironmentVariable("PATH"), ';', File.Exists);
        if (winget is null)
        {
            Note(F.WingetMissingMessage);
            throw new XunitException(F.WingetMissingMessage);
        }

        Note($"winget: {winget}, on the PATH the launch passes on (D449)");
    }

    /// <summary>The machine's git as the test process finds it, D446's (<c>GitLocator.FindExecutable(GitSearchContext.FromSystem())</c>), named in run.txt.</summary>
    private string MachineGit()
    {
        var git = GitLocator.FindExecutable(GitSearchContext.FromSystem());
        Note($"the machine's git (D446): {git ?? "none found"}");
        return git ?? throw new XunitException("No git was found on this machine's PATH or in its usual folders: part F's launches need it (D446).");
    }

    /// <summary>
    /// The stand-in for git in this project's output (<see cref="RealApp.StandInPath"/>): its path and
    /// whether it holds a space in run.txt; a missing one fails with the report's message, also in run.txt;
    /// it must be a path the app runs (a full path to an <c>.exe</c>, D449), else the app takes it for
    /// <c>none</c> (design 12.3.5).
    /// </summary>
    private string StandIn()
    {
        string standIn;
        try
        {
            standIn = RealApp.StandInPath;
        }
        catch (FileNotFoundException ex)
        {
            Note(ex.Message);
            throw;
        }

        Note($"the stand-in: {standIn}; its path holds a space: {(standIn.Contains(' ', StringComparison.Ordinal) ? "yes" : "no")}");
        Assert.True(GitLocator.IsRunnablePath(standIn), $"The stand-in's path {standIn} is not one the app runs (a full path to an .exe file, D449): the app would take it for none.");
        return standIn;
    }

    /// <summary>
    /// Waits, at most <see cref="LoadTimeout"/>, the first wait after a start as the other launches' first
    /// waits are, until the first-run screen shows with its guidance (the report's "Waiting for the screen";
    /// design 5.3: it shows only once its guidance is read, after the start's search for git):
    /// <c>GitSetupGuidance</c> reads <paramref name="guidance"/> or starts with <paramref name="guidancePrefix"/>,
    /// <c>GitSetupChooseButton</c> reads <c>Choose git.exe…</c>, and with <paramref name="winget"/> the winget
    /// button is shown. A timeout names what did not come.
    /// </summary>
    private void WaitForFirstRunScreen(RealApp app, string? guidance = null, string? guidancePrefix = null, bool winget = false)
    {
        var missing = "nothing read yet";
        var clock = Stopwatch.StartNew();
        try
        {
            app.WaitFor(() => (missing = FirstRunScreenMissing(app, guidance, guidancePrefix, winget)) is null, "the first-run screen with its guidance", LoadTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} Not yet: {missing}.", ex);
        }

        Note(string.Create(CultureInfo.InvariantCulture, $"the first-run screen shows with its guidance after {clock.Elapsed.TotalSeconds:0.0} s"));
    }

    /// <summary>One look for <see cref="WaitForFirstRunScreen"/>: what has not come yet, or null.</summary>
    private static string? FirstRunScreenMissing(RealApp app, string? guidance, string? guidancePrefix, bool winget)
    {
        List<string> missing = [];
        var read = app.NamesOnScreen(F.GuidanceId);
        var text = read is [var only] ? only : null;
        if (guidance is not null && text != guidance)
        {
            missing.Add($"{F.GuidanceId} \"{guidance}\" (it reads {Quoted(read)})");
        }

        if (guidancePrefix is not null && text?.StartsWith(guidancePrefix, StringComparison.Ordinal) != true)
        {
            missing.Add($"{F.GuidanceId} starting \"{guidancePrefix}\" (it reads {Quoted(read)})");
        }

        var choose = app.NamesOnScreen(F.ChooseId);
        if (choose is not [F.ChooseGitExe])
        {
            missing.Add($"{F.ChooseId} \"{F.ChooseGitExe}\" (it reads {Quoted(choose)})");
        }

        if (winget && app.ShownElements(F.WingetId).Count != 1)
        {
            missing.Add($"{F.WingetId} shown");
        }

        return missing.Count == 0 ? null : string.Join("; ", missing);
    }

    /// <summary>F1's screen, status bar and offers, after F1 and again after F2's Cancel and F13's search: Missing on Windows, winget found.</summary>
    private void AssertMissingOnWindows(RealApp app, string step)
    {
        AssertFirstRunScreen(app, step, F.MissingTitle, F.MissingBodyWindows, path: null, gitSaid: null, F.MissingGuidanceWindows);
        AssertFirstRunOffers(app, step, MissingWithWinget, GuideLinkOnly);
        AssertFirstRunStatus(app, step, F.GitNotFound, helpText: null, F.SetUpGit);
    }

    /// <summary>
    /// The screen's texts as UI Automation names them, noted in run.txt, then asserted: the title, the
    /// body, the path and git's words (null: not shown), the guidance; no command, Copy or error shown.
    /// The screen's own name and help text (its title and body, design 5.3) are noted, not asserted.
    /// </summary>
    private void AssertFirstRunScreen(RealApp app, string step, string title, string body, string? path, string? gitSaid, string guidance)
    {
        var seen = (Title: OneShownName(app, F.TitleId), Body: OneShownName(app, F.BodyId), Path: OneShownName(app, F.PathId), GitSaid: OneShownName(app, F.GitSaidId), Guidance: OneShownName(app, F.GuidanceId));
        var screen = app.ShownElements(F.ScreenId) is [var view] ? $"\"{view.Name}\", help text \"{RealApp.HelpTextOf(view)}\"" : "not in UI Automation's tree";
        var label = ShownTextsNamed(app, F.GitSaid);
        Note($"{step}: the screen {screen}; title \"{seen.Title}\"; body \"{seen.Body}\"; path {Quoted(seen.Path)}; \"{F.GitSaid}\" shown {label} times; git said {Quoted(seen.GitSaid)}; guidance \"{seen.Guidance}\"");
        Assert.Equal(title, seen.Title);
        Assert.Equal(body, seen.Body);
        Assert.Equal(path, seen.Path);
        Assert.Equal(gitSaid, seen.GitSaid);
        Assert.Equal(guidance, seen.Guidance);
        Assert.Empty(app.ShownElements(F.CommandId));
        Assert.Empty(app.ShownElements(F.CopyId));
        Assert.Empty(app.ShownElements(F.ErrorId));
    }

    /// <summary>
    /// The screen's buttons and links shown, in their order on the screen (top to bottom, then left to
    /// right), each enabled and named by its text; every other button and link of the screen not shown.
    /// Noted in run.txt, then asserted.
    /// </summary>
    private void AssertFirstRunOffers(RealApp app, string step, (string Id, string Text)[] buttons, (string Id, string Text)[] links)
    {
        var shownButtons = ShownInOrder(app, F.ButtonOrder);
        var shownLinks = ShownInOrder(app, F.LinkOrder);
        string Described(List<(string Id, AutomationElement Element)> shown) =>
            string.Join(", ", shown.Select(entry => $"{entry.Id} \"{entry.Element.Name}\"{(entry.Element.IsEnabled ? string.Empty : " (disabled)")}"));
        Note($"{step}: the buttons {Described(shownButtons)}; the links {Described(shownLinks)}");
        Assert.Equal(buttons.Select(button => button.Id), shownButtons.Select(entry => entry.Id));
        Assert.Equal(links.Select(link => link.Id), shownLinks.Select(entry => entry.Id));
        foreach (var ((id, text), (_, element)) in buttons.Concat(links).Zip(shownButtons.Concat(shownLinks)))
        {
            Assert.True(element.IsEnabled, $"{id} is disabled.");
            Assert.Equal(text, element.Name);
        }
    }

    /// <summary>The status bar's git: <c>GitStatus</c>'s text and help text (null: none), and <c>GitSetupButton</c>'s name (null: not shown). Noted in run.txt, then asserted.</summary>
    private void AssertFirstRunStatus(RealApp app, string step, string text, string? helpText, string? setUp)
    {
        var status = ShownOne(app, F.StatusId);
        var help = RealApp.HelpTextOf(status);
        var button = app.NamesOnScreen(F.SetUpButtonId);
        Note($"{step}: GitStatus \"{status.Name}\", its help text \"{help}\"; GitSetupButton {(button.Count == 0 ? "not shown" : Quoted(button))}");
        Assert.Equal(text, status.Name);
        Assert.Equal(helpText ?? string.Empty, help);
        if (setUp is null)
        {
            Assert.Empty(button);
        }
        else
        {
            Assert.Equal([setUp], button);
        }
    }

    /// <summary>
    /// F2's and the confirm's reads (design 5.3, 12.3.4 launch 7): the confirmation shown with
    /// <paramref name="title"/>, git-scm.com's line as its question, <paramref name="detail"/>, its buttons
    /// <c>Cancel</c> and <paramref name="confirm"/>, and <c>CancelButton</c> UI Automation's focused element.
    /// The confirm button is read, never pressed (design 12.3.2). Returns the dialog.
    /// </summary>
    private AutomationElement WaitForWingetConfirm(RealApp app, string title, string detail, string confirm)
    {
        app.WaitFor(() => app.ShownElements("ConfirmationDialog").Count == 1, "the winget confirm", StepTimeout);
        app.WaitForText("ConfirmTitle", title, StepTimeout);
        app.WaitForFocusOn("CancelButton", StepTimeout);
        var dialog = app.ShownElements("ConfirmationDialog")[0];
        var question = OneShownName(app, "ConfirmQuestion");
        var said = OneShownName(app, "ConfirmDetail");
        var cancel = app.FindIn(dialog, "CancelButton").Name;
        var ok = app.FindIn(dialog, "ConfirmButton").Name;
        Note($"F2: the confirm \"{title}\", its question \"{question}\", its detail \"{said}\", its buttons \"{cancel}\" (focused) and \"{ok}\"");
        Assert.Equal(F.ConfirmQuestion, question);
        Assert.Equal(detail, said);
        Assert.Equal(F.Cancel, cancel);
        Assert.Equal(confirm, ok);
        return dialog;
    }

    /// <summary>
    /// After the confirm's Cancel (design 12.3.2): no <c>ConfirmationDialog</c> within
    /// <see cref="Phase8aExpectations.F.ConfirmClosesWithin"/>, else the method fails before any further
    /// key; then no <c>Started in a terminal:</c> line in the log and no <c>cmd.exe</c> or <c>winget.exe</c>
    /// whose parent is the app (<c>NativeMethods.Processes()</c>). Each noted in run.txt.
    /// </summary>
    private void AssertConfirmClosedAndNothingStarted(RealApp app)
    {
        try
        {
            app.WaitUntilGone("ConfirmationDialog", F.ConfirmClosesWithin);
        }
        catch (TimeoutException ex)
        {
            var message = string.Create(CultureInfo.InvariantCulture, $"The winget confirm is still open {F.ConfirmClosesWithin.TotalSeconds:0} s after its Cancel: no further key is sent (D444). {ex.Message}");
            Note(message);
            throw new XunitException(message);
        }

        var started = app.LogLines().Where(line => line.Contains(StartedInTerminal, StringComparison.Ordinal)).ToList();
        var children = NativeMethods.Processes()
            .Where(process => process.ParentId == app.ProcessId && WingetStartPrograms.Contains(process.ExeName, StringComparer.OrdinalIgnoreCase))
            .Select(process => string.Create(CultureInfo.InvariantCulture, $"{process.ExeName} ({process.Id})"))
            .ToList();
        Note($"F2, after Cancel: the confirm closed; \"{StartedInTerminal}\" lines {started.Count}; cmd.exe or winget.exe started by the app: {(children.Count == 0 ? "none" : string.Join(", ", children))}");
        Assert.Empty(started);
        Assert.Empty(children);
    }

    /// <summary>
    /// F13's first step (design 12.3.4 launch 7, 12.3.5; D104): one real F6 always (after the confirm's
    /// Cancel the winget button may already hold the focus, without the outline), then real F6 again while the
    /// winget button is not UI Automation's focused element, at most <see cref="Phase8aExpectations.F.MostF6Presses"/>
    /// in all; each followed by a wait, at most <see cref="StepTimeout"/>, until the focused element is the
    /// winget button or another than before the press. If the button does not have the focus after the
    /// last, the method fails before any further key. Each press's focus in run.txt.
    /// </summary>
    private void F6ToTheWingetButton(RealApp app)
    {
        var presses = 0;
        do
        {
            var before = FocusNow(app);
            Keys(app, VirtualKeyShort.F6);
            presses++;
            app.WaitFor(
                () => app.FocusedId() == F.WingetId || FocusNow(app) != before,
                string.Create(CultureInfo.InvariantCulture, $"F6 number {presses} to move the focus from {before}"),
                StepTimeout);
            Note(string.Create(CultureInfo.InvariantCulture, $"F13: F6 number {presses}: the focus from {before} to {FocusNow(app)}"));
        }
        while (app.FocusedId() != F.WingetId && presses < F.MostF6Presses);

        if (app.FocusedId() != F.WingetId)
        {
            var message = string.Create(CultureInfo.InvariantCulture, $"{F.WingetId} does not have the keyboard focus after {presses} F6 presses; it is on {FocusNow(app)}. No further key is sent.");
            Note(message);
            throw new XunitException(message);
        }
    }

    /// <summary>
    /// F13's last step (design 5.3, 12.3.4 launch 7, 12.3.5): a real Enter, sent only while
    /// <c>GitSetupCheckAgainButton</c> is UI Automation's focused element (design 12.3.2); one more search
    /// logged (<see cref="NotLookedForLine"/>, which the stand-in <c>none</c> writes at each search) within
    /// <see cref="StepTimeout"/>; the search's end, <c>Check again</c> named so and enabled again; then the
    /// button UI Automation's focused element again, waited for (the app posts the focus back, never at
    /// once); still Missing, and exactly one more search. <paramref name="searchesBefore"/> is the count
    /// before.
    /// </summary>
    private void CheckAgainByEnter(RealApp app, int searchesBefore)
    {
        Assert.True(app.FocusedId() == F.CheckAgainId, $"The focus is on {FocusNow(app)}, not {F.CheckAgainId}: no Enter is sent (D444).");
        KeysOn(app, new FocusTarget(F.CheckAgainId, F.CheckAgain), VirtualKeyShort.RETURN);
        var clock = Stopwatch.StartNew();
        app.WaitFor(() => CountLogLines(app, NotLookedForLine) > searchesBefore, "one more search for git logged", StepTimeout);
        app.WaitFor(
            () => app.ShownElements(F.CheckAgainId) is [var button] && button.Name == F.CheckAgain && button.IsEnabled,
            $"{F.CheckAgainId} to read \"{F.CheckAgain}\", enabled, once the search has ended",
            StepTimeout);
        app.WaitForFocusOn(F.CheckAgainId, StepTimeout);
        var searches = CountLogLines(app, NotLookedForLine);
        Note(string.Create(CultureInfo.InvariantCulture, $"F13: Enter on {F.CheckAgainId}; searches logged {searchesBefore}, then {searches}; the focus back on {F.CheckAgainId} {clock.Elapsed.TotalSeconds:0.0} s after the Enter"));
        Assert.Equal(searchesBefore + 1, searches);
        AssertMissingOnWindows(app, "F13, after the search");
    }

    /// <summary>
    /// Rests the real pointer at part F's resting point (<see cref="Phase8aExpectations.F.RestingPoint"/>, design
    /// 12.3.5), the status bar's empty middle, instead of the neutral point, which a too-old git's long
    /// <c>GitStatus</c> text covers and where a real pointer would open its tooltip; then waits, at most
    /// <see cref="StepTimeout"/>, until no tooltip shows, which clears one an earlier hover left open. That
    /// wait cannot show that the resting point opens none, since a tooltip opens only after the hover
    /// delay: the picture's read after its capture (<c>NoToolTip</c>) is the check. The capture that
    /// follows keeps the pointer there.
    /// </summary>
    private void RestAtFirstRunPoint(RealApp app, string picture)
    {
        var (x, y) = ((float)F.RestingPoint.X, (float)F.RestingPoint.Y);
        app.SetPointer(x, y);
        app.WaitFor(() => ToolTipsShown(app).Count == 0, $"no tooltip left open in the window before {picture}, the pointer at part F's resting point", StepTimeout);
        Note(string.Create(CultureInfo.InvariantCulture, $"{picture}: the pointer at part F's resting point ({x:0.#}, {y:0.#}), not the neutral point, which GitStatus's text covers; no tooltip left open when it came to rest (the read after the capture is the check that none opens there)"));
    }

    /// <summary>The one element <paramref name="automationId"/> on screen; fails when there is not exactly one.</summary>
    private static AutomationElement ShownOne(RealApp app, string automationId) =>
        app.ShownElements(automationId) switch
        {
            [var only] => only,
            var found => throw new XunitException($"{found.Count} elements {automationId} are on screen, not one."),
        };

    /// <summary>The name (text) of the one element <paramref name="automationId"/> on screen, null when none is; fails when more than one is.</summary>
    private static string? OneShownName(RealApp app, string automationId) =>
        app.NamesOnScreen(automationId) switch
        {
            [] => null,
            [var only] => only,
            var found => throw new XunitException($"{found.Count} elements {automationId} are on screen, not one."),
        };

    /// <summary>The shown elements of <paramref name="ids"/>, each with its id, in their order on the screen: top to bottom, then left to right.</summary>
    private static List<(string Id, AutomationElement Element)> ShownInOrder(RealApp app, IEnumerable<string> ids) =>
        [.. ids.SelectMany(id => app.ShownElements(id).Select(element => (Id: id, Element: element, Bounds: app.BoundsOf(element))))
            .OrderBy(entry => entry.Bounds.Top)
            .ThenBy(entry => entry.Bounds.Left)
            .Select(entry => (entry.Id, entry.Element))];

    /// <summary>How many text elements named <paramref name="name"/> are on screen (a label without an automation id, such as <c>Git said:</c>).</summary>
    private static int ShownTextsNamed(RealApp app, string name) =>
        app.Window.FindAllDescendants(condition => condition.ByControlType(ControlType.Text).And(condition.ByName(name)))
            .Count(element => !element.IsOffscreen && element.BoundingRectangle is { Width: > 0, Height: > 0 });

    /// <summary>UI Automation's focused element in words, for a comparison before and after a key and for run.txt: its id, type, name and bounds; <c>nothing</c> without one.</summary>
    private static string FocusNow(RealApp app)
    {
        if (app.FocusedElement() is not { } focused)
        {
            return "nothing";
        }

        try
        {
            var id = focused.Properties.AutomationId.ValueOrDefault;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{(string.IsNullOrEmpty(id) ? "(no id)" : id)} ({focused.Properties.ControlType.ValueOrDefault}) \"{focused.Properties.Name.ValueOrDefault}\" at {focused.BoundingRectangle}");
        }
        catch (Exception ex) when (ex is COMException or FlaUI.Core.Exceptions.FlaUIException)
        {
            return "an element that could not be read";
        }
    }

    /// <summary>How many lines of the app's log hold <paramref name="text"/>.</summary>
    private static int CountLogLines(RealApp app, string text) => app.LogLines().Count(line => line.Contains(text, StringComparison.Ordinal));

    /// <summary>The data folder's settings.json's <c>gitExecutable</c>, or null when it has none or there is no file.</summary>
    private static string? SavedGitExecutable(string data)
    {
        var file = new AppPaths(data).SettingsFile;
        if (!File.Exists(file))
        {
            return null;
        }

        using var json = JsonDocument.Parse(ReadShared(file));
        return json.RootElement.TryGetProperty("gitExecutable", out var value) ? value.GetString() : null;
    }

    private static string Quoted(string? text) => text is null ? "nothing" : $"\"{text}\"";

    private static string Quoted(IReadOnlyList<string> texts) => texts.Count == 0 ? "nothing (not on screen)" : string.Join(" | ", texts.Select(text => $"\"{text}\""));
}
