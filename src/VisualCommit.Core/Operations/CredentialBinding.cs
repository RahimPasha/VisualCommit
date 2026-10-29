using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;

namespace VisualCommit.Core.Operations;

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
