using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The conflicted paths of the status (docs/phase-4-design.md 0.4): every <c>u</c> line of
/// <c>git status --porcelain=v2 -z</c> is a <see cref="ConflictEntry"/> with its kind, modes and
/// stage ids as git prints them, beside the unstaged conflicted file phase 3 already listed.
/// Conflicts made by real merges where every git makes the same kind, and by unmerged index
/// entries written with <c>git update-index --index-info</c> for the kinds whose merge differs
/// between git versions (a rename/rename).
/// </summary>
public class ConflictStatusTests
{
    private const string Missing = "0000000000000000000000000000000000000000";

    [Fact]
    public async Task A_merge_gives_each_conflicted_path_its_kind_modes_and_stage_ids()
    {
        using var repo = await TempRepo.CreateAsync();
        repo.WriteFile("both.txt", "base\n").WriteFile("ours-deletes.txt", "base\n").WriteFile("theirs-deletes.txt", "base\n");
        await repo.CommitAsync("Base");
        await repo.CreateBranchAsync("theirs");
        repo.WriteFile("both.txt", "ours\n").WriteFile("added.txt", "ours added\n").WriteFile("theirs-deletes.txt", "ours\n").DeleteFile("ours-deletes.txt");
        await repo.CommitAsync("Ours");
        await repo.CheckoutAsync("theirs");
        repo.WriteFile("both.txt", "theirs\n").WriteFile("added.txt", "theirs added\n").WriteFile("ours-deletes.txt", "theirs\n").DeleteFile("theirs-deletes.txt");
        await repo.CommitAsync("Theirs");
        await repo.CheckoutAsync("main");
        var merge = await repo.GitAllowingFailureAsync("merge", "--no-edit", "theirs");
        Assert.Equal(1, merge.ExitCode);
        var repository = await OpenAsync(repo);

        var status = await repository.ReadStatusAsync(TestCancelled);

        Assert.Equal(
            [
                new ConflictEntry("added.txt", ConflictKind.BothAdded, false, "000000", "100644", "100644", Missing, await BlobAsync(repo, "HEAD:added.txt"), await BlobAsync(repo, "theirs:added.txt")),
                new ConflictEntry("both.txt", ConflictKind.BothModified, false, "100644", "100644", "100644", await BlobAsync(repo, "HEAD~1:both.txt"), await BlobAsync(repo, "HEAD:both.txt"), await BlobAsync(repo, "theirs:both.txt")),
                new ConflictEntry("ours-deletes.txt", ConflictKind.DeletedByUs, false, "100644", "000000", "100644", await BlobAsync(repo, "HEAD~1:ours-deletes.txt"), Missing, await BlobAsync(repo, "theirs:ours-deletes.txt")),
                new ConflictEntry("theirs-deletes.txt", ConflictKind.DeletedByThem, false, "100644", "100644", "000000", await BlobAsync(repo, "HEAD~1:theirs-deletes.txt"), await BlobAsync(repo, "HEAD:theirs-deletes.txt"), Missing),
            ],
            status.Conflicts);

        // Phase 3's unstaged conflicted entries stay as they were.
        Assert.Equal(
            ["added.txt", "both.txt", "ours-deletes.txt", "theirs-deletes.txt"],
            status.Unstaged.Where(file => file.Kind == FileChangeKind.Conflicted).Select(file => file.Path));
        Assert.All(status.Conflicts, conflict => Assert.Same(conflict, status.FindConflict(conflict.Path)));
        Assert.False(status.Conflicts[0].HasBase);
        Assert.True(status.Conflicts[1].HasBase && status.Conflicts[1].HasOurs && status.Conflicts[1].HasTheirs);
        Assert.False(status.Conflicts[2].HasOurs);
        Assert.False(status.Conflicts[3].HasTheirs);
    }

    [Fact]
    public async Task Added_by_us_added_by_them_and_both_deleted_are_read_from_their_stages()
    {
        using var repo = await TempRepo.CreateAsync();
        repo.WriteFile("kept.txt", "kept\n");
        await repo.CommitAsync("Base");
        var ours = await HashAsync(repo, "ours\n");
        var theirs = await HashAsync(repo, "theirs\n");
        var gone = await HashAsync(repo, "base\n");

        // The unmerged entries a rename/rename leaves (0.4): the old path at stage 1 only, each
        // new path at one side's stage only.
        await repo.GitWithInputAsync(
            $"100644 {gone} 1\tgone.txt\n100644 {ours} 2\tmoved-ours.txt\n100644 {theirs} 3\tmoved-theirs.txt\n",
            "update-index", "--index-info");
        repo.WriteFile("moved-ours.txt", "ours\n").WriteFile("moved-theirs.txt", "theirs\n");
        var repository = await OpenAsync(repo);

        var status = await repository.ReadStatusAsync(TestCancelled);

        Assert.Equal(
            [
                new ConflictEntry("gone.txt", ConflictKind.BothDeleted, false, "100644", "000000", "000000", gone, Missing, Missing),
                new ConflictEntry("moved-ours.txt", ConflictKind.AddedByUs, false, "000000", "100644", "000000", Missing, ours, Missing),
                new ConflictEntry("moved-theirs.txt", ConflictKind.AddedByThem, false, "000000", "000000", "100644", Missing, Missing, theirs),
            ],
            status.Conflicts);
        Assert.Equal(3, status.Unstaged.Count(file => file.Kind == FileChangeKind.Conflicted));
    }

    [Fact]
    public async Task A_conflicted_submodule_is_marked_and_keeps_its_gitlink_modes()
    {
        using var repo = await TempRepo.CreateAsync();
        var first = await repo.CommitFileAsync("a.txt", "a\n", "First");
        var second = await repo.CommitFileAsync("a.txt", "b\n", "Second");
        var third = await repo.CommitFileAsync("a.txt", "c\n", "Third");
        await repo.GitWithInputAsync(
            $"160000 {first} 1\tmodule\n160000 {second} 2\tmodule\n160000 {third} 3\tmodule\n",
            "update-index", "--index-info");
        var repository = await OpenAsync(repo);

        var status = await repository.ReadStatusAsync(TestCancelled);

        var conflict = Assert.Single(status.Conflicts);
        Assert.Equal(new ConflictEntry("module", ConflictKind.BothModified, true, "160000", "160000", "160000", first, second, third), conflict);
    }

    [Fact]
    public void The_parser_reads_every_field_of_a_u_line_and_skips_an_unknown_pair()
    {
        const string Base = "1111111111111111111111111111111111111111";
        const string Ours = "2222222222222222222222222222222222222222";
        const string Theirs = "3333333333333333333333333333333333333333";
        var output =
            $"u UU N... 100644 100755 120000 100644 {Base} {Ours} {Theirs} a dir/with space.txt\0"
            + $"u UU SC.. 160000 160000 160000 160000 {Base} {Ours} {Theirs} sub\0"
            + $"u XY N... 100644 100644 100644 100644 {Base} {Ours} {Theirs} odd.txt\0";

        var status = GitRepository.ParseStatus(output);

        Assert.Equal(
            [
                new ConflictEntry("a dir/with space.txt", ConflictKind.BothModified, false, "100644", "100755", "120000", Base, Ours, Theirs),
                new ConflictEntry("sub", ConflictKind.BothModified, true, "160000", "160000", "160000", Base, Ours, Theirs),
            ],
            status.Conflicts);
        Assert.Equal(["a dir/with space.txt", "odd.txt", "sub"], status.Unstaged.Select(file => file.Path));
    }

    [Fact]
    public void Two_statuses_with_other_conflict_stages_are_not_the_same()
    {
        var entry = new ConflictEntry("f.txt", ConflictKind.BothModified, false, "100644", "100644", "100644", "a", "b", "c");
        var files = new[] { new ChangedFile("f.txt", FileChangeKind.Conflicted) };
        var one = new WorkingTreeStatus([], files, new HashSet<string>()) { Conflicts = [entry] };
        var same = new WorkingTreeStatus([], files, new HashSet<string>()) { Conflicts = [entry with { }] };
        var other = new WorkingTreeStatus([], files, new HashSet<string>()) { Conflicts = [entry with { OursId = "d" }] };

        Assert.True(one.SameAs(same));
        Assert.False(one.SameAs(other));
        Assert.False(one.SameAs(new WorkingTreeStatus([], files, new HashSet<string>())));
    }

    private static Task<string> BlobAsync(TempRepo repo, string revision) => RevAsync(repo, revision);

    private static async Task<string> HashAsync(TempRepo repo, string content) =>
        (await repo.GitWithInputAsync(content, "hash-object", "-w", "--stdin")).StandardOutput.Trim();
}
