using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using Protect = VisualCommit.Testing.Scenarios.ProtectFiles;

namespace VisualCommit.Git.Tests;

/// <summary>
/// C12's git layer (docs/phase-5-design.md 9, D153, D179; the tests of 9.4), run by the app's
/// <see cref="GitRepository"/> against real git on the "protect" scenario and read back with plain
/// git: a refused checkout of each form changes nothing; the automatic stash's round trip (the
/// stash, the checkout that keeps ignored files, the way back); what the stash cannot take; the
/// ignored files in a checkout's way; the files a half-made stash deleted put back; the paths a
/// stash would write; and <c>merge-base --is-ancestor</c>, which decides when the stash is taken.
/// </summary>
public class ProtectedChangesTests
{
    /// <summary>The automatic stash's message for Describe the app (9.2), as the app passes it.</summary>
    private const string AutoStashMessage = "Auto stash before checking out f39ea2d";

    // ---- A refusal changes nothing (0.7) ---------------------------------------------------------

    [Fact]
    public async Task A_refused_checkout_of_each_form_throws_gits_refusal_and_changes_nothing()
    {
        // switch --no-guess <b>, switch --detach <sha> with and without --no-overwrite-ignore,
        // switch --create <b> <sha> (no branch is made) and switch --create <b> --track <remote>.
        await AssertRefusedAsync(repository => repository.CheckoutBranchAsync("other", keepIgnored: false, cancellationToken: TestCancelled));
        await AssertRefusedAsync(async repository => await repository.CheckoutDetachedAsync(await OtherEditsAsync(repository), keepIgnored: false, cancellationToken: TestCancelled));
        await AssertRefusedAsync(async repository => await repository.CheckoutDetachedAsync(await OtherEditsAsync(repository), keepIgnored: true, cancellationToken: TestCancelled));
        await AssertRefusedAsync(async repository => await repository.CreateBranchAsync("topic", await OtherEditsAsync(repository), checkout: true, noTrack: false, keepIgnored: false, cancellationToken: TestCancelled));
        await AssertRefusedAsync(
            repository => repository.CheckoutTrackingBranchAsync("tracked", "refs/remotes/mirror/other", keepIgnored: false, cancellationToken: TestCancelled),
            async repo =>
            {
                // A remote whose branch is other, fetched before the change is made.
                await repo.GitAsync("remote", "add", "mirror", repo.Path);
                await repo.GitAsync("fetch", "--quiet", "mirror");
            });
    }

    // ---- The automatic stash (9.2) ---------------------------------------------------------------

    [Fact]
    public async Task The_automatic_stash_takes_every_change_the_checkout_keeps_the_ignored_file_and_the_way_back_gives_everything_back()
    {
        using var repo = await Scenarios.ProtectAsync();
        await MakeChangesAsync(repo);
        var repository = await OpenAsync(repo);
        var main = await repo.HeadAsync();
        var target = await ResolveCommitAsync(repo, "refs/heads/main~1");
        var before = await repo.ExactStateAsync();
        var reflog = await HeadReflogAsync(repo);

        // Steps 3 and 4: the stash with the untracked files, without GIT_LITERAL_PATHSPECS (D108),
        // then the checkout that refuses rather than overwrite an ignored file.
        var stash = await repository.StashAsync(AutoStashMessage, includeUntracked: true, cancellationToken: TestCancelled);
        await repository.CheckoutDetachedAsync(target, keepIgnored: true, cancellationToken: TestCancelled);

        // The report's P1: the stash 256077f on main's tip (D72's dates make it the same in every run).
        Assert.Equal("256077fbd990adad7fe00df31789590d7370aff0", stash);
        Assert.Equal(target, await repo.HeadAsync());
        Assert.False((await repo.GitAllowingFailureAsync("symbolic-ref", "-q", "HEAD")).Succeeded, "HEAD is not detached.");
        var entry = Assert.Single(await repo.StashListAsync());
        Assert.Equal(("stash@{0}", "On main: " + AutoStashMessage), (entry.Selector, entry.Message));
        Assert.Equal(main, await ResolveCommitAsync(repo, "stash@{0}^1"));
        Assert.Equal(["scratch.txt"], Lines(await repo.GitAsync("ls-tree", "-r", "--name-only", "stash@{0}^3")));
        Assert.Equal(["!! settings.local.json"], await StatusAsync(repo));
        Assert.Equal(Protect.SettingsLocal, Read(repo, "settings.local.json"));
        var reflogNow = await HeadReflogAsync(repo);
        Assert.Equal(["checkout: moving from main to " + target, "reset: moving to HEAD", .. reflog], reflogNow);

        // The way back (D166's undo runs the same): main, then the stash popped with its index.
        await repository.CheckoutBranchAsync("main", keepIgnored: false, cancellationToken: TestCancelled);
        Assert.Equal(ApplyResult.Applied, await repository.ApplyStashAsync(stash!, StashApplyMode.PopKeepingIndex, cancellationToken: TestCancelled));

        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Empty(await repo.StashListAsync());
    }

    [Fact]
    public async Task The_automatic_stash_keeps_the_empty_untracked_folders_through_the_checkout_and_the_way_back()
    {
        using var repo = await Scenarios.ProtectAsync();
        await MakeChangesAsync(repo);
        repo.WriteFile("work/file.txt", "work\n");
        string[] folders = ["uploads", "data/cache", "work/empty"];
        foreach (var folder in folders)
        {
            Directory.CreateDirectory(Path.Combine(repo.Path, folder));
        }

        var repository = await OpenAsync(repo);
        var target = await ResolveCommitAsync(repo, "refs/heads/main~1");
        var before = await repo.ExactStateAsync();
        void AssertFoldersKept(string when) =>
            Assert.True(folders.All(folder => Directory.Exists(Path.Combine(repo.Path, folder))), $"An empty folder was gone {when}.");

        // git's clean -d removes them, and no stash holds a folder (15.9).
        var stash = await repository.StashAsync(AutoStashMessage, includeUntracked: true, cancellationToken: TestCancelled);
        Assert.False(File.Exists(Path.Combine(repo.Path, "work", "file.txt")));
        AssertFoldersKept("after the stash");
        await repository.CheckoutDetachedAsync(target, keepIgnored: true, cancellationToken: TestCancelled);
        AssertFoldersKept("after the checkout");

        await repository.CheckoutBranchAsync("main", keepIgnored: false, cancellationToken: TestCancelled);
        Assert.Equal(ApplyResult.Applied, await repository.ApplyStashAsync(stash!, StashApplyMode.PopKeepingIndex, cancellationToken: TestCancelled));
        AssertFoldersKept("after the way back");
        Assert.Equal(before, await repo.ExactStateAsync());
    }

    [Fact]
    public async Task The_stash_takes_nothing_on_a_clean_tree_or_with_only_a_staged_submodule_and_refuses_conflicted_files()
    {
        using var repo = await Scenarios.ProtectAsync();
        var repository = await OpenAsync(repo);

        // Ignored files are no changes: "No local changes to save" (PD8 makes no call at all there).
        Assert.Null(await repository.StashAsync(AutoStashMessage, includeUntracked: true, cancellationToken: TestCancelled));

        // A staged submodule (a gitlink without its folder) only: git has nothing it stashes either.
        await repo.GitAsync("update-index", "--add", "--cacheinfo", $"160000,{await repo.HeadAsync()},module");
        Assert.Null(await repository.StashAsync(AutoStashMessage, includeUntracked: true, cancellationToken: TestCancelled));
        await repo.GitAsync("rm", "--cached", "--quiet", "module");

        // A conflicted file: git refuses ("needs merge"), and nothing is stashed.
        repo.WriteFile("src/app.txt", "app\nlocal\n");
        await repo.GitAsync("commit", "--quiet", "--all", "--message", "Local app");
        await repo.GitAllowingFailureAsync("merge", "--no-edit", "refs/heads/other");
        Assert.Contains("UU src/app.txt", await StatusAsync(repo));
        await repo.GitAsync("merge", "--quit");
        var refused = await Assert.ThrowsAsync<GitException>(() => repository.StashAsync(AutoStashMessage, includeUntracked: true, cancellationToken: TestCancelled));
        Assert.Contains("needs merge", refused.StandardOutput + refused.StandardError, StringComparison.Ordinal);
        Assert.Empty(await repo.StashListAsync());
    }

    [Fact]
    public async Task During_a_merge_without_conflicts_the_stash_would_drop_MERGE_HEAD_so_the_app_refuses_first()
    {
        // Design 0.7: why 9.2's first step refuses while an operation is in progress.
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("notes.txt", "mine\n");
        await repo.GitAsync("merge", "--no-commit", "--no-ff", "refs/heads/other");
        Assert.True(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")));
        var repository = await OpenAsync(repo);

        Assert.NotNull(await repository.StashAsync(AutoStashMessage, includeUntracked: true, cancellationToken: TestCancelled));

        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")), "The stash kept the merge in progress.");
    }

    // ---- Ignored files in the way (9.1, 9.3) -----------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_old_commit_that_tracks_an_ignored_file_is_refused_with_no_overwrite_ignore_and_the_file_is_kept(bool withChanges)
    {
        using var repo = await Scenarios.ProtectAsync();
        if (withChanges)
        {
            await MakeChangesAsync(repo);
        }

        var repository = await OpenAsync(repo);
        var guide = await ResolveCommitAsync(repo, "refs/heads/main~3");
        var stash = withChanges ? await repository.StashAsync("Auto stash before checking out 73e0bab", includeUntracked: true, cancellationToken: TestCancelled) : null;
        var before = await repo.ExactStateAsync();
        var reflog = await HeadReflogAsync(repo);

        var refused = await Assert.ThrowsAsync<GitException>(() => repository.CheckoutDetachedAsync(guide, keepIgnored: true, cancellationToken: TestCancelled));

        // TP6: the untracked block naming the ignored file; nothing changed, the file intact.
        Assert.Equal(1, refused.ExitCode);
        Assert.Equal(GitFailureKind.CheckoutBlockedByUntrackedFiles, GitFailureClassifier.Classify(refused.StandardError, refused.StandardOutput, refused.ExitCode));
        Assert.Equal(["settings.local.json"], GitFailureClassifier.BlockedPaths(refused.StandardError));
        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Equal(reflog, await HeadReflogAsync(repo));
        Assert.Equal(Protect.SettingsLocal, Read(repo, "settings.local.json"));
        Assert.Equal(["settings.local.json"], await repository.FindIgnoredAsync(["settings.local.json"], TestCancelled));

        if (stash is not null)
        {
            // 9.2 step 5: popped back, everything as it was before the stash.
            Assert.Equal(ApplyResult.Applied, await repository.ApplyStashAsync(stash, StashApplyMode.PopKeepingIndex, cancellationToken: TestCancelled));
            Assert.Equal([" M src/app.txt", "MM src/lib.txt", "?? scratch.txt", "!! settings.local.json"], await StatusAsync(repo));
            Assert.Empty(await repo.StashListAsync());
        }
    }

    [Fact]
    public async Task Find_ignored_answers_files_and_folders_by_their_files_without_literal_pathspecs()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile(".gitignore", Protect.Gitignore + "build/\n*.log\n")
            .WriteFile("build/out.bin", "out\n")
            .WriteFile("logs/today.log", "log\n")
            .WriteFile("mixed/a.log", "a\n")
            .WriteFile("mixed/keep.txt", "keep\n")
            .WriteFile("notes", "mine\n")
            .WriteFile("my notes [draft].log", "draft\n");
        Directory.CreateDirectory(Path.Combine(repo.Path, "empty"));
        var repository = await OpenAsync(repo);

        var ignored = await repository.FindIgnoredAsync(
            ["settings.local.json", "build", "logs", "mixed", "notes", "my notes [draft].log", "empty", "missing.log"],
            TestCancelled);

        // logs is not ignored by name (*.log), but every file in it is; mixed keeps a file.
        // A path is taken as it is, brackets too; a pattern matches a path that does not exist.
        Assert.Equal(["build", "logs", "missing.log", "my notes [draft].log", "settings.local.json"], ignored.Order(StringComparer.Ordinal));
        Assert.Empty(await repository.FindIgnoredAsync([], TestCancelled));
        Assert.Empty(await repository.FindIgnoredAsync(["notes"], TestCancelled));
    }

    // ---- The half-made stash (9.2 step 3) --------------------------------------------------------

    [Fact]
    public async Task Restoring_working_tree_files_from_a_stash_puts_back_untracked_files_and_leaves_the_index_alone()
    {
        using var repo = await Scenarios.ProtectAsync();
        await repo.WriteFile("src/lib.txt", "lib\nstaged\n").GitAsync("add", "--", "src/lib.txt");
        repo.WriteFile("scratch.txt", "scratch\n").WriteFile("ideas/café notes.txt", "café\n").WriteFile("ideas/[draft].txt", "draft\n");
        var repository = await OpenAsync(repo);
        var stash = await repository.StashAsync("Half", includeUntracked: true, cancellationToken: TestCancelled);
        var index = (await repo.GitAsync("ls-files", "--stage")).StandardOutput;

        // As a stash that git stopped half-way leaves it: one untracked file kept, the others gone.
        repo.WriteFile("scratch.txt", "scratch\n");
        await repository.RestoreWorkingTreeFilesAsync(stash + "^3", ["ideas/café notes.txt", "ideas/[draft].txt"], cancellationToken: TestCancelled);

        Assert.Equal("café\n", Read(repo, "ideas/café notes.txt"));
        Assert.Equal("draft\n", Read(repo, "ideas/[draft].txt"));
        Assert.Equal(index, (await repo.GitAsync("ls-files", "--stage")).StandardOutput);
        Assert.Equal(["?? ideas/[draft].txt", "?? \"ideas/café notes.txt\"", "?? scratch.txt", "!! settings.local.json"], await StatusAsync(repo));
        await repository.RestoreWorkingTreeFilesAsync(stash + "^3", [], cancellationToken: TestCancelled);
    }

    [Fact]
    public async Task A_file_another_program_holds_open_stops_the_stash_half_way_on_windows_and_restore_puts_back_what_git_deleted()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("src/app.txt", "app\nlocal\n").WriteFile("held.txt", "held\n").WriteFile("other.txt", "other\n");
        var repository = await OpenAsync(repo);
        var before = await repo.RefTargetAsync("refs/stash");

        string? stash;
        using (new FileStream(Path.Combine(repo.Path, "held.txt"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (!OperatingSystem.IsWindows())
            {
                // Elsewhere an open file can be deleted: the stash completes.
                stash = await repository.StashAsync("Held", includeUntracked: true, cancellationToken: TestCancelled);
                Assert.NotNull(stash);
                Assert.Empty(await StatusAsync(repo, ignored: false));
                return;
            }

            // Design 0.7: the stash is saved, git deletes the other untracked file, fails on the
            // held one ("failed to remove") and skips resetting the tracked change.
            var failure = await Assert.ThrowsAsync<GitException>(() => repository.StashAsync("Held", includeUntracked: true, cancellationToken: TestCancelled));
            Assert.Contains("held.txt", failure.StandardError, StringComparison.Ordinal);
            stash = await repo.RefTargetAsync("refs/stash");
            Assert.NotEqual(before, stash);
        }

        Assert.False(File.Exists(Path.Combine(repo.Path, "other.txt")), "Git kept the untracked file it could delete.");
        Assert.Equal("app\nlocal\n", Read(repo, "src/app.txt"));
        var saved = await repository.ReadStashPathsAsync(stash!, TestCancelled);
        Assert.Equal(["held.txt", "other.txt"], saved.Untracked);

        var missing = saved.Untracked.Where(path => !File.Exists(Path.Combine(repo.Path, path))).ToList();
        await repository.RestoreWorkingTreeFilesAsync(stash + "^3", missing, cancellationToken: TestCancelled);

        Assert.Equal("other\n", Read(repo, "other.txt"));
        Assert.Equal("held\n", Read(repo, "held.txt"));
        Assert.Equal([" M src/app.txt", "?? held.txt", "?? other.txt"], await StatusAsync(repo, ignored: false));
    }

    [Fact]
    public async Task A_tracked_file_held_open_stops_the_stashs_reset_after_it_rewrote_the_others_and_the_put_back_gives_them_back()
    {
        using var repo = await Scenarios.ProtectAsync();
        await MakeChangesAsync(repo);
        await repo.WriteFile("todo.txt", "todo\n").GitAsync("add", "--", "todo.txt");
        File.Delete(Path.Combine(repo.Path, "docs", "guide.txt"));
        var repository = await OpenAsync(repo);
        var before = await repo.ExactStateAsync();
        var stashBefore = await repo.RefTargetAsync("refs/stash");

        string? stash;
        using (new FileStream(Path.Combine(repo.Path, "src", "app.txt"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (!OperatingSystem.IsWindows())
            {
                // Elsewhere an open file can be replaced: the stash completes.
                Assert.NotNull(await repository.StashAsync("Held", includeUntracked: true, cancellationToken: TestCancelled));
                return;
            }

            // 15.9: the stash is saved, git's clean deletes scratch.txt, its reset rewrites every
            // tracked file it can (src/lib.txt, docs/guide.txt, todo.txt), stops at the held one
            // ("unable to unlink") and writes no index.
            var failure = await Assert.ThrowsAsync<GitException>(() => repository.StashAsync("Held", includeUntracked: true, cancellationToken: TestCancelled));
            Assert.Contains("src/app.txt", failure.StandardError, StringComparison.Ordinal);
            stash = await repo.RefTargetAsync("refs/stash");
            Assert.NotEqual(stashBefore, stash);
            Assert.Equal("lib\n", Read(repo, "src/lib.txt"));
            Assert.Equal("app\nunstaged\n", Read(repo, "src/app.txt"));

            // Put back while the file is still held, as the app does.
            Assert.True(await repository.PutBackUnclearedStashAsync(stash!, cancellationToken: TestCancelled));
        }

        // Every change is in the working tree and the index again; the stash is a copy of them.
        await repo.GitAsync("stash", "drop", "--quiet");
        Assert.Equal(before, await repo.ExactStateAsync());
    }

    // ---- The paths a stash would write (9.3) ------------------------------------------------------

    [Fact]
    public async Task A_stashs_paths_are_both_sides_of_a_rename_and_its_untracked_files_none_without_a_third_parent()
    {
        using var repo = await Scenarios.ProtectAsync();
        await repo.GitAsync("mv", "docs/old.txt", "docs/très old.txt");
        repo.WriteFile("src/app.txt", "app\nstashed\n").WriteFile("todo.txt", "todo\n").WriteFile("new dir/é.txt", "é\n");
        var repository = await OpenAsync(repo);
        var withUntracked = await repository.StashAsync("App experiment", includeUntracked: true, cancellationToken: TestCancelled);

        var paths = await repository.ReadStashPathsAsync(withUntracked!, TestCancelled);

        Assert.Equal(["docs/old.txt", "docs/très old.txt", "src/app.txt"], paths.Changed);
        Assert.Equal(["new dir/é.txt", "todo.txt"], paths.Untracked);

        // Two parents (no --include-untracked): no untracked part.
        repo.WriteFile("README.md", "# Protect\n\nLocal.\n");
        var tracked = await repository.StashAsync("Tracked", includeUntracked: false, cancellationToken: TestCancelled);
        var trackedPaths = await repository.ReadStashPathsAsync(tracked!, TestCancelled);
        Assert.Equal(["README.md"], trackedPaths.Changed);
        Assert.Empty(trackedPaths.Untracked);
    }

    // ---- Is it on the branch (9.1) ----------------------------------------------------------------

    [Fact]
    public async Task Is_ancestor_follows_every_parent_peels_tags_and_says_no_for_another_branchs_commit()
    {
        using var repo = await Scenarios.ProtectAsync();
        await repo.MergeAsync("other");
        var repository = await OpenAsync(repo);

        Assert.True(await repository.IsAncestorAsync("refs/tags/v1.1", "HEAD", TestCancelled));
        Assert.True(await repository.IsAncestorAsync("HEAD", "HEAD", TestCancelled));
        Assert.True(await repository.IsAncestorAsync("refs/heads/other~1", "HEAD", TestCancelled));
        Assert.False(await repository.IsAncestorAsync("HEAD", "refs/heads/other", TestCancelled));
        await Assert.ThrowsAsync<GitException>(() => repository.IsAncestorAsync("refs/heads/nowhere", "HEAD", TestCancelled));
    }

    // ---- Helpers ----------------------------------------------------------------------------------

    /// <summary>The report's P25 changes: src/lib.txt staged then changed again, src/app.txt changed, scratch.txt untracked.</summary>
    private static async Task MakeChangesAsync(TempRepo repo)
    {
        await repo.WriteFile("src/lib.txt", "lib\nstaged\n").GitAsync("add", "--", "src/lib.txt");
        repo.WriteFile("src/lib.txt", "lib\nstaged\nand more\n").WriteFile("src/app.txt", "app\nunstaged\n").WriteFile("scratch.txt", "scratch\n");
    }

    /// <summary>
    /// On a fresh copy of the protect scenario with src/app.txt changed (and what
    /// <paramref name="prepare"/> adds first): <paramref name="checkout"/> throws git's refusal for
    /// the local change, and HEAD, the refs, the index, the status, the files and HEAD's reflog are
    /// as they were.
    /// </summary>
    private static async Task AssertRefusedAsync(Func<GitRepository, Task> checkout, Func<TempRepo, Task>? prepare = null)
    {
        using var repo = await Scenarios.ProtectAsync();
        if (prepare is not null)
        {
            await prepare(repo);
        }

        repo.WriteFile("src/app.txt", "app\nlocal\n");
        var repository = await OpenAsync(repo);
        var before = await repo.ExactStateAsync();
        var reflog = await HeadReflogAsync(repo);

        var refused = await Assert.ThrowsAsync<GitException>(() => checkout(repository));

        Assert.Equal(1, refused.ExitCode);
        Assert.Equal(GitFailureKind.CheckoutBlockedByLocalChanges, GitFailureClassifier.Classify(refused.StandardError, refused.StandardOutput, refused.ExitCode));
        Assert.Equal(["src/app.txt"], GitFailureClassifier.BlockedPaths(refused.StandardError));
        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Equal(reflog, await HeadReflogAsync(repo));
        Assert.Equal("main", await repo.CurrentBranchAsync());
    }

    /// <summary>Other edits, the commit only other reaches (row 2 of the report's P0).</summary>
    private static async Task<string> OtherEditsAsync(GitRepository repository) =>
        await repository.ResolveAsync("refs/heads/other~1", TestCancelled) ?? throw new InvalidOperationException("other~1 names no commit.");

    private static async Task<string> ResolveCommitAsync(TempRepo repo, string revision) =>
        (await repo.GitAsync("rev-parse", "--verify", revision + "^{commit}")).StandardOutput.Trim();

    private static async Task<string[]> HeadReflogAsync(TempRepo repo) => Lines(await repo.GitAsync("log", "-g", "--format=%gs", "HEAD"));

    /// <summary><c>git status --porcelain=v1 --untracked-files=all</c> (with <c>--ignored</c> when <paramref name="ignored"/>), names unquoted.</summary>
    private static async Task<string[]> StatusAsync(TempRepo repo, bool ignored = true) =>
        Lines(await repo.GitAsync(["-c", "core.quotePath=false", "status", "--porcelain=v1", "--untracked-files=all", .. (ignored ? new[] { "--ignored" } : [])]));

    private static string[] Lines(GitResult result) => result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToArray();

    private static string Read(TempRepo repo, string path) => File.ReadAllText(Path.Combine(repo.Path, path));

}
