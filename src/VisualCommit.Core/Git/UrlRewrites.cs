namespace VisualCommit.Core.Git;

/// <summary>
/// One of git's URL rewrites (docs/phase-7a-design.md 1.2): <c>url.&lt;Base&gt;.insteadOf =
/// &lt;Prefix&gt;</c>, or <c>pushInsteadOf</c> when <paramref name="ForPush"/>.
/// </summary>
/// <param name="Base">The URL a matching prefix becomes.</param>
/// <param name="Prefix">The prefix it replaces.</param>
/// <param name="ForPush">A <c>pushInsteadOf</c> rule.</param>
public sealed record UrlRewrite(string Base, string Prefix, bool ForPush);

/// <summary>The URLs git uses for one remote after its rewrites: the fetch URLs and the push URLs.</summary>
/// <param name="Remote">The remote's name.</param>
/// <param name="Fetch">The URLs git fetches from.</param>
/// <param name="Push">The URLs git pushes to.</param>
public sealed record RemoteEffectiveUrls(string Remote, IReadOnlyList<string> Fetch, IReadOnlyList<string> Push);

/// <summary>
/// The repository's URL rewrites, as one <c>git config --null --get-regexp ^url\.</c> reads them
/// (docs/phase-7a-design.md 1.2, 4.3). Read only while a hosting account is signed in.
/// </summary>
/// <param name="Rules">The rules in configuration order.</param>
public sealed record UrlRewrites(IReadOnlyList<UrlRewrite> Rules)
{
    /// <summary>No rules: every URL is used as configured.</summary>
    public static UrlRewrites None { get; } = new([]);

    /// <summary>
    /// The rules of <c>git config --null --get-regexp ^url\.</c>'s entries.
    /// </summary>
    /// <remarks>Still no rules: WP-C reads them (docs/phase-7a-design.md 4.3).</remarks>
    public static UrlRewrites Parse(IEnumerable<(string Key, string Value)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return None;
    }

    /// <summary>
    /// The URLs git uses for each remote of <paramref name="urls"/>, in the order the remotes first
    /// appear, after git's rules (4.3).
    /// </summary>
    /// <remarks>
    /// Still without rewrites: every remote's <c>url</c> values are its fetch URLs, and its
    /// <c>pushurl</c> values, or its <c>url</c> values without one, its push URLs. WP-C applies the
    /// rules (docs/phase-7a-design.md 4.3).
    /// </remarks>
    public IReadOnlyList<RemoteEffectiveUrls> Apply(IReadOnlyList<RemoteUrl> urls)
    {
        ArgumentNullException.ThrowIfNull(urls);
        var remotes = new List<string>();
        foreach (var url in urls)
        {
            if (!remotes.Contains(url.Remote, StringComparer.Ordinal))
            {
                remotes.Add(url.Remote);
            }
        }

        var result = new List<RemoteEffectiveUrls>(remotes.Count);
        foreach (var remote in remotes)
        {
            var fetch = urls.Where(u => !u.IsPushUrl && string.Equals(u.Remote, remote, StringComparison.Ordinal)).Select(u => u.Url).ToList();
            var push = urls.Where(u => u.IsPushUrl && string.Equals(u.Remote, remote, StringComparison.Ordinal)).Select(u => u.Url).ToList();
            result.Add(new RemoteEffectiveUrls(remote, fetch, push.Count > 0 ? push : fetch));
        }

        return result;
    }
}
