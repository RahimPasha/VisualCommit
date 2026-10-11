using System.Diagnostics;
using System.Globalization;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Core.Operations;

/// <summary>
/// One repository's operation queue (phase 3 design 1, D81). It only schedules: it runs the
/// operations' delegates one at a time, pauses the watcher and refreshes through the delegates it
/// is given, and never starts a process itself. See <see cref="IOperationQueue"/> for the contract
/// and the design's section 1.3 for the steps of one operation.
/// <para>
/// Unlike the rest of Core, its awaits keep their synchronization context on purpose: an
/// operation and its refresh run on the context it was queued from, the UI thread, because
/// phase 2's writes change bound view-model state.
/// </para>
/// </summary>
public sealed class OperationQueue : IOperationQueue
{
    /// <summary>At most one progress update per this time reaches <see cref="Changed"/> (1.2).</summary>
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(100);

    private static long _lastId;

    private readonly string _repositoryName;
    private readonly Func<IDisposable?> _pauseWatcher;
    private readonly Func<RefreshScope, Task> _refresh;
    private readonly IGitPromptBroker? _prompts;
    private readonly IOperationLog? _operations;
    private readonly IAppLog _log;
    private readonly Func<DateTimeOffset, IReadOnlyList<string>> _findLockFiles;
    private readonly Lock _gate = new();
    private readonly LinkedList<Entry> _waiting = new();
    private readonly List<TaskCompletionSource> _idleWaiters = [];
    private Entry? _running;
    private OperationResult? _lastResult;
    private bool _disposed;

    /// <param name="repositoryName">The repository's name, for the log and the prompt dialogs.</param>
    /// <param name="gitDirectory">The repository's git folder, where a stopped git leaves its lock files.</param>
    /// <param name="pauseWatcher">Pauses the repository's watcher until the result is disposed (D71); may return null.</param>
    /// <param name="refresh">Reads the repository again after an operation, on the operation's synchronization context.</param>
    /// <param name="prompts">Opens the prompt scopes (section 3); null opens none, so every prompt fails at once.</param>
    /// <param name="operations">Where each ended operation is recorded (1.7); may be null.</param>
    /// <param name="log">The app's log.</param>
    /// <param name="findLockFiles">
    /// Lists the lock files written since a time, after a cancelled network operation (1.8). Null
    /// looks in <paramref name="gitDirectory"/> only; a linked worktree passes its common git folder too.
    /// </param>
    public OperationQueue(
        string repositoryName,
        string gitDirectory,
        Func<IDisposable?> pauseWatcher,
        Func<RefreshScope, Task> refresh,
        IGitPromptBroker? prompts,
        IOperationLog? operations,
        IAppLog log,
        Func<DateTimeOffset, IReadOnlyList<string>>? findLockFiles = null)
    {
        ArgumentNullException.ThrowIfNull(repositoryName);
        ArgumentNullException.ThrowIfNull(gitDirectory);
        ArgumentNullException.ThrowIfNull(pauseWatcher);
        ArgumentNullException.ThrowIfNull(refresh);
        ArgumentNullException.ThrowIfNull(log);
        _repositoryName = repositoryName;
        _pauseWatcher = pauseWatcher;
        _refresh = refresh;
        _prompts = prompts;
        _operations = operations;
        _log = log;
        _findLockFiles = findLockFiles ?? (since => LockFiles.FindSince([gitDirectory], since));
    }

    public event EventHandler? Changed;

    public event EventHandler<OperationFinishedEventArgs>? Finished;

    public OperationSnapshot? Current
    {
        get
        {
            lock (_gate)
            {
                return _running is { } entry ? Snapshot(entry) : null;
            }
        }
    }

    public int Waiting
    {
        get
        {
            lock (_gate)
            {
                return _waiting.Count;
            }
        }
    }

    public OperationResult? LastResult
    {
        get
        {
            lock (_gate)
            {
                return _lastResult;
            }
        }
    }

    private bool IsDisposed
    {
        get
        {
            lock (_gate)
            {
                return _disposed;
            }
        }
    }

    public Task<OperationResult> RunAsync(GitOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var context = SynchronizationContext.Current;
        Entry entry;
        bool startNow;
        lock (_gate)
        {
            if (_disposed)
            {
                return Task.FromResult(CancelledResult(operation));
            }

            if (operation.DedupeKey is { } key && FindByKey(key) is { } same)
            {
                return same.Completion.Task;
            }

            entry = new Entry(operation, context, NextId());
            startNow = _running is null;
            if (startNow)
            {
                _running = entry;
            }
            else
            {
                _waiting.AddLast(entry);
            }
        }

        RaiseChanged();
        if (startNow)
        {
            Begin(entry, inline: true);
        }

        return entry.Completion.Task;
    }

    public bool TryStartInBackground(GitOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var entry = new Entry(operation with { IsBackground = true }, SynchronizationContext.Current, NextId());
        lock (_gate)
        {
            if (_disposed || _running is not null || _waiting.Count > 0)
            {
                return false;
            }

            _running = entry;
        }

        RaiseChanged();
        Begin(entry, inline: true);
        return true;
    }

    public Task WhenIdleAsync()
    {
        lock (_gate)
        {
            if (_running is null && _waiting.Count == 0)
            {
                return Task.CompletedTask;
            }

            var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _idleWaiters.Add(idle);
            return idle.Task;
        }
    }

    public void CancelCurrent()
    {
        CancellationTokenSource? cancellation;
        lock (_gate)
        {
            if (_running is not { } entry || !CanCancel(entry))
            {
                return;
            }

            entry.CancelRequested = true;
            cancellation = entry.Cancellation;
        }

        _log.Info($"{_repositoryName}: cancelling the running operation.");
        cancellation?.Cancel();
        RaiseChanged();
    }

    public void DropWaiting()
    {
        List<Entry> dropped;
        List<TaskCompletionSource>? idle = null;
        lock (_gate)
        {
            if (_waiting.Count == 0)
            {
                return;
            }

            dropped = [.. _waiting];
            _waiting.Clear();
            if (_running is null)
            {
                idle = TakeIdleWaiters();
            }
        }

        foreach (var entry in dropped)
        {
            entry.Completion.TrySetResult(CancelledResult(entry.Operation));
        }

        RaiseChanged();
        idle?.ForEach(waiter => waiter.TrySetResult());
    }

    /// <summary>
    /// Closing the tab (1.8): the waiting operations end as cancelled without running; a running
    /// network operation that the user started is cancelled; a running local one goes on to its
    /// end, every step of it, without the refresh; a background one is left to finish. Every
    /// prompt scope of the queue is closed.
    /// </summary>
    public void Dispose()
    {
        List<Entry> dropped;
        Entry? running;
        List<TaskCompletionSource>? idle = null;
        CancellationTokenSource? cancel = null;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            dropped = [.. _waiting];
            _waiting.Clear();
            running = _running;
            if (running is null)
            {
                idle = TakeIdleWaiters();
            }
            else if (running.Cancellation is not null && !running.Operation.IsBackground)
            {
                running.CancelRequested = true;
                cancel = running.Cancellation;
            }
        }

        foreach (var entry in dropped)
        {
            entry.Completion.TrySetResult(CancelledResult(entry.Operation));
        }

        cancel?.Cancel();
        running?.Scope?.Dispose();
        RaiseChanged();
        idle?.ForEach(waiter => waiter.TrySetResult());
    }

    private static string NextId() =>
        "op-" + Interlocked.Increment(ref _lastId).ToString(CultureInfo.InvariantCulture);

    private static bool CanCancel(Entry entry) =>
        entry.Cancellation is not null && entry.CancelWindowOpen && !entry.CancelRequested;

    private static OperationSnapshot Snapshot(Entry entry) => new(
        entry.Operation.Title,
        entry.Operation.RunningText,
        entry.Operation.Kind,
        entry.Operation.IsBackground,
        entry.Operation.Errors,
        entry.Progress,
        CanCancel(entry));

    private static OperationResult CancelledResult(GitOperation operation) =>
        new(OperationOutcome.Cancelled, $"{operation.Title} cancelled", null);

    private static OperationResult FailedResult(GitOperation operation, GitFailure failure) =>
        new(OperationOutcome.Failed, $"{operation.Title} failed", failure);

    private static GitFailure FailureOf(GitOperation operation, string output) =>
        new(GitFailureKind.Other, $"{operation.Title} failed.", GitCommand.HideCredentials(output), string.Empty, null);

    private static GitFailure FailureOf(GitOperation operation, GitException exception)
    {
        var output = GitOutputCleaner.Clean(exception.StandardError, exception.StandardOutput);
        if (output.Length == 0)
        {
            // Nothing from git itself (it could not be started, or said nothing): the exception says what happened.
            output = GitCommand.HideCredentials(exception.Message);
        }

        return new GitFailure(
            GitFailureClassifier.Classify(exception.StandardError, exception.StandardOutput, exception.ExitCode),
            $"{operation.Title} failed.",
            output,
            exception.CommandText,
            exception.ExitCode);
    }

    /// <summary>A running (not background) or waiting operation with this dedupe key.</summary>
    private Entry? FindByKey(string key)
    {
        if (_running is { Operation.IsBackground: false } running && running.Operation.DedupeKey == key)
        {
            return running;
        }

        return _waiting.FirstOrDefault(entry => entry.Operation.DedupeKey == key);
    }

    private List<TaskCompletionSource> TakeIdleWaiters()
    {
        var idle = new List<TaskCompletionSource>(_idleWaiters);
        _idleWaiters.Clear();
        return idle;
    }

    /// <summary>
    /// Starts an operation on its synchronization context: at once when the caller is already on
    /// it (the operation that <see cref="RunAsync"/> starts, as phase 2's writes started at once),
    /// otherwise posted to it. The next operation in line is always posted, so a run of operations
    /// that complete at once does not grow the stack.
    /// </summary>
    private void Begin(Entry entry, bool inline)
    {
        if (entry.Context is { } context)
        {
            if (inline && SynchronizationContext.Current == context)
            {
                Observe(ExecuteAsync(entry));
            }
            else
            {
                context.Post(_ => Observe(ExecuteAsync(entry)), null);
            }
        }
        else if (inline)
        {
            Observe(ExecuteAsync(entry));
        }
        else
        {
            Observe(Task.Run(() => ExecuteAsync(entry)));
        }
    }

    private void Observe(Task task) =>
        _ = task.ContinueWith(
            failed => _log.Error($"{_repositoryName}: the operation queue failed.", failed.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    /// <summary>One operation, steps 3 to 8 of the design's section 1.3.</summary>
    private async Task ExecuteAsync(Entry entry)
    {
        var operation = entry.Operation;
        entry.StartedAt = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();
        _log.Debug($"{_repositoryName}: {operation.Title}: started");

        OperationResult result;
        try
        {
            result = await RunOperationAsync(entry);
        }
        catch (Exception ex)
        {
            // RunOperationAsync turns the operation's exceptions into results; this is the queue's own.
            _log.Error($"{_repositoryName}: {operation.Title}: the queue failed while running it.", ex);
            result = FailedResult(operation, FailureOf(operation, "Unexpected error: " + ex.Message));
        }

        if (result.Outcome == OperationOutcome.Cancelled && operation.Kind == OperationKind.Network && !operation.IsBackground)
        {
            result = ReportStaleLocks(operation, entry.StartedAt, result);
        }

        if (!IsDisposed && operation.Refresh != RefreshScope.None)
        {
            try
            {
                await _refresh(operation.Refresh);
            }
            catch (Exception ex)
            {
                _log.Error($"{_repositoryName}: reading the repository after {operation.Title} failed.", ex);
            }
        }

        stopwatch.Stop();
        Record(entry, result, stopwatch.Elapsed);
        Finish(entry, result);
    }

    /// <summary>Steps 3 to 6: the prompt scope, the watcher's pause, the delegate, its outcome.</summary>
    private async Task<OperationResult> RunOperationAsync(Entry entry)
    {
        var operation = entry.Operation;
        var mode = operation.Prompts ?? (operation.IsBackground ? PromptMode.Deny : PromptMode.Ask);
        PromptScope? scope = null;
        OperationResult result;
        try
        {
            if (mode != PromptMode.None && _prompts is not null)
            {
                scope = _prompts.Begin(operation.Title, _repositoryName, mode, entry.Id);
                bool disposed;
                lock (_gate)
                {
                    entry.Scope = scope;
                    disposed = _disposed;
                }

                if (disposed)
                {
                    scope.Dispose();
                }
            }

            var context = new OperationContext(
                entry.Id,
                entry.Cancellation?.Token ?? CancellationToken.None,
                scope?.Environment,
                new ProgressSink(this, entry),
                () => EndCancelWindow(entry));

            OperationReport report;
            using (GitOperationScope.Enter(entry.Id))
            using (_pauseWatcher())
            {
                if (entry.CancelRequested)
                {
                    throw new OperationCanceledException();
                }

                report = await operation.Run(context)
                    ?? throw new InvalidOperationException($"The operation {operation.Title} returned no report.");
            }

            result = report.Outcome switch
            {
                OperationOutcome.Failed => new OperationResult(
                    OperationOutcome.Failed,
                    report.Message.Length > 0 ? report.Message : $"{operation.Title} failed",
                    report.Failure ?? FailureOf(operation, report.Message)),
                OperationOutcome.Cancelled => CancelledResult(operation),
                _ => new OperationResult(report.Outcome, report.Message, null),
            };
        }
        catch (OperationCanceledException)
        {
            result = CancelledResult(operation);
        }
        catch (GitException ex)
        {
            result = FailedResult(operation, FailureOf(operation, ex));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            result = FailedResult(operation, FailureOf(operation, ex.Message));
        }
        catch (Exception ex)
        {
            // The queue never dies with its operation.
            _log.Error($"{_repositoryName}: {operation.Title}: unexpected error.", ex);
            result = FailedResult(operation, FailureOf(operation, "Unexpected error: " + ex.Message));
        }
        finally
        {
            scope?.Dispose();
        }

        // A prompt the user cancelled makes git fail; that is a cancel, not an error (2.16).
        if (scope is { UserCancelled: true } && result.Outcome == OperationOutcome.Failed)
        {
            result = CancelledResult(operation);
        }

        return result;
    }

    /// <summary>
    /// After a cancelled network operation (1.8): the lock files git left go with the result, for
    /// the "&lt;Title&gt; cancelled" dialog, or to the log when the tab closed meanwhile.
    /// </summary>
    private OperationResult ReportStaleLocks(GitOperation operation, DateTimeOffset since, OperationResult result)
    {
        IReadOnlyList<string> locks;
        try
        {
            locks = _findLockFiles(since);
        }
        catch (Exception ex)
        {
            _log.Warning($"{_repositoryName}: looking for lock files after {operation.Title} failed.", ex);
            return result;
        }

        if (locks.Count == 0)
        {
            return result;
        }

        if (IsDisposed)
        {
            _log.Warning($"{_repositoryName}: {operation.Title} was stopped and left these lock files behind: {string.Join(", ", locks)}");
            return result;
        }

        return result with { StaleLockFiles = locks };
    }

    private bool EndCancelWindow(Entry entry)
    {
        bool cancelled;
        lock (_gate)
        {
            entry.CancelWindowOpen = false;
            cancelled = entry.CancelRequested;
        }

        RaiseChanged();
        return cancelled;
    }

    private void Record(Entry entry, OperationResult result, TimeSpan duration)
    {
        var operation = entry.Operation;
        var milliseconds = ((long)duration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        if (result.Outcome == OperationOutcome.Failed)
        {
            var firstLine = result.Failure?.GitOutput.Split('\n')[0] ?? result.Message;
            _log.Warning($"{_repositoryName}: {operation.Title}: {result.Outcome} after {milliseconds} ms: {firstLine}");
        }
        else
        {
            _log.Info($"{_repositoryName}: {operation.Title}: {result.Outcome} after {milliseconds} ms: {result.Message}");
        }

        try
        {
            _operations?.Add(new OperationRecord(entry.Id, _repositoryName, operation.Title, entry.StartedAt, duration, result.Outcome, result.Message));
        }
        catch (Exception ex)
        {
            _log.Error($"{_repositoryName}: recording {operation.Title} failed.", ex);
        }
    }

    /// <summary>Step 8: the operation is no longer current, its result is kept, its task completes and the next one starts.</summary>
    private void Finish(Entry entry, OperationResult result)
    {
        Entry? next = null;
        List<TaskCompletionSource>? idle = null;
        lock (_gate)
        {
            if (ReferenceEquals(_running, entry))
            {
                _running = null;
            }

            if (!entry.Operation.IsBackground)
            {
                _lastResult = result;
            }

            if (!_disposed && _waiting.First is { } first)
            {
                _waiting.RemoveFirst();
                next = first.Value;
                _running = next;
            }
            else if (_running is null && _waiting.Count == 0)
            {
                idle = TakeIdleWaiters();
            }
        }

        try
        {
            Finished?.Invoke(this, new OperationFinishedEventArgs(entry.Operation, result));
        }
        catch (Exception ex)
        {
            _log.Error($"{_repositoryName}: a listener of the operation queue failed.", ex);
        }

        RaiseChanged();
        entry.Completion.TrySetResult(result);
        idle?.ForEach(waiter => waiter.TrySetResult());
        if (next is not null)
        {
            Begin(next, inline: false);
        }
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _log.Error($"{_repositoryName}: a listener of the operation queue failed.", ex);
        }
    }

    /// <summary>
    /// Takes an operation's progress from any thread and lets <see cref="Changed"/> know at most
    /// once per <see cref="ProgressInterval"/>; the last update of a burst is not lost.
    /// </summary>
    private sealed class ProgressSink(OperationQueue queue, Entry entry) : IProgress<OperationProgress>
    {
        private long _lastRaised;
        private bool _pending;

        public void Report(OperationProgress value)
        {
            ArgumentNullException.ThrowIfNull(value);
            TimeSpan wait;
            lock (queue._gate)
            {
                if (!ReferenceEquals(queue._running, entry))
                {
                    return;
                }

                entry.Progress = value;
                if (_pending)
                {
                    return;
                }

                var since = _lastRaised == 0 ? TimeSpan.MaxValue : Stopwatch.GetElapsedTime(_lastRaised);
                wait = since >= ProgressInterval ? TimeSpan.Zero : ProgressInterval - since;
                if (wait > TimeSpan.Zero)
                {
                    _pending = true;
                }
                else
                {
                    _lastRaised = Stopwatch.GetTimestamp();
                }
            }

            if (wait == TimeSpan.Zero)
            {
                queue.RaiseChanged();
                return;
            }

            _ = Task.Delay(wait, CancellationToken.None).ContinueWith(
                _ =>
                {
                    lock (queue._gate)
                    {
                        _pending = false;
                        _lastRaised = Stopwatch.GetTimestamp();
                    }

                    queue.RaiseChanged();
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    private sealed class Entry(GitOperation operation, SynchronizationContext? context, string id)
    {
        public GitOperation Operation { get; } = operation;

        /// <summary>Where the operation runs: the synchronization context of the call that queued it.</summary>
        public SynchronizationContext? Context { get; } = context;

        public string Id { get; } = id;

        public TaskCompletionSource<OperationResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>For a network operation the user started; null for a local or background one, which is never cancelled.</summary>
        public CancellationTokenSource? Cancellation { get; } =
            operation.Kind == OperationKind.Network && !operation.IsBackground ? new CancellationTokenSource() : null;

        public bool CancelWindowOpen { get; set; } = true;

        public bool CancelRequested { get; set; }

        public OperationProgress? Progress { get; set; }

        public PromptScope? Scope { get; set; }

        public DateTimeOffset StartedAt { get; set; }
    }
}
