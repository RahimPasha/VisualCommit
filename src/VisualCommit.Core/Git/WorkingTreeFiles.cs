namespace VisualCommit.Core.Git;

/// <summary>
/// What stands at a working-tree path, as git's checkout sees it: a symbolic link is a file to
/// git, also one to a folder, and is not followed (docs/phase-5-design.md 15.9).
/// </summary>
public static class WorkingTreeFiles
{
    private static readonly EnumerationOptions EveryEntry = new() { AttributesToSkip = 0 };

    /// <summary>
    /// Whether something other than a file stands in the way of a file at <paramref name="path"/>:
    /// a folder at the path that holds files, or a file or link where one of the path's folders
    /// should be. Git's restore writes with force and deletes either without a word, with every
    /// file in the folder; an empty folder is replaced and loses nothing. A folder that cannot be
    /// read counts as holding files.
    /// </summary>
    /// <param name="root">The working tree's folder.</param>
    /// <param name="path">A repository path, with <c>/</c>.</param>
    public static bool IsInTheWayOfFile(string root, string path)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(path);
        var full = FullPath(root, path);
        if (!IsFileOrLink(full) && Directory.Exists(full) && HoldsFiles(full))
        {
            return true;
        }

        for (var slash = path.IndexOf('/', StringComparison.Ordinal); slash > 0; slash = path.IndexOf('/', slash + 1))
        {
            if (IsFileOrLink(FullPath(root, path[..slash])))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a folder holds a file or a link anywhere below it; folders alone are nothing to lose.</summary>
    private static bool HoldsFiles(string folder)
    {
        try
        {
            var folders = new Stack<DirectoryInfo>([new DirectoryInfo(folder)]);
            while (folders.TryPop(out var current))
            {
                foreach (var entry in current.EnumerateFileSystemInfos("*", EveryEntry))
                {
                    if (entry is DirectoryInfo inner && entry.LinkTarget is null)
                    {
                        folders.Push(inner);
                    }
                    else
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>Whether a path is a file or a link (to a folder too, or broken), which git takes as a file.</summary>
    private static bool IsFileOrLink(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            return true;
        }

        try
        {
            return new FileInfo(fullPath).LinkTarget is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string FullPath(string root, string path) => Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
}
