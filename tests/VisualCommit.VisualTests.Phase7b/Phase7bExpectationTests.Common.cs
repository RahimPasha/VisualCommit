using VisualCommit.Core.Hosting;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// The common expectations' own tests (docs/phase-7b-design.md 2.7, 2.8), as 7a's
/// <c>Phase7aExpectationTests.N.cs</c> pins its <c>PrepareAsync</c>: the one prepared state of parts P
/// and N and WP-R's launches. Not compiled by the real-window pass (its link's pattern does not match).
/// </summary>
public sealed partial class Phase7bExpectationTests
{
    [Theory]
    [InlineData(HostingProvider.GitLab, WebApp)]
    [InlineData(HostingProvider.AzureDevOps, MobileApp)]
    public async Task PrepareRequestsAsync_tracks_topic_no_request_in_the_providers_clone(HostingProvider provider, string clone)
    {
        using var copy = await Scenarios.Hosting7bAsync();
        var prepared = clone == MobileApp ? copy : copy.OpenSibling(clone);
        var other = clone == MobileApp ? copy.OpenSibling(WebApp) : copy;

        await PrepareRequestsAsync(copy, provider);

        Assert.Equal("origin", await ConfigAsync(prepared, "branch.topic/no-request.remote"));
        Assert.Equal("refs/heads/topic/no-request", await ConfigAsync(prepared, "branch.topic/no-request.merge"));
        Assert.Equal(
            (await prepared.GitAsync("rev-parse", "origin/topic/no-request")).StandardOutput.Trim(),
            (await prepared.GitAsync("rev-parse", "refs/heads/topic/no-request")).StandardOutput.Trim());
        Assert.False((await other.GitAllowingFailureAsync("rev-parse", "--verify", "--quiet", "refs/heads/topic/no-request")).Succeeded);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => PrepareRequestsAsync(copy, HostingProvider.GitHub));
    }

    private static async Task<string> ConfigAsync(TempRepo repo, string key) =>
        (await repo.GitAsync("config", "--get", key)).StandardOutput.Trim();
}
