using System.Collections.Concurrent;
using System.Globalization;
using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Phase 3's branch operations and the reads they rely on (design 2.1 to 2.10, D93, D94): checkouts,
/// create, rename, delete, the fast-forward of a branch that is not checked out, and merge. Each
/// write is one git call (merge: two) that throws <see cref="GitException"/> with git's own text
/// when git refuses; a merge's conflicts are returned, not thrown. The queue and the confirmations
/// around them are the caller's (WP7). Phase 5 adds C12's reads and writes (docs/phase-5-design.md
/// section 9): the checkout that keeps ignored files and the automatic stash's steps.
/// </summary>
public sealed partial class GitRepository
{
    // ---- Reads (GIT_OPTIONAL_LOCKS=0) ------------------------------------------------------------

    public async Task<int> CountDetachedCommitsAsync(CancellationToken cancellationToken = default)
    {
        // An unborn HEAD has no commits to leave behind, and rev-list would refuse the name.
        if (await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false) is null)
        {
            return 0;
        }

        var output = await ReadOutputAsync(cancellationToken, "rev-list", "--count", "HEAD", "--not", "--branches", "--remotes", "--tags", "--")
            .ConfigureAwait(false);
        return ParseCount(output);
    }

    public async Task<bool> IsAncestorAsync(string ancestor, string descendant, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ancestor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descendant);
        var command = Read("merge-base", "--is-ancestor", ancestor, descendant);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        return result.ExitCode switch
        {
            0 => true,
            1 => false,
            _ => throw new GitException(command.DisplayText, result.ExitCode, result.StandardError, standardOutput: result.StandardOutput),
        };
    }

    public async Task<int> CountCommitsAsync(string from, string to, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        var output = await ReadOutputAsync(cancellationToken, "rev-list", "--count", $"{from}..{to}", "--").ConfigureAwait(false);
        return ParseCount(output);
    }

    public async Task<string?> ResolveAsync(string revision, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        var command = Read("rev-parse", "--verify", "--quiet", revision);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1)
        {
            return null; // --quiet: the name names nothing.
        }

        var sha = result.EnsureSuccess(command).StandardOutput.Trim();
        return sha.Length > 0 ? sha : null;
    }

    public async Task<bool> CommitExistsAsync(string sha, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);
        var command = Read("cat-file", "-e", sha + "^{commit}");
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);

        // 128: "Not a valid object name" (no such object, or not a commit); 1: an object -e cannot find.
        return result.ExitCode switch
        {
            0 => true,
            1 or 128 => false,
            _ => throw new GitException(command.DisplayText, result.ExitCode, result.StandardError, standardOutput: result.StandardOutput),
        };
    }

    public async Task<IReadOnlyList<WorktreeInfo>> ReadWorktreesAsync(CancellationToken cancellationToken = default)
    {
        var output = await ReadOutputAsync(cancellationToken, "worktree", "list", "--porcelain").ConfigureAwait(false);
        return ParseWorktrees(output);
    }

    /// <summary>
    /// Reads <c>git worktree list --porcelain</c>: blocks separated by an empty line, each starting
    /// with <c>worktree &lt;path&gt;</c> and holding <c>branch &lt;full ref&gt;</c>, <c>detached</c>
    /// or <c>bare</c> among other lines. The main worktree comes first. Paths are printed with
    /// forward slashes on every platform; they get the platform's separators here.
    /// </summary>
    internal static IReadOnlyList<WorktreeInfo> ParseWorktrees(string output)
    {
        var worktrees = new List<WorktreeInfo>();
        string? path = null;
        string? branch = null;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                Flush();
                path = line["worktree ".Length..];
            }
            else if (line.StartsWith("branch ", StringComparison.Ordinal))
            {
                branch = line["branch ".Length..];
            }
            else if (line.Length == 0)
            {
                Flush();
            }
        }

        Flush();
        return worktrees;

        void Flush()
        {
            if (path is not null)
            {
                var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
                worktrees.Add(new WorktreeInfo(full, branch, IsMain: worktrees.Count == 0));
            }

            path = null;
            branch = null;
        }
    }

    // ---- Checkouts (2.1 to 2.3) -----------------------------------------------------------------

    // keepIgnored: work package WP1 adds --no-overwrite-ignore for it (docs/phase-6-design.md
    // 9.11); until then every caller but the automatic stash passes false.
    public Task CheckoutBranchAsync(string branch, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        if (branch == "-")
        {
            // Plumbing can make a branch "-"; switch takes it for the branch checked out before,
            // after --end-of-options too.
            throw new ArgumentException("A branch named '-' cannot be checked out by name: git takes '-' for the previous branch.", nameof(branch));
        }

        // switch takes only branches, so a tag of the same name cannot be checked out instead,
        // and --no-guess keeps it from creating a branch out of a remote one of that name.
        return RunWriteAsync(options, cancellationToken, ["switch", "--no-guess", .. Positional(branch)]);
    }

    // keepIgnored: as CheckoutBranchAsync's, work package WP1's (9.11).
    public Task CheckoutTrackingBranchAsync(string branch, string remoteBranchRef, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        RequirePrefix(remoteBranchRef, RemotesPrefix, nameof(remoteBranchRef));
        return RunWriteAsync(options, cancellationToken, "switch", "--create", branch, "--track", remoteBranchRef);
    }

    // C12: the automatic stash (docs/phase-5-design.md 9, D179) is StashAsync with untracked
    // files, then this checkout with keepIgnored; the operation (RepositoryOperations.Protect.cs)
    // runs the steps of 9.2 with these.
    public Task CheckoutDetachedAsync(string commit, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);

        // --no-overwrite-ignore (a switch option since Git 2.23) refuses where git would otherwise
        // overwrite an ignored file the commit tracks, or delete an ignored folder where it has a
        // file (0.7); the refusal is git's untracked-files block and changes nothing.
        return keepIgnored
            ? RunWriteAsync(options, cancellationToken, ["switch", "--detach", "--no-overwrite-ignore", .. Positional(commit)])
            : RunWriteAsync(options, cancellationToken, ["switch", "--detach", .. Positional(commit)]);
    }

    public async Task RestoreWorkingTreeFilesAsync(string source, IReadOnlyList<string> paths, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return;
        }

        // The paths on standard input, NUL-separated: as many as there are, any name. They stay
        // literal (GIT_LITERAL_PATHSPECS, D74), and the index is left alone (--worktree only).
        var command = new GitCommand("restore", $"--source={source}", "--worktree", "--pathspec-from-file=-", "--pathspec-file-nul")
        {
            WorkingDirectory = WorkingDirectory,
            Environment = (options ?? NetworkOptions.None).Environment,
            StandardInput = string.Concat(paths.Select(path => path + "\0")),
        };
        (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
    }

    public async Task<IReadOnlySet<string>> FindIgnoredAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var ignored = new HashSet<string>(StringComparer.Ordinal);
        if (paths.Count == 0)
        {
            return ignored;
        }

        // A folder git names ("would lose untracked files in them") is asked about by its files,
        // which check-ignore matches where the folder's own name may not (*.log in logs/): it is
        // ignored when every file in it is. A symbolic link is asked about as a file. The same
        // rules and the same check-ignore call as the undo's switch (GitRepository.Undo.cs).
        var asked = paths.Distinct(StringComparer.Ordinal).ToDictionary(
            path => path,
            path => !IsFileOrLink(WorkingTreePath(path)) && Directory.Exists(WorkingTreePath(path)) ? FilesUnder(path) : [path],
            StringComparer.Ordinal);
        var matched = (await IgnoredAmongAsync([.. asked.Values.SelectMany(files => files).Distinct(StringComparer.Ordinal)], cancellationToken).ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var (path, files) in asked)
        {
            if (files.Count > 0 && files.All(matched.Contains))
            {
                ignored.Add(path);
            }
        }

        return ignored;
    }

    /// <summary>
    /// The paths of the stash commits read so far, by full SHA: a commit never changes, so C12's
    /// check, the undo history's reads before and after an apply or a pop, and an undo step's check
    /// of a pop read them once.
    /// </summary>
    private readonly ConcurrentDictionary<string, Task<StashPaths>> _stashPaths = new(StringComparer.OrdinalIgnoreCase);

    public Task<StashPaths> ReadStashPathsAsync(string stashCommit, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stashCommit);
        if (!IsFullObjectId(stashCommit))
        {
            // A name (stash@{0}) may name another commit tomorrow: read, not kept.
            return ReadStashPathsUncachedAsync(stashCommit, cancellationToken);
        }

        if (_stashPaths.TryGetValue(stashCommit, out var known))
        {
            return known.WaitAsync(cancellationToken);
        }

        // One read for every caller, not cancelled by any one of them; a failed read is not kept.
        var read = ReadStashPathsUncachedAsync(stashCommit, CancellationToken.None);
        var kept = _stashPaths.GetOrAdd(stashCommit, read);
        if (ReferenceEquals(kept, read))
        {
            _ = read.ContinueWith(
                failed => _stashPaths.TryRemove(new KeyValuePair<string, Task<StashPaths>>(stashCommit, failed)),
                CancellationToken.None,
                TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        return kept.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// <see cref="ReadStashPathsAsync"/>'s reads, side by side: what the stash's working-tree commit
    /// changes against its base, with each path's status (<c>--name-status</c>: the paths it adds,
    /// <see cref="StashPaths.Added"/>), and what its index commit changes (both sides of a rename,
    /// <c>--no-renames</c>), and, when it has a third parent, its untracked files (a stash made
    /// without them has two parents: <c>^3</c> names nothing, the report's PD15).
    /// </summary>
    private async Task<StashPaths> ReadStashPathsUncachedAsync(string stashCommit, CancellationToken cancellationToken)
    {
        var changed = ReadOutputAsync(cancellationToken, "diff-tree", "-r", "-z", "--no-renames", "--name-status", stashCommit + "^1", stashCommit);
        var staged = ReadOutputAsync(cancellationToken, "diff-tree", "-r", "-z", "--no-renames", "--name-only", stashCommit + "^1", stashCommit + "^2");
        var third = ResolveAsync(stashCommit + "^3", cancellationToken);
        await Task.WhenAll((Task[])[changed, staged, third]).ConfigureAwait(false);
        var untracked = await third.ConfigureAwait(false) is { } tree
            ? await ReadOutputAsync(cancellationToken, "ls-tree", "-r", "-z", "--name-only", tree).ConfigureAwait(false)
            : string.Empty;

        // "<status>\0<path>\0" per path: --no-renames gives one path to every status.
        var statuses = SplitPaths(await changed.ConfigureAwait(false));
        var paths = new List<string>(statuses.Length / 2);
        var added = new List<string>();
        for (var i = 0; i + 1 < statuses.Length; i += 2)
        {
            paths.Add(statuses[i + 1]);
            if (statuses[i] == "A")
            {
                added.Add(statuses[i + 1]);
            }
        }

        return new StashPaths(paths, SplitPaths(untracked))
        {
            Staged = SplitPaths(await staged.ConfigureAwait(false)),
            Added = added,
        };

        static string[] SplitPaths(string output) => output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Whether <paramref name="value"/> is a whole object id (SHA-1 or SHA-256, hex), which names one object for good.</summary>
    private static bool IsFullObjectId(string value) =>
        value.Length is 40 or 64 && value.All(char.IsAsciiHexDigit);

    /// <summary>
    /// The files under a folder of the working tree (<paramref name="folder"/>, a repository path),
    /// as repository paths in byte order: what git would lose with it. A symbolic link is a file to
    /// git, also one to a folder, and is not followed. For the checkout's refusal (9.3) and the
    /// undo's switch and Keep moves (<c>UndoStepRun</c>, 7.3).
    /// </summary>
    private List<string> FilesUnder(string folder)
    {
        var files = new List<string>();
        var folders = new Stack<string>([folder]);
        while (folders.TryPop(out var relative))
        {
            foreach (var entry in new DirectoryInfo(WorkingTreePath(relative)).EnumerateFileSystemInfos("*", new EnumerationOptions { AttributesToSkip = 0 }))
            {
                var path = relative + "/" + entry.Name;
                if (entry is DirectoryInfo && entry.LinkTarget is null)
                {
                    folders.Push(path);
                }
                else
                {
                    files.Add(path);
                }
            }
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    // ---- Create, rename, delete (2.4 to 2.6) -----------------------------------------------------

    // noTrack and keepIgnored: work package WP1 adds --no-track and --no-overwrite-ignore for
    // them (docs/phase-6-design.md 9.11, 10.4, D201); until then every caller passes false.
    public Task CreateBranchAsync(string name, string startPoint, bool checkout, bool noTrack, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(startPoint);
        // --create takes the name as its value, whatever it starts with.
        return checkout
            ? RunWriteAsync(options, cancellationToken, ["switch", "--create", name, .. Positional(startPoint)])
            : RunWriteAsync(options, cancellationToken, ["branch", .. Positional(name, startPoint)]);
    }

    public Task RenameBranchAsync(string branch, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        return RunWriteAsync(null, cancellationToken, ["branch", "--move", .. Positional(branch, newName)]);
    }

    public Task DeleteBranchAsync(string branch, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        return RunWriteAsync(null, cancellationToken, ["branch", "--delete", "--force", .. Positional(branch)]);
    }

    // ---- Fast-forward a branch that is not checked out (2.7) -------------------------------------

    public Task FastForwardBranchAsync(string branch, string target, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        // "." is this repository: no network and no prompt. Git refuses a non-fast-forward and a
        // branch checked out in any worktree, so a branch that moved meanwhile is never damaged.
        return RunWriteAsync(
            null,
            cancellationToken,
            ["fetch", "--no-auto-maintenance", "--no-write-fetch-head", .. Positional(".", $"{target}:{HeadsPrefix}{branch}")]);
    }

    // ---- Merge (2.8 to 2.10, D94) ----------------------------------------------------------------

    // keepIgnored: work package WP1 adds --no-overwrite-ignore for it (docs/phase-6-design.md
    // 9.4); until then every caller passes false.
    public async Task<MergeResult> MergeAsync(string reference, MergeMode mode, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        // A name that is no commit gets no message; git's merge then refuses it in its own words.
        var target = await ResolveAsync(reference + "^{commit}", cancellationToken).ConfigureAwait(false);
        var before = await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false);
        var conflictedBefore = await HasConflictsAsync(cancellationToken).ConfigureAwait(false);
        var mergeHead = Path.Combine(GitDirectory, "MERGE_HEAD");
        var mergingBefore = File.Exists(mergeHead);

        var arguments = new List<string> { "merge" };
        if (mode == MergeMode.FastForwardOnly)
        {
            arguments.Add("--ff-only");
        }
        else
        {
            arguments.AddRange(["--no-edit", "--no-log"]);
            var message = target is null ? string.Empty : await MergeMessageAsync(reference, target, cancellationToken).ConfigureAwait(false);
            if (message.Length > 0)
            {
                arguments.AddRange(["-m", message]);
            }

            if (mode == MergeMode.NoFastForward)
            {
                arguments.Add("--no-ff");
            }
        }

        arguments.AddRange(Positional(reference));
        var command = WriteCommand(options, [.. arguments]);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            // Conflicts the merge made, not ones that were there before and made git refuse it.
            // A merge that git leaves in progress without them stopped all the same: rerere
            // (with rerere.autoUpdate) staged its recorded resolutions, or a hook refused the
            // merge commit. The merge waits for its commit or its abort, which the banner offers.
            if (!conflictedBefore
                && (await HasConflictsAsync(cancellationToken).ConfigureAwait(false) || (!mergingBefore && File.Exists(mergeHead))))
            {
                return MergeResult.Conflicts;
            }

            result.EnsureSuccess(command);
        }

        var after = await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false);
        if (string.Equals(after, before, StringComparison.Ordinal))
        {
            return MergeResult.UpToDate;
        }

        return string.Equals(after, target, StringComparison.Ordinal) ? MergeResult.FastForward : MergeResult.Merged;
    }

    /// <summary>
    /// The message <c>git merge &lt;short name&gt;</c> would write, from <c>git fmt-merge-msg</c> (D94):
    /// "Merge branch 'feature/alpha'", "Merge remote-tracking branch 'origin/main'" or "Merge tag
    /// 'v1.0'", with " into &lt;current&gt;" and the shortlog where git's configuration adds them.
    /// Empty for anything else (git then writes its own) and when git prints nothing, as it does
    /// for a commit that is already merged; the merge then has nothing to commit.
    /// </summary>
    private async Task<string> MergeMessageAsync(string reference, string target, CancellationToken cancellationToken)
    {
        string? description = null;
        if (reference.StartsWith(HeadsPrefix, StringComparison.Ordinal))
        {
            description = $"branch '{reference[HeadsPrefix.Length..]}'";
        }
        else if (reference.StartsWith(RemotesPrefix, StringComparison.Ordinal))
        {
            description = $"remote-tracking branch '{reference[RemotesPrefix.Length..]}'";
        }
        else if (reference.StartsWith(TagsPrefix, StringComparison.Ordinal))
        {
            description = $"tag '{reference[TagsPrefix.Length..]}'";
        }

        if (description is null)
        {
            return string.Empty;
        }

        var command = new GitCommand("fmt-merge-msg")
        {
            WorkingDirectory = WorkingDirectory,
            Environment = ReadEnvironment,
            StandardInput = $"{target}\t\t{description} of .\n",
        };
        var result = (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
        return result.StandardOutput.TrimEnd('\r', '\n');
    }

    // ---- Shared by the files of WP1 --------------------------------------------------------------

    /// <summary>
    /// A call that writes, in the working tree, with the operation's prompt variables
    /// (<see cref="NetworkOptions.Environment"/>, design 1.5).
    /// </summary>
    private GitCommand WriteCommand(NetworkOptions? options, params string[] arguments) =>
        new(arguments) { WorkingDirectory = WorkingDirectory, Environment = (options ?? NetworkOptions.None).Environment };

    /// <summary>
    /// A call that writes, as <see cref="WriteCommand(NetworkOptions?, string[])"/>, with
    /// <paramref name="environment"/> added for this call only: the app's sequence editor
    /// (<c>GIT_SEQUENCE_EDITOR</c>, docs/phase-4-design.md 2.5) or a stopped commit's author
    /// (<c>GIT_AUTHOR_NAME</c>, <c>GIT_AUTHOR_EMAIL</c>, <c>GIT_AUTHOR_DATE</c>, 5.4). They come
    /// after the prompt variables and, like any command's own, override the runner's defaults
    /// (0.2); a null value removes a variable.
    /// </summary>
    private GitCommand WriteCommand(NetworkOptions? options, IReadOnlyDictionary<string, string?> environment, params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var merged = new Dictionary<string, string?>((options ?? NetworkOptions.None).Environment);
        foreach (var (name, value) in environment)
        {
            merged[name] = value;
        }

        return new(arguments) { WorkingDirectory = WorkingDirectory, Environment = merged };
    }

    /// <summary>
    /// <paramref name="names"/> as git's positional arguments: after <c>--end-of-options</c> when
    /// one of them starts with "-", so that git cannot take it for an option (a branch named
    /// "-f", which plumbing or a fetch can make, would otherwise force the call). Only then, so
    /// that the commands the app shows stay as a user would type them. <c>git reset</c> refuses
    /// the marker before its revision (Git 2.36): it checks its revision itself.
    /// </summary>
    private static string[] Positional(params string[] names) =>
        names.Any(name => name.StartsWith('-')) ? ["--end-of-options", .. names] : names;

    /// <summary>Runs a call that writes; a failure throws <see cref="GitException"/> with git's own text.</summary>
    private async Task RunWriteAsync(NetworkOptions? options, CancellationToken cancellationToken, params string[] arguments)
    {
        var command = WriteCommand(options, arguments);
        (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
    }

    /// <summary>Whether the index has a conflicted entry: how a merging operation that failed is told from one that stopped (2.8).</summary>
    private async Task<bool> HasConflictsAsync(CancellationToken cancellationToken)
    {
        var output = await ReadOutputAsync(cancellationToken, "ls-files", "--unmerged", "-z").ConfigureAwait(false);
        return output.Length > 0;
    }

    private static int ParseCount(string output) =>
        int.Parse(output.Trim(), NumberStyles.None, CultureInfo.InvariantCulture);

    // ---- Shared by phase 4's rebase and sequence files (WP1, WP2) ------------------------------

    /// <summary>
    /// Whether a rebase is in progress in this worktree: <c>rebase-merge/</c> or
    /// <c>rebase-apply/</c> exists in <see cref="GitDirectory"/> (docs/phase-4-design.md 0.1:
    /// only these folders mean "in progress", also after an exit 0). Read before a rebase command
    /// for <see cref="ClassifyRebaseAsync"/>.
    /// </summary>
    internal bool RebaseFolderExists() =>
        Directory.Exists(Path.Combine(GitDirectory, "rebase-merge")) || Directory.Exists(Path.Combine(GitDirectory, "rebase-apply"));

    /// <summary>
    /// How a rebase command ended (docs/phase-4-design.md 1.4), shared by every rebase command of
    /// sections 1, 2, 3, 5 and 6, from its <paramref name="result"/> and whether a rebase folder
    /// existed before it ran (<paramref name="rebaseFolderBefore"/>, <see cref="RebaseFolderExists"/>):
    /// <list type="bullet">
    /// <item>A rebase folder after the call: <see cref="RebaseOutcome.Stopped"/> with the
    /// conflicted files of the index, whatever the exit code (an <c>edit</c> stop exits 0). When a
    /// folder was there before (Continue, Skip), a fatal exit (128 and above: "No rebase in
    /// progress?", another rebase's folder) and git's refusal to continue with unresolved files
    /// (<c>You must edit all merge conflicts</c>, exit 1, nothing changed) throw instead: the call
    /// changed nothing. A rebase onto refuses to run while a folder is there (1.3), so for it a
    /// folder after the call is always its own.</item>
    /// <item>No folder, exit 0, and a whole line of standard output is git's <c>Current branch
    /// &lt;b&gt; is up to date.</c> or <c>HEAD is up to date.</c> (not the words inside a commit
    /// subject git printed): <see cref="RebaseOutcome.UpToDate"/>.</item>
    /// <item>No folder, exit 0: <see cref="RebaseOutcome.Rebased"/> with HEAD as the new tip, and
    /// <see cref="RebaseResult.AutostashConflicted"/> with the conflicted files when standard error
    /// says <c>Applying autostash resulted in conflicts</c>.</item>
    /// <item>No folder, non-zero: git refused and nothing changed: <see cref="GitException"/> with
    /// git's own text.</item>
    /// </list>
    /// The texts are matched by their classifying part only, the same on Git 2.36 and 2.55.
    /// </summary>
    internal async Task<RebaseResult> ClassifyRebaseAsync(GitCommand command, GitResult result, bool rebaseFolderBefore, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(result);
        if (RebaseFolderExists())
        {
            if (rebaseFolderBefore && (result.ExitCode >= 128 || (result.ExitCode != 0 && RefusedToContinue(result))))
            {
                result.EnsureSuccess(command);
            }

            return new RebaseResult(RebaseOutcome.Stopped, null, await CountConflictedFilesAsync(cancellationToken).ConfigureAwait(false));
        }

        result.EnsureSuccess(command);
        if (SaysUpToDate(result))
        {
            return new RebaseResult(RebaseOutcome.UpToDate, null, 0);
        }

        // A rebase that finished and left conflicted files can only have had them from putting
        // back the user's stashed changes (rebase.autoStash). Told by the state, not the words:
        // Git 2.36 says "Applying autostash resulted in conflicts", 2.55 "Your local changes are
        // stashed, however applying them resulted in conflicts", over several lines.
        var newTip = await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false);
        var conflicted = await CountConflictedFilesAsync(cancellationToken).ConfigureAwait(false);
        return new RebaseResult(RebaseOutcome.Rebased, newTip, conflicted) { AutostashConflicted = conflicted > 0 };

        // "f.txt: needs merge" / "You must edit all merge conflicts and then mark them as resolved
        // using git add": git rebase --continue with unresolved files changed nothing (0.1).
        static bool RefusedToContinue(GitResult result) => Says(result, "You must edit all merge conflicts");

        static bool Says(GitResult result, string text) =>
            result.StandardOutput.Contains(text, StringComparison.Ordinal) || result.StandardError.Contains(text, StringComparison.Ordinal);

        // Git's own line on standard output, whole: "Current branch <b> is up to date." or, while
        // detached, "HEAD is up to date.". Not the words anywhere: git also prints commit subjects
        // ("Applying: <subject>" under the apply backend, "dropping <sha> <subject> -- patch
        // contents already upstream"), and a subject can say "is up to date" (the review's finding 11).
        static bool SaysUpToDate(GitResult result) =>
            result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(line => line == "HEAD is up to date."
                    || (line.StartsWith("Current branch ", StringComparison.Ordinal) && line.EndsWith(" is up to date.", StringComparison.Ordinal)));
    }

    /// <summary>The number of conflicted paths in the index: the distinct paths of <c>git ls-files --unmerged -z</c> (one line per stage).</summary>
    private async Task<int> CountConflictedFilesAsync(CancellationToken cancellationToken) =>
        (await ReadConflictedPathsAsync(cancellationToken).ConfigureAwait(false)).Count;

    /// <summary>Whether the repository stores its refs in the reftable format (null: not asked yet), asked once per <see cref="GitRepository"/> (8.1).</summary>
    private bool? _usesReftable;

    /// <summary>
    /// Reads a pseudo-ref that tells a pick or revert in progress, <c>CHERRY_PICK_HEAD</c> or
    /// <c>REVERT_HEAD</c> (docs/phase-4-design.md 8.1, D128, D146): the one place that reads them.
    /// First the file <c>&lt;git folder&gt;/&lt;name&gt;</c>, as git writes it under the files ref
    /// format. When there is none, and only while <c>MERGE_MSG</c> or <c>sequencer/</c> exists
    /// (every stopped pick or revert writes one of them, 0.3) in a repository whose
    /// <c>extensions.refStorage</c> is <c>reftable</c> (Git 2.45+, where these are refs), through
    /// git: <c>git rev-parse --verify --quiet --symbolic-full-name &lt;name&gt;</c> must print
    /// exactly <paramref name="name"/> (or nothing, with git's <c>ambiguous</c> warning, when a
    /// branch of that name exists too), so that a branch named <c>CHERRY_PICK_HEAD</c> alone does
    /// not answer; then <c>git rev-parse --verify --quiet &lt;name&gt;</c> gives its commit. The
    /// ref format is read lazily with <c>git config --get extensions.refStorage</c> and kept: a
    /// files repository with nothing in progress makes no git call. Returns the full SHA, or null.
    /// </summary>
    internal async Task<string?> ReadPseudoRefAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (FirstLine(ReadGitFile(name)) is { } fromFile)
        {
            return fromFile;
        }

        if (!File.Exists(Path.Combine(GitDirectory, "MERGE_MSG")) && !Directory.Exists(Path.Combine(GitDirectory, "sequencer")))
        {
            return null;
        }

        if (!await UsesReftableAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var symbolic = Read("rev-parse", "--verify", "--quiet", "--symbolic-full-name", name);
        var named = await _runner.RunAsync(symbolic, cancellationToken).ConfigureAwait(false);
        var printed = named.StandardOutput.Trim();
        var isPseudoRef = named.Succeeded
            && (string.Equals(printed, name, StringComparison.Ordinal)
                || (printed.Length == 0 && named.StandardError.Contains("ambiguous", StringComparison.Ordinal)));
        return isPseudoRef ? await ResolveAsync(name, cancellationToken).ConfigureAwait(false) : null;
    }

    /// <summary>Whether <c>extensions.refStorage</c> is <c>reftable</c>: <c>git config --get</c>, exit 1 when unset (the files format).</summary>
    private async Task<bool> UsesReftableAsync(CancellationToken cancellationToken)
    {
        if (_usesReftable is { } known)
        {
            return known;
        }

        var command = Read("config", "--get", "extensions.refStorage");
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        var reftable = result.ExitCode switch
        {
            0 => string.Equals(result.StandardOutput.Trim(), "reftable", StringComparison.OrdinalIgnoreCase),
            1 => false,
            _ => throw new GitException(command.DisplayText, result.ExitCode, result.StandardError, standardOutput: result.StandardOutput),
        };
        _usesReftable = reftable;
        return reftable;
    }

    private static void RequirePrefix(string fullName, string prefix, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName, parameterName);
        if (!fullName.StartsWith(prefix, StringComparison.Ordinal) || fullName.Length == prefix.Length)
        {
            throw new ArgumentException($"A full ref name under {prefix} is needed, not '{fullName}'.", parameterName);
        }
    }
}
