// The pull requests of the left panel's section (docs/phase-7a-design.md 1.7, section 10): open in
// the browser, check out, copy the link, refresh, and sign in to GitHub. Not built yet: WP-P
// builds them.

using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <inheritdoc cref="IRepositoryActions.OpenPullRequestAsync"/>
    /// <remarks>Does nothing yet: WP-P builds it (docs/phase-7a-design.md 10).</remarks>
    public Task OpenPullRequestAsync(PullRequest pullRequest) => Task.CompletedTask;

    /// <inheritdoc cref="IRepositoryActions.CheckOutPullRequestAsync"/>
    /// <remarks>Does nothing yet: WP-P builds it (docs/phase-7a-design.md 10.5).</remarks>
    public Task CheckOutPullRequestAsync(PullRequest pullRequest) => Task.CompletedTask;

    /// <inheritdoc cref="IRepositoryActions.CopyPullRequestLinkAsync"/>
    /// <remarks>Does nothing yet: WP-P builds it (docs/phase-7a-design.md 10).</remarks>
    public Task CopyPullRequestLinkAsync(PullRequest pullRequest) => Task.CompletedTask;

    /// <inheritdoc cref="IRepositoryActions.RefreshPullRequestsAsync"/>
    /// <remarks>Does nothing yet: WP-P builds it (docs/phase-7a-design.md 10).</remarks>
    public Task RefreshPullRequestsAsync() => Task.CompletedTask;

    /// <inheritdoc cref="IRepositoryActions.OpenPullRequestsInBrowserAsync"/>
    /// <remarks>Does nothing yet: WP-P builds it (docs/phase-7a-design.md 10).</remarks>
    public Task OpenPullRequestsInBrowserAsync() => Task.CompletedTask;

    /// <inheritdoc cref="IRepositoryActions.SignInToGitHubAsync"/>
    /// <remarks>Does nothing yet: WP-P builds it (docs/phase-7a-design.md 10).</remarks>
    public Task SignInToGitHubAsync() => Task.CompletedTask;
}
