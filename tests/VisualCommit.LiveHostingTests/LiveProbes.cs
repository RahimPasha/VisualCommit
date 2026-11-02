using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using VisualCommit.Core.Hosting;
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

/// <summary>A credential the test's own call sends (docs/phase-7b-design.md 15.7): none, the token, or a made-up value (D373).</summary>
internal sealed record LiveCredential(string? Value, bool IsToken, string Name)
{
    public static LiveCredential None { get; } = new(null, false, "no credential");

    public static LiveCredential Token { get; } = new(null, true, "the token");

    /// <summary>A made-up value, named in notes by <paramref name="name"/> (its kind and length), never as written.</summary>
    public static LiveCredential MadeUp(string value, string name) => new(value, false, name);

    public override string ToString() => Name;
}

/// <summary>
/// One answer of Azure DevOps or GitLab to the test's own call (15.7): the status, the request id
/// (<c>ActivityId</c> or <c>x-request-id</c>), the headers by name, the body without a BOM and
/// whether it had one, and the JSON when it is JSON. <see cref="ToString"/> shows the status and the
/// request id only.
/// </summary>
internal sealed record LiveServiceAnswer(int Status, string? RequestId, IReadOnlyDictionary<string, string> Headers, string Text, bool Bom, JsonElement? Json)
{
    private static readonly Regex Guid = new("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant);

    private static readonly Regex Date = new(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?", RegexOptions.CultureInvariant);

    private static readonly Regex Origin = new(@"https?://[A-Za-z0-9.-]+(?::\d+)?/", RegexOptions.CultureInvariant);

    private static readonly Regex TfCode = new(@"\bTF\d{5,6}\b|\bVS\d{6}\b", RegexOptions.CultureInvariant);

    public override string ToString() => $"{Status.ToString(CultureInfo.InvariantCulture)} (request {RequestId ?? "none"})";

    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;

    /// <summary>The media type of <c>Content-Type</c>, lower case, or null.</summary>
    public string? MediaType => Header("Content-Type")?.Split(';')[0].Trim().ToLowerInvariant();

    /// <summary>Azure DevOps' <c>api-version</c> parameter of <c>Content-Type</c>, or null.</summary>
    public string? ApiVersion => Header("Content-Type")?.Split(';').Select(part => part.Trim()).FirstOrDefault(part => part.StartsWith("api-version=", StringComparison.OrdinalIgnoreCase))?["api-version=".Length..];

    public string? Location => Header("Location");

    /// <summary><c>typeKey</c>, Azure DevOps' exception's name, or null.</summary>
    public string? TypeKey => Member("typeKey");

    /// <summary>
    /// <c>message</c> as text: a string as it is, an object or array as JSON (GitLab's shapes B and C,
    /// r6 10); without one, <c>error_description</c>, shape F's text, as r6 10's client rule reads it.
    /// </summary>
    public string? Message => Json is { ValueKind: JsonValueKind.Object } json && json.TryGetProperty("message", out var message)
        ? message.ValueKind == JsonValueKind.String ? message.GetString() : message.GetRawText()
        : Member("error_description");

    /// <summary>The TF (or VS) code that starts a message, or null.</summary>
    public string? Code => Message is { } text && TfCode.Match(text) is { Success: true } match ? match.Value : null;

    /// <summary>The body's shape, naming nothing in it: <c>(empty)</c>, an array of its length, an object with its top-level names, or no JSON and its length.</summary>
    public string Shape => Text.Length == 0
        ? "(empty)"
        : Json switch
        {
            { ValueKind: JsonValueKind.Array } array => $"an array of {array.GetArrayLength().ToString(CultureInfo.InvariantCulture)}",
            { ValueKind: JsonValueKind.Object } json => "an object with " + string.Join(", ", json.EnumerateObject().Select(property => property.Name)),
            _ => $"no JSON, {Text.Length.ToString(CultureInfo.InvariantCulture)} characters",
        };

    /// <summary>The body for a note (15.7): a non-2xx's as sent (the note screens it), a 2xx's by its shape.</summary>
    public string BodyForNotes => Text.Length == 0
        ? "The body: (empty)."
        : Status is >= 200 and < 300 ? $"The body, a 2xx's, by its shape: {Shape}." : "The body as sent:\n" + Text;

    /// <summary>The headers a probe's note shows: <c>Content-Type</c>, <c>WWW-Authenticate</c>, <c>Location</c>'s presence and the BOM.</summary>
    public string HeadersForNotes =>
        $"Content-Type {Header("Content-Type") ?? "not sent"}, WWW-Authenticate {Header("WWW-Authenticate") ?? "not sent"}, Location {(Location is null ? "not sent" : "sent")}, BOM {(Bom ? "yes" : "no")}";

    /// <summary>
    /// The fake's answer beside the service's (15.7, g4 D1.7.6): each side's names, ids, dates and
    /// origin replaced first, then the status, the media type, <c>api-version</c>, the BOM,
    /// <c>typeKey</c>, the TF code, <c>message</c>, GitLab's <c>error</c> and <c>scope</c> (a set),
    /// <c>WWW-Authenticate</c> and a 2xx's top-level names compared: <c>same</c>, or the fake's value of
    /// each that differs; without the service's answer, the fake's status and message.
    /// </summary>
    public static string Beside(LiveServiceAnswer? service, LiveServiceAnswer fake, IReadOnlyList<string> serviceNames, IReadOnlyList<string> fakeNames)
    {
        ArgumentNullException.ThrowIfNull(fake);
        if (service is null)
        {
            return $"status {fake.Status.ToString(CultureInfo.InvariantCulture)}, message {Normal(fake.Message, fakeNames) ?? "none"}";
        }

        var differences = new List<string>();
        void Compare(string what, string? left, string? right)
        {
            if (!string.Equals(left, right, StringComparison.Ordinal))
            {
                differences.Add($"{what} {right ?? "none"}");
            }
        }

        Compare("status", service.Status.ToString(CultureInfo.InvariantCulture), fake.Status.ToString(CultureInfo.InvariantCulture));
        Compare("media type", service.MediaType, fake.MediaType);
        Compare("api-version", service.ApiVersion, fake.ApiVersion);
        Compare("BOM", service.Bom ? "yes" : "no", fake.Bom ? "yes" : "no");
        Compare("typeKey", service.TypeKey, fake.TypeKey);
        Compare("code", service.Code, fake.Code);
        Compare("message", Normal(service.Message, serviceNames), Normal(fake.Message, fakeNames));
        Compare("error", service.Member("error"), fake.Member("error"));
        Compare("scope", ScopeSet(service.Member("scope")), ScopeSet(fake.Member("scope")));
        Compare("WWW-Authenticate", Normal(service.Header("WWW-Authenticate"), serviceNames), Normal(fake.Header("WWW-Authenticate"), fakeNames));
        if (service.Status is >= 200 and < 300 && service.Json is { ValueKind: JsonValueKind.Object } && fake.Json is { ValueKind: JsonValueKind.Object })
        {
            Compare("top-level names", service.Shape, fake.Shape);
        }

        return differences.Count == 0 ? "same" : string.Join(", ", differences);
    }

    /// <summary>A member of the body's object as text (a string as it is), or null.</summary>
    public string? Member(string name) => Json is { ValueKind: JsonValueKind.Object } json && json.TryGetProperty(name, out var value)
        ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText()
        : null;

    private static string? ScopeSet(string? scope) => scope is null ? null : string.Join(' ', scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));

    /// <summary><paramref name="text"/> with <paramref name="names"/> (longest first, case ignored), GUIDs, dates and origins replaced.</summary>
    internal static string? Normal(string? text, IReadOnlyList<string> names)
    {
        if (text is null)
        {
            return null;
        }

        foreach (var name in names.Where(name => name.Length > 0).OrderByDescending(name => name.Length))
        {
            text = text.Replace(name, "<name>", StringComparison.OrdinalIgnoreCase);
        }

        return Origin.Replace(Date.Replace(Guid.Replace(text, "<id>"), "<date>"), "<origin>/");
    }
}

/// <summary>A call of the test's own that the service did not answer as the step needs, or did not answer: never a request or an answer.</summary>
internal sealed class LiveServiceException(string message) : Exception(message);

/// <summary>
/// The live command's own calls to a new provider (15.4, 15.7): its own <see cref="HttpClient"/>
/// over a handler that follows no redirect (built as <c>GitHubLiveApi</c>'s public constructor builds
/// its one), through <see cref="LiveRequestLog"/> and the provider's <see cref="LiveReach"/> for the
/// service, through the fake's in-process handler for the fake beside. Each answer is learned by the
/// screen (<see cref="LiveScreen.LearnFromAnswer"/>) before anything can note it; a message holding
/// the token is withheld and told to the run.
/// </summary>
internal abstract class LiveServiceApi : IDisposable
{
    /// <summary>How long one call to the service may take.</summary>
    public static readonly TimeSpan CallLimit = TimeSpan.FromSeconds(30);

    /// <summary>How long one call to the fake in process may take (15.7).</summary>
    public static readonly TimeSpan FakeCallLimit = TimeSpan.FromSeconds(5);

    private readonly HttpClient _client;
    private readonly string _token;
    private readonly Action<string>? _withheld;

    protected LiveServiceApi(HttpMessageHandler handler, Uri baseAddress, TimeSpan limit, string token, LiveScreen screen, Action<string>? withheld)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(baseAddress);
        ArgumentException.ThrowIfNullOrEmpty(token);
        ArgumentNullException.ThrowIfNull(screen);
        Handler = handler;
        _client = new HttpClient(handler, disposeHandler: true) { BaseAddress = baseAddress, Timeout = Timeout.InfiniteTimeSpan };
        _token = token;
        ScreenOfAnswers = screen;
        _withheld = withheld;
        Limit = limit;
    }

    /// <summary>The provider's name in messages.</summary>
    public abstract string Name { get; }

    /// <summary>How long one call may take.</summary>
    public TimeSpan Limit { get; set; }

    /// <summary>The screen every answer is learned by.</summary>
    public LiveScreen ScreenOfAnswers { get; }

    /// <summary>The handler the calls go through: for a public constructor's, <see cref="LiveRequestLog"/> over <see cref="NewHandler"/>'s (15.9).</summary>
    internal HttpMessageHandler Handler { get; }

    /// <summary>The handler the public constructors build (15.4): no redirect followed, any compression, no cookies, 10 s to connect.</summary>
    public static SocketsHttpHandler NewHandler() => new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
    };

    /// <summary>"{Name} answered {status} {message} for {what} (request {id})", screened.</summary>
    public string Describe(LiveServiceAnswer answer, string what)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return Screen($"{Name} answered {answer.Status.ToString(CultureInfo.InvariantCulture)} {answer.Message ?? "(no message)"} for {what} (request {answer.RequestId ?? "none"})");
    }

    /// <summary><paramref name="text"/> screened, or a sentence without it when it holds the token (told to the run).</summary>
    public string Screen(string text)
    {
        if (LiveTokenForms.FoundIn(text, _token) is { } form)
        {
            _withheld?.Invoke($"{Name}'s answer held the token ({form}); it was withheld.");
            return $"{Name}'s answer was withheld: it held the token";
        }

        return ScreenOfAnswers.Apply(text);
    }

    public void Dispose() => _client.Dispose();

    /// <summary>Adds the provider's credential header for <paramref name="value"/>.</summary>
    protected abstract void Authorize(HttpRequestMessage request, string value);

    /// <summary>Adds the provider's other headers; <paramref name="asGit"/> for a read sent as git sends it (no API's <c>Accept</c>).</summary>
    protected virtual void AddHeaders(HttpRequestMessage request, bool suppress, bool asGit)
    {
    }

    /// <summary>One call, answered whatever the status: learned by the screen before it is returned.</summary>
    /// <exception cref="LiveServiceException">No answer within <see cref="Limit"/>, the guard refused it, or the service was not reached.</exception>
    /// <param name="gitAgent">git's own <c>User-Agent</c> (<c>git/&lt;version&gt;</c>) for a read sent as git sends it (g1 G0.7), or null for the test's own agent and the API's headers.</param>
    internal async Task<LiveServiceAnswer> SendAsync(HttpMethod method, string path, JsonNode? body, LiveCredential credential, CancellationToken cancellationToken, bool suppress = true, string? ifNoneMatch = null, string? gitAgent = null)
    {
        ArgumentNullException.ThrowIfNull(credential);
        using var request = new HttpRequestMessage(method, path);
        if (credential.IsToken)
        {
            Authorize(request, _token);
        }
        else if (credential.Value is { } value)
        {
            Authorize(request, value);
        }

        request.Headers.TryAddWithoutValidation("User-Agent", gitAgent ?? GitHubLiveApi.UserAgent);
        AddHeaders(request, suppress, gitAgent is not null);
        if (ifNoneMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        }

        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        var shown = $"{method.Method} /{path.Split('?')[0]}";
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Limit);
        try
        {
            using var response = await _client.SendAsync(request, limit.Token);
            var bytes = await response.Content.ReadAsByteArrayAsync(limit.Token);
            var bom = bytes is [0xEF, 0xBB, 0xBF, ..];
            var text = Encoding.UTF8.GetString(bom ? bytes[3..] : bytes);
            var headers = response.Headers.Concat(response.Content.Headers).ToList();
            ScreenOfAnswers.LearnFromAnswer(headers, text);
            JsonElement? json = null;
            try
            {
                using var document = JsonDocument.Parse(text);
                json = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                // Not JSON: an HTML page, a git answer or no body; the status and the shape say enough.
            }

            var named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, values) in headers)
            {
                named[name] = string.Join(", ", values);
            }

            return new LiveServiceAnswer((int)response.StatusCode, named.GetValueOrDefault("ActivityId") ?? named.GetValueOrDefault("x-request-id"), named, text, bom, json);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LiveServiceException($"{Name} did not answer {shown} within {Limit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s.");
        }
        catch (HttpRequestException ex)
        {
            throw new LiveServiceException(ex.Message.StartsWith("The live command refused", StringComparison.Ordinal)
                ? ex.Message
                : $"{shown} did not reach {Name}: {ex.HttpRequestError}.");
        }
    }

    /// <summary>One call that must answer <paramref name="status"/>, else a <see cref="LiveServiceException"/> with <see cref="Describe"/>'s sentence.</summary>
    protected async Task<LiveServiceAnswer> ExpectAsync(HttpMethod method, string path, JsonNode? body, int status, string what, CancellationToken cancellationToken)
    {
        var answer = await SendAsync(method, path, body, LiveCredential.Token, cancellationToken);
        return answer.Status == status ? answer : throw new LiveServiceException(Describe(answer, what) + ".");
    }

    /// <summary>The items of the body's array, or of its <paramref name="member"/> array.</summary>
    /// <exception cref="LiveServiceException">No such array.</exception>
    protected IEnumerable<JsonElement> ListOf(LiveServiceAnswer answer, string? member = null)
    {
        var list = member is null ? answer.Json : answer.Json is { ValueKind: JsonValueKind.Object } json && json.TryGetProperty(member, out var value) ? value : null;
        return list is { ValueKind: JsonValueKind.Array } array ? array.EnumerateArray() : throw new LiveServiceException($"{Name}'s answer {answer} is no list.");
    }
}

/// <summary>
/// The premises of the new providers' live command (15.7, D342): what the app relies on the services
/// to answer, each read from the answer's parts it names; a broken one fails the run after its note.
/// </summary>
internal static class LiveProviderPremises
{
    /// <summary>A GUID's text that is not the Anonymous identity's (g1 G0.3).</summary>
    public static bool IsMemberId(string? id) =>
        System.Guid.TryParse(id, out var guid) && !string.Equals(guid.ToString("D"), LiveScreen.AnonymousId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What ruling 16 refuses of a 2xx (6.4): a 203, an HTML body, or the Anonymous identity, so a
    /// made-up value's 2xx is a premise broken only when none of these holds.
    /// </summary>
    public static bool IsRefusedSuccess(LiveServiceAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return answer.Status == 203
            || answer.MediaType is "text/html"
            || answer.Text.Contains(LiveScreen.AnonymousId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A made-up value earns no 2xx that ruling 16 would take (D373).</summary>
    public static bool MadeUpRefused(LiveServiceAnswer answer) => answer.Status is < 200 or >= 300 || IsRefusedSuccess(answer);

    /// <summary>Azure DevOps' duplicate create: 409 <c>GitPullRequestExistsException</c>.</summary>
    public static bool IsAzureDuplicate(LiveServiceAnswer answer) => answer.Status == 409 && answer.TypeKey == "GitPullRequestExistsException";

    /// <summary>Azure DevOps' create from a missing branch: 400 <c>GitPullRequestCannotBeActivated</c> (its typeKey or message).</summary>
    public static bool IsAzureMissingBranch(LiveServiceAnswer answer) =>
        answer.Status == 400 && (answer.TypeKey?.Contains("GitPullRequestCannotBeActivated", StringComparison.Ordinal) == true || answer.Message?.Contains("GitPullRequestCannotBeActivated", StringComparison.Ordinal) == true);

    /// <summary>GitLab's duplicate create: 409.</summary>
    public static bool IsGitLabDuplicate(LiveServiceAnswer answer) => answer.Status == 409;

    /// <summary>GitLab's create from a missing branch: 400 naming <c>source_branch</c> (7.3).</summary>
    public static bool IsGitLabMissingBranch(LiveServiceAnswer answer) => answer.Status == 400 && answer.Text.Contains("source_branch", StringComparison.Ordinal);
}

/// <summary>
/// D373's made-up values (15.7), drawn at run time so that no token-shaped literal is committed: the
/// 84-character one letters and digits with <c>AZDO</c> at its characters 76 to 79, GitLab's
/// <c>glpat-</c> and 20 letters and digits, each drawn again while the token is found in it; the
/// short one a fixed plain string. A note names each by <see cref="LiveCredential.Name"/>.
/// </summary>
internal static class LiveMadeUp
{
    public const string Short = "visualcommit-not-a-token";

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static LiveCredential ShortValue { get; } = LiveCredential.MadeUp(Short, $"a made-up plain value of {Short.Length.ToString(CultureInfo.InvariantCulture)} characters");

    /// <summary>An 84-character value in Azure DevOps' form (<c>AZDO</c> at 76 to 79), sharing no 16 characters in a row with <paramref name="token"/>.</summary>
    public static string AzureShaped(string? token) => Draw(token, () => Letters(76) + "AZDO" + Letters(4));

    /// <summary>A <c>glpat-</c> value of 20 letters and digits after the prefix, sharing no 16 characters in a row with <paramref name="token"/>.</summary>
    public static string GitLabShaped(string? token) => Draw(token, () => "glpat-" + Letters(20));

    /// <summary>The first value <paramref name="draw"/> gives that shares no 16 characters in a row with <paramref name="token"/>, either way round; internal for its test.</summary>
    internal static string Draw(string? token, Func<string> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);
        while (true)
        {
            var value = draw();
            if (LiveTokenForms.FoundIn(value, token) is null && (token is null || LiveTokenForms.FoundIn(token, value) is null))
            {
                return value;
            }
        }
    }

    private static string Letters(int count) => string.Create(count, 0, (span, _) =>
    {
        for (var i = 0; i < span.Length; i++)
        {
            span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
    });
}

/// <summary>
/// The fake beside for Azure DevOps (15.7): <see cref="FakeAzureDevOps"/> in process with
/// <c>fake-org</c>, the sandbox's project and repository names, a user and a token of its own, and a
/// README on <c>main</c>; for Check_2b's probes also the run's branch, one commit ahead of <c>main</c>
/// as the sandbox's is, and the branch with nothing ahead, at <c>main</c>. Its calls go through its
/// in-process handler alone.
/// </summary>
internal sealed class LiveAzureFake : IAsyncDisposable
{
    public const string Organization = "fake-org";

    /// <summary>The default branch of both fakes beside.</summary>
    public const string DefaultBranch = "main";

    private readonly TempDirectory _folder;
    private readonly TempRepo _source;
    private readonly FakeAzureDevOps _fake;

    private LiveAzureFake(TempDirectory folder, TempRepo source, FakeAzureDevOps fake, AzureDevOpsLiveApi api, IReadOnlyDictionary<string, string> tips)
    {
        _folder = folder;
        _source = source;
        _fake = fake;
        Api = api;
        Tips = tips;
    }

    public AzureDevOpsLiveApi Api { get; }

    /// <summary>The tips of the fake's run's branch and branch with nothing ahead, by full ref name (<c>refs/heads/…</c>), for its side of the clean-up's refs delete (15.7).</summary>
    public IReadOnlyDictionary<string, string> Tips { get; }

    /// <summary>The names the fake's side replaces before a comparison.</summary>
    public IReadOnlyList<string> Names => [Organization, Api.Sandbox.Project, Api.Sandbox.Repository];

    /// <summary>The fake, with <paramref name="branch"/> one commit ahead of <c>main</c> and <paramref name="even"/> at <c>main</c> when given (Check_2b's probes).</summary>
    public static async Task<LiveAzureFake> StartAsync(string project, string repository, string? branch, string? even, CancellationToken ct)
    {
        var folder = new TempDirectory("live-azure-fake");
        TempRepo? source = null;
        FakeAzureDevOps? fake = null;
        try
        {
            source = await TempRepo.CreateAsync("live-azure-source");
            var tips = await BranchesAsync(source, branch, even);
            fake = FakeAzureDevOps.InProcess(folder.Combine("azure"));
            fake.AddOrganization(Organization);
            fake.AddProject(Organization, project);
            var user = fake.AddUser(Organization, "Ada Lovelace");
            var token = fake.AddToken(Organization, user);
            await fake.AddRepositoryAsync(Organization, project, repository, source.Path, ct: ct);
            var api = new AzureDevOpsLiveApi(new LiveAzureSandbox(Organization, project, repository, token), fake.CreateHandler(), fake.Endpoints.WebBase, LiveServiceApi.FakeCallLimit, new LiveScreen());
            return new LiveAzureFake(folder, source, fake, api, tips);
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
        await _fake.DisposeAsync();
        _source.Dispose();
        _folder.Dispose();
    }

    /// <summary>A README on <c>main</c>; <paramref name="branch"/> one commit ahead of it and <paramref name="even"/> at it, each when given (both fakes beside): the given branches' tips by full ref name.</summary>
    internal static async Task<IReadOnlyDictionary<string, string>> BranchesAsync(TempRepo source, string? branch, string? even)
    {
        var tips = new Dictionary<string, string>(StringComparer.Ordinal);
        var main = await source.CommitFileAsAsync("README.md", "The fake's namesake of the sandbox.\n", "Initial commit", "Sandbox author", "sandbox-author@example.com");
        if (even is not null)
        {
            await source.CreateBranchAsync(even);
            tips[LiveLeftovers.HeadsPrefix + even] = main;
        }

        if (branch is not null)
        {
            await source.CreateBranchAsync(branch);
            await source.CheckoutAsync(branch);
            tips[LiveLeftovers.HeadsPrefix + branch] = await source.CommitFileAsAsync("visualcommit-live/fake.txt", "The fake's side of the run's commit.\n", LiveSteps.IdentityName, LiveSteps.IdentityName, LiveScreen.KeptAddress);
            await source.CheckoutAsync(DefaultBranch);
        }

        return tips;
    }
}

/// <summary>The fake beside for GitLab (15.7): <see cref="FakeGitLab"/> in process with <c>ada/&lt;project&gt;</c>, a fine-grained token of its own, a README on <c>main</c>, and for Check_2b's probes the run's branch one commit ahead and the branch with nothing ahead, as <see cref="LiveAzureFake"/>'s.</summary>
internal sealed class LiveGitLabFake : IAsyncDisposable
{
    public const string Login = "ada";

    private readonly TempDirectory _folder;
    private readonly TempRepo _source;
    private readonly FakeGitLab _fake;

    private LiveGitLabFake(TempDirectory folder, TempRepo source, FakeGitLab fake, GitLabLiveApi api)
    {
        _folder = folder;
        _source = source;
        _fake = fake;
        Api = api;
    }

    public GitLabLiveApi Api { get; }

    public IReadOnlyList<string> Names => [Login, Api.Sandbox.Project];

    public static async Task<LiveGitLabFake> StartAsync(string project, string? branch, string? even, CancellationToken ct)
    {
        var folder = new TempDirectory("live-gitlab-fake");
        TempRepo? source = null;
        FakeGitLab? fake = null;
        try
        {
            source = await TempRepo.CreateAsync("live-gitlab-source");
            await LiveAzureFake.BranchesAsync(source, branch, even);
            fake = FakeGitLab.InProcess(folder.Combine("gitlab"));
            fake.AddUser(Login, 1001, "Ada Lovelace");
            var token = fake.AddToken(Login);
            await fake.AddProjectAsync($"{Login}/{project}", source.Path, ct: ct);
            var api = new GitLabLiveApi(new LiveGitLabSandbox(Login, project, token), fake.CreateHandler(), fake.Endpoints.ApiBase, LiveServiceApi.FakeCallLimit, new LiveScreen());
            return new LiveGitLabFake(folder, source, fake, api);
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
        await _fake.DisposeAsync();
        _source.Dispose();
        _folder.Dispose();
    }
}
