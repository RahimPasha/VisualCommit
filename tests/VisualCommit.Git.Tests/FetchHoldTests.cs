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
/// run under one hold waits for its own, a fetch that saw its release does not give up when the
/// hold is armed again before it goes on, a hold that gave up fails its waits and its release but
/// not its disposal, so that a step that failed first keeps its own exception, and a repository
/// without a remote is neither held nor holdable.
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
        Assert.Empty(hold.GaveUp());
    }

    [Fact]
    public async Task A_held_fetch_that_gives_up_leaves_a_marker_on_which_the_hold_fails()
    {
        // 17.2: a fetch whose rounds run out goes on unheld; the hold says so instead of passing
        // a test whose steps between hold and release took too long. Two rounds: a second.
        using var data = new TempDirectory("data");
        using var copy = await Scenarios.WorkspaceAsync();
        var hold = FetchHold.Install(data.Path, rounds: 2);
        var api = copy.Repo("api");
        hold.HoldIn(copy.PathOf("api"));
        Assert.Equal(FetchHold.ScriptFor(2), File.ReadAllText(hold.ScriptPath));
        Assert.Equal(TimeSpan.FromSeconds(1), hold.HeldFor);

        var result = await FetchAsync(api, TestCancelled).WaitAsync(StepTimeout, TestCancelled);

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith(FetchHold.GaveUpPrefix + "api.git.", Assert.Single(hold.GaveUp()), StringComparison.Ordinal);
        Assert.Equal(0, hold.Held);
        var waited = await Assert.ThrowsAsync<InvalidOperationException>(() => hold.WaitForHeldAsync(1, StepTimeout));
        Assert.StartsWith("The hold gave up: the steps between hold and release took over 1 s (gave-up-api.git.", waited.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(hold.Release);

        // Disposing releases and never fails: a check's own end says whether the hold held
        // (Release, or no GaveUp), so that a failing step's exception is never replaced by this one.
        File.Delete(Path.Combine(hold.Folder, FetchHold.ReleaseName));
        hold.Dispose();
        Assert.True(File.Exists(Path.Combine(hold.Folder, FetchHold.ReleaseName)));
    }

    [Fact]
    public async Task A_step_that_fails_under_a_hold_that_gave_up_is_reported_with_its_own_exception()
    {
        // CI's run 44969394972: a step failed while a held fetch ran out of its rounds, and the
        // using's Dispose replaced the step's exception with the hold's give-up. Two rounds: a second.
        using var data = new TempDirectory("data");
        using var copy = await Scenarios.WorkspaceAsync();
        var hold = FetchHold.Install(data.Path, rounds: 2);
        hold.HoldIn(copy.PathOf("api"));
        Assert.Equal(0, (await FetchAsync(copy.Repo("api"), TestCancelled).WaitAsync(StepTimeout, TestCancelled)).ExitCode);
        Assert.Single(hold.GaveUp());

        async Task StepThatFailsAsync()
        {
            using var held = hold;
            await Task.Yield();
            throw new TimeoutException("the step that failed first");
        }

        var thrown = await Assert.ThrowsAnyAsync<Exception>(StepThatFailsAsync);
        Assert.IsType<TimeoutException>(thrown);
        Assert.Equal("the step that failed first", thrown.Message);
        Assert.True(File.Exists(Path.Combine(hold.Folder, FetchHold.ReleaseName)), "Disposing did not release.");

        // A check that ends normally still fails on the hold that held nothing, at its own end.
        Assert.Throws<InvalidOperationException>(hold.Release);
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

    [Fact]
    public async Task A_fetch_that_saw_the_release_does_not_give_up_when_the_hold_is_armed_again_before_it_goes_on()
    {
        // CI's run 44969394972 (K3 and K8 in a Linux container): on macOS and Linux a cancel kills
        // git alone (D36), so a cancelled fetch's script waits on and sees the release that follows;
        // Arm, for the next hold, may delete release in the moment between the script's wait and its
        // going on. A stand-in for rm, first on the script's PATH, holds the script in just that
        // moment (its rm of the marker) until the test has armed again, on every system.
        using var data = new TempDirectory("data");
        using var standIn = new TempDirectory("stand-in");
        using var copy = await Scenarios.WorkspaceAsync();
        var hold = FetchHold.Install(data.Path);
        var api = copy.Repo("api");
        var launcher = WriteRmStandIn(standIn, hold);
        var go = standIn.Combine("rm-go");
        try
        {
            var command = new GitCommand("-c", "remote.origin.uploadpack=sh " + ShellQuote(launcher), "fetch", "--progress", "origin")
            {
                WorkingDirectory = api.Path,
                Environment = api.Environment,
            };
            var fetch = api.Runner.RunAsync(command, TestCancelled);
            await hold.WaitForHeldAsync(1, StepTimeout);

            hold.Release();
            await WaitForFileAsync(standIn.Combine("rm-entered"), "the script's rm of its marker after it saw the release");
            hold.Arm();
            File.WriteAllText(go, string.Empty);
            var result = await fetch.WaitAsync(StepTimeout, TestCancelled);

            Assert.True(File.Exists(standIn.Combine("rm-let-go")), "The stand-in for rm stopped waiting before the test armed again.");
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(hold.GaveUp());
            Assert.Empty(Directory.EnumerateFiles(hold.Folder, FetchHold.MarkerPrefix + "*"));
            Assert.Equal(0, hold.Held);
        }
        finally
        {
            // Whatever failed: the stand-in and the script go on and end.
            File.WriteAllText(go, string.Empty);
            File.WriteAllText(Path.Combine(hold.Folder, FetchHold.ReleaseName), string.Empty);
        }
    }

    private static Task<GitResult> FetchAsync(TempRepo repo, CancellationToken cancellationToken)
    {
        var command = new GitCommand("fetch", "--progress", "origin") { WorkingDirectory = repo.Path, Environment = repo.Environment };
        return repo.Runner.RunAsync(command, cancellationToken);
    }

    /// <summary>
    /// Writes into <paramref name="folder"/> a stand-in for <c>rm</c> and a launcher, and returns the
    /// launcher's path: git runs the launcher as the remote's upload-pack, and it runs the hold's
    /// script as git would (<c>sh '&lt;script&gt;' "$1"</c>) with the folder first on its
    /// <c>PATH</c>, after git's own folders, which Git for Windows puts first. The stand-in makes
    /// <c>rm-entered</c>, waits for <c>rm-go</c> (at most 1,200 rounds of <c>sleep 0.05</c>), makes
    /// <c>rm-let-go</c> if it came, and then removes what it was given with the system's <c>rm</c>.
    /// </summary>
    private static string WriteRmStandIn(TempDirectory folder, FetchHold hold)
    {
        var launcher = folder.Combine("launch.sh");
        WriteScript(
            launcher,
            "#!/bin/sh\n" +
            "# FetchHoldTests: the hold's script as git runs it, with this folder first on its PATH.\n" +
            "PATH=\"$(cd \"${0%/*}\" && pwd):$PATH\"\n" +
            "export PATH\n" +
            "exec sh " + ShellQuote(hold.ScriptPath) + " \"$1\"\n");
        WriteScript(
            folder.Combine("rm"),
            "#!/bin/sh\n" +
            "# FetchHoldTests: rm, held until the file rm-go is beside it.\n" +
            "here=\"${0%/*}\"\n" +
            ": > \"$here/rm-entered\"\n" +
            "i=0\n" +
            "while [ ! -e \"$here/rm-go\" ] && [ \"$i\" -lt 1200 ]; do\n" +
            "  sleep 0.05\n" +
            "  i=$((i + 1))\n" +
            "done\n" +
            "if [ -e \"$here/rm-go\" ]; then\n" +
            "  : > \"$here/rm-let-go\"\n" +
            "fi\n" +
            "command -p rm \"$@\"\n");
        return launcher;
    }

    private static void WriteScript(string path, string text)
    {
        File.WriteAllText(path, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>A path for sh: forward slashes, in single quotes, as <see cref="FetchHold.UploadPackCommand"/> writes it.</summary>
    private static string ShellQuote(string path) => "'" + path.Replace('\\', '/').Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static async Task WaitForFileAsync(string path, string what)
    {
        var clock = Stopwatch.StartNew();
        while (!File.Exists(path))
        {
            if (clock.Elapsed >= StepTimeout)
            {
                throw new TimeoutException($"Timed out after {StepTimeout.TotalSeconds:0} s waiting for {what} ({path}).");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), TestCancelled);
        }
    }
}
