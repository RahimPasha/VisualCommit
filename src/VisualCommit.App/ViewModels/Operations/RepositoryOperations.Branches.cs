// Branches and history: docs/phase-3-design.md 2.0 to 2.15, 2.31, 4g's "Check out and merge",
// section 5's Abort, and their texts in 2.33 (OperationTexts). Stubs left by WP7-core: the
// sub-package WP7-branches replaces each body with the operation, built on the helpers of
// RepositoryOperations.Run.cs, and keeps every signature (IRepositoryActions lists them). Its
// tests go into tests/VisualCommit.App.Tests/RepositoryOperationsTests.Branches.cs (a part of
// the partial class RepositoryOperationsTests, through OperationsHarness).

using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    private const string BranchesPackage = "branches";

    public Task CheckoutAsync(string target) => NotBuilt(BranchesPackage);

    public Task StashAndCheckoutAsync(string target) => NotBuilt(BranchesPackage);

    public Task CreateBranchAsync(string? start) => NotBuilt(BranchesPackage);

    public Task RenameBranchAsync(string branchRef) => NotBuilt(BranchesPackage);

    public Task DeleteBranchAsync(string branchRef) => NotBuilt(BranchesPackage);

    public Task FastForwardBranchAsync(string branchRef, string targetRef) => NotBuilt(BranchesPackage);

    public Task MergeAsync(string sourceRef, MergeMode mode) => NotBuilt(BranchesPackage);

    public Task CheckoutAndMergeAsync(string branchRef, string sourceRef) => NotBuilt(BranchesPackage);

    public Task ResetAsync(string commit) => NotBuilt(BranchesPackage);

    public Task CherryPickAsync(string commit) => NotBuilt(BranchesPackage);

    public Task CherryPickSeveralAsync(IReadOnlyList<string> commits) => NotBuilt(BranchesPackage);

    public Task RevertAsync(string commit) => NotBuilt(BranchesPackage);

    public Task RevertSeveralAsync(IReadOnlyList<string> commits) => NotBuilt(BranchesPackage);

    public Task AbortAsync() => NotBuilt(BranchesPackage);
}
