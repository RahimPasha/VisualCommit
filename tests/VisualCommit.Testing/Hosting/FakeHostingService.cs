using System.Text;
using VisualCommit.Core.Git;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The base of 7b's fakes of a hosting service (docs/phase-7b-design.md 1.9, 2.3, D363):
/// <see cref="FakeAzureDevOps"/> and <see cref="FakeGitLab"/>. A fake serves its API, pages and git
/// on one host, <c>http://127.0.0.1:&lt;port&gt;/</c>, through <see cref="AuthRemoteServer"/>'s host
/// mode (<see cref="StartServerAsync"/>), its repositories at paths of any depth
/// (<see cref="AuthRemoteServer.ServeAt"/>), or the same routes in process (<see cref="CreateHandler"/>).
/// 7a's <see cref="FakeGitHub"/> keeps its own surface over the same core.
/// <para>
/// A request goes through, in order: its route (<see cref="RouteOf"/>, a lower-case key), the hold
/// on that route (<see cref="HoldNext"/>), the fault waiting for it (<see cref="FailNext"/>):
/// <see cref="FakeServiceFault.Dropped"/> answers nothing; else the fake's answer
/// (<see cref="AnswerAsync"/>), which answers any other fault where its checks put it;
/// <see cref="FakeServiceFault.ServedThenDropped"/> then drops that answer; the record last. A
/// path the fake does not claim (in process: git's) gets 404 "Not found.". The credential's form and
/// token are read first (<see cref="FakeServiceCall"/>): <c>Bearer</c>, <c>Basic</c>,
/// <c>PRIVATE-TOKEN</c> and a <c>private_token</c> parameter, two of them <see cref="FakeAuthorizationForm.Other"/>.
/// </para>
/// <para>
/// A repository is named by its folder under <see cref="Root"/> without a final <c>.git</c>
/// (<c>Fernhill-Labs/Mobile App/_git/mobile-app</c>, <c>g/s/p</c>) in every callback, hold and gate,
/// whichever registered path reached it. <see cref="Requests"/> never holds a token; checks assert
/// <see cref="Errors"/> empty.
/// </para>
/// </summary>
public abstract class FakeHostingService : IAsyncDisposable
{
    private readonly FakeServiceCore<FakeServiceFault, FakeServiceRequest> _core;
    private readonly Dictionary<string, KnownToken> _tokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _servedNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _heldGit = new(StringComparer.OrdinalIgnoreCase);
    private int _maxPerPage = 100;

    /// <summary>A fake on <paramref name="root"/> (made when missing) named <paramref name="displayName"/>, in process on <paramref name="inProcessPort"/> until <see cref="StartServerAsync"/>.</summary>
    protected FakeHostingService(string root, string displayName, int inProcessPort)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        _core = new FakeServiceCore<FakeServiceFault, FakeServiceRequest>(root, "fake " + displayName);
        DisplayName = displayName;
        Port = inProcessPort;
    }

    /// <summary>The port the fake listens on; its in-process port (never listened on) until it is started.</summary>
    public int Port { get; private set; }

    /// <summary>Whether the port asked for was taken, so that the fake took one the system picked.</summary>
    public bool PortWasTaken { get; private set; }

    /// <summary>The folder of the fake's bare repositories.</summary>
    public string Root => _core.Root;

    /// <summary>The service's name in the fake's messages: "Azure DevOps", "GitLab".</summary>
    public string DisplayName { get; }

    /// <summary>The most a page of a list holds, whatever the request asks for: 100 unless set.</summary>
    public int MaxPerPage
    {
        get => Volatile.Read(ref _maxPerPage);
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            Volatile.Write(ref _maxPerPage, value);
        }
    }

    /// <summary>Every API and page request so far, in the order their answers were decided. Safe from another thread.</summary>
    public IReadOnlyList<FakeServiceRequest> Requests => _core.Requests;

    /// <summary>Every git request the server served (none in process). Safe from another thread.</summary>
    public IReadOnlyList<AuthRemoteRequest> GitRequests =>
        _core.Server is not { } server ? [] : [.. server.Requests.Where(request => !Claims(request.Method, request.Path))];

    /// <summary>What went wrong in the fake or its server, ref syncs included: empty when all is well.</summary>
    public IReadOnlyList<string> Errors => _core.Errors;

    /// <summary>The lock the fake's tables are kept under; git never runs inside it.</summary>
    protected Lock Gate => _core.Gate;

    /// <summary>The service's realm in a git 401's <c>WWW-Authenticate: Basic realm="…"</c>.</summary>
    protected abstract string Realm { get; }

    /// <summary>A handler that serves the fake's routes in process, whatever the request's host; recorded in <see cref="Requests"/> as over TCP.</summary>
    public HttpMessageHandler CreateHandler() => new FakeHostingServiceHandler(this);

    /// <summary>
    /// Holds the next request of <paramref name="route"/> (a route's key, without case) before anything
    /// is decided about it, until the hold is disposed, at most 120 s (then it goes on with an
    /// <see cref="Errors"/> line). <c>git:&lt;repository&gt;</c> (its folder's name, without case, with or
    /// without a final <c>.git</c>) holds that repository's next git request
    /// (<see cref="AuthRemoteServer.HoldNextRequest"/>), on a server only; it stays held until its
    /// hold is disposed, since the server's release opens every gate of the repository.
    /// </summary>
    /// <exception cref="InvalidOperationException">The route is held already (an API route until a request arrives at it, a git route until its hold is disposed), or a git route in process.</exception>
    /// <exception cref="ArgumentException">A git route names no repository the fake serves.</exception>
    public FakeServiceHold HoldNext(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        var trimmed = route.Trim();
        if (trimmed.StartsWith("git:", StringComparison.OrdinalIgnoreCase))
        {
            var server = _core.Server ?? throw new InvalidOperationException($"The fake of {DisplayName} in process serves no git: git:<repository> is held on a server only.");
            var name = ServedNameOf(trimmed[4..]) ?? throw new ArgumentException($"The fake of {DisplayName} serves no repository {trimmed[4..]}.", nameof(route));
            lock (Gate)
            {
                // A second gate would share the first's release (ReleaseRequest opens them all).
                if (!_heldGit.Add(name))
                {
                    throw new InvalidOperationException($"The route git:{name} is held already.");
                }
            }

            Task arrived;
            try
            {
                arrived = server.HoldNextRequest(name);
            }
            catch
            {
                lock (Gate)
                {
                    _heldGit.Remove(name);
                }

                throw;
            }

            var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return new FakeServiceHold("git:" + name, arrived, released.Task, new TaskCompletionSource().Task, () =>
            {
                if (released.TrySetResult())
                {
                    server.ReleaseRequest(name);
                    lock (Gate)
                    {
                        _heldGit.Remove(name);
                    }
                }
            });
        }

        var key = trimmed.ToLowerInvariant();
        var hold = _core.AddHold(key, this);
        return new FakeServiceHold(key, hold.Arrived, hold.Released, hold.Abandoned, hold.Release);
    }

    /// <summary>Answers the next request of <paramref name="route"/> (without case) with <paramref name="fault"/> once; the one after it is normal.</summary>
    /// <exception cref="NotSupportedException">A git route: git's requests are the server's.</exception>
    public void FailNext(string route, FakeServiceFault fault)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentNullException.ThrowIfNull(fault);
        var key = route.Trim().ToLowerInvariant();
        if (key.StartsWith("git:", StringComparison.Ordinal))
        {
            throw new NotSupportedException($"The fake of {DisplayName} fails its API's routes only; git's requests are the server's.");
        }

        _core.SetFault(key, fault);
    }

    /// <summary>Revokes <paramref name="token"/>: the next request with it, git's included, is refused.</summary>
    public void Revoke(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        lock (Gate)
        {
            if (_tokens.TryGetValue(token, out var known))
            {
                known.Revoked = true;
            }
        }
    }

    /// <summary>
    /// Releases every hold, a request already waiting at one included (over TCP it then meets the
    /// server's stop, so it may be cut off), then stops the server. The repositories stay.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _core.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>Whether the fake answers this request itself (its API and pages), decided from the request line alone (the raw path); git's paths are not claimed.</summary>
    protected abstract bool Claims(string method, string path);

    /// <summary>The route's lower-case key, the key of holds and faults (from the raw path); null when no route serves the path.</summary>
    protected abstract string? RouteOf(string method, string path);

    /// <summary>The answer to a claimed request, the fault of <see cref="FakeServiceCall.Fault"/> where its checks put it; it sets <see cref="FakeServiceCall.Login"/> once the token signed in.</summary>
    protected abstract Task<AuthRemoteResponse> AnswerAsync(FakeServiceCall call, CancellationToken ct);

    /// <summary>Whether git may read <paramref name="repository"/> (its folder's name) without a credential.</summary>
    protected abstract bool IsPublic(string repository);

    /// <summary>Whether <paramref name="login"/> may push to <paramref name="repository"/>.</summary>
    protected abstract bool MayPush(string repository, string login);

    /// <summary>The text of the 403 to a push <see cref="MayPush"/> refuses: 7a's, <c>Permission to &lt;repository&gt;.git denied to &lt;login&gt;.</c></summary>
    protected virtual string PushRefusal(string repository, string login) => $"Permission to {repository}.git denied to {login}.";

    /// <summary>The text of a 403 <c>text/plain</c> to a signed-in fetch, before http-backend; null: the fetch goes on.</summary>
    protected virtual string? FetchRefusal(string repository, string login) => null;

    /// <summary>A Basic password's login for git in <paramref name="repository"/>: a known token that is not revoked (<see cref="TokenLoginLocked"/>).</summary>
    protected virtual string? LoginOfToken(string repository, string token)
    {
        lock (Gate)
        {
            return TokenLoginLocked(token);
        }
    }

    /// <summary>Called before git lists <paramref name="repository"/>'s refs (the ref sync); none unless overridden. A failure is an <see cref="Errors"/> line.</summary>
    protected virtual Task BeforeGitAsync(string repository, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Starts the server on <see cref="Root"/> in host mode on <paramref name="port"/> (0: one the
    /// system picks), a taken port giving way to a free one with <paramref name="fallBackToFreePort"/>;
    /// sets <see cref="Port"/> and <see cref="PortWasTaken"/>. The virtuals above are its callbacks.
    /// </summary>
    protected async Task StartServerAsync(int port, bool fallBackToFreePort)
    {
        var options = new AuthRemoteHostOptions(
            new Routes(this),
            token =>
            {
                lock (Gate)
                {
                    return TokenLoginLocked(token);
                }
            },
            IsPublic,
            MayPush,
            Realm,
            BeforeGitSafelyAsync,
            PushRefusal,
            LoginOfToken,
            FetchRefusal);
        var server = await _core.StartServerAsync(options, port, fallBackToFreePort).ConfigureAwait(false);
        Port = server.Port;
        PortWasTaken = server.PortWasTaken;
    }

    /// <summary>Makes <paramref name="token"/> known as <paramref name="login"/>'s until <see cref="Revoke"/>; under <see cref="Gate"/>.</summary>
    protected void AddTokenLocked(string token, string login)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        ArgumentException.ThrowIfNullOrEmpty(login);
        _tokens[token] = new KnownToken(login);
    }

    /// <summary>The login of a known token that is not revoked; null otherwise. Under <see cref="Gate"/>.</summary>
    protected string? TokenLoginLocked(string token) =>
        _tokens.TryGetValue(token, out var known) && !known.Revoked ? known.Login : null;

    /// <summary>
    /// The bare repository <c>&lt;Root&gt;/&lt;urlPath&gt;</c> (plus <c>.git</c> with
    /// <paramref name="gitSuffix"/>): in place when it holds <c>HEAD</c>; else a
    /// <c>git clone --bare --no-hardlinks</c> of <paramref name="source"/>; else a new empty one
    /// (<c>git init --bare --initial-branch main</c>). Then served at <paramref name="urlPath"/>
    /// (<see cref="ServeAt"/>). Returns its folder.
    /// </summary>
    /// <exception cref="ArgumentException">A segment of <paramref name="urlPath"/> is empty, <c>.</c> or <c>..</c>, or holds <c>\</c> or a control character.</exception>
    protected async Task<string> BareRepositoryAsync(string urlPath, bool gitSuffix, string? source, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(urlPath);
        if (!AuthRemoteServer.IsServablePath(urlPath))
        {
            throw new ArgumentException($"A repository's path is of segments that are not empty, '.' or '..' and hold no '\\' or control character: '{urlPath}'.", nameof(urlPath));
        }

        var folder = urlPath + (gitSuffix ? ".git" : string.Empty);
        var bare = Path.Combine([Root, .. folder.Split('/')]);
        if (!File.Exists(Path.Combine(bare, "HEAD")))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(bare)!);
            if (source is not null)
            {
                await _core.GitOkAsync(Root, null, null, ct, "clone", "--quiet", "--bare", "--no-hardlinks", "--", source, bare).ConfigureAwait(false);
            }
            else
            {
                await _core.GitOkAsync(Root, null, null, ct, "init", "--quiet", "--bare", "--initial-branch", "main", bare).ConfigureAwait(false);
            }
        }

        ServeAt(urlPath, folder, gitSuffix);
        return bare;
    }

    /// <summary>
    /// Serves the bare repository <paramref name="folder"/> (relative to <see cref="Root"/>, with
    /// <c>/</c>) at <paramref name="urlPath"/> too (<see cref="AuthRemoteServer.ServeAt"/>, on a server
    /// only), and makes it known to <see cref="HoldNext"/>'s git routes.
    /// </summary>
    protected void ServeAt(string urlPath, string folder, bool gitSuffix)
    {
        _core.Server?.ServeAt(urlPath, folder, gitSuffix);
        var name = NameOf(folder);
        lock (Gate)
        {
            _servedNames.TryAdd(name, name);
        }
    }

    /// <summary>Runs git in <paramref name="folder"/>, isolated from the machine, with <paramref name="environment"/> over that (commit-tree's names and dates; a null value removes a variable).</summary>
    protected Task<GitResult> GitAsync(string folder, string? input, IReadOnlyDictionary<string, string?>? environment, CancellationToken ct, params string[] arguments) =>
        _core.GitAsync(folder, input, environment, ct, arguments);

    /// <summary>The lock of one repository's ref sync and creates (by its name, without case), so that two never interleave.</summary>
    protected SemaphoreSlim LockOf(string repository) => _core.LockOf(repository);

    /// <summary>Records what went wrong, for <see cref="Errors"/>.</summary>
    protected void RecordError(string text) => _core.RecordError(text);

    /// <summary>Answers one request, over TCP or in process (2.3's order); see the class's summary.</summary>
    internal async Task<AuthRemoteResponse> ServeAsync(AuthRemoteRouteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var claimed = Claims(request.Method, request.Path);
        var route = claimed ? RouteOf(request.Method, request.Path)?.ToLowerInvariant() : null;
        if (route is not null)
        {
            await _core.WaitAtHoldAsync(route, ct).ConfigureAwait(false);
        }

        var fault = route is null ? null : _core.TakeFault(route);
        var (form, token) = CredentialOf(request);
        var handed = fault == FakeServiceFault.Dropped || fault == FakeServiceFault.ServedThenDropped ? null : fault;
        var call = new FakeServiceCall(request, route, handed, form, token);
        AuthRemoteResponse response;
        if (fault == FakeServiceFault.Dropped)
        {
            response = new AuthRemoteResponse(0, string.Empty, [], [], Drop: true);
        }
        else if (!claimed)
        {
            // In process, git's paths and any other the fake does not claim.
            response = new AuthRemoteResponse(404, "Not Found", [KeyValuePair.Create("Content-Type", "text/plain; charset=utf-8")], "Not found.\n"u8.ToArray());
        }
        else
        {
            response = await AnswerAsync(call, ct).ConfigureAwait(false);
            if (fault == FakeServiceFault.ServedThenDropped)
            {
                response = response with { Drop = true };
            }
        }

        var body = request.Method is "POST" or "PUT" or "PATCH" ? Encoding.UTF8.GetString(request.Body) : null;
        _core.Record(new FakeServiceRequest(request.Method, request.Host, request.Path, MaskedQuery(request.Query), route, form, call.Login, response.Status, body));
        return response;
    }

    /// <summary>The credential a request carried: its form and token (2.3).</summary>
    private static (FakeAuthorizationForm Form, string? Token) CredentialOf(AuthRemoteRouteRequest request)
    {
        var found = new List<(FakeAuthorizationForm Form, string? Token)>();
        if (request.Headers.TryGetValue("Authorization", out var authorization))
        {
            found.Add(FromAuthorization(authorization));
        }

        if (request.Headers.TryGetValue("PRIVATE-TOKEN", out var privateToken))
        {
            found.Add(privateToken.Length > 0 && !privateToken.Contains(',', StringComparison.Ordinal) ? (FakeAuthorizationForm.PrivateToken, privateToken) : (FakeAuthorizationForm.Other, null));
        }

        foreach (var value in QueryValues(request.Query, "private_token"))
        {
            found.Add(value.Length > 0 ? (FakeAuthorizationForm.Query, value) : (FakeAuthorizationForm.Other, null));
        }

        return found.Count switch
        {
            0 => (FakeAuthorizationForm.None, null),
            1 => found[0],
            _ => (FakeAuthorizationForm.Other, null),
        };
    }

    /// <summary>An <c>Authorization</c> header's form and token: Bearer, Basic with a user or with an empty one; another scheme, an unreadable Basic, an empty token or two headers (joined by a comma), <see cref="FakeAuthorizationForm.Other"/>.</summary>
    private static (FakeAuthorizationForm Form, string? Token) FromAuthorization(string authorization)
    {
        if (authorization.Contains(',', StringComparison.Ordinal))
        {
            return (FakeAuthorizationForm.Other, null);
        }

        const string Bearer = "Bearer ", Basic = "Basic ";
        if (authorization.StartsWith(Bearer, StringComparison.OrdinalIgnoreCase))
        {
            var token = authorization[Bearer.Length..].Trim();
            return token.Length > 0 ? (FakeAuthorizationForm.Bearer, token) : (FakeAuthorizationForm.Other, null);
        }

        if (!authorization.StartsWith(Basic, StringComparison.OrdinalIgnoreCase))
        {
            return (FakeAuthorizationForm.Other, null);
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[Basic.Length..].Trim()));
        }
        catch (FormatException)
        {
            return (FakeAuthorizationForm.Other, null);
        }

        var colon = decoded.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0 || colon == decoded.Length - 1)
        {
            return (FakeAuthorizationForm.Other, null);
        }

        return (colon == 0 ? FakeAuthorizationForm.BasicEmptyUser : FakeAuthorizationForm.Basic, decoded[(colon + 1)..]);
    }

    /// <summary>The values of the query's parameters named <paramref name="key"/>, read with <c>+</c> as a space.</summary>
    private static IEnumerable<string> QueryValues(string query, string key)
    {
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = part.IndexOf('=', StringComparison.Ordinal);
            var name = Uri.UnescapeDataString((equals < 0 ? part : part[..equals]).Replace('+', ' '));
            if (name == key)
            {
                yield return equals < 0 ? string.Empty : Uri.UnescapeDataString(part[(equals + 1)..].Replace('+', ' '));
            }
        }
    }

    /// <summary>The query as recorded: a <c>private_token</c> parameter's value replaced by <c>***</c>, the rest as it came.</summary>
    private static string MaskedQuery(string query) =>
        string.Join('&', query.Split('&').Select(part =>
        {
            var equals = part.IndexOf('=', StringComparison.Ordinal);
            var name = Uri.UnescapeDataString((equals < 0 ? part : part[..equals]).Replace('+', ' '));
            return name == "private_token" ? "private_token=***" : part;
        }));

    /// <summary>A served repository's name in its folder's case, from a name without case (with or without a final <c>.git</c>); null when the fake serves none.</summary>
    private string? ServedNameOf(string repository)
    {
        var name = NameOf(repository.Trim());
        lock (Gate)
        {
            return _servedNames.GetValueOrDefault(name);
        }
    }

    private static string NameOf(string folder) =>
        folder.EndsWith(".git", StringComparison.OrdinalIgnoreCase) && folder.Length > 4 ? folder[..^4] : folder;

    /// <summary>The server's hook before git lists a repository's refs: <see cref="BeforeGitAsync"/>, which never throws out of it.</summary>
    private async Task BeforeGitSafelyAsync(string repository, CancellationToken ct)
    {
        try
        {
            await BeforeGitAsync(repository, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordError($"The ref sync of {repository} before git's request failed: {ex.Message}");
        }
    }

    /// <summary>A token the fake knows: never shown, never recorded.</summary>
    private sealed class KnownToken(string login)
    {
        public string Login { get; } = login;

        public bool Revoked { get; set; }

        public override string ToString() => $"KnownToken({Login}, ***)";
    }

    /// <summary>The fake's routes as the server's hook (<see cref="IAuthRemoteRoutes"/>).</summary>
    private sealed class Routes(FakeHostingService fake) : IAuthRemoteRoutes
    {
        public bool Claims(string method, string path) => fake.Claims(method, path);

        public Task<AuthRemoteResponse> ServeAsync(AuthRemoteRouteRequest request, CancellationToken cancellationToken) =>
            fake.ServeAsync(request, cancellationToken);
    }
}
