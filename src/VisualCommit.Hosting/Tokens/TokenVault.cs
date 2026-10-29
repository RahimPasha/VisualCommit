using VisualCommit.Core.Logging;

namespace VisualCommit.Hosting.Tokens;

/// <summary>
/// Where the hosting session keeps tokens (docs/phase-7a-design.md 3.7, D272): this computer's
/// keychain when it can be reached and the save works, else memory for the session (D253, rulings
/// 18, 19). Memory is asked first; the store is probed once per run and read at most once per key
/// and run (ruling 6). Once a save, a read or a delete has started, its caller's token no longer
/// stops it, so no entry is left half-written and no second prompt of the system follows; only the
/// store's own limits end it. A key's keeps and forgets reach the store in the order they were
/// called, and a store read waits for the one before it, so memory and the keychain end alike.
/// </summary>
public sealed class TokenVault
{
    private readonly ITokenStore _store;
    private readonly IAppLog _log;
    private readonly Lazy<Task<TokenStoreProbe>> _probe;
    private readonly Lock _lock = new();

    // All under _lock. A read's task is kept for the run, whatever its outcome. A key's last keep
    // or forget ends when its store call has ended (it never faults); the key's next keep, forget
    // or store read waits for it.
    private readonly Dictionary<TokenKey, string> _memory = [];
    private readonly Dictionary<TokenKey, Task<TokenRead>> _reads = [];
    private readonly Dictionary<TokenKey, Task> _changes = [];

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
    /// keychain when one can be reached, after the key's earlier keep or forget. A failed save keeps
    /// the token for the session with its reason and leaves the store alone (an older entry may
    /// stay; its token is refused at first use). <paramref name="ct"/> is checked first and stops
    /// nothing after that.
    /// </summary>
    public async Task<(TokenPlace Place, string? SaveFailedReason)> KeepAsync(TokenKey key, string token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrEmpty(token);
        ct.ThrowIfCancellationRequested();
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? earlier;
        lock (_lock)
        {
            _memory[key] = token;
            _reads.Remove(key);
            earlier = Change(key, ended.Task);
        }

        var reason = await AfterAsync(earlier, ended, () => SaveAsync(key, token)).ConfigureAwait(false);
        if (reason is null)
        {
            _log.Debug($"Token for {TokenEntryNames.ForLog(key)} kept in the keychain.");
            return (TokenPlace.Keychain, null);
        }

        _log.Info($"Token for {TokenEntryNames.ForLog(key)} kept for this session: {reason}.");
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
        Task? earlier = null;
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
                _changes.TryGetValue(key, out earlier);
            }
        }

        if (started is not null)
        {
            _ = ReadStoreAsync(key, started, earlier);
        }

        return read.WaitAsync(ct);
    }

    /// <summary>
    /// Forgets <paramref name="key"/>'s token: memory and the read at once, then the keychain's
    /// entry, after the key's earlier keep or forget. Null, or the reason the keychain kept its
    /// entry (logged with the entry's name for whoever removes it by hand). Without a keychain
    /// there is nothing to delete: null. <paramref name="ct"/> ends the wait, not the delete.
    /// </summary>
    public Task<string?> ForgetAsync(TokenKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<string?>(ct);
        }

        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? earlier;
        lock (_lock)
        {
            _memory.Remove(key);
            _reads.Remove(key);
            earlier = Change(key, ended.Task);
        }

        return AfterAsync(earlier, ended, () => DeleteAsync(key)).WaitAsync(ct);
    }

    // Under _lock: makes `next` the key's last keep or forget and gives the one before it, if any.
    private Task? Change(TokenKey key, Task next)
    {
        _changes.TryGetValue(key, out var earlier);
        _changes[key] = next;
        return earlier;
    }

    // A keep's or a forget's store call, made once the key's earlier one has ended; `ended` is set
    // whatever happens, so the next one never waits for ever.
    private static async Task<string?> AfterAsync(Task? earlier, TaskCompletionSource ended, Func<Task<string?>> call)
    {
        try
        {
            if (earlier is not null)
            {
                await earlier.ConfigureAwait(false);
            }

            return await call().ConfigureAwait(false);
        }
        finally
        {
            ended.SetResult();
        }
    }

    // The save's reason, or null when the keychain keeps the token.
    private async Task<string?> SaveAsync(TokenKey key, string token)
    {
        if (!(await _probe.Value.ConfigureAwait(false)).Available)
        {
            return TokenStoreReasons.NoKeychain;
        }

        TokenStoreResult result;
        try
        {
            result = await _store.SaveAsync(key, token, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _log.Warning($"{_store.Name}: saving the token for {TokenEntryNames.ForLog(key)} failed.", exception);
            result = new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused);
        }

        return result.Outcome == TokenStoreOutcome.Done ? null : result.Reason ?? TokenStoreReasons.Refused;
    }

    // The reason the keychain kept the entry, or null.
    private async Task<string?> DeleteAsync(TokenKey key)
    {
        if (!(await _probe.Value.ConfigureAwait(false)).Available)
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
            _log.Warning($"{_store.Name}: deleting the token for {TokenEntryNames.ForLog(key)} failed.", exception);
            result = new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused);
        }

        if (result.Outcome != TokenStoreOutcome.Failed)
        {
            return null;
        }

        var reason = result.Reason ?? TokenStoreReasons.Refused;
        var entry = TokenEntryNames.TryFor(key, TokenEntryNames.Application, out var names) ? names.Entry : TokenEntryNames.ForLog(key);
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

    private async Task ReadStoreAsync(TokenKey key, TaskCompletionSource<TokenRead> started, Task? earlier)
    {
        // A keep or a forget still in the store: its save or delete comes first, so a read never
        // finds an entry that a forget called before it is about to delete.
        if (earlier is not null)
        {
            await earlier.ConfigureAwait(false);
        }

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
                _log.Warning($"{_store.Name}: reading the token for {TokenEntryNames.ForLog(key)} failed.", exception);
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
