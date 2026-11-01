using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="CredentialBinding.Select"/> (docs/phase-7a-design.md 4.4, D278): which of a
/// repository's URLs git asks the account's token for, and the binding's URL text. Since 7b
/// (docs/phase-7b-design.md 8.2, 8.7; D369) also through a <see cref="GitCoverage"/>: 7a's cases
/// through both overloads, GitLab's <c>.git</c> twin, and the drop in git's credential view, whose
/// cases <see cref="CoverageGitMatchTests"/> runs through real git.
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

    private const string GitLabWeb = "https://gitlab.com";
    private const string AzureRepos = "https://dev.azure.com/myorg/p/_git";
    private const string EvilRepos = "https://evil@dev.azure.com/myorg/p/_git";

    private static readonly AccountKey Ada = new(HostingProvider.GitLab, "gitlab.com", "Ada");

    /// <summary>GitLab's coverage of <see cref="Ada"/> (design 8.2: the <c>.git</c> twin and <c>x-access-token</c>).</summary>
    private static readonly GitCoverage GitLab = new(Ada, new Uri("https://gitlab.com/")) { GitTwin = true, HelperUserIsAccounts = true };

    /// <summary>
    /// The credential view's cases (design 8.7): a URL bound alone, another user's URL beside it,
    /// and whether that URL drops something of what the first binds. <see cref="CoverageGitMatchTests"/>
    /// checks that git asks the app's helper for the other user's URL with exactly these pairs.
    /// </summary>
    public static TheoryData<string, string, string, bool> CredentialViewCases => new()
    {
        { "AzureDevOps", $"{AzureRepos}/r%28x%29", $"{EvilRepos}/r(x)", true },
        { "AzureDevOps", $"{AzureRepos}/r(x)", $"{EvilRepos}/r%28x%29", true },
        { "AzureDevOps", $"{AzureRepos}/r(x)", $"{EvilRepos}/r(x%29", true },
        { "AzureDevOps", $"{AzureRepos}/r^x", $"{EvilRepos}/r%5Ex", true },
        { "AzureDevOps", $"{AzureRepos}/r%5Ex", $"{EvilRepos}/r^x", true },
        { "AzureDevOps", $"{AzureRepos}/r%5ex", $"{EvilRepos}/r%5Ex", true },
        { "AzureDevOps", $"{AzureRepos}/r{{x}}", $"{EvilRepos}/r%7Bx%7D", true },
        { "AzureDevOps", $"{AzureRepos}/r/", $"{EvilRepos}/r", true },
        { "AzureDevOps", $"{AzureRepos}/r(x)", $"{EvilRepos}/R(x)", false },
        { "AzureDevOps", $"{AzureRepos}/r", "https://otherorg@dev.azure.com/otherorg/My%20Project/_git/r", false },
        { "AzureDevOps", $"{AzureRepos}/r", $"{EvilRepos}/R", false },
        { "GitLab", $"{GitLabWeb}/g/p", "https://alice@gitlab.com/g/p.git", true },
        { "GitLab", $"{GitLabWeb}/g/p.git", "https://alice@gitlab.com/g/%70.git", true },
        { "GitLab", $"{GitLabWeb}/g/p/", "https://bob@gitlab.com/g/p", true },
        { "GitLab", $"{GitLabWeb}/r/", "https://evil@gitlab.com/r", true },
        { "GitLab", $"{GitLabWeb}/g/p", "https://alice@gitlab.com/h/p.git", false },
        { "GitLab", $"{GitLabWeb}/g/p", "https://bob@gitlab.com/john.doe/p.git", false },
    };

    /// <summary>7a's cases (the lists above), for the coverage overload.</summary>
    public static TheoryData<string[]> SevenACases => new()
    {
        new[] { "https://github.com/o/r.git", "https://octocat@github.com/o/r.git", "https://OctoCat@github.com/o/r.git", "https://GitHub.com/O/R" },
        new[] { "https://github.com:443/o/r.git", "HTTPS://octocat@github.com/o/r.git?x=1#y", "https://github.com" },
        new[] { "https://alice@github.com/o/r.git", "https://octocat:secret@github.com/o/r.git", "https://octocat:@github.com/o/r.git", "http://github.com/o/r.git" },
        new[] { "ssh://git@github.com/o/r.git", "git@github.com:o/r.git", "https://gitlab.com/o/r.git", "https://github.com.evil.example/o/r.git", "https://github.com:8443/o/r.git", "/local/path/r.git", "gh:o/r" },
        new[] { "https://alice@github.com/o/r/sub/deeper", "https://github.com/o/r/sub/deeper", "https://github.com/o/r/sub", "https://github.com/o/r/", "https://github.com/o/rx", "https://github.com/o/r/sub/deeper/child", "https://github.com/O/r/sub", "https://OCTOCAT@github.com/o/r/sub/deeper/child2" },
        new[] { "https://alice@github.com/o/r", "https://github.com/o/r", "https://github.com/o/r/sub", "https://github.com/o/rx" },
        new[] { "https://alice@gitlab.com/o/r", "https://github.com/o/r" },
        new[] { "http://127.0.0.1:47395/octocat/hello.git", "http://127.0.0.1:47396/octocat/hello.git", "https://127.0.0.1:47395/x.git" },
        new[] { "https://evil@github.com/o/r/%52", "https://github.com/o/r/R", "https://github.com/o/r/./R", "https://github.com/x/../o/r" },
    };

    /// <summary>The coverage a <see cref="CredentialViewCases"/> row names (and <c>FakeGitLab</c>, the GitLab fake's on <c>http://127.0.0.1:2/</c>).</summary>
    public static GitCoverage CoverageOf(string provider) => provider switch
    {
        "AzureDevOps" => AzureDevOpsCoverageVectors.Coverage,
        "GitLab" => GitLab,
        "FakeGitLab" => FakeGitLabCoverage(),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };

    [Theory]
    [MemberData(nameof(SevenACases))]
    public void Seven_a_cases_give_the_same_bindings_through_both_overloads(string[] urls)
    {
        var remotes = urls.Select((url, i) => Fetch("r" + i, url)).ToList();

        foreach (var (origin, account) in new[] { (GitHub, Octocat), (new Uri("http://127.0.0.1:47395/"), new AccountKey(HostingProvider.GitHub, "127.0.0.1:47395", "octocat")) })
        {
            var coverage = CredentialBinding.Select(remotes, new GitCoverage(account, origin));
            Assert.Equal(CredentialBinding.Select(remotes, origin, account), coverage);
            Assert.All(coverage, binding => Assert.Null(binding.SharedOriginOwner));
        }
    }

    [Theory]
    [InlineData("https://gitlab.com/g/p.git", new[] { "https://gitlab.com/g/p.git" })]
    [InlineData("https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p", "https://gitlab.com/g/p.git" })]
    [InlineData("https://gitlab.com/g/p/", new[] { "https://gitlab.com/g/p/", "https://gitlab.com/g/p.git" })]
    [InlineData("https://gitlab.com/g/p.git/", new[] { "https://gitlab.com/g/p.git/" })]
    [InlineData("https://gitlab.com/g/s/p", new[] { "https://gitlab.com/g/s/p", "https://gitlab.com/g/s/p.git" })]
    [InlineData("https://gitlab.com/g/p.GIT", new[] { "https://gitlab.com/g/p.GIT", "https://gitlab.com/g/p.GIT.git" })]
    [InlineData("https://gitlab.com/g/My%20P", new[] { "https://gitlab.com/g/My%20P", "https://gitlab.com/g/My%20P.git" })]
    [InlineData("https://gitlab.com/p", new[] { "https://gitlab.com/p" })]
    [InlineData("https://gitlab.com", new[] { "https://gitlab.com" })]
    [InlineData("https://Ada@gitlab.com/g/p.git", new[] { "https://gitlab.com/g/p.git" })]
    [InlineData("https://ada@gitlab.com/g/p", new[] { "https://gitlab.com/g/p", "https://gitlab.com/g/p.git" })]
    [InlineData("https://x-access-token@gitlab.com/g/p.git", new[] { "https://gitlab.com/g/p.git" })]
    [InlineData("https://GitLab.com:443/g/p", new[] { "https://GitLab.com:443/g/p", "https://GitLab.com:443/g/p.git" })]
    [InlineData("https://gitlab.com/g/my(p)", new[] { "https://gitlab.com/g/my(p)", "https://gitlab.com/g/my%28p%29", "https://gitlab.com/g/my(p).git", "https://gitlab.com/g/my%28p%29.git" })]
    public void On_GitLab_a_git_less_url_is_bound_with_its_git_twin_right_after_it(string url, string[] bound)
    {
        var bindings = CredentialBinding.Select([Fetch("origin", url)], GitLab);

        Assert.Equal(bound, bindings.Select(binding => binding.Url));
        Assert.All(bindings, binding => Assert.Equal(new CredentialBinding(binding.Url, Ada), binding));
    }

    [Theory]
    [InlineData("https://oauth2@gitlab.com/g/p.git")]
    [InlineData("https://X-Access-Token@gitlab.com/g/p.git")]
    [InlineData("https://alice@gitlab.com/g/p.git")]
    [InlineData("https://Ada:secret@gitlab.com/g/p.git")]
    [InlineData("https://x-access-token:secret@gitlab.com/g/p.git")]
    [InlineData("https://gitlab.com.evil.example/g/p.git")]
    [InlineData("https://evil.gitlab.com/g/p.git")]
    [InlineData("https://gitlab.co/g/p.git")]
    [InlineData("https://gitlab.com./g/p.git")]
    [InlineData("https://gitlab.com:8443/g/p.git")]
    [InlineData("http://gitlab.com/g/p.git")]
    [InlineData("ssh://git@gitlab.com/g/p.git")]
    [InlineData("git@gitlab.com:g/p.git")]
    [InlineData("https://gitlab.com/g/../p.git")]
    [InlineData("https://gitlab.com/g/./p.git")]
    [InlineData("https://gitlab.com//g/p.git")]
    [InlineData("https://gitlab.com/g//p.git")]
    [InlineData("https://gitlab.com/g/p.git?x=1")]
    [InlineData("https://gitlab.com/g/p.git#x")]
    [InlineData("https://gitlab.com/g/%2E%2E/p.git")]
    [InlineData("https://gitlab.com/g/p%2Fq.git")]
    [InlineData("https://gitlab.com/g/p%25.git")]
    [InlineData("https://gitlab.com/g/p%.git")]
    [InlineData("https://gitlab.com/g/p\\q.git")]
    [InlineData("https://gitlab.com/g/p q.git")]
    [InlineData("https://gitlab.com/g/pé.git")]
    public void On_GitLab_another_user_a_password_a_lookalike_another_scheme_or_a_url_out_of_the_form_is_not_bound(string url)
    {
        Assert.Empty(CredentialBinding.Select([Fetch("origin", url)], GitLab));
    }

    [Theory]
    [InlineData("https://alice@gitlab.com/g/p.git", "https://gitlab.com/g/p", new string[0])]
    [InlineData("https://alice@gitlab.com/g/%70.git", "https://gitlab.com/g/p.git", new string[0])]
    [InlineData("https://bob@gitlab.com/g/p", "https://gitlab.com/g/p/", new string[0])]
    [InlineData("https://alice@gitlab.com/g/x/../p.git", "https://gitlab.com/g/p", new string[0])]
    [InlineData("https://evil.example\\@gitlab.com/g/p.git", "https://gitlab.com/g/p", new string[0])]
    [InlineData("https://alice@gitlab.com/g/p.git?x=1", "https://gitlab.com/g/p", new string[0])]
    [InlineData("https://alice@gitlab.com/h/p.git", "https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p", "https://gitlab.com/g/p.git" })]
    [InlineData("https://bob@gitlab.com/john.doe/p.git", "https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p", "https://gitlab.com/g/p.git" })]
    [InlineData("https://bob@gitlab.com/g/p", "https://gitlab.com/g/pq", new[] { "https://gitlab.com/g/pq", "https://gitlab.com/g/pq.git" })]
    [InlineData("https://bob@gitlab.com/g/p/sub", "https://gitlab.com/g/p", new string[0])]
    [InlineData("https://bob@github.com/g/p.git", "https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p", "https://gitlab.com/g/p.git" })]
    [InlineData("http://bob@gitlab.com/g/p.git", "https://gitlab.com/g/p", new string[0])]
    public void On_GitLab_another_users_url_drops_in_gits_credential_view_or_leaves_the_host_unbound(string other, string url, string[] bound)
    {
        // D379 changed four rows: alice@…/g/p.git and bob@…/g/p now drop their project's every
        // spelling (R3), bob@…/g/p/sub the twin under its project too, and http://bob@ counts as on
        // gitlab.com (R2), which 301s it into https://.
        // Either order of the remotes gives the same bindings: the other users are read first.
        Assert.Equal(bound, CredentialBinding.Select([Fetch("other", other), Fetch("origin", url)], GitLab).Select(binding => binding.Url));
        Assert.Equal(bound, CredentialBinding.Select([Fetch("origin", url), Fetch("other", other)], GitLab).Select(binding => binding.Url));
    }

    [Theory]
    [MemberData(nameof(CredentialViewCases))]
    public void Another_users_url_drops_a_binding_exactly_where_both_meet_in_gits_credential_view(string provider, string url, string other, bool dropped)
    {
        var coverage = CoverageOf(provider);
        var alone = CredentialBinding.Select([Fetch("origin", url)], coverage);

        var beside = CredentialBinding.Select([Fetch("origin", url), Fetch("other", other)], coverage);

        Assert.NotEmpty(alone);
        Assert.Equal(dropped, beside.Count < alone.Count);
        Assert.All(beside, binding => Assert.Contains(binding, alone));
    }

    [Fact]
    public void On_Azure_DevOps_a_binding_and_its_credential_twin_drop_together()
    {
        var coverage = AzureDevOpsCoverageVectors.Coverage;

        Assert.Equal([$"{AzureRepos}/r(x)", $"{AzureRepos}/r%28x%29"], CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r(x)")], coverage).Select(binding => binding.Url));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r(x)"), Fetch("evil", $"{EvilRepos}/r%28x%29")], coverage));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r(x)"), Fetch("evil", $"{EvilRepos}/r(x%29")], coverage));
        Assert.Equal(
            [$"{AzureRepos}/r(x)", $"{AzureRepos}/r%28x%29"],
            CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r(x)"), Fetch("evil", $"{EvilRepos}/R(x)")], coverage).Select(binding => binding.Url));
    }

    [Theory]
    [InlineData("https://otherorg@dev.azure.com/")]
    [InlineData("https://otherorg@dev.azure.com")]
    [InlineData("https://evil@dev.azure.com/my_org/p/_git/r")]
    [InlineData("https://evil@dev.azure.com/myorg/p/_git/./r")]
    public void On_Azure_DevOps_another_users_url_out_of_the_form_leaves_the_host_unbound(string other)
    {
        var coverage = AzureDevOpsCoverageVectors.Coverage;

        Assert.Empty(CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r"), Fetch("other", other)], coverage));

        // The organization's own host is another host: it stays bound.
        Assert.Equal(
            ["https://myorg.visualstudio.com/p/_git/r"],
            CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r"), Fetch("vs", "https://myorg.visualstudio.com/p/_git/r"), Fetch("other", other)], coverage).Select(binding => binding.Url));
    }

    /// <summary>
    /// URLs under <c>…/myorg/p/_git/r</c> in git's credential view that the server may read as
    /// another organization's (g3 G3.2's sent column: X4, X6, V21's spellings): <c>%5C</c>, a raw
    /// <c>\</c>, overlong dots. <see cref="CoverageGitMatchTests"/> shows that the binding's key
    /// answers them with no user, which is why 8.2 step 5, as WP-C's review amended it, leaves the
    /// origin unbound beside them whatever their user.
    /// </summary>
    public static TheoryData<string> OutOfFormUnderTheBinding => new()
    {
        $"{AzureRepos}/r/..%5C..%5C..%5C..%5Cotherorg/p/_git/r",
        $"{AzureRepos}/r/..\\..\\..\\..\\otherorg/p/_git/r",
        $"{AzureRepos}/r/%C0%AE%C0%AE/%C0%AE%C0%AE/%C0%AE%C0%AE/%C0%AE%C0%AE/otherorg/p/_git/r",
    };

    [Theory]
    [MemberData(nameof(OutOfFormUnderTheBinding))]
    public void On_Azure_DevOps_a_url_out_of_the_form_with_no_user_or_the_accounts_leaves_the_origin_unbound(string url)
    {
        var coverage = AzureDevOpsCoverageVectors.Coverage;
        foreach (var spelled in new[] { url, url.Replace("https://", "https://myorg@", StringComparison.Ordinal), url.Replace("https://", "https://x-access-token@", StringComparison.Ordinal) })
        {
            Assert.Empty(CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r"), Fetch("other", spelled)], coverage));
            Assert.Equal(
                ["https://myorg.visualstudio.com/p/_git/r"],
                CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r"), Fetch("vs", "https://myorg.visualstudio.com/p/_git/r"), Fetch("other", spelled)], coverage).Select(binding => binding.Url));
        }
    }

    [Fact]
    public void On_Azure_DevOps_a_url_with_no_first_segment_in_the_grammar_leaves_the_origin_unbound_and_another_organizations_does_not()
    {
        var coverage = AzureDevOpsCoverageVectors.Coverage;

        Assert.Empty(CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r"), Fetch("root", "https://dev.azure.com/")], coverage));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r"), Fetch("other", "https://dev.azure.com/my_org/p/_git/r")], coverage));
        Assert.Equal([$"{AzureRepos}/r"], CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r"), Fetch("other", "https://dev.azure.com/otherorg/p/_git/r")], coverage).Select(binding => binding.Url));

        // A password is git's own credential: never bound, and it fails nothing (7a's rule).
        Assert.Equal([$"{AzureRepos}/r"], CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r"), Fetch("pw", "https://myorg:secret@dev.azure.com/myorg/p/_git/r/..%5Cx")], coverage).Select(binding => binding.Url));
    }

    [Fact]
    public void On_GitLab_and_the_organizations_own_host_a_user_less_url_out_of_the_form_fails_nothing()
    {
        // Their token reaches no other account there (WP-C's review on 8.2 step 5).
        Assert.Equal(
            ["https://gitlab.com/g/p", "https://gitlab.com/g/p.git"],
            CredentialBinding.Select([Fetch("origin", "https://gitlab.com/g/p"), Fetch("other", "https://gitlab.com/g/p/..%5C..%5Cq")], GitLab).Select(binding => binding.Url));
        Assert.Equal(
            ["https://myorg.visualstudio.com/p/_git/r"],
            CredentialBinding.Select([Fetch("vs", "https://myorg.visualstudio.com/p/_git/r"), Fetch("other", "https://myorg.visualstudio.com/p/_git/r/..%5Cq")], AzureDevOpsCoverageVectors.Coverage).Select(binding => binding.Url));
    }

    [Theory]
    [InlineData("evil@")]
    [InlineData("")]
    [InlineData("myorg@")]
    [InlineData("x-access-token@")]
    [InlineData("myorg:secret@")]
    public void A_url_on_a_covered_host_with_one_trailing_dot_counts_as_another_users_whatever_its_user(string user)
    {
        // git's match_host reads dev.azure.com. as dev.azure.com, .NET's Host keeps the dot: a
        // binding's key would answer the URL with the app's helper alone, and the broker binds no
        // such host, so the URL drops what lies over it as another user's would on that host, and
        // is never bound itself (V23).
        var azure = AzureDevOpsCoverageVectors.Coverage;
        var gitLabUser = user.Replace("myorg", "Ada", StringComparison.Ordinal);

        Assert.Empty(CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r"), Fetch("dot", $"https://{user}dev.azure.com./myorg/p/_git/r")], azure));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r"), Fetch("dot", $"https://{user}dev.azure.com./myorg/p/_git/./r")], azure));
        Assert.Empty(CredentialBinding.Select([Fetch("vs", "https://myorg.visualstudio.com/p/_git/r"), Fetch("dot", $"https://{user}myorg.visualstudio.com./p/_git/r")], azure));
        // Since D379 (R3) on GitLab it drops its project's every spelling, the twin included.
        Assert.Empty(CredentialBinding.Select([Fetch("origin", "https://gitlab.com/g/p"), Fetch("dot", $"https://{gitLabUser}gitlab.com./g/p")], GitLab));
        Assert.Empty(CredentialBinding.Select([Fetch("dot", $"https://{user}dev.azure.com./myorg/p/_git/r")], azure));
        Assert.Empty(CredentialBinding.Select([Fetch("dot", $"https://{gitLabUser}gitlab.com./g/p")], GitLab));

        // Where it lies over nothing bound, nothing drops.
        Assert.Equal(
            [$"{AzureRepos}/r"],
            CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r"), Fetch("dot", $"https://{user}dev.azure.com./myorg/p/_git/q")], azure).Select(binding => binding.Url));
    }

    [Fact]
    public void On_Azure_DevOps_another_organizations_url_in_the_form_drops_nothing()
    {
        var bindings = CredentialBinding.Select(
            [Fetch("origin", $"{AzureRepos}/r"), Fetch("other", "https://otherorg@dev.azure.com/otherorg/My%20Project/_git/r"), Fetch("evil", $"{EvilRepos}/R")],
            AzureDevOpsCoverageVectors.Coverage);

        Assert.Equal([new CredentialBinding($"{AzureRepos}/r", AzureDevOpsCoverageVectors.Account) { SharedOriginOwner = "myorg" }], bindings);
    }

    [Theory]
    [InlineData("http://127.0.0.1:2/")]
    [InlineData("http://127.0.0.1:2")]
    [InlineData("http://Fernhill-Labs@127.0.0.1:2/")]
    public void The_fakes_origin_alone_is_not_bound(string url)
    {
        Assert.Empty(CredentialBinding.Select([Fetch("origin", url)], FakeAzureCoverage()));
    }

    [Fact]
    public void On_the_fakes_origin_the_organizations_repositories_are_bound_with_it_as_owner()
    {
        var coverage = FakeAzureCoverage();

        var bindings = CredentialBinding.Select(
            [
                Fetch("origin", "http://127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/mobile-app"),
                Fetch("other", "http://127.0.0.1:2/Larkspur-Studio/p/_git/r"),
                Fetch("port", "http://127.0.0.1:3/Fernhill-Labs/p/_git/r"),
                Fetch("vs", "https://fernhill-labs.visualstudio.com/p/_git/r"),
            ],
            coverage);

        Assert.Equal(
            [new CredentialBinding("http://127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/mobile-app", coverage.Account) { SharedOriginOwner = "Fernhill-Labs" }],
            bindings);
    }

    private const string V1 = "https://dev.azure.com/myorg/p/_git/r";
    private const string VisualStudio = "https://myorg.visualstudio.com/p/_git/r";

    /// <summary>
    /// D379 (R1): http and https URLs that .NET cannot read, wave 1's percent-encoded hosts among them,
    /// which git's credential code and curl decode into a covered host (<see cref="CoverageGitMatchTests"/>
    /// shows the key match), and one that names no covered host at all. V26's spelling was in
    /// <see cref="On_Azure_DevOps_another_users_url_out_of_the_form_leaves_the_host_unbound"/>, which
    /// kept <c>myorg.visualstudio.com</c> bound beside it; since D379 no covered host stays bound.
    /// </summary>
    public static TheoryData<string> UnreadableHttpUrls => new()
    {
        "https://evil.example\\@dev.azure.com/myorg/p/_git/r",
        "https://%64ev.azure.com/myorg/p/_git/r/..%5C..%5C..%5C..%5Cotherorg/p/_git/r",
        "https://%64%65%76.azure.com/myorg/p/_git/r/%C0%AE%C0%AE/%C0%AE%C0%AE/%C0%AE%C0%AE/%C0%AE%C0%AE/otherorg/p/_git/r",
        "https://dev.azure%2Ecom/myorg/p/_git/r/..%5C..%5C..%5C..%5Cotherorg/p/_git/r",
        "https://myorg%2Evisualstudio.com/p/_git/r/..%5Cq",
        "https://evil@%64ev.azure.com/myorg/p/_git/r",
        "https://evil@dev%2Eazure.com/myorg/p/_git/r",
        "https://evil@myorg.visualstudio%2Ecom/p/_git/r",
        "https://myorg@%64ev.azure.com/myorg/p/_git/r",
        "https://evil@%67itlab.com/g/p.git",
        "https://%67itlab.com/g/p.git",
        "HTTPS://%64ev.azure.com/myorg/p/_git/r",
        "http://%64ev.azure.com/myorg/p/_git/r",
        "Http://%31%32%37.0.0.1:2/Fernhill-Labs/p/_git/r",
        "https://%65vil.example/x",
    };

    [Theory]
    [MemberData(nameof(UnreadableHttpUrls))]
    public void An_http_or_https_url_that_cannot_be_read_leaves_every_covered_host_unbound_whatever_its_text(string url)
    {
        Assert.False(Uri.TryCreate(url, UriKind.Absolute, out _), $"{url} is readable.");
        var azure = AzureDevOpsCoverageVectors.Coverage;

        Assert.Equal([V1, VisualStudio], CredentialBinding.Select([Fetch("origin", V1), Fetch("vs", VisualStudio)], azure).Select(binding => binding.Url));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", V1), Fetch("vs", VisualStudio), Fetch("other", url)], azure));
        Assert.Empty(CredentialBinding.Select([Fetch("other", url), Fetch("vs", VisualStudio)], azure));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", "https://gitlab.com/g/p.git"), Fetch("other", url)], GitLab));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", "http://127.0.0.1:2/Fernhill-Labs/p/_git/r"), Fetch("other", url)], FakeAzureCoverage()));
    }

    /// <summary>
    /// The hunt on the fixed code, its third finding (2026-11-01): URLs in git's remote helper
    /// grammar, <c>&lt;helper&gt;::&lt;address&gt;</c>, which git reads as the address over
    /// <c>git-remote-&lt;helper&gt;</c> (for ftp, ftps and Windows' http.exe or https.exe, remote-curl
    /// on the address; <see cref="CoverageGitMatchTests"/> shows the token taken over a transport).
    /// Before the fix the app read the text before the first <c>:</c> as the scheme, so R1's and the
    /// non-ASCII rule never ran, and the URL reached no covered host: the bindings stayed beside
    /// another user's or an out-of-form address. Now any URL in that grammar, whatever its address,
    /// leaves every covered host unbound, as R1's unreadable one does (D379).
    /// </summary>
    public static TheoryData<string> RemoteHelperUrls => new()
    {
        "ftp::https://evil@dev.azure.com/myorg/p/_git/r",
        "ftps::https://dev.azure.com/myorg/p/_git/r/..%5C..%5Cotherorg/p/_git/r",
        "https.exe::https://evil@dev.azure.com/myorg/p/_git/r",
        "HTTP.EXE::http://dev.azure.com/myorg/p/_git/r/..%5C..%5C..%5C..%5Cotherorg/p/_git/r",
        "ftp::https://evil@myorg.visualstudio.com/p/_git/r",
        "ftp::https://evil@%64ev.azure.com/myorg/p/_git/r",
        "ftp::https://evil@ｄｅｖ.azure.com/myorg/p/_git/r",
        "ftp::https://bob@gitlab.com/g/p",
        "ftp::http://bob@gitlab.com/g/p.git",
        "https.exe::https://bob@gitlab.com/g/p",
        "ftp::http://bob@127.0.0.1:2/g/p",
        "http.exe::http://127.0.0.1:2/Fernhill-Labs/p/_git/r/..%5Cq",
        "ftp::https://dev.azure.com/myorg/p/_git/r",
        "ftp::https://gitlab.com/g/p.git",
        "gcrypt::ftp::https://evil@dev.azure.com/myorg/p/_git/r",
        "1x::https://evil@dev.azure.com/myorg/p/_git/r",
        "a+b.c-d::https://bob@gitlab.com/g/p",
        "::https://evil@dev.azure.com/myorg/p/_git/r",
        "hg::ssh://hg.example/r",
        "dev.azure.com::myorg/p/_git/r",
    };

    [Theory]
    [MemberData(nameof(RemoteHelperUrls))]
    public void A_url_in_gits_remote_helper_grammar_leaves_every_covered_host_unbound_whatever_its_address(string url)
    {
        var azure = AzureDevOpsCoverageVectors.Coverage;

        Assert.False(CredentialBinding.InPlainForm(url), $"{url} is in the form.");
        Assert.Empty(CredentialBinding.Select([Fetch("origin", V1), Fetch("vs", VisualStudio), Fetch("other", url)], azure));
        Assert.Empty(CredentialBinding.Select([Fetch("other", url), Fetch("vs", VisualStudio)], azure));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", "https://gitlab.com/g/p.git"), Fetch("other", url)], GitLab));
        Assert.Empty(CredentialBinding.Select([new RemoteEffectiveUrls("origin", ["https://gitlab.com/g/p"], [url])], GitLab));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", "http://127.0.0.1:2/Fernhill-Labs/p/_git/r"), Fetch("other", url)], FakeAzureCoverage()));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", "http://127.0.0.1:2/g/p.git"), Fetch("other", url)], FakeGitLabCoverage()));
        Assert.Empty(CredentialBinding.Select([Fetch("other", url)], GitLab));
    }

    [Theory]
    [InlineData("+x::https://evil@dev.azure.com/myorg/p/_git/r")]
    [InlineData("x_y::https://evil@dev.azure.com/myorg/p/_git/r")]
    [InlineData("ftp:/:https://evil@dev.azure.com/myorg/p/_git/r")]
    [InlineData("https://dev.azure.com/otherorg/p/_git/r::x")]
    [InlineData("https://[::1]:8080/myorg/p/_git/r")]
    [InlineData("ftp://evil@dev.azure.com/myorg/p/_git/r")]
    public void A_url_outside_gits_remote_helper_grammar_with_a_double_colon_fails_nothing(string url)
    {
        // git's transport_get takes a helper only from a name of is_urlschemechar's characters,
        // the first alphanumeric, right before "::"; here git reads a local path, an http URL
        // or curl's ftp, which no binding's key answers.
        Assert.Equal([V1], CredentialBinding.Select([Fetch("origin", V1), Fetch("other", url)], AzureDevOpsCoverageVectors.Coverage).Select(binding => binding.Url));
    }

    [Theory]
    [InlineData("https://gitlab.com/g/p.git", true)]
    [InlineData("HTTPS://gitlab.com/g/p.git", true)]
    [InlineData("git+ssh://gitlab.com/g/p.git", true)]
    [InlineData("a1+b.c-d://gitlab.com/g/p.git", true)]
    [InlineData("ftp::https://gitlab.com/g/p.git", false)]
    [InlineData("http.exe::https://gitlab.com/g/p.git", false)]
    [InlineData("::https://gitlab.com/g/p.git", false)]
    [InlineData("1x://gitlab.com/g/p.git", false)]
    [InlineData("-x://gitlab.com/g/p.git", false)]
    [InlineData("x_y://gitlab.com/g/p.git", false)]
    public void The_form_needs_a_scheme_in_rfc_3986s_grammar_before_its_double_slash(string url, bool inForm)
    {
        Assert.Equal(inForm, CredentialBinding.InPlainForm(url));
    }

    [Theory]
    [InlineData("git@ssh.dev.azure.com:v3/myorg/p/r")]
    [InlineData("dev.azure.com:myorg/p/_git/r")]
    [InlineData("/local/%64ev.azure.com/r")]
    public void A_url_of_no_http_scheme_that_cannot_be_read_fails_nothing(string url)
    {
        Assert.Equal([V1], CredentialBinding.Select([Fetch("origin", V1), Fetch("other", url)], AzureDevOpsCoverageVectors.Coverage).Select(binding => binding.Url));
    }

    /// <summary>
    /// The hunt on the fixed code (2026-11-01): http and https URLs that .NET reads whose host holds
    /// a character outside U+0021 to U+007E. .NET keeps such a host as written (its <c>IdnHost</c>
    /// is <c>gitlab.com</c> or <c>dev.azure.com</c> for most of these), git's curl may map it into a
    /// covered host (2.56's did, 2.43's did not) and, after a redirect into the origin, git builds
    /// the credential again from the ASCII target without the URL's user
    /// (<see cref="CoverageGitMatchTests"/> shows it over a transport). Before the fix
    /// <c>Select</c> kept the bindings beside them; now, as for R1's unreadable URL (D379), every
    /// covered host is left unbound, whatever the URL's user, and one naming no covered host too.
    /// </summary>
    public static TheoryData<string> HostsOutsideAscii => new()
    {
        "https://evil@gitlab．com/g/p",
        "https://evil@ｇｉｔｌａｂ.com/g/p",
        "https://evil@gitlab。com/g/p",
        "https://evil@ｄｅｖ.azure.com/myorg/p/_git/r",
        "http://evil@ｄｅｖ.azure.com/myorg/p/_git/r",
        "https://ｄｅｖ.azure.com/myorg/p/_git/r",
        "https://myorg@ｄｅｖ.azure.com/myorg/p/_git/r",
        "https://evil@myorg.visualstudio．com/p/_git/r",
        "https://gitlab．com/g/p.git",
        "https://Ada@gitlab．com/g/p.git",
        "https://evil@git­lab.com/g/p",
        "https://evil@gitlab.com​/g/p",
        "https://evil@gitlab.com．/g/p",
        "https://gitlab.com /g/p",
        "HTTPS://evil@gitlab．com/g/p",
        "http://bob@127．0．0．1:2/g/p",
        "http://bob@１２７．０．０．１:2/Fernhill-Labs/p/_git/r",
        "https://bücher.example/x",
    };

    [Theory]
    [MemberData(nameof(HostsOutsideAscii))]
    public void An_http_or_https_url_whose_host_is_outside_printable_ascii_leaves_every_covered_host_unbound(string url)
    {
        Assert.True(Uri.TryCreate(url, UriKind.Absolute, out _), $"{url} is unreadable, so R1's.");
        var azure = AzureDevOpsCoverageVectors.Coverage;

        Assert.Empty(CredentialBinding.Select([Fetch("origin", V1), Fetch("vs", VisualStudio), Fetch("other", url)], azure));
        Assert.Empty(CredentialBinding.Select([Fetch("other", url), Fetch("vs", VisualStudio)], azure));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", "https://gitlab.com/g/p"), Fetch("other", url)], GitLab));
        Assert.Empty(CredentialBinding.Select([Fetch("other", url), Fetch("origin", "https://gitlab.com/g/p.git")], GitLab));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", "http://127.0.0.1:2/Fernhill-Labs/p/_git/r"), Fetch("other", url)], FakeAzureCoverage()));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", "http://127.0.0.1:2/g/p.git"), Fetch("other", url)], FakeGitLabCoverage()));
        Assert.Empty(CredentialBinding.Select([Fetch("other", url)], GitLab));
    }

    [Theory]
    [InlineData("ssh://git@ｄｅｖ.azure.com/v3/myorg/p/r")]
    [InlineData("https://xn--bcher-kva.example/x")]
    [InlineData("https://other.example/g/p．")]
    [InlineData("https://other.example/x?．@dev.azure.com")]
    public void Another_scheme_a_punycode_host_or_a_character_outside_ascii_after_the_host_fails_nothing(string url)
    {
        // Only the host counts: after the first /, ? or # nothing is in it, for git as for .NET.
        Assert.Equal([V1], CredentialBinding.Select([Fetch("origin", V1), Fetch("other", url)], AzureDevOpsCoverageVectors.Coverage).Select(binding => binding.Url));
    }

    /// <summary>
    /// D379 (R2): beside a bound URL, a URL that a measured redirect leads into the covered origin
    /// (http:// and www. on gitlab.com, r6 6.1; http:// on dev.azure.com, r5), and lookalikes that
    /// none leads there. Another user's drops by path or fails the host; no user or the account's
    /// fails it only out of the form on an origin with an organization.
    /// </summary>
    public static TheoryData<string, string, string, string[]> RedirectedUrls => new()
    {
        { "GitLab", "http://bob@gitlab.com/g/p.git", "https://gitlab.com/g/p.git", [] },
        { "GitLab", "https://bob@www.gitlab.com/g/p.git", "https://gitlab.com/g/p.git", [] },
        { "GitLab", "http://bob@www.gitlab.com/g/p", "https://gitlab.com/g/p.git", [] },
        { "GitLab", "https://bob@WWW.GitLab.COM./g/p.git", "https://gitlab.com/g/p.git", [] },
        { "GitLab", "https://bob@gitlab.com:8443/g/p.git", "https://gitlab.com/g/p.git", [] },
        { "GitLab", "https://Ada:secret@www.gitlab.com/g/p.git?x=1", "https://gitlab.com/g/p.git", ["https://gitlab.com/g/p.git"] },
        { "GitLab", "https://bob@www.gitlab.com/g/p.git?x=1", "https://gitlab.com/h/q.git", [] },
        { "GitLab", "http://bob@gitlab.com/h/p.git", "https://gitlab.com/g/p.git", ["https://gitlab.com/g/p.git"] },
        { "GitLab", "http://gitlab.com/g/p.git", "https://gitlab.com/g/p.git", ["https://gitlab.com/g/p.git"] },
        { "GitLab", "https://Ada@www.gitlab.com/g/p.git", "https://gitlab.com/g/p.git", ["https://gitlab.com/g/p.git"] },
        { "GitLab", "http://x-access-token@gitlab.com/g/p", "https://gitlab.com/g/p", ["https://gitlab.com/g/p", "https://gitlab.com/g/p.git"] },
        { "GitLab", "https://www.gitlab.com/g/p/..%5C..%5Cq", "https://gitlab.com/g/p", ["https://gitlab.com/g/p", "https://gitlab.com/g/p.git"] },
        { "GitLab", "https://bob@wwwgitlab.com/g/p.git", "https://gitlab.com/g/p.git", ["https://gitlab.com/g/p.git"] },
        { "GitLab", "https://bob@www.www.gitlab.com/g/p.git", "https://gitlab.com/g/p.git", ["https://gitlab.com/g/p.git"] },
        { "GitLab", "https://bob@gitlab.com.evil.example/g/p.git", "https://gitlab.com/g/p.git", ["https://gitlab.com/g/p.git"] },
        { "GitLab", "ftp://bob@gitlab.com/g/p.git", "https://gitlab.com/g/p.git", ["https://gitlab.com/g/p.git"] },
        { "AzureDevOps", "http://evil@dev.azure.com/myorg/p/_git/r", V1, [] },
        { "AzureDevOps", "https://evil@www.dev.azure.com/myorg/p/_git/r", V1, [] },
        { "AzureDevOps", "https://evil@dev.azure.com:8443/myorg/p/_git/r", V1, [] },
        { "AzureDevOps", "http://evil@dev.azure.com/my_org/p/_git/r", V1, [] },
        { "AzureDevOps", "http://dev.azure.com/myorg/p/_git/r/..%5C..%5C..%5C..%5Cotherorg/p/_git/r", V1, [] },
        { "AzureDevOps", "http://myorg@www.dev.azure.com/myorg/p/_git/r/%C0%AE%C0%AE/otherorg/p/_git/r", V1, [] },
        { "AzureDevOps", "http://dev.azure.com/myorg/p/_git/r", V1, [V1] },
        { "AzureDevOps", "http://myorg@dev.azure.com/myorg/p/_git/r", V1, [V1] },
        { "AzureDevOps", "http://evil@dev.azure.com/otherorg/p/_git/r", V1, [V1] },
        { "AzureDevOps", "http://evil@myorg.visualstudio.com/p/_git/r", VisualStudio, [] },
        { "AzureDevOps", "https://evil@www.myorg.visualstudio.com.:444/p/_git/r", VisualStudio, [] },
        { "AzureDevOps", "http://myorg.visualstudio.com/p/_git/r/..%5Cq", VisualStudio, [VisualStudio] },
    };

    [Theory]
    [MemberData(nameof(RedirectedUrls))]
    public void A_url_a_redirect_leads_into_a_covered_origin_counts_as_on_it_and_is_never_bound(string provider, string other, string url, string[] bound)
    {
        var coverage = CoverageOf(provider);

        Assert.Equal(bound, CredentialBinding.Select([Fetch("other", other), Fetch("origin", url)], coverage).Select(binding => binding.Url));
        Assert.Equal(bound, CredentialBinding.Select([Fetch("origin", url), Fetch("other", other)], coverage).Select(binding => binding.Url));
        Assert.Empty(CredentialBinding.Select([Fetch("other", other)], coverage));
    }

    [Fact]
    public void On_the_fakes_origin_another_users_url_on_another_port_drops_what_lies_over_it()
    {
        const string Bound = "http://127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/mobile-app";

        Assert.Empty(CredentialBinding.Select([Fetch("origin", Bound), Fetch("evil", "http://evil@127.0.0.1:3/Fernhill-Labs/Mobile%20App/_git/mobile-app")], FakeAzureCoverage()));
        Assert.Equal([Bound], CredentialBinding.Select([Fetch("origin", Bound), Fetch("evil", "http://evil@127.0.0.1:3/Fernhill-Labs/p/_git/r")], FakeAzureCoverage()).Select(binding => binding.Url));
    }

    /// <summary>D379 (R3): GitLab's four spellings of one project, <c>…/g/p</c>, <c>…/g/p/</c>, <c>…/g/p.git</c> and <c>…/g/p.git/</c>, each as another user's beside each bound.</summary>
    public static TheoryData<string, string> GitLabProjectSpellings
    {
        get
        {
            string[] paths = ["/g/p", "/g/p/", "/g/p.git", "/g/p.git/"];
            var rows = new TheoryData<string, string>();
            foreach (var other in paths)
            {
                foreach (var bound in paths)
                {
                    rows.Add("https://bob@gitlab.com" + other, GitLabWeb + bound);
                }
            }

            return rows;
        }
    }

    [Theory]
    [MemberData(nameof(GitLabProjectSpellings))]
    public void On_GitLab_another_users_url_drops_every_spelling_of_its_project(string other, string url)
    {
        // gitlab.com answers …/p with 301 and …/p.git/ with 308 into …/p.git/info/refs (r6 6.1), so
        // each spelling's key answers the others' requests once git has followed the redirect.
        Assert.NotEmpty(CredentialBinding.Select([Fetch("origin", url)], GitLab));
        Assert.Empty(CredentialBinding.Select([Fetch("other", other), Fetch("origin", url)], GitLab));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", url), Fetch("other", other)], GitLab));
    }

    [Theory]
    [InlineData("GitLab", "https://bob@gitlab.com/g/pq.git", "https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p", "https://gitlab.com/g/p.git" })]
    [InlineData("GitLab", "https://bob@gitlab.com/g/p.gitx", "https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p", "https://gitlab.com/g/p.git" })]
    [InlineData("GitLab", "https://bob@gitlab.com/g/p.git.git", "https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p", "https://gitlab.com/g/p.git" })]
    [InlineData("GitLab", "https://bob@gitlab.com/g/.git", "https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p", "https://gitlab.com/g/p.git" })]
    [InlineData("GitLab", "https://bob@gitlab.com/g/p.git/x", "https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p" })]
    [InlineData("AzureDevOps", "https://evil@dev.azure.com/myorg/p/_git/r.git", V1, new[] { V1 })]
    [InlineData("AzureDevOps", "https://evil@dev.azure.com/myorg/p/_git/r", V1 + ".git", new[] { V1 + ".git" })]
    public void Another_users_url_of_another_project_and_Azure_DevOps_git_suffix_drop_by_path_alone(string provider, string other, string url, string[] bound)
    {
        var coverage = CoverageOf(provider);

        Assert.Equal(bound, CredentialBinding.Select([Fetch("origin", url), Fetch("other", other)], coverage).Select(binding => binding.Url));
    }

    /// <summary>
    /// Another user's URL whose user holds white space or a control character, before, inside or
    /// after the name (the hunt on the fixed code, 2026-11-01): git's credential code reads the user
    /// up to the first <c>@</c> before the first <c>/</c>, <c>?</c> or <c>#</c>, through them
    /// (<c>bob&lt;U+00A0&gt;</c>), and .NET reads it too (<c>bob%C2%A0</c>), where
    /// <see cref="GitCommand.EndsAuthority"/> ends the authority before the <c>@</c>. Out of the form,
    /// so nothing on its host is bound; <see cref="CoverageGitMatchTests"/> shows a binding's key
    /// asking the app's helper alone with that user. Each row: the coverage, the URL, a URL bound alone.
    /// </summary>
    public static TheoryData<string, string, string> UsersWithWhiteSpace => new()
    {
        { "GitLab", "https://bob\u00A0@gitlab.com/g/p.git", "https://gitlab.com/g/p.git" },
        { "GitLab", "https://bob @gitlab.com/g/p.git", "https://gitlab.com/g/p.git" },
        { "GitLab", "https://bob\t@gitlab.com/g/p.git", "https://gitlab.com/g/p.git" },
        { "GitLab", "https://bob\u000B@gitlab.com/g/p.git", "https://gitlab.com/g/p.git" },
        { "GitLab", "https://bob\u0001@gitlab.com/g/p.git", "https://gitlab.com/g/p.git" },
        { "GitLab", "https://bob\u0085@gitlab.com/g/p.git", "https://gitlab.com/g/p.git" },
        { "GitLab", "https://bob\u2028@gitlab.com/g/p.git", "https://gitlab.com/g/p.git" },
        { "GitLab", "https://bob\u3000@gitlab.com/g/p.git", "https://gitlab.com/g/p.git" },
        { "GitLab", "https://\u00A0bob@gitlab.com/g/p.git", "https://gitlab.com/g/p.git" },
        { "GitLab", "https://\u3000bob@gitlab.com/g/p.git", "https://gitlab.com/g/p.git" },
        { "GitLab", "https://b\u00A0ob@gitlab.com/g/p.git", "https://gitlab.com/g/p.git" },
        { "GitLab", "https://bob\u00A0@gitlab.com/h/q.git", "https://gitlab.com/g/p" },
        { "GitLab", "https://bob\u00A0:pw@gitlab.com/g/p.git", "https://gitlab.com/g/p.git" },
        { "GitLab", "https://Ada\u00A0@gitlab.com/g/p.git", "https://gitlab.com/g/p.git" },
        { "GitLab", "https://bob\u00A0@www.gitlab.com/g/p.git", "https://gitlab.com/g/p.git" },
        { "GitLab", "http://bob\u00A0@gitlab.com/g/p", "https://gitlab.com/g/p" },
        { "AzureDevOps", "https://evil\u00A0@myorg.visualstudio.com/p/_git/r", VisualStudio },
        { "AzureDevOps", "https://\u00A0evil@myorg.visualstudio.com/q/_git/s", VisualStudio },
        { "AzureDevOps", "https://evil\u00A0@dev.azure.com/myorg/p/_git/r", V1 },
        { "FakeGitLab", "http://bob\u00A0@127.0.0.1:2/g/p.git", "http://127.0.0.1:2/g/p.git" },
        { "FakeGitLab", "http://bob\u00A0@127.0.0.1:2/g/p", "http://127.0.0.1:2/g/p.git" },
    };

    [Theory]
    [MemberData(nameof(UsersWithWhiteSpace))]
    public void Another_users_url_with_white_space_or_a_control_character_in_its_user_leaves_its_host_unbound(string provider, string other, string url)
    {
        // .NET reads the URL, so it is not R1's unreadable one (D379).
        Assert.True(Uri.TryCreate(other, UriKind.Absolute, out var read), $"{other} is unreadable.");
        Assert.NotEqual(string.Empty, read.UserInfo);
        var coverage = CoverageOf(provider);

        Assert.NotEmpty(CredentialBinding.Select([Fetch("origin", url)], coverage));
        Assert.Empty(CredentialBinding.Select([Fetch("origin", url), Fetch("other", other)], coverage));
        Assert.Empty(CredentialBinding.Select([Fetch("other", other), Fetch("origin", url)], coverage));
        Assert.Empty(CredentialBinding.Select([Fetch("other", other)], coverage));
    }

    [Fact]
    public void White_space_in_another_users_name_fails_its_own_host_alone_and_white_space_after_the_authority_is_no_user()
    {
        // The organization's own host is another host than dev.azure.com: V1 stays bound.
        Assert.Equal(
            [V1],
            CredentialBinding.Select([Fetch("origin", V1), Fetch("vs", VisualStudio), Fetch("evil", "https://evil\u00A0@myorg.visualstudio.com/p/_git/r")], AzureDevOpsCoverageVectors.Coverage).Select(binding => binding.Url));

        // An @ after the first / or ? is in no user, for git as for .NET: a user-less URL out of the
        // form, which fails nothing on GitLab (WP-C's review on 8.2 step 5).
        foreach (var other in new[] { "https://gitlab.com/g/p\u00A0@x.git", "https://gitlab.com?\u00A0@x/g/p.git" })
        {
            Assert.Equal(
                ["https://gitlab.com/g/p", "https://gitlab.com/g/p.git"],
                CredentialBinding.Select([Fetch("origin", "https://gitlab.com/g/p"), Fetch("other", other)], GitLab).Select(binding => binding.Url));
        }
    }

    private static GitCoverage FakeAzureCoverage() =>
        new(new AccountKey(HostingProvider.AzureDevOps, "127.0.0.1:2", "fernhill-labs"), new Uri("http://127.0.0.1:2/"))
        {
            Organization = "Fernhill-Labs",
            HelperUserIsAccounts = true,
        };

    private static GitCoverage FakeGitLabCoverage() =>
        new(new AccountKey(HostingProvider.GitLab, "127.0.0.1:2", "ada"), new Uri("http://127.0.0.1:2/"))
        {
            GitTwin = true,
            HelperUserIsAccounts = true,
        };

    private static RemoteEffectiveUrls Fetch(string remote, string url) => new(remote, [url], [url]);
}
