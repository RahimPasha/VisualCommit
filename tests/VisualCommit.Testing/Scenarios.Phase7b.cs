using VisualCommit.Testing.Hosting;

namespace VisualCommit.Testing;

/// <summary>
/// Phase 7b's scenario, "hosting7b" (docs/phase-7b-design.md 2.6), which every part of 7b's checks
/// uses: the bare repositories of a local Azure DevOps, GitLab and GitHub beside the user's three
/// clones, built once per test process as a template and copied for every call; made-up names only.
/// <c>Phase7bSupportTests</c> pins what it holds. Its two halves of the fakes' seeding are
/// <c>Scenarios.Phase7b.AzureDevOps.cs</c> and <c>Scenarios.Phase7b.GitLab.cs</c> (WP-FA's and WP-FL's).
/// </summary>
public static partial class Scenarios
{
    /// <summary>The folder beside <c>mobile-app/</c> of the fake of Azure DevOps' bare repositories, <c>&lt;organization&gt;/&lt;project&gt;/_git/&lt;repository&gt;</c>.</summary>
    public const string AzureDevOpsFolder = "azure-devops";

    /// <summary>The folder beside <c>mobile-app/</c> of the fake of GitLab's bare repositories, <c>&lt;namespace path&gt;/&lt;project&gt;.git</c>.</summary>
    public const string GitLabFolder = "gitlab";

    /// <summary>The scenario's Azure DevOps organization, whose account the checks sign in to.</summary>
    public const string Hosting7bOrganization = "Fernhill-Labs";

    /// <summary>Another organization (D356): <c>manifest/</c>'s, which no account of the scenario covers.</summary>
    public const string Hosting7bOtherOrganization = "harbor-co";

    /// <summary>Fernhill-Labs' project, whose name holds a space (<c>%20</c> in its URLs).</summary>
    public const string Hosting7bProject = "Mobile App";

    /// <summary>harbor-co's project.</summary>
    public const string Hosting7bOtherProject = "Docks";

    /// <summary>The Azure DevOps repository whose long name the browser must cut (empty).</summary>
    public const string Hosting7bLongRepository = "customer-onboarding-and-identity-verification-service";

    /// <summary>The GitLab project, three groups deep, whose long path the browser must cut (empty).</summary>
    public const string Hosting7bLongProject = "northfield/platform/services/billing/invoice-generation-and-reconciliation";

    /// <summary>web-app's path on GitLab, under the nested group <c>northfield/platform</c>.</summary>
    public const string Hosting7bWebApp = "northfield/platform/web-app";

    /// <summary>ada's fork of web-app on GitLab.</summary>
    public const string Hosting7bWebAppFork = "ada/web-app";

    /// <summary>web-app's upstream on GitHub (ruling 5).</summary>
    public const string Hosting7bGitHubWebApp = "octo/web-app";

    /// <summary>
    /// A fresh copy of "hosting7b" (2.6), made with one git process (<see cref="TempRepo.CopyAsync"/>):
    /// the returned repository is <c>mobile-app/</c>; beside it <c>manifest/</c>, <c>web-app/</c> and
    /// the fakes' folders <c>azure-devops/</c>, <c>gitlab/</c> and <c>github/</c>. Commits a minute
    /// apart by <see cref="OctoName"/>, <see cref="AdaName"/> and <see cref="TempRepo.AuthorName"/>:
    /// <list type="bullet">
    /// <item><c>azure-devops/Fernhill-Labs/Mobile App/_git/mobile-app</c>: <c>main</c> 12:00 "Initial
    /// commit" (Octo), 12:01 "Add the greeting" (Ada), 12:02 "Add the docs folder" (Test Author); one
    /// commit each on top of <c>main</c>: <c>feature/sign-in</c> 12:03 "Add the sign-in page" (Ada),
    /// <c>docs/guide</c> 12:04 "Write the guide" (Octo), <c>topic/no-request</c> 12:05 "Try an idea"
    /// (Test Author). Beside it <c>mobile-app-fork</c> (the same and <c>fix-typo</c> 12:11 "Fix a typo
    /// in the greeting", Ada), and <c>sandbox</c> and <see cref="Hosting7bLongRepository"/>, empty
    /// (no default branch).</item>
    /// <item><c>azure-devops/harbor-co/Docks/_git/manifest</c>: <c>main</c> 12:40 "Initial commit" (Test Author).</item>
    /// <item><c>gitlab/northfield/platform/web-app.git</c>: <c>main</c> 12:20 "Initial commit" (Octo),
    /// 12:21 "Add the footer" (Ada), 12:22 "Add the docs folder" (Test Author); <c>feature/search</c>
    /// 12:23 "Add the search box" (Ada), <c>docs/readme</c> 12:24 "Update the readme" (Octo),
    /// <c>topic/no-request</c> 12:25 "Try an idea" (Test Author). <c>gitlab/ada/web-app.git</c>: the
    /// same and <c>fix-typo</c> 12:31 "Fix a typo in the footer" (Ada).
    /// <c>gitlab/</c><see cref="Hosting7bLongProject"/><c>.git</c>: empty.</item>
    /// <item><c>github/octo/web-app.git</c>: web-app's <c>main</c>.</item>
    /// <item><c>mobile-app/</c>: <c>main</c> and <c>feature/sign-in</c> tracking their remote
    /// branches, local-only <c>local-work</c> 12:06 "Local change" (Test Author), on <c>main</c>; origin
    /// <c>http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app</c>
    /// (the service's <c>&lt;organization&gt;@</c> form, r5 0.5, MEASURED; <c>%20</c> ASSUMED, r5 6.1).</item>
    /// <item><c>manifest/</c>: a clone of manifest on <c>main</c>; origin
    /// <c>http://harbor-co@127.0.0.1:47397/harbor-co/Docks/_git/manifest</c> (D356).</item>
    /// <item><c>web-app/</c>: a clone of web-app, <c>feature/search</c> tracking its remote branch,
    /// local-only <c>local-work</c> 12:26 "Local change" (Test Author), on <c>main</c>; origin
    /// <c>http://127.0.0.1:47398/northfield/platform/web-app.git</c>, upstream
    /// <c>http://127.0.0.1:47399/octo/web-app.git</c> (ruling 5).</item>
    /// </list>
    /// Each remote has its <c>HEAD</c> at its <c>main</c>. The URLs, on 1.8's ports, are written last;
    /// <see cref="ServeHosting7bAsync"/> serves them.
    /// </summary>
    public static async Task<TempRepo> Hosting7bAsync()
    {
        var template = await TemplateAsync("hosting7b", Hosting7bTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> Hosting7bTemplate = new(() => KeepUntilExit(BuildHosting7bAsync()));

    /// <summary>
    /// The URL of an Azure DevOps repository on the fake at <paramref name="port"/>, in the service's
    /// form: <c>http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app</c>
    /// (the organization as the user, the project <see cref="Uri.EscapeDataString"/>d).
    /// </summary>
    public static string AzureDevOpsUrl(string organization, string project, string repository, int port = AuthRemoteServer.Phase7bAzureDevOpsPort) =>
        FormattableString.Invariant($"http://{organization}@127.0.0.1:{port}/{organization}/{Uri.EscapeDataString(project)}/_git/{repository}");

    /// <summary>The URL of the GitLab project <paramref name="path"/> on the fake at <paramref name="port"/>: <c>http://127.0.0.1:47398/northfield/platform/web-app.git</c>.</summary>
    public static string GitLabUrl(string path, int port = AuthRemoteServer.Phase7bGitLabPort) =>
        FormattableString.Invariant($"http://127.0.0.1:{port}/{path}.git");

    /// <summary>
    /// Serves a copy of "hosting7b" (<see cref="Hosting7bAsync"/>): the fakes of Azure DevOps, GitLab and
    /// GitHub on the copy's <c>azure-devops/</c>, <c>gitlab/</c> and <c>github/</c>, each on its port
    /// (<see cref="AuthRemoteServer.Phase7bAzureDevOpsPort"/>, <see cref="AuthRemoteServer.Phase7bGitLabPort"/>,
    /// <see cref="AuthRemoteServer.Phase7bGitHubPort"/> unless given; 0 for one the system picks) with a
    /// fallback to a free port, none in process. GitHub's fake has octo 1001 "Octo Cat", his token
    /// <c>github_pat_FAKE-octo-1</c> and the public <c>octo/web-app</c>; the other two are seeded by
    /// their halves (<c>SeedHosting7bAzureDevOpsAsync</c>, <c>SeedHosting7bGitLabAsync</c>, 3.5 and
    /// 4.5), which return their tokens. Only a fake on a port other than its fixed one has its clones'
    /// remotes rewritten (<c>git remote set-url</c>, D265, keeping <c>&lt;organization&gt;@</c> and
    /// <c>%20</c>). With <paramref name="ownsCopy"/> disposing the result deletes the copy too.
    /// </summary>
    public static async Task<ServedHosting7b> ServeHosting7bAsync(
        TempRepo copy,
        int azureDevOpsPort = AuthRemoteServer.Phase7bAzureDevOpsPort,
        int gitLabPort = AuthRemoteServer.Phase7bGitLabPort,
        int gitHubPort = AuthRemoteServer.Phase7bGitHubPort,
        bool ownsCopy = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(copy);
        FakeAzureDevOps? azureDevOps = null;
        FakeGitLab? gitLab = null;
        FakeGitHub? gitHub = null;
        try
        {
            azureDevOps = await FakeAzureDevOps.StartAsync(copy.SiblingPath(AzureDevOpsFolder), azureDevOpsPort, fallBackToFreePort: true, ct).ConfigureAwait(false);
            gitLab = await FakeGitLab.StartAsync(copy.SiblingPath(GitLabFolder), gitLabPort, fallBackToFreePort: true, ct).ConfigureAwait(false);
            gitHub = await FakeGitHub.StartAsync(copy.SiblingPath(HostingFolder), gitHubPort, fallBackToFreePort: true, ct).ConfigureAwait(false);

            gitHub.AddUser("octo", 1001, OctoName);
            var gitHubToken = gitHub.AddToken("octo");
            await gitHub.AddRepositoryAsync("octo", "web-app", ct: ct).ConfigureAwait(false);
            var azureDevOpsToken = await SeedHosting7bAzureDevOpsAsync(azureDevOps, ct).ConfigureAwait(false);
            var (gitLabToken, gitLabLegacyToken) = await SeedHosting7bGitLabAsync(gitLab, ct).ConfigureAwait(false);

            var manifest = copy.OpenSibling("manifest");
            var webApp = copy.OpenSibling("web-app");
            if (azureDevOps.Port != AuthRemoteServer.Phase7bAzureDevOpsPort)
            {
                await copy.GitAsync("remote", "set-url", "origin", AzureDevOpsUrl(Hosting7bOrganization, Hosting7bProject, "mobile-app", azureDevOps.Port)).ConfigureAwait(false);
                await manifest.GitAsync("remote", "set-url", "origin", AzureDevOpsUrl(Hosting7bOtherOrganization, Hosting7bOtherProject, "manifest", azureDevOps.Port)).ConfigureAwait(false);
            }

            if (gitLab.Port != AuthRemoteServer.Phase7bGitLabPort)
            {
                await webApp.GitAsync("remote", "set-url", "origin", GitLabUrl(Hosting7bWebApp, gitLab.Port)).ConfigureAwait(false);
            }

            if (gitHub.Port != AuthRemoteServer.Phase7bGitHubPort)
            {
                await webApp.GitAsync("remote", "set-url", "upstream", HostingUrl(Hosting7bGitHubWebApp, gitHub.Port)).ConfigureAwait(false);
            }

            return new ServedHosting7b(azureDevOps, gitLab, gitHub, copy, manifest, webApp, azureDevOpsToken, gitLabToken, gitLabLegacyToken, gitHubToken, ownsCopy);
        }
        catch
        {
            foreach (var fake in new IAsyncDisposable?[] { gitHub, gitLab, azureDevOps })
            {
                if (fake is not null)
                {
                    await fake.DisposeAsync().ConfigureAwait(false);
                }
            }

            throw;
        }
    }

    private static async Task<TempRepo> BuildHosting7bAsync()
    {
        var repo = await TempRepo.CreateAsync("mobile-app");
        try
        {
            var day = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            // mobile-app's history from 12:00, a branch's commit on top of main each, then the
            // local-only work and the fork's fix.
            await repo.CommitFileAsAsync("README.md", "# mobile-app\n", "Initial commit", OctoName, OctoEmail);
            await repo.CommitFileAsAsync("src/greeting.txt", "Helo\n", "Add the greeting", AdaName, AdaEmail);
            await repo.CommitFileAsync("docs/index.md", "# Docs\n", "Add the docs folder");
            await BranchCommitAsync(repo, "feature/sign-in", "main", "src/sign-in.txt", "sign-in page\n", "Add the sign-in page", AdaName, AdaEmail);
            await BranchCommitAsync(repo, "docs/guide", "main", "docs/guide.md", "# Guide\n", "Write the guide", OctoName, OctoEmail);
            await BranchCommitAsync(repo, "topic/no-request", "main", "src/idea.txt", "an idea\n", "Try an idea", TempRepo.AuthorName, TempRepo.AuthorEmail);
            await BranchCommitAsync(repo, "local-work", "main", "src/local.txt", "local change\n", "Local change", TempRepo.AuthorName, TempRepo.AuthorEmail);
            repo.MoveClockTo(day.AddHours(12).AddMinutes(11));
            await BranchCommitAsync(repo, "mobile-fix-typo", "main", "src/greeting.txt", "Hello\n", "Fix a typo in the greeting", AdaName, AdaEmail);

            // web-app's history from 12:20 on an orphan branch of this repository.
            repo.MoveClockTo(day.AddHours(12).AddMinutes(20));
            await repo.GitAsync("switch", "--quiet", "--orphan", "web-main");
            await repo.CommitFileAsAsync("README.md", "# web-app\n", "Initial commit", OctoName, OctoEmail);
            await repo.CommitFileAsAsync("src/footer.txt", "Thnaks for visiting\n", "Add the footer", AdaName, AdaEmail);
            await repo.CommitFileAsync("docs/index.md", "# Docs\n", "Add the docs folder");
            await BranchCommitAsync(repo, "web-feature-search", "web-main", "src/search.txt", "search box\n", "Add the search box", AdaName, AdaEmail);
            await BranchCommitAsync(repo, "web-docs-readme", "web-main", "README.md", "# web-app\n\nA web app for the tests.\n", "Update the readme", OctoName, OctoEmail);
            await BranchCommitAsync(repo, "web-topic-no-request", "web-main", "src/idea.txt", "an idea\n", "Try an idea", TempRepo.AuthorName, TempRepo.AuthorEmail);
            await BranchCommitAsync(repo, "web-local-work", "web-main", "src/local.txt", "local change\n", "Local change", TempRepo.AuthorName, TempRepo.AuthorEmail);
            repo.MoveClockTo(day.AddHours(12).AddMinutes(31));
            await BranchCommitAsync(repo, "web-fix-typo", "web-main", "src/footer.txt", "Thanks for visiting\n", "Fix a typo in the footer", AdaName, AdaEmail);

            // manifest's at 12:40, on another orphan branch.
            repo.MoveClockTo(day.AddHours(12).AddMinutes(40));
            await repo.GitAsync("switch", "--quiet", "--orphan", "manifest-main");
            await repo.CommitFileAsync("manifest.yml", "name: manifest\n", "Initial commit");
            await repo.GitAsync("switch", "--quiet", "main");

            // The fakes' bare repositories, each pushed to once; the empty ones never.
            string[] mobileBranches = ["main", "feature/sign-in", "docs/guide", "topic/no-request"];
            string[] webBranches = ["web-main:refs/heads/main", "web-feature-search:refs/heads/feature/search", "web-docs-readme:refs/heads/docs/readme", "web-topic-no-request:refs/heads/topic/no-request"];
            var mobileBare = BareOf7b(repo, AzureDevOpsFolder, Hosting7bOrganization, Hosting7bProject, "_git", "mobile-app");
            var manifestBare = BareOf7b(repo, AzureDevOpsFolder, Hosting7bOtherOrganization, Hosting7bOtherProject, "_git", "manifest");
            var webBare = BareOf7b(repo, GitLabFolder, "northfield", "platform", "web-app.git");
            var gitHubBare = BareOf7b(repo, HostingFolder, "octo", "web-app.git");
            await PushToNewBareAsync(repo, mobileBare, mobileBranches);
            await PushToNewBareAsync(repo, BareOf7b(repo, AzureDevOpsFolder, Hosting7bOrganization, Hosting7bProject, "_git", "mobile-app-fork"), [.. mobileBranches, "mobile-fix-typo:refs/heads/fix-typo"]);
            await PushToNewBareAsync(repo, BareOf7b(repo, AzureDevOpsFolder, Hosting7bOrganization, Hosting7bProject, "_git", "sandbox"), []);
            await PushToNewBareAsync(repo, BareOf7b(repo, AzureDevOpsFolder, Hosting7bOrganization, Hosting7bProject, "_git", Hosting7bLongRepository), []);
            await PushToNewBareAsync(repo, manifestBare, ["manifest-main:refs/heads/main"]);
            await PushToNewBareAsync(repo, webBare, webBranches);
            await PushToNewBareAsync(repo, BareOf7b(repo, GitLabFolder, "ada", "web-app.git"), [.. webBranches, "web-fix-typo:refs/heads/fix-typo"]);
            await PushToNewBareAsync(repo, BareOf7b(repo, GitLabFolder, [.. (Hosting7bLongProject + ".git").Split('/')]), []);
            await PushToNewBareAsync(repo, gitHubBare, ["web-main:refs/heads/main"]);

            // web-app/: a clone of web-app with octo/web-app as upstream, and its local-only work.
            var webApp = await repo.CloneSiblingAsync("web-app", webBare);
            await webApp.GitAsync("remote", "add", "upstream", gitHubBare);
            await webApp.GitAsync("fetch", "--quiet", "upstream");
            await webApp.GitAsync("symbolic-ref", "refs/remotes/upstream/HEAD", "refs/remotes/upstream/main");
            await webApp.GitAsync("branch", "--quiet", "--track", "feature/search", "origin/feature/search");
            await repo.GitAsync("push", "--quiet", webApp.Path, "web-local-work:refs/heads/local-work");

            // manifest/: a clone of manifest.
            var manifest = await repo.CloneSiblingAsync("manifest", manifestBare);

            // mobile-app/ keeps main, feature/sign-in and local-work, tracking mobile-app.
            await repo.GitAsync(
                "branch", "--quiet", "-D", "docs/guide", "topic/no-request", "mobile-fix-typo", "web-main", "web-feature-search", "web-docs-readme",
                "web-topic-no-request", "web-local-work", "web-fix-typo", "manifest-main");
            await repo.GitAsync("remote", "add", "origin", mobileBare);
            await repo.GitAsync("fetch", "--quiet", "origin");

            // Git 2.48 and newer make the remote's HEAD at a fetch, older ones do not: made here, so
            // that every git builds the same scenario (a clone has it too).
            await repo.GitAsync("symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/main");
            await repo.GitAsync("branch", "--quiet", "--set-upstream-to", "origin/main", "main");
            await repo.GitAsync("branch", "--quiet", "--set-upstream-to", "origin/feature/sign-in", "feature/sign-in");

            // The URLs on the fakes, written last (D265).
            await repo.GitAsync("remote", "set-url", "origin", AzureDevOpsUrl(Hosting7bOrganization, Hosting7bProject, "mobile-app"));
            await manifest.GitAsync("remote", "set-url", "origin", AzureDevOpsUrl(Hosting7bOtherOrganization, Hosting7bOtherProject, "manifest"));
            await webApp.GitAsync("remote", "set-url", "origin", GitLabUrl(Hosting7bWebApp));
            await webApp.GitAsync("remote", "set-url", "upstream", HostingUrl(Hosting7bGitHubWebApp, AuthRemoteServer.Phase7bGitHubPort));
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>A bare repository's folder under one of the fakes' folders beside <paramref name="repo"/>.</summary>
    private static string BareOf7b(TempRepo repo, string fakeFolder, params string[] segments) =>
        Path.Combine([repo.SiblingPath(fakeFolder), .. segments]);

    /// <summary>Makes <paramref name="bare"/> as a copy of the blank bare repository, on <c>main</c>, and pushes <paramref name="refspecs"/> to it in one push (none: it stays empty).</summary>
    private static async Task PushToNewBareAsync(TempRepo repo, string bare, string[] refspecs)
    {
        TempRepo.CopyDirectory(await TempRepo.BlankBareFolderAsync().ConfigureAwait(false), bare);
        await File.WriteAllTextAsync(Path.Combine(bare, "HEAD"), "ref: refs/heads/main\n").ConfigureAwait(false);
        if (refspecs.Length > 0)
        {
            await repo.GitAsync(["push", "--quiet", bare, .. refspecs]).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// A served copy of "hosting7b" (<see cref="Scenarios.ServeHosting7bAsync"/>): the three fakes, the
/// three clones and the scenario's tokens. Disposing it stops the fakes; the copy is the caller's
/// unless it was handed over (<c>ownsCopy</c>). <see cref="ToString"/> shows no token.
/// </summary>
public sealed class ServedHosting7b(
    FakeAzureDevOps azureDevOps,
    FakeGitLab gitLab,
    FakeGitHub gitHub,
    TempRepo mobileApp,
    TempRepo manifest,
    TempRepo webApp,
    string azureDevOpsToken,
    string gitLabToken,
    string gitLabLegacyToken,
    string gitHubToken,
    bool ownsCopy = false) : IAsyncDisposable
{
    public FakeAzureDevOps AzureDevOps { get; } = azureDevOps;

    public FakeGitLab GitLab { get; } = gitLab;

    public FakeGitHub GitHub { get; } = gitHub;

    /// <summary>The copy's <c>mobile-app/</c>, a clone of Fernhill-Labs' mobile-app on Azure DevOps.</summary>
    public TempRepo MobileApp { get; } = mobileApp;

    /// <summary>The copy's <c>manifest/</c>, a clone of harbor-co's manifest (D356).</summary>
    public TempRepo Manifest { get; } = manifest;

    /// <summary>The copy's <c>web-app/</c>, a clone of web-app on GitLab with octo/web-app on GitHub as upstream.</summary>
    public TempRepo WebApp { get; } = webApp;

    /// <summary>Ada's token of Fernhill-Labs (<c>azdo_FAKE-Fernhill-Labs-1</c>, 3.5).</summary>
    public string AzureDevOpsToken { get; } = azureDevOpsToken;

    /// <summary>ada's fine-grained token on GitLab (<c>glpat-FAKE-ada-1</c>, 4.5).</summary>
    public string GitLabToken { get; } = gitLabToken;

    /// <summary>ada's legacy token on GitLab (4.5).</summary>
    public string GitLabLegacyToken { get; } = gitLabLegacyToken;

    /// <summary>octo's fine-grained token on GitHub, <c>github_pat_FAKE-octo-1</c>.</summary>
    public string GitHubToken { get; } = gitHubToken;

    /// <summary>The scenario's tokens, for the tripwire: Azure DevOps', GitLab's two and GitHub's.</summary>
    public IReadOnlyList<string> Tokens => [AzureDevOpsToken, GitLabToken, GitLabLegacyToken, GitHubToken];

    /// <summary>The organization the checks sign in to, <c>Fernhill-Labs</c>.</summary>
    public string Organization => Scenarios.Hosting7bOrganization;

    /// <summary>manifest's organization, <c>harbor-co</c>, which no account of the scenario covers (D356).</summary>
    public string OtherOrganization => Scenarios.Hosting7bOtherOrganization;

    public async ValueTask DisposeAsync()
    {
        await GitHub.DisposeAsync().ConfigureAwait(false);
        await GitLab.DisposeAsync().ConfigureAwait(false);
        await AzureDevOps.DisposeAsync().ConfigureAwait(false);
        if (ownsCopy)
        {
            MobileApp.Dispose();
        }
    }

    public override string ToString() => $"ServedHosting7b({AzureDevOps.Endpoints.WebBase}, {GitLab.Endpoints.WebBase}, {GitHub.Endpoints.WebBase})";
}
