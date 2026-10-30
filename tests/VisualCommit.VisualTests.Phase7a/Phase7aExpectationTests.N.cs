using VisualCommit.Testing;
using Xunit;
using N = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartN;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.CreatePullRequestTexts;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>Part N's expectations against the app and the scenario (design 12.8): the texts are the app's, the picture names follow the rule, and the pin of <see cref="N.PrepareAsync"/>.</summary>
public sealed class Phase7aExpectationTestsN
{
    [Fact]
    public void The_texts_are_the_app_s()
    {
        Assert.Equal(
            [N.DialogTitle, N.From, N.Into, N.ReadingGitHub, N.TitlePlaceholder, N.DescriptionPlaceholder, N.Draft, N.Create, N.Creating, N.ChooseBaseTitle, N.ChooseBase, N.EnterTitle, N.OpenInBrowser, N.CreatingPullRequest, N.Cancelled],
            [Texts.DialogTitle, Texts.From, Texts.Into, Texts.ReadingGitHub, Texts.TitlePlaceholder, Texts.DescriptionPlaceholder, Texts.Draft, Texts.Create, Texts.Creating, Texts.ChooseBaseTitle, Texts.ChooseBase, Texts.EnterTitle, Texts.OpenInBrowser, Texts.CreatingPullRequest, Texts.Cancelled]);
        var operation = Texts.Operation(N.Topic);
        Assert.Equal((N.OperationTitle(N.Topic), N.CreatingPullRequest, N.OperationErrorTitle, N.OperationSummary), (operation.Title, operation.RunningText, operation.ErrorTitle, operation.FailureSummary));
        Assert.Equal(N.OpenExisting(6), Texts.OpenExisting(6));
        Assert.Equal((N.Created(6, false), N.Created(6, true)), (Texts.Created(6, false), Texts.Created(6, true)));
        Assert.Equal(N.PushedFirstWithUpstream("origin/local-work"), Texts.PushedFirstWithUpstream("origin/local-work"));
        Assert.Equal(N.SameBranch(N.Main), Texts.SameBranch(N.Main));
        Assert.Equal(N.AlreadyOpen(6, N.Topic, N.OriginMain), Texts.AlreadyOpen(6, N.Topic, N.OriginMain));
        Assert.Equal(N.NoCommits(N.OriginTopic, N.Main), Texts.NoCommits(N.OriginTopic, N.Main));
        Assert.Equal(N.RefusedWithPermission, Texts.Refused(N.Repository, namesPermission: true));
        Assert.Equal(N.Pushing(N.LocalWork, "origin/local-work"), VisualCommit.App.ViewModels.Operations.OperationTexts.Push(N.LocalWork, "origin/local-work").RunningText);
        Assert.Equal((N.Choose, N.PickConfirm), (VisualCommit.App.ViewModels.Operations.OperationTexts.SettingsTexts.Choose, VisualCommit.App.ViewModels.Operations.OperationTexts.SettingsTexts.PickConfirm));
        Assert.Equal(N.StopGit, VisualCommit.App.ViewModels.Operations.OperationStatusViewModel.StopGitText);
    }

    [Fact]
    public void The_picture_names_are_unique_and_follow_the_rule()
    {
        Assert.Equal(N.Pictures.Length, N.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(N.Pictures, name => Assert.Matches(@"^[abpgn]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$", name));
        Assert.All(N.Pictures, name => Assert.StartsWith("n", name, StringComparison.Ordinal));
        Assert.All(N.RealWindowPictures, name => Assert.Contains(name, N.Pictures));
        Assert.Equal(20, N.Pictures.Length);
    }

    [Fact]
    public async Task PrepareAsync_makes_topic_no_pr_track_origin_s_branch_of_its_name()
    {
        using var copy = await Scenarios.HostingAsync();

        await N.PrepareAsync(copy);

        Assert.Equal("origin", (await copy.GitAsync("config", "branch.topic/no-pr.remote")).StandardOutput.Trim());
        Assert.Equal("refs/heads/topic/no-pr", (await copy.GitAsync("config", "branch.topic/no-pr.merge")).StandardOutput.Trim());
        Assert.Equal(
            (await copy.GitAsync("rev-parse", "refs/remotes/origin/topic/no-pr")).StandardOutput.Trim(),
            (await copy.GitAsync("rev-parse", N.TopicRef)).StandardOutput.Trim());
        Assert.Equal(N.TopicTitle, (await copy.GitAsync("log", "-1", "--format=%s", N.TopicRef)).StandardOutput.Trim());
    }
}
