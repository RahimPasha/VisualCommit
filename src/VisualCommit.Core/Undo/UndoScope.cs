using VisualCommit.Core.Git;

namespace VisualCommit.Core.Undo;

/// <summary>
/// What a recorded action is and what it may change (docs/phase-5-design.md 7.7 step 1): its kind
/// and entry text (7.6), and what the history reads before and after it besides HEAD, which it
/// always reads: refs by exact full name or by prefix, the stash list, configuration keys, files
/// and their index entries, the whole status, the <c>.gitignore</c>. Each operation names its own
/// (<c>RunOptions.Undo</c>); <see cref="None"/> is for the kinds that are not recorded (7.2:
/// stage, unstage, fetch, the clean-up, the resolver, Abort, undo and redo themselves). The
/// factories below give every recorded kind its scope.
/// </summary>
public sealed record UndoScope
{
    private const string HeadsPrefix = "refs/heads/";
    private const string RemotesPrefix = "refs/remotes/";
    private const string TagsPrefix = "refs/tags/";

    /// <summary>Not recorded.</summary>
    public static UndoScope None { get; } = new();

    /// <summary>
    /// Continue and Skip (7.2): no entry of their own; when the stopped action ends, its pending
    /// entry is completed with the state after, as the stage panel's Commit that ends it does.
    /// </summary>
    public static UndoScope Continuation { get; } = new() { CompletesPending = true };

    /// <summary>Abort (7.2, D165): no entry; the stopped action's pending entry goes, and the backups list is the way back.</summary>
    public static UndoScope Abort { get; } = new() { RemovesPending = true };

    /// <summary>The recorded action's kind, or null for <see cref="None"/>.</summary>
    public UndoKind? Kind { get; init; }

    /// <summary>The entry's text as known before the action runs (7.6): <c>commit "Fix the parser"</c>, <c>checkout of main</c>.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Refs read by exact full name (<c>refs/heads/main</c>, <c>refs/tags/v1</c>).</summary>
    public IReadOnlyList<string> Refs { get; init; } = [];

    /// <summary>
    /// Refs read by prefix, which matches whole path components (<c>refs/heads/</c>: every branch;
    /// <c>refs/remotes/origin/</c>: every branch of <c>origin</c>, not those of <c>origin2</c>).
    /// </summary>
    public IReadOnlyList<string> RefPrefixes { get; init; } = [];

    /// <summary>The stash list is read.</summary>
    public bool StashList { get; init; }

    /// <summary>The configuration keys read: a whole section, or one subsection of it.</summary>
    public IReadOnlyList<ConfigScope> Config { get; init; } = [];

    /// <summary>Working-tree files whose raw bytes and index entries are read (repository-relative, <c>/</c>).</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>A stash whose files (its changed paths and its untracked ones) are read as <see cref="Files"/> are: an apply or a pop.</summary>
    public string? FilesOfStash { get; init; }

    /// <summary>The whole status is read (the stash kinds, whose checks compare it).</summary>
    public bool Status { get; init; }

    /// <summary>The working tree's <c>.gitignore</c> is read.</summary>
    public bool Gitignore { get; init; }

    /// <summary>The operation ends a stopped action: its pending entry is completed when it ends (<see cref="Continuation"/>).</summary>
    public bool CompletesPending { get; init; }

    /// <summary>The operation abandons a stopped action: its pending entry goes (<see cref="Abort"/>).</summary>
    public bool RemovesPending { get; init; }

    /// <summary>The action is recorded.</summary>
    public bool IsRecorded => Kind is not null;

    /// <summary>Whether <paramref name="fullName"/> is one of the scope's refs: named exactly, or under one of its prefixes by whole path components.</summary>
    public bool Covers(string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        return Refs.Contains(fullName, StringComparer.Ordinal)
            || RefPrefixes.Any(prefix => fullName.StartsWith(prefix.EndsWith('/') ? prefix : prefix + "/", StringComparison.Ordinal));
    }

    /// <summary>Whether <paramref name="key"/> (as <c>git config --list</c> prints it) is one of the scope's configuration keys.</summary>
    public bool Covers(ConfigEntry key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Config.Any(scope => scope.Covers(key));
    }

    // The scopes of the recorded kinds (7.2, 7.3, 7.6, 7.7). Branches are short names, refs full names.

    /// <summary>The stage panel's Commit, or Amend: HEAD's branch moves.</summary>
    public static UndoScope Commit(string subject, bool amend, string? branch) => new()
    {
        Kind = amend ? UndoKind.Amend : UndoKind.Commit,
        Text = amend ? UndoTexts.Amend(subject) : UndoTexts.Commit(subject),
        Refs = BranchRefs(branch),
    };

    /// <summary>
    /// A checkout of <paramref name="name"/> (a branch, a remote branch, a tag or a commit, as the
    /// texts name it). <paramref name="createdBranch"/> is the local branch a remote branch's
    /// checkout may create, with its upstream keys; the branch HEAD was on is <paramref name="current"/>.
    /// </summary>
    public static UndoScope Checkout(string name, string? current, string? createdBranch = null) => new()
    {
        Kind = UndoKind.Checkout,
        Text = UndoTexts.Checkout(name),
        Refs = BranchRefs(current, createdBranch),
        Config = createdBranch is null ? [] : [ConfigScope.Branch(createdBranch)],
    };

    /// <summary>The automatic stash's checkout of <paramref name="name"/> (9.2): the stash and the whole status too.</summary>
    public static UndoScope AutoStashCheckout(string name, string? current) => new()
    {
        Kind = UndoKind.AutoStashCheckout,
        Text = UndoTexts.Checkout(name),
        Refs = BranchRefs(current),
        StashList = true,
        Status = true,
    };

    /// <summary>D91's "Stash changes and check out" of <paramref name="name"/>.</summary>
    public static UndoScope StashAndCheckout(string name, string? current, string? createdBranch = null) => new()
    {
        Kind = UndoKind.StashAndCheckout,
        Text = UndoTexts.Checkout(name),
        Refs = BranchRefs(current, createdBranch),
        Config = createdBranch is null ? [] : [ConfigScope.Branch(createdBranch)],
        StashList = true,
        Status = true,
    };

    /// <summary>"Create branch" <paramref name="branch"/>, checked out or not: its ref and upstream keys, and HEAD's branch.</summary>
    public static UndoScope CreateBranch(string branch, string? current) => new()
    {
        Kind = UndoKind.CreateBranch,
        Text = UndoTexts.CreateBranch(branch),
        Refs = BranchRefs(branch, current),
        Config = [ConfigScope.Branch(branch)],
    };

    /// <summary>A rename of <paramref name="branch"/> to <paramref name="newName"/>.</summary>
    public static UndoScope RenameBranch(string branch, string newName) => new()
    {
        Kind = UndoKind.RenameBranch,
        Text = UndoTexts.RenameBranch(branch, newName),
        Refs = BranchRefs(branch, newName),
        Config = [ConfigScope.Branch(branch), ConfigScope.Branch(newName)],
    };

    /// <summary>A deletion of <paramref name="branch"/>: its tip and its keys come back on undo.</summary>
    public static UndoScope DeleteBranch(string branch) => new()
    {
        Kind = UndoKind.DeleteBranch,
        Text = UndoTexts.DeleteBranch(branch),
        Refs = BranchRefs(branch),
        Config = [ConfigScope.Branch(branch)],
    };

    /// <summary>A fast-forward of <paramref name="branch"/>, not checked out.</summary>
    public static UndoScope FastForwardBranch(string branch) => new()
    {
        Kind = UndoKind.FastForwardBranch,
        Text = UndoTexts.FastForward(branch),
        Refs = BranchRefs(branch),
    };

    /// <summary>
    /// A merge of <paramref name="source"/> into the current branch <paramref name="current"/>, or
    /// with <paramref name="fastForwardOnly"/> the drop's fast-forward of it (<c>fast-forward of {b}</c>).
    /// </summary>
    public static UndoScope Merge(string source, string current, bool fastForwardOnly = false) => new()
    {
        Kind = UndoKind.Merge,
        Text = fastForwardOnly ? UndoTexts.FastForward(current) : UndoTexts.Merge(source, current),
        Refs = BranchRefs(current),
    };

    /// <summary>"Check out {B} and merge {A} into it": <paramref name="branch"/> B, <paramref name="source"/> A; the branch HEAD was on is <paramref name="current"/>.</summary>
    public static UndoScope CheckoutAndMerge(string branch, string source, string? current) => new()
    {
        Kind = UndoKind.CheckoutAndMerge,
        Text = UndoTexts.Merge(source, branch),
        Refs = BranchRefs(branch, current),
    };

    /// <summary>A reset of <paramref name="branch"/> (HEAD's name: the branch, or <c>HEAD</c> while detached) to <paramref name="target"/>.</summary>
    public static UndoScope Reset(string branch, string target, ResetMode mode) => new()
    {
        Kind = UndoKind.Reset,
        Text = UndoTexts.Reset(branch, target, mode),
        Refs = BranchRefs(branch),
    };

    /// <summary>A cherry-pick of <paramref name="commits"/> (full SHAs) onto <paramref name="current"/>.</summary>
    public static UndoScope CherryPick(IReadOnlyList<string> commits, string? current) => new()
    {
        Kind = UndoKind.CherryPick,
        Text = UndoTexts.CherryPick(commits),
        Refs = BranchRefs(current),
    };

    /// <summary>A revert of <paramref name="commits"/> (full SHAs) on <paramref name="current"/>.</summary>
    public static UndoScope Revert(IReadOnlyList<string> commits, string? current) => new()
    {
        Kind = UndoKind.Revert,
        Text = UndoTexts.Revert(commits),
        Refs = BranchRefs(current),
    };

    /// <summary>
    /// A plain rebase of <paramref name="branch"/> onto <paramref name="target"/>: every branch is
    /// read, so that branches a user's <c>rebase.updateRefs</c> moves join the entry (7.7 step 1).
    /// </summary>
    public static UndoScope Rebase(string branch, string target) => new()
    {
        Kind = UndoKind.Rebase,
        Text = UndoTexts.Rebase(branch, target),
        RefPrefixes = [HeadsPrefix],
    };

    /// <summary>"Check out {A} and rebase it onto {B}": every branch is read, as for a plain rebase.</summary>
    public static UndoScope CheckoutAndRebase(string branch, string target) => new()
    {
        Kind = UndoKind.CheckoutAndRebase,
        Text = UndoTexts.Rebase(branch, target),
        RefPrefixes = [HeadsPrefix],
    };

    /// <summary>An interactive rebase of <paramref name="branch"/>, its list's checkout first or not; the branch HEAD was on is <paramref name="current"/>.</summary>
    public static UndoScope InteractiveRebase(string branch, string? current) => new()
    {
        Kind = UndoKind.InteractiveRebase,
        Text = UndoTexts.InteractiveRebase(branch),
        Refs = BranchRefs(branch, current),
    };

    /// <summary>A squash of <paramref name="count"/> commits of <paramref name="current"/>.</summary>
    public static UndoScope Squash(int count, string? current) => new()
    {
        Kind = UndoKind.Squash,
        Text = UndoTexts.Squash(count),
        Refs = BranchRefs(current),
    };

    /// <summary>A pull into <paramref name="branch"/>: every branch is read, since it may rebase (7.7 step 1).</summary>
    public static UndoScope Pull(string branch) => new()
    {
        Kind = UndoKind.Pull,
        Text = UndoTexts.Pull(branch),
        RefPrefixes = [HeadsPrefix],
    };

    /// <summary>A stash; <paramref name="message"/> is the user's, or null when git names it (the recorder takes the new entry's message then).</summary>
    public static UndoScope Stash(string? message) => new()
    {
        Kind = UndoKind.Stash,
        Text = UndoTexts.Stash(message ?? string.Empty),
        StashList = true,
        Status = true,
    };

    /// <summary>An apply of the stash <paramref name="sha"/>, listed with <paramref name="message"/>.</summary>
    public static UndoScope ApplyStash(string sha, string message) => new()
    {
        Kind = UndoKind.ApplyStash,
        Text = UndoTexts.ApplyStash(message),
        StashList = true,
        FilesOfStash = sha,
    };

    /// <summary>A pop of the stash <paramref name="sha"/>, listed with <paramref name="message"/>.</summary>
    public static UndoScope PopStash(string sha, string message) => new()
    {
        Kind = UndoKind.PopStash,
        Text = UndoTexts.PopStash(message),
        StashList = true,
        FilesOfStash = sha,
    };

    /// <summary>A drop of a stash listed with <paramref name="message"/>.</summary>
    public static UndoScope DropStash(string message) => new()
    {
        Kind = UndoKind.DropStash,
        Text = UndoTexts.DropStash(message),
        StashList = true,
    };

    /// <summary>A creation of the tag <paramref name="tag"/>.</summary>
    public static UndoScope CreateTag(string tag) => new()
    {
        Kind = UndoKind.CreateTag,
        Text = UndoTexts.CreateTag(tag),
        Refs = [TagsPrefix + tag],
    };

    /// <summary>A deletion of the tag <paramref name="tag"/>.</summary>
    public static UndoScope DeleteTag(string tag) => new()
    {
        Kind = UndoKind.DeleteTag,
        Text = UndoTexts.DeleteTag(tag),
        Refs = [TagsPrefix + tag],
    };

    /// <summary>A discard of whole files (<paramref name="paths"/>).</summary>
    public static UndoScope Discard(IReadOnlyList<string> paths) => new()
    {
        Kind = UndoKind.Discard,
        Text = UndoTexts.Discard(paths),
        Files = paths,
    };

    /// <summary>A discard of a hunk or of lines of <paramref name="path"/>.</summary>
    public static UndoScope DiscardLines(string path) => new()
    {
        Kind = UndoKind.Discard,
        Text = UndoTexts.DiscardLines(path),
        Files = [path],
    };

    /// <summary>The restore bar's Restore of <paramref name="paths"/>.</summary>
    public static UndoScope Restore(IReadOnlyList<string> paths) => new()
    {
        Kind = UndoKind.Restore,
        Text = UndoTexts.Restore,
        Files = paths,
    };

    /// <summary>An append of <paramref name="pattern"/> to <c>.gitignore</c>.</summary>
    public static UndoScope Ignore(string pattern) => new()
    {
        Kind = UndoKind.Ignore,
        Text = UndoTexts.Ignore(pattern),
        Gitignore = true,
    };

    /// <summary>A change of <paramref name="branch"/>'s upstream: set, changed or stopped.</summary>
    public static UndoScope Upstream(string branch) => new()
    {
        Kind = UndoKind.Upstream,
        Text = UndoTexts.Upstream(branch),
        Config = [ConfigScope.Branch(branch)],
    };

    /// <summary>An addition of the remote <paramref name="remote"/>.</summary>
    public static UndoScope AddRemote(string remote) => new()
    {
        Kind = UndoKind.AddRemote,
        Text = UndoTexts.AddRemote(remote),
        RefPrefixes = [RemotesPrefix + remote + "/"],
        Config = [ConfigScope.Remote(remote)],
    };

    /// <summary>A rename of the remote <paramref name="remote"/> to <paramref name="newName"/>: both names' refs, every remote key and every branch's keys (git rewrites the upstreams).</summary>
    public static UndoScope RenameRemote(string remote, string newName) => new()
    {
        Kind = UndoKind.RenameRemote,
        Text = UndoTexts.RenameRemote(remote, newName),
        RefPrefixes = [RemotesPrefix + remote + "/", RemotesPrefix + newName + "/"],
        Config = [new ConfigScope("remote"), new ConfigScope("branch")],
    };

    /// <summary>
    /// A removal of the remote <paramref name="remote"/>: its remote branches, every remote key
    /// (<c>remote.pushDefault</c> included) and every branch's keys, which git removes where they name it (0.5).
    /// </summary>
    public static UndoScope RemoveRemote(string remote) => new()
    {
        Kind = UndoKind.RemoveRemote,
        Text = UndoTexts.RemoveRemote(remote),
        RefPrefixes = [RemotesPrefix + remote + "/"],
        Config = [new ConfigScope("remote"), new ConfigScope("branch")],
    };

    /// <summary>A change of the URL of <paramref name="remote"/>.</summary>
    public static UndoScope SetRemoteUrl(string remote) => new()
    {
        Kind = UndoKind.SetRemoteUrl,
        Text = UndoTexts.SetRemoteUrl(remote),
        Config = [ConfigScope.Remote(remote)],
    };

    /// <summary>A push of <paramref name="branch"/> to <paramref name="remote"/> (normal or <paramref name="force"/>): the remote's values are captured, the branch's upstream keys read.</summary>
    public static UndoScope Push(string branch, string remote, bool force) => new()
    {
        Kind = force ? UndoKind.ForcePush : UndoKind.Push,
        Text = force ? UndoTexts.ForcePush(branch, remote) : UndoTexts.Push(branch, remote),
        Config = [ConfigScope.Branch(branch)],
    };

    /// <summary>A deletion of <paramref name="branch"/> on <paramref name="remote"/>.</summary>
    public static UndoScope DeleteRemoteBranch(string branch, string remote) => new()
    {
        Kind = UndoKind.DeleteRemoteBranch,
        Text = UndoTexts.DeleteRemoteBranch(branch, remote),
    };

    /// <summary>A push of the tag <paramref name="tag"/> to <paramref name="remote"/>.</summary>
    public static UndoScope PushTag(string tag, string remote) => new()
    {
        Kind = UndoKind.PushTag,
        Text = UndoTexts.PushTag(tag, remote),
    };

    /// <summary>A deletion of the tag <paramref name="tag"/> on <paramref name="remote"/>.</summary>
    public static UndoScope DeleteRemoteTag(string tag, string remote) => new()
    {
        Kind = UndoKind.DeleteRemoteTag,
        Text = UndoTexts.DeleteRemoteTag(tag, remote),
    };

    /// <summary>The branches' full names; a null or <c>HEAD</c> (how texts name a detached HEAD, never a branch's name) is left out.</summary>
    private static IReadOnlyList<string> BranchRefs(params string?[] branches) =>
        [.. branches.OfType<string>().Where(branch => branch != "HEAD").Distinct(StringComparer.Ordinal).Select(branch => HeadsPrefix + branch)];
}

/// <summary>A part of the repository's own configuration that an <see cref="UndoScope"/> reads.</summary>
/// <param name="Section">The section, lower-case: <c>branch</c>, <c>remote</c>.</param>
/// <param name="Subsection">One subsection of it, as written (<c>feature/X</c>), or null for every key of the section, those without a subsection (<c>remote.pushdefault</c>) included.</param>
public sealed record ConfigScope(string Section, string? Subsection = null)
{
    /// <summary>A branch's keys: <c>branch.&lt;b&gt;.*</c>.</summary>
    public static ConfigScope Branch(string branch) => new("branch", branch);

    /// <summary>A remote's keys: <c>remote.&lt;r&gt;.*</c>.</summary>
    public static ConfigScope Remote(string remote) => new("remote", remote);

    /// <summary>Whether <paramref name="key"/> lies in this part: its section, and its subsection when one is named.</summary>
    public bool Covers(ConfigEntry key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return string.Equals(key.Section, Section, StringComparison.OrdinalIgnoreCase)
            && (Subsection is null || string.Equals(key.Subsection, Subsection, StringComparison.Ordinal));
    }
}
