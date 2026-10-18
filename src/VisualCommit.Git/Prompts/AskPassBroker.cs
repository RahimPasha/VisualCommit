using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using VisualCommit.AskPass;
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
/// </summary>
public sealed class AskPassBroker : IGitPromptBroker, IDisposable
{
    /// <summary>How many helpers can be connected at once; a fifth waits for a free instance.</summary>
    public const int MaxConnections = 4;

    /// <summary>How long a connected helper has to send its request.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly IGitPromptHandler _handler;
    private readonly IAppLog _log;
    private readonly byte[] _token;
    private readonly ConcurrentDictionary<string, Scope> _scopes = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _connections = new(MaxConnections, MaxConnections);
    private readonly Task _listening;
    private long _lastScope;
    private int _disposed;

    /// <summary>Starts listening.</summary>
    /// <param name="handler">Answers the prompts (the app's dialogs).</param>
    /// <param name="log">The app log.</param>
    /// <param name="helperPath">The helper to name in the variables; null finds it next to this assembly (<see cref="AskPassLocator"/>).</param>
    public AskPassBroker(IGitPromptHandler handler, IAppLog? log = null, string? helperPath = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
        _log = log ?? NullAppLog.Instance;
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

    /// <summary>The session's secret. Never logged; only the scopes' variables carry it.</summary>
    private string TokenText { get; }

    // group: work package WP4 puts it in every prompt of the scope (docs/phase-6-design.md 4.4).
    public PromptScope Begin(string title, string repositoryName, PromptMode mode, string? operationId = null, PromptGroup? group = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(repositoryName);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var id = operationId ?? "prompt-" + Interlocked.Increment(ref _lastScope).ToString(CultureInfo.InvariantCulture);
        var scope = new Scope(this, id, title, repositoryName, mode, EnvironmentFor(id, mode));
        if (mode == PromptMode.Ask && !_scopes.TryAdd(id, scope))
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
            }
            catch (OperationCanceledException)
            {
                pipe?.Dispose();
                _connections.Release();
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A client that is not the current user, or one that left at once: listen again.
                pipe?.Dispose();
                _connections.Release();
                _log.Debug($"The prompt channel refused a connection: {ex.Message}");
                continue;
            }
            catch (Exception ex)
            {
                pipe?.Dispose();
                _connections.Release();
                _log.Error("The prompt channel stopped listening.", ex);
                return;
            }

            _ = ServeAsync(pipe);
        }
    }

    /// <summary>
    /// One instance of the pipe, which only the current user can open (3.4). On Windows it belongs
    /// to the user of the app's access token, not to the token's default owner
    /// (<see cref="WindowsPipeUser"/>); on macOS and Linux .NET checks the socket's user.
    /// </summary>
    private NamedPipeServerStream CreatePipe() => OperatingSystem.IsWindows()
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
            MaxConnections,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    /// <summary>One connection: one request, one answer (3.1).</summary>
    private async Task ServeAsync(NamedPipeServerStream pipe)
    {
        try
        {
            byte[]? line;
            using (var reading = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token))
            {
                reading.CancelAfter(RequestTimeout);
                line = await AskPassProtocol.ReadLineAsync(pipe, reading.Token).ConfigureAwait(false);
            }

            var answer = line is null ? null : await AnswerAsync(AskPassProtocol.ParseRequest(line), pipe).ConfigureAwait(false);
            await pipe.WriteAsync(AskPassProtocol.FormatAnswer(answer), _lifetime.Token).ConfigureAwait(false);
            await pipe.FlushAsync(_lifetime.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // The helper went away (git was stopped), took too long to ask, or the session ended.
        }
        catch (Exception ex)
        {
            _log.Error("The prompt channel failed to answer a prompt.", ex);
        }
        finally
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
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

    private void Remove(Scope scope) => _scopes.TryRemove(new KeyValuePair<string, Scope>(scope.Id, scope));

    private sealed class Scope : PromptScope
    {
        private readonly AskPassBroker _owner;
        private readonly CancellationTokenSource _closed = new();
        private readonly List<CancellationTokenSource> _askings = [];

        public Scope(AskPassBroker owner, string id, string title, string repositoryName, PromptMode mode, IReadOnlyDictionary<string, string?> environment)
            : base(id, environment)
        {
            _owner = owner;
            Title = title;
            RepositoryName = repositoryName;
            Mode = mode;
        }

        public string Title { get; }

        public string RepositoryName { get; }

        public PromptMode Mode { get; }

        /// <summary>
        /// Hands the prompt to the handler. Its token is cancelled when the scope closes, the
        /// session ends, or the helper goes away (git or ssh stopped it): the dialog then closes.
        /// </summary>
        public async Task<string?> AskAsync(AskPassRequest request, Stream pipe)
        {
            var classification = GitPromptClassifier.Classify(request.Prompt, request.SshPrompt);
            var prompt = new GitPrompt(
                classification.Kind,
                request.Prompt,
                Id,
                Title,
                RepositoryName,
                classification.Url,
                classification.User,
                classification.KeyPath);

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
