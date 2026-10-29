using VisualCommit.Hosting.Tokens;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// The test harness's keychain (docs/phase-7a-design.md 2.5): tokens in memory that, unlike the
/// app's memory store, count as kept across restarts, so that one instance handed to two app
/// starts is the scripted restart. A check sets it unavailable, makes one save fail, holds the next
/// call or takes a token away behind the app's back. <see cref="Calls"/> names each call and its
/// key, never a token. Safe from any thread.
/// </summary>
public sealed class FakeTokenStore : ITokenStore
{
    /// <summary>The reason the probe gives while <see cref="Available"/> is false.</summary>
    public const string UnavailableReason = "The fake keychain is set unavailable.";

    private readonly Lock _gate = new();
    private readonly Dictionary<TokenKey, string> _tokens = [];
    private readonly List<string> _calls = [];
    private volatile bool _available = true;
    private string? _nextSaveFailure;
    private FakeTokenStoreHold? _hold;

    public string Name => "fake";

    public bool KeepsTokensAcrossRestarts => true;

    /// <summary>Whether the keychain is there: true at first. While false the probe says so and every save fails.</summary>
    public bool Available
    {
        get => _available;
        set => _available = value;
    }

    /// <summary>The calls so far, in order: <c>probe</c>, <c>save github:127.0.0.1:47395:octo</c>, <c>read …</c>, <c>delete …</c>.</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>Makes the next save fail once with <paramref name="reason"/>.</summary>
    public void FailNextSave(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (_gate)
        {
            _nextSaveFailure = reason;
        }
    }

    /// <summary>
    /// Holds the next call of any kind, as it arrives, until the hold is disposed; the hold's
    /// <see cref="FakeTokenStoreHold.Arrived"/> completes when a call waits. Calling it again before
    /// a call arrived gives the same hold.
    /// </summary>
    public FakeTokenStoreHold HoldNext()
    {
        lock (_gate)
        {
            return _hold ??= new FakeTokenStoreHold(this);
        }
    }

    /// <summary>Whether a token is kept under <paramref name="key"/>.</summary>
    public bool Holds(TokenKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            return _tokens.ContainsKey(key);
        }
    }

    /// <summary>Takes the token under <paramref name="key"/> away, as a user who deleted it from the keychain would, without a call.</summary>
    public void Remove(TokenKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            _tokens.Remove(key);
        }
    }

    public async Task<TokenStoreProbe> ProbeAsync(CancellationToken ct)
    {
        await ArriveAsync("probe", ct).ConfigureAwait(false);
        return _available ? new TokenStoreProbe(true, null) : new TokenStoreProbe(false, UnavailableReason);
    }

    public async Task<TokenStoreResult> SaveAsync(TokenKey key, string token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrEmpty(token);
        await ArriveAsync("save " + key, ct).ConfigureAwait(false);
        lock (_gate)
        {
            if (!_available)
            {
                return new TokenStoreResult(TokenStoreOutcome.Failed, UnavailableReason);
            }

            if (_nextSaveFailure is { } reason)
            {
                _nextSaveFailure = null;
                return new TokenStoreResult(TokenStoreOutcome.Failed, reason);
            }

            _tokens[key] = token;
            return new TokenStoreResult(TokenStoreOutcome.Done);
        }
    }

    public async Task<TokenRead> ReadAsync(TokenKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        await ArriveAsync("read " + key, ct).ConfigureAwait(false);
        lock (_gate)
        {
            return _tokens.TryGetValue(key, out var token)
                ? new TokenRead(TokenStoreOutcome.Done, token, null)
                : new TokenRead(TokenStoreOutcome.NotFound, null, null);
        }
    }

    public async Task<TokenStoreResult> DeleteAsync(TokenKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        await ArriveAsync("delete " + key, ct).ConfigureAwait(false);
        lock (_gate)
        {
            return new TokenStoreResult(_tokens.Remove(key) ? TokenStoreOutcome.Done : TokenStoreOutcome.NotFound);
        }
    }

    public override string ToString() => $"FakeTokenStore({(_available ? "available" : "unavailable")})";

    /// <summary>Records a call, then waits at the hold when one is set for it.</summary>
    private async Task ArriveAsync(string call, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        FakeTokenStoreHold? hold;
        lock (_gate)
        {
            _calls.Add(call);
            hold = _hold;
            _hold = null;
        }

        if (hold is not null)
        {
            hold.ArrivedSource.TrySetResult();
            await hold.Released.Task.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    internal void Drop(FakeTokenStoreHold hold)
    {
        lock (_gate)
        {
            if (_hold == hold)
            {
                _hold = null;
            }
        }
    }
}

/// <summary>A hold on a <see cref="FakeTokenStore"/>'s next call (<see cref="FakeTokenStore.HoldNext"/>).</summary>
public sealed class FakeTokenStoreHold : IDisposable
{
    private readonly FakeTokenStore _store;

    internal FakeTokenStoreHold(FakeTokenStore store) => _store = store;

    /// <summary>Completes when a call waits at the hold; cancelled when the hold is disposed first.</summary>
    public Task Arrived => ArrivedSource.Task;

    internal TaskCompletionSource ArrivedSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Lets the held call go on, or drops the hold when no call arrived.</summary>
    public void Dispose()
    {
        _store.Drop(this);
        ArrivedSource.TrySetCanceled();
        Released.TrySetResult();
    }
}
