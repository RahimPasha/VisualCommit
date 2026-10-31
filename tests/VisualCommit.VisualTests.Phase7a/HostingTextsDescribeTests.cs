using System.Globalization;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Hosting;
using Xunit;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// <see cref="HostingTexts.Describe"/> (docs/phase-7a-design.md 1.7, 9.6; D338): each row of 1.7's
/// table, with and without a login, with and without a time to try again after, and single
/// sign-on's URL with and without an organisation.
/// </summary>
public sealed class HostingTextsDescribeTests
{
    [Fact]
    public void A_network_failure_is_its_message_as_it_is()
    {
        Assert.Equal("Could not reach GitHub.", HostingTexts.Describe(new HostingFailure(HostingFailureKind.Network, "Could not reach GitHub."), "octo"));
        Assert.Equal("GitHub did not answer within 30 seconds.", HostingTexts.Describe(new HostingFailure(HostingFailureKind.Network, "GitHub did not answer within 30 seconds."), null));
    }

    [Fact]
    public void Bad_credentials_name_the_login_or_else_quote_github_and_point_to_the_settings()
    {
        var failure = new HostingFailure(HostingFailureKind.BadCredentials, "Bad credentials");
        Assert.Equal(OperationTexts.TokenRefused("octo"), HostingTexts.Describe(failure, "octo"));
        Assert.Equal("GitHub refused the token of octo. Sign in to GitHub in Settings, Accounts.", HostingTexts.Describe(failure, "octo"));
        Assert.Equal("Bad credentials. Sign in to GitHub in Settings, Accounts.", HostingTexts.Describe(failure, null));
        Assert.Equal("Bad credentials. Sign in to GitHub in Settings, Accounts.", HostingTexts.Describe(failure, string.Empty));
        Assert.Equal(
            "The token is empty. Sign in to GitHub in Settings, Accounts.",
            HostingTexts.Describe(new HostingFailure(HostingFailureKind.BadCredentials, "The token is empty."), null));
    }

    [Fact]
    public void A_rate_limit_gives_the_local_time_to_try_again_after_or_a_few_minutes()
    {
        var retryAt = new DateTimeOffset(2026, 10, 29, 21, 5, 0, TimeSpan.Zero);
        var expected = retryAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        Assert.Equal(
            $"GitHub's rate limit was reached. Try again after {expected}.",
            HostingTexts.Describe(new HostingFailure(HostingFailureKind.RateLimited, "API rate limit exceeded", RetryAt: retryAt), "octo"));
        Assert.Equal(
            $"GitHub's rate limit was reached. Try again after {expected}.",
            HostingTexts.Describe(new HostingFailure(HostingFailureKind.RateLimited, "API rate limit exceeded", RetryAt: retryAt), null));
        Assert.Equal(
            "GitHub's rate limit was reached. Try again in a few minutes.",
            HostingTexts.Describe(new HostingFailure(HostingFailureKind.RateLimited, "API rate limit exceeded"), "octo"));
    }

    [Theory]
    [InlineData("https://github.com/orgs/acme/sso?authorization_request=A1B2", "acme")]
    [InlineData("http://127.0.0.1:47395/orgs/acme-labs/sso", "acme-labs")]
    [InlineData("https://github.com/sso", null)]
    [InlineData("https://github.com/orgs/", null)]
    [InlineData(null, null)]
    public void Single_sign_on_names_the_organisation_of_its_url_or_the_organisation(string? url, string? organisation)
    {
        var failure = new HostingFailure(HostingFailureKind.SsoRequired, "Resource protected by organization SAML enforcement.", url);
        Assert.Equal(
            $"{organisation ?? "The organisation"} requires single sign-on: authorise the token for it on GitHub, then try again.",
            HostingTexts.Describe(failure, "octo"));
        Assert.Equal(HostingTexts.Describe(failure, "octo"), HostingTexts.Describe(failure, null));
    }

    [Fact]
    public void The_api_version_a_server_error_and_an_unreadable_answer_are_the_app_s_words()
    {
        foreach (var login in new[] { "octo", null })
        {
            Assert.Equal("This version of VisualCommit needs an update to work with GitHub.", HostingTexts.Describe(new HostingFailure(HostingFailureKind.ApiVersion, "Unsupported API version"), login));
            Assert.Equal("GitHub had a problem. Try again later.", HostingTexts.Describe(new HostingFailure(HostingFailureKind.ServerError, "Server Error"), login));
            Assert.Equal("GitHub's answer could not be read.", HostingTexts.Describe(new HostingFailure(HostingFailureKind.Malformed, "Unexpected character"), login));
        }
    }

    [Theory]
    [InlineData(HostingFailureKind.Forbidden, "Resource not accessible by personal access token", "GitHub answered: Resource not accessible by personal access token.")]
    [InlineData(HostingFailureKind.NotFound, "Not Found", "GitHub answered: Not Found.")]
    [InlineData(HostingFailureKind.Validation, "A pull request already exists for octo:topic.", "GitHub answered: A pull request already exists for octo:topic.")]
    [InlineData(HostingFailureKind.Validation, "Validation Failed", "GitHub answered: Validation Failed.")]
    public void A_refusal_a_missing_repository_and_a_rejected_form_are_github_s_words_with_one_full_stop(HostingFailureKind kind, string message, string expected)
    {
        Assert.Equal(expected, HostingTexts.Describe(new HostingFailure(kind, message), "octo"));
        Assert.Equal(expected, HostingTexts.Describe(new HostingFailure(kind, message), null));
    }

    [Fact]
    public void Every_kind_has_a_wording()
    {
        foreach (var kind in Enum.GetValues<HostingFailureKind>())
        {
            Assert.False(string.IsNullOrWhiteSpace(HostingTexts.Describe(new HostingFailure(kind, "Message"), "octo")), kind.ToString());
        }
    }
}
