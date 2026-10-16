using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.ConflictOperationTests;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The external merge tool (docs/phase-4-design.md 0.4, 11.9, D149): <c>git -c
/// mergetool.keepBackup=false mergetool --tool=&lt;t&gt; --no-prompt -- &lt;p&gt;</c> with fake tools
/// (a repository-local <c>mergetool.&lt;t&gt;.cmd</c> with <c>trustExitCode</c>), and the copies git
/// leaves beside the file (<c>f_BACKUP_&lt;pid&gt;.txt</c>, <c>_BASE_</c>, <c>_LOCAL_</c>,
/// <c>_REMOTE_</c>), which are removed after every run that made them: one that succeeded, failed,
/// could not start the tool, or was cancelled. A class of its own beside
/// <see cref="ConflictOperationTests"/>, so that the two run side by side.
/// </summary>
public class MergeToolTests
{
    private const string Conflicted = "dir/f.txt";

    [Fact]
    public async Task The_merge_tool_is_read_from_gits_configuration()
    {
        using var repo = await ConflictedAsync();
        var repository = await OpenAsync(repo);

        Assert.Null(await repository.ReadMergeToolAsync(cancellationToken: TestCancelled));
        await repo.ConfigAsync("merge.tool", "fake");
        Assert.Equal("fake", await repository.ReadMergeToolAsync(cancellationToken: TestCancelled));
    }

    [Fact]
    public async Task A_tool_that_resolves_the_file_leaves_it_staged_and_nothing_beside_it()
    {
        using var repo = await ConflictedAsync();
        await repo.ConfigAsync("mergetool.fake.cmd", "printf 'resolved\\n' > \"$MERGED\"");
        await repo.ConfigAsync("mergetool.fake.trustExitCode", "true");
        var repository = await OpenAsync(repo);

        var result = await repository.RunMergeToolAsync(Conflicted, "fake", cancellationToken: TestCancelled);

        Assert.True(result.Staged);
        Assert.Equal("resolved\n", ReadText(repo, Conflicted));
        Assert.Equal(["M  dir/f.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(["f.txt"], FilesIn(repo));
    }

    [Fact]
    public async Task A_tool_that_fails_leaves_the_file_unresolved_and_the_copies_it_made_are_removed()
    {
        using var repo = await ConflictedAsync();
        await repo.ConfigAsync("mergetool.bad.cmd", "printf 'half\\n' > \"$MERGED\"; exit 3");
        await repo.ConfigAsync("mergetool.bad.trustExitCode", "true");

        // The user keeps git's copies: git leaves them after a failure. One of the same name made
        // before the run is the user's own and stays.
        await repo.ConfigAsync("mergetool.keepTemporaries", "true");
        repo.WriteFile("dir/f_BASE_99.txt", "the user's\n");
        var markers = ReadText(repo, Conflicted);
        var repository = await OpenAsync(repo);

        var result = await repository.RunMergeToolAsync(Conflicted, "bad", cancellationToken: TestCancelled);

        Assert.False(result.Staged);

        // Git puts the file back as it was before the tool ran.
        Assert.Equal(markers, ReadText(repo, Conflicted));
        Assert.Equal(["UU dir/f.txt", "?? dir/f_BASE_99.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(["f.txt", "f_BASE_99.txt"], FilesIn(repo));
    }

    [Fact]
    public async Task A_tool_git_cannot_find_fails_with_gits_text_and_its_copies_are_removed()
    {
        using var repo = await ConflictedAsync();
        await repo.ConfigAsync("mergetool.kdiff3.path", "/no/such/folder/kdiff3");
        var repository = await OpenAsync(repo);

        var failure = await Assert.ThrowsAsync<GitException>(() => repository.RunMergeToolAsync(Conflicted, "kdiff3", cancellationToken: TestCancelled));

        // Git 2.36: "The merge tool kdiff3 is not available as '/no/such/folder/kdiff3'".
        Assert.Contains("kdiff3 is not available as", failure.StandardError, StringComparison.Ordinal);
        Assert.Equal(["f.txt"], FilesIn(repo));
        Assert.Equal(["UU dir/f.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
    }

    [Fact]
    public async Task A_tool_git_does_not_know_fails_and_changes_nothing()
    {
        using var repo = await ConflictedAsync();
        var markers = ReadText(repo, Conflicted);
        var repository = await OpenAsync(repo);

        var failure = await Assert.ThrowsAsync<GitException>(() => repository.RunMergeToolAsync(Conflicted, "no-such-tool", cancellationToken: TestCancelled));

        Assert.Equal(1, failure.ExitCode);
        Assert.Equal(markers, ReadText(repo, Conflicted));
        Assert.Equal(["f.txt"], FilesIn(repo));
        Assert.Equal(["UU dir/f.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
    }

    [Fact]
    public async Task A_cancelled_tool_is_stopped_the_file_is_put_back_as_it_was_and_the_copies_git_made_are_removed()
    {
        using var repo = await ConflictedAsync();
        var started = Sibling(repo, "tool-started");
        var release = Sibling(repo, "tool-release");

        // The user's own partial resolution, made before the tool ran.
        repo.WriteFile(Conflicted, "line 1\nmy partial resolution\nline 3\n");

        // The tool runs in the working tree's top folder, next to which the test's files lie. It
        // saves part of its work, waits to be stopped (or released, at the latest after a minute)
        // and then fails.
        await repo.ConfigAsync("mergetool.slow.cmd", "printf 'tool-partial\\n' > \"$MERGED\"; touch ../tool-started; i=0; while [ ! -f ../tool-release ] && [ $i -lt 600 ]; do sleep 0.1; i=$((i+1)); done; exit 1");
        await repo.ConfigAsync("mergetool.slow.trustExitCode", "true");
        var repository = await OpenAsync(repo);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestCancelled);

        try
        {
            var run = repository.RunMergeToolAsync(Conflicted, "slow", cancellationToken: cancel.Token);
            await WaitForFileAsync(started, run);

            // While the tool runs, git's copies are beside the file, which holds the tool's work.
            Assert.Equal(4, FilesIn(repo).Count(name => name.StartsWith("f_", StringComparison.Ordinal)));
            Assert.Equal("tool-partial\n", ReadText(repo, Conflicted));
            await cancel.CancelAsync();

            // 14.2: the file is as it was before the tool (git's backup put back), and only it is left.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.Equal("line 1\nmy partial resolution\nline 3\n", ReadText(repo, Conflicted));
            Assert.Equal(["f.txt"], FilesIn(repo));
            Assert.Equal(["UU dir/f.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        }
        finally
        {
            // Where only git was killed (macOS, Linux), the tool's shell ends now.
            File.WriteAllText(release, string.Empty);
        }
    }

    [Fact]
    public async Task A_path_without_a_conflict_runs_no_tool()
    {
        using var repo = await ConflictedAsync();
        await repo.ConfigAsync("mergetool.fake.cmd", "printf 'resolved\\n' > \"$MERGED\"");
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);

        await Assert.ThrowsAsync<IOException>(() => repository.RunMergeToolAsync("keep.txt", "fake", cancellationToken: TestCancelled));

        Assert.Equal("keep\n", ReadText(repo, "keep.txt"));
        Assert.DoesNotContain(calls.Snapshot(), call => call.CommandText.Contains("mergetool", StringComparison.Ordinal));
    }

    /// <summary><c>dir/f.txt</c> changed on both sides and merged with its conflict.</summary>
    private static Task<TempRepo> ConflictedAsync() =>
        MergeAsync(
            r => r.WriteFile(Conflicted, "line 1\nline 2\nline 3\n"),
            r => r.WriteFile(Conflicted, "line 1\nline 2 ours\nline 3\n"),
            r => r.WriteFile(Conflicted, "line 1\nline 2 theirs\nline 3\n"));

    /// <summary>The names of the files beside the conflicted one, in order.</summary>
    private static string[] FilesIn(TempRepo repo) =>
        [.. Directory.EnumerateFiles(Path.Combine(repo.Path, "dir")).Select(Path.GetFileName).Order(StringComparer.Ordinal)!];

    /// <summary>Waits until <paramref name="path"/> exists; fails when <paramref name="run"/> ends first or after <see cref="Scenarios.MergeToolTimeout"/>.</summary>
    private static async Task WaitForFileAsync(string path, Task run)
    {
        var deadline = DateTime.UtcNow + Scenarios.MergeToolTimeout;
        while (!File.Exists(path))
        {
            if (run.IsCompleted)
            {
                await run;
                Assert.Fail("The merge tool ended before it started.");
            }

            Assert.True(DateTime.UtcNow < deadline, $"The merge tool did not start within {Scenarios.MergeToolTimeout.TotalSeconds:F0} seconds.");
            await Task.Delay(50, TestCancelled);
        }
    }
}
