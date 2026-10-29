using System.Net;
using VisualCommit.Core.Logging;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// <see cref="HostingOptions"/> and <see cref="GitHubEndpoints"/> (docs/phase-7a-design.md 1.4,
/// 1.8, 6.5): where the environment says GitHub is, and the handler every client is built on.
/// </summary>
public sealed class HostingOptionsTests
{
    [Fact]
    public void Github_com_and_the_fake_have_their_addresses_and_hosts()
    {
        Assert.Equal(
            ("https://github.com/", "https://api.github.com/", "github.com"),
            (GitHubEndpoints.GitHubCom.WebBase.AbsoluteUri, GitHubEndpoints.GitHubCom.ApiBase.AbsoluteUri, GitHubEndpoints.GitHubCom.Host));

        var local = GitHubEndpoints.Local(47395);
        Assert.Equal(
            ("http://127.0.0.1:47395/", "http://127.0.0.1:47395/api/v3/", "127.0.0.1:47395"),
            (local.WebBase.AbsoluteUri, local.ApiBase.AbsoluteUri, local.Host));
        Assert.Equal(local, GitHubEndpoints.Local(47395));
        Assert.NotEqual(local, GitHubEndpoints.Local(47396));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Unset_is_github_com_without_a_log_line(string? value)
    {
        var log = new ListLog();

        var options = HostingOptions.FromEnvironment(name => name == HostingOptions.GitHubVariable ? value : null, log);

        Assert.Equal(GitHubEndpoints.GitHubCom, options.GitHub);
        Assert.Empty(log.Lines);
    }

    [Theory]
    [InlineData("http://127.0.0.1:47395/", 47395)]
    [InlineData("http://127.0.0.1:47395", 47395)]
    [InlineData(" http://127.0.0.1:50123/ ", 50123)]
    public void The_fakes_url_is_local_on_its_port(string value, int port)
    {
        var log = new ListLog();

        var options = HostingOptions.FromEnvironment(name => name == HostingOptions.GitHubVariable ? value : null, log);

        Assert.Equal(GitHubEndpoints.Local(port), options.GitHub);
        Assert.Empty(log.Lines);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("NONE")]
    public void None_is_off_without_a_log_line(string value)
    {
        var log = new ListLog();

        Assert.Same(HostingOptions.Off, HostingOptions.FromEnvironment(name => name == HostingOptions.GitHubVariable ? value : null, log));
        Assert.Empty(log.Lines);
    }

    [Theory]
    [InlineData("http://localhost:47395/")]
    [InlineData("https://127.0.0.1:47395/")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://127.0.0.1:47395/api/v3/")]
    [InlineData("https://github.com/")]
    [InlineData("github.com")]
    [InlineData("junk")]
    [InlineData("http://x:ghp_FAKE-octocat-1@127.0.0.1:47395/")]
    [InlineData("http://ghp_FAKE-octocat-1@127.0.0.1:47395/")]
    [InlineData("ghp_FAKE-octocat-1")]
    public void A_value_that_is_not_accepted_turns_GitHub_off_and_stays_out_of_the_warning(string value)
    {
        // Section 1's rule: no token in a log line. A user part is refused, and the warning names
        // the variable without its value, so nothing of what was typed reaches the log.
        var log = new ListLog();

        var options = HostingOptions.FromEnvironment(name => name == HostingOptions.GitHubVariable ? value : null, log);

        Assert.Same(HostingOptions.Off, options);
        Assert.Null(options.GitHub);
        var line = Assert.Single(log.Lines);
        Assert.Equal(
            (LogLevel.Warning, "VISUALCOMMIT_GITHUB_URL is neither none nor http://127.0.0.1:<port>/: GitHub is off."),
            line);
        Assert.DoesNotContain("FAKE", line.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Off_refuses_every_request()
    {
        using var client = new HttpClient(HostingOptions.Off.CreateHandler());

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => client.GetAsync(new Uri("https://api.github.com/user"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_default_handler_follows_no_redirect_keeps_no_cookie_and_leaves_the_proxy_out_for_loopback()
    {
        using var forGitHub = Assert.IsType<SocketsHttpHandler>(HostingOptions.DefaultHandler(GitHubEndpoints.GitHubCom)());
        using var forFake = Assert.IsType<SocketsHttpHandler>(HostingOptions.DefaultHandler(GitHubEndpoints.Local(47395))());

        foreach (var handler in new[] { forGitHub, forFake })
        {
            Assert.False(handler.AllowAutoRedirect);
            Assert.Equal(DecompressionMethods.All, handler.AutomaticDecompression);
            Assert.False(handler.UseCookies);
            Assert.Equal(TimeSpan.FromSeconds(10), handler.ConnectTimeout);
        }

        Assert.True(forGitHub.UseProxy);
        Assert.False(forFake.UseProxy);

        // Each call makes a handler of its own: a client disposes the one it was given.
        var create = HostingOptions.DefaultHandler(GitHubEndpoints.GitHubCom);
        using var first = create();
        using var second = create();
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task A1_on_github_com_goes_to_exactly_its_api_with_the_four_headers()
    {
        var recorder = new RecordingHttpHandler(_ => RecordingHttpHandler.Json(HttpStatusCode.OK,
            "{\"login\":\"Octo\",\"id\":1001,\"name\":null,\"avatar_url\":\"https://avatars.githubusercontent.com/u/1001?v=4\"}"));
        using var provider = new GitHub.GitHubProvider(GitHubEndpoints.GitHubCom, () => recorder, new ListLog(), TimeProvider.System);

        var account = await provider.CheckTokenAsync("github_pat_FAKE-octo-1", TestContext.Current.CancellationToken);

        var sent = Assert.Single(recorder.Sent);
        Assert.Equal(("GET", "https://api.github.com/user"), (sent.Method, sent.Url.AbsoluteUri));
        Assert.Equal(
            new[] { "Accept: application/vnd.github+json", "Authorization: Bearer github_pat_FAKE-octo-1", "User-Agent: VisualCommit/0.1.0", "X-GitHub-Api-Version: 2026-03-10" },
            sent.Headers.Select(header => $"{header.Key}: {header.Value}").Order(StringComparer.Ordinal));
        Assert.Equal(("github:github.com:octo", "Octo", (string?)null, "https://avatars.githubusercontent.com/u/1001?v=4"),
            (account.Value!.Key.Id, account.Value.Login, account.Value.Name, account.Value.AvatarUrl!.AbsoluteUri));
    }
}
