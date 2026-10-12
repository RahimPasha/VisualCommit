using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The rules of <see cref="GitRepository.ReadPseudoRefAsync"/> (docs/phase-4-design.md 8.1,
/// D146): the pick head's file first; git is asked only while <c>MERGE_MSG</c> or
/// <c>sequencer/</c> is there, the ref format once per repository, and a name answers only as a
/// pseudo-ref, never as a branch of the same name. Git's answers for a reftable repository are
/// scripted (Git 2.36 has no reftable); every other call goes to real git and is recorded.
/// </summary>
public class PseudoRefProbeTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";
    private const string Config = "config --get extensions.refStorage";
    private const string Symbolic = "rev-parse --verify --quiet --symbolic-full-name CHERRY_PICK_HEAD";
    private const string Verify = "rev-parse --verify --quiet CHERRY_PICK_HEAD";

    [Fact]
    public async Task The_pick_heads_file_answers_without_a_git_call()
    {
        using var repo = await RepoAsync();
        File.WriteAllText(Path.Combine(repo.Path, ".git", "CHERRY_PICK_HEAD"), Sha + "\n");
        var (repository, runner) = await OpenRecordedAsync(repo);

        Assert.Equal(Sha, await repository.ReadPseudoRefAsync("CHERRY_PICK_HEAD", TestCancelled));
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Without_merge_msg_or_a_sequencer_nothing_is_asked()
    {
        using var repo = await RepoAsync();
        var (repository, runner) = await OpenRecordedAsync(repo);

        Assert.Null(await repository.ReadPseudoRefAsync("CHERRY_PICK_HEAD", TestCancelled));
        Assert.Null(await repository.ReadPseudoRefAsync("REVERT_HEAD", TestCancelled));
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task A_files_repository_is_probed_once_and_answers_nothing_more()
    {
        using var repo = await RepoAsync();
        File.WriteAllText(Path.Combine(repo.Path, ".git", "MERGE_MSG"), "Pick\n");
        var (repository, runner) = await OpenRecordedAsync(repo);

        Assert.Null(await repository.ReadPseudoRefAsync("CHERRY_PICK_HEAD", TestCancelled));
        Assert.Null(await repository.ReadPseudoRefAsync("REVERT_HEAD", TestCancelled));

        // Real git: extensions.refStorage is unset, so the files format; asked once.
        Assert.Equal([Config], runner.Calls);
    }

    [Fact]
    public async Task A_reftable_repository_reads_the_pseudo_ref_through_git()
    {
        using var repo = await RepoAsync();
        Directory.CreateDirectory(Path.Combine(repo.Path, ".git", "sequencer"));
        var (repository, runner) = await OpenRecordedAsync(repo);
        runner.Answer(Config, 0, "reftable\n");
        runner.Answer(Symbolic, 0, "CHERRY_PICK_HEAD\n");
        runner.Answer(Verify, 0, Sha + "\n");

        Assert.Equal(Sha, await repository.ReadPseudoRefAsync("CHERRY_PICK_HEAD", TestCancelled));
        Assert.Equal(Sha, await repository.ReadPseudoRefAsync("CHERRY_PICK_HEAD", TestCancelled));

        Assert.Equal([Config, Symbolic, Verify, Symbolic, Verify], runner.Calls);
    }

    [Fact]
    public async Task A_branch_named_like_the_pseudo_ref_does_not_answer()
    {
        using var repo = await RepoAsync();
        File.WriteAllText(Path.Combine(repo.Path, ".git", "MERGE_MSG"), "Pick\n");
        var (repository, runner) = await OpenRecordedAsync(repo);
        runner.Answer(Config, 0, "reftable\n");
        runner.Answer(Symbolic, 0, "refs/heads/CHERRY_PICK_HEAD\n");
        runner.Answer(Verify, 0, Sha + "\n");

        Assert.Null(await repository.ReadPseudoRefAsync("CHERRY_PICK_HEAD", TestCancelled));
        Assert.Equal([Config, Symbolic], runner.Calls);
    }

    [Fact]
    public async Task A_pseudo_ref_beside_a_branch_of_its_name_answers_through_gits_ambiguous_warning()
    {
        using var repo = await RepoAsync();
        File.WriteAllText(Path.Combine(repo.Path, ".git", "MERGE_MSG"), "Pick\n");
        var (repository, runner) = await OpenRecordedAsync(repo);
        runner.Answer(Config, 0, "reftable\n");
        runner.Answer(Symbolic, 0, string.Empty, "warning: refname 'CHERRY_PICK_HEAD' is ambiguous.\n");
        runner.Answer(Verify, 0, Sha + "\n");

        Assert.Equal(Sha, await repository.ReadPseudoRefAsync("CHERRY_PICK_HEAD", TestCancelled));
    }

    [Fact]
    public async Task A_reftable_repository_without_the_pseudo_ref_answers_nothing()
    {
        using var repo = await RepoAsync();
        File.WriteAllText(Path.Combine(repo.Path, ".git", "MERGE_MSG"), "Pick\n");
        var (repository, runner) = await OpenRecordedAsync(repo);
        runner.Answer(Config, 0, "reftable\n");
        runner.Answer(Symbolic, 1, string.Empty);

        Assert.Null(await repository.ReadPseudoRefAsync("CHERRY_PICK_HEAD", TestCancelled));
        Assert.Equal([Config, Symbolic], runner.Calls);
    }

    [Fact]
    public async Task A_stopped_cherry_pick_is_read_from_its_file_by_real_git()
    {
        using var repo = await RepoAsync();
        await repo.CreateBranchAsync("other");
        await repo.CommitFileAsync("f.txt", "main\n", "Main edit");
        await repo.CheckoutAsync("other");
        var picked = await repo.CommitFileAsync("f.txt", "other\n", "Other edit");
        await repo.CheckoutAsync("main");
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("cherry-pick", picked)).ExitCode);
        var (repository, runner) = await OpenRecordedAsync(repo);

        Assert.Equal(picked, await repository.ReadPseudoRefAsync("CHERRY_PICK_HEAD", TestCancelled));
        Assert.Null(await repository.ReadPseudoRefAsync("REVERT_HEAD", TestCancelled));

        // REVERT_HEAD has no file, and MERGE_MSG is there: the files format is asked for once.
        Assert.Equal([Config], runner.Calls);
        Assert.Equal(picked, await RevAsync(repo, "CHERRY_PICK_HEAD"));
    }

    private static async Task<TempRepo> RepoAsync()
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            await repo.CommitFileAsync("f.txt", "base\n", "Initial commit");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>The repository opened through a <see cref="RecordingRunner"/>, whose record starts after the opening.</summary>
    private static async Task<(GitRepository Repository, RecordingRunner Runner)> OpenRecordedAsync(TempRepo repo)
    {
        var runner = new RecordingRunner(NewRunner());
        var repository = await GitRepository.OpenAsync(runner, repo.Path, TestCancelled);
        runner.Clear();
        return (repository, runner);
    }

    /// <summary>A runner that records every call's arguments and answers the scripted ones itself; the others go to real git.</summary>
    private sealed class RecordingRunner(IGitRunner real) : IGitRunner
    {
        private readonly Lock _gate = new();
        private readonly List<string> _calls = [];
        private readonly Dictionary<string, GitResult> _answers = new(StringComparer.Ordinal);

        public string ExecutablePath => real.ExecutablePath;

        public IReadOnlyList<string> Calls
        {
            get
            {
                lock (_gate)
                {
                    return [.. _calls];
                }
            }
        }

        public void Answer(string arguments, int exitCode, string output, string error = "")
        {
            lock (_gate)
            {
                _answers[arguments] = new GitResult(exitCode, output, error, TimeSpan.Zero);
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _calls.Clear();
            }
        }

        public Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default)
        {
            var arguments = string.Join(' ', command.Arguments);
            GitResult? answer;
            lock (_gate)
            {
                _calls.Add(arguments);
                _answers.TryGetValue(arguments, out answer);
            }

            return answer is not null ? Task.FromResult(answer) : real.RunAsync(command, cancellationToken);
        }
    }
}
