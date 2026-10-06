using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>Pins down what each scenario repo contains. Visual checks describe their expected results in these terms.</summary>
public class ScenarioTests
{
    [Fact]
    public async Task Linear_has_three_commits_on_main_and_a_clean_working_tree()
    {
        using var repo = await Scenarios.LinearAsync();

        var subjects = (await repo.GitAsync("log", "--format=%s")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(["Describe the project", "Add greeting", "Add README"], subjects);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal("refs/heads/main", (await repo.GitAsync("for-each-ref", "--format=%(refname)")).StandardOutput.Trim());
        Assert.Empty((await repo.GitAsync("status", "--porcelain")).StandardOutput);
        Assert.Equal("# Sample\n\nA repository for tests.\n", File.ReadAllText(Path.Combine(repo.Path, "README.md")));
        Assert.Equal("Hello\n", File.ReadAllText(Path.Combine(repo.Path, "src", "greeting.txt")));
    }

    [Fact]
    public async Task Linear_has_the_same_commit_ids_on_every_run()
    {
        using var repo = await Scenarios.LinearAsync();

        Assert.Equal("7270ded9d9f138de90d34b5dcf330b37c44fc043", await repo.HeadAsync());
    }
}
