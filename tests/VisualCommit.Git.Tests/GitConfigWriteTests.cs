using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.GitConfigTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The settings screen's writes (docs/phase-6-design.md 8.3 to 8.5, facts 0.3): the global ones
/// (<see cref="GitConfiguration.WriteGlobalAsync"/>) and this repository's
/// (<see cref="GitRepository.WriteConfigAsync"/>), each read back as the screen reads it.
/// </summary>
public class GitConfigWriteTests
{
    private const string Layout = "[user]\n\tname = Old\n[x]\n\tmulti = one\n\tmulti = two\n[pull]\n\trebase\n";

    private static readonly GitConfigWrite[] LayoutWrites =
    [
        new("user.email", "new@example.com"),
        new("user.name", "New"),
        new("x.multi", "only"),
        new("pull.rebase", "true"),
    ];

    [Fact]
    public async Task A_global_replace_all_adds_an_absent_key_and_replaces_single_multi_valued_and_bare_ones()
    {
        using var lab = new ConfigLab();
        lab.WriteGlobal(Layout);

        await lab.Configuration.WriteGlobalAsync(LayoutWrites, TestCancelled);

        AssertWritten(await lab.Configuration.ReadAsync(null, TestCancelled), GitConfigScope.Global, lab.GlobalFile);
    }

    [Fact]
    public async Task A_local_replace_all_does_the_same_in_the_repositorys_own_file()
    {
        using var lab = new ConfigLab();
        using var repo = await TempRepo.CreateAsync();
        await File.AppendAllTextAsync(Path.Combine(repo.Path, ".git", "config"), Layout, TestCancelled);
        var repository = await OpenAsync(repo);

        await repository.WriteConfigAsync(LayoutWrites, TestCancelled);

        AssertWritten(await lab.Configuration.ReadAsync(repo.Path, TestCancelled), GitConfigScope.Local, Path.Combine(repository.GitDirectory, "config"));
    }

    [Fact]
    public async Task Unsetting_an_absent_key_is_done_and_unsetting_a_multi_valued_one_removes_every_value()
    {
        using var lab = new ConfigLab();
        lab.WriteGlobal(Layout);
        using var repo = await TempRepo.CreateAsync();
        await File.AppendAllTextAsync(Path.Combine(repo.Path, ".git", "config"), Layout, TestCancelled);
        var repository = await OpenAsync(repo);
        GitConfigWrite[] writes = [new("merge.tool", null), new("x.multi", null)];

        await lab.Configuration.WriteGlobalAsync(writes, TestCancelled);
        await repository.WriteConfigAsync(writes, TestCancelled);

        Assert.Empty((await lab.Configuration.ReadAsync(null, TestCancelled)).Of("x.multi"));
        Assert.Empty((await lab.Configuration.ReadAsync(repo.Path, TestCancelled)).Of("x.multi"));
    }

    [Fact]
    public async Task A_held_global_lock_fails_with_gits_line_and_changes_nothing()
    {
        using var lab = new ConfigLab();
        lab.WriteGlobal(Layout);
        var lockFile = lab.GlobalFile + ".lock";
        File.WriteAllText(lockFile, string.Empty);

        var failure = await Assert.ThrowsAsync<GitException>(() => lab.Configuration.WriteGlobalAsync([new("user.name", "New")], TestCancelled));

        AssertLockRefusal(failure);
        Assert.Equal(Layout, File.ReadAllText(lab.GlobalFile));
        Assert.True(File.Exists(lockFile));
    }

    [Fact]
    public async Task A_held_local_lock_fails_with_gits_line_and_changes_nothing()
    {
        using var repo = await TempRepo.CreateAsync();
        var repository = await OpenAsync(repo);
        var config = Path.Combine(repository.GitDirectory, "config");
        var before = File.ReadAllText(config);
        File.WriteAllText(config + ".lock", string.Empty);

        var failure = await Assert.ThrowsAsync<GitException>(() => repository.WriteConfigAsync([new("user.name", "New")], TestCancelled));

        AssertLockRefusal(failure);
        Assert.Equal(before, File.ReadAllText(config));
    }

    [Fact]
    public async Task A_global_file_in_a_missing_folder_fails_and_no_folder_is_made()
    {
        using var lab = new ConfigLab(Path.Combine("missing", "gitconfig"));

        var failure = await Assert.ThrowsAsync<GitException>(() => lab.Configuration.WriteGlobalAsync([new("user.name", "New")], TestCancelled));

        AssertLockRefusal(failure);
        Assert.False(Directory.Exists(lab.Combine("missing")));
        Assert.Empty((await lab.Configuration.ReadAsync(null, TestCancelled)).Values);
    }

    public static TheoryData<string> Values =>
    [
        "-n Bob",
        "Ann \"The Dev\" O'Neil",
        "Zoë 日本",
        "  padded  ",
        "line one\nline two",
        "a # not comment ; semi \\ back",
        "--global",
    ];

    [Theory]
    [MemberData(nameof(Values))]
    public async Task A_value_round_trips_through_the_argument_list(string value)
    {
        using var lab = new ConfigLab();
        using var repo = await TempRepo.CreateAsync();
        var repository = await OpenAsync(repo);

        await lab.Configuration.WriteGlobalAsync([new("user.name", value)], TestCancelled);
        await repository.WriteConfigAsync([new("user.email", value)], TestCancelled);

        Assert.Equal(value, (await lab.Configuration.ReadAsync(null, TestCancelled)).Winner("user.name")?.Value);
        Assert.Equal(value, (await lab.Configuration.ReadAsync(repo.Path, TestCancelled)).Winner("user.email")?.Value);
    }

    [Fact]
    public async Task A_write_that_an_include_still_beats_is_read_back_as_the_includes()
    {
        using var lab = new ConfigLab();
        var included = lab.Combine("inc.gitconfig");
        File.WriteAllText(included, "[user]\n\tname = Inc\n[merge]\n\ttool = meld\n");
        lab.WriteGlobal($"[user]\n\temail = e@example.com\n[include]\n\tpath = {GitPath(included)}\n");

        // git writes into the [user] above the include, and an unset of the included value is done
        // with nothing changed (exit 5); the read back shows what still decides (8.4).
        await lab.Configuration.WriteGlobalAsync([new("user.name", "New"), new("merge.tool", null)], TestCancelled);

        var read = await lab.Configuration.ReadAsync(null, TestCancelled);
        Assert.Equal(
            [(GitConfigScope.Global, lab.GlobalFile, "New"), (GitConfigScope.Global, included, "Inc")],
            read.Of("user.name").Select(value => (value.Scope, value.Origin!, value.Value!)));
        Assert.Equal((included, "Inc"), (read.Winner("user.name")!.Origin, read.Winner("user.name")!.Value));
        Assert.Equal((included, "meld"), (read.Winner("merge.tool")!.Origin, read.Winner("merge.tool")!.Value));
    }

    [Fact]
    public async Task Under_an_includeIf_a_global_write_reads_back_outside_while_the_work_repository_keeps_its_value()
    {
        using var lab = new ConfigLab();
        using var work = await TempRepo.CreateAsync("work");
        using var other = await TempRepo.CreateAsync("other");
        foreach (var repo in new[] { work, other })
        {
            await repo.GitAsync("config", "--unset", "user.email");
        }

        var workFile = lab.Combine("work.gitconfig");
        File.WriteAllText(workFile, "[user]\n\temail = work@example.com\n");
        lab.WriteGlobal($"[user]\n\temail = personal@example.com\n[includeIf \"gitdir:{await GitDirectoryAsync(work)}\"]\n\tpath = {GitPath(workFile)}\n");

        await lab.Configuration.WriteGlobalAsync([new("user.email", "new@example.com")], TestCancelled);

        // The outside read, which judges a global save (8.4), has what was written; the work
        // repository still commits with the includeIf's address, every other with the new one.
        var outside = await lab.Configuration.ReadAsync(null, TestCancelled);
        Assert.Equal((GitConfigScope.Global, lab.GlobalFile, "new@example.com"), (outside.Winner("user.email")!.Scope, outside.Winner("user.email")!.Origin, outside.Winner("user.email")!.Value));
        Assert.Equal(["new@example.com"], outside.Of("user.email").Select(value => value.Value));
        var inWork = await lab.Configuration.ReadAsync(work.Path, TestCancelled);
        Assert.Equal((workFile, "work@example.com"), (inWork.Winner("user.email")!.Origin, inWork.Winner("user.email")!.Value));
        Assert.Equal("new@example.com", (await lab.Configuration.ReadAsync(other.Path, TestCancelled)).Winner("user.email")!.Value);
        Assert.Equal("Test Author <work@example.com>", (await lab.Configuration.ReadIdentityAsync(work.Path, TestCancelled)).Author);
        Assert.Equal("Test Author <new@example.com>", (await lab.Configuration.ReadIdentityAsync(other.Path, TestCancelled)).Author);
    }

    [Fact]
    public async Task The_first_failure_stops_the_global_list()
    {
        using var lab = new ConfigLab();

        var failure = await Assert.ThrowsAsync<GitException>(() =>
            lab.Configuration.WriteGlobalAsync([new("a.first", "1"), new("nodot", "x"), new("a.last", "2")], TestCancelled));

        Assert.True(failure.ExitCode is 1 or 2, $"exit {failure.ExitCode}");
        var read = await lab.Configuration.ReadAsync(null, TestCancelled);
        Assert.Equal("1", read.Winner("a.first")?.Value);
        Assert.Null(read.Winner("a.last"));
        Assert.Contains(lab.Log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("nodot", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_first_failure_stops_the_local_list()
    {
        using var lab = new ConfigLab();
        using var repo = await TempRepo.CreateAsync();
        var repository = await OpenAsync(repo);

        var failure = await Assert.ThrowsAsync<GitException>(() =>
            repository.WriteConfigAsync([new("a.first", "1"), new("nodot", "x"), new("a.last", "2")], TestCancelled));

        Assert.True(failure.ExitCode is 1 or 2, $"exit {failure.ExitCode}");
        var read = await lab.Configuration.ReadAsync(repo.Path, TestCancelled);
        Assert.Equal("1", read.Winner("a.first")?.Value);
        Assert.Null(read.Winner("a.last"));
    }

    [Fact]
    public async Task Two_global_saves_never_interleave()
    {
        using var lab = new ConfigLab();
        GitConfigWrite[] Writes(string prefix) => [.. Enumerable.Range(1, 8).Select(i => new GitConfigWrite($"{prefix}.key{i}", $"{prefix}{i}"))];

        // Overlapping calls of git config on one file would refuse each other ("could not lock
        // config file"); the saves take turns.
        await Task.WhenAll(lab.Configuration.WriteGlobalAsync(Writes("first"), TestCancelled), lab.Configuration.WriteGlobalAsync(Writes("second"), TestCancelled));

        var read = await lab.Configuration.ReadAsync(null, TestCancelled);
        Assert.All(Writes("first").Concat(Writes("second")), write => Assert.Equal(write.Value, read.Winner(write.Key)?.Value));
    }

    [Fact]
    public async Task A_cancelled_save_writes_nothing()
    {
        using var lab = new ConfigLab();
        using var repo = await TempRepo.CreateAsync();
        var repository = await OpenAsync(repo);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lab.Configuration.WriteGlobalAsync([new("user.name", "New")], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.WriteConfigAsync([new("a.key", "New")], cancelled.Token));

        Assert.Empty((await lab.Configuration.ReadAsync(null, TestCancelled)).Values);
        Assert.Null((await lab.Configuration.ReadAsync(repo.Path, TestCancelled)).Winner("a.key"));
    }

    [Fact]
    public async Task A_local_write_in_a_linked_worktree_lands_in_the_common_config()
    {
        using var lab = new ConfigLab();
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "First");
        var linked = Sibling(repo, "linked");
        await repo.GitAsync("worktree", "add", "--quiet", "-b", "side", linked);
        var repository = await GitRepository.OpenAsync(NewRunner(), linked, TestCancelled);

        await repository.WriteConfigAsync([new("user.name", "Linked")], TestCancelled);

        Assert.Contains("name = Linked", File.ReadAllText(Path.Combine(repo.Path, ".git", "config")), StringComparison.Ordinal);
        var read = await lab.Configuration.ReadAsync(linked, TestCancelled);
        Assert.Equal((GitConfigScope.Local, "Linked"), (read.Winner("user.name")!.Scope, read.Winner("user.name")!.Value));
        AssertSameFile(Path.Combine(repo.Path, ".git", "config"), read.Winner("user.name")!.Origin!);
    }

    private static void AssertWritten(GitConfigRead read, GitConfigScope scope, string file)
    {
        foreach (var (key, value) in new[] { ("user.email", "new@example.com"), ("user.name", "New"), ("x.multi", "only"), ("pull.rebase", "true") })
        {
            var entries = read.Of(key).Where(entry => entry.Scope == scope).ToList();
            Assert.Equal([value], entries.Select(entry => entry.Value));
            AssertSameFile(file, entries[0].Origin!);
        }
    }

    /// <summary>Git's refusal to write a file it cannot lock (8.5): exit 255 and its "could not lock config file" line, whatever follows it.</summary>
    private static void AssertLockRefusal(GitException failure)
    {
        Assert.Equal(255, failure.ExitCode);
        Assert.Contains("could not lock config file", failure.StandardError, StringComparison.Ordinal);
    }

    /// <summary>The same file, through two paths git and the test may write differently (a short name, macOS's /var).</summary>
    private static void AssertSameFile(string expected, string actual)
    {
        Assert.True(Path.IsPathFullyQualified(actual), actual);
        AssertSameFolder(Path.GetDirectoryName(expected)!, Path.GetDirectoryName(actual)!);
        Assert.Equal(Path.GetFileName(expected), Path.GetFileName(actual));
    }
}
