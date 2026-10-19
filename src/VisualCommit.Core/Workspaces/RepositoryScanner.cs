using System.Diagnostics;

namespace VisualCommit.Core.Workspaces;

/// <summary>How far <see cref="RepositoryScanner.Scan"/> searches (docs/phase-6-design.md 1.8).</summary>
/// <param name="MaxDepth">The deepest folder looked at, the chosen folder being depth 0: a folder at this depth is a candidate or nothing, never opened.</param>
/// <param name="MaxFolders">The most folders taken from the queue; the search stops there and keeps what it found.</param>
public sealed record ScanOptions(int MaxDepth = 4, int MaxFolders = 10_000);

/// <summary>How far a search has come: the folders taken from the queue and the candidates found.</summary>
public sealed record ScanProgress(int FoldersSearched, int Found);

/// <summary>What a search found.</summary>
/// <param name="Candidates">The folders with a <c>.git</c> folder or file, breadth-first, each folder's folders in ordinal order of their names.</param>
/// <param name="FoldersSearched">The folders taken from the queue, the chosen one included.</param>
/// <param name="StoppedAtLimit">The search stopped at <see cref="ScanOptions.MaxFolders"/> with folders still to look at.</param>
public sealed record ScanResult(IReadOnlyList<string> Candidates, int FoldersSearched, bool StoppedAtLimit);

/// <summary>One folder of a folder, as <see cref="IScanFileSystem.ListFolders"/> lists it.</summary>
/// <param name="Name">Its name.</param>
/// <param name="IsLink">
/// A symbolic link or a junction: the reparse-point attribute with a link target
/// (<see cref="FileSystemInfo.LinkTarget"/> not null). Other reparse points, such as every
/// OneDrive folder, are not links (docs/phase-6-design.md 0.8).
/// </param>
/// <param name="IsHidden">On Windows, a Hidden or System folder; false elsewhere (1.8 leaves those out on Windows only).</param>
public sealed record ScanFolder(string Name, bool IsLink, bool IsHidden);

/// <summary>What the scan reads of the file system, so that tests can fake an unreadable folder or 12,000 of them.</summary>
public interface IScanFileSystem
{
    /// <summary>
    /// The folders directly inside <paramref name="path"/>. Throws <see cref="UnauthorizedAccessException"/>
    /// or <see cref="IOException"/> (<see cref="DirectoryNotFoundException"/> among them) when it
    /// cannot be listed.
    /// </summary>
    IReadOnlyList<ScanFolder> ListFolders(string path);

    /// <summary>Whether <paramref name="path"/> has a <c>.git</c> entry, a folder or a file. Never throws.</summary>
    bool HasGitEntry(string path);
}

/// <summary>The real file system for <see cref="RepositoryScanner"/>.</summary>
public sealed class PhysicalScanFileSystem : IScanFileSystem
{
    private static readonly EnumerationOptions Listing = new()
    {
        // The scanner decides about hidden and system folders itself (on Windows only); the
        // default options would skip them everywhere.
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
    };

    public static PhysicalScanFileSystem Instance { get; } = new();

    public IReadOnlyList<ScanFolder> ListFolders(string path)
    {
        var folders = new List<ScanFolder>();
        foreach (var folder in new DirectoryInfo(path).EnumerateDirectories("*", Listing))
        {
            var attributes = folder.Attributes;
            folders.Add(new ScanFolder(
                folder.Name,
                attributes.HasFlag(FileAttributes.ReparsePoint) && LinkTargetOf(folder) is not null,
                OperatingSystem.IsWindows() && (attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0));
        }

        return folders;
    }

    public bool HasGitEntry(string path) => Path.Exists(Path.Combine(path, ".git"));

    /// <summary>A link's target, or null; a reparse point whose target cannot be read counts as a link, so that it is not followed.</summary>
    private static string? LinkTargetOf(DirectoryInfo folder)
    {
        try
        {
            return folder.LinkTarget;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return folder.FullName;
        }
    }
}

/// <summary>
/// Looks for repositories under a folder (docs/phase-6-design.md 1.8, D204): only the file system,
/// no git, on the thread pool (the "Add repositories" dialog runs it in <c>Task.Run</c>); git
/// confirms each candidate afterwards (1.8's "Confirming").
/// <list type="bullet">
/// <item>Breadth-first from the chosen folder (depth 0, never a candidate) to <see cref="ScanOptions.MaxDepth"/>:
/// a folder with a <c>.git</c> folder or file is a candidate and is not opened (submodules and nested
/// repositories are left out); a folder at the deepest depth is not opened.</item>
/// <item>Left out below the chosen folder: <see cref="LeftOutNames"/> (ignoring case) and names starting
/// with <c>.</c>; links (loops, a repository twice), while other reparse points are followed; on
/// Windows, Hidden or System folders; folders whose listing fails, quietly.</item>
/// <item>At most <see cref="ScanOptions.MaxFolders"/> folders, counted as taken from the queue.
/// Progress at most every 100 ms and at the end; the token stops the search at the next folder,
/// with <see cref="OperationCanceledException"/>.</item>
/// </list>
/// </summary>
public static class RepositoryScanner
{
    /// <summary>The most often a search reports its progress.</summary>
    public static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>The names of folders that hold tools' output or dependencies, left out ignoring case.</summary>
    public static IReadOnlyList<string> LeftOutNames { get; } =
        ["node_modules", "bower_components", "vendor", "packages", "bin", "obj", "target", "build", "dist", "out"];

    private static readonly HashSet<string> LeftOut = new(LeftOutNames, StringComparer.OrdinalIgnoreCase);

    /// <summary>Searches <paramref name="root"/> by the rules above.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static ScanResult Scan(
        string root,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken,
        ScanOptions? options = null,
        IScanFileSystem? fileSystem = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        options ??= new ScanOptions();
        fileSystem ??= PhysicalScanFileSystem.Instance;
        var candidates = new List<string>();
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));
        var searched = 0;
        var stoppedAtLimit = false;
        var sinceReport = Stopwatch.StartNew();
        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (searched >= options.MaxFolders)
            {
                stoppedAtLimit = true;
                break;
            }

            var (path, depth) = queue.Dequeue();
            searched++;
            if (depth > 0 && fileSystem.HasGitEntry(path))
            {
                candidates.Add(path);
            }
            else if (depth < options.MaxDepth)
            {
                foreach (var folder in ListQuietly(fileSystem, path))
                {
                    if (!IsLeftOut(folder))
                    {
                        queue.Enqueue((Path.Combine(path, folder.Name), depth + 1));
                    }
                }
            }

            if (progress is not null && sinceReport.Elapsed >= ProgressInterval)
            {
                progress.Report(new ScanProgress(searched, candidates.Count));
                sinceReport.Restart();
            }
        }

        progress?.Report(new ScanProgress(searched, candidates.Count));
        return new ScanResult(candidates, searched, stoppedAtLimit);
    }

    private static bool IsLeftOut(ScanFolder folder) =>
        folder.Name.StartsWith('.') || LeftOut.Contains(folder.Name) || folder.IsLink || folder.IsHidden;

    /// <summary>The folder's folders in ordinal order of their names, or none when it cannot be listed.</summary>
    private static IEnumerable<ScanFolder> ListQuietly(IScanFileSystem fileSystem, string path)
    {
        try
        {
            return fileSystem.ListFolders(path).OrderBy(folder => folder.Name, StringComparer.Ordinal).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }
}
