using VisualCommit.Testing.Hosting;

namespace VisualCommit.Testing;

/// <summary>The Azure DevOps half of "hosting7b"'s seeding (docs/phase-7b-design.md 1.9, 2.6, 3.5).</summary>
public static partial class Scenarios
{
    /// <summary>
    /// Seeds the fake of Azure DevOps of a served "hosting7b" (<see cref="ServeHosting7bAsync"/>) and
    /// returns Ada's token of Fernhill-Labs. WP-F's stub: it seeds nothing yet and returns the token
    /// WP-FA's <c>AddToken</c> will give, <c>azdo_FAKE-Fernhill-Labs-1</c>; WP-FA fills it with 3.5's
    /// half (the organizations, projects, users, repositories served in place and pull requests).
    /// </summary>
    private static Task<string> SeedHosting7bAzureDevOpsAsync(FakeAzureDevOps fake, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fake);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult("azdo_FAKE-Fernhill-Labs-1");
    }
}
