using Avalonia.Media;

namespace VisualCommit.VisualTests.Phase8a;

public static partial class Phase8aExpectations
{
    /// <summary>
    /// Part A, About and the third-party notices (docs/test-reports/phase-8a.md, checks A1 to A6;
    /// docs/phase-8a-design.md 1.13, 8.3, 8.5, 12.3.4 launch 3): About's texts, the text dialog's,
    /// the section list's places, the ids, the colours by token, the picture names and the real
    /// window's launch facts, written from the report and not from the app's texts. The real-window
    /// pass links this file (design 0.3): it uses the BCL and Avalonia only.
    /// </summary>
    public static class A
    {
        // ---- About's texts (8.3.5), word for word ----------------------------------------------

        public const string Section = "About";
        public const string Name = "VisualCommit";
        public const string Creator = "Created by Rahim Khajei";
        public const string Copyright = "Copyright (c) 2026 Rahim Khajei";
        public const string VersionLabel = "Version";
        public const string CopyrightLabel = "Copyright";
        public const string LicenseLabel = "License";
        public const string NoticesLabel = "Notices";
        public const string SourceCodeLabel = "Source code";
        public const string UserGuideLabel = "User guide";
        public const string MitLicense = "MIT License";
        public const string ShowLicense = "Show license…";
        public const string NoticesNote = "VisualCommit is built on open-source components, each under its own license.";
        public const string ShowNotices = "Show third-party notices…";
        public const string RepositoryLink = "github.com/RahimPasha/VisualCommit";
        public const string RepositoryTip = "Opens the repository's page in your browser.";
        public const string OpenUserGuide = "Open the user guide";
        public const string GuideTip = "Opens the user guide in your browser.";

        /// <summary>The section's labels, top to bottom, in the form's first column.</summary>
        public static IReadOnlyList<string> Labels { get; } = [VersionLabel, CopyrightLabel, LicenseLabel, NoticesLabel, SourceCodeLabel, UserGuideLabel];

        // ---- The text dialog's texts ----------------------------------------------------------

        public const string LicenseTitle = "MIT License";
        public const string NoticesTitle = "Third-party notices";
        public const string MainNotices = "Components and licenses";
        public const string CopyAll = "Copy all";
        public const string Close = "Close";

        /// <summary>The notices' switch, left to right: the main file, then each vendor file in name order.</summary>
        public static IReadOnlyList<string> NoticesSwitch { get; } = [MainNotices, "Avalonia", ".NET Community Toolkit", "Onigwrap", "SkiaSharp and HarfBuzzSharp"];

        public static IReadOnlyList<string> NoticesSwitchIds { get; } =
            ["TextDocumentPartMain", "TextDocumentPartAvalonia", "TextDocumentPartCommunityToolkitMvvm", "TextDocumentPartOnigwrap", "TextDocumentPartSkiaSharp"];

        // ---- Where About opens from (D417) ----------------------------------------------------

        public const string PaletteCommand = "About VisualCommit…";
        public const string MenuItem = "About VisualCommit";

        /// <summary>The palette's query of A1 and A6.</summary>
        public const string PaletteQuery = "about";

        /// <summary>The command the palette lists right before "About VisualCommit…" (ruling 54).</summary>
        public const string BeforeAbout = "Follow the system's theme";

        // ---- The log lines (8.3.1, 8.3.2), which no picture shows ----------------------------

        /// <summary>The Error when a text cannot be read; {0} the resource's name.</summary>
        public const string TextNotReadLine = "The text {0} could not be read.";

        public const string RepositoryFailedLine = "Opening the repository's page failed.";
        public const string GuideFailedLine = "Opening the user guide failed.";
        public const string CopyFailedLine = "Copying the text to the clipboard failed.";

                // ---- The addresses (1.7) --------------------------------------------------------------

        public const string RepositoryAddress = "https://github.com/RahimPasha/VisualCommit";
        public const string GuideAddress = "https://rahimpasha.github.io/VisualCommit/";

        // ---- The files the texts are (8.2, 1.13), under the repository ------------------------

        public const string LicenseFile = "LICENSE";
        public const string NoticesFile = "THIRD-PARTY-NOTICES.md";
        public const string AvaloniaNoticeFile = "notices/Avalonia-NOTICE.md";

        /// <summary><c>LICENSE</c>'s lines; the document holds one more, the empty line after its final line end.</summary>
        public const int LicenseLines = 21;

        // ---- The ids (1.13) -------------------------------------------------------------------

        public const string NavId = "SettingsNav";
        public const string NavAboutId = "SettingsNavAbout";
        public const string NavAccountsId = "SettingsNavAccounts";
        public const string SectionId = "SettingsAbout";
        public const string SectionScrollerId = "SettingsSection";
        public const string IconId = "AboutIcon";
        public const string NameId = "AboutName";
        public const string CreatorId = "AboutCreator";
        public const string VersionId = "AboutVersion";
        public const string CopyrightId = "AboutCopyright";
        public const string LicenseId = "AboutLicense";
        public const string ShowLicenseId = "AboutShowLicenseButton";
        public const string NoticesNoteId = "AboutNoticesNote";
        public const string ShowNoticesId = "AboutShowNoticesButton";
        public const string RepositoryLinkId = "AboutRepositoryLink";
        public const string GuideLinkId = "AboutGuideLink";
        public const string DialogId = "TextDocumentDialog";
        public const string DialogTitleId = "DialogTitle";
        public const string SwitchId = "TextDocumentSwitch";
        public const string TextId = "TextDocumentText";
        public const string CopyId = "TextDocumentCopyButton";
        public const string CloseId = "ConfirmButton";

        /// <summary>The section list's buttons, top to bottom, each named by its text.</summary>
        public static IReadOnlyList<string> NavButtons { get; } = ["General", "Git", "Tools", "Keyboard shortcuts", "Accounts", Section];

        public static IReadOnlyList<string> NavIds { get; } = ["SettingsNavGeneral", "SettingsNavGit", "SettingsNavTools", "SettingsNavShortcuts", NavAccountsId, NavAboutId];

        /// <summary>The section's controls that take the focus, in Tab's order from <see cref="NavAboutId"/>; Cancel and Save follow.</summary>
        public static IReadOnlyList<string> TabOrder { get; } = [ShowLicenseId, ShowNoticesId, RepositoryLinkId, GuideLinkId];

        // ---- The places (8.5; open points 56, 58, 59), window coordinates at scale 1 ----------

        /// <summary>Places are asserted within this, as phase 7a's and 7b's section list (7a's <c>PartA.Tolerance</c>).</summary>
        public const double Tolerance = 2;

        /// <summary>About's button at 1100×700, exactly <see cref="AboutBelowAccounts"/> below Accounts' with the same left.</summary>
        public static readonly (double X, double Y) AboutAt = (141, 255);

        /// <summary>About's button at 1920×1080.</summary>
        public static readonly (double X, double Y) AboutAtFull = (551, 415);

        public const double AboutBelowAccounts = 32;
        public const double NavWidth = 176;
        public const double NavHeight = 30;
        public const double NavGap = 2;

        /// <summary>The settings card at 1100×700 and at 1920×1080 (phase 7a's <c>Card</c>, <c>CardFull</c>).</summary>
        public static readonly (double X, double Y, double Width, double Height) Card = (120, 40, 860, 620);

        public static readonly (double X, double Y, double Width, double Height) CardFull = (530, 200, 860, 680);

        /// <summary>Where the section's labels start (phase 7a's <c>SectionX</c>) and its values (<c>ControlsX</c>).</summary>
        public const double SectionX = 336;

        public const double ValuesX = 480;
        public const double SectionXFull = 746;
        public const double ValuesXFull = 890;

        /// <summary>The section's rows lie this far apart (the stack's spacing).</summary>
        public const double RowSpacing = 18;

        /// <summary>A Show button lies this far below its row's text.</summary>
        public const double ButtonBelowText = 10;

        /// <summary>The icon's size, and the name's left: the icon, then 16.</summary>
        public const double IconSize = 48;

        public const double NameAfterIcon = 16;

        public const double NameFontSize = 20;

        /// <summary>The text dialog's card at 1100×700: 760 × 600, centred by its maximum.</summary>
        public static readonly (double X, double Y, double Width, double Height) DialogCard = (170, 50, 760, 600);

        /// <summary>The switch lies 12 below the title, the text 8 below the switch; with one text, no switch, the text 12 below the title.</summary>
        public const double SwitchBelowTitle = 12;

        public const double TextBelowSwitch = 8;
        public const double TextBelowTitleAlone = 12;

        /// <summary>The buttons' row lies 20 below the text.</summary>
        public const double ButtonsBelowText = 20;

        /// <summary>The switch's buttons: 28 high, 4 apart, in one row.</summary>
        public const double SwitchButtonHeight = 28;

        public const double SwitchButtonGap = 4;

        /// <summary>The dialog's title (<c>dialogTitle</c>) and the text's code font.</summary>
        public const double TitleFontSize = 16;

        public const double TextFontSize = 11.5;

        /// <summary>A5: from the click on Show third-party notices… to the dialog laid out with its first line in view (8.5).</summary>
        public static readonly TimeSpan NoticesWithin = TimeSpan.FromSeconds(1);

        // ---- The colours: tokens the contrast round keeps, dark then light --------------------

        /// <summary>The colours part A reads, in one theme.</summary>
        public sealed record Colours(
            Color Accent,
            Color Panel,
            Color Secondary,
            Color Primary,
            Color Control,
            Color Border,
            Color Selection,
            Color Backdrop);

        public static readonly Colours Dark = new(
            Accent: Color.FromRgb(0x7C, 0x8C, 0xFF),
            Panel: Color.FromRgb(0x1A, 0x1D, 0x24),
            Secondary: Color.FromRgb(0x9B, 0xA3, 0xB0),
            Primary: Color.FromRgb(0xE4, 0xE7, 0xEC),
            Control: Color.FromRgb(0x27, 0x2C, 0x36),
            Border: Color.FromRgb(0x32, 0x38, 0x45),
            Selection: Color.FromRgb(0x2A, 0x31, 0x50),
            Backdrop: Color.FromArgb(0x99, 0x00, 0x00, 0x00));

        public static readonly Colours Light = new(
            Accent: Color.FromRgb(0x43, 0x53, 0xD8),
            Panel: Color.FromRgb(0xF5, 0xF6, 0xF8),
            Secondary: Color.FromRgb(0x5C, 0x65, 0x72),
            Primary: Color.FromRgb(0x1B, 0x1F, 0x27),
            Control: Color.FromRgb(0xFF, 0xFF, 0xFF),
            Border: Color.FromRgb(0xD2, 0xD6, 0xDE),
            Selection: Color.FromRgb(0xDD, 0xE1, 0xFA),
            Backdrop: Color.FromArgb(0x66, 0x00, 0x00, 0x00));

        /// <summary>The icon's centre is white, its top-left pixel the card's background, each within this per channel (part B's reading).</summary>
        public const int IconTolerance = 2;

        // ---- The pictures (the report's Screenshots column) ------------------------------------

        public const string A1aAboutDark = "a1a-about-dark";
        public const string A1bAboutLinkFocus = "a1b-about-link-focus";
        public const string A2aAboutLight = "a2a-about-light";
        public const string A3aAbout1920 = "a3a-about-1920";
        public const string A4aLicense = "a4a-license";
        public const string A5aNoticesTop = "a5a-notices-top";
        public const string A5bNoticesEnd = "a5b-notices-end";
        public const string A5cNoticesAvalonia = "a5c-notices-avalonia";
        public const string A6aPaletteAbout = "a6a-palette-about";

        /// <summary>Every scripted picture of part A, in the order of its methods.</summary>
        public static IReadOnlyList<string> Pictures { get; } =
            [A1aAboutDark, A4aLicense, A1bAboutLinkFocus, A2aAboutLight, A3aAbout1920, A5aNoticesTop, A5bNoticesEnd, A5cNoticesAvalonia, A6aPaletteAbout];

        // ---- The real window: launch 3's part A (12.3.4) ---------------------------------------

        /// <summary>The launch's session: one empty tab, dark, at <see cref="DefaultSize"/>, no settings written.</summary>
        public const int LaunchEmptyTabs = 1;

        /// <summary>The captures in the launch's order, each compared with its scripted twin of the same name; the second only where 1920×1080 fits.</summary>
        public static IReadOnlyList<string> RealWindowPictures { get; } = [A1aAboutDark, A3aAbout1920, A4aLicense];

        /// <summary>run.txt's line when 1920×1080 does not fit; {0} and {1} the work area, {2} the scaling.</summary>
        public const string FullSizeNotRunLine = "A1 (1920x1080): not run. A 1920x1080 window does not fit the {0}x{1} work area at {2} scaling.";

        /// <summary><c>AboutVersion</c>'s name in the real window ends so: a space, seven hexadecimal digits in brackets.</summary>
        public const string BuiltVersionEnding = @" \([0-9a-f]{7}\)$";
    }
}
