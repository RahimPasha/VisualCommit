using VisualCommit.Core;
using VisualCommit.Hosting.GitLab;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// A local GitLab for the tests (docs/phase-7b-design.md 1.9, 2.3, 4; D346, D363, D370): its API
/// (<c>api/v4/</c>), pages and git on one host, <c>http://127.0.0.1:&lt;port&gt;/</c>, on the fakes'
/// shared core (<see cref="FakeHostingService"/>) and <see cref="AuthRemoteServer"/>'s host mode
/// (<see cref="StartAsync"/>), or the same routes in process (<see cref="InProcess"/>,
/// <see cref="FakeHostingService.CreateHandler"/>). Written from the research (r6 and its gap round
/// g2), not from a client: each answer carries its mark (MEASURED, READ, ASSUMED) in its doc comment,
/// and 4.7's list of what the live command settles heads <c>FakeGitLab.Router.cs</c>. Made-up names,
/// ids and addresses only (<c>&lt;username&gt;@example.com</c>).
/// <para>
/// Users (<see cref="AddUser"/>) hold tokens <c>glpat-FAKE-&lt;username&gt;-&lt;n&gt;</c>
/// (<see cref="AddToken"/>), legacy or fine-grained; projects (<see cref="AddProjectAsync"/>, ids 5001
/// and up) live under a username (its Owner, 50) or under groups nested to any depth, which
/// <see cref="Grant"/> opens to a user with an access level (10 to 50) for the group or project and all
/// below it; merge requests (<see cref="AddMergeRequest"/>, ids 7001 and up, iids per target) keep their
/// head in the target's <c>refs/merge-requests/&lt;iid&gt;/head</c> (<see cref="SyncRefsAsync"/>).
/// </para>
/// </summary>
public sealed partial class FakeGitLab : FakeHostingService
{
    /// <summary>The port of <see cref="InProcess"/>'s endpoints, <c>Local(3)</c>: never listened on.</summary>
    public const int InProcessPort = 3;

    private const long FirstProjectId = 5001;
    private const long FirstMergeRequestId = 7001;
    private const long FirstTokenId = 8001;
    private const long FirstGroupId = 9001;

    /// <summary>The token of the git request being decided on this thread (<see cref="LoginOfToken"/>, read by <see cref="MayPush"/>, <see cref="PushRefusal"/> and <see cref="FetchRefusal"/>): the server asks all four in one synchronous call per request (AuthRemoteServer.HostAccess), which a change there must keep.</summary>
    [ThreadStatic]
    private static (FakeGitLab Fake, GitLabToken Token)? t_gitToken;

    private readonly Dictionary<string, FakeGitLabUser> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GitLabToken> _tokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _tokenCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FakeGitLabProject> _projects = [];
    private readonly Dictionary<string, FakeGitLabProject> _movedFrom = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FakeGitLabProject> _byFolder = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Path, string Username, int Level)> _grants = [];
    private readonly Dictionary<string, long> _groupIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FakeMergeRequest> _mergeRequests = [];
    private readonly Dictionary<string, int> _used = new(StringComparer.OrdinalIgnoreCase);
    private long _nextProjectId = FirstProjectId;
    private long _nextMergeRequestId = FirstMergeRequestId;
    private long _nextTokenId = FirstTokenId;
    private long _nextGroupId = FirstGroupId;
    private int _requestCount;

    private FakeGitLab(string root)
        : base(root, "GitLab", InProcessPort)
    {
    }

    /// <summary>The fake's addresses: <c>GitLabEndpoints.Local(Port)</c>; <c>Local(3)</c> in process.</summary>
    public GitLabEndpoints Endpoints => GitLabEndpoints.Local(Port);

    /// <summary>A git 401's realm, <c>GitLab</c> (MEASURED r6 11).</summary>
    protected override string Realm => "GitLab";

    /// <summary>
    /// Starts the fake on 127.0.0.1 in <see cref="AuthRemoteServer"/>'s host mode on
    /// <paramref name="root"/> (made when missing): on <paramref name="port"/>, or one the system
    /// picks when it is 0; with <paramref name="fallBackToFreePort"/> a taken port gives way to a
    /// free one (<see cref="FakeHostingService.PortWasTaken"/>). Nothing under <paramref name="root"/> is deleted.
    /// </summary>
    public static async Task<FakeGitLab> StartAsync(string root, int port = 0, bool fallBackToFreePort = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ct.ThrowIfCancellationRequested();
        var fake = new FakeGitLab(root);
        try
        {
            await fake.StartServerAsync(port, fallBackToFreePort).ConfigureAwait(false);
            return fake;
        }
        catch
        {
            await fake.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// The fake without a server: its routes reached only through <see cref="FakeHostingService.CreateHandler"/>,
    /// by path from any host; git's paths get 404. <see cref="Endpoints"/> is <c>Local(3)</c>. Its
    /// projects still have bare repositories, which its ref sync and branch checks read.
    /// </summary>
    public static FakeGitLab InProcess(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return new FakeGitLab(root);
    }

    /// <summary>Adds the user <paramref name="username"/> with <paramref name="id"/> and <paramref name="name"/> (the username when null); its address is <c>&lt;username&gt;@example.com</c>.</summary>
    public FakeGitLabUser AddUser(string username, long id, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        if (!IsSlug(username))
        {
            throw new ArgumentException($"A username is of letters, digits, '_', '.' and '-': '{username}'.", nameof(username));
        }

        var user = new FakeGitLabUser(username, id, name ?? username);
        lock (Gate)
        {
            if (!_users.TryAdd(username, user))
            {
                throw new ArgumentException($"The fake of GitLab has a user {username} already.", nameof(username));
            }
        }

        return user;
    }

    /// <summary>
    /// A new token of <paramref name="username"/>: <c>glpat-FAKE-&lt;username&gt;-&lt;n&gt;</c>, n counting
    /// that user's tokens from 1. <paramref name="permissions"/> are scopes for a legacy token (default
    /// <c>api</c>) and labels for a fine-grained one (default <see cref="FakeGitLabPermissions.FineGrainedDefault"/>),
    /// on every project the user reaches ("Only specific…" is not modelled, D370). A token whose
    /// <paramref name="expires"/> is on or before <see cref="AppClocks.HarnessTime"/>'s date is expired
    /// (ASSUMED r6 2.4: unusable from 00:00 UTC of its date).
    /// </summary>
    public string AddToken(string username, FakeGitLabTokenKind kind = FakeGitLabTokenKind.FineGrained, IReadOnlyCollection<string>? permissions = null, DateOnly? expires = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        lock (Gate)
        {
            var user = _users.GetValueOrDefault(username) ?? throw new ArgumentException($"The fake of GitLab has no user {username}.", nameof(username));
            var n = _tokenCounts[user.Username] = _tokenCounts.GetValueOrDefault(user.Username) + 1;
            var token = $"glpat-FAKE-{user.Username}-{n}";
            var granted = permissions ?? (kind == FakeGitLabTokenKind.Legacy ? FakeGitLabPermissions.LegacyDefault : FakeGitLabPermissions.FineGrainedDefault);
            _tokens[token] = new GitLabToken(_nextTokenId++, user.Username, kind, new HashSet<string>(granted, StringComparer.Ordinal), expires);
            AddTokenLocked(token, user.Username);
            return token;
        }
    }

    /// <summary>Expires <paramref name="token"/>: the API answers it 401 <c>invalid_token</c> "Token is expired. …", git 401.</summary>
    public void Expire(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        lock (Gate)
        {
            if (_tokens.TryGetValue(token, out var known))
            {
                known.Expired = true;
            }
        }
    }

    /// <summary>
    /// Adds the project <paramref name="path"/> (two to 22 segments of letters, digits, <c>_</c>,
    /// <c>.</c> and <c>-</c>; groups nest to 20 levels, READ r6 0.3), served by git at
    /// <c>&lt;path&gt;.git</c> and <c>&lt;path&gt;</c> from <c>&lt;root&gt;/&lt;path&gt;.git</c>: in place when
    /// that holds a bare repository, else a <c>git clone --bare --no-hardlinks</c> of
    /// <paramref name="sourceRepository"/>, else a new empty one (2.3's rule).
    /// </summary>
    public async Task<FakeGitLabProject> AddProjectAsync(string path, string? sourceRepository = null, string visibility = "private", FakeGitLabProject? forkOf = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        CheckProjectPath(path);
        var checkedVisibility = CheckVisibility(visibility);
        lock (Gate)
        {
            if (ProjectLocked(path) is not null || _movedFrom.ContainsKey(path))
            {
                throw new ArgumentException($"The fake of GitLab has a project {path} already.", nameof(path));
            }

            if (forkOf is not null && !_projects.Contains(forkOf))
            {
                throw new ArgumentException($"{forkOf} is not this fake's.", nameof(forkOf));
            }
        }

        var bare = await BareRepositoryAsync(path, gitSuffix: true, sourceRepository, ct).ConfigureAwait(false);
        lock (Gate)
        {
            var project = new FakeGitLabProject(_nextProjectId, path, checkedVisibility, forkOf, path, bare, ProjectTime(_nextProjectId));
            _nextProjectId++;
            _projects.Add(project);
            _byFolder[path] = project;
            return project;
        }
    }

    /// <summary>Gives <paramref name="username"/> <paramref name="accessLevel"/> (10 Guest to 50 Owner, r6 3.1) on the group or project <paramref name="path"/> and all below it; the higher of two grants counts.</summary>
    public void Grant(string path, string username, int accessLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentOutOfRangeException.ThrowIfLessThan(accessLevel, 10);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(accessLevel, 50);
        lock (Gate)
        {
            _grants.Add((path.Trim('/'), username, accessLevel));
        }
    }

    /// <summary>Adds <paramref name="count"/> private projects <c>&lt;namespacePath&gt;/listed-0001</c> on, for the API alone (no git): the browser's long lists and paging.</summary>
    public void AddListedProjects(string namespacePath, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespacePath);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        lock (Gate)
        {
            for (var i = 1; i <= count; i++)
            {
                var path = $"{namespacePath.Trim('/')}/listed-{i:D4}";
                CheckProjectPath(path);
                if (ProjectLocked(path) is not null)
                {
                    throw new ArgumentException($"The fake of GitLab has a project {path} already.", nameof(namespacePath));
                }

                _projects.Add(new FakeGitLabProject(_nextProjectId, path, "private", null, null, null, ProjectTime(_nextProjectId)));
                _nextProjectId++;
            }
        }
    }

    /// <summary>
    /// Adds a merge request into <paramref name="target"/> from <paramref name="source"/> (the target
    /// when null; a fork's head ref is still the target's, MEASURED r6 4.2), by the known user
    /// <paramref name="author"/>; its iid is <paramref name="iid"/> or the next in the target. Its head
    /// ref is written at the next ref sync.
    /// </summary>
    public FakeMergeRequest AddMergeRequest(FakeGitLabProject target, string title, string author, string sourceBranch, string targetBranch, FakeGitLabProject? source = null, string? description = null, int? iid = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceBranch);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetBranch);
        lock (Gate)
        {
            if (!_projects.Contains(target) || source is not null && !_projects.Contains(source))
            {
                throw new ArgumentException("A merge request's projects are this fake's.", nameof(target));
            }

            var user = _users.GetValueOrDefault(author) ?? throw new ArgumentException($"The fake of GitLab has no user {author}.", nameof(author));
            if (iid is { } given && (given < 1 || _mergeRequests.Any(request => request.Target == target && request.Iid == given)))
            {
                throw new ArgumentException($"{target.Path} has a merge request !{given} already, or it is not positive.", nameof(iid));
            }

            return AddMergeRequestLocked(target, source ?? target, title, user.Username, sourceBranch, targetBranch, description, iid);
        }
    }

    /// <summary>The project at <paramref name="path"/> now, without case.</summary>
    /// <exception cref="ArgumentException">The fake has none there.</exception>
    public FakeGitLabProject Project(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        lock (Gate)
        {
            return ProjectLocked(path) ?? throw new ArgumentException($"The fake of GitLab has no project {path}.", nameof(path));
        }
    }

    /// <summary>The merge requests into <paramref name="target"/>, in the order they were made; a deleted one left out.</summary>
    public IReadOnlyList<FakeMergeRequest> MergeRequests(FakeGitLabProject target)
    {
        ArgumentNullException.ThrowIfNull(target);
        lock (Gate)
        {
            return [.. _mergeRequests.Where(request => request.Target == target && !request.Deleted)];
        }
    }

    /// <summary>
    /// Moves the project at <paramref name="path"/> to <paramref name="newPath"/> (a rename or a transfer):
    /// the API's GET by the old path is 301 to <c>projects/&lt;id&gt;</c> and the rest of the path, the query
    /// kept (MEASURED r6 3.2), any other method 405 (READ); the web pages' 301 go to the new path
    /// (MEASURED g2 G4); git serves the old path and the new, fetch and push, 200 with no redirect
    /// (MEASURED r6 6.1, push READ g2 G3).
    /// </summary>
    public void Move(string path, string newPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);
        CheckProjectPath(newPath);
        FakeGitLabProject project;
        lock (Gate)
        {
            project = ProjectLocked(path) ?? throw new ArgumentException($"The fake of GitLab has no project {path}.", nameof(path));
            if (ProjectLocked(newPath) is not null)
            {
                throw new ArgumentException($"The fake of GitLab has a project {newPath} already.", nameof(newPath));
            }

            // Memberships of the project itself move with it.
            for (var i = 0; i < _grants.Count; i++)
            {
                if (_grants[i].Path.Equals(project.Path, StringComparison.OrdinalIgnoreCase))
                {
                    _grants[i] = _grants[i] with { Path = newPath };
                }
            }

            _movedFrom[project.Path] = project;
            _movedFrom.Remove(newPath);
            project.Path = newPath;
        }

        if (project.Folder is { } folder)
        {
            ServeAt(newPath, folder + ".git", gitSuffix: true);
        }
    }

    /// <summary>The visibility checked: <c>private</c>, <c>internal</c> or <c>public</c>.</summary>
    internal static string CheckVisibility(string visibility) =>
        visibility is "private" or "internal" or "public"
            ? visibility
            : throw new ArgumentException($"A visibility is private, internal or public: '{visibility}'.", nameof(visibility));

    /// <summary>GitLab's draft title: it starts <c>Draft:</c>, <c>[Draft]</c> or <c>(Draft)</c>, without case (READ r6 5.1, g2 G1.4).</summary>
    internal static bool IsDraftTitle(string title) =>
        title.StartsWith("Draft:", StringComparison.OrdinalIgnoreCase)
        || title.StartsWith("[Draft]", StringComparison.OrdinalIgnoreCase)
        || title.StartsWith("(Draft)", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether git may read a project without a credential: <c>public</c> alone (4.5).</summary>
    protected override bool IsPublic(string repository)
    {
        lock (Gate)
        {
            return _byFolder.GetValueOrDefault(repository)?.Visibility == "public";
        }
    }

    /// <summary>A push: Developer (30) or above, with <c>api</c>, <c>write_repository</c> or Code: Push (READ g2 G3).</summary>
    protected override bool MayPush(string repository, string login)
    {
        var token = GitToken();
        lock (Gate)
        {
            return _byFolder.GetValueOrDefault(repository) is { } project
                && AccessLocked(project, login) >= 30
                && token is not null
                && (token.Kind == FakeGitLabTokenKind.Legacy
                    ? token.Has(FakeGitLabPermissions.Api) || token.Has(FakeGitLabPermissions.WriteRepository)
                    : token.Has(FakeGitLabPermissions.CodePush));
        }
    }

    /// <summary>
    /// A refused push's 403 <c>text/plain</c>, in <c>Gitlab::GitAccess#check</c>'s order (READ g2 G3;
    /// lib/gitlab/git_access.rb:77-98, :230-262, :290-312): a legacy token without <c>api</c> or
    /// <c>write_repository</c> "You are not allowed to upload code." first, whatever the project; then a
    /// project the user cannot see GitLab's 404 text, a fine-grained token's too; then a fine-grained
    /// token without Code: Push "Access denied: This operation requires a fine-grained personal access
    /// token with the following project permissions: [Code: Push]."; else "You are not allowed to upload
    /// code." (READ r6 11, g2 G3).
    /// </summary>
    protected override string PushRefusal(string repository, string login)
    {
        var token = GitToken();
        if (token is { Kind: FakeGitLabTokenKind.Legacy } && !token.Has(FakeGitLabPermissions.Api) && !token.Has(FakeGitLabPermissions.WriteRepository))
        {
            return UploadText;
        }

        if (!ProjectVisible(repository, login))
        {
            return ProjectNotFoundText;
        }

        return token is { Kind: FakeGitLabTokenKind.FineGrained } && !token.Has(FakeGitLabPermissions.CodePush)
            ? GranularText("project", FakeGitLabPermissions.CodePush)
            : UploadText;
    }

    /// <summary>
    /// A signed-in fetch's 403 <c>text/plain</c> (4.5), in <c>Gitlab::GitAccess#check</c>'s order (READ
    /// g2 G3; lib/gitlab/git_access.rb:77-98, :230-262, :290-312): a legacy token without <c>api</c>,
    /// <c>read_api</c>, <c>read_repository</c> or <c>write_repository</c> "You are not allowed to download
    /// code." first, whatever the project; then a project the user cannot see GitLab's 404 text (READ r6
    /// 11, g2 G3), a fine-grained token's too, as a 403, since the server's refusal is a 403; then a
    /// fine-grained token without Code: Download its granular text.
    /// </summary>
    protected override string? FetchRefusal(string repository, string login)
    {
        var token = GitToken();
        if (token is { Kind: FakeGitLabTokenKind.Legacy }
            && !(token.Has(FakeGitLabPermissions.Api) || token.Has(FakeGitLabPermissions.ReadApi) || token.Has(FakeGitLabPermissions.ReadRepository) || token.Has(FakeGitLabPermissions.WriteRepository)))
        {
            return "You are not allowed to download code.";
        }

        if (!ProjectVisible(repository, login))
        {
            return ProjectNotFoundText;
        }

        return token is { Kind: FakeGitLabTokenKind.FineGrained } && !token.Has(FakeGitLabPermissions.CodeDownload)
            ? GranularText("project", FakeGitLabPermissions.CodeDownload)
            : null;
    }

    private const string UploadText = "You are not allowed to upload code.";

    private const string ProjectNotFoundText = "The project you were looking for could not be found or you don't have permission to view it.";

    /// <summary>Whether the served folder's project is one <paramref name="login"/> may see.</summary>
    private bool ProjectVisible(string repository, string login)
    {
        lock (Gate)
        {
            return _byFolder.GetValueOrDefault(repository) is { } project && VisibleLocked(project, login);
        }
    }

    /// <summary>
    /// A Basic password's login for git: the username of a live token under any non-empty user (READ r6
    /// 11, g2 G3); an unknown, revoked or expired one null, so 401 with realm GitLab (MEASURED; expired
    /// and revoked READ g2 G3).
    /// </summary>
    protected override string? LoginOfToken(string repository, string token)
    {
        lock (Gate)
        {
            t_gitToken = null;
            if (TokenLoginLocked(token) is not { } login || !_tokens.TryGetValue(token, out var known) || known.IsExpired)
            {
                return null;
            }

            t_gitToken = (this, known);
            return login;
        }
    }

    /// <summary>Before git lists a project's refs: its merge requests' head refs brought up to date (4.5).</summary>
    protected override async Task BeforeGitAsync(string repository, CancellationToken ct)
    {
        FakeGitLabProject? project;
        lock (Gate)
        {
            project = _byFolder.GetValueOrDefault(repository);
            if (project is null || !_mergeRequests.Any(request => request.Target == project))
            {
                return;
            }
        }

        await SyncRefsAsync(project, ct).ConfigureAwait(false);
    }

    private GitLabToken? GitToken() => t_gitToken is { } held && held.Fake == this ? held.Token : null;

    private FakeMergeRequest AddMergeRequestLocked(FakeGitLabProject target, FakeGitLabProject source, string title, string author, string sourceBranch, string targetBranch, string? description, int? iid)
    {
        var number = iid ?? target.LastIid + 1;
        target.LastIid = Math.Max(target.LastIid, number);
        var id = _nextMergeRequestId++;
        var request = new FakeMergeRequest(id, number, target, source, title, author, sourceBranch, targetBranch, description, AppClocks.HarnessTime.AddDays(-1).AddMinutes(id - FirstMergeRequestId));
        _mergeRequests.Add(request);
        return request;
    }

    /// <summary>The project at <paramref name="path"/> now, without case; under <see cref="FakeHostingService.Gate"/>.</summary>
    private FakeGitLabProject? ProjectLocked(string path) =>
        _projects.FirstOrDefault(project => project.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// <paramref name="username"/>'s access level on <paramref name="project"/>: 50 under its own
    /// namespace, else the highest grant on the project or a group above it (READ r6 3.1, g2 G5.4); 0
    /// for none or no user.
    /// </summary>
    private int AccessLocked(FakeGitLabProject project, string? username)
    {
        if (username is null || !_users.ContainsKey(username))
        {
            return 0;
        }

        return Math.Max(OwnsLocked(project, username) ? 50 : 0, Math.Max(ProjectGrantLocked(project, username) ?? 0, GroupGrantLocked(project, username) ?? 0));
    }

    private static bool OwnsLocked(FakeGitLabProject project, string username) => project.Namespace.Equals(username, StringComparison.OrdinalIgnoreCase);

    private int? ProjectGrantLocked(FakeGitLabProject project, string username) =>
        _grants.Where(grant => grant.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && grant.Path.Equals(project.Path, StringComparison.OrdinalIgnoreCase))
            .Select(grant => (int?)grant.Level).Max();

    private int? GroupGrantLocked(FakeGitLabProject project, string username) =>
        _grants.Where(grant => grant.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && project.Path.StartsWith(grant.Path + "/", StringComparison.OrdinalIgnoreCase))
            .Select(grant => (int?)grant.Level).Max();

    /// <summary>Whether <paramref name="username"/> (null: anonymous) may see <paramref name="project"/>: public, internal to any signed-in user, else a member (READ r6 3.1).</summary>
    private bool VisibleLocked(FakeGitLabProject project, string? username) =>
        project.Visibility == "public"
        || project.Visibility == "internal" && username is not null && _users.ContainsKey(username)
        || AccessLocked(project, username) >= 10;

    private static DateTimeOffset ProjectTime(long id) => AppClocks.HarnessTime.AddDays(-30).AddMinutes(id - FirstProjectId);

    private static void CheckProjectPath(string path)
    {
        var segments = path.Split('/');
        if (segments.Length is < 2 or > 22 || !segments.All(IsSlug) || segments[^1].EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"A project's path is of 2 to 22 segments of letters, digits, '_', '.' and '-', the last not ending in .git: '{path}'.", nameof(path));
        }
    }

    private static bool IsSlug(string segment) =>
        segment.Length > 0 && segment is not ("." or ".." or "-") && segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-');

    /// <summary>A token the fake knows: never shown, never recorded.</summary>
    private sealed class GitLabToken(long id, string username, FakeGitLabTokenKind kind, HashSet<string> permissions, DateOnly? expires)
    {
        public long Id { get; } = id;

        public string Username { get; } = username;

        public FakeGitLabTokenKind Kind { get; } = kind;

        public IReadOnlySet<string> Permissions { get; } = permissions;

        public DateOnly? Expires { get; } = expires;

        public bool Expired { get; set; }

        public bool IsExpired => Expired || Expires is { } date && date <= DateOnly.FromDateTime(AppClocks.HarnessTime.UtcDateTime);

        public bool Has(string permission) => Permissions.Contains(permission);

        public override string ToString() => $"GitLabToken({Id}, {Username}, {Kind}, ***)";
    }
}
