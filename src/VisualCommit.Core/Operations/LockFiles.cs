namespace VisualCommit.Core.Operations;

/// <summary>
/// Finds the lock files a stopped git left behind (phase 3 design 1.8): git takes
/// <c>&lt;name&gt;.lock</c> next to a ref, <c>packed-refs</c> or the configuration while it changes
/// them, and a git that is killed in the middle leaves them, after which every later git call that
/// needs them fails until they are removed.
/// <para>
/// Only the locks that the cancelled calls can take are looked at: a fetch, a pull's fetch and a
/// push update refs (<c>refs/**</c>, and <c>packed-refs</c> when one is deleted), a shallow
/// clone's <c>shallow</c>, and the configuration (<c>push --set-upstream</c>). None of them takes
/// the index or HEAD, and none writes into another worktree's folder (<c>worktrees/*</c>), a
/// submodule's (<c>modules/*</c>) or <c>objects/</c>, so locks there, which other live git
/// processes hold, are never listed. A lock of those names that a live git holds lasts as long as
/// one ref update, so a lock is listed only when it is still there, unchanged, a moment after it
/// was first seen.
/// </para>
/// </summary>
public static class LockFiles
{
    /// <summary>How long a lock must stay unchanged to count as left behind.</summary>
    public static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(500);

    /// <summary>The lock files at the top of a git folder that a fetch or push can leave.</summary>
    private static readonly string[] TopLevelLocks = ["packed-refs.lock", "shallow.lock", "config.lock"];

    /// <summary>
    /// The lock files a stopped fetch or push can leave under <paramref name="gitFolders"/> (the
    /// repository's git folder, and the common one of a linked worktree), last written at or after
    /// <paramref name="since"/> and unchanged after <paramref name="settle"/> (null:
    /// <see cref="SettleTime"/>), each listed once. Folders that cannot be read are passed over.
    /// It blocks for the settle time when there is a candidate: call it off the UI thread.
    /// </summary>
    public static IReadOnlyList<string> FindSince(IEnumerable<string> gitFolders, DateTimeOffset since, TimeSpan? settle = null)
    {
        ArgumentNullException.ThrowIfNull(gitFolders);
        var candidates = new SortedDictionary<string, (DateTime Time, long Length)>(StringComparer.Ordinal);
        foreach (var folder in gitFolders.Distinct(StringComparer.Ordinal))
        {
            foreach (var file in Candidates(folder))
            {
                // File times have a coarse resolution on some file systems: a second's margin.
                if (Stamp(file) is { } stamp && stamp.Time >= since.UtcDateTime.AddSeconds(-1))
                {
                    candidates[Path.GetFullPath(file)] = stamp;
                }
            }
        }

        var wait = settle ?? SettleTime;
        if (candidates.Count == 0 || wait <= TimeSpan.Zero)
        {
            return [.. candidates.Keys];
        }

        Thread.Sleep(wait);
        return [.. candidates.Where(candidate => Stamp(candidate.Key) == candidate.Value).Select(candidate => candidate.Key)];
    }

    /// <summary>A file's last write time (UTC) and length, or null when it is gone or cannot be read.</summary>
    public static (DateTime Time, long Length)? Stamp(string file)
    {
        try
        {
            var info = new FileInfo(file);
            return info.Exists ? (info.LastWriteTimeUtc, info.Length) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Gone meanwhile, or not readable: not ours to report.
            return null;
        }
    }

    private static List<string> Candidates(string folder)
    {
        var found = new List<string>();
        try
        {
            if (!Directory.Exists(folder))
            {
                return found;
            }

            found.AddRange(TopLevelLocks.Select(name => Path.Combine(folder, name)).Where(File.Exists));
            var refs = Path.Combine(folder, "refs");
            if (Directory.Exists(refs))
            {
                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
                found.AddRange(Directory.EnumerateFiles(refs, "*.lock", options));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The folder went away or cannot be listed.
        }

        return found;
    }
}
