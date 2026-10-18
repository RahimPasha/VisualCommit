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
/// 127.0.0.1 serving one bare repository through <c>git http-backend</c> run as CGI: an empty one
/// of its own, <see cref="RepositoryName"/>, or an existing one in place (<see cref="StartAsync"/>). Unless <see cref="RequireAuth"/> is false it demands HTTP
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
/// <see cref="HoldNextFetchPack(int)"/> holds back part of the next response that carries a pack, so
/// that a test sees a fetch in progress, with a percentage (one it chose, if it likes), for as
/// long as it likes.
/// </para>
/// <para>
/// Since phase 6 (docs/phase-6-design.md 17.2, amending D97 as D150 did) a server can serve a
/// folder of bare repositories (<see cref="StartForFolderAsync"/>), each at its own URL
/// (<see cref="UrlOf"/>), with a hold of its own per repository
/// (<see cref="HoldNextFetchPack(string, int)"/>, <see cref="Release(string)"/>) and a gate on a
/// repository's next request of any kind (<see cref="HoldNextRequest"/>).
/// </para>
/// </summary>
public sealed class AuthRemoteServer : IAsyncDisposable, IDisposable
{
    /// <summary>The port of the servers whose URL the visual checks' pictures show (design 7.3).</summary>
    public const int VisualChecksPort = 47391;

    /// <summary>
    /// The port of the server of phase 1's clone checks (13 and 14, D150), whose URL picture 13b
    /// shows: not <see cref="VisualChecksPort"/>, because phase 1's checks run in another process
    /// at the same time as phase 3's.
    /// </summary>
    public const int Phase1VisualChecksPort = 47392;

    /// <summary>
    /// The port of phase 6's servers (docs/phase-6-design.md 17.2): the workspace's folder server
    /// (<see cref="Scenarios.ServeWorkspaceAsync"/>) and L31's "auth" server, whose URLs the
    /// pictures show. Not <see cref="VisualChecksPort"/> or <see cref="Phase1VisualChecksPort"/>:
    /// phase 6's checks run in a process of their own beside phases 1 and 3's.
    /// </summary>
    public const int Phase6VisualChecksPort = 47393;

    /// <summary>The folder name of the served repository, and the last part of <see cref="Url"/>.</summary>
    public const string RepositoryName = "remote.git";

    /// <summary>The realm of the <c>WWW-Authenticate</c> header.</summary>
    public const string Realm = "VisualCommit tests";

    /// <summary>The most a request's line and headers may take.</summary>
    private const int MaxHeaderBytes = 64 * 1024;


    /// <summary>How long disposing waits for connections to end once they were told to.</summary>
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(15);

    private readonly TempDirectory _root;

    /// <summary>The folder that holds the served repositories: <c>GIT_PROJECT_ROOT</c> of <c>http-backend</c>. <see cref="_root"/> unless the server serves a repository in place or a folder.</summary>
    private readonly string _projectRoot;

    /// <summary>The served repositories' folder names (<c>remote.git</c>, <c>docs.git</c>), by their names without a final <c>.git</c> (<c>remote</c>, <c>docs</c>).</summary>
    private readonly IReadOnlyDictionary<string, string> _served;

    /// <summary>The folder name of the one repository a server of one repository serves; null for a folder server.</summary>
    private readonly string? _single;
    private readonly TcpListener _listener;
    private readonly GitRunner _runner;
    private readonly IReadOnlyDictionary<string, string?> _environment;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _gate = new();
    private readonly List<AuthRemoteRequest> _requests = [];
    private readonly List<string> _errors = [];
    private readonly List<Task> _connections = [];
    private readonly List<Hold> _activeHolds = [];

    /// <summary>The holds asked for and not begun yet, by the served folder name they wait for.</summary>
    private readonly Dictionary<string, Hold> _pendingHolds = new(StringComparer.Ordinal);

    /// <summary>The gates on a repository's next request (<see cref="HoldNextRequest"/>) that no request has reached yet, by folder name.</summary>
    private readonly Dictionary<string, RequestGate> _pendingGates = new(StringComparer.Ordinal);

    /// <summary>The gates a request waits at now.</summary>
    private readonly List<RequestGate> _closedGates = [];
    private Task _acceptLoop = Task.CompletedTask;
    private NetworkCredential _credentials = new("alice", "s3cret");
    private volatile bool _requireAuth = true;
    private int _disposed;

    private AuthRemoteServer(TempDirectory root, TcpListener listener, bool portWasTaken, string projectRoot, IReadOnlyList<string> servedFolders, bool folderServer)
    {
        _root = root;
        _listener = listener;
        PortWasTaken = portWasTaken;
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _runner = new GitRunner(TempRepo.GitPath);
        _environment = TempRepo.IsolatedEnvironment(root.Combine("home"));
        _projectRoot = projectRoot;
        _served = servedFolders.ToDictionary(NameOf, folder => folder, StringComparer.Ordinal);
        _single = folderServer ? null : servedFolders.Single();
    }

    /// <summary>The port the server listens on, on 127.0.0.1.</summary>
    public int Port { get; }

    /// <summary>
    /// How long a client may take to send its request: 60 s from the connection's start, and for
    /// a request held at a gate (<see cref="HoldNextRequest"/>), 60 s for the rest of it from the
    /// gate's opening. A test may shorten it for the connections that start afterwards.
    /// </summary>
    internal TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Whether the port asked for was taken, so that the server took one the system picked.</summary>
    public bool PortWasTaken { get; }

    /// <summary>Whether the server serves a folder of repositories (<see cref="StartForFolderAsync"/>) rather than one.</summary>
    public bool ServesFolder => _single is null;

    /// <summary>The served repositories' names, in ordinal order: their folder names without a final <c>.git</c> (<c>docs</c> for <c>docs.git</c>).</summary>
    public IReadOnlyList<string> ServedRepositories => [.. _served.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// The served repository's folder name, which is also the last part of <see cref="Url"/>:
    /// <see cref="RepositoryName"/> for the server's own repository, the existing folder's name
    /// for one served in place (<c>origin.git</c>). A folder server has none (<see cref="UrlOf"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">The server serves a folder.</exception>
    public string RepositoryFolderName => _single ?? throw new InvalidOperationException("A folder server serves several repositories: name one (UrlOf).");

    /// <summary>Whether the server serves existing repositories in place (one, or a folder of them), which it neither made nor deletes.</summary>
    public bool ServesInPlace => !string.Equals(_projectRoot, _root.Path, StringComparison.Ordinal);

    /// <summary>The served repository's URL: <c>http://127.0.0.1:&lt;port&gt;/remote.git</c>, or <c>/origin.git</c> for <c>origin.git</c> served in place.</summary>
    /// <exception cref="InvalidOperationException">The server serves a folder (<see cref="UrlOf"/>).</exception>
    public string Url => UrlOf(RepositoryFolderName);

    /// <summary>The served bare repository's folder, for git calls that go around the server.</summary>
    /// <exception cref="InvalidOperationException">The server serves a folder.</exception>
    public string RepositoryPath => Path.Combine(_projectRoot, RepositoryFolderName);

    /// <summary>
    /// The URL of the served repository <paramref name="repository"/>, named by its folder name
    /// (<c>docs.git</c>) or without its final <c>.git</c> (<c>docs</c>):
    /// <c>http://127.0.0.1:&lt;port&gt;/docs.git</c>, the folder name as the URL's path.
    /// </summary>
    /// <exception cref="ArgumentException">The server serves no such repository.</exception>
    public string UrlOf(string repository) =>
        string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{Port}/{FolderOf(repository)}");

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
    /// Starts listening on 127.0.0.1: on <paramref name="port"/>, or on a port the system picks
    /// when it is 0. With <paramref name="fallBackToFreePort"/>, a port that is taken gives way to
    /// one the system picks (<see cref="PortWasTaken"/>); without it, it throws
    /// <see cref="SocketException"/>.
    /// <para>
    /// Without <paramref name="existingRepository"/> the server serves an empty bare repository
    /// of its own (on main), <see cref="RepositoryName"/>, which disposing deletes. With it, the
    /// server serves that existing bare repository in place (design 7.3), at a URL whose path is
    /// the folder's name (<c>http://127.0.0.1:&lt;port&gt;/origin.git</c>), so that pushes made
    /// around the server (a scenario's <c>PushFromElsewhereAsync</c>) land in what it serves.
    /// The repository is not changed (its configuration included) and not deleted.
    /// </para>
    /// </summary>
    /// <param name="port">The port, or 0 for one the system picks.</param>
    /// <param name="fallBackToFreePort">Whether a taken port gives way to a free one.</param>
    /// <param name="existingRepository">A bare repository to serve in place; its folder name may hold only letters, digits, '.', '-' and '_'.</param>
    public static async Task<AuthRemoteServer> StartAsync(int port = 0, bool fallBackToFreePort = false, string? existingRepository = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        await HttpBackendCheck.Value.ConfigureAwait(false);
        var root = new TempDirectory("auth-server");
        TcpListener? listener = null;
        try
        {
            var served = existingRepository is null
                ? null
                : await ExistingRepositoryAsync(existingRepository, TempRepo.IsolatedEnvironment(root.Combine("home"))).ConfigureAwait(false);
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

            var server = served is null
                ? new AuthRemoteServer(root, listener, portWasTaken, root.Path, [RepositoryName], folderServer: false)
                : new AuthRemoteServer(root, listener, portWasTaken, served.Value.ProjectRoot, [served.Value.Name], folderServer: false);
            if (served is null)
            {
                await server.CreateRepositoryAsync().ConfigureAwait(false);
            }

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

    /// <summary>
    /// Starts a server that serves every bare repository directly in <paramref name="folder"/>
    /// in place (docs/phase-6-design.md 17.2): each at <see cref="UrlOf"/> its folder's name, each
    /// checked as a repository served in place is (a bare repository whose name needs no escaping
    /// in a URL, one <c>rev-parse --is-bare-repository</c> each), with <c>GIT_PROJECT_ROOT</c> the
    /// folder. Nothing in the folder is changed or deleted. On <paramref name="port"/>, or one the
    /// system picks when it is 0; with <paramref name="fallBackToFreePort"/> a taken port gives way
    /// to a free one, as for <see cref="StartAsync"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="folder"/> holds no bare repository, or a
    /// folder in it is no bare repository or has a name that needs escaping in a URL: a scenario
    /// whose remote is broken or misnamed fails here, not later with a 404.</exception>
    public static async Task<AuthRemoteServer> StartForFolderAsync(string folder, int port = 0, bool fallBackToFreePort = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        await HttpBackendCheck.Value.ConfigureAwait(false);
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if (!Directory.Exists(full))
        {
            throw new DirectoryNotFoundException($"No folder of repositories to serve at {full}.");
        }

        var root = new TempDirectory("auth-server");
        TcpListener? listener = null;
        try
        {
            var environment = TempRepo.IsolatedEnvironment(root.Combine("home"));
            var served = new List<string>();
            foreach (var candidate in Directory.EnumerateDirectories(full).Order(StringComparer.Ordinal))
            {
                var repository = await ExistingRepositoryAsync(candidate, environment).ConfigureAwait(false);
                served.Add(repository!.Value.Name);
            }

            if (served.Count == 0)
            {
                throw new ArgumentException($"{full} holds no bare repository to serve.", nameof(folder));
            }

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

            var server = new AuthRemoteServer(root, listener, portWasTaken, full, served, folderServer: true);
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

    /// <summary>A served repository's name: its folder's name without a final <c>.git</c>.</summary>
    private static string NameOf(string folder) =>
        folder.EndsWith(".git", StringComparison.Ordinal) && folder.Length > 4 ? folder[..^4] : folder;

    /// <summary>The folder name of the served repository <paramref name="repository"/>, given by its name or its folder name.</summary>
    private string FolderOf(string repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        if (_served.TryGetValue(repository, out var folder) || _served.TryGetValue(NameOf(repository), out folder) && folder == repository)
        {
            return folder;
        }

        throw new ArgumentException($"The server serves no repository {repository}; it serves {string.Join(", ", ServedRepositories)}.", nameof(repository));
    }

    /// <summary>
    /// Starts a server for a visual check: on <paramref name="port"/>, <see cref="VisualChecksPort"/>
    /// unless another is given (<see cref="Phase1VisualChecksPort"/>), or on a free port when it
    /// is taken; serving <paramref name="existingRepository"/> in place when given
    /// (<see cref="StartAsync"/>).
    /// </summary>
    public static Task<AuthRemoteServer> StartForVisualChecksAsync(string? existingRepository = null, int port = VisualChecksPort) =>
        StartAsync(port, fallBackToFreePort: true, existingRepository);

    /// <summary>
    /// Fails once per process, with a message that says why, when the tests' git has no
    /// <c>http-backend</c>, through which the server serves its repository. Visual Studio's Git
    /// is such a trimmed distribution (docs/plan.md risk 28): without this, every test against
    /// the server timed out or saw the server's errors. A full git answers a call without a
    /// request with a "500" response and exit 0; a git without the program says "is not a git
    /// command".
    /// </summary>
    private static readonly Lazy<Task> HttpBackendCheck = new(CheckHttpBackendAsync);

    private static async Task CheckHttpBackendAsync()
    {
        var command = new GitCommand("http-backend")
        {
            Environment = new Dictionary<string, string?> { ["REQUEST_METHOD"] = null },
        };
        var result = await new GitRunner(TempRepo.GitPath).RunAsync(command).ConfigureAwait(false);
        if (!result.Succeeded && result.StandardError.Contains("is not a git command", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The git at {TempRepo.GitPath} has no http-backend, which AuthRemoteServer serves its repository through. " +
                "Visual Studio's Git is such a trimmed distribution (docs/plan.md risk 28): put a full Git first on PATH.");
        }
    }

    /// <summary>The folder that holds <paramref name="repository"/> and its name, once it is known to be a bare repository with a name that needs no escaping in a URL.</summary>
    private static async Task<(string ProjectRoot, string Name)?> ExistingRepositoryAsync(string repository, IReadOnlyDictionary<string, string?> environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repository));
        if (!Directory.Exists(full))
        {
            throw new DirectoryNotFoundException($"No repository to serve at {full}.");
        }

        var name = Path.GetFileName(full);
        if (name.Length == 0 || name is "." or ".." || !name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_'))
        {
            throw new ArgumentException($"The served folder's name must need no escaping in a URL: '{name}'.", nameof(repository));
        }

        var command = new GitCommand("rev-parse", "--is-bare-repository")
        {
            WorkingDirectory = full,
            Environment = environment,
        };
        var bare = await new GitRunner(TempRepo.GitPath).RunAsync(command).ConfigureAwait(false);
        if (!bare.Succeeded || bare.StandardOutput.Trim() != "true")
        {
            throw new ArgumentException($"{full} is not a bare repository.", nameof(repository));
        }

        return (Path.GetDirectoryName(full)!, name);
    }

    /// <summary>
    /// Holds back the next response that carries a pack: one to an upload-pack POST that asks for
    /// a pack (under protocol v2 a body with <c>command=fetch</c>, not the <c>command=ls-refs</c>
    /// that comes first; under protocol v0 any) and whose response has pack data. The server
    /// sends the headers and the first part of the response, then waits for
    /// <see cref="Release"/>; git meanwhile shows the progress it got and waits for the rest.
    /// <para>
    /// With <paramref name="atPercent"/> 0 the first part ends with the first progress line
    /// (side band 2) that holds a <c>%</c>: a pkt-line, which may hold several of git's lines
    /// (<c>Counting objects:   0%</c> to <c>3%</c> for the 100k-commit repo). With a percentage
    /// it ends right after the first whole line of git's (up to its <c>\r</c> or <c>\n</c>)
    /// whose percentage is at least that one; when more follows that line in its pkt-line, the
    /// pkt-line is split in two there and the response sent with the length of the rebuilt body
    /// (<see cref="SplitAtProgress"/>). Git shows a line only once it is whole, so the last line
    /// it shows while held is that one: <c>Counting objects:  45% (…)</c> for 45 (D150).
    /// </para>
    /// <para>
    /// When git asked for no progress (<c>no-progress</c>, a fetch without <c>--progress</c> and
    /// without a terminal), or no line reaches the percentage, only the headers go first.
    /// Calling it again before a response was held changes nothing, the percentage included.
    /// </para>
    /// </summary>
    /// <param name="atPercent">0, or the percentage, 1 to 100, of the progress line to hold at.</param>
    /// <returns>A task that completes when the response is held: its first part has been sent.
    /// It is cancelled when <see cref="Release()"/> comes first or the server is disposed.</returns>
    /// <exception cref="InvalidOperationException">The server serves a folder: name the repository (<see cref="HoldNextFetchPack(string, int)"/>).</exception>
    public Task HoldNextFetchPack(int atPercent = 0) => HoldNextFetchPack(RepositoryFolderName, atPercent);

    /// <summary>
    /// Holds back the next response that carries a pack of the served repository
    /// <paramref name="repository"/> (its name or folder name), as <see cref="HoldNextFetchPack(int)"/>
    /// does on a server of one repository, D150's split at <paramref name="atPercent"/> included
    /// (docs/phase-6-design.md 17.2): each repository has a hold of its own, taken by its own next
    /// pack response, so that a folder server holds several fetches at their own percentages and
    /// releases them one at a time (<see cref="Release(string)"/>). Calling it again for the same
    /// repository before a response was held changes nothing.
    /// </summary>
    /// <exception cref="ArgumentException">The server serves no such repository.</exception>
    public Task HoldNextFetchPack(string repository, int atPercent = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(atPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(atPercent, 100);
        var folder = FolderOf(repository);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (!_pendingHolds.TryGetValue(folder, out var hold))
            {
                hold = new Hold(folder, atPercent);
                _pendingHolds.Add(folder, hold);
            }

            return hold.Engaged.Task;
        }
    }

    /// <summary>Sends the rest of every held response, of every served repository, and drops every hold that has not begun.</summary>
    public void Release() => ReleaseHolds(null);

    /// <summary>Sends the rest of the held responses of <paramref name="repository"/> (its name or folder name) and drops its hold that has not begun; the other repositories' stay held.</summary>
    /// <exception cref="ArgumentException">The server serves no such repository.</exception>
    public void Release(string repository) => ReleaseHolds(FolderOf(repository));

    private void ReleaseHolds(string? folder)
    {
        List<Hold> holds;
        lock (_gate)
        {
            holds = [.. _activeHolds.Where(hold => folder is null || hold.Folder == folder)];
            _activeHolds.RemoveAll(holds.Contains);
            foreach (var (key, pending) in _pendingHolds.Where(entry => folder is null || entry.Key == folder).ToList())
            {
                holds.Add(pending);
                _pendingHolds.Remove(key);
            }
        }

        foreach (var hold in holds)
        {
            hold.Engaged.TrySetCanceled();
            hold.Released.TrySetResult();
        }
    }

    /// <summary>
    /// Holds the next request of any kind to the served repository <paramref name="repository"/>
    /// (its name or folder name) as it arrives, before the server decides whether it is signed in,
    /// until <see cref="ReleaseRequest"/> or disposing opens it (docs/phase-6-design.md 17.2): a
    /// check orders two repositories' sign-ins with it (W6, 4.4). Nothing about the request is
    /// recorded (<see cref="Requests"/>) until it goes on; the other repositories are served
    /// meanwhile. Calling it again before a request arrived changes nothing.
    /// </summary>
    /// <returns>A task that completes when the request has arrived and waits. It is cancelled when
    /// <see cref="ReleaseRequest"/> comes first or the server is disposed.</returns>
    /// <exception cref="ArgumentException">The server serves no such repository.</exception>
    public Task HoldNextRequest(string repository)
    {
        var folder = FolderOf(repository);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (!_pendingGates.TryGetValue(folder, out var gate))
            {
                gate = new RequestGate(folder);
                _pendingGates.Add(folder, gate);
            }

            return gate.Arrived.Task;
        }
    }

    /// <summary>Lets the request held for <paramref name="repository"/> go on (<see cref="HoldNextRequest"/>), or drops its gate when no request has arrived yet.</summary>
    /// <exception cref="ArgumentException">The server serves no such repository.</exception>
    public void ReleaseRequest(string repository) => OpenGates(FolderOf(repository));

    private void OpenGates(string? folder)
    {
        List<RequestGate> gates;
        lock (_gate)
        {
            gates = [.. _closedGates.Where(gate => folder is null || gate.Folder == folder)];
            _closedGates.RemoveAll(gates.Contains);
            foreach (var (key, pending) in _pendingGates.Where(entry => folder is null || entry.Key == folder).ToList())
            {
                gates.Add(pending);
                _pendingGates.Remove(key);
            }
        }

        foreach (var gate in gates)
        {
            gate.Arrived.TrySetCanceled();
            gate.Opened.TrySetResult();
        }
    }

    /// <summary>Stops listening, ends every connection (a held response is not sent) and deletes the server's own repository; one served in place stays.</summary>
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
            holds = [.. _activeHolds, .. _pendingHolds.Values];
            _activeHolds.Clear();
            _pendingHolds.Clear();
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        foreach (var hold in holds)
        {
            hold.Engaged.TrySetCanceled();
            hold.Released.TrySetCanceled();
        }

        // A held request goes on, into a server that is stopping.
        OpenGates(null);

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

    /// <param name="connectionRead">Cancelled when the connection's time to send its request (<see cref="RequestTimeout"/>) has run out.</param>
    private async Task HandleAsync(NetworkStream stream, RequestReader reader, RequestHead head, CancellationToken connectionRead)
    {
        var stopping = _stopping.Token;
        var (path, query) = SplitTarget(head.Target);

        // A gate on this repository's next request (HoldNextRequest) holds it here, before
        // anything is decided about it. The time to send the rest of the request starts again
        // once it goes on, on a timer of its own: the connection's may have run out meanwhile,
        // and a cancelled timer cannot be started again.
        using var afterGate = await PassGateAsync(path, stopping).ConfigureAwait(false);
        var readCancelled = afterGate?.Token ?? connectionRead;
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

        // A hold applies to an upload-pack POST that asks for a pack and gets one. A hold at a
        // percentage may rebuild the body (a pkt-line split in two); the rebuilt one is sent.
        Hold? hold = null;
        var holdAt = 0;
        var sent = response.Body;
        var asksForPack = head.Method == "POST"
            && path.EndsWith("/git-upload-pack", StringComparison.Ordinal)
            && (!IsVersion2(protocol) || command == "fetch");
        if (asksForPack && response.Status == 200)
        {
            var (hasPack, progressEnd) = ScanPktLines(response.Body);
            if (hasPack)
            {
                hold = TakePendingHold(path);
                if (hold is { AtPercent: > 0 })
                {
                    (sent, var lineEnd) = SplitAtProgress(response.Body, hold.AtPercent);
                    holdAt = Math.Max(lineEnd, 0);
                }
                else
                {
                    holdAt = Math.Max(progressEnd, 0);
                }
            }
        }

        Record(request with { Status = response.Status, Held = hold is not null });
        var responseHead = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"HTTP/1.1 {response.Status} {response.Reason}\r\n")
            .Append(response.Headers)
            .Append(CultureInfo.InvariantCulture, $"Content-Length: {sent.Length}\r\n")
            .Append("Connection: close\r\n\r\n")
            .ToString();
        await stream.WriteAsync(Encoding.Latin1.GetBytes(responseHead), stopping).ConfigureAwait(false);

        if (hold is null)
        {
            await stream.WriteAsync(sent, stopping).ConfigureAwait(false);
            return;
        }

        try
        {
            await stream.WriteAsync(sent.AsMemory(0, holdAt), stopping).ConfigureAwait(false);
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
        await stream.WriteAsync(sent.AsMemory(holdAt), stopping).ConfigureAwait(false);
    }

    /// <summary>The hold that waits for the next pack response of the repository <paramref name="path"/> lies in, made active; null when there is none.</summary>
    private Hold? TakePendingHold(string path)
    {
        if (RepositoryOf(path) is not { } folder)
        {
            return null;
        }

        lock (_gate)
        {
            if (_pendingHolds.Remove(folder, out var hold))
            {
                _activeHolds.Add(hold);
            }

            return hold;
        }
    }

    /// <summary>
    /// Waits at the gate on the next request of the repository <paramref name="path"/> lies in,
    /// when there is one (<see cref="HoldNextRequest"/>), until it opens; then returns a new timer
    /// for the rest of the request, cancelled <see cref="RequestTimeout"/> from now or when the
    /// server stops. Null when no gate held the request.
    /// </summary>
    private async Task<CancellationTokenSource?> PassGateAsync(string path, CancellationToken stopping)
    {
        if (TakeGate(path) is not { } gate)
        {
            return null;
        }

        gate.Arrived.TrySetResult();
        await gate.Opened.Task.WaitAsync(stopping).ConfigureAwait(false);
        var timer = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        timer.CancelAfter(RequestTimeout);
        return timer;
    }

    /// <summary>The gate that waits for the next request of the repository <paramref name="path"/> lies in, now closed on that request; null when there is none.</summary>
    private RequestGate? TakeGate(string path)
    {
        if (RepositoryOf(path) is not { } folder)
        {
            return null;
        }

        lock (_gate)
        {
            if (_pendingGates.Remove(folder, out var gate))
            {
                _closedGates.Add(gate);
            }

            return gate;
        }
    }

    private async Task<(string Output, string Error, int ExitCode)> RunBackendAsync(RequestHead head, string path, string query, string? user, byte[] body, CancellationToken cancellationToken)
    {
        var environment = new Dictionary<string, string?>(_environment)
        {
            ["GIT_PROJECT_ROOT"] = _projectRoot,
            ["GIT_HTTP_EXPORT_ALL"] = "1",

            // No background gc after a push, which would outlive the request; given through the
            // environment (Git 2.31 and newer), so that a repository served in place keeps its
            // configuration unchanged.
            ["GIT_CONFIG_COUNT"] = "2",
            ["GIT_CONFIG_KEY_0"] = "gc.auto",
            ["GIT_CONFIG_VALUE_0"] = "0",
            ["GIT_CONFIG_KEY_1"] = "receive.autogc",
            ["GIT_CONFIG_VALUE_1"] = "false",
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
            WorkingDirectory = _projectRoot,
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

    private bool IsInRepository(string path) => RepositoryOf(path) is not null;

    /// <summary>The folder name of the served repository a request's path lies in (<c>/docs.git/info/refs</c>: <c>docs.git</c>); null for a path outside every one, or with <c>..</c> in it.</summary>
    private string? RepositoryOf(string path)
    {
        if (path.Contains("..", StringComparison.Ordinal) || !path.StartsWith('/'))
        {
            return null;
        }

        var end = path.IndexOf('/', 1);
        if (end < 0)
        {
            return null;
        }

        var folder = path[1..end];
        return _served.Values.Contains(folder, StringComparer.Ordinal) ? folder : null;
    }

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

    /// <summary>
    /// Where a hold at <paramref name="atPercent"/> begins (<see cref="HoldNextFetchPack"/>):
    /// right after the first whole line of git's progress (side band 2, up to and including its
    /// <c>\r</c> or <c>\n</c>; a line may run over several pkt-lines) whose first percentage is
    /// at least <paramref name="atPercent"/>. When more follows that line in its pkt-line, the
    /// pkt-line is split in two right after the line, each part with its own 4-hex length and the
    /// band's byte (<see cref="SplitPktLine"/>), so that the first part ends with the line.
    /// Returns the body to send (5 bytes longer after a split) and the hold's offset in it; the
    /// body unchanged and -1 when no line reaches the percentage.
    /// </summary>
    internal static (byte[] Body, int HoldAt) SplitAtProgress(byte[] body, int atPercent)
    {
        var line = new StringBuilder();
        var offset = 0;
        while (offset + 4 <= body.Length)
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

            var end = offset + length;
            if (end > body.Length)
            {
                break;
            }

            // The band's byte, then git's text; other lines (pack data, ACK, NAK, packfile) do
            // not break a progress line that runs over several pkt-lines.
            if (length > 4 && body[offset + 4] == 2)
            {
                for (var i = offset + 5; i < end; i++)
                {
                    if (body[i] is not ((byte)'\r' or (byte)'\n'))
                    {
                        line.Append((char)body[i]);
                        continue;
                    }

                    if (PercentOf(line.ToString()) >= atPercent)
                    {
                        var lineEnd = i + 1;
                        return lineEnd == end ? (body, end) : (SplitPktLine(body, offset, lineEnd), lineEnd);
                    }

                    line.Clear();
                }
            }

            offset = end;
        }

        return (body, -1);
    }

    /// <summary>
    /// <paramref name="body"/> with the side-band pkt-line at <paramref name="offset"/> split in
    /// two at <paramref name="at"/>: the first part keeps the pkt-line's start and gets the length
    /// up to <paramref name="at"/>, the second the rest after a length and a band's byte of its
    /// own. Everything before <paramref name="at"/> stays where it was.
    /// </summary>
    private static byte[] SplitPktLine(byte[] body, int offset, int at)
    {
        var length = int.Parse(Encoding.ASCII.GetString(body, offset, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        var end = offset + length;
        var split = new byte[body.Length + 5];
        body.AsSpan(0, at).CopyTo(split);
        WritePktLength(split.AsSpan(offset, 4), at - offset);
        WritePktLength(split.AsSpan(at, 4), 4 + 1 + (end - at));
        split[at + 4] = body[offset + 4];
        body.AsSpan(at).CopyTo(split.AsSpan(at + 5));
        return split;
    }

    private static void WritePktLength(Span<byte> target, int length) =>
        Encoding.ASCII.GetBytes(length.ToString("x4", CultureInfo.InvariantCulture), target);

    /// <summary>The first percentage in a line of git's progress (45 for <c>Counting objects:  45% (45/100)</c>); -1 when it has none.</summary>
    private static int PercentOf(string line)
    {
        for (var percent = line.IndexOf('%', StringComparison.Ordinal); percent >= 0; percent = line.IndexOf('%', percent + 1))
        {
            var start = percent;
            while (start > 0 && char.IsAsciiDigit(line[start - 1]))
            {
                start--;
            }

            if (start < percent && int.TryParse(line.AsSpan(start, percent - start), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return -1;
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

    /// <summary>One hold: begun when the first part of the response is sent, ended by <see cref="Release()"/> or <see cref="Release(string)"/>.</summary>
    private sealed class Hold(string folder, int atPercent)
    {
        /// <summary>The folder name of the served repository whose pack response it holds.</summary>
        public string Folder { get; } = folder;

        /// <summary>0, or the percentage of the progress line the hold begins after (<see cref="HoldNextFetchPack(string, int)"/>).</summary>
        public int AtPercent { get; } = atPercent;

        public TaskCompletionSource Engaged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>A gate on a served repository's next request (<see cref="HoldNextRequest"/>): closed when the request arrives, opened by <see cref="ReleaseRequest"/> or disposing.</summary>
    private sealed class RequestGate(string folder)
    {
        public string Folder { get; } = folder;

        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Opened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
