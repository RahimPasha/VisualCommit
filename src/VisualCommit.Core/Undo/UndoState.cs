using VisualCommit.Core.Git;

namespace VisualCommit.Core.Undo;

/// <summary>
/// What the history reads of a repository just before and just after a recorded action, inside
/// the action's own operation (docs/phase-5-design.md 7.7 steps 2 and 4), for what the action's
/// <see cref="UndoScope"/> names: HEAD always, then the scope's refs, the stash list, config keys,
/// files and index entries, the status and the <c>.gitignore</c>. An entry's sequences
/// (<see cref="UndoSequence"/>) are made from the differences between its two states and what
/// was captured (<see cref="UndoCaptured"/>). <see cref="IGitRepository.ReadRefStatesAsync"/> reads one.
/// </summary>
public sealed record UndoState
{
    /// <summary>Where HEAD was: its branch (null while detached) and its commit (null while unborn).</summary>
    public required HeadState Head { get; init; }

    /// <summary>
    /// The scope's refs that exist, by full name, with the object each points at as git stores it:
    /// a branch's commit, a tag's own object (an annotated tag's tag object, not the commit it tags).
    /// A ref of the scope that does not exist is absent.
    /// </summary>
    public IReadOnlyDictionary<string, string> Refs { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The scope's symbolic refs (a remote's <c>refs/remotes/&lt;r&gt;/HEAD</c>, which every clone
    /// has and every fetch on Git 2.55 writes, 0.2), by full name, with the full name of the ref
    /// each points at (<c>%(symref)</c>). They have no object of their own and are not in <see cref="Refs"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> SymbolicRefs { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The folder of the worktree each branch of <see cref="Refs"/> is checked out in, when one is (<c>%(worktreepath)</c>); branches checked out nowhere are absent.</summary>
    public IReadOnlyDictionary<string, string> CheckedOutIn { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The stash list, newest first, when the scope has the stash (<see cref="UndoScope.StashList"/>); empty otherwise.</summary>
    public IReadOnlyList<UndoStash> Stashes { get; init; } = [];

    /// <summary>The repository's own configuration keys that the scope names (<see cref="UndoScope.Config"/>), in the order <c>git config --local --list</c> gives them.</summary>
    public IReadOnlyList<ConfigEntry> Config { get; init; } = [];

    /// <summary>The scope's files in the working tree (<see cref="UndoScope.Files"/>), in the scope's order.</summary>
    public IReadOnlyList<RecordedFile> Files { get; init; } = [];

    /// <summary>The scope's files' index entries as <c>git ls-files --stage -z</c> prints them, one per entry (unmerged stages included), without the NUL.</summary>
    public IReadOnlyList<string> IndexEntries { get; init; } = [];

    /// <summary>
    /// The whole status when the scope asks for it (<see cref="UndoScope.Status"/>), as git prints
    /// it (<c>git status --porcelain=v2 -z --untracked-files=all</c>), compared whole; null otherwise.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>The working tree's <c>.gitignore</c> when the scope has it (<see cref="UndoScope.Gitignore"/>); null otherwise.</summary>
    public GitignoreState? Gitignore { get; init; }

    /// <summary>The object a ref of <see cref="Refs"/> points at, or null when it does not exist.</summary>
    public string? RefTarget(string fullName) => Refs.TryGetValue(fullName, out var target) ? target : null;
}

/// <summary>One key of the repository's own configuration (<c>.git/config</c>).</summary>
/// <param name="Key">The key as <c>git config --list</c> prints it: section and name lower-cased, a subsection as written (<c>branch.feature/X.remote</c>).</param>
/// <param name="Value">Its value.</param>
public sealed record ConfigEntry(string Key, string Value)
{
    /// <summary>The key's section: what comes before its first dot (<c>branch</c>).</summary>
    public string Section => Key[..Math.Max(0, Key.IndexOf('.', StringComparison.Ordinal))];

    /// <summary>The key's subsection (<c>feature/X</c> of <c>branch.feature/X.remote</c>), or null for a key without one (<c>remote.pushdefault</c>).</summary>
    public string? Subsection
    {
        get
        {
            var first = Key.IndexOf('.', StringComparison.Ordinal);
            var last = Key.LastIndexOf('.');
            return first >= 0 && last > first ? Key[(first + 1)..last] : null;
        }
    }
}

/// <summary>
/// One working-tree file as the history recorded it: its SHA-256, which the checks compare with
/// the file on disk, and, where a step may write them back, its raw bytes saved as a blob
/// (<c>git hash-object -w --no-filters</c>), so that they come back byte for byte
/// (<see cref="UndoScope.KeepsFileBytes"/>).
/// </summary>
/// <param name="Path">The repository-relative path, with <c>/</c>.</param>
/// <param name="Blob">
/// The blob that holds the file's bytes; null when the file did not exist, when something other
/// than a file stood at its path (<see cref="InTheWay"/>), or when only its SHA-256 was recorded.
/// </param>
/// <param name="Sha256">
/// The SHA-256 of the file's bytes, upper-case hex, or null when the file did not exist, or
/// <see cref="InTheWay"/> when something other than a file stood at its path.
/// </param>
public sealed record RecordedFile(string Path, string? Blob, string? Sha256)
{
    /// <summary>
    /// The <see cref="Sha256"/> of a path where something other than a file stands
    /// (<see cref="WorkingTreeFiles.IsInTheWayOfFile"/>: a folder holding files, or a file where
    /// one of the path's folders should be; docs/phase-5-design.md 15.9). It equals neither a
    /// file's hash nor absence, so that the checks refuse a step that would write the file there:
    /// git's restore would delete what stands there without a word.
    /// </summary>
    public const string InTheWay = "in the way";

    /// <summary>Whether a file stood at the path: a SHA-256 was recorded, and it is not <see cref="InTheWay"/>. Its bytes may not have been kept (<see cref="Blob"/>).</summary>
    public bool Exists => Sha256 is not null && !string.Equals(Sha256, InTheWay, StringComparison.Ordinal);

    /// <summary>Whether this is the same file content as <paramref name="other"/>: the same path, both absent, both <see cref="InTheWay"/> or both with the same SHA-256, whether or not either kept its bytes.</summary>
    public bool SameContent(RecordedFile other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(Path, other.Path, StringComparison.Ordinal) && string.Equals(Sha256, other.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>One entry of the stash list as the history recorded it: never by its <c>stash@{n}</c>, which renumbering reuses (7.6).</summary>
/// <param name="Sha">The stash commit.</param>
/// <param name="Message">Its message as the list shows it (<c>On main: Work in progress</c>).</param>
public sealed record UndoStash(string Sha, string Message);

/// <summary>The working tree's <c>.gitignore</c> as the history recorded it (0.5, 7.3's <see cref="UndoEffect.GitignoreBytes"/>).</summary>
/// <param name="Exists">The file existed.</param>
/// <param name="Length">Its length in bytes (0 when it did not exist).</param>
/// <param name="Sha256">The SHA-256 of its bytes, upper-case hex, or null when it did not exist.</param>
public sealed record GitignoreState(bool Exists, long Length, string? Sha256);

/// <summary>
/// What the reads before and after an action cannot see, captured while it ran (7.7 step 3,
/// <see cref="UndoCapture"/>): a reset's mode and snapshot, the new stash, a push's remote
/// values, the snapshot a discard saved or a restore restored, the index a restore from the
/// backups list put back, the files whose bytes the after-read records, the bytes appended to
/// <c>.gitignore</c>, and every backup the action saved.
/// </summary>
public sealed record UndoCaptured
{
    /// <summary>Nothing captured.</summary>
    public static UndoCaptured None { get; } = new();

    /// <summary>A reset's mode.</summary>
    public Git.ResetMode? ResetMode { get; init; }

    /// <summary>
    /// A reset's <c>-changes</c> snapshot commit (a hard reset's files and index, a mixed reset's
    /// index), or null when the reset saved none (nothing to save, or the index equalled HEAD). A
    /// hard reset's entry that has one also has its files (<see cref="Snapshot"/>) and, unless the
    /// index had conflicts, <see cref="ResetIndex"/>: its undo needs all three.
    /// </summary>
    public string? ResetSnapshot { get; init; }

    /// <summary>A hard reset's index before it: its snapshot's second parent, or null when the index had conflicts and the snapshot has none.</summary>
    public string? ResetIndex { get; init; }

    /// <summary>
    /// The commit whose tree a restore from the backups list put the index to (7.10): a mixed
    /// reset's snapshot ("Restore the staged changes"), or a hard reset's snapshot's second parent
    /// when "Restore the changes…" put the index back first; null otherwise.
    /// </summary>
    public string? IndexSource { get; init; }

    /// <summary>
    /// The paths of <see cref="IndexSource"/> the restore put back (the report's BD16: a
    /// snapshot's own paths), or null for the whole index.
    /// </summary>
    public IReadOnlyList<string>? IndexPaths { get; init; }

    /// <summary>The stash commit a stash made: <see cref="IGitRepository.StashAsync"/>'s result.</summary>
    public string? Stash { get; init; }

    /// <summary>Whether the stash included untracked files.</summary>
    public bool StashIncludedUntracked { get; init; }

    /// <summary>The message the user gave the stash, or null when git named it.</summary>
    public string? StashMessage { get; init; }

    /// <summary>The remote a push changed, with what it changed there (7.7: never read from the remote-tracking ref).</summary>
    public string? Remote { get; init; }

    /// <summary>The remote refs a push changed, each with its old and new value.</summary>
    public PushOutcome? Push { get; init; }

    /// <summary>
    /// The snapshot a discard saved, the one a restore put back, or a hard reset's <c>-changes</c>
    /// snapshot as its files are put back (its ref, its paths from its message, those present from
    /// its tree).
    /// </summary>
    public DiscardSnapshot? Snapshot { get; init; }

    /// <summary>The paths whose bytes the read after the action records, beyond the scope's own (the files a hard reset changed).</summary>
    public IReadOnlyList<string> ChangedFiles { get; init; } = [];

    /// <summary>The bytes the action appended to <c>.gitignore</c>, one character per byte (D66), which the recorder reads after the append.</summary>
    public string? GitignoreAppended { get; init; }

    /// <summary>The full names of the backup refs the action saved (7.8): the history keeps them from the clean-up while the entry exists.</summary>
    public IReadOnlyList<string> Backups { get; init; } = [];
}
