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
    /// Where HEAD is now, without the rest of the refs: the branch it is on (null while detached)
    /// and its commit (null while unborn). An operation that waited in the queue reads it to check
    /// that HEAD is still where the user saw it. The default implementation reads all the refs.
    /// </summary>
    async Task<HeadState> ReadHeadAsync(CancellationToken cancellationToken = default) =>
        (await ReadRefsAsync(cancellationToken).ConfigureAwait(false)).Head;

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

    // Phase 3: branch, remote, stash and tag operations (docs/phase-3-design.md, section 2). Each
    // runs inside one operation of the queue (D81). A write throws GitException with git's own
    // text when git refuses; "options" carries the operation's prompt variables and progress
    // (NetworkOptions); local writes get CancellationToken.None from the queue (1.5).

    // Reads (GIT_OPTIONAL_LOCKS=0).

    /// <summary>
    /// How many commits HEAD has that no branch, remote branch or tag reaches:
    /// <c>git rev-list --count HEAD --not --branches --remotes --tags</c>. Asked before any
    /// checkout while HEAD is detached; above 0, the checkout confirms and backs up (2.1).
    /// </summary>
    Task<int> CountDetachedCommitsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether <paramref name="ancestor"/> is an ancestor of <paramref name="descendant"/>, or the
    /// same commit: <c>git merge-base --is-ancestor</c> (exit 0 yes, 1 no). Used by 2.6's
    /// question, the drop menu's fast-forwards (2.7, 2.10, 4g).
    /// </summary>
    Task<bool> IsAncestorAsync(string ancestor, string descendant, CancellationToken cancellationToken = default);

    /// <summary>The number of commits in <paramref name="to"/> that <paramref name="from"/> lacks: <c>git rev-list --count &lt;from&gt;..&lt;to&gt;</c> (2.18's result).</summary>
    Task<int> CountCommitsAsync(string from, string to, CancellationToken cancellationToken = default);

    /// <summary>
    /// The object id <paramref name="revision"/> names, without peeling a tag
    /// (<c>git rev-parse --verify --quiet</c>), or null when it names nothing. Used for the
    /// commit a backup or a lease holds (2.20, 2.22, 2.29) and the new stash (2.31).
    /// </summary>
    Task<string?> ResolveAsync(string revision, CancellationToken cancellationToken = default);

    /// <summary>The repository's worktrees and the branch each has checked out: <c>git worktree list --porcelain</c> (2.7).</summary>
    Task<IReadOnlyList<WorktreeInfo>> ReadWorktreesAsync(CancellationToken cancellationToken = default);

    /// <summary>The configured remotes with their URLs: <c>git remote -v</c>, sorted by name (2.23). URLs are as configured: show them through <see cref="RemoteInfo.ShownUrl"/> (D103).</summary>
    Task<IReadOnlyList<RemoteInfo>> ReadRemotesAsync(CancellationToken cancellationToken = default);

    // Backups (2.0, D83).

    /// <summary>
    /// Writes the backup refs of <paramref name="request"/> under <c>refs/visualcommit/backup/</c>
    /// with one UTC timestamp (D67's format) in one <c>git update-ref --stdin</c> transaction of
    /// <c>create</c> lines; on a collision the whole batch is retried with a suffix on the
    /// kind-and-time part (2.0).
    /// </summary>
    Task<BackupResult> SaveBackupRefsAsync(BackupRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Before a hard reset to <paramref name="target"/>: a snapshot commit (D67's way, without a
    /// ref) of every staged or unstaged tracked file, every file staged as new and every file the
    /// index does not track that the reset overwrites or deletes (ignored files too, which a hard
    /// reset overwrites just as silently): one at a path <paramref name="target"/> tracks
    /// (<c>git ls-tree -r</c>), every file in a folder where the target has a file, and a file
    /// where the target has a folder. The real index's tree (<c>git write-tree</c>) is the second
    /// parent when it has no conflicts. Null when there is nothing to save (2.11). It goes into
    /// the reset's backup batch as <see cref="BackupRequest.ChangesCommit"/>.
    /// </summary>
    /// <exception cref="IOException">A repository is nested in a folder the reset would delete.</exception>
    Task<string?> SnapshotForHardResetAsync(string target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Before a mixed reset, which replaces the index: a commit of the real index's tree with HEAD
    /// as its parent, or null when the index holds HEAD's tree or has a conflict (2.11). It keeps a
    /// staged version that differs from the working tree's, and goes into the reset's backup batch
    /// as <see cref="BackupRequest.ChangesCommit"/>.
    /// </summary>
    Task<string?> SnapshotIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Before an abort: a snapshot commit (D67's way, without a ref) of every changed tracked
    /// file, or null when none changed (2.15).
    /// </summary>
    Task<string?> SnapshotTrackedChangesAsync(CancellationToken cancellationToken = default);

    // Branches and checkouts (2.1 to 2.10).

    /// <summary>Checks out the local branch <paramref name="branch"/> (short name): <c>git switch --no-guess &lt;branch&gt;</c> (2.1).</summary>
    Task CheckoutBranchAsync(string branch, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the local branch <paramref name="branch"/> tracking the remote branch
    /// <paramref name="remoteBranchRef"/> (<c>refs/remotes/&lt;r&gt;/&lt;b&gt;</c>) and checks it out:
    /// <c>git switch --create &lt;branch&gt; --track &lt;remoteBranchRef&gt;</c> (2.2).
    /// </summary>
    Task CheckoutTrackingBranchAsync(string branch, string remoteBranchRef, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Detaches HEAD at <paramref name="commit"/>: <c>git switch --detach &lt;sha&gt;</c> (2.3).</summary>
    Task CheckoutDetachedAsync(string commit, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the branch <paramref name="name"/> at <paramref name="startPoint"/> (a full SHA,
    /// <c>refs/heads/…</c>, <c>refs/remotes/…</c> or <c>refs/tags/&lt;t&gt;^{commit}</c>):
    /// <c>git branch &lt;name&gt; &lt;start&gt;</c>, or with <paramref name="checkout"/>
    /// <c>git switch --create &lt;name&gt; &lt;start&gt;</c>. A remote branch as start becomes the
    /// upstream, as git's <c>branch.autoSetupMerge</c> says (2.4).
    /// </summary>
    Task CreateBranchAsync(string name, string startPoint, bool checkout, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Renames a local branch, the current one too; its upstream setting moves with it: <c>git branch --move &lt;branch&gt; &lt;newName&gt;</c> (2.5).</summary>
    Task RenameBranchAsync(string branch, string newName, CancellationToken cancellationToken = default);

    /// <summary>Deletes a local branch, merged or not: <c>git branch --delete --force &lt;branch&gt;</c>. Save its backup first (2.6).</summary>
    Task DeleteBranchAsync(string branch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fast-forwards the local branch <paramref name="branch"/>, which is not checked out, to
    /// <paramref name="target"/> (a full ref): <c>git fetch --no-auto-maintenance
    /// --no-write-fetch-head . &lt;target&gt;:refs/heads/&lt;branch&gt;</c>. Git refuses a
    /// non-fast-forward and a branch checked out in any worktree (2.7). No prompts.
    /// </summary>
    Task FastForwardBranchAsync(string branch, string target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges <paramref name="reference"/> (the full name of a local or remote branch) into the
    /// current branch with the message git would write for its short name: the message from
    /// <c>git fmt-merge-msg</c>, then <c>git merge --no-edit --no-log -m &lt;message&gt;
    /// [--no-ff | --ff-only] &lt;reference&gt;</c> (2.8 to 2.10, D94). Conflicts are returned,
    /// not thrown: git failed and the status lists a conflicted file. A merge (or pick, or
    /// revert) refused because the index already had conflicts is a failure, a
    /// <see cref="GitException"/>, not a conflict of its own.
    /// </summary>
    Task<MergeResult> MergeAsync(string reference, MergeMode mode, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    // History (2.11 to 2.15).

    /// <summary>Moves the current branch (or a detached HEAD) to <paramref name="commit"/>: <c>git reset --soft | --mixed --quiet | --hard &lt;sha&gt;</c>. Save the backups first (2.11).</summary>
    Task ResetAsync(string commit, ResetMode mode, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies one commit on HEAD: <c>git cherry-pick &lt;sha&gt;</c>, never several at once (no
    /// sequencer, D88). Returns <see cref="ApplyResult.Conflicts"/> when it stopped with
    /// conflicted files and <see cref="ApplyResult.Empty"/> when it stopped with nothing to
    /// commit (2.12).
    /// </summary>
    Task<ApplyResult> CherryPickAsync(string commit, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverts one commit: <c>git revert --no-edit &lt;sha&gt;</c>, a merge commit with
    /// <c>-m 1</c> (<paramref name="isMerge"/>). Results as <see cref="CherryPickAsync"/> (2.14),
    /// except that a revert that changes nothing leaves no state on Git 2.36 (no REVERT_HEAD):
    /// it throws a <see cref="GitException"/> with git's output, and returns
    /// <see cref="ApplyResult.Empty"/> only where git keeps REVERT_HEAD.
    /// </summary>
    Task<ApplyResult> RevertAsync(string commit, bool isMerge, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aborts the operation in progress: <c>git merge --abort</c>, <c>git cherry-pick --abort</c>,
    /// <c>git revert --abort</c>, <c>git rebase --abort</c> or <c>git am --abort</c>. Save the
    /// backups first (2.15).
    /// </summary>
    Task AbortAsync(InProgressKind kind, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    // Remotes and the network (2.16 to 2.23, 2.29).

    /// <summary>
    /// Fetches <paramref name="remote"/>, or every remote when null: <c>git fetch --progress
    /// &lt;remote&gt;</c>, and for null one such call per remote in <c>git remote</c>'s order
    /// (D109), leaving out remotes with <c>remote.&lt;name&gt;.skipFetchAll</c>, every call but
    /// the last with <c>--no-auto-maintenance</c>; it goes on after a remote fails and throws one
    /// <see cref="GitException"/> at the end whose standard error has an <c>error: Could not
    /// fetch &lt;remote&gt;</c> line per failed remote. Never <c>--prune</c> or <c>--tags</c>
    /// (D99); with <paramref name="noAutoMaintenance"/> (auto-fetch, D85) every call has
    /// <c>--no-auto-maintenance</c> (2.16, 2.17). Pull's first step is the fetch of one remote (2.18).
    /// </summary>
    Task FetchAsync(string? remote, NetworkOptions options, bool noAutoMaintenance = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pull's second step, after its fetch (2.18, D84): integrates the current branch
    /// <paramref name="branch"/>'s upstream as <c>git pull</c> would, from <c>pull.ff</c>,
    /// <c>branch.&lt;b&gt;.rebase</c> and <c>pull.rebase</c>: <c>git merge --ff-only FETCH_HEAD</c>,
    /// <c>git rebase [--rebase-merges]</c> (with a rebase setting, HEAD that is an ancestor of
    /// the fetched head is fast-forwarded with <c>git merge --ff-only</c>, as git 2.34 and later
    /// do, and the result says <c>FastForward</c>) or <c>git merge --no-edit [--no-ff | --ff] FETCH_HEAD</c>;
    /// a local upstream uses <see cref="GitRef.UpstreamRef"/> in place of <c>FETCH_HEAD</c>.
    /// Conflicts are returned, not thrown.
    /// </summary>
    Task<PullResult> IntegratePullAsync(GitRef branch, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes a local branch: <c>git push --progress [--set-upstream] &lt;remote&gt;
    /// refs/heads/&lt;branch&gt;:&lt;remote ref&gt;</c>, or with a lease
    /// <c>--force-with-lease=&lt;remote ref&gt; --force-if-includes</c> (2.19, 2.20, 4g, D100).
    /// </summary>
    Task PushAsync(PushRequest request, NetworkOptions options, CancellationToken cancellationToken = default);

    /// <summary>Makes <paramref name="remoteBranchRef"/> (<c>refs/remotes/&lt;r&gt;/&lt;x&gt;</c>) the upstream of a local branch: <c>git branch --set-upstream-to=&lt;ref&gt; &lt;branch&gt;</c> (2.21).</summary>
    Task SetUpstreamAsync(string branch, string remoteBranchRef, CancellationToken cancellationToken = default);

    /// <summary>Removes a local branch's upstream: <c>git branch --unset-upstream &lt;branch&gt;</c> (2.21).</summary>
    Task UnsetUpstreamAsync(string branch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the branch <paramref name="branch"/> on <paramref name="remote"/> only while it is
    /// still at <paramref name="expectedCommit"/>: <c>git push --progress
    /// --force-with-lease=refs/heads/&lt;b&gt;:&lt;sha&gt; &lt;remote&gt; --delete refs/heads/&lt;b&gt;</c> (2.22, D100).
    /// </summary>
    Task DeleteRemoteBranchAsync(string remote, string branch, string expectedCommit, NetworkOptions options, CancellationToken cancellationToken = default);

    /// <summary>Adds a remote: <c>git remote add &lt;name&gt; &lt;url&gt;</c> (2.23).</summary>
    Task AddRemoteAsync(string name, string url, CancellationToken cancellationToken = default);

    /// <summary>Renames a remote: <c>git remote rename &lt;old&gt; &lt;new&gt;</c> (2.23).</summary>
    Task RenameRemoteAsync(string name, string newName, CancellationToken cancellationToken = default);

    /// <summary>Changes a remote's URL: <c>git remote set-url &lt;name&gt; &lt;url&gt;</c> (2.23).</summary>
    Task SetRemoteUrlAsync(string name, string url, CancellationToken cancellationToken = default);

    /// <summary>Removes a remote and its remote branches: <c>git remote remove &lt;name&gt;</c>. Save the backups of its remote branches first (2.23).</summary>
    Task RemoveRemoteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Pushes a tag: <c>git push --progress &lt;remote&gt; refs/tags/&lt;t&gt;:refs/tags/&lt;t&gt;</c> (2.29).</summary>
    Task PushTagAsync(string remote, string tag, NetworkOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the tag <paramref name="tag"/> on <paramref name="remote"/> only when it is the tag
    /// object <paramref name="expectedObject"/> (the local tag's, which its backup holds):
    /// <c>git push --progress --force-with-lease=refs/tags/&lt;t&gt;:&lt;sha&gt; &lt;remote&gt;
    /// --delete refs/tags/&lt;t&gt;</c> (2.29, D100).
    /// </summary>
    Task DeleteRemoteTagAsync(string remote, string tag, string expectedObject, NetworkOptions options, CancellationToken cancellationToken = default);

    // Stashes, tags, ignore (2.24 to 2.30).

    /// <summary>
    /// Stashes the changes: <c>git stash push [--include-untracked] [--message &lt;m&gt;]</c> (an
    /// empty message lets git name it). Returns the new stash commit (<c>refs/stash</c>), or null
    /// when git had nothing to stash (<c>No local changes to save</c>) (2.24, 2.31).
    /// </summary>
    Task<string?> StashAsync(string? message, bool includeUntracked, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies or pops the stash whose commit is <paramref name="stashCommit"/>, found again by
    /// its id in <c>git stash list</c> (its position may have changed): <c>git stash apply
    /// stash@{n}</c>, <c>git stash pop stash@{n}</c> or <c>git stash pop --index stash@{n}</c>
    /// with its fallback (<see cref="StashApplyMode"/>). A stash no longer listed throws
    /// <see cref="GitException"/> saying "This stash no longer exists" (2.25, 2.31).
    /// </summary>
    Task<ApplyResult> ApplyStashAsync(string stashCommit, StashApplyMode mode, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Drops the stash whose commit is <paramref name="stashCommit"/>, found by its id as <see cref="ApplyStashAsync"/> finds it: <c>git stash drop stash@{n}</c>. Save its backup first (2.26).</summary>
    Task DropStashAsync(string stashCommit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a tag at <paramref name="target"/>: lightweight (<c>git tag &lt;t&gt; &lt;sha&gt;</c>),
    /// or annotated with <paramref name="message"/> (<c>git tag --annotate --cleanup=whitespace
    /// --file=- &lt;t&gt; &lt;sha&gt;</c>, the message on standard input) (2.27).
    /// </summary>
    Task CreateTagAsync(string name, string target, string? message, CancellationToken cancellationToken = default);

    /// <summary>Deletes a local tag: <c>git tag --delete &lt;t&gt;</c>. Save its backup first (2.28).</summary>
    Task DeleteTagAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends <paramref name="pattern"/> (already escaped, 2.30) as a line of the working tree's
    /// <c>.gitignore</c>, creating it, after a line feed when the file does not end with one. No
    /// git call.
    /// </summary>
    Task AppendToGitignoreAsync(string pattern, CancellationToken cancellationToken = default);
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
    /// <paramref name="environment"/> holds the variables of the clone's prompt scope, which lead
    /// git's and ssh's prompts to the app's dialogs (phase 3 design 3.3); null adds none.
    /// </summary>
    /// <exception cref="GitException">Git reported an error; its message is git's own.</exception>
    Task<IGitRepository> CloneAsync(
        string url,
        string destination,
        IProgress<CloneProgress>? progress,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string?>? environment = null);
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
