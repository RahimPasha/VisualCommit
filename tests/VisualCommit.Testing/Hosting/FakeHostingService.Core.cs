using System.Collections.Concurrent;
using VisualCommit.Core.Git;
using VisualCommit.Git;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// What every fake of a hosting service shares and no provider owns (docs/phase-7b-design.md 2.3,
/// D363), taken out of 7a's <c>FakeGitHub</c>: the lock its state is kept under, the isolated home
/// and the <see cref="GitRunner"/> its git runs through, holds (at most <see cref="HoldLimit"/>),
/// faults taken once per route, the request log, <see cref="Errors"/> (with the server's), the
/// per-repository locks, the start of its <see cref="AuthRemoteServer"/> in host mode, and a dispose
/// that releases the holds first, those a request waits at included. <see cref="FakeGitHub"/> keeps its tables and types over one, and
/// <see cref="FakeHostingService"/> is 1.9's surface over another.
/// </summary>
/// <typeparam name="TFault">The fake's fault type.</typeparam>
/// <typeparam name="TRequest">The fake's record of a request it answered.</typeparam>
internal sealed class FakeServiceCore<TFault, TRequest> : IAsyncDisposable
    where TFault : class
{
    /// <summary>How long a held request waits at most before it goes on with an <see cref="Errors"/> line.</summary>
    public static readonly TimeSpan HoldLimit = TimeSpan.FromSeconds(120);

    private readonly TempDirectory _home;
    private readonly Dictionary<string, PendingHold> _holds = new(StringComparer.Ordinal);
    private readonly HashSet<PendingHold> _waiting = [];
    private readonly Dictionary<string, TFault> _faults = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _repositoryLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TRequest> _requests = [];
    private readonly List<string> _errors = [];
    private int _disposed;

    /// <summary>A core on <paramref name="root"/> (made when missing), its git's home in a temporary folder named after <paramref name="homeName"/>.</summary>
    public FakeServiceCore(string root, string homeName)
    {
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        Directory.CreateDirectory(Root);
        _home = new TempDirectory(homeName);
        Runner = new GitRunner(TempRepo.GitPath);
        Environment = TempRepo.IsolatedEnvironment(_home.Combine("home"));
    }

    /// <summary>The lock every table of the fake is kept under; git never runs inside it.</summary>
    public Lock Gate { get; } = new();

    /// <summary>The folder of the fake's bare repositories.</summary>
    public string Root { get; }

    /// <summary>The runner of the fake's own git calls.</summary>
    public GitRunner Runner { get; }

    /// <summary>The environment of the fake's git calls, isolated from the machine (<see cref="TempRepo.IsolatedEnvironment"/>).</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; }

    /// <summary>The server in host mode once started; null in process.</summary>
    public AuthRemoteServer? Server { get; private set; }

    /// <summary>Starts the server on <see cref="Root"/> in host mode with <paramref name="options"/>, as <see cref="AuthRemoteServer.StartForHostAsync"/> does.</summary>
    public async Task<AuthRemoteServer> StartServerAsync(AuthRemoteHostOptions options, int port, bool fallBackToFreePort)
    {
        var server = await AuthRemoteServer.StartForHostAsync(Root, options, port, fallBackToFreePort).ConfigureAwait(false);
        Server = server;
        return server;
    }

    /// <summary>Every request recorded so far, in order. Safe from another thread.</summary>
    public IReadOnlyList<TRequest> Requests
    {
        get
        {
            lock (Gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>What went wrong in the fake or its server: empty when all is well.</summary>
    public IReadOnlyList<string> Errors
    {
        get
        {
            lock (Gate)
            {
                return [.. _errors, .. Server?.Errors ?? []];
            }
        }
    }

    /// <summary>Records a request the fake answered.</summary>
    public void Record(TRequest request)
    {
        lock (Gate)
        {
            _requests.Add(request);
        }
    }

    /// <summary>Records what went wrong, for <see cref="Errors"/>.</summary>
    public void RecordError(string error)
    {
        lock (Gate)
        {
            _errors.Add(error);
        }
    }

    /// <summary>Answers the next request of <paramref name="key"/> with <paramref name="fault"/> once (the last one set wins).</summary>
    public void SetFault(string key, TFault fault)
    {
        lock (Gate)
        {
            _faults[key] = fault;
        }
    }

    /// <summary>The fault waiting for <paramref name="key"/>, taken; null when none.</summary>
    public TFault? TakeFault(string key)
    {
        lock (Gate)
        {
            _faults.Remove(key, out var fault);
            return fault;
        }
    }

    /// <summary>
    /// Holds the next request of <paramref name="key"/> (<see cref="WaitAtHoldAsync"/>): its tasks and
    /// the release, which lets a waiting request go on or drops the hold when none arrived.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The fake is disposed (named by <paramref name="owner"/>).</exception>
    /// <exception cref="InvalidOperationException">The route is held already.</exception>
    public HoldHandle AddHold(string key, object owner)
    {
        var hold = new PendingHold();
        lock (Gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, owner);
            if (!_holds.TryAdd(key, hold))
            {
                throw new InvalidOperationException($"The route {key} is held already.");
            }
        }

        return new HoldHandle(hold.Arrived.Task, hold.Abandoned.Task, hold.Released.Task, () =>
        {
            lock (Gate)
            {
                if (_holds.TryGetValue(key, out var pending) && pending == hold)
                {
                    _holds.Remove(key);
                }
            }

            hold.Arrived.TrySetCanceled();
            hold.Released.TrySetResult();
        });
    }

    /// <summary>
    /// Waits at the hold on <paramref name="key"/> when there is one, until it is released or the
    /// fake is disposed, at most <see cref="HoldLimit"/> (then the request goes on with an
    /// <see cref="Errors"/> line). A wait its sender cancels (in process) completes the hold's
    /// <c>Abandoned</c> and throws.
    /// </summary>
    public async Task WaitAtHoldAsync(string key, CancellationToken ct)
    {
        PendingHold? hold;
        lock (Gate)
        {
            if (_holds.Remove(key, out hold))
            {
                _waiting.Add(hold);
            }
        }

        if (hold is null)
        {
            return;
        }

        hold.Arrived.TrySetResult();
        try
        {
            await hold.Released.Task.WaitAsync(HoldLimit, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            RecordError($"The hold on {key} was not released within {HoldLimit.TotalSeconds:F0} s; the request went on.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Given up by its sender in process (over a server ct is only the server's stop): neither
            // answered nor recorded, so a test waiting for this looks at once.
            hold.Abandoned.TrySetResult();
            throw;
        }
        finally
        {
            lock (Gate)
            {
                _waiting.Remove(hold);
            }
        }
    }

    /// <summary>The lock of one repository's ref sync and creates (<paramref name="repository"/> without case), so that two never interleave.</summary>
    public SemaphoreSlim LockOf(string repository) => _repositoryLocks.GetOrAdd(repository, _ => new SemaphoreSlim(1, 1));

    /// <summary>
    /// Runs git in <paramref name="folder"/>, isolated from the machine, with <paramref name="environment"/>
    /// over <see cref="Environment"/> (a null value removes a variable): commit-tree's names and dates.
    /// </summary>
    public Task<GitResult> GitAsync(string folder, string? input, IReadOnlyDictionary<string, string?>? environment, CancellationToken ct, params string[] arguments) =>
        Runner.RunAsync(Command(folder, input, environment, arguments), ct);

    /// <summary><see cref="GitAsync"/> that must succeed; its standard output.</summary>
    public async Task<string> GitOkAsync(string folder, string? input, IReadOnlyDictionary<string, string?>? environment, CancellationToken ct, params string[] arguments)
    {
        var command = Command(folder, input, environment, arguments);
        return (await Runner.RunAsync(command, ct).ConfigureAwait(false)).EnsureSuccess(command).StandardOutput;
    }

    /// <summary>
    /// Releases every hold, a request waiting at one going on (over TCP into a server that then stops,
    /// so it may be cut off), then stops the server and deletes the home. The repositories stay.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        List<PendingHold> holds;
        lock (Gate)
        {
            // Under the lock, so that no request takes a hold between the flag and the lists.
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            holds = [.. _holds.Values, .. _waiting];
            _holds.Clear();
        }

        foreach (var hold in holds)
        {
            hold.Arrived.TrySetCanceled();
            hold.Released.TrySetResult();
        }

        if (Server is not null)
        {
            await Server.DisposeAsync().ConfigureAwait(false);
        }

        _home.Dispose();
    }

    private GitCommand Command(string folder, string? input, IReadOnlyDictionary<string, string?>? environment, string[] arguments)
    {
        IReadOnlyDictionary<string, string?> merged = Environment;
        if (environment is not null)
        {
            var combined = new Dictionary<string, string?>(Environment);
            foreach (var (name, value) in environment)
            {
                combined[name] = value;
            }

            merged = combined;
        }

        return new GitCommand(arguments) { WorkingDirectory = folder, Environment = merged, StandardInput = input };
    }

    /// <summary>A hold that no request has reached yet, or the one a request waits at.</summary>
    private sealed class PendingHold
    {
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completed when the wait here is cancelled before its release: by its sender in process, by the server's stop over TCP.</summary>
        public TaskCompletionSource Abandoned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

/// <summary>A hold <see cref="FakeServiceCore{TFault, TRequest}.AddHold"/> made: its tasks and its release.</summary>
internal readonly record struct HoldHandle(Task Arrived, Task Abandoned, Task Released, Action Release);
