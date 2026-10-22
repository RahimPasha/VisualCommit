using VisualCommit.App.ViewModels.Workspaces;

namespace VisualCommit.App.ViewModels.Menus;

/// <summary>
/// The workspaces' menus outside the dashboard (docs/phase-6-design.md 13.1, 14.10), as data (D86):
/// what the menu layer draws for a welcome page's workspace item.
/// </summary>
public static class WorkspaceMenus
{
    /// <summary>
    /// A welcome page's workspace (13.1): header the name; <c>Open</c>; separator; <c>Rename
    /// workspace…</c>; <c>Delete workspace…</c> (destructive), the last two disabled with the
    /// read-only reason while the store is read-only.
    /// </summary>
    /// <param name="item">The workspace's item.</param>
    /// <param name="commands">The session's workspace commands.</param>
    /// <param name="from">The welcome page's tab: <c>Open</c> opens the workspace from it (2.2).</param>
    public static MenuModel ForWelcomeItem(WorkspaceItem item, WorkspaceCommands commands, RepoTabViewModel? from)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(commands);
        var renameReason = commands.RenameReason(item.Id);
        var deleteReason = commands.DeleteReason(item.Id);
        return new MenuModel(
        [
            new MenuHeader(item.Name),
            new MenuItem(WorkspaceTexts.Open, () => commands.Open(item.Id, from)),
            new MenuSeparator(),
            new MenuItem(WorkspaceTexts.RenameWorkspace, () => _ = commands.RenameAsync(item.Id), renameReason is null, renameReason),
            new MenuItem(WorkspaceTexts.DeleteWorkspace, () => _ = commands.DeleteAsync(item.Id), deleteReason is null, deleteReason, IsDestructive: true),
        ]);
    }
}
