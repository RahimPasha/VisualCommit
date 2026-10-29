using System.Net;
using VisualCommit.Core.Logging;

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
/// How the session reaches the hosting services (docs/phase-7a-design.md 1.4, 1.8): GitHub's
/// endpoints, or null when GitHub is off (nothing is recognised and no request is made), and the
/// HTTP handler every client is built on.
/// </summary>
/// <param name="GitHub">GitHub's endpoints; null: off.</param>
/// <param name="CreateHandler">Makes the handler of a client.</param>
public sealed record HostingOptions(GitHubEndpoints? GitHub, Func<HttpMessageHandler> CreateHandler)
{
    /// <summary>
    /// The environment variable that says where GitHub is: unset, github.com; <c>none</c>, off;
    /// <c>http://127.0.0.1:&lt;port&gt;/</c>, the test harness's fake (1.8).
    /// </summary>
    public const string GitHubVariable = "VISUALCOMMIT_GITHUB_URL";

    /// <summary>GitHub off: nothing is recognised and no request is made (the harnesses' default). Its handler refuses every request.</summary>
    public static HostingOptions Off { get; } = new(null, static () => new RefusingHandler());

    /// <summary>
    /// The options <paramref name="readEnvironment"/>'s <see cref="GitHubVariable"/> asks for:
    /// unset or empty, github.com; <c>none</c>, <see cref="Off"/>; <c>http://127.0.0.1:&lt;port&gt;/</c>,
    /// <see cref="GitHubEndpoints.Local"/>; anything else, <see cref="Off"/> and a warning.
    /// </summary>
    public static HostingOptions FromEnvironment(Func<string, string?> readEnvironment, IAppLog log)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentNullException.ThrowIfNull(log);
        var value = readEnvironment(GitHubVariable)?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return For(GitHubEndpoints.GitHubCom);
        }

        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            return Off;
        }

        if (LocalPort(value) is { } port)
        {
            return For(GitHubEndpoints.Local(port));
        }

        log.Warning($"{GitHubVariable}={value} is neither none nor http://127.0.0.1:<port>/: GitHub is off.");
        return Off;
    }

    /// <summary>
    /// The handler of a client of <paramref name="endpoints"/> (6.2): no redirect followed, every
    /// compression taken, no cookies, 10 s to connect, and no proxy for a loopback API (the
    /// environment's proxy does not leave loopback out by itself), the system's otherwise.
    /// </summary>
    public static Func<HttpMessageHandler> DefaultHandler(GitHubEndpoints endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var loopback = endpoints.ApiBase.IsLoopback;
        return () => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            UseProxy = !loopback,
        };
    }

    private static HostingOptions For(GitHubEndpoints endpoints) => new(endpoints, DefaultHandler(endpoints));

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
