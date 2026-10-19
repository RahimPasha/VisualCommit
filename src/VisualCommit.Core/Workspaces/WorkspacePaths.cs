using VisualCommit.Core.Git;

namespace VisualCommit.Core.Workspaces;

/// <summary>
/// What "the same repository folder" means (docs/phase-6-design.md 1.2): every match of a folder
/// (the workspace code, the dashboard's rows and pending tabs, the palette) goes through
/// <see cref="Same"/>, so that a hand-edited file, <c>C:\Users\RUNNER~1</c> and macOS's
/// <c>/var</c> match what git reports. Pure.
/// <para>
/// It compares texts, after <see cref="Path.GetFullPath(string)"/>, which on Windows also expands
/// the short (8.3) names of folders that exist: a junction or a symbolic link (macOS's
/// <c>/var</c>) is another spelling of a folder that only git can see through, which
/// <c>IGitRepository.OpenedAtTopAsync</c> and the open rows' working directories cover (1.2's two rules).
/// </para>
/// </summary>
public static class WorkspacePaths
{
    /// <summary>
    /// <c>Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))</c> for a fully qualified path;
    /// any other path (only a hand-edited file has one) as it is, and so is a path that
    /// <see cref="Path.GetFullPath(string)"/> refuses (a hand-edited one with a null character).
    /// </summary>
    public static string Normalise(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!Path.IsPathFullyQualified(path))
        {
            return path;
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    /// <summary>
    /// Whether <paramref name="a"/> and <paramref name="b"/> name one folder: their
    /// <see cref="Normalise"/>d paths compared with <see cref="WorktreeKey.Comparer"/> (ignoring
    /// case on Windows and macOS, exactly elsewhere), as the recent list and the graph filters do.
    /// </summary>
    public static bool Same(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return WorktreeKey.Comparer.Equals(Normalise(a), Normalise(b));
    }
}
