using VisualCommit.Core.Hosting;
using VisualCommit.Hosting.GitLab;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// Which remote URLs are GitLab's (docs/phase-7b-design.md 7.2, 7.6): every form on gitlab.com and
/// on the fake's endpoints, a namespace of 21 segments but not 22, and the refusals (other hosts and
/// ports, one segment, a web address's <c>/-/</c>, <c>..</c>, <c>%2F</c>, a query, <c>git://</c>,
/// GitHub's URLs).
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
    [InlineData("https://gitlab.com:/ada/web-app")]
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
}
