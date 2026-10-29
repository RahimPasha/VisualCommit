using System.Text.RegularExpressions;
using VisualCommit.Core.Hosting;

namespace VisualCommit.Hosting.GitHub;

/// <summary>
/// Which remote URLs are GitHub's (docs/phase-7a-design.md 6.3; ruling 23), without the network,
/// and the push guard's rule for <c>refs/pull</c> (ruling 8). The URL is read by hand rather than
/// by <see cref="Uri"/>, which would unescape, normalise and guess where git does not.
/// </summary>
internal static partial class GitHubUrls
{
    private const string PullPrefix = "refs/pull/";

    /// <summary>
    /// The repository <paramref name="url"/> names on <paramref name="endpoints"/>, or null. On
    /// github.com (<see cref="GitHubEndpoints.GitHubCom"/>), the host <c>github.com</c> or
    /// <c>www.github.com</c> in <c>https://[user[:pw]@]host[:443]/o/r</c>,
    /// <c>http://[user[:pw]@]host[:80]/o/r</c>, <c>ssh://[user@]host[:22]/o/r</c>,
    /// <c>ssh://[user@]ssh.github.com:443/o/r</c>, scp-like <c>user@host:o/r</c> and
    /// <c>git://host/o/r</c>; on any other endpoints (the fake, <see cref="GitHubEndpoints.Local"/>)
    /// only <see cref="GitHubEndpoints.WebBase"/>'s scheme, host and port,
    /// <c>http://[user[:pw]@]127.0.0.1:&lt;port&gt;/o/r</c>. The path is exactly two segments, then
    /// an optional <c>.git</c> (once) and <c>/</c>, with no query, fragment or percent escape. The
    /// owner and name are kept as written; the user part never is.
    /// </summary>
    public static HostedRepository? Recognise(GitHubEndpoints endpoints, string? url)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (string.IsNullOrEmpty(url) || url.Any(c => c <= ' ' || c == '\u007F'))
        {
            return null;
        }

        var path = IsGitHubCom(endpoints) ? GitHubComPath(url) : LocalPath(endpoints.WebBase, url);
        return path is not null && Split(path) is { } repository
            ? new HostedRepository(HostingProvider.GitHub, endpoints.Host, repository.Owner, repository.Name)
            : null;
    }

    /// <summary>
    /// The push guard's rule (ruling 8): false only for <c>refs/pull/&lt;x&gt;/head</c> or
    /// <c>refs/pull/&lt;x&gt;/merge</c> whose <c>&lt;x&gt;</c> is not one or more ASCII digits
    /// (<c>refs/pull/fix/typo/head</c> included), which GitHub never has; true otherwise.
    /// </summary>
    public static bool SourceCanExist(string remoteRef)
    {
        ArgumentNullException.ThrowIfNull(remoteRef);
        if (!remoteRef.StartsWith(PullPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        var rest = remoteRef[PullPrefix.Length..];
        var slash = rest.LastIndexOf('/');
        if (slash < 0 || rest[(slash + 1)..] is not ("head" or "merge"))
        {
            return true;
        }

        var number = rest[..slash];
        return number.Length > 0 && number.All(char.IsAsciiDigit);
    }

    /// <summary>Whether <paramref name="endpoints"/> are github.com's own.</summary>
    public static bool IsGitHubCom(GitHubEndpoints endpoints) => endpoints == GitHubEndpoints.GitHubCom;

    /// <summary>Whether two addresses have the same scheme, host and port (ruling 23's match, D285's).</summary>
    public static bool SameOrigin(Uri a, Uri b) =>
        string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.IdnHost, b.IdnHost, StringComparison.OrdinalIgnoreCase)
        && a.Port == b.Port;

    /// <summary><c>&lt;scheme&gt;://&lt;host&gt;:&lt;port&gt;</c> of <paramref name="uri"/>, for a log line: never its path or query.</summary>
    public static string OriginOf(Uri uri) => FormattableString.Invariant($"{uri.Scheme}://{uri.IdnHost}:{uri.Port}");

    /// <summary>The path of a github.com URL in one of its forms, or null.</summary>
    private static string? GitHubComPath(string url)
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
            "http" when isWeb && port is null or 80 => path,
            "ssh" when isWeb && port is null or 22 && !HasPassword(user) => path,
            "ssh" when IsHost(host, "ssh.github.com") && port == 443 && !HasPassword(user) => path,
            "git" when isWeb && port is null && user is null => path,
            _ => null,
        };
    }

    /// <summary>The path of the fake's URL: its web address's scheme, host and port exactly (ruling 23), or null.</summary>
    private static string? LocalPath(Uri webBase, string url)
    {
        if (Parts(url) is not { } parts)
        {
            return null;
        }

        var (scheme, _, host, port, path) = parts;
        var defaultPort = scheme switch
        {
            "http" => 80,
            "https" => 443,
            _ => -1,
        };
        return string.Equals(scheme, webBase.Scheme, StringComparison.Ordinal)
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

            port = int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
        }

        return host.Length == 0 ? null : (scheme, user, host, port, rest[(slash + 1)..]);
    }

    /// <summary>The path of scp-like <c>user@host:o/r</c> on github.com or www.github.com, or null.</summary>
    private static string? ScpPath(string url)
    {
        var colon = url.IndexOf(':');
        if (colon < 0 || url.AsSpan(0, colon).Contains('/'))
        {
            return null;
        }

        var authority = url[..colon];
        var at = authority.LastIndexOf('@');
        return at > 0 && IsWebHost(authority[(at + 1)..]) && !HasPassword(authority[..at])
            ? url[(colon + 1)..]
            : null;
    }

    /// <summary>
    /// The owner and name of <c>o/r</c>, <c>o/r.git</c>, <c>o/r/</c> or <c>o/r.git/</c>, when both
    /// are names GitHub allows and nothing else is there; null otherwise.
    /// </summary>
    private static (string Owner, string Name)? Split(string path)
    {
        if (path.Length == 0 || path.AsSpan().IndexOfAny("?#%\\") >= 0)
        {
            return null;
        }

        if (path.EndsWith('/'))
        {
            path = path[..^1];
        }

        if (path.EndsWith(".git", StringComparison.Ordinal))
        {
            path = path[..^4];
        }

        var segments = path.Split('/');
        return segments is [var owner, var name] && OwnerPattern().IsMatch(owner) && NamePattern().IsMatch(name) && name is not ("." or "..")
            ? (owner, name)
            : null;
    }

    private static bool IsWebHost(string host) => IsHost(host, "github.com") || IsHost(host, "www.github.com");

    private static bool IsHost(string host, string expected) => string.Equals(host, expected, StringComparison.OrdinalIgnoreCase);

    private static bool HasPassword(string? user) => user is not null && user.Contains(':', StringComparison.Ordinal);

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnerPattern();

    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
