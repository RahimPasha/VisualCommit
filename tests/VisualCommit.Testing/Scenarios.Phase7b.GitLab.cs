using VisualCommit.Testing.Hosting;

namespace VisualCommit.Testing;

/// <summary>The GitLab half of "hosting7b"'s seeding (docs/phase-7b-design.md 1.9, 2.6, 4.5).</summary>
public static partial class Scenarios
{
    /// <summary>The GitLab group both scenario users are Developers (30) of, web-app's top namespace.</summary>
    public const string Hosting7bGitLabGroup = "northfield";

    /// <summary>
    /// Seeds the fake of GitLab of a served "hosting7b" (<see cref="ServeHosting7bAsync"/>, 4.5) and
    /// returns ada's fine-grained and legacy tokens, <c>glpat-FAKE-ada-1</c> and <c>glpat-FAKE-ada-2</c>:
    /// users ada (2001, <see cref="AdaName"/>) and octo (2002, <see cref="OctoName"/>), both Developers
    /// (30) of the group <see cref="Hosting7bGitLabGroup"/>; the copy's bare folders served in place,
    /// all private: <see cref="Hosting7bWebApp"/>, <see cref="Hosting7bWebAppFork"/> as its fork
    /// (ada's, Owner) and <see cref="Hosting7bLongProject"/>, empty; web-app's merge requests !21 "Add the
    /// search box" (ada, <c>feature/search</c>), !22 "Draft: Update the readme" (octo,
    /// <c>docs/readme</c>) and !23 "Fix a typo in the footer" (ada, from the fork's <c>fix-typo</c>), each
    /// into <c>main</c>. Their head refs are written at the fake's first ref sync (a list, or git's ref
    /// listing).
    /// </summary>
    private static async Task<(string FineGrained, string Legacy)> SeedHosting7bGitLabAsync(FakeGitLab fake, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fake);
        fake.AddUser("ada", 2001, AdaName);
        fake.AddUser("octo", 2002, OctoName);
        var fineGrained = fake.AddToken("ada");
        var legacy = fake.AddToken("ada", FakeGitLabTokenKind.Legacy);
        fake.Grant(Hosting7bGitLabGroup, "ada", 30);
        fake.Grant(Hosting7bGitLabGroup, "octo", 30);

        var webApp = await fake.AddProjectAsync(Hosting7bWebApp, ct: ct).ConfigureAwait(false);
        var fork = await fake.AddProjectAsync(Hosting7bWebAppFork, forkOf: webApp, ct: ct).ConfigureAwait(false);
        await fake.AddProjectAsync(Hosting7bLongProject, ct: ct).ConfigureAwait(false);
        fake.AddMergeRequest(webApp, "Add the search box", "ada", "feature/search", "main", iid: 21);
        fake.AddMergeRequest(webApp, "Draft: Update the readme", "octo", "docs/readme", "main", iid: 22);
        fake.AddMergeRequest(webApp, "Fix a typo in the footer", "ada", "fix-typo", "main", source: fork, iid: 23);
        return (fineGrained, legacy);
    }
}
