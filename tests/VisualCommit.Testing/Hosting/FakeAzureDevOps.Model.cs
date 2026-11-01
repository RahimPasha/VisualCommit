using System.Globalization;
using System.Text;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// An organization of the fake of Azure DevOps (docs/phase-7b-design.md 3.2): its name in the
/// spelling it was added with, which every answer uses (MEASURED r5 2.1: the service writes a name in
/// its stored case), and whether it allows public projects, without which an anonymous
/// <c>connectionData</c> is refused (MEASURED r5 1.3).
/// </summary>
public sealed class FakeAzureOrganization
{
    internal FakeAzureOrganization(string name, bool publicProjects, Guid id)
    {
        Name = name;
        PublicProjects = publicProjects;
        Id = id;
    }

    /// <summary>The organization's name as added (<c>Fernhill-Labs</c>).</summary>
    public string Name { get; }

    /// <summary>Whether the organization allows public projects: an anonymous <c>connectionData</c> then answers the Anonymous identity (MEASURED r5 1.2).</summary>
    public bool PublicProjects { get; }

    /// <summary>A made-up GUID: the collection's id in a fork's <c>parentRepository.collection</c>.</summary>
    public Guid Id { get; }

    /// <summary>The next pull request's id: one sequence per organization (ASSUMED g1 G0.13).</summary>
    internal int NextPullRequestId { get; set; } = 1;

    /// <summary>How many tokens the organization has given (<c>azdo_FAKE-&lt;organization&gt;-&lt;n&gt;</c>).</summary>
    internal int TokenCount { get; set; }

    public override string ToString() => Name;
}

/// <summary>
/// A project of the fake of Azure DevOps (docs/phase-7b-design.md 3.2). A repository's visibility is
/// its project's (MEASURED r5 2.2). <see cref="Members"/> hides it from everyone else.
/// </summary>
public sealed class FakeAzureProject
{
    private IReadOnlyList<FakeAzureUser> _members = [];

    internal FakeAzureProject(FakeAzureOrganization organization, string name, Guid id, bool isPublic, string? description)
    {
        Organization = organization;
        Name = name;
        Id = id;
        IsPublic = isPublic;
        Description = description;
    }

    public FakeAzureOrganization Organization { get; }

    /// <summary>The project's name as added (<c>Mobile App</c>; a space is <c>%20</c> in its URLs).</summary>
    public string Name { get; }

    /// <summary>A made-up GUID: the project in the API's <c>url</c> members.</summary>
    public Guid Id { get; }

    /// <summary>Whether anyone, signed in or not, reads it (<c>visibility: "public"</c>).</summary>
    public bool IsPublic { get; }

    /// <summary>The project's description in a repository's <c>project</c>; none when null.</summary>
    public string? Description { get; }

    /// <summary>The users of its organization who see it when it is private; empty (the default): every one of them.</summary>
    public IReadOnlyList<FakeAzureUser> Members
    {
        get => Volatile.Read(ref _members);
        set => Volatile.Write(ref _members, value ?? []);
    }

    public override string ToString() => $"{Organization.Name}/{Name}";
}

/// <summary>
/// A user of the fake of Azure DevOps (docs/phase-7b-design.md 3.2, 3.4): a fixed made-up GUID, a
/// display name and a made-up address at example.com (<see cref="UniqueName"/>), which serves as the
/// identity's <c>properties.Account.$value</c> and <c>uniqueName</c>.
/// </summary>
public sealed class FakeAzureUser
{
    private string? _customDisplayName;
    private int _metaTypeId;

    internal FakeAzureUser(FakeAzureOrganization organization, Guid id, string displayName, string uniqueName)
    {
        Organization = organization;
        Id = id;
        DisplayName = displayName;
        UniqueName = uniqueName;
    }

    public FakeAzureOrganization Organization { get; }

    /// <summary>The identity's id, its "storage key" (a made-up GUID, fixed by the order users are added in).</summary>
    public Guid Id { get; }

    /// <summary>The display name: <c>providerDisplayName</c>, a pull request's <c>createdBy.displayName</c>.</summary>
    public string DisplayName { get; }

    /// <summary>The made-up address (<c>ada.lovelace@example.com</c>; the display name itself when it is one).</summary>
    public string UniqueName { get; }

    /// <summary>The identity's <c>customDisplayName</c>, left out when null (the default; g1 G3.2 sample 1, READ).</summary>
    public string? CustomDisplayName
    {
        get => Volatile.Read(ref _customDisplayName);
        set => Volatile.Write(ref _customDisplayName, value);
    }

    /// <summary>The identity's <c>metaTypeId</c>: 0 (a member, g1 G3.2, READ) unless set; 255 is a member's too on another host (g1 G0.4, READ).</summary>
    public int MetaTypeId
    {
        get => Volatile.Read(ref _metaTypeId);
        set => Volatile.Write(ref _metaTypeId, value);
    }

    /// <summary>The graph descriptor: <c>aad.</c> and the base64 of a GUID's text (g1 G3.3, READ; the fake's of its own id).</summary>
    public string SubjectDescriptor => "aad." + Convert.ToBase64String(Encoding.UTF8.GetBytes(Id.ToString("D", CultureInfo.InvariantCulture)));

    public override string ToString() => $"{Organization.Name}/{DisplayName}";
}

/// <summary>
/// A repository of the fake of Azure DevOps (docs/phase-7b-design.md 3.2, 3.5), its bare folder at
/// <c>&lt;root&gt;/{o}/{p}/_git/{r}</c>. Writes need <see cref="Contributors"/>' leave when it is set.
/// </summary>
public sealed class FakeAzureRepository
{
    private IReadOnlyList<FakeAzureUser> _contributors = [];
    private int _isDisabled;

    internal FakeAzureRepository(FakeAzureProject project, string name, Guid id, FakeAzureRepository? forkOf, string barePath)
    {
        Project = project;
        Name = name;
        Id = id;
        ForkOf = forkOf;
        BarePath = barePath;
    }

    public FakeAzureProject Project { get; }

    /// <summary>The organization's name as added.</summary>
    public string Organization => Project.Organization.Name;

    /// <summary>The repository's name as added (Azure DevOps keeps a final <c>.git</c> in a name, r5 6.1).</summary>
    public string Name { get; }

    /// <summary>A made-up GUID, by which the API finds it too (MEASURED r5 2.1).</summary>
    public Guid Id { get; }

    /// <summary>The repository it is a fork of (<c>isFork</c>, <c>parentRepository</c>); null when none.</summary>
    public FakeAzureRepository? ForkOf { get; }

    /// <summary>The bare repository's folder.</summary>
    public string BarePath { get; }

    /// <summary>Its folder's name under the fake's root, the name of every git callback, hold and gate: <c>Fernhill-Labs/Mobile App/_git/mobile-app</c>.</summary>
    public string FolderName => $"{Organization}/{Project.Name}/_git/{Name}";

    /// <summary><c>isDisabled</c>: listed, but git refuses it anonymously (MEASURED r5 2.1).</summary>
    public bool IsDisabled
    {
        get => Volatile.Read(ref _isDisabled) != 0;
        set => Volatile.Write(ref _isDisabled, value ? 1 : 0);
    }

    /// <summary>The users who may push and create pull requests; empty (the default): everyone who reads it.</summary>
    public IReadOnlyList<FakeAzureUser> Contributors
    {
        get => Volatile.Read(ref _contributors);
        set => Volatile.Write(ref _contributors, value ?? []);
    }

    public override string ToString() => $"{Organization}/{Project.Name}/{Name}";
}

/// <summary>A pull request's <c>status</c> (READ r5 3.1): <c>active</c>, <c>abandoned</c>, <c>completed</c>.</summary>
public enum FakeAzurePullRequestStatus
{
    Active,
    Abandoned,
    Completed,
}

/// <summary>
/// A pull request of the fake of Azure DevOps (docs/phase-7b-design.md 3.2, 3.5) into
/// <see cref="Repository"/>, from a branch of it or of <see cref="Fork"/>. Its merge ref,
/// <c>refs/pull/&lt;id&gt;/merge</c>, is written by the ref sync while it is active and without
/// <see cref="Conflicts"/>; <see cref="MergeStatus"/> is <c>queued</c> until the first sync after it was
/// made (READ sample).
/// </summary>
public sealed class FakeAzurePullRequest
{
    private int _status;
    private int _conflicts;

    internal FakeAzurePullRequest(int id, FakeAzureRepository repository, FakeAzureRepository? fork, string title, string? description, FakeAzureUser creator, string sourceBranch, string targetBranch, bool isDraft, Guid mergeId)
    {
        Id = id;
        Repository = repository;
        Fork = fork;
        Title = title;
        Description = description;
        Creator = creator;
        SourceBranch = sourceBranch;
        TargetBranch = targetBranch;
        IsDraft = isDraft;
        MergeId = mergeId;
    }

    /// <summary><c>pullRequestId</c>, one sequence per organization (ASSUMED g1 G0.13).</summary>
    public int Id { get; }

    /// <summary>The target repository (<c>repository</c>).</summary>
    public FakeAzureRepository Repository { get; }

    /// <summary>The fork its source branch is in (<c>forkSource</c>); null for a branch of <see cref="Repository"/>.</summary>
    public FakeAzureRepository? Fork { get; }

    public string Title { get; internal set; }

    /// <summary>The description; left out when null; cut to 400 characters in a list (READ and MEASURED r5 0.8).</summary>
    public string? Description { get; internal set; }

    /// <summary><c>createdBy</c>; the merge commits' author and committer.</summary>
    public FakeAzureUser Creator { get; }

    /// <summary>The source branch's name without <c>refs/heads/</c>, in <see cref="Fork"/> when there is one.</summary>
    public string SourceBranch { get; }

    /// <summary>The target branch's name without <c>refs/heads/</c>.</summary>
    public string TargetBranch { get; }

    public bool IsDraft { get; }

    /// <summary><c>mergeId</c>, a made-up GUID.</summary>
    public Guid MergeId { get; }

    /// <summary>The status; set it to abandon or complete the pull request (its merge ref goes at the next sync).</summary>
    public FakeAzurePullRequestStatus Status
    {
        get => (FakeAzurePullRequestStatus)Volatile.Read(ref _status);
        set => Volatile.Write(ref _status, (int)value);
    }

    /// <summary>Whether its branches conflict: <c>mergeStatus: "conflicts"</c>, no merge commit and no merge ref (READ r5 8.4).</summary>
    public bool Conflicts
    {
        get => Volatile.Read(ref _conflicts) != 0;
        set => Volatile.Write(ref _conflicts, value ? 1 : 0);
    }

    /// <summary><c>mergeStatus</c>: <c>queued</c> until the first sync after it was made, then <c>succeeded</c>; <c>conflicts</c> while <see cref="Conflicts"/>.</summary>
    public string MergeStatus => Conflicts ? "conflicts" : MergedStatus;

    /// <summary><c>lastMergeSourceCommit.commitId</c>: the source's tip at the last merge, or at its making; null before.</summary>
    public string? LastMergeSourceCommit { get; internal set; }

    /// <summary><c>lastMergeTargetCommit.commitId</c>.</summary>
    public string? LastMergeTargetCommit { get; internal set; }

    /// <summary><c>lastMergeCommit.commitId</c>: <c>refs/pull/&lt;id&gt;/merge</c>; null while queued or conflicting (READ g1 G1.2).</summary>
    public string? LastMergeCommit => Conflicts ? null : MergeCommit;

    internal string MergedStatus { get; set; } = "queued";

    internal string? MergeCommit { get; set; }

    /// <summary>The target's and source's tips <see cref="MergeCommit"/> was made from; null when none was.</summary>
    internal (string Target, string Source)? MergedFrom { get; set; }

    internal bool IsActive => Status == FakeAzurePullRequestStatus.Active;

    public override string ToString() => $"{Repository}!{Id}";
}

/// <summary>
/// A failure the fake of Azure DevOps answers once in place of a route's normal answer
/// (<see cref="FakeHostingService.FailNext"/>, docs/phase-7b-design.md 3.4; r5 8.4, g1 G2.2): after
/// the organization and version checks, before the credentials, but for <see cref="AnonymousIdentity"/>,
/// <see cref="Delayed"/> and <see cref="CreatedThenDropped"/>, which change the normal answer.
/// </summary>
public sealed class FakeAzureDevOpsFault : FakeServiceFault
{
    private FakeAzureDevOpsFault(string name)
        : base(name)
    {
    }

    /// <summary>401 with an empty body and one <c>WWW-Authenticate: Basic realm="…"</c>: a PAT-shaped token unknown or revoked (READ g1 G2, community).</summary>
    public static FakeAzureDevOpsFault InvalidToken { get; } = new(nameof(InvalidToken));

    /// <summary>401 <c>UnauthorizedRequestException</c> TF400813 naming the caller's id (the token's user, else the anonymous one; READ g1 G2).</summary>
    public static FakeAzureDevOpsFault Unauthorized { get; } = new(nameof(Unauthorized));

    /// <summary>401 <c>AccessCheckException</c> "Access Denied: The Personal Access Token used has expired.", led by a UTF-8 BOM (READ g1 G2; on dev.azure.com ASSUMED).</summary>
    public static FakeAzureDevOpsFault Expired { get; } = new(nameof(Expired));

    /// <summary>A 302 to the fake's own <c>/_signin?…</c> despite <c>X-TFS-FedAuthRedirect: Suppress</c> (ASSUMED).</summary>
    public static FakeAzureDevOpsFault SignInRedirect { get; } = new(nameof(SignInRedirect));

    /// <summary>203 <c>text/html</c> on the API's origin: defensive only, since the service's 203 is its sign-in page's (g1 G0.1, MEASURED there; ASSUMED here).</summary>
    public static FakeAzureDevOpsFault NonAuthoritative { get; } = new(nameof(NonAuthoritative));

    /// <summary>The token ignored: <c>connectionData</c> answers the Anonymous identity, any other route as to no credential (g1 G0.3, READ).</summary>
    public static FakeAzureDevOpsFault AnonymousIdentity { get; } = new(nameof(AnonymousIdentity));

    /// <summary>429 TF400733 with <c>Retry-After</c> and <c>X-RateLimit-*</c> (READ r5 7; the typeKey ASSUMED).</summary>
    public static FakeAzureDevOpsFault Throttled { get; } = new(nameof(Throttled));

    /// <summary>The normal answer with <c>Retry-After</c> and <c>X-RateLimit-Delay</c> (READ r5 7: "still returns HTTP 200").</summary>
    public static FakeAzureDevOpsFault Delayed { get; } = new(nameof(Delayed));

    /// <summary>400 <c>VssVersionOutOfRangeException</c> for the version sent (MEASURED words, r5 1.4).</summary>
    public static FakeAzureDevOpsFault VersionRefused { get; } = new(nameof(VersionRefused));

    /// <summary>404: TF401180 on a pull request's route, else TF401019 (MEASURED shapes, r5 1.4).</summary>
    public static FakeAzureDevOpsFault NotFound { get; } = new(nameof(NotFound));

    /// <summary>404 <c>text/plain</c> "Page not found" (MEASURED r5 1.4).</summary>
    public static FakeAzureDevOpsFault RouteMissing { get; } = new(nameof(RouteMissing));

    /// <summary>404 <c>text/plain</c> "The resource cannot be found." with <c>X-TFS-ServiceError</c> and no <c>ActivityId</c> (MEASURED r5 1.3).</summary>
    public static FakeAzureDevOpsFault OrganizationMissing { get; } = new(nameof(OrganizationMissing));

    /// <summary>500 TF400898 "An Internal Error Occurred." (READ g1 G2; the status and typeKey ASSUMED).</summary>
    public static FakeAzureDevOpsFault ServerError { get; } = new(nameof(ServerError));

    /// <summary>503 with an HTML body (ASSUMED).</summary>
    public static FakeAzureDevOpsFault BadGateway { get; } = new(nameof(BadGateway));

    /// <summary>200 with the JSON cut short.</summary>
    public static FakeAzureDevOpsFault Malformed { get; } = new(nameof(Malformed));

    /// <summary>The request served in full (a create made), then the connection closed without an answer (7a's).</summary>
    public static FakeAzureDevOpsFault CreatedThenDropped { get; } = new(nameof(CreatedThenDropped));
}

/// <summary>A token of the fake of Azure DevOps: one organization's, one user's, never shown.</summary>
internal sealed class FakeAzureToken(FakeAzureOrganization organization, FakeAzureUser user, bool codeWrite, string login)
{
    public FakeAzureOrganization Organization { get; } = organization;

    public FakeAzureUser User { get; } = user;

    /// <summary>Code (Read &amp; write); else Code (Read) alone.</summary>
    public bool CodeWrite { get; } = codeWrite;

    /// <summary>Its login in the request log and git's: <c>{o}/{user id}</c>, with <c>#read</c> for a read-only token.</summary>
    public string Login { get; } = login;

    public bool Expired { get; set; }

    public override string ToString() => $"FakeAzureToken({Login}, ***)";
}
