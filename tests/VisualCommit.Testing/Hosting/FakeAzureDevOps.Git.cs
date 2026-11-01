using System.Globalization;
using System.IO.Enumeration;
using System.Text;
using VisualCommit.Core;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The fake of Azure DevOps' git (docs/phase-7b-design.md 3.5): its repositories' folders, served at
/// <c>{o}/{p}/_git/{r}</c> and, when the repository is named as its project, at <c>{o}/_git/{r}</c>
/// too (MEASURED r5 6.1), never with a final <c>.git</c> stripped (MEASURED r5 6.1); who git signs in
/// as; and the ref sync of pull requests' merge refs (D267's pattern), the only pull request refs
/// Azure DevOps has (MEASURED r5 3.3: <c>refs/pull/&lt;n&gt;/merge</c>, never <c>/head</c>). Git runs
/// outside the fake's lock, isolated from the machine.
/// </summary>
public sealed partial class FakeAzureDevOps
{
    private const string ZeroId = "0000000000000000000000000000000000000000";

    /// <summary>
    /// Adds the repository <paramref name="name"/> of <paramref name="organization"/>'s
    /// <paramref name="project"/> at <c>&lt;root&gt;/{o}/{p}/_git/{r}</c> (the stored spellings): an
    /// existing bare repository there in place, else one <c>git clone --bare --no-hardlinks</c> of
    /// <paramref name="sourceRepository"/>, else a new empty one (7a design 2.3's rule, design 2.3).
    /// The server, when there is one, serves it at once; at <c>{o}/_git/{r}</c> too when
    /// <paramref name="name"/> is its project's name without case (MEASURED r5 6.1).
    /// </summary>
    /// <param name="forkOf">The repository it is a fork of (<c>isFork</c>, and <c>parentRepository</c> on <c>repo:</c>).</param>
    /// <exception cref="ArgumentException">The project is not the fake's, the name is not a repository's, or the project has it already.</exception>
    public async Task<FakeAzureRepository> AddRepositoryAsync(string organization, string project, string name, string? sourceRepository = null, FakeAzureRepository? forkOf = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        CheckName(name, nameof(name));
        FakeAzureProject owner;
        lock (Gate)
        {
            var inOrganization = OrganizationLocked(organization) ?? throw new ArgumentException($"The fake of Azure DevOps has no organization {organization}.", nameof(organization));
            owner = ProjectLocked(inOrganization, project) ?? throw new ArgumentException($"{inOrganization.Name} has no project {project}.", nameof(project));
            if (RepositoryLocked(owner, name) is not null)
            {
                throw new ArgumentException($"{owner} has the repository {name} already.", nameof(name));
            }

            // x beside x.git: git's callbacks would get one name for both (ServedName).
            if (_repositories.Find(other => other.Project == owner && string.Equals(ServedName(other.Name), ServedName(name), StringComparison.OrdinalIgnoreCase)) is { } twin)
            {
                throw new ArgumentException($"{owner} has the repository {twin.Name}, which git's server would name as {name}.", nameof(name));
            }

            if (forkOf is not null && !_repositories.Contains(forkOf))
            {
                throw new ArgumentException($"{forkOf} is not a repository of this fake.", nameof(forkOf));
            }
        }

        var folder = $"{owner.Organization.Name}/{owner.Name}/_git/{name}";
        var bare = await BareRepositoryAsync(folder, gitSuffix: false, sourceRepository, ct).ConfigureAwait(false);
        if (string.Equals(name, owner.Name, StringComparison.OrdinalIgnoreCase))
        {
            ServeAt($"{owner.Organization.Name}/_git/{name}", folder, gitSuffix: false);
        }

        lock (Gate)
        {
            var repository = new FakeAzureRepository(owner, name, MadeUpLocked(4), forkOf, bare);
            _repositories.Add(repository);
            return repository;
        }
    }

    /// <summary>
    /// Brings <paramref name="repository"/>'s merge refs in step with its pull requests (design 3.5),
    /// as the fake does before <c>pulls</c>, <c>pull</c>, <c>create</c>, <c>refs</c> and git's ref
    /// listing: an active pull request without <see cref="FakeAzurePullRequest.Conflicts"/> whose
    /// target's or source's tip moved gets a new <c>refs/pull/&lt;n&gt;/merge</c>, made by one
    /// <c>git commit-tree &lt;source&gt;^{tree} -p &lt;target&gt; -p &lt;source&gt;</c> with the message
    /// "Merge pull request &lt;n&gt; from &lt;source&gt; into &lt;target&gt;" (parents and message MEASURED
    /// r5 3.3), its creator at <see cref="AppClocks.HarnessTime"/> as author and committer; a fork's
    /// source is fetched first into <c>refs/fake/source/&lt;n&gt;</c>. One <c>update-ref --stdin</c>
    /// writes them and deletes every <c>refs/fake/source/*</c> and the merge refs of abandoned,
    /// completed (MEASURED r5 3.3) and conflicting ones (READ). A pull request whose branch is gone keeps its ref.
    /// </summary>
    public async Task SyncRefsAsync(FakeAzureRepository repository, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var gate = LockOf(repository.FolderName);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await SyncLockedAsync(repository, ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Whether git may read a repository (its folder's name) anonymously: its project is public and it is not disabled (MEASURED r5 2.1: a disabled one answered 401).</summary>
    protected override bool IsPublic(string repository)
    {
        lock (Gate)
        {
            return RepositoryByFolderLocked(repository) is { IsDisabled: false, Project.IsPublic: true };
        }
    }

    /// <summary>Whether a login may push: a write token's user (no <c>#read</c>) who is one of the repository's contributors, or any when it names none (design 3.5).</summary>
    protected override bool MayPush(string repository, string login)
    {
        if (login.EndsWith(ReadOnlyLoginSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        lock (Gate)
        {
            var user = UserOfLoginLocked(login);
            return RepositoryByFolderLocked(repository) is { } served && user is not null && user.Organization == served.Project.Organization
                && (served.Contributors.Count == 0 || served.Contributors.Contains(user));
        }
    }

    /// <summary>A refused push's 403 text: TF401027 for <c>GenericContribute</c> (the text READ g1 G2, ReneSorensen/VSTSTools#44; that git's push gets it ASSUMED), naming the user's display name.</summary>
    protected override string PushRefusal(string repository, string login)
    {
        string identity;
        lock (Gate)
        {
            identity = UserOfLoginLocked(login.Replace(ReadOnlyLoginSuffix, string.Empty, StringComparison.Ordinal))?.DisplayName ?? login;
        }

        return PermissionText("GenericContribute", identity);
    }

    /// <summary>
    /// A Basic password's login for git: a known token, neither revoked nor expired, of the
    /// repository's organization (its first segment), under any non-empty user, the organization and
    /// <c>x-access-token</c> included (READ r5 0.5: "a username, which can be anything except an empty
    /// string"); another organization's token is refused (401, ASSUMED). The login is <c>{o}/{user id}</c>,
    /// with <c>#read</c> for a read-only token.
    /// </summary>
    protected override string? LoginOfToken(string repository, string token)
    {
        var organization = repository.Split('/')[0];
        lock (Gate)
        {
            return _azureTokens.TryGetValue(token, out var known) && !known.Expired && TokenLoginLocked(token) is not null
                && string.Equals(known.Organization.Name, organization, StringComparison.OrdinalIgnoreCase)
                ? known.Login
                : null;
        }
    }

    /// <summary>Before git lists a repository's refs: its ref sync, when it has pull requests.</summary>
    protected override async Task BeforeGitAsync(string repository, CancellationToken ct)
    {
        FakeAzureRepository? served;
        bool hasPulls;
        lock (Gate)
        {
            served = RepositoryByFolderLocked(repository);
            hasPulls = served is not null && _pullRequests.Exists(pull => pull.Repository == served);
        }

        if (served is not null && hasPulls)
        {
            await SyncRefsAsync(served, ct).ConfigureAwait(false);
        }
    }

    /// <summary><see cref="SyncRefsAsync"/> under the repository's lock; returns its refs as read before the sync (<c>refs/heads/</c>, <c>refs/pull/</c>, <c>refs/fake/</c>).</summary>
    private async Task<IReadOnlyDictionary<string, string>> SyncLockedAsync(FakeAzureRepository repository, CancellationToken ct)
    {
        var refs = await ReadRefsAsync(repository.BarePath, ct, "refs/heads/", "refs/pull/", "refs/fake/").ConfigureAwait(false);
        List<FakeAzurePullRequest> pulls;
        lock (Gate)
        {
            pulls = [.. _pullRequests.Where(pull => pull.Repository == repository)];
        }

        var stale = refs.Keys.Where(name => name.StartsWith("refs/fake/", StringComparison.Ordinal)).ToList();
        if (pulls.Count == 0 && stale.Count == 0)
        {
            return refs;
        }

        var forkRefs = new Dictionary<FakeAzureRepository, IReadOnlyDictionary<string, string>>();
        foreach (var fork in pulls.Where(pull => pull.IsActive && !pull.Conflicts && pull.Fork is not null).Select(pull => pull.Fork!).Distinct())
        {
            forkRefs[fork] = await ReadRefsAsync(fork.BarePath, ct, "refs/heads/").ConfigureAwait(false);
        }

        var updates = new StringBuilder();
        var toMake = new List<(FakeAzurePullRequest Pull, string Target, string Source)>();
        var fetches = new Dictionary<FakeAzureRepository, List<string>>();
        var dropped = new List<FakeAzurePullRequest>();
        foreach (var pull in pulls)
        {
            var mergeRef = MergeRef(pull.Id);
            refs.TryGetValue(mergeRef, out var existing);
            if (!pull.IsActive || pull.Conflicts)
            {
                if (existing is not null)
                {
                    updates.Append(CultureInfo.InvariantCulture, $"delete {mergeRef}\n");
                }

                dropped.Add(pull);
                continue;
            }

            refs.TryGetValue("refs/heads/" + pull.TargetBranch, out var target);
            string? source = null;
            if (pull.Fork is null)
            {
                refs.TryGetValue("refs/heads/" + pull.SourceBranch, out source);
            }
            else if (forkRefs.TryGetValue(pull.Fork, out var heads))
            {
                heads.TryGetValue("refs/heads/" + pull.SourceBranch, out source);
            }

            if (target is null || source is null)
            {
                continue;
            }

            bool current;
            lock (Gate)
            {
                current = pull.MergedFrom == (target, source) && existing is not null && existing == pull.MergeCommit;
            }

            if (current)
            {
                continue;
            }

            toMake.Add((pull, target, source));
            if (pull.Fork is not null)
            {
                if (!fetches.TryGetValue(pull.Fork, out var refspecs))
                {
                    fetches.Add(pull.Fork, refspecs = []);
                }

                refspecs.Add(string.Create(CultureInfo.InvariantCulture, $"+refs/heads/{pull.SourceBranch}:refs/fake/source/{pull.Id}"));
                stale.Add(string.Create(CultureInfo.InvariantCulture, $"refs/fake/source/{pull.Id}"));
            }
        }

        foreach (var (fork, refspecs) in fetches)
        {
            await GitOkAsync(repository.BarePath, null, null, ct, ["-c", "gc.auto=0", "fetch", "--quiet", "--no-tags", fork.BarePath, .. refspecs]).ConfigureAwait(false);
        }

        var made = new List<(FakeAzurePullRequest Pull, string Target, string Source, string Commit)>();
        foreach (var (pull, target, source) in toMake)
        {
            var when = string.Create(CultureInfo.InvariantCulture, $"@{AppClocks.HarnessTime.ToUnixTimeSeconds()} +0000");
            var environment = new Dictionary<string, string?>
            {
                ["GIT_AUTHOR_NAME"] = pull.Creator.DisplayName,
                ["GIT_AUTHOR_EMAIL"] = pull.Creator.UniqueName,
                ["GIT_AUTHOR_DATE"] = when,
                ["GIT_COMMITTER_NAME"] = pull.Creator.DisplayName,
                ["GIT_COMMITTER_EMAIL"] = pull.Creator.UniqueName,
                ["GIT_COMMITTER_DATE"] = when,
            };
            var message = string.Create(CultureInfo.InvariantCulture, $"Merge pull request {pull.Id} from {pull.SourceBranch} into {pull.TargetBranch}");
            var commit = (await GitOkAsync(repository.BarePath, null, environment, ct, "commit-tree", source + "^{tree}", "-p", target, "-p", source, "-m", message).ConfigureAwait(false)).Trim();
            updates.Append(CultureInfo.InvariantCulture, $"update {MergeRef(pull.Id)} {commit}\n");
            made.Add((pull, target, source, commit));
        }

        foreach (var name in stale.Distinct(StringComparer.Ordinal))
        {
            updates.Append(CultureInfo.InvariantCulture, $"delete {name}\n");
        }

        if (updates.Length > 0)
        {
            await GitOkAsync(repository.BarePath, updates.ToString(), null, ct, "update-ref", "--stdin").ConfigureAwait(false);
        }

        lock (Gate)
        {
            foreach (var (pull, target, source, commit) in made)
            {
                pull.MergeCommit = commit;
                pull.MergedFrom = (target, source);
                pull.MergedStatus = "succeeded";
                pull.LastMergeSourceCommit = source;
                pull.LastMergeTargetCommit = target;
            }

            foreach (var pull in dropped)
            {
                pull.MergedFrom = null;
            }
        }

        return refs;
    }

    /// <summary>
    /// Deletes branches and tags for <c>POST …/refs</c> (r5 4's results; design 3.3): each whose
    /// <c>oldObjectId</c> is its tip is deleted (<c>succeeded</c>); another tip or none is
    /// <c>staleOldObjectId</c> and a ref outside <c>refs/heads/</c> and <c>refs/tags/</c>
    /// <c>invalidRefName</c>, both with HTTP 200 (READ enum; the status ASSUMED). One <c>update-ref --stdin</c>.
    /// </summary>
    private async Task<IReadOnlyList<(string Name, string Old, string Status)>> DeleteRefsAsync(FakeAzureRepository repository, IReadOnlyList<(string Name, string Old)> deletes, CancellationToken ct)
    {
        var gate = LockOf(repository.FolderName);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var refs = await ReadRefsAsync(repository.BarePath, ct, "refs/heads/", "refs/tags/").ConfigureAwait(false);
            var results = new List<(string Name, string Old, string Status)>();
            var updates = new StringBuilder();
            foreach (var (name, old) in deletes)
            {
                if (!name.StartsWith("refs/heads/", StringComparison.Ordinal) && !name.StartsWith("refs/tags/", StringComparison.Ordinal))
                {
                    results.Add((name, old, "invalidRefName"));
                }
                else if (refs.TryGetValue(name, out var tip) && string.Equals(tip, old, StringComparison.OrdinalIgnoreCase))
                {
                    updates.Append(CultureInfo.InvariantCulture, $"delete {name} {tip}\n");
                    results.Add((name, old, "succeeded"));
                }
                else
                {
                    results.Add((name, old, "staleOldObjectId"));
                }
            }

            if (updates.Length > 0)
            {
                await GitOkAsync(repository.BarePath, updates.ToString(), null, ct, "update-ref", "--stdin").ConfigureAwait(false);
            }

            return results;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>The refs under <paramref name="prefixes"/> of a bare repository, by full name: one <c>for-each-ref</c>.</summary>
    private async Task<IReadOnlyDictionary<string, string>> ReadRefsAsync(string bare, CancellationToken ct, params string[] prefixes)
    {
        var listed = await GitOkAsync(bare, null, null, ct, ["for-each-ref", "--format=%(objectname) %(refname)", .. prefixes]).ConfigureAwait(false);
        var refs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in listed.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            if (space > 0)
            {
                refs[line[(space + 1)..].TrimEnd('\r')] = line[..space];
            }
        }

        return refs;
    }

    /// <summary>Runs git and returns its output; a failure throws with git's error text.</summary>
    private async Task<string> GitOkAsync(string folder, string? input, IReadOnlyDictionary<string, string?>? environment, CancellationToken ct, params string[] arguments)
    {
        var result = await GitAsync(folder, input, environment, ct, arguments).ConfigureAwait(false);
        return result.Succeeded
            ? result.StandardOutput
            : throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed in {folder}: {result.StandardError.Trim()}");
    }

    /// <summary>What a repository's answer reads from its folder: the default branch (null for an empty repository, MEASURED r5 2.1) and the size of its objects in bytes (0 when empty).</summary>
    private static (string? DefaultBranch, long Size) FolderFacts(FakeAzureRepository repository)
    {
        var bare = repository.BarePath;
        if (IsEmpty(bare))
        {
            return (null, 0);
        }

        const string Prefix = "ref: refs/heads/";
        var head = File.ReadAllText(Path.Combine(bare, "HEAD")).Trim();
        var branch = head.StartsWith(Prefix, StringComparison.Ordinal) ? head[Prefix.Length..] : "main";
        return ("refs/heads/" + branch, ObjectsSize(Path.Combine(bare, "objects")));
    }

    /// <summary>
    /// The bytes of the files under <paramref name="objects"/>, read outside any lock while git may be
    /// writing there: each file's length comes from the directory listing itself (no second look that a
    /// file git renamed or deleted in between could fail), a file that vanished counts 0, and a listing
    /// git disturbs ends the count with what it has (the value only fills <c>size</c>, which nothing reads).
    /// </summary>
    private static long ObjectsSize(string objects)
    {
        if (!Directory.Exists(objects))
        {
            return 0;
        }

        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
        var files = new FileSystemEnumerable<long>(objects, LengthOf, options) { ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory };
        long size = 0;
        try
        {
            foreach (var length in files)
            {
                size += length;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A folder git removed while it was listed: the count so far.
        }

        return size;

        static long LengthOf(ref FileSystemEntry entry)
        {
            try
            {
                return entry.Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return 0;
            }
        }
    }

    /// <summary>Whether a bare repository has no branch, read from its files.</summary>
    private static bool IsEmpty(string bare)
    {
        var heads = Path.Combine(bare, "refs", "heads");
        if (Directory.Exists(heads) && Directory.EnumerateFiles(heads, "*", SearchOption.AllDirectories).Any())
        {
            return false;
        }

        var packed = Path.Combine(bare, "packed-refs");
        return !File.Exists(packed) || !File.ReadLines(packed).Any(line => line.Contains(" refs/heads/", StringComparison.Ordinal));
    }

    /// <summary>The user of a login <c>{o}/{user id}</c> (a read-only one's suffix left out); null when none.</summary>
    private FakeAzureUser? UserOfLoginLocked(string login)
    {
        var slash = login.IndexOf('/', StringComparison.Ordinal);
        return slash > 0 && Guid.TryParse(login[(slash + 1)..], out var id) ? _users.Find(user => user.Id == id) : null;
    }

    private static string MergeRef(int number) => string.Create(CultureInfo.InvariantCulture, $"refs/pull/{number}/merge");
}
