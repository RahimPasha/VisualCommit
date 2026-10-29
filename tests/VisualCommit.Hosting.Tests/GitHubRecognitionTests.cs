using VisualCommit.Core.Hosting;
using VisualCommit.Hosting.GitHub;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// Which remote URLs are GitHub's (docs/phase-7a-design.md 6.3, 6.5; ruling 23): every form on
/// github.com and on the fake's endpoints, the refusals, and the push guard's rule for
/// <c>refs/pull</c> (ruling 8).
/// </summary>
public sealed class GitHubRecognitionTests
{
    [Theory]
    [InlineData("https://github.com/octo/hello", "octo", "hello")]
    [InlineData("https://github.com/octo/hello.git", "octo", "hello")]
    [InlineData("https://github.com/octo/hello/", "octo", "hello")]
    [InlineData("https://github.com/octo/hello.git/", "octo", "hello")]
    [InlineData("https://www.github.com/octo/hello", "octo", "hello")]
    [InlineData("https://GitHub.COM/Octo/Hello", "Octo", "Hello")]
    [InlineData("https://octo@github.com/octo/hello", "octo", "hello")]
    [InlineData("https://octo:secret@github.com/octo/hello.git", "octo", "hello")]
    [InlineData("https://github.com:443/octo/hello", "octo", "hello")]
    [InlineData("HTTPS://github.com/octo/hello", "octo", "hello")]
    [InlineData("http://github.com/octo/hello", "octo", "hello")]
    [InlineData("http://octo@github.com:80/octo/hello.git", "octo", "hello")]
    [InlineData("ssh://git@github.com/octo/hello.git", "octo", "hello")]
    [InlineData("ssh://github.com:22/octo/hello", "octo", "hello")]
    [InlineData("ssh://git@www.github.com/octo/hello", "octo", "hello")]
    [InlineData("ssh://git@ssh.github.com:443/octo/hello.git", "octo", "hello")]
    [InlineData("git@github.com:octo/hello.git", "octo", "hello")]
    [InlineData("git@www.github.com:octo/hello", "octo", "hello")]
    [InlineData("git://github.com/octo/hello.git", "octo", "hello")]
    [InlineData("https://github.com/a-b-c/my.repo_name-1", "a-b-c", "my.repo_name-1")]
    [InlineData("https://github.com/octo/hello.git.git", "octo", "hello.git")]
    [InlineData("https://github.com/o/.hidden", "o", ".hidden")]
    public void Github_com_recognises_its_forms_with_the_owner_and_name_as_written(string url, string owner, string name)
    {
        var repository = GitHubUrls.Recognise(GitHubEndpoints.GitHubCom, url);

        Assert.NotNull(repository);
        Assert.Equal((HostingProvider.GitHub, "github.com", owner, name), (repository.Provider, repository.Host, repository.Owner, repository.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" https://github.com/octo/hello")]
    [InlineData("https://github.com/octo/hello ")]
    [InlineData("https://github.com/octo/hel\tlo")]
    [InlineData("https://gist.github.com/octo/hello")]
    [InlineData("https://github.com.evil.example/octo/hello")]
    [InlineData("https://evil.example/github.com/octo")]
    [InlineData("https://api.github.com/octo/hello")]
    [InlineData("https://github.com/octo")]
    [InlineData("https://github.com/octo/")]
    [InlineData("https://github.com/octo/hello/pulls")]
    [InlineData("https://github.com//octo/hello")]
    [InlineData("https://github.com/octo/hello//")]
    [InlineData("https://github.com/octo/hello?tab=readme")]
    [InlineData("https://github.com/octo/hello#readme")]
    [InlineData("https://github.com/octo/hel%6Co")]
    [InlineData("https://github.com/octo/hello/.git")]
    [InlineData("https://github.com/-octo/hello")]
    [InlineData("https://github.com/octo_cat/hello")]
    [InlineData("https://github.com/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/hello")]
    [InlineData("https://github.com/octo/.")]
    [InlineData("https://github.com/octo/..")]
    [InlineData("https://github.com/octo/hello!")]
    [InlineData("https://github.com:8443/octo/hello")]
    [InlineData("https://github.com:/octo/hello")]
    [InlineData("https://@github.com/octo/hello")]
    [InlineData("http://github.com:443/octo/hello")]
    [InlineData("http://octo:secret@github.com/octo/hello")]
    [InlineData("ssh://ssh.github.com/octo/hello")]
    [InlineData("ssh://git@ssh.github.com:22/octo/hello")]
    [InlineData("ssh://git:secret@github.com/octo/hello")]
    [InlineData("git://github.com:9418/octo/hello")]
    [InlineData("git://git@github.com/octo/hello")]
    [InlineData("ftp://github.com/octo/hello")]
    [InlineData("github.com:octo/hello")]
    [InlineData("git@gist.github.com:octo/hello")]
    [InlineData("git@github.com:octo")]
    [InlineData("git@github.com:/octo/hello")]
    [InlineData("http://localhost:47395/octo/hello")]
    [InlineData("http://127.0.0.1:47395/octo/hello.git")]
    [InlineData(@"C:\repos\octo\hello")]
    [InlineData("/srv/git/octo/hello.git")]
    [InlineData("../octo/hello")]
    [InlineData("file:///srv/git/octo/hello.git")]
    public void Github_com_refuses_everything_else(string url) =>
        Assert.Null(GitHubUrls.Recognise(GitHubEndpoints.GitHubCom, url));

    [Theory]
    [InlineData("http://127.0.0.1:47395/octo/hello.git", "octo", "hello")]
    [InlineData("http://127.0.0.1:47395/octo/hello", "octo", "hello")]
    [InlineData("http://127.0.0.1:47395/acme/tool.git/", "acme", "tool")]
    [InlineData("HTTP://127.0.0.1:47395/octo/hello", "octo", "hello")]
    public void The_fake_is_recognised_by_its_exact_scheme_host_and_port(string url, string owner, string name)
    {
        var repository = GitHubUrls.Recognise(GitHubEndpoints.Local(47395), url);

        Assert.NotNull(repository);
        Assert.Equal((HostingProvider.GitHub, "127.0.0.1:47395", owner, name), (repository.Provider, repository.Host, repository.Owner, repository.Name));
    }

    [Theory]
    [InlineData("https://127.0.0.1:47395/octo/hello.git")]
    [InlineData("http://localhost:47395/octo/hello.git")]
    [InlineData("http://127.0.0.2:47395/octo/hello.git")]
    [InlineData("http://127.0.0.1/octo/hello.git")]
    [InlineData("http://127.0.0.1:47391/octo/hello.git")]
    [InlineData("http://127.0.0.1:47393/octo/hello.git")]
    [InlineData("http://127.0.0.1:47396/octo/hello.git")]
    [InlineData("http://127.0.0.1:47395/octo")]
    [InlineData("http://127.0.0.1:47395/octo/hello/pulls")]
    [InlineData("http://127.0.0.1:47395/octo/hello?x=1")]
    [InlineData("http://octo@127.0.0.1:47395/octo/hello.git")]
    [InlineData("http://octo:secret@127.0.0.1:47395/octo/hello.git")]
    [InlineData("http://127.0.0.1:47395/api/v3/user")]
    [InlineData("ssh://git@127.0.0.1:47395/octo/hello.git")]
    [InlineData("git@127.0.0.1:octo/hello.git")]
    [InlineData("https://github.com/octo/hello.git")]
    [InlineData("git@github.com:octo/hello.git")]
    [InlineData(@"C:\repos\octo\hello")]
    public void The_fakes_endpoints_refuse_every_other_url(string url) =>
        Assert.Null(GitHubUrls.Recognise(GitHubEndpoints.Local(47395), url));

    [Theory]
    [InlineData("refs/pull/1/head", true)]
    [InlineData("refs/pull/12/merge", true)]
    [InlineData("refs/pull/007/head", true)]
    [InlineData("refs/pull/fix-typo/head", false)]
    [InlineData("refs/pull/fix/typo/head", false)]
    [InlineData("refs/pull/1a/head", false)]
    [InlineData("refs/pull/x/merge", false)]
    [InlineData("refs/pull//head", false)]
    [InlineData("refs/pull/\u0661/head", false)]
    [InlineData("refs/pull/x/files", true)]
    [InlineData("refs/pull/x", true)]
    [InlineData("refs/pull/head", true)]
    [InlineData("refs/pulls/x/head", true)]
    [InlineData("refs/heads/pr/fix-typo", true)]
    [InlineData("refs/heads/pr/12", true)]
    [InlineData("pr/fix-typo", true)]
    [InlineData("", true)]
    public void Only_a_pull_ref_whose_number_is_not_all_digits_cannot_exist(string remoteRef, bool canExist) =>
        Assert.Equal(canExist, GitHubUrls.SourceCanExist(remoteRef));
}
