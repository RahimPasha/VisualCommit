using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// A merge or a cherry-pick that meets a conflict git has seen resolved before, with
/// <c>rerere.enabled</c> and <c>rerere.autoUpdate</c>: git stages the recorded resolution, exits
/// with 1 and leaves the operation in progress with nothing conflicted. That is a stop that waits
/// for its commit or its abort (the banner's), not a failure.
/// </summary>
public class RerereTests
{
    [Fact]
    public async Task A_merge_that_rerere_resolved_stops_and_waits_for_its_commit()
    {
        var (repo, mainTip, _) = await RepoWithRecordedResolutionAsync();
        using var _repo = repo;
        await repo.GitAllowingFailureAsync("merge", "--no-edit", "side");
        await ResolveAndCommitAsync(repo);
        await repo.GitAsync("reset", "--quiet", "--hard", mainTip);
        var repository = await OpenAsync(repo);

        var result = await repository.MergeAsync("refs/heads/side", MergeMode.Default, cancellationToken: TestCancelled);

        Assert.Equal(MergeResult.Conflicts, result);
        Assert.True(GitFileExists(repo, "MERGE_HEAD"));
        Assert.Equal(["M  a.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal("resolved\n", ReadText(repo, "a.txt"));
    }

    [Fact]
    public async Task A_cherry_pick_that_rerere_resolved_stops_and_waits_for_its_commit()
    {
        var (repo, mainTip, sideTip) = await RepoWithRecordedResolutionAsync();
        using var _repo = repo;
        await repo.GitAllowingFailureAsync("cherry-pick", sideTip);
        await ResolveAndCommitAsync(repo);
        await repo.GitAsync("reset", "--quiet", "--hard", mainTip);
        var repository = await OpenAsync(repo);

        var result = await repository.CherryPickAsync(sideTip, cancellationToken: TestCancelled);

        Assert.Equal(ApplyResult.Conflicts, result);
        Assert.True(GitFileExists(repo, "CHERRY_PICK_HEAD"));
        Assert.Equal(["M  a.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
    }

    /// <summary>main and side change a.txt's one line; rerere is on and updates the index.</summary>
    private static async Task<(TempRepo Repo, string MainTip, string SideTip)> RepoWithRecordedResolutionAsync()
    {
        var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "base\n", "Base");
        await repo.CreateBranchAsync("side");
        var mainTip = await repo.CommitFileAsync("a.txt", "main\n", "Main change");
        await repo.CheckoutAsync("side");
        var sideTip = await repo.CommitFileAsync("a.txt", "side\n", "Side change");
        await repo.CheckoutAsync("main");
        await repo.ConfigAsync("rerere.enabled", "true");
        await repo.ConfigAsync("rerere.autoUpdate", "true");
        return (repo, mainTip, sideTip);
    }

    /// <summary>Resolves the conflict in a.txt and commits, which makes rerere record the resolution.</summary>
    private static async Task ResolveAndCommitAsync(TempRepo repo)
    {
        Assert.Equal(["UU a.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        repo.WriteFile("a.txt", "resolved\n");
        await repo.GitAsync("add", "a.txt");
        await repo.GitAsync("commit", "--quiet", "--no-edit");
        Assert.NotEmpty(Directory.GetDirectories(Path.Combine(repo.Path, ".git", "rr-cache")));
    }
}
