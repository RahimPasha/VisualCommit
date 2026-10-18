using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// Every workspace flow of docs/phase-6-design.md 1.5 to 1.10, one per session
/// (<c>TabServices.WorkspaceCommands</c>), so that the welcome page, the dashboard and the palette
/// run the same code. Each command has its reason, null while it can run, and its run, which
/// checks again and does nothing when it cannot. While the store is read-only every command but
/// Open is disabled with <see cref="EditReason"/>.
/// <para>
/// A stub: work package WP-W builds the flows. Until then every command but Open is disabled
/// (with <see cref="EditReason"/>, or <see cref="NotBuiltText"/>) and does nothing, and Open asks
/// the host, whose <c>ShowWorkspace</c> is itself a stub until work package WP-D.
/// </para>
/// </summary>
public sealed class WorkspaceCommands
{
    /// <summary>The read-only reason of a newer version's file (1.5).</summary>
    public const string NewerVersionText = "workspaces.json was made by a newer version of VisualCommit";

    /// <summary>The read-only reason of a file that could not be read (1.5).</summary>
    public const string UnreadableText = "workspaces.json could not be read";

    /// <summary>The stub's reason for a command work package WP-W has not built yet; never shown before then.</summary>
    public const string NotBuiltText = "Workspaces are not available yet";

    private readonly TabServices _services;

    public WorkspaceCommands(TabServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <summary>Why no workspace can be edited this session (1.3, 1.5): the store's read-only cause in words; null while it can.</summary>
    public string? EditReason => _services.Workspaces.ReadOnlyCause switch
    {
        WorkspaceReadOnlyCause.NewerVersion => NewerVersionText,
        WorkspaceReadOnlyCause.Unreadable => UnreadableText,
        _ => null,
    };

    /// <summary>Why <c>New workspace…</c> cannot run (1.6), or null.</summary>
    public string? CreateReason => Disabled;

    /// <summary>
    /// <c>New workspace…</c> (1.6): the name dialog, then the workspace is created and opened from
    /// <paramref name="from"/> (2.2). Returns the new workspace's id, or null when nothing was created.
    /// </summary>
    public Task<string?> CreateAsync(RepoTabViewModel? from) => Task.FromResult<string?>(null);

    /// <summary>Opens the workspace <paramref name="workspaceId"/> from <paramref name="from"/> through the host (2.2): never disabled.</summary>
    public void Open(string workspaceId, RepoTabViewModel? from)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        _services.WorkspaceHost?.ShowWorkspace(workspaceId, from);
    }

    /// <summary>Why <c>Rename workspace…</c> cannot run (1.6), or null.</summary>
    public string? RenameReason(string workspaceId) => Disabled;

    /// <summary><c>Rename workspace…</c> (1.6): the name dialog, then the rename.</summary>
    public Task RenameAsync(string workspaceId) => Task.CompletedTask;

    /// <summary>Why <c>Delete workspace…</c> cannot run (1.6), or null.</summary>
    public string? DeleteReason(string workspaceId) => Disabled;

    /// <summary><c>Delete workspace…</c> (1.6): the confirmation, then the deletion; its dashboard closes.</summary>
    public Task DeleteAsync(string workspaceId) => Task.CompletedTask;

    /// <summary>Why <c>Add repository…</c> cannot run (1.7), or null.</summary>
    public string? AddRepositoryReason(string workspaceId) => Disabled;

    /// <summary>
    /// <c>Add repository…</c> (1.7): the folder dialog, then a working tree's top appended at once,
    /// or any other folder scanned and offered as 1.9's checklist. Returns the path appended (or
    /// already held) for a top, which the dashboard selects; null otherwise.
    /// </summary>
    public Task<string?> AddRepositoryAsync(string workspaceId) => Task.FromResult<string?>(null);

    /// <summary>Why <c>Add open tabs</c> cannot run (1.7): read-only, <c>No tab has a repository open</c>, <c>Every open repository is in this workspace</c>; or null.</summary>
    public string? AddOpenTabsReason(string workspaceId) => Disabled;

    /// <summary><c>Add open tabs</c> (1.7): appends the open tabs' working trees the workspace does not hold, in tab order.</summary>
    public void AddOpenTabs(string workspaceId)
    {
    }

    /// <summary>Why <c>Remove from workspace</c> cannot run on <paramref name="path"/> (1.10), or null; a bulk run holding the row adds its own reason (4.7).</summary>
    public string? RemoveReason(string workspaceId, string path) => Disabled;

    /// <summary><c>Remove from workspace</c> (1.10): at once, no question.</summary>
    public void Remove(string workspaceId, string path)
    {
    }

    /// <summary>Why <c>Move up</c> cannot run on <paramref name="path"/> (1.10): read-only, <c>It is the first repository</c>; or null.</summary>
    public string? MoveUpReason(string workspaceId, string path) => Disabled;

    /// <summary><c>Move up</c> (1.10, Alt+Up).</summary>
    public void MoveUp(string workspaceId, string path)
    {
    }

    /// <summary>Why <c>Move down</c> cannot run on <paramref name="path"/> (1.10): read-only, <c>It is the last repository</c>; or null.</summary>
    public string? MoveDownReason(string workspaceId, string path) => Disabled;

    /// <summary><c>Move down</c> (1.10, Alt+Down).</summary>
    public void MoveDown(string workspaceId, string path)
    {
    }

    /// <summary>Why <c>Locate…</c> cannot run on <paramref name="path"/> (1.10), or null.</summary>
    public string? LocateReason(string workspaceId, string path) => Disabled;

    /// <summary><c>Locate…</c> (1.10): the folder dialog, then the chosen folder's top in the saved path's place.</summary>
    public Task LocateAsync(string workspaceId, string path) => Task.CompletedTask;

    /// <summary>The stub's reason for every edit: the read-only one, else that it is not built yet.</summary>
    private string Disabled => EditReason ?? NotBuiltText;
}
