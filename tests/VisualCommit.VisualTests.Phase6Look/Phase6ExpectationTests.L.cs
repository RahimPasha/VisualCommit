// Tests of Phase6Expectations.L.cs's data and helpers (the real-window pass leaves this file out:
// its name does not match Phase6Expectations*.cs): part L's expectations held against each other,
// against 13.3's arithmetic and 11.10's contrast, and against plain git on fresh copies of the
// scenarios part L uses. No window: an expectation copied wrong, or a scenario that drifted, fails
// here before a check opens one. Git's own texts are asserted word for word on 2.36 and 2.55 only.
using System.Globalization;
using Avalonia.Media;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Git;
using VisualCommit.Testing;
using VisualCommit.VisualTests.Phase6;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6Look;

/// <summary>Part L's expectations pinned without a window (as the other parts' <c>Phase6ExpectationTests.&lt;part&gt;</c>).</summary>
public sealed class Phase6ExpectationTestsL : Phase6Checks
{
    /// <summary>LD24: 68 pictures, each of the report's form and unique; L34's four captures are four of them.</summary>
    [Fact]
    public void The_pictures_are_the_reports_68()
    {
        Assert.Equal(68, PartL.Pictures.Count);
        Assert.Equal(PartL.Pictures.Count, PartL.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PartL.Pictures, name => Assert.Matches(PictureName(), name));
        Assert.Equal(29, PartL.Pairs.Count);
        Assert.Equal(2, PartL.LongFolderPairs.Count);
        Assert.Equal(5, PartL.StripPictures.Count);
        Assert.All(PartL.RealWindowCaptures, name => Assert.Contains(name, PartL.Pictures));
        Assert.All(PartL.Pairs.Concat(PartL.LongFolderPairs), name => Assert.StartsWith("l", name, StringComparison.Ordinal));
    }

    /// <summary>L27's blends: the pill's point (0.85 × lane 3 over the selected row) and the backdrop over the graph area, each variant.</summary>
    [Fact]
    public void The_blends_are_the_reports()
    {
        AssertNear(PartL.PillPoint(dark: true), PartL.Blend(Dark.Lane(3), 0.85, Dark.Selection));
        AssertNear(PartL.PillPoint(dark: false), PartL.Blend(Light.Lane(3), 0.85, Light.Selection));
        AssertNear(PartL.BackdropPoint(dark: true), PartL.OverBackdrop(Dark.Backdrop, Dark.Window));
        AssertNear(PartL.BackdropPoint(dark: false), PartL.OverBackdrop(Light.Backdrop, Light.Window));
        Assert.Equal(Color.Parse("#C95967"), PartL.PillPoint(dark: true));
        Assert.Equal(Color.Parse("#999999"), PartL.BackdropPoint(dark: false));
    }

    /// <summary>The menus' heights by phase 3's 4a: 571 (19 items, 5 separators), 441 (14, 5), 293 (9, 3).</summary>
    [Fact]
    public void The_menus_heights_follow_from_their_lines()
    {
        static (int Items, int Separators) Count(IReadOnlyList<string> lines) =>
            (lines.Skip(1).Count(line => line != "—"), lines.Count(line => line == "—"));
        Assert.Equal((19, 5), Count(PartL.AlphaMenu));
        Assert.Equal((14, 5), Count(PartL.MainMenu));
        Assert.Equal((9, 3), Count(PartL.OldSiteMenu("x")));
        Assert.Equal(PartL.AlphaMenuHeight, MenuHeight(19, 5));
        Assert.Equal(PartL.MainMenuHeight, MenuHeight(14, 5));
        Assert.Equal(PartL.OldSiteMenuHeight, MenuHeight(9, 3));
    }

    /// <summary>L33's table is 13.3's arithmetic over the scenario facts' natural widths (Web 26.9 px, design-system 87.9, the rest at most 69).</summary>
    [Fact]
    public void The_strips_widths_are_13_3s_rule()
    {
        var web = PartL.NaturalWidth(26.9, dashboard: true);
        var designSystem = PartL.NaturalWidth(87.9, dashboard: false);
        Assert.Equal(120, web);
        Assert.Equal(138.9, designSystem, 3);
        Assert.Equal(120, PartL.NaturalWidth(53.2, dashboard: false));
        var eight = Enumerable.Repeat(120d, 8).ToList();
        Assert.Equal(PartL.Ts8, PartL.TabWidths(eight, PartL.Room(PartL.NarrowSize.Width)));
        Assert.Equal(PartL.Ts8, PartL.TabWidths(eight, PartL.Room(PartL.Size.Width)));
        var room = PartL.Room(PartL.Size.Width);
        Assert.Equal(1062, room);
        List<double> nine = [.. eight, designSystem];
        Assert.Equal(PartL.Ts9, PartL.TabWidths(nine, room));
        List<double> ten = [.. nine, 120];
        Assert.Equal(PartL.Ts10, PartL.TabWidths(ten, room));
        List<double> eleven = [.. ten, 120];
        Assert.Equal(PartL.Ts11, PartL.TabWidths(eleven, room));
        List<double> twelve = [.. eleven, 120];
        Assert.Equal(PartL.Ts12, PartL.TabWidths(twelve, room));
        List<double> thirteen = [.. twelve, 120];
        Assert.Equal(PartL.Ts13, PartL.TabWidths(thirteen, room));
        List<double> fourteen = [.. thirteen, 120];
        Assert.Equal(PartL.Ts14, PartL.TabWidths(fourteen, room));
        foreach (var state in new[] { PartL.Ts9, PartL.Ts10, PartL.Ts11, PartL.Ts12, PartL.Ts13 })
        {
            Assert.Equal(room, state.Sum());
        }

        Assert.Equal(PartL.Ts14Offset, PartL.OffsetFor(PartL.Ts14, room, 13));
        Assert.Equal(0, PartL.OffsetFor(PartL.Ts14, room, 0));
        Assert.Equal(PartL.Ts8.Sum(), PartL.PlusAt1000.Left - 2);
        Assert.Equal(room + 2, PartL.PlusAt1100Shrunk.Left);
    }

    /// <summary>The texts the checks build: the workspace item's tooltip (13.1), the port's substitution (D97), the welcome page in reading order.</summary>
    [Fact]
    public void The_built_texts_are_the_reports()
    {
        Assert.Equal(PartL.WebItemToolTip, PartL.WorkspaceToolTip(PartL.WebNames));
        Assert.Equal(TempWorkspace.DashboardOrder, PartL.WebNames);
        Assert.Equal("Sign in to 127.0.0.1:50000", WithPort(PartL.CredentialsTitle, 50000, PartL.LookServerPort));
        Assert.Equal(PartL.CredentialsText, WithPort(PartL.CredentialsText, PartL.LookServerPort, PartL.LookServerPort));
        Assert.Equal(PartL.CredentialsTitle, WithPort(PartL.CredentialsTitle, 50000));
        Assert.Equal(47394, PartL.LookServerPort);
        Assert.Equal(
            ["No repository open", "Open, clone or init a repository to see its history.", "Open", "Clone", "Init", "Workspaces", "New workspace…", "Web", "10 repositories", "Recent repositories", "graph", "G", "linear", "L"],
            PartL.WelcomeTexts([("graph", "G"), ("linear", "L")]));
        Assert.Equal(["1 repository", "No repositories", "10 repositories"], new[] { 1, 0, 10 }.Select(PartL.Count));
        Assert.Equal(78, PartL.SectionHeight(1));
        Assert.Equal(36, PartL.HeadSelectors.Count);
        Assert.DoesNotContain("HEAD@{5}", PartL.HeadSelectors);
        Assert.All(PartL.HeadLost, selector => Assert.Contains(selector, PartL.HeadSelectors));
        Assert.Equal(15, PartL.BackupRows.Count);
        Assert.Equal(PartL.Pairs.Count, PartL.Pairs.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// 11.10's and 11.13's light colours reach 4.5:1 where part L pictures their text (D198): the
    /// warning M on a selected row, the success A on the panel, the danger item under the pointer,
    /// the danger button's white label; white label text on light lanes 1 and 2.
    /// </summary>
    [Fact]
    public void The_light_colours_reach_their_contrast()
    {
        Assert.InRange(Contrast.Ratio(Light.Warning, Light.Selection), 4.5, 21);
        Assert.InRange(Contrast.Ratio(Light.Warning, Light.Chrome), 4.5, 21);
        Assert.InRange(Contrast.Ratio(Light.Success, Light.Panel), 4.5, 21);
        Assert.InRange(Contrast.Ratio(Light.Danger, Light.Hover), 4.5, 21);
        Assert.InRange(Contrast.Ratio(Light.DangerText, Light.Danger), 4.5, 21);
        Assert.InRange(Contrast.Ratio(Light.LabelText, Light.Lane(1)), 4.5, 21);
        Assert.InRange(Contrast.Ratio(Light.LabelText, Light.Lane(2)), 4.5, 21);
        Assert.Equal(Colors.White, Dark.DangerText);
        Assert.Equal(Colors.White, Light.DangerText);
    }

    /// <summary>
    /// OPS0 in part L, in git: the short SHAs of the commits the checks name, feature/alpha not in
    /// HEAD (its deletion's second sentence), origin's two heads, and the toolbar's Fetch, which
    /// prints nothing, exits 0 and changes no ref.
    /// </summary>
    [Fact]
    public async Task Ops_is_OPS0_and_its_fetch_changes_nothing()
    {
        using var repo = await Scenarios.OperationsAsync();
        var log = await LinesAsync(repo, "log", "--format=%h %s", "--all", "--date-order");
        foreach (var row in PartL.Ops0.Where(row => !row.Stash))
        {
            Assert.Contains($"{row.ShortSha} {row.Message}", log);
        }

        Assert.Equal(1, (await repo.GitAllowingFailureAsync("merge-base", "--is-ancestor", "refs/heads/feature/alpha", "HEAD")).ExitCode);
        Assert.Equal(PartL.OpsRemoteHeads, (await LinesAsync(repo, "ls-remote", "--heads", "origin")).Select(line => line.Split('\t')[1]));
        var refs = await LinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)");
        var fetch = await repo.GitAllowingFailureAsync("fetch", "--progress", "origin");
        Assert.Equal(0, fetch.ExitCode);
        Assert.Equal(string.Empty, fetch.StandardOutput.Trim() + fetch.StandardError.Trim());
        Assert.Equal(refs, await LinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)"));
        Assert.Empty(await LinesAsync(repo, "for-each-ref", "refs/visualcommit/backup"));
        Assert.Equal("Test Author", (await LinesAsync(repo, "config", "--local", "--get", "user.name")).Single());
    }

    /// <summary>
    /// H0t, H0d and HS1 in git: docs/todo.md untracked beside src/Reader.cs's change, its two blobs,
    /// the discard's snapshot commit as plain git makes it (0bd36b6), and the soloed log's twelve commits.
    /// </summary>
    [Fact]
    public async Task History_with_todo_is_H0t_and_its_snapshot_is_0bd36b6()
    {
        using var repo = await Scenarios.HistoryAsync();
        repo.WriteFile(PartL.TodoPath, PartL.Todo);
        Assert.Equal(PartL.H0tStatus, await LinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all"));
        Assert.Equal(PartL.ReaderCommittedBlob, (await LinesAsync(repo, "rev-parse", "HEAD:" + PartL.ReaderPath)).Single());
        Assert.Equal(PartL.ReaderWorkingBlob, (await LinesAsync(repo, "hash-object", "-w", "--", PartL.ReaderPath)).Single());

        using var index = new TempDirectory("index");
        var environment = new Dictionary<string, string?>
        {
            ["GIT_INDEX_FILE"] = Path.Combine(index.Path, "index"),
            ["GIT_AUTHOR_NAME"] = "VisualCommit",
            ["GIT_AUTHOR_EMAIL"] = "snapshot@visualcommit.invalid",
            ["GIT_COMMITTER_NAME"] = "VisualCommit",
            ["GIT_COMMITTER_EMAIL"] = "snapshot@visualcommit.invalid",
            ["GIT_AUTHOR_DATE"] = GitIsolation.AppCommitDates["GIT_AUTHOR_DATE"],
            ["GIT_COMMITTER_DATE"] = GitIsolation.AppCommitDates["GIT_COMMITTER_DATE"],
        };
        await repo.GitWithEnvironmentAsync(environment, "read-tree", "HEAD");
        await repo.GitWithEnvironmentAsync(environment, "update-index", "--add", "--cacheinfo", $"100644,{PartL.ReaderWorkingBlob},{PartL.ReaderPath}");
        var tree = (await repo.GitWithEnvironmentAsync(environment, "write-tree")).StandardOutput.Trim();
        var head = (await LinesAsync(repo, "rev-parse", "HEAD")).Single();
        var snapshot = (await repo.GitWithEnvironmentAsync(environment, "commit-tree", tree, "-p", head, "-m", PartL.DiscardSnapshotSubject, "-m", PartL.ReaderPath)).StandardOutput.Trim();
        Assert.Equal(PartL.DiscardSnapshot, snapshot);

        var solo = await LinesAsync(repo, "log", "--date-order", "--format=%h %s", "--ignore-missing", "refs/heads/main", "refs/remotes/origin/main", "HEAD", "--");
        Assert.Equal(PartL.Hs1.Select(row => $"{row.ShortSha} {row.Message}"), solo);
    }

    /// <summary>"Web" as L30 reads it: the dashboard's order, and the arrows' counts of git's own (scenario facts 2.2).</summary>
    [Fact]
    public async Task Web_has_the_rows_counts()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        var rows = PartL.DashboardRows(copy.PathOf("old-site"));
        Assert.Equal(PartL.WebNames, rows.Select(row => row.Name));
        foreach (var (name, ahead, behind) in new[] { ("web-app", 2, 0), ("docs", 0, 3), ("mobile", 1, 2) })
        {
            using var repo = copy.Repo(name);
            Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"{ahead}\t{behind}"), (await LinesAsync(repo, "rev-list", "--left-right", "--count", "main...origin/main")).Single());
            var cell = rows.Single(row => row.Name == name).AheadBehind;
            Assert.Equal(string.Join(' ', new[] { ahead > 0 ? $"↑ {ahead}" : null, behind > 0 ? $"↓ {behind}" : null }.OfType<string>()), cell.Text);
        }

        Assert.False(Directory.Exists(copy.PathOf("old-site")));
        Assert.Equal(PartL.DoesNotExist(copy.PathOf("old-site")), rows[^1].State.ToolTip);
        Assert.Equal(PartL.Checklist, PartL.Checklist.Order(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>L32's clone error, in git: the failed clone of a missing folder's URL, cleaned as the app cleans it: its key line on every git, the four lines on 2.36 and 2.55; no folder made.</summary>
    [Fact]
    public async Task The_clone_error_is_the_reports()
    {
        using var clones = new TempDirectory("clones");
        var url = new Uri(clones.Combine("does-not-exist")).AbsoluteUri;
        var git = GitLocator.FindExecutable(GitSearchContext.FromSystem()) ?? throw new InvalidOperationException("Git was not found.");
        var result = await new GitRunner(git).RunAsync(new GitCommand("clone", "--progress", "--", url, "cloned") { WorkingDirectory = clones.Path }, TestContext.Current.CancellationToken);
        Assert.Equal(128, result.ExitCode);
        Assert.False(Directory.Exists(clones.Combine("cloned")));
        var lines = GitOutputCleaner.Clean(result.StandardError, result.StandardOutput).Trim().Split('\n');
        Assert.Contains(lines, line => line.StartsWith("fatal: '", StringComparison.Ordinal) && line.EndsWith(PartL.CloneKeyEnd, StringComparison.Ordinal));
        if (await MachineGitVersionAsync() is { Major: 2, Minor: 36 or 55 })
        {
            var path = new Uri(url).LocalPath;
            Assert.Equal(PartL.CloneErrorLines(OperatingSystem.IsWindows() ? path.Replace('\\', '/') : path), lines);
        }
    }

    private static void AssertNear(Color expected, Color actual) =>
        Assert.True(
            Math.Abs(expected.R - actual.R) <= 1 && Math.Abs(expected.G - actual.G) <= 1 && Math.Abs(expected.B - actual.B) <= 1,
            $"{expected} is not the blend {actual} (±1).");

    private static async Task<List<string>> LinesAsync(TempRepo repo, params string[] arguments) =>
        [.. (await repo.GitAsync(arguments)).StandardOutput.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries)];
}
