using System.Globalization;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;

namespace VisualCommit.Hosting.AzureDevOps;

/// <summary>
/// Which remote URLs are Azure DevOps' (docs/phase-7b-design.md 6.2), without the network and of any
/// organization; the organization a typed text names (ruling 16, D360); and the web and API
/// addresses of a repository. URLs are read by hand, as <c>GitHubUrls</c> reads them, never by
/// <see cref="Uri"/>, which would unescape, normalise and guess where git does not.
/// </summary>
internal static class AzureDevOpsUrls
{
    private const string OrganizationHostSuffix = ".visualstudio.com";
    private const string SshHost = "ssh.dev.azure.com";
    private const string OrganizationSshHost = "vs-ssh.visualstudio.com";
    private const string DefaultCollection = "DefaultCollection";
    private const string GitSegment = "_git";
    private const string SshVersion = "v3/";

    /// <summary>The labels before <c>.visualstudio.com</c> that are the service's own hosts, never an organization's (r7 0.9; G3.2 V17).</summary>
    private static readonly string[] ServiceLabels = ["vssps", "app", "vs-ssh"];

    /// <summary>
    /// The repository <paramref name="url"/> names on <paramref name="endpoints"/>, or null (6.2).
    /// On <see cref="AzureDevOpsEndpoints.WebBase"/>'s scheme, host (without case) and port:
    /// <c>[user@]host/&lt;org&gt;/&lt;project&gt;/_git/&lt;repo&gt;</c> and <c>…/&lt;org&gt;/_git/&lt;repo&gt;</c>
    /// (the project the repository's name, MEASURED r5 6.1). With
    /// <see cref="AzureDevOpsEndpoints.OrganizationHosts"/> (the service) also
    /// <c>https://[user@]&lt;org&gt;.visualstudio.com[:443]/[DefaultCollection/]&lt;project&gt;/_git/&lt;repo&gt;</c> and
    /// <c>…/[DefaultCollection/]_git/&lt;repo&gt;</c>, one label in the organization's grammar that is not
    /// <c>vssps</c>, <c>app</c> or <c>vs-ssh</c>; and SSH, recognised and never bound:
    /// <c>[user@]ssh.dev.azure.com:v3/&lt;org&gt;/&lt;project&gt;/&lt;repo&gt;</c>, its <c>ssh://…[:22]/v3/…</c>
    /// form, and both on <c>vs-ssh.visualstudio.com</c>. After <c>_git/</c> an optional <c>_full/</c>
    /// or <c>_optimized/</c> is skipped; one trailing <c>/</c> is allowed; <c>.git</c> belongs to the
    /// name (MEASURED r5 6.1). An <c>http</c> or <c>https</c> URL must pass
    /// <see cref="CredentialBinding.InPlainForm"/> (g3 G3.4's form), and an SSH form's path after
    /// <c>v3/</c> its character, segment and escape rules; a user part is allowed (no password) and
    /// never kept. The organization (the first segment, or the label) matches <c>^[A-Za-z0-9-]+$</c>
    /// (D360) and is kept as written; <c>DefaultCollection</c> is no organization of dev.azure.com's;
    /// a project starting with <c>_</c> is refused; project and repository are percent-decoded as UTF-8.
    /// The host is <see cref="AzureDevOpsEndpoints.Host"/> for every form (r5 6.2).
    /// </summary>
    public static HostedRepository? Recognise(AzureDevOpsEndpoints endpoints, string? url)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        var found = HttpForm(endpoints, url) ?? (endpoints.OrganizationHosts ? SshForm(url) : null);
        return found is { } named
            ? new HostedRepository(HostingProvider.AzureDevOps, endpoints.Host, named.Organization + "/" + named.Project, named.Repository)
            : null;
    }

    /// <summary>
    /// The organization <paramref name="text"/> names (1.4; ruling 16, the one rule of WP-A's box),
    /// trimmed: a text without <c>://</c> is the name; an <c>http</c> or <c>https</c> URL, its user
    /// part ignored, gives its first path segment on <see cref="AzureDevOpsEndpoints.WebBase"/>'s host
    /// and port, or with <see cref="AzureDevOpsEndpoints.OrganizationHosts"/> the label of
    /// <c>https://&lt;label&gt;.visualstudio.com</c> (the host without case). The result must match
    /// <c>^[A-Za-z0-9-]+$</c> (D360), else null; it is kept as written.
    /// </summary>
    public static string? OrganizationOf(string? text, AzureDevOpsEndpoints endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            return IsOrganizationName(trimmed) ? trimmed : null;
        }

        var end = trimmed.AsSpan().IndexOfAny('?', '#');
        if (Parts(end < 0 ? trimmed : trimmed[..end], passwordAllowed: true) is not { } parts)
        {
            return null;
        }

        var (scheme, host, port, path) = parts;
        if (OnWebBase(endpoints, host, port ?? DefaultPort(scheme), sameScheme: null))
        {
            var slash = path.IndexOf('/', StringComparison.Ordinal);
            var first = slash < 0 ? path : path[..slash];
            return IsOrganizationName(first) ? first : null;
        }

        return endpoints.OrganizationHosts && scheme == Uri.UriSchemeHttps && (port ?? 443) == 443 ? OrganizationLabel(host) : null;
    }

    /// <summary>
    /// <c>{WebBase}&lt;org&gt;/&lt;project&gt;/_git/&lt;repo&gt;</c>, each segment escaped (<c>webUrl</c>'s form, MEASURED r5 2.1).
    /// </summary>
    /// <exception cref="ArgumentException">The repository's owner is not <c>&lt;org&gt;/&lt;project&gt;</c> (<see cref="NamesOf"/>).</exception>
    public static Uri WebUrlOf(AzureDevOpsEndpoints endpoints, HostedRepository repository)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var (organization, project, name) = NamesOf(repository) ?? throw new ArgumentException("Not an Azure DevOps repository's names.", nameof(repository));
        return new Uri(endpoints.WebBase, $"{Escape(organization)}/{Escape(project)}/{GitSegment}/{Escape(name)}");
    }

    /// <summary>
    /// <c>&lt;org&gt;/&lt;project&gt;/_apis/git/repositories/&lt;repo&gt;&lt;rest&gt;</c> on
    /// <see cref="AzureDevOpsEndpoints.WebBase"/>, each name escaped: every request of A3 to A5 is below it (6.5).
    /// </summary>
    /// <exception cref="ArgumentException">The repository's owner is not <c>&lt;org&gt;/&lt;project&gt;</c> (<see cref="NamesOf"/>).</exception>
    public static Uri ApiPath(AzureDevOpsEndpoints endpoints, HostedRepository repository, string rest)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(rest);
        var (organization, project, name) = NamesOf(repository) ?? throw new ArgumentException("Not an Azure DevOps repository's names.", nameof(repository));
        return new Uri(endpoints.WebBase, $"{Escape(organization)}/{Escape(project)}/_apis/git/repositories/{Escape(name)}{rest}");
    }

    /// <summary>
    /// A repository's organization, project and name when its owner is two segments: an
    /// organization in the grammar (D360), and a project and a name that are neither empty, <c>.</c>
    /// nor <c>..</c> (an escaped name stays one segment, but a dot segment would leave the
    /// organization once the address is resolved); else null.
    /// </summary>
    public static (string Organization, string Project, string Name)? NamesOf(HostedRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var owner = repository.Owner.Split('/');
        return owner is [var organization, var project]
            && IsOrganizationName(organization)
            && IsName(project)
            && IsName(repository.Name)
            ? (organization, project, repository.Name)
            : null;

        static bool IsName(string name) => name.Length > 0 && name is not ("." or "..");
    }

    /// <summary>Azure DevOps' organization grammar, <c>^[A-Za-z0-9-]+$</c> (D360).</summary>
    public static bool IsOrganizationName(string? name) =>
        !string.IsNullOrEmpty(name) && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    /// <summary>A path segment escaped for an address (<see cref="Uri.EscapeDataString(string)"/>).</summary>
    public static string Escape(string name) => Uri.EscapeDataString(name);

    /// <summary>An <c>http</c> or <c>https</c> URL in one of the web base's or the organization host's forms.</summary>
    private static (string Organization, string Project, string Repository)? HttpForm(AzureDevOpsEndpoints endpoints, string url)
    {
        if (!CredentialBinding.InPlainForm(url) || Parts(url, passwordAllowed: false) is not { } parts)
        {
            return null;
        }

        var (scheme, host, port, path) = parts;
        var segments = Segments(path);
        if (OnWebBase(endpoints, host, port ?? DefaultPort(scheme), sameScheme: scheme))
        {
            return segments is [var organization, .. var rest] && IsOrganizationName(organization) && !IsDefaultCollection(organization)
                ? Below(organization, rest)
                : null;
        }

        if (endpoints.OrganizationHosts && scheme == Uri.UriSchemeHttps && (port ?? 443) == 443 && OrganizationLabel(host) is { } label)
        {
            return Below(label, segments is [var first, .. var rest] && IsDefaultCollection(first) ? rest : segments);
        }

        return null;
    }

    /// <summary>One of the SSH forms on the service, its path after <c>v3/</c> <c>&lt;org&gt;/&lt;project&gt;/&lt;repo&gt;</c>.</summary>
    private static (string Organization, string Project, string Repository)? SshForm(string url)
    {
        if (url.AsSpan().ContainsAnyExceptInRange('!', '~') || url.AsSpan().IndexOfAny('\\', '?', '#') >= 0)
        {
            return null;
        }

        string authority;
        string path;
        int? port = null;
        if (url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
        {
            var rest = url[6..];
            var slash = rest.IndexOf('/', StringComparison.Ordinal);
            if (slash < 0)
            {
                return null;
            }

            (authority, path) = (rest[..slash], rest[(slash + 1)..]);
            var colon = authority.LastIndexOf(':');
            if (colon >= 0)
            {
                if (PortOf(authority[(colon + 1)..]) is not { } given)
                {
                    return null;
                }

                (authority, port) = (authority[..colon], given);
            }
        }
        else
        {
            // scp-like [user@]host:path, with no '/' before the ':' (git's rule).
            var colon = url.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0 || url.AsSpan(0, colon).Contains('/'))
            {
                return null;
            }

            (authority, path) = (url[..colon], url[(colon + 1)..]);
        }

        if (WithoutUser(authority, passwordAllowed: false) is not { } host
            || !(IsHost(host, SshHost) || IsHost(host, OrganizationSshHost))
            || (port ?? 22) != 22
            || !path.StartsWith(SshVersion, StringComparison.Ordinal))
        {
            return null;
        }

        path = path[SshVersion.Length..];
        if (!CredentialBinding.InPlainForm("ssh://" + SshHost + "/" + path))
        {
            return null;
        }

        return Segments(path) is [var organization, var project, var repository] && IsOrganizationName(organization)
            ? Named(organization, project, repository)
            : null;
    }

    /// <summary>
    /// What follows the organization: <c>&lt;project&gt;/_git/[opt/]&lt;repo&gt;</c> or
    /// <c>_git/[opt/]&lt;repo&gt;</c>, <c>opt</c> one of <c>_full</c> and <c>_optimized</c>.
    /// </summary>
    private static (string Organization, string Project, string Repository)? Below(string organization, string[] rest)
    {
        string? project;
        if (rest is [var git, .. var afterGit] && IsGit(git))
        {
            (project, rest) = (null, afterGit);
        }
        else if (rest is [var named, var git2, .. var afterNamed] && IsGit(git2))
        {
            (project, rest) = (named, afterNamed);
        }
        else
        {
            return null;
        }

        if (rest is [var option, _] && (IsSegment(option, "_full") || IsSegment(option, "_optimized")))
        {
            rest = rest[1..];
        }

        return rest is [var repository] ? Named(organization, project ?? repository, repository) : null;
    }

    /// <summary>The names decoded; null when one is empty or the project starts with <c>_</c>.</summary>
    private static (string Organization, string Project, string Repository)? Named(string organization, string projectText, string repositoryText)
    {
        var project = Uri.UnescapeDataString(projectText);
        var repository = Uri.UnescapeDataString(repositoryText);
        return project.Length > 0 && repository.Length > 0 && !project.StartsWith('_')
            ? (organization, project, repository)
            : null;
    }

    /// <summary>
    /// <c>scheme://[user@]host[:port][/path]</c> with scheme <c>http</c> or <c>https</c> (in lower
    /// case), its host, port (null when none) and path after the first <c>/</c> (empty when none);
    /// null for anything else, and for an empty user, two <c>@</c> or, unless
    /// <paramref name="passwordAllowed"/>, a password.
    /// </summary>
    private static (string Scheme, string Host, int? Port, string Path)? Parts(string url, bool passwordAllowed)
    {
        var separator = url.IndexOf("://", StringComparison.Ordinal);
        if (separator <= 0)
        {
            return null;
        }

        var scheme = url[..separator].ToLowerInvariant();
        if (scheme is not ("http" or "https"))
        {
            return null;
        }

        var rest = url[(separator + 3)..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        var authority = slash < 0 ? rest : rest[..slash];
        var path = slash < 0 ? string.Empty : rest[(slash + 1)..];
        if (WithoutUser(authority, passwordAllowed) is not { } hostAndPort)
        {
            return null;
        }

        var colon = hostAndPort.IndexOf(':', StringComparison.Ordinal);
        var host = colon < 0 ? hostAndPort : hostAndPort[..colon];
        int? port = null;
        if (colon >= 0)
        {
            if (PortOf(hostAndPort[(colon + 1)..]) is not { } given)
            {
                return null;
            }

            port = given;
        }

        return host.Length == 0 ? null : (scheme, host, port, path);
    }

    /// <summary>The authority without its <c>user@</c>; null for an empty user, a second <c>@</c> or a password not allowed.</summary>
    private static string? WithoutUser(string authority, bool passwordAllowed)
    {
        var at = authority.IndexOf('@', StringComparison.Ordinal);
        if (at < 0)
        {
            return authority;
        }

        var user = authority[..at];
        return user.Length == 0 || authority.IndexOf('@', at + 1) >= 0 || (!passwordAllowed && user.Contains(':', StringComparison.Ordinal))
            ? null
            : authority[(at + 1)..];
    }

    private static int? PortOf(string digits) =>
        digits.Length is > 0 and <= 5 && digits.All(char.IsAsciiDigit) && int.Parse(digits, CultureInfo.InvariantCulture) is var port and <= 65535
            ? port
            : null;

    private static int DefaultPort(string scheme) => scheme == Uri.UriSchemeHttps ? 443 : 80;

    /// <summary>Whether a host and port are the web base's (the host without case), and its scheme when <paramref name="sameScheme"/> names one.</summary>
    private static bool OnWebBase(AzureDevOpsEndpoints endpoints, string host, int port, string? sameScheme) =>
        (sameScheme is null || string.Equals(sameScheme, endpoints.WebBase.Scheme, StringComparison.OrdinalIgnoreCase))
        && string.Equals(host, endpoints.WebBase.IdnHost, StringComparison.OrdinalIgnoreCase)
        && port == endpoints.WebBase.Port;

    /// <summary>The organization of <c>&lt;label&gt;.visualstudio.com</c>: one label in the grammar, none of the service's own; else null.</summary>
    private static string? OrganizationLabel(string host)
    {
        if (!host.EndsWith(OrganizationHostSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var label = host[..^OrganizationHostSuffix.Length];
        return IsOrganizationName(label) && !ServiceLabels.Contains(label, StringComparer.OrdinalIgnoreCase) ? label : null;
    }

    /// <summary>The path's segments, one trailing <c>/</c> removed; none for an empty path.</summary>
    private static string[] Segments(string path)
    {
        if (path.EndsWith('/'))
        {
            path = path[..^1];
        }

        return path.Length == 0 ? [] : path.Split('/');
    }

    private static bool IsGit(string segment) => IsSegment(segment, GitSegment);

    private static bool IsDefaultCollection(string segment) => IsSegment(segment, DefaultCollection);

    private static bool IsSegment(string segment, string literal) => string.Equals(segment, literal, StringComparison.OrdinalIgnoreCase);

    private static bool IsHost(string host, string expected) => string.Equals(host, expected, StringComparison.OrdinalIgnoreCase);
}
