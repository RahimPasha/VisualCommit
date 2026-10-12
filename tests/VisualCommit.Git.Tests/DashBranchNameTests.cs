using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Branch names that start with "-": git branch refuses to make them, but plumbing and a fetch
/// into refs/heads can, and the app lists them. Git must never take one for an option: "git
/// branch --move -r renamed" renames the current branch, "git branch -f main" resets main.
/// </summary>
public class DashBranchNameTests
{
    private static async Task<TempRepo> RepoWithDashBranchesAsync()
    {
        var repo = await Scenarios.OperationsAsync();
        foreach (var name in new[] { "-r", "-f", "-x", "-" })
        {
            await repo.GitAsync("update-ref", "refs/heads/" + name, "refs/heads/feature/alpha");
        }

        return repo;
    }

    [Fact]
    public async Task Renaming_and_deleting_a_branch_that_starts_with_a_dash_touch_that_branch_alone()
    {
        using var repo = await RepoWithDashBranchesAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var alpha = await RevAsync(repo, "refs/heads/feature/alpha");

        await repository.RenameBranchAsync("-r", "renamed", TestCancelled);
        await repository.RenameBranchAsync("-", "was-dash", TestCancelled);
        await repository.DeleteBranchAsync("-f", TestCancelled);

        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(mainWork, await repo.RefTargetAsync("refs/heads/main"));
        Assert.Equal(alpha, await repo.RefTargetAsync("refs/heads/renamed"));
        Assert.Equal(alpha, await repo.RefTargetAsync("refs/heads/was-dash"));
        Assert.Null(await repo.RefTargetAsync("refs/heads/-r"));
        Assert.Null(await repo.RefTargetAsync("refs/heads/-"));
        Assert.Null(await repo.RefTargetAsync("refs/heads/-f"));
    }

    [Fact]
    public async Task A_branch_that_starts_with_a_dash_is_checked_out_and_used_as_a_start_point_by_name()
    {
        using var repo = await RepoWithDashBranchesAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var alpha = await RevAsync(repo, "refs/heads/feature/alpha");

        await repository.CreateBranchAsync("from-dash", "-x", checkout: false, cancellationToken: TestCancelled);
        Assert.Equal(alpha, await repo.RefTargetAsync("refs/heads/from-dash"));
        Assert.Equal(mainWork, await repo.RefTargetAsync("refs/heads/main"));

        await repository.CheckoutBranchAsync("-x", cancellationToken: TestCancelled);
        Assert.Equal("-x", await repo.CurrentBranchAsync());

        // Switch reads "-" as the branch checked out before, whatever comes first.
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CheckoutBranchAsync("-", cancellationToken: TestCancelled));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.ResetAsync("-x", ResetMode.Hard, cancellationToken: TestCancelled));
        Assert.Equal("-x", await repo.CurrentBranchAsync());
    }

    [Fact]
    public async Task Commands_with_ordinary_names_are_written_as_a_user_would_type_them()
    {
        using var repo = await Scenarios.OperationsAsync();
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);

        await repository.CheckoutBranchAsync("feature/alpha", cancellationToken: TestCancelled);

        Assert.Contains(calls.Snapshot(), call => call.CommandText == "git switch --no-guess feature/alpha");
    }
}
