using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Git;

namespace VisualCommit.Testing;

/// <summary>
/// A small git server over HTTP for the tests (design 7.3, D97): a <see cref="TcpListener"/> on
/// 127.0.0.1 serving one bare repository, <see cref="RepositoryName"/>, through
/// <c>git http-backend</c> run as CGI. Unless <see cref="RequireAuth"/> is false it demands HTTP
/// Basic auth on every request (<see cref="Credentials"/>, alice/s3cret at first), so that git
/// asks for a user name and password as it would of a real server. It runs the same on the
/// three platforms with nothing to register.
/// <para>
/// One request per connection: every response carries <c>Connection: close</c>. Request bodies
/// come by <c>Content-Length</c> or chunked, and a gzip-compressed body is handed to
/// <c>http-backend</c> as it came, with <c>HTTP_CONTENT_ENCODING</c> set. <c>http-backend</c> runs
/// through <see cref="GitRunner"/> with <see cref="GitCommand.Latin1"/>, so bodies pass byte for
/// byte; it runs to its end before the response is sent, which is therefore never streamed.
/// </para>
/// <para>
/// <see cref="HoldNextFetchPack"/> holds back part of the next response that carries a pack, so
/// that a test sees a fetch in progress, with a percentage, for as long as it likes.
/// </para>
/// </summary>
public sealed class AuthRemoteServer : IAsyncDisposable, IDisposable
{
    /// <summary>The port of the servers whose URL the visual checks' pictures show (design 7.3).</summary>
    public const int VisualChecksPort = 47391;

    /// <summary>The folder name of the served repository, and the last part of <see cref="Url"/>.</summary>
    public const string RepositoryName = "remote.git";

    /// <summary>The realm of the <c>WWW-Authenticate</c> header.</summary>
    public const string Realm = "VisualCommit tests";

    /// <summary>The most a request's line and headers may take.</summary>
    private const int MaxHeaderBytes = 64 * 1024;

    /// <summary>How long a client may take to send its request.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long disposing waits for connections to end once they were told to.</summary>
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(15);

    private readonly TempDirectory _root;
    private readonly TcpListener _listener;
    private readonly GitRunner _runner;
    private readonly IReadOnlyDictionary<string, string?> _environment;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _gate = new();
    private readonly List<AuthRemoteRequest> _requests = [];
    private readonly List<string> _errors = [];
    private readonly List<Task> _connections = [];
    private readonly List<Hold> _activeHolds = [];
    private Hold? _pendingHold;
    private Task _acceptLoop = Task.CompletedTask;
    private NetworkCredential _credentials = new("alice", "s3cret");
    private volatile bool _requireAuth = true;
    private int _disposed;

    private AuthRemoteServer(TempDirectory root, TcpListener listener, bool portWasTaken)
    {
        _root = root;
        _listener = listener;
        PortWasTaken = portWasTaken;
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _runner = new GitRunner(TempRepo.GitPath);
        _environment = TempRepo.IsolatedEnvironment(root.Combine("home"));
    }

    /// <summary>The port the server listens on, on 127.0.0.1.</summary>
    public int Port { get; }

    /// <summary>Whether the port asked for was taken, so that the server took one the system picked.</summary>
    public bool PortWasTaken { get; }

    /// <summary>The served repository's URL: <c>http://127.0.0.1:&lt;port&gt;/remote.git</c>.</summary>
    public string Url => string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{Port}/{RepositoryName}");

    /// <summary>The served bare repository's folder, for git calls that go around the server.</summary>
    public string RepositoryPath => _root.Combine(RepositoryName);

    /// <summary>Whether every request must carry <see cref="Credentials"/>. True at first.</summary>
    public bool RequireAuth
    {
        get => _requireAuth;
        set => _requireAuth = value;
    }

    /// <summary>The user name and password the server accepts; alice and s3cret at first. Setting it changes the password for the requests that follow.</summary>
    public NetworkCredential Credentials
    {
        get => Volatile.Read(ref _credentials);
        set => Volatile.Write(ref _credentials, value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>Every request served so far, in the order their responses were decided.</summary>
    public IReadOnlyList<AuthRemoteRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>What went wrong inside the server other than a client going away: empty when all is well.</summary>
    public IReadOnlyList<string> Errors
    {
        get
        {
            lock (_gate)
            {
                return [.. _errors];
            }
        }
    }

    /// <summary>
    /// Creates the server's empty bare repository (on main) and starts listening on 127.0.0.1:
    /// on <paramref name="port"/>, or on a port the system picks when it is 0. With
    /// <paramref name="fallBackToFreePort"/>, a port that is taken gives way to one the system
    /// picks (<see cref="PortWasTaken"/>); without it, it throws <see cref="SocketException"/>.
    /// </summary>
    public static async Task<AuthRemoteServer> StartAsync(int port = 0, bool fallBackToFreePort = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        var root = new TempDirectory("auth-server");
        TcpListener? listener = null;
        try
        {
            var portWasTaken = false;
            try
            {
                listener = Listen(port);
            }
            catch (SocketException) when (fallBackToFreePort && port != 0)
            {
                portWasTaken = true;
                listener = Listen(0);
            }

            var server = new AuthRemoteServer(root, listener, portWasTaken);
            await server.CreateRepositoryAsync().ConfigureAwait(false);
            server._acceptLoop = Task.Run(server.AcceptLoopAsync);
            return server;
        }
        catch
        {
            listener?.Stop();
            listener?.Dispose();
            root.Dispose();
            throw;
        }
    }

    /// <summary>Starts a server for a visual check: on <see cref="VisualChecksPort"/>, or on a free port when it is taken.</summary>
    public static Task<AuthRemoteServer> StartForVisualChecksAsync() => StartAsync(VisualChecksPort, fallBackToFreePort: true);

    /// <summary>
    /// Holds back the next response that carries a pack: one to an upload-pack POST that asks for
    /// a pack (under protocol v2 a body with <c>command=fetch</c>, not the <c>command=ls-refs</c>
    /// that comes first; under protocol v0 any) and whose response has pack data. The server
    /// sends the headers and the response up to and including the first progress line (side
    /// band 2) that holds a <c>%</c>, then waits for <see cref="Release"/>; git meanwhile shows
    /// that line and waits for the rest. When git asked for no progress (<c>no-progress</c>,
    /// a fetch without <c>--progress</c> and without a terminal) only the headers go first.
    /// Calling it again before a response was held changes nothing.
    /// </summary>
    /// <returns>A task that completes when the response is held: its first part has been sent.
    /// It is cancelled when <see cref="Release"/> comes first or the server is disposed.</returns>
    public Task HoldNextFetchPack()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _pendingHold ??= new Hold();
            return _pendingHold.Engaged.Task;
        }
    }

    /// <summary>Sends the rest of every held response, and drops a hold that has not begun.</summary>
    public void Release()
    {
        List<Hold> holds;
        lock (_gate)
        {
            holds = [.. _activeHolds];
            _activeHolds.Clear();
            if (_pendingHold is not null)
            {
                holds.Add(_pendingHold);
                _pendingHold = null;
            }
        }

        foreach (var hold in holds)
        {
            hold.Engaged.TrySetCanceled();
            hold.Released.TrySetResult();
        }
    }

    /// <summary>Stops listening, ends every connection (a held response is not sent) and deletes the repository.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        List<Hold> holds;
        Task[] connections;
        lock (_gate)
        {
            holds = [.. _activeHolds];
            if (_pendingHold is not null)
            {
                holds.Add(_pendingHold);
            }

            _activeHolds.Clear();
            _pendingHold = null;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        foreach (var hold in holds)
        {
            hold.Engaged.TrySetCanceled();
            hold.Released.TrySetCanceled();
        }

        _listener.Stop();
        try
        {
            await _acceptLoop.WaitAsync(DisposeTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Left to end by itself; the listener is stopped.
        }

        lock (_gate)
        {
            connections = [.. _connections];
        }

        try
        {
            await Task.WhenAll(connections).WaitAsync(DisposeTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A connection that does not end is left behind; its socket is closed by then.
        }

        _listener.Dispose();
        _stopping.Dispose();
        _root.Dispose();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private static TcpListener Listen(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        try
        {
            listener.Start(backlog: 64);
            return listener;
        }
        catch
        {
            listener.Dispose();
            throw;
        }
    }

    private async Task CreateRepositoryAsync()
    {
        await GitAsync(_root.Path, "init", "--quiet", "--bare", "--initial-branch", "main", RepositoryName).ConfigureAwait(false);

        // No background gc after a push, which would outlive the request.
        await GitAsync(RepositoryPath, "config", "gc.auto", "0").ConfigureAwait(false);
        await GitAsync(RepositoryPath, "config", "receive.autogc", "false").ConfigureAwait(false);
    }

    private async Task GitAsync(string folder, params string[] arguments)
    {
        var command = new GitCommand(arguments) { WorkingDirectory = folder, Environment = _environment };
        (await _runner.RunAsync(command).ConfigureAwait(false)).EnsureSuccess(command);
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException or InvalidOperationException)
            {
                if (_stopping.IsCancellationRequested)
                {
                    return;
                }

                RecordError($"Accepting a connection failed: {ex.Message}");
                continue;
            }

            lock (_gate)
            {
                _connections.RemoveAll(task => task.IsCompleted);
                _connections.Add(Task.Run(() => ServeAsync(client)));
            }
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                var reader = new RequestReader(stream);
                using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
                readTimeout.CancelAfter(RequestTimeout);

                var head = await reader.ReadHeadAsync(readTimeout.Token).ConfigureAwait(false);
                if (head is null)
                {
                    return;
                }

                await HandleAsync(stream, reader, head, readTimeout.Token).ConfigureAwait(false);
                client.Client.Shutdown(SocketShutdown.Send);
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
                // The client went away, or the server is stopping.
            }
            catch (Exception ex)
            {
                RecordError(ex.ToString());
            }
        }
    }

    private async Task HandleAsync(NetworkStream stream, RequestReader reader, RequestHead head, CancellationToken readCancelled)
    {
        var stopping = _stopping.Token;
        var (path, query) = SplitTarget(head.Target);
        var user = AuthenticatedUser(head);
        var authorized = user is not null || !RequireAuth;

        // A client that waits for "100 Continue" sends no body to a refused request.
        var expectsContinue = string.Equals(head.Header("Expect"), "100-continue", StringComparison.OrdinalIgnoreCase);
        byte[] body = [];
        if (authorized || !expectsContinue)
        {
            if (expectsContinue)
            {
                await stream.WriteAsync("HTTP/1.1 100 Continue\r\n\r\n"u8.ToArray(), stopping).ConfigureAwait(false);
            }

            body = await reader.ReadBodyAsync(head, readCancelled).ConfigureAwait(false);
        }

        var protocol = head.Header("Git-Protocol");
        var compressed = IsGzip(head.Header("Content-Encoding"));
        var command = V2Command(protocol, body, compressed);
        var request = new AuthRemoteRequest(head.Method, path, query, protocol, command, user, 0, Held: false, head.IsChunked, compressed);

        if (!authorized)
        {
            Record(request with { Status = 401 });
            await WriteSimpleAsync(stream, 401, "Unauthorized", $"WWW-Authenticate: Basic realm=\"{Realm}\"\r\n", "Authentication required.\n", stopping).ConfigureAwait(false);
            return;
        }

        if (!IsInRepository(path) || head.Method is not ("GET" or "POST"))
        {
            Record(request with { Status = 404 });
            await WriteSimpleAsync(stream, 404, "Not Found", string.Empty, "Not found.\n", stopping).ConfigureAwait(false);
            return;
        }

        var cgi = await RunBackendAsync(head, path, query, user, body, stopping).ConfigureAwait(false);
        var response = ParseCgi(cgi.Output);
        if (response is null)
        {
            Record(request with { Status = 500 });
            RecordError($"git http-backend gave no response to {head.Method} {head.Target} (exit {cgi.ExitCode}): {cgi.Error.Trim()}");
            await WriteSimpleAsync(stream, 500, "Internal Server Error", string.Empty, cgi.Error, stopping).ConfigureAwait(false);
            return;
        }

        // A hold applies to an upload-pack POST that asks for a pack and gets one.
        Hold? hold = null;
        var holdAt = 0;
        var asksForPack = head.Method == "POST"
            && path.EndsWith("/git-upload-pack", StringComparison.Ordinal)
            && (!IsVersion2(protocol) || command == "fetch");
        if (asksForPack && response.Status == 200)
        {
            var (hasPack, progressEnd) = ScanPktLines(response.Body);
            if (hasPack)
            {
                hold = TakePendingHold();
                holdAt = Math.Max(progressEnd, 0);
            }
        }

        Record(request with { Status = response.Status, Held = hold is not null });
        var responseHead = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"HTTP/1.1 {response.Status} {response.Reason}\r\n")
            .Append(response.Headers)
            .Append(CultureInfo.InvariantCulture, $"Content-Length: {response.Body.Length}\r\n")
            .Append("Connection: close\r\n\r\n")
            .ToString();
        await stream.WriteAsync(Encoding.Latin1.GetBytes(responseHead), stopping).ConfigureAwait(false);

        if (hold is null)
        {
            await stream.WriteAsync(response.Body, stopping).ConfigureAwait(false);
            return;
        }

        try
        {
            await stream.WriteAsync(response.Body.AsMemory(0, holdAt), stopping).ConfigureAwait(false);
            await stream.FlushAsync(stopping).ConfigureAwait(false);
            hold.Engaged.TrySetResult();
            await hold.Released.Task.WaitAsync(stopping).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _activeHolds.Remove(hold);
            }
        }

        // The client may have given up meanwhile (a cancelled fetch): then this write fails,
        // which ends the connection quietly.
        await stream.WriteAsync(response.Body.AsMemory(holdAt), stopping).ConfigureAwait(false);
    }

    private Hold? TakePendingHold()
    {
        lock (_gate)
        {
            var hold = _pendingHold;
            if (hold is not null)
            {
                _pendingHold = null;
                _activeHolds.Add(hold);
            }

            return hold;
        }
    }

    private async Task<(string Output, string Error, int ExitCode)> RunBackendAsync(RequestHead head, string path, string query, string? user, byte[] body, CancellationToken cancellationToken)
    {
        var environment = new Dictionary<string, string?>(_environment)
        {
            ["GIT_PROJECT_ROOT"] = _root.Path,
            ["GIT_HTTP_EXPORT_ALL"] = "1",
            ["REQUEST_METHOD"] = head.Method,
            ["PATH_INFO"] = path,
            ["QUERY_STRING"] = query,
            ["CONTENT_TYPE"] = head.Header("Content-Type"),
            ["CONTENT_LENGTH"] = head.Method == "POST" ? body.Length.ToString(CultureInfo.InvariantCulture) : null,
            ["HTTP_CONTENT_ENCODING"] = head.Header("Content-Encoding"),
            ["GIT_PROTOCOL"] = head.Header("Git-Protocol"),
            ["REMOTE_ADDR"] = "127.0.0.1",

            // http-backend lets an authenticated user push; a server that asks nobody lets anyone.
            ["REMOTE_USER"] = user ?? "anonymous",
        };

        var command = new GitCommand("http-backend")
        {
            WorkingDirectory = _root.Path,
            Environment = environment,
            Encoding = GitCommand.Latin1,
            StandardInput = body.Length > 0 ? Encoding.Latin1.GetString(body) : null,
        };

        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        return (result.StandardOutput, result.StandardError, result.ExitCode);
    }

    private string? AuthenticatedUser(RequestHead head)
    {
        var authorization = head.Header("Authorization");
        const string Basic = "Basic ";
        if (authorization is null || !authorization.StartsWith(Basic, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[Basic.Length..].Trim()));
        }
        catch (FormatException)
        {
            return null;
        }

        var colon = decoded.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return null;
        }

        var credentials = Credentials;
        var userName = decoded[..colon];
        return string.Equals(userName, credentials.UserName, StringComparison.Ordinal)
            && string.Equals(decoded[(colon + 1)..], credentials.Password, StringComparison.Ordinal)
            ? userName
            : null;
    }

    private static bool IsInRepository(string path) =>
        path.StartsWith("/" + RepositoryName + "/", StringComparison.Ordinal) && !path.Contains("..", StringComparison.Ordinal);

    private static (string Path, string Query) SplitTarget(string target)
    {
        var question = target.IndexOf('?', StringComparison.Ordinal);
        return question < 0 ? (target, string.Empty) : (target[..question], target[(question + 1)..]);
    }

    private static bool IsGzip(string? encoding) => encoding is not null &&
        (encoding.Equals("gzip", StringComparison.OrdinalIgnoreCase) || encoding.Equals("x-gzip", StringComparison.OrdinalIgnoreCase));

    private static bool IsVersion2(string? protocol) => protocol is not null && protocol.Contains("version=2", StringComparison.Ordinal);

    /// <summary>The command of a protocol v2 request (<c>ls-refs</c>, <c>fetch</c>), read from its body; null for other requests.</summary>
    private static string? V2Command(string? protocol, byte[] body, bool compressed)
    {
        if (!IsVersion2(protocol) || body.Length == 0)
        {
            return null;
        }

        byte[] request;
        try
        {
            request = compressed ? Gunzip(body) : body;
        }
        catch (InvalidDataException)
        {
            return null;
        }

        // The first pkt-line is "command=<name>", with or without a newline.
        foreach (var payload in PktLines(request))
        {
            var text = Encoding.Latin1.GetString(payload).TrimEnd('\n');
            const string Prefix = "command=";
            if (text.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return text[Prefix.Length..];
            }
        }

        return null;
    }

    /// <summary>The payloads of the pkt-lines at the start of <paramref name="data"/>, up to the first that cannot be read; flush, delimiter and response-end packets have none and are skipped.</summary>
    private static IEnumerable<byte[]> PktLines(byte[] data)
    {
        var offset = 0;
        while (offset + 4 <= data.Length
            && int.TryParse(Encoding.ASCII.GetString(data, offset, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var length))
        {
            if (length < 4)
            {
                offset += 4;
                continue;
            }

            if (offset + length > data.Length)
            {
                yield break;
            }

            yield return data[(offset + 4)..(offset + length)];
            offset += length;
        }
    }

    private static byte[] Gunzip(byte[] body)
    {
        using var input = new MemoryStream(body);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>
    /// Reads a response body as pkt-lines (protocol v0's and v2's upload-pack responses are
    /// nothing else). Returns whether a line carries pack data (side band 1), and the end of
    /// the first progress line (side band 2) that holds a <c>%</c>, or -1. Lines that are not
    /// on a side band (<c>ACK</c>, <c>NAK</c>, <c>packfile</c>) start with a letter, never with
    /// the band's byte 1, 2 or 3.
    /// </summary>
    internal static (bool HasPack, int ProgressEnd) ScanPktLines(byte[] body)
    {
        var hasPack = false;
        var progressEnd = -1;
        var offset = 0;
        while (offset + 4 <= body.Length && !(hasPack && progressEnd >= 0))
        {
            if (!int.TryParse(Encoding.ASCII.GetString(body, offset, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var length))
            {
                break;
            }

            // 0000 flush, 0001 delimiter, 0002 response end: no payload.
            if (length < 4)
            {
                offset += 4;
                continue;
            }

            if (offset + length > body.Length)
            {
                break;
            }

            var payload = body.AsSpan(offset + 4, length - 4);
            if (payload.Length > 0 && payload[0] == 1)
            {
                hasPack = true;
            }
            else if (payload.Length > 0 && payload[0] == 2 && progressEnd < 0 && payload.Contains((byte)'%'))
            {
                progressEnd = offset + length;
            }

            offset += length;
        }

        return (hasPack, progressEnd);
    }

    /// <summary>Splits <c>http-backend</c>'s output into status, headers and body; null when it holds no header block.</summary>
    private static CgiResponse? ParseCgi(string output)
    {
        var crlf = output.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var lf = output.IndexOf("\n\n", StringComparison.Ordinal);
        int headerEnd, bodyStart;
        if (crlf >= 0 && (lf < 0 || crlf < lf))
        {
            headerEnd = crlf;
            bodyStart = crlf + 4;
        }
        else if (lf >= 0)
        {
            headerEnd = lf;
            bodyStart = lf + 2;
        }
        else
        {
            return null;
        }

        var status = 200;
        var reason = "OK";
        var headers = new StringBuilder();
        foreach (var rawLine in output[..headerEnd].Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                continue;
            }

            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (name.Equals("Status", StringComparison.OrdinalIgnoreCase))
            {
                var space = value.IndexOf(' ', StringComparison.Ordinal);
                status = int.Parse(space < 0 ? value : value[..space], CultureInfo.InvariantCulture);
                reason = space < 0 ? string.Empty : value[(space + 1)..];
            }
            else if (!name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                && !name.Equals("Connection", StringComparison.OrdinalIgnoreCase)
                && !name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                headers.Append(name).Append(": ").Append(value).Append("\r\n");
            }
        }

        return new CgiResponse(status, reason, headers.ToString(), Encoding.Latin1.GetBytes(output[bodyStart..]));
    }

    private static async Task WriteSimpleAsync(NetworkStream stream, int status, string reason, string extraHeaders, string text, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(text);
        var head = string.Create(
            CultureInfo.InvariantCulture,
            $"HTTP/1.1 {status} {reason}\r\n{extraHeaders}Content-Type: text/plain; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    }

    private void Record(AuthRemoteRequest request)
    {
        lock (_gate)
        {
            _requests.Add(request);
        }
    }

    private void RecordError(string error)
    {
        lock (_gate)
        {
            _errors.Add(error);
        }
    }

    private sealed record CgiResponse(int Status, string Reason, string Headers, byte[] Body);

    /// <summary>One hold: begun when the first part of the response is sent, ended by <see cref="Release"/>.</summary>
    private sealed class Hold
    {
        public TaskCompletionSource Engaged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>A request's line and headers.</summary>
    private sealed class RequestHead(string method, string target, Dictionary<string, string> headers)
    {
        public string Method { get; } = method;

        public string Target { get; } = target;

        public bool IsChunked =>
            Header("Transfer-Encoding") is { } encoding && encoding.Contains("chunked", StringComparison.OrdinalIgnoreCase);

        public string? Header(string name) => headers.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>Reads one HTTP/1.1 request from a connection, through a buffer of its own.</summary>
    private sealed class RequestReader(Stream stream)
    {
        private readonly byte[] _buffer = new byte[16 * 1024];
        private int _start;
        private int _end;

        /// <summary>The request line and headers; null when the client closed the connection before sending any.</summary>
        public async Task<RequestHead?> ReadHeadAsync(CancellationToken cancellationToken)
        {
            var requestLine = await ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (requestLine is null)
            {
                return null;
            }

            var parts = requestLine.Split(' ');
            if (parts.Length != 3 || !parts[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Not an HTTP request: {requestLine}");
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var total = requestLine.Length;
            while (true)
            {
                var line = await ReadLineAsync(cancellationToken).ConfigureAwait(false)
                    ?? throw new IOException("The connection closed in the middle of the headers.");
                total += line.Length + 2;
                if (total > MaxHeaderBytes)
                {
                    throw new InvalidDataException("The request's headers are too long.");
                }

                if (line.Length == 0)
                {
                    break;
                }

                var colon = line.IndexOf(':', StringComparison.Ordinal);
                if (colon > 0)
                {
                    var name = line[..colon].Trim();
                    var value = line[(colon + 1)..].Trim();
                    headers[name] = headers.TryGetValue(name, out var earlier) ? earlier + ", " + value : value;
                }
            }

            return new RequestHead(parts[0], parts[1], headers);
        }

        /// <summary>The request's body, by <c>Content-Length</c> or chunked; empty when it has none.</summary>
        public async Task<byte[]> ReadBodyAsync(RequestHead head, CancellationToken cancellationToken)
        {
            if (head.IsChunked)
            {
                using var body = new MemoryStream();
                while (true)
                {
                    var sizeLine = await ReadLineAsync(cancellationToken).ConfigureAwait(false)
                        ?? throw new IOException("The connection closed in the middle of a chunked body.");
                    var semicolon = sizeLine.IndexOf(';', StringComparison.Ordinal);
                    var size = int.Parse(semicolon < 0 ? sizeLine.Trim() : sizeLine[..semicolon].Trim(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                    if (size == 0)
                    {
                        // Trailers, up to an empty line.
                        while (!string.IsNullOrEmpty(await ReadLineAsync(cancellationToken).ConfigureAwait(false)))
                        {
                        }

                        return body.ToArray();
                    }

                    body.Write(await ReadExactlyAsync(size, cancellationToken).ConfigureAwait(false));
                    if (await ReadLineAsync(cancellationToken).ConfigureAwait(false) is not "")
                    {
                        throw new InvalidDataException("A chunk does not end with CRLF.");
                    }
                }
            }

            var contentLength = head.Header("Content-Length");
            return contentLength is null
                ? []
                : await ReadExactlyAsync(int.Parse(contentLength, NumberStyles.None, CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        }

        private async Task<byte[]> ReadExactlyAsync(int count, CancellationToken cancellationToken)
        {
            var result = new byte[count];
            var copied = 0;
            while (copied < count)
            {
                if (_start == _end && !await FillAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new IOException("The connection closed in the middle of the body.");
                }

                var take = Math.Min(count - copied, _end - _start);
                Array.Copy(_buffer, _start, result, copied, take);
                _start += take;
                copied += take;
            }

            return result;
        }

        /// <summary>One line up to CRLF (or LF), without it; null when the connection closed before a line began.</summary>
        private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            var line = new StringBuilder();
            while (true)
            {
                if (_start == _end && !await FillAsync(cancellationToken).ConfigureAwait(false))
                {
                    return line.Length == 0 ? null : throw new IOException("The connection closed in the middle of a line.");
                }

                var b = _buffer[_start++];
                if (b == (byte)'\n')
                {
                    if (line.Length > 0 && line[^1] == '\r')
                    {
                        line.Length--;
                    }

                    return line.ToString();
                }

                line.Append((char)b);
                if (line.Length > MaxHeaderBytes)
                {
                    throw new InvalidDataException("A line of the request is too long.");
                }
            }
        }

        private async Task<bool> FillAsync(CancellationToken cancellationToken)
        {
            _start = 0;
            _end = await stream.ReadAsync(_buffer, cancellationToken).ConfigureAwait(false);
            return _end > 0;
        }
    }
}

/// <summary>One request <see cref="AuthRemoteServer"/> served.</summary>
/// <param name="Method"><c>GET</c> or <c>POST</c>.</param>
/// <param name="Path">The path, such as <c>/remote.git/info/refs</c>.</param>
/// <param name="Query">The query without <c>?</c>, such as <c>service=git-upload-pack</c>; empty when none.</param>
/// <param name="GitProtocol">The <c>Git-Protocol</c> header (<c>version=2</c>); null when none.</param>
/// <param name="Command">A protocol v2 request's command (<c>ls-refs</c>, <c>fetch</c>); null for other requests.</param>
/// <param name="UserName">The user the request signed in as; null when it gave no valid credentials.</param>
/// <param name="Status">The response's status code.</param>
/// <param name="Held">Whether the response was held (<see cref="AuthRemoteServer.HoldNextFetchPack"/>).</param>
/// <param name="Chunked">Whether the body came chunked.</param>
/// <param name="Compressed">Whether the body came compressed with gzip.</param>
public sealed record AuthRemoteRequest(
    string Method,
    string Path,
    string Query,
    string? GitProtocol,
    string? Command,
    string? UserName,
    int Status,
    bool Held,
    bool Chunked,
    bool Compressed);
