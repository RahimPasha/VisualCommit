using VisualCommit.Core.Hosting;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="PullRequestRefs"/> (docs/phase-7b-design.md 1.2, 8.4; ruling 14): 7a's GitHub
/// vectors for the push guard and the fetch's head, moved with the rule from <c>GitHubUrls</c>,
/// GitLab's and Azure DevOps' rules (8.7), and each provider's head ref.
/// </summary>
public sealed class PullRequestRefsTests
{
    [Theory]
    [InlineData("refs/pull/1/head", true)]
    [InlineData("refs/pull/12/merge", true)]
    [InlineData("refs/pull/007/head", true)]
    [InlineData("refs/pull/fix-typo/head", false)]
    [InlineData("refs/pull/fix/typo/head", false)]
    [InlineData("refs/pull/1a/head", false)]
    [InlineData("refs/pull/x/merge", false)]
    [InlineData("refs/pull//head", false)]
    [InlineData("refs/pull/١/head", false)]
    [InlineData("refs/pull/x/files", true)]
    [InlineData("refs/pull/x", true)]
    [InlineData("refs/pull/head", true)]
    [InlineData("refs/pulls/x/head", true)]
    [InlineData("refs/heads/pr/fix-typo", true)]
    [InlineData("refs/heads/pr/12", true)]
    [InlineData("pr/fix-typo", true)]
    [InlineData("", true)]
    [InlineData("refs/merge-requests/foo/head", true)]
    public void On_GitHub_only_a_pull_ref_whose_number_is_not_all_digits_cannot_exist(string remoteRef, bool canExist) =>
        Assert.Equal(canExist, PullRequestRefs.SourceCanExist(HostingProvider.GitHub, remoteRef));

    [Theory]
    [InlineData("refs/merge-requests/foo/head", false)]
    [InlineData("refs/merge-requests/a/b/head", false)]
    [InlineData("refs/merge-requests/x/train", false)]
    [InlineData("refs/merge-requests//head", false)]
    [InlineData("refs/merge-requests/١/head", false)]
    [InlineData("refs/merge-requests/5/head", true)]
    [InlineData("refs/merge-requests/5/merge", true)]
    [InlineData("refs/merge-requests/5/train", true)]
    [InlineData("refs/merge-requests/foo", true)]
    [InlineData("refs/merge-requests", true)]
    [InlineData("refs/pull/fix/head", true)]
    [InlineData("refs/heads/feature", true)]
    [InlineData("", true)]
    public void On_GitLab_only_a_merge_request_ref_whose_first_segment_is_not_all_digits_cannot_exist(string remoteRef, bool canExist) =>
        Assert.Equal(canExist, PullRequestRefs.SourceCanExist(HostingProvider.GitLab, remoteRef));

    [Theory]
    [InlineData("refs/pull/fix/merge", false)]
    [InlineData("refs/pull/fix/head", false)]
    [InlineData("refs/pull/a/b/merge", false)]
    [InlineData("refs/pull//merge", false)]
    [InlineData("refs/pull/7/head", false)]
    [InlineData("refs/pull/007/head", false)]
    [InlineData("refs/pull/7/merge", true)]
    [InlineData("refs/pull/7/head/more", true)]
    [InlineData("refs/pull/7/train", true)]
    [InlineData("refs/pull/fix", true)]
    [InlineData("refs/merge-requests/foo/head", true)]
    [InlineData("refs/heads/pr/7", true)]
    [InlineData("", true)]
    public void On_Azure_DevOps_a_pull_ref_with_a_non_digit_number_or_a_head_cannot_exist(string remoteRef, bool canExist) =>
        Assert.Equal(canExist, PullRequestRefs.SourceCanExist(HostingProvider.AzureDevOps, remoteRef));

    [Theory]
    [InlineData("refs/pull/8/head", true)]
    [InlineData("refs/pull/007/head", true)]
    [InlineData("refs/merge-requests/8/head", true)]
    [InlineData("refs/merge-requests/007/head", true)]
    [InlineData("refs/merge-requests/8/merge", false)]
    [InlineData("refs/merge-requests/x/head", false)]
    [InlineData("refs/merge-requests//head", false)]
    [InlineData("refs/merge-requests/8/head/more", false)]
    [InlineData("merge-requests/8/head", false)]
    [InlineData("refs/heads/main", false)]
    [InlineData("refs/pull/8/merge", false)]
    [InlineData("refs/pull/x/head", false)]
    [InlineData("refs/pull//head", false)]
    [InlineData("refs/pull/head", false)]
    [InlineData("refs/pull/8/head/more", false)]
    [InlineData("refs/pull/١/head", false)]
    [InlineData("pull/8/head", false)]
    [InlineData("", false)]
    public void The_fetch_takes_a_pull_requests_head_alone(string sourceRef, bool fetchable) =>
        Assert.Equal(fetchable, PullRequestRefs.IsFetchableHead(sourceRef));

    [Theory]
    [InlineData(HostingProvider.GitHub, 12, "refs/pull/12/head")]
    [InlineData(HostingProvider.GitHub, 1, "refs/pull/1/head")]
    [InlineData(HostingProvider.GitLab, 12, "refs/merge-requests/12/head")]
    [InlineData(HostingProvider.AzureDevOps, 12, null)]
    public void Each_provider_has_its_head_ref(HostingProvider provider, int number, string? head) =>
        Assert.Equal(head, PullRequestRefs.HeadOf(provider, number));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_number_below_one_has_no_head(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PullRequestRefs.HeadOf(HostingProvider.GitHub, number));
        Assert.Throws<ArgumentOutOfRangeException>(() => PullRequestRefs.HeadOf(HostingProvider.AzureDevOps, number));
        Assert.Throws<ArgumentOutOfRangeException>(() => PullRequestRefs.HeadOf(HostingProvider.GitLab, number));
    }

    [Fact]
    public void A_null_ref_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => PullRequestRefs.IsFetchableHead(null!));
        Assert.Throws<ArgumentNullException>(() => PullRequestRefs.SourceCanExist(HostingProvider.GitHub, null!));
        Assert.Throws<ArgumentNullException>(() => PullRequestRefs.SourceCanExist(HostingProvider.GitLab, null!));
        Assert.Throws<ArgumentNullException>(() => PullRequestRefs.SourceCanExist(HostingProvider.AzureDevOps, null!));
    }

    [Fact]
    public void An_unknown_provider_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PullRequestRefs.SourceCanExist((HostingProvider)99, "refs/pull/1/head"));
        Assert.Throws<ArgumentOutOfRangeException>(() => PullRequestRefs.HeadOf((HostingProvider)99, 1));
    }
}
