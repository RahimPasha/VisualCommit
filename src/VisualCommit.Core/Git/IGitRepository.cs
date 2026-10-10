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
    /// <paramref name="refs"/> merged in, each just above the commit it was made on (D47).
    /// <paramref name="onPage"/> receives consecutive pages on a thread-pool thread, never two at
    /// once: the first page holds at most 100 commits and is delivered as soon as it is read,
    /// later pages at most 2,000. Returns when the last page has been delivered. A repository
    /// without commits delivers no page.
    /// </summary>
    Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default);

    /// <summary>Reads the full message, the author and committer and the changed files of one commit or stash.</summary>
    Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default);

    /// <summary>Creates a watcher for this repository's git folder (D52). It does nothing until started.</summary>
    IRepositoryWatcher CreateWatcher();
}

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

/// <summary>Watches a repository for changes made outside the app (D52).</summary>
public interface IRepositoryWatcher : IDisposable
{
    /// <summary>
    /// Raised on a thread-pool thread after something the views show may have changed: once per
    /// burst of changes, after the burst has been quiet for the debounce time.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>Starts watching.</summary>
    void Start();
}

/// <summary>The folder is not inside a git working tree.</summary>
public sealed class NotARepositoryException(string path)
    : Exception($"{path} is not a git repository.")
{
    public string Path { get; } = path;
}

/// <summary>An operation needs git, and no usable git was found.</summary>
public sealed class GitUnavailableException(string message) : Exception(message);
