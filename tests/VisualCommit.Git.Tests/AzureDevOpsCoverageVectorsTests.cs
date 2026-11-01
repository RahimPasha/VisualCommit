using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="CredentialBinding.Select(IReadOnlyList{RemoteEffectiveUrls}, GitCoverage)"/> and
/// <see cref="CredentialBinding.InPlainForm"/> on the 48 rows of docs/phase-7b-design.md 8.7, on
/// strings: g3 G3.4's answer as 8.2 words it, the bound text, its twin and owner (D369).
/// <see cref="CoverageGitMatchTests"/> runs the same rows through real git.
/// </summary>
public sealed class AzureDevOpsCoverageVectorsTests
{
    [Fact]
    public void The_table_has_48_rows_13_of_them_bound()
    {
        Assert.Equal(48, AzureDevOpsCoverageVectors.Rows.Count);
        Assert.Equal(48, AzureDevOpsCoverageVectors.Rows.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            ["V1", "V2", "V10", "V12", "V13", "V14", "V15", "V16", "X1", "P5", "Y1", "Y2", "U3"],
            AzureDevOpsCoverageVectors.Rows.Where(row => row.Bound is not null).Select(row => row.Id));
    }

    [Theory]
    [MemberData(nameof(AzureDevOpsCoverageVectors.Ids), MemberType = typeof(AzureDevOpsCoverageVectors))]
    public void Each_row_is_bound_as_G3_4_answers_with_its_twin_and_owner(string id)
    {
        var row = AzureDevOpsCoverageVectors.Row(id);

        var bindings = CredentialBinding.Select([Fetch(row.Url)], AzureDevOpsCoverageVectors.Coverage);

        CredentialBinding Expected(string url) => new(url, AzureDevOpsCoverageVectors.Account) { SharedOriginOwner = row.Owner };
        Assert.Equal(
            row.Bound is null ? [] : row.Twin is null ? [Expected(row.Bound)] : [Expected(row.Bound), Expected(row.Twin)],
            bindings);
    }

    [Theory]
    [MemberData(nameof(AzureDevOpsCoverageVectors.Ids), MemberType = typeof(AzureDevOpsCoverageVectors))]
    public void Each_row_is_in_the_plain_form_by_step_2s_rule_alone(string id)
    {
        var row = AzureDevOpsCoverageVectors.Row(id);

        Assert.Equal(row.InForm, CredentialBinding.InPlainForm(row.Url));
    }

    [Fact]
    public void The_form_holds_unbound_rows_that_only_the_organization_the_user_or_the_host_refuses()
    {
        // 8.7: the form has no user, host or organization clause.
        string[] inFormUnbound = ["P1", "P2", "P3", "P4", "V6", "X9", "X10", "V11", "V17", "V23"];

        Assert.Equal(
            inFormUnbound.Order(StringComparer.Ordinal),
            AzureDevOpsCoverageVectors.Rows.Where(row => row.InForm && row.Bound is null).Select(row => row.Id).Order(StringComparer.Ordinal));
        Assert.All(AzureDevOpsCoverageVectors.Rows.Where(row => row.Bound is not null), row => Assert.True(row.InForm, row.Id));
    }

    [Theory]
    [InlineData("my_org")]
    [InlineData("my.org")]
    [InlineData("my org")]
    [InlineData("myorg/")]
    [InlineData("")]
    [InlineData("%6Dyorg")]
    [InlineData("myörg")]
    public void An_organization_outside_the_grammar_binds_nothing(string organization)
    {
        var coverage = AzureDevOpsCoverageVectors.Coverage with { Organization = organization };

        Assert.Empty(CredentialBinding.Select(
            [Fetch($"https://dev.azure.com/{Uri.EscapeDataString(organization)}/p/_git/r"), Fetch("https://dev.azure.com/myorg/p/_git/r"), Fetch("https://myorg.visualstudio.com/p/_git/r")],
            coverage));
    }

    [Fact]
    public void An_organization_host_without_an_organization_and_a_git_twin_with_one_are_refused()
    {
        var account = AzureDevOpsCoverageVectors.Account;
        var origin = new Uri("https://dev.azure.com/");

        Assert.Throws<ArgumentException>("coverage", () => CredentialBinding.Select([Fetch("https://dev.azure.com/myorg/p/_git/r")], new GitCoverage(account, origin) { OrganizationHost = true }));
        Assert.Throws<ArgumentException>("coverage", () => CredentialBinding.Select([Fetch("https://dev.azure.com/myorg/p/_git/r")], new GitCoverage(account, origin) { Organization = "myorg", GitTwin = true }));
    }

    [Fact]
    public void Every_bound_row_together_keeps_the_remotes_order_once_each()
    {
        var bound = AzureDevOpsCoverageVectors.Rows.Where(row => row.Bound is not null).ToList();

        var bindings = CredentialBinding.Select([.. bound.Select(row => Fetch(row.Url))], AzureDevOpsCoverageVectors.Coverage);

        // V1 and V10 share their bound text, so it is there once.
        Assert.Equal(
            bound.SelectMany(row => row.Twin is null ? [row.Bound!] : new[] { row.Bound!, row.Twin }).Distinct(StringComparer.Ordinal),
            bindings.Select(binding => binding.Url));
    }

    private static RemoteEffectiveUrls Fetch(string url) => new("origin", [url], [url]);
}
