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
    /// g3 G3.4's form on the URL's text (docs/phase-7b-design.md 8.2 step 2): the one rule that
    /// <see cref="Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/> and Azure DevOps'
    /// recognition both apply. WP-C builds it.
    /// </summary>
    /// <exception cref="NotSupportedException">Always, until WP-C.</exception>
    public static bool InPlainForm(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        throw new NotSupportedException("G3.4's form is WP-C's (docs/phase-7b-design.md 8.2).");
    }

    /// <summary>
    /// The bound URLs of the remotes' fetch and push URLs under <paramref name="coverage"/>
    /// (D359, docs/phase-7b-design.md 8.2). With only <see cref="GitCoverage.Origin"/> set, 7a's
    /// rules on it, byte for byte: see <see cref="Select(IReadOnlyList{RemoteEffectiveUrls}, Uri, AccountKey)"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">A member beyond <see cref="GitCoverage.Origin"/> is set: 7b's rules are WP-C's.</exception>
    public static IReadOnlyList<CredentialBinding> Select(IReadOnlyList<RemoteEffectiveUrls> remotes, GitCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(remotes);
        ArgumentNullException.ThrowIfNull(coverage);
        if (coverage.IsBeyondOrigin)
        {
            throw new NotSupportedException("A coverage beyond its origin is WP-C's (docs/phase-7b-design.md 8.2).");
        }

        return SelectOnOrigin(remotes, coverage.Origin, coverage.Account);
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

    /// <summary>What <see cref="Select"/> reads of one URL: its parts as written, the path's case kept.</summary>
    private sealed record UrlParts(Uri Uri, string? User, bool HasPassword, string Path, string WithoutUser)
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
            return new UrlParts(uri, user, hasPassword, path, withoutUser);
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
