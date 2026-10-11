// Stashes and tags: docs/phase-3-design.md 2.24 to 2.28 (stash save with its dialog, apply,
// pop, drop; tag create and delete), and their texts in 2.33 (OperationTexts). Pushing and
// deleting a tag on a remote is in RepositoryOperations.Remotes.cs (2.29). Stubs left by WP7-core:
// the sub-package WP7-stashtags replaces each body with the operation, built on the helpers of
// RepositoryOperations.Run.cs, and keeps every signature (IRepositoryActions lists them). Its
// tests go into tests/VisualCommit.App.Tests/RepositoryOperationsTests.StashTags.cs (a part of
// the partial class RepositoryOperationsTests, through OperationsHarness).

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    private const string StashTagsPackage = "stashtags";

    public Task StashAsync() => NotBuilt(StashTagsPackage);

    public Task ApplyStashAsync(string stash) => NotBuilt(StashTagsPackage);

    public Task PopStashAsync(string stash) => NotBuilt(StashTagsPackage);

    public Task PopNewestStashAsync() => NotBuilt(StashTagsPackage);

    public Task DropStashAsync(string stash) => NotBuilt(StashTagsPackage);

    public Task CreateTagAsync(string? target) => NotBuilt(StashTagsPackage);

    public Task DeleteTagAsync(string tagRef) => NotBuilt(StashTagsPackage);
}
