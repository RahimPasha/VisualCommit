using Avalonia.Media;

namespace VisualCommit.VisualTests.Phase8a;

public static partial class Phase8aExpectations
{
    /// <summary>
    /// What part L's checks compare against (docs/test-reports/phase-8a.md, "Part L: the contrast
    /// round"; docs/phase-8a-design.md 1.5, 2.3, 2.5; D410, D418): the colours by token, dark and
    /// light, with the colours they replace; the texts the steps pass through; the picture names;
    /// and, for the real window's two launches (design 12.3.4 launches 1 and 2), each launch's
    /// scenario, starting theme, size and pictures. Written from the report, not from the app.
    /// </summary>
    public static class L
    {
        // ---- The colours (the report's "Colours by token") -------------------------------------

        /// <summary>The dark theme's colours of part L.</summary>
        public static Colours Dark { get; } = new(
            Placeholder: Color.Parse("#8F97A4"),
            PlaceholderReplaced: Color.Parse("#616773"),
            Box: Color.Parse("#272C36"),
            Typed: Color.Parse("#E4E7EC"),
            Secondary: Color.Parse("#9BA3B0"),
            Danger: Color.Parse("#EA7D86"),
            DangerReplaced: Color.Parse("#E5606B"),
            DangerText: Color.Parse("#10121A"),
            DangerTextReplaced: Color.Parse("#FFFFFF"),
            Accent: Color.Parse("#7C8CFF"),
            AccentOnRow: Color.Parse("#8B9AFF"),
            Lane3: Color.Parse("#E5606B"),
            Window: Color.Parse("#14161B"),
            Panel: Color.Parse("#1A1D24"),
            Hover: Color.Parse("#2F3542"),
            Selection: Color.Parse("#2A3150"),
            Control: Color.Parse("#272C36"),
            Backdrop: Color.Parse("#99000000"));

        /// <summary>The light theme's colours of part L: the round keeps the light danger, its label and the accent.</summary>
        public static Colours Light { get; } = new(
            Placeholder: Color.Parse("#68707D"),
            PlaceholderReplaced: Color.Parse("#ADB2B8"),
            Box: Color.Parse("#FFFFFF"),
            Typed: Color.Parse("#1B1F27"),
            Secondary: Color.Parse("#5C6572"),
            Danger: Color.Parse("#B7323D"),
            DangerReplaced: Color.Parse("#B7323D"),
            DangerText: Color.Parse("#FFFFFF"),
            DangerTextReplaced: Color.Parse("#FFFFFF"),
            Accent: Color.Parse("#4353D8"),
            AccentOnRow: Color.Parse("#4353D8"),
            Lane3: Color.Parse("#C93A46"),
            Window: Color.Parse("#FFFFFF"),
            Panel: Color.Parse("#F5F6F8"),
            Hover: Color.Parse("#DEE2E9"),
            Selection: Color.Parse("#DDE1FA"),
            Control: Color.Parse("#FFFFFF"),
            Backdrop: Color.Parse("#66000000"));

        /// <summary>
        /// One theme's colours of part L, by token (design 1.5): <c>VcTextPlaceholderBrush</c> (and the
        /// three <c>TextControlPlaceholderForeground…</c> keys) with what it replaces (the secondary
        /// text at Fluent's opacity 0.5 on the box), <c>TextControlBackground</c>,
        /// <c>TextControlForeground</c>, <c>VcTextSecondaryBrush</c> (the Summary counter),
        /// <c>VcDangerBrush</c> and <c>VcDangerTextBrush</c> with what they replace,
        /// <c>VcAccentBrush</c>, <c>VcAccentOnRowBrush</c>, <c>VcLane3Brush</c>, and the backgrounds
        /// a check reads them on (the window's, under the backdrop); the backdrop's colour with its alpha.
        /// </summary>
        public sealed record Colours(
            Color Placeholder,
            Color PlaceholderReplaced,
            Color Box,
            Color Typed,
            Color Secondary,
            Color Danger,
            Color DangerReplaced,
            Color DangerText,
            Color DangerTextReplaced,
            Color Accent,
            Color AccentOnRow,
            Color Lane3,
            Color Window,
            Color Panel,
            Color Hover,
            Color Selection,
            Color Control,
            Color Backdrop);

        /// <summary>The value of <c>TextControlPlaceholderOpacity</c> in both variants (Fluent's own is 0.5).</summary>
        public const double PlaceholderOpacity = 1;

        /// <summary>
        /// How far apart a check tells two colours, the largest difference in one channel (the
        /// report's part L conventions): the placeholder from the secondary text, the accent on a row
        /// from the accent, the danger from lane 3. Each is more than <c>TextInk.Tolerance</c>.
        /// </summary>
        public const int PlaceholderFromSecondary = 12;

        /// <inheritdoc cref="PlaceholderFromSecondary"/>
        public const int AccentOnRowFromAccent = 15;

        /// <inheritdoc cref="PlaceholderFromSecondary"/>
        public const int DangerFromLane3 = 29;

        /// <summary>
        /// <c>CommitSummary</c>'s right padding, inside which <c>SummaryCounter</c> is drawn: the
        /// placeholder's rectangle ends this far from the box's right edge (the report's "How each
        /// colour is read").
        /// </summary>
        public const double SummaryRightPadding = 40;

        // ---- The texts the steps pass through (the report's "Texts these checks rely on") --------

        public const string Filter = "Filter";
        public const string Summary = "Summary";
        public const string Description = "Description";
        public const string PalettePlaceholder = "Search commands, branches, tags, stashes, remotes, repositories and workspaces";
        public const string SearchPlaceholder = "Message, author or SHA";
        public const string BranchName = "Branch name";
        public const string RemoteName = "Name";
        public const string RemoteUrl = "URL";
        public const string NotSet = "Not set";
        public const string SummaryLimit = "72";
        public const string Typed = "sea";
        public const string SearchModeCommits = "Commits";

        public const string LocalBranches = "Local branches";
        public const string Remotes = "Remotes";
        public const string Feature = "feature";
        public const string Login = "login";
        public const string Main = "main";
        public const string Origin = "origin";
        public const string HeadCommit = "Add settings page";

        public const string SetUpstreamItem = "Set upstream…";
        public const string SetUpstreamTitle = "Set upstream for feature/login";
        public const string CreateBranchItem = "Create branch here…";
        public const string CreateBranchTitle = "Create branch";
        public const string ManageRemotesItem = "Manage remotes…";
        public const string RemoveRemote = "Remove";
        public const string AddRemote = "Add remote";
        public const string GlobalScope = "All repositories (global)";
        public const string FeatureLoginMenuHeader = "feature/login";
        public const string DeleteFeatureLogin = "Delete feature/login…";
        public const string PaletteQuery = "delete main";
        public const string DeleteMainOnOrigin = "Delete main on origin…";
        public const string DeleteMainOnOriginDetail = "origin/main";
        public const string CompareMainWithOriginMain = "Compare main with origin/main";
        public const string FileHistoryItem = "File history";

        public const string DiscardTitle = "Discard changes?";
        public const string DiscardQuestion = "Discard the changes to Calculator.cs?";
        public const string DiscardNote = "A snapshot is saved first, so the changes can be restored.";
        public const string Cancel = "Cancel";
        public const string Discard = "Discard";

        public const string Calculator = "src/Calculator.cs";
        public const string Notes = "docs/notes.txt";
        public const string MainApp = "src/main-app.txt";
        public const string App = "src/app.txt";
        public const string Helpers = "src/helpers.py";
        public const string Util = "src/util.py";

        /// <summary>A file row's tooltip (<c>ChangedFileRow.ToolTip</c>): the path, and for a rename its origin on a second line.</summary>
        public static string RenamedToolTip(string path, string from) => path + "\nrenamed from " + from;

        // ---- The pictures (the report's Screenshots column) -----------------------------------

        public const string L1aPlaceholdersDark = "l1a-placeholders-dark";
        public const string L1bFilterTypedDark = "l1b-filter-typed-dark";
        public const string L2aPlaceholdersLight = "l2a-placeholders-light";
        public const string L2bFilterTypedLight = "l2b-filter-typed-light";
        public const string L3aPalette = "l3a-palette-placeholder";
        public const string L3bSearch = "l3b-search-placeholder";
        public const string L3cPickFilter = "l3c-pick-filter-placeholder";
        public const string L3dBranchName = "l3d-branch-name-placeholder";
        public const string L3eRemoteForm = "l3e-remote-form-placeholders";
        public const string L3fSettingsName = "l3f-settings-name-placeholder";
        public const string L4aDeleteItemMenu = "l4a-delete-item-menu";
        public const string L4bDeleteEntryPalette = "l4b-delete-entry-palette";
        public const string L4cDeletedFileHovered = "l4c-deleted-file-hovered";
        public const string L4dDeletedFileSelected = "l4d-deleted-file-selected";
        public const string L4eRemoveButtonAtRest = "l4e-remove-button-at-rest";
        public const string L4fRemoveButtonHovered = "l4f-remove-button-hovered";
        public const string L5aDiscardDialogDark = "l5a-discard-dialog-dark";
        public const string L5bDiscardDialogLight = "l5b-discard-dialog-light";
        public const string L6aHeadIdle = "l6a-head-idle";
        public const string L6bHeadHovered = "l6b-head-hovered";
        public const string L6cHeadSelected = "l6c-head-selected";
        public const string L6dRenamedIdle = "l6d-renamed-idle";
        public const string L6eRenamedHovered = "l6e-renamed-hovered";
        public const string L6fRenamedSelected = "l6f-renamed-selected";
        public const string L6gCompareRenamedSelected = "l6g-compare-renamed-selected";
        public const string L6hHistoryRenamedSelected = "l6h-history-renamed-selected";
        public const string L6iStagedRenamedHovered = "l6i-staged-renamed-hovered";
        public const string L6jStagedRenamedSelected = "l6j-staged-renamed-selected";
        public const string L6kHeadIdleLight = "l6k-head-idle-light";
        public const string L6lHeadHoveredLight = "l6l-head-hovered-light";
        public const string L6mHeadSelectedLight = "l6m-head-selected-light";

        /// <summary>Every picture of part L, in the order of the report's methods table.</summary>
        public static IReadOnlyList<string> Pictures { get; } =
        [
            L1aPlaceholdersDark, L1bFilterTypedDark, L5aDiscardDialogDark, L2aPlaceholdersLight, L2bFilterTypedLight, L5bDiscardDialogLight,
            L4aDeleteItemMenu, L6aHeadIdle, L6bHeadHovered, L6cHeadSelected,
            L3aPalette, L3bSearch, L3cPickFilter, L3dBranchName, L3eRemoteForm, L3fSettingsName,
            L4bDeleteEntryPalette, L4cDeletedFileHovered, L4dDeletedFileSelected, L4eRemoveButtonAtRest, L4fRemoveButtonHovered,
            L6dRenamedIdle, L6eRenamedHovered, L6fRenamedSelected, L6gCompareRenamedSelected, L6hHistoryRenamedSelected,
            L6iStagedRenamedHovered, L6jStagedRenamedSelected,
            L6kHeadIdleLight, L6lHeadHoveredLight, L6mHeadSelectedLight,
        ];

        // ---- The real window's two launches (design 12.3.4 launches 1 and 2; D442) -------------

        /// <summary>
        /// The real window's launch 1, the twin of method 1: one tab on a fresh "changes"
        /// (<c>Scenarios.ChangesAsync</c>), dark, 1100×700; its captures in order.
        /// </summary>
        public static Launch ChangesLaunch { get; } = new(
            "Check_L_changes_placeholders_and_danger_button",
            "changes",
            Dark: true,
            DefaultSize,
            [L1aPlaceholdersDark, L1bFilterTypedDark, L5aDiscardDialogDark, L2aPlaceholdersLight, L2bFilterTypedLight]);

        /// <summary>
        /// The real window's launch 2, the twin of method 2: one tab on a fresh "graph"
        /// (<c>Scenarios.GraphAsync</c>), dark, 1100×700; its captures in order.
        /// </summary>
        public static Launch GraphLaunch { get; } = new(
            "Check_L_graph_danger_item_and_head_row",
            "graph",
            Dark: true,
            DefaultSize,
            [L4aDeleteItemMenu, L6aHeadIdle, L6bHeadHovered, L6cHeadSelected]);

        /// <summary>
        /// A real-window launch of part L: its method's name, the scenario its one tab opens (a
        /// fresh copy), whether it starts dark, its window's size, and the captures it takes, each
        /// compared with the scripted picture of the same name.
        /// </summary>
        public sealed record Launch(string Method, string Scenario, bool Dark, (int Width, int Height) Size, IReadOnlyList<string> Captures);
    }
}
