using VisualCommit.App.Services;
using VisualCommit.Core.Git;
using VisualCommit.Git;

namespace VisualCommit.App.ViewModels.GitSetup;

/// <summary>What the first-run screen offers, in the order it shows them: its buttons, then its links.</summary>
public enum GitSetupAction
{
    /// <summary><c>GitSetupWingetButton</c>: winget, after a confirm, in a terminal of its own (Windows).</summary>
    Winget,

    /// <summary><c>GitSetupDownloadButton</c>: git-scm.com's page for Windows.</summary>
    Download,

    /// <summary><c>GitSetupCltButton</c>: <c>xcode-select --install</c> (macOS).</summary>
    CommandLineTools,

    /// <summary><c>GitSetupShowFileButton</c>: the file git could not read, in the file manager.</summary>
    ShowFile,

    /// <summary><c>GitSetupChooseButton</c>: a git program chosen in a file dialog.</summary>
    Choose,

    /// <summary><c>GitSetupUsePathButton</c>: the setting removed, then a search.</summary>
    UsePath,

    /// <summary><c>GitSetupCheckAgainButton</c>: a search without a restart.</summary>
    CheckAgain,

    /// <summary><c>GitSetupContinueButton</c>: the screen hidden for the session (the helper missing).</summary>
    Continue,

    /// <summary><c>GitSetupMoreLink</c>: git-scm.com's page for macOS or Linux.</summary>
    MoreLink,

    /// <summary><c>GitSetupGuideLink</c>: the user guide's page of the state.</summary>
    GuideLink,
}

/// <summary>One button or link of the screen: what it does and its text, which is also its automation name.</summary>
public sealed record GitSetupOffer(GitSetupAction Action, string Text);

/// <summary>
/// What the first-run screen shows for one state: its texts, the git's or the helper's path, git's
/// own words, the guidance with its command, and the buttons and links in their order.
/// </summary>
/// <param name="Title">The title, <c>GitSetupTitle</c>, also the screen's automation name.</param>
/// <param name="Body">The body, <c>GitSetupBody</c>, also the screen's help text.</param>
/// <param name="Path">The path under the body (<c>GitSetupPath</c>); null for none.</param>
/// <param name="GitSaid">Git's own words under "Git said:" (<c>GitSetupGitSaid</c>); null for none.</param>
/// <param name="Guidance">The guidance (<c>GitSetupGuidance</c>).</param>
/// <param name="Command">The command to type, with Copy (<c>GitSetupCommand</c>); null for none.</param>
/// <param name="Buttons">The buttons, left to right.</param>
/// <param name="Links">The links, left to right.</param>
/// <param name="Page">The user guide's page of the state, which <c>GitSetupGuideLink</c> opens.</param>
public sealed record GitSetupContent(
    string Title,
    string Body,
    string? Path,
    string? GitSaid,
    string Guidance,
    string? Command,
    IReadOnlyList<GitSetupOffer> Buttons,
    IReadOnlyList<GitSetupOffer> Links,
    GuidePage Page);

/// <summary>
/// Every text of git's setup (R4, D405, D423, D453): the first-run screen by state and by the
/// guidance's platform, its confirms and Choose's refusals, the status bar's git box, the settings
/// screen's "Git program" row and the Accounts section's netrc note. Pure: the tests read them.
/// </summary>
public static class GitSetupTexts
{
    // ---- Titles -----------------------------------------------------------------------------

    public const string MissingTitle = "VisualCommit needs Git";
    public const string ChosenMissingTitle = "The chosen Git is gone";
    public const string TooOldTitleFormat = "Git {0} is too old";
    public const string NotWorkingTitle = "Git is not working";
    public const string HelperMissingTitle = "Part of VisualCommit is missing";

    // ---- Bodies -----------------------------------------------------------------------------

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

    /// <summary>The label over git's own words.</summary>
    public const string GitSaid = "Git said:";

    // ---- Guidance ---------------------------------------------------------------------------

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

    // ---- Commands ---------------------------------------------------------------------------

    public const string BrewCommand = "brew install git";
    public const string SelectToolsCommand = "sudo xcode-select --switch /Library/Developer/CommandLineTools";
    public const string AptCommand = "sudo apt install git";
    public const string DnfCommand = "sudo dnf install git";
    public const string PacmanCommand = "sudo pacman -S git";
    public const string ZypperCommand = "sudo zypper install git";

    /// <summary>git-scm.com's winget line, which the confirm shows unchanged.</summary>
    public const string WingetLine = "winget install --id Git.Git -e --source winget";

    /// <summary>What git or <c>xcode-select</c> writes when the Command Line Tools are damaged or half installed.</summary>
    public static IReadOnlyList<string> ToolsBrokenWords { get; } = ["xcrun: error", "requesting installation of command line developer tools"];

    // ---- Buttons and links ------------------------------------------------------------------

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

    // ---- Addresses --------------------------------------------------------------------------

    public static Uri DownloadForWindows { get; } = new("https://git-scm.com/install/windows");
    public static Uri InstallOnMacOS { get; } = new("https://git-scm.com/install/mac");
    public static Uri InstallOnLinux { get; } = new("https://git-scm.com/install/linux");

    // ---- The winget confirms ----------------------------------------------------------------

    public const string InstallConfirmTitle = "Install Git with winget?";
    public const string UpdateConfirmTitle = "Update Git with winget?";
    public const string ConfirmQuestion = "VisualCommit opens a terminal window that runs " + WingetLine;
    public const string InstallConfirmDetail = "winget downloads Git for Windows from its community repository, which the Git for Windows project does not maintain, and runs the installer; Windows may ask for permission. When it has finished, close the window and press Check again.";
    public const string UpdateConfirmDetail = "winget downloads the newest Git for Windows from its community repository, which the Git for Windows project does not maintain, and runs the installer, which updates the one installed; Windows may ask for permission. When it has finished, close the window and press Check again.";
    public const string Install = "Install";
    public const string Update = "Update";

    // ---- Choose -----------------------------------------------------------------------------

    public const string ChooseGitExeTitle = "Choose git.exe";
    public const string ChooseGitProgramTitle = "Choose the git program";
    public const string ChooseTooOldFormat = "Git {0} at this path is too old: VisualCommit needs {1} or newer.";
    public const string ChooseNoVersionFormat = "This Git did not report its version. Git said: {0}";
    public const string ChooseNotStartedFormat = "This file could not be started: {0}";

    /// <summary>A start of winget or <c>xcode-select</c> that failed: {0} the program, {1} why.</summary>
    public const string StartFailedFormat = "{0} could not be started: {1}";

    // ---- The log ----------------------------------------------------------------------------

    public const string StartedInTerminalFormat = "Started in a terminal: {0}";
    public const string StartedFormat = "Started: {0}";

    // ---- The status bar ---------------------------------------------------------------------

    public const string LookingForGit = "Looking for Git...";
    public const string GitFormat = "Git {0}";
    public const string GitTooOldStatusFormat = "Git {0} is too old (needs {1} or newer)";
    public const string GitNotFound = "Git not found";
    public const string GitNotWorkingStatus = "Git is not working";
    public const string ChosenGitNotFound = "Chosen Git not found";
    public const string HelperMissingStatusFormat = "Git {0} (helper missing)";
    public const string FoundOnPathTipFormat = "{0}, found on the PATH";
    public const string FoundInGitForWindowsFolderTipFormat = "{0}, found in Git for Windows' install folder";
    public const string FoundInUsualFolderTipFormat = "{0}, found in a usual install folder";
    public const string ChosenInSettingsTipFormat = "{0}, chosen in Settings";
    public const string StandInTipFormat = "{0}, from VISUALCOMMIT_GIT_STAND_IN (a test switch)";
    public const string SetUpGit = "Set up Git…";
    public const string Details = "Details…";

    // ---- The settings screen's "Git program" row --------------------------------------------

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
    public const string NextStartPathSuffix = " VisualCommit uses the Git on the PATH from its next start.";
    public const string WhenYouSaveSuffix = " VisualCommit looks for Git again when you save.";
    public const string Browse = "Browse…";

    // ---- The Accounts section's netrc note --------------------------------------------------

    public const string NetrcNoteFormat = "Your home folder has a .netrc or _netrc file. With Git {0}, an entry in that file for a host can answer git's requests to it before your signed-in account. Git 2.46 or later stops this for git's own requests; git-lfs reads the file with every version of Git.";
    public const string Dismiss = "Dismiss";

    /// <summary>The first git that keeps a netrc entry from answering before the account's credential helper (D340).</summary>
    public static GitVersion NetrcSafeFrom { get; } = new(2, 46, 0);

    // ---- What GetRunnerAsync says of a chosen git it does not use ---------------------------

    public const string ChosenNotFoundFormat = "The Git chosen in Settings was not found at {0}.";
    public const string ChosenNotFullPathFormat = "The Git chosen in Settings is not used: {0} is not a full path.";
    public const string ChosenNotExeFormat = "The Git chosen in Settings is not used: {0} is not an .exe file.";

    /// <summary>Why the git chosen in Settings at <paramref name="chosen"/> is not used, by the lookup's rule on this machine.</summary>
    public static string ChosenNotUsed(string chosen)
    {
        ArgumentNullException.ThrowIfNull(chosen);
        return !Path.IsPathFullyQualified(chosen) ? Format(ChosenNotFullPathFormat, chosen)
            : !GitLocator.IsRunnablePath(chosen) ? Format(ChosenNotExeFormat, chosen)
            : Format(ChosenNotFoundFormat, chosen);
    }

    /// <summary>The title of a too-old git.</summary>
    public static string TooOldTitle(GitVersion? version) => Format(TooOldTitleFormat, version?.ToString() ?? "?");

    /// <summary>
    /// The screen's content for <paramref name="state"/> of <paramref name="git"/> on the guidance's
    /// platform (<paramref name="guidance"/>), the helper looked for at <paramref name="missingHelperPath"/>.
    /// Null for <see cref="GitSetupState.Searching"/> and <see cref="GitSetupState.Found"/>, which show no screen.
    /// </summary>
    public static GitSetupContent? For(GitSetupState state, GitDetection? git, GitGuidance guidance, string? missingHelperPath)
    {
        ArgumentNullException.ThrowIfNull(guidance);
        var platform = guidance.Platform;
        var windows = platform == GuidancePlatform.Windows;
        var chooseText = windows ? ChooseGitExe : ChooseGitProgram;
        var chosen = git?.Source == GitSource.Setting;
        switch (state)
        {
            case GitSetupState.Missing:
            {
                var toolsMissing = platform == GuidancePlatform.MacOS && git?.CommandLineToolsMissing == true;
                var (body, words, command) = platform switch
                {
                    GuidancePlatform.Windows => (MissingBodyWindows, MissingGuidanceWindows, (string?)null),
                    GuidancePlatform.MacOS => toolsMissing
                        ? (MissingBodyMacOS + MissingBodyMacOSToolsMissing, MissingGuidanceMacOSToolsMissing, BrewCommand)
                        : (MissingBodyMacOS, MissingGuidanceMacOS, BrewCommand),
                    _ => LinuxCommand(guidance.Distribution) is { } linux
                        ? (MissingBodyLinux, Format(MissingGuidanceLinuxKnownFormat, NameOf(guidance.Distribution!)), linux)
                        : (MissingBodyLinux, MissingGuidanceLinuxUnknown, null),
                };
                return Content(MissingTitle, body, null, null, words, command, guidance, GuidePage.GitMissing, offerWinget: true, InstallWithWinget, download: windows, tools: toolsMissing, showFile: false, chooseText, usePath: null, moreLink: !windows);
            }

            case GitSetupState.ChosenMissing:
            {
                var path = git?.ChosenMissing ?? string.Empty;
                var body = GitLocator.IsRunnablePath(path) ? ChosenMissingBody : ChosenNotRunnableBody;
                var candidate = git?.PathCandidate;
                var words = candidate is null ? ChosenMissingGuidanceNone : Format(ChosenMissingGuidanceFormat, candidate);
                return Content(ChosenMissingTitle, body, path, null, words, null, guidance, GuidePage.GitChosenMissing, offerWinget: candidate is null, InstallWithWinget, download: windows && candidate is null, tools: false, showFile: false, chooseText, usePath: candidate is null ? null : UseTheGitItFound, moreLink: !windows && candidate is null);
            }

            case GitSetupState.TooOld:
            {
                var version = git?.Version?.ToString() ?? "?";
                var (words, command) = platform switch
                {
                    GuidancePlatform.Windows => (TooOldGuidanceWindows, (string?)null),
                    GuidancePlatform.MacOS => (TooOldGuidanceMacOS, BrewCommand),
                    _ => (TooOldGuidanceLinux, null),
                };
                return Content(TooOldTitle(git?.Version), Format(TooOldBodyFormat, GitVersion.Minimum, version), git?.ExecutablePath, null, words, command, guidance, GuidePage.GitTooOld, offerWinget: !chosen, UpdateWithWinget, download: windows, tools: false, showFile: false, chooseText, usePath: chosen ? UseTheGitOnThePath : null, moreLink: !windows);
            }

            case GitSetupState.NotWorking:
            {
                var said = git?.ErrorText ?? string.Empty;
                var started = git?.ExitCode is not null || git?.ExecutablePath is null;
                var body = !started ? Format(NotStartedBodyFormat, said)
                    : git?.ExitCode is { } code && said.Length == 0 ? Format(NotWorkingSilentBodyFormat, code)
                    : NotWorkingBody;
                var toolsBroken = platform == GuidancePlatform.MacOS && ToolsBrokenWords.Any(words => said.Contains(words, StringComparison.Ordinal));
                var (words, command) = git?.ConfigFault is { } fault
                    ? (Format(ConfigFaultGuidanceFormat, fault.Line, fault.File), (string?)null)
                    : platform switch
                    {
                        GuidancePlatform.Windows => (NotWorkingGuidanceWindows, null),
                        GuidancePlatform.MacOS => toolsBroken ? (NotWorkingGuidanceMacOSToolsBroken, SelectToolsCommand) : (NotWorkingGuidanceMacOS, null),
                        _ => (NotWorkingGuidanceLinux, null),
                    };
                return Content(NotWorkingTitle, body, git?.ExecutablePath, started && said.Length > 0 ? said : null, words, command, guidance, GuidePage.GitNotWorking, offerWinget: false, null, download: windows, tools: toolsBroken, showFile: git?.ConfigFault is not null, chooseText, usePath: chosen ? UseTheGitOnThePath : null, moreLink: false);
            }

            case GitSetupState.HelperMissing:
                return new GitSetupContent(
                    HelperMissingTitle,
                    HelperMissingBody,
                    missingHelperPath,
                    null,
                    HelperMissingGuidance,
                    null,
                    [new(GitSetupAction.CheckAgain, CheckAgain), new(GitSetupAction.Continue, ContinueWithoutIt)],
                    [new(GitSetupAction.GuideLink, WhatTheHelperDoes)],
                    GuidePage.HelperMissing);

            default:
                return null;
        }
    }

    /// <summary>The package command of <paramref name="distribution"/>, by its <c>ID</c>, then each <c>ID_LIKE</c> token; null for one this list does not know.</summary>
    public static string? LinuxCommand(LinuxDistribution? distribution)
    {
        if (distribution is null)
        {
            return null;
        }

        foreach (var id in new[] { distribution.Id }.Concat(distribution.IdLike))
        {
            var command = id switch
            {
                "debian" or "ubuntu" => AptCommand,
                "fedora" or "rhel" or "centos" => DnfCommand,
                "arch" => PacmanCommand,
                "suse" or "opensuse" => ZypperCommand,
                _ when id.StartsWith("opensuse-", StringComparison.Ordinal) => ZypperCommand,
                _ => null,
            };
            if (command is not null)
            {
                return command;
            }
        }

        return null;
    }

    /// <summary>The status bar's git box (<c>GitStatus</c>) for <paramref name="git"/>, the helper missing or not; <see cref="LookingForGit"/> while no search has ended.</summary>
    public static string Status(GitDetection? git, bool helperMissing = false) => git switch
    {
        null => LookingForGit,
        { Availability: GitAvailability.Available } when helperMissing => Format(HelperMissingStatusFormat, git.Version),
        { Availability: GitAvailability.Available } => Format(GitFormat, git.Version),
        { Availability: GitAvailability.TooOld } => Format(GitTooOldStatusFormat, git.Version, GitVersion.Minimum),
        { Availability: GitAvailability.NotFound, ChosenMissing: not null } => ChosenGitNotFound,
        { Availability: GitAvailability.NotFound } => GitNotFound,
        _ => GitNotWorkingStatus,
    };

    /// <summary>
    /// The git box's tooltip and help text: the git's path and where it came from, the install
    /// folder named as Git for Windows' on Windows (<paramref name="windows"/>); null without a path.
    /// </summary>
    public static string? StatusTip(GitDetection? git, bool windows)
    {
        if (git?.ExecutablePath is not { } path)
        {
            return null;
        }

        return git.Source switch
        {
            GitSource.Setting => Format(ChosenInSettingsTipFormat, path),
            GitSource.StandIn => Format(StandInTipFormat, path),
            GitSource.InstallFolder => Format(windows ? FoundInGitForWindowsFolderTipFormat : FoundInUsualFolderTipFormat, path),
            _ => Format(FoundOnPathTipFormat, path),
        };
    }

    /// <summary>
    /// The settings row's note on where a git in use came from (<c>GitProgramNote</c>), before any
    /// suffix; null <paramref name="git"/> (no search has ended, or the last one failed) has none in use.
    /// </summary>
    public static string SourceNote(GitDetection? git, bool windows) => git switch
    {
        null => NoGitFoundNote,
        { Availability: GitAvailability.NotFound, ChosenMissing: { } chosen } => GitLocator.IsRunnablePath(chosen) ? ChosenGoneNote : ChosenNotRunnableNote,
        { ExecutablePath: null } => NoGitFoundNote,
        { Source: GitSource.Setting } => ChosenHereNote,
        { Source: GitSource.StandIn } => StandInNote,
        { Source: GitSource.InstallFolder } => windows ? FoundInGitForWindowsFolderNote : FoundInUsualFolderNote,
        _ => FoundOnPathNote,
    };

    /// <summary>The netrc note for git <paramref name="version"/>.</summary>
    public static string NetrcNote(GitVersion version) => Format(NetrcNoteFormat, version);

    private static GitSetupContent Content(
        string title,
        string body,
        string? path,
        string? gitSaid,
        string guidanceWords,
        string? command,
        GitGuidance guidance,
        GuidePage page,
        bool offerWinget,
        string? wingetText,
        bool download,
        bool tools,
        bool showFile,
        string chooseText,
        string? usePath,
        bool moreLink)
    {
        var windows = guidance.Platform == GuidancePlatform.Windows;
        var buttons = new List<GitSetupOffer>();
        if (offerWinget && windows && guidance.WingetPath is not null && wingetText is not null)
        {
            buttons.Add(new(GitSetupAction.Winget, wingetText));
        }

        if (download)
        {
            buttons.Add(new(GitSetupAction.Download, DownloadGitForWindows));
        }

        if (tools)
        {
            buttons.Add(new(GitSetupAction.CommandLineTools, InstallCommandLineTools));
        }

        if (showFile)
        {
            buttons.Add(new(GitSetupAction.ShowFile, ShowTheFile));
        }

        buttons.Add(new(GitSetupAction.Choose, chooseText));
        if (usePath is not null)
        {
            buttons.Add(new(GitSetupAction.UsePath, usePath));
        }

        buttons.Add(new(GitSetupAction.CheckAgain, CheckAgain));
        List<GitSetupOffer> links = moreLink
            ? [new(GitSetupAction.MoreLink, OtherWaysToInstallGit), new(GitSetupAction.GuideLink, HowVisualCommitFindsGit)]
            : [new(GitSetupAction.GuideLink, HowVisualCommitFindsGit)];
        return new GitSetupContent(title, body, path, gitSaid, guidanceWords, command, buttons, links, page);
    }

    /// <summary>The distribution's name as the guidance gives it: its <c>PRETTY_NAME</c>, else its <c>ID</c>.</summary>
    private static string NameOf(LinuxDistribution distribution) => distribution.PrettyName ?? distribution.Id;

    private static string Format(string format, params object?[] values) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, format, values);
}
