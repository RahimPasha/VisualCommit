using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="CredentialBinding.Select"/> (docs/phase-7a-design.md 4.4, D278): which of a
/// repository's URLs git asks the account's token for, and the binding's URL text.
/// </summary>
public class CredentialBindingSelectTests
{
    private static readonly Uri GitHub = new("https://github.com/");
    private static readonly AccountKey Octocat = new(HostingProvider.GitHub, "github.com", "octocat");

    [Theory]
    [InlineData("https://github.com/o/r.git", "https://github.com/o/r.git")]
    [InlineData("https://octocat@github.com/o/r.git", "https://github.com/o/r.git")]
    [InlineData("https://OctoCat@github.com/o/r.git", "https://github.com/o/r.git")]
    [InlineData("https://GitHub.com/O/R", "https://GitHub.com/O/R")]
    [InlineData("https://github.com:443/o/r.git", "https://github.com:443/o/r.git")]
    [InlineData("HTTPS://octocat@github.com/o/r.git?x=1#y", "HTTPS://github.com/o/r.git?x=1#y")]
    [InlineData("https://github.com", "https://github.com")]
    public void A_url_of_the_web_host_without_a_user_or_with_the_login_is_bound_with_its_text_kept_but_for_the_user(string url, string bound)
    {
        var binding = Assert.Single(CredentialBinding.Select([Fetch("origin", url)], GitHub, Octocat));

        Assert.Equal(new CredentialBinding(bound, Octocat), binding);
    }

    [Theory]
    [InlineData("https://alice@github.com/o/r.git")]
    [InlineData("https://octocat:secret@github.com/o/r.git")]
    [InlineData("https://octocat:@github.com/o/r.git")]
    [InlineData("http://github.com/o/r.git")]
    [InlineData("ssh://git@github.com/o/r.git")]
    [InlineData("git@github.com:o/r.git")]
    [InlineData("https://gitlab.com/o/r.git")]
    [InlineData("https://github.com.evil.example/o/r.git")]
    [InlineData("https://github.com:8443/o/r.git")]
    [InlineData("/local/path/r.git")]
    [InlineData("gh:o/r")]
    public void Another_user_a_password_another_scheme_host_or_port_is_not_bound(string url)
    {
        Assert.Empty(CredentialBinding.Select([Fetch("origin", url)], GitHub, Octocat));
    }

    [Fact]
    public void Push_urls_are_bound_in_the_remotes_order_once_each()
    {
        var remotes = new[]
        {
            new RemoteEffectiveUrls("origin", ["https://github.com/o/r.git"], ["https://octocat@github.com/o/r.git", "https://github.com/o/push.git"]),
            new RemoteEffectiveUrls("upstream", ["https://github.com/u/r.git"], ["https://github.com/u/r.git"]),
        };

        Assert.Equal(
            ["https://github.com/o/r.git", "https://github.com/o/push.git", "https://github.com/u/r.git"],
            CredentialBinding.Select(remotes, GitHub, Octocat).Select(binding => binding.Url));
    }

    [Fact]
    public void The_fakes_exact_port_is_bound_and_another_port_of_its_host_is_not()
    {
        var fake = new Uri("http://127.0.0.1:47395/");
        var account = new AccountKey(HostingProvider.GitHub, "127.0.0.1:47395", "octocat");

        var bindings = CredentialBinding.Select(
            [Fetch("origin", "http://127.0.0.1:47395/octocat/hello.git"), Fetch("other", "http://127.0.0.1:47396/octocat/hello.git"), Fetch("https", "https://127.0.0.1:47395/x.git")],
            fake,
            account);

        Assert.Equal([new CredentialBinding("http://127.0.0.1:47395/octocat/hello.git", account)], bindings);
    }

    [Fact]
    public void A_path_that_another_users_url_shares_or_lies_under_is_dropped()
    {
        // A credential.<url> key without a user also answers alice's URL (4.2), so a binding at or
        // above her path would give her requests the account's token.
        var bindings = CredentialBinding.Select(
            [
                Fetch("alice", "https://alice@github.com/o/r/sub/deeper"),
                Fetch("same", "https://github.com/o/r/sub/deeper"),
                Fetch("parent", "https://github.com/o/r/sub"),
                Fetch("slash", "https://github.com/o/r/"),
                Fetch("sibling", "https://github.com/o/rx"),
                Fetch("child", "https://github.com/o/r/sub/deeper/child"),
                Fetch("case", "https://github.com/O/r/sub"),
                Fetch("login", "https://OCTOCAT@github.com/o/r/sub/deeper/child2"),
            ],
            GitHub,
            Octocat);

        Assert.Equal(
            ["https://github.com/o/rx", "https://github.com/o/r/sub/deeper/child", "https://github.com/O/r/sub", "https://github.com/o/r/sub/deeper/child2"],
            bindings.Select(binding => binding.Url));
    }

    [Theory]
    [InlineData("https://alice@github.com/o/r", new[] { "https://github.com/o/r/sub", "https://github.com/o/rx" })]
    [InlineData("https://alice@github.com/o/r/sub", new[] { "https://github.com/o/rx" })]
    public void Beside_alices_url_its_path_and_those_above_it_are_dropped_and_o_rx_is_kept(string alice, string[] bound)
    {
        // D278: dropped when alice's path is equal to the binding's or under it. A key for /o/r/sub
        // does not match alice's requests to /o/r/info/refs (a path prefix ending at /, 4.2), so it
        // stays bound beside her /o/r; beside her /o/r/sub it would answer her and goes.
        var bindings = CredentialBinding.Select(
            [Fetch("alice", alice), Fetch("a", "https://github.com/o/r"), Fetch("b", "https://github.com/o/r/sub"), Fetch("c", "https://github.com/o/rx")],
            GitHub,
            Octocat);

        Assert.Equal(bound, bindings.Select(binding => binding.Url));
    }

    [Fact]
    public void Another_users_url_on_another_host_drops_nothing()
    {
        var bindings = CredentialBinding.Select(
            [Fetch("alice", "https://alice@gitlab.com/o/r"), Fetch("a", "https://github.com/o/r")],
            GitHub,
            Octocat);

        Assert.Equal(["https://github.com/o/r"], bindings.Select(binding => binding.Url));
    }

    private static RemoteEffectiveUrls Fetch(string remote, string url) => new(remote, [url], [url]);
}
