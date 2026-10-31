using System.Net.Http.Headers;
using System.Text.Json;
using VisualCommit.Core.Git;
using VisualCommit.Hosting.GitHub;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Phase 7a's scenario, "hosting" (docs/phase-7a-design.md 2.6, 2.9): what its repositories,
/// authors, upstreams, URLs and <c>refs/pull</c> hold, that a copy costs one git process, that a
/// fake served on another port rewrites the clones' URLs, and what the fake in process lists.
/// </summary>
public class Phase7aScenarioTests
{
    [Fact]
    public async Task The_scenario_holds_the_repositories_branches_authors_and_upstreams_of_its_table()
    {
        using var hello = await Scenarios.HostingAsync();
        var tool = hello.OpenSibling("tool");

        // A copy runs git once: it reads the remotes' URLs, none of which points into the template.
        Assert.Single(hello.Calls.Snapshot());

        Assert.Equal("main", await hello.CurrentBranchAsync());
        Assert.Empty((await hello.GitAsync("status", "--porcelain")).StandardOutput);
        Assert.Equal(
            ["refs/heads/feature/login origin/feature/login", "refs/heads/local-work", "refs/heads/main origin/main"],
            Lines(await Out(hello, "for-each-ref", "--format=%(refname) %(upstream:short)", "refs/heads/")));
        Assert.Equal(
            ["refs/remotes/origin/HEAD", "refs/remotes/origin/docs/readme", $"refs/remotes/origin/{Scenarios.LongBranch}", "refs/remotes/origin/feature/login", "refs/remotes/origin/main", "refs/remotes/origin/topic/no-pr"],
            Lines(await Out(hello, "for-each-ref", "--format=%(refname)", "refs/remotes/")));
        Assert.Equal("refs/remotes/origin/main", await Out(hello, "symbolic-ref", "refs/remotes/origin/HEAD"));
        Assert.Equal(
            [
                "2026-01-01T12:10:00+0000 Test Author <author@example.com> Local change",
                "2026-01-01T12:02:00+0000 Test Author <author@example.com> Add the docs folder",
                "2026-01-01T12:01:00+0000 Ada Lovelace <ada@example.com> Add the greeting",
                "2026-01-01T12:00:00+0000 Octo Cat <octo@example.com> Initial commit",
            ],
            Lines(await Out(hello, "log", "--format=%ad %an <%ae> %s", DateFormat, "local-work")));
        Assert.Equal("Ada Lovelace Add the login form", await Out(hello, "log", "-1", "--format=%an %s", "origin/feature/login"));
        Assert.Equal("Octo Cat Update the readme", await Out(hello, "log", "-1", "--format=%an %s", "origin/docs/readme"));
        Assert.Equal("Octo Cat Shorten long labels", await Out(hello, "log", "-1", "--format=%an %s", $"origin/{Scenarios.LongBranch}"));
        Assert.Equal("Test Author Try an idea", await Out(hello, "log", "-1", "--format=%an %s", "origin/topic/no-pr"));

        Assert.Equal("main", await tool.CurrentBranchAsync());
        Assert.Equal(
            ["refs/heads/fix-typo origin/fix-typo", "refs/heads/main origin/main"],
            Lines(await Out(tool, "for-each-ref", "--format=%(refname) %(upstream:short)", "refs/heads/")));
        Assert.Equal(
            ["refs/remotes/origin/HEAD", "refs/remotes/origin/fix-typo", "refs/remotes/origin/main", "refs/remotes/upstream/HEAD", "refs/remotes/upstream/main"],
            Lines(await Out(tool, "for-each-ref", "--format=%(refname)", "refs/remotes/")));
        Assert.Equal("Octo Cat Fix a typo in the readme", await Out(tool, "log", "-1", "--format=%an %s", "fix-typo"));
        Assert.Equal("Ada Lovelace Initial commit", await Out(tool, "log", "-1", "--format=%an %s", "upstream/main"));

        // The remotes' URLs, on the fake's fixed port (D265).
        Assert.Equal("http://127.0.0.1:47395/octo/hello.git", await Out(hello, "remote", "get-url", "origin"));
        Assert.Equal("http://127.0.0.1:47395/octo/tool.git", await Out(tool, "remote", "get-url", "origin"));
        Assert.Equal("http://127.0.0.1:47395/acme/tool.git", await Out(tool, "remote", "get-url", "upstream"));

        // The fake's bare repositories; gone/tool's folder does not exist.
        var github = hello.SiblingPath(Scenarios.HostingFolder);
        Assert.Equal(["acme/tool.git", "ada/hello.git", "octo/hello.git", "octo/notes.git", "octo/tool.git", "octo/website.git"],
            Directory.EnumerateDirectories(github).SelectMany(owner => Directory.EnumerateDirectories(owner).Select(name => Path.GetFileName(owner) + "/" + Path.GetFileName(name))).Order(StringComparer.Ordinal));
        // Every tip with its author's date, in time order: a minute apart, as 2.6's table has them.
        Assert.Equal(
            [
                "2026-01-01T12:02:00+0000 Test Author Add the docs folder refs/heads/main",
                "2026-01-01T12:03:00+0000 Ada Lovelace Add the login form refs/heads/feature/login refs/pull/1/head",
                "2026-01-01T12:04:00+0000 Octo Cat Update the readme refs/heads/docs/readme refs/pull/2/head",
                "2026-01-01T12:05:00+0000 Octo Cat Shorten long labels refs/heads/" + Scenarios.LongBranch + " refs/pull/3/head",
                "2026-01-01T12:06:00+0000 Test Author Try an idea refs/heads/topic/no-pr refs/pull/4/head",
                "2026-01-01T12:11:00+0000 Ada Lovelace Fix a typo in the greeting refs/pull/5/head",
            ],
            await TipsAsync(hello, Path.Combine(github, "octo", "hello.git")));
        Assert.Equal(
            [
                "2026-01-01T12:02:00+0000 Test Author Add the docs folder refs/heads/main",
                "2026-01-01T12:03:00+0000 Ada Lovelace Add the login form refs/heads/feature/login",
                "2026-01-01T12:04:00+0000 Octo Cat Update the readme refs/heads/docs/readme",
                "2026-01-01T12:05:00+0000 Octo Cat Shorten long labels refs/heads/" + Scenarios.LongBranch,
                "2026-01-01T12:06:00+0000 Test Author Try an idea refs/heads/topic/no-pr",
                "2026-01-01T12:11:00+0000 Ada Lovelace Fix a typo in the greeting refs/heads/fix-typo",
            ],
            await TipsAsync(hello, Path.Combine(github, "ada", "hello.git")));
        Assert.Equal(
            [
                "2026-01-01T12:07:00+0000 Ada Lovelace Initial commit refs/heads/main",
                "2026-01-01T12:08:00+0000 Octo Cat Fix a typo in the readme refs/pull/7/head",
                "2026-01-01T12:09:00+0000 Ada Lovelace An old idea refs/pull/8/head",
            ],
            await TipsAsync(hello, Path.Combine(github, "acme", "tool.git")));
        Assert.Equal(
            [
                "2026-01-01T12:07:00+0000 Ada Lovelace Initial commit refs/heads/main",
                "2026-01-01T12:08:00+0000 Octo Cat Fix a typo in the readme refs/heads/fix-typo",
            ],
            await TipsAsync(hello, Path.Combine(github, "octo", "tool.git")));
        Assert.Empty(await TipsAsync(hello, Path.Combine(github, "octo", "notes.git")));
        Assert.Equal("ref: refs/heads/main", File.ReadAllText(Path.Combine(github, "octo", "website.git", "HEAD")).Trim());
    }

    [Fact]
    public async Task Served_on_another_port_the_clones_get_its_urls()
    {
        using var hello = await Scenarios.HostingAsync();
        await using var served = await Scenarios.ServeHostingAsync(hello, port: 0, ct: TestCancelled);

        Assert.NotEqual(47395, served.GitHub.Port);
        Assert.Equal(Scenarios.HostingUrl("octo/hello", served.GitHub.Port), await Out(hello, "remote", "get-url", "origin"));
        Assert.Equal(Scenarios.HostingUrl("octo/tool", served.GitHub.Port), await Out(served.Tool, "remote", "get-url", "origin"));
        Assert.Equal(Scenarios.HostingUrl("acme/tool", served.GitHub.Port), await Out(served.Tool, "remote", "get-url", "upstream"));
        Assert.Equal(("github_pat_FAKE-octo-1", "ghp_FAKE-octo-2", "github_pat_FAKE-ada-3"), (served.OctoToken, served.OctoClassicToken, served.AdaToken));
        Assert.Equal(3001, served.GitHub.Repository("octo/hello").Id);
        Assert.Empty(served.GitHub.Errors);
    }

    [Fact]
    public async Task In_process_the_fake_lists_the_open_pull_requests_and_octos_repositories()
    {
        using var hello = await Scenarios.HostingAsync();
        await using var served = await Scenarios.ServeHostingAsync(hello, inProcess: true, ct: TestCancelled);
        using var client = new HttpClient(served.GitHub.CreateHandler()) { BaseAddress = served.GitHub.Endpoints.ApiBase };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VisualCommit-tests");
        client.DefaultRequestHeaders.Accept.ParseAdd(GitHubApi.MediaType);
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", GitHubApi.Version);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", served.OctoToken);

        // In process the URLs stay as they are.
        Assert.Equal("http://127.0.0.1:47395/octo/hello.git", await Out(hello, "remote", "get-url", "origin"));

        using var hellos = await JsonAsync(client, "repos/octo/hello/pulls");
        Assert.Equal(
            ["5 Fix a typo in the greeting False ada/hello", "3 Shorten long labels False octo/hello", "2 Update the readme True octo/hello", "1 Add the login form False octo/hello"],
            hellos.RootElement.EnumerateArray().Select(PullOf));
        var five = hellos.RootElement[0];
        Assert.Equal(("ada:fix-typo", "Fix a typo in the greeting"), (five.GetProperty("head").GetProperty("label").GetString(), await Out(hello, "log", "-1", "--format=%s", five.GetProperty("head").GetProperty("sha").GetString()!)));

        using var tools = await JsonAsync(client, "repos/acme/tool/pulls");
        Assert.Equal(["8 An old idea False (deleted)", "7 Fix a typo in the readme False octo/tool"], tools.RootElement.EnumerateArray().Select(PullOf));

        using var repositories = await JsonAsync(client, "user/repos?sort=updated");
        Assert.Equal(["octo/hello", "octo/tool", "octo/notes", "octo/website"], repositories.RootElement.EnumerateArray().Select(repository => repository.GetProperty("full_name").GetString()));

        // octo/tool alone is described (the report's open point 11), in words without "private".
        Assert.Equal(
            [null, "Small command-line tools.", null, null],
            repositories.RootElement.EnumerateArray().Select(repository => repository.GetProperty("description").GetString()));
        Assert.Equal("Small command-line tools.", Scenarios.ToolDescription);
        Assert.DoesNotContain("private", Scenarios.ToolDescription, StringComparison.OrdinalIgnoreCase);
        using var tool = await JsonAsync(client, "repos/octo/tool");
        Assert.Equal("Small command-line tools.", tool.RootElement.GetProperty("description").GetString());
        Assert.Empty(served.GitHub.Errors);
    }

    private static string PullOf(JsonElement pull) =>
        $"{pull.GetProperty("number").GetInt32()} {pull.GetProperty("title").GetString()} {pull.GetProperty("draft").GetBoolean()} " +
        (pull.GetProperty("head").GetProperty("repo").ValueKind == JsonValueKind.Null ? "(deleted)" : pull.GetProperty("head").GetProperty("repo").GetProperty("full_name").GetString());

    private static async Task<JsonDocument> JsonAsync(HttpClient client, string relative)
    {
        using var response = await client.GetAsync(new Uri(relative, UriKind.Relative), TestCancelled);
        Assert.True(response.IsSuccessStatusCode, $"{relative}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(TestCancelled)}");
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestCancelled), cancellationToken: TestCancelled);
    }

    /// <summary>Each commit a bare repository's refs point at, as "author-date author subject ref ref…", sorted (so by time).</summary>
    private static async Task<IReadOnlyList<string>> TipsAsync(TempRepo repo, string bare)
    {
        var command = new GitCommand("for-each-ref", "--format=%(objectname) %(refname)") { WorkingDirectory = bare, Environment = repo.Environment };
        var refs = Lines((await repo.Runner.RunAsync(command, TestCancelled)).EnsureSuccess(command).StandardOutput);
        var tips = new List<string>();
        foreach (var group in refs.Select(line => line.Split(' ', 2)).GroupBy(parts => parts[0]))
        {
            var log = new GitCommand("log", "-1", "--format=%ad %an %s", DateFormat, group.Key) { WorkingDirectory = bare, Environment = repo.Environment };
            var subject = (await repo.Runner.RunAsync(log, TestCancelled)).EnsureSuccess(log).StandardOutput.Trim();
            tips.Add(subject + " " + string.Join(' ', group.Select(parts => parts[1]).Order(StringComparer.Ordinal)));
        }

        return [.. tips.Order(StringComparer.Ordinal)];
    }

    /// <summary>An author date as the commit has it, to the second with its zone.</summary>
    private const string DateFormat = "--date=format:%Y-%m-%dT%H:%M:%S%z";

    private static async Task<string> Out(TempRepo repo, params string[] arguments) => (await repo.GitAsync(arguments)).StandardOutput.Trim();

    private static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
