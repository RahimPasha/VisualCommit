using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="UrlRewrites.Parse"/> and <see cref="UrlRewrites.Apply"/> on each fact of
/// docs/phase-7a-design.md 4.2, as measured on git 2.56. <see cref="UrlRewritesParityTests"/>
/// compares the same rules with git's own <c>remote get-url</c>.
/// </summary>
public class UrlRewritesTests
{
    [Fact]
    public void Parse_takes_insteadOf_and_pushInsteadOf_without_case_and_keeps_the_base_as_written()
    {
        var rules = UrlRewrites.Parse(
        [
            ("url.https://GitHub.com/A.b/.insteadof", "gh:"),
            ("url.ssh://git@github.com/.pushinsteadof", "https://github.com/"),
            ("URL.https://x.example/.InsteadOf", "x:"),
            ("url.https://y.example/.insteadof", ""),
            ("url.https://z.example/.insteadof", null!),
            ("url..insteadof", "nothing:"),
            ("url.https://w.example/.other", "w:"),
            ("remote.origin.url", "https://github.com/o/r.git"),
        ]);

        Assert.Equal(
            [
                new UrlRewrite("https://GitHub.com/A.b/", "gh:", false),
                new UrlRewrite("ssh://git@github.com/", "https://github.com/", true),
                new UrlRewrite("https://x.example/", "x:", false),
                new UrlRewrite("https://y.example/", "", false),
            ],
            rules.Rules);
        Assert.Same(UrlRewrites.None, UrlRewrites.Parse([]));
    }

    [Fact]
    public void InsteadOf_is_a_plain_case_sensitive_prefix_and_the_longest_wins()
    {
        var rewrites = Rules(
            ("https://mirror.example/", "https://github.com/chain", false),
            ("https://longer.example/", "https://github.com/chainx/", false),
            ("https://never.example/", "HTTPS://github.com/", false));

        Assert.Equal(
            [
                Remote("a", ["https://mirror.example/z/q"], ["https://mirror.example/z/q"]),
                Remote("b", ["https://longer.example/y"], ["https://longer.example/y"]),
                Remote("c", ["https://github.com/other"], ["https://github.com/other"]),
            ],
            rewrites.Apply([Url("a", "https://github.com/chainz/q"), Url("b", "https://github.com/chainx/y"), Url("c", "https://github.com/other")]),
            Same);
    }

    [Fact]
    public void A_tie_goes_to_the_base_configured_first_then_to_its_earlier_value()
    {
        var rewrites = Rules(
            ("https://first.example/", "tie:", false),
            ("https://second.example/", "tie:", false),
            ("https://first.example/", "t:", false),
            ("https://third.example/", "t:", false));

        var urls = rewrites.Apply([Url("a", "tie:x"), Url("b", "t:x")]);

        Assert.Equal(["https://first.example/x", "https://first.example/x"], urls.Select(remote => remote.Fetch.Single()));
    }

    [Fact]
    public void A_base_seen_first_wins_a_tie_even_when_its_tying_value_comes_later()
    {
        // git groups the values by base, in the order the bases first appear.
        var rewrites = Rules(
            ("https://first.example/", "other:", false),
            ("https://second.example/", "tie:", false),
            ("https://first.example/", "tie:", false));

        Assert.Equal("https://first.example/x", rewrites.Apply([Url("a", "tie:x")]).Single().Fetch.Single());
    }

    [Fact]
    public void An_empty_prefix_matches_every_url_unless_a_longer_one_does_and_nothing_chains()
    {
        var rewrites = Rules(
            ("https://empty.example/", "", false),
            ("https://github.com/", "gh:", false),
            ("https://chained.example/", "https://github.com/", false));

        Assert.Equal(
            [
                Remote("a", ["https://empty.example/https://x.example/r"], ["https://empty.example/https://x.example/r"]),
                Remote("b", ["https://github.com/o/r"], ["https://github.com/o/r"]),
            ],
            rewrites.Apply([Url("a", "https://x.example/r"), Url("b", "gh:o/r")]),
            Same);
    }

    [Fact]
    public void A_remote_without_pushurl_pushes_to_the_pushInsteadOf_rewrites_of_its_urls_alone()
    {
        var rewrites = Rules(
            ("https://github.com/", "gh:", false),
            ("ssh://git@github.com/", "https://github.com/", true),
            ("https://first.example/", "tie:", false));

        Assert.Equal(
            [
                // pushInsteadOf applies to the configured value, never after insteadOf.
                Remote("origin", ["https://github.com/o/r.git"], ["https://github.com/o/r.git"]),
                Remote("direct", ["https://github.com/o/d.git"], ["ssh://git@github.com/o/d.git"]),
                Remote("multi", ["https://first.example/a", "https://github.com/o/m.git"], ["ssh://git@github.com/o/m.git"]),

                // A plain prefix: it does not rewrite a URL with a user part.
                Remote("alice", ["https://alice@github.com/o/a.git"], ["https://alice@github.com/o/a.git"]),
            ],
            rewrites.Apply(
            [
                Url("origin", "gh:o/r.git"),
                Url("direct", "https://github.com/o/d.git"),
                Url("multi", "tie:a"),
                Url("multi", "https://github.com/o/m.git"),
                Url("alice", "https://alice@github.com/o/a.git"),
            ]),
            Same);
    }

    [Fact]
    public void A_remote_with_pushurl_pushes_to_its_values_after_insteadOf_and_one_without_url_has_no_fetch_url()
    {
        var rewrites = Rules(
            ("https://github.com/", "gh:", false),
            ("ssh://git@github.com/", "https://github.com/", true));

        Assert.Equal(
            [
                Remote("withpush", ["https://github.com/o/w.git"], ["https://github.com/o/w-push.git", "https://x.example/p"]),
                Remote("pushonly", [], ["https://github.com/o/p.git"]),
            ],
            rewrites.Apply(
            [
                Url("withpush", "https://github.com/o/w.git"),
                Url("withpush", "gh:o/w-push.git", push: true),
                Url("pushonly", "gh:o/p.git", push: true),
                Url("withpush", "https://x.example/p", push: true),
            ]),
            Same);
    }

    [Fact]
    public void Without_rules_the_urls_are_as_configured_in_the_order_the_remotes_first_appear()
    {
        Assert.Equal(
            [
                Remote("upstream", ["https://github.com/u/r.git"], ["https://github.com/u/r.git"]),
                Remote("origin", ["https://github.com/o/r.git"], ["https://push.example/o/r.git"]),
            ],
            UrlRewrites.None.Apply(
            [
                Url("upstream", "https://github.com/u/r.git"),
                Url("origin", "https://push.example/o/r.git", push: true),
                Url("origin", "https://github.com/o/r.git"),
            ]),
            Same);
    }

    private static readonly IEqualityComparer<RemoteEffectiveUrls> Same = EqualityComparer<RemoteEffectiveUrls>.Create(
        (a, b) => a is not null && b is not null && a.Remote == b.Remote && a.Fetch.SequenceEqual(b.Fetch) && a.Push.SequenceEqual(b.Push),
        remote => remote.Remote.GetHashCode(StringComparison.Ordinal));

    private static UrlRewrites Rules(params (string Base, string Prefix, bool ForPush)[] rules) =>
        new([.. rules.Select(rule => new UrlRewrite(rule.Base, rule.Prefix, rule.ForPush))]);

    private static RemoteUrl Url(string remote, string url, bool push = false) => new(remote, url, push);

    private static RemoteEffectiveUrls Remote(string remote, string[] fetch, string[] push) => new(remote, fetch, push);
}
