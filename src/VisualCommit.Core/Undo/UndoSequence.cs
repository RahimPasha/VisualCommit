using VisualCommit.Core.Git;
using static VisualCommit.Core.Undo.UndoEffect;

namespace VisualCommit.Core.Undo;

/// <summary>
/// Each recorded kind's undo and redo as a sequence of effects in a fixed order
/// (docs/phase-5-design.md 7.3, D167), made from an entry's states before and after and what was
/// captured: never a generic reverse of what happened, because the order of a step matters (a
/// branch is deleted only after switching away from it; a hard reset's files come back after its
/// move) and one git call can change several things. The git layer checks and runs them in that
/// order (7.5, <see cref="IGitRepository.RunUndoStepAsync"/>). Pure: no git.
/// </summary>
public static class UndoSequence
{
    private const string HeadsPrefix = "refs/heads/";
    private const string RemotesPrefix = "refs/remotes/";
    private const string TagsPrefix = "refs/tags/";

    /// <summary>The effects that take <paramref name="entry"/> back (<see cref="UndoDirection.Undo"/>) or forth again (<see cref="UndoDirection.Redo"/>), in order.</summary>
    /// <exception cref="InvalidOperationException">The entry's states do not hold what its kind needs (a pending entry, or a recording that missed its values).</exception>
    public static IReadOnlyList<UndoEffect> For(UndoEntry entry, UndoDirection direction)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.State != UndoEntryState.Done)
        {
            throw new InvalidOperationException($"The {entry.Kind} entry \"{entry.Text}\" is not finished.");
        }

        var undo = direction == UndoDirection.Undo;
        return entry.Kind switch
        {
            UndoKind.Commit or UndoKind.Amend => [Move(entry, undo, MoveMode.Soft)],
            UndoKind.Reset => Reset(entry, undo),
            UndoKind.Merge or UndoKind.CheckoutAndMerge or UndoKind.CherryPick or UndoKind.Revert or UndoKind.Rebase
                or UndoKind.CheckoutAndRebase or UndoKind.InteractiveRebase or UndoKind.Squash or UndoKind.Pull => History(entry, undo),
            UndoKind.Checkout or UndoKind.CreateBranch => Checkout(entry, undo),
            UndoKind.StashAndCheckout => StashAndCheckout(entry, undo),
            UndoKind.AutoStashCheckout => AutoStashCheckout(entry, undo),
            UndoKind.RenameBranch => RenameBranch(entry, undo),
            UndoKind.DeleteBranch => DeleteBranch(entry, undo),
            UndoKind.FastForwardBranch => ChangedRefs(entry, HeadsPrefix, undo),
            UndoKind.Stash => Stash(entry, undo),
            UndoKind.ApplyStash or UndoKind.PopStash => ApplyOrPop(entry, undo),
            UndoKind.DropStash => DropStash(entry, undo),
            UndoKind.CreateTag => Tag(entry, created: true, undo),
            UndoKind.DeleteTag => Tag(entry, created: false, undo),
            UndoKind.Discard => undo
                ? [new RestoreSnapshot(Snapshot(entry), entry.After.Files)]
                : [new FilesTo(entry.Before.Files, entry.After.Files)],
            UndoKind.Restore => undo
                ? [new FilesTo(entry.After.Files, entry.Before.Files)]
                : [new RestoreSnapshot(Snapshot(entry), entry.Before.Files)],
            UndoKind.Ignore => [Gitignore(entry, undo)],
            UndoKind.Upstream => BranchConfigs(entry, undo),
            UndoKind.AddRemote or UndoKind.RemoveRemote => Remote(entry, undo),
            UndoKind.RenameRemote => RenameRemote(entry, undo),
            UndoKind.SetRemoteUrl => RemoteUrl(entry, undo),
            UndoKind.Push or UndoKind.ForcePush or UndoKind.DeleteRemoteBranch or UndoKind.PushTag or UndoKind.DeleteRemoteTag => Push(entry, undo),
            _ => throw new InvalidOperationException($"No sequence for {entry.Kind}."),
        };
    }

    /// <summary>
    /// HEAD's branch (or a detached HEAD) moved from its state before to its state after:
    /// <see cref="MoveHead"/>, or <see cref="MoveDetachedHead"/> for a detached one. The initial
    /// commit's undo deletes the branch, its redo creates it (a null side).
    /// </summary>
    private static UndoEffect Move(UndoEntry entry, bool undo, MoveMode mode)
    {
        var (from, to) = undo ? (entry.After.Head, entry.Before.Head) : (entry.Before.Head, entry.After.Head);
        if (from.BranchName is { } branch)
        {
            return new MoveHead(branch, from.Sha, to.Sha, mode);
        }

        return new MoveDetachedHead(
            from.Sha ?? throw Missing(entry, "HEAD's commit"),
            to.Sha ?? throw Missing(entry, "HEAD's commit"),
            mode);
    }

    /// <summary>
    /// A reset: soft as a commit; mixed moves back softly, then puts the index back from its
    /// snapshot (or the commit itself when the index equalled HEAD); hard moves back keeping the
    /// files, then the index from the snapshot's second parent, then the snapshot's files, in this
    /// order (restored files would make the move refuse). A hard reset's redo is the app's own
    /// hard reset with a new snapshot.
    /// </summary>
    private static IReadOnlyList<UndoEffect> Reset(UndoEntry entry, bool undo)
    {
        var captured = entry.Captured;
        var before = entry.Before.Head.Sha ?? throw Missing(entry, "HEAD's commit before");
        var after = entry.After.Head.Sha ?? throw Missing(entry, "HEAD's commit after");
        switch (captured.ResetMode ?? throw Missing(entry, "the reset's mode"))
        {
            case ResetMode.Soft:
                return [Move(entry, undo, MoveMode.Soft)];

            case ResetMode.Mixed:
                var source = captured.ResetSnapshot ?? before;
                return undo
                    ? [Move(entry, undo, MoveMode.Soft), new IndexTo(source, after)]
                    : [new IndexTo(after, source), Move(entry, undo, MoveMode.Soft)];

            default:
                if (!undo)
                {
                    return [new HardReset(entry.Before.Head.BranchName, before, after)];
                }

                List<UndoEffect> effects = [Move(entry, undo, MoveMode.Keep)];
                if (captured.ResetIndex is { } index)
                {
                    effects.Add(new IndexTo(index, after));
                }

                if (captured.Snapshot is { } snapshot)
                {
                    effects.Add(new RestoreSnapshot(snapshot, FilesOf(entry.After, snapshot.Paths)));
                }

                return effects;
        }
    }

    /// <summary>
    /// A merge, fast-forward, pick, revert, rebase, interactive rebase, squash or pull, and a
    /// finished stopped action: HEAD's branch moves back keeping the files, and the other branches
    /// a rebase moved (a user's <c>rebase.updateRefs</c>) are set back. When the action checked a
    /// branch out first ("Check out and merge", "Check out and rebase", the list's checkout), the
    /// branch moves back while HEAD is still on it, then HEAD switches back; the redo switches
    /// first. A merge that failed after its checkout leaves the switch alone.
    /// </summary>
    private static IReadOnlyList<UndoEffect> History(UndoEntry entry, bool undo)
    {
        var before = entry.Before;
        var after = entry.After;
        if (string.Equals(before.Head.BranchName, after.Head.BranchName, StringComparison.Ordinal))
        {
            var others = OtherBranches(entry, after.Head.BranchName, undo);
            return undo ? [Move(entry, undo, MoveMode.Keep), .. others] : [.. others, Move(entry, undo, MoveMode.Keep)];
        }

        var branch = after.Head.BranchName ?? throw Missing(entry, "the branch checked out");
        var tipBefore = before.RefTarget(HeadsPrefix + branch) ?? throw Missing(entry, $"{branch}'s tip before");
        var tipAfter = after.Head.Sha ?? throw Missing(entry, $"{branch}'s tip after");
        var onBranch = new HeadState(branch, tipBefore);
        IReadOnlyList<UndoEffect> moves = tipBefore == tipAfter
            ? []
            : [new MoveHead(branch, undo ? tipAfter : tipBefore, undo ? tipBefore : tipAfter, MoveMode.Keep)];
        var rest = OtherBranches(entry, branch, undo);
        return undo
            ? [.. moves, .. rest, new Switch(onBranch, before.Head)]
            : [new Switch(before.Head, onBranch), .. rest, .. moves];
    }

    /// <summary>
    /// A checkout, and "Create branch": HEAD switches back. A branch the action created (a remote
    /// branch's tracking branch, the new branch) is deleted with its upstream keys after the
    /// switch; the redo creates it and its keys before switching. A branch created without a
    /// checkout is only deleted.
    /// </summary>
    private static IReadOnlyList<UndoEffect> Checkout(UndoEntry entry, bool undo)
    {
        var before = entry.Before;
        var after = entry.After;
        var switched = before.Head != after.Head;
        var created = Created(entry, HeadsPrefix).FirstOrDefault();
        IReadOnlyList<UndoEffect> branch = created is null ? [] : CreatedBranch(entry, created, undo);
        if (!switched)
        {
            return branch;
        }

        return undo
            ? [new Switch(after.Head, before.Head), .. branch]
            : [.. branch, new Switch(before.Head, after.Head)];
    }

    /// <summary>The created branch <paramref name="fullName"/> deleted with its keys, or created again with them.</summary>
    private static IReadOnlyList<UndoEffect> CreatedBranch(UndoEntry entry, string fullName, bool undo)
    {
        var branch = fullName[HeadsPrefix.Length..];
        var tip = entry.After.Refs[fullName];
        var keysBefore = BranchKeys(entry.Before, branch);
        var keysAfter = BranchKeys(entry.After, branch);
        return undo
            ? [new SetRef(fullName, tip, null), new BranchConfig(branch, keysAfter, keysBefore)]
            : [new SetRef(fullName, null, tip), new BranchConfig(branch, keysBefore, keysAfter)];
    }

    /// <summary>
    /// D91's "Stash changes and check out": the carried changes are stashed again (tracked files
    /// only, as D91), HEAD switches back, the stash is popped; the redo is the mirror.
    /// </summary>
    private static IReadOnlyList<UndoEffect> StashAndCheckout(UndoEntry entry, bool undo)
    {
        var (from, to) = undo ? (entry.After, entry.Before) : (entry.Before, entry.After);
        var created = Created(entry, HeadsPrefix).FirstOrDefault();
        IReadOnlyList<UndoEffect> branch = created is null ? [] : CreatedBranch(entry, created, undo);
        return undo
            ? [new StashPush(IncludeUntracked: false, Message: null, from.Status), new Switch(from.Head, to.Head), .. branch, new StashPop(null, null)]
            : [new StashPush(IncludeUntracked: false, Message: null, from.Status), .. branch, new Switch(from.Head, to.Head), new StashPop(null, null)];
    }

    /// <summary>
    /// The automatic stash's checkout (9.2, D166): HEAD switches back to the branch and the stash
    /// it made is popped; the redo stashes again, untracked files included, with its message, and
    /// switches to the commit. Without a stash (nothing was left to stash) it is a switch alone.
    /// </summary>
    private static IReadOnlyList<UndoEffect> AutoStashCheckout(UndoEntry entry, bool undo)
    {
        var captured = entry.Captured;
        if (captured.Stash is not { } stash)
        {
            return undo ? [new Switch(entry.After.Head, entry.Before.Head)] : [new Switch(entry.Before.Head, entry.After.Head)];
        }

        return undo
            ? [new Switch(entry.After.Head, entry.Before.Head), new StashPop(stash, entry.After.Status)]
            : [new StashPush(IncludeUntracked: true, captured.StashMessage, entry.Before.Status), new Switch(entry.Before.Head, entry.After.Head)];
    }

    private static IReadOnlyList<UndoEffect> RenameBranch(UndoEntry entry, bool undo)
    {
        var old = Removed(entry, HeadsPrefix).SingleOrDefault() ?? throw Missing(entry, "the branch's old name");
        var renamed = Created(entry, HeadsPrefix).SingleOrDefault() ?? throw Missing(entry, "the branch's new name");
        var (from, to) = undo ? (renamed, old) : (old, renamed);
        return [new UndoEffect.RenameBranch(from[HeadsPrefix.Length..], to[HeadsPrefix.Length..])];
    }

    /// <summary>A deleted branch comes back at its tip with a reflog of its own, then its keys; the redo removes the keys, then the branch.</summary>
    private static IReadOnlyList<UndoEffect> DeleteBranch(UndoEntry entry, bool undo)
    {
        var fullName = Removed(entry, HeadsPrefix).SingleOrDefault() ?? throw Missing(entry, "the deleted branch");
        var branch = fullName[HeadsPrefix.Length..];
        var tip = entry.Before.Refs[fullName];
        var keysBefore = BranchKeys(entry.Before, branch);
        var keysAfter = BranchKeys(entry.After, branch);
        return undo
            ? [new SetRef(fullName, null, tip) { CreateReflog = true }, new BranchConfig(branch, keysAfter, keysBefore)]
            : [new BranchConfig(branch, keysBefore, keysAfter), new SetRef(fullName, tip, null)];
    }

    /// <summary>A stash is popped back with its index; the redo stashes again with the same options and message (the entry takes the new stash).</summary>
    private static IReadOnlyList<UndoEffect> Stash(UndoEntry entry, bool undo)
    {
        var captured = entry.Captured;
        return undo
            ? [new StashPop(captured.Stash ?? throw Missing(entry, "the stash made"), entry.After.Status)]
            : [new StashPush(captured.StashIncludedUntracked, captured.StashMessage, entry.Before.Status)];
    }

    /// <summary>
    /// An apply puts the stash's files and their index entries back as they were; a pop first
    /// stores its stash again (so that a failure leaves the changes in the working tree), and its
    /// redo drops it last. A pop that kept its stash (it conflicted) is undone as an apply.
    /// </summary>
    private static IReadOnlyList<UndoEffect> ApplyOrPop(UndoEntry entry, bool undo)
    {
        var (from, to) = undo ? (entry.After, entry.Before) : (entry.Before, entry.After);
        var paths = entry.Before.Files.Select(file => file.Path).ToList();
        List<UndoEffect> files = [new IndexEntries(paths, from.IndexEntries, to.IndexEntries), new FilesTo(from.Files, to.Files)];
        var popped = entry.Kind == UndoKind.PopStash ? RemovedStash(entry) : null;
        if (popped is null)
        {
            return files;
        }

        return undo ? [new StashStore(popped.Sha, popped.Message), .. files] : [.. files, new StashDrop(popped.Sha)];
    }

    private static IReadOnlyList<UndoEffect> DropStash(UndoEntry entry, bool undo)
    {
        var dropped = RemovedStash(entry) ?? throw Missing(entry, "the dropped stash");
        return undo ? [new StashStore(dropped.Sha, dropped.Message)] : [new StashDrop(dropped.Sha)];
    }

    /// <summary>
    /// A created tag is kept in a <c>tag-undone</c> backup, then deleted; a deleted tag is created
    /// again at its object. Either redo brings the same tag object back, or deletes it again.
    /// </summary>
    private static IReadOnlyList<UndoEffect> Tag(UndoEntry entry, bool created, bool undo)
    {
        var fullName = (created ? Created(entry, TagsPrefix) : Removed(entry, TagsPrefix)).SingleOrDefault()
            ?? throw Missing(entry, "the tag");
        var name = fullName[TagsPrefix.Length..];
        var target = (created ? entry.After : entry.Before).Refs[fullName];
        if (created)
        {
            return undo ? [new KeepTag(name, target), new TagRef(name, target, Present: false)] : [new TagRef(name, target, Present: true)];
        }

        return [new TagRef(name, target, Present: undo)];
    }

    private static UndoEffect Gitignore(UndoEntry entry, bool undo)
    {
        var none = new GitignoreState(Exists: false, Length: 0, Sha256: null);
        var before = entry.Before.Gitignore ?? none;
        var after = entry.After.Gitignore ?? none;
        var appended = entry.Captured.GitignoreAppended ?? throw Missing(entry, "the bytes appended");
        return undo ? new GitignoreBytes(after, before, appended) : new GitignoreBytes(before, after, appended);
    }

    /// <summary>Every branch whose keys changed gets them back (an upstream set, changed or stopped).</summary>
    private static IReadOnlyList<UndoEffect> BranchConfigs(UndoEntry entry, bool undo) =>
        [.. ChangedSubsections(entry, "branch").Select(branch => (UndoEffect)(undo
            ? new BranchConfig(branch, BranchKeys(entry.After, branch), BranchKeys(entry.Before, branch))
            : new BranchConfig(branch, BranchKeys(entry.Before, branch), BranchKeys(entry.After, branch))))];

    /// <summary>
    /// An added or removed remote: its keys (and <c>remote.pushDefault</c> when it named it), the
    /// branches' keys git removed with it, and its remote branches, which come back from the
    /// recorded tips (the <c>remote-</c> backup holds them too). Keys are written before the
    /// branches are set, and removed after they are deleted.
    /// </summary>
    private static IReadOnlyList<UndoEffect> Remote(UndoEntry entry, bool undo)
    {
        var remote = ChangedSubsections(entry, "remote").SingleOrDefault() ?? throw Missing(entry, "the remote");
        var (from, to) = undo ? (entry.After, entry.Before) : (entry.Before, entry.After);
        UndoEffect keys = new RemoteConfig(remote, RemoteKeys(from, remote), RemoteKeys(to, remote));
        var branches = ChangedSubsections(entry, "branch")
            .Select(branch => (UndoEffect)new BranchConfig(branch, BranchKeys(from, branch), BranchKeys(to, branch)))
            .ToList();
        var refs = ChangedRefs(entry, RemotesPrefix + remote + "/", undo);
        var creates = !from.Config.Any(ConfigScope.Remote(remote).Covers);
        return creates ? [keys, .. branches, .. refs] : [.. refs, .. branches, keys];
    }

    private static IReadOnlyList<UndoEffect> RenameRemote(UndoEntry entry, bool undo)
    {
        var old = RemoteNames(entry.Before).Except(RemoteNames(entry.After), StringComparer.Ordinal).SingleOrDefault()
            ?? throw Missing(entry, "the remote's old name");
        var renamed = RemoteNames(entry.After).Except(RemoteNames(entry.Before), StringComparer.Ordinal).SingleOrDefault()
            ?? throw Missing(entry, "the remote's new name");
        return [undo ? new RemoteRename(renamed, old) : new RemoteRename(old, renamed)];
    }

    private static IReadOnlyList<UndoEffect> RemoteUrl(UndoEntry entry, bool undo)
    {
        var remote = ChangedSubsections(entry, "remote").SingleOrDefault() ?? throw Missing(entry, "the remote");
        var key = $"remote.{remote}.url";
        var before = Value(entry.Before, key) ?? throw Missing(entry, "the URL before");
        var after = Value(entry.After, key) ?? throw Missing(entry, "the URL after");
        return [undo ? new UndoEffect.RemoteUrl(remote, after, before) : new UndoEffect.RemoteUrl(remote, before, after)];
    }

    /// <summary>
    /// A change to a remote: each remote ref the push changed is set back with a lease first (the
    /// only effect another machine can refuse), then the upstream keys a push set. The redo pushes
    /// first too.
    /// </summary>
    private static IReadOnlyList<UndoEffect> Push(UndoEntry entry, bool undo)
    {
        var captured = entry.Captured;
        var remote = captured.Remote ?? throw Missing(entry, "the remote");
        var refs = (captured.Push ?? throw Missing(entry, "the push's remote values")).Refs
            .Select(pushed => (UndoEffect)(undo
                ? new RemoteRef(remote, pushed.RemoteRef, pushed.NewValue, pushed.OldValue)
                : new RemoteRef(remote, pushed.RemoteRef, pushed.OldValue, pushed.NewValue)));
        return [.. refs, .. BranchConfigs(entry, undo)];
    }

    /// <summary>The refs under <paramref name="prefix"/> (other than HEAD's branch) that the action moved, created or deleted, set back or forth.</summary>
    private static IReadOnlyList<UndoEffect> ChangedRefs(UndoEntry entry, string prefix, bool undo, string? except = null)
    {
        var before = entry.Before.Refs;
        var after = entry.After.Refs;
        return
        [
            .. before.Keys.Union(after.Keys, StringComparer.Ordinal)
                .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && !string.Equals(name, except, StringComparison.Ordinal))
                .Where(name => !string.Equals(before.GetValueOrDefault(name), after.GetValueOrDefault(name), StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .Select(name => (UndoEffect)(undo
                    ? new SetRef(name, after.GetValueOrDefault(name), before.GetValueOrDefault(name))
                    : new SetRef(name, before.GetValueOrDefault(name), after.GetValueOrDefault(name)))),
        ];
    }

    /// <summary>The branches other than <paramref name="branch"/> that the action moved (a rebase's <c>update-ref</c> lines).</summary>
    private static IReadOnlyList<UndoEffect> OtherBranches(UndoEntry entry, string? branch, bool undo) =>
        ChangedRefs(entry, HeadsPrefix, undo, branch is null ? null : HeadsPrefix + branch);

    private static IEnumerable<string> Created(UndoEntry entry, string prefix) =>
        entry.After.Refs.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && !entry.Before.Refs.ContainsKey(name)).Order(StringComparer.Ordinal);

    private static IEnumerable<string> Removed(UndoEntry entry, string prefix) =>
        entry.Before.Refs.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && !entry.After.Refs.ContainsKey(name)).Order(StringComparer.Ordinal);

    private static UndoStash? RemovedStash(UndoEntry entry) =>
        entry.Before.Stashes.FirstOrDefault(stash => !entry.After.Stashes.Any(left => string.Equals(left.Sha, stash.Sha, StringComparison.Ordinal)));

    private static DiscardSnapshot Snapshot(UndoEntry entry) =>
        entry.Captured.Snapshot ?? throw Missing(entry, "the snapshot");

    private static IReadOnlyList<RecordedFile> FilesOf(UndoState state, IReadOnlyList<string> paths) =>
        [.. state.Files.Where(file => paths.Contains(file.Path, StringComparer.Ordinal))];

    private static IReadOnlyList<ConfigEntry> BranchKeys(UndoState state, string branch) =>
        [.. state.Config.Where(ConfigScope.Branch(branch).Covers)];

    /// <summary>A remote's keys, and <c>remote.pushdefault</c> when it names the remote.</summary>
    private static IReadOnlyList<ConfigEntry> RemoteKeys(UndoState state, string remote) =>
        [.. state.Config.Where(key => ConfigScope.Remote(remote).Covers(key)
            || (string.Equals(key.Key, "remote.pushdefault", StringComparison.OrdinalIgnoreCase) && string.Equals(key.Value, remote, StringComparison.Ordinal)))];

    private static IEnumerable<string> RemoteNames(UndoState state) =>
        state.Config.Where(key => string.Equals(key.Section, "remote", StringComparison.OrdinalIgnoreCase)).Select(key => key.Subsection).OfType<string>().Distinct(StringComparer.Ordinal);

    /// <summary>The subsections of <paramref name="section"/> whose keys differ between the two states, in order.</summary>
    private static IReadOnlyList<string> ChangedSubsections(UndoEntry entry, string section)
    {
        IEnumerable<ConfigEntry> Keys(UndoState state, string subsection) =>
            state.Config.Where(new ConfigScope(section, subsection).Covers);

        return
        [
            .. entry.Before.Config.Concat(entry.After.Config)
                .Where(key => string.Equals(key.Section, section, StringComparison.OrdinalIgnoreCase))
                .Select(key => key.Subsection)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .Where(subsection => !Keys(entry.Before, subsection).SequenceEqual(Keys(entry.After, subsection))),
        ];
    }

    private static string? Value(UndoState state, string key) =>
        state.Config.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.Ordinal))?.Value;

    private static InvalidOperationException Missing(UndoEntry entry, string what) =>
        new($"The {entry.Kind} entry \"{entry.Text}\" has no record of {what}.");
}
