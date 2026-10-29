using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using VisualCommit.AskPass;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.Git.Prompts;

/// <summary>
/// The app's end of the askpass channel (docs/phase-3-design.md 3.1 to 3.4, D82): one per app
/// session. It listens on a named pipe that only the current user can open, hands every prompt
/// that comes with the session's token and the id of an open <see cref="PromptScope"/> to the
/// <see cref="IGitPromptHandler"/>, and sends its answer back to the helper.
/// <para>
/// Secrets stay out of every record (D103): the token travels only in the environment of the git
/// processes of a scope, which no call record or log line holds; an answer is written to the
/// pipe and nowhere else. The log names prompts by their kind and scope only.
/// </para>
/// <para>
/// A scope with <see cref="CredentialBinding"/>s (D255, docs/phase-7a-design.md 4.6) also makes
/// the helper git's only credential helper for the bound URLs: two <c>GIT_CONFIG_COUNT</c> pairs
/// per URL, an empty <c>credential.&lt;url&gt;.helper</c> that clears the user's helpers for it,
/// then the helper's credential mode. git's <c>get</c> is answered from
/// <see cref="IGitCredentialSource"/> without UI, never from a prompt; its <c>erase</c> of a token
/// this scope handed out marks the account refused in the scope (D273).
/// </para>
/// </summary>
public sealed class AskPassBroker : IGitPromptBroker, IDisposable
{
    /// <summary>
    /// How many helpers can be connected at once, also the pipe's instance count on Windows; one
    /// more waits there for a free instance, and gives up after the helper's 5 seconds
    /// (<see cref="AskPassProtocol.ConnectTimeout"/>), failing its git as if refused. On macOS and
    /// Linux it connects at once and waits in the socket's queue until an instance is free; the
    /// socket has one instance more, the keeper (<see cref="ListenAsync"/>). A connection
    /// holds its instance until its prompt is answered, so 16 (docs/phase-6-design.md 4.4, D210):
    /// a bulk run's 4 git processes can each wait for their prompt group's turn, with room beside
    /// them for every tab's own prompt and a clone's. With 4, a tab's prompt beside a run's four
    /// failed within 5 seconds (0.2).
    /// </summary>
    public const int MaxConnections = 16;

    /// <summary>How long a connected helper has to send its request.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The oldest git that reads <c>GIT_CONFIG_COUNT</c> (D277).</summary>
    private static readonly GitVersion BindingsVersion = new(2, 31, 0);

    private const string ConfigCountVariable = "GIT_CONFIG_COUNT";
    private const string ConfigParametersVariable = "GIT_CONFIG_PARAMETERS";

    private readonly IGitPromptHandler _handler;
    private readonly IAppLog _log;
    private readonly IGitCredentialSource _credentials;
    private readonly bool _hasCredentials;
    private readonly Func<GitVersion?> _gitVersion;

    /// <summary>The <c>GIT_CONFIG_COUNT</c> git already gets, after which a bound scope numbers its pairs; null when it is not one git accepts.</summary>
    private readonly int? _inheritedCount;

    /// <summary>Whether git would get a non-empty inherited <c>GIT_CONFIG_PARAMETERS</c>, which a bound scope leaves out (D329).</summary>
    private readonly bool _inheritsParameters;
    private readonly byte[] _token;
    private readonly ConcurrentDictionary<string, Scope> _scopes = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _connections = new(MaxConnections, MaxConnections);
    private readonly Task _listening;
    private long _lastScope;
    private int _disposed;

    /// <summary>The pipe's instances made and not yet disposed, the keeper included.</summary>
    private int _liveInstances;

    /// <summary>1 once the listening loop has ended.</summary>
    private int _listenEnded;

    /// <summary>Starts listening.</summary>
    /// <param name="handler">Answers the prompts (the app's dialogs).</param>
    /// <param name="log">The app log.</param>
    /// <param name="helperPath">The helper to name in the variables; null finds it next to this assembly (<see cref="AskPassLocator"/>).</param>
    /// <param name="credentials">Where a bound URL's credential comes from (D255, docs/phase-7a-design.md 1.3); null: none, and no binding is used.</param>
    /// <param name="sessionEnvironment">The session's git environment (D220), which with the process's gives the <c>GIT_CONFIG_COUNT</c> and <c>GIT_CONFIG_PARAMETERS</c> git already gets (4.6); null: none.</param>
    /// <param name="gitVersion">The detected git's version, or null while unknown; below 2.31 no binding is used (D277). A function, as the broker is made before git's detection ends.</param>
    public AskPassBroker(
        IGitPromptHandler handler,
        IAppLog? log = null,
        string? helperPath = null,
        IGitCredentialSource? credentials = null,
        IReadOnlyDictionary<string, string?>? sessionEnvironment = null,
        Func<GitVersion?>? gitVersion = null)
        : this(handler, log, helperPath, credentials, sessionEnvironment, gitVersion, Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>
    /// Starts listening, with <paramref name="processEnvironment"/> reading the process's variables:
    /// the tests' stand-in for the process's own <c>GIT_CONFIG_COUNT</c>, which set in the test
    /// process would reach every git call beside them.
    /// </summary>
    internal AskPassBroker(
        IGitPromptHandler handler,
        IAppLog? log,
        string? helperPath,
        IGitCredentialSource? credentials,
        IReadOnlyDictionary<string, string?>? sessionEnvironment,
        Func<GitVersion?>? gitVersion,
        Func<string, string?> processEnvironment)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(processEnvironment);
        _handler = handler;
        _log = log ?? NullAppLog.Instance;
        _credentials = credentials ?? NullGitCredentialSource.Instance;
        _hasCredentials = credentials is not null;
        _gitVersion = gitVersion ?? (() => null);

        // What git already gets: the session's value when the session names the variable (null
        // removes it), else the process's. The session's environment is applied after the process's.
        string? Inherited(string name) =>
            sessionEnvironment is not null && sessionEnvironment.TryGetValue(name, out var value) ? value : processEnvironment(name);
        var count = Inherited(ConfigCountVariable);
        if (string.IsNullOrEmpty(count))
        {
            _inheritedCount = 0;
        }
        else if (int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            _inheritedCount = parsed;
        }
        else
        {
            // git fails every call with such a count; the pairs would not help it.
            _inheritedCount = null;
            _log.Warning("GIT_CONFIG_COUNT is not a number git accepts; GitHub's token is not given to git.");
        }

        _inheritsParameters = !string.IsNullOrEmpty(Inherited(ConfigParametersVariable));
        HelperPath = Path.GetFullPath(helperPath ?? AskPassLocator.ExpectedPath);
        HelperFound = File.Exists(HelperPath);
        if (!HelperFound)
        {
            // Every prompt then fails: git cannot start the helper and reports "could not read
            // Username", ssh "Permission denied" or "Host key verification failed".
            _log.Error($"The askpass helper is missing ({HelperPath}): git and ssh cannot ask for credentials or passphrases.");
        }

        PipeName = "vc-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        TokenText = token;
        _token = Encoding.UTF8.GetBytes(token);
        _listening = Task.Run(ListenAsync);
    }

    /// <summary>The helper's full path, as the variables of every <c>Ask</c> and <c>Deny</c> scope name it.</summary>
    public string HelperPath { get; }

    /// <summary>Whether the helper was found when the broker started.</summary>
    public bool HelperFound { get; }

    /// <summary>The pipe's name, <c>vc-&lt;16 hex&gt;</c>.</summary>
    public string PipeName { get; }

    /// <summary>
    /// On macOS and Linux, where .NET makes the pipe a Unix domain socket in the temporary
    /// folder: the socket's path, which must stay under 104 characters (7.7, risk 9). Null on Windows.
    /// </summary>
    public string? SocketPath => OperatingSystem.IsWindows() ? null : Path.Combine(Path.GetTempPath(), "CoreFxPipe_" + PipeName);

    /// <summary>
    /// Called by the listening loop between an accept and its serve, for tests that hold the
    /// channel there while another helper connects (AskPassChannelTests). Null in the app.
    /// </summary>
    internal Func<Task>? AfterAccept { get; set; }

    /// <summary>The session's secret. Never logged; only the scopes' variables carry it.</summary>
    private string TokenText { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// Every prompt of the scope carries <paramref name="group"/> (<see cref="GitPrompt.Group"/>,
    /// docs/phase-6-design.md 4.4). <paramref name="bindings"/> are used (docs/phase-7a-design.md
    /// 4.6, D274, D277) only when the helper was found, a credential source was given, the
    /// inherited <c>GIT_CONFIG_COUNT</c> is valid, the mode is <see cref="PromptMode.Ask"/> or
    /// <see cref="PromptMode.Deny"/> and git is not older than 2.31 (a version not yet known counts
    /// as 2.31); otherwise the scope is today's, byte for byte. A deny scope is registered, and
    /// gets the pipe, only when it uses bindings.
    /// </remarks>
    public PromptScope Begin(string title, string repositoryName, PromptMode mode, string? operationId = null, PromptGroup? group = null, IReadOnlyList<CredentialBinding>? bindings = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(repositoryName);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var id = operationId ?? "prompt-" + Interlocked.Increment(ref _lastScope).ToString(CultureInfo.InvariantCulture);
        var used = UsedBindings(mode, bindings);
        if (bindings is { Count: > 0 })
        {
            _log.Debug($"{repositoryName}: {title}: {used.Count} of {bindings.Count} URLs bound to the account ({mode}).");
        }

        if (used.Count > 0 && _inheritsParameters)
        {
            _log.Debug($"{repositoryName}: {title}: the inherited command-line git configuration is left out of calls bound to the account.");
        }

        var scope = new Scope(this, id, title, repositoryName, mode, EnvironmentFor(id, mode, used), group, used);
        if ((mode == PromptMode.Ask || used.Count > 0) && !_scopes.TryAdd(id, scope))
        {
            throw new InvalidOperationException($"A prompt scope with the id {id} is already open.");
        }

        return scope;
    }

    /// <summary>Stops listening and closes every open scope: their prompts are refused and their dialogs close.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifetime.Cancel();
        foreach (var scope in _scopes.Values)
        {
            scope.Dispose();
        }

        try
        {
            _listening.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Logged where it happened.
        }
    }

    /// <summary>The bindings a scope uses (4.6, D274, D277): none unless every condition holds; each URL once.</summary>
    private List<CredentialBinding> UsedBindings(PromptMode mode, IReadOnlyList<CredentialBinding>? bindings)
    {
        if (bindings is null || bindings.Count == 0 || !HelperFound || !_hasCredentials || _inheritedCount is null
            || mode is not (PromptMode.Ask or PromptMode.Deny)
            || _gitVersion() is { } version && version < BindingsVersion)
        {
            return [];
        }

        return [.. bindings.DistinctBy(binding => binding.Url, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The variables of a scope: without bindings exactly 3.1's (every variable the other mode sets
    /// is removed, so that nothing inherited mixes in). With bindings (4.6), Ask's variables, or
    /// Deny's with the pipe, the token and the scope's id; then, numbered after the inherited count
    /// <c>n</c>, for binding <c>i</c> the pair <c>credential.&lt;url&gt;.helper</c> = empty at
    /// <c>n+2i</c> and = the helper's credential mode at <c>n+2i+1</c>; <c>GIT_CONFIG_COUNT</c>
    /// <c>n+2k</c>; <c>GIT_TRACE_REDACT=1</c> (ruling 7); and no <c>GIT_CONFIG_PARAMETERS</c> (D329).
    /// </summary>
    private Dictionary<string, string?> EnvironmentFor(string id, PromptMode mode, IReadOnlyList<CredentialBinding> bindings)
    {
        var environment = EnvironmentFor(id, mode);
        if (bindings.Count == 0)
        {
            return environment;
        }

        if (mode == PromptMode.Deny)
        {
            environment[AskPassProtocol.PipeVariable] = PipeName;
            environment[AskPassProtocol.TokenVariable] = TokenText;
            environment[AskPassProtocol.OperationVariable] = id;
        }

        var first = _inheritedCount!.Value;
        var helper = $"!'{HelperPath.Replace('\\', '/').Replace("'", "'\\''", StringComparison.Ordinal)}' {AskPassProtocol.CredentialArgument}";
        for (var i = 0; i < bindings.Count; i++)
        {
            var key = $"credential.{bindings[i].Url}.helper";
            var reset = (first + (2 * i)).ToString(CultureInfo.InvariantCulture);
            var ours = (first + (2 * i) + 1).ToString(CultureInfo.InvariantCulture);
            environment["GIT_CONFIG_KEY_" + reset] = key;
            environment["GIT_CONFIG_VALUE_" + reset] = string.Empty;
            environment["GIT_CONFIG_KEY_" + ours] = key;
            environment["GIT_CONFIG_VALUE_" + ours] = helper;
        }

        environment[ConfigCountVariable] = (first + (2 * bindings.Count)).ToString(CultureInfo.InvariantCulture);
        environment["GIT_TRACE_REDACT"] = "1";
        environment[ConfigParametersVariable] = null;
        return environment;
    }

    /// <summary>The variables of a scope (3.1). Every variable the other mode sets is removed, so that nothing inherited mixes in.</summary>
    private Dictionary<string, string?> EnvironmentFor(string id, PromptMode mode) => mode switch
    {
        PromptMode.Ask => new Dictionary<string, string?>
        {
            ["GIT_ASKPASS"] = HelperPath,
            ["SSH_ASKPASS"] = HelperPath,
            ["SSH_ASKPASS_REQUIRE"] = "force",
            [AskPassProtocol.PipeVariable] = PipeName,
            [AskPassProtocol.TokenVariable] = TokenText,
            [AskPassProtocol.OperationVariable] = id,
            [AskPassProtocol.DenyVariable] = null,
        },
        PromptMode.Deny => new Dictionary<string, string?>
        {
            ["GIT_ASKPASS"] = HelperPath,
            ["SSH_ASKPASS"] = HelperPath,
            ["SSH_ASKPASS_REQUIRE"] = "force",
            ["GCM_INTERACTIVE"] = "never",
            [AskPassProtocol.DenyVariable] = "1",
            [AskPassProtocol.PipeVariable] = null,
            [AskPassProtocol.TokenVariable] = null,
            [AskPassProtocol.OperationVariable] = null,
        },
        _ => [],
    };

    private async Task ListenAsync()
    {
        var token = _lifetime.Token;

        // On macOS and Linux .NET makes every instance of the pipe share one listening Unix
        // socket, which it closes, its file removed, with the last instance (SharedServer in
        // NamedPipeServerStream.Unix.cs): a helper that had connected and waited in the socket's
        // queue is dropped then, and exits as if the app knew nothing. An answer without UI (git's
        // credential get and erase, 4.6) can run to its end inside the call to ServeAsync below,
        // its instance disposed before the next one is made. The keeper is one instance more that
        // never waits for a connection (only a waiting instance accepts), so that the socket lives
        // as long as the broker listens. None on Windows, where every instance is a pipe of its
        // own from the start, which a helper could connect to and nobody would serve.
        NamedPipeServerStream? keeper = null;
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                keeper = CreatePipe();
            }
            catch (Exception ex)
            {
                _log.Error("The prompt channel stopped listening.", ex);
                return;
            }
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _connections.WaitAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                NamedPipeServerStream? pipe = null;
                try
                {
                    pipe = CreatePipe();
                    await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                    if (AfterAccept is { } hook)
                    {
                        await hook().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    DisposeInstance(pipe);
                    _connections.Release();
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A client that is not the current user, or one that left at once: listen again.
                    DisposeInstance(pipe);
                    _connections.Release();
                    _log.Debug($"The prompt channel refused a connection: {ex.Message}");
                    continue;
                }
                catch (Exception ex)
                {
                    DisposeInstance(pipe);
                    _connections.Release();
                    _log.Error("The prompt channel stopped listening.", ex);
                    return;
                }

                _ = ServeAsync(pipe);
            }
        }
        finally
        {
            // The socket and its file go with the broker.
            Volatile.Write(ref _listenEnded, 1);
            DisposeInstance(keeper);
        }
    }

    /// <summary>
    /// One instance of the pipe, which only the current user can open (3.4). On Windows it belongs
    /// to the user of the app's access token, not to the token's default owner
    /// (<see cref="WindowsPipeUser"/>); on macOS and Linux .NET checks the socket's user. There
    /// every instance, the keeper's included, names the same limit, one more than
    /// <see cref="MaxConnections"/>: .NET refuses an instance whose limit the instances alive
    /// already reach.
    /// </summary>
    private NamedPipeServerStream CreatePipe()
    {
        var pipe = OperatingSystem.IsWindows()
            ? NamedPipeServerStreamAcl.Create(
                PipeName,
                PipeDirection.InOut,
                MaxConnections,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 0,
                outBufferSize: 0,
                WindowsPipeUser.ServerSecurity())
            : new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                MaxConnections + 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        Interlocked.Increment(ref _liveInstances);
        return pipe;
    }

    /// <summary>
    /// Disposes an instance made by <see cref="CreatePipe"/>, if any, and counts it gone. On macOS
    /// and Linux none left while the broker listens means that the socket closed between two
    /// connections, which the keeper is there to prevent: a warning.
    /// </summary>
    private void DisposeInstance(NamedPipeServerStream? pipe)
    {
        if (pipe is null)
        {
            return;
        }

        pipe.Dispose();
        if (Interlocked.Decrement(ref _liveInstances) == 0
            && !OperatingSystem.IsWindows()
            && Volatile.Read(ref _listenEnded) == 0
            && !_lifetime.IsCancellationRequested)
        {
            _log.Warning("The prompt channel closed its socket between two connections; a helper connecting then would go unanswered.");
        }
    }

    /// <summary>One connection: one request, one answer (3.1).</summary>
    private async Task ServeAsync(NamedPipeServerStream pipe)
    {
        try
        {
            byte[]? line;
            using (var reading = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token))
            {
                reading.CancelAfter(RequestTimeout);
                try
                {
                    line = await AskPassProtocol.ReadLineAsync(pipe, reading.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!_lifetime.IsCancellationRequested)
                {
                    _log.Debug($"The prompt channel's helper sent no request within {RequestTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s.");
                    return;
                }
            }

            string? answer = null;
            if (line is null)
            {
                _log.Debug("The prompt channel's helper closed its end without a request.");
            }
            else
            {
                answer = AskPassProtocol.ParseCredentialRequest(line) is { } credential
                    ? await AnswerCredentialAsync(credential).ConfigureAwait(false)
                    : await AnswerAsync(AskPassProtocol.ParseRequest(line), pipe).ConfigureAwait(false);
            }

            await pipe.WriteAsync(AskPassProtocol.FormatAnswer(answer), _lifetime.Token).ConfigureAwait(false);
            await pipe.FlushAsync(_lifetime.Token).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            // Its git was stopped, or it gave up waiting.
            _log.Debug($"The prompt channel's helper went away before its answer: {ex.Message}");
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            // The session ended.
        }
        catch (Exception ex)
        {
            _log.Error("The prompt channel failed to answer a prompt.", ex);
        }
        finally
        {
            DisposeInstance(pipe);
            _connections.Release();
        }
    }

    /// <summary>The answer to a request: null unless the token matches and its scope is open (3.4).</summary>
    private async Task<string?> AnswerAsync(AskPassRequest? request, NamedPipeServerStream pipe)
    {
        if (request is null)
        {
            _log.Debug("The prompt channel received a malformed request.");
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(request.Token), _token))
        {
            _log.Warning("The prompt channel received a request with a wrong token; it was refused.");
            return null;
        }

        if (!_scopes.TryGetValue(request.Operation, out var scope) || scope.IsDisposed)
        {
            _log.Debug($"A prompt for {request.Operation}, which has no open prompt scope, was refused.");
            return null;
        }

        return await scope.AskAsync(request, pipe).ConfigureAwait(false);
    }

    /// <summary>
    /// The answer to git's <c>get</c> or <c>erase</c> through the helper's credential mode (4.6):
    /// null unless the token matches and its scope is open and uses bindings.
    /// </summary>
    private async Task<string?> AnswerCredentialAsync(AskPassCredentialRequest request)
    {
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(request.Token), _token))
        {
            _log.Warning("The prompt channel received a credential request with a wrong token; it was refused.");
            return null;
        }

        if (!_scopes.TryGetValue(request.Operation, out var scope) || scope.IsDisposed || scope.Bindings.Count == 0)
        {
            _log.Debug($"A credential request for {request.Operation}, which has no open scope bound to an account, was not answered.");
            return null;
        }

        return await scope.AnswerCredentialAsync(request).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>scheme://host[:port]</c> in lower case without the scheme's default port, by which a
    /// binding, git's credential request and a prompt's URL are matched (4.6); null for no URL.
    /// </summary>
    private static string? OriginOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.Length > 0
            ? (uri.Scheme + "://" + (uri.IsDefaultPort ? uri.Host : uri.Host + ":" + uri.Port.ToString(CultureInfo.InvariantCulture))).ToLowerInvariant()
            : null;

    /// <summary>
    /// Whether a user name in git's credential request or prompt is <paramref name="account"/>'s:
    /// none, the account's login in any case, or <c>x-access-token</c> (4.6). A prompt that names
    /// another user carries no account (<see cref="GitPrompt.Account"/>).
    /// </summary>
    public static bool IsAccountUser(string? user, AccountKey account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return string.IsNullOrEmpty(user)
            || string.Equals(user, account.Login, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user, AskPassProtocol.CredentialUserName, StringComparison.Ordinal);
    }

    private void Remove(Scope scope) => _scopes.TryRemove(new KeyValuePair<string, Scope>(scope.Id, scope));

    private sealed class Scope : PromptScope
    {
        private readonly AskPassBroker _owner;
        private readonly CancellationTokenSource _closed = new();
        private readonly List<CancellationTokenSource> _askings = [];

        /// <summary>The accounts git refused in this scope (D273): their gets get nothing, their prompts no dialog.</summary>
        private readonly HashSet<AccountKey> _refused = [];

        /// <summary><see cref="AskPassProtocol.SecretHash"/> of every token this scope gave git.</summary>
        private readonly HashSet<string> _handedOut = new(StringComparer.Ordinal);

        public Scope(AskPassBroker owner, string id, string title, string repositoryName, PromptMode mode, IReadOnlyDictionary<string, string?> environment, PromptGroup? group, IReadOnlyList<CredentialBinding> bindings)
            : base(id, environment)
        {
            _owner = owner;
            Title = title;
            RepositoryName = repositoryName;
            Mode = mode;
            Group = group;
            Bindings = bindings;
        }

        /// <summary>The bindings this scope uses (4.6); empty when it uses none.</summary>
        public IReadOnlyList<CredentialBinding> Bindings { get; }

        public string Title { get; }

        public string RepositoryName { get; }

        public PromptMode Mode { get; }

        /// <summary>The bulk run whose prompts this scope's belong to, or null (docs/phase-6-design.md 4.4).</summary>
        public PromptGroup? Group { get; }

        /// <summary>
        /// git's <c>get</c> or <c>erase</c> for a bound URL (4.6, D273, D275): the binding whose
        /// origin is the request's; a user other than the login or <c>x-access-token</c> gets
        /// nothing. A get is the account's token, unless the account was refused in this scope; an
        /// erase of a token this scope handed out marks the account refused, once.
        /// </summary>
        public async Task<string?> AnswerCredentialAsync(AskPassCredentialRequest request)
        {
            var log = _owner._log;
            var what = $"{RepositoryName}: {Title}: git's credential {request.Action} for {request.Host}";
            var origin = OriginOf(request.Protocol + "://" + request.Host + "/");
            var binding = origin is null ? null : Bindings.FirstOrDefault(binding => OriginOf(binding.Url) == origin);
            if (binding is null)
            {
                log.Debug($"{what} is not bound.");
                return null;
            }

            var account = binding.Account;
            if (!IsAccountUser(request.Username, account))
            {
                log.Debug($"{what} names another user.");
                return null;
            }

            if (request.Action == "erase")
            {
                // Why nothing is marked, or null when the account is marked refused now.
                string? unmarked;
                lock (_refused)
                {
                    unmarked = request.SecretHash is not { } hash ? "no password"
                        : !_handedOut.Contains(hash) ? "a token this scope did not hand out"
                        : !_refused.Add(account) ? "the account was already refused in this scope"
                        : null;
                }

                if (unmarked is null)
                {
                    RefusedAccount = account;
                    _owner._credentials.Refused(account);
                    log.Info($"{RepositoryName}: {Title}: {request.Host} refused the account's token.");
                }
                else
                {
                    log.Debug($"{what}: nothing marked ({unmarked}).");
                }

                return null;
            }

            lock (_refused)
            {
                if (_refused.Contains(account))
                {
                    log.Debug($"{what} has no token.");
                    return null;
                }
            }

            GitCredential? credential;
            using (var closing = CancellationTokenSource.CreateLinkedTokenSource(_closed.Token, _owner._lifetime.Token))
            {
                try
                {
                    credential = await _owner._credentials.GetAsync(account, closing.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    credential = null;
                }
            }

            var token = credential?.Token;
            if (string.IsNullOrEmpty(token))
            {
                log.Debug($"{what} has no token.");
                return null;
            }

            if (token.AsSpan().IndexOfAny('\r', '\n', '\0') >= 0)
            {
                log.Warning($"{what}: the account's token holds a line end or a NUL and was not given to git.");
                return null;
            }

            lock (_refused)
            {
                _handedOut.Add(AskPassProtocol.SecretHash(token));
            }

            log.Debug($"{what} answered from the account.");
            return IsDisposed ? null : token;
        }

        /// <summary>
        /// Hands the prompt to the handler. Its token is cancelled when the scope closes, the
        /// session ends, or the helper goes away (git or ssh stopped it): the dialog then closes.
        /// A deny scope's prompt is refused at once. A user name or password prompt for a bound URL
        /// (none, the login or <c>x-access-token</c> as its user) carries the binding's account,
        /// and is refused without a dialog once git refused that account in this scope (D273). No
        /// prompt is answered from the account (D277).
        /// </summary>
        public async Task<string?> AskAsync(AskPassRequest request, Stream pipe)
        {
            if (Mode == PromptMode.Deny)
            {
                return null;
            }

            var classification = GitPromptClassifier.Classify(request.Prompt, request.SshPrompt);
            var account = AccountOf(classification);
            if (account is not null)
            {
                lock (_refused)
                {
                    if (_refused.Contains(account))
                    {
                        _owner._log.Debug($"{RepositoryName}: {Title}: git asks ({classification.Kind}) for an account it refused; no dialog.");
                        return null;
                    }
                }
            }

            var prompt = new GitPrompt(
                classification.Kind,
                request.Prompt,
                Id,
                Title,
                RepositoryName,
                classification.Url,
                classification.User,
                classification.KeyPath,
                Group,
                account);

            // The handler's token stays alive until the scope closes, so that a handler can hear
            // of the scope's end after it has answered (PromptDialogs forgets what it kept then).
            var asking = CancellationTokenSource.CreateLinkedTokenSource(_closed.Token, _owner._lifetime.Token);
            CancellationToken token;
            lock (_askings)
            {
                if (IsDisposed)
                {
                    asking.Dispose();
                    return null;
                }

                _askings.Add(asking);
                token = asking.Token;
            }

            using var watching = CancellationTokenSource.CreateLinkedTokenSource(token);
            var helperGone = WatchForHangUpAsync(pipe, asking, watching.Token);
            _owner._log.Debug($"{RepositoryName}: {Title}: git asks ({classification.Kind}).");
            PromptAnswer answer;
            try
            {
                answer = await _owner._handler.AnswerAsync(prompt, token).ConfigureAwait(false) ?? PromptAnswer.Refused;
            }
            catch (OperationCanceledException)
            {
                answer = PromptAnswer.Refused;
            }
            catch (Exception ex)
            {
                _owner._log.Error($"{RepositoryName}: {Title}: the prompt could not be shown.", ex);
                answer = PromptAnswer.Refused;
            }
            finally
            {
                await watching.CancelAsync().ConfigureAwait(false);
                await helperGone.ConfigureAwait(false);
            }

            if (answer.UserCancelled)
            {
                UserCancelled = true;
            }

            return IsDisposed ? null : answer.Text;
        }

        /// <summary>The account of the binding a user name or password prompt names, or null.</summary>
        private AccountKey? AccountOf(GitPromptClassification classification)
        {
            if (Bindings.Count == 0 || classification.Kind is not (GitPromptKind.Username or GitPromptKind.Password) || classification.Url is not { } url)
            {
                return null;
            }

            var origin = OriginOf(url);
            var binding = origin is null ? null : Bindings.FirstOrDefault(binding => OriginOf(binding.Url) == origin);
            return binding is not null && IsAccountUser(classification.User, binding.Account) ? binding.Account : null;
        }

        protected override void OnDisposed()
        {
            _owner.Remove(this);
            try
            {
                _closed.Cancel();
            }
            catch (AggregateException ex)
            {
                _owner._log.Error($"{RepositoryName}: {Title}: closing a prompt failed.", ex);
            }

            lock (_askings)
            {
                _askings.ForEach(asking => asking.Dispose());
                _askings.Clear();
            }
        }

        /// <summary>
        /// Reads from the helper's end until it closes (the helper sends nothing after its
        /// request), then cancels <paramref name="asking"/>: the prompt's dialog closes. Ends
        /// without cancelling when <paramref name="stop"/> is cancelled: the prompt was answered.
        /// </summary>
        private static async Task WatchForHangUpAsync(Stream pipe, CancellationTokenSource asking, CancellationToken stop)
        {
            var buffer = new byte[256];
            try
            {
                while (await pipe.ReadAsync(buffer, stop).ConfigureAwait(false) > 0)
                {
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                // Asked and answered: nobody waits for the helper any more.
                return;
            }
            catch (IOException)
            {
                // The pipe broke: the helper is gone as well.
            }

            try
            {
                await asking.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // The scope has closed meanwhile.
            }
        }
    }
}
