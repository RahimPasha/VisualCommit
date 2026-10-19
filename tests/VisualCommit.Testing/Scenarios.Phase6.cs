using VisualCommit.Git;

namespace VisualCommit.Testing;

/// <summary>
/// Phase 6's scenarios (docs/phase-6-design.md 17.1), each built once per test process as a
/// template and copied for every call: the ten-repository workspace "Web" and the settings
/// repository the lean way (<see cref="LeanRepositories"/>), the renaming refspecs and the ignored
/// files the TempRepo way. <c>Phase6ScenarioTests</c> pins what they hold.
/// </summary>
public static partial class Scenarios
{
    // ---- The workspace -----------------------------------------------------------------------------

    /// <summary>
    /// A fresh copy of the workspace "Web" (17.1's table), copied without a git process
    /// (<see cref="TempWorkspace.CopyAsync"/>). Commits by <see cref="TempRepo.AuthorName"/> from
    /// 2026-01-01 12:00 UTC, a minute apart per repository; every tracked branch tracks
    /// <c>origin</c>'s of the same name, reached by <c>../remotes/&lt;name&gt;.git</c>:
    /// <list type="bullet">
    /// <item><c>api</c>: <c>main</c> = <c>origin/main</c> = the remote's, level and clean.</item>
    /// <item><c>web-app</c>: two commits ahead.</item>
    /// <item><c>docs</c>: three behind as fetched, four once the remote's newest commit is fetched.</item>
    /// <item><c>mobile</c>: one ahead and two behind, one ahead and three behind after a fetch.</item>
    /// <item><c>design-system</c>: level until a fetch (three behind then); <c>colours.txt</c> staged,
    /// <c>fonts.txt</c> deleted and <c>spacing.txt</c> changed unstaged, <c>icons.txt</c> untracked.</item>
    /// <item><c>billing</c>: stopped in a merge of <c>feature/tax</c> with <c>invoice.txt</c> conflicted.</item>
    /// <item><c>infra</c>: HEAD detached at its first commit, which the tag <c>v1.0</c> marks.</item>
    /// <item><c>scratch</c>: on <c>experiment</c>, which tracks nothing.</item>
    /// <item><c>notes</c>: no remote.</item>
    /// <item><c>old-site</c>: listed, never made.</item>
    /// </list>
    /// </summary>
    public static async Task<TempWorkspace> WorkspaceAsync()
    {
        var template = await WorkspaceTemplateAsync().ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    /// <summary>The workspace's template itself, for the tests that pin how it was built (its <see cref="TempWorkspace.Calls"/>). Never change it.</summary>
    internal static Task<TempWorkspace> WorkspaceTemplateAsync() => TemplateAsync("workspace", WorkspaceTemplate);

    private static readonly Lazy<Task<TempWorkspace>> WorkspaceTemplate = new(() => KeepUntilExit(BuildWorkspaceAsync()));

    /// <summary>The workspace's repositories as 17.1's table gives them, in the dashboard's order (old-site, never made, left out).</summary>
    internal static IReadOnlyList<LeanRepository> WorkspaceRepositories { get; } =
    [
        new(
            "api",
            [
                new("c0", 0, "Initial commit", null, ("README.md", "# api\n"), ("routes.txt", "GET /\n")),
                new("c1", 1, "Add the health route", "c0", ("routes.txt", "GET /\nGET /health\n")),
            ],
            Known: ["c0", "c1"],
            Refs: new Dictionary<string, string> { ["refs/heads/main"] = "c1", ["refs/remotes/origin/main"] = "c1" },
            RemoteHolds: ["c0", "c1"],
            RemoteMain: "c1",
            TrackedBranches: ["main"],
            Head: null,
            Clock: LeanRepositories.TimeOf(2)),
        new(
            "web-app",
            [
                new("c0", 0, "Initial commit", null, ("README.md", "# web-app\n"), ("index.html", "<h1>Home</h1>\n")),
                new("c1", 1, "Add the about page", "c0", ("about.html", "<h1>About</h1>\n")),
                new("c2", 2, "Style the pages", "c1", ("style.css", "h1 { color: teal; }\n")),
            ],
            Known: ["c0", "c1", "c2"],
            Refs: new Dictionary<string, string> { ["refs/heads/main"] = "c2", ["refs/remotes/origin/main"] = "c0" },
            RemoteHolds: ["c0"],
            RemoteMain: "c0",
            TrackedBranches: ["main"],
            Head: null,
            Clock: LeanRepositories.TimeOf(3)),
        new(
            "docs",
            [
                new("c0", 0, "Initial commit", null, ("README.md", "# docs\n"), ("guide.md", "Guide\n")),
                new("c1", 1, "Write the install guide", "c0", ("install.md", "Install it.\n")),
                new("c2", 2, "Write the FAQ", "c1", ("faq.md", "Questions.\n")),
                new("c3", 3, "Fix the guide's typos", "c2", ("guide.md", "Guide.\n")),
                new("c4", 4, "Add the changelog", "c3", ("changelog.md", "Changes.\n")),
            ],
            Known: ["c0", "c1", "c2", "c3"],
            Refs: new Dictionary<string, string> { ["refs/heads/main"] = "c0", ["refs/remotes/origin/main"] = "c3" },
            RemoteHolds: ["c0", "c1", "c2", "c3", "c4"],
            RemoteMain: "c4",
            TrackedBranches: ["main"],
            Head: null,
            Clock: LeanRepositories.TimeOf(5)),
        new(
            "mobile",
            [
                new("c0", 0, "Initial commit", null, ("README.md", "# mobile\n"), ("app.txt", "app\n")),
                new("c1", 1, "Add the login screen", "c0", ("login.txt", "Log in\n")),
                new("c2", 2, "Add the settings screen", "c1", ("settings.txt", "Settings\n")),
                new("c3", 3, "Name the app", "c0", ("app.txt", "Mobile app\n")),
                new("c4", 4, "Add the about screen", "c2", ("about.txt", "About\n")),
            ],
            Known: ["c0", "c1", "c2", "c3"],
            Refs: new Dictionary<string, string> { ["refs/heads/main"] = "c3", ["refs/remotes/origin/main"] = "c2" },
            RemoteHolds: ["c0", "c1", "c2", "c4"],
            RemoteMain: "c4",
            TrackedBranches: ["main"],
            Head: null,
            Clock: LeanRepositories.TimeOf(5)),
        new(
            "design-system",
            [
                new("c0", 0, "Initial commit", null, ("README.md", "# design-system\n"), ("colours.txt", "red\n"), ("fonts.txt", "Inter\n"), ("spacing.txt", "4\n")),
                new("c1", 1, "Tighten the spacing", "c0", ("spacing.txt", "2\n")),
                new("c2", 2, "Loosen the spacing", "c1", ("spacing.txt", "8\n")),
                new("c3", 3, "Even out the spacing", "c2", ("spacing.txt", "6\n")),
            ],
            Known: ["c0"],
            Refs: new Dictionary<string, string> { ["refs/heads/main"] = "c0", ["refs/remotes/origin/main"] = "c0" },
            RemoteHolds: ["c0", "c1", "c2", "c3"],
            RemoteMain: "c3",
            TrackedBranches: ["main"],
            Head: null,
            Clock: LeanRepositories.TimeOf(4),
            ThenAsync: async steps =>
            {
                steps.Write("colours.txt", "red\nblue\n");
                await steps.GitAsync("add", "colours.txt").ConfigureAwait(false);
                steps.Write("spacing.txt", "4\n16\n").Delete("fonts.txt").Write("icons.txt", "home\n");
            }),
        new(
            "billing",
            [
                new("c0", 0, "Initial commit", null, ("README.md", "# billing\n"), ("invoice.txt", "total: 10\n")),
                new("c1", 1, "Add the tax", "c0", ("invoice.txt", "total: 12\n")),
                new("c2", 2, "Round the totals", "c0", ("invoice.txt", "total: 10.00\n")),
            ],
            Known: ["c0", "c1", "c2"],
            Refs: new Dictionary<string, string> { ["refs/heads/main"] = "c2", ["refs/heads/feature/tax"] = "c1", ["refs/remotes/origin/main"] = "c2" },
            RemoteHolds: ["c0", "c2"],
            RemoteMain: "c2",
            TrackedBranches: ["main"],
            Head: null,
            Clock: LeanRepositories.TimeOf(3),
            ThenAsync: async steps =>
            {
                // Stops with invoice.txt conflicted, exit 1 (0.10).
                var merge = await steps.GitAllowingFailureAsync("merge", "--quiet", "feature/tax").ConfigureAwait(false);
                if (merge.ExitCode != 1)
                {
                    throw new InvalidOperationException($"billing's merge was to stop with a conflict (exit 1), and ended with {merge.ExitCode}: {merge.StandardError}");
                }
            }),
        new(
            "infra",
            [
                new("c0", 0, "Initial commit", null, ("README.md", "# infra\n"), ("deploy.sh", "echo deploy\n")),
                new("c1", 1, "Deploy to staging first", "c0", ("deploy.sh", "echo staging\necho deploy\n")),
            ],
            Known: ["c0", "c1"],
            Refs: new Dictionary<string, string> { ["refs/heads/main"] = "c1", ["refs/tags/v1.0"] = "c0", ["refs/remotes/origin/main"] = "c1" },
            RemoteHolds: ["c0", "c1"],
            RemoteMain: "c1",
            TrackedBranches: ["main"],
            Head: "c0",
            Clock: LeanRepositories.TimeOf(2)),
        new(
            "scratch",
            [
                new("c0", 0, "Initial commit", null, ("README.md", "# scratch\n")),
                new("c1", 1, "Try an idea", "c0", ("idea.txt", "An idea\n")),
            ],
            Known: ["c0", "c1"],
            Refs: new Dictionary<string, string> { ["refs/heads/main"] = "c0", ["refs/heads/experiment"] = "c1", ["refs/remotes/origin/main"] = "c0" },
            RemoteHolds: ["c0"],
            RemoteMain: "c0",
            TrackedBranches: ["main"],
            Head: "refs/heads/experiment",
            Clock: LeanRepositories.TimeOf(2)),
        new(
            "notes",
            [
                new("c0", 0, "Initial commit", null, ("README.md", "# notes\n"), ("todo.txt", "Buy milk\n")),
                new("c1", 1, "Add the reading list", "c0", ("reading.txt", "A book\n")),
            ],
            Known: ["c0", "c1"],
            Refs: new Dictionary<string, string> { ["refs/heads/main"] = "c1" },
            RemoteHolds: null,
            RemoteMain: null,
            TrackedBranches: [],
            Head: null,
            Clock: LeanRepositories.TimeOf(2)),
    ];

    private static async Task<TempWorkspace> BuildWorkspaceAsync()
    {
        var root = new TempDirectory("workspace");
        try
        {
            var calls = new GitCallLog();
            var runner = new GitRunner(TempRepo.GitPath, calls);
            foreach (var repository in WorkspaceRepositories)
            {
                await LeanRepositories.BuildAsync(root, repository, runner).ConfigureAwait(false);
            }

            var clocks = WorkspaceRepositories.ToDictionary(repository => repository.Name, repository => repository.Clock, StringComparer.Ordinal);
            return new TempWorkspace(root, clocks, calls);
        }
        catch
        {
            root.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Serves <paramref name="copy"/>'s <c>remotes/</c> over HTTP (<see cref="AuthRemoteServer.StartForFolderAsync"/>)
    /// on <see cref="AuthRemoteServer.Phase6VisualChecksPort"/>, or a free port when it is taken
    /// (<see cref="AuthRemoteServer.PortWasTaken"/>), and points the <c>origin</c> of each of
    /// <paramref name="names"/> at its URL (<see cref="AuthRemoteServer.UrlOf"/>), a git process each
    /// (docs/phase-6-design.md 17.2): the walk-through's and the real window's only way to a folder
    /// server. With <paramref name="requireAuth"/> every request needs the server's credentials.
    /// </summary>
    public static async Task<AuthRemoteServer> ServeWorkspaceAsync(TempWorkspace copy, bool requireAuth, params string[] names)
    {
        ArgumentNullException.ThrowIfNull(copy);
        ArgumentNullException.ThrowIfNull(names);
        var server = await AuthRemoteServer.StartForFolderAsync(
            Path.Combine(copy.Root, LeanRepositories.RemotesFolder),
            AuthRemoteServer.Phase6VisualChecksPort,
            fallBackToFreePort: true).ConfigureAwait(false);
        try
        {
            server.RequireAuth = requireAuth;
            foreach (var name in names)
            {
                await copy.Repo(name).GitAsync("remote", "set-url", "origin", server.UrlOf(name)).ConfigureAwait(false);
            }

            return server;
        }
        catch
        {
            await server.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    // ---- The renaming refspecs ---------------------------------------------------------------------

    /// <summary>
    /// "refspecs" (17.1): <c>main</c> (12:00 "Initial commit", 12:01 "Add the parser") and
    /// <c>topic</c> (12:02 "Work on the topic") with three bare remotes beside it by relative URLs:
    /// <c>origin</c> with the default fetch line and <c>+refs/pull/*/head:refs/remotes/origin/pr/*</c>,
    /// holding <c>main</c>, <c>feature/x</c> (12:01) and <c>refs/pull/1/head</c> (12:02); <c>fork</c>
    /// fetching only <c>+refs/heads/*:refs/remotes/fork/up/*</c>, holding <c>main</c> (12:01);
    /// <c>upstream</c> fetching only <c>+refs/pull/*/head:refs/remotes/upstream/pr/*</c>, holding
    /// <c>main</c> (12:01) and <c>refs/pull/7/head</c> (12:02). Fetched from all three: the tracking
    /// refs <c>fork/up/main</c>, <c>origin/feature/x</c>, <c>origin/main</c>, <c>origin/pr/1</c> and
    /// <c>upstream/pr/7</c>; <c>main</c> tracks <c>origin/main</c>, clean.
    /// </summary>
    public static async Task<TempRepo> RenamingRefspecAsync()
    {
        var template = await TemplateAsync("refspecs", RenamingRefspecTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> RenamingRefspecTemplate = new(() => KeepUntilExit(BuildRenamingRefspecAsync()));

    private static async Task<TempRepo> BuildRenamingRefspecAsync()
    {
        var repo = await TempRepo.CreateAsync("refspecs");
        try
        {
            await repo.CommitFileAsync("README.md", "# refspecs\n", "Initial commit");
            await repo.CommitFileAsync("parser.txt", "parse\n", "Add the parser");
            await repo.CreateBranchAsync("topic");
            await repo.CheckoutAsync("topic");
            await repo.CommitFileAsync("topic.txt", "topic\n", "Work on the topic");
            await repo.CheckoutAsync("main");

            await repo.AddBareRemoteAsync("origin", relative: true);
            await repo.AddBareRemoteAsync("fork", relative: true);
            await repo.AddBareRemoteAsync("upstream", relative: true);
            await repo.GitAsync("config", "--add", "remote.origin.fetch", "+refs/pull/*/head:refs/remotes/origin/pr/*");
            await repo.GitAsync("config", "remote.fork.fetch", "+refs/heads/*:refs/remotes/fork/up/*");
            await repo.GitAsync("config", "remote.upstream.fetch", "+refs/pull/*/head:refs/remotes/upstream/pr/*");

            // main tracks origin/main: pushed with -u alone, as the other pushes of main would
            // make it track the branch they push to (0.10).
            await repo.PushAsync("origin", setUpstream: true, "main");
            await repo.PushAsync("origin", setUpstream: false, "main:refs/heads/feature/x", "topic:refs/pull/1/head");
            await repo.PushAsync("fork", setUpstream: false, "main");
            await repo.PushAsync("upstream", setUpstream: false, "main", "topic:refs/pull/7/head");
            await repo.GitAsync("fetch", "--quiet", "--all");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    // ---- The ignored files -------------------------------------------------------------------------

    /// <summary>
    /// "ignored" (17.1), with a bare <c>origin</c> beside it by a relative URL: on <c>main</c> 12:00
    /// "Initial commit" (<c>README.md</c>, <c>app.txt</c>, a <c>.gitignore</c> of <c>secret.env</c> and
    /// <c>build/</c>), 12:01 "Track the secret" (<c>secret.env</c> <c>old</c>, added with <c>-f</c>),
    /// 12:02 "Stop tracking the secret" (<c>git rm --cached</c>), where <c>vendor</c> and <c>tools</c>
    /// start; 12:03 "Add the vendor secret" on <c>vendor</c> (<c>secret.env</c> <c>vendor</c>), 12:04
    /// "Add the build file" on <c>tools</c> (a file <c>build</c>), 12:05 "Main work" on <c>main</c>.
    /// Then a stash "Stash the secret" holding a staged new <c>secret.env</c> <c>stashed</c>;
    /// <c>main</c> and <c>vendor</c> pushed and tracking theirs, <c>vendor</c> pushed again as
    /// <c>vendor-remote</c> (no local branch); and in the working tree, ignored, <c>secret.env</c>
    /// <c>LOCAL</c> and <c>build/out.bin</c> <c>binary</c> on a clean <c>main</c>, level with
    /// <c>origin/main</c>. Every checkout, merge, rebase, cherry-pick, revert and stash apply of the
    /// checks G22 and G23 would overwrite <c>secret.env</c> or lose <c>build/</c> without the app's check.
    /// </summary>
    public static async Task<TempRepo> IgnoredAsync()
    {
        var template = await TemplateAsync("ignored", IgnoredTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> IgnoredTemplate = new(() => KeepUntilExit(BuildIgnoredAsync()));

    /// <summary>The ignored scenario's files.</summary>
    public static class IgnoredFiles
    {
        public const string GitIgnore = "secret.env\nbuild/\n";

        public const string SecretTracked = "old\n";

        public const string SecretVendor = "vendor\n";

        public const string SecretStashed = "stashed\n";

        /// <summary>What the ignored <c>secret.env</c> of the working tree holds.</summary>
        public const string SecretLocal = "LOCAL\n";

        /// <summary>What the ignored <c>build/out.bin</c> of the working tree holds.</summary>
        public const string BuildOutput = "binary\n";

        public const string AppMain = "app\nmain\n";
    }

    private static async Task<TempRepo> BuildIgnoredAsync()
    {
        var repo = await TempRepo.CreateAsync("ignored");
        try
        {
            await repo
                .WriteFile("README.md", "# Ignored\n")
                .WriteFile("app.txt", "app\n")
                .WriteFile(".gitignore", IgnoredFiles.GitIgnore)
                .CommitAsync("Initial commit");

            repo.WriteFile("secret.env", IgnoredFiles.SecretTracked);
            await repo.GitAsync("add", "-f", "secret.env");
            await repo.CommitAsync("Track the secret");

            await repo.GitAsync("rm", "--cached", "--quiet", "secret.env");
            await repo.CommitAsync("Stop tracking the secret");
            await repo.CreateBranchAsync("vendor");
            await repo.CreateBranchAsync("tools");

            await repo.CheckoutAsync("vendor");
            repo.WriteFile("secret.env", IgnoredFiles.SecretVendor);
            await repo.GitAsync("add", "-f", "secret.env");
            await repo.CommitAsync("Add the vendor secret");

            await repo.CheckoutAsync("tools");
            repo.WriteFile("build", "build\n");
            await repo.GitAsync("add", "-f", "build");
            await repo.CommitAsync("Add the build file");

            await repo.CheckoutAsync("main");
            await repo.CommitFileAsync("app.txt", IgnoredFiles.AppMain, "Main work");

            repo.WriteFile("secret.env", IgnoredFiles.SecretStashed);
            await repo.GitAsync("add", "-f", "secret.env");
            await repo.StashAsync("Stash the secret");

            // main and vendor track theirs; vendor-remote has no local branch: pushed on its own,
            // without -u (listed last in the -u push it would make vendor track it, 0.10).
            await repo.AddBareRemoteAsync("origin", relative: true);
            await repo.PushAsync("origin", setUpstream: true, "main", "vendor");
            await repo.PushAsync("origin", setUpstream: false, "vendor:refs/heads/vendor-remote");

            repo.WriteFile("secret.env", IgnoredFiles.SecretLocal).WriteFile("build/out.bin", IgnoredFiles.BuildOutput);
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    // ---- Git's settings ----------------------------------------------------------------------------

    /// <summary>
    /// "settings" (17.1), the lean way, with a bare remote <c>remotes/settings.git</c> by a relative
    /// URL and no identity of its own (the blank folder's <c>[user]</c> removed): 12:00 "Initial
    /// commit" (<c>README.md</c>, <c>notes.txt</c>, <c>run.py</c>), 12:01 "Remote work" on the
    /// remote's <c>main</c> and <c>origin/main</c>, 12:02 "Local work" on <c>main</c> from 12:00: one
    /// ahead and one behind, clean, so that the pull modes differ (a merge commit, a rebase, a
    /// refusal). The checks seed the session's global file or write <c>.git/config</c> (17.2, 17.4).
    /// </summary>
    public static async Task<TempRepo> GitSettingsAsync()
    {
        var template = await TemplateAsync("settings", GitSettingsTemplate).ConfigureAwait(false);
        return await template.CopyAsync().ConfigureAwait(false);
    }

    private static readonly Lazy<Task<TempRepo>> GitSettingsTemplate = new(() => KeepUntilExit(BuildGitSettingsAsync()));

    /// <summary>The settings repository as 17.1 gives it.</summary>
    internal static LeanRepository GitSettingsRepository { get; } = new(
        "settings",
        [
            new("c0", 0, "Initial commit", null, ("README.md", "# Settings\n"), ("notes.txt", "one\ntwo\n"), ("run.py", "print(\"run\")\n")),
            new("c1", 1, "Remote work", "c0", ("remote.txt", "remote\n")),
            new("c2", 2, "Local work", "c0", ("local.txt", "local\n")),
        ],
        Known: ["c0", "c1", "c2"],
        Refs: new Dictionary<string, string> { ["refs/heads/main"] = "c2", ["refs/remotes/origin/main"] = "c1" },
        RemoteHolds: ["c0", "c1"],
        RemoteMain: "c1",
        TrackedBranches: ["main"],
        Head: null,
        Clock: LeanRepositories.TimeOf(3),
        ThenAsync: steps => steps.GitAsync("config", "--remove-section", "user"));

    private static async Task<TempRepo> BuildGitSettingsAsync()
    {
        var root = new TempDirectory("settings");
        try
        {
            var repository = GitSettingsRepository;
            await LeanRepositories.BuildAsync(root, repository, new GitRunner(TempRepo.GitPath)).ConfigureAwait(false);
            return TempRepo.Over(root, root.Combine(repository.Name), repository.Clock, ownsRoot: true);
        }
        catch
        {
            root.Dispose();
            throw;
        }
    }
}
