using System.Globalization;
using System.Text;
using VisualCommit.Hosting.AzureDevOps;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// A local Azure DevOps for the tests (docs/phase-7b-design.md 1.9, 3; D346, D363, D370): its API,
/// pages and git on one host, <c>http://127.0.0.1:&lt;port&gt;/{o}/…</c>, on the fakes' shared core
/// (<see cref="FakeHostingService"/>) and <see cref="AuthRemoteServer"/>'s host mode
/// (<see cref="StartAsync"/>), or the same routes in process (<see cref="InProcess"/>,
/// <see cref="FakeHostingService.CreateHandler"/>). Written from the research (r5 and its gap round
/// g1), not from a client: each answer's mark, MEASURED, READ or ASSUMED, is in its doc comment, and
/// the ASSUMED ones the live command settles are listed at the head of <c>FakeAzureDevOps.Router.cs</c>
/// (design 3.7). No <c>&lt;organization&gt;.visualstudio.com</c> is served (r5 0.10).
/// <para>
/// Organizations, projects, users, tokens, repositories and pull requests are made by the test
/// (<see cref="AddOrganization"/> and the members beside it); names are looked up without case and
/// answered in the spelling they were added with (MEASURED r5 2.1). Made-up values only: every GUID
/// is the fake's own, every address at example.com, every token <c>azdo_FAKE-&lt;organization&gt;-&lt;n&gt;</c>.
/// </para>
/// </summary>
public sealed partial class FakeAzureDevOps : FakeHostingService
{
    /// <summary>The port of <see cref="InProcess"/>'s endpoints, <c>Local(2)</c>: never listened on.</summary>
    public const int InProcessPort = 2;

    /// <summary>
    /// The realm of the <c>WWW-Authenticate: Basic</c> among the sign-in's three challenges on the API, a
    /// 302's and a 401's (MEASURED r5 1.3), and of git's 401s (MEASURED g1 G4.1: git's first request
    /// with no credential, on three organizations); the host the fake's own.
    /// </summary>
    public const string TfsRealm = "https://tfsprodfake.app.visualstudio.com/";

    /// <summary>
    /// The realm of the API's empty 401 to a PAT-shaped value it refuses (MEASURED 2026-11-05 on
    /// connectionData: the service's own <c>tfsprod…</c> host under <c>visualstudio.com</c>, without
    /// <see cref="TfsRealm"/>'s <c>.app.</c> label; the host the fake's own). Git's 401 to a refused
    /// credential carried this form too (MEASURED 2026-11-05 on the sandbox's info/refs), but the fake's
    /// git keeps <see cref="TfsRealm"/> on every 401: a kept difference (the Router's header, D370).
    /// </summary>
    public const string TokenRealm = "https://tfsprodfake.visualstudio.com/";

    /// <summary>The suffix of a read-only token's login (<c>{o}/{user id}#read</c>), which <see cref="MayPush"/> refuses.</summary>
    private const string ReadOnlyLoginSuffix = "#read";

    private readonly List<FakeAzureOrganization> _organizations = [];
    private readonly List<FakeAzureProject> _projects = [];
    private readonly List<FakeAzureUser> _users = [];
    private readonly List<FakeAzureRepository> _repositories = [];
    private readonly List<FakeAzurePullRequest> _pullRequests = [];
    private readonly Dictionary<string, FakeAzureToken> _azureTokens = new(StringComparer.Ordinal);
    private int _madeUp;
    private int _createStatus = 201;

    private FakeAzureDevOps(string root)
        : base(root, "Azure DevOps", InProcessPort)
    {
    }

    /// <summary>The fake's addresses: <c>AzureDevOpsEndpoints.Local(Port)</c>; <c>Local(2)</c> in process.</summary>
    public AzureDevOpsEndpoints Endpoints => AzureDevOpsEndpoints.Local(Port);

    /// <summary>
    /// The status of a pull request's create: 201 unless set (MEASURED 2026-11-05: the service sent
    /// 201 for a draft and for one with nothing ahead; the documentation's table says 200, g1 G0.12,
    /// which the client takes too).
    /// </summary>
    public int CreateStatus
    {
        get => Volatile.Read(ref _createStatus);
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 200);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 299);
            Volatile.Write(ref _createStatus, value);
        }
    }

    /// <summary>
    /// Git's 401s, which <see cref="AuthRemoteServer"/> gives one realm: <see cref="TfsRealm"/>, the form
    /// g1 G4.1 measured for the first request of every clone and fetch, which carries no credential.
    /// </summary>
    protected override string Realm => TfsRealm;

    /// <summary>
    /// Starts the fake on 127.0.0.1 in <see cref="AuthRemoteServer"/>'s host mode on
    /// <paramref name="root"/> (made when missing): on <paramref name="port"/>, or one the system
    /// picks when it is 0; with <paramref name="fallBackToFreePort"/> a taken port gives way to a
    /// free one (<see cref="FakeHostingService.PortWasTaken"/>). Nothing under <paramref name="root"/> is deleted.
    /// </summary>
    public static async Task<FakeAzureDevOps> StartAsync(string root, int port = 0, bool fallBackToFreePort = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ct.ThrowIfCancellationRequested();
        var fake = new FakeAzureDevOps(root);
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
    /// by path from any host; git's paths get 404. <see cref="Endpoints"/> is <c>Local(2)</c>.
    /// </summary>
    public static FakeAzureDevOps InProcess(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return new FakeAzureDevOps(root);
    }

    /// <summary>
    /// Adds the organization <paramref name="name"/> (letters, digits and hyphens, r5 6.1), answered
    /// in this spelling; with <paramref name="publicProjects"/> an anonymous <c>connectionData</c>
    /// answers the Anonymous identity (MEASURED r5 1.3), and so does one with a Basic value in none of
    /// the PAT forms (MEASURED 2026-11-05 and 2026-11-07); without it both get the sign-in's answer.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not an organization's, or the fake has it already (without case).</exception>
    public FakeAzureOrganization AddOrganization(string name, bool publicProjects = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!name.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') || name.StartsWith('-') || name.EndsWith('-'))
        {
            throw new ArgumentException($"An organization's name is of letters, digits and hyphens, and starts and ends with a letter or digit: '{name}'.", nameof(name));
        }

        lock (Gate)
        {
            if (OrganizationLocked(name) is not null)
            {
                throw new ArgumentException($"The fake of Azure DevOps has the organization {name} already.", nameof(name));
            }

            var organization = new FakeAzureOrganization(name, publicProjects, MadeUpLocked(1));
            _organizations.Add(organization);
            return organization;
        }
    }

    /// <summary>Adds the project <paramref name="name"/> to <paramref name="organization"/>: private unless <paramref name="isPublic"/>, with <paramref name="description"/> in its repositories' answers.</summary>
    /// <exception cref="ArgumentException">The organization is not the fake's, the name is not a project's, or the organization has it already.</exception>
    public FakeAzureProject AddProject(string organization, string name, bool isPublic = false, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);
        CheckName(name, nameof(name));
        lock (Gate)
        {
            var owner = OrganizationLocked(organization) ?? throw new ArgumentException($"The fake of Azure DevOps has no organization {organization}.", nameof(organization));
            if (ProjectLocked(owner, name) is not null)
            {
                throw new ArgumentException($"{owner.Name} has the project {name} already.", nameof(name));
            }

            var project = new FakeAzureProject(owner, name, MadeUpLocked(2), isPublic, description);
            _projects.Add(project);
            return project;
        }
    }

    /// <summary>
    /// Adds a user of <paramref name="organization"/> named <paramref name="displayName"/>, with a fixed
    /// made-up GUID and the address <c>&lt;name&gt;@example.com</c> (<c>ada.lovelace@example.com</c> for
    /// "Ada Lovelace"; a display name that is an address is its own).
    /// </summary>
    public FakeAzureUser AddUser(string organization, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var address = displayName.Contains('@', StringComparison.Ordinal)
            ? displayName
            : string.Join('.', displayName.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => new string([.. word.Where(char.IsAsciiLetterOrDigit)]))) + "@example.com";
        lock (Gate)
        {
            var owner = OrganizationLocked(organization) ?? throw new ArgumentException($"The fake of Azure DevOps has no organization {organization}.", nameof(organization));
            var user = new FakeAzureUser(owner, MadeUpLocked(3), displayName, address);
            _users.Add(user);
            return user;
        }
    }

    /// <summary>
    /// A new token of <paramref name="user"/> for <paramref name="organization"/> alone (r5 5: a token
    /// names one organization), <c>azdo_FAKE-&lt;organization&gt;-&lt;n&gt;</c> with <c>n</c> counted per
    /// organization from 1; Code (Read &amp; write) unless <paramref name="codeWrite"/> is false.
    /// </summary>
    public string AddToken(string organization, FakeAzureUser user, bool codeWrite = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);
        ArgumentNullException.ThrowIfNull(user);
        lock (Gate)
        {
            var owner = OrganizationLocked(organization) ?? throw new ArgumentException($"The fake of Azure DevOps has no organization {organization}.", nameof(organization));
            if (!_users.Contains(user))
            {
                throw new ArgumentException($"{user} is not a user of this fake.", nameof(user));
            }

            owner.TokenCount++;
            var token = string.Create(CultureInfo.InvariantCulture, $"azdo_FAKE-{owner.Name}-{owner.TokenCount}");
            var login = string.Create(CultureInfo.InvariantCulture, $"{owner.Name}/{user.Id:D}") + (codeWrite ? string.Empty : ReadOnlyLoginSuffix);
            _azureTokens[token] = new FakeAzureToken(owner, user, codeWrite, login);
            AddTokenLocked(token, login);
            return token;
        }
    }

    /// <summary>Lets <paramref name="token"/> expire: the API answers 401 <c>AccessCheckException</c> (READ g1 G2) and git 401.</summary>
    public void Expire(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        lock (Gate)
        {
            if (_azureTokens.TryGetValue(token, out var known))
            {
                known.Expired = true;
            }
        }
    }

    /// <summary>The repository <paramref name="organization"/>/<paramref name="project"/>/<paramref name="name"/>, without case.</summary>
    /// <exception cref="ArgumentException">The fake has no such repository.</exception>
    public FakeAzureRepository Repository(string organization, string project, string name)
    {
        lock (Gate)
        {
            var owner = OrganizationLocked(organization);
            var inProject = owner is null ? null : ProjectLocked(owner, project);
            return (inProject is null ? null : RepositoryLocked(inProject, name))
                ?? throw new ArgumentException($"The fake of Azure DevOps has no repository {organization}/{project}/{name}.", nameof(name));
        }
    }

    /// <summary>The user of <paramref name="organization"/> named <paramref name="displayName"/>, both without case (the scenario's Ada Lovelace, Octo Cat).</summary>
    /// <exception cref="ArgumentException">The fake has no such user.</exception>
    public FakeAzureUser User(string organization, string displayName)
    {
        lock (Gate)
        {
            return _users.Find(user => string.Equals(user.Organization.Name, organization, StringComparison.OrdinalIgnoreCase) && string.Equals(user.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"The fake of Azure DevOps has no user {displayName} in {organization}.", nameof(displayName));
        }
    }

    /// <summary>The pull requests into <paramref name="repository"/>, by id.</summary>
    public IReadOnlyList<FakeAzurePullRequest> PullRequests(FakeAzureRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        lock (Gate)
        {
            return [.. _pullRequests.Where(pull => pull.Repository == repository).OrderBy(pull => pull.Id)];
        }
    }

    /// <summary>
    /// Adds an active pull request into <paramref name="target"/>'s <paramref name="targetBranch"/>
    /// from <paramref name="sourceBranch"/> (a name, with or without <c>refs/heads/</c>) of the target
    /// or of <paramref name="fork"/>, made by <paramref name="creator"/>; its id the organization's next
    /// unless given (one sequence per organization, ASSUMED g1 G0.13). Nothing is read from git here:
    /// the ref sync (<see cref="SyncRefsAsync"/>) writes its merge ref.
    /// </summary>
    /// <exception cref="ArgumentException">A repository or the creator is not the fake's, a branch name is empty, or the id is taken in the organization.</exception>
    public FakeAzurePullRequest AddPullRequest(FakeAzureRepository target, string title, FakeAzureUser creator, string sourceBranch, string targetBranch, FakeAzureRepository? fork = null, bool draft = false, string? description = null, int? id = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(creator);
        var source = BranchName(sourceBranch, nameof(sourceBranch));
        var into = BranchName(targetBranch, nameof(targetBranch));
        if (id is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "A pull request's id is 1 or more.");
        }

        lock (Gate)
        {
            if (!_repositories.Contains(target) || fork is not null && !_repositories.Contains(fork) || !_users.Contains(creator))
            {
                throw new ArgumentException("The repositories and the creator must be this fake's.", nameof(target));
            }

            var organization = target.Project.Organization;
            var number = id ?? organization.NextPullRequestId;
            if (_pullRequests.Any(pull => pull.Id == number && pull.Repository.Project.Organization == organization))
            {
                throw new ArgumentException($"{organization.Name} has the pull request {number} already.", nameof(id));
            }

            organization.NextPullRequestId = Math.Max(organization.NextPullRequestId, number + 1);
            var pull = new FakeAzurePullRequest(number, target, fork == target ? null : fork, title, description, creator, source, into, draft, MadeUpLocked(5));
            _pullRequests.Add(pull);
            return pull;
        }
    }

    private FakeAzureOrganization? OrganizationLocked(string name) =>
        _organizations.Find(organization => string.Equals(organization.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A project of <paramref name="organization"/> by name, without case (MEASURED r5 2.1), or by its
    /// GUID, as the fake's own <c>url</c> and <c>_links</c> name it (the API's addresses name the project
    /// by GUID, MEASURED r5 2.1; that the API takes the GUID in the path READ r5 6.1, MEASURED there on git).
    /// </summary>
    private FakeAzureProject? ProjectLocked(FakeAzureOrganization organization, string nameOrId) =>
        _projects.Find(project => project.Organization == organization && string.Equals(project.Name, nameOrId, StringComparison.OrdinalIgnoreCase))
        ?? (Guid.TryParse(nameOrId, out var id) ? _projects.Find(project => project.Organization == organization && project.Id == id) : null);

    /// <summary>A repository of <paramref name="project"/> by name or GUID, without case (MEASURED r5 2.1).</summary>
    private FakeAzureRepository? RepositoryLocked(FakeAzureProject project, string nameOrId) =>
        _repositories.Find(repository => repository.Project == project
            && (string.Equals(repository.Name, nameOrId, StringComparison.OrdinalIgnoreCase)
                || Guid.TryParse(nameOrId, out var id) && repository.Id == id));

    /// <summary>
    /// A repository by the name <see cref="AuthRemoteServer"/>'s callbacks give it: its folder's name
    /// (<c>{o}/{p}/_git/{r}</c>) less one final <c>.git</c>, cut as the server cuts it (ordinal), so that
    /// a repository named <c>tools.git</c> is found as <c>…/_git/tools</c>; without case.
    /// </summary>
    private FakeAzureRepository? RepositoryByFolderLocked(string served) =>
        _repositories.Find(repository => string.Equals(ServedName(repository.FolderName), served, StringComparison.OrdinalIgnoreCase));

    /// <summary>A folder's name less one final <c>.git</c> (ordinal), as <see cref="AuthRemoteServer"/> names a served repository to its callbacks.</summary>
    private static string ServedName(string folder) =>
        folder.EndsWith(".git", StringComparison.Ordinal) && folder.Length > 4 ? folder[..^4] : folder;

    /// <summary>A made-up GUID of <paramref name="kind"/> (1 organizations, 2 projects, 3 users, 4 repositories, 5 merge ids), fixed by the order things are added in.</summary>
    private Guid MadeUpLocked(int kind) => MadeUp(kind, ++_madeUp);

    private static Guid MadeUp(int kind, int n) =>
        Guid.Parse(string.Create(CultureInfo.InvariantCulture, $"7b0a{kind:x4}-{n:x4}-4a00-8000-00000000fa4e"));

    /// <summary>A project's or repository's name (r5 6.1): not empty, <c>.</c> or <c>..</c>, without <c>/</c>, <c>\</c> or a control character, not starting with <c>_</c>.</summary>
    private static void CheckName(string name, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, parameter);
        if (name is "." or ".." || name.StartsWith('_') || name.Any(c => c is '/' or '\\' || char.IsControl(c)))
        {
            throw new ArgumentException($"A project's or repository's name holds no '/', '\\' or control character and does not start with '_': '{name}'.", parameter);
        }
    }

    private static string BranchName(string branch, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch, parameter);
        const string Heads = "refs/heads/";
        var name = branch.StartsWith(Heads, StringComparison.Ordinal) ? branch[Heads.Length..] : branch;
        return name.Length > 0 ? name : throw new ArgumentException("A branch's name is not empty.", parameter);
    }

    /// <summary><paramref name="text"/> as UTF-8 bytes (an answer's body).</summary>
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
