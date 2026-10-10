using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>Pins down what each scenario repo contains. Visual checks describe their expected results in these terms.</summary>
public class ScenarioTests
{
    [Fact]
    public async Task Linear_has_three_commits_on_main_and_a_clean_working_tree()
    {
        using var repo = await Scenarios.LinearAsync();

        var subjects = (await repo.GitAsync("log", "--format=%s")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(["Describe the project", "Add greeting", "Add README"], subjects);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal("refs/heads/main", (await repo.GitAsync("for-each-ref", "--format=%(refname)")).StandardOutput.Trim());
        Assert.Empty((await repo.GitAsync("status", "--porcelain")).StandardOutput);
        Assert.Equal("# Sample\n\nA repository for tests.\n", File.ReadAllText(Path.Combine(repo.Path, "README.md")));
        Assert.Equal("Hello\n", File.ReadAllText(Path.Combine(repo.Path, "src", "greeting.txt")));
    }

    [Fact]
    public async Task Linear_has_the_same_commit_ids_on_every_run()
    {
        using var repo = await Scenarios.LinearAsync();

        Assert.Equal("7270ded9d9f138de90d34b5dcf330b37c44fc043", await repo.HeadAsync());
    }

    [Fact]
    public async Task Graph_has_its_history_in_date_order()
    {
        using var repo = await Scenarios.GraphAsync();

        var log = Lines(await repo.GitAsync("log", "--date-order", "--format=%s|%ad", "--date=format:%H:%M", "--branches", "--remotes", "--tags", "HEAD"));

        Assert.Equal(
            [
                "Highlight matches|12:11",
                "Add settings page|12:10",
                "Fix typo in docs|12:09",
                "Bump version|12:08",
                "Fix crash on start|12:07",
                "Add search box|12:06",
                "Merge branch 'feature/login'|12:05",
                "Update README|12:04",
                "Validate passwords|12:03",
                "Add login form|12:02",
                "Add app skeleton|12:01",
                "Initial commit|12:00",
            ],
            log);
    }

    [Fact]
    public async Task Graph_has_its_branches_tags_remote_and_stash()
    {
        using var repo = await Scenarios.GraphAsync();

        var refs = Lines(await repo.GitAsync("for-each-ref", "--format=%(refname) %(upstream:short) %(upstream:track)"));
        Assert.Equal(
            [
                "refs/heads/bugfix/crash-on-start  ",
                "refs/heads/feature/login  ",
                "refs/heads/feature/search origin/feature/search [ahead 1]",
                "refs/heads/main origin/main [ahead 1, behind 1]",
                "refs/remotes/origin/feature/search  ",
                "refs/remotes/origin/main  ",
                "refs/stash  ",
                "refs/tags/v0.1  ",
                "refs/tags/v0.2  ",
            ],
            refs);

        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Empty((await repo.GitAsync("status", "--porcelain")).StandardOutput);
        Assert.Equal(["stash@{0}: On main: Work in progress on README"], Lines(await repo.GitAsync("stash", "list")));
        Assert.Equal("tag", (await repo.GitAsync("cat-file", "-t", "v0.1")).StandardOutput.Trim());
        Assert.Equal("Add app skeleton", Subject(await repo.GitAsync("log", "-1", "--format=%s", "v0.1")));
        Assert.Equal("commit", (await repo.GitAsync("cat-file", "-t", "v0.2")).StandardOutput.Trim());
        Assert.Equal("Bump version", Subject(await repo.GitAsync("log", "-1", "--format=%s", "v0.2")));
        Assert.Equal("Fix typo in docs", Subject(await repo.GitAsync("log", "-1", "--format=%s", "origin/main")));
        Assert.True(Directory.Exists(repo.SiblingPath("origin.git")));
    }

    [Fact]
    public async Task Graph_add_settings_page_changes_five_files_with_a_body()
    {
        using var repo = await Scenarios.GraphAsync();

        var files = Lines(await repo.GitAsync("diff-tree", "--no-commit-id", "-r", "-M", "--name-status", "main"));
        Assert.Equal(
            ["M\tREADME.md", "D\tdocs/notes.txt", "R100\tsrc/app.txt\tsrc/main-app.txt", "A\tsrc/settings/defaults.txt", "A\tsrc/settings/page.txt"],
            files);
        Assert.Equal(
            "The settings page lists the defaults.\nIt replaces the old notes.",
            (await repo.GitAsync("log", "-1", "--format=%b", "main")).StandardOutput.Trim());
    }

    [Fact]
    public async Task Graph_has_the_same_commit_ids_on_every_run()
    {
        using var repo = await Scenarios.GraphAsync();

        var ids = Lines(await repo.GitAsync("log", "--date-order", "--format=%h %s", "--branches", "--remotes", "--tags", "HEAD"));
        Assert.Equal(
            [
                "f463b85 Highlight matches",
                "47e6ec7 Add settings page",
                "974bd83 Fix typo in docs",
                "2775228 Bump version",
                "72267a3 Fix crash on start",
                "6aab2e4 Add search box",
                "f7919b8 Merge branch 'feature/login'",
                "10bcd42 Update README",
                "4a62ef9 Validate passwords",
                "7b230d3 Add login form",
                "933250d Add app skeleton",
                "53347b3 Initial commit",
            ],
            ids);
        Assert.Equal("95daa96", (await repo.GitAsync("rev-parse", "--short", "refs/stash")).StandardOutput.Trim());
    }

    [Fact]
    public async Task Graph_copies_are_independent_and_keep_their_own_remote()
    {
        using var first = await Scenarios.GraphAsync();
        using var second = await Scenarios.GraphAsync();

        Assert.NotEqual(first.Path, second.Path);
        await first.CommitFileAsync("extra.txt", "extra\n", "Only in the first copy");
        Assert.Equal("Add settings page", Subject(await second.GitAsync("log", "-1", "--format=%s", "main")));

        var url = (await second.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim();
        Assert.Equal(Path.GetFullPath(second.SiblingPath("origin.git")), Path.GetFullPath(url));
        await second.GitAsync("fetch", "--quiet", "origin");
        Assert.Equal("Fix typo in docs", Subject(await second.GitAsync("log", "-1", "--format=%s", "origin/main")));
    }

    [Fact]
    public async Task Large_history_has_100000_commits_in_the_documented_order()
    {
        var path = await LargeHistory.GetAsync();
        var runner = new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem())!);

        async Task<string> Git(params string[] arguments)
        {
            var command = new GitCommand(arguments) { WorkingDirectory = path };
            return (await runner.RunAsync(command, TestContext.Current.CancellationToken)).EnsureSuccess(command).StandardOutput;
        }

        Assert.Equal(LargeHistory.CommitCount.ToString(System.Globalization.CultureInfo.InvariantCulture), (await Git("rev-list", "--count", "--all")).Trim());
        Assert.Equal("72273ba9e4951d5fb157f6aa4e430cee2830a30b", (await Git("rev-parse", "HEAD")).Trim());
        Assert.Equal("main", (await Git("symbolic-ref", "--short", "HEAD")).Trim());
        Assert.Empty(await Git("status", "--porcelain"));

        // The first rows of the graph, and the parents of a merge.
        var top = Lines(await Git("log", "--date-order", "--format=%s", "-12", "--branches", "--tags", "HEAD"));
        Assert.Equal(Enumerable.Range(0, 12).Select(row => LargeHistory.SubjectOf(LargeHistory.CommitInRow(row))), top);
        var mergeParents = Lines(await Git("log", "-1", "--format=%P", "main"))[0].Split(' ');
        Assert.Equal(2, mergeParents.Length);
        Assert.Equal("Main commit 99991", (await Git("log", "-1", "--format=%s", mergeParents[0])).Trim());
        Assert.Equal("Feature commit 99999", (await Git("log", "-1", "--format=%s", mergeParents[1])).Trim());

        // The tag in the middle, and the root.
        Assert.Equal(LargeHistory.SubjectOf(50_000), (await Git("log", "-1", "--format=%s", LargeHistory.MiddleTag)).Trim());
        Assert.Equal("Main commit 1", (await Git("log", "-1", "--format=%s", "--max-parents=0", "main")).Trim());
        Assert.Equal(LargeHistory.DateOf(1).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), (await Git("log", "-1", "--format=%at", "--max-parents=0", "main")).Trim());
    }

    [Fact]
    public async Task Changes_has_two_commits_with_the_same_ids_on_every_run()
    {
        using var repo = await Scenarios.ChangesAsync();

        Assert.Equal(
            ["06faadc Add calculator", "28fb900 Initial commit"],
            Lines(await repo.GitAsync("log", "--format=%h %s")));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(TempRepo.AuthorName, (await repo.GitAsync("config", "user.name")).StandardOutput.Trim());
        Assert.Equal(TempRepo.AuthorEmail, (await repo.GitAsync("config", "user.email")).StandardOutput.Trim());
    }

    [Fact]
    public async Task Changes_has_its_staged_unstaged_and_untracked_files()
    {
        using var repo = await Scenarios.ChangesAsync();

        // Each entry: the two status letters (index, working tree) and the path; a rename adds where it came from.
        var entries = (await repo.GitAsync("status", "--porcelain=v2", "-z", "--untracked-files=all")).StandardOutput
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var summary = new List<string>();
        for (var i = 0; i < entries.Length; i++)
        {
            var fields = entries[i].Split(' ');
            summary.Add(fields[0] switch
            {
                "1" => $"{fields[1]} {fields[8]}",
                "2" => $"{fields[1]} {fields[9]} {fields[8]} from {entries[++i]}",
                _ => $"{fields[0]} {fields[1]}",
            });
        }

        Assert.Equal(
            [
                "MM README.md",
                ".M assets/logo.png",
                "A. config/settings.json",
                ".M data/blob.bin",
                ".M data/large.txt",
                ".D docs/old-notes.txt",
                ".M src/Calculator.cs",
                "R. src/helpers.py R82 from src/util.py",
                "? docs/guide.md",
            ],
            summary);
    }

    [Fact]
    public async Task Changes_has_its_files_in_the_index_and_the_working_tree()
    {
        using var repo = await Scenarios.ChangesAsync();

        Assert.Equal(Scenarios.ChangesFiles.ReadmeStaged, (await repo.GitAsync("show", ":README.md")).StandardOutput);
        Assert.Equal(Scenarios.ChangesFiles.HelpersStaged, (await repo.GitAsync("show", ":src/helpers.py")).StandardOutput);
        Assert.Equal(Scenarios.ChangesFiles.ReadmeWorking, File.ReadAllText(Path.Combine(repo.Path, "README.md")));
        Assert.Equal(Scenarios.ChangesFiles.CalculatorWorking, File.ReadAllText(Path.Combine(repo.Path, "src", "Calculator.cs")));
        Assert.False(File.Exists(Path.Combine(repo.Path, "docs", "old-notes.txt")));
        Assert.Equal(Scenarios.ChangesFiles.LogoWorking, File.ReadAllBytes(Path.Combine(repo.Path, "assets", "logo.png")));
        Assert.Equal(320, File.ReadAllBytes(Path.Combine(repo.Path, "data", "blob.bin")).Length);

        // The blob ids, so that the images and the binary file are the same bytes everywhere.
        Assert.Equal(
            [
                "4dc143d README.md",
                "464956d assets/logo.png",
                "c45876a config/settings.json",
                "c866266 data/blob.bin",
                "a6aa488 data/large.txt",
                "637d51e docs/old-notes.txt",
                "504fe1f src/Calculator.cs",
                "de3d70d src/helpers.py",
            ],
            Lines(await repo.GitAsync("ls-files", "--stage", "--abbrev")).Select(line => line.Split(' ', '\t') is var parts ? $"{parts[1]} {parts[3]}" : line));
        Assert.Equal("2861e78", (await repo.GitAsync("hash-object", "assets/logo.png")).StandardOutput.Trim()[..7]);
        Assert.Equal("187cd58", (await repo.GitAsync("hash-object", "data/blob.bin")).StandardOutput.Trim()[..7]);
    }

    [Fact]
    public async Task Changes_calculator_has_two_hunks_and_large_txt_a_diff_of_60000_lines()
    {
        using var repo = await Scenarios.ChangesAsync();

        var hunks = Lines(await repo.GitAsync("diff", "--no-color", "--no-ext-diff", "--", "src/Calculator.cs"))
            .Where(line => line.StartsWith("@@", StringComparison.Ordinal));
        Assert.Equal(
            ["@@ -12,7 +12,7 @@ public sealed class Calculator", "@@ -29,4 +29,8 @@ public sealed class Calculator"],
            hunks);
        Assert.Equal("30000\t30000\tdata/large.txt", (await repo.GitAsync("diff", "--numstat", "--", "data/large.txt")).StandardOutput.Trim());
    }

    [Fact]
    public async Task Crlf_keeps_lf_in_the_index_and_crlf_in_the_working_tree()
    {
        using var repo = await Scenarios.CrlfAsync();

        Assert.Equal("5b1f13c Add notes", Lines(await repo.GitAsync("log", "--format=%h %s")).Single());
        Assert.Equal("true", (await repo.GitAsync("config", "core.autocrlf")).StandardOutput.Trim());
        Assert.Equal(Scenarios.CrlfNotes(changed: false, lineEnding: "\n"), (await repo.GitAsync("show", ":notes.txt")).StandardOutput);
        Assert.Equal(Scenarios.CrlfNotes(changed: true, lineEnding: "\r\n"), File.ReadAllText(Path.Combine(repo.Path, "notes.txt")));

        var hunks = Lines(await repo.GitAsync("diff", "--no-color", "--no-ext-diff"))
            .Where(line => line.StartsWith("@@", StringComparison.Ordinal));
        Assert.Equal(["@@ -1,5 +1,5 @@", "@@ -15,6 +15,6 @@ Note 14"], hunks);
    }

    private static string[] Lines(GitResult result) => Lines(result.StandardOutput);

    private static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string Subject(GitResult result) => result.StandardOutput.Trim();
}
