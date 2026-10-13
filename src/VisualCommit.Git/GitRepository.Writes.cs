using System.Globalization;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

public sealed partial class GitRepository
{
    /// <summary>The prefix of the refs that keep discard snapshots (D67).</summary>
    public const string SnapshotRefPrefix = "refs/visualcommit/backup/discard-";

    /// <summary>
    /// Who a snapshot commit is by. A snapshot is the app's record, not the user's commit, and
    /// must be made even where the user has set no identity.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string?> SnapshotIdentity = new Dictionary<string, string?>
    {
        ["GIT_AUTHOR_NAME"] = "VisualCommit",
        ["GIT_AUTHOR_EMAIL"] = "snapshot@visualcommit.invalid",
        ["GIT_COMMITTER_NAME"] = "VisualCommit",
        ["GIT_COMMITTER_EMAIL"] = "snapshot@visualcommit.invalid",
    };

    public async Task StageAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        // A path can vanish between the status read and the click, as an editor's temporary file
        // does, and git add refuses the whole batch for a path that matches nothing. A path gone
        // from the working tree is staged only when the index has it: its deletion is staged.
        var gone = paths.Where(path => !File.Exists(WorkingTreePath(path)) && !Directory.Exists(WorkingTreePath(path))).ToList();
        var stage = paths;
        if (gone.Count > 0)
        {
            var output = await ReadOutputAsync(cancellationToken, ["ls-files", "-z", "--", .. gone]).ConfigureAwait(false);
            var tracked = output.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
            stage = [.. paths.Where(path => !gone.Contains(path) || tracked.Contains(path))];
        }

        if (stage.Count == 0)
        {
            return;
        }

        await WriteAsync(cancellationToken, ["add", "--all", "--", .. stage]).ConfigureAwait(false);
    }

    public async Task UnstageAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return;
        }

        // Before the first commit there is no HEAD to take the entries from: they go.
        var head = await _runner.RunAsync(Read("rev-parse", "--verify", "--quiet", "HEAD"), cancellationToken).ConfigureAwait(false);
        if (head.Succeeded)
        {
            await WriteAsync(cancellationToken, ["restore", "--staged", "--", .. paths]).ConfigureAwait(false);
        }
        else
        {
            await WriteAsync(cancellationToken, ["rm", "--cached", "-r", "--quiet", "--ignore-unmatch", "--", .. paths]).ConfigureAwait(false);
        }
    }

    public async Task ApplyPatchAsync(string patch, bool toIndex, PatchDirection direction, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(patch);
        var arguments = new List<string> { "apply", "--whitespace=nowarn" };
        if (toIndex)
        {
            arguments.Add("--cached");
        }

        if (direction == PatchDirection.Reverse)
        {
            arguments.Add("--reverse");
        }

        arguments.Add("-");
        var command = new GitCommand([.. arguments])
        {
            WorkingDirectory = WorkingDirectory,
            StandardInput = patch,
            Encoding = GitCommand.Latin1,
        };
        (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
    }

    public async Task<DiscardSnapshot> SaveSnapshotAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            throw new ArgumentException("A snapshot needs at least one file.", nameof(paths));
        }

        var present = new HashSet<string>(paths.Where(path => File.Exists(WorkingTreePath(path))), StringComparer.Ordinal);

        // An index of its own, filled from HEAD, takes the files as they are in the working tree;
        // the real index is left alone. Not a copy of the real index: one with a conflict in it
        // cannot be written as a tree.
        var index = Path.Combine(Path.GetTempPath(), $"visualcommit-snapshot-{Guid.NewGuid():N}.index");
        try
        {
            var environment = new Dictionary<string, string?>(SnapshotIdentity) { ["GIT_INDEX_FILE"] = index };
            var head = await _runner.RunAsync(Read("rev-parse", "--verify", "--quiet", "HEAD"), cancellationToken).ConfigureAwait(false);
            if (head.Succeeded)
            {
                await RunAsync(environment, null, cancellationToken, "read-tree", "HEAD").ConfigureAwait(false);
            }

            // A file that is gone is taken out by name: one that only the real index has would
            // not match anything for git add.
            var existing = paths.Where(present.Contains).ToList();
            if (existing.Count > 0)
            {
                await RunAsync(environment, null, cancellationToken, ["add", "--all", "--force", "--", .. existing]).ConfigureAwait(false);
            }

            var gone = paths.Where(path => !present.Contains(path)).ToList();
            if (gone.Count > 0)
            {
                await RunAsync(environment, null, cancellationToken, ["update-index", "--force-remove", "--", .. gone]).ConfigureAwait(false);
            }

            var tree = (await RunAsync(environment, null, cancellationToken, "write-tree").ConfigureAwait(false)).Trim();

            var message = $"VisualCommit: snapshot before discarding {Count(paths.Count, "file")}\n\n{string.Join('\n', paths)}\n";
            var commitArguments = head.Succeeded
                ? new[] { "commit-tree", tree, "-p", head.StandardOutput.Trim() }
                : ["commit-tree", tree];
            var commit = (await RunAsync(SnapshotIdentity, message, cancellationToken, commitArguments).ConfigureAwait(false)).Trim();

            // The backups' clock, as every backup's (docs/phase-4-design.md section 9): a test's fixed
            // clock then orders the snapshots with the other backups for the clean-up.
            var name = SnapshotRefPrefix + BackupClock.NextName().ToString(BackupTimeFormat, CultureInfo.InvariantCulture);
            var refName = name;
            for (var attempt = 2; ; attempt++)
            {
                // An empty old value: the ref must not exist yet, so two snapshots never share one.
                var update = new GitCommand("update-ref", refName, commit, string.Empty) { WorkingDirectory = WorkingDirectory };
                var result = await _runner.RunAsync(update, cancellationToken).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    break;
                }

                if (attempt > 20)
                {
                    result.EnsureSuccess(update);
                }

                refName = $"{name}-{attempt}";
            }

            _log.Info($"Saved a snapshot of {Count(paths.Count, "file")} as {refName} ({commit[..Math.Min(7, commit.Length)]}) before discarding.");
            return new DiscardSnapshot(refName, commit, [.. paths], present);
        }
        finally
        {
            File.Delete(index);
        }
    }

    public async Task DiscardAsync(IReadOnlyList<string> paths, IReadOnlySet<string> untracked, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(untracked);

        var tracked = paths.Where(path => !untracked.Contains(path)).ToList();
        if (tracked.Count > 0)
        {
            await WriteAsync(cancellationToken, ["restore", "--worktree", "--", .. tracked]).ConfigureAwait(false);
        }

        foreach (var path in paths.Where(untracked.Contains))
        {
            DeleteWorkingFile(path);
        }
    }

    public async Task RestoreSnapshotAsync(DiscardSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var present = snapshot.Paths.Where(snapshot.Present.Contains).ToList();
        if (present.Count > 0)
        {
            // Through git, so that the working tree gets the file as git checks it out: line
            // endings and filters as configured.
            await WriteAsync(cancellationToken, ["restore", $"--source={snapshot.Commit}", "--worktree", "--", .. present]).ConfigureAwait(false);
        }

        foreach (var path in snapshot.Paths.Where(path => !snapshot.Present.Contains(path)))
        {
            DeleteWorkingFile(path);
        }
    }

    public async Task<string> CommitAsync(string message, bool amend, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var arguments = new List<string> { "commit", "--file=-", "--cleanup=whitespace" };
        if (amend)
        {
            arguments.Add("--amend");
        }

        var command = new GitCommand([.. arguments]) { WorkingDirectory = WorkingDirectory, StandardInput = message };
        (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
        return (await ReadOutputAsync(cancellationToken, "rev-parse", "HEAD").ConfigureAwait(false)).Trim();
    }

    /// <summary>A call that writes: in the working tree, without <c>GIT_OPTIONAL_LOCKS=0</c>. A failure throws <see cref="GitException"/>.</summary>
    private Task<string> WriteAsync(CancellationToken cancellationToken, params string[] arguments) =>
        RunAsync(new Dictionary<string, string?>(), null, cancellationToken, arguments);

    private async Task<string> RunAsync(
        IReadOnlyDictionary<string, string?> environment,
        string? input,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var command = new GitCommand(arguments) { WorkingDirectory = WorkingDirectory, Environment = environment, StandardInput = input };
        return (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command).StandardOutput;
    }

    /// <summary>Deletes a file of the working tree, and then the folders it leaves empty, up to the top folder. A file already gone is left so.</summary>
    private void DeleteWorkingFile(string relativePath)
    {
        var path = WorkingTreePath(relativePath);
        if (!File.Exists(path))
        {
            return;
        }

        File.Delete(path);

        var top = Path.TrimEndingDirectorySeparator(WorkingDirectory);
        for (var folder = Path.GetDirectoryName(path); folder is not null && folder.Length > top.Length; folder = Path.GetDirectoryName(folder))
        {
            if (Directory.EnumerateFileSystemEntries(folder).Any())
            {
                break;
            }

            Directory.Delete(folder);
        }
    }

    private static string Count(int count, string noun) =>
        count == 1 ? $"1 {noun}" : string.Create(CultureInfo.InvariantCulture, $"{count} {noun}s");
}
