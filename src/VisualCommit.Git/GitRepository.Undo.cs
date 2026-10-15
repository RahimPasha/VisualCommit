using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Undo;
using static VisualCommit.Core.Undo.UndoEffect;

namespace VisualCommit.Git;

/// <summary>
/// The undo history's reads and steps (docs/phase-5-design.md 7.3, 7.5, 7.7, 0.4, 0.5, D167, D169,
/// D170): what a recorded action's scope names, read before and after it, and every effect of
/// <see cref="UndoEffect"/> with its check, run in a step's order.
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>The status a stash's checks compare whole (0.5): every untracked file, with the index's object ids.</summary>
    private static readonly string[] UndoStatusArguments = ["status", "--porcelain=v2", "-z", "--untracked-files=all"];

    /// <summary>How long the paths of one git call may be, together, before they go to another call (a command line ends near 32,760 characters on Windows, 0.6).</summary>
    private const int UndoPathsPerCall = 24_000;

    public async Task<UndoState> ReadRefStatesAsync(UndoScope scope, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        // Reads that do not depend on one another, run together; none writes but the files' blobs.
        var headTask = ReadUndoHeadAsync(cancellationToken);
        var refsTask = ReadUndoRefsAsync(scope, cancellationToken);
        var stashesTask = scope.StashList ? ReadUndoStashesAsync(cancellationToken) : Task.FromResult<IReadOnlyList<UndoStash>>([]);
        var configTask = scope.Config.Count > 0 ? ReadUndoConfigAsync(scope, cancellationToken) : Task.FromResult<IReadOnlyList<ConfigEntry>>([]);
        var statusTask = scope.Status ? ReadOutputAsync(cancellationToken, UndoStatusArguments) : Task.FromResult(string.Empty);
        var filesTask = ReadUndoFilesAsync(scope, cancellationToken);
        await Task.WhenAll(headTask, refsTask, stashesTask, configTask, statusTask, filesTask).ConfigureAwait(false);

        var (refs, symbolicRefs, checkedOut) = await refsTask.ConfigureAwait(false);
        var (files, entries) = await filesTask.ConfigureAwait(false);
        return new UndoState
        {
            Head = await headTask.ConfigureAwait(false),
            Refs = refs,
            SymbolicRefs = symbolicRefs,
            CheckedOutIn = checkedOut,
            Stashes = await stashesTask.ConfigureAwait(false),
            Config = await configTask.ConfigureAwait(false),
            Files = files,
            IndexEntries = entries,
            Status = scope.Status ? await statusTask.ConfigureAwait(false) : null,
            Gitignore = scope.Gitignore ? ReadUndoGitignore() : null,
        };
    }

    public async Task<UndoStepResult> RunUndoStepAsync(UndoStep step, NetworkOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(options);
        var run = new UndoStepRun(this, step, options, cancellationToken);
        var result = await run.RunAsync().ConfigureAwait(false);
        var verb = step.Direction == UndoDirection.Undo ? "undo" : "redo";
        _log.Info(result.Refusal is { } refusal
            ? $"Refused the {verb} of {step.Entry.Text}: {refusal.Kind}."
            : $"Did the {verb} of {step.Entry.Text}.");
        return result;
    }

    // ---- The reads (7.7 steps 2 and 4, 7.5 step 2) ----------------------------------------------

    /// <summary>HEAD with one call (<c>rev-parse HEAD --symbolic-full-name HEAD</c>), or <c>symbolic-ref -q HEAD</c> while the branch is unborn.</summary>
    private async Task<HeadState> ReadUndoHeadAsync(CancellationToken cancellationToken)
    {
        var command = Read("rev-parse", "HEAD", "--symbolic-full-name", "HEAD");
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
        {
            var lines = SplitLines(result.StandardOutput);
            var name = lines.Count > 1 ? lines[1] : "HEAD";
            return new HeadState(name.StartsWith(HeadsPrefix, StringComparison.Ordinal) ? name[HeadsPrefix.Length..] : null, lines[0]);
        }

        // Unborn: HEAD names a branch that has no commit yet.
        var headRef = await ReadHeadRefAsync(cancellationToken).ConfigureAwait(false);
        if (headRef is null || await ReadHeadShaAsync(cancellationToken).ConfigureAwait(false) is not null)
        {
            result.EnsureSuccess(command);
        }

        return new HeadState(headRef!.StartsWith(HeadsPrefix, StringComparison.Ordinal) ? headRef[HeadsPrefix.Length..] : headRef, null);
    }

    /// <summary>
    /// The scope's refs with one <c>for-each-ref</c>, and the worktree each branch is checked out in
    /// (<c>%(worktreepath)</c>). A pattern also matches the refs below it (<c>refs/heads/x</c> matches
    /// <c>refs/heads/x/y</c>), so exact names are filtered here. A symbolic ref
    /// (<c>refs/remotes/&lt;r&gt;/HEAD</c>) has no object of its own: it is kept apart with the ref it
    /// points at (<c>%(symref)</c>), which <c>git remote remove</c> deletes with the rest.
    /// </summary>
    private async Task<(IReadOnlyDictionary<string, string> Refs, IReadOnlyDictionary<string, string> SymbolicRefs, IReadOnlyDictionary<string, string> CheckedOut)> ReadUndoRefsAsync(UndoScope scope, CancellationToken cancellationToken)
    {
        var refs = new Dictionary<string, string>(StringComparer.Ordinal);
        var symbolicRefs = new Dictionary<string, string>(StringComparer.Ordinal);
        var checkedOut = new Dictionary<string, string>(StringComparer.Ordinal);
        var patterns = scope.Refs.Concat(scope.RefPrefixes.Select(prefix => prefix.TrimEnd('/'))).Distinct(StringComparer.Ordinal).ToList();
        if (patterns.Count == 0)
        {
            return (refs, symbolicRefs, checkedOut);
        }

        var output = await ReadOutputAsync(
            cancellationToken,
            ["for-each-ref", "--format=%(refname)%1f%(objectname)%1f%(worktreepath)%1f%(symref)", .. patterns]).ConfigureAwait(false);
        foreach (var line in SplitLines(output))
        {
            var fields = line.Split('\x1f');
            if (fields.Length < 4 || !scope.Covers(fields[0]))
            {
                continue;
            }

            if (fields[3].Length > 0)
            {
                symbolicRefs[fields[0]] = fields[3];
                continue;
            }

            refs[fields[0]] = fields[1];
            if (fields[2].Length > 0)
            {
                checkedOut[fields[0]] = NormalizePath(fields[2], WorkingDirectory);
            }
        }

        return (refs, symbolicRefs, checkedOut);
    }

    /// <summary>The stash list, newest first: each stash's commit and its message as the list shows it (<c>%gs</c>).</summary>
    private async Task<IReadOnlyList<UndoStash>> ReadUndoStashesAsync(CancellationToken cancellationToken)
    {
        var output = await ReadOutputAsync(cancellationToken, "stash", "list", "--no-show-signature", "--format=%H%x1f%gs").ConfigureAwait(false);
        return [.. SplitLines(output).Select(line =>
        {
            var separator = line.IndexOf('\x1f', StringComparison.Ordinal);
            return separator < 0 ? new UndoStash(line, string.Empty) : new UndoStash(line[..separator], line[(separator + 1)..]);
        })];
    }

    /// <summary>
    /// The repository's own keys that the scope names, in the order git lists them:
    /// <c>config --local --null --list</c> prints <c>&lt;key&gt;\n&lt;value&gt;\0</c> per key, the
    /// key's section and name in lower case, its subsection as written.
    /// </summary>
    private async Task<IReadOnlyList<ConfigEntry>> ReadUndoConfigAsync(UndoScope scope, CancellationToken cancellationToken)
    {
        var output = await ReadOutputAsync(cancellationToken, "config", "--local", "--null", "--list").ConfigureAwait(false);
        var entries = new List<ConfigEntry>();
        foreach (var item in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            // A key without a value ("[x] flag") has no line feed.
            var newline = item.IndexOf('\n', StringComparison.Ordinal);
            var entry = newline < 0 ? new ConfigEntry(item, string.Empty) : new ConfigEntry(item[..newline], item[(newline + 1)..]);
            if (scope.Covers(entry))
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>
    /// The scope's files and, for an apply or a pop, the stash's (its changed paths and its
    /// untracked ones): their raw bytes kept as blobs, their SHA-256 and their index entries.
    /// </summary>
    private async Task<(IReadOnlyList<RecordedFile> Files, IReadOnlyList<string> Entries)> ReadUndoFilesAsync(UndoScope scope, CancellationToken cancellationToken)
    {
        var paths = new List<string>(scope.Files);
        if (scope.FilesOfStash is { } stash)
        {
            paths.AddRange(await ReadUndoStashPathsAsync(stash, cancellationToken).ConfigureAwait(false));
        }

        paths = [.. paths.Distinct(StringComparer.Ordinal)];
        if (paths.Count == 0)
        {
            return ([], []);
        }

        var files = RecordUndoFilesAsync(paths, cancellationToken);
        var entries = ReadUndoIndexEntriesAsync(paths, cancellationToken);
        await Task.WhenAll(files, entries).ConfigureAwait(false);
        return (await files.ConfigureAwait(false), await entries.ConfigureAwait(false));
    }

    /// <summary>
    /// The paths a stash holds, in byte order: what its working-tree commit and its index commit
    /// change against its base, and its untracked files (its third parent), each name of a rename too.
    /// </summary>
    private async Task<IReadOnlyList<string>> ReadUndoStashPathsAsync(string stash, CancellationToken cancellationToken)
    {
        var tracked = ReadOutputAsync(cancellationToken, "diff-tree", "-r", "-z", "--no-renames", "--name-only", stash + "^1", stash);
        var staged = ReadOutputAsync(cancellationToken, "diff-tree", "-r", "-z", "--no-renames", "--name-only", stash + "^1", stash + "^2");
        var untracked = await ResolveAsync(stash + "^3", cancellationToken).ConfigureAwait(false);
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in SplitNul(await tracked.ConfigureAwait(false)).Concat(SplitNul(await staged.ConfigureAwait(false))))
        {
            paths.Add(path);
        }

        if (untracked is not null)
        {
            paths.UnionWith(SplitNul(await ReadOutputAsync(cancellationToken, "ls-tree", "-r", "-z", "--name-only", untracked).ConfigureAwait(false)));
        }

        return [.. paths];
    }

    /// <summary>
    /// Each path's raw bytes kept as a blob (<c>hash-object -w --no-filters</c>, 0.5: written back
    /// byte for byte, whatever <c>core.autocrlf</c> says) and their SHA-256, or absent.
    /// </summary>
    internal async Task<IReadOnlyList<RecordedFile>> RecordUndoFilesAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var full = WorkingTreePath(path);
            if (File.Exists(full))
            {
                hashes[path] = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(full, cancellationToken).ConfigureAwait(false)));
            }
        }

        var blobs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var chunk in UndoPathChunks([.. paths.Where(hashes.ContainsKey)]))
        {
            var command = new GitCommand(["hash-object", "-w", "--no-filters", "--", .. chunk]) { WorkingDirectory = WorkingDirectory };
            var ids = SplitLines((await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command).StandardOutput);
            for (var i = 0; i < chunk.Count && i < ids.Count; i++)
            {
                blobs[chunk[i]] = ids[i];
            }
        }

        return [.. paths.Select(path => blobs.TryGetValue(path, out var blob)
            ? new RecordedFile(path, blob, hashes[path])
            : new RecordedFile(path, null, null))];
    }

    /// <summary>The paths' index entries as <c>ls-files --stage -z</c> prints them (unmerged stages included), without the NUL; a path names itself only, not what lies below it.</summary>
    private async Task<IReadOnlyList<string>> ReadUndoIndexEntriesAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var wanted = paths.ToHashSet(StringComparer.Ordinal);
        var entries = new List<string>();
        foreach (var chunk in UndoPathChunks(paths))
        {
            var output = await ReadOutputAsync(cancellationToken, ["ls-files", "--stage", "-z", "--", .. chunk]).ConfigureAwait(false);
            entries.AddRange(SplitNul(output).Where(entry => wanted.Contains(UndoChecks.IndexEntryPath(entry))));
        }

        return entries;
    }

    private GitignoreState ReadUndoGitignore()
    {
        var path = WorkingTreePath(".gitignore");
        if (!File.Exists(path))
        {
            return new GitignoreState(Exists: false, Length: 0, Sha256: null);
        }

        var bytes = File.ReadAllBytes(path);
        return new GitignoreState(Exists: true, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    /// <summary>Paths split so that each git call's command line stays short enough.</summary>
    private static IEnumerable<IReadOnlyList<string>> UndoPathChunks(IReadOnlyList<string> paths)
    {
        var chunk = new List<string>();
        var length = 0;
        foreach (var path in paths)
        {
            if (chunk.Count > 0 && length + path.Length > UndoPathsPerCall)
            {
                yield return chunk;
                chunk = [];
                length = 0;
            }

            chunk.Add(path);
            length += path.Length + 3;
        }

        if (chunk.Count > 0)
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// The paths of <paramref name="paths"/> that git ignores: <c>check-ignore --stdin -z</c>, which
    /// leaves tracked files out and exits 1 when it ignores none. Only paths that exist are asked
    /// about: a pattern matches a path whether it exists or not.
    /// </summary>
    private async Task<IReadOnlyList<string>> IgnoredAmongAsync(IReadOnlyCollection<string> paths, CancellationToken cancellationToken)
    {
        if (paths.Count == 0)
        {
            return [];
        }

        var command = new GitCommand("check-ignore", "--stdin", "-z")
        {
            WorkingDirectory = WorkingDirectory,

            // check-ignore refuses literal pathspecs, and takes each path as it is anyway.
            Environment = new Dictionary<string, string?>(ReadEnvironment) { ["GIT_LITERAL_PATHSPECS"] = null },
            StandardInput = string.Concat(paths.Select(path => path + "\0")),
        };
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1)
        {
            return [];
        }

        return SplitNul(result.EnsureSuccess(command).StandardOutput);
    }

    /// <summary>
    /// One undo or redo step inside its operation (7.5): the refusals that need nothing written, the
    /// effects applied in the step's order and groups, what was applied reversed when a later
    /// effect refuses, and the entry rewritten with the objects the step made.
    /// </summary>
    private sealed partial class UndoStepRun
    {
        /// <summary>The mode of an <c>--index-info</c> line that removes a path's entries.</summary>
        private const string RemovedMode = "0";

        private readonly GitRepository _repository;
        private readonly UndoStep _step;
        private readonly NetworkOptions _options;
        private readonly CancellationToken _cancellationToken;

        /// <summary>The effects in the order they are applied, with their place in the step: what another machine can refuse first (7.5 step 4).</summary>
        private readonly List<(int Index, UndoEffect Effect)> _order;

        /// <summary>What was applied, in order, for the reversal: an effect, or one transaction of ref writes.</summary>
        private readonly List<Applied> _applied = [];

        /// <summary>Ref writes waiting for the step's transaction: written when a later effect needs them, or at the end.</summary>
        private readonly List<UndoEffect> _pendingRefs = [];

        /// <summary>Configuration and <c>.gitignore</c> writes, applied after the transaction (7.5 step 4).</summary>
        private readonly List<UndoEffect> _deferred = [];

        /// <summary>The effects (by their place in the step) whose checks only git can make were made before anything was written.</summary>
        private readonly HashSet<int> _checked = [];

        /// <summary>The backup refs the step saved (an undone tag creation's, a redone hard reset's).</summary>
        private readonly List<string> _newBackups = [];

        private UndoState _now = null!;

        /// <summary>Putting back what a refused step applied, which a cancelled operation must still finish.</summary>
        private bool _reversing;

        /// <summary>The stash a <see cref="StashPush"/> of the step made, and whether a later effect popped it.</summary>
        private string? _madeStash;
        private bool _madeStashPopped;

        /// <summary>A redone hard reset's snapshot and backups.</summary>
        private (string? Snapshot, BackupResult Backup)? _hardReset;

        public UndoStepRun(GitRepository repository, UndoStep step, NetworkOptions options, CancellationToken cancellationToken)
        {
            _repository = repository;
            _step = step;
            _options = options;
            _cancellationToken = cancellationToken;
            var indexed = step.Effects.Select((effect, index) => (index, effect)).ToList();
            _order = [.. indexed.Where(item => item.effect is RemoteRef), .. indexed.Where(item => item.effect is not RemoteRef)];
        }

        /// <summary>The token of the step's calls; none while what a refused or cancelled step applied is put back.</summary>
        private CancellationToken Token => _reversing ? CancellationToken.None : _cancellationToken;

        /// <summary>The reflog message of every ref the step writes (7.3): <c>VisualCommit: undo &lt;text&gt;</c> or <c>VisualCommit: redo &lt;text&gt;</c>.</summary>
        private string Message => $"VisualCommit: {(_step.Direction == UndoDirection.Undo ? "undo" : "redo")} {_step.Entry.Text}";

        /// <summary>The reflog message of a write that puts back what a refused step had applied.</summary>
        private string PutBackMessage => $"VisualCommit: put back after a refused {(_step.Direction == UndoDirection.Undo ? "undo" : "redo")} of {_step.Entry.Text}";

        public async Task<UndoStepResult> RunAsync()
        {
            // 1. The app's own check first: update-ref refuses nothing during a merge, a rebase or a pick (0.4).
            var status = await _repository.ReadStatusAsync(Token).ConfigureAwait(false);
            if (status.InProgress.IsActive)
            {
                return Refused(new UndoRefusal(UndoRefusalKind.InProgress));
            }

            if (ConflictsInTheWay(status) is { } conflicts)
            {
                return Refused(conflicts);
            }

            // 2 and 3. What the effects check, then the simulation of the whole step.
            _now = await _repository.ReadRefStatesAsync(UndoChecks.ScopeOf(_step), _options, Token).ConfigureAwait(false);
            var refusal = UndoChecks.FirstRefusal(_step, _now)
                ?? await MissingObjectsAsync().ConfigureAwait(false)
                ?? await UpFrontRefusalAsync().ConfigureAwait(false);
            if (refusal is not null)
            {
                return Refused(refusal);
            }

            // 4. The effects, reversed when a later one refuses.
            try
            {
                if (await ApplyAllAsync().ConfigureAwait(false) is { } late)
                {
                    await ReverseAsync().ConfigureAwait(false);
                    return Refused(late);
                }
            }
            catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                await ReverseAsync().ConfigureAwait(false);
                throw;
            }

            // 5. The entry takes what the step made.
            return new UndoStepResult(null, await RewriteEntryAsync().ConfigureAwait(false));
        }

        private UndoStepResult Refused(UndoRefusal refusal) => new(refusal, _step.Entry);

        /// <summary>
        /// Conflicts without an operation in progress refuse the step (7.4), except for the undo of an
        /// apply or a pop whose recorded files hold every conflicted one: it puts them back as they were.
        /// </summary>
        private UndoRefusal? ConflictsInTheWay(WorkingTreeStatus status)
        {
            var conflicted = status.Unstaged.Where(file => file.Kind == FileChangeKind.Conflicted).Select(file => file.Path).ToList();
            if (conflicted.Count == 0)
            {
                return null;
            }

            var entry = _step.Entry;
            var recorded = entry.Before.Files.Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
            var putBack = _step.Direction == UndoDirection.Undo
                && entry.Kind is UndoKind.ApplyStash or UndoKind.PopStash
                && conflicted.All(recorded.Contains);
            return putBack ? null : new UndoRefusal(UndoRefusalKind.Conflicts, Paths: conflicted);
        }

        /// <summary>
        /// The objects the step writes or points refs at, looked up with one <c>cat-file
        /// --batch-check</c>: a commit gone after an outside <c>gc</c> (7.6's "the commits no longer
        /// exist"), or a recorded file's blob. A submodule's commit is not in this repository.
        /// </summary>
        private async Task<UndoRefusal?> MissingObjectsAsync()
        {
            var objects = new List<string>();
            foreach (var effect in _step.Effects)
            {
                switch (effect)
                {
                    case MoveHead { To: { } to }:
                        objects.Add(to);
                        break;
                    case MoveDetachedHead move:
                        objects.Add(move.To);
                        break;
                    case SetRef { To: { } to }:
                        objects.Add(to);
                        break;
                    case Switch { To: { BranchName: null, Sha: { } sha } }:
                        objects.Add(sha);
                        break;
                    case IndexTo index:
                        objects.Add(index.Source);
                        objects.Add(index.From);
                        break;
                    case StashStore store:
                        objects.Add(store.Sha);
                        break;
                    case TagRef { Present: true } tag:
                        objects.Add(tag.Object);
                        break;
                    case KeepTag keep:
                        objects.Add(keep.Object);
                        break;
                    case RestoreSnapshot restore:
                        objects.Add(restore.Snapshot.Commit);
                        break;
                    case FilesTo write:
                        objects.AddRange(write.To.Select(file => file.Blob).OfType<string>());
                        break;
                    case IndexEntries entries:
                        objects.AddRange(entries.To.Select(UndoIndexEntryObject).OfType<string>());
                        break;
                    case HardReset reset:
                        objects.Add(reset.To);
                        break;
                    case RemoteRef { To: { } to }:
                        objects.Add(to);
                        break;
                }
            }

            if (objects.Count == 0)
            {
                return null;
            }

            var command = new GitCommand("cat-file", "--batch-check")
            {
                WorkingDirectory = _repository.WorkingDirectory,
                Environment = ReadEnvironment,
                StandardInput = string.Concat(objects.Distinct(StringComparer.Ordinal).Select(id => id + "\n")),
            };
            var output = (await _repository._runner.RunAsync(command, Token).ConfigureAwait(false)).EnsureSuccess(command).StandardOutput;
            return SplitLines(output).Any(line => line.EndsWith(" missing", StringComparison.Ordinal))
                ? new UndoRefusal(UndoRefusalKind.CommitsGone)
                : null;
        }

        /// <summary>
        /// The checks only git can make, before anything is written, for every effect whose state
        /// the earlier effects do not change: until the first effect that changes HEAD, the index or
        /// the files. One case beyond it: the index a hard reset's undo puts back is checked after
        /// its Keep move, which carries the index; it then holds the move's target exactly when it
        /// held the commit moved from before. The others are made at their place.
        /// </summary>
        private async Task<UndoRefusal?> UpFrontRefusalAsync()
        {
            var predictable = true;
            (string From, string To)? kept = null;
            foreach (var (index, effect) in _order)
            {
                if (predictable || effect is GitignoreBytes)
                {
                    if (await GitCheckAsync(effect).ConfigureAwait(false) is { } refusal)
                    {
                        return refusal;
                    }

                    _checked.Add(index);
                }
                else if (effect is IndexTo move && kept is { } keep && string.Equals(move.From, keep.To, StringComparison.Ordinal))
                {
                    if (!await IndexHoldsAsync(keep.From).ConfigureAwait(false))
                    {
                        return new UndoRefusal(UndoRefusalKind.IndexChanged);
                    }

                    _checked.Add(index);
                }

                if (ChangesWorkingTree(effect))
                {
                    kept = predictable ? KeepMove(effect) : null;
                    predictable = false;
                }
            }

            return null;
        }

        private async Task<UndoRefusal?> GitCheckAsync(UndoEffect effect) => effect switch
        {
            MoveHead { Mode: MoveMode.Keep } move => await IgnoredInTheWayAsync(Required(move.From), Required(move.To)).ConfigureAwait(false),
            MoveDetachedHead { Mode: MoveMode.Keep } move => await IgnoredInTheWayAsync(move.From, move.To).ConfigureAwait(false),
            IndexTo index => await IndexHoldsAsync(index.From).ConfigureAwait(false) ? null : new UndoRefusal(UndoRefusalKind.IndexChanged),
            IndexEntries entries => await FlagsAsync(entries.Paths).ConfigureAwait(false),
            StashPop { Sha: { } sha } => await StashUntrackedInTheWayAsync(sha).ConfigureAwait(false),
            GitignoreBytes => new FileInfo(_repository.WorkingTreePath(".gitignore")).LinkTarget is not null
                ? new UndoRefusal(UndoRefusalKind.FilesChanged, Paths: [".gitignore"])
                : null,
            _ => null,
        };

        private static bool ChangesWorkingTree(UndoEffect effect) => effect is Switch or StashPush or StashPop or IndexTo or FilesTo or RestoreSnapshot
            or IndexEntries or HardReset or GitignoreBytes or MoveHead { Mode: MoveMode.Keep } or MoveDetachedHead { Mode: MoveMode.Keep };

        private static (string From, string To)? KeepMove(UndoEffect effect) => effect switch
        {
            MoveHead { Mode: MoveMode.Keep, From: { } from, To: { } to } => (from, to),
            MoveDetachedHead { Mode: MoveMode.Keep } move => (move.From, move.To),
            _ => null,
        };

        /// <summary>Whether the index holds <paramref name="commit"/>'s tree: <c>diff-index --cached --quiet</c> exits 0 when it does, 1 when not.</summary>
        private async Task<bool> IndexHoldsAsync(string commit)
        {
            var command = Read(["diff-index", "--cached", "--quiet", commit, "--"]);
            var result = await _repository._runner.RunAsync(command, Token).ConfigureAwait(false);
            return result.ExitCode switch
            {
                0 => true,
                1 => false,
                _ => throw new GitException(command.DisplayText, result.ExitCode, result.StandardError, standardOutput: result.StandardOutput),
            };
        }

        /// <summary>
        /// The ignored-files guard of a Keep move (7.3, D169): <c>read-tree -m -u</c> overwrites an
        /// ignored file where the target adds one, deletes an ignored folder where it adds a file,
        /// and an ignored file where it adds a folder, without a word, on every git from 2.34
        /// (0.4). So the paths the move adds (<c>diff-tree --diff-filter=A</c>), the files inside a
        /// folder at one of them and the parent folders that are files on disk are asked about.
        /// </summary>
        private async Task<UndoRefusal?> IgnoredInTheWayAsync(string from, string to)
        {
            var added = SplitNul(await ReadAsync("diff-tree", "-r", "-z", "--no-renames", "--name-only", "--diff-filter=A", from, to).ConfigureAwait(false));
            var candidates = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var path in added)
            {
                candidates.UnionWith(ParentFolders(path).Where(parent => IsFileOrLink(_repository.WorkingTreePath(parent))));

                var full = _repository.WorkingTreePath(path);
                if (IsFileOrLink(full))
                {
                    candidates.Add(path);
                }
                else if (Directory.Exists(full))
                {
                    candidates.UnionWith(FilesIn(path));
                }
            }

            var ignored = await _repository.IgnoredAmongAsync(candidates, Token).ConfigureAwait(false);
            return ignored.Count > 0 ? new UndoRefusal(UndoRefusalKind.IgnoredFilesInTheWay, Paths: ignored) : null;
        }

        /// <summary>
        /// The files under a folder of the working tree, as repository paths in byte order: what
        /// git would lose with it. A symbolic link is a file to git, also one to a folder, and is
        /// not followed.
        /// </summary>
        private List<string> FilesIn(string folder)
        {
            var files = new List<string>();
            var folders = new Stack<string>([folder]);
            while (folders.TryPop(out var relative))
            {
                foreach (var entry in new DirectoryInfo(_repository.WorkingTreePath(relative)).EnumerateFileSystemInfos("*", new EnumerationOptions { AttributesToSkip = 0 }))
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

        /// <summary>
        /// Index entries that <c>update-index --index-info</c> cannot write back with their flags (0.5):
        /// a skip-worktree entry (<c>ls-files -t</c> tags it <c>S</c>) and an intent-to-add one
        /// (the status shows it added in the working tree, <c>.A</c>).
        /// </summary>
        private async Task<UndoRefusal?> FlagsAsync(IReadOnlyList<string> paths)
        {
            var flagged = new SortedSet<string>(StringComparer.Ordinal);
            var wanted = paths.ToHashSet(StringComparer.Ordinal);
            foreach (var chunk in UndoPathChunks(paths))
            {
                var tags = ReadAsync(["ls-files", "-t", "-z", "--", .. chunk]);
                var status = ReadAsync(["status", "--porcelain=v2", "-z", "--untracked-files=no", "--", .. chunk]);
                await Task.WhenAll(tags, status).ConfigureAwait(false);
                flagged.UnionWith(SplitNul(await tags.ConfigureAwait(false)).Where(entry => entry.StartsWith("S ", StringComparison.Ordinal)).Select(entry => entry[2..]));
                flagged.UnionWith(SplitNul(await status.ConfigureAwait(false))
                    .Where(record => record.StartsWith("1 .A ", StringComparison.Ordinal))
                    .Select(record => record.Split(' ', 9)[^1]));
            }

            flagged.IntersectWith(wanted);
            return flagged.Count > 0 ? new UndoRefusal(UndoRefusalKind.FlagNotRestorable, Paths: [.. flagged]) : null;
        }

        /// <summary>
        /// A stash's untracked files that exist again: <c>stash pop</c> would refuse for them only
        /// after restoring the others (0.5). Ignored ones, which the status does not show, included.
        /// </summary>
        private async Task<UndoRefusal?> StashUntrackedInTheWayAsync(string stash)
        {
            if (await _repository.ResolveAsync(stash + "^3", Token).ConfigureAwait(false) is not { } untracked)
            {
                return null;
            }

            var inTheWay = SplitNul(await ReadAsync("ls-tree", "-r", "-z", "--name-only", untracked).ConfigureAwait(false))
                .Where(path => IsFileOrLink(_repository.WorkingTreePath(path)) || Directory.Exists(_repository.WorkingTreePath(path)))
                .ToList();
            return inTheWay.Count > 0 ? new UndoRefusal(UndoRefusalKind.StatusChanged, Paths: inTheWay) : null;
        }

        // ---- Applying (7.5 step 4) ---------------------------------------------------------------

        /// <summary>
        /// The effects in the step's order, the remote's first. Ref writes wait for one
        /// <c>update-ref --stdin</c> transaction, written when a later effect needs one of them and
        /// otherwise at the end; configuration and <c>.gitignore</c> writes follow it; everything
        /// else (what git can refuse locally, and the index and file writes) runs at its place.
        /// </summary>
        private async Task<UndoRefusal?> ApplyAllAsync()
        {
            foreach (var (index, effect) in _order)
            {
                if (effect is SetRef or TagRef or MoveHead { Mode: MoveMode.Soft } or MoveDetachedHead { Mode: MoveMode.Soft })
                {
                    if (_pendingRefs.Any(pending => RefOf(pending) == RefOf(effect)) && await FlushAsync().ConfigureAwait(false) is { } clash)
                    {
                        return clash;
                    }

                    _pendingRefs.Add(effect);
                    continue;
                }

                if (effect is RemoteConfig removal && RemovesRemote(removal)
                    && await DeleteRefsLeftUnderAsync($"{RemotesPrefix}{removal.Remote}/", index).ConfigureAwait(false) is { } left)
                {
                    return left;
                }

                if (effect is BranchConfig or RemoteConfig or UndoEffect.RemoteUrl or GitignoreBytes)
                {
                    _deferred.Add(effect);
                    continue;
                }

                if (NeedsPendingRefs(effect) && await FlushAsync().ConfigureAwait(false) is { } flushed)
                {
                    return flushed;
                }

                var refusal = (_checked.Contains(index) ? null : await GitCheckAsync(effect).ConfigureAwait(false))
                    ?? await ApplyAsync(effect, reversing: false).ConfigureAwait(false);
                if (refusal is not null)
                {
                    return refusal;
                }
            }

            if (await FlushAsync().ConfigureAwait(false) is { } last)
            {
                return last;
            }

            foreach (var effect in _deferred)
            {
                await ApplyAsync(effect, reversing: false).ConfigureAwait(false);
            }

            return null;
        }

        /// <summary>Whether a remote's keys are removed: its <c>To</c> holds none of them.</summary>
        private static bool RemovesRemote(RemoteConfig keys) => !keys.To.Any(ConfigScope.Remote(keys.Remote).Covers);

        /// <summary>
        /// A removed remote's refs that the step's own effects before <paramref name="index"/> do
        /// not write, deleted as <c>git remote remove</c> deletes them (a fetch after an added remote
        /// made them, its entry does not hold them): the symbolic ones at once, the others in the
        /// step's transaction, each verified at the value read.
        /// </summary>
        private async Task<UndoRefusal?> DeleteRefsLeftUnderAsync(string prefix, int index)
        {
            var written = _order
                .TakeWhile(item => item.Index != index)
                .Select(item => item.Effect switch { SetRef set => set.Ref, SymbolicRef symbolic => symbolic.Ref, _ => null })
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);
            foreach (var (name, target) in _now.SymbolicRefs.Where(item => item.Key.StartsWith(prefix, StringComparison.Ordinal) && !written.Contains(item.Key)))
            {
                if (await ApplyAsync(new SymbolicRef(name, target, null), reversing: false).ConfigureAwait(false) is { } refusal)
                {
                    return refusal;
                }
            }

            _pendingRefs.AddRange(_now.Refs
                .Where(item => item.Key.StartsWith(prefix, StringComparison.Ordinal) && !written.Contains(item.Key))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new SetRef(item.Key, item.Value, null)));
            return null;
        }

        /// <summary>
        /// Whether the effect needs the waiting ref writes first: a switch to a branch the step
        /// creates, a move or a rename of a branch it sets, anything that starts from HEAD's commit
        /// after a soft move of it, a symbolic ref pointed at a ref the step creates.
        /// </summary>
        private bool NeedsPendingRefs(UndoEffect effect)
        {
            if (_pendingRefs.Count == 0)
            {
                return false;
            }

            var pending = _pendingRefs.Select(RefOf).ToHashSet(StringComparer.Ordinal);
            var movesHead = _pendingRefs.Any(write => write is MoveHead or MoveDetachedHead);
            return effect switch
            {
                Switch change => movesHead || new[] { change.From.BranchName, change.To.BranchName }.OfType<string>().Any(branch => pending.Contains(HeadsPrefix + branch)),
                MoveHead move => movesHead || pending.Contains(HeadsPrefix + move.Branch),
                MoveDetachedHead => true,
                UndoEffect.RenameBranch rename => pending.Contains(HeadsPrefix + rename.From) || pending.Contains(HeadsPrefix + rename.To),
                StashPush or StashPop or HardReset => movesHead,
                RemoteRename rename => pending.Any(name => name.StartsWith($"{RemotesPrefix}{rename.From}/", StringComparison.Ordinal)),
                SymbolicRef { To: { } target } => pending.Contains(target),
                _ => false,
            };
        }

        private static string RefOf(UndoEffect effect) => effect switch
        {
            SetRef set => set.Ref,
            TagRef tag => TagsPrefix + tag.Name,
            MoveHead move => HeadsPrefix + move.Branch,
            MoveDetachedHead => "HEAD",
            _ => string.Empty,
        };

        /// <summary>Writes the waiting ref writes in one transaction, all or none (0.4).</summary>
        private async Task<UndoRefusal?> FlushAsync()
        {
            if (_pendingRefs.Count == 0)
            {
                return null;
            }

            var batch = _pendingRefs.ToList();
            _pendingRefs.Clear();
            var refusal = await TransactionAsync(batch, Message, reversing: false).ConfigureAwait(false);
            if (refusal is null)
            {
                _applied.Add(new Applied(null, batch));
            }

            return refusal;
        }

        /// <summary>
        /// <c>update-ref -m &lt;message&gt; --stdin</c> with a <c>create</c>, <c>update</c> or
        /// <c>delete</c> line per write, each with the value expected now, so that every write
        /// verifies its ref (a detached HEAD's with <c>option no-deref</c>). A ref that is not where
        /// the step expected it (a race with a terminal) refuses the step with what git found.
        /// </summary>
        private async Task<UndoRefusal?> TransactionAsync(IReadOnlyList<UndoEffect> writes, string message, bool reversing)
        {
            var input = new StringBuilder();
            var createReflog = false;
            foreach (var write in writes)
            {
                switch (write)
                {
                    case SetRef set:
                        AppendRefLine(input, set.Ref, set.From, set.To);
                        createReflog |= set.CreateReflog;
                        break;
                    case TagRef tag:
                        AppendRefLine(input, TagsPrefix + tag.Name, tag.Present ? null : tag.Object, tag.Present ? tag.Object : null);
                        break;
                    case MoveHead move:
                        AppendRefLine(input, HeadsPrefix + move.Branch, move.From, move.To);
                        break;
                    case MoveDetachedHead move:
                        input.Append("option no-deref\n");
                        AppendRefLine(input, "HEAD", move.From, move.To);
                        break;
                }
            }

            // --create-reflog gives a deleted branch brought back a reflog (0.5); it applies to
            // the whole transaction, in which such a branch is the only write.
            string[] arguments = createReflog ? ["update-ref", "-m", message, "--create-reflog", "--stdin"] : ["update-ref", "-m", message, "--stdin"];
            var command = new GitCommand(arguments)
            {
                WorkingDirectory = _repository.WorkingDirectory,
                StandardInput = input.ToString(),
                RecordsStandardInput = true,
            };
            var result = await _repository._runner.RunAsync(command, Token).ConfigureAwait(false);
            if (result.Succeeded)
            {
                return null;
            }

            if (!reversing && LockRefusal(result.StandardError) is { } refusal)
            {
                return refusal;
            }

            result.EnsureSuccess(command);
            return null;
        }

        private static void AppendRefLine(StringBuilder input, string fullName, string? from, string? to)
        {
            if (from is null)
            {
                input.Append("create ").Append(fullName).Append(' ').Append(to).Append('\n');
            }
            else if (to is null)
            {
                input.Append("delete ").Append(fullName).Append(' ').Append(from).Append('\n');
            }
            else
            {
                input.Append("update ").Append(fullName).Append(' ').Append(to).Append(' ').Append(from).Append('\n');
            }
        }

        /// <summary>
        /// What git's refusal of a compare-and-swap says (0.4): <c>cannot lock ref '&lt;ref&gt;': is at
        /// &lt;sha&gt; but expected &lt;sha&gt;</c>, <c>… reference already exists</c> or
        /// <c>… unable to resolve reference '&lt;ref&gt;'</c>.
        /// </summary>
        private static UndoRefusal? LockRefusal(string standardError)
        {
            var match = LockRefusalPattern().Match(standardError);
            if (!match.Success)
            {
                return null;
            }

            var fullName = match.Groups["ref"].Value;
            var exists = match.Groups["exists"].Success;
            var at = match.Groups["at"].Success ? match.Groups["at"].Value : null;
            if (fullName.StartsWith(HeadsPrefix, StringComparison.Ordinal))
            {
                var branch = fullName[HeadsPrefix.Length..];
                return exists
                    ? new UndoRefusal(UndoRefusalKind.BranchExists, Name: branch, GitOutput: standardError.Trim())
                    : new UndoRefusal(UndoRefusalKind.BranchMoved, Name: branch, Sha: at, GitOutput: standardError.Trim());
            }

            return exists && fullName.StartsWith(TagsPrefix, StringComparison.Ordinal)
                ? new UndoRefusal(UndoRefusalKind.TagExists, Name: fullName[TagsPrefix.Length..], GitOutput: standardError.Trim())
                : new UndoRefusal(UndoRefusalKind.RefChanged, Name: fullName, GitOutput: standardError.Trim());
        }

        [GeneratedRegex(@"cannot lock ref '(?<ref>[^']+)': (?:is at (?<at>[0-9a-f]+) but expected|(?<exists>reference already exists)|unable to resolve reference)")]
        private static partial Regex LockRefusalPattern();

        /// <summary>Applies one effect that runs at its place, or puts one back (<paramref name="reversing"/>: no refusal is expected then, and git's failure throws).</summary>
        private async Task<UndoRefusal?> ApplyAsync(UndoEffect effect, bool reversing)
        {
            var refusal = effect switch
            {
                MoveHead { Mode: MoveMode.Keep } move => await KeepMoveAsync(move.Branch, Required(move.From), Required(move.To), reversing).ConfigureAwait(false),
                MoveDetachedHead { Mode: MoveMode.Keep } move => await KeepMoveAsync(null, move.From, move.To, reversing).ConfigureAwait(false),
                Switch change => await SwitchAsync(change, reversing).ConfigureAwait(false),
                UndoEffect.RenameBranch rename => await RenameAsync(rename, reversing).ConfigureAwait(false),
                SetRef or TagRef or MoveHead or MoveDetachedHead => await TransactionAsync([effect], reversing ? PutBackMessage : Message, reversing).ConfigureAwait(false),
                SymbolicRef symbolic => await SymbolicRefAsync(symbolic, reversing ? PutBackMessage : Message).ConfigureAwait(false),
                _ => await ApplyWriteAsync(effect).ConfigureAwait(false),
            };
            if (refusal is null && !reversing)
            {
                _applied.Add(new Applied(effect, null));
            }

            return refusal;
        }

        /// <summary>The effects git does not refuse once their checks passed: the remote's (whose lease is its check), the index, the files, the stash, the configuration.</summary>
        private async Task<UndoRefusal?> ApplyWriteAsync(UndoEffect effect)
        {
            switch (effect)
            {
                case RemoteRef remote:
                    return await PushRemoteRefAsync(remote).ConfigureAwait(false);
                case IndexTo index:
                    await WriteAsync("restore", "--staged", $"--source={index.Source}", "--", ".").ConfigureAwait(false);
                    break;
                case FilesTo write:
                    await WriteFilesAsync(write.To).ConfigureAwait(false);
                    break;
                case RestoreSnapshot restore:
                    await _repository.RestoreSnapshotAsync(restore.Snapshot, Token).ConfigureAwait(false);
                    break;
                case StashPush push:
                    _madeStash = await _repository.StashAsync(push.Message, push.IncludeUntracked, _options, Token).ConfigureAwait(false);
                    _madeStashPopped = false;
                    break;
                case StashPop pop:
                    await PopAsync(pop).ConfigureAwait(false);
                    break;
                case StashStore store:
                    await StashWriteAsync("stash", "store", "-m", store.Message, store.Sha).ConfigureAwait(false);
                    break;
                case StashDrop drop:
                    await _repository.DropStashAsync(drop.Sha, Token).ConfigureAwait(false);
                    break;
                case IndexEntries entries:
                    await WriteIndexEntriesAsync(entries).ConfigureAwait(false);
                    break;
                case KeepTag keep:
                    var backup = await _repository.SaveBackupRefsAsync(new BackupRequest("tag-undone", [new BackupEntry(keep.Name, keep.Object)]), Token).ConfigureAwait(false);
                    _newBackups.AddRange(backup.Refs);
                    break;
                case HardReset reset:
                    await HardResetAsync(reset).ConfigureAwait(false);
                    break;
                case BranchConfig keys:
                    await WriteKeysAsync($"branch.{keys.Branch}", keys.From, keys.To).ConfigureAwait(false);
                    break;
                case RemoteConfig keys:
                    await WriteRemoteKeysAsync(keys).ConfigureAwait(false);
                    break;
                case RemoteRename rename:
                    await WriteAsync("remote", "rename", rename.From, rename.To).ConfigureAwait(false);
                    break;
                case UndoEffect.RemoteUrl url:
                    await _repository.SetRemoteUrlAsync(url.Remote, url.To, Token).ConfigureAwait(false);
                    break;
                case GitignoreBytes gitignore:
                    await WriteGitignoreAsync(gitignore).ConfigureAwait(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), effect, null);
            }

            return null;
        }

        /// <summary>
        /// A Keep move (7.3, D169): <c>update-index -q --refresh</c> (git's own checkout refreshes
        /// first; without it an unchanged file with stale stat data refuses), then
        /// <c>read-tree -m -u &lt;from&gt; &lt;to&gt;</c>, which keeps staged, partly staged and
        /// unstaged work and refuses, changing nothing, when a file the move changes has local
        /// changes, then the compare-and-swap on <c>refs/heads/&lt;b&gt;</c> (or the detached HEAD).
        /// When that fails (a race), <c>read-tree -m -u &lt;to&gt; &lt;from&gt;</c> puts the files back exactly.
        /// </summary>
        private async Task<UndoRefusal?> KeepMoveAsync(string? branch, string from, string to, bool reversing)
        {
            var refresh = Write(["update-index", "-q", "--refresh"]);
            await _repository._runner.RunAsync(refresh, Token).ConfigureAwait(false);

            var readTree = Write(["read-tree", "-m", "-u", from, to]);
            var merged = await _repository._runner.RunAsync(readTree, Token).ConfigureAwait(false);
            if (!merged.Succeeded)
            {
                if (!reversing && TwoWayMergeRefusalPattern().IsMatch(merged.StandardError))
                {
                    return new UndoRefusal(
                        UndoRefusalKind.LocalChangesInTheWay,
                        Paths: await PathsInTheWayAsync(from, to, merged.StandardError).ConfigureAwait(false),
                        GitOutput: merged.StandardError.Trim());
                }

                merged.EnsureSuccess(readTree);
            }

            var message = reversing ? PutBackMessage : Message;
            string[] update = branch is null
                ? ["update-ref", "-m", message, "--no-deref", "HEAD", to, from]
                : ["update-ref", "-m", message, HeadsPrefix + branch, to, from];
            var updated = await _repository._runner.RunAsync(Write(update), Token).ConfigureAwait(false);
            if (updated.Succeeded)
            {
                return null;
            }

            var back = Write(["read-tree", "-m", "-u", to, from]);
            (await _repository._runner.RunAsync(back, Token).ConfigureAwait(false)).EnsureSuccess(back);
            if (reversing)
            {
                updated.EnsureSuccess(Write(update));
            }

            return branch is null
                ? new UndoRefusal(UndoRefusalKind.HeadNotAtCommit, Sha: from, GitOutput: updated.StandardError.Trim())
                : new UndoRefusal(
                    UndoRefusalKind.BranchMoved,
                    Name: branch,
                    Sha: await _repository.ResolveAsync(HeadsPrefix + branch, Token).ConfigureAwait(false),
                    GitOutput: updated.StandardError.Trim());
        }

        /// <summary>
        /// read-tree's refusals of a two-way merge (0.4), the last one for a folder of untracked files
        /// where the move adds a file: <c>error: Updating 'd' would lose untracked files in it</c>.
        /// </summary>
        [GeneratedRegex(@"not uptodate\. Cannot merge|would be overwritten by merge|would be removed by merge|would lose untracked files in it")]
        private static partial Regex TwoWayMergeRefusalPattern();

        /// <summary>
        /// The paths in a move's way, counted by the app (7.6): read-tree names only the first. The
        /// paths the move changes that the status shows changed or untracked, one where a folder
        /// holds changed or untracked files, and an untracked file where the move puts a folder
        /// (once, however many paths the move puts in it); git's own path when that finds none.
        /// </summary>
        private async Task<IReadOnlyList<string>> PathsInTheWayAsync(string from, string to, string gitOutput)
        {
            var changed = ReadAsync("diff-tree", "-r", "-z", "--no-renames", "--name-only", from, to);
            var status = _repository.ReadPlainStatusAsync(Token);
            await Task.WhenAll(changed, status).ConfigureAwait(false);
            var touched = new HashSet<string>(StringComparer.Ordinal);
            var read = await status.ConfigureAwait(false);
            foreach (var file in read.Staged.Concat(read.Unstaged))
            {
                touched.Add(file.Path);
                if (file.OldPath is { } old)
                {
                    touched.Add(old);
                }
            }

            var folders = new HashSet<string>(touched.SelectMany(ParentFolders), StringComparer.Ordinal);
            var paths = new List<string>();
            var counted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in SplitNul(await changed.ConfigureAwait(false)))
            {
                var inTheWay = touched.Contains(path) || folders.Contains(path) ? path : ParentFolders(path).FirstOrDefault(touched.Contains);
                if (inTheWay is not null && counted.Add(inTheWay))
                {
                    paths.Add(inTheWay);
                }
            }

            if (paths.Count == 0 && GitPathPattern().Match(gitOutput) is { Success: true } named)
            {
                paths.Add(named.Groups["path"].Value);
            }

            return paths;
        }

        /// <summary>The folders a repository path lies in, outermost first: <c>a</c> and <c>a/b</c> for <c>a/b/c.txt</c>.</summary>
        private static IEnumerable<string> ParentFolders(string path)
        {
            for (var slash = path.IndexOf('/', StringComparison.Ordinal); slash > 0; slash = path.IndexOf('/', slash + 1))
            {
                yield return path[..slash];
            }
        }

        [GeneratedRegex(@"'(?<path>[^']+)'")]
        private static partial Regex GitPathPattern();

        /// <summary>
        /// <c>switch --no-guess --no-overwrite-ignore &lt;b&gt;</c>, or <c>switch --detach
        /// --no-overwrite-ignore &lt;sha&gt;</c> (7.3): it carries the user's work or refuses with the
        /// checkout texts, listing every path; the option keeps it from overwriting ignored files. Its
        /// reflog message is git's own, so that <c>@{-1}</c> keeps working.
        /// </summary>
        private async Task<UndoRefusal?> SwitchAsync(Switch change, bool reversing)
        {
            string[] arguments = change.To.BranchName is { } branch
                ? ["switch", "--no-guess", "--no-overwrite-ignore", .. Positional(branch)]
                : ["switch", "--detach", "--no-overwrite-ignore", .. Positional(Required(change.To.Sha))];
            var command = _repository.WriteCommand(_options, arguments);
            var result = await _repository._runner.RunAsync(command, Token).ConfigureAwait(false);
            if (result.Succeeded || reversing)
            {
                result.EnsureSuccess(command);
                return null;
            }

            var blocked = BlockedPaths(result.StandardError);
            if (blocked.Count == 0)
            {
                result.EnsureSuccess(command);
            }

            // A folder git names ("would lose untracked files in them") is asked about by its files,
            // which check-ignore matches and the folder's own name may not (*.log in logs/): it is
            // in the way as ignored files only when every file in it is ignored.
            var files = blocked.ToDictionary(
                path => path,
                path => !IsFileOrLink(_repository.WorkingTreePath(path)) && Directory.Exists(_repository.WorkingTreePath(path)) ? FilesIn(path) : [path],
                StringComparer.Ordinal);
            var ignored = (await _repository.IgnoredAmongAsync([.. files.Values.SelectMany(inside => inside)], Token).ConfigureAwait(false)).ToHashSet(StringComparer.Ordinal);
            var local = blocked.Where(path => files[path].Count == 0 || !files[path].All(ignored.Contains)).ToList();
            return local.Count > 0
                ? new UndoRefusal(UndoRefusalKind.LocalChangesInTheWay, Paths: local, GitOutput: result.StandardError.Trim())
                : new UndoRefusal(UndoRefusalKind.IgnoredFilesInTheWay, Paths: [.. blocked.SelectMany(path => files[path])], GitOutput: result.StandardError.Trim());
        }

        /// <summary>
        /// The paths of git's checkout refusal (0.7): each tab-indented line under <c>error: Your local
        /// changes to the following files would be overwritten by checkout:</c>, <c>error: The
        /// following untracked working tree files would be overwritten by checkout:</c> or
        /// <c>error: Updating the following directories would lose untracked files in them:</c>.
        /// </summary>
        internal static IReadOnlyList<string> BlockedPaths(string standardError)
        {
            var paths = new List<string>();
            var inBlock = false;
            foreach (var raw in standardError.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.StartsWith("error: ", StringComparison.Ordinal) && line.EndsWith(':'))
                {
                    inBlock = line.Contains("would be overwritten by", StringComparison.Ordinal) || line.Contains("would lose untracked files", StringComparison.Ordinal);
                }
                else if (inBlock && line.StartsWith('\t'))
                {
                    paths.Add(line[1..]);
                }
                else
                {
                    inBlock = false;
                }
            }

            return [.. paths.Distinct(StringComparer.Ordinal)];
        }

        /// <summary>
        /// A symbolic ref pointed at its target (<c>symbolic-ref -m</c>), or deleted itself rather
        /// than the ref it points at (<c>update-ref --no-deref -d</c>, which also takes one whose
        /// target is gone). Its check was the simulation's: git refuses neither.
        /// </summary>
        private async Task<UndoRefusal?> SymbolicRefAsync(SymbolicRef symbolic, string message)
        {
            if (symbolic.To is { } target)
            {
                await WriteAsync("symbolic-ref", "-m", message, symbolic.Ref, target).ConfigureAwait(false);
            }
            else
            {
                await WriteAsync("update-ref", "-m", message, "--no-deref", "-d", symbolic.Ref).ConfigureAwait(false);
            }

            return null;
        }

        private async Task<UndoRefusal?> RenameAsync(UndoEffect.RenameBranch rename, bool reversing)
        {
            var command = _repository.WriteCommand(null, ["branch", "--move", .. Positional(rename.From, rename.To)]);
            var result = await _repository._runner.RunAsync(command, Token).ConfigureAwait(false);
            if (!result.Succeeded && !reversing && result.StandardError.Contains("already exists", StringComparison.Ordinal))
            {
                return new UndoRefusal(UndoRefusalKind.BranchExists, Name: rename.To, GitOutput: result.StandardError.Trim());
            }

            result.EnsureSuccess(command);
            return null;
        }

        /// <summary>
        /// <c>push --progress --force-with-lease=&lt;ref&gt;:&lt;from, or empty&gt; &lt;r&gt;
        /// &lt;to&gt;:&lt;ref&gt;</c>, or <c>--delete &lt;ref&gt;</c> (0.5, D170): the lease says what
        /// the remote must hold now, explicitly, both ways; the remote-tracking ref follows. A lease
        /// that fails is <c>(stale info)</c>; <c>Everything up-to-date</c> means the remote already
        /// holds the value: done.
        /// </summary>
        private async Task<UndoRefusal?> PushRemoteRefAsync(RemoteRef remote)
        {
            RemoteArguments.RequireRemote(remote.Remote);
            var lease = $"--force-with-lease={remote.Ref}:{remote.From}";
            string[] arguments = remote.To is { } to
                ? ["push", "--progress", lease, remote.Remote, $"{to}:{remote.Ref}"]
                : ["push", "--progress", lease, remote.Remote, "--delete", remote.Ref];
            var command = _repository.RemoteCommand(arguments, _options);
            var result = await _repository._runner.RunAsync(command, Token).ConfigureAwait(false);
            if (result.Succeeded)
            {
                return null;
            }

            if (result.StandardError.Contains("(stale info)", StringComparison.Ordinal))
            {
                return new UndoRefusal(UndoRefusalKind.RemoteChanged, Name: ShortRefName(remote.Ref), GitOutput: result.StandardError.Trim(), Remote: remote.Remote);
            }

            result.EnsureSuccess(command);
            return null;
        }

        /// <summary>A remote ref's short name as git's push summary shows it: <c>main</c> for <c>refs/heads/main</c>, <c>v1</c> for <c>refs/tags/v1</c>.</summary>
        private static string ShortRefName(string fullName) =>
            fullName.StartsWith(HeadsPrefix, StringComparison.Ordinal) ? fullName[HeadsPrefix.Length..]
            : fullName.StartsWith(TagsPrefix, StringComparison.Ordinal) ? fullName[TagsPrefix.Length..]
            : fullName;

        /// <summary>
        /// <c>stash pop --index stash@{n}</c>, found by its commit: the recorded one, or the one the
        /// step's <see cref="StashPush"/> made (none when it found nothing to stash). The checks made
        /// sure it applies onto the state it was made from.
        /// </summary>
        private async Task PopAsync(StashPop pop)
        {
            var sha = pop.Sha ?? _madeStash;
            if (sha is null)
            {
                return;
            }

            var selector = await _repository.StashSelectorAsync(sha, Token).ConfigureAwait(false);
            await StashWriteAsync("stash", "pop", "--index", selector).ConfigureAwait(false);
            if (string.Equals(sha, _madeStash, StringComparison.Ordinal))
            {
                _madeStashPopped = true;
            }
        }

        private async Task StashWriteAsync(params string[] arguments)
        {
            var command = _repository.StashCommand(_options, literalPathspecs: true, arguments);
            (await _repository._runner.RunAsync(command, Token).ConfigureAwait(false)).EnsureSuccess(command);
        }

        /// <summary>Each file's recorded bytes written back (<c>cat-file blob</c>), or the file deleted with the folders it leaves empty when it was absent (7.3).</summary>
        private async Task WriteFilesAsync(IReadOnlyList<RecordedFile> files)
        {
            foreach (var file in files)
            {
                if (file.Blob is not { } blob)
                {
                    _repository.DeleteWorkingFile(file.Path);
                    continue;
                }

                var read = new GitCommand("cat-file", "blob", blob)
                {
                    WorkingDirectory = _repository.WorkingDirectory,
                    Environment = ReadEnvironment,
                    Encoding = GitCommand.Latin1,
                };
                var content = (await _repository._runner.RunAsync(read, Token).ConfigureAwait(false)).EnsureSuccess(read).StandardOutput;
                var path = _repository.WorkingTreePath(file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, GitCommand.Latin1.GetBytes(content), Token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// <c>update-index -z --index-info</c> (0.5): per path a line that removes its entries
        /// (<c>0 &lt;zeros&gt; 0\t&lt;path&gt;</c>), then its recorded entries, unmerged stages included.
        /// </summary>
        private Task WriteIndexEntriesAsync(IndexEntries entries)
        {
            var zeros = new string('0', (_now.Head.Sha ?? entries.To.Select(UndoIndexEntryObject).OfType<string>().FirstOrDefault())?.Length ?? 40);
            var byPath = entries.To.ToLookup(UndoChecks.IndexEntryPath, StringComparer.Ordinal);
            var input = new StringBuilder();
            foreach (var path in entries.Paths)
            {
                input.Append(RemovedMode).Append(' ').Append(zeros).Append(" 0\t").Append(path).Append('\0');
                foreach (var entry in byPath[path])
                {
                    input.Append(entry).Append('\0');
                }
            }

            return WriteWithInputAsync(input.ToString(), "update-index", "-z", "--index-info");
        }

        /// <summary>
        /// A branch's or a remote's keys: the section removed when it had keys, then the keys of
        /// <paramref name="to"/> added in order (0.5: meaning unchanged, the section moves to the end
        /// of the file and the names are lower-cased).
        /// </summary>
        private async Task WriteKeysAsync(string section, IReadOnlyList<ConfigEntry> from, IReadOnlyList<ConfigEntry> to)
        {
            if (from.Count > 0)
            {
                await WriteAsync("config", "--local", "--remove-section", section).ConfigureAwait(false);
            }

            foreach (var key in to)
            {
                await WriteAsync("config", "--local", "--add", key.Key, key.Value).ConfigureAwait(false);
            }
        }

        /// <summary>A remote's keys (<c>remote.&lt;r&gt;.*</c>), and <c>remote.pushDefault</c> when one side has it naming the remote.</summary>
        private async Task WriteRemoteKeysAsync(RemoteConfig keys)
        {
            static bool IsPushDefault(ConfigEntry key) => string.Equals(key.Key, "remote.pushdefault", StringComparison.OrdinalIgnoreCase);
            await WriteKeysAsync($"remote.{keys.Remote}", [.. keys.From.Where(key => !IsPushDefault(key))], [.. keys.To.Where(key => !IsPushDefault(key))]).ConfigureAwait(false);
            if (keys.To.Any(IsPushDefault))
            {
                await WriteAsync("config", "--local", "remote.pushDefault", keys.Remote).ConfigureAwait(false);
            }
            else if (keys.From.Any(IsPushDefault))
            {
                await WriteAsync("config", "--local", "--unset", "remote.pushDefault").ConfigureAwait(false);
            }
        }

        /// <summary>The app's append to <c>.gitignore</c> done again, or undone by cutting the file back to its old length (deleting a file the append created).</summary>
        private async Task WriteGitignoreAsync(GitignoreBytes gitignore)
        {
            var path = _repository.WorkingTreePath(".gitignore");
            if (!gitignore.To.Exists)
            {
                File.Delete(path);
                return;
            }

            if (gitignore.From.Exists && gitignore.To.Length <= gitignore.From.Length)
            {
                await using var cut = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);
                cut.SetLength(gitignore.To.Length);
                return;
            }

            await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            await stream.WriteAsync(GitCommand.Latin1.GetBytes(gitignore.Appended), Token).ConfigureAwait(false);
        }

        /// <summary>
        /// A hard reset's redo (7.3): the app's own hard reset, with a new snapshot of what it
        /// discards now and its <c>reset-&lt;time&gt;</c> backup, as <c>ResetAsync</c>'s operation saves them.
        /// </summary>
        private async Task HardResetAsync(HardReset reset)
        {
            var snapshot = await _repository.SnapshotForHardResetAsync(reset.To, Token).ConfigureAwait(false);
            var backup = await _repository.SaveBackupRefsAsync(new BackupRequest("reset", [new BackupEntry(null, reset.From)]) { ChangesCommit = snapshot }, Token).ConfigureAwait(false);
            _hardReset = (snapshot, backup);
            _newBackups.AddRange(backup.Refs);
            await _repository.ResetAsync(reset.To, ResetMode.Hard, _options, Token).ConfigureAwait(false);
        }

        // ---- Reversing what a refused step applied (7.5 step 4) ------------------------------------

        /// <summary>
        /// Puts back what was applied, newest first, each with its inverse and without checks: the
        /// values were read and written within this step. A failure is logged and the rest goes on.
        /// </summary>
        private async Task ReverseAsync()
        {
            _reversing = true;
            for (var i = _applied.Count - 1; i >= 0; i--)
            {
                var applied = _applied[i];
                try
                {
                    if (applied.Batch is { } batch)
                    {
                        await TransactionAsync([.. batch.Select(Inverse).OfType<UndoEffect>()], PutBackMessage, reversing: true).ConfigureAwait(false);
                    }
                    else if (applied.Effect is KeepTag)
                    {
                        foreach (var backup in _newBackups.ToList())
                        {
                            await WriteAsync("update-ref", "-d", backup).ConfigureAwait(false);
                            _newBackups.Remove(backup);
                        }
                    }
                    else if (applied.Effect is { } effect && Inverse(effect) is { } inverse)
                    {
                        await ApplyAsync(inverse, reversing: true).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    _repository._log.Error($"Could not put back {applied.Effect?.GetType().Name ?? "a ref transaction"} after a refused step of {_step.Entry.Text}.", ex);
                }
            }

            _applied.Clear();
        }

        /// <summary>
        /// The effect that takes <paramref name="effect"/> back. A stash popped is stashed again
        /// (its commit is then a new one), and a stash the step made is popped. A hard reset has no
        /// inverse: it is a step of its own, after which nothing can refuse.
        /// </summary>
        private UndoEffect? Inverse(UndoEffect effect) => effect switch
        {
            MoveHead move => move with { From = move.To, To = move.From },
            MoveDetachedHead move => move with { From = move.To, To = move.From },
            SetRef set => new SetRef(set.Ref, set.To, set.From),
            SymbolicRef symbolic => symbolic with { From = symbolic.To, To = symbolic.From },
            TagRef tag => tag with { Present = !tag.Present },
            Switch change => new Switch(change.To, change.From),
            UndoEffect.RenameBranch rename => new UndoEffect.RenameBranch(rename.To, rename.From),
            BranchConfig keys => keys with { From = keys.To, To = keys.From },
            RemoteConfig keys => keys with { From = keys.To, To = keys.From },
            RemoteRename rename => new RemoteRename(rename.To, rename.From),
            UndoEffect.RemoteUrl url => url with { From = url.To, To = url.From },
            IndexTo index => new IndexTo(index.From, index.Source),
            FilesTo write => new FilesTo(write.To, write.From),
            RestoreSnapshot restore => new FilesTo([], restore.From),
            StashPush => new StashPop(null, null),
            StashPop => new StashPush(IncludeUntracked: true, Message: null, Status: null),
            StashStore store => new StashDrop(store.Sha),
            StashDrop drop => new StashStore(drop.Sha, _now.Stashes.FirstOrDefault(stash => string.Equals(stash.Sha, drop.Sha, StringComparison.Ordinal))?.Message ?? string.Empty),
            IndexEntries entries => entries with { From = entries.To, To = entries.From },
            GitignoreBytes gitignore => gitignore with { From = gitignore.To, To = gitignore.From },
            RemoteRef remote => remote with { From = remote.To, To = remote.From },
            _ => null,
        };

        // ---- The entry rewritten (7.5 step 5) -------------------------------------------------------

        /// <summary>
        /// The entry with what the step made: a redo's stash (its commit, and its place in the list
        /// after the action), a redo's hard-reset snapshot (with the files the reset left now), the
        /// backups it saved.
        /// </summary>
        private async Task<UndoEntry> RewriteEntryAsync()
        {
            var entry = _step.Entry;
            var captured = entry.Captured;
            var after = entry.After;
            if (_madeStash is { } stash && !_madeStashPopped)
            {
                var listed = await _repository.ReadUndoStashesAsync(Token).ConfigureAwait(false);
                var made = listed.FirstOrDefault(item => string.Equals(item.Sha, stash, StringComparison.Ordinal)) ?? new UndoStash(stash, string.Empty);
                var old = captured.Stash;
                captured = captured with { Stash = stash };
                after = after with
                {
                    Stashes = after.Stashes.Any(item => string.Equals(item.Sha, old, StringComparison.Ordinal))
                        ? [.. after.Stashes.Select(item => string.Equals(item.Sha, old, StringComparison.Ordinal) ? made : item)]
                        : [made, .. after.Stashes],
                };
            }

            if (_hardReset is { } reset)
            {
                (captured, after) = await RewriteHardResetAsync(captured, after, reset.Snapshot, reset.Backup).ConfigureAwait(false);
            }

            if (_newBackups.Count > 0)
            {
                captured = captured with { Backups = [.. captured.Backups.Union(_newBackups, StringComparer.Ordinal)] };
            }

            return entry with { Captured = captured, After = after };
        }

        /// <summary>A redone hard reset's new snapshot as its undo needs it (7.3): the snapshot, its index (second parent), its files, and the bytes the reset left at its paths.</summary>
        private async Task<(UndoCaptured Captured, UndoState After)> RewriteHardResetAsync(UndoCaptured captured, UndoState after, string? snapshot, BackupResult backup)
        {
            if (snapshot is null)
            {
                return (captured with { ResetSnapshot = null, ResetIndex = null, Snapshot = null, ChangedFiles = [] }, after);
            }

            var files = await _repository.ReadUndoSnapshotAsync(backup.Refs[^1], snapshot, Token).ConfigureAwait(false);
            var index = await _repository.ResolveAsync(snapshot + "^2", Token).ConfigureAwait(false);
            var recorded = await _repository.RecordUndoFilesAsync(files.Paths, Token).ConfigureAwait(false);
            var paths = files.Paths.ToHashSet(StringComparer.Ordinal);
            return (
                captured with { ResetSnapshot = snapshot, ResetIndex = index, Snapshot = files, ChangedFiles = files.Paths },
                after with { Files = [.. after.Files.Where(file => !paths.Contains(file.Path)), .. recorded] });
        }

        // ---- Calls -----------------------------------------------------------------------------------

        private GitCommand Read(string[] arguments) => _repository.Read(arguments);

        private GitCommand Write(string[] arguments) => new(arguments) { WorkingDirectory = _repository.WorkingDirectory };

        private Task<string> ReadAsync(params string[] arguments) => _repository.ReadOutputAsync(Token, arguments);

        private Task<string> WriteAsync(params string[] arguments) => _repository.WriteAsync(Token, arguments);

        private async Task WriteWithInputAsync(string input, params string[] arguments)
        {
            var command = new GitCommand(arguments) { WorkingDirectory = _repository.WorkingDirectory, StandardInput = input };
            (await _repository._runner.RunAsync(command, Token).ConfigureAwait(false)).EnsureSuccess(command);
        }

        private static string Required(string? value) =>
            value ?? throw new InvalidOperationException("A Keep move needs both of its commits; only a soft move creates or deletes its branch.");

        /// <summary>What was applied: one effect, or the effects of one ref transaction.</summary>
        private sealed record Applied(UndoEffect? Effect, IReadOnlyList<UndoEffect>? Batch);
    }

    /// <summary>The object of an index entry as <c>ls-files --stage</c> prints it (<c>&lt;mode&gt; &lt;object&gt; &lt;stage&gt;\t&lt;path&gt;</c>), or null for a submodule's commit, which lies in another repository.</summary>
    private static string? UndoIndexEntryObject(string entry)
    {
        var fields = entry.Split(' ', 3);
        return fields.Length < 3 || fields[0] == "160000" ? null : fields[1];
    }

    /// <summary>
    /// A snapshot commit as a <see cref="DiscardSnapshot"/> (D67): its paths from its message's
    /// body, one per line after the subject, and those it holds (the others were gone from the
    /// working tree) from its tree.
    /// </summary>
    internal async Task<DiscardSnapshot> ReadUndoSnapshotAsync(string refName, string commit, CancellationToken cancellationToken)
    {
        var message = ReadOutputAsync(cancellationToken, "log", "-1", "--no-show-signature", "--format=%B", commit, "--");
        var tree = ReadOutputAsync(cancellationToken, "ls-tree", "-r", "-z", "--name-only", commit);
        await Task.WhenAll(message, tree).ConfigureAwait(false);
        var lines = (await message.ConfigureAwait(false)).Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var paths = lines.Skip(1).SkipWhile(line => line.Length == 0).Where(line => line.Length > 0).ToList();
        var inTree = SplitNul(await tree.ConfigureAwait(false)).ToHashSet(StringComparer.Ordinal);
        return new DiscardSnapshot(refName, commit, paths, new HashSet<string>(paths.Where(inTree.Contains), StringComparer.Ordinal));
    }
}
