using VisualCommit.Core.Hosting;
using VisualCommit.Hosting.GitLab;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// Which remote URLs are GitLab's (docs/phase-7b-design.md 7.2, 7.6): every form on gitlab.com and
/// on the fake's endpoints, an empty port as the scheme's default, a namespace of 21 segments but
/// not 22, and the refusals (other hosts and
/// ports, one segment, a web address's <c>/-/</c>, <c>..</c>, <c>%2F</c>, a query, <c>git://</c>,
/// GitHub's URLs, and an authority whose last <c>@</c> is not the host git contacts).
/// </summary>
public sealed class GitLabRecognitionTests
{
    private static readonly GitLabEndpoints Fake = GitLabEndpoints.Local(47398);

    [Theory]
    [InlineData("https://gitlab.com/ada/web-app", "ada", "web-app")]
    [InlineData("https://gitlab.com/ada/web-app.git", "ada", "web-app")]
    [InlineData("https://gitlab.com/ada/web-app/", "ada", "web-app")]
    [InlineData("https://gitlab.com/ada/web-app.git/", "ada", "web-app")]
    [InlineData("https://www.gitlab.com/ada/web-app", "ada", "web-app")]
    [InlineData("https://GitLab.COM/Northfield/Platform/Web-App", "Northfield/Platform", "Web-App")]
    [InlineData("https://ada@gitlab.com/ada/web-app.git", "ada", "web-app")]
    [InlineData("https://x-access-token@gitlab.com/ada/web-app.git", "ada", "web-app")]
    [InlineData("https://ada:secret@gitlab.com/ada/web-app.git", "ada", "web-app")]
    [InlineData("https://gitlab.com:443/ada/web-app", "ada", "web-app")]
    [InlineData("HTTPS://gitlab.com/ada/web-app", "ada", "web-app")]
    [InlineData("http://gitlab.com/ada/web-app", "ada", "web-app")]
    [InlineData("http://ada@gitlab.com:80/ada/web-app.git", "ada", "web-app")]
    [InlineData("ssh://git@gitlab.com/northfield/platform/web-app.git", "northfield/platform", "web-app")]
    [InlineData("ssh://gitlab.com:22/ada/web-app", "ada", "web-app")]
    [InlineData("ssh://git@www.gitlab.com/ada/web-app", "ada", "web-app")]
    [InlineData("ssh://git@altssh.gitlab.com:443/northfield/platform/web-app.git", "northfield/platform", "web-app")]
    [InlineData("git@gitlab.com:northfield/platform/web-app.git", "northfield/platform", "web-app")]
    [InlineData("git@www.gitlab.com:ada/web-app", "ada", "web-app")]
    // OpenSSH splits its destination's user at the last @ (ssh -G 'a@evil.example@gitlab.com'
    // gives hostname gitlab.com, MEASURED by WP-GL's third review), so git's ssh reaches gitlab.com.
    [InlineData("ssh://a@evil.example@gitlab.com/ada/web-app.git", "ada", "web-app")]
    [InlineData("a@evil.example@gitlab.com:ada/web-app.git", "ada", "web-app")]
    // An empty port is the scheme's default: git and curl read it so, and reach gitlab.com there.
    [InlineData("https://gitlab.com:/ada/web-app", "ada", "web-app")]
    [InlineData("https://gitlab.com:/ada/web-app.git", "ada", "web-app")]
    [InlineData("http://gitlab.com:/ada/web-app", "ada", "web-app")]
    [InlineData("http://gitlab.com:/ada/web-app.git", "ada", "web-app")]
    [InlineData("ssh://git@gitlab.com:/ada/web-app", "ada", "web-app")]
    [InlineData("ssh://git@gitlab.com:/ada/web-app.git", "ada", "web-app")]
    [InlineData("https://gitlab.com/northfield/platform/services/billing/invoice-generation-and-reconciliation.git", "northfield/platform/services/billing", "invoice-generation-and-reconciliation")]
    [InlineData("https://gitlab.com/a_b.c-d/my.project_name-1", "a_b.c-d", "my.project_name-1")]
    [InlineData("https://gitlab.com/ada/web-app.git.git", "ada", "web-app.git")]
    [InlineData("https://gitlab.com/ada/.hidden", "ada", ".hidden")]
    public void Gitlab_com_recognises_its_forms_with_the_namespace_and_name_as_written(string url, string owner, string name)
    {
        var repository = GitLabUrls.Recognise(GitLabEndpoints.GitLabCom, url);

        Assert.NotNull(repository);
        Assert.Equal((HostingProvider.GitLab, "gitlab.com", owner, name), (repository.Provider, repository.Host, repository.Owner, repository.Name));
        Assert.Equal(owner + "/" + name, repository.FullName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" https://gitlab.com/ada/web-app")]
    [InlineData("https://gitlab.com/ada/web-app ")]
    [InlineData("https://gitlab.com/ada/web\tapp")]
    [InlineData("https://gitlab.com/ada/web app")]
    [InlineData("https://gitlab.example.com/ada/web-app")]
    [InlineData("https://gitlab.com.evil.example/ada/web-app")]
    [InlineData("https://evil.example/gitlab.com/ada")]
    [InlineData("https://about.gitlab.com/ada/web-app")]
    [InlineData("https://altssh.gitlab.com/ada/web-app")]
    [InlineData("ssh://git@altssh.gitlab.com/ada/web-app")]
    [InlineData("ssh://git@altssh.gitlab.com:22/ada/web-app")]
    [InlineData("ssh://git@gitlab.com:443/ada/web-app")]
    [InlineData("https://gitlab.com/ada")]
    [InlineData("https://gitlab.com/ada/")]
    [InlineData("https://gitlab.com/ada.git")]
    [InlineData("https://gitlab.com/ada/web-app/-/tree/main")]
    [InlineData("https://gitlab.com/ada/web-app/-/merge_requests/21")]
    [InlineData("https://gitlab.com/ada/-")]
    [InlineData("https://gitlab.com/ada/../web-app")]
    [InlineData("https://gitlab.com/ada/./web-app")]
    [InlineData("https://gitlab.com/ada/..")]
    [InlineData("https://gitlab.com/ada//web-app")]
    [InlineData("https://gitlab.com//ada/web-app")]
    [InlineData("https://gitlab.com/ada%2Fweb-app")]
    [InlineData("https://gitlab.com/ada/web%2Dapp")]
    [InlineData("https://gitlab.com/ada/web-app?ref=main")]
    [InlineData("https://gitlab.com/ada/web-app#readme")]
    [InlineData("https://gitlab.com/ada\\web-app")]
    [InlineData("https://gitlab.com/ada/web-app.git//")]
    [InlineData("https://gitlab.com/ada/web+app")]
    [InlineData("https://gitlab.com/ada/wéb-app")]
    [InlineData("https://gitlab.com:8443/ada/web-app")]
    // An empty port is the scheme's default: ssh's 22, on which altssh.gitlab.com is refused.
    [InlineData("ssh://git@altssh.gitlab.com:/ada/web-app")]
    [InlineData("https://@gitlab.com/ada/web-app")]
    [InlineData("http://ada:secret@gitlab.com/ada/web-app")]
    [InlineData("http://gitlab.com:443/ada/web-app")]
    [InlineData("ssh://git:secret@gitlab.com/ada/web-app")]
    [InlineData("git://gitlab.com/ada/web-app.git")]
    [InlineData("ftp://gitlab.com/ada/web-app.git")]
    [InlineData("gitlab.com:ada/web-app.git")]
    [InlineData("git:secret@gitlab.com:ada/web-app.git")]
    [InlineData("git@gitlab.com:/ada/web-app.git")]
    [InlineData("git@gitlab.example.com:ada/web-app.git")]
    [InlineData("ftp::https://gitlab.com/ada/web-app.git")]
    [InlineData("https://github.com/ada/web-app")]
    [InlineData("git@github.com:ada/web-app.git")]
    [InlineData("http://127.0.0.1:47398/ada/web-app")]
    [InlineData("/srv/git/ada/web-app.git")]
    [InlineData("C:/git/ada/web-app")]
    // Git and curl end the authority at ? and #, curl on Windows at \ too: git sends these six to
    // evil.example (MEASURED by WP-GL's review, git 2.56.0.windows.4 through a local proxy).
    [InlineData("https://evil.example#@gitlab.com/ada/web-app.git")]
    [InlineData("https://evil.example?@gitlab.com/ada/web-app.git")]
    [InlineData("https://evil.example\\@gitlab.com/ada/web-app.git")]
    [InlineData("http://evil.example#@gitlab.com/ada/web-app.git")]
    [InlineData("http://evil.example?@gitlab.com/ada/web-app.git")]
    [InlineData("http://evil.example\\@gitlab.com/ada/web-app.git")]
    // Curl splits the user at the first @ and takes the rest as the host, which it rejects: git
    // reaches no host for these four ("URL rejected: Bad hostname", MEASURED by WP-GL's third
    // review, git 2.56.0.windows.4 through a local recording proxy that saw no CONNECT).
    [InlineData("https://a@evil.example@gitlab.com/ada/web-app.git")]
    [InlineData("http://a@evil.example@gitlab.com/ada/web-app")]
    [InlineData("https://a:b@evil.example@gitlab.com/ada/web-app.git")]
    [InlineData("https://Ada@evil.example@gitlab.com/ada/web-app.git")]
    // The same characters before an ssh or scp-like host are refused alike (fail closed).
    [InlineData("ssh://evil.example#@gitlab.com/ada/web-app.git")]
    [InlineData("evil.example#@gitlab.com:ada/web-app.git")]
    [InlineData("git?x@gitlab.com:ada/web-app.git")]
    [InlineData("git\\x@gitlab.com:ada/web-app.git")]
    // Git url-decodes an ssh:// URL before it finds the host, and unwraps [...] at the host's start
    // or after @[: git sends these to evil.example over ssh (MEASURED by WP-GL's review, git
    // 2.56.0.windows.4 with a GIT_SSH_COMMAND that recorded its arguments; the other altssh and
    // www forms and the scp-like %2F refused alike, fail closed).
    [InlineData("[evil.example]@gitlab.com:ada/web-app.git")]
    [InlineData("a@[evil.example]@gitlab.com:ada/web-app.git")]
    [InlineData("git@[evil.example]@www.gitlab.com:ada/web-app.git")]
    [InlineData("git@evil.example%2F@gitlab.com:ada/web-app.git")]
    [InlineData("ssh://[evil.example]@gitlab.com/ada/web-app.git")]
    [InlineData("ssh://a@[evil.example]@gitlab.com/ada/web-app.git")]
    [InlineData("ssh://git@[evil.example]@altssh.gitlab.com:443/ada/web-app.git")]
    [InlineData("ssh://[evil.example]@altssh.gitlab.com:443/ada/web-app.git")]
    [InlineData("ssh://git@[evil.example]@www.gitlab.com/ada/web-app.git")]
    [InlineData("ssh://%5Bevil.example%5D@gitlab.com/ada/web-app.git")]
    [InlineData("ssh://evil.example%2F@gitlab.com/ada/web-app.git")]
    [InlineData("ssh://git%40evil.example%2F@gitlab.com/ada/web-app.git")]
    [InlineData("ssh://evil.example%2F@altssh.gitlab.com:443/ada/web-app.git")]
    public void Gitlab_com_refuses_other_hosts_ports_forms_and_paths(string url)
    {
        Assert.Null(GitLabUrls.Recognise(GitLabEndpoints.GitLabCom, url));
    }

    [Theory]
    [InlineData("http://127.0.0.1:47398/ada/web-app", "ada", "web-app")]
    [InlineData("http://127.0.0.1:47398/northfield/platform/web-app.git", "northfield/platform", "web-app")]
    [InlineData("http://127.0.0.1:47398/northfield/platform/web-app.git/", "northfield/platform", "web-app")]
    [InlineData("HTTP://127.0.0.1:47398/Northfield/Platform/Web-App", "Northfield/Platform", "Web-App")]
    public void The_fakes_endpoints_recognise_their_own_scheme_host_and_port_alone(string url, string owner, string name)
    {
        var repository = GitLabUrls.Recognise(Fake, url);

        Assert.NotNull(repository);
        Assert.Equal((HostingProvider.GitLab, "127.0.0.1:47398", owner, name), (repository.Provider, repository.Host, repository.Owner, repository.Name));
    }

    [Theory]
    [InlineData("https://127.0.0.1:47398/ada/web-app")]
    [InlineData("http://127.0.0.1:47397/ada/web-app")]
    [InlineData("http://127.0.0.1:47399/ada/web-app")]
    [InlineData("http://127.0.0.1/ada/web-app")]
    // An empty port is http's 80, not the fake's.
    [InlineData("http://127.0.0.1:/ada/web-app")]
    [InlineData("http://localhost:47398/ada/web-app")]
    [InlineData("http://127.0.0.2:47398/ada/web-app")]
    [InlineData("http://ada@127.0.0.1:47398/ada/web-app")]
    [InlineData("http://x-access-token@127.0.0.1:47398/ada/web-app")]
    [InlineData("ssh://git@127.0.0.1:47398/ada/web-app")]
    [InlineData("git@127.0.0.1:ada/web-app.git")]
    [InlineData("http://127.0.0.1:47398/ada")]
    [InlineData("http://127.0.0.1:47398/ada/web-app/-/merge_requests")]
    [InlineData("http://127.0.0.1:47398/ada/web%2Dapp")]
    [InlineData("https://gitlab.com/ada/web-app")]
    [InlineData("git@gitlab.com:ada/web-app.git")]
    public void The_fakes_endpoints_refuse_other_ports_schemes_users_and_gitlab_com(string url)
    {
        Assert.Null(GitLabUrls.Recognise(Fake, url));
    }

    [Fact]
    public void A_namespace_of_21_segments_is_recognised_and_one_of_22_is_not()
    {
        var namespace21 = string.Join('/', Enumerable.Range(1, 21).Select(level => "g" + level));
        var namespace22 = namespace21 + "/g22";

        var deep = GitLabUrls.Recognise(GitLabEndpoints.GitLabCom, $"https://gitlab.com/{namespace21}/project.git");
        Assert.Equal((namespace21, "project"), (deep!.Owner, deep.Name));
        Assert.Equal(namespace21, GitLabUrls.Recognise(Fake, $"http://127.0.0.1:47398/{namespace21}/project")!.Owner);
        Assert.Equal(namespace21, GitLabUrls.Recognise(GitLabEndpoints.GitLabCom, $"git@gitlab.com:{namespace21}/project.git")!.Owner);

        Assert.Null(GitLabUrls.Recognise(GitLabEndpoints.GitLabCom, $"https://gitlab.com/{namespace22}/project.git"));
        Assert.Null(GitLabUrls.Recognise(Fake, $"http://127.0.0.1:47398/{namespace22}/project"));
    }

    [Theory]
    [InlineData("northfield/platform/web-app", "northfield/platform", "web-app")]
    [InlineData("ada/web-app", "ada", "web-app")]
    [InlineData("Ada/Web.App", "Ada", "Web.App")]
    public void A_path_with_namespace_from_the_api_splits_into_namespace_and_name(string path, string owner, string name)
    {
        Assert.Equal((owner, name), GitLabUrls.SplitPathWithNamespace(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("web-app")]
    [InlineData("ada/web-app/")]
    [InlineData("ada/web app")]
    [InlineData("ada/../web-app")]
    [InlineData("ada/-/web-app")]
    [InlineData("ada/web%2Fapp")]
    [InlineData("ada/web-app\n")]
    public void A_path_with_namespace_that_breaks_the_rules_gives_nothing(string? path)
    {
        Assert.Null(GitLabUrls.SplitPathWithNamespace(path));
    }

    [Fact]
    public void The_provider_recognises_by_its_endpoints_and_names_its_kind_host_and_web_base()
    {
        using var com = Provider(GitLabEndpoints.GitLabCom);
        using var fake = Provider(Fake);

        Assert.Equal((HostingProvider.GitLab, "gitlab.com", "https://gitlab.com/"), (com.Kind, com.Host, com.WebBase.AbsoluteUri));
        Assert.Equal(("127.0.0.1:47398", "http://127.0.0.1:47398/"), (fake.Host, fake.WebBase.AbsoluteUri));
        Assert.Equal(new HostedRepository(HostingProvider.GitLab, "gitlab.com", "northfield/platform", "web-app"), com.Recognise("git@gitlab.com:northfield/platform/web-app.git"));
        Assert.Null(com.Recognise("http://127.0.0.1:47398/northfield/platform/web-app.git"));
        Assert.Equal("127.0.0.1:47398", fake.Recognise("http://127.0.0.1:47398/northfield/platform/web-app.git")!.Host);
        Assert.Null(fake.Recognise("https://gitlab.com/northfield/platform/web-app.git"));
    }

    [Theory]
    [InlineData("refs/merge-requests/21/head")]
    [InlineData("refs/merge-requests/21/merge")]
    [InlineData("refs/merge-requests/21/train")]
    [InlineData("refs/merge-requests/foo")]
    [InlineData("refs/merge-requests/fix/typo")]
    [InlineData("refs/merge-requests/-1/head")]
    [InlineData("refs/merge-requests//head")]
    [InlineData("refs/pull/fix/head")]
    [InlineData("refs/pull/12/head")]
    [InlineData("refs/heads/feature/search")]
    [InlineData("main")]
    public void The_push_guard_is_PullRequestRefs_rule_for_GitLab(string remoteRef)
    {
        using var provider = Provider(GitLabEndpoints.GitLabCom);

        Assert.Equal(PullRequestRefs.SourceCanExist(HostingProvider.GitLab, remoteRef), provider.SourceCanExist(remoteRef));
        Assert.Equal(remoteRef is not ("refs/merge-requests/fix/typo" or "refs/merge-requests/-1/head" or "refs/merge-requests//head"), provider.SourceCanExist(remoteRef));
    }

    [Fact]
    public void A_requests_head_ref_is_refs_merge_requests_iid_head_in_the_target_for_a_fork_too()
    {
        using var provider = Provider(GitLabEndpoints.GitLabCom);
        var target = new HostedRepository(HostingProvider.GitLab, "gitlab.com", "northfield/platform", "web-app");
        var fork = new HostedRepository(HostingProvider.GitLab, "gitlab.com", "ada", "web-app");
        PullRequest Pull(int iid, HostedRepository? head) =>
            new(iid, "T", "ada", false, new Uri("https://gitlab.com/x"), "fix-typo", "", head, "main", target, DateTimeOffset.MinValue);

        Assert.Equal("refs/merge-requests/21/head", provider.PullRequestHeadRef(Pull(21, target)));
        Assert.Equal("refs/merge-requests/23/head", provider.PullRequestHeadRef(Pull(23, fork)));
        Assert.Equal("refs/merge-requests/24/head", provider.PullRequestHeadRef(Pull(24, null)));
        Assert.Equal(PullRequestRefs.HeadOf(HostingProvider.GitLab, 7), provider.PullRequestHeadRef(Pull(7, target)));
    }

    [Fact]
    public void Coverage_is_the_web_bases_origin_with_the_git_twin_and_the_helpers_user_and_covers_the_accounts_host_alone()
    {
        using var com = Provider(GitLabEndpoints.GitLabCom);
        using var fake = Provider(Fake);
        var ada = new AccountKey(HostingProvider.GitLab, "gitlab.com", "Ada");

        var coverage = com.CoverageOf(ada);

        Assert.Equal((ada, "https://gitlab.com/", true, true, (string?)null, false),
            (coverage.Account, coverage.Origin.AbsoluteUri, coverage.GitTwin, coverage.HelperUserIsAccounts, coverage.Organization, coverage.OrganizationHost));
        Assert.Equal("http://127.0.0.1:47398/", fake.CoverageOf(new AccountKey(HostingProvider.GitLab, "127.0.0.1:47398", "ada")).Origin.AbsoluteUri);

        var nested = new HostedRepository(HostingProvider.GitLab, "gitlab.com", "northfield/platform/services", "billing");
        Assert.True(com.Covers(nested, ada));
        Assert.True(com.Covers(nested with { Host = "GitLab.com" }, ada));
        Assert.False(com.Covers(nested with { Host = "gitlab.example.com" }, ada));
        Assert.False(com.Covers(nested with { Provider = HostingProvider.GitHub }, ada));
        Assert.False(com.Covers(nested, new AccountKey(HostingProvider.GitHub, "gitlab.com", "ada")));
        Assert.False(com.Covers(nested with { Host = "127.0.0.1:47398" }, new AccountKey(HostingProvider.GitLab, "127.0.0.1:47398", "ada")));
        Assert.True(fake.Covers(nested with { Host = "127.0.0.1:47398" }, new AccountKey(HostingProvider.GitLab, "127.0.0.1:47398", "ada")));
        Assert.False(fake.Covers(nested, ada));
    }

    [Fact]
    public void The_token_pages_and_a_nested_projects_merge_requests_page_are_on_the_web_base()
    {
        using var com = Provider(GitLabEndpoints.GitLabCom);
        using var fake = Provider(Fake);
        var nested = new HostedRepository(HostingProvider.GitLab, "gitlab.com", "northfield/platform/services/billing", "invoice-generation-and-reconciliation");

        Assert.Equal("https://gitlab.com/-/user_settings/personal_access_tokens/granular/new", com.TokenPage(null).AbsoluteUri);
        Assert.Equal("https://gitlab.com/-/user_settings/personal_access_tokens/granular/new", com.TokenPage("ignored").AbsoluteUri);
        Assert.Equal("https://gitlab.com/-/user_settings/personal_access_tokens?name=VisualCommit&scopes=api", com.LegacyTokenPage.AbsoluteUri);
        Assert.Equal("https://gitlab.com/northfield/platform/services/billing/invoice-generation-and-reconciliation/-/merge_requests", com.PullRequestsPage(nested).AbsoluteUri);
        Assert.Equal("https://gitlab.com/Ada.Lovelace/web_app/-/merge_requests", com.PullRequestsPage(nested with { Owner = "Ada.Lovelace", Name = "web_app" }).AbsoluteUri);
        Assert.Equal("http://127.0.0.1:47398/-/user_settings/personal_access_tokens/granular/new", fake.TokenPage(null).AbsoluteUri);
        Assert.Equal("http://127.0.0.1:47398/-/user_settings/personal_access_tokens?name=VisualCommit&scopes=api", fake.LegacyTokenPage.AbsoluteUri);
        Assert.Equal("http://127.0.0.1:47398/ada/web-app/-/merge_requests", fake.PullRequestsPage(nested with { Host = "127.0.0.1:47398", Owner = "ada", Name = "web-app" }).AbsoluteUri);
    }

    /// <summary>A provider that sends nothing: every test here is without the network.</summary>
    private static GitLabProvider Provider(GitLabEndpoints endpoints) =>
        new(endpoints, () => new RecordingHttpHandler(_ => throw new InvalidOperationException("No request is sent here.")), new ListLog(), TimeProvider.System);
}
