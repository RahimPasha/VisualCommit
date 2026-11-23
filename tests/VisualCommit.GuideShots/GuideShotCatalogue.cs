namespace VisualCommit.GuideShots;

/// <summary>
/// The guide's 42 pictures by name (docs/phase-8a-design.md 11.3.2), each saved in both themes as
/// <c>&lt;name&gt;-dark.png</c> and <c>&lt;name&gt;-light.png</c>: 84 files per build. A page shows a
/// picture as two images, light first (D440), and <see cref="GuideCatalogueTests"/> holds the pages
/// and this list to each other. Grouped by the guide's parts, in <c>SUMMARY.md</c>'s order.
/// </summary>
public static class GuideShotCatalogue
{
    // Introduction; also the tour of the window.
    public const string MainWindow = "main-window";

    // Getting started.
    public const string FirstRunMissing = "first-run-missing";
    public const string FirstRunMissingMacOS = "first-run-missing-macos";
    public const string FirstRunMissingLinux = "first-run-missing-linux";
    public const string FirstRunTooOld = "first-run-too-old";
    public const string FirstRunNotWorking = "first-run-not-working";
    public const string FirstRunWingetConfirm = "first-run-winget-confirm";
    public const string StatusBarGit = "status-bar-git";
    public const string WelcomePage = "welcome-page";
    public const string CloneForm = "clone-form";

    // Everyday work.
    public const string DiffSideBySide = "diff-side-by-side";
    public const string DiffInline = "diff-inline";
    public const string WorkingChanges = "working-changes";
    public const string StageHunk = "stage-hunk";
    public const string BranchMenu = "branch-menu";
    public const string CreateBranch = "create-branch";
    public const string AheadBehind = "ahead-behind";
    public const string ManageRemotes = "manage-remotes";
    public const string StashInGraph = "stash-in-graph";
    public const string CreateTag = "create-tag";

    // Combine and rewrite.
    public const string DragMenu = "drag-menu";
    public const string InteractiveList = "interactive-list";
    public const string CommitMenu = "commit-menu";
    public const string ResetQuestion = "reset-question";
    public const string Resolver = "resolver";

    // Find history.
    public const string GraphLabels = "graph-labels";
    public const string SearchBar = "search-bar";
    public const string FileHistory = "file-history";
    public const string Blame = "blame";
    public const string Compare = "compare";

    // Stay safe.
    public const string UndoDone = "undo-done";
    public const string Reflog = "reflog";
    public const string RefusedOperation = "refused-operation";
    public const string ActivityLog = "activity-log";

    // Hosting accounts.
    public const string AccountsSection = "accounts-section";
    public const string SignInDialog = "sign-in-dialog";
    public const string PullRequests = "pull-requests";

    // Many repositories.
    public const string Dashboard = "dashboard";

    // Make it yours.
    public const string SettingsGeneral = "settings-general";
    public const string SettingsGit = "settings-git";
    public const string Palette = "palette";

    // Reference.
    public const string AboutSection = "about-section";

    /// <summary>The 42 names, in the order above.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        MainWindow,
        FirstRunMissing, FirstRunMissingMacOS, FirstRunMissingLinux, FirstRunTooOld, FirstRunNotWorking, FirstRunWingetConfirm, StatusBarGit, WelcomePage, CloneForm,
        DiffSideBySide, DiffInline, WorkingChanges, StageHunk, BranchMenu, CreateBranch, AheadBehind, ManageRemotes, StashInGraph, CreateTag,
        DragMenu, InteractiveList, CommitMenu, ResetQuestion, Resolver,
        GraphLabels, SearchBar, FileHistory, Blame, Compare,
        UndoDone, Reflog, RefusedOperation, ActivityLog,
        AccountsSection, SignInDialog, PullRequests,
        Dashboard,
        SettingsGeneral, SettingsGit, Palette,
        AboutSection,
    ];

    /// <summary>The file a picture is saved as in a theme: <c>&lt;name&gt;-dark.png</c> or <c>&lt;name&gt;-light.png</c>.</summary>
    public static string FileName(string name, GuideTheme theme) =>
        name + (theme == GuideTheme.Dark ? "-dark.png" : "-light.png");
}

/// <summary>The app's two themes, in which every picture is taken.</summary>
public enum GuideTheme
{
    Dark,
    Light,
}
