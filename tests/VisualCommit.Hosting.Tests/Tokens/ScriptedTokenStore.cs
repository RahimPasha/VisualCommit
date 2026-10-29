using VisualCommit.Hosting.Tokens;

namespace VisualCommit.Hosting.Tests.Tokens;

/// <summary>
/// A token store whose answers a test sets and whose calls it can hold (<c>TokenVaultTests</c>):
/// it keeps what it saves, records every call with whether its token could still stop it, and
/// touches no keychain.
/// </summary>
internal sealed class ScriptedTokenStore : ITokenStore
{
    private readonly Lock _lock = new();
    private readonly Dictionary<TokenKey, string> _entries = [];
    private readonly List<(string Call, TokenKey? Key, bool Stoppable)> _calls = [];

    public string Name => "scripted";

    public bool KeepsTokensAcrossRestarts => true;

    public TokenStoreProbe Probe { get; set; } = new(true, null);

    /// <summary>A save's answer; null: keep the token and answer <see cref="TokenStoreOutcome.Done"/>.</summary>
    public TokenStoreResult? SaveAnswer { get; set; }

    /// <summary>A read's answer; null: the kept token, or <see cref="TokenStoreOutcome.NotFound"/>.</summary>
    public TokenRead? ReadAnswer { get; set; }

    /// <summary>A delete's answer; null: remove the kept token.</summary>
    public TokenStoreResult? DeleteAnswer { get; set; }

    /// <summary>While set, the next calls of that kind wait until the source is completed.</summary>
    public TaskCompletionSource? HoldProbe { get; set; }

    public TaskCompletionSource? HoldSave { get; set; }

    public TaskCompletionSource? HoldRead { get; set; }

    public TaskCompletionSource? HoldDelete { get; set; }

    public IReadOnlyList<(string Call, TokenKey? Key, bool Stoppable)> Calls
    {
        get
        {
            lock (_lock)
            {
                return [.. _calls];
            }
        }
    }

    public int CountOf(string call) => Calls.Count(c => c.Call == call);

    /// <summary>Puts an entry in the store, as an earlier run would have.</summary>
    public void Put(TokenKey key, string token)
    {
        lock (_lock)
        {
            _entries[key] = token;
        }
    }

    public async Task<TokenStoreProbe> ProbeAsync(CancellationToken ct)
    {
        Record("probe", null, ct);
        await WaitAsync(HoldProbe);
        return Probe;
    }

    public async Task<TokenStoreResult> SaveAsync(TokenKey key, string token, CancellationToken ct)
    {
        Record("save", key, ct);
        await WaitAsync(HoldSave);
        if (SaveAnswer is { } answer)
        {
            return answer;
        }

        lock (_lock)
        {
            _entries[key] = token;
        }

        return new TokenStoreResult(TokenStoreOutcome.Done);
    }

    public async Task<TokenRead> ReadAsync(TokenKey key, CancellationToken ct)
    {
        Record("read", key, ct);
        await WaitAsync(HoldRead);
        if (ReadAnswer is { } answer)
        {
            return answer;
        }

        lock (_lock)
        {
            return _entries.TryGetValue(key, out var token)
                ? new TokenRead(TokenStoreOutcome.Done, token, null)
                : new TokenRead(TokenStoreOutcome.NotFound, null, null);
        }
    }

    public async Task<TokenStoreResult> DeleteAsync(TokenKey key, CancellationToken ct)
    {
        Record("delete", key, ct);
        await WaitAsync(HoldDelete);
        if (DeleteAnswer is { } answer)
        {
            return answer;
        }

        lock (_lock)
        {
            return new TokenStoreResult(_entries.Remove(key) ? TokenStoreOutcome.Done : TokenStoreOutcome.NotFound);
        }
    }

    private static async Task WaitAsync(TaskCompletionSource? hold)
    {
        if (hold is not null)
        {
            await hold.Task.ConfigureAwait(false);
        }
    }

    private void Record(string call, TokenKey? key, CancellationToken ct)
    {
        lock (_lock)
        {
            _calls.Add((call, key, ct.CanBeCanceled));
        }
    }
}
