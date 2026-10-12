using VisualCommit.Core.Git;

namespace VisualCommit.Testing;

/// <summary>The scenario repos of phase 3 (design 7.1): branches, merges, remotes, stashes, blocked checkouts and odd refs.</summary>
public static partial class Scenarios
{
    /// <summary>
    /// The states <see cref="StoppedAsync"/> leaves a repository in. Nested here, and not a
    /// type of its own, so that it never clashes with the app's own type of the same name in
    /// tests that use both namespaces.
    /// </summary>
    public enum InProgressKind
    {
        /// <summary>Conflicts without an operation: a stash that did not apply cleanly.</summary>
        None,

        /// <summary><c>git merge</c> stopped with a conflict.</summary>
        Merge,

        /// <summary>A <c>git cherry-pick</c> of three commits stopped at the second.</summary>
        CherryPick,

        /// <summary><c>git revert</c> stopped with a conflict.</summary>
        Revert,

        /// <summary><c>git rebase</c> stopped with a conflict.</summary>
        Rebase,
    }

    /// <summary>
    /// The operations scenario ("ops"): the base for branches, merges, resets, cherry-picks,
    /// reverts, tags and stashes. Commits, by "Test Author" from 2026-01-01 12:00 UTC:
    /// <list type="number">
    /// <item>12:00 "Initial commit" on main (README.md "# Operations\n")</item>
    /// <item>12:01 "Add core" on main (src/core.txt "core\n"), tagged <c>v1.0</c> (annotated, "Version 1.0")</item>
    /// <item>12:02 "Alpha one" on feature/alpha, from Add core (src/alpha.txt "alpha 1\n")</item>
    /// <item>12:03 "Alpha two" on feature/alpha (src/alpha.txt "alpha 1\nalpha 2\n")</item>
    /// <item>12:04 "Main work" on main (src/core.txt "core\nmain work\n"), tagged <c>v1.1</c> (lightweight)</item>
    /// <item>12:05 "Fast-forward me" on feature/ff, from Main work (docs/ff.txt "ff\n")</item>
    /// <item>12:06 "Conflicting change" on feature/conflict, from Add core (src/core.txt "core\nconflict\n")</item>
    /// <item>12:07 stash@{0} "On main: Experiment" (README.md "# Operations\n\nExperiment.\n")</item>
    /// </list>
    /// HEAD is on a clean main at Main work. The bare remote <c>origin</c> (<c>origin.git</c> next
    /// to the working tree) has main and feature/alpha, both tracked, ahead and behind 0;
    /// feature/ff and feature/conflict are local only. Built once per test process; every call
    /// returns a fresh copy.
    /// </summary>
    public static async Task<TempRepo> OperationsAsync()
    {
        var template = await TemplateAsync("ops", OperationsTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> OperationsTemplate = new(() => KeepUntilExit(BuildOperationsAsync()));

    /// <summary>The operations scenario's file contents, as committed on each branch.</summary>
    public static class OperationsFiles
    {
        public const string Readme = "# Operations\n";

        public const string ReadmeStashed = "# Operations\n\nExperiment.\n";

        public const string Core = "core\n";

        public const string CoreMainWork = "core\nmain work\n";

        public const string CoreConflict = "core\nconflict\n";

        public const string AlphaOne = "alpha 1\n";

        public const string AlphaTwo = "alpha 1\nalpha 2\n";

        public const string FastForward = "ff\n";

        /// <summary>src/core.txt as "Core again" leaves it (<see cref="StoppedAsync"/>, Revert and None).</summary>
        public const string CoreAgain = "core\ncore again\n";

        /// <summary>src/core.txt as the stash of <see cref="StoppedAsync"/>'s None state changes it.</summary>
        public const string CoreStashed = "core\nmain work, stashed\n";
    }

    /// <summary>
    /// A copy of <see cref="OperationsAsync"/> stopped in <paramref name="kind"/>, through git calls
    /// as a terminal would make them, on main unless said otherwise:
    /// <list type="bullet">
    /// <item>Merge: <c>git merge feature/conflict</c>; src/core.txt is conflicted.</item>
    /// <item>CherryPick: <c>git cherry-pick &lt;Alpha one&gt; &lt;Conflicting change&gt; &lt;Alpha two&gt;</c>;
    /// Alpha one is picked (a new commit on main), Conflicting change stops with src/core.txt
    /// conflicted, and Alpha two waits in <c>sequencer/todo</c>.</item>
    /// <item>Revert: a commit "Core again" (src/core.txt <see cref="OperationsFiles.CoreAgain"/>),
    /// then <c>git revert &lt;Main work&gt;</c>; src/core.txt is conflicted.</item>
    /// <item>Rebase: on feature/conflict, <c>git rebase main</c>; src/core.txt is conflicted.</item>
    /// <item>None: src/core.txt changed to <see cref="OperationsFiles.CoreStashed"/> and stashed
    /// as "Core experiment" (stash@{0}; "Experiment" becomes stash@{1}), a commit "Core again",
    /// then <c>git stash apply</c>: src/core.txt is conflicted and no operation is in progress.</item>
    /// </list>
    /// Built each time.
    /// </summary>
    public static async Task<TempRepo> StoppedAsync(InProgressKind kind)
    {
        var repo = await OperationsAsync().ConfigureAwait(false);
        try
        {
            switch (kind)
            {
                case InProgressKind.Merge:
                    await ExpectStopAsync(repo, "merge", "feature/conflict").ConfigureAwait(false);
                    break;

                case InProgressKind.CherryPick:
                    var alphaOne = await RevParseAsync(repo, "refs/heads/feature/alpha~1").ConfigureAwait(false);
                    var conflicting = await RevParseAsync(repo, "refs/heads/feature/conflict").ConfigureAwait(false);
                    var alphaTwo = await RevParseAsync(repo, "refs/heads/feature/alpha").ConfigureAwait(false);
                    await ExpectStopAsync(repo, "cherry-pick", alphaOne, conflicting, alphaTwo).ConfigureAwait(false);
                    break;

                case InProgressKind.Revert:
                    var mainWork = await RevParseAsync(repo, "refs/heads/main").ConfigureAwait(false);
                    await repo.CommitFileAsync("src/core.txt", OperationsFiles.CoreAgain, "Core again").ConfigureAwait(false);
                    await ExpectStopAsync(repo, "revert", mainWork).ConfigureAwait(false);
                    break;

                case InProgressKind.Rebase:
                    await repo.CheckoutAsync("feature/conflict").ConfigureAwait(false);
                    await ExpectStopAsync(repo, "rebase", "main").ConfigureAwait(false);
                    break;

                case InProgressKind.None:
                    repo.WriteFile("src/core.txt", OperationsFiles.CoreStashed);
                    await repo.StashAsync("Core experiment").ConfigureAwait(false);
                    await repo.CommitFileAsync("src/core.txt", OperationsFiles.CoreAgain, "Core again").ConfigureAwait(false);
                    await ExpectStopAsync(repo, "stash", "apply").ConfigureAwait(false);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }

            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The remote sync scenario ("sync"): a repository with a bare <c>origin</c>
    /// (<c>origin.git</c> next to the working tree). Commits, from 2026-01-01 12:00 UTC:
    /// <list type="number">
    /// <item>12:00 "Initial commit" on main (README.md "# Sync\n")</item>
    /// <item>12:01 "Shared work" on main (src/shared.txt "shared\n"); main pushed and tracked</item>
    /// <item>12:02 "Published work" on published (src/published.txt "published\n"); pushed and tracked</item>
    /// <item>12:03 "Unpushed work" on published (src/published.txt "published\nunpushed\n"): published is 1 ahead</item>
    /// <item>12:04 "Topic work" on topic, from Shared work (src/topic.txt "topic\n"); never pushed, no upstream</item>
    /// </list>
    /// HEAD is on a clean main. <see cref="PushFromElsewhereAsync"/> makes origin ahead. Built
    /// once per test process; every call returns a fresh copy.
    /// </summary>
    public static async Task<TempRepo> RemoteSyncAsync()
    {
        var template = await TemplateAsync("sync", RemoteSyncTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> RemoteSyncTemplate = new(() => KeepUntilExit(BuildRemoteSyncAsync()));

    /// <summary>
    /// Pushes a commit to <paramref name="sync"/>'s <c>origin</c> from another clone, as someone
    /// else would: the clone <c>other</c> next to the working tree is made the first time (per
    /// test, since each test has its own copy), fetches, puts its <paramref name="branch"/> where
    /// origin has it (on origin's main when origin has no such branch), writes
    /// <paramref name="file"/>, commits it with <paramref name="message"/> at the next minute of
    /// <paramref name="sync"/>'s clock, and pushes. Returns the new commit's SHA. A fetch in
    /// <paramref name="sync"/> then shows it behind; a local commit makes it diverge.
    /// </summary>
    public static async Task<string> PushFromElsewhereAsync(TempRepo sync, string branch, string file, string content, string message)
    {
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        const string Name = "other";
        var other = Directory.Exists(sync.SiblingPath(Name))
            ? sync.OpenSibling(Name)
            : await sync.CloneSiblingAsync(Name, sync.SiblingPath("origin.git")).ConfigureAwait(false);

        await other.GitAsync("fetch", "--quiet", "origin").ConfigureAwait(false);
        var start = await other.RefTargetAsync("refs/remotes/origin/" + branch).ConfigureAwait(false) is not null
            ? "refs/remotes/origin/" + branch
            : "refs/remotes/origin/main";
        await other.GitAsync("checkout", "--quiet", "--no-track", "-B", branch, start).ConfigureAwait(false);
        var sha = await other.CommitFileAsync(file, content, message).ConfigureAwait(false);
        await other.GitAsync("push", "--quiet", "origin", $"refs/heads/{branch}:refs/heads/{branch}").ConfigureAwait(false);
        return sha;
    }

    /// <summary>
    /// The blocked checkout scenario ("blocked"): main has notes.txt with the seven lines
    /// "one" to "seven" (12:00 "Add notes"); feature/other changes line 7 to "seven (other)"
    /// (12:01 "Other change"); feature/clash, from Add notes, changes line 1 to "one (clash)"
    /// (12:02 "Clashing change"); feature/addfile, from Other change, adds newfile.txt
    /// "theirs\n" (12:03 "Add newfile"). HEAD is on main with an unstaged change of line 1 to
    /// "one (local)" and an untracked newfile.txt "mine\n". Checking out feature/other or
    /// feature/clash is blocked by the local change; stashing it, checking out and applying it
    /// again succeeds on feature/other and conflicts on feature/clash. Checking out
    /// feature/addfile is blocked by the untracked file and by the local change. Built once per
    /// test process; every call returns a fresh copy.
    /// </summary>
    public static async Task<TempRepo> CheckoutBlockedAsync()
    {
        var template = await TemplateAsync("blocked", CheckoutBlockedTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> CheckoutBlockedTemplate = new(() => KeepUntilExit(BuildCheckoutBlockedAsync()));

    /// <summary>The blocked checkout scenario's notes.txt in each version.</summary>
    public static class CheckoutBlockedFiles
    {
        public const string Notes = "one\ntwo\nthree\nfour\nfive\nsix\nseven\n";

        public const string NotesOther = "one\ntwo\nthree\nfour\nfive\nsix\nseven (other)\n";

        public const string NotesClash = "one (clash)\ntwo\nthree\nfour\nfive\nsix\nseven\n";

        public const string NotesLocal = "one (local)\ntwo\nthree\nfour\nfive\nsix\nseven\n";

        public const string NewFileCommitted = "theirs\n";

        public const string NewFileUntracked = "mine\n";
    }

    /// <summary>
    /// The edge cases scenario ("edges"): small cases the reviews found. Commits, from
    /// 2026-01-01 12:00 UTC:
    /// <list type="number">
    /// <item>12:00 "Initial commit" on main (README.md "# Edge cases\n")</item>
    /// <item>12:01 "Main work" on main (src/main.txt "main\n"); main pushed to the bare
    /// <c>origin</c> and tracked</item>
    /// <item>12:02 "Alpha work" on feature/alpha, from Main work (src/alpha.txt "alpha\n")</item>
    /// <item>12:03 "Detached work" (src/detached.txt "detached\n") on a detached HEAD, which stays
    /// there: no ref reaches it</item>
    /// </list>
    /// and these refs:
    /// <list type="bullet">
    /// <item><c>side</c> at Initial commit, behind main, checked out in the linked worktree
    /// <c>edges-wt</c> next to the working tree: it cannot be fast-forwarded (<c>git fetch .</c>
    /// refuses it).</item>
    /// <item><c>loc</c> at Initial commit, whose upstream is local: <c>branch.loc.remote = .</c>,
    /// <c>branch.loc.merge = refs/heads/main</c> (behind 1).</item>
    /// <item>A local branch named <c>origin/main</c> (<c>refs/heads/origin/main</c>) at Initial
    /// commit, beside the remote branch <c>refs/remotes/origin/main</c> at Main work.</item>
    /// <item>A lightweight tag <c>feature/alpha</c> at Main work, beside the branch feature/alpha.</item>
    /// </list>
    /// Built once per test process; every call returns a fresh copy, whose worktree is joined to
    /// the copy.
    /// </summary>
    public static async Task<TempRepo> EdgeCasesAsync()
    {
        var template = await TemplateAsync("edges", EdgeCasesTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> EdgeCasesTemplate = new(() => KeepUntilExit(BuildEdgeCasesAsync()));

    /// <summary>The folder of <see cref="EdgeCasesAsync"/>'s linked worktree, next to the working tree.</summary>
    public const string EdgeCasesWorktree = "edges-wt";

    /// <summary>
    /// A repository ("auth") whose <c>origin</c> is <paramref name="server"/>'s repository, at
    /// <see cref="AuthRemoteServer.Url"/>: as if cloned when the server's main had only
    /// 12:00 "Initial commit" (README.md "# Remote\n"). The server's main has since moved on to
    /// 12:01 "Remote work" (src/remote.txt "remote work\n"), pushed from another clone, so a fetch
    /// brings one commit and main is then behind 1. The local repository was built through the
    /// file system, not through the server, and has never seen "Remote work": no request has
    /// reached the server yet. The clone <c>elsewhere</c> next to the working tree, which pushed
    /// "Remote work" into the server's repository through the file system, can push more
    /// (<see cref="TempRepo.OpenSibling"/>). Made each time for its server, from a template built
    /// once per test process: the server's empty repository is replaced by the template's. Dispose
    /// it before its server.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="server"/> serves an existing repository in place (<see cref="AuthRemoteServer.ServesInPlace"/>), which is not the server's to replace.</exception>
    public static async Task<TempRepo> AuthRemoteAsync(AuthRemoteServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        if (server.ServesInPlace)
        {
            throw new ArgumentException(
                $"The server serves {server.RepositoryPath} in place; the auth scenario replaces the server's own repository and would delete that one. Start the server without an existing repository.",
                nameof(server));
        }

        var template = await TemplateAsync("auth", AuthRemoteTemplate).ConfigureAwait(false);
        var repo = await template.CopyAsync().ConfigureAwait(false);
        try
        {
            var bare = repo.SiblingPath(AuthRemoteServer.RepositoryName);
            TempRepo.DeleteDirectory(server.RepositoryPath);
            TempRepo.CopyDirectory(bare, server.RepositoryPath);
            TempRepo.DeleteDirectory(bare);
            await repo.GitAsync("remote", "set-url", "origin", server.Url).ConfigureAwait(false);
            await repo.OpenSibling("elsewhere").GitAsync("remote", "set-url", "origin", server.RepositoryPath).ConfigureAwait(false);
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static readonly Lazy<Task<TempRepo>> AuthRemoteTemplate = new(() => KeepUntilExit(BuildAuthRemoteAsync()));

    private static async Task<TempRepo> BuildOperationsAsync()
    {
        var repo = await TempRepo.CreateAsync("ops");
        try
        {
            await repo.CommitFileAsync("README.md", OperationsFiles.Readme, "Initial commit");
            await repo.CommitFileAsync("src/core.txt", OperationsFiles.Core, "Add core");
            await repo.AnnotatedTagAsync("v1.0", "Version 1.0");
            await repo.CreateBranchAsync("feature/alpha");
            await repo.CreateBranchAsync("feature/conflict");

            await repo.CheckoutAsync("feature/alpha");
            await repo.CommitFileAsync("src/alpha.txt", OperationsFiles.AlphaOne, "Alpha one");
            await repo.CommitFileAsync("src/alpha.txt", OperationsFiles.AlphaTwo, "Alpha two");

            await repo.CheckoutAsync("main");
            await repo.CommitFileAsync("src/core.txt", OperationsFiles.CoreMainWork, "Main work");
            await repo.TagAsync("v1.1");

            await repo.CreateBranchAsync("feature/ff");
            await repo.CheckoutAsync("feature/ff");
            await repo.CommitFileAsync("docs/ff.txt", OperationsFiles.FastForward, "Fast-forward me");

            await repo.CheckoutAsync("feature/conflict");
            await repo.CommitFileAsync("src/core.txt", OperationsFiles.CoreConflict, "Conflicting change");

            await repo.CheckoutAsync("main");
            await repo.AddBareRemoteAsync("origin");
            await repo.PushAsync("origin", setUpstream: true, "main", "feature/alpha");

            repo.WriteFile("README.md", OperationsFiles.ReadmeStashed);
            await repo.StashAsync("Experiment");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static async Task<TempRepo> BuildRemoteSyncAsync()
    {
        var repo = await TempRepo.CreateAsync("sync");
        try
        {
            await repo.CommitFileAsync("README.md", "# Sync\n", "Initial commit");
            await repo.CommitFileAsync("src/shared.txt", "shared\n", "Shared work");
            await repo.AddBareRemoteAsync("origin");
            await repo.PushAsync("origin", setUpstream: true, "main");

            await repo.CreateBranchAsync("published");
            await repo.CheckoutAsync("published");
            await repo.CommitFileAsync("src/published.txt", "published\n", "Published work");
            await repo.PushAsync("origin", setUpstream: true, "published");
            await repo.CommitFileAsync("src/published.txt", "published\nunpushed\n", "Unpushed work");

            await repo.CreateBranchAsync("topic", "main");
            await repo.CheckoutAsync("topic");
            await repo.CommitFileAsync("src/topic.txt", "topic\n", "Topic work");

            await repo.CheckoutAsync("main");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static async Task<TempRepo> BuildCheckoutBlockedAsync()
    {
        var repo = await TempRepo.CreateAsync("blocked");
        try
        {
            await repo.CommitFileAsync("notes.txt", CheckoutBlockedFiles.Notes, "Add notes");

            await repo.CreateBranchAsync("feature/other");
            await repo.CheckoutAsync("feature/other");
            await repo.CommitFileAsync("notes.txt", CheckoutBlockedFiles.NotesOther, "Other change");

            await repo.CreateBranchAsync("feature/clash", "main");
            await repo.CheckoutAsync("feature/clash");
            await repo.CommitFileAsync("notes.txt", CheckoutBlockedFiles.NotesClash, "Clashing change");

            await repo.CreateBranchAsync("feature/addfile", "feature/other");
            await repo.CheckoutAsync("feature/addfile");
            await repo.CommitFileAsync("newfile.txt", CheckoutBlockedFiles.NewFileCommitted, "Add newfile");

            await repo.CheckoutAsync("main");
            repo.WriteFile("notes.txt", CheckoutBlockedFiles.NotesLocal).WriteFile("newfile.txt", CheckoutBlockedFiles.NewFileUntracked);
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static async Task<TempRepo> BuildEdgeCasesAsync()
    {
        var repo = await TempRepo.CreateAsync("edges");
        try
        {
            await repo.CommitFileAsync("README.md", "# Edge cases\n", "Initial commit");
            await repo.CreateBranchAsync("side");
            await repo.CreateBranchAsync("loc");
            await repo.ConfigAsync("branch.loc.remote", ".");
            await repo.ConfigAsync("branch.loc.merge", "refs/heads/main");
            await repo.CreateBranchAsync("origin/main");

            await repo.CommitFileAsync("src/main.txt", "main\n", "Main work");
            await repo.AddBareRemoteAsync("origin");
            await repo.PushAsync("origin", setUpstream: true, "main");

            await repo.CreateBranchAsync("feature/alpha");
            await repo.CheckoutAsync("feature/alpha");
            await repo.CommitFileAsync("src/alpha.txt", "alpha\n", "Alpha work");
            await repo.CheckoutAsync("main");
            await repo.TagAsync("feature/alpha", "refs/heads/main");

            await repo.GitAsync("worktree", "add", "--quiet", repo.SiblingPath(EdgeCasesWorktree), "side");

            await repo.DetachAsync("refs/heads/main");
            await repo.CommitFileAsync("src/detached.txt", "detached\n", "Detached work");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The template of <see cref="AuthRemoteAsync"/>, with the bare repository next to the working
    /// tree in place of the server's, and a URL that the copy points at its server.
    /// </summary>
    private static async Task<TempRepo> BuildAuthRemoteAsync()
    {
        var repo = await TempRepo.CreateAsync("auth");
        try
        {
            // The bare repository as the server makes its own (AuthRemoteServer.StartAsync).
            var bare = repo.SiblingPath(AuthRemoteServer.RepositoryName);
            await repo.GitAsync("init", "--quiet", "--bare", "--initial-branch", "main", bare);
            await repo.GitAsync("--git-dir", bare, "config", "gc.auto", "0");
            await repo.GitAsync("--git-dir", bare, "config", "receive.autogc", "false");

            await repo.CommitFileAsync("README.md", "# Remote\n", "Initial commit");
            await repo.GitAsync("push", "--quiet", bare, "refs/heads/main:refs/heads/main");

            // What a clone would have set up.
            await repo.GitAsync("remote", "add", "origin", "http://127.0.0.1:1/" + AuthRemoteServer.RepositoryName);
            await repo.GitAsync("update-ref", "refs/remotes/origin/main", "refs/heads/main");
            await repo.ConfigAsync("branch.main.remote", "origin");
            await repo.ConfigAsync("branch.main.merge", "refs/heads/main");

            var elsewhere = await repo.CloneSiblingAsync("elsewhere", bare);
            await elsewhere.CommitFileAsync("src/remote.txt", "remote work\n", "Remote work");
            await elsewhere.GitAsync("push", "--quiet", "origin", "refs/heads/main:refs/heads/main");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>Runs a git step that is meant to stop (a conflict), and fails the build when it does not.</summary>
    private static async Task ExpectStopAsync(TempRepo repo, params string[] arguments)
    {
        var result = await repo.GitAllowingFailureAsync(arguments).ConfigureAwait(false);
        if (result.Succeeded)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} was expected to stop with a conflict, and it did not.");
        }
    }

    private static async Task<string> RevParseAsync(TempRepo repo, string revision) =>
        (await repo.GitAsync("rev-parse", "--verify", revision).ConfigureAwait(false)).StandardOutput.Trim();
}
