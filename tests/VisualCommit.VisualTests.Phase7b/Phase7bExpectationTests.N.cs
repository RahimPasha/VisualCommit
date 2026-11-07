using VisualCommit.Core.Hosting;
using VisualCommit.Hosting.GitLab;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;
using Create = VisualCommit.App.ViewModels.Operations.OperationTexts.CreatePullRequestTexts;
using Menus = VisualCommit.App.ViewModels.Operations.OperationTexts.MenuTexts;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// Part N's expectations against the App (docs/test-reports/phase-7b.md, part N's conventions):
/// <c>CreatePullRequestTexts</c>' provider forms and <c>MenuTexts.CreateRequest(p)</c> equal
/// <see cref="PartN"/>'s for GitLab and Azure DevOps and 7a's constants word for word for GitHub;
/// <see cref="PartN.GitLab"/> and <see cref="PartN.AzureDevOps"/> hold the report's rows; the picture
/// names unique and matching design 0.3's rule.
/// </summary>
public sealed partial class Phase7bExpectationTests
{
    [Fact]
    public void Part_N_s_texts_are_the_app_s_per_provider()
    {
        foreach (var p in new[] { GitLab, Azure })
        {
            Assert.Equal(PartN.DialogTitleOf(p), Create.DialogTitleOf(p));
            Assert.Equal(PartN.ReadingOf(p), Create.ReadingOf(p));
            Assert.Equal(PartN.CreatingOf(p), Create.CreatingRequest(p));
            Assert.Equal(PartN.CreateItemOf(p), Menus.CreateRequest(p));
            Assert.Equal(PartN.CancelledOf(p), Create.CancelledOf(p));
            var operation = Create.Operation(p, PartN.Head);
            Assert.Equal(PartN.OperationOf(p, PartN.Head), (operation.Title, operation.RunningText, operation.ErrorTitle, operation.FailureSummary));
            var next = PartN.NextOf(p);
            Assert.Equal(PartN.CreatedOf(p, next, false), Create.Created(p, next, false));
            Assert.Equal(PartN.CreatedOf(p, next, true), Create.Created(p, next, true));
            Assert.Equal(PartN.OpenExistingOf(p, next), Create.OpenExisting(p, next));
            Assert.Equal(PartN.AlreadyOpenOf(p, next), Create.AlreadyOpen(p, next, PartN.Head, PartN.Base));
            Assert.Equal(PartN.PushedFirstWithUpstreamOf(p), Create.PushedFirstWithUpstream(p, "origin/local-work"));
            Assert.Equal(PartN.OpenExistingOf(p, next), Menus.OpenRequest(p, next));
        }

        Assert.Equal("Created draft merge request !24", PartN.CreatedOf(GitLab, 24, true));
        Assert.Equal("Created pull request !14", PartN.CreatedOf(Azure, 14, false));
        Assert.Equal(PartN.GitLabHeadUnknown, Create.HeadUnknown(GitLab, PartN.Head, PartN.GitLabFullName));
        Assert.Equal(PartN.AzureDevOpsBranchesUnknown, Create.BranchesUnknown(Azure, PartN.Head, "main"));
        Assert.Equal(PartN.GitLabRefused, Create.Refused(GitLab, PartN.GitLabFullName, false));
        Assert.Equal(PartN.GitLabRefused + PartN.GitLabRefusedScope, Create.Refused(GitLab, PartN.GitLabFullName, true));
        Assert.Equal(PartN.AzureDevOpsRefused, Create.Refused(Azure, PartN.AzureDevOpsFullName, true));
        Assert.Equal(PartN.QuickActionsNote, Create.QuickActionsNote);
        Assert.Equal(PartN.LocalWorkSentTitle, Create.SentTitle(GitLab, GitLabProvider.TitleFor(PartN.LocalWorkTitle, true)));
        Assert.Equal(PartN.LocalWorkDraftTitle, GitLabProvider.TitleFor(PartN.LocalWorkTitle, true));
    }

    [Fact]
    public void GitHub_s_create_texts_stay_7a_s_word_for_word()
    {
        Assert.Equal("Create pull request", Create.DialogTitleOf(GitHub));
        Assert.Equal(Create.DialogTitle, Create.DialogTitleOf(GitHub));
        Assert.Equal(Create.ReadingGitHub, Create.ReadingOf(GitHub));
        Assert.Equal(Create.CreatingPullRequest, Create.CreatingRequest(GitHub));
        Assert.Equal(Create.Cancelled, Create.CancelledOf(GitHub));
        Assert.Equal(Create.Operation("topic/no-pr"), Create.Operation(GitHub, "topic/no-pr"));
        Assert.Equal(Create.Created(6, true), Create.Created(GitHub, 6, true));
        Assert.Equal(Create.OpenExisting(1), Create.OpenExisting(GitHub, 1));
        Assert.Equal("Create pull request…", Menus.CreateRequest(GitHub));
        Assert.Equal(
            (PartN.From, PartN.Into, PartN.TitlePlaceholder, PartN.DescriptionPlaceholder, PartN.Draft, PartN.Create, PartN.CreatingButton, PartN.ChooseBaseTitle, PartN.OpenInBrowser),
            (Create.From, Create.Into, Create.TitlePlaceholder, Create.DescriptionPlaceholder, Create.Draft, Create.Create, Create.Creating, Create.ChooseBaseTitle, Create.OpenInBrowser));
    }

    [Fact]
    public void Part_N_s_records_for_WP_R_are_the_report_s_rows()
    {
        foreach (var (record, p) in new[] { (PartN.GitLab, GitLab), (PartN.AzureDevOps, Azure) })
        {
            Assert.Equal(p, record.Kind);
            Assert.Same(record, PartN.Of(p));
            Assert.Equal(["Local branches", "topic", "no-request"], record.BranchRowPath);
            Assert.Equal((PartN.CreateItemOf(p), PartN.DialogTitleOf(p)), (record.MenuItem, record.FormTitle));
            Assert.Equal(("topic/no-request", "origin/main", "Try an idea"), (record.Head, record.Base, record.Title));
            Assert.Equal((201, false, 4), (record.CreateStatus, record.PushedFirst, record.CountAfterCreate));
            Assert.Equal(PartN.OperationOf(p, record.Head).Title, record.OperationTitle);
            Assert.Equal(PartN.CreatedOf(p, PartN.NextOf(p), false), record.StatusText);
            Assert.Equal($"{PartN.NumberOf(PartN.NextOf(p))} Try an idea", record.NewRowName);
            Assert.Contains(record.FormPicture, PartN.RealWindowPictures);
            Assert.Contains(record.CreatedPicture, PartN.RealWindowPictures);
        }

        Assert.Equal(("web-app", "northfield/platform/web-app", "create:northfield/platform/web-app"), (PartN.GitLab.Repository, PartN.GitLab.RepositoryText, PartN.GitLab.CreateRoute));
        Assert.Equal(("mobile-app", "Fernhill-Labs/Mobile App/mobile-app", "create:fernhill-labs/mobile app/mobile-app"), (PartN.AzureDevOps.Repository, PartN.AzureDevOps.RepositoryText, PartN.AzureDevOps.CreateRoute));
        Assert.Equal(["description", "source_branch", "target_branch", "title"], PartN.GitLab.Body.Select(member => member.Member).Order(StringComparer.Ordinal));
        Assert.Equal(["description", "isDraft", "sourceRefName", "targetRefName", "title"], PartN.AzureDevOps.Body.Select(member => member.Member).Order(StringComparer.Ordinal));
        Assert.Equal(["draft", "target_project_id", "allow_collaboration"], PartN.GitLab.AbsentMembers);
        Assert.Equal(["forkSource"], PartN.AzureDevOps.AbsentMembers);
        Assert.Equal(78, PartN.LongFullName.Length);
        Assert.Equal($"{Organization}/{Project}/{LongRepository}", PartN.LongFullName);
    }

    [Fact]
    public void Part_N_s_menus_have_the_report_s_counts_and_heights()
    {
        foreach (var p in new[] { GitLab, Azure })
        {
            foreach (var (lines, items, separators, height) in new[]
            {
                (PartN.LocalBranchMenuOf(p), 20, 5, PartN.LocalBranchMenuHeight),
                (PartN.RemoteBranchMenuOf(p), 13, 5, PartN.RemoteBranchMenuHeight),
                (PartN.LocalWorkMenuOf(p), 17, 5, PartN.LocalWorkMenuHeight),
                (PartN.DropMenuOf(p), 6, 3, PartN.DropMenuHeight),
            })
            {
                Assert.Equal(separators, lines.Count(line => line == "—"));
                Assert.Equal(items, lines.Length - 1 - separators);
                Assert.Equal(height, MenuHeight(items, separators), 2.0);
                Assert.Single(lines, line => line == PartN.CreateItemOf(p));
            }
        }
    }

    [Fact]
    public void Part_N_s_picture_names_are_unique_and_follow_the_rule()
    {
        Assert.Equal(22, PartN.Pictures.Count);
        Assert.Equal(PartN.Pictures.Count, PartN.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PartN.Pictures, name => Assert.Matches(PictureRule(), name));
        Assert.All(PartN.Pictures, name => Assert.StartsWith("n", name, StringComparison.Ordinal));
        Assert.Equal([PartN.N02a, PartN.N06a, PartN.N02b, PartN.N06b], PartN.RealWindowPictures);
        Assert.All(PartN.RealWindowPictures, name => Assert.Contains(name, PartN.Pictures));
        Assert.Empty(PartN.Pictures.Intersect(PartP.Pictures, StringComparer.Ordinal));
    }
}
