using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Git.Prompts;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.PromptTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// docs/phase-7b-design.md 8.7's rows and credential-view cases through real git, offline: the
/// three pairs per bound URL that the broker gives a scope (<c>GIT_CONFIG_COUNT</c>), then
/// <c>git credential fill</c> for a URL, and whether git asks the app's helper (the broker's Debug
/// line). Also the two views git takes of a URL (g3 G3.1: which organization prefix key
/// credential.c and http.c apply), and <c>git config --get-urlmatch</c> of the per-URL
/// <c>http.&lt;url&gt;.proactiveAuth</c> pair, and the prompt git shows on dev.azure.com when the
/// app's helper has nothing (8.3). Nothing here depends on a git feature newer than 2.31 (D277);
/// CI's containers run these on 2.43. One theory (D379's redirects) is not offline in that sense:
/// it runs <c>git ls-remote</c> against two local host servers on 127.0.0.1, because
/// <c>git credential fill</c> follows no redirect.
/// </summary>
public sealed class CoverageGitMatchTests
{
    private const string Token = "FAKE-azure-devops-myorg-1";
    private const string Asked = "repo: Fetch: git's credential get for ";

    /// <summary>The organization prefix keys of G3.1, each named by the view it stands for.</summary>
    private static readonly (string Prefix, string Name)[] OrganizationPrefixes =
    [
        ("https://dev.azure.com/myorg", "myorg"),
        ("https://dev.azure.com/otherorg", "otherorg"),
        ("https://dev.azure.com/MyOrg", "MyOrg"),
        ("https://myorg.visualstudio.com", "vs-myorg"),
    ];

    [Theory]
    [MemberData(nameof(AzureDevOpsCoverageVectors.Ids), MemberType = typeof(AzureDevOpsCoverageVectors))]
    public async Task Each_rows_key_reaches_the_apps_helper_as_its_App_column_says(string id)
    {
        var row = AzureDevOpsCoverageVectors.Row(id);
        using var repo = await TempRepo.CreateAsync();

        // A bound row's pairs are what Select gives (its twin included); another row's are its per-URL key's.
        var bindings = row.Bound is null
            ? [new CredentialBinding(row.Key, AzureDevOpsCoverageVectors.Account)]
            : CredentialBinding.Select([new RemoteEffectiveUrls("origin", [row.Url], [row.Url])], AzureDevOpsCoverageVectors.Coverage);
        var fill = await FillAsync(repo, bindings, row.Url);

        Assert.True(row.App == fill.Asked, $"{row.Id}: the app's helper {(fill.Asked ? "was" : "was not")} asked for {row.Url}.");
        if (row.Bound is not null)
        {
            Assert.Equal(Token, CredentialValue(fill.Output, "password"));
        }

        if (row.RawKeyApp is { } raw)
        {
            Assert.Equal(raw, (await FillAsync(repo, [new CredentialBinding(row.Bound!, AzureDevOpsCoverageVectors.Account)], row.Url)).Asked);
        }

        foreach (var (probe, app) in row.Probes)
        {
            var probed = await FillAsync(repo, [new CredentialBinding(row.Key, AzureDevOpsCoverageVectors.Account)], probe);
            Assert.True(app == probed.Asked, $"{row.Id}: the app's helper {(probed.Asked ? "was" : "was not")} asked for {probe}.");
        }
    }

    [Theory]
    [MemberData(nameof(AzureDevOpsCoverageVectors.Ids), MemberType = typeof(AzureDevOpsCoverageVectors))]
    public async Task Each_row_has_gits_two_views_and_urlmatch_of_its_table(string id)
    {
        var row = AzureDevOpsCoverageVectors.Row(id);
        using var repo = await TempRepo.CreateAsync();

        // The three calls are independent, so they run side by side.
        var urlmatch = UrlmatchAsync(repo, $"http.{row.Key}.proactiveAuth", "basic", "http.proactiveAuth", row.Url);
        var credential = CredentialViewAsync(repo, row.Url);
        var http = HttpViewAsync(repo, row.Url);

        Assert.Equal((row.Urlmatch, row.CredentialView, row.HttpView), (await urlmatch, await credential, await http));
    }

    [Theory]
    [MemberData(nameof(CredentialBindingSelectTests.CredentialViewCases), MemberType = typeof(CredentialBindingSelectTests))]
    public async Task The_pairs_ask_the_helper_with_the_other_user_exactly_where_Select_drops(string provider, string url, string other, bool dropped)
    {
        var coverage = CredentialBindingSelectTests.CoverageOf(provider);
        var alone = CredentialBinding.Select([new RemoteEffectiveUrls("origin", [url], [url])], coverage);
        var kept = CredentialBinding.Select([new RemoteEffectiveUrls("origin", [url], [url]), new RemoteEffectiveUrls("other", [other], [other])], coverage);
        var droppedBindings = alone.Except(kept).ToList();
        using var repo = await TempRepo.CreateAsync();

        Assert.Equal(dropped, droppedBindings.Count > 0);
        if (droppedBindings.Count > 0)
        {
            var fill = await FillAsync(repo, droppedBindings, other);
            Assert.True(fill.AskedOtherUser, $"{other} did not reach the app's helper through {string.Join(", ", droppedBindings.Select(binding => binding.Url))}.");
        }

        if (kept.Count > 0)
        {
            var fill = await FillAsync(repo, kept, other);
            Assert.False(fill.Asked, $"{other} reached the app's helper through {string.Join(", ", kept.Select(binding => binding.Url))}.");
        }
    }

    [Theory]
    [InlineData("https://evil@dev.azure.com/myorg/P/_git/%52")]
    [InlineData("https://evil@dev.azure.com/myorg/P/_git/./R")]
    [InlineData("https://evil@dev.azure.com/myorg/P/x/../_git/R")]
    public async Task G0_5s_spellings_reach_the_helper_beside_their_binding_and_Select_binds_nothing_on_the_host(string other)
    {
        const string Url = "https://dev.azure.com/myorg/P/_git/R";
        using var repo = await TempRepo.CreateAsync();

        var fill = await FillAsync(repo, [new CredentialBinding(Url, AzureDevOpsCoverageVectors.Account)], other);

        Assert.True(fill.AskedOtherUser);
        Assert.Empty(CredentialBinding.Select(
            [new RemoteEffectiveUrls("origin", [Url], [Url]), new RemoteEffectiveUrls("evil", [other], [other])],
            AzureDevOpsCoverageVectors.Coverage));
    }

    [Fact]
    public async Task On_GitLab_a_git_less_key_alone_misses_its_git_url_and_with_its_twin_both_reach_the_helper()
    {
        var coverage = CredentialBindingSelectTests.CoverageOf("GitLab");
        var alone = new CredentialBinding("https://gitlab.com/g/p", coverage.Account);
        var withTwin = CredentialBinding.Select([new RemoteEffectiveUrls("origin", ["https://gitlab.com/g/p"], ["https://gitlab.com/g/p"])], coverage);
        using var repo = await TempRepo.CreateAsync();

        Assert.Equal(["https://gitlab.com/g/p", "https://gitlab.com/g/p.git"], withTwin.Select(binding => binding.Url));
        Assert.True((await FillAsync(repo, [alone], "https://gitlab.com/g/p/info/refs")).Asked);
        Assert.False((await FillAsync(repo, [alone], "https://gitlab.com/g/p.git/info/refs")).Asked);
        Assert.True((await FillAsync(repo, withTwin, "https://gitlab.com/g/p/info/refs")).Asked);
        var twinned = await FillAsync(repo, withTwin, "https://gitlab.com/g/p.git/info/refs");
        Assert.True(twinned.Asked);
        Assert.Equal(Token, CredentialValue(twinned.Output, "password"));
    }

    [Theory]
    [MemberData(nameof(CredentialBindingSelectTests.OutOfFormUnderTheBinding), MemberType = typeof(CredentialBindingSelectTests))]
    public async Task A_user_less_url_out_of_the_form_under_V1_gets_the_token_through_V1s_key_so_Select_binds_nothing_beside_it(string url)
    {
        // git's credential view keeps such a path under V1's (it re-encodes \ and resolves no dot
        // segment), while the server decides what it reads in it (g3 G3.2's sent column).
        var v1 = AzureDevOpsCoverageVectors.Row("V1").Bound!;
        using var repo = await TempRepo.CreateAsync();

        var fill = await FillAsync(repo, [new CredentialBinding(v1, AzureDevOpsCoverageVectors.Account) { SharedOriginOwner = "myorg" }], url);

        Assert.True(fill.Asked, $"{url} did not reach the app's helper through {v1}.");
        Assert.False(fill.AskedOtherUser);
        Assert.Equal(Token, CredentialValue(fill.Output, "password"));
        Assert.Empty(CredentialBinding.Select(
            [new RemoteEffectiveUrls("origin", [v1], [v1]), new RemoteEffectiveUrls("other", [url], [url])],
            AzureDevOpsCoverageVectors.Coverage));
    }

    [Theory]
    [InlineData("AzureDevOps", "https://evil@dev.azure.com./myorg/p/_git/r", "https://dev.azure.com/myorg/p/_git/r", "dev.azure.com.")]
    [InlineData("AzureDevOps", "https://dev.azure.com./myorg/p/_git/r", "https://dev.azure.com/myorg/p/_git/r", "dev.azure.com.")]
    [InlineData("AzureDevOps", "https://myorg@dev.azure.com./myorg/p/_git/r", "https://dev.azure.com/myorg/p/_git/r", "dev.azure.com.")]
    [InlineData("AzureDevOps", "https://myorg.visualstudio.com./p/_git/r", "https://myorg.visualstudio.com/p/_git/r", "myorg.visualstudio.com.")]
    [InlineData("GitLab", "https://gitlab.com./g/p", "https://gitlab.com/g/p", "gitlab.com.")]
    public async Task A_url_on_a_covered_host_with_a_trailing_dot_reaches_the_apps_helper_through_the_hosts_key_in_place_of_its_own_whatever_its_user(
        string provider, string dotted, string key, string host)
    {
        // git's match_host reads dev.azure.com. as dev.azure.com, whatever the URL's user: the
        // key's empty helper resets the user's own, so only the app's is asked (for host
        // dev.azure.com., which the broker does not bind), and D82's order is lost. Select
        // therefore drops the key beside it.
        var coverage = CredentialBindingSelectTests.CoverageOf(provider);
        using var repo = await TempRepo.CreateAsync();
        var own = await OwnHelperAsync(repo);

        var bound = await FillAsync(repo, [new CredentialBinding(key, coverage.Account)], dotted);

        Assert.Equal([$"{Asked}{host} is not bound."], bound.Lines);
        Assert.False(File.Exists(own), "The user's own helper was asked through the host's key.");

        var unbound = await FillAsync(repo, [new CredentialBinding(key.Replace("/p", "/q", StringComparison.Ordinal), coverage.Account)], dotted);

        Assert.False(unbound.Asked);
        Assert.True(File.Exists(own), "The user's own helper was not asked without the host's key.");
        Assert.DoesNotContain(
            CredentialBinding.Select([new RemoteEffectiveUrls("origin", [key], [key]), new RemoteEffectiveUrls("dot", [dotted], [dotted])], coverage),
            binding => binding.Url == key);
    }

    [Theory]
    [InlineData("AzureDevOps", "https://%64ev.azure.com/myorg/p/_git/r/..%5C..%5C..%5C..%5Cotherorg/p/_git/r", "https://dev.azure.com/myorg/p/_git/r", false)]
    [InlineData("AzureDevOps", "https://%64%65%76.azure.com/myorg/p/_git/r/%C0%AE%C0%AE/%C0%AE%C0%AE/%C0%AE%C0%AE/%C0%AE%C0%AE/otherorg/p/_git/r", "https://dev.azure.com/myorg/p/_git/r", false)]
    [InlineData("AzureDevOps", "https://dev.azure%2Ecom/myorg/p/_git/r/..%5C..%5C..%5C..%5Cotherorg/p/_git/r", "https://dev.azure.com/myorg/p/_git/r", false)]
    [InlineData("AzureDevOps", "https://myorg%2Evisualstudio.com/p/_git/r/..%5Cq", "https://myorg.visualstudio.com/p/_git/r", false)]
    [InlineData("AzureDevOps", "https://evil@%64ev.azure.com/myorg/p/_git/r", "https://dev.azure.com/myorg/p/_git/r", true)]
    [InlineData("AzureDevOps", "https://evil@dev%2Eazure.com/myorg/p/_git/r", "https://dev.azure.com/myorg/p/_git/r", true)]
    [InlineData("AzureDevOps", "https://evil@myorg.visualstudio%2Ecom/p/_git/r", "https://myorg.visualstudio.com/p/_git/r", true)]
    [InlineData("GitLab", "https://evil@%67itlab.com/g/p.git", "https://gitlab.com/g/p.git", true)]
    public async Task A_percent_encoded_covered_host_reaches_the_apps_helper_through_the_hosts_key_so_Select_binds_nothing_beside_it(
        string provider, string url, string key, bool otherUser)
    {
        // D379 (R1): .NET cannot read the URL, git's credential side decodes its host (as curl
        // does, wave 1's finding): through the key the token answers a user-less one on a path the
        // server may read as another organization's, and another user's loses D82's order.
        var coverage = CredentialBindingSelectTests.CoverageOf(provider);
        using var repo = await TempRepo.CreateAsync();
        var own = await OwnHelperAsync(repo);

        var bound = await FillAsync(repo, [new CredentialBinding(key, coverage.Account)], url);

        Assert.True(bound.Asked, $"{url} did not reach the app's helper through {key}.");
        Assert.Equal(otherUser, bound.AskedOtherUser);
        Assert.Equal(otherUser ? null : Token, CredentialValue(bound.Output, "password"));
        Assert.False(File.Exists(own), "The user's own helper was asked through the host's key.");

        var bindings = CredentialBinding.Select([new RemoteEffectiveUrls("origin", [key], [key]), new RemoteEffectiveUrls("other", [url], [url])], coverage);
        Assert.Empty(bindings);
        var unbound = await FillAsync(repo, bindings, url);
        Assert.False(unbound.Asked);
        Assert.True(File.Exists(own), "The user's own helper was not asked without the host's key.");
    }

    /// <summary>
    /// D379 (R2, R3) over a transport, as wave 1's finding showed it: a local host serves
    /// <c>g/p.git</c> and answers <c>/g/p/info/refs</c> with gitlab.com's 301 into
    /// <c>/g/p.git/info/refs</c>, and a second port 308s every request into the first, in the role
    /// of <c>http://</c> and <c>www.</c> (r6 6.1). git follows them and builds the credential again
    /// from the target, without bob's name, so the binding Select gave before D379 answers bob's
    /// fetch with the account's token; Select now drops it, and the user's own helper is asked
    /// (D82). <c>git credential fill</c> cannot show this: it follows no redirect.
    /// </summary>
    [Theory]
    [InlineData(false, "/g/p")]
    [InlineData(false, "/g/p/")]
    [InlineData(true, "/g/p.git")]
    [InlineData(true, "/g/p")]
    public async Task Another_users_url_that_a_redirect_leads_into_a_binding_takes_the_token_through_its_key_so_Select_drops_it(bool otherPort, string path)
    {
        using var repo = await TempRepo.CreateAsync("redirect");
        await repo.CommitFileAsync("README.md", "first\n", "First");
        var folder = repo.SiblingPath("gitlab");
        await repo.GitAsync("clone", "--quiet", "--bare", "--no-hardlinks", repo.Path, Path.Combine(folder, "g", "p.git"));
        var empty = Directory.CreateDirectory(repo.SiblingPath("www")).FullName;
        await using var host = await AuthRemoteServer.StartForHostAsync(
            folder,
            new AuthRemoteHostOptions(
                new RedirectRoutes(path => path == "/g/p/info/refs", 301, request => "/g/p.git/info/refs?" + request.Query),
                token => token == Token ? "ada" : null,
                _ => false,
                (_, _) => false,
                Realm: "GitLab"));
        host.Serve("g/p");
        await using var www = await AuthRemoteServer.StartForHostAsync(
            empty,
            new AuthRemoteHostOptions(
                new RedirectRoutes(_ => true, 308, request => $"http://127.0.0.1:{host.Port}{request.Path}?{request.Query}"),
                _ => null,
                _ => false,
                (_, _) => false));
        var coverage = new GitCoverage(new AccountKey(HostingProvider.GitLab, $"127.0.0.1:{host.Port}", "ada"), new Uri($"http://127.0.0.1:{host.Port}/"))
        {
            GitTwin = true,
            HelperUserIsAccounts = true,
        };
        var origin = $"http://127.0.0.1:{host.Port}/g/p.git";
        var bob = $"http://bob@127.0.0.1:{(otherPort ? www.Port : host.Port)}{path}";
        var own = await OwnHelperAsync(repo);

        // What Select gave beside bob's URL before D379: the origin's binding.
        var before = new CredentialBinding(origin, coverage.Account);
        Assert.Equal([before], CredentialBinding.Select([new RemoteEffectiveUrls("origin", [origin], [origin])], coverage));
        var taken = await LsRemoteAsync(repo, [before], bob);

        Assert.True(taken.Asked, $"{bob} did not reach the app's helper through {origin}: {taken.Error}");
        Assert.False(taken.AskedOtherUser);
        Assert.Contains(host.Requests, request => request.Path == "/g/p.git/info/refs" && request.UserName == "ada" && request.GivenUserName == GitCredential.UserName);
        Assert.False(File.Exists(own), "The user's own helper was asked through the origin's key.");

        var bindings = CredentialBinding.Select([new RemoteEffectiveUrls("origin", [origin], [origin]), new RemoteEffectiveUrls("bob", [bob], [bob])], coverage);
        Assert.Empty(bindings);
        Assert.Empty(CredentialBinding.Select([new RemoteEffectiveUrls("bob", [bob], [bob]), new RemoteEffectiveUrls("origin", [origin], [origin])], coverage));
        var signedIn = host.Requests.Count(request => request.UserName == "ada");
        var kept = await LsRemoteAsync(repo, bindings, bob);

        Assert.False(kept.Asked);
        Assert.Equal(signedIn, host.Requests.Count(request => request.UserName == "ada"));
        Assert.True(File.Exists(own), "The user's own helper was not asked without the origin's key.");
        Assert.Empty(host.Errors);
        Assert.Empty(www.Errors);
    }

    [Theory]
    [InlineData("https://dev.azure.com/otherorg/p/_git/r", false, "Username for 'https://dev.azure.com': ", false)]
    [InlineData("https://dev.azure.com/otherorg/p/_git/r", true, "Username for 'https://dev.azure.com/otherorg/p/_git/r': ", false)]
    [InlineData("https://dev.azure.com/myorg/p/_git/r", false, "Username for 'https://dev.azure.com': ", false)]
    [InlineData("https://dev.azure.com/myorg/p/_git/r", true, "Username for 'https://dev.azure.com/myorg/p/_git/r': ", true)]
    public async Task Gits_prompt_on_dev_azure_com_carries_the_account_only_when_its_path_names_the_organization(string url, bool useHttpPath, string prompt, bool carries)
    {
        // Without a token the app's helper answers nothing, so git asks; useHttpPath off is
        // macOS', Linux's and every test run's, on is Git for Windows' system file's (8.3).
        var handler = new RecordingPromptHandler(PromptAnswer.Refused);
        using var broker = new AskPassBroker(handler, new ListLog(), null, new RecordingCredentialSource(), null, null, _ => null);
        var binding = CredentialBinding.Select([new RemoteEffectiveUrls("origin", ["https://dev.azure.com/myorg/p/_git/r"], ["https://dev.azure.com/myorg/p/_git/r"])], AzureDevOpsCoverageVectors.Coverage);
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask, "op-1", bindings: binding);
        using var repo = await TempRepo.CreateAsync();
        string[] fill = useHttpPath ? ["-c", "credential.https://dev.azure.com.useHttpPath=true", "credential", "fill"] : ["credential", "fill"];

        var result = await GitAsync(repo, scope.Environment, fill, $"url={url}\n\n");

        Assert.False(result.Succeeded);
        var asked = Assert.Single(handler.Prompts);
        Assert.Equal((prompt, carries ? AzureDevOpsCoverageVectors.Account : null), (asked.Text, asked.Account));
    }

    /// <summary>What <c>git credential fill</c> for <paramref name="url"/> did in a deny scope bound to <paramref name="bindings"/>.</summary>
    private static async Task<(bool Asked, bool AskedOtherUser, string Output, IReadOnlyList<string> Lines)> FillAsync(TempRepo repo, IReadOnlyList<CredentialBinding> bindings, string url)
    {
        var log = new ListLog();
        using var broker = new AskPassBroker(new RecordingPromptHandler(), log, null, new RecordingCredentialSource(Token), null, null, _ => null);
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Deny, "op-" + Guid.NewGuid().ToString("N"), bindings: bindings);

        var result = await GitAsync(repo, scope.Environment, ["credential", "fill"], $"url={url}\n\n");

        var asked = log.Entries.Where(entry => entry.Level == LogLevel.Debug && entry.Message.StartsWith(Asked, StringComparison.Ordinal)).Select(entry => entry.Message).ToList();
        return (asked.Count > 0, asked.Any(line => line.EndsWith(" names another user.", StringComparison.Ordinal)), result.StandardOutput, asked);
    }

    /// <summary>What <c>git ls-remote <paramref name="url"/></c> did in a deny scope bound to <paramref name="bindings"/>: as <see cref="FillAsync"/>, with git's error output.</summary>
    private static async Task<(bool Asked, bool AskedOtherUser, string Error)> LsRemoteAsync(TempRepo repo, IReadOnlyList<CredentialBinding> bindings, string url)
    {
        var log = new ListLog();
        using var broker = new AskPassBroker(new RecordingPromptHandler(), log, null, new RecordingCredentialSource(Token), null, null, _ => null);
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Deny, "op-" + Guid.NewGuid().ToString("N"), bindings: bindings);

        var result = await GitAsync(repo, scope.Environment, ["ls-remote", url]);

        var asked = log.Entries.Where(entry => entry.Level == LogLevel.Debug && entry.Message.StartsWith(Asked, StringComparison.Ordinal)).Select(entry => entry.Message).ToList();
        return (asked.Count > 0, asked.Any(line => line.EndsWith(" names another user.", StringComparison.Ordinal)), result.StandardError);
    }

    /// <summary>A host's routes that answer the paths <paramref name="claims"/> picks with <paramref name="status"/> to <paramref name="location"/>.</summary>
    private sealed class RedirectRoutes(Func<string, bool> claims, int status, Func<AuthRemoteRouteRequest, string> location) : IAuthRemoteRoutes
    {
        public bool Claims(string method, string path) => claims(path);

        public Task<AuthRemoteResponse> ServeAsync(AuthRemoteRouteRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AuthRemoteResponse(status, status == 301 ? "Moved Permanently" : "Permanent Redirect", [new("Location", location(request))], []));
    }

    /// <summary>Configures the repository's own <c>credential.helper</c> as a recorder that answers nothing; returns the file it writes when git asks it.</summary>
    private static async Task<string> OwnHelperAsync(TempRepo repo)
    {
        var record = repo.SiblingPath("own-helper-" + Guid.NewGuid().ToString("N") + ".txt");
        var quoted = "'" + record.Replace('\\', '/').Replace("'", "'\\''", StringComparison.Ordinal) + "'";
        await repo.GitAsync("config", "credential.helper", $"!f() {{ cat >/dev/null; echo own >> {quoted}; }}; f");
        return record;
    }

    /// <summary><c>git config --get-urlmatch <paramref name="name"/> <paramref name="url"/></c> with one pair: its value, <c>none</c> or <c>fatal</c>.</summary>
    private static async Task<string> UrlmatchAsync(TempRepo repo, string key, string value, string name, string url) =>
        await GetUrlmatchAsync(repo, Pairs([(key, value)]), name, url);

    /// <summary>The organization prefix keys whose helpers git's credential side asks for <paramref name="url"/> (G3.1's <c>cred</c>): their names, or <c>none</c>.</summary>
    private static async Task<string> CredentialViewAsync(TempRepo repo, string url)
    {
        var record = repo.SiblingPath("credential-view-" + Guid.NewGuid().ToString("N") + ".txt");
        var quoted = "'" + record.Replace('\\', '/').Replace("'", "'\\''", StringComparison.Ordinal) + "'";
        var pairs = Pairs([.. OrganizationPrefixes.Select(prefix => ($"credential.{prefix.Prefix}.helper", $"!f() {{ cat >/dev/null; echo {prefix.Name} >> {quoted}; }}; f"))]);
        pairs["GIT_TERMINAL_PROMPT"] = "0";

        await GitAsync(repo, pairs, ["credential", "fill"], $"url={url}\n\n");

        var names = File.Exists(record) ? File.ReadAllLines(record).Select(line => line.Trim()).Where(line => line.Length > 0).ToList() : [];
        return names.Count == 0 ? "none" : string.Join(",", names);
    }

    /// <summary>The organization prefix key git's http side applies to <paramref name="url"/> (G3.1's <c>http</c>): its name, <c>none</c> or <c>fatal</c>.</summary>
    private static Task<string> HttpViewAsync(TempRepo repo, string url) =>
        GetUrlmatchAsync(repo, Pairs([.. OrganizationPrefixes.Select(prefix => ($"http.{prefix.Prefix}.vcorg", prefix.Name))]), "http.vcorg", url);

    private static async Task<string> GetUrlmatchAsync(TempRepo repo, Dictionary<string, string?> pairs, string name, string url)
    {
        var result = await GitAsync(repo, pairs, ["config", "--get-urlmatch", name, url]);
        var value = result.StandardOutput.Trim();
        return value.Length > 0 ? value
            : result.StandardError.Contains("fatal:", StringComparison.Ordinal) ? "fatal"
            : "none";
    }

    /// <summary>The variables that give git <paramref name="pairs"/> as configuration, as the broker gives its own.</summary>
    private static Dictionary<string, string?> Pairs(IReadOnlyList<(string Key, string Value)> pairs)
    {
        var environment = new Dictionary<string, string?> { ["GIT_CONFIG_COUNT"] = pairs.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        for (var i = 0; i < pairs.Count; i++)
        {
            environment[$"GIT_CONFIG_KEY_{i}"] = pairs[i].Key;
            environment[$"GIT_CONFIG_VALUE_{i}"] = pairs[i].Value;
        }

        return environment;
    }
}
