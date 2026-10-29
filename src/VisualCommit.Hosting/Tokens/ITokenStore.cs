using System.Collections.Concurrent;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;

namespace VisualCommit.Hosting.Tokens;

/// <summary>Which token a store entry holds (docs/phase-7a-design.md 1.6): provider, host and account, in lower case.</summary>
public sealed record TokenKey(string Provider, string Host, string Account)
{
    /// <summary>The key of <paramref name="account"/>'s token.</summary>
    public static TokenKey Of(AccountKey account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new TokenKey(account.ProviderId.ToLowerInvariant(), account.Host.ToLowerInvariant(), account.Login.ToLowerInvariant());
    }

    public override string ToString() => $"{Provider}:{Host}:{Account}";
}

/// <summary>Whether a store can keep tokens, and if not why (a <c>TokenStoreReasons</c> text).</summary>
public sealed record TokenStoreProbe(bool Available, string? Reason);

/// <summary>How a store call ended.</summary>
public enum TokenStoreOutcome
{
    Done,
    NotFound,
    Failed,
}

/// <summary>The outcome of a save or a delete, with the reason of a failure.</summary>
public sealed record TokenStoreResult(TokenStoreOutcome Outcome, string? Reason = null);

/// <summary>The outcome of a read: the token when it was found. Its text never shows the token.</summary>
public sealed class TokenRead(TokenStoreOutcome outcome, string? token, string? reason)
{
    public TokenStoreOutcome Outcome { get; } = outcome;

    /// <summary>The token, for <see cref="TokenStoreOutcome.Done"/>; null otherwise.</summary>
    public string? Token { get; } = token;

    public string? Reason { get; } = reason;

    public override string ToString() =>
        $"TokenRead({Outcome}{(Token is null ? string.Empty : ", ***")}{(Reason is null ? string.Empty : ", " + Reason)})";
}

/// <summary>
/// Where tokens are kept (docs/phase-7a-design.md 1.6, 3.2): this computer's keychain, or memory.
/// A store never prompts in <see cref="ProbeAsync"/>, and an expected failure is a result, never an
/// exception.
/// </summary>
public interface ITokenStore
{
    /// <summary>The store's name, for the log only: no shown text names a store (ruling 17).</summary>
    string Name { get; }

    /// <summary>Whether a token saved now is there at the app's next start.</summary>
    bool KeepsTokensAcrossRestarts { get; }

    /// <summary>Whether the store can keep tokens; never prompts, and bounded.</summary>
    Task<TokenStoreProbe> ProbeAsync(CancellationToken ct);

    Task<TokenStoreResult> SaveAsync(TokenKey key, string token, CancellationToken ct);

    Task<TokenRead> ReadAsync(TokenKey key, CancellationToken ct);

    Task<TokenStoreResult> DeleteAsync(TokenKey key, CancellationToken ct);
}

/// <summary>Which token store the app uses (docs/phase-7a-design.md 1.6, 1.8).</summary>
public static class TokenStores
{
    /// <summary>
    /// The environment variable that chooses the store: <c>memory</c>, or <c>unavailable</c> (no
    /// keychain, D253); unset, the platform's. Any other value gives memory and a warning (D269).
    /// </summary>
    public const string Variable = "VISUALCOMMIT_KEYCHAIN";

    /// <summary>Turns on the real stores' test classes: <c>windows</c>, <c>macos</c> or <c>secret-service</c>.</summary>
    public const string TestsVariable = "VISUALCOMMIT_KEYCHAIN_TESTS";

    /// <summary>
    /// The store <paramref name="readEnvironment"/>'s <see cref="Variable"/> asks for, logged:
    /// <c>memory</c> a <see cref="MemoryTokenStore"/> that keeps tokens, <c>unavailable</c> one
    /// without a keychain (D253), any other value memory and a warning (D269); unset, the
    /// platform's, which is memory until the platforms' stores exist.
    /// </summary>
    public static ITokenStore FromEnvironment(Func<string, string?> readEnvironment, IAppLog log)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentNullException.ThrowIfNull(log);
        var value = readEnvironment(Variable)?.Trim();
        ITokenStore store;
        if (string.IsNullOrEmpty(value) || string.Equals(value, "memory", StringComparison.OrdinalIgnoreCase))
        {
            // Unset: the platform's store, which WP-K adds (docs/phase-7a-design.md 3.7).
            store = new MemoryTokenStore(available: true);
        }
        else if (string.Equals(value, "unavailable", StringComparison.OrdinalIgnoreCase))
        {
            store = new MemoryTokenStore(available: false);
        }
        else
        {
            log.Warning($"{Variable}={value} is not known; tokens are kept in memory.");
            return new MemoryTokenStore(available: true);
        }

        log.Info($"Token store: {store.Name}.");
        return store;
    }
}

/// <summary>
/// Tokens in a dictionary of the process (docs/phase-7a-design.md 1.6, 3.7): gone when the app
/// closes. Without a keychain (<c>available</c> false) its probe says so and every save fails.
/// </summary>
public sealed class MemoryTokenStore(bool available = true) : ITokenStore
{
    private const string NoKeychain = "No keychain was found on this computer.";

    private readonly ConcurrentDictionary<TokenKey, string> _tokens = new();

    public string Name => available ? "memory" : "memory, no keychain";

    public bool KeepsTokensAcrossRestarts => false;

    public Task<TokenStoreProbe> ProbeAsync(CancellationToken ct) =>
        ct.IsCancellationRequested
            ? Task.FromCanceled<TokenStoreProbe>(ct)
            : Task.FromResult(available ? new TokenStoreProbe(true, null) : new TokenStoreProbe(false, NoKeychain));

    public Task<TokenStoreResult> SaveAsync(TokenKey key, string token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrEmpty(token);
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<TokenStoreResult>(ct);
        }

        if (!available)
        {
            return Task.FromResult(new TokenStoreResult(TokenStoreOutcome.Failed, NoKeychain));
        }

        _tokens[key] = token;
        return Task.FromResult(new TokenStoreResult(TokenStoreOutcome.Done));
    }

    public Task<TokenRead> ReadAsync(TokenKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<TokenRead>(ct);
        }

        return Task.FromResult(_tokens.TryGetValue(key, out var token)
            ? new TokenRead(TokenStoreOutcome.Done, token, null)
            : new TokenRead(TokenStoreOutcome.NotFound, null, null));
    }

    public Task<TokenStoreResult> DeleteAsync(TokenKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<TokenStoreResult>(ct);
        }

        return Task.FromResult(new TokenStoreResult(_tokens.TryRemove(key, out _) ? TokenStoreOutcome.Done : TokenStoreOutcome.NotFound));
    }
}
