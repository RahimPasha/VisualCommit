using System.Collections.Concurrent;
using VisualCommit.Core;

namespace VisualCommit.Testing.Hosting;

/// <summary>A token's kind (docs/phase-7a-design.md 2.3): <c>ghp_FAKE-…</c> or <c>github_pat_FAKE-…</c>.</summary>
public enum FakeTokenKind
{
    /// <summary>A classic token, <c>ghp_FAKE-&lt;login&gt;-&lt;n&gt;</c>, with scopes (<c>X-OAuth-Scopes</c>).</summary>
    Classic,

    /// <summary>A fine-grained token, <c>github_pat_FAKE-&lt;login&gt;-&lt;n&gt;</c>.</summary>
    FineGrained,
}

/// <summary>A user or an organisation of the fake (<see cref="FakeGitHub.AddUser"/>).</summary>
public sealed class FakeUser
{
    private byte[] _avatarBytes;

    internal FakeUser(string login, long id, string? name)
    {
        Login = login;
        Id = id;
        Name = name;
        _avatarBytes = DefaultAvatar(id);
    }

    public string Login { get; }

    public long Id { get; }

    /// <summary>The display name; null when the account has none.</summary>
    public string? Name { get; }

    /// <summary>Whether the account is an organisation (<c>type</c> <c>Organization</c>): it pushes to nothing by itself.</summary>
    public bool IsOrganization { get; set; }

    /// <summary>
    /// The avatar <c>avatars/u/&lt;id&gt;</c> answers, as <c>image/png</c>: by default a 64 × 64
    /// square of the user's colour with a white square of 24 in its centre (1001 <c>#2F81F7</c>,
    /// 1002 <c>#8250DF</c>, 1003 <c>#1F883D</c>, others <c>#BF3989</c>).
    /// </summary>
    public byte[] AvatarBytes
    {
        get => Volatile.Read(ref _avatarBytes);
        set => Volatile.Write(ref _avatarBytes, value ?? throw new ArgumentNullException(nameof(value)));
    }

    private static byte[] DefaultAvatar(long id)
    {
        var colour = id switch
        {
            1001 => "#2F81F7",
            1002 => "#8250DF",
            1003 => "#1F883D",
            _ => "#BF3989",
        };
        return TestImages.SquareOn(64, 64, TestImages.Rgb.Parse(colour), TestImages.Rgb.Parse("#FFFFFF"), 24);
    }
}

/// <summary>
/// A repository of the fake (<see cref="FakeGitHub.AddRepositoryAsync"/>): a bare repository at
/// <c>&lt;root&gt;/&lt;owner&gt;/&lt;name&gt;.git</c>, or an API-only record
/// (<see cref="FakeGitHub.AddListedRepositories"/>) without one.
/// </summary>
public sealed class FakeRepository
{
    private readonly ConcurrentDictionary<string, bool> _pushers = new(StringComparer.OrdinalIgnoreCase);
    private long _updatedAtTicks = AppClocks.HarnessTime.UtcTicks;
    private volatile bool _isArchived;
    private string _currentName;

    internal FakeRepository(long id, string owner, string name, bool isPrivate, string? forkOf, string? description, string defaultBranch, string? barePath)
    {
        Id = id;
        Owner = owner;
        Name = name;
        _currentName = name;
        IsPrivate = isPrivate;
        ForkOf = forkOf;
        Description = description;
        DefaultBranch = defaultBranch;
        BarePath = barePath;
    }

    /// <summary>The repository's id: 3001 and up in the order the repositories were added.</summary>
    public long Id { get; }

    public string Owner { get; }

    /// <summary>The name it was added under; the API's is <see cref="CurrentName"/>.</summary>
    public string Name { get; }

    /// <summary>
    /// <c>owner/name</c> as added: its folder, git's URL, its pull requests' and forks' key and the
    /// name the fake's methods take (an older name or the current one also finds it).
    /// </summary>
    public string FullName => Owner + "/" + Name;

    /// <summary>The name GitHub gives it now: <see cref="Name"/> until <see cref="FakeGitHub.Rename"/>.</summary>
    public string CurrentName
    {
        get => Volatile.Read(ref _currentName);
        internal set => Volatile.Write(ref _currentName, value);
    }

    /// <summary><c>owner/</c><see cref="CurrentName"/>: the name the API answers with and the routes' keys name.</summary>
    public string CurrentFullName => Owner + "/" + CurrentName;

    public bool IsPrivate { get; }

    /// <summary>The full name of the repository this one is a fork of; null when it is none.</summary>
    public string? ForkOf { get; }

    public string? Description { get; }

    /// <summary>The default branch, read from the bare repository's <c>HEAD</c> when it was added.</summary>
    public string DefaultBranch { get; }

    /// <summary>The bare repository's folder; null for an API-only record, which git does not reach.</summary>
    public string? BarePath { get; }

    public bool IsArchived
    {
        get => _isArchived;
        set => _isArchived = value;
    }

    /// <summary>When the repository was last updated (<c>updated_at</c>, <c>pushed_at</c>, the list's <c>sort=updated</c>): <see cref="AppClocks.HarnessTime"/> unless set.</summary>
    public DateTimeOffset UpdatedAt
    {
        get => new(Interlocked.Read(ref _updatedAtTicks), TimeSpan.Zero);
        set => Interlocked.Exchange(ref _updatedAtTicks, value.UtcTicks);
    }

    /// <summary>The logins that may push (<c>permissions.push</c>, receive-pack): the owner when it is a user, and those added.</summary>
    public IReadOnlyCollection<string> Pushers => [.. _pushers.Keys.Order(StringComparer.Ordinal)];

    /// <summary>Lets <paramref name="login"/> push.</summary>
    public void AddPusher(string login)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        _pushers[login] = true;
    }

    /// <summary>Stops <paramref name="login"/> pushing.</summary>
    public void RemovePusher(string login) => _pushers.TryRemove(login, out _);

    /// <summary>Whether <paramref name="login"/> may push.</summary>
    public bool MayPush(string login) => _pushers.ContainsKey(login);

    /// <summary>Whether <paramref name="login"/> owns the repository (case ignored).</summary>
    internal bool IsOwnedBy(string login) => string.Equals(Owner, login, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => FullName;
}

/// <summary>A pull request of the fake (<see cref="FakeGitHub.AddPullRequest"/>), kept in memory; its head in <c>refs/pull/&lt;n&gt;/head</c> of the base repository.</summary>
public sealed class FakePullRequest
{
    private string _state = "open";
    private string _title;
    private string _body;
    private string _headSha = string.Empty;
    private volatile bool _isDraft;
    private volatile bool _maintainerCanModify = true;

    internal FakePullRequest(string repository, int number, string title, string body, string author, string headRef, string? headRepository, string baseRef, bool isDraft)
    {
        Repository = repository;
        Number = number;
        _title = title;
        _body = body;
        Author = author;
        HeadRef = headRef;
        HeadRepository = headRepository;
        BaseRef = baseRef;
        _isDraft = isDraft;
    }

    /// <summary>The base repository's full name, <c>owner/name</c>.</summary>
    public string Repository { get; }

    public int Number { get; }

    public string Title
    {
        get => Volatile.Read(ref _title);
        set => Volatile.Write(ref _title, value ?? throw new ArgumentNullException(nameof(value)));
    }

    public string Body
    {
        get => Volatile.Read(ref _body);
        set => Volatile.Write(ref _body, value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>The login of the user who opened it.</summary>
    public string Author { get; }

    /// <summary>The head branch's name in <see cref="HeadRepository"/>.</summary>
    public string HeadRef { get; }

    /// <summary>The head repository's full name; null for the base repository itself. A name the fake does not know is a deleted fork.</summary>
    public string? HeadRepository { get; }

    public string BaseRef { get; }

    public bool IsDraft
    {
        get => _isDraft;
        set => _isDraft = value;
    }

    /// <summary><c>open</c> or <c>closed</c>.</summary>
    public string State
    {
        get => Volatile.Read(ref _state);
        set
        {
            if (value is not ("open" or "closed"))
            {
                throw new ArgumentException("A pull request's state is open or closed.", nameof(value));
            }

            Volatile.Write(ref _state, value);
        }
    }

    public bool IsOpen => State == "open";

    public bool MaintainerCanModify
    {
        get => _maintainerCanModify;
        set => _maintainerCanModify = value;
    }

    /// <summary>The head's SHA as of the last ref sync (D267); empty before the first.</summary>
    public string HeadSha
    {
        get => Volatile.Read(ref _headSha);
        internal set => Volatile.Write(ref _headSha, value);
    }

    public DateTimeOffset CreatedAt { get; } = AppClocks.HarnessTime;

    /// <summary>The head repository's owner, the base's for a pull request within it.</summary>
    internal string HeadOwner(string baseOwner) => HeadRepository is null ? baseOwner : HeadRepository[..HeadRepository.IndexOf('/', StringComparison.Ordinal)];

    public override string ToString() => $"{Repository}#{Number}";
}

/// <summary>
/// A failure the fake answers once in place of a route's normal answer (<see cref="FakeGitHub.FailNext"/>,
/// docs/phase-7a-design.md 2.4): after the header checks, before auth.
/// </summary>
public sealed class FakeGitHubFault
{
    private FakeGitHubFault(FakeGitHubFaultKind kind, Uri? location = null)
    {
        Kind = kind;
        Location = location;
    }

    /// <summary>401 "Bad credentials".</summary>
    public static FakeGitHubFault BadCredentials { get; } = new(FakeGitHubFaultKind.BadCredentials);

    /// <summary>403 with <c>X-RateLimit-Remaining: 0</c>, "API rate limit exceeded for user ID &lt;id&gt;.".</summary>
    public static FakeGitHubFault PrimaryRateLimit { get; } = new(FakeGitHubFaultKind.PrimaryRateLimit);

    /// <summary>403 with <c>Retry-After: 60</c>, "You have exceeded a secondary rate limit. …".</summary>
    public static FakeGitHubFault SecondaryRateLimit { get; } = new(FakeGitHubFaultKind.SecondaryRateLimit);

    /// <summary>403 with <c>X-GitHub-SSO: required; url=&lt;web&gt;orgs/acme/sso?authorization_request=FAKE</c>.</summary>
    public static FakeGitHubFault SsoRequired { get; } = new(FakeGitHubFaultKind.SsoRequired);

    /// <summary>The normal answer with <c>X-GitHub-SSO: partial-results; organizations=1002</c>.</summary>
    public static FakeGitHubFault SsoPartialResults { get; } = new(FakeGitHubFaultKind.SsoPartialResults);

    /// <summary>403 "Resource not accessible by personal access token" with the route's <c>X-Accepted-GitHub-Permissions</c>.</summary>
    public static FakeGitHubFault Forbidden { get; } = new(FakeGitHubFaultKind.Forbidden);

    /// <summary>404 "Not Found".</summary>
    public static FakeGitHubFault NotFound { get; } = new(FakeGitHubFaultKind.NotFound);

    /// <summary>
    /// 410: the API version is no longer supported. Not observed on GitHub, whose answer to a
    /// version it does not support is a 400 (the live run of 2026-10-30, which the fake's header
    /// check gives); kept for a version GitHub retires (design 2.4, unverified).
    /// </summary>
    public static FakeGitHubFault ApiVersionGone { get; } = new(FakeGitHubFaultKind.ApiVersionGone);

    /// <summary>
    /// 500 "Server Error". This body is not observed on GitHub: the one 500 the live run of
    /// 2026-10-30 saw had none (A6's lookup of a commit GitHub lacks, which the route answers so itself).
    /// </summary>
    public static FakeGitHubFault ServerError { get; } = new(FakeGitHubFaultKind.ServerError);

    /// <summary>502 with an HTML body.</summary>
    public static FakeGitHubFault BadGateway { get; } = new(FakeGitHubFaultKind.BadGateway);

    /// <summary>200 with the JSON cut short: <c>{"login":</c>.</summary>
    public static FakeGitHubFault Malformed { get; } = new(FakeGitHubFaultKind.Malformed);

    /// <summary>A create made in full, then the connection closed without an answer (a lost POST).</summary>
    public static FakeGitHubFault CreatedThenDropped { get; } = new(FakeGitHubFaultKind.CreatedThenDropped);

    /// <summary>301 to <c>api/v3/repositories/&lt;id&gt;</c> plus the rest of the path, the query kept.</summary>
    public static FakeGitHubFault Moved { get; } = new(FakeGitHubFaultKind.Moved);

    internal FakeGitHubFaultKind Kind { get; }

    internal Uri? Location { get; }

    /// <summary>301 to <paramref name="location"/>.</summary>
    public static FakeGitHubFault MovedTo(Uri location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return new FakeGitHubFault(FakeGitHubFaultKind.MovedTo, location);
    }

    public override string ToString() => Location is null ? Kind.ToString() : $"{Kind} {Location}";
}

internal enum FakeGitHubFaultKind
{
    BadCredentials,
    PrimaryRateLimit,
    SecondaryRateLimit,
    SsoRequired,
    SsoPartialResults,
    Forbidden,
    NotFound,
    ApiVersionGone,
    ServerError,
    BadGateway,
    Malformed,
    CreatedThenDropped,
    Moved,
    MovedTo,
}

/// <summary>One request the fake answered (its API, avatars, pages; git's are <see cref="FakeGitHub.GitRequests"/>). Never holds a token.</summary>
/// <param name="Method"><c>GET</c>, <c>POST</c>, <c>PATCH</c>.</param>
/// <param name="Host">The host it was sent to: <c>127.0.0.1:&lt;port&gt;</c>, or any host in process.</param>
/// <param name="Path">The path without the query, such as <c>/api/v3/user</c>.</param>
/// <param name="Query">The query without <c>?</c>; empty when none.</param>
/// <param name="Route">The route's key (<c>user</c>, <c>pulls:octo/hello</c>, <c>avatar:1001</c>); null for a path no route serves.</param>
/// <param name="TokenPresent">Whether it carried an <c>Authorization</c> header.</param>
/// <param name="Login">The login its token signed in as; null when none did.</param>
/// <param name="Conditional">Whether it carried <c>If-None-Match</c>.</param>
/// <param name="Status">The answer's status code.</param>
/// <param name="Body">The body of a POST or PATCH; null for the others.</param>
public sealed record FakeGitHubRequest(string Method, string Host, string Path, string Query, string? Route, bool TokenPresent, string? Login, bool Conditional, int Status, string? Body);

/// <summary>
/// A hold on a route's next request (<see cref="FakeGitHub.HoldNext"/>): the request waits, before
/// anything is decided about it, until the hold is disposed (at most 120 s) or, in process, the
/// request's own cancellation ends its wait (<see cref="Abandoned"/>).
/// </summary>
public sealed class FakeGitHubHold : IDisposable
{
    private readonly Action _release;
    private int _disposed;

    internal FakeGitHubHold(string route, Task arrived, Task abandoned, Action release)
    {
        Route = route;
        Arrived = arrived;
        Abandoned = abandoned;
        _release = release;
    }

    /// <summary>The route held, such as <c>pulls:octo/hello</c> or <c>git:octo/hello</c>.</summary>
    public string Route { get; }

    /// <summary>Completes when the request has arrived and waits; cancelled when the hold is disposed first.</summary>
    public Task Arrived { get; }

    /// <summary>
    /// Completes when the held request's own cancellation has ended its wait before the hold let it
    /// go: it is neither answered nor recorded, so no answer of it can reach its sender. That holds
    /// only for a request reached through <see cref="FakeGitHub.CreateHandler"/>, in process, whose
    /// wait sees the sender's token. Over a server the wait sees only the server's stop: a client
    /// that gives up never completes it (its request goes on at the release and is recorded), and
    /// the server's stop at the fake's disposal may complete it although nothing was given up. It
    /// never completes when the request went on, and never for a git route, whose request waits at
    /// the server's gate whatever its cancellation.
    /// </summary>
    public Task Abandoned { get; }

    /// <summary>Lets the held request go on, or drops the hold when none arrived.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _release();
        }
    }
}
