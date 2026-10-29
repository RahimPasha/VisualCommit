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

    /// <summary>
    /// The folder the repository was opened from, full and without a trailing separator: the one
    /// the opening's first git call ran in. It is <see cref="WorkingDirectory"/>, a folder inside
    /// it, or either reached by another path, such as through a symbolic link or a junction,
    /// which git resolves in the paths it reports (docs/phase-5-design.md 2.3).
    /// </summary>
    string OpenedFolder => WorkingDirectory;

    /// <summary>
    /// Whether <see cref="OpenedFolder"/> is the working tree's top (docs/phase-6-design.md 1.2,
    /// D204): true with no git call when <c>WorktreeKey.Comparer</c> finds it equal to
    /// <see cref="WorkingDirectory"/>; otherwise one <c>git rev-parse --show-prefix</c> in it, a
    /// read: an empty line is the top spelled another way, anything else a folder inside. A
    /// workspace saves a folder only at its top. A failure throws <see cref="GitException"/>. The
    /// default answers true (the fakes need no change).
    /// </summary>
    Task<bool> OpenedAtTopAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    /// <summary>Reads HEAD, the branches with their upstreams and ahead/behind counts, the remote branches, the tags, the stashes and the remotes.</summary>
    Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Every remote's fetch lines as they are now, in one read (<c>git config --null --get-regexp
    /// ^remote\.</c>, the refs read's own, docs/phase-6-design.md 10.2): what a push's guard reads
    /// inside its operation (10.4), rather than the tab's last refs. The default answers null, not
    /// read (the fakes, whose snapshots keep phase 5's assumption): the guard then has nothing to go by.
    /// </summary>
    Task<FetchRefspecs?> ReadFetchRefspecsAsync(CancellationToken cancellationToken = default) => Task.FromResult<FetchRefspecs?>(null);

    /// <summary>
    /// The repository's URL rewrites (docs/phase-7a-design.md 1.3, 4.3): one <c>git config --null
    /// --get-regexp ^url\.</c>, exit 1 meaning none. Read only while a hosting account is signed
    /// in, so that no earlier count of git calls moves.
    /// </summary>
    Task<UrlRewrites> ReadUrlRewritesAsync(CancellationToken cancellationToken = default);

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

    /// <summary>
    /// As <see cref="LoadCommitsAsync(Task{RepoRefs}, Action{IReadOnlyList{CommitInfo}}, CancellationToken)"/>
    /// with what the graph hides or solos (docs/phase-5-design.md 8.2, D178): git gets
    /// <paramref name="filter"/>'s revisions, and a stash whose base is not shown waits until its base
    /// comes and is left out if it never does (amending D54 while a filter is on). A null filter is
    /// exactly the overload without one: the same command, byte for byte. The default
    /// implementation takes only a null filter.
    /// </summary>
    Task LoadCommitsAsync(Task<RepoRefs> refs, Graph.GraphFilter? filter, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default) =>
        filter is null
            ? LoadCommitsAsync(refs, onPage, cancellationToken)
            : throw new NotSupportedException("This repository does not load a filtered graph.");

    /// <summary>Reads the full message, the author and committer and the changed files of one commit or stash.</summary>
    Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default);

    /// <summary>Reads the staged, unstaged and untracked files (<c>git status --porcelain=v2</c>).</summary>
    Task<WorkingTreeStatus> ReadStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The operation in progress alone, read from the files git leaves in the git folder without
    /// the status: no git call while nothing is in progress (D89). For the checks that need only
    /// whether one is in progress and its kind (<see cref="InProgressState.IsActive"/>,
    /// <see cref="InProgressState.Kind"/>): whether a stop is empty
    /// (<see cref="InProgressState.Empty"/>, <see cref="RebaseStopKind.Empty"/>) needs the status,
    /// so no stop is read as empty here. The default implementation reads the status.
    /// </summary>
    async Task<InProgressState> ReadInProgressStateAsync(CancellationToken cancellationToken = default) =>
        (await ReadStatusAsync(cancellationToken).ConfigureAwait(false)).InProgress;

    /// <summary>
    /// The conflicted paths, each once: those with unmerged index entries (<c>git ls-files
    /// --unmerged -z</c>), the status's conflicted files without the rest of the status. The default
    /// implementation reads the status.
    /// </summary>
    async Task<IReadOnlyList<string>> ReadConflictedPathsAsync(CancellationToken cancellationToken = default) =>
        [.. (await ReadStatusAsync(cancellationToken).ConfigureAwait(false)).Unstaged
            .Where(file => file.Kind == FileChangeKind.Conflicted)
            .Select(file => file.Path)
            .Distinct(StringComparer.Ordinal)];

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

    /// <summary>
    /// Whether <paramref name="sha"/> names a commit that exists: <c>git cat-file -e
    /// &lt;sha&gt;^{commit}</c> (docs/phase-5-design.md 5.2). A commit a reflog entry or a backup
    /// kept may have been pruned by a <c>gc</c> since the page read it; the operations started from
    /// those rows check it first.
    /// </summary>
    Task<bool> CommitExistsAsync(string sha, CancellationToken cancellationToken = default);

    /// <summary>The repository's worktrees and the branch each has checked out: <c>git worktree list --porcelain</c> (2.7).</summary>
    Task<IReadOnlyList<WorktreeInfo>> ReadWorktreesAsync(CancellationToken cancellationToken = default);

    /// <summary>The configured remotes with their URLs: <c>git remote</c> and <c>remote.&lt;name&gt;.url</c> and <c>.pushurl</c>, sorted by name (2.23). URLs are as configured, before any <c>insteadOf</c> rule: show them through <see cref="RemoteInfo.ShownUrl"/> (D103).</summary>
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
    /// <see cref="SnapshotForHardResetAsync"/>, with what the undo history needs of the snapshot
    /// it made (docs/phase-5-design.md 7.7 step 3): its second parent and its files, which the
    /// recorder would otherwise read back from the commit. The default implementation knows only
    /// the commit (<see cref="HardResetSnapshot.Paths"/> null).
    /// </summary>
    /// <exception cref="IOException">A repository is nested in a folder the reset would delete.</exception>
    async Task<HardResetSnapshot?> SnapshotForHardResetWithFilesAsync(string target, CancellationToken cancellationToken = default) =>
        await SnapshotForHardResetAsync(target, cancellationToken).ConfigureAwait(false) is { } commit ? new HardResetSnapshot(commit) : null;

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

    /// <summary>
    /// Checks out the local branch <paramref name="branch"/> (short name): <c>git switch --no-guess
    /// [--no-overwrite-ignore] &lt;branch&gt;</c> (2.1), with <paramref name="keepIgnored"/> refusing
    /// where an ignored file is in the way (Keep ignored files, D193, docs/phase-6-design.md 9.11).
    /// </summary>
    Task CheckoutBranchAsync(string branch, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the local branch <paramref name="branch"/> tracking the remote branch
    /// <paramref name="remoteBranchRef"/> (<c>refs/remotes/&lt;r&gt;/&lt;b&gt;</c>) and checks it out:
    /// <c>git switch --create &lt;branch&gt; --track [--no-overwrite-ignore] &lt;remoteBranchRef&gt;</c>
    /// (2.2), with <paramref name="keepIgnored"/> as <see cref="CheckoutBranchAsync"/> has it.
    /// </summary>
    Task CheckoutTrackingBranchAsync(string branch, string remoteBranchRef, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detaches HEAD at <paramref name="commit"/>: <c>git switch --detach [--no-overwrite-ignore]
    /// &lt;sha&gt;</c> (2.3), with <paramref name="keepIgnored"/> refusing where an ignored file is in
    /// the way (docs/phase-5-design.md 9.1, D179; docs/phase-6-design.md 9.11). Every checkout of a
    /// commit the current branch reaches (the automatic stash) passes true: git would otherwise
    /// overwrite an ignored file that the commit tracks, and the way back would delete it. A
    /// refusal changes nothing.
    /// </summary>
    Task CheckoutDetachedAsync(string commit, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes <paramref name="paths"/> of <paramref name="source"/> (a tree-ish: <c>&lt;stash&gt;^3</c>)
    /// into the working tree, leaving the index alone: <c>git restore --source=&lt;source&gt;
    /// --worktree -- &lt;paths&gt;</c>. The automatic stash puts back the untracked files git deleted
    /// before it failed half-way (9.2 step 3).
    /// </summary>
    Task RestoreWorkingTreeFilesAsync(string source, IReadOnlyList<string> paths, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// After a <c>git stash push</c> that saved <paramref name="stashCommit"/> but failed while it
    /// cleared the working tree (a file held open on Windows, docs/phase-5-design.md 0.7, 15.9):
    /// puts back from the stash what git had already taken out, leaving the index alone (git did
    /// not write it). Its untracked files that are missing (<c>git restore
    /// --source=&lt;stash&gt;^3 --worktree</c>: git's clean deleted them), then its tracked paths
    /// whose files no longer match it (<c>git diff --name-only &lt;stash&gt;</c>, then <c>git
    /// restore --source=&lt;stash&gt; --worktree</c>: git's reset rewrote every file it could
    /// before it stopped at the held one, which still matches and is not touched). True when every
    /// path of the stash is as the stash holds it again.
    /// </summary>
    Task<bool> PutBackUnclearedStashAsync(string stashCommit, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of <paramref name="paths"/> (relative to the working tree, files or folders) git
    /// ignores: <c>git check-ignore --stdin -z</c> (9.3). A checkout refused only for ignored files
    /// in the way says so in words of its own. A read.
    /// </summary>
    Task<IReadOnlySet<string>> FindIgnoredAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default);

    // Keep ignored files (D193, D196, docs/phase-6-design.md 9.3 to 9.8): the ignored files an
    // operation without git's own option would overwrite or delete, distinct, in byte order. Each
    // is a read (GIT_OPTIONAL_LOCKS=0) that the operation makes inside itself before its first
    // write; a read that fails is logged and the operation goes on to git.

    /// <summary>
    /// The ignored files in the way of merging <paramref name="source"/> into <paramref name="into"/>
    /// (<c>HEAD</c>, or <c>refs/heads/&lt;A&gt;</c> before "Check out and merge"'s checkout): the
    /// paths the source has and <paramref name="into"/> lacks that the merge writes (9.4).
    /// </summary>
    Task<IReadOnlyList<string>> FindIgnoredInTheWayOfMergeAsync(string into, string source, CancellationToken cancellationToken = default);

    /// <summary>
    /// The ignored files in the way of cherry-picking (or with <paramref name="revert"/> reverting)
    /// <paramref name="commits"/> in the order git applies them (9.5); a merge reverted with
    /// <c>-m 1</c> names its first parent as <paramref name="mergeParent"/>.
    /// </summary>
    Task<IReadOnlyList<string>> FindIgnoredInTheWayOfCommitsAsync(IReadOnlyList<string> commits, bool revert, string? mergeParent = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The ignored files in the way of rebasing <paramref name="tip"/> (<c>HEAD</c>, or a branch's
    /// full ref before "Check out and rebase"'s checkout) onto <paramref name="onto"/> (9.6): what
    /// git's first checkout of <paramref name="onto"/> and the commits it may replay write.
    /// When the range holds a merge, also what the commits a <c>--rebase-merges</c> rebase resets
    /// to (the side branches' original branch points) track: git may keep merges by
    /// <c>rebase.rebaseMerges</c> (Git 2.42 and later) even in a plain <c>git rebase</c>.
    /// </summary>
    Task<IReadOnlyList<string>> FindIgnoredInTheWayOfRebaseAsync(string tip, string onto, CancellationToken cancellationToken = default);

    /// <summary>The ignored files in the way of an interactive rebase or a squash by <paramref name="plan"/> (9.7), from where git starts it.</summary>
    Task<IReadOnlyList<string>> FindIgnoredInTheWayOfRebaseAsync(RebasePlan plan, CancellationToken cancellationToken = default);

    /// <summary>The ignored files in the way of applying or popping the stash <paramref name="stashCommit"/> (9.8), from its paths (<see cref="ReadStashPathsAsync"/>).</summary>
    Task<IReadOnlyList<string>> FindIgnoredInTheWayOfStashAsync(string stashCommit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the branch <paramref name="name"/> at <paramref name="startPoint"/> (a full SHA,
    /// <c>refs/heads/…</c>, <c>refs/remotes/…</c> or <c>refs/tags/&lt;t&gt;^{commit}</c>):
    /// <c>git branch [--no-track] &lt;name&gt; &lt;start&gt;</c>, or with <paramref name="checkout"/>
    /// <c>git switch --create &lt;name&gt; [--no-track] [--no-overwrite-ignore] &lt;start&gt;</c>. A
    /// remote branch as start becomes the upstream, as git's <c>branch.autoSetupMerge</c> says
    /// (2.4), unless <paramref name="noTrack"/>: a start point fetched ambiguously, whose upstream
    /// would be the wrong ref (D201, docs/phase-6-design.md 10.4, 9.11). <paramref name="keepIgnored"/>
    /// is the checkout's, as <see cref="CheckoutBranchAsync"/> has it; without a checkout nothing is written.
    /// </summary>
    Task CreateBranchAsync(string name, string startPoint, bool checkout, bool noTrack, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default);

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
    /// [--no-ff | --ff-only] [--no-overwrite-ignore] &lt;reference&gt;</c> (2.8 to 2.10, D94), with
    /// <paramref name="keepIgnored"/> git's option (exact for a fast-forward,
    /// docs/phase-6-design.md 9.4). Conflicts are returned, not thrown: git failed and the status
    /// lists a conflicted file. A merge (or pick, or revert) refused because the index already had
    /// conflicts is a failure, a <see cref="GitException"/>, not a conflict of its own.
    /// </summary>
    Task<MergeResult> MergeAsync(string reference, MergeMode mode, bool keepIgnored, NetworkOptions? options = null, CancellationToken cancellationToken = default);

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
    /// Fetches <paramref name="sourceRef"/> of <paramref name="remote"/> (a pull request's head,
    /// docs/phase-7a-design.md 1.3, 10.5) into <c>FETCH_HEAD</c> only: no ref and no configuration
    /// written. Returns the commit's SHA read from <c>FETCH_HEAD</c>'s line, or null when that line
    /// is not this fetch's.
    /// </summary>
    Task<string?> FetchToFetchHeadAsync(string remote, string sourceRef, NetworkOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pull's second step, after its fetch (2.18, D84): integrates the current branch
    /// <paramref name="branch"/>'s upstream as <c>git pull</c> would, from <c>pull.ff</c>,
    /// <c>branch.&lt;b&gt;.rebase</c> and <c>pull.rebase</c>: <c>git merge --ff-only FETCH_HEAD</c>,
    /// <c>git rebase [--rebase-merges]</c> (with a rebase setting, HEAD that is an ancestor of
    /// the fetched head is fast-forwarded with <c>git merge --ff-only</c>, as git 2.34 and later
    /// do, and the result says <c>FastForward</c>) or <c>git merge --no-edit [--no-ff | --ff] FETCH_HEAD</c>;
    /// a local upstream uses <see cref="GitRef.UpstreamRef"/> in place of <c>FETCH_HEAD</c>.
    /// Conflicts are returned, not thrown. The same as the phase 4 overload with
    /// <see cref="PullIntegration.AsConfigured"/> and no callback (docs/phase-4-design.md section 6).
    /// </summary>
    Task<PullResult> IntegratePullAsync(GitRef branch, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes a local branch: <c>git push --progress [--set-upstream] &lt;remote&gt;
    /// refs/heads/&lt;branch&gt;:&lt;remote ref&gt;</c>, or with a lease
    /// <c>--force-with-lease=&lt;remote ref&gt; --force-if-includes</c> (2.19, 2.20, 4g, D100).
    /// Returns what it changed on the remote, read from git's own summary lines (docs/phase-5-design.md
    /// 7.7, D170): the undo history's lease values.
    /// </summary>
    Task<PushOutcome> PushAsync(PushRequest request, NetworkOptions options, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Pushes a tag: <c>git push --progress &lt;remote&gt; refs/tags/&lt;t&gt;:refs/tags/&lt;t&gt;</c> (2.29).
    /// Returns what it changed on the remote, as <see cref="PushAsync"/>.
    /// </summary>
    Task<PushOutcome> PushTagAsync(string remote, string tag, NetworkOptions options, CancellationToken cancellationToken = default);

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
    /// The paths the stash <paramref name="stashCommit"/> would write (docs/phase-5-design.md 9.2,
    /// 9.3): those its changes touch, both sides of a rename (<c>git diff-tree -r -z --no-renames
    /// --name-status &lt;s&gt;^1 &lt;s&gt;</c>, whose added ones are <see cref="StashPaths.Added"/>),
    /// and its untracked files (<c>git ls-tree -r -z --name-only &lt;s&gt;^3</c>, none without a
    /// third parent); with what its index commit changes (<see cref="StashPaths.Staged"/>,
    /// <c>&lt;s&gt;^1 &lt;s&gt;^2</c>). The app checks an apply or a pop against them before git runs,
    /// the undo history records them, and an undo's pop checks what stands in its way. A read,
    /// made once per stash commit named by its full id (a commit never changes): later calls
    /// share it.
    /// </summary>
    Task<StashPaths> ReadStashPathsAsync(string stashCommit, CancellationToken cancellationToken = default);

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

    // Phase 4: rebase and conflict resolution (docs/phase-4-design.md). Each takes the operation's
    // NetworkOptions (a filter or hook can prompt) and a token, as phase 3's writes; a write runs
    // inside one operation of the queue and throws GitException with git's own text when git
    // refuses, while a stop (conflicts, an empty commit, the other stops of 5.1) is returned.

    // Rebase (sections 1 to 3, 6; work package WP1).

    /// <summary>
    /// Rebases the current branch onto <paramref name="onto"/> (a full SHA read when the menu
    /// opened, D124): <c>git rebase &lt;onto&gt;</c>, never with a branch argument and with no
    /// option that changes git's configured behaviour (1.3, D142). The caller saves the backup
    /// first. Classified by 1.4's table: up to date, rebased (with
    /// <see cref="RebaseResult.AutostashConflicted"/> when <c>rebase.autoStash</c>'s changes
    /// conflicted when put back) or stopped; a refusal throws.
    /// </summary>
    Task<RebaseResult> RebaseAsync(string onto, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether git's configuration has <c>rebase.autoStash</c> set (1.3 step 1, D142), so that git
    /// stashes a dirty working tree itself before a rebase and the app does not refuse it: <c>git
    /// config --type=bool --get rebase.autoStash</c>; unset (exit 1) is false. A read:
    /// <c>GIT_OPTIONAL_LOCKS=0</c>. A value git cannot read as a boolean throws
    /// <see cref="GitException"/>.
    /// </summary>
    Task<bool> ReadRebaseAutoStashAsync(NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The commits an interactive rebase of <paramref name="branch"/> (a full ref name: HEAD's
    /// branch, or a drop's source that is not checked out) would replay, read when its list opens
    /// (2.3). <paramref name="fromCommit"/> ("from here", 2.1): <c>git log --reverse
    /// --topo-order &lt;onto&gt;..&lt;branch&gt;</c>, <paramref name="onto"/> being the base
    /// commit; otherwise the commits git's own todo would hold, as git's sequencer chooses them:
    /// <c>git log --reverse --topo-order --no-merges --right-only --cherry-mark
    /// &lt;onto&gt;...&lt;branch&gt;</c>, less the commits marked <c>=</c> (their patch is already
    /// upstream) that do not start empty; a commit that starts empty is kept even when marked, as
    /// git keeps it (docs/phase-4-design.md 14.5). <c>git rev-list --merges --count
    /// &lt;onto&gt;..&lt;branch&gt;</c> gives <see cref="RebaseList.MergeCount"/>. A read:
    /// <c>GIT_OPTIONAL_LOCKS=0</c>.
    /// </summary>
    Task<RebaseList> ReadRebaseListAsync(string onto, string branch, bool fromCommit, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs an interactive rebase (2.4, 2.5) or a squash (section 3): refuses unless HEAD is on
    /// <see cref="RebasePlan.BranchRef"/> at <see cref="RebasePlan.ExpectedTip"/> (D124), then
    /// <c>git rebase -i &lt;onto&gt;</c> with <c>GIT_SEQUENCE_EDITOR</c> set for this call to the
    /// app's shell function, which copies the todo built from the plan (<c>RebaseTodo</c>, Core)
    /// and its message files into <c>rebase-merge/</c>. The caller saves the backup first. A todo
    /// git refused (<c>error: invalid line</c>, the <c>missingCommitsCheck</c> warning) is aborted
    /// and thrown. Classified as <see cref="RebaseAsync"/>.
    /// </summary>
    Task<RebaseResult> InteractiveRebaseAsync(RebasePlan plan, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pull's second step with its integration chosen (section 6, D147):
    /// <see cref="PullIntegration.AsConfigured"/> is phase 3's
    /// <see cref="IntegratePullAsync(GitRef, NetworkOptions?, CancellationToken)"/>;
    /// <see cref="PullIntegration.Rebase"/> is <c>git merge --ff-only &lt;head&gt;</c> when the
    /// branch has nothing of its own, otherwise <c>git rebase</c> without arguments (from the
    /// upstream's fork point, as <c>git pull --rebase</c>): of git's pull settings it reads
    /// <c>pull.ff</c> alone and refuses a value <c>git pull --rebase</c> refuses (D231), and no rebase
    /// setting. Before
    /// either rebases, <paramref name="beforeRebase"/> is called with the branch's old tip (the
    /// app saves 1.2's backup there). Both pulls classify a rebase with 1.4's rules: every stop is
    /// <see cref="PullOutcome.Conflicts"/>. <paramref name="pullOptions"/> (docs/phase-6-design.md
    /// 8.9, 9.10) gives the pull settings read before the fetch, which then decide instead of a
    /// read of their own, and Keep ignored files, under which an ignored file in the integration's
    /// way gives <see cref="PullOutcome.IgnoredFilesInTheWay"/> and nothing is integrated; null is
    /// phase 4's pull.
    /// </summary>
    Task<PullResult> IntegratePullAsync(
        GitRef branch,
        PullIntegration integration,
        Func<string, Task>? beforeRebase,
        NetworkOptions? options = null,
        PullIntegrationOptions? pullOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Git's pull settings for <paramref name="branch"/> (short name) as Pull reads them, before its
    /// fetch (docs/phase-6-design.md 8.9): <c>pull.ff</c>, then <c>branch.&lt;b&gt;.rebase</c> or,
    /// unset, <c>pull.rebase</c>, booleans read as git reads them. A value Pull does not
    /// understand fills <see cref="PullConfiguration.Problem"/> instead of throwing. A read.
    /// </summary>
    Task<PullConfiguration> ReadPullConfigurationAsync(string branch, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>pull.ff</c> alone, as <c>git pull --rebase</c> reads it before it fetches
    /// (docs/phase-6-design.md 8.9, D231): the rebase chosen for one pull reads no rebase setting.
    /// A boolean or <c>only</c> fills <see cref="PullConfiguration.FastForward"/> (the rebase
    /// setting none, no value); a value git refuses fills <see cref="PullConfiguration.Problem"/>
    /// with <c>pull.ff</c> as its key. A read: <c>git config --get pull.ff</c>, and
    /// <c>git config --type=bool --get pull.ff</c> for an uncommon value.
    /// </summary>
    Task<PullConfiguration> ReadPullFastForwardSettingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes this repository's own settings (the settings screen's "This repository",
    /// docs/phase-6-design.md 8.3): <c>git config --local --replace-all &lt;key&gt; &lt;value&gt;</c>, or
    /// <c>--unset-all &lt;key&gt;</c> for a null value (exit 5 counting as done), one call per write,
    /// in order, in the working tree; the first failure stops them and throws <see cref="GitException"/>.
    /// </summary>
    Task WriteConfigAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellationToken = default);

    // Lists of picks and reverts, Continue and Skip (sections 4, 5, 8; work package WP2).

    /// <summary>
    /// Cherry-picks several commits with one git call that takes the whole list (D136):
    /// <c>git cherry-pick &lt;A&gt; &lt;B&gt; &lt;C&gt;</c>, <paramref name="commitsOldestFirst"/>
    /// in that order. One commit keeps phase 3's
    /// <see cref="CherryPickAsync(string, NetworkOptions?, CancellationToken)"/>. How it ended is
    /// read from git's files (section 4's table); a list refused at its start is cleared with
    /// <c>git cherry-pick --quit</c>, which keeps the user's changes, and throws (D145).
    /// </summary>
    Task<SequenceResult> CherryPickAsync(IReadOnlyList<string> commitsOldestFirst, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverts several commits with one git call (D136): <c>git revert --no-edit &lt;C&gt;
    /// &lt;B&gt; &lt;A&gt;</c>, <paramref name="commitsNewestFirst"/> in that order; no merge
    /// commits (those are reverted one at a time). Results as
    /// <see cref="CherryPickAsync(IReadOnlyList{string}, NetworkOptions?, CancellationToken)"/>.
    /// </summary>
    Task<SequenceResult> RevertAsync(IReadOnlyList<string> commitsNewestFirst, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends a list of picks or reverts without undoing anything: <c>git cherry-pick --quit</c>
    /// or <c>git revert --quit</c> for <paramref name="kind"/>, which removes <c>sequencer/</c>
    /// and keeps the user's changes (section 4, D145).
    /// </summary>
    Task QuitSequenceAsync(InProgressKind kind, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Continue for <paramref name="state"/> (5.4): commits a merge, pick or revert with
    /// <paramref name="message"/> (<c>git commit --file=- --cleanup=whitespace</c>), then for a list
    /// <c>git cherry-pick --continue</c> (<c>revert</c>); for a rebase stopped with something
    /// staged, writes a group's message file, commits with the stopped commit's author from
    /// <c>rebase-merge/author-script</c> (<c>--no-verify</c>) and runs <c>git rebase
    /// --continue</c>; for the app's failed amend, the amend again, then <c>--continue</c>;
    /// otherwise <c>git rebase --continue</c>. <paramref name="message"/> is the stage panel's,
    /// or null for the operation's own (5.3). A refusal throws.
    /// </summary>
    Task<ContinueResult> ContinueAsync(InProgressState state, string? message, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Skip for <paramref name="state"/> (5.4): <c>git cherry-pick --skip</c>, <c>git revert
    /// --skip</c> or <c>git rebase --skip</c>; for <see cref="RebaseStopKind.OwnAmendFailed"/>
    /// <c>git rebase --continue</c> (on without the new message). The question and the
    /// <c>skip</c> backup come before, in the app. A refusal throws.
    /// </summary>
    Task<ContinueResult> SkipAsync(InProgressState state, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The message the stage panel fills in for <paramref name="state"/>'s stopped commit (5.3),
    /// or null when there is none: <c>MERGE_MSG</c> less git's trailing comment block for a merge,
    /// pick or revert; <c>rebase-merge/message</c> with no line removed for a rebase, or the
    /// group's <c>rebase-merge/vc-message-&lt;n&gt;</c> inside a group the app wrote; a
    /// <c>fixup</c> or <c>squash</c> stop of a rebase started elsewhere less git's comment lines.
    /// No git call beyond <c>core.commentChar</c>.
    /// </summary>
    Task<string?> ReadOperationMessageAsync(InProgressState state, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    // Conflicts, the merge tool and the backups' clean-up (sections 9, 11; work package WP3).

    /// <summary>
    /// What the resolver reads of a conflicted file (11.3, 11.8): the working-tree file's bytes (a
    /// symbolic link's target; none for a missing file or a folder), its marker size (<c>git
    /// check-attr -z conflict-marker-size -- &lt;p&gt;</c>, a positive number or the default 7),
    /// whether a side's blob is binary (a NUL in its first 8000 bytes, streamed with <c>git cat-file
    /// blob</c>; a missing side, a link and a submodule are not), and whether a side is a symbolic
    /// link (mode <c>120000</c>). Reads only.
    /// </summary>
    Task<ConflictContent> ReadConflictAsync(ConflictEntry entry, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// One side of a conflicted file in its working-tree form (11.6): <c>git cat-file --filters
    /// --path=&lt;p&gt; &lt;stage id&gt;</c>, byte for byte (D66), which writes nothing; null when
    /// that side has no file or is a submodule.
    /// </summary>
    Task<byte[]?> ReadConflictSideAsync(ConflictEntry entry, ConflictSide side, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a text conflict resolved with the resolver's result (11.7): writes
    /// <paramref name="content"/> byte for byte to the working-tree file <paramref name="path"/>,
    /// then <c>git add -- &lt;p&gt;</c>. A symbolic link at the path is refused
    /// (<see cref="IOException"/>): nothing is written through it.
    /// </summary>
    Task WriteResolutionAsync(string path, byte[] content, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a conflict with one side (11.8), from the path's unmerged stages
    /// (<c>git ls-files --unmerged</c>). When both sides have the file (both modified, both added:
    /// a binary file or a symbolic link), <c>git checkout --ours|--theirs -- &lt;p&gt;</c>, then
    /// <c>git add -- &lt;p&gt;</c>; a working-tree file that differs from that side (<c>git
    /// hash-object</c>) is saved first in a discard snapshot (D67's <c>discard-&lt;time&gt;</c>), so
    /// an edit is never lost. When one side has it ("Keep the file", "Keep it", a file moved aside
    /// by a file/folder clash), the file git left is staged as it is (<c>git add</c>), and checked
    /// out from <paramref name="side"/> first only when it is missing (git refuses a side that lacks
    /// it). A path that is not unmerged, or a submodule, is refused with <see cref="IOException"/>
    /// and nothing changes. Never <c>git restore --ours</c> (0.4).
    /// </summary>
    Task TakeSideAsync(string path, ConflictSide side, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a conflict by deleting the file (11.8: "Delete it", both deleted): <c>git rm --
    /// &lt;p&gt;</c>. Git deletes the working-tree file whatever it holds, so one that matches none of
    /// the path's stages (the user edited it) is saved first in a discard snapshot (D67). A path
    /// that is not unmerged, or a submodule, is refused with <see cref="IOException"/>.
    /// </summary>
    Task RemoveConflictedAsync(string path, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>The merge tool git's configuration names (<c>git config --get merge.tool</c>), or null when none (11.9).</summary>
    Task<string?> ReadMergeToolAsync(NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the external merge tool on one conflicted file and waits for it (11.9, D149):
    /// <c>git -c mergetool.keepBackup=false mergetool --tool=&lt;tool&gt; --no-prompt -- &lt;p&gt;</c>.
    /// <see cref="MergeToolResult.Staged"/> when the path is no longer unmerged afterwards. A tool
    /// that ran and did not resolve the file (git's <c>merge of &lt;p&gt; failed</c>, after which git
    /// puts the file back) is not staged; a tool that did not run (unknown to git, or not found:
    /// <c>The merge tool &lt;t&gt; is not available as …</c>) throws <see cref="GitException"/>.
    /// Cancelling stops git and the tool. Afterwards (also after a cancel or a failure) the files
    /// git leaves beside the file (<c>&lt;name&gt;_BACKUP_&lt;n&gt;&lt;.ext&gt;</c>, <c>_BASE_</c>,
    /// <c>_LOCAL_</c>, <c>_REMOTE_</c>) are deleted when they did not exist before. A path that is
    /// not unmerged, or a submodule, is refused with <see cref="IOException"/> before git runs.
    /// </summary>
    Task<MergeToolResult> RunMergeToolAsync(string path, string tool, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of <paramref name="paths"/> have conflict markers in their staged content
    /// (docs/phase-4-design.md 5.1 "Markers", 7.2): <c>git diff --cached --check -- &lt;paths&gt;</c>,
    /// read from its <c>&lt;path&gt;:&lt;line&gt;: leftover conflict marker</c> lines with the
    /// marker size of each path's <c>conflict-marker-size</c>; exit 2 (a problem found) is a result,
    /// and whitespace errors are not reported. Continue asks first when any of the paths that were
    /// conflicted at the stop is returned. No path gives an empty list without a git call. In the
    /// order given. A read: <c>GIT_OPTIONAL_LOCKS=0</c>.
    /// </summary>
    Task<IReadOnlyList<string>> ReadStagedConflictMarkersAsync(IReadOnlyList<string> paths, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The backups' clean-up (section 9, D139): reads <c>refs/visualcommit/backup/</c> with one
    /// <c>git for-each-ref</c>, groups the refs into backups by 9's pattern (the refs that share
    /// kind, time and number), and deletes those older than <see cref="BackupRetention.Days"/>
    /// (by the time in the name, a time later than now counting as now), then, while more than
    /// <see cref="BackupRetention.Count"/> remain, the oldest, in one <c>git update-ref --stdin</c>
    /// transaction of <c>delete &lt;ref&gt; &lt;old value&gt;</c> lines. "Now" is the backups' clock.
    /// Nothing goes while an operation is in progress in any worktree (the main git folder and every
    /// <c>&lt;common&gt;/worktrees/*/</c>: <c>rebase-merge/</c>, <c>rebase-apply/</c>,
    /// <c>MERGE_HEAD</c>, <c>sequencer/</c>, a pick head; a Debug log line says why it waits), nor a
    /// backup with a ref in <paramref name="keep"/> (full ref names: the restore bar's snapshot,
    /// which still counts), nor a ref that fits no scheme (not counted either). A ref changed since
    /// the read fails the transaction: nothing is deleted, a Warning is logged and the list is
    /// empty. One Info line per backup removed: <c>&lt;repo&gt;: Removed backup
    /// &lt;kind&gt;-&lt;time&gt;[-&lt;n&gt;] (older than 30 days)</c> or <c>(more than 50
    /// backups)</c>. Returns what it removed, oldest first.
    /// </summary>
    Task<IReadOnlyList<RemovedBackup>> CleanUpBackupsAsync(
        BackupRetention retention,
        IReadOnlySet<string> keep,
        NetworkOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The read half of <see cref="CleanUpBackupsAsync"/>, which deletes nothing: the names of the
    /// backups (<c>&lt;kind&gt;-&lt;time&gt;[-&lt;n&gt;]</c>) the clean-up would remove now, oldest
    /// first; empty while an operation is in progress in any worktree. The app reads it when a
    /// repository has opened and queues the clean-up only when it is not empty (section 9 (b)).
    /// A read: <c>GIT_OPTIONAL_LOCKS=0</c>.
    /// </summary>
    Task<IReadOnlyList<string>> FindBackupsToRemoveAsync(
        BackupRetention retention,
        IReadOnlySet<string> keep,
        NetworkOptions? options = null,
        CancellationToken cancellationToken = default);

    // Phase 5: history, traceability and undo (docs/phase-5-design.md; the work packages of its
    // section 16). The reads run with GIT_OPTIONAL_LOCKS=0 outside the queue; an undo step runs
    // inside one operation of the queue.

    // File history, blame and the tree's files (sections 4.1 to 4.5; WP1).

    /// <summary>
    /// A file's history (4.1, 0.1): <c>git -c core.quotePath=true log --follow --root -M
    /// --name-status --no-show-signature --no-color --encoding=UTF-8 --format=&lt;the graph's&gt;
    /// &lt;start&gt; -- &lt;path&gt;</c>, newest first, following renames, without merges.
    /// <paramref name="onRow"/> receives each commit as git prints it, on a background thread,
    /// never two at once. No commit has touched an untracked path: no row. Unborn HEAD throws
    /// <see cref="GitException"/>.
    /// </summary>
    /// <param name="startSha">The commit the history starts from, a full SHA resolved when the page opened (it stays fixed).</param>
    /// <param name="path">The file's path at <paramref name="startSha"/>, repository-relative with <c>/</c>.</param>
    /// <param name="onRow">Receives each row.</param>
    /// <param name="cancellationToken">Closing the page cancels the read.</param>
    Task ReadFileHistoryAsync(string startSha, string path, Action<FileHistoryRow> onRow, CancellationToken cancellationToken = default);

    /// <summary>
    /// A file's blame (4.2, 0.1): <c>git -c i18n.logOutputEncoding=UTF-8 blame --porcelain
    /// --no-textconv [&lt;sha&gt;] -- &lt;path&gt;</c>, of the commit <paramref name="sha"/>'s version,
    /// or of the working tree's with null. Git's failure throws <see cref="GitException"/> with
    /// git's message; a <c>blame.ignoreRevsFile</c> naming a missing file throws
    /// <see cref="BlameIgnoreRevsFileMissingException"/>.
    /// </summary>
    Task<BlameResult> ReadBlameAsync(string? sha, string path, CancellationToken cancellationToken = default);

    /// <summary>Every file of the commit <paramref name="sha"/>'s tree, by path: <c>git ls-tree -r -z --name-only &lt;sha&gt;</c> (4.5, D164).</summary>
    Task<IReadOnlyList<string>> ReadTreeFilesAsync(string sha, CancellationToken cancellationToken = default);

    // The reflog and the backups (section 5; WP1).

    /// <summary>
    /// One reflog (5.2, D180) through git, never <c>.git/logs</c>: <c>git log --walk-reflogs
    /// --ignore-missing -z -n &lt;limit&gt;</c> twice (the entries, then their times with
    /// <c>--date=raw</c>), zipped by position and read again when the two disagree (the ref moved
    /// between them). A ref without a reflog gives <see cref="ReflogResult.Empty"/>.
    /// </summary>
    /// <param name="fullRef">
    /// <c>HEAD</c>, or the full name of any ref that has a reflog: a local branch's (the page's
    /// sources), or a remote branch's (<c>refs/remotes/…</c>), whose <c>update by push</c> entries
    /// give a force push's backup its lease (7.10); the page's source menu leaves those out.
    /// </param>
    /// <param name="limit">The most entries to read (the page reads 500).</param>
    /// <param name="cancellationToken">Closing the page cancels the read.</param>
    Task<ReflogResult> ReadReflogAsync(string fullRef, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of <paramref name="shas"/> no branch, tag or remote branch reaches, HEAD aside (5.2):
    /// one <c>git rev-list --ignore-missing --stdin --not --branches --tags --remotes HEAD</c> with
    /// the SHAs on standard input. A SHA that no longer exists is not lost: it is not in the result.
    /// </summary>
    Task<IReadOnlySet<string>> FindLostCommitsAsync(IReadOnlyCollection<string> shas, CancellationToken cancellationToken = default);

    /// <summary>
    /// The backups the clean-up has left (5.3), newest first, read with one <c>git for-each-ref</c>
    /// of <c>refs/visualcommit/backup/</c> and grouped as the clean-up groups them (phase 4's
    /// pattern); a ref that fits no scheme is left out.
    /// </summary>
    Task<IReadOnlyList<BackupInfo>> ReadBackupsAsync(CancellationToken cancellationToken = default);

    // Compare and search (sections 3 and 6; WP1).

    /// <summary>
    /// What two commits or branches differ in (6.3, D177): the names resolved to commits first; for
    /// <paramref name="threeDot"/> <c>git merge-base &lt;from&gt; &lt;to&gt;</c> (none: all differences,
    /// <see cref="CompareBase.NoCommonHistory"/>); then <c>git diff-tree -r -z --name-status -M
    /// --no-ext-diff --no-color --no-commit-id &lt;old&gt; &lt;new&gt;</c> with two SHAs; and with
    /// <paramref name="countCommits"/> <c>git rev-list --left-right --count &lt;from&gt;...&lt;to&gt;</c>.
    /// The same commit on both sides runs no git beyond the names'.
    /// </summary>
    /// <param name="from">The side compared from: a full ref name or a full SHA.</param>
    /// <param name="to">The side compared to: a full ref name or a full SHA.</param>
    /// <param name="threeDot">The changes on <paramref name="to"/> since it left <paramref name="from"/> (branches); false for the two trees.</param>
    /// <param name="countCommits">Count the commits only on each side (branches).</param>
    /// <param name="cancellationToken">Closing the comparison cancels it.</param>
    Task<CompareResult> CompareAsync(string from, string to, bool threeDot, bool countCommits, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches exactly the graph's rows (3.2, D174): <c>git log --no-walk=unsorted --stdin
    /// --no-show-signature --no-color --encoding=UTF-8 --format=%H &lt;mode's options&gt;</c> with
    /// <paramref name="shas"/> on standard input, the first 2,000, then 20,000 per call, one call
    /// after another. <paramref name="onMatch"/> receives each matching SHA as git prints it, on a
    /// background thread; <paramref name="progress"/> how many SHAs have been searched, after each
    /// call. <see cref="SearchMode.Commits"/> matches the message only (the app matches authors and
    /// SHAs itself). An empty <paramref name="text"/> is never searched. A <c>fatal: bad object</c>
    /// (the graph is older than a <c>gc</c>) ends the search quietly.
    /// </summary>
    Task SearchAsync(SearchMode mode, string text, IReadOnlyList<string> shas, Action<string> onMatch, IProgress<int>? progress, CancellationToken cancellationToken = default);

    // Undo and redo (section 7; WP3).

    /// <summary>
    /// What the undo history reads before and after a recorded action, inside its operation (7.7
    /// steps 2 and 4), and what an undo step reads first (7.5 step 2): HEAD (<c>rev-parse HEAD
    /// --symbolic-full-name HEAD</c>, or <c>symbolic-ref -q HEAD</c> while unborn), and what
    /// <paramref name="scope"/> names: its refs with one <c>for-each-ref</c> (their worktrees with
    /// <c>%(worktreepath)</c>; exact names filtered in code), the stash list, the configuration keys,
    /// the files' SHA-256 and index entries, with their raw bytes (<c>hash-object -w
    /// --no-filters</c>) when the scope keeps them (<see cref="Undo.UndoScope.KeepsFileBytes"/>),
    /// the status, the <c>.gitignore</c>. Writes nothing but the files' blobs.
    /// </summary>
    Task<Undo.UndoState> ReadRefStatesAsync(Undo.UndoScope scope, NetworkOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs one undo or redo step (7.5, D167, D169): refuses while an operation is in progress, reads
    /// what the effects check, simulates the sequence (each check against the state the earlier
    /// effects leave), applies the effects grouped as 7.5 step 4 says (what another machine can
    /// refuse first, then what git can refuse locally, then one <c>update-ref --stdin</c>
    /// transaction with <c>verify</c> lines, then configuration and file writes), reverses what it
    /// applied when a later effect refuses, and returns the entry rewritten with the objects it made.
    /// A refusal changes nothing but <c>update-index --refresh</c>'s stat data; git's own failure
    /// throws <see cref="GitException"/>.
    /// </summary>
    Task<Undo.UndoStepResult> RunUndoStepAsync(Undo.UndoStep step, NetworkOptions options, CancellationToken cancellationToken = default);
}

/// <summary>A snapshot taken before a discard (D67).</summary>
/// <param name="Ref">The ref that keeps it, under <c>refs/visualcommit/backup/</c>.</param>
/// <param name="Commit">The snapshot commit's id.</param>
/// <param name="Paths">The files it was taken for.</param>
/// <param name="Present">Those of <paramref name="Paths"/> that existed in the working tree; the others were deleted there.</param>
public sealed record DiscardSnapshot(string Ref, string Commit, IReadOnlyList<string> Paths, IReadOnlySet<string> Present);

/// <summary>
/// The snapshot a hard reset saves first (<see cref="IGitRepository.SnapshotForHardResetWithFilesAsync"/>):
/// its commit and, when known, what the undo of the reset needs of it.
/// </summary>
/// <param name="Commit">The snapshot commit.</param>
public sealed record HardResetSnapshot(string Commit)
{
    /// <summary>Its second parent, the real index's tree before the reset; null when the index had conflicts, or when <see cref="Paths"/> is not known.</summary>
    public string? Index { get; init; }

    /// <summary>The paths it holds, in byte order as its message lists them; null when not known.</summary>
    public IReadOnlyList<string>? Paths { get; init; }

    /// <summary>Those of <see cref="Paths"/> it holds a file for (the others were gone from the working tree); null when not known.</summary>
    public IReadOnlySet<string>? Present { get; init; }
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

    /// <summary>
    /// Whether no change the watcher has counted is waiting for its quiet period or being raised
    /// (a change the system has not reported yet is not known). The harnesses wait for it, through
    /// the view model, before they mark a check's next calls. True for a watcher that holds no burst.
    /// </summary>
    bool IsQuiet => true;
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
