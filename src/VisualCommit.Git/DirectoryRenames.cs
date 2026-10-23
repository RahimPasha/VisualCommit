namespace VisualCommit.Git;

/// <summary>
/// Git's directory-rename detection as Keep ignored files' reads need it (docs/phase-6-design.md
/// 9.3 to 9.8; the phase's review): merge-ort, and so every merge, cherry-pick, revert, rebase and
/// stash apply of Git 2.36 and later, takes a directory that one side renamed whole as renamed for
/// the other side's files too, and writes a file the other side added under the old directory at
/// the path it would have under the new one ("CONFLICT (file location): dir/new.txt added in HEAD
/// inside a directory that was renamed in ren, suggesting it should perhaps be moved to
/// newdir/new.txt"), over an ignored file there, on Git 2.36, 2.55 and 2.56 alike (a stash apply on
/// 2.36 keeps it, writing <c>newdir/new.txt~Updated upstream</c>). No read without renames lists
/// that path. Pure: from one <c>git diff-tree -r -z -M --name-status &lt;ours&gt; &lt;theirs&gt;</c>.
/// <para>
/// Git counts renames against the merge base; the read compares the two sides directly, which
/// shows a rename either side made: one whose old path is on <c>ours</c> and new path on
/// <c>theirs</c> is, seen from <c>theirs</c>, its renaming of a directory (and <c>ours</c>' files
/// that <c>theirs</c> lacks move), and, seen from <c>ours</c>, the other way round (and
/// <c>theirs</c>' files that <c>ours</c> lacks move). Both are taken, the safe side. Git's own
/// rules are kept where the read can tell: a directory is renamed to where the most of its files
/// went (merge-ort's majority, <c>get_provisional_directory_renames</c>), counted at each level
/// whose trailing names agree (<c>update_dir_rename_counts</c>: <c>a/b/c/f</c> to <c>x/b/c/f</c>
/// renames <c>a/b/c</c>, <c>a/b</c> and <c>a</c>), into the root too (<c>dir/a</c> to <c>a</c>),
/// never the root itself; and a directory that still holds a file on the renaming side is not
/// renamed. Where the read cannot tell, every candidate is counted: a tie (git renames to none),
/// and a directory whose remaining files are the same on both sides, which the read does not show.
/// </para>
/// </summary>
internal static class DirectoryRenames
{
    /// <summary>One entry of <c>--name-status</c>: its status letter, its path, and for a rename (<c>R</c>) the path on the other side.</summary>
    internal readonly record struct Entry(char Status, string Path, string? NewPath);

    /// <summary>
    /// <c>git diff-tree -r -z -M --name-status</c>'s output: <c>&lt;status&gt;\0&lt;path&gt;\0</c>, and
    /// for a rename or copy <c>R&lt;score&gt;\0&lt;old&gt;\0&lt;new&gt;\0</c>.
    /// </summary>
    internal static IReadOnlyList<Entry> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var fields = output.Split('\0');
        var entries = new List<Entry>();
        var i = 0;
        while (i < fields.Length)
        {
            var status = fields[i];
            if (status.Length == 0)
            {
                i++;
                continue;
            }

            if (status[0] is 'R' or 'C' && i + 2 < fields.Length)
            {
                entries.Add(new Entry(status[0], fields[i + 1], fields[i + 2]));
                i += 3;
            }
            else if (i + 1 < fields.Length)
            {
                entries.Add(new Entry(status[0], fields[i + 1], null));
                i += 2;
            }
            else
            {
                break;
            }
        }

        return entries;
    }

    /// <summary>
    /// The paths git may write through a directory rename between <c>ours</c> and <c>theirs</c>,
    /// the two sides <paramref name="entries"/> compare, in both directions (see the class), each
    /// once, in byte order; none without a rename.
    /// </summary>
    internal static IReadOnlyList<string> Moved(IReadOnlyList<Entry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var renames = entries.Where(entry => entry is { Status: 'R', NewPath: not null }).ToList();
        if (renames.Count == 0)
        {
            return [];
        }

        var oursHas = new List<string>();
        var theirsHas = new List<string>();
        var oursOnly = new List<string>();
        var theirsOnly = new List<string>();
        foreach (var entry in entries)
        {
            switch (entry.Status)
            {
                case 'A':
                    theirsHas.Add(entry.Path);
                    theirsOnly.Add(entry.Path);
                    break;
                case 'D':
                    oursHas.Add(entry.Path);
                    oursOnly.Add(entry.Path);
                    break;
                case 'R' or 'C' when entry.NewPath is { } newPath:
                    oursHas.Add(entry.Path);
                    theirsHas.Add(newPath);
                    break;
                default:
                    oursHas.Add(entry.Path);
                    theirsHas.Add(entry.Path);
                    break;
            }
        }

        var moved = new SortedSet<string>(StringComparer.Ordinal);

        // theirs renamed a directory of ours (old path on ours): ours' files theirs lacks move.
        AddMoved(renames.Select(rename => (rename.Path, rename.NewPath!)), theirsHas, oursOnly, moved);

        // ours renamed a directory of theirs: theirs' files ours lacks move.
        AddMoved(renames.Select(rename => (rename.NewPath!, rename.Path)), oursHas, theirsOnly, moved);
        return [.. moved];
    }

    /// <summary>
    /// The directory renames <paramref name="pairs"/> (old path, new path) show on the side that
    /// still has <paramref name="renamingSideHas"/>, applied to <paramref name="otherSideOnly"/>, the
    /// other side's files the renaming side lacks: for each directory of such a file that was
    /// renamed, the file's path under each target.
    /// </summary>
    private static void AddMoved(IEnumerable<(string Old, string New)> pairs, IReadOnlyList<string> renamingSideHas, IReadOnlyList<string> otherSideOnly, SortedSet<string> moved)
    {
        var counts = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (var (oldPath, newPath) in pairs)
        {
            foreach (var (oldDirectory, newDirectory) in Levels(oldPath, newPath))
            {
                if (!counts.TryGetValue(oldDirectory, out var targets))
                {
                    counts[oldDirectory] = targets = new Dictionary<string, int>(StringComparer.Ordinal);
                }

                targets[newDirectory] = targets.GetValueOrDefault(newDirectory) + 1;
            }
        }

        // A directory that still holds a file on the renaming side is not renamed (merge-ort).
        var stillThere = renamingSideHas.SelectMany(Ancestors).ToHashSet(StringComparer.Ordinal);
        var renamed = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (oldDirectory, targets) in counts)
        {
            if (stillThere.Contains(oldDirectory))
            {
                continue;
            }

            var most = targets.Values.Max();
            renamed[oldDirectory] = [.. targets.Where(target => target.Value == most).Select(target => target.Key)];
        }

        if (renamed.Count == 0)
        {
            return;
        }

        foreach (var path in otherSideOnly)
        {
            foreach (var folder in Ancestors(path))
            {
                if (!renamed.TryGetValue(folder, out var targets))
                {
                    continue;
                }

                foreach (var target in targets)
                {
                    moved.Add(target.Length == 0 ? path[(folder.Length + 1)..] : target + path[folder.Length..]);
                }
            }
        }
    }

    /// <summary>
    /// The directory renames one file's rename shows (merge-ort's <c>update_dir_rename_counts</c>):
    /// its folder to its new folder, then each pair of parents while the names stripped off agree;
    /// none when the old path is at the root, which is never renamed, or when the folder stays.
    /// The new folder may be the root (<c>""</c>).
    /// </summary>
    internal static IEnumerable<(string Old, string New)> Levels(string oldPath, string newPath)
    {
        var oldDirectory = Parent(oldPath);
        var newDirectory = Parent(newPath);
        if (oldDirectory.Length == 0 || string.Equals(oldDirectory, newDirectory, StringComparison.Ordinal))
        {
            yield break;
        }

        yield return (oldDirectory, newDirectory);
        while (newDirectory.Length > 0
            && string.Equals(LastName(oldDirectory), LastName(newDirectory), StringComparison.Ordinal))
        {
            oldDirectory = Parent(oldDirectory);
            newDirectory = Parent(newDirectory);
            if (oldDirectory.Length == 0)
            {
                yield break;
            }

            yield return (oldDirectory, newDirectory);
        }
    }

    /// <summary>The folders a path lies in, deepest first: <c>a/b</c> and <c>a</c> for <c>a/b/c.txt</c>.</summary>
    private static IEnumerable<string> Ancestors(string path)
    {
        for (var slash = path.LastIndexOf('/'); slash > 0; slash = path.LastIndexOf('/', slash - 1))
        {
            yield return path[..slash];
        }
    }

    /// <summary>The folder a path lies in, <c>""</c> for one at the root.</summary>
    private static string Parent(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash > 0 ? path[..slash] : string.Empty;
    }

    private static string LastName(string path) => path[(path.LastIndexOf('/') + 1)..];
}
