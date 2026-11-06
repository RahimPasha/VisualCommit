using System.Text.RegularExpressions;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting;
using VisualCommit.Hosting.AzureDevOps;
using VisualCommit.Hosting.GitHub;
using VisualCommit.Hosting.GitLab;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.AccountTexts;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// Part A's expectations against the App (docs/test-reports/phase-7b.md, part A's conventions):
/// <c>AccountTexts</c>' and <c>HostingTexts</c>' texts equal <see cref="PartA"/>'s, the pages'
/// addresses equal the providers' <c>TokenPage</c> and <c>LegacyTokenPage</c>, the made-up values have
/// the forms the report gives them, and the picture names are unique and match design 0.3's rule.
/// </summary>
public sealed partial class Phase7bExpectationTests
{
    [Fact]
    public void Part_A_s_section_texts_are_the_app_s()
    {
        Assert.Equal(PartA.Intro, Texts.AccountsIntro);
        Assert.Equal(PartA.Off, Texts.AccountsOff);
        Assert.Equal(PartA.SignedOut, Texts.AccountsSignedOut);
        Assert.Equal(PartA.SignInButton, Texts.AccountsSignInButton);
        Assert.Equal(PartA.SignOutButton, Texts.AccountsSignOutButton);
        Assert.Equal(PartA.Busy, Texts.AccountsBusy);
        Assert.Equal(PartA.InKeychain, Texts.TokenInKeychain);
        Assert.Equal(PartA.NoKeychain, Texts.NoKeychain);
        Assert.Equal(PartA.SessionEnded, Texts.NeedsSignInSessionEnded);
        Assert.Equal(PartA.UseForGit, Texts.AccountUseForGit);
        Assert.Equal(PartA.GitLabel, Texts.GitLabel);
        Assert.Equal(PartA.AccountsButton, HostingTexts.AccountsSection);
        Assert.Equal(PartA.GitHubUseForGitNote, Texts.AccountUseForGitNoteOf(HostingProvider.GitHub, OctoLogin));
        Assert.Equal(PartA.GitLabUseForGitNote, Texts.AccountUseForGitNoteOf(HostingProvider.GitLab, AdaLogin));
        Assert.Equal(PartA.AzureDevOpsUseForGitNote, Texts.AccountUseForGitNoteOf(HostingProvider.AzureDevOps, PartA.Organization));
        Assert.Equal(PartA.GitLabRefusedNote, Texts.NeedsSignInRefusedOf(HostingProvider.GitLab));
        Assert.Equal(PartA.AzureDevOpsRefusedNote, Texts.NeedsSignInRefusedOf(HostingProvider.AzureDevOps));
        Assert.Equal(Texts.NeedsSignInRefused, Texts.NeedsSignInRefusedOf(HostingProvider.GitHub));
        foreach (var provider in HostingProviders.InAccountsOrder)
        {
            Assert.Equal(PartA.LabelOf(provider), HostingTexts.Name(provider));
        }

        Assert.Equal(PartA.BlockIds, HostingProviders.InAccountsOrder.Select(provider => "Accounts" + (PartA.SuffixOf(provider) is { Length: > 0 } suffix ? suffix : "GitHub")));
        Assert.NotEqual(PartA.Intro7a, PartA.Intro);
    }

    [Fact]
    public void Part_A_s_confirmations_and_dialog_texts_are_the_app_s()
    {
        Assert.Equal(Texts.SignOutTitle, Texts.SignOutTitleOf(HostingProvider.GitHub));
        Assert.Equal(PartA.GitLabSignOutTitle, Texts.SignOutTitleOf(HostingProvider.GitLab));
        Assert.Equal(PartA.AzureDevOpsSignOutTitle, Texts.SignOutTitleOf(HostingProvider.AzureDevOps));
        Assert.Equal(PartA.GitLabSignOutQuestionAda, Texts.SignOutQuestionOf(HostingProvider.GitLab, AdaLogin, true));
        Assert.Equal(PartA.AzureDevOpsSignOutQuestion, Texts.SignOutQuestionOf(HostingProvider.AzureDevOps, PartA.Organization, true));
        Assert.Equal(Texts.SignOutQuestion(OctoLogin, true), Texts.SignOutQuestionOf(HostingProvider.GitHub, OctoLogin, true));
        Assert.Equal(PartA.SignOutConfirm, Texts.SignOutConfirm);

        Assert.Equal(PartA.GitHubDialogTitle, HostingTexts.AddAccountTitleOf(HostingProvider.GitHub));
        Assert.Equal(PartA.GitLabDialogTitle, HostingTexts.AddAccountTitleOf(HostingProvider.GitLab));
        Assert.Equal(PartA.AzureDevOpsDialogTitle, HostingTexts.AddAccountTitleOf(HostingProvider.AzureDevOps));
        Assert.Equal(PartA.GitHubHelp, Texts.SignInHelpOf(HostingProvider.GitHub));
        Assert.Equal(PartA.GitLabHelp, Texts.SignInHelpOf(HostingProvider.GitLab));
        Assert.Equal(PartA.AzureDevOpsHelp, Texts.SignInHelpOf(HostingProvider.AzureDevOps));
        Assert.Equal((PartA.GitHubTokenLink, PartA.GitHubTokenLinkTip), (Texts.TokenLinkOf(HostingProvider.GitHub), Texts.TokenLinkTipOf(HostingProvider.GitHub)));
        Assert.Equal((PartA.GitLabTokenLink, PartA.GitLabTokenLinkTip), (Texts.TokenLinkOf(HostingProvider.GitLab), Texts.TokenLinkTipOf(HostingProvider.GitLab)));
        Assert.Equal((PartA.AzureDevOpsTokenLink, PartA.AzureDevOpsTokenLinkTip), (Texts.TokenLinkOf(HostingProvider.AzureDevOps), Texts.TokenLinkTipOf(HostingProvider.AzureDevOps)));
        Assert.Equal((PartA.GitLabLegacyLink, PartA.GitLabLegacyLinkTip), (Texts.LegacyTokenLink, Texts.LegacyTokenLinkTip));
        Assert.Equal(PartA.OrganizationLabel, Texts.OrganizationLabel);
        Assert.Equal(PartA.TokenLabel, Texts.TokenLabel);
        Assert.Equal(PartA.Checking, Texts.SignInChecking);
        Assert.Equal((PartA.Cancel, PartA.SignIn, PartA.Close), (Texts.Cancel, Texts.SignIn, Texts.Close));
    }

    [Fact]
    public void Part_A_s_errors_are_the_app_s()
    {
        Assert.Equal(PartA.GitLabRefused401, Texts.SignInFailedOf(Failure(HostingProvider.GitLab, HostingFailureKind.BadCredentials, "401 Unauthorized"), null));
        Assert.Equal(PartA.AzureDevOpsNoOrganization, Texts.SignInFailedOf(Failure(HostingProvider.AzureDevOps, HostingFailureKind.NotFound, "x"), PartA.UnknownOrganization));
        Assert.Equal(
            PartA.AzureDevOpsAnonymous,
            Texts.SignInFailedOf(Failure(HostingProvider.AzureDevOps, HostingFailureKind.BadCredentials, "x") with { Detail = AzureDevOpsProvider.AnonymousDetail }, PartA.Organization));
        Assert.Equal(PartA.AzureDevOpsRefused, Texts.SignInFailedOf(Failure(HostingProvider.AzureDevOps, HostingFailureKind.BadCredentials, "x"), PartA.Organization));
        Assert.Equal(PartA.NotAToken, Texts.SignInNotAToken);
        Assert.Equal(PartA.NotAnOrganizationError, Texts.SignInNotAnOrganization);
        Assert.Equal(PartA.GitLabGivenAzureDevOpsToken, Texts.SignInOtherProviderTokenOf(HostingProvider.GitLab, HostingProvider.AzureDevOps, null));
        Assert.Equal(PartA.GitHubGivenGitLabToken, Texts.SignInOtherProviderTokenOf(HostingProvider.GitHub, HostingProvider.GitLab, null));
        Assert.Equal(PartA.AzureDevOpsGivenGitHubToken, Texts.SignInOtherProviderTokenOf(HostingProvider.AzureDevOps, HostingProvider.GitHub, PartA.Organization));
        Assert.Equal(PartA.GitHubUnreachable, Texts.SignInUnreachableOf(HostingProvider.GitHub));
        Assert.Equal(PartA.GitLabUnreachable, Texts.SignInUnreachableOf(HostingProvider.GitLab));
        Assert.Equal(PartA.AzureDevOpsUnreachable, Texts.SignInUnreachableOf(HostingProvider.AzureDevOps));
    }

    [Theory]
    [InlineData(GitHubPort, GitLabPort, AzureDevOpsPort)]
    [InlineData(51001, 51002, 51003)]
    public void Part_A_s_pages_are_the_providers(int gitHubPort, int gitLabPort, int azureDevOpsPort)
    {
        using var gitHub = new GitHubProvider(GitHubEndpoints.Local(gitHubPort), () => new NeverSends(), new NullAppLog(), TimeProvider.System);
        using var gitLab = new GitLabProvider(GitLabEndpoints.Local(gitLabPort), () => new NeverSends(), new NullAppLog(), TimeProvider.System);
        using var azureDevOps = new AzureDevOpsProvider(AzureDevOpsEndpoints.Local(azureDevOpsPort), () => new NeverSends(), new NullAppLog(), TimeProvider.System);

        Assert.Equal(PartA.GitHubTokenPage(gitHubPort), gitHub.TokenPage(null).AbsoluteUri);
        Assert.Equal(PartA.GitLabTokenPage(gitLabPort), gitLab.TokenPage(null).AbsoluteUri);
        Assert.Equal(PartA.GitLabLegacyTokenPage(gitLabPort), gitLab.LegacyTokenPage.AbsoluteUri);
        Assert.Equal(PartA.AzureDevOpsTokenPage(azureDevOpsPort), azureDevOps.TokenPage(PartA.Organization).AbsoluteUri);
        Assert.Equal(PartA.Host(azureDevOpsPort), azureDevOps.Host);
        Assert.Equal(PartA.Host(gitLabPort), gitLab.Host);
    }

    [Fact]
    public void Part_A_s_made_up_values_have_the_report_s_forms()
    {
        var local = AzureDevOpsEndpoints.Local(AzureDevOpsPort);
        Assert.Equal(PartA.Organization, AzureDevOpsProvider.OrganizationOf(PartA.Organization, local));
        Assert.Equal(PartA.OrganizationLower, AzureDevOpsProvider.OrganizationOf(PartA.OrganizationLower, local));
        Assert.Equal(PartA.UnknownOrganization, AzureDevOpsProvider.OrganizationOf(PartA.UnknownOrganization, local));
        Assert.Null(AzureDevOpsProvider.OrganizationOf(PartA.NotAnOrganization, local));
        Assert.Equal(PartA.OrganizationLower, PartA.Organization.ToLowerInvariant());

        Assert.Equal(HostingProvider.GitLab, HostingProviders.TokenFormOf(PartA.UnknownGitLabToken));
        Assert.Null(HostingProviders.TokenFormOf(PartA.UnknownAzureDevOpsToken));
        Assert.StartsWith("azdo_FAKE-", PartA.UnknownAzureDevOpsToken, StringComparison.Ordinal);
        Assert.Null(HostingProviders.TokenFormOf(PartA.PlainValue));
        Assert.Matches("^[A-Za-z0-9-]+$", PartA.PlainValue);
        Assert.DoesNotMatch("^[a-z2-7]{52}$", PartA.PlainValue);
        Assert.NotEqual(84, PartA.PlainValue.Length);
        Assert.Equal(84, PartA.AzureDevOpsForm.Length);
        Assert.Equal("AZDO", PartA.AzureDevOpsForm[76..80]);
        Assert.Equal(HostingProvider.AzureDevOps, HostingProviders.TokenFormOf(PartA.AzureDevOpsForm));
        Assert.Equal(HostingProvider.GitHub, HostingProviders.TokenFormOf(GitHubToken));
        Assert.Equal(HostingProvider.GitLab, HostingProviders.TokenFormOf(GitLabToken));

        Assert.Equal(8, PartA.Tokens.Count);
        Assert.Equal(PartA.Tokens.Count, PartA.Tokens.Select(token => token.Token).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(["connection:fernhill-labs", "areas"], PartA.AzureDevOpsSignInRoutes(PartA.OrganizationLower));
        Assert.Equal(["connection:fernhill-labs", "areas"], PartA.AzureDevOpsSignInRoutes(PartA.Organization));
        Assert.Equal("azure-devops:127.0.0.1:47397:fernhill-labs", PartA.AzureDevOpsKey());
        Assert.Equal("gitlab:127.0.0.1:47398:ada", PartA.GitLabKey());
        Assert.Equal("github:127.0.0.1:47399:octo", PartA.GitHubKey());
    }

    [Fact]
    public void Part_A_s_picture_names_are_unique_and_follow_the_rule()
    {
        Assert.Equal(29, PartA.Pictures.Count);
        Assert.Equal(PartA.Pictures.Count, PartA.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PartA.Pictures, name => Assert.Matches(PictureRule(), name));
        Assert.All(PartA.Pictures, name => Assert.StartsWith("a", name, StringComparison.Ordinal));
        Assert.All(PartA.RealWindowPictures, name => Assert.Contains(name, PartA.Pictures));
        Assert.Equal([PartA.A02ThreeEnd, PartA.A05GitLabTokenMasked, PartA.A06AzureTokenMasked], PartA.RealWindowPictures);
        Assert.Equal("the end", PartA.ThreeAccountsPlace);
    }

    private static HostingFailure Failure(HostingProvider provider, HostingFailureKind kind, string message) =>
        new(kind, message) { Provider = provider };

    [GeneratedRegex("^[abpn]?\\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex PictureRule();

    /// <summary>A handler for providers that only build addresses: any request fails unsent.</summary>
    private sealed class NeverSends : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No request is sent by these tests.");
    }
}
