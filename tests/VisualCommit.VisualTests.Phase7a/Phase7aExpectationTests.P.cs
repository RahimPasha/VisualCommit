using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.App.ViewModels.Panels;
using Xunit;
using Menu = VisualCommit.App.ViewModels.Operations.OperationTexts.MenuTexts;
using P = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartP;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// Part P's expectations against the App (docs/test-reports/phase-7a.md, part P's conventions):
/// design 10.2's texts in <c>OperationTexts</c> and <c>MenuTexts</c> equal <see cref="P"/>'s, and the
/// picture names are unique and match design 0.3's rule.
/// </summary>
public sealed class Phase7aExpectationTestsP
{
    [Fact]
    public void The_texts_are_the_app_s()
    {
        Assert.Equal(P.SignInToSeePullRequests, Texts.SignInToSeePullRequests);
        Assert.Equal(P.LoadingPullRequests, Texts.LoadingPullRequests);
        Assert.Equal(P.NoOpenPullRequests, Texts.NoOpenPullRequests);
        Assert.Equal(P.MorePullRequests, Texts.MorePullRequests);
        Assert.Equal(P.Draft, Texts.PullRequestDraft);
        Assert.Equal(P.PullRequestHeader(1, "Add the login form"), Texts.PullRequestHeader(1, "Add the login form"));
        Assert.Equal(P.PullRequestOfBranch(1, "Add the login form"), Texts.PullRequestOfBranch(1, "Add the login form"));
        Assert.Equal(P.FetchPullRequestTitle(5), Texts.FetchPullRequest(5, "origin").Title);
        Assert.Equal("Fetching pull request #5 from origin…", Texts.FetchPullRequest(5, "origin").RunningText);
        Assert.Equal(P.FetchedPullRequest(5, "fix-typo"), Texts.FetchedPullRequest(5, "fix-typo"));
        Assert.Equal(P.CheckedOut("fix-typo"), Texts.CheckedOut("fix-typo"));
        Assert.Equal("Another fetch changed FETCH_HEAD; try again.", Texts.FetchHeadChanged);
        Assert.Equal(P.OpenInBrowser, Menu.OpenInBrowser);
        Assert.Equal(P.CopyLink, Menu.CopyLink);
        Assert.Equal(P.RefreshPullRequests, Menu.RefreshPullRequests);
        Assert.Equal(P.OpenPullRequestsInBrowser, Menu.OpenPullRequestsInBrowser);
        Assert.Equal(P.CreatePullRequest, Menu.CreatePullRequest);
        Assert.Equal(P.OpenPullRequest(1), Menu.OpenPullRequest(1));
        Assert.Equal(P.PullRequestsLoading, Menu.PullRequestsLoading);
        Assert.Equal(P.CheckOutName("ada:fix-typo"), Menu.CheckOutName("ada:fix-typo"));
        Assert.Equal(P.AlreadyCheckedOut("fix-typo"), Menu.AlreadyCheckedOut("fix-typo"));
        Assert.Equal("Add a remote for acme/tool to check out this pull request", Menu.AddRemoteFor("acme/tool"));
        Assert.Equal(P.SignInToGitHubItem, HostingTexts.SignInToGitHubItem);
        Assert.Equal(P.Title, LeftPanelViewModel.PullRequestsTitle);
    }

    [Fact]
    public void The_picture_names_are_unique_and_match_the_rule()
    {
        Assert.Equal(P.Pictures.Count, P.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(P.Pictures, name => Assert.Matches(@"^[abpgn]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$", name));
        Assert.All(P.RealWindowPictures, name => Assert.Contains(name, P.Pictures));
        Assert.Equal(21, P.Pictures.Count);
    }
}
