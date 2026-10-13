using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitRepository.CompareAsync"/> (docs/phase-5-design.md 6.3, 0.3, D177): two trees
/// (two-dot), or the changes on one side since it left the other (three-dot, through the merge
/// base), with the commits only on each side; unrelated histories, a criss-cross and git's rename
/// limit.
/// </summary>
public class CompareTests(CompareTests.Branches branches) : IClassFixture<CompareTests.Branches>
{
    /// <summary>
    /// <c>main</c> and <c>feature</c> from one base: main edits the top of <c>a.txt</c> and renames
    /// <c>b.txt</c> with an edit; feature edits <c>dir/c.txt</c>, adds <c>f.txt</c> and edits the
    /// bottom of <c>a.txt</c>. An orphan branch <c>other</c> shares no history with them.
    /// </summary>
    public sealed class Branches : IAsyncLifetime
    {
        private const string Lines = "1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n";

        public TempRepo Repo { get; private set; } = null!;

        public GitRepository Repository { get; private set; } = null!;

        public string Base { get; private set; } = string.Empty;

        public string Main { get; private set; } = string.Empty;

        public string Feature { get; private set; } = string.Empty;

        public string Other { get; private set; } = string.Empty;

        public async ValueTask InitializeAsync()
        {
            Repo = await TempRepo.CreateAsync("compare");
            Repo.WriteFile("a.txt", Lines).WriteFile("b.txt", Lines + "11\n12\n").WriteFile("dir/c.txt", "c\n");
            Base = await Repo.CommitAsync("Base");

            await Repo.CreateBranchAsync("feature");
            await Repo.CommitFileAsync("a.txt", "one\n" + Lines[2..], "Main edits the top of a");
            Repo.DeleteFile("b.txt").WriteFile("b2.txt", Lines + "11\ntwelve\n");
            Main = await Repo.CommitAsync("Main renames b");

            await Repo.CheckoutAsync("feature");
            await Repo.CommitFileAsync("dir/c.txt", "c, edited\n", "Feature edits c");
            await Repo.CommitFileAsync("f.txt", "f\n", "Feature adds f");
            Feature = await Repo.CommitFileAsync("a.txt", Lines[..^3] + "ten\n", "Feature edits the bottom of a");
            await Repo.AnnotatedTagAsync("v1", "Release", Feature);

            await Repo.GitAsync("checkout", "--quiet", "--orphan", "other");
            await Repo.GitAsync("rm", "--quiet", "-r", "-f", ".");
            Other = await Repo.CommitFileAsync("o.txt", "o\n", "Unrelated");
            await Repo.CheckoutAsync("main");

            Repository = await OpenAsync(Repo);
        }

        public ValueTask DisposeAsync()
        {
            Repo?.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static List<(string Path, FileChangeKind Kind, string? OldPath)> Files(CompareResult result) =>
        [.. result.Files.Select(file => (file.Path, file.Kind, file.OldPath)).OrderBy(file => file.Path, StringComparer.Ordinal)];

    [Fact]
    public async Task Two_dot_compares_the_trees_and_shows_the_other_sides_changes_reversed()
    {
        var result = await branches.Repository.CompareAsync("refs/heads/main", "refs/heads/feature", threeDot: false, countCommits: false, TestCancelled);

        Assert.Equal((branches.Main, branches.Feature, CompareBase.Trees, branches.Main), (result.From, result.To, result.Base, result.Old));

        // Main's rename of b.txt reads as feature renaming it back.
        Assert.Equal(
            [
                ("a.txt", FileChangeKind.Modified, null),
                ("b.txt", FileChangeKind.Renamed, "b2.txt"),
                ("dir/c.txt", FileChangeKind.Modified, null),
                ("f.txt", FileChangeKind.Added, (string?)null),
            ],
            Files(result));
        Assert.False(result.RenamesSkipped);
        Assert.Null(result.Counts);
    }

    [Fact]
    public async Task Three_dot_shows_the_changes_since_the_merge_base_and_counts_each_side()
    {
        var result = await branches.Repository.CompareAsync("refs/heads/main", "refs/heads/feature", threeDot: true, countCommits: true, TestCancelled);

        Assert.Equal((CompareBase.MergeBase, branches.Base), (result.Base, result.Old));
        Assert.Equal(
            [("a.txt", FileChangeKind.Modified, null), ("dir/c.txt", FileChangeKind.Modified, null), ("f.txt", FileChangeKind.Added, (string?)null)],
            Files(result));

        // Main has two commits feature lacks; feature has three.
        Assert.Equal(new CompareCounts(OnlyOnFrom: 2, OnlyOnTo: 3), result.Counts);
    }

    [Fact]
    public async Task Names_are_resolved_to_commits_once_a_tag_peeled()
    {
        var result = await branches.Repository.CompareAsync(branches.Main, "refs/tags/v1", threeDot: true, countCommits: false, TestCancelled);

        Assert.Equal((branches.Main, branches.Feature), (result.From, result.To));
    }

    [Fact]
    public async Task The_same_commit_on_both_sides_runs_no_git_beyond_the_names()
    {
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), branches.Repo.Path, TestCancelled);
        var before = calls.Snapshot().Count;

        var result = await repository.CompareAsync("refs/tags/v1", "refs/heads/feature", threeDot: true, countCommits: true, TestCancelled);

        Assert.True(result.IsSameCommit);
        Assert.Empty(result.Files);
        Assert.Equal(new CompareCounts(0, 0), result.Counts);
        Assert.Equal(["rev-parse"], calls.Snapshot().Skip(before).Select(call => call.CommandText.Split(' ')[1]));
    }

    [Fact]
    public async Task Unrelated_histories_fall_back_to_all_differences()
    {
        var result = await branches.Repository.CompareAsync("refs/heads/main", "refs/heads/other", threeDot: true, countCommits: true, TestCancelled);

        Assert.Equal((CompareBase.NoCommonHistory, branches.Main), (result.Base, result.Old));
        Assert.Contains(("o.txt", FileChangeKind.Added, (string?)null), Files(result));
        Assert.Contains(("a.txt", FileChangeKind.Deleted, (string?)null), Files(result));

        // Every commit of each history is only on its side.
        Assert.Equal(new CompareCounts(OnlyOnFrom: 3, OnlyOnTo: 1), result.Counts);
    }

    [Fact]
    public async Task A_stash_can_be_a_side()
    {
        using var repo = await TempRepo.CreateAsync("compare-stash");
        var head = await repo.CommitFileAsync("f.txt", "one\n", "Add f");
        repo.WriteFile("f.txt", "two\n");
        var stash = await repo.StashAsync("Work");
        var repository = await OpenAsync(repo);

        var result = await repository.CompareAsync(head, stash, threeDot: false, countCommits: false, TestCancelled);

        Assert.Equal([("f.txt", FileChangeKind.Modified, (string?)null)], Files(result));
    }

    [Fact]
    public async Task A_criss_cross_uses_the_base_git_prints_and_says_there_are_several()
    {
        using var repo = await TempRepo.CreateAsync("compare-criss-cross");
        await repo.CommitFileAsync("o.txt", "o\n", "Origin");
        await repo.CreateBranchAsync("b2");
        var m1 = await repo.CommitFileAsync("m.txt", "m\n", "Main one");
        await repo.CheckoutAsync("b2");
        var x1 = await repo.CommitFileAsync("x.txt", "x\n", "B2 one");
        await repo.GitAsync("merge", "--quiet", "--no-ff", "--message", "B2 merges main one", m1);
        await repo.CheckoutAsync("main");
        await repo.GitAsync("merge", "--quiet", "--no-ff", "--message", "Main merges b2 one", x1);
        await repo.CommitFileAsync("m.txt", "m, later\n", "Main two");
        var repository = await OpenAsync(repo);

        var result = await repository.CompareAsync("refs/heads/main", "refs/heads/b2", threeDot: true, countCommits: false, TestCancelled);

        Assert.Equal(CompareBase.SeveralMergeBases, result.Base);
        Assert.Contains(result.Old, new[] { m1, x1 });
        Assert.Equal((await repo.GitAsync("merge-base", "main", "b2")).StandardOutput.Trim(), result.Old);
    }

    [Fact]
    public async Task Gits_rename_limit_is_noted_when_it_turned_renames_into_removals_and_additions()
    {
        using var repo = await TempRepo.CreateAsync("compare-rename-limit");
        var before = await repo.CommitFileAsync("x.txt", "1\n2\n3\n4\n5\n6\n7\n8\n", "Add x");
        repo.DeleteFile("x.txt").WriteFile("y.txt", "1\n2\n3\n4\n5\n6\n7\n8\n9\n").WriteFile("z.txt", "1\n2\n3\n4\n5\n6\nseven\n8\n");
        var after = await repo.CommitAsync("Split x");
        await repo.ConfigAsync("diff.renameLimit", "1");
        var repository = await OpenAsync(repo);

        var result = await repository.CompareAsync(before, after, threeDot: false, countCommits: false, TestCancelled);

        Assert.True(result.RenamesSkipped);
        Assert.Equal(
            [("x.txt", FileChangeKind.Deleted, null), ("y.txt", FileChangeKind.Added, null), ("z.txt", FileChangeKind.Added, (string?)null)],
            Files(result));
    }

    [Fact]
    public async Task A_name_that_names_nothing_throws_with_gits_message_and_an_option_is_refused()
    {
        var ex = await Assert.ThrowsAsync<GitException>(() => branches.Repository.CompareAsync("refs/heads/main", "refs/heads/nope", threeDot: true, countCommits: true, TestCancelled));
        Assert.Contains("refs/heads/nope", ex.StandardError, StringComparison.Ordinal);

        await Assert.ThrowsAsync<ArgumentException>(() => branches.Repository.CompareAsync("--all", "refs/heads/main", threeDot: false, countCommits: false, TestCancelled));
    }
}
