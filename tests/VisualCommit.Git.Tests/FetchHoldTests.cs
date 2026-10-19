using System.Diagnostics;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.ScenarioTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The fetch hold (<see cref="FetchHold"/>, docs/phase-6-design.md 17.2) with real git on the
/// workspace's file remotes, from a data folder of the test's under the temp folder: the line
/// reads back, held fetches wait until released, a fetch after the release goes through, a second
/// run under one hold waits for its own, and a repository without a remote is neither held nor
/// holdable.
/// </summary>
public class FetchHoldTests
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Two_held_fetches_wait_until_released_and_the_line_reads_back()
    {
        using var data = new TempDirectory("data");
        using var copy = await Scenarios.WorkspaceAsync();
        using var hold = FetchHold.Install(data.Path);
        var api = copy.Repo("api");
        var docs = copy.Repo("docs");
        hold.HoldIn(copy.PathOf("api"), copy.PathOf("docs"));

        Assert.Equal(Path.Combine(data.Path, "harness", "hold-upload-pack.sh"), hold.ScriptPath);
        Assert.Equal(FetchHold.Script, File.ReadAllText(hold.ScriptPath));
        var line = (await docs.GitAsync("config", "--get", "remote.origin.uploadpack")).StandardOutput.Trim();
        Assert.Equal("sh '" + hold.ScriptPath.Replace('\\', '/') + "'", line);
        Assert.Equal(hold.UploadPackCommand, line);
        Assert.DoesNotContain('\\', line);

        // The second header merges with the first: the remote is unchanged.
        Assert.Equal(["origin\t../remotes/docs.git (fetch)", "origin\t../remotes/docs.git (push)"], Lines(await docs.GitAsync("remote", "-v")));

        // Two fetches held at once, api's with nothing new.
        var apiFetch = FetchAsync(api, TestCancelled);
        var docsFetch = FetchAsync(docs, TestCancelled);
        await hold.WaitForHeldAsync(2, StepTimeout);
        Assert.Equal(2, hold.Held);
        Assert.Equal(
            ["held-api.git", "held-docs.git"],
            Directory.EnumerateFiles(hold.Folder, "held-*").Select(file => Path.GetFileNameWithoutExtension(file)).Order(StringComparer.Ordinal));
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestCancelled);
        Assert.False(apiFetch.IsCompleted, "api's fetch ended while it was held.");
        Assert.False(docsFetch.IsCompleted, "docs' fetch ended while it was held.");

        hold.Release();
        var apiResult = await apiFetch.WaitAsync(StepTimeout, TestCancelled);
        var docsResult = await docsFetch.WaitAsync(StepTimeout, TestCancelled);

        Assert.Equal(0, apiResult.ExitCode);
        Assert.Equal(0, docsResult.ExitCode);
        Assert.Equal("245b729", (await docs.RefTargetAsync("refs/remotes/origin/main"))![..7]);
        Assert.Equal(0, hold.Held);
        Assert.Empty(Directory.EnumerateFiles(hold.Folder, "held-*"));
    }

    [Fact]
    public async Task A_fetch_after_the_release_is_not_held_and_makes_no_marker()
    {
        using var data = new TempDirectory("data");
        using var copy = await Scenarios.WorkspaceAsync();
        using var hold = FetchHold.Install(data.Path);
        var mobile = copy.Repo("mobile");
        hold.HoldIn(copy.PathOf("mobile"));
        hold.Release();

        var result = await FetchAsync(mobile, TestCancelled).WaitAsync(StepTimeout, TestCancelled);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Counting objects: 100% (4/4)", result.StandardError, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(hold.Folder, "held-*"));
        Assert.Equal(0, hold.Held);
    }

    [Fact]
    public async Task A_second_run_under_one_hold_waits_for_its_own_fetch()
    {
        using var data = new TempDirectory("data");
        using var copy = await Scenarios.WorkspaceAsync();
        using var hold = FetchHold.Install(data.Path);
        var docs = copy.Repo("docs");
        hold.HoldIn(copy.PathOf("docs"));

        // A first run whose held fetch a cancel kills: its marker stays (on Windows the whole tree
        // stops; elsewhere its shell waits on until the release).
        using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestCancelled))
        {
            var killed = FetchAsync(docs, cancel.Token);
            await hold.WaitForHeldAsync(1, StepTimeout);
            await cancel.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => killed.WaitAsync(StepTimeout, TestCancelled));
        }

        Assert.Equal(1, hold.Held);

        // Armed again: the first run's marker is the baseline, so a wait for one held fetch waits
        // for the second run's.
        hold.Arm();
        Assert.Equal(0, hold.Held);
        var second = FetchAsync(docs, TestCancelled);
        await hold.WaitForHeldAsync(1, StepTimeout);
        Assert.Equal(1, hold.Held);
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestCancelled);
        Assert.False(second.IsCompleted, "The second run's fetch ended while it was held.");

        hold.Release();
        Assert.Equal(0, (await second.WaitAsync(StepTimeout, TestCancelled)).ExitCode);
        Assert.Equal("245b729", (await docs.RefTargetAsync("refs/remotes/origin/main"))![..7]);
    }

    [Fact]
    public async Task Beside_held_repositories_one_without_a_remote_has_none_and_is_not_held()
    {
        using var data = new TempDirectory("data");
        using var copy = await Scenarios.WorkspaceAsync();
        using var hold = FetchHold.Install(data.Path);
        hold.HoldIn(copy.PathOf("docs"), copy.PathOf("design-system"));
        var notes = copy.Repo("notes");
        var configBefore = File.ReadAllText(Path.Combine(notes.Path, ".git", "config"));

        Assert.Throws<InvalidOperationException>(() => hold.HoldIn(copy.PathOf("api"), copy.PathOf("notes")));
        Assert.Equal(configBefore, File.ReadAllText(Path.Combine(notes.Path, ".git", "config")));
        Assert.Equal(1, (await copy.Repo("api").GitAllowingFailureAsync("config", "--get", "remote.origin.uploadpack")).ExitCode);

        Assert.Empty((await notes.GitAsync("remote")).StandardOutput.Trim());
        // Its fetch has nothing to fetch from and nothing to hold: it ends at once, whatever its
        // exit code (Git 2.36 ends with 0 and prints nothing), and makes no marker.
        var clock = Stopwatch.StartNew();
        var fetch = await notes.GitAllowingFailureAsync("fetch", "--progress");
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"notes' fetch took {clock.Elapsed}: {fetch.StandardError}");
        Assert.DoesNotContain("held", fetch.StandardError, StringComparison.Ordinal);
        Assert.Equal(0, hold.Held);
        Assert.Empty(Directory.EnumerateFiles(hold.Folder, "held-*"));
    }

    private static Task<GitResult> FetchAsync(TempRepo repo, CancellationToken cancellationToken)
    {
        var command = new GitCommand("fetch", "--progress", "origin") { WorkingDirectory = repo.Path, Environment = repo.Environment };
        return repo.Runner.RunAsync(command, cancellationToken);
    }
}
