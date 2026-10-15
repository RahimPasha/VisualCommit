using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The waiting merge tool of the report's part A (<see cref="Scenarios.ConfigureWaitingMergeToolAsync"/>,
/// AD7, AD12): git runs it on the conflicted file, it says that it started beside the working
/// tree, and it waits until it is released, then fails, leaving the file as it was.
/// </summary>
public class WaitingMergeToolTests
{
    private static CancellationToken TestCancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_tool_waits_until_it_is_released_and_then_fails_leaving_the_conflict()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        await Scenarios.ConfigureWaitingMergeToolAsync(repo);
        var before = await File.ReadAllBytesAsync(Path.Combine(repo.Path, "notes.txt"), TestCancelled);
        Assert.Equal("slow", (await repo.GitAsync("config", "merge.tool")).StandardOutput.Trim());
        Assert.Equal("true", (await repo.GitAsync("config", "mergetool.slow.trustExitCode")).StandardOutput.Trim());
        Assert.False(Scenarios.WaitingMergeToolStarted(repo));

        var tool = repo.GitAllowingFailureAsync("-c", "mergetool.keepBackup=false", "mergetool", "--tool=slow", "--no-prompt", "--", "notes.txt");
        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!Scenarios.WaitingMergeToolStarted(repo))
            {
                if (tool.IsCompleted)
                {
                    Assert.Fail("git mergetool ended before the tool started: " + (await tool).StandardError);
                }

                Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), "The tool did not start within 60 seconds.");
                await Task.Delay(50, TestCancelled);
            }

            await Task.Delay(300, TestCancelled);
            Assert.False(tool.IsCompleted, "The tool ended before it was released.");
        }
        finally
        {
            Scenarios.ReleaseWaitingMergeTool(repo);
        }

        var result = await tool.WaitAsync(TimeSpan.FromSeconds(60), TestCancelled);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(["UU notes.txt"], (await repo.GitAsync("status", "--porcelain")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(repo.Path, "notes.txt"), TestCancelled));
    }
}
