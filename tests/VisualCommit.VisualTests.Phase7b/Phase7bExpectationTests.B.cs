using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.CloneTexts;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// Part B's expectations against the App (docs/test-reports/phase-7b.md, part B's conventions):
/// <c>CloneTexts</c>' <c>…Of(p)</c> members and <c>FromAnAccount</c>, and the <c>HostingTexts.Describe</c>
/// texts pictured, equal <see cref="PartB"/>'s; the scenario's URLs and rows are the report's; the
/// picture names are unique and match design 0.3's rule.
/// </summary>
public sealed partial class Phase7bExpectationTests
{
    [Fact]
    public void Part_B_s_texts_are_the_app_s()
    {
        Assert.Equal(PartB.FromGitHubButton, Texts.FromGitHubButton);
        Assert.Equal(PartB.FromGitHubButton, Texts.FromButtonOf(HostingProvider.GitHub));
        Assert.Equal(PartB.GitLabButton, Texts.FromButtonOf(HostingProvider.GitLab));
        Assert.Equal(PartB.AzureDevOpsButton, Texts.FromButtonOf(HostingProvider.AzureDevOps));
        Assert.Equal(PartB.FromAnAccount, Texts.FromAnAccount);
        Assert.Equal(PartB.GitHubTitle, Texts.DialogTitle);
        Assert.Equal(PartB.GitHubTitle, Texts.DialogTitleOf(HostingProvider.GitHub));
        Assert.Equal(PartB.GitLabTitle, Texts.DialogTitleOf(HostingProvider.GitLab));
        Assert.Equal(PartB.AzureDevOpsTitle, Texts.DialogTitleOf(HostingProvider.AzureDevOps));
        Assert.Equal(PartB.GitHubCommand, Texts.PaletteCommand);
        Assert.Equal(PartB.GitHubCommand, Texts.PaletteCommandOf(HostingProvider.GitHub));
        Assert.Equal(PartB.GitLabCommand, Texts.PaletteCommandOf(HostingProvider.GitLab));
        Assert.Equal(PartB.AzureDevOpsCommand, Texts.PaletteCommandOf(HostingProvider.AzureDevOps));
        Assert.Equal(PartB.GitHubMoreExist, Texts.MoreExist);
        Assert.Equal(PartB.GitHubMoreExist, Texts.MoreExistOf(HostingProvider.GitHub));
        Assert.Equal(PartB.GitLabMoreExist, Texts.MoreExistOf(HostingProvider.GitLab));
        Assert.Null(Texts.MoreExistOf(HostingProvider.AzureDevOps));
        Assert.Equal((PartB.Loading, PartB.Empty, PartB.Unexpected), (Texts.Loading, Texts.Empty, Texts.Unexpected));
        Assert.Equal((PartB.Choose, PartB.Cancel, PartB.Filter), (Texts.Choose, Texts.Cancel, Texts.Filter));
        Assert.Equal(PartB.PrivateFork, Texts.Private + Texts.DetailSeparator + Texts.Fork);
        Assert.Equal(
            [PartB.GitHubName, PartB.GitLabName, PartB.AzureDevOpsName],
            HostingProviders.InAccountsOrder.Select(HostingTexts.Name));
        Assert.Equal(PartB.GitLabListingFailedStart, Texts.ListingFailedOf(HostingProvider.GitLab) + ":");
        Assert.Equal(PartB.AzureDevOpsListingFailedStart, Texts.ListingFailedOf(HostingProvider.AzureDevOps) + ":");
        Assert.Equal(PartB.GitHubListingFailedStart, Texts.ListingFailedOf(HostingProvider.GitHub) + ":");
        Assert.StartsWith(PartB.GitLabListingFailedStart, PartB.GitLabListingFailed, StringComparison.Ordinal);
        Assert.StartsWith(PartB.AzureDevOpsListingFailedStart, PartB.AzureDevOpsListingFailed, StringComparison.Ordinal);
    }

    [Fact]
    public void Part_B_s_pictured_failures_are_describe_s()
    {
        Assert.Equal(
            PartB.GitLabServerError,
            HostingTexts.Describe(new HostingFailure(HostingFailureKind.ServerError, "500 Internal Server Error") { Provider = HostingProvider.GitLab }, AdaLogin));
        Assert.Equal(
            PartB.AzureDevOpsRefused,
            HostingTexts.Describe(new HostingFailure(HostingFailureKind.BadCredentials, "Unauthorized") { Provider = HostingProvider.AzureDevOps }, Organization));
    }

    [Fact]
    public void Part_B_s_scenario_facts_are_the_report_s()
    {
        Assert.Equal(WebAppUrl, PartB.GitLabCloneUrl);
        Assert.Equal(MobileAppUrl, PartB.AzureDevOpsCloneUrl);
        var azureDevOps = PartB.AzureDevOpsCloneUrl;
        Assert.Equal($"http://{Organization}@127.0.0.1:{AzureDevOpsPort}/{Organization}/Mobile%20App/_git/mobile-app", azureDevOps);
        var binding = PartB.AzureDevOpsBinding;
        Assert.Equal(azureDevOps.Replace(Organization + "@", string.Empty, StringComparison.Ordinal), binding);
        Assert.Contains(PartB.GitLabChosen, PartB.GitLabRows.Select(row => row.FullName));
        Assert.Contains(PartB.AzureDevOpsChosen, PartB.AzureDevOpsRows.Select(row => row.FullName));
        Assert.Equal(PartB.GitLabLongName, PartB.GitLabRows[0].FullName);
        Assert.Equal(PartB.AzureDevOpsLongName, PartB.AzureDevOpsRows[0].FullName);
        Assert.EndsWith(PartB.GitLabLongEnd, PartB.GitLabLongName, StringComparison.Ordinal);
        Assert.EndsWith(PartB.AzureDevOpsLongEnd, PartB.AzureDevOpsLongName, StringComparison.Ordinal);
        Assert.Equal($"{WebAppPath}", PartB.GitLabChosen);
        Assert.Equal($"{Organization}/{Project}/{MobileApp}", PartB.AzureDevOpsChosen);
        Assert.Equal($"{Organization}/{Project}/{LongRepository}", PartB.AzureDevOpsLongName);
        Assert.Equal(LongProject, PartB.GitLabLongName);
        Assert.Equal(WebAppFork, PartB.GitLabRows[1].FullName);
        Assert.Equal([PartB.CloneRepository, PartB.GitHubCommand, PartB.GitLabCommand, PartB.AzureDevOpsCommand, PartB.InitRepository], PartB.PaletteRun);
        Assert.All(PartB.PaletteRows, row => Assert.StartsWith(PartB.PaletteQuery, row, StringComparison.Ordinal));
        Assert.Equal(4, PartB.Tokens.Count);
        foreach (var provider in new[] { HostingProvider.GitLab, HostingProvider.AzureDevOps })
        {
            Assert.Contains(PartB.ChosenOf(provider), PartB.RowsOf(provider).Select(row => row.FullName));
            Assert.Equal(WebAppUrlOrMobile(provider), PartB.CloneUrlOf(provider));
            Assert.Contains(PartB.ListedPictureOf(provider), PartB.RealWindowPictures);
            Assert.Equal(Texts.FromButtonOf(provider), PartB.ButtonOf(provider));
            Assert.Equal(Texts.DialogTitleOf(provider), PartB.TitleOf(provider));
        }
    }

    [Fact]
    public void Part_B_s_picture_names_are_unique_and_follow_the_rule()
    {
        Assert.Equal(20, PartB.Pictures.Count);
        Assert.Equal(PartB.Pictures.Count, PartB.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PartB.Pictures, name => Assert.Matches(PictureRule(), name));
        Assert.All(PartB.Pictures, name => Assert.StartsWith("b", name, StringComparison.Ordinal));
        Assert.Equal([PartB.B03GitLab, PartB.B03Azure], PartB.RealWindowPictures);
        Assert.All(PartB.RealWindowPictures, name => Assert.Contains(name, PartB.Pictures));
        Assert.Empty(PartB.Pictures.Intersect(PartA.Pictures, StringComparer.Ordinal));
    }

    private static string WebAppUrlOrMobile(HostingProvider provider) => provider == HostingProvider.AzureDevOps ? MobileAppUrl : WebAppUrl;
}
