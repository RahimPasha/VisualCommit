namespace VisualCommit.Core.Git;

/// <summary>
/// The two rules by which a remote-tracking ref's name (<c>origin/feature/x</c>, without
/// <c>refs/remotes/</c>) is told apart by its remote's name where the fetch lines are not asked or
/// do not tell (docs/phase-6-design.md 10.1 to 10.4): what the name says after <c>&lt;remote&gt;/</c>
/// (<see cref="WithoutRemote"/>), and which configured remote it starts with, remote names holding
/// slashes as they may (<c>team/fork</c>, 0.5; <see cref="RemotesOf"/>, <see cref="LongestRemoteOf"/>,
/// <see cref="RemoteOf"/>). Every place that splits a name so calls these. Pure.
/// </summary>
public static class RemoteBranchNames
{
    /// <summary>
    /// <paramref name="name"/> less <c>&lt;remote&gt;/</c> (<c>origin/pr/1</c> → <c>pr/1</c>), or the
    /// whole name when it does not start so or <paramref name="remote"/> is null: phase 5's name of
    /// the branch on the remote, and of the local branch it checks out into.
    /// </summary>
    public static string WithoutRemote(string name, string? remote)
    {
        ArgumentNullException.ThrowIfNull(name);
        return remote is not null && StartsWithRemote(name, remote) ? name[(remote.Length + 1)..] : name;
    }

    /// <summary>
    /// Every one of <paramref name="remotes"/> that <paramref name="name"/> starts with, followed by
    /// a slash, longest first, each once: <c>team/fork</c> and <c>team</c> for <c>team/fork/main</c>.
    /// More than one means the name alone does not tell its remote.
    /// </summary>
    public static IReadOnlyList<string> RemotesOf(string name, IEnumerable<string> remotes)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(remotes);
        return [.. remotes
            .Where(remote => remote.Length > 0 && StartsWithRemote(name, remote))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(remote => remote.Length)];
    }

    /// <summary>The longest of <paramref name="remotes"/> that <paramref name="name"/> starts with, followed by a slash; null when none does.</summary>
    public static string? LongestRemoteOf(string name, IEnumerable<string> remotes) =>
        RemotesOf(name, remotes).FirstOrDefault();

    /// <summary>
    /// <see cref="LongestRemoteOf"/>, or, for a name no configured remote starts (its remote was
    /// removed, renamed or never configured), its first part before a slash, the best guess, or
    /// the whole name when it has none.
    /// </summary>
    public static string RemoteOf(string name, IEnumerable<string> remotes)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (LongestRemoteOf(name, remotes) is { } remote)
        {
            return remote;
        }

        var slash = name.IndexOf('/', StringComparison.Ordinal);
        return slash > 0 ? name[..slash] : name;
    }

    private static bool StartsWithRemote(string name, string remote) =>
        name.Length > remote.Length && name[remote.Length] == '/' && name.StartsWith(remote, StringComparison.Ordinal);
}
