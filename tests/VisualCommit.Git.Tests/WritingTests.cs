using System.Text;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// What the app writes: staging, unstaging and discarding whole files or chosen lines through
/// <see cref="PatchBuilder"/>, snapshots and their restore (D67), commit and amend (D68). Every
/// expected state is read back with plain git.
/// </summary>
public class WritingTests
{
    private static readonly DiffTarget CalculatorUnstaged = new(DiffSide.Unstaged, "src/Calculator.cs", FileChangeKind.Modified);

    [Fact]
    public async Task Staging_and_unstaging_whole_files_including_a_new_a_deleted_and_a_renamed_one()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await OpenAsync(repo);

        await repository.StageAsync(["docs/guide.md", "docs/old-notes.txt"], TestCancelled);
        Assert.Equal(
            ["M  README.md", "A  config/settings.json", "A  docs/guide.md", "D  docs/old-notes.txt", "R  src/util.py -> src/helpers.py"],
            await StagedAsync(repo));

        await repository.UnstageAsync(["src/helpers.py", "src/util.py", "docs/guide.md"], TestCancelled);
        Assert.Equal(["M  README.md", "A  config/settings.json", "D  docs/old-notes.txt"], await StagedAsync(repo));
        Assert.Contains("?? docs/guide.md", await PorcelainAsync(repo));
        Assert.Contains("?? src/helpers.py", await PorcelainAsync(repo));
        Assert.Contains(" D src/util.py", await PorcelainAsync(repo));
    }

    [Fact]
    public async Task Unstaging_before_the_first_commit_removes_the_entry()
    {
        using var repo = await TempRepo.CreateAsync("unborn");
        repo.WriteFile("first.txt", "first\n");
        var repository = await OpenAsync(repo);

        await repository.StageAsync(["first.txt"], TestCancelled);
        Assert.Equal(["A  first.txt"], await StagedAsync(repo));
        await repository.UnstageAsync(["first.txt"], TestCancelled);

        Assert.Equal(["?? first.txt"], await PorcelainAsync(repo));
    }

    [Fact]
    public async Task Staging_the_first_hunk_then_one_line_of_the_second()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await OpenAsync(repo);
        var diff = await repository.ReadDiffAsync(CalculatorUnstaged, TestCancelled);

        await repository.ApplyPatchAsync(PatchBuilder.Build(diff, Hunk(diff, 0), PatchDirection.Forward)!, toIndex: true, PatchDirection.Forward, TestCancelled);
        Assert.Equal(["@@ -12,7 +12,7 @@ public sealed class Calculator"], Headers(await StagedDiffAsync(repo, "src/Calculator.cs")));
        diff = await repository.ReadDiffAsync(CalculatorUnstaged, TestCancelled);
        Assert.Equal(["@@ -29,4 +29,8 @@ public sealed class Calculator"], diff.Hunks.Select(hunk => hunk.Header));

        // The added "Square" line: the fourth added line of the remaining hunk.
        var square = diff.Hunks[0].Lines.Select((line, index) => (line, index)).Single(pair => pair.line.Text.Contains("Square", StringComparison.Ordinal)).index;
        await repository.ApplyPatchAsync(
            PatchBuilder.Build(diff, new HashSet<DiffLineRef> { new(0, square) }, PatchDirection.Forward)!,
            toIndex: true,
            PatchDirection.Forward,
            TestCancelled);

        var expectedIndex = Scenarios.ChangesFiles.CalculatorCommitted
            .Replace("        return a + b;\n", "        return a + b + _offset;\n", StringComparison.Ordinal)
            .Replace("        return a / b;\n    }\n}\n", "        return a / b;\n    }\n    public int Square(int a) => a * a;\n}\n", StringComparison.Ordinal);
        Assert.Equal(expectedIndex, (await repo.GitAsync("show", ":src/Calculator.cs")).StandardOutput);
        Assert.Equal(Scenarios.ChangesFiles.CalculatorWorking, File.ReadAllText(Path.Combine(repo.Path, "src", "Calculator.cs")));
        Assert.Equal(
            ["@@ -29,5 +29,8 @@ public sealed class Calculator"],
            (await repository.ReadDiffAsync(CalculatorUnstaged, TestCancelled)).Hunks.Select(hunk => hunk.Header));
    }

    [Fact]
    public async Task Unstaging_a_hunk_and_one_line_of_a_staged_new_file()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await OpenAsync(repo);

        // README.md's staged hunk, whole.
        var readme = new DiffTarget(DiffSide.Staged, "README.md", FileChangeKind.Modified);
        var diff = await repository.ReadDiffAsync(readme, TestCancelled);
        await repository.ApplyPatchAsync(PatchBuilder.Build(diff, Hunk(diff, 0), PatchDirection.Reverse)!, toIndex: true, PatchDirection.Reverse, TestCancelled);
        Assert.Equal(Scenarios.ChangesFiles.ReadmeCommitted, (await repo.GitAsync("show", ":README.md")).StandardOutput);
        Assert.Equal(Scenarios.ChangesFiles.ReadmeWorking, File.ReadAllText(Path.Combine(repo.Path, "README.md")));

        // One line of the staged new file: the file stays staged with its other lines.
        var settings = new DiffTarget(DiffSide.Staged, "config/settings.json", FileChangeKind.Added);
        diff = await repository.ReadDiffAsync(settings, TestCancelled);
        var precision = diff.Hunks[0].Lines.Select((line, index) => (line, index)).Single(pair => pair.line.Text.Contains("precision", StringComparison.Ordinal)).index;
        await repository.ApplyPatchAsync(
            PatchBuilder.Build(diff, new HashSet<DiffLineRef> { new(0, precision) }, PatchDirection.Reverse)!,
            toIndex: true,
            PatchDirection.Reverse,
            TestCancelled);
        Assert.Equal("{\n  \"offset\": 2,\n}\n", (await repo.GitAsync("show", ":config/settings.json")).StandardOutput);
    }

    [Fact]
    public async Task Unstaging_a_line_of_a_staged_rename_keeps_the_rename()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await OpenAsync(repo);
        var helpers = new DiffTarget(DiffSide.Staged, "src/helpers.py", FileChangeKind.Renamed, OldPath: "src/util.py");
        var diff = await repository.ReadDiffAsync(helpers, TestCancelled);

        await repository.ApplyPatchAsync(PatchBuilder.Build(diff, Hunk(diff, 0), PatchDirection.Reverse)!, toIndex: true, PatchDirection.Reverse, TestCancelled);

        Assert.Equal(Scenarios.ChangesFiles.UtilCommitted, (await repo.GitAsync("show", ":src/helpers.py")).StandardOutput);
        Assert.Contains("R  src/util.py -> src/helpers.py", await StagedAsync(repo));
    }

    [Fact]
    public async Task Staging_some_lines_of_an_untracked_file_and_of_a_deleted_file()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await OpenAsync(repo);

        var guide = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Unstaged, "docs/guide.md", FileChangeKind.Added, IsUntracked: true), TestCancelled);
        await repository.ApplyPatchAsync(
            PatchBuilder.Build(guide, new HashSet<DiffLineRef> { new(0, 0), new(0, 1) }, PatchDirection.Forward)!,
            toIndex: true,
            PatchDirection.Forward,
            TestCancelled);
        Assert.Equal("# Guide\n\n", (await repo.GitAsync("show", ":docs/guide.md")).StandardOutput);

        var notes = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Unstaged, "docs/old-notes.txt", FileChangeKind.Deleted), TestCancelled);
        await repository.ApplyPatchAsync(
            PatchBuilder.Build(notes, new HashSet<DiffLineRef> { new(0, 0) }, PatchDirection.Forward)!,
            toIndex: true,
            PatchDirection.Forward,
            TestCancelled);
        Assert.Equal("They are out of date.\n", (await repo.GitAsync("show", ":docs/old-notes.txt")).StandardOutput);
    }

    [Fact]
    public async Task Discarding_a_hunk_and_one_line_from_the_working_tree()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await OpenAsync(repo);
        var path = Path.Combine(repo.Path, "src", "Calculator.cs");

        var diff = await repository.ReadDiffAsync(CalculatorUnstaged, TestCancelled);
        await repository.ApplyPatchAsync(PatchBuilder.Build(diff, Hunk(diff, 1), PatchDirection.Reverse)!, toIndex: false, PatchDirection.Reverse, TestCancelled);
        Assert.Equal(
            Scenarios.ChangesFiles.CalculatorCommitted.Replace("        return a + b;\n", "        return a + b + _offset;\n", StringComparison.Ordinal),
            File.ReadAllText(path));

        File.WriteAllText(path, Scenarios.ChangesFiles.CalculatorWorking);
        diff = await repository.ReadDiffAsync(CalculatorUnstaged, TestCancelled);
        var negate = diff.Hunks[1].Lines.Select((line, index) => (line, index)).Single(pair => pair.line.Text.Contains("Negate", StringComparison.Ordinal)).index;
        await repository.ApplyPatchAsync(
            PatchBuilder.Build(diff, new HashSet<DiffLineRef> { new(1, negate) }, PatchDirection.Reverse)!,
            toIndex: false,
            PatchDirection.Reverse,
            TestCancelled);

        Assert.Equal(Scenarios.ChangesFiles.CalculatorWorking.Replace("    public int Negate(int a) => -a;\n", string.Empty, StringComparison.Ordinal), File.ReadAllText(path));
        Assert.Equal(
            ["@@ -12,7 +12,7 @@ public sealed class Calculator", "@@ -29,4 +29,7 @@ public sealed class Calculator"],
            (await repository.ReadDiffAsync(CalculatorUnstaged, TestCancelled)).Hunks.Select(hunk => hunk.Header));
    }

    [Fact]
    public async Task With_autocrlf_a_staged_hunk_has_lf_and_a_discarded_hunk_keeps_crlf()
    {
        using var repo = await Scenarios.CrlfAsync();
        var repository = await OpenAsync(repo);
        var target = new DiffTarget(DiffSide.Unstaged, "notes.txt", FileChangeKind.Modified);

        var diff = await repository.ReadDiffAsync(target, TestCancelled);
        await repository.ApplyPatchAsync(PatchBuilder.Build(diff, Hunk(diff, 0), PatchDirection.Forward)!, toIndex: true, PatchDirection.Forward, TestCancelled);
        diff = await repository.ReadDiffAsync(target, TestCancelled);
        await repository.ApplyPatchAsync(PatchBuilder.Build(diff, Hunk(diff, 0), PatchDirection.Reverse)!, toIndex: false, PatchDirection.Reverse, TestCancelled);

        var staged = Scenarios.CrlfNotes(changed: false, lineEnding: "\n").Replace("Note 2\n", "Note 2, changed\n", StringComparison.Ordinal);
        Assert.Equal(staged, (await repo.GitAsync("show", ":notes.txt")).StandardOutput);
        Assert.Equal(staged.Replace("\n", "\r\n", StringComparison.Ordinal), File.ReadAllText(Path.Combine(repo.Path, "notes.txt")));
        Assert.Empty((await repo.GitAsync("diff", "--", "notes.txt")).StandardOutput);
    }

    [Fact]
    public async Task A_patch_keeps_bytes_that_are_not_utf8()
    {
        using var repo = await TempRepo.CreateAsync("latin1");
        var path = Path.Combine(repo.Path, "names.txt");
        File.WriteAllBytes(path, Encoding.Latin1.GetBytes("café\n"));
        await repo.CommitAsync("Add names");
        File.WriteAllBytes(path, Encoding.Latin1.GetBytes("café\nnaïve\n"));
        var repository = await OpenAsync(repo);

        var diff = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Unstaged, "names.txt", FileChangeKind.Modified), TestCancelled);
        Assert.Equal("naïve", diff.Hunks[0].Lines[1].Text);
        await repository.ApplyPatchAsync(PatchBuilder.Build(diff, Hunk(diff, 0), PatchDirection.Forward)!, toIndex: true, PatchDirection.Forward, TestCancelled);

        var blob = (await repo.GitAsync("rev-parse", ":names.txt")).StandardOutput.Trim();
        var expected = (await repo.GitAsync("hash-object", "names.txt")).StandardOutput.Trim();
        Assert.Equal(expected, blob);
    }

    [Fact]
    public async Task A_snapshot_keeps_untracked_deleted_and_binary_files_and_restore_puts_them_back()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await OpenAsync(repo);
        var status = await repository.ReadStatusAsync(TestCancelled);
        var paths = status.Unstaged.Select(file => file.Path).ToList();
        var before = paths.ToDictionary(path => path, path => ReadIfExists(repo, path));
        var index = (await repo.GitAsync("write-tree")).StandardOutput.Trim();

        var snapshot = await repository.SaveSnapshotAsync(paths, TestCancelled);
        await repository.DiscardAsync(paths, status.Untracked, TestCancelled);

        Assert.StartsWith(GitRepository.SnapshotRefPrefix, snapshot.Ref, StringComparison.Ordinal);
        Assert.Equal(snapshot.Commit, (await repo.GitAsync("rev-parse", snapshot.Ref)).StandardOutput.Trim());
        Assert.Equal(await repo.HeadAsync(), (await repo.GitAsync("rev-parse", snapshot.Commit + "^")).StandardOutput.Trim());
        Assert.False(File.Exists(Path.Combine(repo.Path, "docs", "guide.md")));
        Assert.True(File.Exists(Path.Combine(repo.Path, "docs", "old-notes.txt")));
        Assert.Empty((await repo.GitAsync("diff", "--name-only")).StandardOutput);
        Assert.Equal(index, (await repo.GitAsync("write-tree")).StandardOutput.Trim());

        await repository.RestoreSnapshotAsync(snapshot, TestCancelled);

        Assert.All(paths, path => Assert.Equal(before[path], ReadIfExists(repo, path)));
        Assert.Equal(index, (await repo.GitAsync("write-tree")).StandardOutput.Trim());
    }

    [Fact]
    public async Task Discarding_the_last_file_of_an_untracked_folder_removes_the_folder()
    {
        using var repo = await Scenarios.LinearAsync();
        repo.WriteFile("new/deep/file.txt", "x\n");
        var repository = await OpenAsync(repo);

        await repository.DiscardAsync(["new/deep/file.txt"], new HashSet<string> { "new/deep/file.txt" }, TestCancelled);

        Assert.False(Directory.Exists(Path.Combine(repo.Path, "new")));
        Assert.Empty(await PorcelainAsync(repo));
    }

    [Fact]
    public async Task A_snapshot_of_a_crlf_file_is_restored_with_crlf()
    {
        using var repo = await Scenarios.CrlfAsync();
        var repository = await OpenAsync(repo);
        var path = Path.Combine(repo.Path, "notes.txt");
        var before = File.ReadAllBytes(path);

        var snapshot = await repository.SaveSnapshotAsync(["notes.txt"], TestCancelled);
        await repository.DiscardAsync(["notes.txt"], new HashSet<string>(), TestCancelled);
        Assert.Equal(Scenarios.CrlfNotes(changed: false, lineEnding: "\r\n"), File.ReadAllText(path));
        await repository.RestoreSnapshotAsync(snapshot, TestCancelled);

        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task Commit_and_amend_give_the_commits_that_plain_git_gives()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await OpenAsync(repo);

        var first = await repository.CommitAsync("Add usage and settings\n\nExplains how to use the calculator.", amend: false, TestCancelled);
        Assert.Equal("f976a24c1cb298e1c293542624fb96fe0b2d81c5", first);

        await repository.StageAsync(["docs/guide.md"], TestCancelled);
        var amended = await repository.CommitAsync("Add usage, settings and a guide\n\nExplains how to use the calculator.", amend: true, TestCancelled);

        Assert.Equal("f9add9d8b5d8ae985965e5a4215dd46b0d0a0517", amended);
        Assert.Equal("Add usage, settings and a guide\n\nExplains how to use the calculator.\n", (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.TrimEnd('\n') + "\n");
        Assert.Equal("06faadcfa6435a48192116e2ffe76d1992d2f7c3", (await repo.GitAsync("rev-parse", "HEAD^")).StandardOutput.Trim());
    }

    [Fact]
    public async Task A_summary_that_starts_with_a_hash_is_kept()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await OpenAsync(repo);

        await repository.CommitAsync("#42 Fix the counter", amend: false, TestCancelled);

        Assert.Equal("#42 Fix the counter", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
    }

    [Fact]
    public async Task A_hook_that_refuses_the_commit_gives_its_own_message()
    {
        using var repo = await Scenarios.ChangesAsync();
        var hook = Path.Combine(repo.Path, ".git", "hooks", "pre-commit");
        Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        File.WriteAllText(hook, "#!/bin/sh\necho 'Commit blocked by the test hook' >&2\nexit 1\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var repository = await OpenAsync(repo);
        var head = await repo.HeadAsync();

        var error = await Assert.ThrowsAsync<GitException>(() => repository.CommitAsync("Blocked", amend: false, TestCancelled));

        Assert.Equal("Commit blocked by the test hook", error.StandardError.Trim());
        Assert.Equal(head, await repo.HeadAsync());
    }

    [Fact]
    public async Task Git_never_opens_an_editor()
    {
        using var repo = await Scenarios.LinearAsync();
        var runner = NewRunner();

        var result = await runner.RunAsync(new GitCommand("var", "GIT_EDITOR") { WorkingDirectory = repo.Path }, TestCancelled);

        Assert.Equal(":", result.StandardOutput.Trim());
    }

    private static HashSet<DiffLineRef> Hunk(FileDiff diff, int hunk) =>
        [.. diff.Hunks[hunk].Lines.Select((line, index) => (line, index)).Where(pair => pair.line.IsChange).Select(pair => new DiffLineRef(hunk, pair.index))];

    private static IEnumerable<string> Headers(string diff) =>
        diff.Split('\n').Where(line => line.StartsWith("@@", StringComparison.Ordinal));

    private static async Task<string> StagedDiffAsync(TempRepo repo, string path) =>
        (await repo.GitAsync("diff", "--cached", "--", path)).StandardOutput;

    private static async Task<List<string>> PorcelainAsync(TempRepo repo) =>
        [.. (await repo.GitAsync("status", "--porcelain", "--untracked-files=all")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)];

    private static async Task<List<string>> StagedAsync(TempRepo repo) =>
        [.. (await PorcelainAsync(repo)).Where(line => line[0] is not (' ' or '?'))
            .Select(line => line[..1] + " " + line[2..])];

    private static byte[]? ReadIfExists(TempRepo repo, string path)
    {
        var full = Path.Combine(repo.Path, path);
        return File.Exists(full) ? File.ReadAllBytes(full) : null;
    }
}
