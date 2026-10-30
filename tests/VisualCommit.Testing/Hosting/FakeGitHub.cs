using System.Collections.Concurrent;
using System.Globalization;
using VisualCommit.Core;
using VisualCommit.Git;
using VisualCommit.Hosting;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// A local GitHub for the tests (docs/phase-7a-design.md 2.3, 2.4; D250, D266, D267): git, the
/// REST API under <c>api/v3/</c>, avatars and web pages on one host,
/// <c>http://127.0.0.1:&lt;port&gt;/</c>, through <see cref="AuthRemoteServer"/>'s host mode
/// (<see cref="StartAsync"/>), or the same routes in process without a server
/// (<see cref="InProcess"/>, <see cref="CreateHandler"/>). It is stricter than GitHub: every API
/// request needs a token and the pinned <c>X-GitHub-Api-Version</c>.
/// <para>
/// Users, tokens, repositories and pull requests are added by the test. Repositories are bare
/// repositories at <c>&lt;root&gt;/&lt;owner&gt;/&lt;name&gt;.git</c>; pull requests live in
/// memory, their heads in the base repository's <c>refs/pull/&lt;n&gt;/head</c>, kept in step
/// before a list, a create and git's ref listings (D267). State is kept under one lock; git runs
/// outside it, isolated from the machine (<see cref="TempRepo.IsolatedEnvironment"/>).
/// </para>
/// <para>
/// <see cref="Requests"/> never holds a token; checks assert <see cref="Errors"/> empty.
/// </para>
/// </summary>
public sealed partial class FakeGitHub : IAsyncDisposable
{
    /// <summary>The port of <see cref="InProcess"/>'s endpoints, <c>Local(1)</c>: never listened on.</summary>
    public const int InProcessPort = 1;

    /// <summary>How long a held request (<see cref="HoldNext"/>) waits at most before it goes on with an <see cref="Errors"/> line.</summary>
    private static readonly TimeSpan HoldLimit = TimeSpan.FromSeconds(120);

    private readonly Lock _gate = new();
    private readonly string _root;
    private readonly TempDirectory _home;
    private readonly GitRunner _runner;
    private readonly IReadOnlyDictionary<string, string?> _environment;
    private readonly Dictionary<string, FakeUser> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FakeToken> _tokens = new(StringComparer.Ordinal);
    private readonly List<FakeRepository> _repositories = [];
    private readonly Dictionary<string, FakeRepository> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FakePullRequest> _pulls = [];
    private readonly Dictionary<string, string> _emails = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PendingHold> _holds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FakeGitHubFault> _faults = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _used = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _branches = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _repositoryLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FakeGitHubRequest> _requests = [];
    private readonly List<string> _errors = [];
    private AuthRemoteServer? _server;
    private long _nextRepositoryId = 3001;
    private int _tokenCount;
    private int _requestCount;
    private int _maxPerPage = 100;
    private int _disposed;

    private FakeGitHub(string root)
    {
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        Directory.CreateDirectory(_root);
        _home = new TempDirectory("fake-github");
        _runner = new GitRunner(TempRepo.GitPath);
        _environment = TempRepo.IsolatedEnvironment(_home.Combine("home"));
        Endpoints = GitHubEndpoints.Local(InProcessPort);
        Port = InProcessPort;
    }

    /// <summary>The fake's web and API addresses: <c>http://127.0.0.1:&lt;port&gt;/</c> and its <c>api/v3/</c>; <c>Local(1)</c> in process.</summary>
    public GitHubEndpoints Endpoints { get; private set; }

    /// <summary>The port the fake listens on; <see cref="InProcessPort"/> in process.</summary>
    public int Port { get; private set; }

    /// <summary>Whether the port asked for was taken, so that the fake took one the system picked.</summary>
    public bool PortWasTaken { get; private set; }

    /// <summary>The folder of the bare repositories, <c>&lt;root&gt;/&lt;owner&gt;/&lt;name&gt;.git</c>.</summary>
    public string Root => _root;

    /// <summary>The most a page of a list holds, whatever <c>per_page</c> asks for: 100, as on GitHub.</summary>
    public int MaxPerPage
    {
        get => Volatile.Read(ref _maxPerPage);
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            Volatile.Write(ref _maxPerPage, value);
        }
    }

    /// <summary>
    /// Starts the fake on 127.0.0.1 in <see cref="AuthRemoteServer"/>'s host mode on
    /// <paramref name="root"/> (made when missing): on <paramref name="port"/>, or one the system
    /// picks when it is 0; with <paramref name="fallBackToFreePort"/> a taken port gives way to a
    /// free one (<see cref="PortWasTaken"/>). Nothing under <paramref name="root"/> is deleted.
    /// </summary>
    public static async Task<FakeGitHub> StartAsync(string root, int port = 0, bool fallBackToFreePort = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ct.ThrowIfCancellationRequested();
        var fake = new FakeGitHub(root);
        try
        {
            var options = new AuthRemoteHostOptions(new Routes(fake), fake.LoginOfToken, fake.IsPublic, fake.MayPush, BeforeRefsAsync: fake.SyncBeforeGitAsync);
            fake._server = await AuthRemoteServer.StartForHostAsync(fake._root, options, port, fallBackToFreePort).ConfigureAwait(false);
            fake.Port = fake._server.Port;
            fake.PortWasTaken = fake._server.PortWasTaken;
            fake.Endpoints = GitHubEndpoints.Local(fake.Port);
            return fake;
        }
        catch
        {
            await fake.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// The fake without a server: its routes reached only through <see cref="CreateHandler"/>, by
    /// path from any host; git's paths get 404. <see cref="Endpoints"/> is <c>Local(1)</c>.
    /// </summary>
    public static FakeGitHub InProcess(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return new FakeGitHub(root);
    }

    /// <summary>A handler that serves the fake's routes in process, whatever the request's host; recorded in <see cref="Requests"/> as over TCP.</summary>
    public HttpMessageHandler CreateHandler() => new FakeGitHubHandler(this);

    /// <summary>Adds a user (or, with <see cref="FakeUser.IsOrganization"/> set, an organisation).</summary>
    /// <exception cref="ArgumentException">The login is taken.</exception>
    public FakeUser AddUser(string login, long id, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        lock (_gate)
        {
            if (_users.ContainsKey(login))
            {
                throw new ArgumentException($"The fake has a user {login} already.", nameof(login));
            }

            var user = new FakeUser(login, id, name);
            _users.Add(login, user);
            return user;
        }
    }

    /// <summary>
    /// Makes a token of <paramref name="login"/>: <c>github_pat_FAKE-&lt;login&gt;-&lt;n&gt;</c>, or
    /// for a classic one <c>ghp_FAKE-&lt;login&gt;-&lt;n&gt;</c> with <paramref name="scopes"/>
    /// (<c>repo</c> and <c>workflow</c> unless given), <c>n</c> from 1 across the fake's tokens.
    /// <paramref name="expires"/> is shown in <c>GitHub-Authentication-Token-Expiration</c>.
    /// </summary>
    public string AddToken(string login, FakeTokenKind kind = FakeTokenKind.FineGrained, IReadOnlyCollection<string>? scopes = null, DateTimeOffset? expires = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        lock (_gate)
        {
            var user = UserLocked(login) ?? throw new ArgumentException($"The fake has no user {login}.", nameof(login));
            var n = ++_tokenCount;
            var token = string.Create(CultureInfo.InvariantCulture, $"{(kind == FakeTokenKind.Classic ? "ghp" : "github_pat")}_FAKE-{user.Login}-{n}");
            _tokens.Add(token, new FakeToken(user.Login, kind, kind == FakeTokenKind.Classic ? [.. scopes ?? ["repo", "workflow"]] : [], expires));
            return token;
        }
    }

    /// <summary>Revokes <paramref name="token"/>: the next request with it, git's included, is refused.</summary>
    public void Revoke(string token)
    {
        lock (_gate)
        {
            if (_tokens.TryGetValue(token, out var known))
            {
                known.Revoked = true;
            }
        }
    }

    /// <summary>
    /// Adds <paramref name="count"/> API-only repositories of <paramref name="owner"/>,
    /// <c>&lt;owner&gt;/listed-0001</c> and up, for a long list (A2): no folder, so git gets 404.
    /// Each was updated a minute before the one before it, the first 60 days before
    /// <see cref="AppClocks.HarnessTime"/>.
    /// </summary>
    public void AddListedRepositories(string owner, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        lock (_gate)
        {
            for (var i = 1; i <= count; i++)
            {
                var name = string.Create(CultureInfo.InvariantCulture, $"listed-{i:D4}");
                var repository = new FakeRepository(_nextRepositoryId++, owner, name, false, null, null, "main", barePath: null)
                {
                    UpdatedAt = AppClocks.HarnessTime.AddDays(-60).AddMinutes(-i),
                };
                AddRepositoryLocked(repository);
            }
        }
    }

    /// <summary>
    /// Adds a pull request of <paramref name="owner"/>/<paramref name="repository"/>, in memory:
    /// number <paramref name="number"/>, or the repository's highest plus one; by
    /// <paramref name="author"/>, a known user; from <paramref name="headRef"/> of the same
    /// repository, or of <paramref name="headRepository"/> (<c>owner/name</c>; a name the fake does
    /// not know is a deleted fork, whose head is the existing <c>refs/pull/&lt;n&gt;/head</c>).
    /// No git runs: the head is read at the next ref sync.
    /// </summary>
    public FakePullRequest AddPullRequest(string owner, string repository, string title, string author, string headRef, string baseRef, string? headRepository = null, bool draft = false, int? number = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(headRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseRef);
        lock (_gate)
        {
            var target = RepositoryLocked(owner + "/" + repository) ?? throw new ArgumentException($"The fake has no repository {owner}/{repository}.", nameof(repository));
            var user = UserLocked(author) ?? throw new ArgumentException($"The fake has no user {author}.", nameof(author));
            var n = number ?? NextNumberLocked(target);
            if (_pulls.Exists(pull => pull.Repository == target.FullName && pull.Number == n))
            {
                throw new ArgumentException($"{target.FullName} has a pull request #{n} already.", nameof(number));
            }

            var pull = new FakePullRequest(target.FullName, n, title, string.Empty, user.Login, headRef, headRepository, baseRef, draft);
            _pulls.Add(pull);
            return pull;
        }
    }

    /// <summary>Links <paramref name="email"/> (case ignored) to <paramref name="login"/>: the commits API shows the user for commits by it, and <c>author=&lt;login&gt;</c> finds them.</summary>
    public void LinkEmail(string email, string login)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        lock (_gate)
        {
            var user = UserLocked(login) ?? throw new ArgumentException($"The fake has no user {login}.", nameof(login));
            _emails[email] = user.Login;
        }
    }

    /// <summary>The repository <paramref name="fullName"/> (<c>owner/name</c>, case ignored; any name of a renamed one).</summary>
    /// <exception cref="KeyNotFoundException">The fake has none.</exception>
    public FakeRepository Repository(string fullName)
    {
        lock (_gate)
        {
            return RepositoryLocked(fullName) ?? throw new KeyNotFoundException($"The fake has no repository {fullName}.");
        }
    }

    /// <summary>
    /// Renames <paramref name="fullName"/> to <paramref name="newName"/> under the same owner, as
    /// GitHub does: the API serves the new name and answers with it
    /// (<see cref="FakeRepository.CurrentFullName"/>, which the routes' keys name from then on),
    /// and every request of an older name gets a redirect to <c>api/v3/repositories/&lt;id&gt;</c>
    /// plus the rest of the path, the query kept: 301, or 307 for a POST or a PATCH. Git keeps the
    /// folder and the URL of the name it was added under, as GitHub keeps git's old URL working.
    /// A transfer to another owner is not modelled.
    /// </summary>
    /// <exception cref="ArgumentException">The fake has no such repository, the name is not plain, or another repository has it.</exception>
    public void Rename(string fullName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        if (!IsPlain(newName) || newName.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"A repository's name is of letters, digits, '.', '-' and '_': '{newName}'.", nameof(newName));
        }

        lock (_gate)
        {
            var repository = RepositoryLocked(fullName) ?? throw new ArgumentException($"The fake has no repository {fullName}.", nameof(fullName));
            var renamed = repository.Owner + "/" + newName;
            if (RepositoryLocked(renamed) is { } other && other != repository)
            {
                throw new ArgumentException($"The fake has a repository {renamed} already.", nameof(newName));
            }

            // Every name it had still finds it, so that an older one is redirected.
            _byName[renamed] = repository;
            repository.CurrentName = newName;
        }
    }

    /// <summary>The pull requests of <paramref name="fullName"/> (any of its names), by number.</summary>
    public IReadOnlyList<FakePullRequest> PullRequests(string fullName)
    {
        lock (_gate)
        {
            var key = RepositoryLocked(fullName)?.FullName ?? fullName;
            return [.. _pulls.Where(pull => string.Equals(pull.Repository, key, StringComparison.OrdinalIgnoreCase)).OrderBy(pull => pull.Number)];
        }
    }

    /// <summary>Every API, avatar and page request so far, in the order their answers were decided. Safe from another thread.</summary>
    public IReadOnlyList<FakeGitHubRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Every git request the server served (none in process). Safe from another thread.</summary>
    public IReadOnlyList<AuthRemoteRequest> GitRequests =>
        _server is null ? [] : [.. _server.Requests.Where(request => !Claims(request.Method, request.Path))];

    /// <summary>What went wrong in the fake or its server, ref syncs included: empty when all is well.</summary>
    public IReadOnlyList<string> Errors
    {
        get
        {
            lock (_gate)
            {
                return [.. _errors, .. _server?.Errors ?? []];
            }
        }
    }

    /// <summary>
    /// Holds the next request of <paramref name="route"/> (a key of 2.4's table: <c>user</c>,
    /// <c>pulls:octo/hello</c>, <c>avatar:1001</c>) before anything is decided about it, until the
    /// hold is disposed, at most 120 s (then it goes on with an <see cref="Errors"/> line).
    /// <c>git:octo/hello</c> holds the repository's next git request
    /// (<see cref="AuthRemoteServer.HoldNextRequest"/>), on a server only.
    /// </summary>
    /// <exception cref="InvalidOperationException">The route is held already, or a git route in process.</exception>
    public FakeGitHubHold HoldNext(string route)
    {
        var key = KeyOf(route);
        if (key.StartsWith("git:", StringComparison.Ordinal))
        {
            var server = _server ?? throw new InvalidOperationException("The fake in process serves no git: git:<owner>/<name> is held on a server only.");
            var name = key[4..];
            return new FakeGitHubHold(key, server.HoldNextRequest(name), new TaskCompletionSource().Task, () => server.ReleaseRequest(name));
        }

        var hold = new PendingHold();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (!_holds.TryAdd(key, hold))
            {
                throw new InvalidOperationException($"The route {key} is held already.");
            }
        }

        return new FakeGitHubHold(key, hold.Arrived.Task, hold.Abandoned.Task, () =>
        {
            lock (_gate)
            {
                if (_holds.TryGetValue(key, out var pending) && pending == hold)
                {
                    _holds.Remove(key);
                }
            }

            hold.Arrived.TrySetCanceled();
            hold.Released.TrySetResult();
        });
    }

    /// <summary>Answers the next request of <paramref name="route"/> with <paramref name="fault"/> once, after the header checks and before auth; the one after it is normal.</summary>
    /// <exception cref="NotSupportedException">A git, avatar or page route: those are answered before faults are taken, so a fault would never apply.</exception>
    public void FailNext(string route, FakeGitHubFault fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        var key = KeyOf(route);
        if (key.StartsWith("git:", StringComparison.Ordinal))
        {
            throw new NotSupportedException("The fake fails API routes only; git's requests are the server's.");
        }

        if (key.StartsWith("avatar:", StringComparison.Ordinal) || key.StartsWith("page:", StringComparison.Ordinal))
        {
            throw new NotSupportedException("The fake fails API routes only; an avatar or a page is answered before any fault.");
        }

        lock (_gate)
        {
            _faults[key] = fault;
        }
    }

    /// <summary>Releases every hold, then stops the server. The repositories stay.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        List<PendingHold> holds;
        lock (_gate)
        {
            holds = [.. _holds.Values];
            _holds.Clear();
        }

        foreach (var hold in holds)
        {
            hold.Arrived.TrySetCanceled();
            hold.Released.TrySetResult();
        }

        if (_server is not null)
        {
            await _server.DisposeAsync().ConfigureAwait(false);
        }

        _home.Dispose();
    }

    private static string KeyOf(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        return route.Trim().ToLowerInvariant();
    }

    private FakeUser? UserLocked(string login) => _users.GetValueOrDefault(login);

    private FakeRepository? RepositoryLocked(string fullName) =>
        _byName.GetValueOrDefault(fullName.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? fullName[..^4] : fullName);

    private FakeRepository? RepositoryById(long id)
    {
        lock (_gate)
        {
            return _repositories.Find(repository => repository.Id == id);
        }
    }

    private void AddRepositoryLocked(FakeRepository repository)
    {
        if (_byName.ContainsKey(repository.FullName))
        {
            throw new ArgumentException($"The fake has a repository {repository.FullName} already.");
        }

        if (UserLocked(repository.Owner) is { IsOrganization: false })
        {
            repository.AddPusher(repository.Owner);
        }

        _repositories.Add(repository);
        _byName.Add(repository.FullName, repository);
    }

    private int NextNumberLocked(FakeRepository repository) =>
        _pulls.Where(pull => pull.Repository == repository.FullName).Select(pull => pull.Number).DefaultIfEmpty(0).Max() + 1;

    /// <summary>A Basic password's login for git: a known token that is not revoked.</summary>
    private string? LoginOfToken(string token)
    {
        lock (_gate)
        {
            return _tokens.TryGetValue(token, out var known) && !known.Revoked ? known.Login : null;
        }
    }

    private bool IsPublic(string fullName)
    {
        lock (_gate)
        {
            return RepositoryLocked(fullName) is { IsPrivate: false, BarePath: not null };
        }
    }

    private bool MayPush(string fullName, string login)
    {
        lock (_gate)
        {
            return RepositoryLocked(fullName)?.MayPush(login) ?? false;
        }
    }

    private void RecordError(string error)
    {
        lock (_gate)
        {
            _errors.Add(error);
        }
    }

    /// <summary>A token the fake made: never shown, never recorded.</summary>
    private sealed class FakeToken(string login, FakeTokenKind kind, IReadOnlyList<string> scopes, DateTimeOffset? expires)
    {
        public string Login { get; } = login;

        public FakeTokenKind Kind { get; } = kind;

        public IReadOnlyList<string> Scopes { get; } = scopes;

        public DateTimeOffset? Expires { get; } = expires;

        public bool Revoked { get; set; }

        public override string ToString() => $"FakeToken({Login}, {Kind}, ***)";
    }

    /// <summary>A hold that no request has reached yet, or the one a request waits at.</summary>
    private sealed class PendingHold
    {
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completed when the wait here is cancelled before its release: by its sender in process, by the server's stop over TCP (<see cref="FakeGitHubHold.Abandoned"/>).</summary>
        public TaskCompletionSource Abandoned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>The fake's routes as the server's hook (<see cref="IAuthRemoteRoutes"/>).</summary>
    private sealed class Routes(FakeGitHub fake) : IAuthRemoteRoutes
    {
        public bool Claims(string method, string path) => fake.Claims(method, path);

        public Task<AuthRemoteResponse> ServeAsync(AuthRemoteRouteRequest request, CancellationToken cancellationToken) =>
            fake.ServeAsync(request, cancellationToken);
    }
}
