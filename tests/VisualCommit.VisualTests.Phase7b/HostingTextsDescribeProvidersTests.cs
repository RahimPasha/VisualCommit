using System.Globalization;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using Xunit;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// <c>HostingTexts.Describe</c> for GitLab and Azure DevOps (docs/phase-7b-design.md 11.2, 11.6; D338,
/// D351): each row of 11.2's table in the provider's words, with and without a login and
/// <c>RetryAt</c>, and the empty messages (Azure DevOps' empty 401). GitHub's results stay 7a's, which
/// 7a's <c>HostingTextsDescribeTests</c> pin; a few here show that GitHub's path is not this table's.
/// </summary>
public sealed class HostingTextsDescribeProvidersTests
{
    public static TheoryData<HostingProvider, string, string> Names => new()
    {
        { HostingProvider.GitLab, "GitLab", "GitLab's" },
        { HostingProvider.AzureDevOps, "Azure DevOps", "Azure DevOps'" },
    };

    [Theory]
    [MemberData(nameof(Names))]
    public void Network_is_the_message_as_it_is(HostingProvider provider, string name, string possessive)
    {
        _ = possessive;
        Assert.Equal($"Could not reach {name}.", Describe(provider, HostingFailureKind.Network, $"Could not reach {name}."));
        Assert.Equal($"{name} did not answer within 30 seconds.", Describe(provider, HostingFailureKind.Network, $"{name} did not answer within 30 seconds.", login: "ada"));
    }

    [Fact]
    public void Bad_credentials_with_a_login_is_the_token_refused_of_that_provider()
    {
        Assert.Equal(
            "Azure DevOps refused the token of Fernhill-Labs. Sign in to Azure DevOps in Settings, Accounts.",
            Describe(HostingProvider.AzureDevOps, HostingFailureKind.BadCredentials, "Unauthorized", login: "Fernhill-Labs"));
        Assert.Equal(
            "Azure DevOps refused the token of Fernhill-Labs. Sign in to Azure DevOps in Settings, Accounts.",
            Describe(HostingProvider.AzureDevOps, HostingFailureKind.BadCredentials, string.Empty, login: "Fernhill-Labs"));
        Assert.Equal(
            "GitLab refused the token of ada. Sign in to GitLab in Settings, Accounts.",
            Describe(HostingProvider.GitLab, HostingFailureKind.BadCredentials, "401 Unauthorized", login: "ada"));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Bad_credentials_without_a_login_is_the_message_then_signing_in_in_settings(HostingProvider provider, string name, string possessive)
    {
        _ = possessive;
        var signIn = $"Sign in to {name} in Settings, Accounts.";
        Assert.Equal($"401 Unauthorized. {signIn}", Describe(provider, HostingFailureKind.BadCredentials, "401 Unauthorized"));
        Assert.Equal($"Not signed in. {signIn}", Describe(provider, HostingFailureKind.BadCredentials, "Not signed in."));
        Assert.Equal($"Not signed in. {signIn}", Describe(provider, HostingFailureKind.BadCredentials, "Not signed in.  ", login: string.Empty));
        Assert.Equal(signIn, Describe(provider, HostingFailureKind.BadCredentials, string.Empty));
        Assert.Equal(signIn, Describe(provider, HostingFailureKind.BadCredentials, "   "));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void A_rate_limit_names_the_provider_and_the_time_when_it_is_known(HostingProvider provider, string name, string possessive)
    {
        _ = name;
        var retryAt = new DateTimeOffset(2026, 11, 6, 14, 5, 0, TimeSpan.Zero);
        var local = retryAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        Assert.Equal($"{possessive} rate limit was reached. Try again after {local}.", Describe(provider, HostingFailureKind.RateLimited, "429", retryAt: retryAt));
        Assert.Equal($"{possessive} rate limit was reached. Try again after {local}.", Describe(provider, HostingFailureKind.RateLimited, "429", login: "ada", retryAt: retryAt));
        Assert.Equal($"{possessive} rate limit was reached. Try again in a few minutes.", Describe(provider, HostingFailureKind.RateLimited, "429"));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void The_fixed_sentences_name_the_provider(HostingProvider provider, string name, string possessive)
    {
        Assert.Equal($"This version of VisualCommit needs an update to work with {name}.", Describe(provider, HostingFailureKind.ApiVersion, "410"));
        Assert.Equal($"{name} had a problem. Try again later.", Describe(provider, HostingFailureKind.ServerError, "500 Internal Server Error"));
        Assert.Equal($"{name} had a problem. Try again later.", Describe(provider, HostingFailureKind.ServerError, string.Empty, login: "ada"));
        Assert.Equal($"{possessive} answer could not be read.", Describe(provider, HostingFailureKind.Malformed, $"{possessive} answer could not be read."));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Any_other_kind_is_the_provider_s_words_and_an_empty_message_has_none(HostingProvider provider, string name, string possessive)
    {
        _ = possessive;
        foreach (var kind in new[] { HostingFailureKind.Forbidden, HostingFailureKind.NotFound, HostingFailureKind.Validation, HostingFailureKind.SsoRequired })
        {
            Assert.Equal($"{name} answered: 403 Forbidden.", Describe(provider, kind, "403 Forbidden"));
            Assert.Equal($"{name} answered: 404 Project Not Found.", Describe(provider, kind, "404 Project Not Found.", login: "ada"));
            Assert.Equal($"{name} answered without a reason.", Describe(provider, kind, string.Empty));
            Assert.Equal($"{name} answered without a reason.", Describe(provider, kind, " . "));
        }
    }

    [Fact]
    public void Sso_stays_github_s_alone()
    {
        Assert.Equal(
            "acme requires single sign-on: authorise the token for it on GitHub, then try again.",
            HostingTexts.Describe(new HostingFailure(HostingFailureKind.SsoRequired, "Resource protected by organization SAML enforcement.", Detail: "https://github.com/orgs/acme/sso?authorization_request=1"), null));
        Assert.Equal(
            "GitLab answered: Resource protected by organization SAML enforcement.",
            HostingTexts.Describe(new HostingFailure(HostingFailureKind.SsoRequired, "Resource protected by organization SAML enforcement.", Detail: "https://github.com/orgs/acme/sso") { Provider = HostingProvider.GitLab }, null));
    }

    [Fact]
    public void Github_s_results_are_7a_s()
    {
        Assert.Equal("GitHub refused the token of octo. Sign in to GitHub in Settings, Accounts.", Describe(HostingProvider.GitHub, HostingFailureKind.BadCredentials, "Bad credentials", login: "octo"));
        Assert.Equal("Bad credentials. Sign in to GitHub in Settings, Accounts.", Describe(HostingProvider.GitHub, HostingFailureKind.BadCredentials, "Bad credentials"));
        Assert.Equal("GitHub had a problem. Try again later.", Describe(HostingProvider.GitHub, HostingFailureKind.ServerError, "HTTP 502"));
        Assert.Equal("GitHub's answer could not be read.", Describe(HostingProvider.GitHub, HostingFailureKind.Malformed, "x"));
        Assert.Equal("GitHub answered: Not Found.", Describe(HostingProvider.GitHub, HostingFailureKind.NotFound, "Not Found"));
        Assert.Equal("GitHub's rate limit was reached. Try again in a few minutes.", Describe(HostingProvider.GitHub, HostingFailureKind.RateLimited, "x"));
    }

    private static string Describe(HostingProvider provider, HostingFailureKind kind, string message, string? login = null, DateTimeOffset? retryAt = null) =>
        HostingTexts.Describe(new HostingFailure(kind, message, RetryAt: retryAt) { Provider = provider }, login);
}
