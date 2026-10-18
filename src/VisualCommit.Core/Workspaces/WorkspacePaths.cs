namespace VisualCommit.Core.Workspaces;

/// <summary>
/// What "the same repository folder" means (docs/phase-6-design.md 1.2): every match of a folder
/// (the workspace code, the dashboard's rows and pending tabs, the palette) goes through
/// <see cref="Same"/>, so that a hand-edited file, <c>C:\Users\RUNNER~1</c> and macOS's
/// <c>/var</c> match what git reports. Pure.
/// </summary>
public static class WorkspacePaths
{
    /// <summary>
    /// <c>Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))</c> for a fully qualified path;
    /// any other path (only a hand-edited file has one) as it is.
    /// Work package WP4 gives it its body (docs/phase-6-design.md 1.2).
    /// </summary>
    public static string Normalise(string path) =>
        throw new NotImplementedException("Work package WP4 builds WorkspacePaths.Normalise (docs/phase-6-design.md 1.2).");

    /// <summary>
    /// Whether <paramref name="a"/> and <paramref name="b"/> name one folder: their
    /// <see cref="Normalise"/>d paths compared with <c>WorktreeKey.Comparer</c> (ignoring case on
    /// Windows and macOS, exactly elsewhere). Work package WP4 gives it its body (1.2).
    /// </summary>
    public static bool Same(string a, string b) =>
        throw new NotImplementedException("Work package WP4 builds WorkspacePaths.Same (docs/phase-6-design.md 1.2).");
}
