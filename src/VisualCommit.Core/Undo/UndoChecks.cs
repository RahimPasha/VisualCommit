using VisualCommit.Core.Git;
using static VisualCommit.Core.Undo.UndoEffect;

namespace VisualCommit.Core.Undo;

/// <summary>
/// Whether an undo or redo step can run against a repository's state (docs/phase-5-design.md 7.3's
/// third column, 7.4, 7.5 step 3, 7.6's second table): each effect's check made against the state
/// the effects before it in the step leave, predicted from the state read and the effects'
/// recorded values. The first check that fails gives the refusal the app words (7.6); strict
/// order is the history's (7.4). The app makes the checks that need no git after every refresh
/// (<see cref="FirstRefusalInMemory"/>); the git layer simulates the whole step inside its
/// operation (<see cref="FirstRefusal"/>) and then makes the checks only git can make (the
/// ignored files in a move's way, the index against a commit, the commits' existence, the flags of
/// index entries, a remote's lease) itself, before the first write or, where an earlier effect
/// changes what they look at, at their place.
/// </summary>
public static class UndoChecks
{
    private const string HeadsPrefix = "refs/heads/";
    private const string TagsPrefix = "refs/tags/";
    private const string RemotesPrefix = "refs/remotes/";

    /// <summary>
    /// What a step's operation reads before it changes anything (7.5 step 2): the entry's scope,
    /// and everything the step's effects check: the refs they name (symbolic ones too), the stash
    /// list, the configuration of the branches and remotes they change (every remote's, for
    /// <c>remote.pushDefault</c> and the remotes' names), the files they write, the whole status
    /// for a stash, the <c>.gitignore</c>.
    /// </summary>
    public static UndoScope ScopeOf(UndoStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        var scope = step.Entry.Scope;
        var refs = new List<string>(scope.Refs);
        var config = new List<ConfigScope>(scope.Config);
        var files = new List<string>();
        var stashList = scope.StashList;
        var status = false;
        var gitignore = false;
        foreach (var effect in step.Effects)
        {
            switch (effect)
            {
                case MoveHead move:
                    refs.Add(HeadsPrefix + move.Branch);
                    break;
                case SetRef set:
                    refs.Add(set.Ref);
                    break;
                case SymbolicRef symbolic:
                    refs.Add(symbolic.Ref);
                    break;
                case Switch change:
                    refs.AddRange(new[] { change.From.BranchName, change.To.BranchName }.OfType<string>().Select(branch => HeadsPrefix + branch));
                    break;
                case UndoEffect.RenameBranch rename:
                    refs.Add(HeadsPrefix + rename.From);
                    refs.Add(HeadsPrefix + rename.To);
                    break;
                case BranchConfig keys:
                    config.Add(ConfigScope.Branch(keys.Branch));
                    break;
                case RemoteConfig or RemoteRename or UndoEffect.RemoteUrl:
                    config.Add(new ConfigScope("remote"));
                    break;
                case FilesTo write:
                    files.AddRange(write.From.Concat(write.To).Select(file => file.Path));
                    break;
                case RestoreSnapshot restore:
                    files.AddRange(restore.Snapshot.Paths);
                    files.AddRange(restore.From.Select(file => file.Path));
                    break;
                case IndexEntries entries:
                    files.AddRange(entries.Paths);
                    break;
                case StashPush or StashPop:
                    stashList = true;
                    status = true;
                    break;
                case StashStore or StashDrop:
                    stashList = true;
                    break;
                case TagRef tag:
                    refs.Add(TagsPrefix + tag.Name);
                    break;
                case GitignoreBytes:
                    gitignore = true;
                    break;
                case HardReset { Branch: { } branch }:
                    refs.Add(HeadsPrefix + branch);
                    break;
            }
        }

        return new UndoScope
        {
            Kind = step.Entry.Kind,
            Text = step.Entry.Text,
            Refs = [.. refs.Distinct(StringComparer.Ordinal)],
            RefPrefixes = scope.RefPrefixes,
            StashList = stashList,
            Config = [.. config.Distinct()],
            Files = [.. files.Distinct(StringComparer.Ordinal)],
            Status = status,
            Gitignore = gitignore,
        };
    }

    /// <summary>
    /// The checks that need no git (7.4): HEAD (where a stash is popped too), the branch tips, the
    /// tags, the stashes, the staged files, the branches the other worktrees have checked out and
    /// the remotes, against what the app last read. The app makes them after every refresh and
    /// keeps the result for the toolbar's Undo and Redo; the others (file hashes, index entries,
    /// configuration keys, an annotated tag's object, the ignored files in the way, a remote's
    /// lease) wait for the step's operation. An operation in progress and conflicts are the app's
    /// own reasons, before these (7.4).
    /// </summary>
    /// <param name="step">The step the toolbar would run.</param>
    /// <param name="refs">The refs as last read.</param>
    /// <param name="status">The working tree's status as last read.</param>
    /// <param name="otherWorktrees">The repository's other worktrees and their branches, as last read.</param>
    /// <returns>The first check that fails, or null when every one passes.</returns>
    public static UndoRefusal? FirstRefusalInMemory(UndoStep step, RepoRefs refs, WorkingTreeStatus status, IReadOnlyList<WorktreeInfo> otherWorktrees)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(otherWorktrees);

        var checkedOut = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var worktree in otherWorktrees)
        {
            if (worktree.Branch is { } branch)
            {
                checkedOut.TryAdd(branch, worktree.Path);
            }
        }

        // This worktree's folder is not known here: a refusal for HEAD's own branch names none.
        if (refs.Head.BranchName is { } head)
        {
            checkedOut[HeadsPrefix + head] = string.Empty;
        }

        var state = new UndoState
        {
            Head = refs.Head,
            Refs = refs.Refs.ToDictionary(gitRef => gitRef.FullName, gitRef => gitRef.TargetSha, StringComparer.Ordinal),
            CheckedOutIn = checkedOut,
            Stashes = [.. refs.Stashes.Select(stash => new UndoStash(stash.Sha, stash.Message))],
        };
        var simulation = new Simulation(state, inMemory: true, remotes: refs.Remotes)
        {
            // The status tells whether the index holds HEAD's tree: what IndexTo's check needs
            // when the commit it compares with is HEAD's.
            IndexEquals = status.Staged.Count == 0 ? refs.Head.Sha : null,
            IndexDiffersFrom = status.Staged.Count == 0 ? null : refs.Head.Sha,
        };
        return simulation.FirstRefusal(step.Effects);
    }

    /// <summary>
    /// The simulation of the whole step inside its operation (7.5 step 3): every check against
    /// <paramref name="now"/>, read with <see cref="ScopeOf"/> (HEAD, the refs with their
    /// worktrees, the stash list, the configuration keys, the files and the status), each against
    /// the state the effects before it will leave. The checks only git can make (the ignored files
    /// in a Keep move's way and its two-way merge, the index against a commit, the commits'
    /// existence, the flags of index entries, a stash's untracked files on disk, a remote's lease)
    /// pass here and are the git layer's.
    /// </summary>
    /// <param name="step">The step to run.</param>
    /// <param name="now">What the step's operation read before it changes anything.</param>
    /// <returns>The first check that fails, or null when every one passes.</returns>
    public static UndoRefusal? FirstRefusal(UndoStep step, UndoState now)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(now);
        return new Simulation(now, inMemory: false, remotes: UndoSequence.RemoteNames(now)).FirstRefusal(step.Effects);
    }

    /// <summary>
    /// The state a step's effects leave, effect by effect, as far as it is known. What is not
    /// known (in memory: the configuration, the files, the index entries, the whole status, an
    /// annotated tag's object, the symbolic refs; after a pop: the files and the status) is not checked.
    /// </summary>
    private sealed class Simulation
    {
        /// <summary>Stands for the stash a <see cref="StashPush"/> of the step makes, whose commit is not known before it runs.</summary>
        private const string NewStash = "(the stash the step makes)";

        private readonly bool _inMemory;
        private readonly Dictionary<string, string> _refs;

        /// <summary>The symbolic refs with the refs they point at; null in memory, where they were not read.</summary>
        private readonly Dictionary<string, string>? _symbolicRefs;

        /// <summary>The branches (short names) checked out in another worktree, with its folder.</summary>
        private readonly Dictionary<string, string> _elsewhere = new(StringComparer.Ordinal);

        /// <summary>The folder of this worktree, where HEAD's branch was checked out when the step began, if it was read.</summary>
        private readonly string? _here;

        private readonly List<UndoStash> _stashes;
        private readonly HashSet<string> _remotes;
        private readonly Dictionary<string, string?> _files = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _index = new(StringComparer.Ordinal);
        private HeadState _head;
        private List<ConfigEntry>? _config;
        private string? _status;
        private GitignoreState? _gitignore;
        private bool _filesKnown;
        private bool _indexKnown;
        private bool _stashMade;

        public Simulation(UndoState now, bool inMemory, IEnumerable<string> remotes)
        {
            _inMemory = inMemory;
            _head = now.Head;
            _refs = new Dictionary<string, string>(now.Refs, StringComparer.Ordinal);
            _stashes = [.. now.Stashes];
            _remotes = new HashSet<string>(remotes, StringComparer.Ordinal);
            foreach (var (fullName, folder) in now.CheckedOutIn)
            {
                if (!fullName.StartsWith(HeadsPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var branch = fullName[HeadsPrefix.Length..];
                if (string.Equals(branch, now.Head.BranchName, StringComparison.Ordinal))
                {
                    _here = folder;
                }
                else
                {
                    _elsewhere[branch] = folder;
                }
            }

            if (inMemory)
            {
                return;
            }

            _symbolicRefs = new Dictionary<string, string>(now.SymbolicRefs, StringComparer.Ordinal);
            _config = [.. now.Config];
            _status = now.Status;
            _gitignore = now.Gitignore;
            _filesKnown = true;
            _indexKnown = true;
            foreach (var file in now.Files)
            {
                _files[file.Path] = file.Sha256;
            }

            foreach (var entry in now.IndexEntries)
            {
                var path = IndexEntryPath(entry);
                if (!_index.TryGetValue(path, out var entries))
                {
                    _index[path] = entries = [];
                }

                entries.Add(entry);
            }
        }

        /// <summary>The commit whose tree the index is known to hold, or null.</summary>
        public string? IndexEquals { get; set; }

        /// <summary>A commit whose tree the index is known not to hold, or null.</summary>
        public string? IndexDiffersFrom { get; set; }

        public UndoRefusal? FirstRefusal(IReadOnlyList<UndoEffect> effects)
        {
            foreach (var effect in effects)
            {
                if (Check(effect) is { } refusal)
                {
                    return refusal;
                }
            }

            return null;
        }

        private UndoRefusal? Check(UndoEffect effect) => effect switch
        {
            MoveHead move => Move(move),
            MoveDetachedHead move => MoveDetached(move),
            SetRef set => Set(set),
            SymbolicRef symbolic => Symbolic(symbolic),
            Switch change => SwitchHead(change),
            UndoEffect.RenameBranch rename => Rename(rename),
            BranchConfig keys => Keys(keys),
            RemoteConfig keys => Keys(keys),
            RemoteRename rename => RenameRemote(rename),
            UndoEffect.RemoteUrl url => Url(url),
            IndexTo index => Index(index),
            FilesTo write => Files(write.From, write.To),
            RestoreSnapshot restore => Restore(restore),
            StashPush push => Push(push),
            StashPop pop => Pop(pop),
            StashStore store => Store(store),
            StashDrop drop => Drop(drop),
            IndexEntries entries => Entries(entries),
            TagRef tag => Tag(tag),
            GitignoreBytes gitignore => Gitignore(gitignore),
            HardReset reset => Reset(reset),
            RemoteRef or KeepTag => null,
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, null),
        };

        private UndoRefusal? Move(MoveHead move)
        {
            if (!string.Equals(_head.BranchName, move.Branch, StringComparison.Ordinal))
            {
                return new UndoRefusal(UndoRefusalKind.HeadNotOnBranch, Name: move.Branch);
            }

            var fullName = HeadsPrefix + move.Branch;
            var at = RefOf(fullName);
            if (!string.Equals(at, move.From, StringComparison.Ordinal))
            {
                return new UndoRefusal(UndoRefusalKind.BranchMoved, Name: move.Branch, Sha: at);
            }

            SetValue(fullName, move.To);
            MoveIndex(move.From, move.To, move.Mode);
            _head = _head with { Sha = move.To };
            return null;
        }

        private UndoRefusal? MoveDetached(MoveDetachedHead move)
        {
            if (_head.BranchName is not null || !string.Equals(_head.Sha, move.From, StringComparison.Ordinal))
            {
                return new UndoRefusal(UndoRefusalKind.HeadNotAtCommit, Sha: move.From);
            }

            MoveIndex(move.From, move.To, move.Mode);
            _head = new HeadState(null, move.To);
            return null;
        }

        private UndoRefusal? Set(SetRef set)
        {
            if (!KnowsRef(set.Ref))
            {
                return null;
            }

            var at = RefOf(set.Ref);
            if (!string.Equals(at, set.From, StringComparison.Ordinal))
            {
                return RefRefusal(set.Ref, set.From, at);
            }

            if (set.Ref.StartsWith(HeadsPrefix, StringComparison.Ordinal))
            {
                // update-ref moves a branch under a worktree that has it checked out without a
                // word (0.4): neither this one, while HEAD is still on it, nor another.
                var branch = set.Ref[HeadsPrefix.Length..];
                if (string.Equals(_head.BranchName, branch, StringComparison.Ordinal))
                {
                    return CheckedOut(branch, _here);
                }

                if (_elsewhere.TryGetValue(branch, out var folder))
                {
                    return CheckedOut(branch, folder);
                }
            }

            SetValue(set.Ref, set.To);
            return null;
        }

        private UndoRefusal? Symbolic(SymbolicRef symbolic)
        {
            if (_symbolicRefs is null)
            {
                return null;
            }

            // A ref of the name that is not symbolic is in the way of one created too.
            if (!string.Equals(_symbolicRefs.GetValueOrDefault(symbolic.Ref), symbolic.From, StringComparison.Ordinal)
                || (symbolic.From is null && RefOf(symbolic.Ref) is not null))
            {
                return new UndoRefusal(UndoRefusalKind.RefChanged, Name: symbolic.Ref);
            }

            if (symbolic.To is null)
            {
                _symbolicRefs.Remove(symbolic.Ref);
            }
            else
            {
                _symbolicRefs[symbolic.Ref] = symbolic.To;
            }

            return null;
        }

        private UndoRefusal? SwitchHead(Switch change)
        {
            if (change.From.BranchName is { } from)
            {
                // A branch that moved since is still left as it is: switching away overwrites nothing.
                if (!string.Equals(_head.BranchName, from, StringComparison.Ordinal))
                {
                    return new UndoRefusal(UndoRefusalKind.HeadNotOnBranch, Name: from);
                }
            }
            else if (_head.BranchName is not null || !string.Equals(_head.Sha, change.From.Sha, StringComparison.Ordinal))
            {
                return new UndoRefusal(UndoRefusalKind.HeadNotAtCommit, Sha: change.From.Sha);
            }

            IndexEquals = null;
            IndexDiffersFrom = null;
            if (change.To.BranchName is not { } to)
            {
                _head = new HeadState(null, change.To.Sha);
                return null;
            }

            var fullName = HeadsPrefix + to;
            if (RefOf(fullName) is not { } tip)
            {
                return new UndoRefusal(UndoRefusalKind.RefChanged, Name: fullName);
            }

            if (_elsewhere.TryGetValue(to, out var folder))
            {
                return CheckedOut(to, folder);
            }

            _head = new HeadState(to, tip);
            return null;
        }

        private UndoRefusal? Rename(UndoEffect.RenameBranch rename)
        {
            var from = HeadsPrefix + rename.From;
            var to = HeadsPrefix + rename.To;
            if (RefOf(from) is not { } tip)
            {
                return new UndoRefusal(UndoRefusalKind.RefChanged, Name: from);
            }

            // A name that differs only in case is the same file where refs are files on a file
            // system that ignores case (D131): git refuses both ways.
            var sameIgnoringCase = RefNameRules.NamesIgnoreCase
                && _refs.Keys.Any(name => string.Equals(name, to, StringComparison.OrdinalIgnoreCase));
            if (RefOf(to) is not null
                || sameIgnoringCase
                || (RefNameRules.NamesIgnoreCase && string.Equals(rename.From, rename.To, StringComparison.OrdinalIgnoreCase)))
            {
                return new UndoRefusal(UndoRefusalKind.BranchExists, Name: rename.To);
            }

            _refs.Remove(from);
            _refs[to] = tip;
            if (string.Equals(_head.BranchName, rename.From, StringComparison.Ordinal))
            {
                _head = _head with { BranchName = rename.To };
            }

            if (_elsewhere.Remove(rename.From, out var folder))
            {
                _elsewhere[rename.To] = folder;
            }

            return null;
        }

        private UndoRefusal? Keys(BranchConfig keys)
        {
            if (_config is null)
            {
                return null;
            }

            var state = new UndoState { Head = _head, Config = _config };
            if (!UndoSequence.BranchKeys(state, keys.Branch).SequenceEqual(keys.From))
            {
                return new UndoRefusal(UndoRefusalKind.ConfigChanged, Name: keys.Branch);
            }

            _config = [.. _config.Where(key => !ConfigScope.Branch(keys.Branch).Covers(key)), .. keys.To];
            return null;
        }

        private UndoRefusal? Keys(RemoteConfig keys)
        {
            var creates = !keys.From.Any(ConfigScope.Remote(keys.Remote).Covers);
            var removes = !keys.To.Any(ConfigScope.Remote(keys.Remote).Covers);
            if (creates && _remotes.Contains(keys.Remote))
            {
                return new UndoRefusal(UndoRefusalKind.RemoteExists, Name: keys.Remote);
            }

            if (!creates && !_remotes.Contains(keys.Remote))
            {
                return new UndoRefusal(UndoRefusalKind.ConfigChanged, Name: keys.Remote);
            }

            if (_config is not null)
            {
                var state = new UndoState { Head = _head, Config = _config };
                if (!UndoSequence.RemoteKeys(state, keys.Remote).SequenceEqual(keys.From))
                {
                    return new UndoRefusal(UndoRefusalKind.ConfigChanged, Name: keys.Remote);
                }

                var left = UndoSequence.RemoteKeys(state, keys.Remote);
                _config = [.. _config.Where(key => !left.Contains(key)), .. keys.To];
            }

            if (removes)
            {
                // Every ref left under the remote goes with it, as git remote remove deletes them.
                _remotes.Remove(keys.Remote);
                ForgetRefsUnder($"{RemotesPrefix}{keys.Remote}/");
            }
            else
            {
                _remotes.Add(keys.Remote);
            }

            return null;
        }

        private UndoRefusal? RenameRemote(RemoteRename rename)
        {
            if (!_remotes.Contains(rename.From))
            {
                return new UndoRefusal(UndoRefusalKind.ConfigChanged, Name: rename.From);
            }

            if (_remotes.Contains(rename.To))
            {
                return new UndoRefusal(UndoRefusalKind.RemoteExists, Name: rename.To);
            }

            _remotes.Remove(rename.From);
            _remotes.Add(rename.To);

            // Git rewrites the remote's keys, the upstreams and its remote branches: what is read
            // of them is no longer known.
            _config = null;
            ForgetRefsUnder($"{RemotesPrefix}{rename.From}/");
            return null;
        }

        private UndoRefusal? Url(UndoEffect.RemoteUrl url)
        {
            if (!_remotes.Contains(url.Remote))
            {
                return new UndoRefusal(UndoRefusalKind.ConfigChanged, Name: url.Remote);
            }

            if (_config is null)
            {
                return null;
            }

            // remote set-url refuses a remote with two url values (0.5).
            var key = $"remote.{url.Remote}.url";
            var values = _config.Where(entry => string.Equals(entry.Key, key, StringComparison.Ordinal)).ToList();
            if (values.Count != 1 || !string.Equals(values[0].Value, url.From, StringComparison.Ordinal))
            {
                return new UndoRefusal(UndoRefusalKind.ConfigChanged, Name: url.Remote);
            }

            _config = [.. _config.Select(entry => ReferenceEquals(entry, values[0]) ? entry with { Value = url.To } : entry)];
            return null;
        }

        private UndoRefusal? Index(IndexTo index)
        {
            // Whether the index holds a commit's tree is git's to tell (diff-index), unless it is
            // known: in memory, from the staged files against HEAD. A difference elsewhere says
            // nothing of the paths a restore of some paths compares (the report's BD16).
            if (index.Paths is null && string.Equals(IndexDiffersFrom, index.From, StringComparison.Ordinal))
            {
                return new UndoRefusal(UndoRefusalKind.IndexChanged);
            }

            IndexEquals = index.Paths is null ? index.Source : null;
            IndexDiffersFrom = null;
            ForgetIndexEntries();
            return null;
        }

        private UndoRefusal? Files(IReadOnlyList<RecordedFile> from, IReadOnlyList<RecordedFile> to)
        {
            if (!_filesKnown)
            {
                return null;
            }

            var changed = from
                .Where(file => !_files.TryGetValue(file.Path, out var hash) || !string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                .Select(file => file.Path)
                .ToList();
            if (changed.Count > 0)
            {
                return new UndoRefusal(UndoRefusalKind.FilesChanged, Paths: changed);
            }

            foreach (var file in to)
            {
                _files[file.Path] = file.Sha256;
            }

            return null;
        }

        private UndoRefusal? Restore(RestoreSnapshot restore)
        {
            if (Files(restore.From, []) is { } refusal)
            {
                return refusal;
            }

            // The snapshot's files come back through git's filters: their bytes are not known.
            _filesKnown = false;
            return null;
        }

        private UndoRefusal? Push(StashPush push)
        {
            if (push.Status is { } expected && _status is { } status && !string.Equals(status, expected, StringComparison.Ordinal))
            {
                return new UndoRefusal(UndoRefusalKind.StatusChanged);
            }

            if (_status is { } known)
            {
                // Git leaves the untracked files when they are not stashed, and makes no stash
                // when nothing is left to save.
                var left = push.IncludeUntracked ? string.Empty : UntrackedOnly(known);
                _stashMade = !string.Equals(left, known, StringComparison.Ordinal);
                _status = left;
            }
            else
            {
                _stashMade = true;
            }

            if (_stashMade)
            {
                _stashes.Insert(0, new UndoStash(NewStash, push.Message ?? string.Empty));
                IndexEquals = _head.Sha;
                IndexDiffersFrom = null;
            }

            _filesKnown = false;
            ForgetIndexEntries();
            return null;
        }

        private UndoRefusal? Pop(StashPop pop)
        {
            var sha = pop.Sha ?? (_stashMade ? NewStash : null);
            if (sha is null)
            {
                // The step's stash saved nothing: there is nothing to pop.
                return null;
            }

            var listed = _stashes.FindIndex(stash => string.Equals(stash.Sha, sha, StringComparison.Ordinal));
            if (listed < 0)
            {
                return new UndoRefusal(UndoRefusalKind.StashGone);
            }

            // Popped onto another commit, the stash is merged there and can conflict after every
            // check passed (0.5). The status cannot tell: a commit made since can leave it clean.
            if (pop.Onto is { } onto && !string.Equals(_head.Sha, onto.Sha, StringComparison.Ordinal))
            {
                return onto.BranchName is { } branch && string.Equals(_head.BranchName, branch, StringComparison.Ordinal)
                    ? new UndoRefusal(UndoRefusalKind.BranchMoved, Name: branch, Sha: _head.Sha)
                    : new UndoRefusal(UndoRefusalKind.HeadNotAtCommit, Sha: onto.Sha);
            }

            if (pop.Status is { } expected && _status is { } status && !string.Equals(status, expected, StringComparison.Ordinal))
            {
                return new UndoRefusal(UndoRefusalKind.StatusChanged);
            }

            _stashes.RemoveAt(listed);
            _status = null;
            _filesKnown = false;
            IndexEquals = null;
            IndexDiffersFrom = null;
            ForgetIndexEntries();
            return null;
        }

        private UndoRefusal? Store(StashStore store)
        {
            if (_stashes.Any(stash => string.Equals(stash.Sha, store.Sha, StringComparison.Ordinal)))
            {
                return new UndoRefusal(UndoRefusalKind.StashListed);
            }

            _stashes.Insert(0, new UndoStash(store.Sha, store.Message));
            return null;
        }

        private UndoRefusal? Drop(StashDrop drop)
        {
            var listed = _stashes.FindIndex(stash => string.Equals(stash.Sha, drop.Sha, StringComparison.Ordinal));
            if (listed < 0)
            {
                return new UndoRefusal(UndoRefusalKind.StashGone);
            }

            _stashes.RemoveAt(listed);
            return null;
        }

        private UndoRefusal? Entries(IndexEntries entries)
        {
            if (!_indexKnown)
            {
                return null;
            }

            var expected = Group(entries.From);
            var changed = entries.Paths
                .Where(path => !(_index.GetValueOrDefault(path) ?? []).SequenceEqual(expected.GetValueOrDefault(path) ?? [], StringComparer.Ordinal))
                .ToList();
            if (changed.Count > 0)
            {
                return new UndoRefusal(UndoRefusalKind.IndexChanged, Paths: changed);
            }

            foreach (var path in entries.Paths)
            {
                _index.Remove(path);
            }

            foreach (var (path, list) in Group(entries.To))
            {
                _index[path] = list;
            }

            IndexEquals = null;
            IndexDiffersFrom = null;
            return null;
        }

        private UndoRefusal? Tag(TagRef tag)
        {
            var fullName = TagsPrefix + tag.Name;
            var at = RefOf(fullName);
            if (tag.Present)
            {
                if (at is not null || (RefNameRules.NamesIgnoreCase && _refs.Keys.Any(name => string.Equals(name, fullName, StringComparison.OrdinalIgnoreCase))))
                {
                    return new UndoRefusal(UndoRefusalKind.TagExists, Name: tag.Name);
                }

                _refs[fullName] = tag.Object;
                return null;
            }

            // In memory a tag's target is the commit an annotated tag tags, not its own object:
            // only its presence is known there.
            if (at is null || (!_inMemory && !string.Equals(at, tag.Object, StringComparison.Ordinal)))
            {
                return new UndoRefusal(UndoRefusalKind.RefChanged, Name: fullName);
            }

            _refs.Remove(fullName);
            return null;
        }

        private UndoRefusal? Gitignore(GitignoreBytes gitignore)
        {
            if (_inMemory)
            {
                return null;
            }

            var now = _gitignore ?? new GitignoreState(Exists: false, Length: 0, Sha256: null);
            if (now.Exists != gitignore.From.Exists || !string.Equals(now.Sha256, gitignore.From.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return new UndoRefusal(UndoRefusalKind.FilesChanged, Paths: [".gitignore"]);
            }

            _gitignore = gitignore.To;
            return null;
        }

        private UndoRefusal? Reset(HardReset reset)
        {
            if (reset.Branch is { } branch)
            {
                if (!string.Equals(_head.BranchName, branch, StringComparison.Ordinal))
                {
                    return new UndoRefusal(UndoRefusalKind.HeadNotOnBranch, Name: branch);
                }

                var at = RefOf(HeadsPrefix + branch);
                if (!string.Equals(at, reset.From, StringComparison.Ordinal))
                {
                    return new UndoRefusal(UndoRefusalKind.BranchMoved, Name: branch, Sha: at);
                }

                _refs[HeadsPrefix + branch] = reset.To;
            }
            else if (_head.BranchName is not null || !string.Equals(_head.Sha, reset.From, StringComparison.Ordinal))
            {
                return new UndoRefusal(UndoRefusalKind.HeadNotAtCommit, Sha: reset.From);
            }

            _head = _head with { Sha = reset.To };
            IndexEquals = reset.To;
            IndexDiffersFrom = null;
            _filesKnown = false;
            ForgetIndexEntries();
            _status = null;
            return null;
        }

        /// <summary>
        /// What the index is known to hold after a move: a Keep move carries the index's
        /// differences from the commit it leaves to the commit it goes to; a soft move leaves the index.
        /// </summary>
        private void MoveIndex(string? from, string? to, MoveMode mode)
        {
            if (mode == MoveMode.Soft)
            {
                return;
            }

            IndexEquals = string.Equals(IndexEquals, from, StringComparison.Ordinal) ? to : null;
            IndexDiffersFrom = string.Equals(IndexDiffersFrom, from, StringComparison.Ordinal) ? to : null;
            ForgetIndexEntries();
        }

        private void ForgetIndexEntries() => _indexKnown = false;

        /// <summary>The refs and symbolic refs under <paramref name="prefix"/> are gone, or no longer known.</summary>
        private void ForgetRefsUnder(string prefix)
        {
            foreach (var name in _refs.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            {
                _refs.Remove(name);
            }

            if (_symbolicRefs is not { } symbolicRefs)
            {
                return;
            }

            foreach (var name in symbolicRefs.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            {
                symbolicRefs.Remove(name);
            }
        }

        /// <summary>Whether the ref's value is known: in memory, only branches, remote branches and tags were read.</summary>
        private bool KnowsRef(string fullName) =>
            !_inMemory
            || fullName.StartsWith(HeadsPrefix, StringComparison.Ordinal)
            || fullName.StartsWith(RemotesPrefix, StringComparison.Ordinal)
            || fullName.StartsWith(TagsPrefix, StringComparison.Ordinal);

        private string? RefOf(string fullName) => _refs.GetValueOrDefault(fullName);

        private void SetValue(string fullName, string? value)
        {
            if (value is null)
            {
                _refs.Remove(fullName);
            }
            else
            {
                _refs[fullName] = value;
            }
        }

        private static UndoRefusal CheckedOut(string branch, string? folder) =>
            new(UndoRefusalKind.CheckedOutElsewhere, Name: branch, Paths: folder is { Length: > 0 } ? [folder] : null);

        /// <summary>A ref is not where the step expects it: a branch moved or exists again, a tag exists again, another ref changed.</summary>
        private static UndoRefusal RefRefusal(string fullName, string? expected, string? at)
        {
            if (fullName.StartsWith(HeadsPrefix, StringComparison.Ordinal))
            {
                var branch = fullName[HeadsPrefix.Length..];
                return expected is null
                    ? new UndoRefusal(UndoRefusalKind.BranchExists, Name: branch)
                    : new UndoRefusal(UndoRefusalKind.BranchMoved, Name: branch, Sha: at);
            }

            if (fullName.StartsWith(TagsPrefix, StringComparison.Ordinal) && expected is null)
            {
                return new UndoRefusal(UndoRefusalKind.TagExists, Name: fullName[TagsPrefix.Length..]);
            }

            return new UndoRefusal(UndoRefusalKind.RefChanged, Name: fullName);
        }
    }

    /// <summary>The path of an index entry as <c>ls-files --stage</c> prints it: <c>&lt;mode&gt; &lt;object&gt; &lt;stage&gt;\t&lt;path&gt;</c>.</summary>
    public static string IndexEntryPath(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var tab = entry.IndexOf('\t', StringComparison.Ordinal);
        return tab < 0 ? entry : entry[(tab + 1)..];
    }

    /// <summary>Index entries grouped by path, in their order.</summary>
    private static Dictionary<string, List<string>> Group(IEnumerable<string> entries)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var path = IndexEntryPath(entry);
            if (!groups.TryGetValue(path, out var list))
            {
                groups[path] = list = [];
            }

            list.Add(entry);
        }

        return groups;
    }

    /// <summary>
    /// The untracked files' records of a status read with <c>git status --porcelain=v2 -z
    /// --untracked-files=all</c>: what a stash without its untracked files leaves. Each record ends
    /// with a NUL; a rename's (<c>2</c>) has its old path after one more.
    /// </summary>
    public static string UntrackedOnly(string status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var fields = status.Split('\0');
        var kept = new System.Text.StringBuilder();
        for (var i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            if (field.StartsWith("2 ", StringComparison.Ordinal))
            {
                i++;
            }
            else if (field.StartsWith("? ", StringComparison.Ordinal))
            {
                kept.Append(field).Append('\0');
            }
        }

        return kept.ToString();
    }
}
