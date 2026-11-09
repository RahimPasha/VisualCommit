using System.Net;
using Avalonia.Headless.XUnit;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using VisualCommit.Hosting.AzureDevOps;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// The harness of phase 7b tried once, without pictures (docs/phase-7b-design.md 1.8, 1.9, 2.4 to
/// 2.8): the process's safety nets (ruling 27's names), the three providers' tripwires with and
/// without the fakes, the start's refusals, the scenario "hosting7b" and its serving on port 0, the
/// log pattern, the ports, the picture pattern and the folder.
/// </summary>
public sealed class Phase7bSupportTests : Phase7bChecks
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    /// <summary>Ruling 27's names (1.8; g4 D2.3), written out rather than read from the list under test.</summary>
    private static readonly string[] TokenVariables =
    [
        "GITLAB_TOKEN",
        "GITLAB_ACCESS_TOKEN",
        "OAUTH_TOKEN",
        "GITLAB_HOST",
        "GITLAB_URI",
        "GL_HOST",
        "GITLAB_API_HOST",
        "GL_TOKEN",
        "CI_JOB_TOKEN",
        "AZURE_DEVOPS_EXT_PAT",
        "SYSTEM_ACCESSTOKEN",
        "GCM_AZREPOS_SP_SECRET",
        "GCM_AZREPOS_WIF_ASSERTION",
        "VISUALCOMMIT_LIVE_AZURE_DEVOPS_TOKEN",
        "VISUALCOMMIT_LIVE_AZURE_DEVOPS_REPO",
        "VISUALCOMMIT_LIVE_GITLAB_TOKEN",
        "VISUALCOMMIT_LIVE_GITLAB_REPO",
    ];

    [Fact]
    public void The_process_starts_with_every_provider_off_tokens_in_memory_and_no_tools_token()
    {
        foreach (var variable in new[] { HostingOptions.GitHubVariable, HostingOptions.AzureDevOpsVariable, HostingOptions.GitLabVariable })
        {
            Assert.Equal("none", Environment.GetEnvironmentVariable(variable));
            Assert.Contains(new KeyValuePair<string, string?>(variable, "none"), GitIsolation.MachineIndependence);
        }

        Assert.Equal("VISUALCOMMIT_AZURE_DEVOPS_URL", HostingOptions.AzureDevOpsVariable);
        Assert.Equal("VISUALCOMMIT_GITLAB_URL", HostingOptions.GitLabVariable);
        Assert.Equal("memory", Environment.GetEnvironmentVariable(VisualCommit.Hosting.Tokens.TokenStores.Variable));
        Assert.All(TokenVariables, variable => Assert.Contains(variable, GitIsolation.HostingTokenVariables));
        Assert.All(TokenVariables, variable => Assert.Contains(new KeyValuePair<string, string?>(variable, null), GitIsolation.MachineIndependence));
        Assert.All(TokenVariables, variable => Assert.Null(Environment.GetEnvironmentVariable(variable)));
    }

    [AvaloniaFact]
    public async Task A_start_without_fakes_is_off_and_every_providers_slot_lets_no_request_out()
    {
        using var data = new TempDirectory("phase7b support");
        using var app = ShellDriver.Start(data.Path);

        Assert.True(app.Session.HostingStarted.IsCompletedSuccessfully);
        Assert.True(app.Hosting.AllOff);
        Assert.Null(app.AzureDevOps);
        Assert.Null(app.GitLab);
        Assert.Null(app.GitHub);
        Assert.True(app.Session.Services.Hosting.IsOff);
        Assert.Empty(app.Session.Services.Hosting.Providers);

        var slots = new (Func<HttpMessageHandler>? Create, Uri Address)[]
        {
            (app.Hosting.CreateHandler, new Uri("https://github.com/")),
            (app.Hosting.CreateAzureDevOpsHandler, new Uri("https://dev.azure.com/")),
            (app.Hosting.CreateGitLabHandler, new Uri("https://gitlab.com/api/v4/user")),
        };
        foreach (var (create, address) in slots)
        {
            Assert.NotNull(create);
            using var client = new HttpClient(create());
            var refused = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(address, Cancelled));
            Assert.Equal(LoopbackOnlyHandler.RefusalMessage, refused.Message);
        }

        Assert.Equal(slots.Select(slot => (Uri?)slot.Address), app.RefusedRequests.Addresses);
    }

    [AvaloniaFact]
    public async Task The_three_fakes_on_port_0_give_their_endpoints_and_each_slot_refuses_other_hosts()
    {
        using var data = new TempDirectory("phase7b support");
        using var root = new TempDirectory("phase7b fakes");
        await using var azureDevOps = await FakeAzureDevOps.StartAsync(Path.Combine(root.Path, "azure-devops"));
        await using var gitLab = await FakeGitLab.StartAsync(Path.Combine(root.Path, "gitlab"));
        await using var gitHub = await FakeGitHub.StartAsync(Path.Combine(root.Path, "github"));
        using var app = ShellDriver.Start(data.Path, gitHub: gitHub, azureDevOps: azureDevOps, gitLab: gitLab);

        Assert.Same(azureDevOps, app.AzureDevOps);
        Assert.Same(gitLab, app.GitLab);
        Assert.Same(gitHub, app.GitHub);
        Assert.Equal(azureDevOps.Endpoints, app.Hosting.AzureDevOps);
        Assert.Equal(gitLab.Endpoints, app.Hosting.GitLab);
        Assert.Equal(gitHub.Endpoints, app.Hosting.GitHub);
        Assert.Equal($"http://127.0.0.1:{azureDevOps.Port}/", azureDevOps.Endpoints.WebBase.AbsoluteUri);
        Assert.False(azureDevOps.Endpoints.OrganizationHosts);
        Assert.Equal($"http://127.0.0.1:{gitLab.Port}/api/v4/", gitLab.Endpoints.ApiBase.AbsoluteUri);
        Assert.NotEqual(FakeAzureDevOps.InProcessPort, azureDevOps.Port);
        Assert.NotEqual(FakeGitLab.InProcessPort, gitLab.Port);
        Assert.Equal(("Azure DevOps", "GitLab"), (azureDevOps.DisplayName, gitLab.DisplayName));

        var slots = new (Func<HttpMessageHandler>? Create, int Port)[]
        {
            (app.Hosting.CreateHandler, gitHub.Port),
            (app.Hosting.CreateAzureDevOpsHandler, azureDevOps.Port),
            (app.Hosting.CreateGitLabHandler, gitLab.Port),
        };
        foreach (var (create, port) in slots)
        {
            using var client = new HttpClient(create!());
            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(new Uri("https://example.com/"), Cancelled));

            // A loopback request goes through to the fake: git's 404 for a repository none serves (a
            // .git-less info/refs would be the fake of GitLab's 301, design 4.5).
            using var passed = await client.GetAsync(new Uri($"http://127.0.0.1:{port}/nothing/here.git/info/refs"), Cancelled);
            Assert.Equal(HttpStatusCode.NotFound, passed.StatusCode);
        }

        Assert.Equal(Enumerable.Repeat<Uri?>(new Uri("https://example.com/"), 3), app.RefusedRequests.Addresses);
        Assert.Empty(azureDevOps.Errors);
        Assert.Empty(gitLab.Errors);
        Assert.Empty(gitHub.Errors);
    }

    [AvaloniaFact]
    public async Task A_new_fake_with_hosting_options_and_an_in_process_new_fake_are_refused()
    {
        using var data = new TempDirectory("phase7b support");
        using var root = new TempDirectory("phase7b fakes");
        await using var azureDevOps = FakeAzureDevOps.InProcess(Path.Combine(root.Path, "azure-devops"));
        await using var gitLab = FakeGitLab.InProcess(Path.Combine(root.Path, "gitlab"));
        Assert.Equal((2, 3), (azureDevOps.Port, gitLab.Port));
        Assert.Equal(AzureDevOpsEndpoints.Local(2), azureDevOps.Endpoints);

        Assert.Equal("hosting", Assert.Throws<ArgumentException>(() => ShellDriver.Start(data.Path, hosting: HostingOptions.Off, azureDevOps: azureDevOps)).ParamName);
        Assert.Equal("hosting", Assert.Throws<ArgumentException>(() => ShellDriver.StartWithTabs(data.Path, [null], hosting: HostingOptions.Off, gitLab: gitLab)).ParamName);
        var inProcess = Assert.Throws<ArgumentException>(() => ShellDriver.Start(data.Path, azureDevOps: azureDevOps));
        Assert.Equal("azureDevOps", inProcess.ParamName);
        Assert.Contains("in-process fake of Azure DevOps", inProcess.Message, StringComparison.Ordinal);
        Assert.Equal("gitLab", Assert.Throws<ArgumentException>(() => ShellDriver.StartWithTabs(data.Path, [null], gitLab: gitLab)).ParamName);
        Assert.Empty(Directory.EnumerateFileSystemEntries(data.Path));

        // In process, a fake answers by path from any host, git's paths 404.
        using var client = new HttpClient(azureDevOps.CreateHandler());
        using var answered = await client.GetAsync(new Uri("https://dev.azure.com/Fernhill-Labs/_apis/connectionData"), Cancelled);
        Assert.Equal(HttpStatusCode.NotFound, answered.StatusCode);
        Assert.Equal(("dev.azure.com", 404), (azureDevOps.Requests[^1].Host, azureDevOps.Requests[^1].Status));
    }

    [Fact]
    public async Task The_scenario_holds_its_bare_folders_clones_urls_and_authors()
    {
        using var copy = await Scenarios.Hosting7bAsync();
        var manifest = copy.OpenSibling(Manifest);
        var webApp = copy.OpenSibling(WebApp);
        string[] bare =
        [
            "azure-devops/Fernhill-Labs/Mobile App/_git/mobile-app",
            "azure-devops/Fernhill-Labs/Mobile App/_git/mobile-app-fork",
            "azure-devops/Fernhill-Labs/Mobile App/_git/sandbox",
            "azure-devops/Fernhill-Labs/Mobile App/_git/" + LongRepository,
            "azure-devops/harbor-co/Docks/_git/manifest",
            "gitlab/northfield/platform/web-app.git",
            "gitlab/ada/web-app.git",
            "gitlab/" + LongProject + ".git",
            "github/octo/web-app.git",
        ];
        var folders = bare.Select(folder => Path.Combine([Path.GetDirectoryName(copy.Path)!, .. folder.Split('/')])).ToList();
        Assert.All(folders, folder => Assert.True(File.Exists(Path.Combine(folder, "HEAD")), folder));

        // The histories: subject, author and time, newest first.
        var mobile = folders[0];
        Assert.Equal(
            ["Add the docs folder|Test Author|12:02", "Add the greeting|Ada Lovelace|12:01", "Initial commit|Octo Cat|12:00"],
            await LogAsync(copy, mobile, "main"));
        Assert.Equal(["Add the sign-in page|Ada Lovelace|12:03"], (await LogAsync(copy, mobile, "feature/sign-in")).Take(1));
        Assert.Equal(["Write the guide|Octo Cat|12:04"], (await LogAsync(copy, mobile, "docs/guide")).Take(1));
        Assert.Equal(["Try an idea|Test Author|12:05"], (await LogAsync(copy, mobile, "topic/no-request")).Take(1));
        Assert.Equal(["Fix a typo in the greeting|Ada Lovelace|12:11"], (await LogAsync(copy, folders[1], "fix-typo")).Take(1));
        Assert.Empty(await RefsAsync(copy, folders[2]));
        Assert.Empty(await RefsAsync(copy, folders[3]));
        Assert.Equal(["Initial commit|Test Author|12:40"], await LogAsync(copy, folders[4], "main"));
        var web = folders[5];
        Assert.Equal(
            ["Add the docs folder|Test Author|12:22", "Add the footer|Ada Lovelace|12:21", "Initial commit|Octo Cat|12:20"],
            await LogAsync(copy, web, "main"));
        Assert.Equal(["Add the search box|Ada Lovelace|12:23"], (await LogAsync(copy, web, "feature/search")).Take(1));
        Assert.Equal(["Update the readme|Octo Cat|12:24"], (await LogAsync(copy, web, "docs/readme")).Take(1));
        Assert.Equal(["Try an idea|Test Author|12:25"], (await LogAsync(copy, web, "topic/no-request")).Take(1));
        Assert.Equal(["Fix a typo in the footer|Ada Lovelace|12:31"], (await LogAsync(copy, folders[6], "fix-typo")).Take(1));
        Assert.Empty(await RefsAsync(copy, folders[7]));
        Assert.Equal(await RevParseAsync(copy, web, "main"), await RevParseAsync(copy, folders[8], "main"));

        // The clones: on main, their tracking branches, their remotes on 1.8's ports, written last.
        Assert.Equal(MobileAppUrl, await ConfigAsync(copy, "remote.origin.url"));
        Assert.Equal(ManifestUrl, await ConfigAsync(manifest, "remote.origin.url"));
        Assert.Equal(WebAppUrl, await ConfigAsync(webApp, "remote.origin.url"));
        Assert.Equal(WebAppUpstreamUrl, await ConfigAsync(webApp, "remote.upstream.url"));
        Assert.Equal("main", await copy.CurrentBranchAsync());
        Assert.Equal("main", await manifest.CurrentBranchAsync());
        Assert.Equal("main", await webApp.CurrentBranchAsync());
        Assert.Equal("refs/heads/feature/sign-in", await ConfigAsync(copy, "branch.feature/sign-in.merge"));
        Assert.Equal("refs/heads/feature/search", await ConfigAsync(webApp, "branch.feature/search.merge"));
        Assert.Equal(
            ["refs/heads/feature/sign-in", "refs/heads/local-work", "refs/heads/main", "refs/remotes/origin/HEAD", "refs/remotes/origin/docs/guide", "refs/remotes/origin/feature/sign-in", "refs/remotes/origin/main", "refs/remotes/origin/topic/no-request"],
            await RefsAsync(copy, copy.Path));
        Assert.Contains("refs/remotes/upstream/HEAD", await RefsAsync(copy, webApp.Path));
        Assert.Equal(["Local change|Test Author|12:06"], (await LogAsync(copy, copy.Path, "local-work")).Take(1));
        Assert.Equal(["Local change|Test Author|12:26"], (await LogAsync(copy, webApp.Path, "local-work")).Take(1));

        // The local-only work is in no bare folder (13.7 and 13.8 push it first).
        var localWork = new[] { await RevParseAsync(copy, copy.Path, "local-work"), await RevParseAsync(copy, webApp.Path, "local-work") };
        foreach (var folder in folders)
        {
            var checkedObjects = await copy.GitWithInputAsync(string.Join('\n', localWork) + "\n", "-C", folder, "cat-file", "--batch-check");
            Assert.Equal([$"{localWork[0]} missing", $"{localWork[1]} missing"], checkedObjects.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')));
        }
    }

    [Fact]
    public async Task Served_on_port_0_every_remote_is_rewritten_and_the_tokens_are_the_scenarios()
    {
        var copy = await Scenarios.Hosting7bAsync();
        await using var served = await Scenarios.ServeHosting7bAsync(copy, 0, 0, 0, ownsCopy: true, ct: Cancelled);
        var (azureDevOps, gitLab, gitHub) = (served.AzureDevOps.Port, served.GitLab.Port, served.GitHub.Port);
        Assert.DoesNotContain(azureDevOps, new[] { 0, AzureDevOpsPort, FakeAzureDevOps.InProcessPort });
        Assert.DoesNotContain(gitLab, new[] { 0, GitLabPort, FakeGitLab.InProcessPort });
        Assert.DoesNotContain(gitHub, new[] { 0, GitHubPort, FakeGitHub.InProcessPort });

        Assert.Equal(WithPorts(MobileAppUrl, served), await ConfigAsync(served.MobileApp, "remote.origin.url"));
        Assert.Equal($"http://Fernhill-Labs@127.0.0.1:{azureDevOps}/Fernhill-Labs/Mobile%20App/_git/mobile-app", await ConfigAsync(served.MobileApp, "remote.origin.url"));
        Assert.Equal(WithPorts(ManifestUrl, served), await ConfigAsync(served.Manifest, "remote.origin.url"));
        Assert.Equal(WithPorts(WebAppUrl, served), await ConfigAsync(served.WebApp, "remote.origin.url"));
        Assert.Equal(WithPorts(WebAppUpstreamUrl, served), await ConfigAsync(served.WebApp, "remote.upstream.url"));
        Assert.Equal([AzureDevOpsToken, GitLabToken, GitLabLegacyToken, GitHubToken], served.Tokens);
        Assert.Equal((Organization, OtherOrganization), (served.Organization, served.OtherOrganization));
        Assert.DoesNotContain("FAKE", served.ToString(), StringComparison.Ordinal);

        // GitHub's fake serves octo/web-app, public, at the upstream's URL.
        var listed = await served.WebApp.GitAsync("ls-remote", "upstream", "refs/heads/main");
        Assert.Equal(await RevParseAsync(served.WebApp, served.WebApp.Path, "upstream/main"), listed.StandardOutput.Split('\t')[0]);
        Assert.Empty(served.GitHub.Errors);
        Assert.Empty(served.AzureDevOps.Errors);
        Assert.Empty(served.GitLab.Errors);
    }

    [Fact]
    public void The_log_pattern_takes_a_listed_repository_of_any_depth_and_7as_lines_as_before()
    {
        var deep = AppLogFile.PullRequestsListed(MobileApp).Match("09:00:00.000 [Debug] mobile-app: listed 3 open pull requests of Fernhill-Labs/Mobile App/mobile-app");
        Assert.True(deep.Success);
        Assert.Equal(("3", "Fernhill-Labs/Mobile App/mobile-app"), (deep.Groups["count"].Value, deep.Groups["repository"].Value));
        var nested = AppLogFile.PullRequestsListed(WebApp).Match("09:00:00.000 [Debug] web-app: listed 2 open pull requests of northfield/platform/web-app");
        Assert.Equal("northfield/platform/web-app", nested.Groups["repository"].Value);
        var sevenA = AppLogFile.PullRequestsListed("hello").Match("09:00:00.000 [Debug] hello: listed 4 open pull requests of octo/hello");
        Assert.Equal(("4", "octo/hello"), (sevenA.Groups["count"].Value, sevenA.Groups["repository"].Value));
        Assert.DoesNotMatch(AppLogFile.PullRequestsListed("hello"), "09:00:00.000 [Debug] hello: listed 4 open pull requests of octo");
        Assert.DoesNotMatch(AppLogFile.PullRequestsListed("hello"), "09:00:00.000 [Debug] hello: listed 4 open pull requests of octo/");
    }

    [Fact]
    public void The_ports_WithPorts_the_picture_names_and_the_folder_are_7bs()
    {
        Assert.Equal("7b", Phase);
        Assert.Equal((47397, 47398, 47399), (AzureDevOpsPort, GitLabPort, GitHubPort));
        Assert.Equal(
            "http://Fernhill-Labs@127.0.0.1:50001/x 127.0.0.1:50002/y 127.0.0.1:50003/z 47395",
            WithPorts("http://Fernhill-Labs@127.0.0.1:47397/x 127.0.0.1:47398/y 127.0.0.1:47399/z 47395", 50001, 50002, 50003));
        Assert.Equal("text", WithPorts("text", 1, 2, 3));
        Assert.Equal("http://127.0.0.1:47397/", WebBase());
        Assert.Equal("http://127.0.0.1:47398/", WebBase(GitLabPort));
        Assert.EndsWith(Path.Combine("artifacts", "visual", "phase-7b", "scripted"), VisualRun.FolderFor(Phase), StringComparison.Ordinal);
        Assert.Equal(HostingProvider.AzureDevOps, ProviderOf(Manifest));
        Assert.Equal(HostingProvider.GitLab, ProviderOf(WebApp));

        foreach (var name in new[] { "a03a-azure-dialog", "p02b-gitlab-rows", "b9-browser-listed", "n24b-form-draft", "1a-start" })
        {
            Assert.Matches(PictureName(), name);
        }

        foreach (var name in new[] { "g20a-badge", "c1a-commit", "r3-real", "t2-x", "o1-x", "q1-x", "a1a_bad", "a1abcd-too-long-step", "A1a-upper" })
        {
            Assert.DoesNotMatch(PictureName(), name);
        }

        Assert.Throws<ArgumentException>(() => ClaimPictureName("g20a-badge"));
    }

    private static async Task<List<string>> LogAsync(TempRepo repo, string folder, string revision) =>
        [.. (await repo.GitAsync("-C", folder, "log", "--format=%s|%an|%ad", "--date=format:%H:%M", revision, "--")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r'))];

    private static async Task<List<string>> RefsAsync(TempRepo repo, string folder) =>
        [.. (await repo.GitAsync("-C", folder, "for-each-ref", "--format=%(refname)")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r'))];

    private static async Task<string> RevParseAsync(TempRepo repo, string folder, string revision) =>
        (await repo.GitAsync("-C", folder, "rev-parse", revision)).StandardOutput.Trim();

    private static async Task<string> ConfigAsync(TempRepo repo, string key) =>
        (await repo.GitAsync("config", "--get", key)).StandardOutput.Trim();
}
