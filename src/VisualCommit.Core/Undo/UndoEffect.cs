using VisualCommit.Core.Git;

namespace VisualCommit.Core.Undo;

/// <summary>
/// One effect of an undo or redo step, from the closed set of docs/phase-5-design.md 7.3 (D167):
/// what it changes, and the values its check compares with the repository before it runs
/// (the state it leaves, "From", and the state it makes, "To"). The git layer runs them
/// (<see cref="IGitRepository.RunUndoStepAsync"/>); <see cref="UndoSequence"/> puts each kind's in order.
/// </summary>
public abstract record UndoEffect
{
    private UndoEffect()
    {
    }

    /// <summary>
    /// Moves the checked-out branch <paramref name="Branch"/> (short name) from
    /// <paramref name="From"/> to <paramref name="To"/>, through <c>refs/heads/&lt;b&gt;</c>, never
    /// through HEAD's link (D169). <see cref="MoveMode.Soft"/>: <c>update-ref -m &lt;msg&gt;
    /// refs/heads/&lt;b&gt; &lt;to&gt; &lt;from&gt;</c>; <see cref="MoveMode.Keep"/>: the files move
    /// too. A null <paramref name="To"/> deletes the branch (the initial commit's undo: it becomes
    /// unborn); a null <paramref name="From"/> creates it (that undo's redo). Check: HEAD is on the
    /// branch and the branch is at <paramref name="From"/>; for Keep, no local change or ignored file is in the way.
    /// </summary>
    public sealed record MoveHead(string Branch, string? From, string? To, MoveMode Mode) : UndoEffect;

    /// <summary>As <see cref="MoveHead"/> for a detached HEAD, on <c>--no-deref HEAD</c>. Check: HEAD is detached at <paramref name="From"/>.</summary>
    public sealed record MoveDetachedHead(string From, string To, MoveMode Mode) : UndoEffect;

    /// <summary>
    /// Sets the ref <paramref name="Ref"/> (full name) from <paramref name="From"/> to
    /// <paramref name="To"/>, either absent (null): <c>update-ref --stdin</c> with <c>update</c>,
    /// <c>create</c> or <c>delete</c> and the expected value. <see cref="CreateReflog"/> gives a
    /// branch brought back its reflog. Check: the ref is at <paramref name="From"/>, and a branch
    /// is checked out in no worktree.
    /// </summary>
    public sealed record SetRef(string Ref, string? From, string? To) : UndoEffect
    {
        /// <summary><c>--create-reflog</c>: a deleted branch brought back starts a reflog (0.5).</summary>
        public bool CreateReflog { get; init; }
    }

    /// <summary>
    /// Switches HEAD from <paramref name="From"/> to <paramref name="To"/>: <c>switch --no-guess
    /// --no-overwrite-ignore &lt;b&gt;</c>, or <c>switch --detach --no-overwrite-ignore &lt;sha&gt;</c>
    /// for a detached one, never with an automatic stash (9.1). Check: HEAD is exactly
    /// <paramref name="From"/>, and <paramref name="To"/> exists.
    /// </summary>
    public sealed record Switch(HeadState From, HeadState To) : UndoEffect;

    /// <summary>Renames a branch: <c>branch --move &lt;from&gt; &lt;to&gt;</c>. Check: <paramref name="From"/> exists and <paramref name="To"/> does not (a name differing only in case where names ignore case is refused, D131).</summary>
    public sealed record RenameBranch(string From, string To) : UndoEffect;

    /// <summary>Writes <paramref name="Branch"/>'s keys of <paramref name="To"/> with <c>config --local</c> and unsets the others. Check: the keys are <paramref name="From"/>.</summary>
    public sealed record BranchConfig(string Branch, IReadOnlyList<ConfigEntry> From, IReadOnlyList<ConfigEntry> To) : UndoEffect;

    /// <summary>As <see cref="BranchConfig"/> for a remote's keys (<c>remote.&lt;r&gt;.*</c>, and <c>remote.pushDefault</c> when it names it), written with <c>--add</c> in order.</summary>
    public sealed record RemoteConfig(string Remote, IReadOnlyList<ConfigEntry> From, IReadOnlyList<ConfigEntry> To) : UndoEffect;

    /// <summary>Renames a remote: <c>remote rename</c>. Check: <paramref name="From"/> exists and <paramref name="To"/> does not.</summary>
    public sealed record RemoteRename(string From, string To) : UndoEffect;

    /// <summary>Changes a remote's URL: <c>remote set-url --</c>. Check: the URL is <paramref name="From"/>, its only <c>url</c> value.</summary>
    public sealed record RemoteUrl(string Remote, string From, string To) : UndoEffect;

    /// <summary>Puts the index to <paramref name="Source"/>'s tree: <c>restore --staged --source=&lt;source&gt; -- .</c>. Check: the index equals <paramref name="From"/> (<c>diff-index --cached --quiet &lt;from&gt;</c>).</summary>
    public sealed record IndexTo(string Source, string From) : UndoEffect;

    /// <summary>
    /// Writes each file's recorded bytes back (<c>cat-file blob</c>), or deletes a file recorded
    /// as absent, removing folders left empty. Check: each file's SHA-256 equals <paramref name="From"/>.
    /// </summary>
    public sealed record FilesTo(IReadOnlyList<RecordedFile> From, IReadOnlyList<RecordedFile> To) : UndoEffect;

    /// <summary>Puts a snapshot's files back (<c>RestoreSnapshotAsync</c>). Check: the snapshot's files' SHA-256 equal <paramref name="From"/>.</summary>
    public sealed record RestoreSnapshot(DiscardSnapshot Snapshot, IReadOnlyList<RecordedFile> From) : UndoEffect;

    /// <summary>
    /// Stashes the changes: <c>stash push [--include-untracked] [--message &lt;m&gt;]</c> without
    /// <c>GIT_LITERAL_PATHSPECS</c> (D108); the entry takes the new stash's commit. Check: the whole
    /// status equals <paramref name="Status"/> (null: as the step's earlier effects leave it).
    /// </summary>
    public sealed record StashPush(bool IncludeUntracked, string? Message, string? Status) : UndoEffect;

    /// <summary>
    /// Pops a stash with its index: <c>stash pop --index stash@{n}</c>, found by its commit
    /// <paramref name="Sha"/> (null: the one the step's <see cref="StashPush"/> made). Check: it is
    /// listed and exists, HEAD is at <see cref="Onto"/>'s commit, the whole status equals
    /// <paramref name="Status"/> (null: as the step's earlier effects leave it), and none of its
    /// untracked files exists.
    /// </summary>
    public sealed record StashPop(string? Sha, string? Status) : UndoEffect
    {
        /// <summary>
        /// Where HEAD must be for the pop (its branch names the refusal): the commit the stash was
        /// made on, or the one a step's own stash is popped onto after its switch. A pop onto
        /// another commit merges the stash there and can conflict after every other check passed,
        /// leaving conflict markers and the stash's untracked files behind (0.5). Null: not checked.
        /// </summary>
        public HeadState? Onto { get; init; }
    }

    /// <summary>Puts a stash back in the list with its message: <c>stash store -m &lt;message&gt; &lt;sha&gt;</c> (it comes back as <c>stash@{0}</c>). Check: not listed, and the commit exists.</summary>
    public sealed record StashStore(string Sha, string Message) : UndoEffect;

    /// <summary>Drops a stash found by its commit: <c>stash drop stash@{k}</c>. Check: listed.</summary>
    public sealed record StashDrop(string Sha) : UndoEffect;

    /// <summary>
    /// Writes the paths' index entries back (<c>update-index -z --index-info</c>, a removal line
    /// first, then the recorded entries, unmerged stages included). Check: the paths' entries
    /// (<c>ls-files --stage -z</c>) equal <paramref name="From"/>, and none is intent-to-add or skip-worktree.
    /// </summary>
    public sealed record IndexEntries(IReadOnlyList<string> Paths, IReadOnlyList<string> From, IReadOnlyList<string> To) : UndoEffect;

    /// <summary>
    /// Creates the tag <paramref name="Name"/> at <paramref name="Object"/> (<paramref name="Present"/>:
    /// <c>update-ref refs/tags/&lt;t&gt; &lt;object&gt; ""</c>) or deletes it (<c>update-ref -d</c>
    /// with the object as expected value). Check: the tag is absent, or at the object.
    /// </summary>
    public sealed record TagRef(string Name, string Object, bool Present) : UndoEffect;

    /// <summary>
    /// Appends <paramref name="Appended"/> (one character per byte) to <c>.gitignore</c>, or
    /// truncates it to <paramref name="To"/>'s length, deleting a file the append created. Check:
    /// the file's SHA-256 equals <paramref name="From"/>'s, and it is not a symbolic link.
    /// </summary>
    public sealed record GitignoreBytes(GitignoreState From, GitignoreState To, string Appended) : UndoEffect;

    /// <summary>
    /// Sets the ref <paramref name="Ref"/> (full name) on <paramref name="Remote"/> from
    /// <paramref name="From"/> to <paramref name="To"/>, either absent: <c>push --progress
    /// --force-with-lease=&lt;ref&gt;:&lt;from, or empty&gt; &lt;r&gt; &lt;to&gt;:&lt;ref&gt;</c>, or
    /// <c>--delete &lt;ref&gt;</c>; the remote-tracking ref follows. Check: git's lease;
    /// <c>Everything up-to-date</c> on a push that must create counts as done (0.5).
    /// </summary>
    public sealed record RemoteRef(string Remote, string Ref, string? From, string? To) : UndoEffect;

    /// <summary>Keeps an undone tag creation's object in a <c>tag-undone-&lt;time&gt;/&lt;t&gt;</c> backup before the tag goes (7.3, D171); the entry takes the backup.</summary>
    public sealed record KeepTag(string Name, string Object) : UndoEffect;

    /// <summary>
    /// A hard reset's redo: the app's own hard reset of <paramref name="Branch"/> (null for a
    /// detached HEAD) from <paramref name="From"/> to <paramref name="To"/> with a new snapshot,
    /// since the files may have changed since the undo; the entry takes the new snapshot. Check:
    /// HEAD on the branch (or detached) at <paramref name="From"/>.
    /// </summary>
    public sealed record HardReset(string? Branch, string From, string To) : UndoEffect;
}
