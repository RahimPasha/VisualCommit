using System.Collections.Concurrent;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// One thread with a <see cref="SynchronizationContext"/> of its own (docs/phase-7a-design.md
/// 7.13): the hosting session's post delegate in the tests, and the thread where they call what
/// the app calls on its UI thread (<c>Update</c>, the refreshes). An awaited task's continuation
/// comes back to it, as on Avalonia's UI thread. An exception of a posted action is kept in
/// <see cref="Errors"/> and the thread goes on.
/// </summary>
internal sealed class HostingTestThread : IDisposable
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

    private readonly BlockingCollection<Action> _queue = [];
    private readonly ConcurrentQueue<Exception> _errors = new();
    private readonly Thread _thread;

    public HostingTestThread()
    {
        var context = new Context(this);
        _thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            foreach (var action in _queue.GetConsumingEnumerable())
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _errors.Enqueue(ex);
                }
            }
        })
        {
            IsBackground = true,
            Name = "hosting test thread",
        };
        _thread.Start();
    }

    /// <summary>What the posted actions threw, in order.</summary>
    public IReadOnlyList<Exception> Errors => [.. _errors];

    /// <summary>Whether the caller runs on this thread.</summary>
    public bool IsCurrent => Thread.CurrentThread == _thread;

    /// <summary>Runs <paramref name="action"/> on the thread, after everything posted before it; dropped once disposed.</summary>
    public void Post(Action action)
    {
        try
        {
            _queue.Add(action);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // Disposed: nothing runs any more.
        }
    }

    /// <summary>Runs <paramref name="function"/> on the thread and gives its result.</summary>
    public Task<T> RunAsync<T>(Func<T> function)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try
            {
                done.SetResult(function());
            }
            catch (Exception ex)
            {
                done.SetException(ex);
            }
        });
        return done.Task;
    }

    /// <summary>Runs <paramref name="action"/> on the thread.</summary>
    public Task RunAsync(Action action) => RunAsync(() =>
    {
        action();
        return true;
    });

    /// <summary>Starts <paramref name="function"/> on the thread, whose awaits come back to it, and gives its task.</summary>
    public async Task<T> RunAsync<T>(Func<Task<T>> function) => await (await RunAsync<Task<T>>(function));

    /// <summary>Starts <paramref name="function"/> on the thread, whose awaits come back to it, and gives its task.</summary>
    public async Task RunAsync(Func<Task> function) => await (await RunAsync<Task>(function));

    /// <summary>Everything posted so far has run.</summary>
    public Task IdleAsync() => RunAsync(() => { });

    /// <summary>Waits until <paramref name="condition"/>, read on the thread, holds; fails after 30 s naming <paramref name="what"/>.</summary>
    public async Task WaitForAsync(Func<bool> condition, string what)
    {
        var limit = DateTime.UtcNow + WaitLimit;
        while (!await RunAsync(condition))
        {
            if (DateTime.UtcNow > limit)
            {
                throw new TimeoutException($"Still waiting after 30 s for {what}.");
            }

            await Task.Delay(10);
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(10));
        _queue.Dispose();
    }

    private sealed class Context(HostingTestThread owner) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => owner.Post(() => d(state));

        public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException("The hosting test thread is only posted to.");

        public override SynchronizationContext CreateCopy() => this;
    }
}
