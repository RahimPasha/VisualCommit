using System.Collections.Concurrent;
using System.Diagnostics;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>Tests of <see cref="GitRunner"/> against the real git executable.</summary>
public class GitRunnerTests
{
    private static readonly string GitPath =
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found.");

    private static CancellationToken TestCancelled => TestContext.Current.CancellationToken;

    /// <summary>
    /// A git command that runs a shell script, through a one-off alias. Git brings its own shell
    /// on Windows, so this gives the tests slow and chatty commands on every platform, and a
    /// child process below git, as real operations have.
    /// </summary>
    private static GitCommand Script(string script, Action<string>? onOutputLine = null, Action<string>? onErrorLine = null) =>
        new("-c", $"alias.script=!{script}", "script") { OnOutputLine = onOutputLine, OnErrorLine = onErrorLine };

    [Fact]
    public async Task Runs_git_and_returns_its_output_and_exit_code()
    {
        var runner = new GitRunner(GitPath);

        var result = await runner.RunAsync(new GitCommand("--version"), TestCancelled);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("git version ", result.StandardOutput);
        Assert.Empty(result.StandardError);
        Assert.True(result.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task A_failing_command_is_returned_with_gits_own_error_text_not_thrown()
    {
        var runner = new GitRunner(GitPath);
        var command = new GitCommand("definitely-not-a-git-command");

        var result = await runner.RunAsync(command, TestCancelled);

        Assert.False(result.Succeeded);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("is not a git command", result.StandardError);

        var exception = Assert.Throws<GitException>(() => result.EnsureSuccess(command));
        Assert.Equal(result.ExitCode, exception.ExitCode);
        Assert.Contains("git definitely-not-a-git-command", exception.Message);
        Assert.Contains("is not a git command", exception.Message);
    }

    [Fact]
    public async Task Runs_in_the_working_directory_of_the_command()
    {
        using var repo = await TempRepo.CreateAsync();
        var runner = new GitRunner(GitPath);

        var result = await runner.RunAsync(
            new GitCommand("rev-parse", "--is-inside-work-tree") { WorkingDirectory = repo.Path, Environment = repo.Environment },
            TestCancelled);
        var outside = await runner.RunAsync(
            new GitCommand("rev-parse", "--is-inside-work-tree") { WorkingDirectory = Path.GetPathRoot(repo.Path), Environment = repo.Environment },
            TestCancelled);

        Assert.Equal("true", result.StandardOutput.Trim());
        Assert.False(outside.Succeeded);
    }

    [Fact]
    public async Task Writes_standard_input_to_git()
    {
        var runner = new GitRunner(GitPath);

        var result = await runner.RunAsync(
            new GitCommand("hash-object", "--stdin") { StandardInput = "hello\n" },
            TestCancelled);

        // The well-known blob id of "hello\n".
        Assert.Equal("ce013625030ba8dba906f756967f9e9ca394464a", result.StandardOutput.Trim());
    }

    [Fact]
    public async Task A_command_that_waits_for_input_gets_an_empty_input_instead_of_hanging()
    {
        var runner = new GitRunner(GitPath);

        var result = await runner.RunAsync(new GitCommand("hash-object", "--stdin"), TestCancelled);

        // The blob id of empty content.
        Assert.Equal("e69de29bb2d1d6434b8b29ae775ad8c2e48c5391", result.StandardOutput.Trim());
    }

    [Fact]
    public async Task Text_outside_ascii_survives_the_round_trip()
    {
        using var repo = await TempRepo.CreateAsync();
        const string message = "Füge Größe hinzu — 日本語 ✓";
        await repo.WriteFile("überblick.txt", "x\n").CommitAsync(message);

        var log = await repo.GitAsync("log", "-1", "--format=%s");
        var files = await repo.GitAsync("-c", "core.quotepath=false", "ls-files");

        Assert.Equal(message, log.StandardOutput.Trim());
        Assert.Equal("überblick.txt", files.StandardOutput.Trim());
    }

    [Fact]
    public async Task Environment_variables_of_the_command_reach_git_and_null_removes_one()
    {
        var runner = new GitRunner(GitPath);
        var environment = new Dictionary<string, string?>
        {
            ["GIT_AUTHOR_NAME"] = "Env Author",
            ["GIT_AUTHOR_EMAIL"] = "env@example.com",
            ["GIT_TERMINAL_PROMPT"] = null,
        };

        var ident = await runner.RunAsync(new GitCommand("var", "GIT_AUTHOR_IDENT") { Environment = environment }, TestCancelled);
        var prompt = await runner.RunAsync(Script("echo prompt=${GIT_TERMINAL_PROMPT-unset}"), TestCancelled);
        var promptRemoved = await runner.RunAsync(
            new GitCommand("-c", "alias.script=!echo prompt=${GIT_TERMINAL_PROMPT-unset}", "script") { Environment = environment },
            TestCancelled);

        Assert.StartsWith("Env Author <env@example.com>", ident.StandardOutput);
        Assert.Equal("prompt=0", prompt.StandardOutput.Trim());
        Assert.Equal("prompt=unset", promptRemoved.StandardOutput.Trim());
    }

    [Fact]
    public async Task Output_lines_are_streamed_while_git_is_still_running()
    {
        var runner = new GitRunner(GitPath);
        var lines = new ConcurrentQueue<string>();
        var firstLine = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = Script(
            "echo first; sleep 2; echo second",
            onOutputLine: line =>
            {
                lines.Enqueue(line);
                firstLine.TrySetResult();
            });

        var run = runner.RunAsync(command, TestCancelled);
        var firstToFinish = await Task.WhenAny(firstLine.Task, run);

        Assert.Same(firstLine.Task, firstToFinish);
        Assert.False(run.IsCompleted, "The first line must arrive before git exits.");

        var result = await run;
        Assert.Equal(["first", "second"], lines);
        Assert.Empty(result.StandardOutput);
        Assert.True(result.Duration >= TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public async Task Output_lines_have_no_line_endings_even_with_windows_ones()
    {
        var runner = new GitRunner(GitPath);
        var lines = new ConcurrentQueue<string>();

        await runner.RunAsync(Script(@"printf 'one\r\ntwo\n\nlast'", onOutputLine: lines.Enqueue), TestCancelled);

        Assert.Equal(["one", "two", "", "last"], lines);
    }

    [Fact]
    public async Task Error_lines_are_streamed_and_carriage_returns_separate_progress_updates()
    {
        var runner = new GitRunner(GitPath);
        var lines = new ConcurrentQueue<string>();

        var result = await runner.RunAsync(
            Script(@"printf 'Receiving 10%%\rReceiving 50%%\rReceiving 100%%, done.\r\nResolving\n' >&2", onErrorLine: lines.Enqueue),
            TestCancelled);

        Assert.Equal(["Receiving 10%", "Receiving 50%", "Receiving 100%, done.", "Resolving"], lines);

        // Standard error is kept in full as well, for error messages.
        Assert.Contains("Receiving 100%, done.", result.StandardError);
        Assert.Contains("Resolving", result.StandardError);
    }

    [Fact]
    public async Task Output_handlers_never_run_on_the_callers_thread()
    {
        var runner = new GitRunner(GitPath);
        var handlerThreads = new ConcurrentQueue<(Thread Thread, bool IsBackground)>();
        void Record(string line) => handlerThreads.Enqueue((Thread.CurrentThread, Thread.CurrentThread.IsBackground));
        var command = Script("echo out; echo err >&2", onOutputLine: Record, onErrorLine: Record);

        // A thread of its own stands for the UI thread: the thread pool never runs work on it, so
        // a handler can only run there if the call ran it before returning (D59).
        var started = new TaskCompletionSource<Task<GitResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var caller = new Thread(() => started.SetResult(runner.RunAsync(command, TestCancelled)));
        caller.Start();

        var result = await await started.Task;

        Assert.True(result.Succeeded);
        Assert.Equal(2, handlerThreads.Count);
        Assert.All(handlerThreads, handler =>
        {
            Assert.NotSame(caller, handler.Thread);
            Assert.True(handler.IsBackground);
        });
    }

    [Fact]
    public async Task Cancelling_stops_git_and_returns_quickly()
    {
        var calls = new GitCallLog();
        var runner = new GitRunner(GitPath, calls);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancelled);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = runner.RunAsync(
            Script("echo started; sleep 30", onOutputLine: _ => started.TrySetResult()),
            cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30), TestCancelled);

        var stopwatch = Stopwatch.StartNew();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        // The sleeping child of git's shell holds the output pipes open. On Windows it is
        // stopped together with git; elsewhere only git is stopped and the call must still
        // return, long before the child's 30 seconds are over.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"Cancelling took {stopwatch.Elapsed}.");

        var call = Assert.Single(calls.Snapshot());
        Assert.Equal(GitCallOutcome.Cancelled, call.Outcome);
        Assert.Null(call.ExitCode);
        Assert.Contains("started", call.StandardOutput);
    }

    [Fact]
    public async Task A_background_process_left_behind_by_git_does_not_hold_up_the_call()
    {
        var runner = new GitRunner(GitPath);
        var stopwatch = Stopwatch.StartNew();

        // As a hook that starts a background job does: git exits, the job keeps the output open.
        var result = await runner.RunAsync(Script("sleep 20 & echo done"), TestCancelled);

        Assert.True(result.Succeeded);
        Assert.Equal("done", result.StandardOutput.Trim());
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(12), $"The call took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task A_slow_output_handler_near_the_end_still_gets_every_line()
    {
        // Git prints its last lines and exits while the handler is still busy with an earlier
        // one, for longer than a process left behind is waited for. A busy handler is progress,
        // so the call waits for it and delivers the rest before it returns.
        const int Count = 2000;
        var runner = new GitRunner(GitPath);
        var lines = new ConcurrentQueue<string>();

        var result = await runner.RunAsync(
            Script(
                $"i=1; while [ $i -le {Count} ]; do echo \"line $i\"; i=$((i+1)); done",
                onOutputLine: line =>
                {
                    lines.Enqueue(line);
                    if (line == $"line {Count - 10}")
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(3));
                    }
                }),
            TestCancelled);

        Assert.True(result.Succeeded);
        Assert.Equal(Enumerable.Range(1, Count).Select(i => $"line {i}"), lines);
    }

    [Fact]
    public async Task Cancelling_while_a_handler_holds_the_call_after_git_exited_returns_quickly()
    {
        var runner = new GitRunner(GitPath);
        using var release = new ManualResetEventSlim();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancelled);
        var inHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var run = runner.RunAsync(
                Script(
                    "echo only",
                    onOutputLine: _ =>
                    {
                        inHandler.TrySetResult();
                        release.Wait(TimeSpan.FromSeconds(60));
                    }),
                cancellation.Token);
            await inHandler.Task.WaitAsync(TimeSpan.FromSeconds(30), TestCancelled);

            // Git is long gone by now; the busy handler still holds the call.
            await Task.Delay(TimeSpan.FromSeconds(3), TestCancelled);
            Assert.False(run.IsCompleted, "A handler that is still at work must hold the call.");

            var stopwatch = Stopwatch.StartNew();
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Cancelling took {stopwatch.Elapsed}.");
        }
        finally
        {
            release.Set();
        }
    }

    [Theory]
    [InlineData("https://user:ghp_secret123@github.com/team/repo.git", "https://***@github.com/team/repo.git")]
    [InlineData("https://ghp_secret123@dev.azure.com/org/project/_git/repo", "https://***@dev.azure.com/org/project/_git/repo")]
    [InlineData("http://user:p@ss@127.0.0.1:8080/x.git", "http://***@127.0.0.1:8080/x.git")]
    [InlineData("ssh://user:secret@host:22/x.git", "ssh://***@host:22/x.git")]
    [InlineData("remote.origin.url=https://user:secret@host/x.git", "remote.origin.url=https://***@host/x.git")]
    [InlineData("https://a:secret@one/x https://b:secret@two/y", "\"https://***@one/x https://***@two/y\"")]
    public void The_display_text_hides_the_credentials_of_a_url_and_git_still_gets_them(string argument, string shown)
    {
        var command = new GitCommand("clone", "--", argument, "copy");

        Assert.Equal($"git clone -- {shown} copy", command.DisplayText);
        Assert.DoesNotContain("secret", command.DisplayText);
        Assert.Equal(argument, command.Arguments[2]);
    }

    [Theory]
    [InlineData("https://github.com/team/repo.git")]
    [InlineData("git@github.com:team/repo.git")]
    [InlineData("https://host/people/user@example.com/repo.git")]
    [InlineData("https://host/x.git?user=a@b")]
    [InlineData("file:///C:/repos/x")]
    [InlineData("--format=%an <%ae>")]
    public void Arguments_without_credentials_are_shown_as_given(string argument)
    {
        var command = new GitCommand("clone", argument);

        Assert.Equal("git clone " + (argument.Contains(' ') ? $"\"{argument}\"" : argument), command.DisplayText);
    }

    [Fact]
    public async Task A_token_that_is_already_cancelled_does_not_start_git()
    {
        var calls = new GitCallLog();
        var runner = new GitRunner(GitPath, calls);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(new GitCommand("--version"), cancellation.Token));

        Assert.Empty(calls.Snapshot());
    }

    [Fact]
    public async Task A_missing_executable_gives_a_git_exception_and_a_record()
    {
        var calls = new GitCallLog();
        var runner = new GitRunner(Path.Combine(Path.GetTempPath(), "no-such-folder", "git-missing.exe"), calls);

        var exception = await Assert.ThrowsAsync<GitException>(
            () => runner.RunAsync(new GitCommand("status"), TestCancelled));

        Assert.Null(exception.ExitCode);
        Assert.Contains("git status", exception.Message);
        Assert.Contains("could not be started", exception.Message);

        var call = Assert.Single(calls.Snapshot());
        Assert.Equal(GitCallOutcome.FailedToStart, call.Outcome);
        Assert.False(call.Succeeded);
    }

    [Fact]
    public async Task Every_call_is_recorded_with_command_folder_result_and_timing()
    {
        using var repo = await TempRepo.CreateAsync();
        var calls = new GitCallLog();
        var log = new ListLog();
        var runner = new GitRunner(GitPath, calls, log);
        var before = DateTimeOffset.Now;

        await runner.RunAsync(new GitCommand("status", "--porcelain") { WorkingDirectory = repo.Path, Environment = repo.Environment }, TestCancelled);
        await runner.RunAsync(new GitCommand("rev-parse", "--verify", "no such ref") { WorkingDirectory = repo.Path, Environment = repo.Environment }, TestCancelled);

        var records = calls.Snapshot();
        Assert.Equal(2, records.Count);

        Assert.Equal("git status --porcelain", records[0].CommandText);
        Assert.Equal(repo.Path, records[0].WorkingDirectory);
        Assert.Equal(GitCallOutcome.Completed, records[0].Outcome);
        Assert.Equal(0, records[0].ExitCode);
        Assert.True(records[0].Succeeded);
        Assert.True(records[0].Duration > TimeSpan.Zero);
        Assert.InRange(records[0].StartedAt, before.AddSeconds(-1), DateTimeOffset.Now.AddSeconds(1));

        Assert.Equal("git rev-parse --verify \"no such ref\"", records[1].CommandText);
        Assert.NotEqual(0, records[1].ExitCode);
        Assert.False(records[1].Succeeded);
        Assert.Contains("fatal", records[1].StandardError);

        // Each call also goes to the app log: successes at debug level, failures at info level.
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Debug && entry.Message.StartsWith("git status --porcelain in ", StringComparison.Ordinal) && entry.Message.Contains("exit 0"));
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("rev-parse"));
    }

    [Fact]
    public async Task A_record_keeps_only_the_start_of_long_output_while_the_result_keeps_it_all()
    {
        using var repo = await TempRepo.CreateAsync();
        var content = string.Concat(Enumerable.Repeat("0123456789abcdef0123456789abcde\n", 2000)); // 64,000 characters
        await repo.CommitFileAsync("big.txt", content, "Add a big file");
        var calls = new GitCallLog();
        var runner = new GitRunner(GitPath, calls);

        var result = await runner.RunAsync(new GitCommand("show", "HEAD:big.txt") { WorkingDirectory = repo.Path, Environment = repo.Environment }, TestCancelled);

        Assert.Equal(content, result.StandardOutput);
        var recorded = Assert.Single(calls.Snapshot()).StandardOutput;
        Assert.Equal(GitCallRecord.MaxOutputLength + GitCallRecord.TruncationMarker.Length, recorded.Length);
        Assert.StartsWith(content[..GitCallRecord.MaxOutputLength], recorded);
        Assert.EndsWith(GitCallRecord.TruncationMarker, recorded);
    }

    [Fact]
    public async Task Calls_can_run_at_the_same_time()
    {
        var calls = new GitCallLog();
        var runner = new GitRunner(GitPath, calls);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => runner.RunAsync(new GitCommand("--version"), TestCancelled)));

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(8, calls.Snapshot().Count);
    }

    [Fact]
    public async Task An_output_handler_that_throws_stops_git_and_passes_the_exception_on()
    {
        var calls = new GitCallLog();
        var runner = new GitRunner(GitPath, calls);
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(
                Script("echo first; sleep 30", onOutputLine: _ => throw new InvalidOperationException("handler failed")),
                TestCancelled));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"Stopping took {stopwatch.Elapsed}.");
        Assert.Single(calls.Snapshot());
    }

    [Fact]
    public async Task A_failing_call_log_listener_does_not_fail_the_git_call()
    {
        var calls = new GitCallLog();
        calls.Added += (_, _) => throw new InvalidOperationException("listener failed");
        var log = new ListLog();
        var runner = new GitRunner(GitPath, calls, log);

        var result = await runner.RunAsync(new GitCommand("--version"), TestCancelled);

        Assert.True(result.Succeeded);
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Error && entry.Exception is InvalidOperationException);
    }

    private sealed class ListLog : IAppLog
    {
        public ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

        public void Write(LogLevel level, string message, Exception? exception = null) =>
            Entries.Enqueue((level, message, exception));
    }
}
