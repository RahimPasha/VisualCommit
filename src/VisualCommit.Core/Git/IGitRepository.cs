namespace VisualCommit.Core.Git;

/// <summary>
/// One open repository: the reading git calls that the views need. The implementation in the Git
/// project (<c>GitRepository</c>) runs real git; view-model tests can use a fake. Every call that
/// only reads runs with <c>GIT_OPTIONAL_LOCKS=0</c>, so reading never writes to the repository
/// (D47). Methods can be called from any thread.
/// </summary>
public interface IGitRepository
{
    /// <summary>The top-level folder of the working tree, as git reports it (<c>git rev-parse --show-toplevel</c>), with the platform's separators.</summary>
    string WorkingDirectory { get; }

    /// <summary>The repository's git folder, absolute. For a linked worktree, the worktree's own folder under the main repository's git folder.</summary>
    string GitDirectory { get; }

    /// <summary>The git folder that holds the refs and objects: the same as <see cref="GitDirectory"/> except in a linked worktree.</summary>
    string CommonDirectory { get; }

    /// <summary>The name the tab shows: the working tree's folder name.</summary>
    string Name { get; }

    /// <summary>Reads HEAD, the branches with their upstreams and ahead/behind counts, the remote branches, the tags, the stashes and the remotes.</summary>
    Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the history the graph shows: every commit reachable from a branch, a remote branch,
    /// a tag or HEAD, in <c>git log --date-order</c> order, with the stashes of
    /// <paramref name="refs"/> merged in by date: each before the first commit older than it, and
    /// in any case before the commit it was made on (D47, D54).
    /// <paramref name="onPage"/> receives consecutive pages on a background thread, never the
    /// caller's and never two at once: the first page holds at most 100 commits and is delivered as soon as it is read,
    /// later pages at most 2,000. Returns when the last page has been delivered. A repository
    /// without commits delivers no page.
    /// </summary>
    Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default);

    /// <summary>
    /// As <see cref="LoadCommitsAsync(RepoRefs, Action{IReadOnlyList{CommitInfo}}, CancellationToken)"/>,
    /// while the refs are still being read: <c>git log</c> starts at once, and no page is handed
    /// over before <paramref name="refs"/> has completed, because the stashes are merged in from
    /// it. Opening a repository reads the refs and the history side by side this way, which
    /// brings the first graph forward by the time the refs take (Q1). If reading the refs fails,
    /// so does this. The default implementation simply waits for the refs first.
    /// </summary>
    async Task LoadCommitsAsync(Task<RepoRefs> refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refs);
        await LoadCommitsAsync(await refs.ConfigureAwait(false), onPage, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the full message, the author and committer and the changed files of one commit or stash.</summary>
    Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default);

    /// <summary>Reads the staged, unstaged and untracked files (<c>git status --porcelain=v2</c>).</summary>
    Task<WorkingTreeStatus> ReadStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads one file's diff, byte for byte (D66). A file without changes on that side gives <see cref="Diff.FileDiff.Empty"/>.</summary>
    Task<Diff.FileDiff> ReadDiffAsync(Diff.DiffTarget target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one version of a file whole, or null when it does not exist there or is larger than
    /// <paramref name="maxBytes"/> (its size is then in <see cref="ReadFileSizeAsync"/>).
    /// </summary>
    Task<byte[]?> ReadFileAsync(Diff.FileVersion version, long maxBytes, CancellationToken cancellationToken = default);

    /// <summary>The size in bytes of one version of a file, or null when it does not exist there.</summary>
    Task<long?> ReadFileSizeAsync(Diff.FileVersion version, CancellationToken cancellationToken = default);

    /// <summary>Stages these files whole: changes, new files and deletions (<c>git add --all</c>).</summary>
    Task StageAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default);

    /// <summary>Unstages these files whole: their index entries go back to HEAD's (or away, before the first commit).</summary>
    Task UnstageAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a patch made by <see cref="Diff.PatchBuilder"/>: to the index (<paramref name="toIndex"/>)
    /// or to the working tree, forward or in reverse (D66).
    /// </summary>
    /// <exception cref="GitException">Git refused the patch; the message is git's own.</exception>
    Task ApplyPatchAsync(string patch, bool toIndex, Diff.PatchDirection direction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the working-tree versions of <paramref name="paths"/> (untracked ones included, a
    /// deleted one as deleted) in a snapshot commit kept under <c>refs/visualcommit/backup/</c>
    /// (D67), before they are discarded.
    /// </summary>
    Task<DiscardSnapshot> SaveSnapshotAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards the unstaged changes of these files whole: tracked files get their index version
    /// back, untracked files are deleted. Take a snapshot first (<see cref="SaveSnapshotAsync"/>).
    /// </summary>
    Task DiscardAsync(IReadOnlyList<string> paths, IReadOnlySet<string> untracked, CancellationToken cancellationToken = default);

    /// <summary>Puts the files of a snapshot back into the working tree as they were when it was taken (D67).</summary>
    Task RestoreSnapshotAsync(DiscardSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits what is staged with <paramref name="message"/>, or with <paramref name="amend"/>
    /// replaces HEAD's commit (D68). Returns the new commit's id.
    /// </summary>
    /// <exception cref="GitException">Git refused, for example a hook or a missing identity; the message is git's own.</exception>
    Task<string> CommitAsync(string message, bool amend, CancellationToken cancellationToken = default);

    /// <summary>Creates a watcher for this repository's git folder and working tree (D52, D71). It does nothing until started.</summary>
    IRepositoryWatcher CreateWatcher();
}

/// <summary>A snapshot taken before a discard (D67).</summary>
/// <param name="Ref">The ref that keeps it, under <c>refs/visualcommit/backup/</c>.</param>
/// <param name="Commit">The snapshot commit's id.</param>
/// <param name="Paths">The files it was taken for.</param>
/// <param name="Present">Those of <paramref name="Paths"/> that existed in the working tree; the others were deleted there.</param>
public sealed record DiscardSnapshot(string Ref, string Commit, IReadOnlyList<string> Paths, IReadOnlySet<string> Present);

/// <summary>Opens, initialises and clones repositories. The app's implementation waits for git to be found first.</summary>
public interface IRepositoryProvider
{
    /// <summary>Opens the repository that contains <paramref name="path"/> (the folder or any folder inside its working tree).</summary>
    /// <exception cref="NotARepositoryException">The folder is not inside a git working tree.</exception>
    /// <exception cref="GitUnavailableException">No usable git was found on this machine.</exception>
    Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Runs <c>git init</c> in <paramref name="path"/>, creating the folder if needed, and opens the result.</summary>
    Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clones <paramref name="url"/> into <paramref name="destination"/>, which must not exist yet
    /// or be empty, reporting git's progress, and opens the result. If the clone fails or is
    /// cancelled, a destination folder the clone created is removed again.
    /// </summary>
    /// <exception cref="GitException">Git reported an error; its message is git's own.</exception>
    Task<IGitRepository> CloneAsync(string url, string destination, IProgress<CloneProgress>? progress, CancellationToken cancellationToken = default);
}

/// <summary>A line of progress from <c>git clone</c>.</summary>
/// <param name="Stage">What git is doing, such as <c>Receiving objects</c> or <c>Resolving deltas</c>.</param>
/// <param name="Percent">How far that stage is, 0 to 100, or null when git gives no percentage.</param>
/// <param name="Text">The whole line as git printed it, without a leading <c>remote: </c>.</param>
public sealed record CloneProgress(string Stage, int? Percent, string Text);

/// <summary>Watches a repository for changes made outside the app (D52, D71).</summary>
public interface IRepositoryWatcher : IDisposable
{
    /// <summary>
    /// Raised on a thread-pool thread after something the views show may have changed: once per
    /// burst of changes, after the burst has been quiet for the debounce time. The arguments say
    /// where: the git folder (refs, HEAD, the index, the stash) or the working tree.
    /// </summary>
    event EventHandler<RepositoryChangedEventArgs>? Changed;

    /// <summary>Starts watching.</summary>
    void Start();

    /// <summary>
    /// Stops reporting changes until the returned object is disposed, while the app writes to the
    /// repository; the app refreshes once when its write ends (D71).
    /// </summary>
    IDisposable Pause();
}

/// <summary>Where a burst of changes happened.</summary>
public sealed class RepositoryChangedEventArgs(bool gitFolder, bool workingTree) : EventArgs
{
    /// <summary>In the git folder: refs, HEAD, the index or the stash may have changed.</summary>
    public bool GitFolder { get; } = gitFolder;

    /// <summary>In the working tree: files may have changed.</summary>
    public bool WorkingTree { get; } = workingTree;
}

/// <summary>The folder is not inside a git working tree.</summary>
public sealed class NotARepositoryException(string path)
    : Exception($"{path} is not a git repository.")
{
    public string Path { get; } = path;
}

/// <summary>An operation needs git, and no usable git was found.</summary>
public sealed class GitUnavailableException(string message) : Exception(message);
