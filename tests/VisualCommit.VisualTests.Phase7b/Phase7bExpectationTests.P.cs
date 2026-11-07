using System.Globalization;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;
using Menus = VisualCommit.App.ViewModels.Operations.OperationTexts.MenuTexts;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// Part P's expectations against the App (docs/test-reports/phase-7b.md, part P's conventions): the
/// provider forms of <c>OperationTexts</c>' request texts and <c>MenuTexts</c>, and <c>HostingTexts</c>'
/// <c>RequestsTitle</c>, <c>Number</c> and <c>SignInToItemOf</c>, equal <see cref="PartP"/>'s for GitLab
/// and Azure DevOps and 7a's constants word for word for GitHub; the picture names unique and matching
/// design 0.3's rule.
/// </summary>
public sealed partial class Phase7bExpectationTests
{
    private const HostingProvider GitLab = HostingProvider.GitLab;
    private const HostingProvider Azure = HostingProvider.AzureDevOps;
    private const HostingProvider GitHub = HostingProvider.GitHub;

    [Fact]
    public void Part_P_s_section_texts_are_the_app_s_per_provider()
    {
        Assert.Equal((PartP.GitLabTitle, PartP.AzureDevOpsTitle, PartP.GitHubTitle), (HostingTexts.RequestsTitle(GitLab), HostingTexts.RequestsTitle(Azure), HostingTexts.RequestsTitle(GitHub)));
        Assert.Equal((PartP.GitLabSignInToSee, PartP.AzureDevOpsSignInToSee), (Texts.SignInToSee(GitLab), Texts.SignInToSee(Azure)));
        Assert.Equal((PartP.GitLabLoading, PartP.AzureDevOpsLoading), (Texts.LoadingRequests(GitLab), Texts.LoadingRequests(Azure)));
        Assert.Equal((PartP.GitLabNone, PartP.AzureDevOpsNone), (Texts.NoOpenRequests(GitLab), Texts.NoOpenRequests(Azure)));
        Assert.Equal((PartP.GitLabMore, PartP.AzureDevOpsMore), (Texts.MoreRequests(GitLab), Texts.MoreRequests(Azure)));
        Assert.Equal(("!21", "!11", "#1"), (HostingTexts.Number(GitLab, 21), HostingTexts.Number(Azure, 11), HostingTexts.Number(GitHub, 1)));
        Assert.Equal(PartP.Header(GitLab, 21, "Add the search box"), Texts.RequestHeader(GitLab, 21, "Add the search box"));
        Assert.Equal("!11 Add the sign-in page", Texts.RequestHeader(Azure, 11, "Add the sign-in page"));
        Assert.Equal("Merge request !21: Add the search box", Texts.RequestOfBranch(GitLab, 21, "Add the search box"));
        Assert.Equal(PartP.OfBranch(Azure, 11, "Add the sign-in page"), Texts.RequestOfBranch(Azure, 11, "Add the sign-in page"));
        foreach (var row in PartP.GitLabRows)
        {
            Assert.Equal(row.ToolTip, Texts.RequestToolTip(GitLab, row.Number, row.Title, row.Author, row.HeadLabel, "main", row.Draft));
        }

        foreach (var row in PartP.AzureDevOpsRows)
        {
            Assert.Equal(row.ToolTip, Texts.RequestToolTip(Azure, row.Number, row.Title, row.Author, row.HeadLabel, "main", row.Draft));
        }

        var fetch = Texts.FetchRequest(GitLab, 23, "origin");
        Assert.Equal(PartP.FetchMergeRequest23, (fetch.Title, fetch.RunningText, fetch.ErrorTitle, fetch.FailureSummary));
        Assert.Equal(PartP.FetchedMergeRequest23, Texts.FetchedRequest(GitLab, 23, "fix-typo"));
        Assert.Equal(PartP.OtherOrganizationRow, Texts.OtherOrganization(OtherOrganization, Organization));
        Assert.Equal(PartP.OtherOrganizationToolTip, Texts.OtherOrganizationToolTip(OtherOrganization, Organization));
        Assert.Equal((PartP.Draft, PartP.OpenInBrowser, PartP.CopyLink), (Texts.PullRequestDraft, Menus.OpenInBrowser, Menus.CopyLink));
    }

    [Fact]
    public void Part_P_s_menu_texts_are_the_app_s_per_provider()
    {
        Assert.Equal((PartP.GitLabRefresh, PartP.AzureDevOpsRefresh), (Menus.RefreshRequests(GitLab), Menus.RefreshRequests(Azure)));
        Assert.Equal((PartP.GitLabOpenInBrowser, PartP.AzureDevOpsOpenInBrowser), (Menus.OpenRequestsInBrowser(GitLab), Menus.OpenRequestsInBrowser(Azure)));
        Assert.Equal((PartP.GitLabCreate, PartP.AzureDevOpsCreate), (Menus.CreateRequest(GitLab), Menus.CreateRequest(Azure)));
        Assert.Equal((PartP.OpenRequest(GitLab, 21), PartP.OpenRequest(Azure, 11)), (Menus.OpenRequest(GitLab, 21), Menus.OpenRequest(Azure, 11)));
        Assert.Equal(("Open merge request !21", "Open pull request !11"), (PartP.OpenRequest(GitLab, 21), PartP.OpenRequest(Azure, 11)));
        Assert.Equal((PartP.GitLabLoadingReason, PartP.AzureDevOpsLoadingReason), (Menus.RequestsLoading(GitLab), Menus.RequestsLoading(Azure)));
        Assert.Equal(PartP.AzureDevOpsForkReason, Menus.AddRemoteFor(Azure, $"{Organization}/{Project}/mobile-app-fork"));
        Assert.Equal(PartP.AzureDevOpsHeadNotNamed, Menus.HeadNotNamed(Azure));
        Assert.Equal(PartP.NotFetchedBy("origin", "docs/guide"), Menus.NotFetchedBy("origin", "docs/guide"));
        Assert.Equal(
            (PartP.GitLabSignInItem, PartP.AzureDevOpsSignInItem, PartP.GitHubSignInItem),
            (HostingTexts.SignInToItemOf(GitLab), HostingTexts.SignInToItemOf(Azure), HostingTexts.SignInToItemOf(GitHub)));
    }

    [Fact]
    public void Part_P_s_github_forms_are_7a_s_constants_word_for_word()
    {
        Assert.Equal(PartP.GitHubSignInToSee, Texts.SignInToSeePullRequests);
        Assert.Equal(Texts.SignInToSeePullRequests, Texts.SignInToSee(GitHub));
        Assert.Equal(Texts.LoadingPullRequests, Texts.LoadingRequests(GitHub));
        Assert.Equal(Texts.NoOpenPullRequests, Texts.NoOpenRequests(GitHub));
        Assert.Equal(Texts.MorePullRequests, Texts.MoreRequests(GitHub));
        Assert.Equal((PartP.AzureDevOpsLoading, PartP.AzureDevOpsNone, PartP.AzureDevOpsMore), (Texts.LoadingPullRequests, Texts.NoOpenPullRequests, Texts.MorePullRequests));
        Assert.Equal(Texts.PullRequestHeader(5, "Fix a typo"), Texts.RequestHeader(GitHub, 5, "Fix a typo"));
        Assert.Equal(PartP.Header(GitHub, 5, "Fix a typo"), Texts.RequestHeader(GitHub, 5, "Fix a typo"));
        Assert.Equal(Texts.PullRequestOfBranch(1, "Add the login form"), Texts.RequestOfBranch(GitHub, 1, "Add the login form"));
        Assert.Equal(
            Texts.PullRequestToolTip(2, "Update the readme", "octo", "docs/readme", "main", true),
            Texts.RequestToolTip(GitHub, 2, "Update the readme", "octo", "docs/readme", "main", true));
        Assert.Equal(Texts.FetchPullRequest(5, "origin"), Texts.FetchRequest(GitHub, 5, "origin"));
        Assert.Equal(Texts.FetchedPullRequest(5, "fix-typo"), Texts.FetchedRequest(GitHub, 5, "fix-typo"));
        Assert.Equal(Menus.RefreshPullRequests, Menus.RefreshRequests(GitHub));
        Assert.Equal(Menus.OpenPullRequestsInBrowser, Menus.OpenRequestsInBrowser(GitHub));
        Assert.Equal(PartP.GitHubCreate, Menus.CreatePullRequest);
        Assert.Equal(Menus.CreatePullRequest, Menus.CreateRequest(GitHub));
        Assert.Equal(Menus.OpenPullRequest(1), Menus.OpenRequest(GitHub, 1));
        Assert.Equal(Menus.PullRequestsLoading, Menus.RequestsLoading(GitHub));
        Assert.Equal(Menus.AddRemoteFor("ada/hello"), Menus.AddRemoteFor(GitHub, "ada/hello"));
        Assert.Equal(HostingTexts.SignInToGitHubItem, HostingTexts.SignInToItemOf(GitHub));
        Assert.Equal((PartP.AzureDevOpsRefresh, PartP.AzureDevOpsOpenInBrowser, PartP.AzureDevOpsLoadingReason), (Menus.RefreshPullRequests, Menus.OpenPullRequestsInBrowser, Menus.PullRequestsLoading));
    }

    [Fact]
    public void Part_P_s_scenario_facts_are_the_report_s()
    {
        Assert.Equal([23, 22, 21], PartP.GitLabRows.Select(row => row.Number));
        Assert.Equal([13, 12, 11], PartP.AzureDevOpsRows.Select(row => row.Number));
        Assert.All(PartP.GitLabRows, row => Assert.Equal(PartP.Header(GitLab, row.Number, row.Title), row.Name));
        Assert.All(PartP.AzureDevOpsRows, row => Assert.Equal(PartP.Header(Azure, row.Number, row.Title), row.Name));
        Assert.Equal([false, true, false], PartP.GitLabRows.Select(row => row.Draft));
        Assert.Equal([false, true, false], PartP.AzureDevOpsRows.Select(row => row.Draft));
        Assert.Equal(("!21 Add the search box", "!11 Add the sign-in page"), (PartP.MenuRowOf(GitLab).Name, PartP.MenuRowOf(Azure).Name));
        Assert.Equal((WebApp, MobileApp), (PartP.RepositoryOf(GitLab), PartP.RepositoryOf(Azure)));
        Assert.Equal(PartP.RequestBranchMenu(GitLab).Length, PartP.RequestBranchMenu(Azure).Length);
        Assert.Equal(20, PartP.RequestBranchMenu(GitLab).Count(line => line != "—") - 1);
        Assert.Equal(PartP.RequestBranchMenuHeight, MenuHeight(20, 5));
        Assert.Equal(PartP.SectionMenuHeight, MenuHeight(2, 0));
        Assert.Equal(PartP.RequestMenuHeight, MenuHeight(3, 1));
        Assert.Equal(PartP.OtherOrganizationMenuHeight, MenuHeight(1, 0));
        Assert.Equal(PartP.LastAdded - PartP.FirstAdded + 1, 298);
        Assert.Equal(("!21", "!22", "!11", "!12"), (PartP.GitLabRequestBadge.Text, PartP.GitLabDraftBadge.Text, PartP.AzureDevOpsRequestBadge.Text, PartP.AzureDevOpsDraftBadge.Text));
        Assert.Equal(
            "http://127.0.0.1:47398/northfield/platform/web-app/-/merge_requests/21",
            WithPorts(PartP.GitLabRequestUrl(21), AzureDevOpsPort, GitLabPort, GitHubPort));
        Assert.Equal("http://127.0.0.1:1/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/11", WithPorts(PartP.AzureDevOpsRequestUrl(11), 1, 2, 3));
        Assert.Equal("http://127.0.0.1:1/harbor-co/Docks/_git/manifest/pullrequests", WithPorts(PartP.ManifestRequestsPage, 1, 2, 3));
        Assert.Equal($"gitlab:127.0.0.1:{GitLabPort}:{AdaLogin}", PartP.GitLabKey);
        Assert.Equal($"azure-devops:127.0.0.1:{AzureDevOpsPort}:{Organization.ToLowerInvariant()}", PartP.AzureDevOpsKey);
        Assert.Equal(3, PartP.Tokens.Count);
        Assert.Equal(PartP.GitLabForbiddenText, HostingTexts.Describe(new HostingFailure(HostingFailureKind.Forbidden, PartP.GitLabForbiddenMessage, PartP.GitLabForbiddenDetail) { Provider = GitLab }, AdaLogin));
        Assert.Equal(PartP.AzureDevOpsServerErrorText, HostingTexts.Describe(new HostingFailure(HostingFailureKind.ServerError, PartP.AzureDevOpsServerErrorStart + "x.") { Provider = Azure }, Organization));
    }

    [Fact]
    public void Part_P_s_label_table_keeps_the_report_s_rule()
    {
        // Open point 26's record against the report's rule: one badge per request, on the first label of
        // the request's local row and of the draft's remote row, none elsewhere; a cut name a prefix of
        // its full name ending "…"; a +N folding N names; every label inside the column.
        foreach (var (provider, request, draft, requestLabel, draftLabel) in new[]
        {
            (GitLab, PartP.GitLabRequestBadge, PartP.GitLabDraftBadge, PartP.GitLabRequestLabel, PartP.GitLabDraftLabel),
            (Azure, PartP.AzureDevOpsRequestBadge, PartP.AzureDevOpsDraftBadge, PartP.AzureDevOpsRequestLabel, PartP.AzureDevOpsDraftLabel),
        })
        {
            var table = PartP.LabelsOf(provider);
            Assert.Equal(5, table.Count);
            Assert.Equal((requestLabel, request.Number), (table[PartP.RequestRow][0].FullText, table[PartP.RequestRow][0].Badge));
            Assert.Equal((draftLabel, draft.Number), (table[PartP.DraftRow][0].FullText, table[PartP.DraftRow][0].Badge));
            Assert.Equal(2, table.SelectMany(row => row).Count(label => label.Badge != 0));
            Assert.All(table.SelectMany(row => row), label =>
            {
                Assert.True(label.Right <= PartP.BranchColumnWidth, $"{label.Text} ends past the column.");
                if (label.Text.StartsWith('+'))
                {
                    Assert.Equal((int.Parse(label.Text[1..], CultureInfo.InvariantCulture), (string?)null), (label.FullText.Split('\n').Length, label.RefName));
                }
                else if (label.Text != label.FullText)
                {
                    Assert.EndsWith("…", label.Text, StringComparison.Ordinal);
                    Assert.StartsWith(label.Text[..^1], label.FullText, StringComparison.Ordinal);
                }
            });
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => PartP.LabelsOf(GitHub));
    }

    [Fact]
    public void Part_P_s_picture_names_are_unique_and_follow_the_rule()
    {
        Assert.Equal(22, PartP.Pictures.Count);
        Assert.Equal(PartP.Pictures.Count, PartP.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PartP.Pictures, name => Assert.Matches(PictureRule(), name));
        Assert.All(PartP.Pictures, name => Assert.StartsWith("p", name, StringComparison.Ordinal));
        Assert.Equal([PartP.P01a, PartP.P01c, PartP.P10a, PartP.P10c], PartP.RealWindowPictures);
        Assert.All(PartP.RealWindowPictures, name => Assert.Contains(name, PartP.Pictures));
        Assert.Equal((PartP.P01a, PartP.P01c), (PartP.RowsPictureOf(GitLab), PartP.RowsPictureOf(Azure)));
        Assert.Equal((PartP.P10a, PartP.P10c), (PartP.SectionMenuPictureOf(GitLab), PartP.SectionMenuPictureOf(Azure)));
        Assert.All(PartP.Pictures, name => Assert.True(name.Contains("-gitlab-", StringComparison.Ordinal) || name.Contains("-azure-", StringComparison.Ordinal), name + " names no provider."));
        Assert.Empty(PartP.Pictures.Intersect(PartA.Pictures, StringComparer.Ordinal));
        Assert.Empty(PartP.Pictures.Intersect(PartB.Pictures, StringComparer.Ordinal));
    }
}
