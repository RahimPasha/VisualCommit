// A new pull request (docs/phase-7a-design.md 1.7, section 12): the form of a branch's or a drop's
// "Create pull request…", the push when one is needed and the create, as one operation of the
// repository's queue. Not built yet: WP-N builds it.

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <inheritdoc cref="IRepositoryActions.CreatePullRequestAsync"/>
    /// <remarks>Does nothing yet: WP-N builds it (docs/phase-7a-design.md 12).</remarks>
    public Task CreatePullRequestAsync(string headRef, string? baseRef) => Task.CompletedTask;
}
