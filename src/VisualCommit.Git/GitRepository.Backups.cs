using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// Phase 3's backup refs and the snapshots that join them (design 2.0, 2.11, 2.15, Q3, D83):
/// one timestamp per operation, every ref in one <c>git update-ref --stdin</c> transaction, under
/// <c>refs/visualcommit/backup/&lt;kind&gt;-&lt;UTC time&gt;[-&lt;n&gt;][/&lt;name&gt;]</c>; and
/// phase 4's clean-up of them (docs/phase-4-design.md section 9, D137, D139).
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>Where every backup ref of phase 3 lives (D67's discard snapshots included).</summary>
    public const string BackupRefPrefix = "refs/visualcommit/backup/";

    /// <summary>How many names a batch tries before it gives up: the first, then the suffixes -2 to -20.</summary>
    private const int BackupAttempts = 20;

    /// <summary>A backup's time in its name: UTC to the millisecond (D67).</summary>
    private const string BackupTimeFormat = "yyyyMMdd'T'HHmmssfff'Z'";

    /// <summary>
    /// The clock of the backup refs' timestamps, the discard snapshots' included
    /// (<see cref="Core.Git.BackupClock.NextName"/>), and the clean-up's "now"
    /// (<see cref="Core.Git.BackupClock.Now"/>). The system clock unless the app gives every
    /// repository it opens its own (docs/phase-5-design.md 2.3: the harnesses' fixed clock); tests
    /// set a fixed one to make two batches collide (2.0's retry naming) and to age backups (section 9).
    /// </summary>
    public BackupClock BackupClock { get; set; } = BackupClock.System;

    /// <summary>
    /// Called by the clean-up between its read of the backup refs and its deletion, for tests that
    /// change a ref in between (section 9: the transaction then deletes nothing). Null in the app.
    /// </summary>
    internal Func<Task>? AfterBackupRefsRead { get; set; }

    public async Task<BackupResult> SaveBackupRefsAsync(BackupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateBackupRequest(request);

        // One timestamp for the whole operation; a retry puts its suffix on the kind-and-time
        // part, never on the whole ref, so a "-changes" ref keeps its pair's name and a name with
        // slashes cannot clash with a folder of the same name (2.0).
        var time = BackupClock.NextName().ToString(BackupTimeFormat, CultureInfo.InvariantCulture);
        var names = BackupNames(request.Entries);
        for (var attempt = 1; ; attempt++)
        {
            var stamp = attempt == 1 ? time : string.Create(CultureInfo.InvariantCulture, $"{time}-{attempt}");
            var stem = $"{BackupRefPrefix}{request.Kind}-{stamp}";
            var lines = request.Entries
                .Select((entry, i) => (Ref: names[i] is { } name ? $"{stem}/{name}" : stem, entry.Target))
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

            var command = new GitCommand("update-ref", "--stdin") { WorkingDirectory = WorkingDirectory, StandardInput = input.ToString(), RecordsStandardInput = true };
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

    /// <summary>
    /// The entries' names as the batch writes them: equal to the request's, except where a name
    /// meets an earlier one ignoring case, as the remote branches <c>origin/Foo</c> and
    /// <c>origin/foo</c> do. Where refs are files (Windows, macOS) the two would be one file, and
    /// the whole batch would fail; on every platform the later one's clashing part gets a number,
    /// <c>origin/foo-2</c>, as does a folder that meets a ref's name (<c>origin/foo-2/x</c> after
    /// <c>origin/Foo</c>).
    /// </summary>
    private static List<string?> BackupNames(IReadOnlyList<BackupEntry> entries)
    {
        var root = new BackupNameFolder();
        var names = new List<string?>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Name is null)
            {
                names.Add(null);
                continue;
            }

            var parts = entry.Name.Split('/');
            var folder = root;
            for (var i = 0; i < parts.Length; i++)
            {
                var isRef = i == parts.Length - 1;
                var part = parts[i];
                for (var number = 2; !folder.CanTake(part, isRef); number++)
                {
                    part = string.Create(CultureInfo.InvariantCulture, $"{parts[i]}-{number}");
                }

                parts[i] = part;
                folder = folder.Take(part, isRef);
            }

            names.Add(string.Join('/', parts));
        }

        return names;
    }

    /// <summary>The names a batch has used in one folder of its refs, compared ignoring case.</summary>
    private sealed class BackupNameFolder
    {
        /// <summary>By name: its spelling, and the folder it names, or null for a ref.</summary>
        private readonly Dictionary<string, (string Spelling, BackupNameFolder? Folder)> _names = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Whether <paramref name="name"/> is free here, or is the same folder spelled the same.</summary>
        public bool CanTake(string name, bool isRef) =>
            !_names.TryGetValue(name, out var taken)
            || (!isRef && taken.Folder is not null && string.Equals(taken.Spelling, name, StringComparison.Ordinal));

        /// <summary>Takes <paramref name="name"/>; returns the folder it names (a new, empty one for a ref, which has none).</summary>
        public BackupNameFolder Take(string name, bool isRef)
        {
            if (_names.TryGetValue(name, out var taken) && taken.Folder is { } existing)
            {
                return existing;
            }

            var folder = new BackupNameFolder();
            _names[name] = (name, isRef ? null : folder);
            return folder;
        }
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
            indexCommit = await CommitIndexAsync(head, "VisualCommit: index before a hard reset", cancellationToken).ConfigureAwait(false);
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

    public async Task<string?> SnapshotIndexAsync(CancellationToken cancellationToken = default)
    {
        // A mixed reset keeps the working tree and replaces the index: a staged version that the
        // working tree no longer has would be left to git's garbage collection. An index with a
        // conflict has no tree; its files keep their markers in the working tree.
        var head = await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false);
        if (head is null || await HasConflictsAsync(cancellationToken).ConfigureAwait(false) || await IndexEqualsHeadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return await CommitIndexAsync(head, "VisualCommit: index before a mixed reset", cancellationToken).ConfigureAwait(false);
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
    private async Task<string?> CommitIndexAsync(string? head, string subject, CancellationToken cancellationToken)
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
            return (await RunAsync(SnapshotIdentity, subject + "\n", cancellationToken, arguments).ConfigureAwait(false)).Trim();
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

    public async Task<IReadOnlyList<RemovedBackup>> CleanUpBackupsAsync(
        BackupRetention retention,
        IReadOnlySet<string> keep,
        NetworkOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(retention);
        ArgumentNullException.ThrowIfNull(keep);
        ArgumentOutOfRangeException.ThrowIfNegative(retention.Days);
        ArgumentOutOfRangeException.ThrowIfNegative(retention.Count);

        var removed = await ChooseBackupsToCleanUpAsync(retention, keep, cancellationToken).ConfigureAwait(false);
        if (removed.Count == 0)
        {
            return [];
        }

        if (AfterBackupRefsRead is { } hook)
        {
            await hook().ConfigureAwait(false);
        }

        // One transaction of "delete <ref> <old value>" lines: a ref changed since it was read
        // fails the whole of it, and nothing is deleted.
        var input = new StringBuilder();
        foreach (var (backup, _) in removed)
        {
            foreach (var (refName, value) in backup.Refs)
            {
                input.Append("delete ").Append(refName).Append(' ').Append(value).Append('\n');
            }
        }

        var command = new GitCommand("update-ref", "--stdin")
        {
            WorkingDirectory = WorkingDirectory,
            Environment = (options ?? NetworkOptions.None).Environment,
            StandardInput = input.ToString(),
            RecordsStandardInput = true,
        };
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            _log.Warning($"{Name}: The backups' clean-up deleted nothing: {result.StandardError.Trim()}");
            return [];
        }

        var removedBackups = new List<RemovedBackup>(removed.Count);
        foreach (var (backup, reason) in removed)
        {
            var removedBackup = new RemovedBackup(backup.Name, [.. backup.Refs.Select(entry => entry.Ref)], reason);
            _log.Info($"{Name}: {removedBackup.Describe(retention)}");
            removedBackups.Add(removedBackup);
        }

        return removedBackups;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> FindBackupsToRemoveAsync(
        BackupRetention retention,
        IReadOnlySet<string> keep,
        NetworkOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(retention);
        ArgumentNullException.ThrowIfNull(keep);
        ArgumentOutOfRangeException.ThrowIfNegative(retention.Days);
        ArgumentOutOfRangeException.ThrowIfNegative(retention.Count);
        var removed = await ChooseBackupsToCleanUpAsync(retention, keep, cancellationToken).ConfigureAwait(false);
        return [.. removed.Select(entry => entry.Backup.Name)];
    }

    /// <summary>
    /// The read half of <see cref="CleanUpBackupsAsync"/>: nothing while an operation is in
    /// progress in any worktree; otherwise the backups that go, by 9's rules, oldest first.
    /// </summary>
    private async Task<List<(Backup Backup, BackupRemovalReason Reason)>> ChooseBackupsToCleanUpAsync(
        BackupRetention retention,
        IReadOnlySet<string> keep,
        CancellationToken cancellationToken)
    {
        // The backup of an operation still in progress is never deleted: while any worktree has
        // one, the whole clean-up waits for another time.
        if (await OperationInProgressAnywhereAsync(cancellationToken).ConfigureAwait(false) is { } inProgress)
        {
            _log.Debug($"{Name}: The backups' clean-up waits: {inProgress}.");
            return [];
        }

        var output = await ReadOutputAsync(cancellationToken, "for-each-ref", "--format=%(refname) %(objectname)", BackupRefPrefix).ConfigureAwait(false);
        var now = BackupClock.Now;
        var backups = new Dictionary<string, Backup>(StringComparer.Ordinal);
        foreach (var line in SplitLines(output))
        {
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            if (space < 0 || ParseBackupName(line[..space]) is not { } name)
            {
                // A ref under the prefix that fits no scheme is never deleted, and not counted.
                continue;
            }

            if (!backups.TryGetValue(name.Backup, out var backup))
            {
                backup = new Backup(name.Backup, name.Kind, name.Number, name.When > now ? now : name.When);
                backups.Add(name.Backup, backup);
            }

            backup.Refs.Add((line[..space], line[(space + 1)..]));
        }

        return ChooseBackupsToRemove(backups.Values, retention, keep, now);
    }

    /// <summary>
    /// Which backups go (section 9): every one older than <see cref="BackupRetention.Days"/>, then,
    /// while more than <see cref="BackupRetention.Count"/> remain, the oldest. A backup with a ref
    /// in <paramref name="keep"/> never goes, but counts. Oldest first.
    /// </summary>
    private static List<(Backup Backup, BackupRemovalReason Reason)> ChooseBackupsToRemove(
        IEnumerable<Backup> backups,
        BackupRetention retention,
        IReadOnlySet<string> keep,
        DateTimeOffset now)
    {
        var oldestFirst = backups
            .OrderBy(backup => backup.When)
            .ThenBy(backup => backup.Number)
            .ThenBy(backup => backup.Kind, StringComparer.Ordinal)
            .ToList();
        var removable = oldestFirst.Where(backup => !backup.Refs.Any(entry => keep.Contains(entry.Ref))).ToList();
        var cutoff = now.AddDays(-retention.Days);
        var removed = removable
            .Where(backup => backup.When < cutoff)
            .Select(backup => (backup, BackupRemovalReason.OlderThanDays))
            .ToList();
        var remaining = oldestFirst.Count - removed.Count;
        foreach (var backup in removable.Where(backup => backup.When >= cutoff))
        {
            if (remaining <= retention.Count)
            {
                break;
            }

            removed.Add((backup, BackupRemovalReason.BeyondCount));
            remaining--;
        }

        return removed;
    }

    /// <summary>
    /// What makes the clean-up wait, or null: an operation in progress in any worktree of the
    /// repository (section 9), in the main git folder (<see cref="CommonDirectory"/>) and every
    /// <c>&lt;common&gt;/worktrees/*/</c>: <c>rebase-merge/</c>, <c>rebase-apply/</c>
    /// (<c>git am</c> too), <c>MERGE_HEAD</c>, <c>sequencer/</c>, or a pick head. This worktree's
    /// pick heads are read through <see cref="ReadPseudoRefAsync"/>, which also finds them as refs
    /// under the reftable format; another worktree's are files, and under reftable, where they are
    /// refs, a <c>MERGE_MSG</c> there (which every stopped pick or revert writes) counts as one.
    /// </summary>
    private async Task<string?> OperationInProgressAnywhereAsync(CancellationToken cancellationToken)
    {
        var folders = new List<string> { CommonDirectory };
        var worktrees = Path.Combine(CommonDirectory, "worktrees");
        if (Directory.Exists(worktrees))
        {
            folders.AddRange(Directory.EnumerateDirectories(worktrees).Order(StringComparer.Ordinal));
        }

        foreach (var folder in folders)
        {
            if (OperationInProgressIn(folder) is { } what)
            {
                return $"{what} is in progress in {folder}";
            }
        }

        if (await ReadPseudoRefAsync("CHERRY_PICK_HEAD", cancellationToken).ConfigureAwait(false) is not null
            || await ReadPseudoRefAsync("REVERT_HEAD", cancellationToken).ConfigureAwait(false) is not null)
        {
            return $"a cherry-pick or revert is in progress in {GitDirectory}";
        }

        var others = folders.Where(folder => !SameFolder(folder, GitDirectory) && File.Exists(Path.Combine(folder, "MERGE_MSG"))).ToList();
        if (others.Count > 0 && await UsesReftableAsync(cancellationToken).ConfigureAwait(false))
        {
            return $"a cherry-pick or revert may be in progress in {others[0]}";
        }

        return null;

        static string? OperationInProgressIn(string folder) =>
            Directory.Exists(Path.Combine(folder, "rebase-merge")) || Directory.Exists(Path.Combine(folder, "rebase-apply")) ? "a rebase"
            : File.Exists(Path.Combine(folder, "MERGE_HEAD")) ? "a merge"
            : Directory.Exists(Path.Combine(folder, "sequencer")) ? "a list of cherry-picks or reverts"
            : File.Exists(Path.Combine(folder, "CHERRY_PICK_HEAD")) ? "a cherry-pick"
            : File.Exists(Path.Combine(folder, "REVERT_HEAD")) ? "a revert"
            : null;

        static bool SameFolder(string one, string two) =>
            string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(one)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(two)),
                OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A backup ref's name read with section 9's pattern: the part after the prefix is
    /// <c>&lt;kind&gt;-&lt;time&gt;[-&lt;n&gt;]</c>, then optionally <c>-changes</c> or
    /// <c>/&lt;name&gt;</c>; a kind may have hyphens (<c>force-push</c>, <c>remote-branch</c>). Null
    /// for a name that fits no scheme, or whose time is no real time.
    /// </summary>
    private static (string Backup, string Kind, int Number, DateTimeOffset When)? ParseBackupName(string refName)
    {
        if (!refName.StartsWith(BackupRefPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var match = BackupNamePattern().Match(refName[BackupRefPrefix.Length..]);
        if (!match.Success
            || !DateTimeOffset.TryParseExact(match.Groups["time"].Value, BackupTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when)
            || (match.Groups["n"].Success && !int.TryParse(match.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            return null;
        }

        var kind = match.Groups["kind"].Value;
        var stem = $"{kind}-{match.Groups["time"].Value}";
        return match.Groups["n"].Success
            ? ($"{stem}-{match.Groups["n"].Value}", kind, int.Parse(match.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture), when)
            : (stem, kind, 1, when);
    }

    [GeneratedRegex(@"^(?<kind>[a-z]+(?:-[a-z]+)*)-(?<time>\d{8}T\d{9}Z)(?:-(?<n>\d+))?(?<rest>-changes|/.+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex BackupNamePattern();

    /// <summary>One backup: the refs that share its kind, time and number (section 9).</summary>
    private sealed class Backup(string name, string kind, int number, DateTimeOffset when)
    {
        /// <summary><c>&lt;kind&gt;-&lt;time&gt;[-&lt;n&gt;]</c>.</summary>
        public string Name { get; } = name;

        public string Kind { get; } = kind;

        /// <summary>Its retry number (1 without one), which orders the backups of one millisecond.</summary>
        public int Number { get; } = number;

        /// <summary>The time in its name; a time later than now counts as now.</summary>
        public DateTimeOffset When { get; } = when;

        /// <summary>Its refs and the objects they held when read.</summary>
        public List<(string Ref, string Value)> Refs { get; } = [];
    }
}
