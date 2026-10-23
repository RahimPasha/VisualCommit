using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

/// <summary>
/// Part W's expectations (Phase6Expectations.W.cs) held against plain git on fresh copies of
/// "workspace", and against each other: the ten repositories as built (refs, statuses, remotes,
/// commits), the scan's checklist, Fetch all's fetches (WF), scratch's missing remote, Pull all's
/// reads and merges (WP) with git's refusals, the pull's undo (DU), W8's commit and file, and W2's
/// copy of notes for Locate…. No app: a scenario that drifted, or an expectation copied wrong,
/// fails here, before a window opens. Git's own texts are asserted word for word on the report's
/// git, 2.36, only; on every git their key lines, exit codes and states (part W's "Git's own texts").
/// </summary>
public sealed class Phase6ExpectationTestsW
{
    /// <summary>W0 in git: each repository's refs, status, remote and commits; old-site absent; the copy's folders.</summary>
    [Fact]
    public async Task The_workspace_is_W0_as_the_report_gives_it()
    {
        using var copy = await Scenarios.WorkspaceAsync();

        Assert.Equal(PartW.Names, copy.Names);
        Assert.Equal(PartW.RootFolders, Directory.GetDirectories(copy.Root).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(PartW.RemoteFolders, Directory.GetDirectories(Path.Combine(copy.Root, "remotes")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.False(Directory.Exists(copy.PathOf("old-site")));
        Assert.Equal([".gitconfig"], Directory.GetFileSystemEntries(Path.Combine(copy.Root, "home")).Select(Path.GetFileName));
        foreach (var name in PartW.WithFolder)
        {
            using var repo = copy.Repo(name);
            await AssertRefsAsync(repo, PartW.W0Refs[name]);
            AssertStatus(name, PartW.W0Status[name], await StatusAsync(repo));
            var remotes = await LinesAsync(repo, "remote");
            if (name == "notes")
            {
                Assert.Empty(remotes);
                Assert.Empty(await ConfigLinesAsync(repo, @"^remote\."));
            }
            else
            {
                Assert.Equal(["origin"], remotes);
                Assert.Equal(
                    [$"remote.origin.url ../remotes/{name}.git", "remote.origin.fetch +refs/heads/*:refs/remotes/origin/*"],
                    await ConfigLinesAsync(repo, @"^remote\."));
                Assert.Equal(["branch.main.remote origin", "branch.main.merge refs/heads/main"], await ConfigLinesAsync(repo, @"^branch\."));
            }
        }

        using (var billing = copy.Repo("billing"))
        {
            Assert.Equal(PartW.BillingMergeHead, File.ReadAllText(Path.Combine(billing.Path, ".git", "MERGE_HEAD")).Trim());
        }

        using (var infra = copy.Repo("infra"))
        {
            Assert.Equal(PartW.InfraInitial, File.ReadAllText(Path.Combine(infra.Path, ".git", "HEAD")).Trim());
        }

        using (var scratch = copy.Repo("scratch"))
        {
            Assert.Equal("ref: refs/heads/experiment", File.ReadAllText(Path.Combine(scratch.Path, ".git", "HEAD")).Trim());
        }

        // The graphs' commits: each short SHA names its commit with the report's subject.
        await AssertCommitsAsync(copy, "api", PartW.A0);
        await AssertCommitsAsync(copy, "web-app", PartW.WebApp0);
        await AssertCommitsAsync(copy, "docs", PartW.Docs0);
        await AssertCommitsAsync(copy, "mobile", PartW.Mobile0);

        // The rows' counts against git's own (scenario facts 2.2's ahead and behind).
        foreach (var (name, ahead, behind) in new[] { ("api", 0, 0), ("web-app", 2, 0), ("docs", 0, 3), ("mobile", 1, 2), ("design-system", 0, 0), ("billing", 0, 0) })
        {
            using var repo = copy.Repo(name);
            Assert.Equal($"{ahead}\t{behind}", (await LinesAsync(repo, "rev-list", "--left-right", "--count", "main...origin/main")).Single());
            var cell = PartW.W0Row(name, copy.PathOf("old-site")).AheadBehind;
            Assert.Equal(ahead == 0 && behind == 0 ? PartW.UpToDate() : PartW.Arrows(ahead, behind), cell);
        }

        Assert.Equal(PartW.DoesNotExist(copy.PathOf("old-site")), PartW.W0Row("old-site", copy.PathOf("old-site")).State.ToolTip);
    }

    /// <summary>
    /// The scan of <c>{root}</c> (scenario facts 2.7): the nine folders at depth 1 that hold a
    /// <c>.git</c>, by relative path ignoring case, each opened at its top; <c>home</c> and
    /// <c>remotes</c>' bare repositories give none.
    /// </summary>
    [Fact]
    public async Task The_scan_finds_the_nine_by_relative_path()
    {
        using var copy = await Scenarios.WorkspaceAsync();

        var found = Directory.GetDirectories(copy.Root)
            .Where(folder => Directory.Exists(Path.Combine(folder, ".git")) || File.Exists(Path.Combine(folder, ".git")))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.Equal(PartW.Checklist, found);
        Assert.Equal(PartW.Checklist, PartW.WaOrder);
        Assert.DoesNotContain(Directory.GetDirectories(Path.Combine(copy.Root, "remotes")), folder => Directory.Exists(Path.Combine(folder, ".git")));
        foreach (var name in found)
        {
            using var repo = copy.Repo(name);
            var top = (await LinesAsync(repo, "rev-parse", "--show-toplevel")).Single();
            Assert.Equal(Path.GetFullPath(await WorkingTreeAsync(repo)), Path.GetFullPath(top));
            Assert.Equal(string.Empty, (await RunAsync(repo, "rev-parse", "--show-prefix")).StandardOutput.Trim());
        }

        Assert.Equal(PartW.WaItemToolTip, string.Join(", ", PartW.WaOrder.Take(8)) + ", …");
        Assert.Equal(PartW.Wb10Order, PartW.Wb8Order.Concat(["web-app", "docs"]));
        Assert.Equal(PartW.WrOrder, PartW.Names.Where(name => name != "old-site"));
        Assert.Equal(PartW.WmOrder, new[] { "api", "docs", "web-app" }.Concat(PartW.WrOrder.Skip(3)));
    }

    /// <summary>
    /// Fetch all's eight fetches on the file remotes (scenario facts 2.5): each exits 0; api,
    /// web-app, billing, infra and scratch print nothing; docs, mobile and design-system their
    /// lines (word for word on 2.36, their ref line on every git); WF's refs and counts after.
    /// </summary>
    [Fact]
    public async Task Fetch_all_gives_WF()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        var reportGit = await IsReportGitAsync();

        foreach (var name in PartW.WithRemote)
        {
            using var repo = copy.Repo(name);
            var fetch = await RunAsync(repo, Arguments(PartW.FetchCommand));
            Assert.True(fetch.ExitCode == 0, $"{name}: \"{PartW.FetchCommand}\" exited {fetch.ExitCode}.\n{fetch.StandardError}");
            Assert.Equal(string.Empty, fetch.StandardOutput);
            var lines = Lines(fetch.StandardError).Where(line => !line.Contains('%', StringComparison.Ordinal)).ToList();
            if (PartW.FetchLines.TryGetValue(name, out var expected))
            {
                Assert.Contains(PartW.FetchedRefLine[name], fetch.StandardError.Split(['\r', '\n']));
                if (reportGit)
                {
                    Assert.Equal(expected, lines);
                }
            }
            else if (reportGit)
            {
                Assert.Empty(Lines(fetch.StandardError));
            }

            await AssertRefsAsync(repo, PartW.WfRefs[name]);
        }

        foreach (var (name, cell) in PartW.WfAheadBehind)
        {
            using var repo = copy.Repo(name);
            var counts = (await LinesAsync(repo, "rev-list", "--left-right", "--count", "main...origin/main")).Single().Split('\t');
            Assert.Equal(PartW.Arrows(int.Parse(counts[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(counts[1], System.Globalization.CultureInfo.InvariantCulture)), cell);
        }

        using (var designSystem = copy.Repo("design-system"))
        {
            AssertStatus("design-system", ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -3", .. PartW.W0Status["design-system"][3..]], await StatusAsync(designSystem));
        }

        using var notes = copy.Repo("notes");
        Assert.Empty(await LinesAsync(notes, "remote"));
    }

    /// <summary>scratch's missing remote (W6): exit 128, the key line on every git, the four lines word for word on 2.36.</summary>
    [Fact]
    public async Task Scratchs_missing_remote_fails_as_W6_shows()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var scratch = copy.Repo("scratch");
        await scratch.GitAsync("remote", "set-url", "origin", PartW.MissingRemote);

        var fetch = await RunAsync(scratch, Arguments(PartW.FetchCommand));
        Assert.Equal(128, fetch.ExitCode);
        Assert.Contains(PartW.MissingRemoteKey, Lines(fetch.StandardError));
        if (await IsReportGitAsync())
        {
            Assert.Equal(PartW.MissingRemoteLines, GitOutputCleaner.Clean(fetch.StandardError, fetch.StandardOutput).Split('\n'));
        }

        await AssertRefsAsync(scratch, PartW.W0Refs["scratch"]);
        Assert.Equal(PartW.CouldNotFetchOrigin, "error: Could not fetch " + "origin");
    }

    /// <summary>
    /// Pull all's five pulls (scenario facts 2.6), each as the app runs it: the fetch, 9.10's read of
    /// the added paths, then git's merge with <c>--no-overwrite-ignore</c>: api and web-app already
    /// up to date, docs fast-forwarded, mobile refused under <c>pull.ff = only</c>, design-system
    /// refused for its local change to spacing.txt (its <c>Updating</c> line on standard output);
    /// WP's refs, files and statuses after.
    /// </summary>
    [Fact]
    public async Task Pull_all_gives_WP()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        var reportGit = await IsReportGitAsync();
        using (var mobile = copy.Repo("mobile"))
        {
            await mobile.GitAsync("config", "pull.ff", "only");
            Assert.Equal(["only"], await LinesAsync(mobile, Arguments(PartW.PullFastForwardRead)));
        }

        foreach (var name in new[] { "api", "web-app", "docs", "mobile", "design-system" })
        {
            using var repo = copy.Repo(name);
            var fetch = await RunAsync(repo, Arguments(PartW.FetchCommand));
            Assert.True(fetch.ExitCode == 0, $"{name}'s fetch exited {fetch.ExitCode}.\n{fetch.StandardError}");
            var added = await RunAsync(repo, Arguments(PartW.PullAddedRead));
            Assert.Equal(0, added.ExitCode);
            Assert.Equal(PartW.AddedByPull[name], added.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries));
            Assert.All(PartW.AddedByPull[name], path => Assert.False(File.Exists(Path.Combine(repo.Path, path)), $"{name}: {path} is on disk."));
            var merge = await RunAsync(repo, Arguments(name == "mobile" ? PartW.FastForwardOnlyCommand : PartW.MergeCommand), D72Dates);
            switch (name)
            {
                case "api" or "web-app":
                    Assert.True(merge.ExitCode == 0, $"{name}'s merge exited {merge.ExitCode}.\n{merge.StandardError}");
                    Assert.Equal([PartW.AlreadyUpToDate], Lines(merge.StandardOutput));
                    break;
                case "docs":
                    Assert.True(merge.ExitCode == 0, $"docs' merge exited {merge.ExitCode}.\n{merge.StandardError}");
                    Assert.Contains("Fast-forward", Lines(merge.StandardOutput));
                    if (reportGit)
                    {
                        Assert.Equal(PartW.DocsFastForwardLines, merge.StandardOutput.TrimEnd('\n').Split('\n'));
                    }

                    break;
                case "mobile":
                    Assert.Equal(128, merge.ExitCode);
                    Assert.Contains(PartW.NotFastForwardKey, Lines(merge.StandardError));
                    if (reportGit)
                    {
                        Assert.Equal(PartW.NotFastForwardLines, GitOutputCleaner.Clean(merge.StandardError, merge.StandardOutput).Split('\n'));
                    }

                    Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")));
                    break;
                default:
                    Assert.Equal(1, merge.ExitCode);
                    Assert.Contains(PartW.LocalChangesKey, Lines(merge.StandardError));
                    Assert.Contains(PartW.DesignSystemUpdating, Lines(merge.StandardOutput));
                    Assert.DoesNotContain(PartW.DesignSystemUpdating, Lines(merge.StandardError));
                    if (reportGit)
                    {
                        Assert.Equal(PartW.LocalChangesLines, GitOutputCleaner.Clean(merge.StandardError, merge.StandardOutput).Split('\n'));
                    }

                    break;
            }

            await AssertRefsAsync(repo, PartW.WpRefs[name]);
        }

        using (var docs = copy.Repo("docs"))
        {
            Assert.Equal(PartW.DocsPulledFiles, WorkingFiles(docs));
            AssertStatus("docs", ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -0"], await StatusAsync(docs));
            await AssertCommitsAsync(copy, "docs", PartW.DocsPulled);
        }

        using (var designSystem = copy.Repo("design-system"))
        {
            AssertStatus("design-system", ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -3", .. PartW.W0Status["design-system"][3..]], await StatusAsync(designSystem));
        }

        using (var mobile = copy.Repo("mobile"))
        {
            AssertStatus("mobile", ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +1 -3"], await StatusAsync(mobile));
            await AssertCommitsAsync(copy, "mobile", PartW.MobileFetched);
        }

        // The five left out did not run: nothing changed there (each ref as built).
        foreach (var name in new[] { "billing", "infra", "scratch", "notes" })
        {
            using var repo = copy.Repo(name);
            await AssertRefsAsync(repo, PartW.W0Refs[name]);
        }
    }

    /// <summary>
    /// The undo of docs' pull (W7 step 6, phase 5's Keep move): the refresh, the guard's read of
    /// added paths (none), <c>read-tree -m -u</c> and <c>update-ref</c> with the undo's message, as
    /// the call log renders them; DU after: main back at Initial commit, 4 behind, README.md and
    /// guide.md only, the reflogs' newest entry the undo's.
    /// </summary>
    [Fact]
    public async Task The_pulls_undo_gives_DU()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var docs = copy.Repo("docs");
        Assert.Equal(0, (await RunAsync(docs, Arguments(PartW.FetchCommand))).ExitCode);
        Assert.Equal(0, (await RunAsync(docs, Arguments(PartW.MergeCommand), D72Dates)).ExitCode);
        await AssertRefsAsync(docs, PartW.WpRefs["docs"]);

        Assert.Equal(0, (await RunAsync(docs, Arguments(PartW.UpdateIndexRefresh))).ExitCode);
        var guard = await RunAsync(docs, "diff-tree", "-r", "-z", "--no-renames", "--name-only", "--diff-filter=A", PartW.DocsChangelog, PartW.DocsInitial);
        Assert.Equal((0, string.Empty), (guard.ExitCode, guard.StandardOutput));
        string[] readTree = ["read-tree", "-m", "-u", PartW.DocsChangelog, PartW.DocsInitial];
        string[] updateRef = ["update-ref", "-m", PartW.UndoReflogMessage, "refs/heads/main", PartW.DocsInitial, PartW.DocsChangelog];
        Assert.Equal(PartW.UndoReadTree, new GitCommand(readTree).DisplayText);
        Assert.Equal(PartW.UndoUpdateRef, new GitCommand(updateRef).DisplayText);
        Assert.Equal(0, (await RunAsync(docs, readTree, D72Dates)).ExitCode);
        Assert.Equal(0, (await RunAsync(docs, updateRef, D72Dates)).ExitCode);

        await AssertRefsAsync(docs, PartW.UndoneRefs["docs"]);
        AssertStatus("docs", ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -4"], await StatusAsync(docs));
        Assert.Equal(PartW.DocsUndoneFiles, WorkingFiles(docs));
        Assert.Equal(PartW.UndoReflogMessage, (await LinesAsync(docs, "log", "-g", "-1", "--format=%gs", "HEAD")).Single());
        Assert.Equal(PartW.UndoReflogMessage, (await LinesAsync(docs, "log", "-g", "-1", "--format=%gs", "refs/heads/main")).Single());
        await AssertCommitsAsync(copy, "docs", PartW.DocsUndone);
        Assert.Equal("4 commits behind origin/main", PartW.Arrows(0, 4).ToolTip);
    }

    /// <summary>W8's commit (WD17): contact.html staged by Stage all and committed with D72's dates and the copy's identity: <c>092116c</c>, then 3 ahead and clean.</summary>
    [Fact]
    public async Task W8s_commit_is_092116c()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        Assert.Equal(PartW.ContactCommit, await CommitContactPageAsync(copy));
    }

    /// <summary>W2's Locate repository (WD7): notes copied file by file to <c>archive/old-site</c> opens there, on main at Add the reading list, with no remote, clean.</summary>
    [Fact]
    public async Task The_copy_of_notes_reads_as_WL()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        var located = PartW.LocatedFolder(copy.Root);
        CopyFiles(copy.PathOf("notes"), located);

        using var repo = copy.RepoAt(located);
        var open = await RunAsync(repo, Arguments(PartW.OpenCommand));
        Assert.Equal(0, open.ExitCode);
        var lines = Lines(open.StandardOutput);
        Assert.Equal(4, lines.Count);
        Assert.Equal("true", lines[0]);
        Assert.Equal(Path.GetFullPath(await WorkingTreeAsync(repo)), Path.GetFullPath(lines[1]));
        Assert.Equal(".git", Path.GetFileName(lines[2]));
        Assert.Equal(".git", Path.GetFileName(lines[3].TrimEnd('/', '\\')));
        Assert.Equal(string.Empty, (await RunAsync(repo, "rev-parse", "--show-prefix")).StandardOutput.Trim());
        await AssertRefsAsync(repo, PartW.W0Refs["notes"]);
        Assert.Empty(await LinesAsync(repo, "remote"));
        AssertStatus("old-site", PartW.W0Status["notes"], await StatusAsync(repo));
        Assert.Equal(PartW.Located, PartW.W0Row("notes", copy.PathOf("old-site")) with { Name = "old-site" });
    }

    /// <summary>W8's file in scratch (WD18): <c>todo.txt</c> untracked, the row's <c>1 untracked</c>.</summary>
    [Fact]
    public async Task Scratch_with_todo_has_one_untracked_file()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var scratch = copy.Repo("scratch");
        File.WriteAllText(Path.Combine(scratch.Path, PartW.TodoFile), PartW.TodoText);

        AssertStatus("scratch", ["# branch.head experiment", "? todo.txt"], await StatusAsync(scratch));
    }

    /// <summary>The report's counts: 45 pictures of part W, each a picture name of the report and none twice; W4's holds; the bar's values (WD13).</summary>
    [Fact]
    public void The_pictures_and_counts_are_the_reports()
    {
        Assert.Equal(45, PartW.Pictures.Length);
        Assert.Equal(PartW.Pictures.Length, PartW.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PartW.Pictures, picture => Assert.Matches(@"^w\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$", picture));
        Assert.Equal((62, 12, 75), (PartW.Percent(5, 8), PartW.Percent(1, 8), PartW.Percent(6, 8)));
        Assert.Equal("Fetching 8 repositories… 5 done", PartW.Fetching(8, 5));
        Assert.Equal("Fetching 8 repositories…", PartW.Fetching(8));
        Assert.Equal("Cancelling: waiting for 1 repository…", PartW.Cancelling(1));
        Assert.Equal(267, MenuHeight(8, 3));
        Assert.Equal(293, MenuHeight(9, 3));
        Assert.Equal(119, MenuHeight(3, 1));
        Assert.Equal((8, 3), (PartW.ReadyRowMenu("web-app").Count(line => line != "—") - 1, PartW.ReadyRowMenu("web-app").Count(line => line == "—")));
        Assert.Equal((9, 3), (PartW.GoneFolderMenu("x").Count(line => line != "—") - 1, PartW.GoneFolderMenu("x").Count(line => line == "—")));
        Assert.Equal((9, 3), (PartW.ReadyRowMenu("scratch", errorDetails: true).Count(line => line != "—") - 1, PartW.ReadyRowMenu("scratch", errorDetails: true).Count(line => line == "—")));
        Assert.Equal((68, 78), (PartW.SectionHeight(0), PartW.SectionHeight(1)));
        Assert.Equal("1 commit ahead of and 2 behind origin/main", PartW.Arrows(1, 2).ToolTip);
        Assert.Equal("↑ 1 ↓ 3", PartW.Arrows(1, 3).Text);
        Assert.Equal("Sign in to 127.0.0.1:50123", PartW.SignInTitle(50123));
    }

    // ---- Helpers ----

    /// <summary>D72's dates for what the app writes (2026-01-02 09:00 UTC).</summary>
    private static readonly IReadOnlyDictionary<string, string?> D72Dates = new Dictionary<string, string?>
    {
        ["GIT_AUTHOR_DATE"] = "2026-01-02T09:00:00+00:00",
        ["GIT_COMMITTER_DATE"] = "2026-01-02T09:00:00+00:00",
    };

    /// <summary>A command's arguments from its text as the call log renders it (part W's have no argument with a space but the undo's message, which is never split here).</summary>
    private static string[] Arguments(string commandText)
    {
        Assert.StartsWith("git ", commandText, StringComparison.Ordinal);
        return commandText["git ".Length..].Split(' ');
    }

    /// <summary>Runs git in <paramref name="repo"/> as the app runs a read: with <c>GIT_OPTIONAL_LOCKS=0</c> (and <paramref name="extra"/>), its result whatever its exit.</summary>
    private static Task<GitResult> RunAsync(TempRepo repo, string[] arguments, IReadOnlyDictionary<string, string?>? extra = null)
    {
        var environment = new Dictionary<string, string?> { ["GIT_OPTIONAL_LOCKS"] = "0", ["GIT_LITERAL_PATHSPECS"] = "1" };
        foreach (var (key, value) in extra ?? new Dictionary<string, string?>())
        {
            environment[key] = value;
        }

        return repo.GitWithEnvironmentAllowingFailureAsync(environment, arguments);
    }

    private static Task<GitResult> RunAsync(TempRepo repo, params string[] arguments) => RunAsync(repo, arguments, null);

    /// <summary>Plain git's standard output as lines (exit 0 asserted), without empty lines.</summary>
    private static async Task<List<string>> LinesAsync(TempRepo repo, params string[] arguments)
    {
        var result = await RunAsync(repo, arguments, null);
        Assert.True(result.ExitCode == 0, $"git {string.Join(' ', arguments)} exited {result.ExitCode}.\n{result.StandardError}");
        return Lines(result.StandardOutput);
    }

    /// <summary>The configuration lines matching <paramref name="pattern"/> (<c>git config --get-regexp</c>), none when exit 1.</summary>
    private static async Task<List<string>> ConfigLinesAsync(TempRepo repo, string pattern)
    {
        var result = await RunAsync(repo, "config", "--get-regexp", pattern);
        return result.ExitCode == 1 ? [] : Lines(result.StandardOutput);
    }

    /// <summary>The lines of a stream, split at every carriage return and line feed, without trailing spaces and empty lines.</summary>
    private static List<string> Lines(string output) =>
        [.. output.Split(['\r', '\n']).Select(line => line.TrimEnd()).Where(line => line.Length > 0)];

    private static async Task AssertRefsAsync(TempRepo repo, string[] expected) =>
        Assert.Equal(expected, await LinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)"));

    /// <summary><c>git status --porcelain=v2 --branch</c> with <c>GIT_OPTIONAL_LOCKS=0</c>, less <c>branch.oid</c>.</summary>
    private static async Task<List<string>> StatusAsync(TempRepo repo) =>
        [.. (await LinesAsync(repo, "status", "--porcelain=v2", "--branch")).Where(line => !line.StartsWith("# branch.oid ", StringComparison.Ordinal))];

    /// <summary>A status as the report writes it (<see cref="PartW.W0Status"/>): "…" stands for the modes and object names of a changed file's line.</summary>
    private static void AssertStatus(string name, IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        var matches = expected.Count == actual.Count && expected.Zip(actual).All(pair =>
            pair.First.Split(" … ") is [var start, var end]
                ? pair.Second.StartsWith(start + " ", StringComparison.Ordinal) && pair.Second.EndsWith(" " + end, StringComparison.Ordinal)
                : pair.First == pair.Second);
        Assert.True(matches, $"{name}'s status is not the report's.\nExpected:\n  {string.Join("\n  ", expected)}\nActual:\n  {string.Join("\n  ", actual)}");
    }

    /// <summary>Each row of a graph names its commit: its short SHA's subject is the row's message.</summary>
    private static async Task AssertCommitsAsync(TempWorkspace copy, string name, (string Message, int Lane, int Colour, string[] Labels, string Sha)[] rows)
    {
        using var repo = copy.Repo(name);
        foreach (var row in rows)
        {
            Assert.Equal(row.Message, (await LinesAsync(repo, "log", "-1", "--format=%s", row.Sha)).Single());
        }

        // The rows are the whole history the graph shows (every branch, every remote branch, every tag), newest first.
        var all = await LinesAsync(repo, "log", "--format=%h", "--abbrev=7", "--branches", "--remotes", "--tags", "HEAD");
        Assert.Equal(rows.Select(row => row.Sha).Order(StringComparer.Ordinal), all.Order(StringComparer.Ordinal));
    }

    /// <summary>The working tree's files outside <c>.git</c> with their texts, in ordinal order.</summary>
    private static (string Path, string Text)[] WorkingFiles(TempRepo repo) =>
    [
        .. Directory.EnumerateFiles(repo.Path, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(repo.Path, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0].Equals(".git", StringComparison.Ordinal))
            .Select(file => (Path.GetRelativePath(repo.Path, file).Replace('\\', '/'), File.ReadAllText(file)))
            .OrderBy(file => file.Item1, StringComparer.Ordinal),
    ];

    /// <summary>The working tree's folder as git names it, full, without a trailing separator.</summary>
    private static async Task<string> WorkingTreeAsync(TempRepo repo) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath((await LinesAsync(repo, "rev-parse", "--show-toplevel")).Single()));

    /// <summary>W8's commit as the app makes it (WD17): contact.html staged by <c>git add -A -- .</c>, the summary on standard input of <c>git commit --file=- --cleanup=whitespace</c>, D72's dates; its SHA, with WC's graph and status after.</summary>
    private static async Task<string> CommitContactPageAsync(TempWorkspace copy)
    {
        using var webApp = copy.Repo("web-app");
        File.WriteAllText(Path.Combine(webApp.Path, PartW.ContactFile), PartW.ContactText);
        Assert.Equal(0, (await RunAsync(webApp, ["add", "-A", "--", "."], D72Dates)).ExitCode);
        var environment = new Dictionary<string, string?>(webApp.Environment)
        {
            ["GIT_AUTHOR_DATE"] = D72Dates["GIT_AUTHOR_DATE"],
            ["GIT_COMMITTER_DATE"] = D72Dates["GIT_COMMITTER_DATE"],
        };
        var command = new GitCommand("-c", "core.abbrev=no", "commit", "--file=-", "--cleanup=whitespace")
        {
            WorkingDirectory = webApp.Path,
            Environment = environment,
            StandardInput = PartW.ContactSummary,
        };
        var result = await webApp.Runner.RunAsync(command);
        Assert.True(result.ExitCode == 0, $"The commit exited {result.ExitCode}.\n{result.StandardError}");
        var sha = (await LinesAsync(webApp, "rev-parse", "HEAD")).Single();
        Assert.StartsWith(PartW.ContactTree, (await LinesAsync(webApp, "rev-parse", "HEAD^{tree}")).Single(), StringComparison.Ordinal);
        Assert.Equal(PartW.WebAppStyle, (await LinesAsync(webApp, "rev-parse", "HEAD^")).Single());
        Assert.Equal([$"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}> {TempRepo.AuthorName} <{TempRepo.AuthorEmail}> {PartW.ContactSummary}"], await LinesAsync(webApp, "log", "-1", "--format=%an <%ae> %cn <%ce> %B"));
        AssertStatus("web-app", ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +3 -0"], await StatusAsync(webApp));
        await AssertCommitsAsync(copy, "web-app", PartW.WebAppCommitted);
        return sha;
    }

    /// <summary>Copies <paramref name="from"/> file by file to <paramref name="to"/> (W2's "file by file", WD7): no git process.</summary>
    internal static void CopyFiles(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        foreach (var folder in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, folder)));
        }
    }
}
