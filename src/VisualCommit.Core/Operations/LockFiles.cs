namespace VisualCommit.Core.Operations;

/// <summary>
/// Finds the lock files a stopped git left behind (phase 3 design 1.8): git takes
/// <c>&lt;name&gt;.lock</c> next to a ref, <c>packed-refs</c> or the index while it changes them,
/// and a git that is killed in the middle leaves them, after which every later git call that
/// needs them fails until they are removed.
/// </summary>
public static class LockFiles
{
    /// <summary>
    /// The <c>*.lock</c> files under <paramref name="gitFolders"/> (the repository's git folder,
    /// and the common one of a linked worktree) last written at or after <paramref name="since"/>,
    /// each listed once. Folders that cannot be read are passed over.
    /// </summary>
    public static IReadOnlyList<string> FindSince(IEnumerable<string> gitFolders, DateTimeOffset since)
    {
        ArgumentNullException.ThrowIfNull(gitFolders);
        var found = new SortedSet<string>(StringComparer.Ordinal);
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
        foreach (var folder in gitFolders.Distinct(StringComparer.Ordinal))
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(folder, "*.lock", options))
                {
                    try
                    {
                        // File times have a coarse resolution on some file systems: a second's margin.
                        if (File.GetLastWriteTimeUtc(file) >= since.UtcDateTime.AddSeconds(-1))
                        {
                            found.Add(Path.GetFullPath(file));
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // Gone meanwhile, or not readable: not ours to report.
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The folder went away or cannot be listed.
            }
        }

        return [.. found];
    }
}
