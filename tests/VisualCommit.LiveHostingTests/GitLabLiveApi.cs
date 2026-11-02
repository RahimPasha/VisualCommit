using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using VisualCommit.Hosting.GitLab;

namespace VisualCommit.LiveHostingTests;

/// <summary>
/// A merge request as GitLab lists and reads it: its iid, its source branch and whether its source
/// may be another project. It fails closed: a request is the sandbox's own only when
/// <c>source_project_id</c> and <c>target_project_id</c> are both numbers and equal, so one whose
/// source is null, missing or not a number counts as a fork's, which the clean-up never touches.
/// </summary>
internal sealed record LiveMergeRequest(int Iid, string SourceBranch, bool FromFork)
{
    internal static LiveMergeRequest From(JsonElement request) => new(
        request.GetProperty("iid").GetInt32(),
        request.TryGetProperty("source_branch", out var source) ? source.GetString() ?? string.Empty : string.Empty,
        !(request.TryGetProperty("source_project_id", out var from) && request.TryGetProperty("target_project_id", out var to)
            && from.ValueKind == JsonValueKind.Number && to.ValueKind == JsonValueKind.Number
            && from.TryGetInt64(out var fromId) && to.TryGetInt64(out var toId) && fromId == toId));
}

/// <summary>
/// The live command's own calls to GitLab (docs/phase-7b-design.md 15.4, 15.5, 15.7), as the app's
/// client sends them (<c>Authorization: Bearer</c>): the user, the token's own record, the sandbox
/// project, its opened merge requests and <c>visualcommit-live/</c> branches, the close, the delete,
/// the create and the probes. Its public constructor sends through <see cref="LiveRequestLog"/> and
/// <see cref="GitLabReach"/> to <c>https://gitlab.com/api/v4/</c> over a handler that follows no
/// redirect; the internal one goes over a given handler (the fake beside, a stub).
/// </summary>
internal sealed class GitLabLiveApi : LiveServiceApi
{
    public GitLabLiveApi(LiveGitLabSandbox sandbox, GitLabReach reach, LiveRequestLog.Store store, LiveScreen screen, Action<string>? withheld = null)
        : base(new LiveRequestLog(NewHandler(), store, reach), GitLabEndpoints.GitLabCom.ApiBase, CallLimit, sandbox.Token, screen, withheld)
    {
        Sandbox = sandbox;
    }

    internal GitLabLiveApi(LiveGitLabSandbox sandbox, HttpMessageHandler handler, Uri apiBase, TimeSpan limit, LiveScreen screen, Action<string>? withheld = null)
        : base(handler, apiBase, limit, sandbox.Token, screen, withheld)
    {
        Sandbox = sandbox;
    }

    public override string Name => "GitLab";

    public LiveGitLabSandbox Sandbox { get; }

    private string ProjectPath => "projects/" + Uri.EscapeDataString(Sandbox.FullName);

    public Task<LiveServiceAnswer> UserAsync(LiveCredential credential, CancellationToken ct, string? ifNoneMatch = null) =>
        SendAsync(HttpMethod.Get, "user", null, credential, ct, ifNoneMatch: ifNoneMatch);

    /// <summary><c>personal_access_tokens/self</c>: 403 <c>insufficient_granular_scope</c> expected for D364's token (r6 2.2).</summary>
    public Task<LiveServiceAnswer> TokenSelfAsync(CancellationToken ct) =>
        SendAsync(HttpMethod.Get, "personal_access_tokens/self", null, LiveCredential.Token, ct);

    public Task<LiveServiceAnswer> ProjectAsync(CancellationToken ct) =>
        SendAsync(HttpMethod.Get, ProjectPath, null, LiveCredential.Token, ct);

    /// <summary>The user's projects with <c>permissions</c>, one row (A2's list, 4.7).</summary>
    public Task<LiveServiceAnswer> ProjectsAsync(CancellationToken ct) =>
        SendAsync(HttpMethod.Get, "projects?membership=true&simple=false&per_page=1&order_by=last_activity_at", null, LiveCredential.Token, ct);

    /// <summary>The sandbox's opened merge requests, up to 100.</summary>
    /// <exception cref="LiveServiceException">Not 200 with a list.</exception>
    public async Task<IReadOnlyList<LiveMergeRequest>> OpenedMergeRequestsAsync(CancellationToken ct)
    {
        var answer = await ExpectAsync(HttpMethod.Get, $"{ProjectPath}/merge_requests?state=opened&per_page=100", null, 200, "the opened merge requests", ct);
        return [.. ListOf(answer).Select(LiveMergeRequest.From)];
    }

    public Task<LiveServiceAnswer> MergeRequestsAnswerAsync(CancellationToken ct) =>
        SendAsync(HttpMethod.Get, $"{ProjectPath}/merge_requests?state=opened&per_page=100", null, LiveCredential.Token, ct);

    public Task<LiveServiceAnswer> MergeRequestAsync(int iid, CancellationToken ct) =>
        SendAsync(HttpMethod.Get, $"{ProjectPath}/merge_requests/{Show(iid)}", null, LiveCredential.Token, ct);

    public Task<LiveServiceAnswer> CloseAsync(int iid, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, $"{ProjectPath}/merge_requests/{Show(iid)}", new JsonObject { ["state_event"] = "close" }, LiveCredential.Token, ct);

    public Task<LiveServiceAnswer> DeleteMergeRequestAsync(int iid, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, $"{ProjectPath}/merge_requests/{Show(iid)}", null, LiveCredential.Token, ct);

    /// <summary>The branches whose names start with <paramref name="prefix"/> (<c>search=^&lt;prefix&gt;</c>, g2 G1.7).</summary>
    /// <exception cref="LiveServiceException">Not 200 with a list.</exception>
    public async Task<IReadOnlyList<string>> BranchesAsync(string prefix, CancellationToken ct)
    {
        var answer = await ExpectAsync(HttpMethod.Get, $"{ProjectPath}/repository/branches?search={Uri.EscapeDataString("^" + prefix)}&per_page=100", null, 200, "the branches under " + prefix, ct);
        return [.. ListOf(answer).Select(branch => branch.GetProperty("name").GetString() ?? string.Empty).Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length)];
    }

    public Task<LiveServiceAnswer> DeleteBranchAsync(string branch, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, $"{ProjectPath}/repository/branches/{Uri.EscapeDataString(branch)}", null, LiveCredential.Token, ct);

    /// <summary>A create from <paramref name="source"/> into <paramref name="target"/>, as answered; a draft by its <c>Draft: </c> title (7.4).</summary>
    public Task<LiveServiceAnswer> CreateAsync(string source, string target, string title, CancellationToken ct) =>
        SendAsync(
            HttpMethod.Post,
            $"{ProjectPath}/merge_requests",
            new JsonObject { ["source_branch"] = source, ["target_branch"] = target, ["title"] = title, ["description"] = LiveSteps.Description },
            LiveCredential.Token,
            ct);

    protected override void Authorize(HttpRequestMessage request, string value) =>
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + value);

    private static string Show(int value) => value.ToString(CultureInfo.InvariantCulture);
}
