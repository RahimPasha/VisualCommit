namespace VisualCommit.Core.Workspaces;

/// <summary>
/// The changes to the workspaces (docs/phase-6-design.md 1.4), pure but for
/// <see cref="WorkspacePaths.Same"/>, which on Windows reads the file system for a path with a
/// <c>~</c> in it. Each returns the same object when it changes nothing, so that the store neither
/// saves nor raises <see cref="IWorkspaceStore.Changed"/>. Paths are matched by
/// <see cref="WorkspacePaths.Same"/> and kept as given (the working tree's top as git reported
/// it); a workspace's other fields and a repository's other fields are kept by every edit.
/// </summary>
public static class WorkspaceEdits
{
    /// <summary>
    /// Appends <c>Workspace(id, name, [])</c>, the name trimmed (1.1). Nothing changes when a
    /// workspace has <paramref name="id"/> already (an id is a new GUID, so only a repeated call has one).
    /// </summary>
    public static WorkspaceList Create(WorkspaceList list, string id, string name)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (list.Find(id) is not null)
        {
            return list;
        }

        return list with { Workspaces = [.. list.Workspaces, new Workspace(id, name.Trim(), [])] };
    }

    /// <summary>
    /// Renames the workspace <paramref name="id"/> to <paramref name="name"/>, trimmed; nothing
    /// changes when no workspace has the id or the trimmed name is its name already (compared
    /// ordinally: a change of case alone is a rename, 1.6).
    /// </summary>
    public static WorkspaceList Rename(WorkspaceList list, string id, string name)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var index = IndexOf(list, id);
        var trimmed = name.Trim();
        if (index < 0 || string.Equals(list.Workspaces[index].Name, trimmed, StringComparison.Ordinal))
        {
            return list;
        }

        return WithWorkspace(list, index, list.Workspaces[index] with { Name = trimmed });
    }

    /// <summary>Deletes the workspace <paramref name="id"/>; nothing on disk changes, and nothing at all when no workspace has the id.</summary>
    public static WorkspaceList Delete(WorkspaceList list, string id)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(id);
        var index = IndexOf(list, id);
        if (index < 0)
        {
            return list;
        }

        var workspaces = list.Workspaces.ToList();
        workspaces.RemoveAt(index);
        return list with { Workspaces = workspaces };
    }

    /// <summary>
    /// Appends to the workspace <paramref name="id"/>, in the order given, each path it does not
    /// hold yet (<see cref="WorkspacePaths.Same"/>), the ones appended before it included; a blank
    /// path is passed over.
    /// </summary>
    public static WorkspaceList Append(WorkspaceList list, string id, IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(paths);
        var index = IndexOf(list, id);
        if (index < 0)
        {
            return list;
        }

        var workspace = list.Workspaces[index];
        var repositories = workspace.Repositories.ToList();
        foreach (var path in paths)
        {
            if (!string.IsNullOrWhiteSpace(path) && IndexOfPath(repositories, path) < 0)
            {
                repositories.Add(new WorkspaceRepository(path));
            }
        }

        return repositories.Count == workspace.Repositories.Count
            ? list
            : WithWorkspace(list, index, workspace with { Repositories = repositories });
    }

    /// <summary>Removes <paramref name="path"/> from the workspace <paramref name="id"/>.</summary>
    public static WorkspaceList Remove(WorkspaceList list, string id, string path)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(path);
        var index = IndexOf(list, id);
        if (index < 0)
        {
            return list;
        }

        var workspace = list.Workspaces[index];
        var repositories = workspace.Repositories.ToList();
        var at = IndexOfPath(repositories, path);
        if (at < 0)
        {
            return list;
        }

        repositories.RemoveAt(at);
        return WithWorkspace(list, index, workspace with { Repositories = repositories });
    }

    /// <summary>
    /// Moves <paramref name="path"/> by <paramref name="delta"/> (−1 or +1) in the workspace
    /// <paramref name="id"/>; nothing at either end.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delta"/> is neither −1 nor +1.</exception>
    public static WorkspaceList Move(WorkspaceList list, string id, string path, int delta)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(path);
        if (delta is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(delta), delta, "A repository moves by one place, up (−1) or down (+1).");
        }

        var index = IndexOf(list, id);
        if (index < 0)
        {
            return list;
        }

        var workspace = list.Workspaces[index];
        var repositories = workspace.Repositories.ToList();
        var at = IndexOfPath(repositories, path);
        var to = at + delta;
        if (at < 0 || to < 0 || to >= repositories.Count)
        {
            return list;
        }

        (repositories[at], repositories[to]) = (repositories[to], repositories[at]);
        return WithWorkspace(list, index, workspace with { Repositories = repositories });
    }

    /// <summary>
    /// Puts <paramref name="newPath"/> in <paramref name="oldPath"/>'s place (Locate…, 1.10); the
    /// same list when the workspace holds <paramref name="newPath"/> already (also as
    /// <paramref name="oldPath"/> spelled another way) or does not hold <paramref name="oldPath"/>.
    /// </summary>
    public static WorkspaceList Replace(WorkspaceList list, string id, string oldPath, string newPath)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(oldPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);
        var index = IndexOf(list, id);
        if (index < 0)
        {
            return list;
        }

        var workspace = list.Workspaces[index];
        var repositories = workspace.Repositories.ToList();
        var at = IndexOfPath(repositories, oldPath);
        if (at < 0 || IndexOfPath(repositories, newPath) >= 0)
        {
            return list;
        }

        repositories[at] = repositories[at] with { Path = newPath };
        return WithWorkspace(list, index, workspace with { Repositories = repositories });
    }

    private static int IndexOf(WorkspaceList list, string id)
    {
        for (var i = 0; i < list.Workspaces.Count; i++)
        {
            if (string.Equals(list.Workspaces[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfPath(List<WorkspaceRepository> repositories, string path) =>
        repositories.FindIndex(repository => WorkspacePaths.Same(repository.Path, path));

    private static WorkspaceList WithWorkspace(WorkspaceList list, int index, Workspace workspace)
    {
        var workspaces = list.Workspaces.ToList();
        workspaces[index] = workspace;
        return list with { Workspaces = workspaces };
    }
}
