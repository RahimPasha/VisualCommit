using System.Security.Cryptography;
using System.Text;
using Tmds.DBus.Protocol;
using VisualCommit.Core.Logging;
using static VisualCommit.Hosting.Tokens.SecretServiceCalls;

namespace VisualCommit.Hosting.Tokens;

/// <summary>The time limits of the Secret Service store's steps (docs/phase-7a-design.md 3.6, D271).</summary>
public sealed record SecretServiceLimits(TimeSpan Connect, TimeSpan NameQuery, TimeSpan OpenSession, TimeSpan Call, TimeSpan Prompt)
{
    /// <summary>Connect 2 s, a name query 5 s, the session 10 s, a call 25 s, a prompt 120 s.</summary>
    public static SecretServiceLimits Default { get; } = new(
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(120));
}

/// <summary>What the Secret Service store reads from its surroundings; tests put their own.</summary>
public sealed class SecretServiceOptions
{
    public Func<string, string?> ReadEnvironment { get; init; } = Environment.GetEnvironmentVariable;

    public Func<string, bool> SocketExists { get; init; } = File.Exists;

    /// <summary>The connection's options for an address; never connected before the store asks.</summary>
    public Func<string, DBusConnectionOptions> ConnectionOptions { get; init; } =
        address => new DBusConnectionOptions(address) { AutoConnect = false };

    public SecretServiceLimits Limits { get; init; } = SecretServiceLimits.Default;
}

/// <summary>
/// Tokens in the Secret Service on Linux (docs/phase-7a-design.md 3.6, D271) through
/// Tmds.DBus.Protocol: an item per key, its secret the token's UTF-8 in a <c>plain</c> session.
/// Each operation opens one connection and one session and closes both in <c>finally</c>; every
/// step is bounded, and a prompt of the service (an unlock, a new collection) is waited for at most
/// 120 s and dismissed past that. The probe never unlocks, creates or reads anything, so it shows no
/// dialog. The constructor does nothing.
/// </summary>
public sealed class SecretServiceStore(IAppLog log, SecretServiceOptions? options = null, string application = TokenEntryNames.Application) : ITokenStore
{
    // Closing the session is a courtesy: the service closes it with the connection anyway.
    private static readonly TimeSpan CloseLimit = TimeSpan.FromSeconds(2);

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly SecretServiceOptions _options = options ?? new SecretServiceOptions();

    public string Name => "Secret Service";

    public bool KeepsTokensAcrossRestarts => true;

    public Task<TokenStoreProbe> ProbeAsync(CancellationToken ct) => Task.Run(() => ProbeCoreAsync(ct), ct);

    public Task<TokenStoreResult> SaveAsync(TokenKey key, string token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrEmpty(token);
        return Names(key) is { } names
            ? Task.Run(
                () => ConverseAsync("save", key, conversation => SaveCoreAsync(conversation, key, names, token), Failed, ct),
                ct)
            : Task.FromResult(Failed(TokenStoreReasons.Refused));

        static TokenStoreResult Failed(string reason) => new(TokenStoreOutcome.Failed, reason);
    }

    public Task<TokenRead> ReadAsync(TokenKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Names(key) is { } names
            ? Task.Run(() => ConverseAsync("read", key, conversation => ReadCoreAsync(conversation, key, names), Failed, ct), ct)
            : Task.FromResult(Failed(TokenStoreReasons.Refused));

        static TokenRead Failed(string reason) => new(TokenStoreOutcome.Failed, null, reason);
    }

    public Task<TokenStoreResult> DeleteAsync(TokenKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Names(key) is { } names
            ? Task.Run(() => ConverseAsync("delete", key, conversation => DeleteCoreAsync(conversation, key, names), Failed, ct), ct)
            : Task.FromResult(Failed(TokenStoreReasons.Refused));

        static TokenStoreResult Failed(string reason) => new(TokenStoreOutcome.Failed, reason);
    }

    private TokenEntryNames? Names(TokenKey key)
    {
        if (TokenEntryNames.TryFor(key, application, out var names))
        {
            return names;
        }

        log.Warning($"{Name}: a part of the key {key} cannot name an entry; no call was made.");
        return null;
    }

    private string? Address() => SecretServiceAddress.Resolve(_options.ReadEnvironment, _options.SocketExists);

    private async Task<TokenStoreProbe> ProbeCoreAsync(CancellationToken ct)
    {
        var unavailable = new TokenStoreProbe(false, TokenStoreReasons.NoKeychain);
        if (Address() is not { } address)
        {
            log.Warning($"{Name}: no session bus ({SecretServiceAddress.AddressVariable} is not set and $XDG_RUNTIME_DIR/bus does not exist).");
            return unavailable;
        }

        var limits = _options.Limits;
        var step = "connect";
        try
        {
            using var connection = new DBusConnection(_options.ConnectionOptions(address));
            await connection.ConnectAsync().AsTask().WaitAsync(limits.Connect, ct).ConfigureAwait(false);
            step = "NameHasOwner";
            if (!await NameHasOwnerAsync(connection, Bus).WaitAsync(limits.NameQuery, ct).ConfigureAwait(false))
            {
                step = "ListActivatableNames";
                var activatable = await connection.ListActivatableServicesAsync().WaitAsync(limits.NameQuery, ct).ConfigureAwait(false);
                if (!activatable.Contains(Bus, StringComparer.Ordinal))
                {
                    log.Warning($"{Name}: nothing on the session bus owns or can start {Bus}.");
                    return unavailable;
                }
            }

            step = "OpenSession";
            var session = await OpenSessionAsync(connection).WaitAsync(limits.OpenSession, ct).ConfigureAwait(false);
            await CloseQuietlyAsync(connection, session).ConfigureAwait(false);
            log.Debug($"{Name}: available.");
            return new TokenStoreProbe(true, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            log.Warning($"{Name}: the session bus did not answer {step} in time.");
            return unavailable;
        }
        catch (DBusErrorReplyException exception)
        {
            log.Warning($"{Name}: {step} was answered with {exception.ErrorName}.");
            return unavailable;
        }
        catch (Exception exception)
        {
            log.Warning($"{Name}: {step} failed ({exception.GetType().Name}).", exception);
            return unavailable;
        }
    }

    // One connection and one plain session for an operation, both closed in finally; every failure a result.
    private async Task<T> ConverseAsync<T>(
        string call, TokenKey key, Func<Conversation, Task<T>> body, Func<string, T> failed, CancellationToken ct)
    {
        if (Address() is not { } address)
        {
            log.Warning($"{Name}: no session bus for the {call} of the token for {key.Host}/{key.Account}.");
            return failed(TokenStoreReasons.NoKeychain);
        }

        var limits = _options.Limits;
        var conversation = new Conversation(limits, ct);
        try
        {
            using var connection = new DBusConnection(_options.ConnectionOptions(address));
            await connection.ConnectAsync().AsTask().WaitAsync(limits.Connect, ct).ConfigureAwait(false);
            conversation.Step = "OpenSession";
            var session = await OpenSessionAsync(connection).WaitAsync(limits.OpenSession, ct).ConfigureAwait(false);
            conversation.Connected(connection, session);
            try
            {
                return await body(conversation).ConfigureAwait(false);
            }
            finally
            {
                await CloseQuietlyAsync(connection, session).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (PromptDismissedException)
        {
            log.Warning($"{Name}: the prompt of {conversation.Step} for the token of {key.Host}/{key.Account} was dismissed.");
            return failed(TokenStoreReasons.Locked);
        }
        catch (TimeoutException)
        {
            log.Warning($"{Name}: {conversation.Step} for the {call} of the token for {key.Host}/{key.Account} got no answer in time.");
            return failed(TokenStoreReasons.NoAnswer);
        }
        catch (DBusErrorReplyException exception)
        {
            log.Warning($"{Name}: {conversation.Step} for the {call} of the token for {key.Host}/{key.Account} was answered with {exception.ErrorName}.");
            return failed(exception.ErrorName == IsLockedError ? TokenStoreReasons.Locked : TokenStoreReasons.Refused);
        }
        catch (Exception exception)
        {
            // Only the type: an exception of a failed connection says nothing of the token, but one of a
            // call could quote what was sent.
            log.Warning($"{Name}: {conversation.Step} for the {call} of the token for {key.Host}/{key.Account} failed ({exception.GetType().Name}).");
            return failed(conversation.Step == "connect" ? TokenStoreReasons.NoKeychain : TokenStoreReasons.Refused);
        }
    }

    private async Task<TokenRead> ReadCoreAsync(Conversation conversation, TokenKey key, TokenEntryNames names)
    {
        var (unlocked, locked) = await conversation.CallAsync("SearchItems", c => SearchItemsAsync(c, names.SecretServiceSearch)).ConfigureAwait(false);
        if (unlocked.Length + locked.Length == 0)
        {
            log.Debug($"{Name}: no token for {key.Host}/{key.Account}.");
            return new TokenRead(TokenStoreOutcome.NotFound, null, null);
        }

        if (unlocked.Length + locked.Length > 1)
        {
            log.Debug($"{Name}: {unlocked.Length + locked.Length} entries for {key.Host}/{key.Account}; the first is read.");
        }

        var items = unlocked.Length > 0 ? unlocked : await conversation.UnlockAsync(locked).ConfigureAwait(false);
        if (items.Count == 0)
        {
            log.Warning($"{Name}: the entry for {key.Host}/{key.Account} stayed locked.");
            return new TokenRead(TokenStoreOutcome.Failed, null, TokenStoreReasons.Locked);
        }

        var first = items[0];
        var secrets = await conversation.CallAsync("GetSecrets", c => GetSecretsAsync(c, [first], conversation.Session)).ConfigureAwait(false);
        try
        {
            if (!secrets.TryGetValue(first, out var value) || value.Length == 0)
            {
                log.Warning($"{Name}: the service gave no secret for the entry of {key.Host}/{key.Account}.");
                return new TokenRead(TokenStoreOutcome.Failed, null, TokenStoreReasons.Unreadable);
            }

            var token = StrictUtf8.GetString(value);
            log.Debug($"{Name}: read the token for {key.Host}/{key.Account}.");
            return new TokenRead(TokenStoreOutcome.Done, token, null);
        }
        catch (DecoderFallbackException)
        {
            log.Warning($"{Name}: the entry for {key.Host}/{key.Account} is not UTF-8 text.");
            return new TokenRead(TokenStoreOutcome.Failed, null, TokenStoreReasons.Unreadable);
        }
        finally
        {
            foreach (var value in secrets.Values)
            {
                CryptographicOperations.ZeroMemory(value);
            }
        }
    }

    private async Task<TokenStoreResult> SaveCoreAsync(Conversation conversation, TokenKey key, TokenEntryNames names, string token)
    {
        // The default alias' collection, else the login collection, else a new "Login" made the default (D271).
        var collection = await conversation.CallAsync("ReadAlias", c => ReadAliasAsync(c, "default")).ConfigureAwait(false);
        bool? locked = null;
        if (collection == NoObject)
        {
            try
            {
                locked = await conversation.CallAsync("the login collection's Locked", c => IsLockedAsync(c, LoginCollection)).ConfigureAwait(false);
                collection = LoginCollection;
            }
            catch (DBusErrorReplyException exception)
            {
                log.Debug($"{Name}: no default collection, and the login collection answered {exception.ErrorName}; creating one.");
                var (made, prompt) = await conversation.CallAsync("CreateCollection", c => CreateCollectionAsync(c, "Login", "default"))
                    .ConfigureAwait(false);
                collection = made != NoObject ? made : (await conversation.PromptAsync("CreateCollection", prompt).ConfigureAwait(false)).FirstOrDefault() ?? NoObject;
                if (collection == NoObject)
                {
                    log.Warning($"{Name}: no collection was made for the token of {key.Host}/{key.Account}.");
                    return new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused);
                }
            }
        }

        locked ??= await conversation.CallAsync("Locked", c => IsLockedAsync(c, collection)).ConfigureAwait(false);
        if (locked == true)
        {
            await conversation.UnlockAsync([collection]).ConfigureAwait(false);
        }

        var secret = Encoding.UTF8.GetBytes(token);
        string item;
        string itemPrompt;
        try
        {
            (item, itemPrompt) = await conversation.CallAsync(
                "CreateItem",
                c => CreateItemAsync(c, collection, conversation.Session, names.SecretServiceLabel, names.SecretServiceAttributes, secret))
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        if (item == NoObject)
        {
            item = (await conversation.PromptAsync("CreateItem", itemPrompt).ConfigureAwait(false)).FirstOrDefault() ?? NoObject;
        }

        if (item == NoObject)
        {
            log.Warning($"{Name}: no item was made for the token of {key.Host}/{key.Account}.");
            return new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused);
        }

        log.Debug($"{Name}: saved the token for {key.Host}/{key.Account}.");
        return new TokenStoreResult(TokenStoreOutcome.Done);
    }

    private async Task<TokenStoreResult> DeleteCoreAsync(Conversation conversation, TokenKey key, TokenEntryNames names)
    {
        var (unlocked, locked) = await conversation.CallAsync("SearchItems", c => SearchItemsAsync(c, names.SecretServiceSearch)).ConfigureAwait(false);
        if (unlocked.Length + locked.Length == 0)
        {
            return new TokenStoreResult(TokenStoreOutcome.NotFound);
        }

        if (locked.Length > 0)
        {
            await conversation.UnlockAsync(locked).ConfigureAwait(false);
        }

        foreach (var item in unlocked.Concat(locked))
        {
            var prompt = await conversation.CallAsync("Delete", c => DeleteItemAsync(c, item)).ConfigureAwait(false);
            await conversation.PromptAsync("Delete", prompt).ConfigureAwait(false);
        }

        log.Debug($"{Name}: deleted the token for {key.Host}/{key.Account} ({unlocked.Length + locked.Length} entries).");
        return new TokenStoreResult(TokenStoreOutcome.Done);
    }

    private static async Task CloseQuietlyAsync(DBusConnection connection, string session)
    {
        try
        {
            await CloseSessionAsync(connection, session).WaitAsync(CloseLimit).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The service closes the session with the connection, which is disposed next.
        }
    }

    /// <summary>An operation's connection and session, the step it is at, and its bounded calls and prompts.</summary>
    private sealed class Conversation(SecretServiceLimits limits, CancellationToken ct)
    {
        private DBusConnection? _connection;

        public string Step { get; set; } = "connect";

        public string Session { get; private set; } = NoObject;

        private DBusConnection Connection => _connection ?? throw new InvalidOperationException("Not connected.");

        public void Connected(DBusConnection connection, string session)
        {
            _connection = connection;
            Session = session;
        }

        public Task<T> CallAsync<T>(string step, Func<DBusConnection, Task<T>> call)
        {
            Step = step;
            return call(Connection).WaitAsync(limits.Call, ct);
        }

        /// <summary>Unlocks <paramref name="objects"/>, through the service's prompt when it asks for one: the objects unlocked.</summary>
        public async Task<IReadOnlyList<string>> UnlockAsync(IReadOnlyList<string> objects)
        {
            var (unlocked, prompt) = await CallAsync("Unlock", c => SecretServiceCalls.UnlockAsync(c, objects)).ConfigureAwait(false);
            return prompt == NoObject ? unlocked : [.. unlocked, .. await PromptAsync("Unlock", prompt).ConfigureAwait(false)];
        }

        /// <summary>
        /// Shows <paramref name="prompt"/> and waits for its <c>Completed</c> (the prompt limit): its
        /// result's paths. Dismissed: <see cref="PromptDismissedException"/>. Past the limit or on a
        /// cancel the prompt is dismissed as a best effort.
        /// </summary>
        public async Task<IReadOnlyList<string>> PromptAsync(string step, string prompt)
        {
            if (prompt == NoObject)
            {
                return [];
            }

            Step = step + "'s prompt";
            var completed = new TaskCompletionSource<PromptCompletion>(TaskCreationOptions.RunContinuationsAsynchronously);
            var match = await WatchPromptAsync(Connection, prompt, completed).AsTask().WaitAsync(limits.Call, ct).ConfigureAwait(false);
            try
            {
                await ShowPromptAsync(Connection, prompt).WaitAsync(limits.Call, ct).ConfigureAwait(false);
                var completion = await completed.Task.WaitAsync(limits.Prompt, ct).ConfigureAwait(false);
                return completion.Dismissed ? throw new PromptDismissedException() : completion.Paths;
            }
            catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
            {
                try
                {
                    await DismissPromptAsync(Connection, prompt).WaitAsync(CloseLimit).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // A best effort: the connection's end removes the prompt too.
                }

                throw;
            }
            finally
            {
                match.Dispose();
            }
        }
    }

    private sealed class PromptDismissedException : Exception
    {
    }
}
