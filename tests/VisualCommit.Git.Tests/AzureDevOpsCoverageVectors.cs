using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// One row of docs/phase-7b-design.md 8.7's table: a remote URL for the account
/// <see cref="AzureDevOpsCoverageVectors.Account"/> under <see cref="AzureDevOpsCoverageVectors.Coverage"/>,
/// with g3 G3.4's answer as 8.2 words it, and what git does with the URL.
/// </summary>
/// <param name="Id">G3.2's name (V1 to V26, X1 to X12) or the design review's (P1 to P5, Y1, Y2, U1 to U3).</param>
/// <param name="Url">The remote's URL.</param>
/// <param name="InForm"><see cref="CredentialBinding.InPlainForm"/>'s answer (8.2 step 2's rule alone).</param>
/// <param name="Bound">The binding's <see cref="CredentialBinding.Url"/> when <c>Select</c> binds the URL, else null.</param>
/// <param name="Twin">The binding's credential twin (8.2 step 4b), or null.</param>
/// <param name="Owner">The binding's <see cref="CredentialBinding.SharedOriginOwner"/>.</param>
/// <param name="Key">The per-URL key whose three pairs the git check gives: the bound text, else the URL's text without <c>user@</c> (G3.1).</param>
/// <param name="App">Whether those pairs (with the twin's, when there is one) make git ask the app's helper for <see cref="Url"/>.</param>
/// <param name="Urlmatch"><c>git config --get-urlmatch http.proactiveAuth</c> with the key's pair: <c>basic</c>, or <c>fatal</c> when git cannot normalise the URL.</param>
/// <param name="CredentialView">The organization prefix key git's credential side applies (<c>myorg</c>, <c>MyOrg</c>, <c>otherorg</c>, <c>vs-myorg</c> or <c>none</c>).</param>
/// <param name="HttpView">The one its http side applies (the same names, or <c>fatal</c>).</param>
/// <param name="Mark">Where the git columns come from: G3.2's or the design review's measurements, or the U rows' "MEASURED by CoverageGitMatchTests".</param>
public sealed record CoverageVector(
    string Id,
    string Url,
    bool InForm,
    string? Bound,
    string? Twin,
    string? Owner,
    string Key,
    bool App,
    string Urlmatch,
    string CredentialView,
    string HttpView,
    string Mark)
{
    /// <summary>The Y rows: whether the raw key alone, without its twin, makes git ask the app's helper (false: D82's helpers answer).</summary>
    public bool? RawKeyApp { get; init; }

    /// <summary>The P rows: other URLs and whether the row's key, were it bound, makes git ask the app's helper for each (why <c>Select</c> refuses P1 to P4).</summary>
    public IReadOnlyList<(string Url, bool App)> Probes { get; init; } = [];

    public override string ToString() => Id;
}

/// <summary>
/// The 48 rows of docs/phase-7b-design.md 8.7: g3 G3.2's V1 to V26 and X1 to X12 (MEASURED on
/// 2.31.1, 2.43.0 and 2.56), then the design review's P1 to P5 and Y1, Y2 (MEASURED by it) and U1
/// to U3 (their git columns ASSUMED as X6's until <see cref="CoverageGitMatchTests"/> first ran;
/// MEASURED by it since, on 2.56). 13 rows bound, 35 not.
/// </summary>
public static class AzureDevOpsCoverageVectors
{
    private const string D = "https://dev.azure.com";
    private const string G32 = "MEASURED, g3 G3.2";
    private const string Review = "MEASURED, the design review";
    private const string Ours = "MEASURED, CoverageGitMatchTests (ASSUMED as X6's before)";

    /// <summary>
    /// What P1 to P4's key would answer, were it bound (design 8.7): another organization's URL
    /// without a user, and its <c>otherorg@</c> form, whose <c>get</c> names <c>username=otherorg</c>.
    /// Declared before <see cref="Rows"/>: static initializers run in the order they are written.
    /// </summary>
    private static readonly (string Url, bool App)[] OtherOrganizationProbes =
        [($"{D}/otherorg/p/_git/r", true), ("https://otherorg@dev.azure.com/otherorg/p/_git/r", true)];

    /// <summary>The account of every row (design 8.7).</summary>
    public static AccountKey Account { get; } = new(HostingProvider.AzureDevOps, "dev.azure.com", "myorg");

    /// <summary>Azure DevOps' coverage of <see cref="Account"/> on the service (design 8.7; D359).</summary>
    public static GitCoverage Coverage { get; } = new(Account, new Uri("https://dev.azure.com/"))
    {
        Organization = "myorg",
        OrganizationHost = true,
        HelperUserIsAccounts = true,
    };

    /// <summary>The rows in the design's order.</summary>
    public static IReadOnlyList<CoverageVector> Rows { get; } =
    [
        Bound("V1", $"{D}/myorg/p/_git/r", "myorg", "myorg", "myorg"),
        Bound("V2", $"{D}/MyOrg/p/_git/r", "myorg", "MyOrg", "MyOrg"),
        Unbound("V3", $"{D}/myorg/../otherorg/p/_git/r", false, true, "otherorg", "otherorg"),
        Unbound("V4", $"{D}/otherorg/../myorg/p/_git/r", false, true, "myorg", "myorg"),
        Unbound("V5", $"{D}/./myorg/p/_git/r", false, true, "myorg", "myorg"),
        Unbound("V6", $"{D}/%6Dyorg/p/_git/r", true, true, "myorg", "myorg"),
        Unbound("V7", $"{D}/myorg%2F..%2Fotherorg/p/_git/r", false, false, "otherorg", "none"),
        Unbound("V8", $"{D}//myorg/p/_git/r", false, false, "myorg", "none"),
        Unbound("V9", $"{D}/myorg/./p/_git/r", false, true, "myorg", "myorg"),
        Bound("V10", "https://myorg@dev.azure.com/myorg/p/_git/r", "myorg", "myorg", "myorg", bound: $"{D}/myorg/p/_git/r"),
        Unbound("V11", "https://otherorg@dev.azure.com/myorg/p/_git/r", true, true, "myorg", "myorg"),
        Bound("V12", "https://DEV.AZURE.COM/myorg/p/_git/r", "myorg", "myorg", "myorg"),
        Bound("V13", "https://dev.azure.com:443/myorg/p/_git/r", "myorg", "myorg", "myorg"),
        Bound("V14", $"{D}/myorg/p/_git/r/", "myorg", "myorg", "myorg"),
        Bound("V15", "https://myorg.visualstudio.com/DefaultCollection/p/_git/r", null, "vs-myorg", "vs-myorg"),
        Bound("V16", "https://MYORG.visualstudio.com/p/_git/r", null, "vs-myorg", "vs-myorg"),
        Unbound("V17", "https://x.myorg.visualstudio.com/p/_git/r", true, true, "none", "none"),
        Unbound("V18", $"{D}/myorg/%2E%2E/otherorg/p/_git/r", false, true, "otherorg", "otherorg"),
        Unbound("V19", $"{D}/myorg/..%2Fotherorg/p/_git/r", false, false, "otherorg", "myorg"),
        Unbound("V20", $"{D}/../myorg/p/_git/r", false, true, "none", "fatal", urlmatch: "fatal"),
        Unbound("V21", $"{D}/myorg\\..\\otherorg/p/_git/r", false, true, "none", "none"),
        Unbound("V22", $"{D}/otherorg\\..\\myorg/p/_git/r", false, true, "none", "none"),
        Unbound("V23", "https://dev.azure.com./myorg/p/_git/r", true, true, "myorg", "myorg"),
        Unbound("V24", $"{D}/myorg/p/_git/r/../../../../otherorg/p/_git/r", false, true, "otherorg", "otherorg"),
        Unbound("V25", "https://dev.azure.com\\@evil.example/myorg/p/_git/r", false, true, "none", "none", key: "https://evil.example/myorg/p/_git/r"),
        Unbound("V26", "https://evil.example\\@dev.azure.com/myorg/p/_git/r", false, true, "myorg", "myorg", key: $"{D}/myorg/p/_git/r"),
        Bound("X1", $"{D}/myorg/My%20Project/_git/My%20Repo", "myorg", "myorg", "myorg"),
        Unbound("X2", $"{D}/myorg/%2e%2e/otherorg/p/_git/r", false, true, "otherorg", "otherorg"),
        Unbound("X3", $"{D}/myorg/p/%2E%2E/%2E%2E/%2E%2E/otherorg/p/_git/r", false, true, "none", "fatal", urlmatch: "fatal"),
        Unbound("X4", $"{D}/myorg/%5C..%5Cotherorg/p/_git/r", false, true, "myorg", "myorg"),
        Unbound("X5", $"{D}/myorg/%252E%252E/otherorg/p/_git/r", false, true, "myorg", "myorg"),
        Unbound("X6", $"{D}/myorg/%C0%AE%C0%AE/otherorg/p/_git/r", false, true, "myorg", "myorg"),
        Unbound("X7", $"{D}/myorg/p/_git/r?x=1", false, false, "myorg", "myorg"),
        Unbound("X8", $"{D}/myorg/p/_git/r#frag", false, false, "myorg", "myorg"),
        Unbound("X9", $"{D}/my%6Frg/p/_git/r", true, true, "myorg", "myorg"),
        Unbound("X10", $"{D}/myorg%20/p/_git/r", true, true, "none", "none"),
        Unbound("X11", $"{D}/myorg/p/_git/r%2F..%2F..%2F..%2F..%2Fotherorg", false, false, "otherorg", "myorg"),
        Unbound("X12", $"{D}/myorg/.%2E/otherorg/p/_git/r", false, true, "otherorg", "otherorg"),
        Unbound("P1", D, true, true, "none", "none", mark: Review) with { Probes = OtherOrganizationProbes },
        Unbound("P2", $"{D}/", true, true, "none", "none", mark: Review) with { Probes = OtherOrganizationProbes },
        Unbound("P3", "https://dev.azure.com:443", true, true, "none", "none", mark: Review) with { Probes = OtherOrganizationProbes },
        Unbound("P4", "https://myorg@dev.azure.com/", true, true, "none", "none", mark: Review) with { Probes = OtherOrganizationProbes },
        Bound("P5", "https://myorg.visualstudio.com", null, "vs-myorg", "vs-myorg", mark: Review) with { Probes = [("https://otherorg.visualstudio.com/p/_git/r", false)] },
        Bound("Y1", $"{D}/myorg/p/_git/r(x)", "myorg", "myorg", "myorg", twin: $"{D}/myorg/p/_git/r%28x%29", mark: Review) with { RawKeyApp = false },
        Bound("Y2", $"{D}/myorg/My%20P/_git/a&b", "myorg", "myorg", "myorg", twin: $"{D}/myorg/My%20P/_git/a%26b", mark: Review) with { RawKeyApp = false },
        Unbound("U1", $"{D}/myorg/%E0%80%AE%E0%80%AE/otherorg/p/_git/r", false, true, "myorg", "myorg", mark: Ours),
        Unbound("U2", $"{D}/myorg/p/_git/r%ED%A0%80", false, true, "myorg", "myorg", mark: Ours),
        Bound("U3", $"{D}/myorg/p/_git/r%C3%A9", "myorg", "myorg", "myorg", mark: Ours),
    ];

    /// <summary>The rows' ids, for a theory.</summary>
    public static TheoryData<string> Ids => [.. Rows.Select(row => row.Id)];

    /// <summary>The row named <paramref name="id"/>.</summary>
    public static CoverageVector Row(string id) => Rows.Single(row => row.Id == id);

    private static CoverageVector Bound(string id, string url, string? owner, string credential, string http, string? bound = null, string? twin = null, string mark = G32) =>
        new(id, url, true, bound ?? url, twin, owner, bound ?? url, true, "basic", credential, http, mark);

    private static CoverageVector Unbound(string id, string url, bool inForm, bool app, string credential, string http, string? key = null, string urlmatch = "basic", string mark = G32) =>
        new(id, url, inForm, null, null, null, key ?? WithoutUser(url), app, urlmatch, credential, http, mark);

    /// <summary>G3.1's per-URL key: the text with <c>user@</c> removed (<c>c3.sh</c>'s <c>nouser</c>: up to the first <c>@</c> before any <c>/</c>).</summary>
    private static string WithoutUser(string url)
    {
        var start = url.IndexOf("://", StringComparison.Ordinal) + 3;
        var at = url.IndexOf('@', start);
        var slash = url.IndexOf('/', start);
        return at >= 0 && (slash < 0 || at < slash) ? url[..start] + url[(at + 1)..] : url;
    }
}
