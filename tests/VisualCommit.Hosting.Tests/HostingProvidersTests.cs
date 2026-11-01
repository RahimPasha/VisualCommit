using VisualCommit.Core.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// <see cref="HostingProviders"/> and the provider part of <see cref="AccountKey"/>
/// (docs/phase-7b-design.md 1.2, 1.10; D353, D378): each provider's names and id, the Accounts
/// section's order, and which provider's form a token has, from its prefix and length alone.
/// </summary>
public sealed class HostingProvidersTests
{
    [Fact]
    public void Seven_a_s_value_keeps_its_ordinal_and_the_accounts_order_is_GitHub_GitLab_Azure_DevOps()
    {
        Assert.Equal(0, (int)HostingProvider.GitHub);
        Assert.Equal([HostingProvider.GitHub, HostingProvider.AzureDevOps, HostingProvider.GitLab], Enum.GetValues<HostingProvider>());
        Assert.Equal([HostingProvider.GitHub, HostingProvider.GitLab, HostingProvider.AzureDevOps], HostingProviders.InAccountsOrder);
    }

    [Theory]
    [InlineData(HostingProvider.GitHub, "GitHub", "GitHub's", "github")]
    [InlineData(HostingProvider.GitLab, "GitLab", "GitLab's", "gitlab")]
    [InlineData(HostingProvider.AzureDevOps, "Azure DevOps", "Azure DevOps'", "azure-devops")]
    public void Each_provider_has_its_name_possessive_and_id(HostingProvider provider, string name, string possessive, string id)
    {
        Assert.Equal(name, HostingProviders.DisplayName(provider));
        Assert.Equal(possessive, HostingProviders.Possessive(provider));
        Assert.Equal(id, HostingProviders.Id(provider));
        Assert.Equal(provider, HostingProviders.FromId(id));
        Assert.Equal(provider, HostingProviders.FromId(id.ToUpperInvariant()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("azure devops")]
    [InlineData("azuredevops")]
    [InlineData(" github")]
    [InlineData("bitbucket")]
    public void Another_id_is_no_provider(string? id) => Assert.Null(HostingProviders.FromId(id));

    [Fact]
    public void An_unknown_value_has_no_name()
    {
        var unknown = (HostingProvider)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => HostingProviders.DisplayName(unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => HostingProviders.Possessive(unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => HostingProviders.Id(unknown));
    }

    [Fact]
    public void An_account_key_names_its_provider_by_id_and_keeps_host_and_login_lower_case()
    {
        var gitLab = new AccountKey(HostingProvider.GitLab, "GitLab.com", "Ada");
        var azure = new AccountKey(HostingProvider.AzureDevOps, "dev.azure.com", "Fernhill-Labs");

        Assert.Equal(("gitlab", "gitlab:gitlab.com:ada"), (gitLab.ProviderId, gitLab.Id));
        Assert.Equal(("azure-devops", "azure-devops:dev.azure.com:fernhill-labs"), (azure.ProviderId, azure.Id));
        Assert.Equal("github:github.com:octocat", new AccountKey(HostingProvider.GitHub, "github.com", "OctoCat").ToString());
        Assert.Equal(azure, new AccountKey(HostingProvider.AzureDevOps, "DEV.AZURE.COM", "fernhill-labs"));
        Assert.NotEqual(new AccountKey(HostingProvider.GitHub, "gitlab.com", "ada"), gitLab);
    }

    [Theory]
    [InlineData("github_pat_x")]
    [InlineData("ghp_x")]
    [InlineData("gho_x")]
    [InlineData("ghu_x")]
    [InlineData("ghs_x")]
    [InlineData("ghr_x")]
    [InlineData("github_pat_FAKE-octo-1")]
    public void GitHubs_prefixes_are_GitHubs_form(string token) =>
        Assert.Equal(HostingProvider.GitHub, HostingProviders.TokenFormOf(token));

    [Theory]
    [InlineData("glpat-x")]
    [InlineData("glpat-FAKE-ada-1")]
    public void GitLabs_prefix_is_GitLabs_form(string token) =>
        Assert.Equal(HostingProvider.GitLab, HostingProviders.TokenFormOf(token));

    [Fact]
    public void Eighty_four_characters_with_AZDO_at_76_are_Azure_DevOps_form()
    {
        Assert.Equal(HostingProvider.AzureDevOps, HostingProviders.TokenFormOf(AzureForm(76, 84)));
        Assert.Equal(HostingProvider.AzureDevOps, HostingProviders.TokenFormOf(new string('7', 76) + "AZDO" + "x9Q_"));
    }

    [Theory]
    [InlineData(76, 83)]
    [InlineData(76, 85)]
    [InlineData(75, 84)]
    [InlineData(77, 84)]
    public void A_length_or_a_mark_one_off_is_no_form(int markAt, int length) =>
        Assert.Null(HostingProviders.TokenFormOf(AzureForm(markAt, length)));

    [Theory]
    [InlineData("GLPAT-x")]
    [InlineData("GHP_x")]
    [InlineData("Github_pat_x")]
    [InlineData("azdo_FAKE-Fernhill-Labs-1")]
    [InlineData("0123456789abcdef0123456789abcdef01234567")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789abcdefghijklmnop")]
    [InlineData("glpat")]
    [InlineData("gh_x")]
    [InlineData("token")]
    [InlineData("")]
    public void Anything_else_is_no_form_and_case_counts(string token) =>
        Assert.Null(HostingProviders.TokenFormOf(token));

    [Fact]
    public void A_lower_case_mark_is_no_form() =>
        Assert.Null(HostingProviders.TokenFormOf(new string('a', 76) + "azdo" + "bcde"));

    [Fact]
    public void A_null_token_is_refused() =>
        Assert.Throws<ArgumentNullException>(() => HostingProviders.TokenFormOf(null!));

    /// <summary>A made-up token of <paramref name="length"/> characters with <c>AZDO</c> at the 0-based <paramref name="markAt"/>.</summary>
    private static string AzureForm(int markAt, int length)
    {
        var token = new string('a', markAt) + "AZDO";
        return token + new string('b', length - token.Length);
    }
}
