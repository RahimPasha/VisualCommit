using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 6's workspaces before the start (docs/phase-6-design.md 17.2, D189): the workspaces file
/// and the session's tabs, dashboards among them, written as the app writes them. Work package
/// WP-W adds the welcome section, the add-repositories checklist and the starts on a workspace
/// to this file.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>
    /// Writes the workspaces file of a data folder (<see cref="AppPaths.WorkspacesFile"/>) before the
    /// app starts on it, through the store's own <see cref="JsonWorkspaceStore.Write"/>, as
    /// <see cref="WriteSession"/> writes the session.
    /// </summary>
    public static void WriteWorkspaces(string dataDirectory, WorkspaceList workspaces) =>
        JsonWorkspaceStore.Write(new AppPaths(dataDirectory).WorkspacesFile, workspaces);

    /// <summary>
    /// Starts the app as <see cref="Start"/> does, after writing a session file with these tabs:
    /// <see cref="StartWithTabs(string, IReadOnlyList{string?}, int, int?, int?, IGitPromptHandler?, bool, TimeProvider?)"/>
    /// for any tab the session can hold, a workspace's dashboard (<c>new TabState(null, workspaceId)</c>,
    /// docs/phase-6-design.md 1.11) included. Write the workspaces first (<see cref="WriteWorkspaces"/>).
    /// </summary>
    public static ShellDriver StartWithTabs(
        string dataDirectory,
        IReadOnlyList<TabState> tabs,
        int activeTab = 0,
        int? width = 1100,
        int? height = 700,
        IGitPromptHandler? promptHandler = null,
        bool systemDark = true,
        TimeProvider? readClock = null)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        WriteSession(dataDirectory, new SessionState { Tabs = [.. tabs], ActiveTab = activeTab });
        return Start(dataDirectory, width, height, promptHandler, systemDark, readClock);
    }
}
