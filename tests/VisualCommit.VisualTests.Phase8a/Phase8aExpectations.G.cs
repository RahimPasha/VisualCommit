using Avalonia;
using Avalonia.Media;

namespace VisualCommit.VisualTests.Phase8a;

public static partial class Phase8aExpectations
{
    /// <summary>
    /// What part G's checks compare against (docs/test-reports/phase-8a.md, "Part G: the user guide's
    /// links: F1, the palette row, the first-run links and About's link"; docs/phase-8a-design.md 1.7,
    /// 1.8, 1.13, 11.3.1, 11.5; D417, D452; rulings 77 to 85): the command, its key and its row in the
    /// shortcuts list, the first-run links' and About's texts, the guide's addresses, the ids, the
    /// query, the shortcuts list's counts, phase 6's click point, the fakes' answers, the colours by
    /// token and the picture names, word for word as the report writes them. The made-up values part F
    /// holds (the chosen git, the helper's path, git's path) are read from <see cref="F"/>. Part G has
    /// no real-window launch, but the real-window pass links every <c>Phase8aExpectations*.cs</c>
    /// (design 0.3), so this file uses the BCL, Avalonia and Core only.
    /// </summary>
    public static class G
    {
        // ---- The command, its key and the shortcuts list (1.8, 11.3.1) -------------------------

        /// <summary>The palette's command and the shortcuts list's row (<c>AppCommands.OpenUserGuideText</c>).</summary>
        public const string OpenUserGuide = "Open the user guide";

        /// <summary>Its key, as Windows shows it and the palette's row and the list write it.</summary>
        public const string Key = "F1";

        /// <summary>The query G1 and method 2's step 11 type into the palette (this report's).</summary>
        public const string Query = "guide";

        /// <summary>The shortcuts list's last group and the group before it.</summary>
        public const string HelpGroup = "Help";

        public const string GroupBeforeHelp = "Dialogs and menus";

        /// <summary>The Help row's automation name, phase 6's form <c>{action}: {keys}</c>.</summary>
        public const string HelpRowName = "Open the user guide: F1";

        /// <summary>The Help group's note, on macOS alone, which no check shows (the report's open point 109).</summary>
        public const string MacNote = "On a Mac keyboard F1 needs Fn unless \"Use F1, F2, etc. keys as standard function keys\" is on.";

        /// <summary>The list's groups and rows on Windows and Linux, with Help last (ruling 62).</summary>
        public const int ShortcutGroups = 12;

        public const int ShortcutRows = 51;

        /// <summary>The list's one note, the Repository group's (phase 6's S14).</summary>
        public const string RepositoryNote = "On a dashboard, Fetch and Pull fetch and pull every repository of its workspace.";

        /// <summary>The section's intro (phase 6's part S).</summary>
        public const string ShortcutsIntro = "These keys are fixed.";

        /// <summary>A group's heading: 12.5, SemiBold (phase 6's S14).</summary>
        public const double GroupHeadingSize = 12.5;

        /// <summary>A shortcut row is at least this high (phase 6's S14).</summary>
        public const double ShortcutRowHeight = 22;

        // ---- The log lines (11.3.1, 8.3.5), whose absence G1 and G3 assert ----------------------

        /// <summary>The Info of "Open the user guide" in a session without a launcher.</summary>
        public const string NoLauncherLine = "Open the user guide: there is no launcher.";

        /// <summary>The Error of "Open the user guide" when the launcher fails.</summary>
        public const string FailedLine = "Open the user guide failed.";

        /// <summary>About's Warning when its guide link's launcher fails (8.3.5).</summary>
        public const string AboutGuideFailedLine = "Opening the user guide failed.";

        // ---- The first-run screen's links (5.3) --------------------------------------------------

        public const string HowVisualCommitFindsGit = "How VisualCommit finds Git";
        public const string WhatTheHelperDoes = "What the helper does";
        public const string OtherWaysToInstallGit = "Other ways to install Git";

        // ---- About's guide row (1.13, 8.3.5) -----------------------------------------------------

        public const string UserGuideLabel = "User guide";
        public const string AboutGuideLink = "Open the user guide";
        public const string AboutGuideTip = "Opens the user guide in your browser.";

        // ---- The guide's addresses (1.7) ---------------------------------------------------------

        public const string StartPage = "https://rahimpasha.github.io/VisualCommit/";
        public const string GitMissingAddress = "https://rahimpasha.github.io/VisualCommit/troubleshooting/git.html#git-missing";
        public const string GitChosenMissingAddress = "https://rahimpasha.github.io/VisualCommit/troubleshooting/git.html#git-chosen-missing";
        public const string GitTooOldAddress = "https://rahimpasha.github.io/VisualCommit/troubleshooting/git.html#git-too-old";
        public const string GitNotWorkingAddress = "https://rahimpasha.github.io/VisualCommit/troubleshooting/git.html#git-not-working";
        public const string HelperMissingAddress = "https://rahimpasha.github.io/VisualCommit/troubleshooting/git.html#helper-missing";

        /// <summary>Pages no control opens (<c>AppLinksTests</c> alone holds that they exist).</summary>
        public const string FirstRunAddress = "https://rahimpasha.github.io/VisualCommit/first-run.html";

        public const string ShortcutsAddress = "https://rahimpasha.github.io/VisualCommit/reference/shortcuts.html";

        /// <summary>Method 2's eight addresses, in order: each state's by its link, then the start page by F1.</summary>
        public static IReadOnlyList<string> Method2Requests { get; } =
        [
            GitMissingAddress, StartPage, GitChosenMissingAddress, StartPage, GitTooOldAddress, StartPage, GitNotWorkingAddress, StartPage,
        ];

        /// <summary>Method 3's two addresses, in order.</summary>
        public static IReadOnlyList<string> Method3Requests { get; } = [HelperMissingAddress, StartPage];

        // ---- The ids (1.2, 1.13; phase 6's 5.11 and part S) --------------------------------------

        public const string PaletteBoxId = "CommandPaletteBox";
        public const string PaletteListId = "CommandPaletteList";
        public const string PaletteItemId = "CommandPaletteItem";
        public const string PaletteEmptyId = "CommandPaletteEmpty";
        public const string PaletteItemLabel = "CommandPaletteItemLabel";
        public const string PaletteItemDetail = "CommandPaletteItemDetail";
        public const string PaletteItemKey = "CommandPaletteItemKey";
        public const string GraphId = "GraphRows";
        public const string NavGeneralId = "SettingsNavGeneral";
        public const string NavShortcutsId = "SettingsNavShortcuts";
        public const string NavAboutId = "SettingsNavAbout";
        public const string SectionScrollerName = "SectionScroller";
        public const string SectionScrollerId = "SettingsSection";
        public const string ShortcutListName = "ShortcutList";
        public const string ShortcutGroupId = "ShortcutGroup";
        public const string ShortcutRowId = "ShortcutRow";
        public const string AboutSectionId = "SettingsAbout";
        public const string AboutRepositoryLinkId = "AboutRepositoryLink";
        public const string AboutGuideLinkId = "AboutGuideLink";
        public const string AboutShowLicenseId = "AboutShowLicenseButton";
        public const string AboutShowNoticesId = "AboutShowNoticesButton";
        public const string GitSetupId = "GitSetup";
        public const string GitSetupTitleId = "GitSetupTitle";
        public const string GitSetupGuidanceId = "GitSetupGuidance";
        public const string GitSetupGuideLinkId = "GitSetupGuideLink";
        public const string GitSetupMoreLinkId = "GitSetupMoreLink";
        public const string GitSetupErrorId = "GitSetupError";

        /// <summary>The settings screen's section buttons G3 walks by Down from General, to About (part A's A3 route).</summary>
        public static IReadOnlyList<string> DownsToAbout { get; } = ["SettingsNavGit", "SettingsNavTools", NavShortcutsId, "SettingsNavAccounts", NavAboutId];

        /// <summary>About's stops G3 walks by Tab from its section button, to the guide link (1.13).</summary>
        public static IReadOnlyList<string> TabsToGuideLink { get; } = [AboutShowLicenseId, AboutShowNoticesId, AboutRepositoryLinkId, AboutGuideLinkId];

        // ---- G1's start on "ops" (phase 6's K10 step 1) ------------------------------------------

        /// <summary>"Main work", display row 3 of "ops", and the point in its Message column G1 clicks.</summary>
        public const string MainWork = "Main work";

        public const int MainWorkRow = 3;

        public static readonly Point MainWorkPoint = new(484, 207);

        /// <summary>The palette's worktree read at its opening (phase 6's K10).</summary>
        public const string WorktreeList = "git worktree list --porcelain";

        /// <summary>The palette's card with one row (phase 6's design 5.3: 2 + 16 + 32 + 6 + 28).</summary>
        public const double CardHeightOneRow = 84;

        /// <summary>A palette row's background is read this far into its left padding, clear of its text.</summary>
        public const double RowBackgroundInset = 5;

        // ---- G2's starts: the fakes' answers (the report's methods table; open point 102) --------

        /// <summary>Method 2's states, in the order "Check again" reaches them: the title each shows.</summary>
        public const string MissingTitle = "VisualCommit needs Git";
        public const string ChosenMissingTitle = "The chosen Git is gone";
        public const string TooOldTitle = "Git 2.29.0 is too old";
        public const string NotWorkingTitle = "Git is not working";
        public const string HelperMissingTitle = "Part of VisualCommit is missing";

        /// <summary>The guidance each state shows, which the wait for its screen reads.</summary>
        public const string MissingGuidance = "Install Git for Windows, then press Check again. VisualCommit does not need to restart.";
        public const string ChosenMissingGuidance = @"VisualCommit found another Git at C:\Program Files\Git\cmd\git.exe.";
        public const string TooOldGuidance = "Update Git for Windows, then press Check again. Its installer updates it in place.";
        public const string NotWorkingGuidance = "Git cannot read line 1 of C:/Users/demo/.gitconfig. Correct that line, or rename the file, then press Check again.";
        public const string HelperMissingGuidance = "Without it, git and ssh cannot ask for a password, a passphrase or a host key here, and a signed-in account cannot be used for git. Install VisualCommit again to restore it.";

        /// <summary>The too-old answer's version and the broken configuration's file and line (part F's method 5's answer).</summary>
        public const string TooOldVersion = "2.29.0";

        public const string BrokenConfigFile = "C:/Users/demo/.gitconfig";

        public const int BrokenConfigLine = 1;

        public const string ChooseGitExe = "Choose git.exe…";
        public const string CheckAgain = "Check again";

        /// <summary>The screen's buttons in each state, in their order, as their ids; the guide link follows the last.</summary>
        public static IReadOnlyList<string> MissingButtons { get; } = ["GitSetupWingetButton", "GitSetupDownloadButton", "GitSetupChooseButton", "GitSetupCheckAgainButton"];

        public static IReadOnlyList<string> ChosenMissingButtons { get; } = ["GitSetupChooseButton", "GitSetupUsePathButton", "GitSetupCheckAgainButton"];

        public static IReadOnlyList<string> TooOldButtons { get; } = ["GitSetupWingetButton", "GitSetupDownloadButton", "GitSetupChooseButton", "GitSetupCheckAgainButton"];

        public static IReadOnlyList<string> NotWorkingButtons { get; } = ["GitSetupDownloadButton", "GitSetupShowFileButton", "GitSetupChooseButton", "GitSetupCheckAgainButton"];

        public static IReadOnlyList<string> HelperMissingButtons { get; } = ["GitSetupCheckAgainButton", "GitSetupContinueButton"];

        /// <summary>Their texts, in the same order.</summary>
        public static IReadOnlyList<string> MissingButtonTexts { get; } = ["Install with winget…", "Download Git for Windows", ChooseGitExe, CheckAgain];

        public static IReadOnlyList<string> ChosenMissingButtonTexts { get; } = [ChooseGitExe, "Use the Git it found", CheckAgain];

        public static IReadOnlyList<string> TooOldButtonTexts { get; } = ["Update with winget…", "Download Git for Windows", ChooseGitExe, CheckAgain];

        public static IReadOnlyList<string> NotWorkingButtonTexts { get; } = ["Download Git for Windows", "Show the file", ChooseGitExe, CheckAgain];

        public static IReadOnlyList<string> HelperMissingButtonTexts { get; } = [CheckAgain, "Continue without it"];

        /// <summary>The status bar's git box with the helper missing: git's version put in.</summary>
        public const string HelperMissingStatusFormat = "Git {0} (helper missing)";

        /// <summary>F6 presses at most to the screen's first button (part F's F13).</summary>
        public const int MostF6Presses = 4;

        // ---- The colours: dark tokens no package of 8a changes again (1.5) ----------------------

        /// <summary><c>VcAccentBrush</c>: both links' text, not hovered.</summary>
        public static readonly Color Accent = Color.Parse("#7C8CFF");

        /// <summary><c>VcPanelBackgroundBrush</c>: the settings card, behind About's link.</summary>
        public static readonly Color Panel = Color.Parse("#1A1D24");

        /// <summary><c>VcTextSecondaryBrush</c>: About's row label, the shortcut row's keys.</summary>
        public static readonly Color Secondary = Color.Parse("#9BA3B0");

        /// <summary><c>VcTextPrimaryBrush</c>: the palette row's label.</summary>
        public static readonly Color Primary = Color.Parse("#E4E7EC");

        /// <summary><c>VcSelectionBrush</c>: the palette's highlighted row, at a point clear of text.</summary>
        public static readonly Color Selection = Color.Parse("#2A3150");

        // ---- About's places at 1100×700 (part A's) -----------------------------------------------

        /// <summary>The form's first column (labels) and third (values), within <see cref="Tolerance"/>.</summary>
        public const double LabelX = 336;

        public const double ValueX = 480;

        public const double Tolerance = 2;

        // ---- The pictures (the report's Screenshots column) --------------------------------------

        public const string G1aPaletteGuide = "g1a-palette-guide";
        public const string G1bShortcutsHelp = "g1b-shortcuts-help";
        public const string G2aFirstRunGuideLink = "g2a-first-run-guide-link";
        public const string G3aAboutGuideLink = "g3a-about-guide-link";

        /// <summary>Every picture of part G, in the order of its methods.</summary>
        public static IReadOnlyList<string> Pictures { get; } = [G1aPaletteGuide, G1bShortcutsHelp, G2aFirstRunGuideLink, G3aAboutGuideLink];
    }
}
