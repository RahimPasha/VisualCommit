using System.Globalization;
using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// Phase 3's backup refs and the snapshots that join them (design 2.0, 2.11, 2.15, Q3, D83):
/// one timestamp per operation, every ref in one <c>git update-ref --stdin</c> transaction, under
/// <c>refs/visualcommit/backup/&lt;kind&gt;-&lt;UTC time&gt;[-&lt;n&gt;][/&lt;name&gt;]</c>.
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>Where every backup ref of phase 3 lives (D67's discard snapshots included).</summary>
    public const string BackupRefPrefix = "refs/visualcommit/backup/";

    /// <summary>How many names a batch tries before it gives up: the first, then the suffixes -2 to -20.</summary>
    private const int BackupAttempts = 20;

    /// <summary>
    /// The clock of the backup refs' timestamps. Tests set a fixed one to make two batches
    /// collide (2.0's retry naming).
    /// </summary>
    internal TimeProvider BackupClock { get; set; } = TimeProvider.System;

    public async Task<BackupResult> SaveBackupRefsAsync(BackupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateBackupRequest(request);

        // One timestamp for the whole operation; a retry puts its suffix on the kind-and-time
        // part, never on the whole ref, so a "-changes" ref keeps its pair's name and a name with
        // slashes cannot clash with a folder of the same name (2.0).
        var time = BackupClock.GetUtcNow().ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        for (var attempt = 1; ; attempt++)
        {
            var stamp = attempt == 1 ? time : string.Create(CultureInfo.InvariantCulture, $"{time}-{attempt}");
            var stem = $"{BackupRefPrefix}{request.Kind}-{stamp}";
            var lines = request.Entries
                .Select(entry => (Ref: entry.Name is null ? stem : $"{stem}/{entry.Name}", entry.Target))
                .ToList();
            if (request.ChangesCommit is { } changes)
            {
                lines.Add(($"{stem}-changes", changes));
            }

            // "create" refuses a ref that exists; the transaction writes all of them or none.
            var input = new StringBuilder();
            foreach (var (name, target) in lines)
            {
                input.Append("create ").Append(name).Append(' ').Append(target).Append('\n');
            }

            var command = new GitCommand("update-ref", "--stdin") { WorkingDirectory = WorkingDirectory, StandardInput = input.ToString() };
            var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                var refs = lines.Select(line => line.Ref).ToList();
                _log.Info($"Saved {Count(refs.Count, "backup ref")}: {string.Join(", ", refs)}.");
                return new BackupResult(stamp, refs);
            }

            // A name taken (by a batch of the same millisecond, or a folder of that name) is
            // retried under the next suffix; anything else is git's to explain. The clash is
            // looked up rather than read from git's message, whose wording may change.
            if (attempt >= BackupAttempts || !await AnyBackupNameTakenAsync(lines.Select(line => line.Ref), cancellationToken).ConfigureAwait(false))
            {
                result.EnsureSuccess(command);
            }
        }
    }

    /// <summary>
    /// Whether a ref already exists with one of <paramref name="names"/>, or as a folder of one of
    /// them, or below one of them: each makes <c>create</c> fail. On Windows and macOS names that
    /// differ only in case clash too, where refs are files.
    /// </summary>
    private async Task<bool> AnyBackupNameTakenAsync(IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var existing = SplitLines(await ReadOutputAsync(cancellationToken, "for-each-ref", "--format=%(refname)", BackupRefPrefix).ConfigureAwait(false));
        var comparison = RefNameRules.NamesIgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return names.Any(name => existing.Any(taken =>
            string.Equals(taken, name, comparison)
            || taken.StartsWith(name + "/", comparison)
            || name.StartsWith(taken + "/", comparison)));
    }

    private static void ValidateBackupRequest(BackupRequest request)
    {
        if (string.IsNullOrEmpty(request.Kind) || !request.Kind.All(c => c is (>= 'a' and <= 'z') or '-'))
        {
            throw new ArgumentException($"A backup's kind is a word such as 'reset', not '{request.Kind}'.", nameof(request));
        }

        var unnamed = request.Entries.Count(entry => entry.Name is null);
        if (request.Entries.Count == 0 && request.ChangesCommit is null)
        {
            throw new ArgumentException("A backup needs at least one ref.", nameof(request));
        }

        if (unnamed > 1 || (unnamed == 1 && request.Entries.Count > 1))
        {
            // <kind>-<time> and <kind>-<time>/<name> cannot both exist: a ref is not a folder.
            throw new ArgumentException("A backup has one ref without a name, or only named refs.", nameof(request));
        }

        if (request.Entries.Select(entry => entry.Name).Distinct(StringComparer.Ordinal).Count() != request.Entries.Count)
        {
            throw new ArgumentException("A backup names each ref once.", nameof(request));
        }

        foreach (var entry in request.Entries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Target, nameof(request));
        }
    }

    public async Task<string?> SnapshotForHardResetAsync(string target, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        var status = await ReadPlainStatusAsync(cancellationToken).ConfigureAwait(false);

        // What a hard reset loses (2.11): every staged or unstaged tracked file, every file staged
        // as new, and every file in the working tree that the index does not track but that the
        // reset overwrites or deletes without a word, an untracked file and an ignored one alike
        // (the status does not list ignored files): one at a path the target tracks, every file
        // in a folder where the target has a file, and a file where the target has a folder.
        // Files the reset leaves alone are not saved.
        var paths = ChangedTrackedPaths(status);
        var targetTask = ReadOutputAsync(cancellationToken, "ls-tree", "-r", "-z", target, "--");
        var indexTask = ReadOutputAsync(cancellationToken, "ls-files", "-z");
        await Task.WhenAll(targetTask, indexTask).ConfigureAwait(false);
        var inIndex = SplitNul(await indexTask.ConfigureAwait(false)).ToHashSet(StringComparer.Ordinal);
        var collisions = new ResetCollisions(this, target, inIndex, paths);
        foreach (var entry in SplitNul(await targetTask.ConfigureAwait(false)))
        {
            // "<mode> <type> <object>\t<path>"; a submodule's entry has the type "commit".
            var tab = entry.IndexOf('\t', StringComparison.Ordinal);
            collisions.Add(entry[(tab + 1)..], entry.AsSpan(0, tab).Contains(" commit ", StringComparison.Ordinal));
        }

        if (paths.Count == 0)
        {
            return null;
        }

        var head = await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false);

        // The real index's own tree as a second parent, so a file whose staged content differs
        // from its working-tree content keeps both. An index with a conflict has no tree.
        string? indexCommit = null;
        if (!status.Unstaged.Any(file => file.Kind == FileChangeKind.Conflicted))
        {
            indexCommit = await CommitIndexAsync(head, cancellationToken).ConfigureAwait(false);
        }

        var shortTarget = target.Length > 7 ? target[..7] : target;
        return await CommitSnapshotAsync(
            paths,
            head,
            indexCommit,
            $"VisualCommit: snapshot before a hard reset to {shortTarget}",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds what <c>git reset --hard &lt;target&gt;</c> deletes or overwrites in the working tree
    /// that the index does not track, path by path of the target (2.11). Git takes a path's place
    /// whatever is there: a file at the path, everything inside a folder at the path (ignored
    /// files and nested folders too), and a file where the path has a folder. Only paths that
    /// the index does not track, or that are changed, can meet anything of the kind: a tracked
    /// file that is unchanged is in the working tree as a file, below folders.
    /// </summary>
    private sealed class ResetCollisions(GitRepository repository, string target, HashSet<string> inIndex, SortedSet<string> paths)
    {
        private static readonly EnumerationOptions AllEntries = new() { AttributesToSkip = 0, IgnoreInaccessible = false, RecurseSubdirectories = false };

        private readonly HashSet<string> _changed = new(paths, StringComparer.Ordinal);

        /// <summary>Whether a folder of a target path is a folder (or missing) in the working tree, by folder.</summary>
        private readonly Dictionary<string, bool> _folders = new(StringComparer.Ordinal);

        public void Add(string path, bool isSubmodule)
        {
            if (inIndex.Contains(path) && !_changed.Contains(path))
            {
                return;
            }

            // A file (or link) where the path has a folder is deleted, and what lies beyond it
            // is not this path's: git does not follow the link.
            var slash = path.LastIndexOf('/');
            if (slash > 0 && !IsFolderOrMissing(path[..slash]))
            {
                return;
            }

            var full = repository.WorkingTreePath(path);
            if (IsFileOrLink(full))
            {
                AddUntracked(path);
            }
            else if (Directory.Exists(full) && !isSubmodule)
            {
                // A submodule's folder is left as it is; any other folder gives way to the file.
                AddFolder(path, full);
            }
        }

        private bool IsFolderOrMissing(string folder)
        {
            if (_folders.TryGetValue(folder, out var known))
            {
                return known;
            }

            var slash = folder.LastIndexOf('/');
            var clear = slash <= 0 || IsFolderOrMissing(folder[..slash]);
            if (clear && IsFileOrLink(repository.WorkingTreePath(folder)))
            {
                AddUntracked(folder);
                clear = false;
            }

            _folders[folder] = clear;
            return clear;
        }

        private void AddFolder(string relative, string full)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(full, "*", AllEntries))
            {
                var name = Path.GetFileName(entry);
                if (string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase))
                {
                    // Git deletes a repository nested here with its history, which no snapshot
                    // commit can hold: the reset does not start.
                    var shortTarget = target.Length > 7 ? target[..7] : target;
                    throw new IOException(
                        $"A hard reset to {shortTarget} would delete the repository in '{relative}', which a backup cannot keep. Move it out of the working tree first.");
                }

                if (IsFileOrLink(entry))
                {
                    AddUntracked($"{relative}/{name}");
                }
                else
                {
                    AddFolder($"{relative}/{name}", entry);
                }
            }
        }

        private void AddUntracked(string path)
        {
            if (!inIndex.Contains(path))
            {
                paths.Add(path);
            }
        }
    }

    /// <summary>
    /// Whether a working-tree path is a file or a link (to a folder too, or broken), which git
    /// takes as a file: not a real folder, and not missing.
    /// </summary>
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

    public async Task<string?> SnapshotTrackedChangesAsync(CancellationToken cancellationToken = default)
    {
        var status = await ReadPlainStatusAsync(cancellationToken).ConfigureAwait(false);
        var paths = ChangedTrackedPaths(status);
        if (paths.Count == 0)
        {
            return null;
        }

        var head = await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false);
        return await CommitSnapshotAsync(paths, head, null, "VisualCommit: snapshot before an abort", cancellationToken).ConfigureAwait(false);
    }

    private static string[] SplitNul(string output) => output.Split('\0', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The status without the operation in progress: the snapshots need only the files.</summary>
    private async Task<WorkingTreeStatus> ReadPlainStatusAsync(CancellationToken cancellationToken) =>
        ParseStatus(await ReadOutputAsync(cancellationToken, "status", "--porcelain=v2", "-z", "--untracked-files=all", "--renames")
            .ConfigureAwait(false));

    /// <summary>
    /// Every staged file (a rename's old path too), and every unstaged file that git tracks,
    /// conflicted ones included, in byte order.
    /// </summary>
    private static SortedSet<string> ChangedTrackedPaths(WorkingTreeStatus status)
    {
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in status.Staged)
        {
            paths.Add(file.Path);
            if (file.OldPath is { } old)
            {
                paths.Add(old);
            }
        }

        foreach (var file in status.Unstaged.Where(file => !status.IsUntracked(file.Path)))
        {
            paths.Add(file.Path);
        }

        return paths;
    }

    /// <summary>
    /// A commit of the real index's tree with HEAD as its parent, as <c>git stash</c> keeps the
    /// index. The tree is written from a copy of the index, so the real one is not touched. Null
    /// when there is no index file.
    /// </summary>
    private async Task<string?> CommitIndexAsync(string? head, CancellationToken cancellationToken)
    {
        var real = Path.Combine(GitDirectory, "index");
        if (!File.Exists(real))
        {
            return null;
        }

        var copy = Path.Combine(Path.GetTempPath(), $"visualcommit-index-{Guid.NewGuid():N}.index");
        try
        {
            File.Copy(real, copy);
            var environment = new Dictionary<string, string?>(SnapshotIdentity) { ["GIT_INDEX_FILE"] = copy };
            var tree = (await RunAsync(environment, null, cancellationToken, "write-tree").ConfigureAwait(false)).Trim();
            string[] arguments = head is null ? ["commit-tree", tree] : ["commit-tree", tree, "-p", head];
            return (await RunAsync(SnapshotIdentity, "VisualCommit: index before a hard reset\n", cancellationToken, arguments).ConfigureAwait(false)).Trim();
        }
        finally
        {
            File.Delete(copy);
        }
    }

    /// <summary>
    /// A snapshot commit made D67's way, without a ref: an index of its own, read from HEAD, with
    /// <paramref name="paths"/> added as they are in the working tree (a path gone from it as
    /// deleted). Its parents are HEAD and <paramref name="secondParent"/>. The paths go to git on
    /// standard input, so any number of them fits.
    /// </summary>
    private async Task<string> CommitSnapshotAsync(
        IReadOnlyCollection<string> paths,
        string? head,
        string? secondParent,
        string subject,
        CancellationToken cancellationToken)
    {
        var present = paths.Where(path => IsFileOrLink(WorkingTreePath(path))).ToList();
        var gone = paths.Where(path => !IsFileOrLink(WorkingTreePath(path))).ToList();

        var index = Path.Combine(Path.GetTempPath(), $"visualcommit-snapshot-{Guid.NewGuid():N}.index");
        try
        {
            var environment = new Dictionary<string, string?>(SnapshotIdentity) { ["GIT_INDEX_FILE"] = index };
            if (head is not null)
            {
                await RunAsync(environment, null, cancellationToken, "read-tree", head).ConfigureAwait(false);
            }

            if (present.Count > 0)
            {
                await RunAsync(environment, NulSeparated(present), cancellationToken, "add", "--all", "--force", "--pathspec-from-file=-", "--pathspec-file-nul")
                    .ConfigureAwait(false);
            }

            if (gone.Count > 0)
            {
                await RunAsync(environment, NulSeparated(gone), cancellationToken, "update-index", "--force-remove", "-z", "--stdin").ConfigureAwait(false);
            }

            var tree = (await RunAsync(environment, null, cancellationToken, "write-tree").ConfigureAwait(false)).Trim();
            var arguments = new List<string> { "commit-tree", tree };
            foreach (var parent in new[] { head, secondParent })
            {
                if (parent is not null)
                {
                    arguments.AddRange(["-p", parent]);
                }
            }

            var message = $"{subject}\n\n{string.Join('\n', paths)}\n";
            var commit = (await RunAsync(SnapshotIdentity, message, cancellationToken, [.. arguments]).ConfigureAwait(false)).Trim();
            _log.Info($"Made a snapshot of {Count(paths.Count, "file")} ({commit[..Math.Min(7, commit.Length)]}): {subject}.");
            return commit;
        }
        finally
        {
            File.Delete(index);
        }

        static string NulSeparated(IEnumerable<string> items) => string.Concat(items.Select(item => item + "\0"));
    }
}
