using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Unicode;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;

namespace VisualCommit.Core.Operations;

/// <summary>
/// What one account's token covers for git (D359, docs/phase-7b-design.md 1.2): the provider
/// builds it, <see cref="CredentialBinding.Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/>
/// applies it. With only <see cref="Origin"/> set it is 7a's rule (GitHub's coverage).
/// </summary>
/// <param name="Account">The account the bound URLs are bound to.</param>
/// <param name="Origin">The scheme, host and port the bindings are on.</param>
public sealed record GitCoverage(AccountKey Account, Uri Origin)
{
    /// <summary>Azure DevOps: the first path segment on <see cref="Origin"/> must exist and be it (g3 G3.4); null: any path.</summary>
    public string? Organization { get; init; }

    /// <summary>Azure DevOps on the service: <c>https://&lt;Organization&gt;.visualstudio.com</c>, any path, covered too.</summary>
    public bool OrganizationHost { get; init; }

    /// <summary>GitLab: a URL whose path lacks <c>.git</c> is bound with its <c>.git</c> twin (ruling 12).</summary>
    public bool GitTwin { get; init; }

    /// <summary><c>x-access-token@</c> counts as the account's user (ruling 11; Azure DevOps and GitLab).</summary>
    public bool HelperUserIsAccounts { get; init; }

    /// <summary>Whether a member beyond <see cref="Origin"/> is set, so that 7b's rules apply (docs/phase-7b-design.md 8.2).</summary>
    internal bool IsBeyondOrigin => Organization is not null || OrganizationHost || GitTwin || HelperUserIsAccounts;
}

/// <summary>
/// A URL whose credentials come from a hosting account (D255, docs/phase-7a-design.md 0.2): git's
/// URL after <c>insteadOf</c>, without <c>user@</c>. A network operation's prompt scope carries its
/// bindings, and git asks the app's helper for that URL's credentials.
/// </summary>
/// <param name="Url">The bound URL.</param>
/// <param name="Account">The account whose token answers it.</param>
public sealed record CredentialBinding(string Url, AccountKey Account)
{
    /// <summary>
    /// On an origin that serves other accounts (<c>dev.azure.com</c>): the organization a prompt must
    /// name to be this account's (its raw path's first segment, or with an empty path its user,
    /// without case; a path-less <c>x-access-token</c> prompt is the account's too;
    /// docs/phase-7b-design.md 8.3); null: the origin is the account's alone.
    /// </summary>
    public string? SharedOriginOwner { get; init; }

    /// <summary>
    /// g3 G3.4's form on the URL's text (docs/phase-7b-design.md 8.2 step 2, D369): the one rule
    /// that <see cref="Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/> and Azure DevOps'
    /// recognition both apply. The text holds only U+0021 to U+007E and no <c>\</c>, <c>?</c> or
    /// <c>#</c>, and has a scheme before <c>://</c>; after <c>://</c> a <c>%</c> stands only before two
    /// hex digits that encode none of <c>2E</c>, <c>2F</c>, <c>5C</c>, <c>25</c>, <c>00</c> to <c>1F</c>
    /// and <c>7F</c>; the path (the text from the first <c>/</c> after <c>://</c>) is empty or
    /// <c>/</c>-separated segments, none <c>.</c> or <c>..</c>, none empty but one trailing, each of
    /// which, its escapes decoded, is valid UTF-8 (no overlong form, no surrogate, nothing above
    /// U+10FFFF). It has no user, host or organization clause: an empty path or <c>/</c> alone is
    /// in the form, and <see cref="Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/>
    /// refuses it on an origin with an organization.
    /// </summary>
    public static bool InPlainForm(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (url.AsSpan().ContainsAnyExceptInRange('!', '~') || url.AsSpan().ContainsAny(NotInForm))
        {
            return false;
        }

        var schemeEnd = url.IndexOf(SchemeSeparator, StringComparison.Ordinal);
        if (schemeEnd <= 0)
        {
            return false;
        }

        var afterScheme = url.AsSpan(schemeEnd + SchemeSeparator.Length);
        if (!EscapesInForm(afterScheme))
        {
            return false;
        }

        var pathStart = afterScheme.IndexOf('/');
        return pathStart < 0 || PathInForm(afterScheme[pathStart..]);
    }

    /// <summary>
    /// The bound URLs of the remotes' fetch and push URLs under <paramref name="coverage"/>
    /// (D359, D369, docs/phase-7b-design.md 8.2). With only <see cref="GitCoverage.Origin"/> set,
    /// 7a's rules on it, byte for byte: see <see cref="Select(IReadOnlyList{RemoteEffectiveUrls}, Uri, AccountKey)"/>.
    /// With any other member set, 8.2's steps: a URL on a covered host (<see cref="GitCoverage.Origin"/>,
    /// and with <see cref="GitCoverage.OrganizationHost"/> <c>https://&lt;Organization&gt;.visualstudio.com</c>)
    /// is bound when it is in <see cref="InPlainForm"/>'s form, on the origin with an organization
    /// also with a first path segment equal to it without case, and its user is none, the login
    /// without case or, with <see cref="GitCoverage.HelperUserIsAccounts"/>, <c>x-access-token</c>,
    /// with no password. With <see cref="GitCoverage.GitTwin"/> a <c>.git</c>-less URL of two or more
    /// segments is followed by its <c>.git</c> twin, and a URL whose path holds a raw reserved
    /// character by its credential twin. An <c>http</c> or <c>https</c> URL that cannot be read
    /// leaves nothing on any covered host bound (D379), and so does one whose host, as .NET or
    /// git's credential code reads it, holds a character outside U+0021 to U+007E (a fullwidth
    /// <c>dev.azure.com</c>, <c>gitlab&lt;U+FF0E&gt;com</c>), which git's curl may map into a covered host.
    /// Another user's URL on a covered host that
    /// is not in the form leaves nothing on that host bound (a URL whose user git's credential code
    /// reads otherwise, through white space or a control character, counts as one), and so, on the
    /// origin with an organization, does any URL without a password that is not in the form or has no first
    /// segment in the organization's grammar, whatever its user; else a binding is dropped when
    /// another user's path, in git's credential view, equals its path or lies under it at a
    /// <c>/</c>, and with <see cref="GitCoverage.GitTwin"/> also when their projects (one final
    /// <c>.git</c> removed) do (D379). A URL on a covered host written with one trailing dot (git's
    /// <c>match_host</c>) counts as another user's there, whatever its user; one that a redirect may
    /// lead into a covered origin (<c>http</c> or <c>https</c>, any port, the host or <c>www.</c>
    /// followed by it, one trailing dot removed) counts as on that host, never bound (D379).
    /// Distinct by <see cref="Url"/> (ordinal), in the remotes' order;
    /// <see cref="SharedOriginOwner"/> is the organization on the origin.
    /// </summary>
    /// <exception cref="ArgumentException"><see cref="GitCoverage.OrganizationHost"/> without <see cref="GitCoverage.Organization"/>, or <see cref="GitCoverage.GitTwin"/> with it.</exception>
    public static IReadOnlyList<CredentialBinding> Select(IReadOnlyList<RemoteEffectiveUrls> remotes, GitCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(remotes);
        ArgumentNullException.ThrowIfNull(coverage);
        if (!coverage.IsBeyondOrigin)
        {
            return SelectOnOrigin(remotes, coverage.Origin, coverage.Account);
        }

        if (coverage.OrganizationHost && coverage.Organization is null)
        {
            throw new ArgumentException("An organization's own host needs the organization.", nameof(coverage));
        }

        if (coverage.GitTwin && coverage.Organization is not null)
        {
            throw new ArgumentException("A .git twin is GitLab's, whose coverage has no organization.", nameof(coverage));
        }

        return coverage.Organization is { } organization && !IsOrganizationName(organization)
            ? []
            : SelectCovered(remotes, coverage);
    }

    /// <summary>
    /// The bound URLs of the remotes' fetch and push URLs, by D278's rules (docs/phase-7a-design.md
    /// 4.4): every URL, in order, that parses as an absolute URI with <paramref name="webBase"/>'s
    /// scheme, host (without case) and port (the default port the same as none), and whose user
    /// information is empty or exactly the account's login (without case); a password, or another
    /// user, leaves it unbound (ruling 1). A binding's <see cref="Url"/> is the URL's text with
    /// <c>user@</c> removed and nothing else changed. A binding is then dropped when another URL of
    /// the remotes, on the same scheme, host and port, names a user other than the login with a
    /// path equal to the binding's or under it at a <c>/</c>: git's key without a user would answer
    /// that user too (4.2). Distinct by <see cref="Url"/> (ordinal), in the remotes' order.
    /// </summary>
    /// <param name="remotes">The remotes' URLs after git's rewrites.</param>
    /// <param name="webBase">The hosting service's web address, whose scheme and host are bound.</param>
    /// <param name="account">The account they are bound to.</param>
    public static IReadOnlyList<CredentialBinding> Select(IReadOnlyList<RemoteEffectiveUrls> remotes, Uri webBase, AccountKey account)
    {
        ArgumentNullException.ThrowIfNull(remotes);
        ArgumentNullException.ThrowIfNull(webBase);
        ArgumentNullException.ThrowIfNull(account);
        return Select(remotes, new GitCoverage(account, webBase));
    }

    /// <summary>7a's rules (D278, docs/phase-7a-design.md 4.4) on <paramref name="webBase"/>'s scheme, host and port.</summary>
    private static List<CredentialBinding> SelectOnOrigin(IReadOnlyList<RemoteEffectiveUrls> remotes, Uri webBase, AccountKey account)
    {
        var urls = remotes.SelectMany(remote => remote.Fetch.Concat(remote.Push))
            .Select(UrlParts.Of)
            .OfType<UrlParts>()
            .Where(url => url.IsOn(webBase))
            .ToList();
        var otherUsers = urls.Where(url => url.User is { Length: > 0 } user && !string.Equals(user, account.Login, StringComparison.OrdinalIgnoreCase)).ToList();
        var bindings = new List<CredentialBinding>();
        foreach (var url in urls)
        {
            if (url.HasPassword
                || (url.User is { Length: > 0 } user && !string.Equals(user, account.Login, StringComparison.OrdinalIgnoreCase))
                || otherUsers.Any(other => other.IsAtOrUnder(url.Path))
                || bindings.Any(binding => string.Equals(binding.Url, url.WithoutUser, StringComparison.Ordinal)))
            {
                continue;
            }

            bindings.Add(new CredentialBinding(url.WithoutUser, account));
        }

        return bindings;
    }

    private const string SchemeSeparator = "://";

    /// <summary>The characters G3.4's form refuses in the printable range (8.2 step 2).</summary>
    private static readonly SearchValues<char> NotInForm = SearchValues.Create("\\?#");

    /// <summary>The reserved characters whose raw form in a key's path git never matches in its credential view (8.2 step 4b).</summary>
    private static readonly SearchValues<char> NeedsCredentialTwin = SearchValues.Create("!$&'()*+,;=:@[]");

    /// <summary>The bytes git's credential view keeps unescaped: unreserved characters and <c>/</c> (8.2 step 5).</summary>
    private static readonly SearchValues<byte> KeptInCredentialView =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~/"u8);

    /// <summary>Azure DevOps' organization grammar's characters (D360).</summary>
    private static readonly SearchValues<char> OrganizationCharacters =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-");

    /// <summary>Azure DevOps' organization grammar, <c>^[A-Za-z0-9-]+$</c> (D360).</summary>
    private static bool IsOrganizationName(ReadOnlySpan<char> name) =>
        name.Length > 0 && !name.ContainsAnyExcept(OrganizationCharacters);

    /// <summary>8.2 steps 1 to 6 for a coverage beyond its origin.</summary>
    private static List<CredentialBinding> SelectCovered(IReadOnlyList<RemoteEffectiveUrls> remotes, GitCoverage coverage)
    {
        var hosts = new List<CoveredHost> { new(coverage.Origin.Scheme, coverage.Origin.Host, coverage.Origin.Port, coverage.Organization) };
        if (coverage.OrganizationHost)
        {
            hosts.Add(new CoveredHost(Uri.UriSchemeHttps, coverage.Organization + ".visualstudio.com", 443, null));
        }

        // Step 5's other users and fail-closed hosts first, then the candidates in the remotes' order.
        var candidates = new List<(CoveredHost Host, UrlParts Url)>();
        foreach (var text in remotes.SelectMany(remote => remote.Fetch.Concat(remote.Push)))
        {
            if (UrlParts.Of(text) is not { } url)
            {
                // D379 (R1): .NET cannot read it, while git's credential code and curl decode what
                // .NET refuses (a percent-encoded host, %64ev.azure.com, dev%2Eazure.com) and may
                // reach any covered host and path in it, so the app cannot know which (V26, D369):
                // an http or https URL that cannot be read leaves nothing on any covered host bound.
                if (HasHttpScheme(text))
                {
                    foreach (var host in hosts)
                    {
                        host.Failed = true;
                    }
                }

                continue;
            }

            // The hunt on the fixed code (2026-11-01): .NET reads a host outside printable ASCII
            // (gitlab<U+FF0E>com, a fullwidth dev.azure.com, U+3002 or U+200B in it) as written, while
            // git's curl may map it into a covered host (2.56's did, 2.43's did not) and, after a
            // redirect into the origin (gitlab.com's 301, dev.azure.com's 302 for http://), git
            // builds the credential again from the ASCII target without the URL's user, so a
            // binding's key answers it with the token. The app cannot know which host git reaches:
            // as for R1's unreadable URL (D379), nothing on any covered host is bound.
            if (HasHttpScheme(text) && (!InPrintableAscii(url.Uri.Host) || !InPrintableAscii(GitHostOf(text))))
            {
                foreach (var host in hosts)
                {
                    host.Failed = true;
                }

                continue;
            }

            // git's credential code reads the user up to the first @ before the first /, ? or #
            // (credential_from_url), through the white space and control characters that end
            // UrlParts' authority (GitCommand.EndsAuthority, for the prose it hides credentials in):
            // in bob<U+00A0>@gitlab.com git reads the user bob<U+00A0>, UrlParts none. A URL git
            // reads another user in counts as another user's out of the form, whatever UrlParts
            // read, so nothing on its host is bound (8.2 step 5, ruling 10, D369).
            var readOtherwise = !string.Equals(GitUserOf(url.Text), url.User, StringComparison.Ordinal);
            var otherUser = readOtherwise || (url.User is { Length: > 0 } user && !IsAccountsUser(user, coverage));
            var exact = true;
            var covered = hosts.FirstOrDefault(host => host.Covers(url.Uri));
            if (covered is null)
            {
                exact = false;

                // git's match_host takes a host with one trailing dot as the host (urlmatch.c), so a
                // binding's key answers a URL there too, whatever its user, while the broker binds
                // no such host: git would ask the app's helper alone, which answers nothing, and
                // D82's order would be lost. Such a URL counts as another user's on the host it
                // names, for the drop or the fail alone, never bound (V23).
                covered = hosts.FirstOrDefault(host => host.CoversWithTrailingDot(url.Uri));
                if (covered is not null)
                {
                    otherUser = true;
                }
                else
                {
                    // D379 (R2): a measured redirect leads http:// and www. into the origin (gitlab.com
                    // 301 and 308, dev.azure.com 302), and git builds the credential again from the
                    // target without the URL's user, so the binding's key answers it there. Such a
                    // URL counts as on the covered host for the drop and the fail, never bound.
                    covered = hosts.FirstOrDefault(host => host.IsRedirectedFrom(url.Uri));
                    if (covered is null)
                    {
                        continue;
                    }
                }
            }

            var inForm = !readOtherwise && InPlainForm(url.Text) && (covered.Organization is null || IsOrganizationName(FirstSegment(url.Path)));
            if (otherUser)
            {
                if (inForm)
                {
                    covered.OtherUsersPaths.Add(CredentialView(url.Path));
                }
                else
                {
                    covered.Failed = true;
                }

                continue;
            }

            if (url.HasPassword)
            {
                continue;
            }

            if (!inForm)
            {
                // Step 5 as WP-C's review amended it: on an origin that serves other organizations, a
                // URL out of the form with no user or the account's may lie under a binding in
                // git's credential view while the server reads another organization in it (X4,
                // X6, V21); the binding's key would answer it with the token, so nothing there is bound.
                covered.Failed |= covered.Organization is not null;
                continue;
            }

            if (exact && (covered.Organization is not { } organization || FirstSegment(url.Path).Equals(organization, StringComparison.OrdinalIgnoreCase)))
            {
                candidates.Add((covered, url));
            }
        }

        var bindings = new List<CredentialBinding>();
        foreach (var (host, url) in candidates.Where(candidate => !candidate.Host.Failed))
        {
            foreach (var bound in WithTwins(url.WithoutUser, coverage.GitTwin))
            {
                var view = CredentialView(PathOf(bound));
                if (host.OtherUsersPaths.Any(other => IsAtOrUnder(other, view) || (coverage.GitTwin && IsAtOrUnder(ProjectOf(other), ProjectOf(view))))
                    || bindings.Any(binding => string.Equals(binding.Url, bound, StringComparison.Ordinal)))
                {
                    continue;
                }

                bindings.Add(new CredentialBinding(bound, coverage.Account) { SharedOriginOwner = host.Organization });
            }
        }

        return bindings;
    }

    /// <summary>Step 3's users beyond none: the login without case and, with <see cref="GitCoverage.HelperUserIsAccounts"/>, <c>x-access-token</c> (ruling 11).</summary>
    private static bool IsAccountsUser(string user, GitCoverage coverage) =>
        string.Equals(user, coverage.Account.Login, StringComparison.OrdinalIgnoreCase)
        || (coverage.HelperUserIsAccounts && string.Equals(user, GitCredential.UserName, StringComparison.Ordinal));

    /// <summary>
    /// A bound URL, then (step 4) its <c>.git</c> twin, and (step 4b) after each of them its
    /// credential twin.
    /// </summary>
    private static List<string> WithTwins(string url, bool gitTwin)
    {
        var urls = new List<string> { url };
        if (gitTwin && GitTwinOf(url) is { } twin)
        {
            urls.Add(twin);
        }

        var withCredentialTwins = new List<string>(urls.Count * 2);
        foreach (var each in urls)
        {
            withCredentialTwins.Add(each);
            if (CredentialTwinOf(each) is { } credentialTwin)
            {
                withCredentialTwins.Add(credentialTwin);
            }
        }

        return withCredentialTwins;
    }

    /// <summary>Step 4: the text without its one trailing <c>/</c>, plus <c>.git</c>, for a path of two or more segments that does not end in <c>.git</c>.</summary>
    private static string? GitTwinOf(string url)
    {
        var withoutSlash = url.EndsWith('/') ? url[..^1] : url;
        var path = PathOf(withoutSlash);
        return path.Length > 1 && path.AsSpan(1).Contains('/') && !path.EndsWith(".git", StringComparison.Ordinal)
            ? withoutSlash + ".git"
            : null;
    }

    /// <summary>Step 4b: the URL with each raw reserved character of its path written <c>%XX</c> in upper-case hex; null when the path holds none.</summary>
    private static string? CredentialTwinOf(string url)
    {
        var pathStart = url.Length - PathOf(url).Length;
        if (url.AsSpan(pathStart).IndexOfAny(NeedsCredentialTwin) < 0)
        {
            return null;
        }

        var twin = new StringBuilder(url, 0, pathStart, url.Length + 16);
        foreach (var c in url.AsSpan(pathStart))
        {
            if (NeedsCredentialTwin.Contains(c))
            {
                twin.Append('%').Append(((byte)c).ToString("X2", CultureInfo.InvariantCulture));
            }
            else
            {
                twin.Append(c);
            }
        }

        return twin.ToString();
    }

    /// <summary>
    /// A path (the text after the authority) in git's credential view (step 5): every <c>%XX</c>
    /// decoded, one trailing <c>/</c> removed, then every byte outside <c>A-Z a-z 0-9 - . _ ~ /</c>
    /// written <c>%XX</c> in upper-case hex, case kept. The path is in <see cref="InPlainForm"/>'s
    /// form, so no escape forms a <c>/</c>, a dot segment, <c>%</c>, <c>\</c> or a control character.
    /// </summary>
    private static string CredentialView(string path)
    {
        var bytes = new List<byte>(path.Length);
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] == '%' && i + 2 < path.Length && TryParseHex(path.AsSpan(i + 1, 2), out var escaped))
            {
                bytes.Add(escaped);
                i += 2;
            }
            else
            {
                bytes.Add((byte)path[i]);
            }
        }

        if (bytes.Count > 0 && bytes[^1] == (byte)'/')
        {
            bytes.RemoveAt(bytes.Count - 1);
        }

        var view = new StringBuilder(bytes.Count);
        foreach (var b in bytes)
        {
            if (KeptInCredentialView.Contains(b))
            {
                view.Append((char)b);
            }
            else
            {
                view.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return view.ToString();
    }

    /// <summary>Whether credential view <paramref name="other"/> is <paramref name="view"/> or lies under it at a <c>/</c> (ordinal, case kept).</summary>
    private static bool IsAtOrUnder(string other, string view) =>
        string.Equals(other, view, StringComparison.Ordinal) || other.StartsWith(view + "/", StringComparison.Ordinal);

    /// <summary>
    /// D379 (R3): GitLab's project of a path in git's credential view (whose one trailing <c>/</c> is
    /// already gone): without one final <c>.git</c> (ordinal) after a non-empty segment. gitlab.com
    /// answers <c>…/p</c> with 301 and <c>…/p.git/</c> with 308 into <c>…/p.git/info/refs</c>, so
    /// another user's URL of the project reaches every spelling's key (r6 6.1).
    /// </summary>
    private static string ProjectOf(string view) =>
        view.Length > GitSuffix.Length + 1 && view[^(GitSuffix.Length + 1)] != '/' && view.EndsWith(GitSuffix, StringComparison.Ordinal)
            ? view[..^GitSuffix.Length]
            : view;

    private const string GitSuffix = ".git";

    /// <summary>
    /// The user git's credential code reads in a URL that has <c>://</c> (<c>credential_from_url</c>):
    /// the text after <c>://</c> up to the first <c>@</c> that comes before the first <c>/</c>,
    /// <c>?</c> or <c>#</c>, cut at a <c>:</c> before that <c>@</c>, its escapes decoded as
    /// <see cref="UrlParts"/> decodes its own; null without such an <c>@</c>. Unlike
    /// <see cref="UrlParts"/>' authority, it does not end at white space or a control character.
    /// </summary>
    private static string? GitUserOf(string text)
    {
        var authority = text.AsSpan(text.IndexOf(SchemeSeparator, StringComparison.Ordinal) + SchemeSeparator.Length);
        var end = authority.IndexOfAny(EndsGitsAuthority);
        if (end >= 0)
        {
            authority = authority[..end];
        }

        var at = authority.IndexOf('@');
        if (at < 0)
        {
            return null;
        }

        var userInfo = authority[..at];
        var colon = userInfo.IndexOf(':');
        return Uri.UnescapeDataString((colon >= 0 ? userInfo[..colon] : userInfo).ToString());
    }

    /// <summary>
    /// The host (with its port) git's credential code reads in a URL that has <c>://</c>
    /// (<c>credential_from_url</c>): the text after <c>://</c>, or after the first <c>@</c> when one
    /// comes before the first <c>/</c>, <c>?</c> or <c>#</c>, up to that character; as written.
    /// </summary>
    private static ReadOnlySpan<char> GitHostOf(string text)
    {
        var authority = text.AsSpan(text.IndexOf(SchemeSeparator, StringComparison.Ordinal) + SchemeSeparator.Length);
        var end = authority.IndexOfAny(EndsGitsAuthority);
        var at = authority.IndexOf('@');
        if (end >= 0 && at > end)
        {
            at = -1;
        }

        return authority[(at + 1)..(end >= 0 ? end : authority.Length)];
    }

    /// <summary>Whether <paramref name="text"/> holds only U+0021 to U+007E.</summary>
    private static bool InPrintableAscii(ReadOnlySpan<char> text) => !text.ContainsAnyExceptInRange('!', '~');

    /// <summary>Where git's credential code ends a URL's authority (<c>strcspn(cp, "/?#")</c>).</summary>
    private static readonly SearchValues<char> EndsGitsAuthority = SearchValues.Create("/?#");

    /// <summary>D379 (R1): whether a URL's scheme, the text before its first <c>:</c>, is <c>http</c> or <c>https</c> without case.</summary>
    private static bool HasHttpScheme(string text)
    {
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        return colon > 0
            && (text.AsSpan(0, colon).Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || text.AsSpan(0, colon).Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The text after the authority of a URL that has no <c>?</c> or <c>#</c>: empty, or from the first <c>/</c> after <c>://</c>.</summary>
    private static string PathOf(string url)
    {
        var authorityStart = url.IndexOf(SchemeSeparator, StringComparison.Ordinal) + SchemeSeparator.Length;
        var pathStart = url.IndexOf('/', authorityStart);
        return pathStart < 0 ? string.Empty : url[pathStart..];
    }

    /// <summary>A path's first segment: the text after its leading <c>/</c> up to the next one; empty for an empty path.</summary>
    private static ReadOnlySpan<char> FirstSegment(string path)
    {
        if (path.Length == 0)
        {
            return [];
        }

        var rest = path.AsSpan(1);
        var slash = rest.IndexOf('/');
        return slash < 0 ? rest : rest[..slash];
    }

    private static bool TryParseHex(ReadOnlySpan<char> digits, out byte value) =>
        byte.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);

    /// <summary>Step 2's escapes: a <c>%</c> only before two hex digits that encode none of <c>.</c>, <c>/</c>, <c>\</c>, <c>%</c>, <c>00</c> to <c>1F</c> and <c>7F</c>.</summary>
    private static bool EscapesInForm(ReadOnlySpan<char> text)
    {
        for (var i = text.IndexOf('%'); i >= 0; i = text.IndexOf('%'))
        {
            if (i + 2 >= text.Length
                || !TryParseHex(text.Slice(i + 1, 2), out var escaped)
                || escaped is (byte)'.' or (byte)'/' or (byte)'\\' or (byte)'%' or < 0x20 or 0x7F)
            {
                return false;
            }

            text = text[(i + 3)..];
        }

        return true;
    }

    /// <summary>Step 2's path, from its leading <c>/</c>: segments none <c>.</c> or <c>..</c>, none empty but a trailing one, each valid UTF-8 once decoded.</summary>
    private static bool PathInForm(ReadOnlySpan<char> path)
    {
        var rest = path[1..];
        while (true)
        {
            var slash = rest.IndexOf('/');
            var segment = slash < 0 ? rest : rest[..slash];
            if ((segment.Length == 0 && slash >= 0) || segment is "." or ".." || !DecodesToUtf8(segment))
            {
                return false;
            }

            if (slash < 0)
            {
                return true;
            }

            rest = rest[(slash + 1)..];
        }
    }

    /// <summary>Whether a segment whose escapes are in the form (<see cref="EscapesInForm"/>) is valid UTF-8 once they are decoded.</summary>
    private static bool DecodesToUtf8(ReadOnlySpan<char> segment)
    {
        if (!segment.Contains('%'))
        {
            return true;
        }

        var bytes = new byte[segment.Length];
        var count = 0;
        for (var i = 0; i < segment.Length; i++)
        {
            if (segment[i] == '%' && TryParseHex(segment.Slice(i + 1, 2), out var escaped))
            {
                bytes[count++] = escaped;
                i += 2;
            }
            else
            {
                bytes[count++] = (byte)segment[i];
            }
        }

        return Utf8.IsValid(bytes.AsSpan(0, count));
    }

    /// <summary>A host a coverage binds on (8.2 step 1), with step 5's state during one <see cref="Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/>.</summary>
    private sealed class CoveredHost(string scheme, string host, int port, string? organization)
    {
        /// <summary>On the origin with an organization: the first segment it requires, and the bindings' owner; null elsewhere.</summary>
        public string? Organization { get; } = organization;

        /// <summary>An http or https URL could not be read (D379) or has a host outside printable ASCII, or a URL here is not in the form as step 5 says: nothing here is bound (ruling 10).</summary>
        public bool Failed { get; set; }

        /// <summary>Other users' paths here, in git's credential view.</summary>
        public List<string> OtherUsersPaths { get; } = [];

        public bool Covers(Uri uri) =>
            string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase)
            && uri.Port == port;

        /// <summary>The host as git's <c>match_host</c> reads it in a URL whose host has one trailing dot (<c>dev.azure.com.</c>), which .NET keeps.</summary>
        public bool CoversWithTrailingDot(Uri uri) =>
            string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase)
            && uri.Host.EndsWith('.')
            && uri.Host.AsSpan(0, uri.Host.Length - 1).Equals(host, StringComparison.OrdinalIgnoreCase)
            && uri.Port == port;

        /// <summary>
        /// D379 (R2): a URL a measured redirect may lead into this host's origin: scheme <c>http</c>
        /// or <c>https</c>, any port, and a host that, without case and with one trailing <c>.</c>
        /// removed, is this host or <c>www.</c> followed by it.
        /// </summary>
        public bool IsRedirectedFrom(Uri uri)
        {
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var name = uri.Host.AsSpan();
            if (name.EndsWith('.'))
            {
                name = name[..^1];
            }

            return name.Equals(host, StringComparison.OrdinalIgnoreCase)
                || (name.StartsWith(WwwPrefix, StringComparison.OrdinalIgnoreCase) && name[WwwPrefix.Length..].Equals(host, StringComparison.OrdinalIgnoreCase));
        }

        private const string WwwPrefix = "www.";
    }

    /// <summary>What <see cref="Select"/> reads of one URL: its parts as written, the path's case kept.</summary>
    private sealed record UrlParts(string Text, Uri Uri, string? User, bool HasPassword, string Path, string WithoutUser)
    {
        public static UrlParts? Of(string text)
        {
            const string SchemeEnd = "://";
            var schemeEnd = text.IndexOf(SchemeEnd, StringComparison.Ordinal);
            if (schemeEnd <= 0 || !Uri.TryCreate(text, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var authorityStart = schemeEnd + SchemeEnd.Length;
            var authorityEnd = authorityStart;
            while (authorityEnd < text.Length && !GitCommand.EndsAuthority(text[authorityEnd]))
            {
                authorityEnd++;
            }

            var at = authorityEnd == authorityStart ? -1 : text.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart);
            string? user = null;
            var hasPassword = false;
            var withoutUser = text;
            if (at >= authorityStart)
            {
                var userInfo = text[authorityStart..at];
                var colon = userInfo.IndexOf(':', StringComparison.Ordinal);
                hasPassword = colon >= 0;
                user = Uri.UnescapeDataString(colon >= 0 ? userInfo[..colon] : userInfo);
                withoutUser = text[..authorityStart] + text[(at + 1)..];
            }

            var pathEnd = text.IndexOfAny(['?', '#'], authorityEnd);
            var path = text[authorityEnd..(pathEnd < 0 ? text.Length : pathEnd)];
            return new UrlParts(text, uri, user, hasPassword, path, withoutUser);
        }

        public bool IsOn(Uri webBase) =>
            string.Equals(Uri.Scheme, webBase.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Uri.Host, webBase.Host, StringComparison.OrdinalIgnoreCase)
            && Uri.Port == webBase.Port;

        /// <summary>This URL's path is <paramref name="path"/> or lies under it at a <c>/</c> (case counts, as for git).</summary>
        public bool IsAtOrUnder(string path) =>
            string.Equals(Path, path, StringComparison.Ordinal)
            || Path.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal);
    }
}

/// <summary>
/// The credential git gets for a bound URL (D255): the user name <see cref="UserName"/> and the
/// account's token. Its text never shows the token.
/// </summary>
public sealed class GitCredential(string token)
{
    /// <summary>The user name git sends with the token: never the login (docs/phase-7a-design.md 0.2).</summary>
    public const string UserName = "x-access-token";

    public string Token { get; } = token;

    public override string ToString() => "GitCredential(***)";
}

/// <summary>
/// Where the app's helper gets a bound URL's credential (D255, ruling 6): the hosting session.
/// Bound URLs only, and never through UI.
/// </summary>
public interface IGitCredentialSource
{
    /// <summary>
    /// git's <c>get</c>: the account's token, or null when signed out, when the account is not used
    /// for git, when the token was refused, or when it is still unread after 10 s.
    /// </summary>
    Task<GitCredential?> GetAsync(AccountKey account, CancellationToken cancellationToken);

    /// <summary>git's <c>erase</c> to the app's helper (ruling 4); the session checks the token before it marks the account (D330).</summary>
    void Refused(AccountKey account);
}

/// <summary>A credential source without accounts: no credential for any URL, and a refusal changes nothing.</summary>
public sealed class NullGitCredentialSource : IGitCredentialSource
{
    private NullGitCredentialSource()
    {
    }

    public static NullGitCredentialSource Instance { get; } = new();

    public Task<GitCredential?> GetAsync(AccountKey account, CancellationToken cancellationToken) => Task.FromResult<GitCredential?>(null);

    public void Refused(AccountKey account)
    {
    }
}
