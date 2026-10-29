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
    /// <summary>The bound URLs of the remotes' fetch and push URLs, by D278's rules (docs/phase-7a-design.md 4.4).</summary>
    /// <param name="remotes">The remotes' URLs after git's rewrites.</param>
    /// <param name="webBase">The hosting service's web address, whose scheme and host are bound.</param>
    /// <param name="account">The account they are bound to.</param>
    public static IReadOnlyList<CredentialBinding> Select(IReadOnlyList<RemoteEffectiveUrls> remotes, Uri webBase, AccountKey account) =>
        throw new NotImplementedException("WP-C builds the selection of bound URLs (docs/phase-7a-design.md 4.4).");
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
