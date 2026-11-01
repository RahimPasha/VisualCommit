using VisualCommit.Testing.Hosting;

namespace VisualCommit.Testing;

/// <summary>The GitLab half of "hosting7b"'s seeding (docs/phase-7b-design.md 1.9, 2.6, 4.5).</summary>
public static partial class Scenarios
{
    /// <summary>
    /// Seeds the fake of GitLab of a served "hosting7b" (<see cref="ServeHosting7bAsync"/>) and returns
    /// ada's fine-grained and legacy tokens. WP-F's stub: it seeds nothing yet and returns the tokens
    /// WP-FL's <c>AddToken</c> will give, <c>glpat-FAKE-ada-1</c> and <c>glpat-FAKE-ada-2</c>; WP-FL fills
    /// it with 4.5's half (the users, the group, the projects served in place and merge requests).
    /// </summary>
    private static Task<(string FineGrained, string Legacy)> SeedHosting7bGitLabAsync(FakeGitLab fake, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fake);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(("glpat-FAKE-ada-1", "glpat-FAKE-ada-2"));
    }
}
