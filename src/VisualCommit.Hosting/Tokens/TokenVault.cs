using VisualCommit.Core.Logging;

namespace VisualCommit.Hosting.Tokens;

/// <summary>
/// Where the hosting session keeps tokens (docs/phase-7a-design.md 3.7, D272): this computer's
/// keychain when it can be reached and the save works, else memory for the session (D253, rulings
/// 18, 19). Memory is asked first; the store is probed once per run and read at most once per key
/// and run (ruling 6). Once a save, a read or a delete has started, its caller's token no longer
/// stops it, so no entry is left half-written and no second prompt of the system follows; only the
/// store's own limits end it.
/// </summary>
public sealed class TokenVault
{
    private readonly ITokenStore _store;
    private readonly IAppLog _log;
    private readonly Lazy<Task<TokenStoreProbe>> _probe;
    private readonly Lock _lock = new();

    // Both under _lock. A read's task is kept for the run, whatever its outcome.
    private readonly Dictionary<TokenKey, string> _memory = [];
    private readonly Dictionary<TokenKey, Task<TokenRead>> _reads = [];

    public TokenVault(ITokenStore store, IAppLog log)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(log);
        _store = store;
        _log = log;
        _probe = new Lazy<Task<TokenStoreProbe>>(ProbeOnceAsync);
    }

    /// <summary>Whether the store can keep tokens: its probe, made once per run without the caller's token.</summary>
    public async Task<bool> KeychainAvailableAsync(CancellationToken ct) =>
        (await _probe.Value.WaitAsync(ct).ConfigureAwait(false)).Available;

    /// <summary>
    /// Keeps <paramref name="token"/> for <paramref name="key"/>: in memory at once, then in the
    /// keychain when one can be reached. A failed save keeps the token for the session with its
    /// reason and leaves the store alone (an older entry may stay; its token is refused at first use).
    /// <paramref name="ct"/> is checked first and stops nothing after that.
    /// </summary>
    public async Task<(TokenPlace Place, string? SaveFailedReason)> KeepAsync(TokenKey key, string token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrEmpty(token);
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _memory[key] = token;
            _reads.Remove(key);
        }

        string? reason;
        if (!(await _probe.Value.ConfigureAwait(false)).Available)
        {
            reason = TokenStoreReasons.NoKeychain;
        }
        else
        {
            TokenStoreResult result;
            try
            {
                result = await _store.SaveAsync(key, token, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _log.Warning($"{_store.Name}: saving the token for {key.Host}/{key.Account} failed.", exception);
                result = new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused);
            }

            reason = result.Outcome == TokenStoreOutcome.Done ? null : result.Reason ?? TokenStoreReasons.Refused;
        }

        if (reason is null)
        {
            _log.Debug($"Token for {key.Host}/{key.Account} kept in the keychain.");
            return (TokenPlace.Keychain, null);
        }

        _log.Info($"Token for {key.Host}/{key.Account} kept for this session: {reason}.");
        return (TokenPlace.Session, reason);
    }

    /// <summary>
    /// <paramref name="key"/>'s token: from memory, else from the store's one read of this run,
    /// which concurrent callers share and which is kept whatever it found. <paramref name="ct"/>
    /// ends the wait, not the read.
    /// </summary>
    public Task<TokenRead> ReadAsync(TokenKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<TokenRead>(ct);
        }

        Task<TokenRead>? read;
        TaskCompletionSource<TokenRead>? started = null;
        lock (_lock)
        {
            if (_memory.TryGetValue(key, out var token))
            {
                return Task.FromResult(new TokenRead(TokenStoreOutcome.Done, token, null));
            }

            if (!_reads.TryGetValue(key, out read))
            {
                started = new TaskCompletionSource<TokenRead>(TaskCreationOptions.RunContinuationsAsynchronously);
                read = started.Task;
                _reads[key] = read;
            }
        }

        if (started is not null)
        {
            _ = ReadStoreAsync(key, started);
        }

        return read.WaitAsync(ct);
    }

    /// <summary>
    /// Forgets <paramref name="key"/>'s token: memory and the read at once, then the keychain's
    /// entry. Null, or the reason the keychain kept its entry (logged with the entry's name for
    /// whoever removes it by hand). Without a keychain there is nothing to delete: null.
    /// </summary>
    public async Task<string?> ForgetAsync(TokenKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _memory.Remove(key);
            _reads.Remove(key);
        }

        if (!(await _probe.Value.WaitAsync(ct).ConfigureAwait(false)).Available)
        {
            return null;
        }

        TokenStoreResult result;
        try
        {
            result = await _store.DeleteAsync(key, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _log.Warning($"{_store.Name}: deleting the token for {key.Host}/{key.Account} failed.", exception);
            result = new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused);
        }

        if (result.Outcome != TokenStoreOutcome.Failed)
        {
            return null;
        }

        var reason = result.Reason ?? TokenStoreReasons.Refused;
        var entry = TokenEntryNames.TryFor(key, TokenEntryNames.Application, out var names) ? names.Entry : key.ToString();
        _log.Warning($"The keychain kept the entry {entry}: {reason}. Remove it by hand.");
        return reason;
    }

    private async Task<TokenStoreProbe> ProbeOnceAsync()
    {
        TokenStoreProbe probe;
        try
        {
            probe = await _store.ProbeAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _log.Warning($"{_store.Name}: the probe failed.", exception);
            probe = new TokenStoreProbe(false, TokenStoreReasons.NoKeychain);
        }

        _log.Info(probe.Available ? "Keychain: available." : $"Keychain: not available ({probe.Reason ?? TokenStoreReasons.NoKeychain}).");
        return probe;
    }

    private async Task ReadStoreAsync(TokenKey key, TaskCompletionSource<TokenRead> started)
    {
        TokenRead read;
        if (!(await _probe.Value.ConfigureAwait(false)).Available)
        {
            read = new TokenRead(TokenStoreOutcome.Failed, null, TokenStoreReasons.NoKeychain);
        }
        else
        {
            try
            {
                read = await _store.ReadAsync(key, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _log.Warning($"{_store.Name}: reading the token for {key.Host}/{key.Account} failed.", exception);
                read = new TokenRead(TokenStoreOutcome.Failed, null, TokenStoreReasons.Refused);
            }
        }

        if (read.Outcome == TokenStoreOutcome.Done && read.Token is { Length: > 0 } token)
        {
            lock (_lock)
            {
                // A keep or a forget since this read began dropped its record: its token is older.
                if (_reads.TryGetValue(key, out var current) && current == started.Task)
                {
                    _memory[key] = token;
                }
            }
        }

        started.SetResult(read);
    }
}
