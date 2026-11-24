using System.Reflection;
using System.Runtime.ExceptionServices;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The limit of <see cref="OperationsHarness"/>'s own steps: a first read that stalls and a queue
/// that stays busy fail by name at the limit, saying what ran meanwhile, instead of waiting for
/// the hang watchdog to stop the test process; and the limit a test gives none is what the
/// watchdog leaves the test, less the margin.
/// </summary>
public class OperationsHarnessTests
{
    private static readonly TimeSpan Short = TimeSpan.FromSeconds(1);

    /// <summary>How long a test waits for a note due after <see cref="Short"/>: its watch is a thread of its own, not the pool's.</summary>
    private static readonly TimeSpan NoteWait = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task A_first_read_that_stalls_fails_by_name_at_the_step_limit()
    {
        using var repo = await Scenarios.OperationsAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var thrown = await Assert.ThrowsAsync<TimeoutException>(
                () => OperationsHarness.OpenAsync(repo, prompts: null, wrap: git => HeldStatusReads.Wrap(git, gate.Task), stepLimit: Short));
            Assert.StartsWith($"OperationsHarness: the first read of {repo.Path} did not end within 1 s: ", thrown.Message, StringComparison.Ordinal);
            Assert.Contains("git call", thrown.Message, StringComparison.Ordinal);
        }
        finally
        {
            gate.TrySetResult();
        }
    }

    /// <summary>
    /// The open itself (git's rev-parse) keeps the watchdog's limit: a limit of nothing given to the
    /// harness's steps meets the first read, whose status read is held, and not the open before it.
    /// </summary>
    [Fact]
    public async Task The_open_before_the_first_read_is_not_held_to_the_step_limit_a_test_gives()
    {
        using var repo = await Scenarios.OperationsAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var thrown = await Assert.ThrowsAsync<TimeoutException>(
                () => OperationsHarness.OpenAsync(repo, prompts: null, wrap: git => HeldStatusReads.Wrap(git, gate.Task), stepLimit: TimeSpan.Zero));
            Assert.StartsWith($"OperationsHarness: the first read of {repo.Path} did not end within 0 s: ", thrown.Message, StringComparison.Ordinal);
        }
        finally
        {
            gate.TrySetResult();
        }
    }

    [Fact]
    public async Task A_queue_that_stays_busy_fails_its_wait_by_name_with_the_operation_it_runs()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = app.Repository.Queue.RunAsync(new GitOperation("Held step", "Holding…", OperationKind.Local, RefreshScope.None, async _ =>
        {
            await gate.Task;
            return OperationReport.Done("Held step done");
        }));
        try
        {
            var thrown = await Assert.ThrowsAsync<TimeoutException>(() => app.WaitForIdleAsync(Short));
            Assert.StartsWith($"OperationsHarness: the queue of {repo.Path} to be idle did not end within 1 s: ", thrown.Message, StringComparison.Ordinal);
            Assert.EndsWith("; the queue runs \"Held step\", 0 waiting.", thrown.Message, StringComparison.Ordinal);
        }
        finally
        {
            gate.TrySetResult();
        }

        await running;
        await app.WaitForIdleAsync();
    }

    /// <summary>
    /// An await the harness does not hold to a limit (an operation the test runs through
    /// <see cref="OperationsHarness.Operations"/>) still gets named before the hang watchdog stops
    /// the process: a harness still open at its note's time writes one line with the test, the
    /// repository, the queue's operation and the git calls since the open.
    /// </summary>
    [Fact]
    public async Task A_harness_still_open_at_its_note_names_the_test_and_the_operation_the_queue_runs()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = app.Repository.Queue.RunAsync(new GitOperation("Held step", "Holding…", OperationKind.Local, RefreshScope.None, async _ =>
        {
            await gate.Task;
            return OperationReport.Done("Held step done");
        }));
        var notes = new FirstLine();
        try
        {
            _ = app.StartStallNote(Short, notes);
            var note = await notes.Written.Task.WaitAsync(NoteWait, TestContext.Current.CancellationToken);
            Assert.StartsWith($"OperationsHarness: {TestContext.Current.Test!.TestDisplayName} has run ", note, StringComparison.Ordinal);
            Assert.Contains($" s counted with its harness on {repo.Path} still open: the queue runs \"Held step\", 0 waiting; ", note, StringComparison.Ordinal);
            Assert.Contains(" since the open", note, StringComparison.Ordinal);
        }
        finally
        {
            gate.TrySetResult();
        }

        await running;
        await app.WaitForIdleAsync();
    }

    [Fact]
    public void A_step_is_given_what_the_hang_watchdog_leaves_the_test_less_the_margin()
    {
        var left = HangWatchdogAttribute.TimeLeftForCurrentTest();
        Assert.NotNull(left);
        var limit = OperationsHarness.StepLimit();
        Assert.InRange(limit, left.Value - OperationsHarness.WatchdogMargin - TimeSpan.FromSeconds(5), left.Value - OperationsHarness.WatchdogMargin);
        Assert.True(left.Value <= HangWatchdogAttribute.TestLimit);
    }

    /// <summary>A writer that keeps the first line written to it.</summary>
    private sealed class FirstLine : StringWriter
    {
        public TaskCompletionSource<string> Written { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void WriteLine(string? value) => Written.TrySetResult(value ?? string.Empty);
    }

    /// <summary>The real repository, except that its status reads wait until the gate opens.</summary>
    internal class HeldStatusReads : DispatchProxy
    {
        private IGitRepository _inner = null!;
        private Task _gate = Task.CompletedTask;

        public static IGitRepository Wrap(IGitRepository inner, Task gate)
        {
            var proxy = Create<IGitRepository, HeldStatusReads>();
            var held = (HeldStatusReads)(object)proxy;
            held._inner = inner;
            held._gate = gate;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IGitRepository.ReadStatusAsync))
            {
                return ReadWhenLetGoAsync(args is [CancellationToken token] ? token : default);
            }

            try
            {
                return targetMethod.Invoke(_inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Throw(ex.InnerException);
                throw;
            }
        }

        private async Task<WorkingTreeStatus> ReadWhenLetGoAsync(CancellationToken cancellationToken)
        {
            await _gate;
            return await _inner.ReadStatusAsync(cancellationToken);
        }
    }
}
