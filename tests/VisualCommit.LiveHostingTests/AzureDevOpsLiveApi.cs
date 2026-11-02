using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VisualCommit.Hosting.AzureDevOps;

namespace VisualCommit.LiveHostingTests;

/// <summary>A pull request as Azure DevOps lists and reads it: its id, its source ref, its status and whether it comes from a fork.</summary>
internal sealed record LiveAzurePullRequest(int Id, string SourceRef, string Status, bool FromFork)
{
    internal static LiveAzurePullRequest From(JsonElement pull) => new(
        pull.GetProperty("pullRequestId").GetInt32(),
        pull.TryGetProperty("sourceRefName", out var source) ? source.GetString() ?? string.Empty : string.Empty,
        pull.TryGetProperty("status", out var status) ? status.GetString() ?? string.Empty : string.Empty,
        pull.TryGetProperty("forkSource", out var fork) && fork.ValueKind == JsonValueKind.Object);
}

/// <summary>
/// The live command's own calls to Azure DevOps (docs/phase-7b-design.md 15.4, 15.5, 15.7), as the
/// app's client sends them (<c>Basic</c> of <c>":" + token</c>, <c>X-TFS-FedAuthRedirect:
/// Suppress</c> unless a probe leaves it out, <c>Accept: application/json</c>, 1.4's versions): the
/// sandbox's reads, its active pull requests and <c>visualcommit-live/</c> refs, the abandon, the refs
/// delete, the create and the probes. Its public constructor sends through
/// <see cref="LiveRequestLog"/> and <see cref="AzureDevOpsReach"/> to <c>https://dev.azure.com/</c>
/// over a handler that follows no redirect; the internal one goes over a given handler (the fake beside,
/// a stub).
/// </summary>
internal sealed class AzureDevOpsLiveApi : LiveServiceApi
{
    public const string ZeroId = "0000000000000000000000000000000000000000";

    public AzureDevOpsLiveApi(LiveAzureSandbox sandbox, AzureDevOpsReach reach, LiveRequestLog.Store store, LiveScreen screen, Action<string>? withheld = null)
        : base(new LiveRequestLog(NewHandler(), store, reach), AzureDevOpsEndpoints.Service.WebBase, CallLimit, sandbox.Token, screen, withheld)
    {
        Sandbox = sandbox;
        MadeUpOrganization = reach.MadeUpOrganization;
    }

    internal AzureDevOpsLiveApi(LiveAzureSandbox sandbox, HttpMessageHandler handler, Uri webBase, TimeSpan limit, LiveScreen screen, Action<string>? withheld = null, string madeUpOrganization = "vc-none-000000000000")
        : base(handler, webBase, limit, sandbox.Token, screen, withheld)
    {
        Sandbox = sandbox;
        MadeUpOrganization = madeUpOrganization;
    }

    public override string Name => "Azure DevOps";

    public LiveAzureSandbox Sandbox { get; }

    /// <summary>D374's made-up organization of this run.</summary>
    public string MadeUpOrganization { get; }

    private string O => Uri.EscapeDataString(Sandbox.Organization);

    private string RepositoryPath => $"{O}/{Uri.EscapeDataString(Sandbox.Project)}/_apis/git/repositories/{Uri.EscapeDataString(Sandbox.Repository)}";

    /// <summary><c>{o}/_apis/connectionData</c> (or another organization's), with <paramref name="credential"/>, Suppress or not, at <paramref name="version"/>.</summary>
    public Task<LiveServiceAnswer> ConnectionDataAsync(LiveCredential credential, CancellationToken ct, bool suppress = true, string version = AzureDevOpsApi.ConnectionDataVersion, string? organization = null) =>
        SendAsync(HttpMethod.Get, $"{Uri.EscapeDataString(organization ?? Sandbox.Organization)}/_apis/connectionData?api-version={version}", null, credential, ct, suppress);

    /// <summary>The core area's location (D360): <c>_apis/resourceAreas/{id}?organizationName={o}</c>.</summary>
    public Task<LiveServiceAnswer> CoreAreaAsync(LiveCredential credential, CancellationToken ct) =>
        SendAsync(HttpMethod.Get, $"_apis/resourceAreas/{AzureDevOpsApi.CoreAreaId}?organizationName={O}&api-version={AzureDevOpsApi.ConnectionDataVersion}", null, credential, ct);

    /// <summary>git's first read of the sandbox repository: <c>{o}/{p}/_git/{r}/info/refs?service=git-upload-pack</c>.</summary>
    public Task<LiveServiceAnswer> InfoRefsAsync(LiveCredential credential, bool suppress, CancellationToken ct) =>
        SendAsync(HttpMethod.Get, $"{O}/{Uri.EscapeDataString(Sandbox.Project)}/_git/{Uri.EscapeDataString(Sandbox.Repository)}/info/refs?service=git-upload-pack", null, credential, ct, suppress);

    /// <summary>The sandbox repository.</summary>
    public Task<LiveServiceAnswer> RepositoryAsync(CancellationToken ct) =>
        SendAsync(HttpMethod.Get, $"{RepositoryPath}?api-version={AzureDevOpsApi.Version}", null, LiveCredential.Token, ct);

    /// <summary>The sandbox project's repositories (<c>ListProject</c>'s list, 3.7).</summary>
    public Task<LiveServiceAnswer> RepositoriesAsync(CancellationToken ct) =>
        SendAsync(HttpMethod.Get, $"{O}/{Uri.EscapeDataString(Sandbox.Project)}/_apis/git/repositories?api-version={AzureDevOpsApi.Version}", null, LiveCredential.Token, ct);

    /// <summary>The sandbox's active pull requests, up to 100.</summary>
    /// <exception cref="LiveServiceException">Not 200 with a <c>value</c> list.</exception>
    public async Task<IReadOnlyList<LiveAzurePullRequest>> ActivePullRequestsAsync(CancellationToken ct)
    {
        var answer = await ExpectAsync(HttpMethod.Get, $"{RepositoryPath}/pullrequests?searchCriteria.status=active&$top=100&api-version={AzureDevOpsApi.Version}", null, 200, "the active pull requests", ct);
        return [.. ListOf(answer, "value").Select(LiveAzurePullRequest.From)];
    }

    public Task<LiveServiceAnswer> PullRequestAsync(int id, CancellationToken ct) =>
        SendAsync(HttpMethod.Get, $"{RepositoryPath}/pullrequests/{Show(id)}?api-version={AzureDevOpsApi.Version}", null, LiveCredential.Token, ct);

    /// <summary>Abandons a pull request (one cannot be deleted, 15.5).</summary>
    public Task<LiveServiceAnswer> AbandonAsync(int id, CancellationToken ct) =>
        SendAsync(HttpMethod.Patch, $"{RepositoryPath}/pullrequests/{Show(id)}?api-version={AzureDevOpsApi.Version}", new JsonObject { ["status"] = "abandoned" }, LiveCredential.Token, ct);

    /// <summary>A create from <paramref name="source"/> into <paramref name="target"/>, as answered.</summary>
    public Task<LiveServiceAnswer> CreateAsync(string source, string target, string title, bool draft, CancellationToken ct) =>
        SendAsync(
            HttpMethod.Post,
            $"{RepositoryPath}/pullrequests?api-version={AzureDevOpsApi.Version}",
            new JsonObject { ["sourceRefName"] = "refs/heads/" + source, ["targetRefName"] = "refs/heads/" + target, ["title"] = title, ["description"] = LiveSteps.Description, ["isDraft"] = draft },
            LiveCredential.Token,
            ct);

    /// <summary>The refs under <paramref name="filter"/> (<c>heads/visualcommit-live/</c>, <c>pull/{n}/merge</c>): each name and object id.</summary>
    /// <exception cref="LiveServiceException">Not 200 with a <c>value</c> list.</exception>
    public async Task<IReadOnlyList<(string Name, string ObjectId)>> RefsAsync(string filter, CancellationToken ct)
    {
        var answer = await ExpectAsync(HttpMethod.Get, $"{RepositoryPath}/refs?filter={Uri.EscapeDataString(filter)}&api-version={AzureDevOpsApi.Version}", null, 200, "the refs under " + filter, ct);
        return [.. ListOf(answer, "value").Select(item => (item.GetProperty("name").GetString() ?? string.Empty, item.GetProperty("objectId").GetString() ?? string.Empty))];
    }

    /// <summary>One <c>POST …/refs</c> deleting <paramref name="refs"/> (a zero <c>newObjectId</c> each), as answered.</summary>
    public Task<LiveServiceAnswer> DeleteRefsAsync(IEnumerable<(string Name, string ObjectId)> refs, CancellationToken ct) =>
        SendAsync(
            HttpMethod.Post,
            $"{RepositoryPath}/refs?api-version={AzureDevOpsApi.Version}",
            new JsonArray([.. refs.Select(reference => (JsonNode)new JsonObject { ["name"] = reference.Name, ["oldObjectId"] = reference.ObjectId, ["newObjectId"] = ZeroId })]),
            LiveCredential.Token,
            ct);

    protected override void Authorize(HttpRequestMessage request, string value) =>
        request.Headers.TryAddWithoutValidation("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + value)));

    protected override void AddHeaders(HttpRequestMessage request, bool suppress)
    {
        if (suppress)
        {
            request.Headers.TryAddWithoutValidation("X-TFS-FedAuthRedirect", "Suppress");
        }

        request.Headers.TryAddWithoutValidation("Accept", "application/json");
    }

    private static string Show(int value) => value.ToString(CultureInfo.InvariantCulture);
}
