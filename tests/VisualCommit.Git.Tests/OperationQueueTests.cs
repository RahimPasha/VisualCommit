using System.Collections.Concurrent;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="OperationQueue"/> (phase 3 design 1.2 to 1.8, D81, D85): every rule that holds
/// without git or a UI. The headless UI thread and phase 2's writes through the queue are in
/// App.Tests (<c>OperationQueueUiTests</c>).
/// </summary>
public class OperationQueueTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static CancellationToken TestCancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Operations_run_one_at_a_time_in_the_order_they_were_queued()
    {
        using var queue = new TestQueue();
        var order = new ConcurrentQueue<string>();
        var running = 0;
        var most = 0;
        var gate = new TaskCompletionSource();

        Func<OperationContext, Task<OperationReport>> Step(string name, Task? wait = null) => async _ =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref most, now);
            order.Enqueue(name);
            if (wait is not null)
            {
                await wait;
            }

            await Task.Yield();
            Interlocked.Decrement(ref running);
            return OperationReport.Done(name);
        };

        // A stage runs; two stages of other files are queued behind it, with no dedupe key: all
        // three run, in order (D76).
        var first = queue.Queue.RunAsync(Local("Stage", Step("a.txt", gate.Task)));
        var second = queue.Queue.RunAsync(Local("Stage", Step("b.txt")));
        var third = queue.Queue.RunAsync(Local("Stage", Step("c.txt")));
        Assert.Equal(2, queue.Queue.Waiting);
        gate.SetResult();

        var results = await Task.WhenAll(first, second, third).WaitAsync(Patience, TestCancelled);

        Assert.Equal(["a.txt", "b.txt", "c.txt"], order);
        Assert.Equal(["a.txt", "b.txt", "c.txt"], results.Select(result => result.Message));
        Assert.All(results, result => Assert.Equal(OperationOutcome.Succeeded, result.Outcome));
        Assert.Equal(1, most);
        Assert.Equal(0, queue.Queue.Waiting);
        Assert.Null(queue.Queue.Current);
    }

    [Fact]
    public async Task A_second_operation_with_the_dedupe_key_of_a_waiting_one_gets_its_task_but_never_that_of_a_running_one()
    {
        using var queue = new TestQueue();
        var gate = new TaskCompletionSource();
        var fetches = 0;
        GitOperation Fetch(string key) => new("Fetch", "Fetching…", OperationKind.Network, RefreshScope.Refs, async _ =>
        {
            Interlocked.Increment(ref fetches);
            await gate.Task;
            return OperationReport.Done("Fetched from all remotes");
        })
        { DedupeKey = key };

        // A running operation has already read what it acts on (a push, the commits it sends):
        // the same request made while it runs waits behind it and runs (review: queue).
        var running = queue.Queue.RunAsync(Fetch("fetch:*"));
        var whileRunning = queue.Queue.RunAsync(Fetch("fetch:*"));
        var sameWhileWaiting = queue.Queue.RunAsync(Fetch("fetch:*"));
        var other = queue.Queue.RunAsync(Fetch("fetch:origin"));
        var otherWhileWaiting = queue.Queue.RunAsync(Fetch("fetch:origin"));
        var withoutKey = queue.Queue.RunAsync(Local("Stage", _ => Task.FromResult(OperationReport.Done(string.Empty))));

        Assert.NotSame(running, whileRunning);
        Assert.Same(whileRunning, sameWhileWaiting);
        Assert.Same(other, otherWhileWaiting);
        Assert.Equal(3, queue.Queue.Waiting);
        gate.SetResult();
        await Task.WhenAll(running, whileRunning, other, withoutKey).WaitAsync(Patience, TestCancelled);
        Assert.Equal(3, fetches);
    }

    [Fact]
    public async Task The_same_request_again_after_Cancel_runs_once_the_cancelled_one_has_ended()
    {
        using var queue = new TestQueue();
        var started = new TaskCompletionSource();
        var fetches = 0;
        var first = queue.Queue.RunAsync(new GitOperation("Fetch origin", "Fetching from origin…", OperationKind.Network, RefreshScope.Refs, async context =>
        {
            Interlocked.Increment(ref fetches);
            started.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done("Fetched from origin");
        })
        { DedupeKey = "fetch:origin" });
        await started.Task.WaitAsync(Patience, TestCancelled);

        queue.Queue.CancelCurrent();
        var retry = queue.Queue.RunAsync(new GitOperation("Fetch origin", "Fetching from origin…", OperationKind.Network, RefreshScope.Refs, _ =>
        {
            Interlocked.Increment(ref fetches);
            return Task.FromResult(OperationReport.Done("Fetched from origin"));
        })
        { DedupeKey = "fetch:origin" });

        Assert.NotSame(first, retry);
        Assert.Equal(OperationOutcome.Cancelled, (await first.WaitAsync(Patience, TestCancelled)).Outcome);
        Assert.Equal((OperationOutcome.Succeeded, "Fetched from origin"), ((await retry.WaitAsync(Patience, TestCancelled)).Outcome, (await retry).Message));
        Assert.Equal(2, fetches);
    }

    [Fact]
    public async Task Run_is_entered_on_the_synchronization_context_of_the_caller_and_so_is_the_refresh()
    {
        using var context = new SingleThreadContext();
        var threads = new ConcurrentBag<(string What, int Thread)>();
        var queue = await context.RunAsync(() => Task.FromResult(new TestQueue(refresh: _ =>
        {
            threads.Add(("refresh", Environment.CurrentManagedThreadId));
            return Task.CompletedTask;
        })));

        var results = await context.RunAsync(async () =>
        {
            var first = queue.Queue.RunAsync(Local("Stage", async _ =>
            {
                threads.Add(("first", Environment.CurrentManagedThreadId));
                await Task.Delay(20);
                threads.Add(("first after await", Environment.CurrentManagedThreadId));
                return OperationReport.Done(string.Empty);
            }));
            var second = queue.Queue.RunAsync(Local("Unstage", _ =>
            {
                threads.Add(("second", Environment.CurrentManagedThreadId));
                return Task.FromResult(OperationReport.Done(string.Empty));
            }));
            return await Task.WhenAll(first, second);
        }).WaitAsync(Patience, TestCancelled);

        Assert.All(results, result => Assert.Equal(OperationOutcome.Succeeded, result.Outcome));
        Assert.Equal(5, threads.Count);
        Assert.All(threads, entry => Assert.Equal(context.ThreadId, entry.Thread));
        queue.Dispose();
    }

    [Fact]
    public async Task The_operation_that_finds_the_queue_idle_starts_at_once_inside_RunAsync()
    {
        using var queue = new TestQueue();
        var entered = false;

        var running = queue.Queue.RunAsync(Local("Stage", async _ =>
        {
            entered = true;
            await Task.Yield();
            return OperationReport.Done(string.Empty);
        }));

        Assert.True(entered, "Phase 2's writes started at once; so does an operation on an idle queue.");
        await running.WaitAsync(Patience, TestCancelled);
    }

    [Fact]
    public async Task A_background_operation_is_never_pre_empted_and_the_users_operation_waits_for_it()
    {
        using var queue = new TestQueue();
        var gate = new TaskCompletionSource();
        CancellationToken backgroundToken = default;
        var order = new ConcurrentQueue<string>();
        var autoFetch = new GitOperation("Auto-fetch", "Fetching…", OperationKind.Network, RefreshScope.Refs, async context =>
        {
            backgroundToken = context.CancellationToken;
            await gate.Task;
            order.Enqueue("auto-fetch");
            return OperationReport.Done(string.Empty);
        })
        { Prompts = PromptMode.Deny };

        Assert.True(queue.Queue.TryStartInBackground(autoFetch));
        Assert.False(queue.Queue.TryStartInBackground(autoFetch), "Only an idle queue starts a background operation.");
        var current = queue.Queue.Current;
        Assert.NotNull(current);
        Assert.True(current.IsBackground);

        // While nothing waits for it, it offers no Cancel, and a cancel does nothing.
        Assert.False(current.CanCancel);
        queue.Queue.CancelCurrent();
        Assert.False(backgroundToken.IsCancellationRequested);

        var fetch = queue.Queue.RunAsync(new GitOperation("Fetch", "Fetching…", OperationKind.Network, RefreshScope.Refs, _ =>
        {
            order.Enqueue("fetch");
            return Task.FromResult(OperationReport.Done("Fetched"));
        })
        { DedupeKey = "fetch:*" });

        Assert.Equal(1, queue.Queue.Waiting);
        Assert.False(fetch.IsCompleted);
        Assert.Same(fetch, queue.Queue.RunAsync(new GitOperation("Fetch", "Fetching…", OperationKind.Network, RefreshScope.Refs, _ => throw new InvalidOperationException()) { DedupeKey = "fetch:*" }));
        gate.SetResult();
        var result = await fetch.WaitAsync(Patience, TestCancelled);

        Assert.Equal(OperationOutcome.Succeeded, result.Outcome);
        Assert.Equal(["auto-fetch", "fetch"], order);
        Assert.Same(result, queue.Queue.LastResult);
    }

    [Fact]
    public async Task A_stalled_background_operation_can_be_cancelled_while_the_users_operation_waits_for_it()
    {
        using var folder = new TempDirectory("locks");
        var gitFolder = Directory.CreateDirectory(Path.Combine(folder.Path, ".git")).FullName;
        var left = Path.Combine(Directory.CreateDirectory(Path.Combine(gitFolder, "refs", "remotes", "origin")).FullName, "main.lock");
        using var queue = new TestQueue(gitDirectory: gitFolder);
        var finished = new TaskCompletionSource<OperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Queue.Finished += (_, e) =>
        {
            if (e.Operation.IsBackground)
            {
                finished.TrySetResult(e.Result);
            }
        };

        // An auto-fetch whose connection went silent: it would never end by itself.
        Assert.True(queue.Queue.TryStartInBackground(new GitOperation("Auto-fetch", string.Empty, OperationKind.Network, RefreshScope.Refs, async context =>
        {
            File.WriteAllText(left, string.Empty);
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done(string.Empty);
        })));
        var staged = queue.Queue.RunAsync(Local("Stage", _ => Task.FromResult(OperationReport.Done(string.Empty))));

        Assert.True(queue.Queue.Current!.CanCancel, "The user's operation waits for the auto-fetch: Cancel is offered.");
        queue.Queue.CancelCurrent();

        var background = await finished.Task.WaitAsync(Patience, TestCancelled);
        Assert.Equal((OperationOutcome.Cancelled, "Auto-fetch cancelled"), (background.Outcome, background.Message));
        Assert.Equal([Path.GetFullPath(left)], background.StaleLockFiles);
        Assert.Equal(OperationOutcome.Succeeded, (await staged.WaitAsync(Patience, TestCancelled)).Outcome);
        Assert.Contains(queue.Log.Entries, entry => entry.Message.Contains("graph: Auto-fetch: Cancelled after ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_background_operations_result_is_not_the_last_result_but_is_told_to_listeners()
    {
        using var queue = new TestQueue();
        var finished = new ConcurrentQueue<(GitOperation Operation, OperationResult Result)>();
        queue.Queue.Finished += (_, e) => finished.Enqueue((e.Operation, e.Result));
        var done = new TaskCompletionSource();
        queue.Queue.Finished += (_, _) => done.TrySetResult();

        Assert.True(queue.Queue.TryStartInBackground(new GitOperation("Auto-fetch", string.Empty, OperationKind.Network, RefreshScope.None, _ =>
            throw new GitException("git fetch --all", 1, "fatal: unable to access 'https://example.com/x.git/'"))));
        await done.Task.WaitAsync(Patience, TestCancelled);
        await queue.Queue.WhenIdleAsync().WaitAsync(Patience, TestCancelled);

        Assert.Null(queue.Queue.LastResult);
        var (operation, result) = Assert.Single(finished);
        Assert.True(operation.IsBackground);
        Assert.Equal(OperationOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task Cancel_stops_a_network_operation_until_its_cancel_window_ends_and_never_a_local_one()
    {
        using var queue = new TestQueue();
        var started = new TaskCompletionSource();
        var fetch = queue.Queue.RunAsync(new GitOperation("Fetch", "Fetching…", OperationKind.Network, RefreshScope.Refs, async context =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done("Fetched");
        }));
        await started.Task.WaitAsync(Patience, TestCancelled);

        Assert.True(queue.Queue.Current!.CanCancel);
        queue.Queue.CancelCurrent();
        var result = await fetch.WaitAsync(Patience, TestCancelled);

        Assert.Equal(OperationOutcome.Cancelled, result.Outcome);
        Assert.Equal("Fetch cancelled", result.Message);
        Assert.Null(result.Failure);
        Assert.Equal([RefreshScope.Refs], queue.Refreshes);

        // A local operation offers no Cancel, and its token cannot be cancelled.
        var localGate = new TaskCompletionSource();
        CancellationToken localToken = default;
        var local = queue.Queue.RunAsync(Local("Merge", async context =>
        {
            localToken = context.CancellationToken;
            await localGate.Task;
            return OperationReport.Done("Merged");
        }));
        Assert.False(queue.Queue.Current!.CanCancel);
        queue.Queue.CancelCurrent();
        localGate.SetResult();
        Assert.Equal(OperationOutcome.Succeeded, (await local.WaitAsync(Patience, TestCancelled)).Outcome);
        Assert.False(localToken.CanBeCanceled);
    }

    [Fact]
    public async Task After_EndCancelWindow_Cancel_is_no_longer_offered_and_the_token_no_longer_reaches_git()
    {
        using var queue = new TestQueue();
        var fetched = new TaskCompletionSource();
        var integrate = new TaskCompletionSource();
        CancellationToken afterWindow = default;
        var pull = queue.Queue.RunAsync(new GitOperation("Pull", "Pulling…", OperationKind.Network, RefreshScope.Refs, async context =>
        {
            Assert.True(context.CancellationToken.CanBeCanceled);
            context.EndCancelWindow();
            afterWindow = context.CancellationToken;
            fetched.SetResult();
            await integrate.Task;
            return OperationReport.Done("Pulled");
        }));
        await fetched.Task.WaitAsync(Patience, TestCancelled);

        Assert.False(queue.Queue.Current!.CanCancel);
        queue.Queue.CancelCurrent();
        integrate.SetResult();

        Assert.Equal(OperationOutcome.Succeeded, (await pull.WaitAsync(Patience, TestCancelled)).Outcome);
        Assert.False(afterWindow.CanBeCanceled);
    }

    [Fact]
    public async Task A_cancel_that_came_before_EndCancelWindow_stops_the_operation_there()
    {
        using var queue = new TestQueue();
        var fetching = new TaskCompletionSource();
        var fetched = new TaskCompletionSource();
        var integrated = false;
        var pull = queue.Queue.RunAsync(new GitOperation("Pull", "Pulling…", OperationKind.Network, RefreshScope.Refs, async context =>
        {
            fetching.SetResult();
            await fetched.Task;
            context.EndCancelWindow();
            integrated = true;
            return OperationReport.Done("Pulled");
        }));
        await fetching.Task.WaitAsync(Patience, TestCancelled);

        // The fetch did not look at the token (it had finished): the cancel still holds.
        queue.Queue.CancelCurrent();
        fetched.SetResult();

        Assert.Equal(OperationOutcome.Cancelled, (await pull.WaitAsync(Patience, TestCancelled)).Outcome);
        Assert.False(integrated);
    }

    [Fact]
    public async Task Exceptions_become_failures_with_gits_cleaned_output_and_the_queue_goes_on()
    {
        using var queue = new TestQueue();
        var stderr = "Receiving objects:  45% (9/20)\rReceiving objects: 100% (20/20), done.\n" +
            "fatal: unable to access 'https://alice:ghp_SECRET@example.com/x.git/': 403\n";
        var git = await queue.Queue.RunAsync(Local("Push main", _ =>
            throw new GitException("git push origin", 1, stderr)));
        var stdoutOnly = await queue.Queue.RunAsync(Local("Merge", _ =>
            throw new GitException("git merge x", 1, string.Empty, standardOutput: "Auto-merging a.txt\nCONFLICT (content): Merge conflict in a.txt\n")));
        var silent = await queue.Queue.RunAsync(Local("Stage", _ => throw new GitException("git add", null, string.Empty)));
        var io = await queue.Queue.RunAsync(Local("Ignore a.txt", _ => throw new IOException("The file is in use.")));
        var unexpected = await queue.Queue.RunAsync(Local("Reset", _ => throw new InvalidOperationException("Broken.")));
        var cancelled = await queue.Queue.RunAsync(Local("Stash", _ => throw new OperationCanceledException()));
        var after = await queue.Queue.RunAsync(Local("Commit", _ => Task.FromResult(OperationReport.Done("Committed"))));

        Assert.Equal(OperationOutcome.Failed, git.Outcome);
        Assert.Equal("Push main failed", git.Message);
        Assert.Equal(
            new GitFailure(GitFailureKind.Other, "Push main failed.", "fatal: unable to access 'https://***@example.com/x.git/': 403", "git push origin", 1),
            git.Failure);
        Assert.Equal("Auto-merging a.txt\nCONFLICT (content): Merge conflict in a.txt", stdoutOnly.Failure!.GitOutput);
        Assert.Equal("'git add' could not be started.", silent.Failure!.GitOutput);
        Assert.Equal((OperationOutcome.Failed, "The file is in use."), (io.Outcome, io.Failure!.GitOutput));
        Assert.Equal((OperationOutcome.Failed, "Unexpected error: Broken."), (unexpected.Outcome, unexpected.Failure!.GitOutput));
        Assert.Contains(queue.Log.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("Reset: unexpected error", StringComparison.Ordinal));
        Assert.Equal((OperationOutcome.Cancelled, "Stash cancelled"), (cancelled.Outcome, cancelled.Message));
        Assert.Equal(OperationOutcome.Succeeded, after.Outcome);
    }

    [Fact]
    public async Task A_reported_failure_or_conflict_keeps_its_message()
    {
        using var queue = new TestQueue();
        var failure = new GitFailure(GitFailureKind.Other, "a1b2c3d changes nothing on main.", "The previous cherry-pick is now empty.", "git cherry-pick a1b2c3d", 1);

        var empty = await queue.Queue.RunAsync(Local("Cherry-pick", _ => Task.FromResult(OperationReport.Failed("Cherry-pick stopped", failure))));
        var conflicts = await queue.Queue.RunAsync(Local("Merge", _ => Task.FromResult(OperationReport.Conflicts("Merge stopped: 1 file has conflicts"))));

        Assert.Equal(new OperationResult(OperationOutcome.Failed, "Cherry-pick stopped", failure), empty);
        Assert.Equal(new OperationResult(OperationOutcome.Conflicts, "Merge stopped: 1 file has conflicts", null), conflicts);
    }

    [Fact]
    public async Task The_refresh_it_names_runs_after_success_failure_and_cancel_with_the_watcher_paused_only_around_the_work()
    {
        using var queue = new TestQueue();
        var pausedDuringRun = new List<int>();

        await queue.Queue.RunAsync(new GitOperation("Commit", string.Empty, OperationKind.Local, RefreshScope.Refs, _ =>
        {
            pausedDuringRun.Add(queue.Pauses);
            return Task.FromResult(OperationReport.Done(string.Empty));
        }));
        await queue.Queue.RunAsync(new GitOperation("Stage", string.Empty, OperationKind.Local, RefreshScope.Status, _ =>
        {
            pausedDuringRun.Add(queue.Pauses);
            throw new GitException("git add", 128, "fatal: x");
        }));
        await queue.Queue.RunAsync(new GitOperation("Stash", string.Empty, OperationKind.Local, RefreshScope.Status, _ => throw new OperationCanceledException()));
        await queue.Queue.RunAsync(new GitOperation("Copy", string.Empty, OperationKind.Local, RefreshScope.None, _ => Task.FromResult(OperationReport.Done(string.Empty))));

        Assert.Equal([1, 1], pausedDuringRun);
        Assert.Equal(0, queue.Pauses);
        Assert.Equal([0, 0, 0], queue.PausesDuringRefresh);
        Assert.Equal([RefreshScope.Refs, RefreshScope.Status, RefreshScope.Status], queue.Refreshes);
    }

    [Fact]
    public async Task A_refresh_that_fails_is_logged_and_the_operation_keeps_its_outcome()
    {
        using var queue = new TestQueue(refresh: _ => throw new InvalidOperationException("Refresh broke."));

        var result = await queue.Queue.RunAsync(Local("Stage", _ => Task.FromResult(OperationReport.Done("Staged"))));

        Assert.Equal(OperationOutcome.Succeeded, result.Outcome);
        Assert.Contains(queue.Log.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("Refresh broke.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Each_operation_gets_an_id_that_its_git_calls_record_and_a_log_line_and_record_of_its_own()
    {
        var operations = new OperationLog();
        using var queue = new TestQueue(operations: operations);
        string? seen = null;
        string? contextId = null;

        var result = await queue.Queue.RunAsync(Local("Merge feature", async context =>
        {
            await Task.Yield();
            seen = GitOperationScope.Current;
            contextId = context.OperationId;
            return OperationReport.Done("Merged feature into main");
        }));
        await queue.Queue.RunAsync(Local("Stage", _ => throw new GitException("git add", 1, "error: one\nerror: two")));

        Assert.NotNull(seen);
        Assert.Equal(contextId, seen);
        Assert.StartsWith("op-", seen, StringComparison.Ordinal);
        Assert.Null(GitOperationScope.Current);
        var records = operations.Snapshot();
        Assert.Equal(2, records.Count);
        Assert.Equal((seen, "graph", "Merge feature", OperationOutcome.Succeeded, "Merged feature into main"), (records[0].Id, records[0].Repository, records[0].Title, records[0].Outcome, records[0].Message));
        Assert.NotEqual(records[0].Id, records[1].Id);
        Assert.Contains(queue.Log.Entries, entry => entry is { Level: LogLevel.Debug, Message: "graph: Merge feature: started" });
        Assert.Contains(queue.Log.Entries, entry => entry.Level == LogLevel.Info
            && entry.Message.StartsWith("graph: Merge feature: Succeeded after ", StringComparison.Ordinal)
            && entry.Message.EndsWith(" ms: Merged feature into main", StringComparison.Ordinal));
        Assert.Contains(queue.Log.Entries, entry => entry.Level == LogLevel.Warning
            && entry.Message.StartsWith("graph: Stage: Failed after ", StringComparison.Ordinal)
            && entry.Message.EndsWith(" ms: error: one", StringComparison.Ordinal));
        _ = result;
    }

    [Fact]
    public async Task A_prompt_scope_is_opened_for_Ask_and_Deny_given_to_the_operation_and_closed_after_it()
    {
        var broker = new RecordingBroker();
        using var queue = new TestQueue(prompts: broker);
        IReadOnlyDictionary<string, string?>? environment = null;

        await queue.Queue.RunAsync(new GitOperation("Fetch", string.Empty, OperationKind.Network, RefreshScope.Refs, context =>
        {
            environment = context.Environment;
            return Task.FromResult(OperationReport.Done(string.Empty));
        }));
        await queue.Queue.RunAsync(Local("Stage", _ => Task.FromResult(OperationReport.Done(string.Empty))) with { Prompts = PromptMode.None });
        var done = new TaskCompletionSource();
        queue.Queue.Finished += (_, _) => done.TrySetResult();
        Assert.True(queue.Queue.TryStartInBackground(new GitOperation("Auto-fetch", string.Empty, OperationKind.Network, RefreshScope.Refs, _ => Task.FromResult(OperationReport.Done(string.Empty)))));
        await done.Task.WaitAsync(Patience, TestCancelled);

        Assert.Equal([("Fetch", "graph", PromptMode.Ask), ("Auto-fetch", "graph", PromptMode.Deny)], broker.Begun.Select(scope => (scope.Title, scope.Repository, scope.Mode)));
        Assert.Equal("yes", environment!["VISUALCOMMIT_TEST"]);
        Assert.All(broker.Begun, scope => Assert.True(scope.IsDisposed));
        Assert.StartsWith("op-", broker.Begun[0].Id, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failure_after_the_user_cancelled_a_prompt_is_a_cancel()
    {
        var broker = new RecordingBroker();
        using var queue = new TestQueue(prompts: broker);

        var result = await queue.Queue.RunAsync(new GitOperation("Fetch", string.Empty, OperationKind.Network, RefreshScope.Refs, _ =>
        {
            broker.Begun[^1].CancelByUser();
            throw new GitException("git fetch origin", 128, "fatal: could not read Username for 'https://example.com': terminal prompts disabled");
        }));

        Assert.Equal((OperationOutcome.Cancelled, "Fetch cancelled"), (result.Outcome, result.Message));
    }

    [Fact]
    public async Task Closing_the_tab_drops_the_waiting_cancels_the_running_network_operation_and_skips_its_refresh()
    {
        var queue = new TestQueue();
        var started = new TaskCompletionSource();
        var waitingRan = false;
        var fetch = queue.Queue.RunAsync(new GitOperation("Fetch", string.Empty, OperationKind.Network, RefreshScope.Refs, async context =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done(string.Empty);
        }));
        var waiting = queue.Queue.RunAsync(Local("Stage", _ =>
        {
            waitingRan = true;
            return Task.FromResult(OperationReport.Done(string.Empty));
        }));
        await started.Task.WaitAsync(Patience, TestCancelled);

        queue.Dispose();

        Assert.Equal(OperationOutcome.Cancelled, (await waiting.WaitAsync(Patience, TestCancelled)).Outcome);
        Assert.Equal(OperationOutcome.Cancelled, (await fetch.WaitAsync(Patience, TestCancelled)).Outcome);
        await queue.Queue.WhenIdleAsync().WaitAsync(Patience, TestCancelled);
        Assert.False(waitingRan);
        Assert.Empty(queue.Refreshes);
        var late = await queue.Queue.RunAsync(Local("Commit", _ => throw new InvalidOperationException("Must not run.")));
        Assert.Equal(OperationOutcome.Cancelled, late.Outcome);
        Assert.False(queue.Queue.TryStartInBackground(Local("Auto-fetch", _ => throw new InvalidOperationException("Must not run."))));
    }

    [Fact]
    public async Task Closing_the_tab_lets_a_running_local_operation_finish_every_step()
    {
        var queue = new TestQueue();
        var firstStep = new TaskCompletionSource();
        var secondStep = new TaskCompletionSource();
        var steps = new ConcurrentQueue<string>();
        var operation = queue.Queue.RunAsync(new GitOperation("Check out main with your changes", string.Empty, OperationKind.Local, RefreshScope.Refs, async context =>
        {
            steps.Enqueue("stash");
            await firstStep.Task;
            Assert.False(context.CancellationToken.CanBeCanceled);
            steps.Enqueue("checkout");
            await secondStep.Task;
            steps.Enqueue("pop");
            return OperationReport.Done("Checked out main and re-applied your changes");
        })
        { HasSeveralSteps = true });

        queue.Dispose();
        var idle = queue.Queue.WhenIdleAsync();
        firstStep.SetResult();
        Assert.False(idle.IsCompleted);
        secondStep.SetResult();

        Assert.Equal(OperationOutcome.Succeeded, (await operation.WaitAsync(Patience, TestCancelled)).Outcome);
        await idle.WaitAsync(Patience, TestCancelled);
        Assert.Equal(["stash", "checkout", "pop"], steps);
        Assert.Empty(queue.Refreshes);
    }

    [Fact]
    public async Task The_next_operation_whose_start_is_posted_but_has_not_run_does_not_run_after_the_tab_closed()
    {
        var context = new ManualContext();
        var previous = SynchronizationContext.Current;
        var nextRan = false;
        Task<OperationResult> next;
        var queue = new TestQueue();
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            var gate = new TaskCompletionSource();
            _ = queue.Queue.RunAsync(Local("Discard", async _ =>
            {
                await gate.Task;
                return OperationReport.Done("Discarded");
            }));
            next = queue.Queue.RunAsync(Local("Delete branch", _ =>
            {
                nextRan = true;
                return Task.FromResult(OperationReport.Done("Deleted"));
            }));

            // The Discard ends: Delete branch is next in line and its start is posted to the UI
            // thread, which closes the tab before it gets to that start.
            gate.SetResult();
            while (queue.Queue.Current?.Title != "Delete branch")
            {
                Assert.True(context.RunOne(), "The queue stopped before Delete branch was next in line.");
            }

            queue.Dispose();
            while (context.RunOne())
            {
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        Assert.False(nextRan, "The operation next in line ran after the tab closed.");
        Assert.Equal(OperationOutcome.Cancelled, (await next.WaitAsync(Patience, TestCancelled)).Outcome);
        await queue.Queue.WhenIdleAsync().WaitAsync(Patience, TestCancelled);
        Assert.Null(queue.Queue.Current);
    }

    [Fact]
    public async Task Closing_the_tab_leaves_a_background_operation_to_finish()
    {
        var queue = new TestQueue();
        var gate = new TaskCompletionSource();
        CancellationToken token = default;
        var finished = new TaskCompletionSource<OperationResult>();
        queue.Queue.Finished += (_, e) => finished.TrySetResult(e.Result);
        Assert.True(queue.Queue.TryStartInBackground(new GitOperation("Auto-fetch", string.Empty, OperationKind.Network, RefreshScope.Refs, async context =>
        {
            token = context.CancellationToken;
            await gate.Task;
            return OperationReport.Done(string.Empty);
        })));

        queue.Dispose();
        gate.SetResult();

        Assert.Equal(OperationOutcome.Succeeded, (await finished.Task.WaitAsync(Patience, TestCancelled)).Outcome);
        Assert.False(token.IsCancellationRequested);
    }

    [Fact]
    public async Task Dropping_the_waiting_operations_leaves_the_running_one()
    {
        using var queue = new TestQueue();
        var gate = new TaskCompletionSource();
        var running = queue.Queue.RunAsync(Local("Merge", async _ =>
        {
            await gate.Task;
            return OperationReport.Done("Merged");
        }));
        var waiting = queue.Queue.RunAsync(Local("Stage", _ => throw new InvalidOperationException("Must not run.")));

        queue.Queue.DropWaiting();

        Assert.Equal(OperationOutcome.Cancelled, (await waiting.WaitAsync(Patience, TestCancelled)).Outcome);
        Assert.Equal(0, queue.Queue.Waiting);
        gate.SetResult();
        Assert.Equal(OperationOutcome.Succeeded, (await running.WaitAsync(Patience, TestCancelled)).Outcome);
    }

    [Fact]
    public async Task A_cancelled_network_operation_reports_the_lock_files_git_left_behind()
    {
        using var folder = new TempDirectory("locks");
        var gitFolder = Directory.CreateDirectory(Path.Combine(folder.Path, ".git")).FullName;
        Directory.CreateDirectory(Path.Combine(gitFolder, "refs", "remotes", "origin"));
        var old = Path.Combine(gitFolder, "old.lock");
        File.WriteAllText(old, string.Empty);
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-1));
        using var queue = new TestQueue(gitDirectory: gitFolder);
        var started = new TaskCompletionSource();
        var left = Path.Combine(gitFolder, "refs", "remotes", "origin", "main.lock");

        var fetch = queue.Queue.RunAsync(new GitOperation("Fetch", string.Empty, OperationKind.Network, RefreshScope.Refs, async context =>
        {
            File.WriteAllText(left, string.Empty);
            started.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done(string.Empty);
        }));
        await started.Task.WaitAsync(Patience, TestCancelled);
        queue.Queue.CancelCurrent();
        var result = await fetch.WaitAsync(Patience, TestCancelled);

        Assert.Equal(OperationOutcome.Cancelled, result.Outcome);
        Assert.Equal([Path.GetFullPath(left)], result.StaleLockFiles);

        // A local operation that ends cancelled is not looked at: git was not stopped.
        var local = await queue.Queue.RunAsync(Local("Stash", _ => throw new OperationCanceledException()));
        Assert.Empty(local.StaleLockFiles);
    }

    [Fact]
    public async Task A_local_operation_that_can_be_cancelled_gets_a_token_and_its_cancel_reports_the_lock_files()
    {
        // Phase 4's merge tool (docs/phase-4-design.md 11.9, D149): local, but it only waits for
        // another program, so Cancel is offered and stops it as it stops a network operation.
        using var folder = new TempDirectory("locks");
        var gitFolder = Directory.CreateDirectory(Path.Combine(folder.Path, ".git")).FullName;
        using var queue = new TestQueue(gitDirectory: gitFolder);
        var started = new TaskCompletionSource();
        var left = Path.Combine(gitFolder, "config.lock");
        CancellationToken token = default;

        var tool = queue.Queue.RunAsync(new GitOperation("Merge tool for notes.txt", "Waiting for fake to close…", OperationKind.Local, RefreshScope.Status, async context =>
        {
            token = context.CancellationToken;
            File.WriteAllText(left, string.Empty);
            started.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done(string.Empty);
        })
        { CanBeCancelled = true });
        await started.Task.WaitAsync(Patience, TestCancelled);

        Assert.True(token.CanBeCanceled);
        Assert.Equal(OperationKind.Local, queue.Queue.Current!.Kind);
        Assert.True(queue.Queue.Current.CanCancel);
        queue.Queue.CancelCurrent();
        var result = await tool.WaitAsync(Patience, TestCancelled);

        Assert.Equal(OperationOutcome.Cancelled, result.Outcome);
        Assert.Equal("Merge tool for notes.txt cancelled", result.Message);
        Assert.Equal([Path.GetFullPath(left)], result.StaleLockFiles);
        Assert.Equal([RefreshScope.Status], queue.Refreshes);
    }

    [Fact]
    public async Task Closing_the_tab_leaves_a_local_operation_that_can_be_cancelled_to_finish()
    {
        var queue = new TestQueue();
        var started = new TaskCompletionSource();
        var gate = new TaskCompletionSource();
        CancellationToken token = default;
        var tool = queue.Queue.RunAsync(new GitOperation("Merge tool for notes.txt", string.Empty, OperationKind.Local, RefreshScope.Status, async context =>
        {
            token = context.CancellationToken;
            started.SetResult();
            await gate.Task;
            return OperationReport.Done("Resolved notes.txt in fake");
        })
        { CanBeCancelled = true });
        await started.Task.WaitAsync(Patience, TestCancelled);

        queue.Dispose();
        Assert.False(token.IsCancellationRequested);
        gate.SetResult();

        var result = await tool.WaitAsync(Patience, TestCancelled);
        Assert.Equal(OperationOutcome.Succeeded, result.Outcome);
        Assert.False(token.IsCancellationRequested);
        Assert.Empty(queue.Refreshes);
    }

    [Fact]
    public async Task Progress_shows_in_the_running_operation_at_most_ten_times_a_second()
    {
        using var queue = new TestQueue();
        var changes = 0;
        queue.Queue.Changed += (_, _) => Interlocked.Increment(ref changes);
        var reported = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var fetch = queue.Queue.RunAsync(new GitOperation("Fetch", "Fetching…", OperationKind.Network, RefreshScope.None, async context =>
        {
            for (var percent = 0; percent <= 100; percent++)
            {
                context.Progress.Report(new OperationProgress("Receiving objects", percent, $"Receiving objects: {percent}%"));
            }

            reported.SetResult();
            await finish.Task;
            return OperationReport.Done(string.Empty);
        }));
        await reported.Task.WaitAsync(Patience, TestCancelled);
        var afterBurst = Volatile.Read(ref changes);

        Assert.Equal(new OperationProgress("Receiving objects", 100, "Receiving objects: 100%"), queue.Queue.Current!.Progress);
        Assert.True(afterBurst <= 4, $"{afterBurst} changes were raised for a burst of 101 progress lines.");
        for (var wait = 0; Volatile.Read(ref changes) == afterBurst && wait < 100; wait++)
        {
            await Task.Delay(20, TestCancelled);
        }

        Assert.True(Volatile.Read(ref changes) > afterBurst, "The last update of a burst is raised after the interval.");
        finish.SetResult();
        await fetch.WaitAsync(Patience, TestCancelled);
    }

    [Fact]
    public async Task Cancel_of_a_waiting_operation_completes_it_cancelled_without_running_and_leaves_the_others()
    {
        // docs/phase-6-design.md 4.5: a bulk run's Cancel all cancels its own operations only.
        using var queue = new TestQueue();
        var gate = new TaskCompletionSource();
        var changes = 0;
        var running = queue.Queue.RunAsync(Local("Push main", async _ =>
        {
            await gate.Task;
            return OperationReport.Done("Pushed main");
        }));
        var first = Network("Fetch", _ => throw new InvalidOperationException("Must not run."));
        var waiting = queue.Queue.RunAsync(first);
        var other = queue.Queue.RunAsync(Local("Stage", _ => Task.FromResult(OperationReport.Done("Staged"))));
        queue.Queue.Changed += (_, _) => Interlocked.Increment(ref changes);

        queue.Queue.Cancel(first);

        var result = await waiting.WaitAsync(Patience, TestCancelled);
        Assert.Equal((OperationOutcome.Cancelled, "Fetch cancelled"), (result.Outcome, result.Message));
        Assert.Equal(1, queue.Queue.Waiting);
        Assert.Equal("Stage", queue.Queue.FirstWaitingTitle);
        Assert.Equal("Push main", queue.Queue.Current!.Title);
        Assert.True(Volatile.Read(ref changes) >= 1);
        gate.SetResult();
        Assert.Equal(OperationOutcome.Succeeded, (await running.WaitAsync(Patience, TestCancelled)).Outcome);
        Assert.Equal(OperationOutcome.Succeeded, (await other.WaitAsync(Patience, TestCancelled)).Outcome);
    }

    [Fact]
    public async Task Cancel_of_the_operation_next_in_line_whose_start_is_posted_but_has_not_run_completes_it_cancelled_without_running()
    {
        // An operation ends: the next one is current and its start is posted to the UI thread, which
        // runs a Cancel (Cancel all's click, a registration's callback) before that start. It has
        // not started, so it ends as a waiting one does (docs/phase-6-design.md 4.5), and the one
        // after it is next in line instead.
        var context = new ManualContext();
        var previous = SynchronizationContext.Current;
        var operations = new OperationLog();
        var started = new List<string>();
        operations.Started += (_, start) => started.Add(start.Title);
        var broker = new RecordingBroker();
        var lockScans = 0;
        var ran = new List<string>();
        Task<OperationResult> discard;
        Task<OperationResult> fetching;
        Task<OperationResult> deleting;
        Task<OperationResult> staging;
        using var queue = new TestQueue(prompts: broker, operations: operations, findLockFiles: _ =>
        {
            Interlocked.Increment(ref lockScans);
            return [];
        });
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            var gate = new TaskCompletionSource();
            discard = queue.Queue.RunAsync(Local("Discard", async _ =>
            {
                await gate.Task;
                return OperationReport.Done("Discarded");
            }));
            var fetch = Network("Fetch", _ =>
            {
                ran.Add("Fetch");
                return Task.FromResult(OperationReport.Done("Fetched from all remotes"));
            });
            var delete = Local("Delete branch", _ =>
            {
                ran.Add("Delete branch");
                return Task.FromResult(OperationReport.Done("Deleted"));
            });
            fetching = queue.Queue.RunAsync(fetch);
            deleting = queue.Queue.RunAsync(delete);

            // Discard ends: Fetch, a network operation, is next in line and its start is posted.
            gate.SetResult();
            while (queue.Queue.Current?.Title != "Fetch")
            {
                Assert.True(context.RunOne(), "The queue stopped before Fetch was next in line.");
            }

            queue.Queue.Cancel(fetch);

            Assert.Equal("Delete branch", queue.Queue.Current?.Title);
            Assert.Equal(0, queue.Queue.Waiting);

            // Delete branch, a local operation (no token to cancel), is now next in line, its start
            // posted in turn and not run either.
            queue.Queue.Cancel(delete);

            Assert.Null(queue.Queue.Current);
            while (context.RunOne())
            {
            }

            // The queue goes on: an operation queued now runs.
            staging = queue.Queue.RunAsync(Local("Stage", _ =>
            {
                ran.Add("Stage");
                return Task.FromResult(OperationReport.Done("Staged"));
            }));
            while (context.RunOne())
            {
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        Assert.Equal(OperationOutcome.Succeeded, (await discard.WaitAsync(Patience, TestCancelled)).Outcome);
        var fetched = await fetching.WaitAsync(Patience, TestCancelled);
        var deleted = await deleting.WaitAsync(Patience, TestCancelled);
        Assert.Equal((OperationOutcome.Cancelled, "Fetch cancelled"), (fetched.Outcome, fetched.Message));
        Assert.Equal((OperationOutcome.Cancelled, "Delete branch cancelled"), (deleted.Outcome, deleted.Message));
        Assert.Equal(OperationOutcome.Succeeded, (await staging.WaitAsync(Patience, TestCancelled)).Outcome);
        await queue.Queue.WhenIdleAsync().WaitAsync(Patience, TestCancelled);

        // Neither ran, nor got a start record, a prompt scope, a lock scan or a refresh.
        Assert.Equal(["Stage"], ran);
        Assert.Equal(["Discard", "Stage"], started);
        Assert.Equal(["Discard", "Stage"], broker.Begun.Select(scope => scope.Title));
        Assert.Equal(0, Volatile.Read(ref lockScans));
        Assert.Equal([RefreshScope.Status, RefreshScope.Status], queue.Refreshes);
        Assert.Equal(["Discard", "Stage"], operations.Snapshot().Select(record => record.Title));
        Assert.Equal(2, queue.Log.Entries.Count(entry => entry.Message.EndsWith(": cancelled before it started.", StringComparison.Ordinal)));
        Assert.Null(queue.Queue.Current);
    }

    [Fact]
    public async Task Cancel_of_the_running_network_operation_in_its_cancel_window_cancels_it_and_scans_for_lock_files()
    {
        using var folder = new TempDirectory("locks");
        var gitFolder = Directory.CreateDirectory(Path.Combine(folder.Path, ".git")).FullName;
        Directory.CreateDirectory(Path.Combine(gitFolder, "refs", "remotes", "origin"));
        using var queue = new TestQueue(gitDirectory: gitFolder);
        var started = new TaskCompletionSource();
        var left = Path.Combine(gitFolder, "refs", "remotes", "origin", "main.lock");
        var fetch = Network("Fetch", async context =>
        {
            File.WriteAllText(left, string.Empty);
            started.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done("Fetched from all remotes");
        });
        var task = queue.Queue.RunAsync(fetch);
        var behind = queue.Queue.RunAsync(Local("Stage", _ => Task.FromResult(OperationReport.Done("Staged"))));
        await started.Task.WaitAsync(Patience, TestCancelled);

        queue.Queue.Cancel(fetch);
        var result = await task.WaitAsync(Patience, TestCancelled);

        Assert.Equal((OperationOutcome.Cancelled, "Fetch cancelled"), (result.Outcome, result.Message));
        Assert.Equal([Path.GetFullPath(left)], result.StaleLockFiles);
        Assert.Equal(OperationOutcome.Succeeded, (await behind.WaitAsync(Patience, TestCancelled)).Outcome);
    }

    [Fact]
    public async Task Cancel_of_a_running_operation_past_its_cancel_window_or_of_a_local_one_lets_it_run_to_its_end()
    {
        using var queue = new TestQueue();
        var fetched = new TaskCompletionSource();
        var integrate = new TaskCompletionSource();
        var pull = Network("Pull", async context =>
        {
            context.EndCancelWindow();
            fetched.SetResult();
            await integrate.Task;
            return OperationReport.Done("Pulled");
        });
        var pulling = queue.Queue.RunAsync(pull);
        await fetched.Task.WaitAsync(Patience, TestCancelled);

        queue.Queue.Cancel(pull);
        integrate.SetResult();
        Assert.Equal(OperationOutcome.Succeeded, (await pulling.WaitAsync(Patience, TestCancelled)).Outcome);

        var localGate = new TaskCompletionSource();
        var merge = Local("Merge", async _ =>
        {
            await localGate.Task;
            return OperationReport.Done("Merged");
        });
        var merging = queue.Queue.RunAsync(merge);
        queue.Queue.Cancel(merge);
        localGate.SetResult();
        Assert.Equal(OperationOutcome.Succeeded, (await merging.WaitAsync(Patience, TestCancelled)).Outcome);
    }

    [Fact]
    public async Task Cancel_of_an_operation_merged_into_a_waiting_one_cancels_the_shared_one_for_both_callers()
    {
        // A tab's waiting Fetch and a row's that D123's dedupe made one entry, in either order.
        using var queue = new TestQueue();
        var gate = new TaskCompletionSource();
        var fetches = 0;
        GitOperation Fetch() => Network("Fetch", _ =>
        {
            Interlocked.Increment(ref fetches);
            return Task.FromResult(OperationReport.Done("Fetched from all remotes"));
        }) with { DedupeKey = "fetch:*" };
        var running = queue.Queue.RunAsync(Local("Push main", async _ =>
        {
            await gate.Task;
            return OperationReport.Done("Pushed main");
        }));
        var tabs = Fetch();
        var rows = Fetch();
        var tabTask = queue.Queue.RunAsync(tabs);
        var rowTask = queue.Queue.RunAsync(rows);
        Assert.Same(tabTask, rowTask);

        queue.Queue.Cancel(rows);

        var result = await tabTask.WaitAsync(Patience, TestCancelled);
        Assert.Equal((OperationOutcome.Cancelled, "Fetch cancelled"), (result.Outcome, result.Message));
        Assert.Equal(0, queue.Queue.Waiting);
        gate.SetResult();
        await running.WaitAsync(Patience, TestCancelled);
        Assert.Equal(0, fetches);
    }

    [Fact]
    public async Task Cancel_of_a_merged_operation_once_the_shared_one_runs_cancels_it_in_its_cancel_window()
    {
        using var queue = new TestQueue();
        var gate = new TaskCompletionSource();
        var started = new TaskCompletionSource();
        GitOperation Fetch() => Network("Fetch", async context =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done("Fetched from all remotes");
        }) with { DedupeKey = "fetch:*" };
        var running = queue.Queue.RunAsync(Local("Commit", async _ =>
        {
            await gate.Task;
            return OperationReport.Done("Committed");
        }));
        var first = Fetch();
        var merged = Fetch();
        var shared = queue.Queue.RunAsync(first);
        Assert.Same(shared, queue.Queue.RunAsync(merged));
        gate.SetResult();
        await running.WaitAsync(Patience, TestCancelled);
        await started.Task.WaitAsync(Patience, TestCancelled);

        queue.Queue.Cancel(merged);

        Assert.Equal(OperationOutcome.Cancelled, (await shared.WaitAsync(Patience, TestCancelled)).Outcome);
    }

    [Fact]
    public async Task Cancel_of_a_background_operation_or_of_one_that_has_ended_does_nothing()
    {
        using var queue = new TestQueue();
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        CancellationToken backgroundToken = default;
        var autoFetch = Network("Auto-fetch", async context =>
        {
            backgroundToken = context.CancellationToken;
            started.SetResult();
            await release.Task;
            return OperationReport.Done("Fetched from all remotes");
        });
        var finished = new TaskCompletionSource<OperationResult>();
        queue.Queue.Finished += (_, e) =>
        {
            if (e.Operation.IsBackground)
            {
                finished.TrySetResult(e.Result);
            }
        };
        Assert.True(queue.Queue.TryStartInBackground(autoFetch));
        await started.Task.WaitAsync(Patience, TestCancelled);

        // A user's operation waits for it, so CancelCurrent would stop it (D85); Cancel does not,
        // whether given the operation as started or flagged as the queue runs it.
        var waiting = queue.Queue.RunAsync(Local("Stage", _ => Task.FromResult(OperationReport.Done("Staged"))));
        queue.Queue.Cancel(autoFetch);
        queue.Queue.Cancel(autoFetch with { IsBackground = true });
        Assert.False(backgroundToken.IsCancellationRequested);
        Assert.True(queue.Queue.Current!.CanCancel);
        release.SetResult();
        Assert.Equal(OperationOutcome.Succeeded, (await finished.Task.WaitAsync(Patience, TestCancelled)).Outcome);
        Assert.Equal(OperationOutcome.Succeeded, (await waiting.WaitAsync(Patience, TestCancelled)).Outcome);

        // Ended: the queue no longer holds it, and the next operation is not touched.
        var ended = Network("Fetch", _ => Task.FromResult(OperationReport.Done("Fetched from all remotes")));
        Assert.Equal(OperationOutcome.Succeeded, (await queue.Queue.RunAsync(ended).WaitAsync(Patience, TestCancelled)).Outcome);
        var nextStarted = new TaskCompletionSource();
        var nextGate = new TaskCompletionSource();
        var next = queue.Queue.RunAsync(Network("Fetch", async _ =>
        {
            nextStarted.SetResult();
            await nextGate.Task;
            return OperationReport.Done("Fetched from all remotes");
        }));
        await nextStarted.Task.WaitAsync(Patience, TestCancelled);
        queue.Queue.Cancel(ended);
        nextGate.SetResult();
        Assert.Equal(OperationOutcome.Succeeded, (await next.WaitAsync(Patience, TestCancelled)).Outcome);
    }

    [Fact]
    public async Task The_operations_prompt_group_reaches_the_brokers_Begin()
    {
        var broker = new RecordingBroker();
        using var queue = new TestQueue(prompts: broker);
        using var group = new PromptGroup();

        await queue.Queue.RunAsync(Network("Fetch", _ => Task.FromResult(OperationReport.Done(string.Empty))) with { PromptGroup = group });
        await queue.Queue.RunAsync(Network("Fetch", _ => Task.FromResult(OperationReport.Done(string.Empty))));

        Assert.Equal([group, null], broker.Begun.Select(scope => scope.Group));
        Assert.StartsWith("bulk-", group.Id, StringComparison.Ordinal);
    }

    private static GitOperation Network(string title, Func<OperationContext, Task<OperationReport>> run) =>
        new(title, title + "…", OperationKind.Network, RefreshScope.Refs, run);

    private static GitOperation Local(string title, Func<OperationContext, Task<OperationReport>> run) =>
        new(title, title + "…", OperationKind.Local, RefreshScope.Status, run);

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }

    /// <summary>A queue for repository "graph", whose watcher pauses and refreshes the test counts.</summary>
    private sealed class TestQueue : IDisposable
    {
        private int _pauses;

        public TestQueue(
            Func<RefreshScope, Task>? refresh = null,
            IGitPromptBroker? prompts = null,
            IOperationLog? operations = null,
            string? gitDirectory = null,
            Func<DateTimeOffset, IReadOnlyList<string>>? findLockFiles = null)
        {
            Queue = new OperationQueue(
                "graph",
                gitDirectory ?? Path.Combine(Path.GetTempPath(), "no-such-git-folder-" + Guid.NewGuid().ToString("N")),
                () =>
                {
                    Interlocked.Increment(ref _pauses);
                    return new Released(() => Interlocked.Decrement(ref _pauses));
                },
                scope =>
                {
                    Refreshes.Enqueue(scope);
                    PausesDuringRefresh.Enqueue(Pauses);
                    return refresh?.Invoke(scope) ?? Task.CompletedTask;
                },
                prompts,
                operations,
                Log,
                findLockFiles);
        }

        public OperationQueue Queue { get; }

        public ListLog Log { get; } = new();

        public ConcurrentQueue<RefreshScope> Refreshes { get; } = new();

        public ConcurrentQueue<int> PausesDuringRefresh { get; } = new();

        public int Pauses => Volatile.Read(ref _pauses);

        public void Dispose() => Queue.Dispose();

        private sealed class Released(Action release) : IDisposable
        {
            public void Dispose() => release();
        }
    }

    /// <summary>Opens scopes with one test variable and records them.</summary>
    private sealed class RecordingBroker : IGitPromptBroker
    {
        public List<Scope> Begun { get; } = [];

        public PromptScope Begin(string title, string repositoryName, PromptMode mode, string? operationId = null, PromptGroup? group = null)
        {
            var scope = new Scope(operationId ?? "prompt", title, repositoryName, mode) { Group = group };
            lock (Begun)
            {
                Begun.Add(scope);
            }

            return scope;
        }

        public sealed class Scope(string id, string title, string repository, PromptMode mode)
            : PromptScope(id, new Dictionary<string, string?> { ["VISUALCOMMIT_TEST"] = "yes" })
        {
            public string Title { get; } = title;

            public string Repository { get; } = repository;

            public PromptMode Mode { get; } = mode;

            /// <summary>The prompt group the queue passed (docs/phase-6-design.md 4.4), or null.</summary>
            public PromptGroup? Group { get; init; }

            public void CancelByUser() => UserCancelled = true;
        }
    }
}

/// <summary>A synchronization context whose posts run only when the test runs them, one at a time, on the test's thread.</summary>
internal sealed class ManualContext : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _posted = new();

    public override void Post(SendOrPostCallback d, object? state) => _posted.Enqueue((d, state));

    public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();

    /// <summary>Runs the oldest post; false when none waits (after a short wait for posts from other threads).</summary>
    public bool RunOne()
    {
        for (var tries = 0; tries < 50; tries++)
        {
            if (_posted.TryDequeue(out var work))
            {
                work.Callback(work.State);
                return true;
            }

            Thread.Sleep(10);
        }

        return false;
    }
}

/// <summary>A synchronization context with one thread of its own, as a UI thread has.</summary>
internal sealed class SingleThreadContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _work = [];
    private readonly Thread _thread;

    public SingleThreadContext()
    {
        _thread = new Thread(Pump) { IsBackground = true, Name = "Test UI thread" };
        _thread.Start();
    }

    public int ThreadId => _thread.ManagedThreadId;

    public override void Post(SendOrPostCallback d, object? state) => _work.TryAdd((d, state));

    public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();

    /// <summary>Runs <paramref name="work"/> on the thread and completes with its result.</summary>
    public Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(
            async _ =>
            {
                try
                {
                    done.SetResult(await work());
                }
                catch (Exception ex)
                {
                    done.SetException(ex);
                }
            },
            null);
        return done.Task;
    }

    public void Dispose() => _work.CompleteAdding();

    private void Pump()
    {
        SetSynchronizationContext(this);
        foreach (var (callback, state) in _work.GetConsumingEnumerable())
        {
            callback(state);
        }
    }
}
