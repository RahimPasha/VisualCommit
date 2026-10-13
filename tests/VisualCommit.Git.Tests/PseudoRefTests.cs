using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// A stopped cherry-pick in a real reftable repository (Git 2.45 and later: <c>git init
/// --ref-format=reftable</c>), where <c>CHERRY_PICK_HEAD</c> is a ref and no file
/// (docs/phase-4-design.md 8.1, D146): the in-progress state, phase 3's single pick, the list and
/// Continue read it as they do in a files repository. On an older git, which has no reftable, the
/// test asserts that git refuses the option, so that it asserts something on every git (phase 3's
/// "two gits" rule). <see cref="PseudoRefProbeTests"/> has the probe's rules with scripted answers.
/// </summary>
public class PseudoRefTests
{
    private static readonly GitVersion Reftable = new(2, 45, 0);

    [Fact]
    public async Task A_stopped_pick_in_a_reftable_repository_is_read_as_in_a_files_repository()
    {
        using var folder = new TempDirectory("reftable");
        var path = folder.Combine("repo");
        Directory.CreateDirectory(path);
        var environment = new Dictionary<string, string?>(TempRepo.IsolatedEnvironment(folder.Combine("home")))
        {
            ["GIT_AUTHOR_NAME"] = TempRepo.AuthorName,
            ["GIT_AUTHOR_EMAIL"] = TempRepo.AuthorEmail,
            ["GIT_COMMITTER_NAME"] = TempRepo.AuthorName,
            ["GIT_COMMITTER_EMAIL"] = TempRepo.AuthorEmail,
            ["GIT_AUTHOR_DATE"] = "2026-01-01T12:00:00+00:00",
            ["GIT_COMMITTER_DATE"] = "2026-01-01T12:00:00+00:00",
        };
        var runner = NewRunner();
        var version = await VersionAsync(runner);

        var init = await GitAsync("init", "--quiet", "--initial-branch", "main", "--ref-format=reftable");

        if (version < Reftable)
        {
            // Git 2.36 exits 129: an option it does not know (0.5).
            Assert.False(init.Succeeded);
            Assert.Contains("ref-format", init.StandardError, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(path, ".git")));
            return;
        }

        Assert.True(init.Succeeded, init.StandardError);
        foreach (var (key, value) in new[] { ("core.autocrlf", "false"), ("commit.gpgsign", "false"), ("user.name", TempRepo.AuthorName), ("user.email", TempRepo.AuthorEmail) })
        {
            await EnsureAsync("config", key, value);
        }

        Assert.Equal("reftable", (await EnsureAsync("config", "--get", "extensions.refStorage")).StandardOutput.Trim());
        await CommitAsync("f.txt", "base\n", "Initial commit");
        await EnsureAsync("branch", "side");
        await CommitAsync("f.txt", "main\n", "Main edit");
        await EnsureAsync("checkout", "--quiet", "side");
        var conflicting = await CommitAsync("f.txt", "side\n", "Side edit");
        var more = await CommitAsync("g.txt", "more\n", "Side more");
        await EnsureAsync("checkout", "--quiet", "main");
        var repository = await GitRepository.OpenAsync(runner, path, TestCancelled);

        // A list stops at its first pick: CHERRY_PICK_HEAD is a ref, not a file.
        Assert.Equal(new SequenceResult(SequenceStop.Conflicts, 0, 2), await repository.CherryPickAsync([conflicting, more], cancellationToken: TestCancelled));
        Assert.False(File.Exists(Path.Combine(path, ".git", "CHERRY_PICK_HEAD")));
        Assert.Equal(conflicting, (await EnsureAsync("rev-parse", "--verify", "CHERRY_PICK_HEAD")).StandardOutput.Trim());

        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal(
            new InProgressState(InProgressKind.CherryPick, conflicting, "Side edit", null, null, 1, false) { Sequenced = true, HasPickHead = true, Done = 0, Total = 2 },
            state);

        // Continue commits it and picks the rest.
        File.WriteAllText(Path.Combine(path, "f.txt"), "resolved\n");
        await EnsureAsync("add", "f.txt");
        Assert.Equal(ContinueOutcome.Done, (await repository.ContinueAsync(state, null, cancellationToken: TestCancelled)).Outcome);
        Assert.Equal("Side more\nSide edit\nMain edit", (await EnsureAsync("log", "--format=%s", "-3")).StandardOutput.Trim().ReplaceLineEndings("\n"));
        Assert.Same(InProgressState.None, (await repository.ReadStatusAsync(TestCancelled)).InProgress);

        // Phase 3's single pick sees its own stop through the ref too: picked again, it is empty.
        Assert.Equal(ApplyResult.Empty, await repository.CherryPickAsync(more, cancellationToken: TestCancelled));
        Assert.True((await repository.ReadStatusAsync(TestCancelled)).InProgress is { Kind: InProgressKind.CherryPick, Empty: true, HasPickHead: true, Sequenced: false });

        Task<GitResult> GitAsync(params string[] arguments) =>
            runner.RunAsync(new GitCommand(arguments) { WorkingDirectory = path, Environment = environment }, TestCancelled);

        async Task<GitResult> EnsureAsync(params string[] arguments)
        {
            var command = new GitCommand(arguments) { WorkingDirectory = path, Environment = environment };
            return (await runner.RunAsync(command, TestCancelled)).EnsureSuccess(command);
        }

        async Task<string> CommitAsync(string file, string content, string message)
        {
            File.WriteAllText(Path.Combine(path, file), content);
            await EnsureAsync("add", "--", file);
            await EnsureAsync("commit", "--quiet", "--message", message);
            return (await EnsureAsync("rev-parse", "HEAD")).StandardOutput.Trim();
        }
    }

    private static async Task<GitVersion> VersionAsync(IGitRunner runner)
    {
        var output = (await runner.RunAsync(new GitCommand("--version"), TestCancelled)).StandardOutput;
        Assert.True(GitVersion.TryParse(output, out var version), output);
        return version;
    }
}
