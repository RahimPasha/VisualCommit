using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>Tests of the temporary-repo builder that the other tests rely on.</summary>
public class TempRepoTests
{
    [Fact]
    public async Task A_new_repo_is_empty_on_the_requested_branch()
    {
        using var repo = await TempRepo.CreateAsync("fresh", initialBranch: "trunk");

        Assert.True(Directory.Exists(Path.Combine(repo.Path, ".git")));
        Assert.Equal("trunk", await repo.CurrentBranchAsync());
        Assert.Empty((await repo.GitAsync("status", "--porcelain")).StandardOutput);
        Assert.Empty((await repo.GitAsync("for-each-ref")).StandardOutput);
    }

    [Fact]
    public async Task The_repo_sees_no_configuration_from_the_machine()
    {
        using var repo = await TempRepo.CreateAsync();

        var origins = (await repo.GitAsync("config", "--list", "--show-scope")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t')[0])
            .Distinct()
            .ToList();

        // Only the repo's own settings: nothing from the system or the user's global file.
        Assert.Equal(["local"], origins);
    }

    [Fact]
    public async Task Commits_carry_the_fixed_author_and_a_clock_that_advances_a_minute_each()
    {
        using var repo = await TempRepo.CreateAsync();
        var first = await repo.CommitFileAsync("a.txt", "a\n", "First");
        var second = await repo.CommitFileAsync("b.txt", "b\n", "Second");

        // Times as seconds since 1970 and the zone separately: how git prints an ISO date in
        // UTC ("+00:00" or "Z") depends on its version.
        var log = (await repo.GitAsync("log", "--date=format:%z", "--format=%H|%an|%ae|%cn|%at|%ct|%ad|%s")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var noon = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        Assert.Equal(
            [
                $"{second}|Test Author|author@example.com|Test Author|{noon + 60}|{noon + 60}|+0000|Second",
                $"{first}|Test Author|author@example.com|Test Author|{noon}|{noon}|+0000|First",
            ],
            log);
    }

    [Fact]
    public async Task The_same_steps_give_the_same_commit_ids_every_time()
    {
        using var one = await TempRepo.CreateAsync("one");
        using var two = await TempRepo.CreateAsync("two");

        var first = await one.CommitFileAsync("file.txt", "content\n", "Add file");
        var second = await two.CommitFileAsync("file.txt", "content\n", "Add file");

        Assert.Equal(first, second);

        // Pinned, so a change in how repos are built, or a platform that builds them
        // differently, is noticed: scenario screenshots show these ids.
        Assert.Equal("bda26680b4e5c90e10b08d06a41751ffab903dd7", first);
    }

    [Fact]
    public async Task Files_are_stored_exactly_as_written()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("mixed.txt", "unix\nwindows\r\nend", "Add mixed line endings");

        File.Delete(Path.Combine(repo.Path, "mixed.txt"));
        await repo.GitAsync("checkout", "--", "mixed.txt");

        Assert.Equal("unix\nwindows\r\nend", File.ReadAllText(Path.Combine(repo.Path, "mixed.txt")));
    }

    [Fact]
    public async Task Branches_tags_and_merges_build_the_expected_history()
    {
        using var repo = await TempRepo.CreateAsync();
        var root = await repo.CommitFileAsync("main.txt", "main\n", "Start");
        await repo.CreateBranchAsync("feature/one");
        await repo.CheckoutAsync("feature/one");
        var feature = await repo.CommitFileAsync("feature.txt", "feature\n", "Add feature");
        await repo.CheckoutAsync("main");
        var mainTip = await repo.CommitFileAsync("main.txt", "main changed\n", "Change main");
        await repo.TagAsync("v1.0");
        var merge = await repo.MergeAsync("feature/one");

        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(merge, await repo.HeadAsync());
        Assert.Equal($"{merge} {mainTip} {feature}", (await repo.GitAsync("rev-list", "--parents", "-1", "HEAD")).StandardOutput.Trim());
        Assert.Equal(mainTip, (await repo.GitAsync("rev-parse", "v1.0")).StandardOutput.Trim());
        Assert.Equal(root, (await repo.GitAsync("merge-base", mainTip, feature)).StandardOutput.Trim());
        Assert.Equal("Merge branch 'feature/one'", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
        Assert.True(File.Exists(Path.Combine(repo.Path, "feature.txt")));
    }

    [Fact]
    public async Task A_history_can_be_built_from_a_stream_on_standard_input()
    {
        using var repo = await TempRepo.CreateAsync();
        const string stream =
            "commit refs/heads/main\n" +
            "committer Stream Author <stream@example.com> 1767268800 +0000\n" +
            "data 6\nFirst\n" +
            "M 100644 inline a.txt\ndata 2\na\n\n" +
            "commit refs/heads/main\n" +
            "committer Stream Author <stream@example.com> 1767268860 +0000\n" +
            "data 7\nSecond\n" +
            "M 100644 inline b.txt\ndata 2\nb\n\n";

        await repo.GitWithInputAsync(stream, "fast-import", "--quiet");

        var subjects = (await repo.GitAsync("log", "--format=%s", "main")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["Second", "First"], subjects);
        Assert.Equal("a\nb\n", (await repo.GitAsync("show", "main:a.txt")).StandardOutput + (await repo.GitAsync("show", "main:b.txt")).StandardOutput);
    }

    [Fact]
    public async Task A_failing_git_call_throws_with_gits_own_message()
    {
        using var repo = await TempRepo.CreateAsync();

        var exception = await Assert.ThrowsAsync<Core.Git.GitException>(() => repo.CheckoutAsync("no-such-branch"));

        Assert.Contains("no-such-branch", exception.Message);
    }

    [Fact]
    public async Task Disposing_removes_the_repo_from_disk()
    {
        string path;
        using (var repo = await TempRepo.CreateAsync())
        {
            await repo.CommitFileAsync("a.txt", "a\n", "First");
            path = repo.Path;
            Assert.True(Directory.Exists(path));
        }

        Assert.False(Directory.Exists(path));
    }
}
