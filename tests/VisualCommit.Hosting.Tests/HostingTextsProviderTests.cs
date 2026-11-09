using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// <c>HostingTexts</c>' provider words and the credential texts' provider forms
/// (docs/phase-7b-design.md 1.7, 1.10; ruling 21, D351, D355): GitHub's 7a's word for word, the
/// others' words and numbers.
/// </summary>
public sealed class HostingTextsProviderTests
{
    [Fact]
    public void GitHubs_words_are_7as_word_for_word()
    {
        const HostingProvider p = HostingProvider.GitHub;

        Assert.Equal(HostingTexts.GitHub, HostingTexts.Name(p));
        Assert.Equal("GitHub", HostingTexts.Name(p));
        Assert.Equal("GitHub's", HostingTexts.Possessive(p));
        Assert.Equal("pull request", HostingTexts.Request(p));
        Assert.Equal("pull requests", HostingTexts.Requests(p));
        Assert.Equal("Pull request", HostingTexts.RequestTitle(p));
        Assert.Equal(LeftPanelViewModel.PullRequestsTitle, HostingTexts.RequestsTitle(p));
        Assert.Equal("#12", HostingTexts.Number(p, 12));
        Assert.Equal(HostingTexts.AddAccountTitle, HostingTexts.AddAccountTitleOf(p));
        Assert.Equal("Add GitHub account", HostingTexts.AddAccountTitleOf(p));
        Assert.Equal(HostingTexts.SignInToGitHubItem, HostingTexts.SignInToItemOf(p));
        Assert.Equal("Sign in to GitHub…", HostingTexts.SignInToItemOf(p));
        Assert.Equal(HostingTexts.SignInInSettings, HostingTexts.SignInInSettingsOf(p));
        Assert.Equal("Sign in to GitHub in Settings, Accounts.", HostingTexts.SignInInSettingsOf(p));
    }

    [Theory]
    [InlineData(HostingProvider.GitLab, "GitLab", "GitLab's", "merge request", "merge requests", "Merge request", "Merge requests")]
    [InlineData(HostingProvider.AzureDevOps, "Azure DevOps", "Azure DevOps'", "pull request", "pull requests", "Pull request", "Pull requests")]
    public void The_new_providers_have_their_own_words(
        HostingProvider p, string name, string possessive, string request, string requests, string requestTitle, string requestsTitle)
    {
        Assert.Equal(name, HostingTexts.Name(p));
        Assert.Equal(possessive, HostingTexts.Possessive(p));
        Assert.Equal(request, HostingTexts.Request(p));
        Assert.Equal(requests, HostingTexts.Requests(p));
        Assert.Equal(requestTitle, HostingTexts.RequestTitle(p));
        Assert.Equal(requestsTitle, HostingTexts.RequestsTitle(p));
        Assert.Equal($"Add {name} account", HostingTexts.AddAccountTitleOf(p));
        Assert.Equal($"Sign in to {name}…", HostingTexts.SignInToItemOf(p));
        Assert.Equal($"Sign in to {name} in Settings, Accounts.", HostingTexts.SignInInSettingsOf(p));
    }

    [Theory]
    [InlineData(HostingProvider.GitHub, 1, "#1")]
    [InlineData(HostingProvider.GitHub, 1234, "#1234")]
    [InlineData(HostingProvider.GitLab, 12, "!12")]
    [InlineData(HostingProvider.AzureDevOps, 12, "!12")]
    [InlineData(HostingProvider.AzureDevOps, 20001, "!20001")]
    public void A_requests_number_is_GitHubs_hash_and_the_others_bang(HostingProvider p, int number, string text) =>
        Assert.Equal(text, HostingTexts.Number(p, number));

    [Fact]
    public void GitHubs_refusals_are_7as_word_for_word_in_both_forms()
    {
        const string GitCredentials =
            "GitHub refused git's credentials for Octo. If the token has expired or was revoked, sign in to GitHub again in Settings, Accounts.";
        const string Token = "GitHub refused the token of Octo. Sign in to GitHub in Settings, Accounts.";

        Assert.Equal(GitCredentials, OperationTexts.GitCredentialsRefused("Octo"));
        Assert.Equal(GitCredentials, OperationTexts.GitCredentialsRefused(HostingProvider.GitHub, "Octo"));
        Assert.Equal(Token, OperationTexts.TokenRefused("Octo"));
        Assert.Equal(Token, OperationTexts.TokenRefused(HostingProvider.GitHub, "Octo"));
    }

    [Fact]
    public void The_new_providers_refusals_name_them()
    {
        Assert.Equal(
            "GitLab refused git's credentials for Ada. If the token has expired or was revoked, sign in to GitLab again in Settings, Accounts.",
            OperationTexts.GitCredentialsRefused(HostingProvider.GitLab, "Ada"));
        Assert.Equal(
            "Azure DevOps refused the token of Fernhill-Labs. Sign in to Azure DevOps in Settings, Accounts.",
            OperationTexts.TokenRefused(HostingProvider.AzureDevOps, "Fernhill-Labs"));
    }
}
