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
    /// The rules of <c>git config --null --get-regexp ^url\.</c>'s entries (4.3): every key that
    /// starts <c>url.</c> and ends <c>.insteadof</c> or <c>.pushinsteadof</c> (without case), its
    /// base the text between, dots and case kept; an entry without a value is skipped. Rules keep
    /// the entries' order.
    /// </summary>
    public static UrlRewrites Parse(IEnumerable<(string Key, string Value)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        const string Prefix = "url.";
        const string Fetch = ".insteadof";
        const string Push = ".pushinsteadof";
        var rules = new List<UrlRewrite>();
        foreach (var (key, value) in entries)
        {
            if (key is null || value is null || !key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // .pushinsteadof ends with .insteadof's letters too, so it is asked first.
            var forPush = key.EndsWith(Push, StringComparison.OrdinalIgnoreCase);
            var suffix = forPush ? Push : Fetch;
            if ((forPush || key.EndsWith(Fetch, StringComparison.OrdinalIgnoreCase)) && key.Length > Prefix.Length + suffix.Length)
            {
                rules.Add(new UrlRewrite(key[Prefix.Length..^suffix.Length], value, forPush));
            }
        }

        return rules.Count == 0 ? None : new UrlRewrites(rules);
    }

    /// <summary>
    /// The URLs git uses for each remote of <paramref name="urls"/>, in the order the remotes first
    /// appear, after git's rules (docs/phase-7a-design.md 4.2, 4.3, as measured on 2.56): a fetch
    /// URL is each <c>url</c> after <c>insteadOf</c>. A remote with a <c>pushurl</c> pushes to its
    /// values after <c>insteadOf</c>; one without pushes to the <c>pushInsteadOf</c> rewrites of
    /// its configured <c>url</c> values when any has one (those alone), else to its fetch URLs.
    /// <c>pushInsteadOf</c> never applies after <c>insteadOf</c>, and no rewrite is applied twice.
    /// A remote without <c>url</c> has no fetch URL.
    /// </summary>
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
            var configured = urls.Where(u => !u.IsPushUrl && string.Equals(u.Remote, remote, StringComparison.Ordinal)).Select(u => u.Url).ToList();
            var pushUrls = urls.Where(u => u.IsPushUrl && string.Equals(u.Remote, remote, StringComparison.Ordinal)).Select(u => u.Url).ToList();
            var fetch = configured.Select(url => Rewrite(url, forPush: false) ?? url).ToList();
            List<string> push;
            if (pushUrls.Count > 0)
            {
                push = [.. pushUrls.Select(url => Rewrite(url, forPush: false) ?? url)];
            }
            else
            {
                var pushRewrites = configured.Select(url => Rewrite(url, forPush: true)).OfType<string>().ToList();
                push = pushRewrites.Count > 0 ? pushRewrites : fetch;
            }

            result.Add(new RemoteEffectiveUrls(remote, fetch, push));
        }

        return result;
    }

    /// <summary>
    /// <paramref name="url"/> after the rule of its kind with the longest prefix, a plain
    /// case-sensitive one (an empty prefix matches every URL), ties going to the base first seen and
    /// then to its earlier value, as git groups the values by base; null when no rule matches.
    /// </summary>
    private string? Rewrite(string url, bool forPush)
    {
        var ofKind = Rules.Where(rule => rule.ForPush == forPush).ToList();
        UrlRewrite? best = null;
        foreach (var @base in ofKind.Select(rule => rule.Base).Distinct(StringComparer.Ordinal))
        {
            foreach (var rule in ofKind.Where(rule => string.Equals(rule.Base, @base, StringComparison.Ordinal)))
            {
                if (url.StartsWith(rule.Prefix, StringComparison.Ordinal) && (best is null || rule.Prefix.Length > best.Prefix.Length))
                {
                    best = rule;
                }
            }
        }

        return best is null ? null : best.Base + url[best.Prefix.Length..];
    }
}
