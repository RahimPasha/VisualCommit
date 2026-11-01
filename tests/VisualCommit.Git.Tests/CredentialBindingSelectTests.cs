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

    /// <summary>The coverage a <see cref="CredentialViewCases"/> row names.</summary>
    public static GitCoverage CoverageOf(string provider) => provider switch
    {
        "AzureDevOps" => AzureDevOpsCoverageVectors.Coverage,
        "GitLab" => GitLab,
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
    [InlineData("https://alice@gitlab.com/g/p.git", "https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p" })]
    [InlineData("https://alice@gitlab.com/g/%70.git", "https://gitlab.com/g/p.git", new string[0])]
    [InlineData("https://bob@gitlab.com/g/p", "https://gitlab.com/g/p/", new[] { "https://gitlab.com/g/p.git" })]
    [InlineData("https://alice@gitlab.com/g/x/../p.git", "https://gitlab.com/g/p", new string[0])]
    [InlineData("https://evil.example\\@gitlab.com/g/p.git", "https://gitlab.com/g/p", new string[0])]
    [InlineData("https://alice@gitlab.com/g/p.git?x=1", "https://gitlab.com/g/p", new string[0])]
    [InlineData("https://alice@gitlab.com/h/p.git", "https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p", "https://gitlab.com/g/p.git" })]
    [InlineData("https://bob@gitlab.com/john.doe/p.git", "https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p", "https://gitlab.com/g/p.git" })]
    [InlineData("https://bob@gitlab.com/g/p", "https://gitlab.com/g/pq", new[] { "https://gitlab.com/g/pq", "https://gitlab.com/g/pq.git" })]
    [InlineData("https://bob@gitlab.com/g/p/sub", "https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p.git" })]
    [InlineData("https://bob@github.com/g/p.git", "https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p", "https://gitlab.com/g/p.git" })]
    [InlineData("http://bob@gitlab.com/g/p.git", "https://gitlab.com/g/p", new[] { "https://gitlab.com/g/p", "https://gitlab.com/g/p.git" })]
    public void On_GitLab_another_users_url_drops_in_gits_credential_view_or_leaves_the_host_unbound(string other, string url, string[] bound)
    {
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
    [InlineData("https://evil.example\\@dev.azure.com/myorg/p/_git/r")]
    public void On_Azure_DevOps_another_users_url_out_of_the_form_leaves_the_host_unbound(string other)
    {
        var coverage = AzureDevOpsCoverageVectors.Coverage;

        Assert.Empty(CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r"), Fetch("other", other)], coverage));

        // The organization's own host is another host: it stays bound.
        Assert.Equal(
            ["https://myorg.visualstudio.com/p/_git/r"],
            CredentialBinding.Select([Fetch("origin", $"{AzureRepos}/r"), Fetch("vs", "https://myorg.visualstudio.com/p/_git/r"), Fetch("other", other)], coverage).Select(binding => binding.Url));
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

    private static GitCoverage FakeAzureCoverage() =>
        new(new AccountKey(HostingProvider.AzureDevOps, "127.0.0.1:2", "fernhill-labs"), new Uri("http://127.0.0.1:2/"))
        {
            Organization = "Fernhill-Labs",
            HelperUserIsAccounts = true,
        };

    private static RemoteEffectiveUrls Fetch(string remote, string url) => new(remote, [url], [url]);
}
