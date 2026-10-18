namespace VisualCommit.Core.Workspaces;

/// <summary>
/// The changes to the workspaces (docs/phase-6-design.md 1.4), pure. Each returns the same object
/// when it changes nothing, so that the store neither saves nor raises <see cref="IWorkspaceStore.Changed"/>.
/// Paths are matched by <see cref="WorkspacePaths.Same"/>. Work package WP4 gives them their bodies.
/// </summary>
public static class WorkspaceEdits
{
    private const string Builder = "Work package WP4 builds WorkspaceEdits (docs/phase-6-design.md 1.4).";

    /// <summary>Appends <c>Workspace(id, name, [])</c>.</summary>
    public static WorkspaceList Create(WorkspaceList list, string id, string name) => throw new NotImplementedException(Builder);

    /// <summary>Renames the workspace <paramref name="id"/>.</summary>
    public static WorkspaceList Rename(WorkspaceList list, string id, string name) => throw new NotImplementedException(Builder);

    /// <summary>Deletes the workspace <paramref name="id"/>; nothing on disk changes.</summary>
    public static WorkspaceList Delete(WorkspaceList list, string id) => throw new NotImplementedException(Builder);

    /// <summary>Appends to the workspace <paramref name="id"/>, in the order given, each path it does not hold yet.</summary>
    public static WorkspaceList Append(WorkspaceList list, string id, IReadOnlyList<string> paths) => throw new NotImplementedException(Builder);

    /// <summary>Removes <paramref name="path"/> from the workspace <paramref name="id"/>.</summary>
    public static WorkspaceList Remove(WorkspaceList list, string id, string path) => throw new NotImplementedException(Builder);

    /// <summary>Moves <paramref name="path"/> by <paramref name="delta"/> (−1 or +1) in the workspace <paramref name="id"/>; nothing at either end.</summary>
    public static WorkspaceList Move(WorkspaceList list, string id, string path, int delta) => throw new NotImplementedException(Builder);

    /// <summary>
    /// Puts <paramref name="newPath"/> in <paramref name="oldPath"/>'s place (Locate…, 1.10); the
    /// same list when the workspace holds <paramref name="newPath"/> already.
    /// </summary>
    public static WorkspaceList Replace(WorkspaceList list, string id, string oldPath, string newPath) => throw new NotImplementedException(Builder);
}
