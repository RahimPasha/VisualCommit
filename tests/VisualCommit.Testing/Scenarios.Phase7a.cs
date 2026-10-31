using VisualCommit.Core;
using VisualCommit.Testing.Hosting;

namespace VisualCommit.Testing;

/// <summary>
/// Phase 7a's scenario, "hosting" (docs/phase-7a-design.md 2.6), which every part of 7a's checks
/// uses: a local GitHub's repositories beside the user's two clones, built once per test process
/// as a template and copied for every call. <c>Phase7aScenarioTests</c> pins what it holds.
/// </summary>
public static partial class Scenarios
{
    /// <summary>Octo Cat, octo on the fake (1001).</summary>
    public const string OctoName = "Octo Cat";

    public const string OctoEmail = "octo@example.com";

    /// <summary>Ada Lovelace, ada on the fake (1003).</summary>
    public const string AdaName = "Ada Lovelace";

    public const string AdaEmail = "ada@example.com";

    /// <summary>octo/tool's description, the scenario's only one (the report's open point 11; part B's browser).</summary>
    public const string ToolDescription = "Small command-line tools.";

    /// <summary>The long branch whose label the graph must cut (part G).</summary>
    public const string LongBranch = "feature/a-very-long-branch-name-that-the-graph-must-cut";

    /// <summary>The folder beside <c>hello/</c> that holds the fake's bare repositories, <c>&lt;owner&gt;/&lt;name&gt;.git</c>.</summary>
    public const string HostingFolder = "github";

    /// <summary>
    /// A fresh copy of "hosting" (2.6), made with one git process (<see cref="TempRepo.CopyAsync"/>):
    /// the returned repository is <c>hello/</c>; beside it <c>tool/</c> and the fake's folder
    /// <c>github/</c> (<see cref="HostingFolder"/>). Commits a minute apart from 2026-01-01 12:00 UTC
    /// by <see cref="OctoName"/>, <see cref="AdaName"/> and <see cref="TempRepo.AuthorName"/>, whom
    /// GitHub does not know:
    /// <list type="bullet">
    /// <item><c>github/octo/hello.git</c> (private): <c>main</c> 12:00 "Initial commit" (Octo), 12:01
    /// "Add the greeting" (Ada), 12:02 "Add the docs folder" (Test Author); one commit each on top
    /// of <c>main</c>: <c>feature/login</c> 12:03 "Add the login form" (Ada), <c>docs/readme</c>
    /// 12:04 "Update the readme" (Octo), <see cref="LongBranch"/> 12:05 "Shorten long labels"
    /// (Octo), <c>topic/no-pr</c> 12:06 "Try an idea" (Test Author); <c>refs/pull/1</c> to
    /// <c>5/head</c> written.</item>
    /// <item><c>github/ada/hello.git</c> (a fork by ada): octo/hello's branches and <c>fix-typo</c>
    /// 12:11 "Fix a typo in the greeting" (Ada), octo/hello's <c>refs/pull/5/head</c>.</item>
    /// <item><c>github/acme/tool.git</c> (organisation acme): <c>main</c> 12:07 "Initial commit"
    /// (Ada); its fork <c>github/octo/tool.git</c> adds <c>fix-typo</c> 12:08 "Fix a typo in the
    /// readme" (Octo), acme's <c>refs/pull/7/head</c>; a fork <c>gone/tool</c>'s <c>idea</c> 12:09
    /// "An old idea" (Ada) is acme's <c>refs/pull/8/head</c>, the fork itself deleted.</item>
    /// <item><c>github/octo/notes.git</c> and <c>github/octo/website.git</c>: empty.</item>
    /// <item><c>hello/</c>: <c>main</c> tracking <c>origin/main</c>, <c>feature/login</c> tracking
    /// its remote branch, local-only <c>local-work</c> 12:10 "Local change" (Test Author); on
    /// <c>main</c>, clean.</item>
    /// <item><c>tool/</c>: a clone of octo/tool, <c>upstream</c> acme/tool, <c>fix-typo</c> tracking
    /// <c>origin/fix-typo</c>, on <c>main</c>.</item>
    /// </list>
    /// Each remote has its <c>HEAD</c> (<c>refs/remotes/&lt;remote&gt;/HEAD</c> at its <c>main</c>),
    /// whatever git built it.
    /// The remotes' URLs are <c>http://127.0.0.1:47395/&lt;owner&gt;/&lt;name&gt;.git</c> (D265),
    /// written last; <see cref="ServeHostingAsync"/> serves them.
    /// </summary>
    public static async Task<TempRepo> HostingAsync()
    {
        var template = await TemplateAsync("hosting", HostingTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> HostingTemplate = new(() => KeepUntilExit(BuildHostingAsync()));

    /// <summary>The URL of <paramref name="ownerAndName"/> on the fake at <paramref name="port"/>: <c>http://127.0.0.1:&lt;port&gt;/octo/hello.git</c>.</summary>
    public static string HostingUrl(string ownerAndName, int port = AuthRemoteServer.Phase7aVisualChecksPort) =>
        FormattableString.Invariant($"http://127.0.0.1:{port}/{ownerAndName}.git");

    /// <summary>
    /// Serves a copy of "hosting" (<see cref="HostingAsync"/>): the fake of GitHub on the copy's
    /// <c>github/</c>, on <paramref name="port"/> (<see cref="AuthRemoteServer.Phase7aVisualChecksPort"/>
    /// unless given; 0 for one the system picks) with a fallback to a free port, or in process
    /// (<paramref name="inProcess"/>, no server, the URLs left as they are). Users octo 1001 "Octo
    /// Cat", acme 1002 (an organisation) "Acme", ada 1003 "Ada Lovelace"; tokens
    /// <c>github_pat_FAKE-octo-1</c>, <c>ghp_FAKE-octo-2</c>, <c>github_pat_FAKE-ada-3</c>; the six
    /// repositories, updated (hello) at <see cref="AppClocks.HarnessTime"/>, (the tools) an hour,
    /// (notes) a day, (website, archived) 30 days and (ada/hello) 31 days before; acme/tool's
    /// pusher ada; octo/hello's pull requests #1 "Add the login form" (ada, <c>feature/login</c>),
    /// #2 "Update the readme" (octo, a draft), #3 "Shorten long labels" (octo), #4 "Try an idea"
    /// (octo, closed), #5 "Fix a typo in the greeting" (ada, from ada/hello); acme/tool's #7 "Fix a
    /// typo in the readme" (octo, from octo/tool) and #8 "An old idea" (ada, from the deleted
    /// gone/tool); octo's and ada's e-mails linked. Only when the fake is on a port other than
    /// 47395 do the clones' remotes get its URLs (<c>git remote set-url</c>, D265). With
    /// <paramref name="ownsCopy"/> disposing the result deletes <paramref name="hello"/>'s copy too.
    /// <para>
    /// In process the clones' remotes stay at <c>http://127.0.0.1:47395/…</c> while the fake's
    /// endpoints are <c>Local(1)</c> (<see cref="FakeGitHub.InProcessPort"/>): the app, which knows
    /// the fake by scheme, host and port, recognises neither clone's remotes as GitHub, and git
    /// reaches nothing. Such a copy serves only calls to the fake's routes by handler (give the
    /// session <c>hosting: new HostingOptions(fake.Endpoints, fake.CreateHandler)</c>, never
    /// <c>ShellDriver.Start(gitHub: …)</c>, which refuses it). A test that needs the remotes
    /// recognised, or git to reach the fake, serves on a port: <c>port: 0</c>.
    /// </para>
    /// </summary>
    public static async Task<ServedHosting> ServeHostingAsync(TempRepo hello, int port = AuthRemoteServer.Phase7aVisualChecksPort, bool inProcess = false, CancellationToken ct = default, bool ownsCopy = false)
    {
        ArgumentNullException.ThrowIfNull(hello);
        var root = hello.SiblingPath(HostingFolder);
        var fake = inProcess ? FakeGitHub.InProcess(root) : await FakeGitHub.StartAsync(root, port, fallBackToFreePort: true, ct).ConfigureAwait(false);
        try
        {
            fake.AddUser("octo", 1001, OctoName);
            fake.AddUser("acme", 1002, "Acme").IsOrganization = true;
            fake.AddUser("ada", 1003, AdaName);
            var octoToken = fake.AddToken("octo");
            var octoClassicToken = fake.AddToken("octo", FakeTokenKind.Classic);
            var adaToken = fake.AddToken("ada");

            var time = AppClocks.HarnessTime;
            (await fake.AddRepositoryAsync("octo", "hello", isPrivate: true, ct: ct).ConfigureAwait(false)).UpdatedAt = time;
            (await fake.AddRepositoryAsync("ada", "hello", forkOf: "octo/hello", ct: ct).ConfigureAwait(false)).UpdatedAt = time.AddDays(-31);
            var acmeTool = await fake.AddRepositoryAsync("acme", "tool", ct: ct).ConfigureAwait(false);
            acmeTool.UpdatedAt = time.AddHours(-1);
            acmeTool.AddPusher("ada");
            // The one described repository (the report's open point 11): the browser's detail joins
            // its flag and its description, "Fork · Small command-line tools." (design 9.4).
            (await fake.AddRepositoryAsync("octo", "tool", forkOf: "acme/tool", description: ToolDescription, ct: ct).ConfigureAwait(false)).UpdatedAt = time.AddHours(-1);
            (await fake.AddRepositoryAsync("octo", "notes", isPrivate: true, ct: ct).ConfigureAwait(false)).UpdatedAt = time.AddDays(-1);
            var website = await fake.AddRepositoryAsync("octo", "website", ct: ct).ConfigureAwait(false);
            website.UpdatedAt = time.AddDays(-30);
            website.IsArchived = true;

            fake.AddPullRequest("octo", "hello", "Add the login form", "ada", "feature/login", "main", number: 1);
            fake.AddPullRequest("octo", "hello", "Update the readme", "octo", "docs/readme", "main", draft: true, number: 2);
            fake.AddPullRequest("octo", "hello", "Shorten long labels", "octo", LongBranch, "main", number: 3);
            fake.AddPullRequest("octo", "hello", "Try an idea", "octo", "topic/no-pr", "main", number: 4).State = "closed";
            fake.AddPullRequest("octo", "hello", "Fix a typo in the greeting", "ada", "fix-typo", "main", headRepository: "ada/hello", number: 5);
            fake.AddPullRequest("acme", "tool", "Fix a typo in the readme", "octo", "fix-typo", "main", headRepository: "octo/tool", number: 7);
            fake.AddPullRequest("acme", "tool", "An old idea", "ada", "idea", "main", headRepository: "gone/tool", number: 8);
            fake.LinkEmail(OctoEmail, "octo");
            fake.LinkEmail(AdaEmail, "ada");

            var tool = hello.OpenSibling("tool");
            if (!inProcess && fake.Port != AuthRemoteServer.Phase7aVisualChecksPort)
            {
                await hello.GitAsync("remote", "set-url", "origin", HostingUrl("octo/hello", fake.Port)).ConfigureAwait(false);
                await tool.GitAsync("remote", "set-url", "origin", HostingUrl("octo/tool", fake.Port)).ConfigureAwait(false);
                await tool.GitAsync("remote", "set-url", "upstream", HostingUrl("acme/tool", fake.Port)).ConfigureAwait(false);
            }

            return new ServedHosting(fake, hello, tool, octoToken, octoClassicToken, adaToken, ownsCopy);
        }
        catch
        {
            await fake.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<TempRepo> BuildHostingAsync()
    {
        var repo = await TempRepo.CreateAsync("hello");
        try
        {
            // octo/hello's history, a minute apart from 12:00; a branch's commit on top of main each.
            await repo.CommitFileAsAsync("README.md", "# hello\n", "Initial commit", OctoName, OctoEmail);
            await repo.CommitFileAsAsync("src/greeting.txt", "Helo\n", "Add the greeting", AdaName, AdaEmail);
            await repo.CommitFileAsync("docs/index.md", "# Docs\n", "Add the docs folder");
            var login = await BranchCommitAsync(repo, "feature/login", "main", "src/login.txt", "login form\n", "Add the login form", AdaName, AdaEmail);
            var readme = await BranchCommitAsync(repo, "docs/readme", "main", "README.md", "# hello\n\nA greeting for the tests.\n", "Update the readme", OctoName, OctoEmail);
            var labels = await BranchCommitAsync(repo, LongBranch, "main", "src/labels.txt", "short labels\n", "Shorten long labels", OctoName, OctoEmail);
            var idea = await BranchCommitAsync(repo, "topic/no-pr", "main", "src/idea.txt", "an idea\n", "Try an idea", TempRepo.AuthorName, TempRepo.AuthorEmail);

            // acme/tool's history on an orphan branch of this repository, then its two forks' branches.
            await repo.GitAsync("switch", "--quiet", "--orphan", "acme-main");
            var toolMain = await repo.CommitFileAsAsync("README.md", "# tool\n", "Initial commit", AdaName, AdaEmail);
            var toolFix = await BranchCommitAsync(repo, "octo-fix-typo", "acme-main", "README.md", "# tool\n\nA tool for the tests.\n", "Fix a typo in the readme", OctoName, OctoEmail);
            var goneIdea = await BranchCommitAsync(repo, "gone-idea", "acme-main", "idea.txt", "an old idea\n", "An old idea", AdaName, AdaEmail);

            await BranchCommitAsync(repo, "local-work", "main", "src/local.txt", "local change\n", "Local change", TempRepo.AuthorName, TempRepo.AuthorEmail);
            var adaFix = await BranchCommitAsync(repo, "ada-fix-typo", "main", "src/greeting.txt", "Hello\n", "Fix a typo in the greeting", AdaName, AdaEmail);
            await repo.GitAsync("switch", "--quiet", "main");

            // The fake's bare repositories, each pushed to once; gone/tool is never made.
            string[] helloBranches = ["main", "feature/login", "docs/readme", LongBranch, "topic/no-pr"];
            await PushToBareAsync(repo, "octo/hello", [.. helloBranches, $"{login}:refs/pull/1/head", $"{readme}:refs/pull/2/head", $"{labels}:refs/pull/3/head", $"{idea}:refs/pull/4/head", $"{adaFix}:refs/pull/5/head"]);
            await PushToBareAsync(repo, "ada/hello", [.. helloBranches, "ada-fix-typo:refs/heads/fix-typo"]);
            await PushToBareAsync(repo, "acme/tool", [$"{toolMain}:refs/heads/main", $"{toolFix}:refs/pull/7/head", $"{goneIdea}:refs/pull/8/head"]);
            await PushToBareAsync(repo, "octo/tool", [$"{toolMain}:refs/heads/main", "octo-fix-typo:refs/heads/fix-typo"]);
            await PushToBareAsync(repo, "octo/notes", []);
            await PushToBareAsync(repo, "octo/website", []);

            // hello/ keeps main, feature/login and local-work, tracking octo/hello.
            await repo.GitAsync("branch", "--quiet", "-D", "docs/readme", LongBranch, "topic/no-pr", "acme-main", "octo-fix-typo", "gone-idea", "ada-fix-typo");
            await repo.GitAsync("remote", "add", "origin", BareOf(repo, "octo/hello"));
            await repo.GitAsync("fetch", "--quiet", "origin");

            // Git 2.48 and newer make the remote's HEAD at a fetch, older ones do not: made here, so
            // that every git builds the same scenario (a clone has it too).
            await repo.GitAsync("symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/main");
            await repo.GitAsync("branch", "--quiet", "--set-upstream-to", "origin/main", "main");
            await repo.GitAsync("branch", "--quiet", "--set-upstream-to", "origin/feature/login", "feature/login");

            // tool/: a clone of octo/tool with acme/tool as upstream.
            var tool = await repo.CloneSiblingAsync("tool", BareOf(repo, "octo/tool"));
            await tool.GitAsync("remote", "add", "upstream", BareOf(repo, "acme/tool"));
            await tool.GitAsync("fetch", "--quiet", "upstream");
            await tool.GitAsync("symbolic-ref", "refs/remotes/upstream/HEAD", "refs/remotes/upstream/main");
            await tool.GitAsync("branch", "--quiet", "--track", "fix-typo", "origin/fix-typo");

            // The URLs on the fake, written last (D265).
            await repo.GitAsync("remote", "set-url", "origin", HostingUrl("octo/hello"));
            await tool.GitAsync("remote", "set-url", "origin", HostingUrl("octo/tool"));
            await tool.GitAsync("remote", "set-url", "upstream", HostingUrl("acme/tool"));
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>Makes <paramref name="branch"/> at <paramref name="startPoint"/>, switches to it and commits one file there by the author given. Returns the commit's SHA.</summary>
    private static async Task<string> BranchCommitAsync(TempRepo repo, string branch, string startPoint, string path, string content, string message, string authorName, string authorEmail)
    {
        await repo.GitAsync("switch", "--quiet", "--create", branch, startPoint);
        return await repo.CommitFileAsAsync(path, content, message, authorName, authorEmail);
    }

    /// <summary>The bare repository <c>github/&lt;owner&gt;/&lt;name&gt;.git</c> beside <paramref name="repo"/>.</summary>
    private static string BareOf(TempRepo repo, string ownerAndName) =>
        Path.Combine(repo.SiblingPath(HostingFolder), ownerAndName.Replace('/', Path.DirectorySeparatorChar) + ".git");

    /// <summary>Makes <c>github/&lt;owner&gt;/&lt;name&gt;.git</c> as a copy of the blank bare repository, on <c>main</c>, and pushes <paramref name="refspecs"/> to it in one push (none: it stays empty).</summary>
    private static async Task PushToBareAsync(TempRepo repo, string ownerAndName, string[] refspecs)
    {
        var bare = BareOf(repo, ownerAndName);
        TempRepo.CopyDirectory(await TempRepo.BlankBareFolderAsync().ConfigureAwait(false), bare);
        await File.WriteAllTextAsync(Path.Combine(bare, "HEAD"), "ref: refs/heads/main\n").ConfigureAwait(false);
        if (refspecs.Length > 0)
        {
            await repo.GitAsync(["push", "--quiet", bare, .. refspecs]).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// A served copy of "hosting" (<see cref="Scenarios.ServeHostingAsync"/>): the fake of GitHub,
/// the two clones and the scenario's tokens. Disposing it stops the fake; the copy is the caller's
/// unless it was handed over (<c>ownsCopy</c>).
/// </summary>
public sealed class ServedHosting(FakeGitHub gitHub, TempRepo hello, TempRepo tool, string octoToken, string octoClassicToken, string adaToken, bool ownsCopy = false) : IAsyncDisposable
{
    public FakeGitHub GitHub { get; } = gitHub;

    /// <summary>The copy's <c>hello/</c>, a clone of octo/hello.</summary>
    public TempRepo Hello { get; } = hello;

    /// <summary>The copy's <c>tool/</c>, a clone of octo/tool with acme/tool as upstream.</summary>
    public TempRepo Tool { get; } = tool;

    /// <summary>octo's fine-grained token, <c>github_pat_FAKE-octo-1</c>.</summary>
    public string OctoToken { get; } = octoToken;

    /// <summary>octo's classic token, <c>ghp_FAKE-octo-2</c>, with <c>repo</c> and <c>workflow</c>.</summary>
    public string OctoClassicToken { get; } = octoClassicToken;

    /// <summary>ada's fine-grained token, <c>github_pat_FAKE-ada-3</c>.</summary>
    public string AdaToken { get; } = adaToken;

    public async ValueTask DisposeAsync()
    {
        await GitHub.DisposeAsync().ConfigureAwait(false);
        if (ownsCopy)
        {
            Hello.Dispose();
        }
    }

    public override string ToString() => $"ServedHosting({GitHub.Endpoints.WebBase})";
}
