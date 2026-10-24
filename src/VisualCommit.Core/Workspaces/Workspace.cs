namespace VisualCommit.Core.Workspaces;

/// <summary>
/// Every workspace of the user (C1, D187 to D189), as <c>workspaces.json</c> keeps it
/// (docs/phase-6-design.md 1.1, 1.3): the app's own data, so creating, changing or deleting a
/// workspace changes nothing on disk but that file. Immutable: change it through
/// <see cref="IWorkspaceStore.Update"/> with the edits of <see cref="WorkspaceEdits"/>.
/// </summary>
public sealed record WorkspaceList
{
    /// <summary>The version of the file this version writes and fully understands.</summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The version of the file it was read from. A newer one is read as far as this version
    /// understands it and is never saved (1.3); 0 or a missing one reads as version 1.
    /// </summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>The workspaces in the order they were created; the welcome page lists them by name (13.1).</summary>
    public IReadOnlyList<Workspace> Workspaces { get; init; } = [];

    /// <summary>The workspace whose <see cref="Workspace.Id"/> is <paramref name="id"/> (compared ordinally), or null.</summary>
    public Workspace? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Workspaces.FirstOrDefault(workspace => string.Equals(workspace.Id, id, StringComparison.Ordinal));
    }
}

/// <summary>
/// A named, ordered list of repositories checked and updated together (C1, docs/phase-6-design.md 1.1).
/// </summary>
/// <param name="Id">
/// <c>Guid.NewGuid().ToString("N")</c>, never shown: the session names an open dashboard by it
/// (1.11), so a rename keeps that dashboard.
/// </param>
/// <param name="Name">
/// Trimmed, 1 to <see cref="MaxNameLength"/> characters, without control characters, unique
/// ignoring case on every platform (a name is no path).
/// </param>
/// <param name="Repositories">
/// The repositories in the user's order: added at the end, moved by Move up and Move down (1.10).
/// A repository may be in several workspaces, once in each.
/// </param>
public sealed record Workspace(string Id, string Name, IReadOnlyList<WorkspaceRepository> Repositories)
{
    /// <summary>The longest name a workspace may have.</summary>
    public const int MaxNameLength = 100;
}

/// <summary>
/// One repository of a workspace (docs/phase-6-design.md 1.1): an object, not a bare path, so that
/// a later version can add fields.
/// </summary>
/// <param name="Path">
/// The working tree's top as git reported it (<c>IGitRepository.WorkingDirectory</c>: absolute,
/// no trailing separator), as the tabs keep it. A hand-edited or copied file may spell it
/// otherwise; every comparison goes through <see cref="WorkspacePaths.Same"/> (1.2), and a read
/// never rewrites it.
/// </param>
public sealed record WorkspaceRepository(string Path);
