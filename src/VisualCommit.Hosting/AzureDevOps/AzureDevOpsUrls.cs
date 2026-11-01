using System.Globalization;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;

namespace VisualCommit.Hosting.AzureDevOps;

/// <summary>
/// Which remote URLs are Azure DevOps' (docs/phase-7b-design.md 6.2), without the network and of any
/// organization; the organization a typed text names (ruling 16, D360); and the web and API
/// addresses of a repository. URLs are read by hand, as <c>GitHubUrls</c> reads them, except an
/// <c>http</c> or <c>https</c> URL's host and port, which are read as .NET reads them, as
/// <see cref="CredentialBinding"/>'s <c>Select</c> does, so that recognition and the binding agree
/// on which host a URL names; the path is never read by <see cref="Uri"/>, which would unescape,
/// normalise and guess where git does not.
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
    /// <c>_git</c>, <c>_full</c>, <c>_optimized</c> and <c>DefaultCollection</c> match without case and
    /// with their escapes decoded (<c>%5Fgit</c> is <c>_git</c>, as git and .NET read it);
    /// a project starting with <c>_</c> is refused; project and repository are percent-decoded as UTF-8.
    /// The host is <see cref="AzureDevOpsEndpoints.Host"/> for every form (r5 6.2). An <c>http</c> or
    /// <c>https</c> URL's host and port are read as .NET reads them, as
    /// <see cref="CredentialBinding.Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/>
    /// does, so that a URL is recognised wherever it is bound: an empty user is none, an empty port
    /// the scheme's, a port's leading zeros are read by value, and the fake's <c>127.1</c> (or any
    /// other form .NET reads as <c>127.0.0.1</c>) is its host.
    /// </summary>
    public static HostedRepository? Recognise(AzureDevOpsEndpoints endpoints, string? url) => Recognise(endpoints, url, out _);

    /// <summary>
    /// <see cref="Recognise(AzureDevOpsEndpoints, string?)"/>, with the URL's user part as written
    /// (null when it has none or an empty one): A2 and A3 hold an answer's <c>remoteUrl</c> to it.
    /// </summary>
    public static HostedRepository? Recognise(AzureDevOpsEndpoints endpoints, string? url, out string? user)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        user = null;
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if ((HttpForm(endpoints, url) ?? (endpoints.OrganizationHosts ? SshForm(url) : null)) is not { } found)
        {
            return null;
        }

        user = found.User;
        return new HostedRepository(HostingProvider.AzureDevOps, endpoints.Host, found.Named.Organization + "/" + found.Named.Project, found.Named.Repository);
    }

    /// <summary>
    /// The organization <paramref name="text"/> names (1.4; ruling 16, the one rule of WP-A's box),
    /// trimmed: a text without <c>://</c> is the name; an <c>http</c> or <c>https</c> URL, its user
    /// part ignored, gives its first path segment on <see cref="AzureDevOpsEndpoints.WebBase"/>'s host
    /// and port, or with <see cref="AzureDevOpsEndpoints.OrganizationHosts"/> the label of
    /// <c>https://&lt;label&gt;.visualstudio.com</c> (the host without case). The result must match
    /// <c>^[A-Za-z0-9-]+$</c> (D360), else null; it is kept as written. The host and port are read as
    /// <see cref="Recognise(AzureDevOpsEndpoints, string?)"/> reads them; a URL holding <c>\</c>, which
    /// .NET reads as <c>/</c> where the text has none, names nothing.
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
        if (trimmed.Contains('\\', StringComparison.Ordinal) || Parts(end < 0 ? trimmed : trimmed[..end], passwordAllowed: true) is not { } parts)
        {
            return null;
        }

        if (OnWebBase(endpoints, parts.Host, parts.Port, sameScheme: null))
        {
            var slash = parts.Path.IndexOf('/', StringComparison.Ordinal);
            var first = slash < 0 ? parts.Path : parts.Path[..slash];
            return IsOrganizationName(first) ? first : null;
        }

        return endpoints.OrganizationHosts ? OrganizationOfHost(parts) : null;
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
    /// organization in the grammar (D360), and a project and a name in <see cref="IsName"/>'s
    /// form; else null.
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
    }

    /// <summary>
    /// A project's or a repository's name that stays one segment of the organization's addresses:
    /// not empty, <c>.</c> or <c>..</c> (a dot segment would leave the organization once the address
    /// is resolved), and without <c>/</c> or <c>\</c>, which no Azure DevOps name holds and which a
    /// server that decodes <c>%2F</c> or <c>%5C</c> before it resolves dot segments would read as
    /// separators. A2's rows, A3's parent and A4's fork source come from the server, so they are
    /// held to it too.
    /// </summary>
    public static bool IsName(string? name) =>
        !string.IsNullOrEmpty(name) && name is not ("." or "..") && name.AsSpan().IndexOfAny('/', '\\') < 0;

    /// <summary>Azure DevOps' organization grammar, <c>^[A-Za-z0-9-]+$</c> (D360).</summary>
    public static bool IsOrganizationName(string? name) =>
        !string.IsNullOrEmpty(name) && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    /// <summary>A path segment escaped for an address (<see cref="Uri.EscapeDataString(string)"/>).</summary>
    public static string Escape(string name) => Uri.EscapeDataString(name);

    /// <summary>An <c>http</c> or <c>https</c> URL in one of the web base's or the organization host's forms, with its user.</summary>
    private static ((string Organization, string Project, string Repository) Named, string? User)? HttpForm(AzureDevOpsEndpoints endpoints, string url)
    {
        if (!CredentialBinding.InPlainForm(url) || Parts(url, passwordAllowed: false) is not { } parts)
        {
            return null;
        }

        var segments = Segments(parts.Path);
        (string Organization, string Project, string Repository)? named = null;
        if (OnWebBase(endpoints, parts.Host, parts.Port, sameScheme: parts.Scheme))
        {
            if (segments is [var organization, .. var rest] && IsOrganizationName(organization) && !IsDefaultCollection(organization))
            {
                named = Below(organization, rest);
            }
        }
        else if (endpoints.OrganizationHosts && OrganizationOfHost(parts) is { } label)
        {
            named = Below(label, segments is [var first, .. var rest] && IsDefaultCollection(first) ? rest : segments);
        }

        return named is { } found ? (found, parts.User) : null;
    }

    /// <summary>
    /// The organization of <c>https://&lt;label&gt;.visualstudio.com</c> on port 443, the host as .NET
    /// reads it (as <see cref="CredentialBinding.Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/>
    /// does) and the label in the spelling written (the host as written equal to .NET's without case); else null.
    /// </summary>
    private static string? OrganizationOfHost((string Scheme, string Host, int Port, string WrittenHost, string? User, string Path) parts) =>
        parts.Scheme == Uri.UriSchemeHttps
        && parts.Port == 443
        && string.Equals(parts.WrittenHost, parts.Host, StringComparison.OrdinalIgnoreCase)
            ? OrganizationLabel(parts.WrittenHost)
            : null;

    /// <summary>One of the SSH forms on the service, its path after <c>v3/</c> <c>&lt;org&gt;/&lt;project&gt;/&lt;repo&gt;</c>, with its user.</summary>
    private static ((string Organization, string Project, string Repository) Named, string? User)? SshForm(string url)
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

        if (SplitUser(authority, passwordAllowed: false) is not { } split
            || !(IsHost(split.HostAndPort, SshHost) || IsHost(split.HostAndPort, OrganizationSshHost))
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

        return Segments(path) is [var organization, var project, var repository] && IsOrganizationName(organization) && Named(organization, project, repository) is { } named
            ? (named, split.User)
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
    /// case): its host and port as .NET reads them (<see cref="Uri.Host"/>, <see cref="Uri.Port"/>), as
    /// <see cref="CredentialBinding.Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/>
    /// reads them (an empty port the scheme's, leading zeros by value, any form of an IPv4 address
    /// in its dotted form, the host in lower case); and by hand the host as written, the user as
    /// written (null when none or empty) and the path after the first <c>/</c> (empty when none).
    /// Null for anything else, for a URL .NET does not read, for two <c>@</c> or, unless
    /// <paramref name="passwordAllowed"/>, a password.
    /// </summary>
    private static (string Scheme, string Host, int Port, string WrittenHost, string? User, string Path)? Parts(string url, bool passwordAllowed)
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
        if (SplitUser(authority, passwordAllowed) is not { } split
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, scheme, StringComparison.Ordinal)
            || uri.Host.Length == 0)
        {
            return null;
        }

        var colon = split.HostAndPort.IndexOf(':', StringComparison.Ordinal);
        var writtenHost = colon < 0 ? split.HostAndPort : split.HostAndPort[..colon];
        return writtenHost.Length == 0 ? null : (scheme, uri.Host, uri.Port, writtenHost, split.User, path);
    }

    /// <summary>
    /// The authority's user as written, null when it has none or an empty one (which .NET and git
    /// read as none: <c>https://@dev.azure.com/…</c> is bound as without a user), and the rest; null
    /// for a second <c>@</c> or a password not allowed.
    /// </summary>
    private static (string? User, string HostAndPort)? SplitUser(string authority, bool passwordAllowed)
    {
        var at = authority.IndexOf('@', StringComparison.Ordinal);
        if (at < 0)
        {
            return (null, authority);
        }

        var user = authority[..at];
        return authority.IndexOf('@', at + 1) >= 0 || (!passwordAllowed && user.Contains(':', StringComparison.Ordinal))
            ? null
            : (user.Length == 0 ? null : user, authority[(at + 1)..]);
    }

    /// <summary>An SSH port: digits, read by value whatever their leading zeros, up to 65535.</summary>
    private static int? PortOf(string digits)
    {
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
        {
            return null;
        }

        var significant = digits.TrimStart('0');
        return significant.Length <= 5 && (significant.Length == 0 ? 0 : int.Parse(significant, CultureInfo.InvariantCulture)) is var port and <= 65535
            ? port
            : null;
    }

    /// <summary>Whether a host and port, as .NET reads them, are the web base's (the host without case), and its scheme when <paramref name="sameScheme"/> names one.</summary>
    private static bool OnWebBase(AzureDevOpsEndpoints endpoints, string host, int port, string? sameScheme) =>
        (sameScheme is null || string.Equals(sameScheme, endpoints.WebBase.Scheme, StringComparison.OrdinalIgnoreCase))
        && string.Equals(host, endpoints.WebBase.Host, StringComparison.OrdinalIgnoreCase)
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

    /// <summary>
    /// Whether a path segment is <paramref name="literal"/> without case, its escapes decoded: RFC 3986
    /// 2.3 makes <c>%5Fgit</c> and <c>_gi%74</c> the same segment as <c>_git</c>, and .NET, git's
    /// <c>url_normalize</c> and so <see cref="CredentialBinding.Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/>
    /// read them so. Every caller's URL has passed <see cref="CredentialBinding.InPlainForm"/>, so no
    /// escape decodes to <c>/</c>, <c>.</c>, <c>\</c>, <c>%</c> or a control.
    /// </summary>
    private static bool IsSegment(string segment, string literal) => string.Equals(Uri.UnescapeDataString(segment), literal, StringComparison.OrdinalIgnoreCase);

    private static bool IsHost(string host, string expected) => string.Equals(host, expected, StringComparison.OrdinalIgnoreCase);
}
