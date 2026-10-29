using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.ScenarioTestSupport;
using static VisualCommit.Git.Tests.SequenceTestSupport;
using Ignored = VisualCommit.Testing.Scenarios.IgnoredFiles;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Pins down phase 6's scenarios (docs/phase-6-design.md 17.1, 0.10): the workspace's ten
/// repositories (their status, refs, remotes and what a fetch from a copy brings), the renaming
/// refspecs (their fetch lines, tracking refs and what <c>git branch --track</c> makes of each), the
/// ignored files (commits, stash, ignored entries and the refusals that git gives only with
/// <c>--no-overwrite-ignore</c>) and the settings repository. Git's own sentences are asserted word
/// for word on the report's git, 2.36; on every git what classifies them (exit codes, refs,
/// configuration). The SHAs depend only on the scenarios' streams and clocks.
/// </summary>
public class Phase6ScenarioTests
{
    // ---- The workspace -----------------------------------------------------------------------------

    [Fact]
    public async Task The_workspace_has_its_ten_repositories_in_the_dashboards_order_and_old_site_is_never_made()
    {
        using var workspace = await Scenarios.WorkspaceAsync();

        Assert.Equal(["api", "web-app", "docs", "mobile", "design-system", "billing", "infra", "scratch", "notes", "old-site"], workspace.Names);
        Assert.All(workspace.Names.Take(9), name => Assert.True(Directory.Exists(Path.Combine(workspace.PathOf(name), ".git")), name));
        Assert.False(Directory.Exists(workspace.PathOf("old-site")));
        Assert.Throws<DirectoryNotFoundException>(() => workspace.Repo("old-site"));
        Assert.Equal(
            ["api.git", "billing.git", "design-system.git", "docs.git", "infra.git", "mobile.git", "scratch.git", "web-app.git"],
            Directory.EnumerateDirectories(Path.Combine(workspace.Root, "remotes")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Throws<ArgumentException>(() => workspace.RemoteOf("notes"));

        var web = workspace.ToWorkspace();
        Assert.Equal(("web", "Web"), (web.Id, web.Name));
        Assert.Equal(workspace.Names.Select(workspace.PathOf), web.Repositories.Select(repository => repository.Path));
    }

    [Fact]
    public async Task The_workspaces_repositories_are_each_in_their_own_state()
    {
        using var workspace = await Scenarios.WorkspaceAsync();

        Assert.Equal(["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -0"], await StatusAsync(workspace, "api"));
        Assert.Equal(["# branch.head main", "# branch.upstream origin/main", "# branch.ab +2 -0"], await StatusAsync(workspace, "web-app"));
        Assert.Equal(["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -3"], await StatusAsync(workspace, "docs"));
        Assert.Equal(["# branch.head main", "# branch.upstream origin/main", "# branch.ab +1 -2"], await StatusAsync(workspace, "mobile"));
        Assert.Equal(
            ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -0", "1 M. colours.txt", "1 .D fonts.txt", "1 .M spacing.txt", "? icons.txt"],
            await StatusAsync(workspace, "design-system"));
        Assert.Equal(["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -0", "u UU invoice.txt"], await StatusAsync(workspace, "billing"));
        Assert.Equal(["# branch.head (detached)"], await StatusAsync(workspace, "infra"));
        Assert.Equal(["# branch.head experiment"], await StatusAsync(workspace, "scratch"));
        Assert.Equal(["# branch.head main"], await StatusAsync(workspace, "notes"));

        // design-system's changes and billing's merge, as the dashboard reads them.
        var design = workspace.PathOf("design-system");
        Assert.Equal("red\nblue\n", File.ReadAllText(Path.Combine(design, "colours.txt")));
        Assert.Equal("4\n16\n", File.ReadAllText(Path.Combine(design, "spacing.txt")));
        Assert.False(File.Exists(Path.Combine(design, "fonts.txt")));
        Assert.Equal("home\n", File.ReadAllText(Path.Combine(design, "icons.txt")));
        var billing = workspace.Repo("billing");
        Assert.Equal(await billing.RefTargetAsync("refs/heads/feature/tax"), File.ReadAllText(Path.Combine(billing.Path, ".git", "MERGE_HEAD")).Trim());
    }

    [Fact]
    public async Task The_workspaces_refs_and_remotes_are_the_documented_ones()
    {
        using var workspace = await Scenarios.WorkspaceAsync();

        // The lab's SHAs (0.10, 17.1), the same on every git: fast-import's commits depend only on the stream.
        Assert.Equal(["2248c9d refs/heads/main", "2248c9d refs/remotes/origin/main"], await RefsAsync(workspace, "api"));
        Assert.Equal(["cbd9037 refs/heads/main", "147c6e3 refs/remotes/origin/main"], await RefsAsync(workspace, "web-app"));
        Assert.Equal(["ec855ba refs/heads/main", "23e2ad1 refs/remotes/origin/main"], await RefsAsync(workspace, "docs"));
        Assert.Equal(["21b8aeb refs/heads/main", "f419c6e refs/remotes/origin/main"], await RefsAsync(workspace, "mobile"));
        Assert.Equal(["98ba313 refs/heads/main", "98ba313 refs/remotes/origin/main"], await RefsAsync(workspace, "design-system"));
        Assert.Equal(["8236f53 refs/heads/feature/tax", "0e943ed refs/heads/main", "0e943ed refs/remotes/origin/main"], await RefsAsync(workspace, "billing"));
        Assert.Equal(["229cea5 refs/heads/main", "229cea5 refs/remotes/origin/main", "09d320a refs/tags/v1.0"], await RefsAsync(workspace, "infra"));
        Assert.Equal(["ba18976 refs/heads/experiment", "42a778b refs/heads/main", "42a778b refs/remotes/origin/main"], await RefsAsync(workspace, "scratch"));
        Assert.Equal(["937a43b refs/heads/main"], await RefsAsync(workspace, "notes"));
        Assert.Equal("09d320a", (await workspace.Repo("infra").HeadAsync())[..7]);
        Assert.Equal("ref: refs/heads/experiment", File.ReadAllText(Path.Combine(workspace.PathOf("scratch"), ".git", "HEAD")).Trim());

        // The remotes' main, the commits only they hold included.
        Assert.Equal(["2248c9d refs/heads/main"], await RemoteRefsAsync(workspace, "api"));
        Assert.Equal(["147c6e3 refs/heads/main"], await RemoteRefsAsync(workspace, "web-app"));
        Assert.Equal(["245b729 refs/heads/main"], await RemoteRefsAsync(workspace, "docs"));
        Assert.Equal(["67a2660 refs/heads/main"], await RemoteRefsAsync(workspace, "mobile"));
        Assert.Equal(["948f7d1 refs/heads/main"], await RemoteRefsAsync(workspace, "design-system"));
        Assert.Equal(["0e943ed refs/heads/main"], await RemoteRefsAsync(workspace, "billing"));
        Assert.Equal(["229cea5 refs/heads/main"], await RemoteRefsAsync(workspace, "infra"));
        Assert.Equal(["42a778b refs/heads/main"], await RemoteRefsAsync(workspace, "scratch"));

        // Relative URLs, which a copy resolves against its own folder; only main tracks, but in
        // billing (feature/tax tracks nothing) and scratch (experiment neither).
        foreach (var name in TempWorkspace.WithRemote)
        {
            var repo = workspace.Repo(name);
            Assert.Equal($"../remotes/{name}.git", (await repo.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim());
            Assert.Equal(["branch.main.remote origin", "branch.main.merge refs/heads/main"], Lines(await repo.GitAsync("config", "--get-regexp", @"^branch\.")));
            Assert.Equal(Path.GetFullPath(workspace.RemoteOf(name)), Path.GetFullPath(Path.Combine(workspace.PathOf(name), "..", "remotes", name + ".git")));
        }

        var notes = workspace.Repo("notes");
        Assert.Empty((await notes.GitAsync("remote")).StandardOutput.Trim());
        Assert.Equal(1, (await notes.GitAllowingFailureAsync("config", "--get-regexp", @"^branch\.")).ExitCode);

        // The reflogs hold the build's own entries: fast-import's for each ref it set (in HEAD's
        // log too, as HEAD names main) and, in HEAD's alone, the reset's.
        Assert.Equal(["reset: moving to HEAD", "fast-import"], Lines(await workspace.Repo("api").GitAsync("reflog", "--format=%gs", "HEAD")));
        Assert.Equal(["fast-import"], Lines(await workspace.Repo("api").GitAsync("reflog", "--format=%gs", "refs/heads/main")));
        Assert.Equal(["fast-import"], Lines(await workspace.Repo("api").GitAsync("reflog", "--format=%gs", "refs/remotes/origin/main")));
    }

    [Fact]
    public async Task A_copy_of_the_workspace_takes_no_git_process_and_fetches_from_its_own_remotes()
    {
        var template = await Scenarios.WorkspaceTemplateAsync();
        var built = template.Calls.Snapshot().Count;
        GitCallBudget.AssertAtMost(template.Calls.Snapshot(), 28, "the workspace template's build");

        using var copy = await template.CopyAsync();
        Assert.Equal(built, template.Calls.Snapshot().Count);
        Assert.Empty(copy.Calls.Snapshot());
        Assert.NotEqual(template.Root, copy.Root);

        // docs and mobile count one new commit and file (4 objects with the boundary's), design-system
        // three commits of one file each (11) (0.10); each from the copy's own remotes/.
        var docs = await FetchAsync(copy, "docs");
        var mobile = await FetchAsync(copy, "mobile");
        var design = await FetchAsync(copy, "design-system");
        Assert.Contains("Counting objects: 100% (4/4)", docs.StandardError, StringComparison.Ordinal);
        Assert.Contains("Counting objects: 100% (4/4)", mobile.StandardError, StringComparison.Ordinal);
        Assert.Contains("Counting objects: 100% (11/11)", design.StandardError, StringComparison.Ordinal);
        foreach (var name in new[] { "api", "web-app", "billing", "infra", "scratch" })
        {
            var quiet = await FetchAsync(copy, name);
            Assert.True(string.IsNullOrWhiteSpace(quiet.StandardError), $"{name}'s fetch printed:\n{quiet.StandardError}");
        }

        Assert.Equal(["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -4"], await StatusAsync(copy, "docs"));
        Assert.Equal(["# branch.head main", "# branch.upstream origin/main", "# branch.ab +1 -3"], await StatusAsync(copy, "mobile"));
        Assert.Equal(
            ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -3", "1 M. colours.txt", "1 .D fonts.txt", "1 .M spacing.txt", "? icons.txt"],
            await StatusAsync(copy, "design-system"));
        Assert.Equal("245b729", (await copy.Repo("docs").RefTargetAsync("refs/remotes/origin/main"))![..7]);

        // The template's own refs did not move.
        Assert.Equal(["ec855ba refs/heads/main", "23e2ad1 refs/remotes/origin/main"], await RefsAsync(template, "docs"));
        Assert.Equal(built, template.Calls.Snapshot().Count);
    }

    [Fact]
    public async Task No_copy_of_a_phase_6_scenario_holds_gits_hook_samples()
    {
        using var workspace = await Scenarios.WorkspaceAsync();
        using var refspecs = await Scenarios.RenamingRefspecAsync();
        using var ignored = await Scenarios.IgnoredAsync();
        using var settings = await Scenarios.GitSettingsAsync();

        var folders = new[] { workspace.Root, Path.GetDirectoryName(refspecs.Path)!, Path.GetDirectoryName(ignored.Path)!, Path.GetDirectoryName(settings.Path)! };
        var hooks = folders.SelectMany(folder => Directory.EnumerateDirectories(folder, "hooks", SearchOption.AllDirectories)).ToList();
        // The workspace's nine working repositories and eight remotes, refspecs' with its three
        // remotes, ignored's with its one, settings' with its one: git's hooks folders, kept empty.
        Assert.Equal(9 + 8 + 4 + 2 + 2, hooks.Count);
        Assert.All(hooks, folder => Assert.Empty(Directory.EnumerateFiles(folder, "*.sample")));
    }

    // ---- The renaming refspecs ---------------------------------------------------------------------

    [Fact]
    public async Task The_renaming_refspecs_map_each_remotes_refs_as_documented()
    {
        using var repo = await Scenarios.RenamingRefspecAsync();

        Assert.Equal(["+refs/heads/*:refs/remotes/origin/*", "+refs/pull/*/head:refs/remotes/origin/pr/*"], Lines(await repo.GitAsync("config", "--get-all", "remote.origin.fetch")));
        Assert.Equal(["+refs/heads/*:refs/remotes/fork/up/*"], Lines(await repo.GitAsync("config", "--get-all", "remote.fork.fetch")));
        Assert.Equal(["+refs/pull/*/head:refs/remotes/upstream/pr/*"], Lines(await repo.GitAsync("config", "--get-all", "remote.upstream.fetch")));
        foreach (var remote in new[] { "origin", "fork", "upstream" })
        {
            Assert.Equal($"../{remote}.git", (await repo.GitAsync("remote", "get-url", remote)).StandardOutput.Trim());
        }

        var main = await repo.RefTargetAsync("refs/heads/main");
        var topic = await repo.RefTargetAsync("refs/heads/topic");
        Assert.Equal(
            [
                $"{main} refs/heads/main",
                $"{topic} refs/heads/topic",
                $"{main} refs/remotes/fork/up/main",
                $"{main} refs/remotes/origin/feature/x",
                $"{main} refs/remotes/origin/main",
                $"{topic} refs/remotes/origin/pr/1",
                $"{topic} refs/remotes/upstream/pr/7",
            ],
            Lines(await repo.GitAsync("for-each-ref", "--format=%(objectname) %(refname)")));
        Assert.Equal(["Work on the topic", "Add the parser", "Initial commit"], Lines(await repo.GitAsync("log", "--format=%s", "topic")));
        Assert.Equal(["origin", "refs/heads/main"], [(await repo.GitAsync("config", "branch.main.remote")).StandardOutput.Trim(), (await repo.GitAsync("config", "branch.main.merge")).StandardOutput.Trim()]);
        Assert.Equal(["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -0"], await StatusLinesAsync(repo));

        // What each remote holds.
        Assert.Equal([$"{main} refs/heads/feature/x", $"{main} refs/heads/main", $"{topic} refs/pull/1/head"], await BareRefsAsync(repo, "origin.git"));
        Assert.Equal([$"{main} refs/heads/main"], await BareRefsAsync(repo, "fork.git"));
        Assert.Equal([$"{main} refs/heads/main", $"{topic} refs/pull/7/head"], await BareRefsAsync(repo, "upstream.git"));
    }

    [Theory]
    [InlineData("fork/up/main", "fork", "refs/heads/main", "fork/main")]
    [InlineData("origin/pr/1", "origin", "refs/heads/pr/1", "origin/pr/1")]
    [InlineData("upstream/pr/7", "upstream", "refs/pull/7/head", "upstream/refs/pull/7/head")]
    [InlineData("origin/feature/x", "origin", "refs/heads/feature/x", "origin/feature/x")]
    public async Task Git_branch_track_makes_of_each_tracking_ref_the_documented_upstream(string tracking, string remote, string merge, string named)
    {
        // 0.10: git maps a tracking ref back by the remote's fetch lines (R2 takes the first
        // candidate, R4 a ref that is no branch); origin/feature/x is the identity.
        using var repo = await Scenarios.RenamingRefspecAsync();

        var result = await repo.GitAsync("branch", "--track", "probe", "refs/remotes/" + tracking);

        Assert.Equal(remote, (await repo.GitAsync("config", "branch.probe.remote")).StandardOutput.Trim());
        Assert.Equal(merge, (await repo.GitAsync("config", "branch.probe.merge")).StandardOutput.Trim());
        if (await GitVersionAsync(repo) == new GitVersion(2, 36, 0))
        {
            Assert.Equal($"branch 'probe' set up to track '{named}'.", (result.StandardOutput + result.StandardError).Trim());
        }
    }

    // ---- The ignored files -------------------------------------------------------------------------

    [Fact]
    public async Task The_ignored_scenario_has_its_commits_stash_and_ignored_files()
    {
        using var repo = await Scenarios.IgnoredAsync();

        Assert.Equal(["Main work", "Stop tracking the secret", "Track the secret", "Initial commit"], Lines(await repo.GitAsync("log", "--format=%s", "main")));
        Assert.Equal(["Add the vendor secret", "Stop tracking the secret"], Lines(await repo.GitAsync("log", "--format=%s", "-2", "vendor")));
        Assert.Equal(["Add the build file", "Stop tracking the secret"], Lines(await repo.GitAsync("log", "--format=%s", "-2", "tools")));
        Assert.Equal("c49cac4", (await repo.HeadAsync())[..7]);
        Assert.Equal(Ignored.SecretTracked, (await repo.GitAsync("show", "main~2:secret.env")).StandardOutput);
        Assert.Equal(Ignored.SecretVendor, (await repo.GitAsync("show", "vendor:secret.env")).StandardOutput);
        Assert.Equal("build\n", (await repo.GitAsync("show", "tools:build")).StandardOutput);
        Assert.False((await repo.GitAllowingFailureAsync("cat-file", "-e", "main:secret.env")).Succeeded, "main tracks secret.env.");
        Assert.False((await repo.GitAllowingFailureAsync("cat-file", "-e", "main:build")).Succeeded, "main tracks build.");

        var stash = Assert.Single(await repo.StashListAsync());
        Assert.Equal("On main: Stash the secret", stash.Message);
        Assert.Equal(Ignored.SecretStashed, (await repo.GitAsync("show", "stash@{0}:secret.env")).StandardOutput);

        Assert.Equal(
            ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -0", "! build/", "! secret.env"],
            Lines(await repo.GitAsync("status", "--porcelain=v2", "--branch", "--ignored")).Where(line => !line.StartsWith("# branch.oid", StringComparison.Ordinal)));
        Assert.Equal(Ignored.SecretLocal, File.ReadAllText(Path.Combine(repo.Path, "secret.env")));
        Assert.Equal(Ignored.BuildOutput, File.ReadAllText(Path.Combine(repo.Path, "build", "out.bin")));

        // vendor-remote is on origin only; vendor still tracks origin/vendor.
        Assert.NotNull(await repo.RefTargetAsync("refs/remotes/origin/vendor-remote"));
        Assert.Null(await repo.RefTargetAsync("refs/heads/vendor-remote"));
        Assert.Equal(await repo.RefTargetAsync("refs/heads/vendor"), await repo.RefTargetAsync("refs/remotes/origin/vendor-remote"));
        Assert.Equal("refs/heads/vendor", (await repo.GitAsync("config", "branch.vendor.merge")).StandardOutput.Trim());
        Assert.Equal("origin", (await repo.GitAsync("config", "branch.vendor.remote")).StandardOutput.Trim());
        Assert.Equal("../origin.git", (await repo.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim());
    }

    [Theory]
    [InlineData("vendor")]
    [InlineData("vendor-remote")]
    [InlineData("tools")]
    public async Task Git_refuses_the_ignored_scenarios_checkouts_only_with_no_overwrite_ignore(string target)
    {
        using var repo = await Scenarios.IgnoredAsync();
        await repo.GitAsync("update-index", "-q", "--refresh");
        string[] switchTo = target == "vendor-remote"
            ? ["switch", "--create", "vendor-remote", "--track", "--no-overwrite-ignore", "refs/remotes/origin/vendor-remote"]
            : ["switch", "--no-guess", "--no-overwrite-ignore", target];

        var refused = await repo.GitAllowingFailureAsync(switchTo);

        Assert.Equal(1, refused.ExitCode);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(Ignored.SecretLocal, File.ReadAllText(Path.Combine(repo.Path, "secret.env")));
        Assert.Equal(Ignored.BuildOutput, File.ReadAllText(Path.Combine(repo.Path, "build", "out.bin")));
        Assert.Null(await repo.RefTargetAsync("refs/heads/vendor-remote"));
        Assert.Contains(target == "tools" ? "build" : "secret.env", refused.StandardError, StringComparison.Ordinal);
        if (await GitVersionAsync(repo) == new GitVersion(2, 36, 0))
        {
            Assert.Equal(
                target == "tools"
                    ? ["error: Updating the following directories would lose untracked files in them:", "\tbuild", "", "Aborting"]
                    : ["error: The following untracked working tree files would be overwritten by checkout:", "\tsecret.env", "Please move or remove them before you switch branches.", "Aborting"],
                refused.StandardError.TrimEnd('\n').Split('\n'));
        }

        // Without the option git checks vendor out and overwrites the file (0.10).
        if (target == "vendor")
        {
            await repo.GitAsync("switch", "--no-guess", "vendor");
            Assert.Equal(Ignored.SecretVendor, File.ReadAllText(Path.Combine(repo.Path, "secret.env")));
        }
    }

    // ---- Git's settings ----------------------------------------------------------------------------

    [Fact]
    public async Task The_settings_repository_has_no_identity_and_is_one_ahead_and_one_behind()
    {
        using var repo = await Scenarios.GitSettingsAsync();

        Assert.Equal("settings", Path.GetFileName(repo.Path));
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("config", "--local", "--get-regexp", @"^user\.")).ExitCode);
        Assert.Equal(["# branch.head main", "# branch.upstream origin/main", "# branch.ab +1 -1"], await StatusLinesAsync(repo));
        Assert.Equal(["Local work", "Initial commit"], Lines(await repo.GitAsync("log", "--format=%s", "main")));
        Assert.Equal(["Remote work", "Initial commit"], Lines(await repo.GitAsync("log", "--format=%s", "origin/main")));
        Assert.Equal("../remotes/settings.git", (await repo.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim());
        Assert.Equal("print(\"run\")\n", File.ReadAllText(Path.Combine(repo.Path, "run.py")));
        Assert.Equal("one\ntwo\n", File.ReadAllText(Path.Combine(repo.Path, "notes.txt")));

        // A copy fetches from its own remote and its commits stay the scenario's.
        Assert.True((await repo.GitAllowingFailureAsync("fetch", "--quiet", "origin")).Succeeded);
        Assert.Equal(["Remote work", "Initial commit"], Lines(await repo.GitAsync("log", "--format=%s", "origin/main")));
    }

    // ---- Helpers --------------------------------------------------------------------------------

    /// <summary><c>git status --porcelain=v2 --branch</c> of a workspace repository, its <c># branch.oid</c> line left out and each entry as its kind, its letters and its path.</summary>
    private static Task<List<string>> StatusAsync(TempWorkspace workspace, string name) => StatusLinesAsync(workspace.Repo(name));

    private static async Task<List<string>> StatusLinesAsync(TempRepo repo) =>
        [.. Lines(await repo.GitAsync("status", "--porcelain=v2", "--branch"))
            .Where(line => !line.StartsWith("# branch.oid", StringComparison.Ordinal))
            .Select(Entry)];

    /// <summary>A status line as "1 M. colours.txt": its kind and letters, then its path (the last field), for ordinary and unmerged entries.</summary>
    private static string Entry(string line)
    {
        if (line.StartsWith('#') || line.StartsWith('?') || line.StartsWith('!'))
        {
            return line;
        }

        var fields = line.Split(' ');
        return $"{fields[0]} {fields[1]} {fields[^1]}";
    }

    /// <summary>Every ref of a workspace repository as "&lt;short sha&gt; &lt;name&gt;", in git's order.</summary>
    private static async Task<string[]> RefsAsync(TempWorkspace workspace, string name) =>
        Lines(await workspace.Repo(name).GitAsync("for-each-ref", "--format=%(objectname:short=7) %(refname)"));

    /// <summary>Every ref of a workspace repository's remote, read in the bare repository itself.</summary>
    private static async Task<string[]> RemoteRefsAsync(TempWorkspace workspace, string name)
    {
        var repo = workspace.Repo(name);
        var command = new GitCommand("for-each-ref", "--format=%(objectname:short=7) %(refname)") { WorkingDirectory = workspace.RemoteOf(name), Environment = repo.Environment };
        return Lines((await repo.Runner.RunAsync(command, TestCancelled)).EnsureSuccess(command));
    }

    /// <summary>Every ref of a bare repository beside <paramref name="repo"/>, full SHAs.</summary>
    private static async Task<string[]> BareRefsAsync(TempRepo repo, string folder)
    {
        var command = new GitCommand("for-each-ref", "--format=%(objectname) %(refname)") { WorkingDirectory = repo.SiblingPath(folder), Environment = repo.Environment };
        return Lines((await repo.Runner.RunAsync(command, TestCancelled)).EnsureSuccess(command));
    }

    private static async Task<GitResult> FetchAsync(TempWorkspace workspace, string name)
    {
        var result = await workspace.Repo(name).GitAllowingFailureAsync("fetch", "--progress", "origin");
        Assert.True(result.Succeeded, $"{name}'s fetch: {result.StandardError}");
        return result;
    }
}
