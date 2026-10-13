namespace VisualCommit.Core.Git;

/// <summary>
/// How the app tells worktrees apart (docs/phase-5-design.md 2.3, 7.1): by the worktree's own git
/// folder (<see cref="IGitRepository.GitDirectory"/>, <c>.git/worktrees/&lt;n&gt;</c> for a linked
/// worktree), which is unique per worktree, unlike the folder's name. The key is the full path
/// without a trailing separator, compared as the platform compares paths, as the session's tabs
/// and recent repositories are.
/// </summary>
public static class WorktreeKey
{
    /// <summary>Compares keys ignoring case on Windows and macOS, exactly elsewhere.</summary>
    public static StringComparer Comparer { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>The key of the worktree whose git folder is <paramref name="gitDirectory"/>.</summary>
    public static string Of(string gitDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gitDirectory);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(gitDirectory));
    }
}
