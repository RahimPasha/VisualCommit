using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>Reading the working tree: <see cref="GitRepository.ReadStatusAsync"/>, <see cref="GitRepository.ReadDiffAsync"/> and the versions of a file.</summary>
public class WorkingChangesTests
{
    [Fact]
    public async Task The_changes_scenario_has_its_staged_and_unstaged_files_in_path_order()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await OpenAsync(repo);

        var status = await repository.ReadStatusAsync(TestCancelled);

        Assert.True(status.HasChanges);
        Assert.Equal(
            ["M README.md", "A config/settings.json", "R src/helpers.py from src/util.py"],
            status.Staged.Select(Describe));
        Assert.Equal(
            ["M README.md", "M assets/logo.png", "M data/blob.bin", "M data/large.txt", "A docs/guide.md", "D docs/old-notes.txt", "M src/Calculator.cs"],
            status.Unstaged.Select(Describe));
        Assert.Equal(["docs/guide.md"], status.Untracked);
    }

    [Fact]
    public async Task A_clean_repository_has_no_changes_and_reading_does_not_write()
    {
        using var repo = await Scenarios.LinearAsync();
        var repository = await OpenAsync(repo);
        var index = Path.Combine(repo.Path, ".git", "index");
        var before = File.GetLastWriteTimeUtc(index);

        // Touch a file without changing it: git status would refresh the index if it could write.
        var readme = Path.Combine(repo.Path, "README.md");
        File.SetLastWriteTimeUtc(readme, DateTime.UtcNow.AddMinutes(1));
        var status = await repository.ReadStatusAsync(TestCancelled);

        Assert.False(status.HasChanges);
        Assert.True(status.SameAs(WorkingTreeStatus.Clean));
        Assert.Equal(before, File.GetLastWriteTimeUtc(index));
    }

    [Fact]
    public async Task A_conflicted_file_is_unstaged_and_conflicted()
    {
        using var repo = await TempRepo.CreateAsync("conflict");
        await repo.CommitFileAsync("a.txt", "base\n", "Base");
        await repo.CreateBranchAsync("other");
        await repo.CommitFileAsync("a.txt", "main\n", "On main");
        await repo.CheckoutAsync("other");
        await repo.CommitFileAsync("a.txt", "other\n", "On other");
        await repo.CheckoutAsync("main");
        await Assert.ThrowsAsync<GitException>(() => repo.GitAsync("merge", "--quiet", "other"));
        var repository = await OpenAsync(repo);

        var status = await repository.ReadStatusAsync(TestCancelled);

        Assert.Empty(status.Staged);
        Assert.Equal(["U a.txt"], status.Unstaged.Select(Describe));
        Assert.True((await repository.ReadDiffAsync(new DiffTarget(DiffSide.Unstaged, "a.txt", FileChangeKind.Conflicted), TestCancelled)).IsEmpty);
    }

    [Fact]
    public async Task Unstaged_staged_untracked_deleted_and_renamed_diffs()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await OpenAsync(repo);

        var calculator = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Unstaged, "src/Calculator.cs", FileChangeKind.Modified), TestCancelled);
        Assert.Equal(
            ["@@ -12,7 +12,7 @@ public sealed class Calculator", "@@ -29,4 +29,8 @@ public sealed class Calculator"],
            calculator.Hunks.Select(hunk => hunk.Header));
        Assert.Equal("        return a + b + _offset;", calculator.Hunks[0].Lines[4].Text);
        Assert.Equal(15, calculator.Hunks[0].Lines[4].NewNumber);

        var guide = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Unstaged, "docs/guide.md", FileChangeKind.Added, IsUntracked: true), TestCancelled);
        Assert.True(guide.IsNewFile);
        Assert.Equal("@@ -0,0 +1,4 @@", guide.Hunks.Single().Header);
        Assert.Equal(["# Guide", string.Empty, "Add numbers with Add.", "Subtract them with Subtract."], guide.Hunks[0].Lines.Select(line => line.Text));
        Assert.Contains("+++ b/docs/guide.md", guide.HeaderLines);

        var oldNotes = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Unstaged, "docs/old-notes.txt", FileChangeKind.Deleted), TestCancelled);
        Assert.True(oldNotes.IsDeletedFile);
        Assert.Equal("@@ -1,2 +0,0 @@", oldNotes.Hunks.Single().Header);

        var readmeStaged = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Staged, "README.md", FileChangeKind.Modified), TestCancelled);
        Assert.Equal("@@ -1,3 +1,7 @@", readmeStaged.Hunks.Single().Header);
        var readmeUnstaged = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Unstaged, "README.md", FileChangeKind.Modified), TestCancelled);
        Assert.Equal("@@ -1,6 +1,6 @@", readmeUnstaged.Hunks.Single().Header);

        var helpers = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Staged, "src/helpers.py", FileChangeKind.Renamed, OldPath: "src/util.py"), TestCancelled);
        Assert.Contains("rename from src/util.py", helpers.HeaderLines);
        Assert.Equal("@@ -1,4 +1,4 @@", helpers.Hunks.Single().Header);
        Assert.Equal(["\"\"\"Small helpers for the calculator.\"\"\"", "\"\"\"Helpers shared by the calculator.\"\"\""], helpers.Hunks[0].Lines.Take(2).Select(line => line.Text));
    }

    [Fact]
    public async Task Binary_image_and_very_large_diffs()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await OpenAsync(repo);

        var blob = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Unstaged, "data/blob.bin", FileChangeKind.Modified), TestCancelled);
        Assert.True(blob.IsBinary);
        var logo = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Unstaged, "assets/logo.png", FileChangeKind.Modified), TestCancelled);
        Assert.True(logo.IsBinary);

        var large = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Unstaged, "data/large.txt", FileChangeKind.Modified), TestCancelled);
        Assert.True(large.IsVeryLarge);
        Assert.Equal((30_000, 30_000), (large.RemovedCount, large.AddedCount));
        Assert.Equal("@@ -1,30000 +1,30000 @@", large.Hunks.Single().Header);
    }

    [Fact]
    public async Task A_commits_file_and_a_root_commits_file()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await OpenAsync(repo);
        var calculatorCommit = await repo.HeadAsync();
        var root = (await repo.GitAsync("rev-parse", "HEAD~1")).StandardOutput.Trim();

        var calculator = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Commit, "src/Calculator.cs", FileChangeKind.Added, Commit: calculatorCommit, Parent: root), TestCancelled);
        Assert.Equal("@@ -0,0 +1,32 @@", calculator.Hunks.Single().Header);
        Assert.Equal("namespace Demo;", calculator.Hunks[0].Lines[0].Text);

        var readme = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Commit, "README.md", FileChangeKind.Added, Commit: root), TestCancelled);
        Assert.Equal("@@ -0,0 +1,3 @@", readme.Hunks.Single().Header);
    }

    [Fact]
    public async Task Versions_of_a_file_are_read_byte_for_byte_with_their_sizes()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await OpenAsync(repo);
        var target = new DiffTarget(DiffSide.Unstaged, "assets/logo.png", FileChangeKind.Modified);

        Assert.Equal(Scenarios.ChangesFiles.LogoCommitted, await repository.ReadFileAsync(target.Before!, 1_000_000, TestCancelled));
        Assert.Equal(Scenarios.ChangesFiles.LogoWorking, await repository.ReadFileAsync(target.After!, 1_000_000, TestCancelled));
        Assert.Equal(7_028, await repository.ReadFileSizeAsync(target.Before!, TestCancelled));
        Assert.Equal(9_332, await repository.ReadFileSizeAsync(target.After!, TestCancelled));
        Assert.Null(await repository.ReadFileAsync(target.After!, 100, TestCancelled));

        var blob = new DiffTarget(DiffSide.Unstaged, "data/blob.bin", FileChangeKind.Modified);
        Assert.Equal(Scenarios.ChangesFiles.BlobCommitted, await repository.ReadFileAsync(blob.Before!, 1_000_000, TestCancelled));
        Assert.Equal(320, await repository.ReadFileSizeAsync(blob.After!, TestCancelled));

        var staged = new DiffTarget(DiffSide.Staged, "config/settings.json", FileChangeKind.Added);
        Assert.Null(staged.Before);
        Assert.Equal(Scenarios.ChangesFiles.SettingsStaged.Length, await repository.ReadFileSizeAsync(staged.After!, TestCancelled));
        Assert.Null(await repository.ReadFileSizeAsync(FileVersion.InCommit("HEAD", "no/such/file"), TestCancelled));
    }

    [Fact]
    public async Task The_crlf_scenarios_diff_has_no_carriage_returns_in_its_lines()
    {
        using var repo = await Scenarios.CrlfAsync();
        var repository = await OpenAsync(repo);

        var diff = await repository.ReadDiffAsync(new DiffTarget(DiffSide.Unstaged, "notes.txt", FileChangeKind.Modified), TestCancelled);

        Assert.Equal(["@@ -1,5 +1,5 @@", "@@ -15,6 +15,6 @@ Note 14"], diff.Hunks.Select(hunk => hunk.Header));
        Assert.All(diff.Hunks.SelectMany(hunk => hunk.Lines), line => Assert.DoesNotContain('\r', line.Raw));
    }

    private static string Describe(ChangedFile file)
    {
        var letter = file.Kind switch
        {
            FileChangeKind.Added => "A",
            FileChangeKind.Modified => "M",
            FileChangeKind.Deleted => "D",
            FileChangeKind.Renamed => "R",
            FileChangeKind.Conflicted => "U",
            _ => file.Kind.ToString(),
        };
        return file.OldPath is null ? $"{letter} {file.Path}" : $"{letter} {file.Path} from {file.OldPath}";
    }
}
