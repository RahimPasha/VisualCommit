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

    /// <summary>The core area's location (D360), as A1's spelling read sends it (6.4): <c>{o}/_apis/resourceAreas/{id}</c> at <c>ConnectionDataVersion</c>.</summary>
    public Task<LiveServiceAnswer> CoreAreaAsync(LiveCredential credential, CancellationToken ct) =>
        SendAsync(HttpMethod.Get, $"{O}/_apis/resourceAreas/{AzureDevOpsApi.CoreAreaId}?api-version={AzureDevOpsApi.ConnectionDataVersion}", null, credential, ct);

    /// <summary>
    /// git's first read of the sandbox repository, <c>{o}/{p}/_git/{r}/info/refs?service=git-upload-pack</c>,
    /// sent as git sends it (g1 G0.7: the git endpoints answer by <c>User-Agent</c>): git's agent
    /// <paramref name="gitAgent"/>, <c>Accept: */*</c>, <c>Pragma: no-cache</c> and
    /// <c>Git-Protocol: version=2</c>, never the API's <c>Accept</c>; Suppress as <paramref name="suppress"/> says (D373).
    /// </summary>
    public Task<LiveServiceAnswer> InfoRefsAsync(LiveCredential credential, bool suppress, string gitAgent, CancellationToken ct) =>
        SendAsync(HttpMethod.Get, $"{O}/{Uri.EscapeDataString(Sandbox.Project)}/_git/{Uri.EscapeDataString(Sandbox.Repository)}/info/refs?service=git-upload-pack", null, credential, ct, suppress, gitAgent: gitAgent);

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

    /// <summary>One <c>POST …/refs</c> making <paramref name="name"/> (a branch, without <c>refs/heads/</c>) at <paramref name="objectId"/>, as answered: its outcome is <see cref="RefUpdated"/>, not the status.</summary>
    public Task<LiveServiceAnswer> MakeBranchAsync(string name, string objectId, CancellationToken ct) =>
        SendAsync(
            HttpMethod.Post,
            $"{RepositoryPath}/refs?api-version={AzureDevOpsApi.Version}",
            new JsonArray(new JsonObject { ["name"] = LiveLeftovers.HeadsPrefix + name, ["oldObjectId"] = ZeroId, ["newObjectId"] = objectId }),
            LiveCredential.Token,
            ct);

    /// <summary>
    /// Whether a refs POST's first update took: Azure DevOps answers 200 even when an update failed,
    /// so the outcome is <c>value[0].success</c> (and <c>updateStatus</c> <c>succeeded</c> when given).
    /// </summary>
    public static bool RefUpdated(LiveServiceAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return answer.Status == 200 && UpdatesOf(answer) is [var first, ..] && Took(first);
    }

    /// <summary>
    /// The refs of a 200 refs POST that sent <paramref name="names"/> whose update did not take, one
    /// line each naming the ref (a <c>visualcommit-live/</c> name of the run's) and its
    /// <c>updateStatus</c>: Azure DevOps answers 200 even when an update failed, so each item's
    /// <c>success</c> must be true and its <c>updateStatus</c> <c>succeeded</c> when given. An item is
    /// matched by its <c>name</c>, else by its position; an answer with no <c>value</c> list or another
    /// count of items than refs sent fails too. Empty when every update took.
    /// </summary>
    public static IReadOnlyList<string> RefFailures(LiveServiceAnswer answer, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(names);
        if (UpdatesOf(answer) is not { } items)
        {
            return [$"the refs POST answered {answer.Status.ToString(CultureInfo.InvariantCulture)} with no value list, so no update of {string.Join(", ", names)} is shown to have taken"];
        }

        var failures = new List<string>();
        if (items.Count != names.Count)
        {
            failures.Add($"the refs POST answered {Show(items.Count)} updates for {Show(names.Count)} refs");
        }

        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];
            JsonElement? item = items.FindIndex(update => NameOf(update) == name) is var named and >= 0
                ? items[named]
                : i < items.Count && (NameOf(items[i]) is not { } other || !names.Contains(other, StringComparer.Ordinal)) ? items[i] : null;
            if (item is not { } update)
            {
                failures.Add($"{name}: no update in the answer");
            }
            else if (!Took(update))
            {
                failures.Add($"{name}: success {Text(update, "success")}, updateStatus {Text(update, "updateStatus")}");
            }
        }

        return failures;
    }

    /// <summary>A refs POST's updates for a note, by position and naming no ref (15.7): each one's <c>success</c> and <c>updateStatus</c>, and the first one's member names.</summary>
    public static string RefItems(LiveServiceAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (UpdatesOf(answer) is not { } items)
        {
            return "no value list";
        }

        var each = items.Select((update, i) => $"{Show(i + 1)} success {Text(update, "success")}, updateStatus {Text(update, "updateStatus")}");
        var members = items is [{ ValueKind: JsonValueKind.Object } first, ..] ? $"; the first's members {string.Join(", ", first.EnumerateObject().Select(member => member.Name))}" : string.Empty;
        return $"{Show(items.Count)} updates: {(items.Count == 0 ? "none" : string.Join("; ", each))}{members}";
    }

    private static List<JsonElement>? UpdatesOf(LiveServiceAnswer answer) =>
        answer.Json is { ValueKind: JsonValueKind.Object } json && json.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray()]
            : null;

    private static string? NameOf(JsonElement update) =>
        update.ValueKind == JsonValueKind.Object && update.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() : null;

    private static bool Took(JsonElement update) =>
        update.ValueKind == JsonValueKind.Object
        && update.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True
        && (!update.TryGetProperty("updateStatus", out var status) || string.Equals(status.GetString(), "succeeded", StringComparison.OrdinalIgnoreCase));

    private static string Text(JsonElement update, string name) =>
        update.ValueKind == JsonValueKind.Object && update.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? "null" : value.GetRawText()
            : "not given";

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

    protected override void AddHeaders(HttpRequestMessage request, bool suppress, bool asGit)
    {
        if (suppress)
        {
            request.Headers.TryAddWithoutValidation("X-TFS-FedAuthRedirect", "Suppress");
        }

        if (asGit)
        {
            request.Headers.TryAddWithoutValidation("Accept", "*/*");
            request.Headers.TryAddWithoutValidation("Pragma", "no-cache");
            request.Headers.TryAddWithoutValidation("Git-Protocol", "version=2");
        }
        else
        {
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
        }
    }

    private static string Show(int value) => value.ToString(CultureInfo.InvariantCulture);
}
