using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;

namespace VisualCommit.LiveHostingTests;

/// <summary>
/// What the app relies on GitHub to answer, as checks the live command fails on once its note is
/// kept (D342, after the live run of 2026-10-30): the duplicate create's refusal that the create
/// form reads, the avatars' lookups' arrays (D339), the statuses after which a lookup leaves out
/// only its own address, and the 400 for an API version GitHub does not have. Each reads only the
/// status and the body's parts it names, in every shape the app's client reads them (D342's
/// <c>errors</c>: one string, or an array of strings or objects), so that a premise fails only
/// where the app would.
/// </summary>
internal static class LivePremises
{
    /// <summary>
    /// How the duplicate create's detail starts (GitHub's first error's <c>message</c>, as the client
    /// reads it: <see cref="ValidationDetail"/>) for a create from a head that has an open pull
    /// request already: the create form's D316 path matches it ordinally
    /// (<c>RepositoryOperations.CreatePullRequest.cs</c>, its <c>Validation</c> case).
    /// </summary>
    public const string AlreadyExists = "A pull request already exists";

    /// <summary>The header an answer about the API version names; GitHub writes it <c>X-GitHub-API-Version</c>, so it is compared ignoring case, as the client does (6.2).</summary>
    public const string VersionHeader = "X-GitHub-Api-Version";

    /// <summary>The duplicate create's answer as the form needs it: 422 whose detail (<see cref="ValidationDetail"/>) starts with <see cref="AlreadyExists"/>.</summary>
    internal static bool IsDuplicateRefusal(LiveAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return answer.Status == 422 && ValidationDetail(answer)?.StartsWith(AlreadyExists, StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// A 400 whose first error's message (<see cref="FirstError"/>) or whose <c>message</c> names
    /// <see cref="VersionHeader"/>: the client's <c>ApiVersion</c>, which reads them in that order
    /// (6.2, D342; GitHub's explanation came as <c>errors</c>, one string, in the live run).
    /// </summary>
    internal static bool NamesVersionHeader(LiveAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return answer.Status == 400 && (NamesVersionHeader(FirstError(answer)?.Message) || NamesVersionHeader(answer.Message));
    }

    /// <summary>Whether <paramref name="text"/> names <see cref="VersionHeader"/>, case ignored.</summary>
    internal static bool NamesVersionHeader(string? text) => text?.Contains(VersionHeader, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// The first of the body's <c>errors</c> as the client reads it since D342 (its
    /// <c>GitHubErrorsConverter</c>): <c>errors</c> as one string, or an array's first string or
    /// object (items of other kinds skipped), with its <c>message</c>, <c>field</c> and <c>code</c>
    /// where each is a string; null without one.
    /// </summary>
    internal static (string? Message, string? Field, string? Code)? FirstError(LiveAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (answer.Json is not { ValueKind: JsonValueKind.Object } json || !json.TryGetProperty("errors", out var errors))
        {
            return null;
        }

        if (errors.ValueKind == JsonValueKind.String)
        {
            return (errors.GetString(), null, null);
        }

        if (errors.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in errors.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    return (item.GetString(), null, null);
                }

                if (item.ValueKind == JsonValueKind.Object)
                {
                    return (Text(item, "message"), Text(item, "field"), Text(item, "code"));
                }
            }
        }

        return null;
    }

    /// <summary>
    /// A 422's detail as the client reads it (its <c>ValidationDetail</c>, 6.3's A5): the first
    /// error's message when it has one; else <c>&lt;field&gt; is invalid</c>, <c>is missing</c> or
    /// <c>already exists</c> by its code; else GitHub's <c>message</c>.
    /// </summary>
    internal static string? ValidationDetail(LiveAnswer answer)
    {
        var first = FirstError(answer);
        if (first?.Message is { Length: > 0 } message)
        {
            return message;
        }

        return first is { Field: { Length: > 0 } field, Code: var code }
            ? code switch
            {
                "invalid" => field + " is invalid",
                "missing" or "missing_field" => field + " is missing",
                "already_exists" => field + " already exists",
                _ => answer.Message,
            }
            : answer.Message;
    }

    /// <summary>A 200 whose body is an array of <paramref name="count"/>: what the avatars' lookup answers for a commit by that address (1) and by an address with no commit (0), D339.</summary>
    internal static bool IsArrayOf(LiveAnswer answer, int count)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return answer is { Status: 200, Json: { ValueKind: JsonValueKind.Array } array } && array.GetArrayLength() == count;
    }

    /// <summary>
    /// A lookup's status after which the provider leaves out only its own address on the listed
    /// repository and goes on (D342, amending D339): 422, a 5xx (GitHub's 500 for an unknown SHA
    /// beside <c>author</c>) or 404.
    /// </summary>
    internal static bool LeavesOutOnlyItsAddress(LiveAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return answer.Status is 404 or 422 or (>= 500 and < 600);
    }

    /// <summary>
    /// A SHA no repository holds that is not all <c>f</c>s, so that GitHub cannot answer it as a
    /// special value: the SHA-1 of <c>visualcommit-live &lt;stamp&gt;</c>, in lower-case hex.
    /// </summary>
    internal static string UnknownSha(string stamp)
    {
        ArgumentException.ThrowIfNullOrEmpty(stamp);
        return Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes("visualcommit-live " + stamp)));
    }

    /// <summary><paramref name="item"/>'s member <paramref name="name"/> when it is a string, else null.</summary>
    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>
/// A handler between the app's provider and GitHub that asks for another API version on every
/// request (Check_3, D342): it replaces <c>X-GitHub-Api-Version</c> with its version, adding it when
/// missing, and touches nothing else, <c>Authorization</c> least of all.
/// </summary>
internal sealed class LiveVersionRewriter(string version, HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.Remove(LivePremises.VersionHeader);
        request.Headers.TryAddWithoutValidation(LivePremises.VersionHeader, version);
        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// The app's provider's log in Check_3, in memory: its lines are never written anywhere, only
/// searched for the token (<see cref="FoundIn"/>), the provider's own tripwire.
/// </summary>
internal sealed class LiveLog : IAppLog
{
    private readonly Lock _gate = new();
    private readonly List<string> _lines = [];

    /// <summary>How many lines the provider wrote.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _lines.Count;
            }
        }
    }

    public void Write(LogLevel level, string message, Exception? exception = null)
    {
        var line = $"{level} {message}" + (exception is null ? string.Empty : "\n" + exception);
        lock (_gate)
        {
            _lines.Add(line);
        }
    }

    /// <summary>The name of the first form of <paramref name="token"/> in any line, or null (<see cref="LiveTokenForms"/>).</summary>
    public string? FoundIn(string token)
    {
        lock (_gate)
        {
            return _lines.Select(line => LiveTokenForms.FoundIn(line, token)).FirstOrDefault(form => form is not null);
        }
    }
}

/// <summary>
/// The fake of GitHub in process (design 2.3), asked Check_1's probes beside GitHub so that each
/// note says where the fake answers otherwise, from what it answers at the time, not from words
/// copied into the test: a user named as the sandbox's owner with a fine-grained token of the fake's
/// own, a private repository named as the sandbox with one commit by <see cref="Email"/>, which is
/// linked to that user, and an empty one, <see cref="EmptyName"/>. It is given the sandbox's owner
/// and name alone, never its token, which <see cref="GitHubLiveApi"/> refuses for the fake's address
/// anyway (<see cref="GitHubLiveApi.SendsTheTokenElsewhere"/>); its calls (<see cref="Api"/>,
/// <see cref="EmptyApi"/>) go through its in-process handler alone, never to the network. Its start
/// (a few git calls) has no limit of its own beside the hang watchdog's (AssemblyInfo).
/// </summary>
internal sealed class LiveFake : IAsyncDisposable
{
    /// <summary>The author's address of the fake's one commit, linked to its user: the fake's side of the latest commit's author's.</summary>
    public const string Email = "sandbox-author@visualcommit.invalid";

    /// <summary>The fake's empty repository, for the commits of an empty one, which the sandbox cannot show.</summary>
    public const string EmptyName = "visualcommit-live-empty";

    private readonly TempDirectory _folder;
    private readonly TempRepo _source;
    private readonly FakeGitHub _fake;

    private LiveFake(TempDirectory folder, TempRepo source, FakeGitHub fake, string owner, string name, string token, string sha)
    {
        _folder = folder;
        _source = source;
        _fake = fake;
        Sha = sha;
        Api = new GitHubLiveApi(new LiveSandbox(owner, name, token), fake.CreateHandler(), apiBase: fake.Endpoints.ApiBase, limit: GitHubLiveApi.FakeCallLimit);
        EmptyApi = new GitHubLiveApi(new LiveSandbox(owner, EmptyName, token), fake.CreateHandler(), apiBase: fake.Endpoints.ApiBase, limit: GitHubLiveApi.FakeCallLimit);
    }

    /// <summary>The test's own calls, on the fake's namesake of the sandbox.</summary>
    public GitHubLiveApi Api { get; }

    /// <summary>The test's own calls, on the fake's empty repository.</summary>
    public GitHubLiveApi EmptyApi { get; }

    /// <summary>The fake's one commit: the fake's side of the latest commit.</summary>
    public string Sha { get; }

    /// <summary>The fake with the sandbox's <paramref name="owner"/> and <paramref name="name"/>: a few git calls, no server.</summary>
    public static async Task<LiveFake> StartAsync(string owner, string name, CancellationToken ct)
    {
        var folder = new TempDirectory("live-fake");
        TempRepo? source = null;
        FakeGitHub? fake = null;
        try
        {
            source = await TempRepo.CreateAsync("live-fake-source");
            var sha = await source.CommitFileAsAsync("README.md", "The fake's namesake of the sandbox.\n", "Initial commit", "Sandbox author", Email);
            fake = FakeGitHub.InProcess(folder.Combine("github"));
            fake.AddUser(owner, 1001);
            var token = fake.AddToken(owner);
            fake.LinkEmail(Email, owner);
            await fake.AddRepositoryAsync(owner, name, source.Path, isPrivate: true, ct: ct);
            await fake.AddRepositoryAsync(owner, EmptyName, isPrivate: true, ct: ct);
            return new LiveFake(folder, source, fake, owner, name, token, sha);
        }
        catch
        {
            if (fake is not null)
            {
                await fake.DisposeAsync();
            }

            source?.Dispose();
            folder.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Api.Dispose();
        EmptyApi.Dispose();
        await _fake.DisposeAsync();
        _source.Dispose();
        _folder.Dispose();
    }
}
