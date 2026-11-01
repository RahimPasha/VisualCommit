namespace VisualCommit.Testing.Hosting;

/// <summary>The two kinds of a GitLab personal access token (r6 1.1, 2.1, 2.2; one model, one prefix, the same headers, READ).</summary>
public enum FakeGitLabTokenKind
{
    /// <summary>A legacy token: its permissions are scopes (<c>api</c>, <c>read_api</c>, <c>read_user</c>, <c>read_repository</c>, <c>write_repository</c>).</summary>
    Legacy,

    /// <summary>A fine-grained token: its permissions are labels (<c>User: Read</c>, <c>Code: Push</c>), on every project the user reaches.</summary>
    FineGrained,
}

/// <summary>
/// The permissions the fake knows (docs/phase-7b-design.md 4.2, 4.3): legacy scopes (r6 2.1, READ) and
/// fine-grained labels (r6 2.2, READ; the label form <c>"&lt;Resource&gt;: &lt;Action&gt;"</c>, g2 G2.4).
/// </summary>
public static class FakeGitLabPermissions
{
    public const string Api = "api";
    public const string ReadApi = "read_api";
    public const string ReadUser = "read_user";
    public const string ReadRepository = "read_repository";
    public const string WriteRepository = "write_repository";

    public const string UserRead = "User: Read";
    public const string PersonalAccessTokenRead = "Personal Access Token: Read";
    public const string ProjectRead = "Project: Read";
    public const string MergeRequestRead = "Merge Request: Read";
    public const string MergeRequestCreate = "Merge Request: Create";
    public const string MergeRequestUpdate = "Merge Request: Update";
    public const string MergeRequestDelete = "Merge Request: Delete";
    public const string BranchRead = "Branch: Read";
    public const string BranchDelete = "Branch: Delete";
    public const string CodeDownload = "Code: Download";
    public const string CodePush = "Code: Push";

    /// <summary>A legacy token's default scopes: <c>api</c> (D345).</summary>
    public static IReadOnlyList<string> LegacyDefault { get; } = [Api];

    /// <summary>
    /// A fine-grained token's default labels: r6 2.2's table as D364's live token has it (User: Read;
    /// Project: Read; Merge Request: Read, Create, Update and Delete; Branch: Read and Delete; Code:
    /// Download and Push), so without Personal Access Token: Read, as 4.7 says of <c>self</c>.
    /// </summary>
    public static IReadOnlyList<string> FineGrainedDefault { get; } =
        [UserRead, ProjectRead, MergeRequestRead, MergeRequestCreate, MergeRequestUpdate, MergeRequestDelete, BranchRead, BranchDelete, CodeDownload, CodePush];

    /// <summary>
    /// A label's raw permission name, as <c>personal_access_tokens/self</c>'s <c>granular_scopes</c>
    /// carries it (<c>read_personal_access_token</c> READ g2 G2.4; <c>read_user</c>,
    /// <c>update_merge_request</c>, <c>delete_merge_request</c>, <c>delete_branch</c> READ g2 G1.5 to G1.7,
    /// G2.4; the others ASSUMED by the same pattern).
    /// </summary>
    internal static string RawName(string label) => label switch
    {
        UserRead => "read_user",
        PersonalAccessTokenRead => "read_personal_access_token",
        ProjectRead => "read_project",
        MergeRequestRead => "read_merge_request",
        MergeRequestCreate => "create_merge_request",
        MergeRequestUpdate => "update_merge_request",
        MergeRequestDelete => "delete_merge_request",
        BranchRead => "read_branch",
        BranchDelete => "delete_branch",
        CodeDownload => "download_code",
        CodePush => "push_code",
        _ => label.ToLowerInvariant().Replace(": ", "_", StringComparison.Ordinal).Replace(' ', '_'),
    };

    /// <summary>Whether a label's boundary is the user (<c>User</c>, <c>Personal Access Token</c>) rather than a project (r6 2.2).</summary>
    internal static bool IsUserBoundary(string label) => label is UserRead or PersonalAccessTokenRead;
}

/// <summary>A GitLab user of the fake (r6 1.3): <see cref="Email"/> is the made-up <c>&lt;username&gt;@example.com</c>.</summary>
public sealed class FakeGitLabUser
{
    internal FakeGitLabUser(string username, long id, string name)
    {
        Username = username;
        Id = id;
        Name = name;
    }

    /// <summary>The username as added (looked up without case, ASSUMED r6 1.3).</summary>
    public string Username { get; }

    public long Id { get; }

    /// <summary>The display name; the username when none was given.</summary>
    public string Name { get; }

    /// <summary>The made-up address <c>&lt;username&gt;@example.com</c>, the user's <c>email</c> and <c>public_email</c>.</summary>
    public string Email => Username + "@example.com";

    public override string ToString() => $"{Username} ({Id})";
}

/// <summary>
/// A project of the fake (r6 3.1): its id (5001 and up), its path with namespace, a visibility of
/// <c>private</c>, <c>internal</c> or <c>public</c>, the project it is a fork of, and its bare
/// repository (none for <see cref="FakeGitLab.AddListedProjects"/>'s). A project under a username
/// is that user's (Owner, 50).
/// </summary>
public sealed class FakeGitLabProject
{
    private string _visibility;

    internal FakeGitLabProject(long id, string path, string visibility, FakeGitLabProject? forkOf, string? folder, string? barePath, DateTimeOffset createdAt)
    {
        Id = id;
        Path = path;
        _visibility = visibility;
        ForkOf = forkOf;
        Folder = folder;
        BarePath = barePath;
        CreatedAt = createdAt;
        LastActivityAt = createdAt;
    }

    public long Id { get; }

    /// <summary>Its path with namespace now (<see cref="FakeGitLab.Move"/> changes it): <c>northfield/platform/web-app</c>.</summary>
    public string Path { get; internal set; }

    /// <summary>Its namespace's full path: all of <see cref="Path"/> but its last segment.</summary>
    public string Namespace => Path[..Path.LastIndexOf('/')];

    /// <summary>Its path's last segment, which the fake also gives as its name.</summary>
    public string Name => Path[(Path.LastIndexOf('/') + 1)..];

    /// <summary><c>private</c>, <c>internal</c> (any signed-in user reads it) or <c>public</c>.</summary>
    public string Visibility
    {
        get => _visibility;
        set => _visibility = FakeGitLab.CheckVisibility(value);
    }

    public FakeGitLabProject? ForkOf { get; }

    /// <summary>Archived: <c>archived: true</c>, left out by <c>archived=false</c>.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Merge requests on (<c>merge_requests_enabled</c>); off, a create from or into it is 403 (g2 G1.2).</summary>
    public bool MergeRequestsEnabled { get; set; } = true;

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary><c>last_activity_at</c>, which <c>order_by=last_activity_at</c> sorts by: its creation unless set.</summary>
    public DateTimeOffset LastActivityAt { get; set; }

    /// <summary>Its bare repository; null for a project the API alone lists.</summary>
    public string? BarePath { get; }

    /// <summary>The repository's name in callbacks, holds and gates: its folder under the root without <c>.git</c> (its first path), null without one.</summary>
    internal string? Folder { get; }

    /// <summary>The highest iid given in it so far (its merge requests' iids are never reused).</summary>
    internal int LastIid { get; set; }

    public override string ToString() => $"{Path} ({Id})";
}

/// <summary>
/// A merge request of the fake (r6 4.1): its global id (7001 and up), its iid in the target,
/// the target and source projects (the same unless from a fork), its branches and author, and its
/// state (<c>opened</c>, <c>closed</c>, <c>merged</c>, <c>locked</c>). <see cref="Draft"/> comes from its
/// title (r6 5.1, READ).
/// </summary>
public sealed class FakeMergeRequest
{
    internal FakeMergeRequest(long id, int iid, FakeGitLabProject target, FakeGitLabProject source, string title, string author, string sourceBranch, string targetBranch, string? description, DateTimeOffset createdAt)
    {
        Id = id;
        Iid = iid;
        Target = target;
        Source = source;
        Title = title;
        Author = author;
        SourceBranch = sourceBranch;
        TargetBranch = targetBranch;
        Description = description;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public long Id { get; }

    public int Iid { get; }

    public FakeGitLabProject Target { get; }

    public FakeGitLabProject Source { get; }

    /// <summary>The title as sent; <see cref="Draft"/> reads it.</summary>
    public string Title { get; }

    /// <summary>The author's username.</summary>
    public string Author { get; }

    public string SourceBranch { get; }

    public string TargetBranch { get; }

    /// <summary>The description as sent (the fake runs no quick action, D370).</summary>
    public string? Description { get; }

    /// <summary><c>opened</c> unless set or changed by a PUT's <c>state_event</c>.</summary>
    public string State { get; set; } = "opened";

    /// <summary>Whether the title starts <c>Draft:</c>, <c>[Draft]</c> or <c>(Draft)</c> without case (READ r6 5.1).</summary>
    public bool Draft => FakeGitLab.IsDraftTitle(Title);

    /// <summary>A fork's <c>allow_collaboration</c>, as the create sent it.</summary>
    public bool AllowCollaboration { get; internal set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; internal set; }

    /// <summary>Its head, <c>sha</c>: the source branch's tip at the last ref sync (<see cref="FakeGitLab.SyncRefsAsync"/>); null before one.</summary>
    public string? HeadSha { get; internal set; }

    /// <summary>The target branch's tip at the last ref sync (<c>diff_refs</c>' base and start, ASSUMED).</summary>
    internal string? TargetSha { get; set; }

    /// <summary>Deleted by the API: out of every list; its head ref goes at the next sync.</summary>
    internal bool Deleted { get; set; }

    public override string ToString() => $"{Target.Path}!{Iid}";
}

/// <summary>
/// A failure the fake answers once in place of a route's normal answer (<see cref="FakeHostingService.FailNext"/>,
/// docs/phase-7b-design.md 4.4): first, before the credential is read (4.3's check 1), but
/// <see cref="CreatedThenDropped"/>, whose create is made in full before the connection closes.
/// The core's <see cref="FakeServiceFault.Dropped"/> and <see cref="FakeServiceFault.ServedThenDropped"/> serve too.
/// </summary>
public sealed class FakeGitLabFault : FakeServiceFault
{
    private FakeGitLabFault(string name, Uri? location = null)
        : base(name)
    {
        Location = location;
    }

    /// <summary>401 shape A <c>{"message":"401 Unauthorized"}</c>, no <c>WWW-Authenticate</c> (MEASURED r6 1.2).</summary>
    public static FakeGitLabFault Unauthorized { get; } = new(nameof(Unauthorized));

    /// <summary>401 shape F <c>invalid_token</c> "Token is expired. …" with its <c>WWW-Authenticate</c> (READ g2 G2.2).</summary>
    public static FakeGitLabFault Expired { get; } = new(nameof(Expired));

    /// <summary>401 shape F <c>invalid_token</c> "Token was revoked. …" with its <c>WWW-Authenticate</c> (READ g2 G2.2).</summary>
    public static FakeGitLabFault Revoked { get; } = new(nameof(Revoked));

    /// <summary>403 shape F <c>insufficient_scope</c>, the route's <c>scope</c> list in body and header (READ g2 G2.3; the lists ASSUMED).</summary>
    public static FakeGitLabFault InsufficientScope { get; } = new(nameof(InsufficientScope));

    /// <summary>403 shape F <c>insufficient_granular_scope</c> naming the route's boundary and label, no <c>WWW-Authenticate</c> (READ g2 G2.4).</summary>
    public static FakeGitLabFault InsufficientGranular { get; } = new(nameof(InsufficientGranular));

    /// <summary>403 shape A <c>{"message":"403 Forbidden"}</c> (READ r6 5.2).</summary>
    public static FakeGitLabFault Forbidden { get; } = new(nameof(Forbidden));

    /// <summary>404 shape A <c>{"message":"404 Not Found"}</c> (READ r6 1.2).</summary>
    public static FakeGitLabFault NotFound { get; } = new(nameof(NotFound));

    /// <summary>429 shape G <c>text/plain</c> "Retry later\n", <c>Retry-After: 60</c>, <c>RateLimit-ResetTime</c> and the <c>ratelimit-*</c> (Rack::Attack, READ r6 8).</summary>
    public static FakeGitLabFault RateLimited { get; } = new(nameof(RateLimited));

    /// <summary>429 shape D "This endpoint has been requested too many times. Try again later.", <c>Retry-After: 600</c>, <c>RateLimit-Name</c> alone (application limits, READ r6 8).</summary>
    public static FakeGitLabFault EndpointRateLimited { get; } = new(nameof(EndpointRateLimited));

    /// <summary>408 shape D <c>{"message":{"error":"Request timed out"}}</c> (READ r6 5.2, 8).</summary>
    public static FakeGitLabFault Timeout { get; } = new(nameof(Timeout));

    /// <summary>500 shape A <c>{"message":"500 Internal Server Error"}</c> (ASSUMED).</summary>
    public static FakeGitLabFault ServerError { get; } = new(nameof(ServerError));

    /// <summary>502 with an HTML body (ASSUMED: gitlab.com is behind Cloudflare, r6 8).</summary>
    public static FakeGitLabFault BadGateway { get; } = new(nameof(BadGateway));

    /// <summary>200 <c>application/json</c> cut short: <c>{"id":</c>.</summary>
    public static FakeGitLabFault Malformed { get; } = new(nameof(Malformed));

    /// <summary>The request served in full (a create made), then the connection closed without an answer (7a's).</summary>
    public static FakeGitLabFault CreatedThenDropped { get; } = new(nameof(CreatedThenDropped));

    /// <summary>The 301's target of <see cref="MovedTo"/>.</summary>
    public Uri? Location { get; }

    /// <summary>301 <c>text/plain</c> "This resource has been moved permanently to &lt;location&gt;" with that <c>Location</c> (the moved project's form, MEASURED r6 3.2).</summary>
    public static FakeGitLabFault MovedTo(Uri location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return new FakeGitLabFault(nameof(MovedTo), location);
    }

    public override string ToString() => Location is null ? Name : $"{Name} {Location}";
}
