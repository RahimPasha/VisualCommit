using VisualCommit.Testing.Hosting;

namespace VisualCommit.Testing;

/// <summary>The Azure DevOps half of "hosting7b"'s seeding (docs/phase-7b-design.md 1.9, 2.6, 3.5).</summary>
public static partial class Scenarios
{
    /// <summary>The title of harbor-co's pull request !5 into manifest (D356's other organization).</summary>
    public const string Hosting7bManifestRequestTitle = "Update the manifest";

    /// <summary>
    /// Seeds the fake of Azure DevOps of a served "hosting7b" (<see cref="ServeHosting7bAsync"/>, design
    /// 3.5) and returns Ada's token of Fernhill-Labs, <c>azdo_FAKE-Fernhill-Labs-1</c>, the
    /// organization's first. With no git process: the bare folders are served in place.
    /// <list type="bullet">
    /// <item>Fernhill-Labs, its private project "Mobile App", the users Ada Lovelace and Octo Cat (the
    /// scenario's authors, <c>ada.lovelace@example.com</c> and <c>octo.cat@example.com</c>); Ada's token,
    /// Code (Read &amp; write).</item>
    /// <item>The repositories <c>mobile-app</c>, <c>mobile-app-fork</c> (a fork of it), <c>sandbox</c> and
    /// <see cref="Hosting7bLongRepository"/> (both empty, so without a default branch), in that order.</item>
    /// <item>mobile-app's pull requests !11 "Add the sign-in page" (Ada, <c>feature/sign-in</c>), !12
    /// "Write the guide" (Octo, <c>docs/guide</c>, a draft) and !13 "Fix a typo in the greeting" (Ada,
    /// from the fork's <c>fix-typo</c>), each into <c>main</c>.</item>
    /// <item>harbor-co, its private project "Docks" and a user of its own, Test Author, who has no
    /// token; its repository <c>manifest</c> and !5 <see cref="Hosting7bManifestRequestTitle"/> (Test
    /// Author) into <c>main</c> from <c>update-manifest</c>, a branch the bare repository has not got:
    /// no merge ref is made for it, and nothing lists it (no account covers harbor-co, D356).</item>
    /// </list>
    /// The merge refs of !11 to !13 are written by the fake's first ref sync (a list, or git's ref listing).
    /// </summary>
    private static async Task<string> SeedHosting7bAzureDevOpsAsync(FakeAzureDevOps fake, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fake);
        fake.AddOrganization(Hosting7bOrganization);
        fake.AddProject(Hosting7bOrganization, Hosting7bProject);
        var ada = fake.AddUser(Hosting7bOrganization, AdaName);
        var octo = fake.AddUser(Hosting7bOrganization, OctoName);
        var token = fake.AddToken(Hosting7bOrganization, ada);
        fake.AddOrganization(Hosting7bOtherOrganization);
        fake.AddProject(Hosting7bOtherOrganization, Hosting7bOtherProject);
        var author = fake.AddUser(Hosting7bOtherOrganization, TempRepo.AuthorName);

        var mobileApp = await fake.AddRepositoryAsync(Hosting7bOrganization, Hosting7bProject, "mobile-app", ct: ct).ConfigureAwait(false);
        var fork = await fake.AddRepositoryAsync(Hosting7bOrganization, Hosting7bProject, "mobile-app-fork", forkOf: mobileApp, ct: ct).ConfigureAwait(false);
        await fake.AddRepositoryAsync(Hosting7bOrganization, Hosting7bProject, "sandbox", ct: ct).ConfigureAwait(false);
        await fake.AddRepositoryAsync(Hosting7bOrganization, Hosting7bProject, Hosting7bLongRepository, ct: ct).ConfigureAwait(false);
        var manifest = await fake.AddRepositoryAsync(Hosting7bOtherOrganization, Hosting7bOtherProject, "manifest", ct: ct).ConfigureAwait(false);

        fake.AddPullRequest(mobileApp, "Add the sign-in page", ada, "feature/sign-in", "main", id: 11);
        fake.AddPullRequest(mobileApp, "Write the guide", octo, "docs/guide", "main", draft: true, id: 12);
        fake.AddPullRequest(mobileApp, "Fix a typo in the greeting", ada, "fix-typo", "main", fork: fork, id: 13);
        fake.AddPullRequest(manifest, Hosting7bManifestRequestTitle, author, "update-manifest", "main", id: 5);
        return token;
    }
}
