using System.Globalization;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// The texts of the workspaces' own screens and flows (docs/phase-6-design.md 1.5 to 1.10, 13.1,
/// 14.5, 14.6, 14.9, 14.10): the welcome page's Workspaces section, the commands' labels and
/// reasons, the name dialog, the deletion's question, Add repository… and its checklist, Locate….
/// Counts take the singular for one (D113); numbers are written with the invariant culture's
/// group separator (<c>10,000</c>).
/// </summary>
public static class WorkspaceTexts
{
    // ---- The welcome page's Workspaces section (13.1, 14.9). ----

    /// <summary>The section's heading (<c>WorkspacesHeading</c>).</summary>
    public const string Heading = "Workspaces";

    /// <summary><c>NewWorkspaceButton</c>'s label, also the palette's (5.4).</summary>
    public const string NewWorkspace = "New workspace…";

    /// <summary><c>NewWorkspaceButton</c>'s automation name (15.5).</summary>
    public const string NewWorkspaceName = "New workspace";

    /// <summary><c>NewWorkspaceButton</c>'s tooltip while it is enabled.</summary>
    public const string NewWorkspaceToolTip = "Create a workspace: repositories you check and update together";

    /// <summary><c>NoWorkspacesText</c>: the list with no workspace.</summary>
    public const string NoWorkspaces = "No workspaces";

    /// <summary>A workspace item's tooltip while it has no repository.</summary>
    public const string NoRepositoriesYet = "No repositories yet";

    /// <summary>How many folder names a workspace item's tooltip shows before <c>, …</c>.</summary>
    public const int ToolTipFolderNames = 8;

    // ---- Labels (1.5, 14.10). ----

    /// <summary>The welcome item menu's first item: opens the workspace.</summary>
    public const string Open = "Open";

    public const string RenameWorkspace = "Rename workspace…";

    public const string DeleteWorkspace = "Delete workspace…";

    public const string AddRepository = "Add repository…";

    public const string AddOpenTabs = "Add open tabs";

    public const string RemoveFromWorkspace = "Remove from workspace";

    public const string MoveUp = "Move up";

    public const string MoveDown = "Move down";

    public const string Locate = "Locate…";

    // ---- Disabled reasons (1.5, 14.5). ----

    /// <summary><c>Add open tabs</c> while no tab has a repository open.</summary>
    public const string NoTabHasARepositoryOpen = "No tab has a repository open";

    /// <summary><c>Add open tabs</c> while the workspace holds every open repository.</summary>
    public const string EveryOpenRepositoryIsInTheWorkspace = "Every open repository is in this workspace";

    /// <summary><c>Move up</c> on the first row.</summary>
    public const string FirstRepository = "It is the first repository";

    /// <summary><c>Move down</c> on the last row.</summary>
    public const string LastRepository = "It is the last repository";

    // ---- The name dialog (1.6, 14.6). ----

    public const string NewWorkspaceTitle = "New workspace";

    public const string RenameWorkspaceTitle = "Rename workspace";

    public const string NamePlaceholder = "Workspace name";

    public const string CreateConfirm = "Create";

    public const string RenameConfirm = "Rename";

    // ---- The deletion (1.6, 14.6). ----

    public const string DeleteTitle = "Delete workspace?";

    /// <summary>The detail while the workspace's dashboard is open.</summary>
    public const string DeleteDetail = "Its dashboard tab closes.";

    public const string DeleteConfirm = "Delete";

    // ---- Errors (1.7, 1.10, 14.4). ----

    public const string AddingFailedTitle = "Adding a repository failed";

    public const string LocatingFailedTitle = "Locating the repository failed";

    // ---- The "Add repositories" checklist (1.9, 14.6). ----

    /// <summary>A row of the workspace's own repositories: checked and disabled.</summary>
    public const string AlreadyInThisWorkspace = "Already in this workspace";

    /// <summary>The confirm button while no row would be added.</summary>
    public const string AddRepositoriesConfirm = "Add repositories";

    /// <summary>How many relative paths <see cref="CouldNotOpen"/>'s tooltip lists before <c>…</c>.</summary>
    public const int FailedPathsShown = 10;

    /// <summary><c>1 repository</c>, <c>{N} repositories</c> or <c>No repositories</c>: a workspace item's count (13.1).</summary>
    public static string RepositoryCount(int count) => count switch
    {
        0 => "No repositories",
        1 => "1 repository",
        _ => $"{Number(count)} repositories",
    };

    /// <summary>
    /// A workspace item's tooltip (13.1): its repositories' folder names in order, joined by
    /// <c>, </c>, at most <see cref="ToolTipFolderNames"/>, then <c>, …</c>; or <c>No repositories yet</c>.
    /// </summary>
    public static string FolderNames(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var names = paths.Select(FolderName).ToList();
        if (names.Count == 0)
        {
            return NoRepositoriesYet;
        }

        var shown = string.Join(", ", names.Take(ToolTipFolderNames));
        return names.Count > ToolTipFolderNames ? shown + ", …" : shown;
    }

    /// <summary>A folder's own name: the last name of <paramref name="path"/>, or the path itself for a root.</summary>
    public static string FolderName(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return name.Length > 0 ? name : path;
    }

    /// <summary>The deletion's question (1.6), by how many repositories the workspace holds.</summary>
    public static string DeleteQuestion(string name, int repositories) => repositories switch
    {
        0 => $"Delete the workspace {name}? Nothing on disk changes.",
        1 => $"Delete the workspace {name}? Its repository stays where it is: nothing on disk changes.",
        _ => $"Delete the workspace {name}? Its {Number(repositories)} repositories stay where they are: nothing on disk changes.",
    };

    /// <summary>The rename dialog's context line.</summary>
    public static string RenameContext(string name) => $"Rename {name}";

    /// <summary>The folder dialog of <c>Add repository…</c> (1.7), also the header button's tooltip (2.5).</summary>
    public static string AddRepositoryFolderTitle(string workspace) => $"Add a repository to {workspace}";

    /// <summary>The folder dialog of <c>Locate…</c> (1.10), by the saved path's last name.</summary>
    public static string LocateFolderTitle(string folderName) => $"Locate {folderName}";

    /// <summary>A folder git refused to open (1.7, 1.10).</summary>
    public static string GitCouldNotOpen(string folder) => $"Git could not open {folder}.";

    /// <summary><c>Locate…</c> on a folder that is no repository (1.10).</summary>
    public static string NotAGitRepository(string folder) => $"{folder} is not a git repository.";

    /// <summary>
    /// The palette's <c>Open repository</c> of a workspace's folder that lies inside another working
    /// tree (D204, 5.10), on the tab's welcome page: the row's State tooltip as a sentence.
    /// </summary>
    public static string NotATopOf(string path, string outer) => $"{WorkspaceFolderProbe.NotARepositoryInside(path, outer).ToolTip}.";

    /// <summary><c>Locate…</c> on a repository the workspace holds already (1.10).</summary>
    public static string AlreadyIn(string top, string workspace) => $"{top} is already in {workspace}.";

    /// <summary>The checklist's title (<c>AddRepositoriesTitle</c>).</summary>
    public static string AddRepositoriesTitle(string workspace) => $"Add repositories to {workspace}";

    /// <summary><c>ScanFolderText</c> while the search runs.</summary>
    public static string LookingIn(string folder) => $"Looking for repositories in {folder}…";

    /// <summary>
    /// <c>ScanFolderText</c> once done (1.9): <c>Found {N} repositories in {folder}.</c>,
    /// <c>Found 1 repository in {folder}.</c>, <c>No repositories were found in {folder}.</c>, or with
    /// the repository holding the folder listed and nothing else found <c>No other repositories were
    /// found in {folder}.</c>. <paramref name="found"/> counts the repositories found in the folder,
    /// the workspace's own among them, not the one holding it.
    /// </summary>
    public static string FoundIn(int found, string folder, bool enclosingListed) => found switch
    {
        0 when enclosingListed => $"No other repositories were found in {folder}.",
        0 => $"No repositories were found in {folder}.",
        1 => $"Found 1 repository in {folder}.",
        _ => $"Found {Number(found)} repositories in {folder}.",
    };

    /// <summary><c>ScanProgressText</c>: <c>Searched {n} folders, found {k} repositories</c>, singular for one.</summary>
    public static string Searched(int folders, int found) =>
        $"Searched {(folders == 1 ? "1 folder" : $"{Number(folders)} folders")}, found {(found == 1 ? "1 repository" : $"{Number(found)} repositories")}";

    /// <summary>The row of the repository holding the chosen folder: <c>Contains {folder name}</c>.</summary>
    public static string Contains(string folderName) => $"Contains {folderName}";

    /// <summary><c>ScanStoppedText</c>: the search stopped at its limit of folders.</summary>
    public static string StoppedAfter(int folders) =>
        $"Stopped after {Number(folders)} folders: choose a folder further down to search the rest.";

    /// <summary><c>ScanFailedText</c>: candidates git refused or passed over.</summary>
    public static string CouldNotOpen(int count) => count == 1
        ? "1 more folder looks like a repository, but git could not open it."
        : $"{Number(count)} more folders look like repositories, but git could not open them.";

    /// <summary><c>ScanFailedText</c>'s tooltip: the relative paths, one a line, at most <see cref="FailedPathsShown"/>, then <c>…</c>.</summary>
    public static string CouldNotOpenToolTip(IReadOnlyList<string> relativePaths)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);
        var shown = relativePaths.Take(FailedPathsShown).ToList();
        if (relativePaths.Count > FailedPathsShown)
        {
            shown.Add("…");
        }

        return string.Join('\n', shown);
    }

    /// <summary>The checklist's confirm button: <c>Add {N} repositories</c>, <c>Add 1 repository</c>, or <c>Add repositories</c> (disabled) for none.</summary>
    public static string AddConfirm(int count) => count switch
    {
        0 => AddRepositoriesConfirm,
        1 => "Add 1 repository",
        _ => $"Add {Number(count)} repositories",
    };

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
