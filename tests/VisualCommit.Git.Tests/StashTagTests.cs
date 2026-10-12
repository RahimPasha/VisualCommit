using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Testing.Scenarios;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Stash save, apply, pop and drop, tag create and delete (phase 3 design 2.24 to 2.28, 2.31;
/// D91, D108), run by the app's <see cref="GitRepository"/> against real git on the scenario
/// repos, and every result read back with plain git. The backups that the operations save before
/// a drop or a delete (2.0) are written here as the operation would write them, with
/// <c>git update-ref</c>: these tests prove that such a backup brings the stash or the tag back.
/// </summary>
public class StashTagTests
{
    // ---- Stash save (2.24) ----------------------------------------------------------------------

    [Fact]
    public async Task A_stash_with_untracked_files_leaves_a_clean_tree_and_a_pop_brings_every_file_back()
    {
        using var repo = await OperationsAsync();
        repo.WriteFile("README.md", "# Operations, staged\n");
        await repo.GitAsync("add", "README.md");
        repo.WriteFile("src/core.txt", "core\nmain work\nunstaged\n")
            .WriteFile("notes/new.txt", "new\n")
            .WriteFile("pages/[id].tsx", "page\n");
        var repository = await OpenAsync(repo);

        var stash = await repository.StashAsync("Work in progress", includeUntracked: true, cancellationToken: TestCancelled);

        // D108: the app's runner sets GIT_LITERAL_PATHSPECS for every call, and the stash must
        // still take the untracked files out of the working tree.
        Assert.Equal(await repo.RefTargetAsync("refs/stash"), stash);
        Assert.Empty(await PorcelainAsync(repo));
        Assert.False(File.Exists(Path.Combine(repo.Path, "notes", "new.txt")));
        Assert.False(File.Exists(Path.Combine(repo.Path, "pages", "[id].tsx")));
        Assert.Equal(["On main: Work in progress", "On main: Experiment"], (await repo.StashListAsync()).Select(entry => entry.Message));
        Assert.True((await repo.GitAllowingFailureAsync("rev-parse", "--verify", "--quiet", stash + "^3")).Succeeded, "The untracked files are not in the stash's third parent.");

        Assert.Equal(ApplyResult.Applied, await repository.ApplyStashAsync(stash!, StashApplyMode.Pop, cancellationToken: TestCancelled));

        Assert.Equal("# Operations, staged\n", ReadFile(repo, "README.md"));
        Assert.Equal("core\nmain work\nunstaged\n", ReadFile(repo, "src/core.txt"));
        Assert.Equal("new\n", ReadFile(repo, "notes/new.txt"));
        Assert.Equal("page\n", ReadFile(repo, "pages/[id].tsx"));
        Assert.Equal([" M README.md", " M src/core.txt", "?? notes/new.txt", "?? pages/[id].tsx"], await PorcelainAsync(repo));
        Assert.Equal(["On main: Experiment"], (await repo.StashListAsync()).Select(entry => entry.Message));
    }

    [Fact]
    public async Task A_stash_without_untracked_files_leaves_them_and_without_a_message_git_names_it()
    {
        using var repo = await OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nchanged\n").WriteFile("scratch.txt", "scratch\n");
        var repository = await OpenAsync(repo);

        var stash = await repository.StashAsync(null, includeUntracked: false, cancellationToken: TestCancelled);

        Assert.Equal(await repo.RefTargetAsync("refs/stash"), stash);
        Assert.Equal(["?? scratch.txt"], await PorcelainAsync(repo));
        Assert.Equal(OperationsFiles.CoreMainWork, ReadFile(repo, "src/core.txt"));
        var top = (await repo.StashListAsync())[0];
        Assert.StartsWith("WIP on main: ", top.Message, StringComparison.Ordinal);
        Assert.EndsWith(" Main work", top.Message, StringComparison.Ordinal);
        Assert.False((await repo.GitAllowingFailureAsync("rev-parse", "--verify", "--quiet", stash + "^3")).Succeeded, "Untracked files were stashed.");
    }

    [Fact]
    public async Task Nothing_to_stash_returns_null_and_adds_no_entry()
    {
        using var repo = await OperationsAsync();
        var repository = await OpenAsync(repo);

        Assert.Null(await repository.StashAsync("Nothing", includeUntracked: true, cancellationToken: TestCancelled));

        // Untracked files alone are nothing to stash when they are left out.
        repo.WriteFile("scratch.txt", "scratch\n");
        Assert.Null(await repository.StashAsync("Nothing", includeUntracked: false, cancellationToken: TestCancelled));

        Assert.Equal(["On main: Experiment"], (await repo.StashListAsync()).Select(entry => entry.Message));
        Assert.Equal(["?? scratch.txt"], await PorcelainAsync(repo));
    }

    [Fact]
    public async Task A_stash_before_the_first_commit_fails()
    {
        using var repo = await TempRepo.CreateAsync("unborn");
        repo.WriteFile("first.txt", "first\n");
        await repo.GitAsync("add", "first.txt");
        var repository = await OpenAsync(repo);

        await Assert.ThrowsAsync<GitException>(() => repository.StashAsync("First", includeUntracked: true, cancellationToken: TestCancelled));
        Assert.Equal(["A  first.txt"], await PorcelainAsync(repo));
    }

    // ---- Apply and pop (2.25) -------------------------------------------------------------------

    [Fact]
    public async Task Apply_keeps_the_stash_and_pop_drops_it()
    {
        using var repo = await OperationsAsync();
        var experiment = (await repo.StashListAsync())[0].Sha;
        var repository = await OpenAsync(repo);

        Assert.Equal(ApplyResult.Applied, await repository.ApplyStashAsync(experiment, StashApplyMode.Apply, cancellationToken: TestCancelled));
        Assert.Equal(OperationsFiles.ReadmeStashed, ReadFile(repo, "README.md"));
        Assert.Equal([experiment], (await repo.StashListAsync()).Select(entry => entry.Sha));

        await repo.GitAsync("checkout", "--", "README.md");
        Assert.Equal(ApplyResult.Applied, await repository.ApplyStashAsync(experiment, StashApplyMode.Pop, cancellationToken: TestCancelled));
        Assert.Equal(OperationsFiles.ReadmeStashed, ReadFile(repo, "README.md"));
        Assert.Empty(await repo.StashListAsync());
    }

    [Fact]
    public async Task The_stash_is_found_by_its_commit_after_the_list_has_changed()
    {
        using var repo = await OperationsAsync();
        var experiment = (await repo.StashListAsync())[0].Sha;
        repo.WriteFile("src/core.txt", "core\nlater\n");
        var later = await repo.StashAsync("Later");
        var repository = await OpenAsync(repo);

        // "Experiment" is stash@{1} now: popping it by its commit must not pop stash@{0}.
        Assert.Equal(ApplyResult.Applied, await repository.ApplyStashAsync(experiment, StashApplyMode.Pop, cancellationToken: TestCancelled));

        Assert.Equal(OperationsFiles.ReadmeStashed, ReadFile(repo, "README.md"));
        Assert.Equal(OperationsFiles.CoreMainWork, ReadFile(repo, "src/core.txt"));
        Assert.Equal([later], (await repo.StashListAsync()).Select(entry => entry.Sha));
    }

    [Fact]
    public async Task A_stash_no_longer_listed_is_reported_as_gone()
    {
        using var repo = await OperationsAsync();
        var experiment = (await repo.StashListAsync())[0].Sha;
        await repo.GitAsync("stash", "drop", "--quiet", "stash@{0}");
        var repository = await OpenAsync(repo);

        var apply = await Assert.ThrowsAsync<GitException>(() => repository.ApplyStashAsync(experiment, StashApplyMode.Apply, cancellationToken: TestCancelled));
        Assert.Equal(GitRepository.StashGoneMessage, apply.StandardError);
        var drop = await Assert.ThrowsAsync<GitException>(() => repository.DropStashAsync(experiment, TestCancelled));
        Assert.Equal(GitRepository.StashGoneMessage, drop.StandardError);
        Assert.Equal(OperationsFiles.Readme, ReadFile(repo, "README.md"));
    }

    [Theory]
    [InlineData(StashApplyMode.Apply)]
    [InlineData(StashApplyMode.Pop)]
    public async Task A_stash_that_conflicts_returns_conflicts_and_keeps_its_entry(StashApplyMode mode)
    {
        // The state Scenarios.StoppedAsync(None) builds, up to its last step, which the app makes here.
        using var repo = await OperationsAsync();
        repo.WriteFile("src/core.txt", OperationsFiles.CoreStashed);
        var coreExperiment = await repo.StashAsync("Core experiment");
        await repo.CommitFileAsync("src/core.txt", OperationsFiles.CoreAgain, "Core again");
        var before = await repo.StashListAsync();
        var repository = await OpenAsync(repo);

        Assert.Equal(ApplyResult.Conflicts, await repository.ApplyStashAsync(coreExperiment, mode, cancellationToken: TestCancelled));

        Assert.Equal(["src/core.txt"], await UnmergedAsync(repo));
        Assert.Equal(before, await repo.StashListAsync());
        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")), "A stash that conflicts starts no merge.");
    }

    [Fact]
    public async Task Applying_a_stash_onto_conflicted_files_fails_without_reporting_conflicts()
    {
        using var repo = await StoppedAsync(Scenarios.InProgressKind.None);
        var before = await repo.StashListAsync();
        var experiment = before.Single(entry => entry.Message == "On main: Experiment").Sha;
        var repository = await OpenAsync(repo);

        // The conflict was there before: the call's failure is git's, not a new conflict.
        await Assert.ThrowsAsync<GitException>(() => repository.ApplyStashAsync(experiment, StashApplyMode.Pop, cancellationToken: TestCancelled));

        Assert.Equal(before, await repo.StashListAsync());
        Assert.Equal(["src/core.txt"], await UnmergedAsync(repo));
        Assert.Equal(OperationsFiles.Readme, ReadFile(repo, "README.md"));
    }

    [Fact]
    public async Task A_pop_with_local_changes_in_the_way_fails_and_keeps_the_stash()
    {
        using var repo = await OperationsAsync();
        var experiment = (await repo.StashListAsync())[0].Sha;
        repo.WriteFile("README.md", "# Operations, local\n");
        var repository = await OpenAsync(repo);

        var failure = await Assert.ThrowsAsync<GitException>(() => repository.ApplyStashAsync(experiment, StashApplyMode.Pop, cancellationToken: TestCancelled));

        // 2.25's summary is chosen from this text.
        Assert.Contains("Your local changes to the following files would be overwritten by merge", failure.StandardError, StringComparison.Ordinal);
        Assert.Contains("README.md", failure.StandardError, StringComparison.Ordinal);
        Assert.Equal("# Operations, local\n", ReadFile(repo, "README.md"));
        Assert.Equal([experiment], (await repo.StashListAsync()).Select(entry => entry.Sha));
    }

    [Fact]
    public async Task A_pop_with_an_untracked_file_in_the_way_fails_and_keeps_the_stash()
    {
        using var repo = await OperationsAsync();
        repo.WriteFile("scratch.txt", "stashed\n");
        var repository = await OpenAsync(repo);
        var stash = await repository.StashAsync("Scratch", includeUntracked: true, cancellationToken: TestCancelled);
        repo.WriteFile("scratch.txt", "mine\n");

        await Assert.ThrowsAsync<GitException>(() => repository.ApplyStashAsync(stash!, StashApplyMode.Pop, cancellationToken: TestCancelled));

        Assert.Equal("mine\n", ReadFile(repo, "scratch.txt"));
        Assert.Equal(stash, (await repo.StashListAsync())[0].Sha);
        Assert.Empty(await UnmergedAsync(repo));
    }

    // ---- Pop keeping the index: stash changes and check out (2.31, D91) -------------------------

    [Fact]
    public async Task Stash_check_out_and_pop_keeping_the_index_carries_staged_changes_across()
    {
        using var repo = await CheckoutBlockedAsync();
        await repo.GitAsync("add", "notes.txt");
        var repository = await OpenAsync(repo);

        var stash = await repository.StashAsync("VisualCommit: before checking out feature/other", includeUntracked: false, cancellationToken: TestCancelled);
        Assert.Equal(["?? newfile.txt"], await PorcelainAsync(repo));
        await repo.GitAsync("switch", "--quiet", "feature/other");

        Assert.Equal(ApplyResult.Applied, await repository.ApplyStashAsync(stash!, StashApplyMode.PopKeepingIndex, cancellationToken: TestCancelled));

        Assert.Equal("one (local)\ntwo\nthree\nfour\nfive\nsix\nseven (other)\n", ReadFile(repo, "notes.txt"));
        Assert.Equal(["M  notes.txt", "?? newfile.txt"], await PorcelainAsync(repo));
        Assert.Equal(CheckoutBlockedFiles.NewFileUntracked, ReadFile(repo, "newfile.txt"));
        Assert.Empty(await repo.StashListAsync());
    }

    [Fact]
    public async Task Pop_keeping_the_index_conflicts_on_a_clashing_branch_and_keeps_the_stash()
    {
        // 2.31's conflict case on the scenario as built: an unstaged change of line 1, and a branch that changes it too.
        using var repo = await CheckoutBlockedAsync();
        var repository = await OpenAsync(repo);
        var stash = await repository.StashAsync("VisualCommit: before checking out feature/clash", includeUntracked: false, cancellationToken: TestCancelled);
        await repo.GitAsync("switch", "--quiet", "feature/clash");

        Assert.Equal(ApplyResult.Conflicts, await repository.ApplyStashAsync(stash!, StashApplyMode.PopKeepingIndex, cancellationToken: TestCancelled));

        Assert.Equal(["notes.txt"], await UnmergedAsync(repo));
        Assert.Equal(
            [new StashListEntry("stash@{0}", stash!, "On main: VisualCommit: before checking out feature/clash")],
            await repo.StashListAsync());
        Assert.Equal(CheckoutBlockedFiles.NewFileUntracked, ReadFile(repo, "newfile.txt"));
    }

    [Fact]
    public async Task When_the_index_part_does_not_apply_a_plain_pop_follows()
    {
        // The staged change of line 1 does not apply to an index whose line 3 differs (git apply
        // needs its context), while the merge of the two changes is clean.
        using var repo = await NotesRepoAsync(("feature/near", "one\ntwo\nthree (near)\nfour\nfive\nsix\nseven\n"));
        repo.WriteFile("notes.txt", CheckoutBlockedFiles.NotesLocal);
        await repo.GitAsync("add", "notes.txt");
        var repository = await OpenAsync(repo);
        var stash = await repository.StashAsync("Before near", includeUntracked: false, cancellationToken: TestCancelled);
        await repo.GitAsync("switch", "--quiet", "feature/near");

        Assert.Equal(ApplyResult.Applied, await repository.ApplyStashAsync(stash!, StashApplyMode.PopKeepingIndex, cancellationToken: TestCancelled));

        Assert.Equal("one (local)\ntwo\nthree (near)\nfour\nfive\nsix\nseven\n", ReadFile(repo, "notes.txt"));

        // Unstaged: the plain pop does not restore the index.
        Assert.Equal([" M notes.txt"], await PorcelainAsync(repo));
        Assert.Empty(await repo.StashListAsync());
    }

    [Fact]
    public async Task When_the_index_part_does_not_apply_and_the_plain_pop_conflicts_the_stash_is_kept()
    {
        using var repo = await NotesRepoAsync(("feature/clash", CheckoutBlockedFiles.NotesClash));
        repo.WriteFile("notes.txt", CheckoutBlockedFiles.NotesLocal);
        await repo.GitAsync("add", "notes.txt");
        var repository = await OpenAsync(repo);
        var stash = await repository.StashAsync("Before clash", includeUntracked: false, cancellationToken: TestCancelled);
        await repo.GitAsync("switch", "--quiet", "feature/clash");

        Assert.Equal(ApplyResult.Conflicts, await repository.ApplyStashAsync(stash!, StashApplyMode.PopKeepingIndex, cancellationToken: TestCancelled));

        Assert.Equal(["notes.txt"], await UnmergedAsync(repo));
        Assert.Equal([stash], (await repo.StashListAsync()).Select(entry => entry.Sha));
    }

    // ---- Drop (2.26) ----------------------------------------------------------------------------

    [Fact]
    public async Task Drop_removes_the_entry_and_its_backup_brings_it_back_with_its_untracked_files()
    {
        using var repo = await OperationsAsync();
        repo.WriteFile("scratch.txt", "scratch\n");
        var repository = await OpenAsync(repo);
        var stash = await repository.StashAsync("Scratch", includeUntracked: true, cancellationToken: TestCancelled);
        var experiment = (await repo.StashListAsync())[1].Sha;

        // The backup 2.26 saves before the drop.
        const string Backup = "refs/visualcommit/backup/stash-20260101T120000000Z";
        await repo.GitAsync("update-ref", Backup, stash!, string.Empty);

        await repository.DropStashAsync(stash!, TestCancelled);

        Assert.Equal([experiment], (await repo.StashListAsync()).Select(entry => entry.Sha));
        Assert.Equal([new RefEntry(Backup, stash!)], await repo.BackupRefsAsync());

        // The stash commit keeps its index and untracked parents: git stash store brings it back.
        await repo.GitAsync("stash", "store", "--message", "Restored", stash!);
        Assert.Equal(ApplyResult.Applied, await repository.ApplyStashAsync(stash!, StashApplyMode.Pop, cancellationToken: TestCancelled));
        Assert.Equal("scratch\n", ReadFile(repo, "scratch.txt"));
        Assert.Equal([experiment], (await repo.StashListAsync()).Select(entry => entry.Sha));
    }

    [Fact]
    public async Task Drop_finds_the_stash_by_its_commit()
    {
        using var repo = await OperationsAsync();
        var experiment = (await repo.StashListAsync())[0].Sha;
        repo.WriteFile("src/core.txt", "core\nlater\n");
        var later = await repo.StashAsync("Later");
        var repository = await OpenAsync(repo);

        await repository.DropStashAsync(experiment, TestCancelled);

        Assert.Equal([later], (await repo.StashListAsync()).Select(entry => entry.Sha));
    }

    // ---- Tags (2.27, 2.28) ----------------------------------------------------------------------

    [Fact]
    public async Task An_annotated_tag_keeps_a_message_that_starts_with_a_hash()
    {
        using var repo = await OperationsAsync();
        var alphaTwo = await repo.RefTargetAsync("refs/heads/feature/alpha");
        var repository = await OpenAsync(repo);

        await repository.CreateTagAsync("v1.2", alphaTwo!, "#42 hotfix", TestCancelled);
        await repository.CreateTagAsync("v1.3", alphaTwo!, "\n#42 hotfix release   \n\n\n# not a comment\n\n", TestCancelled);

        Assert.Equal("tag", await ObjectTypeAsync(repo, "refs/tags/v1.2"));
        Assert.Equal(alphaTwo, await PeeledAsync(repo, "refs/tags/v1.2"));
        Assert.EndsWith("\n\n#42 hotfix\n", await CatTagAsync(repo, "refs/tags/v1.2"), StringComparison.Ordinal);
        Assert.Contains($"\ntagger {TempRepo.AuthorName} <{TempRepo.AuthorEmail}> ", await CatTagAsync(repo, "refs/tags/v1.2"), StringComparison.Ordinal);

        // Only white space is cleaned up: no comment lines are taken out.
        Assert.EndsWith("\n\n#42 hotfix release\n\n# not a comment\n", await CatTagAsync(repo, "refs/tags/v1.3"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tag_without_a_message_is_lightweight()
    {
        using var repo = await OperationsAsync();
        var alphaOne = (await repo.GitAsync("rev-parse", "refs/heads/feature/alpha~1")).StandardOutput.Trim();
        var repository = await OpenAsync(repo);

        await repository.CreateTagAsync("light", alphaOne, null, TestCancelled);
        await repository.CreateTagAsync("blank", alphaOne, "  \n", TestCancelled);

        Assert.Equal(alphaOne, await repo.RefTargetAsync("refs/tags/light"));
        Assert.Equal(alphaOne, await repo.RefTargetAsync("refs/tags/blank"));
        Assert.Equal("commit", await ObjectTypeAsync(repo, "refs/tags/light"));
    }

    [Fact]
    public async Task A_tag_is_made_at_the_commit_a_full_ref_names_when_a_branch_and_a_tag_share_a_name()
    {
        using var repo = await EdgeCasesAsync();
        var alphaWork = await repo.RefTargetAsync("refs/heads/feature/alpha");
        var mainWork = await repo.RefTargetAsync("refs/tags/feature/alpha");
        Assert.NotEqual(alphaWork, mainWork);
        var repository = await OpenAsync(repo);

        await repository.CreateTagAsync("on-branch", "refs/heads/feature/alpha", null, TestCancelled);
        await repository.CreateTagAsync("on-tag", "refs/tags/feature/alpha", "On the tag", TestCancelled);

        Assert.Equal(alphaWork, await repo.RefTargetAsync("refs/tags/on-branch"));
        Assert.Equal(mainWork, await PeeledAsync(repo, "refs/tags/on-tag"));
    }

    [Fact]
    public async Task A_tag_named_like_a_branch_leaves_the_branch_alone()
    {
        using var repo = await OperationsAsync();
        var ff = await repo.RefTargetAsync("refs/heads/feature/ff");
        var addCore = (await repo.GitAsync("rev-parse", "refs/tags/v1.0^{commit}")).StandardOutput.Trim();
        var repository = await OpenAsync(repo);

        await repository.CreateTagAsync("feature/ff", addCore, null, TestCancelled);

        Assert.Equal(addCore, await repo.RefTargetAsync("refs/tags/feature/ff"));
        Assert.Equal(ff, await repo.RefTargetAsync("refs/heads/feature/ff"));
    }

    [Fact]
    public async Task A_tag_of_an_annotated_tag_points_at_its_commit()
    {
        using var repo = await OperationsAsync();
        var addCore = (await repo.GitAsync("rev-parse", "refs/tags/v1.0^{commit}")).StandardOutput.Trim();
        var repository = await OpenAsync(repo);

        await repository.CreateTagAsync("again", "refs/tags/v1.0", null, TestCancelled);

        Assert.Equal(addCore, await repo.RefTargetAsync("refs/tags/again"));
    }

    [Fact]
    public async Task Creating_a_tag_that_exists_fails_and_leaves_it()
    {
        using var repo = await OperationsAsync();
        var v11 = await repo.RefTargetAsync("refs/tags/v1.1");
        var alphaTwo = await repo.RefTargetAsync("refs/heads/feature/alpha");
        var repository = await OpenAsync(repo);

        await Assert.ThrowsAsync<GitException>(() => repository.CreateTagAsync("v1.1", alphaTwo!, null, TestCancelled));
        await Assert.ThrowsAsync<GitException>(() => repository.CreateTagAsync("v1.1", alphaTwo!, "Again", TestCancelled));

        Assert.Equal(v11, await repo.RefTargetAsync("refs/tags/v1.1"));
    }

    [Fact]
    public async Task Deleting_tags_keeps_what_their_backups_hold()
    {
        using var repo = await OperationsAsync();
        var v10 = await repo.RefTargetAsync("refs/tags/v1.0");
        var v11 = await repo.RefTargetAsync("refs/tags/v1.1");
        var repository = await OpenAsync(repo);

        // The backups 2.28 saves before each delete: the tag object itself.
        await repo.GitAsync("update-ref", "refs/visualcommit/backup/tag-20260101T120000000Z/v1.0", "refs/tags/v1.0", string.Empty);
        await repository.DeleteTagAsync("v1.0", TestCancelled);
        await repository.DeleteTagAsync("v1.1", TestCancelled);

        Assert.Null(await repo.RefTargetAsync("refs/tags/v1.0"));
        Assert.Null(await repo.RefTargetAsync("refs/tags/v1.1"));
        Assert.Equal(v11, await repo.RefTargetAsync("refs/heads/main"));
        Assert.Equal([new RefEntry("refs/visualcommit/backup/tag-20260101T120000000Z/v1.0", v10!)], await repo.BackupRefsAsync());
        Assert.EndsWith("\n\nVersion 1.0\n", await CatTagAsync(repo, "refs/visualcommit/backup/tag-20260101T120000000Z/v1.0"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deleting_a_tag_named_like_a_branch_leaves_the_branch()
    {
        using var repo = await EdgeCasesAsync();
        var alphaWork = await repo.RefTargetAsync("refs/heads/feature/alpha");
        var repository = await OpenAsync(repo);

        await repository.DeleteTagAsync("feature/alpha", TestCancelled);

        Assert.Null(await repo.RefTargetAsync("refs/tags/feature/alpha"));
        Assert.Equal(alphaWork, await repo.RefTargetAsync("refs/heads/feature/alpha"));
    }

    [Fact]
    public async Task Deleting_a_missing_tag_fails()
    {
        using var repo = await OperationsAsync();
        var repository = await OpenAsync(repo);

        await Assert.ThrowsAsync<GitException>(() => repository.DeleteTagAsync("v9.9", TestCancelled));
    }

    // ---- Helpers --------------------------------------------------------------------------------

    /// <summary>
    /// A repository with notes.txt as <see cref="CheckoutBlockedFiles.Notes"/> on main
    /// ("Add notes") and one branch from it per entry of <paramref name="branches"/>, each
    /// committing its own notes.txt; HEAD on a clean main.
    /// </summary>
    private static async Task<TempRepo> NotesRepoAsync(params (string Branch, string Notes)[] branches)
    {
        var repo = await TempRepo.CreateAsync("notes");
        try
        {
            await repo.CommitFileAsync("notes.txt", CheckoutBlockedFiles.Notes, "Add notes");
            foreach (var (branch, notes) in branches)
            {
                await repo.GitAsync("switch", "--quiet", "--create", branch, "main");
                await repo.CommitFileAsync("notes.txt", notes, "Change on " + branch);
            }

            await repo.GitAsync("switch", "--quiet", "main");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static string ReadFile(TempRepo repo, string relativePath) => File.ReadAllText(Path.Combine(repo.Path, relativePath));

    private static async Task<IReadOnlyList<string>> PorcelainAsync(TempRepo repo) =>
        Lines((await repo.GitAsync("status", "--porcelain", "--untracked-files=all")).StandardOutput);

    private static async Task<IReadOnlyList<string>> UnmergedAsync(TempRepo repo) =>
        Lines((await repo.GitAsync("diff", "--name-only", "--diff-filter=U")).StandardOutput);

    private static async Task<string> ObjectTypeAsync(TempRepo repo, string fullRef) =>
        (await repo.GitAsync("cat-file", "-t", fullRef)).StandardOutput.Trim();

    private static async Task<string> PeeledAsync(TempRepo repo, string fullRef) =>
        (await repo.GitAsync("rev-parse", fullRef + "^{commit}")).StandardOutput.Trim();

    private static async Task<string> CatTagAsync(TempRepo repo, string fullRef) =>
        (await repo.GitAsync("cat-file", "tag", fullRef)).StandardOutput;

    private static string[] Lines(string output) => output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
