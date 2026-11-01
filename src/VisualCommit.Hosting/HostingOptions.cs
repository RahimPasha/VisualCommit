using System.Net;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting.AzureDevOps;
using VisualCommit.Hosting.GitLab;

namespace VisualCommit.Hosting;

/// <summary>
/// Where GitHub is (docs/phase-7a-design.md 1.4): its web address, which also serves git and the
/// avatars, and its API's address. The app recognises a URL as GitHub's by the exact scheme, host
/// and port of <paramref name="WebBase"/> (ruling 23).
/// </summary>
/// <param name="WebBase">The web address, ending in <c>/</c>.</param>
/// <param name="ApiBase">The REST API's address, ending in <c>/</c>.</param>
public sealed record GitHubEndpoints(Uri WebBase, Uri ApiBase)
{
    /// <summary>github.com: <c>https://github.com/</c> and <c>https://api.github.com/</c>.</summary>
    public static GitHubEndpoints GitHubCom { get; } = new(new Uri("https://github.com/"), new Uri("https://api.github.com/"));

    /// <summary>The test harness's fake on <paramref name="port"/>: <c>http://127.0.0.1:&lt;port&gt;/</c> and its <c>api/v3/</c>.</summary>
    public static GitHubEndpoints Local(int port)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        var web = new Uri(FormattableString.Invariant($"http://127.0.0.1:{port}/"));
        return new GitHubEndpoints(web, new Uri(web, "api/v3/"));
    }

    /// <summary>The web address's host, with its port when it is not the scheme's: <c>github.com</c> or <c>127.0.0.1:&lt;port&gt;</c>.</summary>
    public string Host => WebBase.Authority;
}

/// <summary>
/// How the session reaches the hosting services (docs/phase-7a-design.md 1.4, 1.8;
/// docs/phase-7b-design.md 1.4, 1.8): each provider's endpoints, or null when it is off (nothing of
/// it is recognised and no request is made), and the HTTP handler each provider's client is built
/// on. 7a's positional record keeps compiling, with Azure DevOps and GitLab off.
/// </summary>
/// <param name="GitHub">GitHub's endpoints; null: off.</param>
/// <param name="CreateHandler">Makes the handler of GitHub's client.</param>
public sealed record HostingOptions(GitHubEndpoints? GitHub, Func<HttpMessageHandler> CreateHandler)
{
    /// <summary>
    /// The environment variable that says where GitHub is: unset, github.com; <c>none</c>, off;
    /// <c>http://127.0.0.1:&lt;port&gt;/</c>, the test harness's fake (1.8).
    /// </summary>
    public const string GitHubVariable = "VISUALCOMMIT_GITHUB_URL";

    /// <summary>
    /// Where Azure DevOps is: unset, <c>dev.azure.com</c> and <c>&lt;organization&gt;.visualstudio.com</c>;
    /// <c>none</c>, off; <c>http://127.0.0.1:&lt;port&gt;/</c>, the fake (docs/phase-7b-design.md 1.8).
    /// </summary>
    public const string AzureDevOpsVariable = "VISUALCOMMIT_AZURE_DEVOPS_URL";

    /// <summary>Where GitLab is: unset, gitlab.com; <c>none</c>, off; <c>http://127.0.0.1:&lt;port&gt;/</c>, the fake (1.8).</summary>
    public const string GitLabVariable = "VISUALCOMMIT_GITLAB_URL";

    /// <summary>The three providers off: nothing is recognised and no request is made (the harnesses' default). Its handler refuses every request.</summary>
    public static HostingOptions Off { get; } = new(null, static () => new RefusingHandler());

    /// <summary>Azure DevOps' endpoints; null: off.</summary>
    public AzureDevOpsEndpoints? AzureDevOps { get; init; }

    /// <summary>Makes the handler of Azure DevOps' client; null: every request refused, as <see cref="Off"/>'s handler does. Never a real handler by default.</summary>
    public Func<HttpMessageHandler>? CreateAzureDevOpsHandler { get; init; }

    /// <summary>GitLab's endpoints; null: off.</summary>
    public GitLabEndpoints? GitLab { get; init; }

    /// <summary>Makes the handler of GitLab's client; null: every request refused, as <see cref="Off"/>'s handler does. Never a real handler by default.</summary>
    public Func<HttpMessageHandler>? CreateGitLabHandler { get; init; }

    /// <summary>Every provider is off.</summary>
    public bool AllOff => GitHub is null && AzureDevOps is null && GitLab is null;

    /// <summary>
    /// The options <paramref name="readEnvironment"/>'s three variables ask for, each read as 7a's
    /// <see cref="GitHubVariable"/>: unset or empty, the service; <c>none</c>, off;
    /// <c>http://127.0.0.1:&lt;port&gt;/</c>, the provider's <c>Local(port)</c>; anything else, off
    /// and a warning that names the variable but not its value. Each provider it turns on gets its
    /// handler set explicitly (<see cref="DefaultHandler(Uri)"/> of Azure DevOps' web base, of
    /// GitLab's API base). All three off is <see cref="Off"/> itself.
    /// </summary>
    public static HostingOptions FromEnvironment(Func<string, string?> readEnvironment, IAppLog log)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentNullException.ThrowIfNull(log);
        var gitHub = Read(readEnvironment, log, GitHubVariable, "GitHub", GitHubEndpoints.GitHubCom, GitHubEndpoints.Local);
        var azureDevOps = Read(readEnvironment, log, AzureDevOpsVariable, "Azure DevOps", AzureDevOpsEndpoints.Service, AzureDevOpsEndpoints.Local);
        var gitLab = Read(readEnvironment, log, GitLabVariable, "GitLab", GitLabEndpoints.GitLabCom, GitLabEndpoints.Local);
        if (gitHub is null && azureDevOps is null && gitLab is null)
        {
            return Off;
        }

        var options = gitHub is null ? Off : new HostingOptions(gitHub, DefaultHandler(gitHub));
        return options with
        {
            AzureDevOps = azureDevOps,
            CreateAzureDevOpsHandler = azureDevOps is null ? null : DefaultHandler(azureDevOps.WebBase),
            GitLab = gitLab,
            CreateGitLabHandler = gitLab is null ? null : DefaultHandler(gitLab.ApiBase),
        };
    }

    /// <summary>
    /// The handler of a client of <paramref name="endpoints"/> (6.2): no redirect followed, every
    /// compression taken, no cookies, 10 s to connect, and no proxy for a loopback API (the
    /// environment's proxy does not leave loopback out by itself), the system's otherwise.
    /// </summary>
    public static Func<HttpMessageHandler> DefaultHandler(GitHubEndpoints endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return DefaultHandler(endpoints.ApiBase);
    }

    /// <summary>
    /// The handler of a client of the API at <paramref name="apiBase"/>, by 7a's rules (6.2): no
    /// redirect followed, every compression taken, no cookies, 10 s to connect, and no proxy for a
    /// loopback address, the system's otherwise.
    /// </summary>
    public static Func<HttpMessageHandler> DefaultHandler(Uri apiBase)
    {
        ArgumentNullException.ThrowIfNull(apiBase);
        var loopback = apiBase.IsLoopback;
        return () => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            UseProxy = !loopback,
        };
    }

    /// <summary>One provider's variable: its service, its <c>Local(port)</c>, or null (off), with a warning for a value that is neither.</summary>
    private static T? Read<T>(Func<string, string?> readEnvironment, IAppLog log, string variable, string name, T service, Func<int, T> local)
        where T : class
    {
        var value = readEnvironment(variable)?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return service;
        }

        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (LocalPort(value) is { } port)
        {
            return local(port);
        }

        // The value is left out: whatever it holds (a user part, a token typed by mistake) never
        // reaches the log (docs/phase-7a-design.md 1).
        log.Warning($"{variable} is neither none nor http://127.0.0.1:<port>/: {name} is off.");
        return null;
    }

    /// <summary>The port of <c>http://127.0.0.1:&lt;port&gt;/</c> (the trailing <c>/</c> optional), or null for any other value.</summary>
    private static int? LocalPort(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttp
        && uri.Host == "127.0.0.1"
        && !uri.IsDefaultPort
        && uri.UserInfo.Length == 0
        && uri.AbsolutePath == "/"
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0
            ? uri.Port
            : null;

    /// <summary>The handler of <see cref="Off"/>: every request fails, so that no request leaves the app while GitHub is off.</summary>
    private sealed class RefusingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new InvalidOperationException("GitHub is off: the app makes no request."));
    }
}
