using Avalonia.Media;
using VisualCommit.Core.Git;

namespace VisualCommit.VisualTests.Phase8a;

public static partial class Phase8aExpectations
{
    /// <summary>
    /// What part F's checks compare against (docs/test-reports/phase-8a.md, "Part F: first run: git's
    /// detection, guidance and setting"; docs/phase-8a-design.md 1.2 to 1.4, 1.14, 1.15, 5.3, 5.5; D405,
    /// D423, D424, D449, D453): every text of the first-run screen, its confirms, Choose, the status
    /// bar, the settings row and the netrc note, word for word; the automation ids; the fake's made-up
    /// values and answers; the colours by token; the picture names; and, for the real window's
    /// launches 7 to 10 (design 12.3.4), each launch's session, scenario, stand-in and waits. Written
    /// from the report, not from the app; the real-window pass links this file, so it uses the BCL,
    /// Avalonia and Core only.
    /// </summary>
    public static class F
    {
        // ---- The screen's titles, bodies and guidance (5.3) ---------------------------------

        public const string MissingTitle = "VisualCommit needs Git";
        public const string ChosenMissingTitle = "The chosen Git is gone";
        public const string TooOldTitleFormat = "Git {0} is too old";
        public const string NotWorkingTitle = "Git is not working";
        public const string HelperMissingTitle = "Part of VisualCommit is missing";

        public const string MissingBodyWindows = "VisualCommit works through the Git installed on this computer, and it found none: not on the PATH, nor in Git for Windows' usual install folders.";
        public const string MissingBodyMacOS = "VisualCommit works through the Git installed on this Mac, and it found none.";
        public const string MissingBodyMacOSToolsMissing = " Apple's Command Line Tools, which include Git, are not installed.";
        public const string MissingBodyLinux = "VisualCommit works through the Git installed on this computer, and it found none on the PATH or in the usual folders.";
        public const string ChosenMissingBody = "VisualCommit was set to use the Git at this path, and there is no file there now:";
        public const string ChosenNotRunnableBody = "VisualCommit was set to use the Git at this path, but it runs Git only from a full path to the program itself:";
        public const string TooOldBodyFormat = "VisualCommit needs Git {0} or newer. It found Git {1} at:";
        public const string NotWorkingBody = "VisualCommit found Git at the path below, but it did not report its version.";
        public const string NotWorkingSilentBodyFormat = "VisualCommit found Git at the path below, but it said nothing and exited with code {0}.";
        public const string NotStartedBodyFormat = "VisualCommit found Git at the path below, but it could not be started: {0}";
        public const string HelperMissingBody = "Git works, but VisualCommit's helper for git's questions is missing. It was looked for at:";
        public const string GitSaid = "Git said:";

        public const string MissingGuidanceWindows = "Install Git for Windows, then press Check again. VisualCommit does not need to restart.";
        public const string MissingGuidanceMacOSToolsMissing = "Install the Command Line Tools, which macOS asks you to confirm, or install Git with Homebrew, then press Check again. With Homebrew, in Terminal:";
        public const string MissingGuidanceMacOS = "Install Git with Homebrew, then press Check again. In Terminal:";
        public const string MissingGuidanceLinuxKnownFormat = "Install it with your distribution's package manager, in a terminal, then press Check again. On {0}:";
        public const string MissingGuidanceLinuxUnknown = "Install the package named git with your distribution's package manager, then press Check again.";
        public const string ChosenMissingGuidanceFormat = "VisualCommit found another Git at {0}.";
        public const string ChosenMissingGuidanceNone = "No other Git was found on the PATH or in the usual install folders. Choose another, or press Check again once the chosen one is back.";
        public const string TooOldGuidanceWindows = "Update Git for Windows, then press Check again. Its installer updates it in place.";
        public const string TooOldGuidanceMacOS = "Update the Command Line Tools in System Settings, General, Software Update, or install Git with Homebrew, then press Check again. With Homebrew, in Terminal:";
        public const string TooOldGuidanceLinux = "Install a newer Git from your distribution, or choose another Git, then press Check again.";
        public const string ConfigFaultGuidanceFormat = "Git cannot read line {0} of {1}. Correct that line, or rename the file, then press Check again.";
        public const string NotWorkingGuidanceWindows = "Repair or reinstall Git for Windows, then press Check again.";
        public const string NotWorkingGuidanceMacOSToolsBroken = "Apple's Command Line Tools look damaged. Install them again, or select them in Terminal, then press Check again:";
        public const string NotWorkingGuidanceMacOS = "Reinstall Git, then press Check again.";
        public const string NotWorkingGuidanceLinux = "Reinstall git with your distribution's package manager, then press Check again.";
        public const string HelperMissingGuidance = "Without it, git and ssh cannot ask for a password, a passphrase or a host key here, and a signed-in account cannot be used for git. Install VisualCommit again to restore it.";

        public const string BrewCommand = "brew install git";
        public const string SelectToolsCommand = "sudo xcode-select --switch /Library/Developer/CommandLineTools";
        public const string AptCommand = "sudo apt install git";
        public const string DnfCommand = "sudo dnf install git";
        public const string PacmanCommand = "sudo pacman -S git";
        public const string ZypperCommand = "sudo zypper install git";

        // ---- The buttons and links -------------------------------------------------------------

        public const string InstallWithWinget = "Install with winget…";
        public const string UpdateWithWinget = "Update with winget…";
        public const string DownloadGitForWindows = "Download Git for Windows";
        public const string InstallCommandLineTools = "Install the Command Line Tools";
        public const string ShowTheFile = "Show the file";
        public const string ChooseGitExe = "Choose git.exe…";
        public const string ChooseGitProgram = "Choose the git program…";
        public const string UseTheGitItFound = "Use the Git it found";
        public const string UseTheGitOnThePath = "Use the Git on the PATH";
        public const string CheckAgain = "Check again";
        public const string Checking = "Checking…";
        public const string ContinueWithoutIt = "Continue without it";
        public const string Copy = "Copy";
        public const string OtherWaysToInstallGit = "Other ways to install Git";
        public const string HowVisualCommitFindsGit = "How VisualCommit finds Git";
        public const string WhatTheHelperDoes = "What the helper does";

        public const string DownloadForWindows = "https://git-scm.com/install/windows";
        public const string InstallOnMacOS = "https://git-scm.com/install/mac";
        public const string InstallOnLinux = "https://git-scm.com/install/linux";

        // ---- The winget confirms ---------------------------------------------------------------

        public const string WingetLine = "winget install --id Git.Git -e --source winget";
        public const string InstallConfirmTitle = "Install Git with winget?";
        public const string UpdateConfirmTitle = "Update Git with winget?";
        public const string ConfirmQuestion = "VisualCommit opens a terminal window that runs winget install --id Git.Git -e --source winget";
        public const string InstallConfirmDetail = "winget downloads Git for Windows from its community repository, which the Git for Windows project does not maintain, and runs the installer; Windows may ask for permission. When it has finished, close the window and press Check again.";
        public const string UpdateConfirmDetail = "winget downloads the newest Git for Windows from its community repository, which the Git for Windows project does not maintain, and runs the installer, which updates the one installed; Windows may ask for permission. When it has finished, close the window and press Check again.";
        public const string Install = "Install";
        public const string Update = "Update";
        public const string Cancel = "Cancel";

        // ---- Choose ----------------------------------------------------------------------------

        public const string ChooseGitExeTitle = "Choose git.exe";
        public const string ChooseGitProgramTitle = "Choose the git program";
        public const string ChooseTooOldFormat = "Git {0} at this path is too old: VisualCommit needs {1} or newer.";
        public const string ChooseNoVersionFormat = "This Git did not report its version. Git said: {0}";
        public const string ChooseNotStartedFormat = "This file could not be started: {0}";
        public const string ProgramMissing = "There is no file at this path.";
        public const string ProgramNotExe = "Choose the program itself (an .exe file): a .cmd or .bat file would run through cmd.exe.";
        public const string StartFailedFormat = "{0} could not be started: {1}";

        // ---- The status bar --------------------------------------------------------------------

        public const string LookingForGit = "Looking for Git...";
        public const string GitFormat = "Git {0}";
        public const string GitTooOldStatusFormat = "Git {0} is too old (needs {1} or newer)";
        public const string GitNotFound = "Git not found";
        public const string GitNotWorking = "Git is not working";
        public const string ChosenGitNotFound = "Chosen Git not found";
        public const string HelperMissingStatusFormat = "Git {0} (helper missing)";
        public const string FoundOnPathTipFormat = "{0}, found on the PATH";
        public const string FoundInGitForWindowsFolderTipFormat = "{0}, found in Git for Windows' install folder";
        public const string FoundInUsualFolderTipFormat = "{0}, found in a usual install folder";
        public const string ChosenInSettingsTipFormat = "{0}, chosen in Settings";
        public const string StandInTipFormat = "{0}, from VISUALCOMMIT_GIT_STAND_IN (a test switch)";
        public const string SetUpGit = "Set up Git…";
        public const string Details = "Details…";

        // ---- The settings row and the netrc note -----------------------------------------------

        public const string GitProgramLabel = "Git program";
        public const string NoneFound = "None found";
        public const string FoundOnPathNote = "Found on the PATH.";
        public const string FoundInGitForWindowsFolderNote = "Found in Git for Windows' install folder.";
        public const string FoundInUsualFolderNote = "Found in a usual install folder.";
        public const string ChosenHereNote = "Chosen here.";
        public const string ChosenGoneNote = "Chosen here. There is no file at this path.";
        public const string ChosenNotRunnableNote = "Chosen here. VisualCommit runs Git only from a full path to the program itself.";
        public const string StandInNote = "From VISUALCOMMIT_GIT_STAND_IN, a test switch.";
        public const string NoGitFoundNote = "No Git was found on the PATH or in the usual install folders.";
        public const string NextStartSuffix = " VisualCommit uses it from its next start.";
        public const string WhenYouSaveSuffix = " VisualCommit looks for Git again when you save.";
        public const string Browse = "Browse…";
        public const string NetrcNoteFormat = "Your home folder has a .netrc or _netrc file. With Git {0}, an entry in that file for a host can answer git's requests to it before your signed-in account. Git 2.46 or later stops this for git's own requests; git-lfs reads the file with every version of Git.";
        public const string Dismiss = "Dismiss";

        // ---- The log lines (1.2, 1.14, 5.3) ----------------------------------------------------

        public const string UsingGitLineFormat = "Using Git {0} at {1}.";
        public const string StartedInTerminalLineFormat = "Started in a terminal: {0}";
        public const string StartedLineFormat = "Started: {0}";
        public const string StandInAtLineFormat = "VISUALCOMMIT_GIT_STAND_IN is set (a test switch): unless Settings names a Git, Git is looked for only at {0}.";
        public const string StandInNoneLine = "VISUALCOMMIT_GIT_STAND_IN is set (a test switch): unless Settings names a Git, this computer is treated as having no Git.";
        public const string ChosenNotFullPathLineFormat = "The Git chosen in Settings is not used: {0} is not a full path.";
        public const string ChosenNotExeLineFormat = "The Git chosen in Settings is not used: {0} is not an .exe file.";

        // ---- The texts the steps pass through (today's) ----------------------------------------

        public const string NewTab = "New tab";
        public const string NoRepositoryOpen = "No repository open";
        public const string ScopeGlobal = "All repositories (global)";
        public const string ScopeRepository = "This repository";
        public const string Save = "Save";
        public const string UseForGit = "Use for git over HTTPS";

        // ---- The automation ids (1.2) ----------------------------------------------------------

        public const string ScreenId = "GitSetup";
        public const string TitleId = "GitSetupTitle";
        public const string BodyId = "GitSetupBody";
        public const string PathId = "GitSetupPath";
        public const string GitSaidId = "GitSetupGitSaid";
        public const string GuidanceId = "GitSetupGuidance";
        public const string CommandId = "GitSetupCommand";
        public const string WingetId = "GitSetupWingetButton";
        public const string DownloadId = "GitSetupDownloadButton";
        public const string CltId = "GitSetupCltButton";
        public const string CopyId = "GitSetupCopyButton";
        public const string ShowFileId = "GitSetupShowFileButton";
        public const string ChooseId = "GitSetupChooseButton";
        public const string UsePathId = "GitSetupUsePathButton";
        public const string CheckAgainId = "GitSetupCheckAgainButton";
        public const string ContinueId = "GitSetupContinueButton";
        public const string ErrorId = "GitSetupError";
        public const string MoreLinkId = "GitSetupMoreLink";
        public const string GuideLinkId = "GitSetupGuideLink";
        public const string StatusId = "GitStatus";
        public const string SetUpButtonId = "GitSetupButton";
        public const string ProgramPathId = "GitProgramPath";
        public const string ProgramNoteId = "GitProgramNote";
        public const string ProgramStatusId = "GitProgramStatus";
        public const string ProgramBrowseId = "GitProgramBrowseButton";
        public const string ProgramUsePathId = "GitProgramUsePathButton";
        public const string NetrcNoteId = "AccountsNetrcNote";
        public const string NetrcDismissId = "AccountsNetrcDismiss";

        /// <summary>The screen's buttons in 5.3's table order, which is their order on the screen (the report's open point 7).</summary>
        public static IReadOnlyList<string> ButtonOrder { get; } = [WingetId, DownloadId, CltId, ShowFileId, ChooseId, UsePathId, CheckAgainId, ContinueId];

        /// <summary>The links in their order.</summary>
        public static IReadOnlyList<string> LinkOrder { get; } = [MoreLinkId, GuideLinkId];

        /// <summary>F13's focus order with winget found: F6 to the first button, then Tab, Tab, Tab.</summary>
        public static IReadOnlyList<string> FocusOrderWithWinget { get; } = [WingetId, DownloadId, ChooseId, CheckAgainId];

        // ---- The fake's made-up values (5.3, the report's conventions) -------------------------

        public const string WindowsGit = @"C:\Program Files\Git\cmd\git.exe";
        public const string UnixGit = "/usr/bin/git";
        public const string DemoGitConfig = "C:/Users/demo/.gitconfig";
        public const string WingetPath = @"C:\Users\demo\AppData\Local\Microsoft\WindowsApps\winget.exe";
        public const string WingetFolderOnWindows = @"C:\Users\demo\AppData\Local\Microsoft\WindowsApps";
        public const string TooOldVersion = "2.29.0";
        public const string NetrcVersion = "2.43.0";

        /// <summary>F7's start failure, the fake's (the report's open point 8).</summary>
        public const string NotStartedMessage = "The system cannot find the file specified.";

        /// <summary>F9 b's words of damaged Command Line Tools, the fake's (the report's open point 8).</summary>
        public const string ToolsBrokenWords = "xcrun: error: invalid active developer path (/Library/Developer/CommandLineTools), missing xcrun at: /Library/Developer/CommandLineTools/usr/bin/xcrun";

        /// <summary>F6's failure text: git's words for line 1 of the made-up global file.</summary>
        public const string ConfigFaultText = "fatal: bad config line 1 in file C:/Users/demo/.gitconfig";

        /// <summary>F6b's words up to the file: real git with <c>[user</c> on line 1 of the session's global file.</summary>
        public const string ConfigFaultPrefix = "fatal: bad config line 1 in file ";

        /// <summary>F6b's guidance up to the file.</summary>
        public const string ConfigFaultGuidancePrefix = "Git cannot read line 1 of";

        /// <summary>What F6b writes on line 1 of the session's global file.</summary>
        public const string BrokenConfigLine = "[user";

        public const string UbuntuId = "ubuntu";
        public const string UbuntuLike = "debian";
        public const string UbuntuPrettyName = "Ubuntu 22.04.5 LTS";
        public const string AlpineId = "alpine";

        /// <summary>The chosen git (F3 gone; F8 and F12 found) on the machine the check runs on: a full path there.</summary>
        public static string Chosen => OperatingSystem.IsWindows() ? @"D:\Tools\PortableGit\cmd\git.exe" : "/home/demo/tools/PortableGit/cmd/git.exe";

        /// <summary>The too-old file chosen (F8, F12) on the machine the check runs on.</summary>
        public static string Old => OperatingSystem.IsWindows() ? @"C:\Users\demo\Tools\OldGit\cmd\git.exe" : "/home/demo/tools/OldGit/cmd/git.exe";

        /// <summary>The helper looked for in F11 (no file there) on the machine the check runs on.</summary>
        public static string MissingHelper => OperatingSystem.IsWindows() ? @"C:\Users\demo\AppData\Local\Programs\VisualCommit\visualcommit-askpass.exe" : "/home/demo/.local/share/VisualCommit/visualcommit-askpass";

        /// <summary>The working folder of the winget start: the literal on Windows, the expression's value elsewhere (the report's open point 10).</summary>
        public static string? WingetFolder => OperatingSystem.IsWindows() ? WingetFolderOnWindows : Path.GetDirectoryName(WingetPath);

        /// <summary>The command line of the winget start (1.3, D449).</summary>
        public const string WingetCommandLine = "/S /K \"\"" + WingetPath + "\" install --id Git.Git -e --source winget\"";

        /// <summary>The start's text in the log.</summary>
        public const string WingetDisplayText = "cmd.exe " + WingetCommandLine;

        public const string CommandLineToolsProgram = "/usr/bin/xcode-select";
        public const string CommandLineToolsArgument = "--install";

        // ---- The colours, by token ------------------------------------------------------------

        /// <summary><c>VcTextSecondaryBrush</c>: the body.</summary>
        public static readonly Color SecondaryDark = Color.Parse("#9BA3B0");

        public static readonly Color SecondaryLight = Color.Parse("#5C6572");

        /// <summary><c>VcAccentBrush</c>: the links.</summary>
        public static readonly Color AccentDark = Color.Parse("#7C8CFF");

        public static readonly Color AccentLight = Color.Parse("#4353D8");

        /// <summary><c>VcAccentOnRowBrush</c>, dark: a hovered link's text, on the hover background (design 1.5; no picture hovers one).</summary>
        public static readonly Color AccentOnRowDark = Color.Parse("#8B9AFF");

        /// <summary><c>VcBorderBrush</c>: "Git said:"'s border.</summary>
        public static readonly Color BorderDark = Color.Parse("#323845");

        /// <summary><c>VcWarningBrush</c>: the netrc note.</summary>
        public static readonly Color WarningDark = Color.Parse("#E0A23B");

        /// <summary>The screen's width at most (5.3).</summary>
        public const double MaxWidth = 520;

        /// <summary>The title's size (5.3).</summary>
        public const double TitleSize = 16;

        // ---- The pictures (the report's Screenshots column) -------------------------------------

        public const string F1aMissingWindows = "f1a-missing-windows";
        public const string F1bMissingWindowsLight = "f1b-missing-windows-light";
        public const string F2aWingetConfirm = "f2a-winget-confirm";
        public const string F3aChosenGone = "f3a-chosen-gone";
        public const string F4aFoundAgain = "f4a-found-again";
        public const string F5aTooOld = "f5a-too-old";
        public const string F5bWingetUpdateConfirm = "f5b-winget-update-confirm";
        public const string F6aConfigFault = "f6a-config-fault";
        public const string F7aNotStarted = "f7a-not-started";
        public const string F8aChooseTooOld = "f8a-choose-too-old";
        public const string F9aMacOSMissing = "f9a-macos-missing";
        public const string F9bMacOSBroken = "f9b-macos-broken";
        public const string F10aUbuntuMissing = "f10a-ubuntu-missing";
        public const string F10bAlpineMissing = "f10b-alpine-missing";
        public const string F10cUbuntuTooOld = "f10c-ubuntu-too-old";
        public const string F11aHelperMissing = "f11a-helper-missing";
        public const string F12aRowNextStart = "f12a-row-next-start";
        public const string F12bRowTooOld = "f12b-row-too-old";
        public const string F14aNetrcNote = "f14a-netrc-note";

        /// <summary>Every picture of part F, scripted, in the methods table's order.</summary>
        public static IReadOnlyList<string> Pictures { get; } =
        [
            F1aMissingWindows, F2aWingetConfirm, F4aFoundAgain, F1bMissingWindowsLight, F3aChosenGone, F5aTooOld, F5bWingetUpdateConfirm,
            F6aConfigFault, F7aNotStarted, F8aChooseTooOld, F9aMacOSMissing, F9bMacOSBroken, F10aUbuntuMissing, F10bAlpineMissing,
            F10cUbuntuTooOld, F11aHelperMissing, F12aRowNextStart, F12bRowTooOld, F14aNetrcNote,
        ];

        /// <summary>The real window's captures, each compared with its scripted twin (launches 7 to 9).</summary>
        public static IReadOnlyList<string> RealWindowPictures { get; } = [F1aMissingWindows, F2aWingetConfirm, F4aFoundAgain, F5aTooOld, F6aConfigFault];

        // ---- The real window's launches (12.3.4 launches 7 to 10; the report's "The real window (part F)")

        /// <summary>Launch 7's scenario (F1, F2, F13, F4), its twin method 1's: one tab restored on a copy of "graph".</summary>
        public const string MissingLaunchScenario = "graph";

        /// <summary>Launch 7's stand-in value: the machine has no git.</summary>
        public const string MissingLaunchStandIn = "none";

        /// <summary>Launch 8's stand-in version line (F5).</summary>
        public const string TooOldVersionLine = "git version 2.29.0";

        /// <summary>The picker's title the real window's F4 waits for in the native Open dialog.</summary>
        public const string NativePickerTitle = ChooseGitExeTitle;

        /// <summary>How long F4's native Open dialog may take to show.</summary>
        public static readonly TimeSpan NativePickerWithin = TimeSpan.FromSeconds(30);

        /// <summary>How long F4's screen may take to go and the restored repository to load after the dialog.</summary>
        public static readonly TimeSpan FoundAgainWithin = TimeSpan.FromSeconds(60);

        /// <summary>How long F2's confirm has to close after its Cancel before the method fails.</summary>
        public static readonly TimeSpan ConfirmClosesWithin = TimeSpan.FromSeconds(5);

        /// <summary>F13: F6 presses at most, one per region of the navigator's order.</summary>
        public const int MostF6Presses = 4;

        /// <summary>The real window fails so when winget is not on the gate's machine's PATH (D449, D453).</summary>
        public const string WingetMissingMessage = "winget.exe is not on this machine's PATH: F1, F2 and F5 need it (install App Installer from the Microsoft Store).";

        /// <summary>The real window fails so when the stand-in is not in its output; {0} the path.</summary>
        public const string StandInMissingMessageFormat = "The stand-in for git is missing at {0}: build tests/VisualCommit.RealWindowTests, which references it.";

        /// <summary>The minimum of git the checks read, never a literal (1.2).</summary>
        public static GitVersion Minimum => GitVersion.Minimum;

        /// <summary>
        /// Part F's resting point at 1100×700 (design 12.3.5, the lead's first ruling): where the pointer
        /// rests before the pictures of a too-old git, <c>f5a-too-old</c> (scripted and real window) and
        /// <c>f10c-ubuntu-too-old</c>, instead of the neutral point. There <c>GitStatus</c>'s long text,
        /// <c>Git 2.29.0 is too old (needs 2.34.1 or newer)</c>, covers the neutral point (760, 687), and a
        /// real pointer resting on it opens its tooltip. This point is the status bar's empty middle, right
        /// of <c>OperationStatus</c>'s <c>Ready</c> and left of <c>GitSetupButton</c>: no control with a
        /// tooltip or a hover look lies under it. Headless no pointer is drawn, so the scripted pictures
        /// are the same files at either point.
        /// </summary>
        public static readonly (double X, double Y) RestingPoint = (400, 687);
    }
}
