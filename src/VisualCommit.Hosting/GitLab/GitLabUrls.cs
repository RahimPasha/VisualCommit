using System.Globalization;
using System.Text.RegularExpressions;
using VisualCommit.Core.Hosting;

namespace VisualCommit.Hosting.GitLab;

/// <summary>
/// Which remote URLs are GitLab's (docs/phase-7b-design.md 7.2), without the network. The URL is
/// read by hand, as <c>GitHubUrls</c> reads GitHub's, rather than by <see cref="Uri"/>, which would
/// unescape, normalise and guess where git does not.
/// </summary>
internal static partial class GitLabUrls
{
    /// <summary>
    /// The most segments a project's path has: a namespace of up to 21 (subgroups "nest up to 20
    /// levels", r6 6.1 READ, unclear whether with the top-level group) and the project's own name.
    /// </summary>
    public const int MaxSegments = 22;

    /// <summary>
    /// The repository <paramref name="url"/> names on <paramref name="endpoints"/>, or null. On
    /// gitlab.com (<see cref="GitLabEndpoints.GitLabCom"/>), the host <c>gitlab.com</c> or
    /// <c>www.gitlab.com</c> (which 308s, r6 6.1 MEASURED) in
    /// <c>https://[user[:pw]@]host[:443]/p</c>, <c>http://[user@]host[:80]/p</c>,
    /// <c>ssh://[user@]host[:22]/p</c> and scp-like <c>user@host:p</c>, and
    /// <c>ssh://[user@]altssh.gitlab.com:443/p</c>; no <c>git://</c>. On any other endpoints (the
    /// fake, <see cref="GitLabEndpoints.Local"/>) only <see cref="GitLabEndpoints.WebBase"/>'s
    /// scheme, host and port, <c>http://127.0.0.1:&lt;port&gt;/p</c>, with no user part. The path
    /// <c>p</c> loses one final <c>/</c> and then one final <c>.git</c>; it has 2 to
    /// <see cref="MaxSegments"/> segments, each of <c>[A-Za-z0-9_.-]</c> and none <c>.</c>,
    /// <c>..</c> or <c>-</c> (a web address's <c>/-/</c>), and no <c>%</c>, <c>?</c>, <c>#</c>,
    /// <c>\</c>, space or control character. The owner is the namespace path and the name the last
    /// segment, both as written; the user part is never kept. An authority with <c>?</c>, <c>#</c>
    /// or <c>\</c> is refused: git and curl end it there, so its last <c>@</c> names no host. An
    /// ssh or scp-like one with <c>%</c>, <c>[</c> or <c>]</c> is refused too: git url-decodes an
    /// <c>ssh://</c> URL before it finds the host, and unwraps <c>[…]</c> at the host's start or after
    /// <c>@[</c>, so there too its last <c>@</c> names no host. An https or http one with two or
    /// more <c>@</c> is refused: curl ends its user at the first <c>@</c> and rejects the rest as a
    /// host, while ssh's user ends at the last <c>@</c>, so ssh and scp-like keep that reading.
    /// </summary>
    public static HostedRepository? Recognise(GitLabEndpoints endpoints, string? url)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (string.IsNullOrEmpty(url) || url.Any(c => c <= ' ' || c == '\u007F'))
        {
            return null;
        }

        var path = IsGitLabCom(endpoints) ? GitLabComPath(url) : LocalPath(endpoints.WebBase, url);
        return path is not null && Split(path) is { } repository
            ? new HostedRepository(HostingProvider.GitLab, endpoints.Host, repository.Owner, repository.Name)
            : null;
    }

    /// <summary>Whether <paramref name="endpoints"/> are gitlab.com's own.</summary>
    public static bool IsGitLabCom(GitLabEndpoints endpoints) => endpoints == GitLabEndpoints.GitLabCom;

    /// <summary>
    /// The owner and name of a project's path as GitLab's API names it (<c>path_with_namespace</c>),
    /// under 7.2's rules for a path; null when it breaks them.
    /// </summary>
    public static (string Owner, string Name)? SplitPathWithNamespace(string? path) =>
        string.IsNullOrEmpty(path) || path.Any(c => c <= ' ' || c == '\u007F') || path.EndsWith('/') ? null : Segments(path);

    /// <summary>The path of a gitlab.com URL in one of its forms, or null.</summary>
    private static string? GitLabComPath(string url)
    {
        if (Parts(url) is not { } parts)
        {
            return ScpPath(url);
        }

        var (scheme, user, host, port, path) = parts;
        var isWeb = IsWebHost(host);
        return scheme switch
        {
            "https" when isWeb && port is null or 443 => path,
            "http" when isWeb && port is null or 80 && !HasPassword(user) => path,
            "ssh" when isWeb && port is null or 22 && !HasPassword(user) => path,
            "ssh" when IsHost(host, "altssh.gitlab.com") && port == 443 && !HasPassword(user) => path,
            _ => null,
        };
    }

    /// <summary>The path of the fake's URL: its web address's scheme, host and port exactly and no user part, or null.</summary>
    private static string? LocalPath(Uri webBase, string url)
    {
        if (Parts(url) is not { } parts)
        {
            return null;
        }

        var (scheme, user, host, port, path) = parts;
        var defaultPort = scheme switch
        {
            "http" => 80,
            "https" => 443,
            _ => -1,
        };
        return user is null
            && string.Equals(scheme, webBase.Scheme, StringComparison.Ordinal)
            && string.Equals(host, webBase.IdnHost, StringComparison.OrdinalIgnoreCase)
            && (port ?? defaultPort) == webBase.Port
            ? path
            : null;
    }

    /// <summary>
    /// An URL of the form <c>scheme://[user@]host[:port]/path</c>: its scheme in lower case, its
    /// user part (null when none), host, port (null when none) and path after the first <c>/</c>;
    /// null for anything else.
    /// </summary>
    private static (string Scheme, string? User, string Host, int? Port, string Path)? Parts(string url)
    {
        var separator = url.IndexOf("://", StringComparison.Ordinal);
        if (separator <= 0)
        {
            return null;
        }

        var scheme = url[..separator].ToLowerInvariant();
        var rest = url[(separator + 3)..];
        var slash = rest.IndexOf('/');
        if (slash < 0)
        {
            return null;
        }

        var authority = rest[..slash];
        if (EndsAnAuthority(authority)
            || (scheme == "ssh" && GitReadsAnotherHost(authority))
            || (scheme is "http" or "https" && CurlReadsAnotherHost(authority)))
        {
            return null;
        }

        var at = authority.LastIndexOf('@');
        var user = at < 0 ? null : authority[..at];
        if (user is { Length: 0 })
        {
            return null;
        }

        var hostAndPort = authority[(at + 1)..];
        var colon = hostAndPort.IndexOf(':');
        var host = colon < 0 ? hostAndPort : hostAndPort[..colon];
        int? port = null;
        if (colon >= 0)
        {
            var digits = hostAndPort[(colon + 1)..];
            if (digits.Length is 0 or > 5 || !digits.All(char.IsAsciiDigit))
            {
                return null;
            }

            port = int.Parse(digits, CultureInfo.InvariantCulture);
        }

        return host.Length == 0 ? null : (scheme, user, host, port, rest[(slash + 1)..]);
    }

    /// <summary>The path of scp-like <c>user@host:p</c> on gitlab.com or www.gitlab.com, or null.</summary>
    private static string? ScpPath(string url)
    {
        var colon = url.IndexOf(':');
        if (colon < 0 || url.AsSpan(0, colon).Contains('/'))
        {
            return null;
        }

        var authority = url[..colon];
        var at = authority.LastIndexOf('@');
        return at > 0 && !EndsAnAuthority(authority) && !GitReadsAnotherHost(authority) && IsWebHost(authority[(at + 1)..]) && !HasPassword(authority[..at])
            ? url[(colon + 1)..]
            : null;
    }

    /// <summary>The owner and name of a remote's path: one final <c>/</c>, then one final <c>.git</c>, stripped; null when the rest breaks 7.2's rules.</summary>
    private static (string Owner, string Name)? Split(string path)
    {
        if (path.EndsWith('/'))
        {
            path = path[..^1];
        }

        if (path.EndsWith(".git", StringComparison.Ordinal))
        {
            path = path[..^4];
        }

        return Segments(path);
    }

    /// <summary>The namespace path and the last segment of <paramref name="path"/>, when it has 2 to <see cref="MaxSegments"/> segments that GitLab allows.</summary>
    private static (string Owner, string Name)? Segments(string path)
    {
        if (path.Length == 0 || path.AsSpan().IndexOfAny("?#%\\") >= 0)
        {
            return null;
        }

        var segments = path.Split('/');
        if (segments.Length is < 2 or > MaxSegments || !segments.All(segment => SegmentPattern().IsMatch(segment) && segment is not ("." or ".." or "-")))
        {
            return null;
        }

        var last = path.LastIndexOf('/');
        return (path[..last], path[(last + 1)..]);
    }

    /// <summary>
    /// Whether <paramref name="authority"/> holds a character at which git or curl end an
    /// authority before its last <c>@</c>: <c>?</c> and <c>#</c>, and <c>\</c> for curl on Windows.
    /// Git sends <c>https://evil.example#@gitlab.com/g/p.git</c> to evil.example (MEASURED by WP-GL's
    /// review with git 2.56.0.windows.4 through a local proxy, the ?@ and \@ forms and http too), so
    /// such a URL is no one's; an scp-like one is refused alike.
    /// </summary>
    private static bool EndsAnAuthority(string authority) => authority.AsSpan().IndexOfAny("?#\\") >= 0;

    /// <summary>
    /// Whether git may read another host in an ssh or scp-like <paramref name="authority"/> than its
    /// text after the last <c>@</c>: one with <c>%</c>, which git url-decodes in an <c>ssh://</c> URL
    /// before it splits the host from the path (<c>ssh://evil.example%2F@gitlab.com/g/p.git</c> goes
    /// to evil.example), or with <c>[</c> or <c>]</c>, which git unwraps at the host's start or after
    /// <c>@[</c> (<c>git@[evil.example]@gitlab.com:g/p.git</c> goes to evil.example). MEASURED by
    /// WP-GL's review with git 2.56.0.windows.4 and a recording <c>GIT_SSH_COMMAND</c>. https and
    /// http keep the rule of <see cref="EndsAnAuthority"/> alone: curl neither decodes nor unwraps
    /// there (the review's proxy saw <c>CONNECT gitlab.com:443</c>).
    /// </summary>
    private static bool GitReadsAnotherHost(string authority) => authority.AsSpan().IndexOfAny("%[]") >= 0;

    /// <summary>
    /// Whether curl reads another host in an https or http <paramref name="authority"/> than its
    /// text after the last <c>@</c>: one with two or more <c>@</c>, whose user curl ends at the first
    /// <c>@</c>, so that it takes <c>evil.example@gitlab.com</c> as the host of
    /// <c>https://a@evil.example@gitlab.com/g/p.git</c> and rejects it ("URL rejected: Bad
    /// hostname"; MEASURED by WP-GL's third review with git 2.56.0.windows.4 through a local
    /// recording proxy, which saw no CONNECT). Git then reaches no host, so such a URL is no
    /// one's. ssh and scp-like keep the last <c>@</c>: OpenSSH splits its destination's user there
    /// (<c>ssh -G 'a@evil.example@gitlab.com'</c> gives hostname gitlab.com).
    /// </summary>
    private static bool CurlReadsAnotherHost(string authority) => authority.IndexOf('@') != authority.LastIndexOf('@');

    private static bool IsWebHost(string host) => IsHost(host, "gitlab.com") || IsHost(host, "www.gitlab.com");

    private static bool IsHost(string host, string expected) => string.Equals(host, expected, StringComparison.OrdinalIgnoreCase);

    private static bool HasPassword(string? user) => user is not null && user.Contains(':', StringComparison.Ordinal);

    // \z, not $: $ matches before a last line end too.
    [GeneratedRegex(@"^[A-Za-z0-9_.-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPattern();
}
