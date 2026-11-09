using System.Net;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting.AzureDevOps;
using VisualCommit.Hosting.GitLab;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// <see cref="HostingOptions"/> and the endpoints (docs/phase-7a-design.md 1.4, 1.8, 6.5;
/// docs/phase-7b-design.md 1.4, 1.8): where the environment says each provider is, and the handler
/// every client is built on.
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

        // The other two providers' variables at none, as in every test process (7b's 1.8): unset,
        // each would be its service.
        Assert.Same(HostingOptions.Off, HostingOptions.FromEnvironment(GitHubAlone(value), log));
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

        var options = HostingOptions.FromEnvironment(GitHubAlone(value), log);

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

    [Fact]
    public void Azure_DevOps_and_GitLab_have_their_addresses_and_hosts()
    {
        var service = AzureDevOpsEndpoints.Service;
        Assert.Equal(("https://dev.azure.com/", true, "dev.azure.com"), (service.WebBase.AbsoluteUri, service.OrganizationHosts, service.Host));
        Assert.Null(service.ListProject);
        Assert.Equal("https://dev.azure.com/Fernhill-Labs/", service.ApiOf("Fernhill-Labs").AbsoluteUri);

        var azureFake = AzureDevOpsEndpoints.Local(47397);
        Assert.Equal(("http://127.0.0.1:47397/", false, "127.0.0.1:47397"), (azureFake.WebBase.AbsoluteUri, azureFake.OrganizationHosts, azureFake.Host));
        Assert.Equal("http://127.0.0.1:47397/Fernhill-Labs/", azureFake.ApiOf("Fernhill-Labs").AbsoluteUri);
        Assert.Equal("http://127.0.0.1:47397/a%2Fb%20c/", azureFake.ApiOf("a/b c").AbsoluteUri);
        Assert.Equal(azureFake, AzureDevOpsEndpoints.Local(47397));
        Assert.NotEqual(azureFake, azureFake with { ListProject = "Sandbox" });

        Assert.Equal(
            ("https://gitlab.com/", "https://gitlab.com/api/v4/", "gitlab.com"),
            (GitLabEndpoints.GitLabCom.WebBase.AbsoluteUri, GitLabEndpoints.GitLabCom.ApiBase.AbsoluteUri, GitLabEndpoints.GitLabCom.Host));
        var gitLabFake = GitLabEndpoints.Local(47398);
        Assert.Equal(
            ("http://127.0.0.1:47398/", "http://127.0.0.1:47398/api/v4/", "127.0.0.1:47398"),
            (gitLabFake.WebBase.AbsoluteUri, gitLabFake.ApiBase.AbsoluteUri, gitLabFake.Host));

        Assert.Throws<ArgumentOutOfRangeException>(() => AzureDevOpsEndpoints.Local(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GitLabEndpoints.Local(65536));
    }

    [Fact]
    public void Unset_the_new_variables_are_the_services_each_with_its_own_handler()
    {
        var log = new ListLog();

        var options = HostingOptions.FromEnvironment(_ => null, log);

        Assert.Equal(GitHubEndpoints.GitHubCom, options.GitHub);
        Assert.Equal(AzureDevOpsEndpoints.Service, options.AzureDevOps);
        Assert.Equal(GitLabEndpoints.GitLabCom, options.GitLab);
        Assert.False(options.AllOff);
        Assert.Empty(log.Lines);
        using var azure = Assert.IsType<SocketsHttpHandler>(options.CreateAzureDevOpsHandler!());
        using var gitLab = Assert.IsType<SocketsHttpHandler>(options.CreateGitLabHandler!());
        foreach (var handler in new[] { azure, gitLab })
        {
            Assert.False(handler.AllowAutoRedirect);
            Assert.False(handler.UseCookies);
            Assert.True(handler.UseProxy);
        }
    }

    [Theory]
    [InlineData(HostingOptions.AzureDevOpsVariable)]
    [InlineData(HostingOptions.GitLabVariable)]
    public void A_new_variable_names_its_fake_or_turns_its_provider_off_alone(string variable)
    {
        var log = new ListLog();
        Func<string, Func<string, string?>> only = value => name => name == variable ? value : "none";

        var local = HostingOptions.FromEnvironment(only(" http://127.0.0.1:47397 "), log);
        var none = HostingOptions.FromEnvironment(only("NONE"), log);

        Assert.Null(local.GitHub);
        Assert.False(local.AllOff);
        if (variable == HostingOptions.AzureDevOpsVariable)
        {
            Assert.Equal(AzureDevOpsEndpoints.Local(47397), local.AzureDevOps);
            Assert.Null(local.GitLab);
            Assert.Null(local.CreateGitLabHandler);
            using var handler = Assert.IsType<SocketsHttpHandler>(local.CreateAzureDevOpsHandler!());
            Assert.False(handler.UseProxy);
        }
        else
        {
            Assert.Equal(GitLabEndpoints.Local(47397), local.GitLab);
            Assert.Null(local.AzureDevOps);
            Assert.Null(local.CreateAzureDevOpsHandler);
            using var handler = Assert.IsType<SocketsHttpHandler>(local.CreateGitLabHandler!());
            Assert.False(handler.UseProxy);
        }

        Assert.Same(HostingOptions.Off, none);
        Assert.True(none.AllOff);
        Assert.Empty(log.Lines);
    }

    [Theory]
    [InlineData(HostingOptions.AzureDevOpsVariable, "Azure DevOps", "https://dev.azure.com/")]
    [InlineData(HostingOptions.AzureDevOpsVariable, "Azure DevOps", "http://x:azdo_FAKE-Fernhill-Labs-1@127.0.0.1:47397/")]
    [InlineData(HostingOptions.GitLabVariable, "GitLab", "http://127.0.0.1:47398/api/v4/")]
    [InlineData(HostingOptions.GitLabVariable, "GitLab", "glpat-FAKE-ada-1")]
    public void A_new_variables_value_that_is_not_accepted_turns_its_provider_off_and_stays_out_of_the_warning(string variable, string name, string value)
    {
        var log = new ListLog();

        var options = HostingOptions.FromEnvironment(n => n == variable ? value : null, log);

        Assert.Equal(GitHubEndpoints.GitHubCom, options.GitHub);
        Assert.Null(variable == HostingOptions.AzureDevOpsVariable ? options.AzureDevOps : options.GitLab);
        Assert.NotNull(variable == HostingOptions.AzureDevOpsVariable ? options.GitLab : options.AzureDevOps);
        var line = Assert.Single(log.Lines);
        Assert.Equal((LogLevel.Warning, $"{variable} is neither none nor http://127.0.0.1:<port>/: {name} is off."), line);
        Assert.DoesNotContain("FAKE", line.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_positional_record_and_Off_keep_the_new_providers_off_with_no_real_handler()
    {
        Func<HttpMessageHandler> handler = () => new HttpClientHandler();

        var gitHubOnly = new HostingOptions(GitHubEndpoints.GitHubCom, handler);
        var serviceWithoutHandler = HostingOptions.Off with { AzureDevOps = AzureDevOpsEndpoints.Service };

        Assert.Null(gitHubOnly.AzureDevOps);
        Assert.Null(gitHubOnly.CreateAzureDevOpsHandler);
        Assert.Null(gitHubOnly.GitLab);
        Assert.Null(gitHubOnly.CreateGitLabHandler);
        Assert.False(gitHubOnly.AllOff);
        Assert.True(HostingOptions.Off.AllOff);
        Assert.Null(HostingOptions.Off.AzureDevOps);
        Assert.Null(HostingOptions.Off.GitLab);
        Assert.False(serviceWithoutHandler.AllOff);
        Assert.Null(serviceWithoutHandler.CreateAzureDevOpsHandler);
    }

    [Fact]
    public void The_default_handler_of_any_api_follows_7as_rules()
    {
        using var service = Assert.IsType<SocketsHttpHandler>(HostingOptions.DefaultHandler(new Uri("https://gitlab.com/api/v4/"))());
        using var fake = Assert.IsType<SocketsHttpHandler>(HostingOptions.DefaultHandler(new Uri("http://127.0.0.1:47398/api/v4/"))());

        foreach (var handler in new[] { service, fake })
        {
            Assert.False(handler.AllowAutoRedirect);
            Assert.Equal(DecompressionMethods.All, handler.AutomaticDecompression);
            Assert.False(handler.UseCookies);
            Assert.Equal(TimeSpan.FromSeconds(10), handler.ConnectTimeout);
        }

        Assert.True(service.UseProxy);
        Assert.False(fake.UseProxy);
    }

    /// <summary>GitHub's variable as given, Azure DevOps' and GitLab's at <c>none</c>, as every test process has them.</summary>
    private static Func<string, string?> GitHubAlone(string? value) =>
        name => name == HostingOptions.GitHubVariable ? value : (name is HostingOptions.AzureDevOpsVariable or HostingOptions.GitLabVariable ? "none" : null);
}
