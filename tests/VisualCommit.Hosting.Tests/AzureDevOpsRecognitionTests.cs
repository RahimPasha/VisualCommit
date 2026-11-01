using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting.AzureDevOps;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// Which remote URLs are Azure DevOps' (docs/phase-7b-design.md 6.2): the forms under the service
/// and under the fake (<see cref="AzureDevOpsEndpoints.Local"/>), the SSH forms' path rules, g3
/// G3.2's 38 rows (V1 to V26, X1 to X12) with 6.2's answers, other ports, and the organization a
/// typed text names (<see cref="AzureDevOpsUrls.OrganizationOf"/>, WP-A's one rule). Made-up names only.
/// </summary>
public sealed class AzureDevOpsRecognitionTests
{
    private const string D = "https://dev.azure.com";

    private static readonly AzureDevOpsEndpoints Service = AzureDevOpsEndpoints.Service;

    private static readonly AzureDevOpsEndpoints Local = AzureDevOpsEndpoints.Local(47397);

    /// <summary>
    /// G3.2's 38 rows (docs/phase-7b-design.md 8.7; the same URLs as Git.Tests'
    /// <c>AzureDevOpsCoverageVectors</c>) with 6.2's answer: the owner and name recognised, or null.
    /// Recognised exactly where G3.4 binds (V1, V2, V10, V12 to V16, X1) and V11, another user's,
    /// which is never bound; V6, X9 and X10, refused by G3.4 only for the organization's grammar, are not.
    /// </summary>
    public static TheoryData<string, string, string?, string?> G32Rows => new()
    {
        { "V1", $"{D}/myorg/p/_git/r", "myorg/p", "r" },
        { "V2", $"{D}/MyOrg/p/_git/r", "MyOrg/p", "r" },
        { "V3", $"{D}/myorg/../otherorg/p/_git/r", null, null },
        { "V4", $"{D}/otherorg/../myorg/p/_git/r", null, null },
        { "V5", $"{D}/./myorg/p/_git/r", null, null },
        { "V6", $"{D}/%6Dyorg/p/_git/r", null, null },
        { "V7", $"{D}/myorg%2F..%2Fotherorg/p/_git/r", null, null },
        { "V8", $"{D}//myorg/p/_git/r", null, null },
        { "V9", $"{D}/myorg/./p/_git/r", null, null },
        { "V10", "https://myorg@dev.azure.com/myorg/p/_git/r", "myorg/p", "r" },
        { "V11", "https://otherorg@dev.azure.com/myorg/p/_git/r", "myorg/p", "r" },
        { "V12", "https://DEV.AZURE.COM/myorg/p/_git/r", "myorg/p", "r" },
        { "V13", "https://dev.azure.com:443/myorg/p/_git/r", "myorg/p", "r" },
        { "V14", $"{D}/myorg/p/_git/r/", "myorg/p", "r" },
        { "V15", "https://myorg.visualstudio.com/DefaultCollection/p/_git/r", "myorg/p", "r" },
        { "V16", "https://MYORG.visualstudio.com/p/_git/r", "MYORG/p", "r" },
        { "V17", "https://x.myorg.visualstudio.com/p/_git/r", null, null },
        { "V18", $"{D}/myorg/%2E%2E/otherorg/p/_git/r", null, null },
        { "V19", $"{D}/myorg/..%2Fotherorg/p/_git/r", null, null },
        { "V20", $"{D}/../myorg/p/_git/r", null, null },
        { "V21", $"{D}/myorg\\..\\otherorg/p/_git/r", null, null },
        { "V22", $"{D}/otherorg\\..\\myorg/p/_git/r", null, null },
        { "V23", "https://dev.azure.com./myorg/p/_git/r", null, null },
        { "V24", $"{D}/myorg/p/_git/r/../../../../otherorg/p/_git/r", null, null },
        { "V25", "https://dev.azure.com\\@evil.example/myorg/p/_git/r", null, null },
        { "V26", "https://evil.example\\@dev.azure.com/myorg/p/_git/r", null, null },
        { "X1", $"{D}/myorg/My%20Project/_git/My%20Repo", "myorg/My Project", "My Repo" },
        { "X2", $"{D}/myorg/%2e%2e/otherorg/p/_git/r", null, null },
        { "X3", $"{D}/myorg/p/%2E%2E/%2E%2E/%2E%2E/otherorg/p/_git/r", null, null },
        { "X4", $"{D}/myorg/%5C..%5Cotherorg/p/_git/r", null, null },
        { "X5", $"{D}/myorg/%252E%252E/otherorg/p/_git/r", null, null },
        { "X6", $"{D}/myorg/%C0%AE%C0%AE/otherorg/p/_git/r", null, null },
        { "X7", $"{D}/myorg/p/_git/r?x=1", null, null },
        { "X8", $"{D}/myorg/p/_git/r#frag", null, null },
        { "X9", $"{D}/my%6Frg/p/_git/r", null, null },
        { "X10", $"{D}/myorg%20/p/_git/r", null, null },
        { "X11", $"{D}/myorg/p/_git/r%2F..%2F..%2F..%2F..%2Fotherorg", null, null },
        { "X12", $"{D}/myorg/.%2E/otherorg/p/_git/r", null, null },
    };

    /// <summary>The service's other forms (6.2) and their answers.</summary>
    public static TheoryData<string, string?, string?> ServiceForms => new()
    {
        // The project-less form: the project is the repository's name (MEASURED r5 6.1).
        { $"{D}/myorg/_git/r", "myorg/r", "r" },
        { $"{D}/myorg/_git/r/", "myorg/r", "r" },
        { $"{D}/myorg/p/_git/_full/r", "myorg/p", "r" },
        { $"{D}/myorg/p/_git/_optimized/r", "myorg/p", "r" },
        { $"{D}/myorg/_git/_full/r", "myorg/r", "r" },
        { $"{D}/myorg/p/_GIT/r", "myorg/p", "r" },
        // .git belongs to the name (MEASURED r5 6.1).
        { $"{D}/myorg/p/_git/r.git", "myorg/p", "r.git" },
        { "https://x-access-token@dev.azure.com/myorg/p/_git/r", "myorg/p", "r" },
        { $"{D}/myorg/p/_git/caf%C3%A9", "myorg/p", "café" },
        { "https://myorg.visualstudio.com/_git/r", "myorg/r", "r" },
        { "https://myorg.visualstudio.com/DefaultCollection/_git/r", "myorg/r", "r" },
        { "https://myorg.visualstudio.com:443/p/_git/r", "myorg/p", "r" },
        { "https://myorg@myorg.visualstudio.com/p/_git/r/", "myorg/p", "r" },
        // SSH, recognised and never bound (r5 6.1).
        { "git@ssh.dev.azure.com:v3/myorg/p/r", "myorg/p", "r" },
        { "ssh.dev.azure.com:v3/myorg/p/r", "myorg/p", "r" },
        { "ssh://git@ssh.dev.azure.com/v3/myorg/p/r", "myorg/p", "r" },
        { "ssh://git@ssh.dev.azure.com:22/v3/myorg/My%20Project/r", "myorg/My Project", "r" },
        { "myorg@vs-ssh.visualstudio.com:v3/myorg/p/r", "myorg/p", "r" },
        { "ssh://myorg@vs-ssh.visualstudio.com/v3/myorg/p/r/", "myorg/p", "r" },
        // Not recognised.
        { $"{D}/myorg/p/_git", null, null },
        { $"{D}/myorg/p/_git/", null, null },
        { $"{D}/myorg/p/_git/r/extra", null, null },
        { $"{D}/myorg/p/_git/_full", "myorg/p", "_full" },
        { $"{D}/myorg/p", null, null },
        { $"{D}/myorg", null, null },
        { D, null, null },
        { $"{D}/DefaultCollection/p/_git/r", null, null },
        { $"{D}/defaultcollection/p/_git/r", null, null },
        { $"{D}/myorg/_p/_git/r", null, null },
        { $"{D}/myorg/%5Fp/_git/r", null, null },
        { $"{D}/myorg/_git/_r", null, null },
        { $"{D}/my.org/p/_git/r", null, null },
        { $"{D}/my_org/p/_git/r", null, null },
        { "https://user:secret@dev.azure.com/myorg/p/_git/r", null, null },
        // An empty user is none, as .NET and git read it (and Select binds it).
        { "https://@dev.azure.com/myorg/p/_git/r", "myorg/p", "r" },
        { "https://a@b@dev.azure.com/myorg/p/_git/r", null, null },
        { "http://dev.azure.com/myorg/p/_git/r", null, null },
        { "https://dev.azure.com:8443/myorg/p/_git/r", null, null },
        { "https://dev.azure.com:80/myorg/p/_git/r", null, null },
        { "https://www.dev.azure.com/myorg/p/_git/r", null, null },
        { "https://dev.azure.com.example/myorg/p/_git/r", null, null },
        { "https://myorg.visualstudio.com./p/_git/r", null, null },
        { "https://vssps.visualstudio.com/p/_git/r", null, null },
        { "https://app.visualstudio.com/p/_git/r", null, null },
        { "https://vs-ssh.visualstudio.com/p/_git/r", null, null },
        { "https://my_org.visualstudio.com/p/_git/r", null, null },
        { "http://myorg.visualstudio.com/p/_git/r", null, null },
        { "https://myorg.visualstudio.com:8443/p/_git/r", null, null },
        { "https://myorg.visualstudio.com", null, null },
        { "https://myorg.visualstudio.com/DefaultCollection/p/_git/r/x", null, null },
        { "ftp::https://dev.azure.com/myorg/p/_git/r", null, null },
        { "git@ssh.dev.azure.com:v3/myorg/p", null, null },
        { "git@ssh.dev.azure.com:v3/myorg/p/r/x", null, null },
        { "git@ssh.dev.azure.com:v2/myorg/p/r", null, null },
        { "git@ssh.dev.azure.com:myorg/p/r", null, null },
        { "git@ssh.dev.azure.com:v3/my.org/p/r", null, null },
        { "git@ssh.dev.azure.com:v3/myorg/../p/r", null, null },
        { "git@ssh.dev.azure.com:v3/myorg/%2E%2E/r", null, null },
        { "git@ssh.dev.azure.com:v3/myorg//r", null, null },
        { "git@ssh.dev.azure.com:v3/myorg/p\\x/r", null, null },
        { "git@ssh.dev.azure.com:v3/myorg/p/r?x", null, null },
        { "git@ssh.dev.azure.com:v3/myorg/_p/r", null, null },
        { "git:pw@ssh.dev.azure.com:v3/myorg/p/r", null, null },
        { "ssh://git@ssh.dev.azure.com:2222/v3/myorg/p/r", null, null },
        { "ssh://git@ssh.dev.azure.com/myorg/p/r", null, null },
        { "git@x.ssh.dev.azure.com:v3/myorg/p/r", null, null },
        { "git@ssh.dev.azure.com:v3/myorg/p/_ssh/r", null, null },
        { "https://ssh.dev.azure.com/v3/myorg/p/r", null, null },
        { "https://dev.azure.com/myorg/p/_git/ré", null, null },
        { "https://dev.azure.com/myorg/p/_git/r x", null, null },
        { "", null, null },
    };

    /// <summary>The fake's forms (<see cref="AzureDevOpsEndpoints.Local"/>): only its own scheme, host and port, in the dev.azure.com forms.</summary>
    public static TheoryData<string, string?, string?> LocalForms => new()
    {
        { "http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app", "Fernhill-Labs/Mobile App", "mobile-app" },
        { "http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app", "Fernhill-Labs/Mobile App", "mobile-app" },
        { "http://x-access-token@127.0.0.1:47397/Fernhill-Labs/_git/manifest/", "Fernhill-Labs/manifest", "manifest" },
        { "http://127.0.0.1:47398/Fernhill-Labs/Mobile%20App/_git/mobile-app", null, null },
        { "http://127.0.0.1/Fernhill-Labs/Mobile%20App/_git/mobile-app", null, null },
        { "https://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app", null, null },
        { "http://localhost:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app", null, null },
        { "https://dev.azure.com/myorg/p/_git/r", null, null },
        { "https://myorg.visualstudio.com/p/_git/r", null, null },
        { "git@ssh.dev.azure.com:v3/myorg/p/r", null, null },
        { "http://127.0.0.1:47397/DefaultCollection/p/_git/r", null, null },
    };

    [Theory]
    [MemberData(nameof(G32Rows))]
    public void G32s_rows_are_recognised_where_g34_binds_them_and_v11_too(string id, string url, string? owner, string? name)
    {
        var recognised = AzureDevOpsUrls.Recognise(Service, url);

        Assert.True(owner is null ? recognised is null : recognised == new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", owner, name!), id);
        if (recognised is not null)
        {
            // Kept as written (the record compares without case): the organization's spelling too.
            Assert.Equal((owner, name, "dev.azure.com"), (recognised.Owner, recognised.Name, recognised.Host));

            // A recognised http or https URL is in g3 G3.4's form, so it names what git reaches.
            Assert.True(CredentialBinding.InPlainForm(url), id);
        }
    }

    [Theory]
    [MemberData(nameof(ServiceForms))]
    public void The_services_forms_are_recognised_and_others_are_not(string url, string? owner, string? name)
    {
        var recognised = AzureDevOpsUrls.Recognise(Service, url);

        Assert.Equal(owner is null ? null : $"{owner}|{name}|dev.azure.com", Described(recognised));
        Assert.True(recognised is null || recognised.Provider == HostingProvider.AzureDevOps);
    }

    [Theory]
    [MemberData(nameof(LocalForms))]
    public void The_fakes_forms_are_its_own_origin_alone(string url, string? owner, string? name)
    {
        var recognised = AzureDevOpsUrls.Recognise(Local, url);

        Assert.Equal(owner is null ? null : $"{owner}|{name}|127.0.0.1:47397", Described(recognised));
    }

    /// <summary>
    /// URLs that .NET (and so <see cref="CredentialBinding.Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/>)
    /// and git read as the service's or the fake's host and port, written another way: an empty user,
    /// an empty port, a port's leading zeros, the fake's 127.0.0.1 in another IPv4 form; and some
    /// near them that name another host or port, or none .NET reads. The owner and name recognised, or null.
    /// </summary>
    public static TheoryData<string, string, string?, string?> AuthorityForms => new()
    {
        { "service", "https://@dev.azure.com/myorg/p/_git/r", "myorg/p", "r" },
        { "service", "https://@myorg.visualstudio.com/p/_git/r", "myorg/p", "r" },
        { "service", "https://dev.azure.com:/myorg/p/_git/r", "myorg/p", "r" },
        { "service", "https://myorg@dev.azure.com:/myorg/p/_git/r", "myorg/p", "r" },
        { "service", "https://myorg.visualstudio.com:/p/_git/r", "myorg/p", "r" },
        { "service", "https://dev.azure.com:0443/myorg/p/_git/r", "myorg/p", "r" },
        { "service", "https://dev.azure.com:000443/myorg/p/_git/r", "myorg/p", "r" },
        { "service", "https://dev.azure.com:0000000000443/myorg/p/_git/r", "myorg/p", "r" },
        { "service", "https://myorg.visualstudio.com:000443/p/_git/r", "myorg/p", "r" },
        { "service", "https://dev.azure.com:0000000000444/myorg/p/_git/r", null, null },
        { "service", "https://dev.azure.com:99999/myorg/p/_git/r", null, null },
        { "service", "https://myorg.visualstudio.com:0080/p/_git/r", null, null },
        { "local", "http://127.1:47397/fake-org/p/_git/r", "fake-org/p", "r" },
        { "local", "http://2130706433:47397/fake-org/p/_git/r", "fake-org/p", "r" },
        { "local", "http://0x7f000001:47397/fake-org/p/_git/r", "fake-org/p", "r" },
        { "local", "http://0177.0.0.1:47397/fake-org/p/_git/r", "fake-org/p", "r" },
        { "local", "http://127.000.000.001:47397/fake-org/p/_git/r", "fake-org/p", "r" },
        { "local", "http://@127.0.0.1:47397/fake-org/p/_git/r", "fake-org/p", "r" },
        { "local", "http://127.0.0.1:047397/fake-org/p/_git/r", "fake-org/p", "r" },
        { "local", "http://127.0.0.1:/fake-org/p/_git/r", null, null },
        { "local", "http://127.2:47397/fake-org/p/_git/r", null, null },
        { "local", "http://127.0.0.1:47398/fake-org/p/_git/r", null, null },
    };

    [Theory]
    [MemberData(nameof(AuthorityForms))]
    public void A_host_and_port_are_read_as_dotnet_reads_them_so_recognition_is_where_select_binds(string endpoints, string url, string? owner, string? name)
    {
        var (on, organization) = endpoints == "local" ? (Local, "fake-org") : (Service, "myorg");

        var recognised = AzureDevOpsUrls.Recognise(on, url);

        Assert.Equal(owner is null ? null : $"{owner}|{name}|{on.Host}", Described(recognised));

        // Recognised exactly where git gets the account's token: else the repository would show no
        // provider while git is answered with the token, and Azure DevOps' push guard would be skipped.
        Assert.Equal(recognised is not null, Bound(on, organization, url));
        Assert.Equal(owner is null ? null : organization, AzureDevOpsUrls.OrganizationOf(url, on));
    }

    [Theory]
    [MemberData(nameof(G32Rows))]
    public void G32s_rows_are_recognised_exactly_where_select_binds_them_but_for_another_users(string id, string url, string? owner, string? name)
    {
        _ = name;
        var recognised = AzureDevOpsUrls.Recognise(Service, url) is not null;

        Assert.Equal(owner is not null, recognised);
        Assert.Equal(id != "V11" && recognised, Bound(Service, "myorg", url));
    }

    /// <summary>Whether <see cref="CredentialBinding.Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/> binds <paramref name="url"/> under the provider's coverage of <paramref name="organization"/>'s account.</summary>
    private static bool Bound(AzureDevOpsEndpoints endpoints, string organization, string url)
    {
        using var provider = new AzureDevOpsProvider(endpoints, () => new RecordingHttpHandler(_ => throw new InvalidOperationException("No request is made.")), new ListLog(), TimeProvider.System);
        var coverage = provider.CoverageOf(new AccountKey(HostingProvider.AzureDevOps, endpoints.Host, organization));

        // One URL, as fetch and push: any binding is of it (a binding is the URL without its user).
        return CredentialBinding.Select([new RemoteEffectiveUrls("origin", [url], [url])], coverage).Count > 0;
    }

    private static string? Described(HostedRepository? repository) =>
        repository is null ? null : $"{repository.Owner}|{repository.Name}|{repository.Host}";

    [Fact]
    public void A_null_url_and_the_services_forms_under_the_fake_are_not_recognised()
    {
        Assert.Null(AzureDevOpsUrls.Recognise(Service, null));
        Assert.Null(AzureDevOpsUrls.Recognise(Local, null));
        Assert.Null(AzureDevOpsUrls.Recognise(Service, "http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app"));

        // The service's endpoints with the live command's project (a copy) recognise as the service's.
        Assert.NotNull(AzureDevOpsUrls.Recognise(Service with { ListProject = "Sandbox" }, "https://myorg.visualstudio.com/p/_git/r"));
    }

    /// <summary>The organization a typed text names (6.2; ruling 16): names, both URL forms, a user part, the fake's base, and refusals.</summary>
    public static TheoryData<string?, string?> ServiceOrganizations => new()
    {
        { "myorg", "myorg" },
        { "  My-Org-2  ", "My-Org-2" },
        { "https://dev.azure.com/myorg", "myorg" },
        { "https://dev.azure.com/myorg/", "myorg" },
        { "https://dev.azure.com/MyOrg/p/_git/r", "MyOrg" },
        { "https://DEV.AZURE.COM:443/myorg?view=x", "myorg" },
        { "https://dev.azure.com/myorg#top", "myorg" },
        { "https://myorg@dev.azure.com/myorg/p/_git/r", "myorg" },
        { "https://user:secret@dev.azure.com/myorg", "myorg" },
        { "https://myorg.visualstudio.com", "myorg" },
        { "https://MyOrg.VisualStudio.com/DefaultCollection/p/_git/r", "MyOrg" },
        { "https://myorg@myorg.visualstudio.com/", "myorg" },
        { "HTTPS://dev.azure.com/myorg", "myorg" },
        { null, null },
        { "", null },
        { "   ", null },
        { "my org", null },
        { "my.org", null },
        { "my_org", null },
        { "my%6Frg", null },
        { "myorg/p", null },
        { "dev.azure.com/myorg", null },
        { "https://dev.azure.com", null },
        { "https://dev.azure.com/", null },
        { "https://dev.azure.com//myorg", null },
        { "https://dev.azure.com/my%6Frg", null },
        { "https://dev.azure.com/my org", null },
        { "https://dev.azure.com:8443/myorg", null },
        { "http://dev.azure.com/myorg", null },
        { "https://www.dev.azure.com/myorg", null },
        { "https://app.vssps.visualstudio.com/myorg", null },
        { "https://vssps.visualstudio.com/myorg", null },
        { "https://x.myorg.visualstudio.com/", null },
        { "http://myorg.visualstudio.com/", null },
        { "https://example.com/myorg", null },
        { "ftp://dev.azure.com/myorg", null },
        { "http://127.0.0.1:47397/myorg", null },

        // A backslash, which .NET reads as a slash where the text has none, names nothing either way round.
        { "https://dev.azure.com\\@evil.example/myorg", null },
        { "https://evil.example\\@dev.azure.com/myorg", null },
    };

    [Theory]
    [MemberData(nameof(ServiceOrganizations))]
    public void A_typed_organization_is_a_name_or_either_url_form_in_the_grammar(string? text, string? expected)
    {
        Assert.Equal(expected, AzureDevOpsUrls.OrganizationOf(text, Service));
    }

    [Fact]
    public void Under_the_fake_the_organization_is_the_first_segment_on_its_base_alone()
    {
        Assert.Equal("Fernhill-Labs", AzureDevOpsUrls.OrganizationOf("http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app", Local));
        Assert.Equal("Fernhill-Labs", AzureDevOpsUrls.OrganizationOf("http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs", Local));
        Assert.Equal("Fernhill-Labs", AzureDevOpsUrls.OrganizationOf("Fernhill-Labs", Local));
        Assert.Null(AzureDevOpsUrls.OrganizationOf("http://127.0.0.1:47397/", Local));
        Assert.Null(AzureDevOpsUrls.OrganizationOf("http://127.0.0.1:47398/Fernhill-Labs", Local));
        Assert.Null(AzureDevOpsUrls.OrganizationOf("https://dev.azure.com/Fernhill-Labs", Local));
        Assert.Null(AzureDevOpsUrls.OrganizationOf("https://Fernhill-Labs.visualstudio.com/", Local));
    }

    [Fact]
    public void A_repositorys_web_and_api_addresses_escape_each_name()
    {
        var repository = new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", "Fernhill-Labs/Mobile App", "mobile app#1");

        Assert.Equal("https://dev.azure.com/Fernhill-Labs/Mobile%20App/_git/mobile%20app%231", AzureDevOpsUrls.WebUrlOf(Service, repository).AbsoluteUri);
        Assert.Equal(
            "https://dev.azure.com/Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile%20app%231/pullrequests?api-version=7.1",
            AzureDevOpsUrls.ApiPath(Service, repository, "/pullrequests?api-version=7.1").AbsoluteUri);
        Assert.Equal("http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile%20app%231", AzureDevOpsUrls.WebUrlOf(Local, repository).AbsoluteUri);

        // A name that would leave the organization once resolved, or an owner of another shape, is refused.
        // A name holding / or \ (which no Azure DevOps name holds) is refused too: escaped as %2F or
        // %5C, a server that decodes it before resolving dot segments would read a separator.
        foreach (var (owner, name) in new[] { ("Fernhill-Labs/..", "r"), ("Fernhill-Labs/p", ".."), ("Fernhill-Labs/p", "."), ("Fernhill-Labs", "r"), ("Fernhill-Labs/p/x", "r"), ("../x", "r"), ("my.org/p", "r"), ("/p", "r"), ("Fernhill-Labs/p", "a/b"), ("Fernhill-Labs/p", "a\\b"), ("Fernhill-Labs/p\\..", "r"), ("Fernhill-Labs/p", "..\\..\\x") })
        {
            var refused = new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", owner, name);
            Assert.Null(AzureDevOpsUrls.NamesOf(refused));
            Assert.Throws<ArgumentException>(() => AzureDevOpsUrls.ApiPath(Service, refused, string.Empty));
            Assert.Throws<ArgumentException>(() => AzureDevOpsUrls.WebUrlOf(Service, refused));
        }
    }
}
