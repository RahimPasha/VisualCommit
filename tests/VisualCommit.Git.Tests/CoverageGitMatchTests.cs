using VisualCommit.Core.Git;
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
/// CI's containers run these on 2.43.
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
    private static async Task<(bool Asked, bool AskedOtherUser, string Output)> FillAsync(TempRepo repo, IReadOnlyList<CredentialBinding> bindings, string url)
    {
        var log = new ListLog();
        using var broker = new AskPassBroker(new RecordingPromptHandler(), log, null, new RecordingCredentialSource(Token), null, null, _ => null);
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Deny, "op-" + Guid.NewGuid().ToString("N"), bindings: bindings);

        var result = await GitAsync(repo, scope.Environment, ["credential", "fill"], $"url={url}\n\n");

        var asked = log.Entries.Where(entry => entry.Level == LogLevel.Debug && entry.Message.StartsWith(Asked, StringComparison.Ordinal)).ToList();
        return (asked.Count > 0, asked.Any(entry => entry.Message.EndsWith(" names another user.", StringComparison.Ordinal)), result.StandardOutput);
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
