using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Core.Undo;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Core.Undo.UndoEffect;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.UndoTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The effects of docs/phase-5-design.md 7.3, each run alone in a step against real git, forward
/// and back, and each check's refusal, which leaves the repository as it was but for
/// <c>update-index --refresh</c>'s stat data: the index's entries (<c>ls-files --stage</c>), every
/// ref, HEAD's reflog and every file's bytes compared, never the index file's bytes. The Keep move
/// (D169) keeps staged, partly staged and unstaged work, is not refused for stale stat data,
/// refuses ignored files in its way that read-tree would overwrite without a word, counts every
/// file in its way, and puts the files back when its compare-and-swap loses a race.
/// </summary>
public class UndoEffectTests
{
    [Fact]
    public async Task A_keep_move_keeps_staged_partly_staged_unstaged_and_untracked_work_both_ways()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);
        var (c1, c3) = (await ShaAsync(repo, "HEAD~2"), await ShaAsync(repo, "HEAD"));
        repo.WriteFile("src/c.txt", "c-staged\n");
        await repo.GitAsync("add", "src/c.txt");
        repo.WriteFile("src/c.txt", "c-unstaged\n").WriteFile("n.txt", "new\n").WriteFile("u.txt", "untracked\n");
        await repo.GitAsync("add", "n.txt");
        var start = await PictureAsync(repo);

        await RunAsync(repository, new MoveHead("main", c3, c1, MoveMode.Keep));
        Assert.Equal(c1, await TargetAsync(repo, "refs/heads/main"));
        Assert.Equal("a1\n", ReadText(repo, "a.txt"));
        Assert.Equal("c-unstaged\n", ReadText(repo, "src/c.txt"));
        Assert.Equal("c-staged\n", (await repo.GitAsync("show", ":src/c.txt")).StandardOutput);
        Assert.Equal("new\n", (await repo.GitAsync("show", ":n.txt")).StandardOutput);
        Assert.Equal("VisualCommit: undo effect", (await repo.GitAsync("log", "-g", "-1", "--format=%gs", "HEAD")).StandardOutput.Trim());

        await RunAsync(repository, new MoveHead("main", c1, c3, MoveMode.Keep));
        Assert.Equal(start, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_keep_move_refused_for_local_changes_counts_every_file_in_its_way_and_changes_nothing_but_stat_data()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);
        var (c1, c3) = (await ShaAsync(repo, "HEAD~2"), await ShaAsync(repo, "HEAD"));
        repo.WriteFile("a.txt", "a-changed\n").WriteFile("b.txt", "b-changed\n").WriteFile("src/c.txt", "c-changed\n");
        var still = await StillAsync(repo);

        // read-tree names only the first; the count is the app's: both files the move changes.
        var refusal = await RefuseAsync(repository, new MoveHead("main", c3, c1, MoveMode.Keep));
        Assert.Equal(UndoRefusalKind.LocalChangesInTheWay, refusal.Kind);
        Assert.Equal(["a.txt", "b.txt"], refusal.Paths);
        Assert.Contains("Cannot merge", refusal.GitOutput, StringComparison.Ordinal);
        Assert.Equal(still, await StillAsync(repo));
    }

    [Fact]
    public async Task Stale_stat_data_does_not_refuse_a_keep_move()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);
        var (c1, c3) = (await ShaAsync(repo, "HEAD~2"), await ShaAsync(repo, "HEAD"));

        // Unchanged files the move changes, written again and given another time: read-tree
        // alone would refuse them as not up to date (0.4).
        foreach (var path in new[] { "a.txt", "b.txt" })
        {
            var full = Path.Combine(repo.Path, path);
            File.WriteAllBytes(full, File.ReadAllBytes(full));
            File.SetLastWriteTimeUtc(full, new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        await RunAsync(repository, new MoveHead("main", c3, c1, MoveMode.Keep));
        Assert.Equal(c1, await TargetAsync(repo, "refs/heads/main"));
        Assert.Equal("b1\n", ReadText(repo, "b.txt"));
    }

    [Fact]
    public async Task Ignored_files_where_a_keep_move_adds_a_file_refuse_it_and_stay()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);
        var c3 = await ShaAsync(repo, "HEAD");

        // Three commits after c3 on a side branch: one adds the file logs, one d/e.txt, one f.txt.
        await repo.GitAsync("checkout", "--quiet", "-b", "adds");
        var logs = await repo.CommitFileAsync("logs", "a file\n", "logs");
        await repo.GitAsync("reset", "--quiet", "--hard", c3);
        var nested = await repo.CommitFileAsync("d/e.txt", "e\n", "d/e.txt");
        await repo.GitAsync("reset", "--quiet", "--hard", c3);
        var file = await repo.CommitFileAsync("f.txt", "f\n", "f.txt");
        await repo.CheckoutAsync("main");
        File.WriteAllText(Path.Combine(repo.Path, ".git", "info", "exclude"), "logs/\nd\nf.txt\n");

        // An ignored folder where the target has a file, an ignored file where it has a folder,
        // an ignored file where it has a file: read-tree would delete or overwrite each.
        repo.WriteFile("logs/today.log", "kept\n").WriteFile("d", "an ignored file\n").WriteFile("f.txt", "ignored\n");
        var still = await StillAsync(repo);
        foreach (var (target, path) in new[] { (logs, "logs/today.log"), (nested, "d"), (file, "f.txt") })
        {
            var refusal = await RefuseAsync(repository, new MoveHead("main", c3, target, MoveMode.Keep));
            Assert.Equal(UndoRefusalKind.IgnoredFilesInTheWay, refusal.Kind);
            Assert.Equal([path], refusal.Paths);
            Assert.Equal(still, await StillAsync(repo));
        }
    }

    [Fact]
    public async Task A_detached_heads_moves_leave_every_branch_and_need_HEAD_detached_where_they_start()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);
        var (c1, c3) = (await ShaAsync(repo, "HEAD~2"), await ShaAsync(repo, "HEAD"));
        await repo.GitAsync("checkout", "--quiet", "--detach", c3);
        repo.WriteFile("src/c.txt", "c-unstaged\n");
        var start = await PictureAsync(repo);

        // Keep: the files follow HEAD, the work stays, main stays where it is.
        await RunAsync(repository, new MoveDetachedHead(c3, c1, MoveMode.Keep));
        Assert.Equal(c1, await ShaAsync(repo, "HEAD"));
        Assert.Equal(c3, await TargetAsync(repo, "refs/heads/main"));
        Assert.Equal("a1\n", ReadText(repo, "a.txt"));
        Assert.Equal("c-unstaged\n", ReadText(repo, "src/c.txt"));
        Assert.Equal("VisualCommit: undo effect", (await repo.GitAsync("log", "-g", "-1", "--format=%gs", "HEAD")).StandardOutput.Trim());
        await RunAsync(repository, new MoveDetachedHead(c1, c3, MoveMode.Keep));
        Assert.Equal(start, await PictureAsync(repo));

        // Soft: HEAD alone, through the transaction's no-deref option; the index and the files stay.
        await RunAsync(repository, new MoveDetachedHead(c3, c1, MoveMode.Soft));
        Assert.Equal(c1, await ShaAsync(repo, "HEAD"));
        Assert.Equal(c3, await TargetAsync(repo, "refs/heads/main"));
        Assert.Equal("b2\n", (await repo.GitAsync("show", ":b.txt")).StandardOutput);
        await RunAsync(repository, new MoveDetachedHead(c1, c3, MoveMode.Soft));
        Assert.Equal(start, await PictureAsync(repo));

        // Refused: HEAD detached elsewhere, and HEAD on a branch at the commit.
        var still = await StillAsync(repo);
        Assert.Equal(new UndoRefusal(UndoRefusalKind.HeadNotAtCommit, Sha: c1), await RefuseAsync(repository, new MoveDetachedHead(c1, c3, MoveMode.Keep)));
        Assert.Equal(still, await StillAsync(repo));
        await repo.CheckoutAsync("main");
        still = await StillAsync(repo);
        Assert.Equal(new UndoRefusal(UndoRefusalKind.HeadNotAtCommit, Sha: c3), await RefuseAsync(repository, new MoveDetachedHead(c3, c1, MoveMode.Soft)));
        Assert.Equal(still, await StillAsync(repo));
    }

    [Fact]
    public async Task A_keep_move_that_loses_the_race_for_its_branch_puts_the_files_back()
    {
        using var repo = await UndoRepoAsync();
        var c2 = await ShaAsync(repo, "HEAD~1");
        var (c1, c3) = (await ShaAsync(repo, "HEAD~2"), await ShaAsync(repo, "HEAD"));
        repo.WriteFile("src/c.txt", "c-unstaged\n");
        var start = await PictureAsync(repo);

        // A terminal moves main between the step's read-tree and its update-ref.
        var runner = new InterceptingRunner(NewRunner(), async command =>
        {
            if (command.Arguments is ["update-ref", "-m", _, "refs/heads/main", ..])
            {
                await repo.GitAsync("update-ref", "refs/heads/main", c2);
            }
        });
        var repository = await GitRepository.OpenAsync(runner, repo.Path, TestCancelled);

        var refusal = await RefuseAsync(repository, new MoveHead("main", c3, c1, MoveMode.Keep));
        Assert.Equal(new UndoRefusal(UndoRefusalKind.BranchMoved, Name: "main", Sha: c2, GitOutput: refusal.GitOutput), refusal);
        var after = await PictureAsync(repo);
        Assert.Equal(start with { Head = after.Head, Refs = after.Refs, Status = after.Status }, after);
        Assert.Equal("c-unstaged\n", ReadText(repo, "src/c.txt"));
        Assert.Equal("b2\n", ReadText(repo, "b.txt"));
    }

    [Fact]
    public async Task Ref_writes_are_one_transaction_written_whole_or_not_at_all()
    {
        using var repo = await UndoRepoAsync();
        var (c1, c2, c3) = (await ShaAsync(repo, "HEAD~2"), await ShaAsync(repo, "HEAD~1"), await ShaAsync(repo, "HEAD"));
        var feature = await ShaAsync(repo, "feature");
        var race = true;
        var runner = new InterceptingRunner(NewRunner(), async command =>
        {
            if (race && command.Arguments is ["update-ref", "-m", _, "--stdin"] && command.StandardInput!.Contains("refs/heads/feature", StringComparison.Ordinal))
            {
                race = false;
                await repo.GitAsync("update-ref", "refs/heads/feature", c1);
            }
        });
        var repository = await GitRepository.OpenAsync(runner, repo.Path, TestCancelled);
        var step = Step(UndoDirection.Undo, new SetRef("refs/heads/behind", c2, c3), new SetRef("refs/heads/feature", feature, c2), new TagRef("t1", c1, Present: true));

        var result = await repository.RunUndoStepAsync(step, NetworkOptions.None, TestCancelled);
        Assert.Equal(UndoRefusalKind.BranchMoved, result.Refusal!.Kind);
        Assert.Equal(("feature", c1), (result.Refusal.Name, result.Refusal.Sha));
        Assert.Equal(c2, await TargetAsync(repo, "refs/heads/behind"));
        Assert.Null(await TargetAsync(repo, "refs/tags/t1"));

        // Without the race: every write, with the step's message in the branches' reflogs.
        await repo.GitAsync("update-ref", "refs/heads/feature", feature);
        Assert.True((await repository.RunUndoStepAsync(step, NetworkOptions.None, TestCancelled)).IsDone);
        Assert.Equal((c3, c2, c1), (await TargetAsync(repo, "refs/heads/behind"), await TargetAsync(repo, "refs/heads/feature"), await TargetAsync(repo, "refs/tags/t1")));
        Assert.Equal("VisualCommit: undo effect", (await repo.GitAsync("log", "-g", "-1", "--format=%gs", "refs/heads/behind")).StandardOutput.Trim());
    }

    [Fact]
    public async Task A_switch_refused_names_every_path_in_its_way_and_ignored_ones_as_such()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);
        var main = new HeadState("main", await ShaAsync(repo, "main"));
        var feature = new HeadState("feature", await ShaAsync(repo, "feature"));

        // feature has its own src/c.txt and adds f.txt.
        repo.WriteFile("src/c.txt", "c-changed\n").WriteFile("f.txt", "untracked\n");
        var still = await StillAsync(repo);
        var local = await RefuseAsync(repository, new Switch(main, feature));
        Assert.Equal(UndoRefusalKind.LocalChangesInTheWay, local.Kind);
        Assert.Equal(["src/c.txt", "f.txt"], local.Paths);
        Assert.Contains("would be overwritten by checkout", local.GitOutput, StringComparison.Ordinal);
        Assert.Equal(still, await StillAsync(repo));

        // An ignored f.txt alone: --no-overwrite-ignore refuses instead of overwriting it.
        await repo.GitAsync("checkout", "--quiet", "--", "src/c.txt");
        File.WriteAllText(Path.Combine(repo.Path, ".git", "info", "exclude"), "f.txt\n");
        still = await StillAsync(repo);
        var ignored = await RefuseAsync(repository, new Switch(main, feature));
        Assert.Equal(UndoRefusalKind.IgnoredFilesInTheWay, ignored.Kind);
        Assert.Equal(["f.txt"], ignored.Paths);
        Assert.Equal("untracked\n", ReadText(repo, "f.txt"));
        Assert.Equal(still, await StillAsync(repo));

        File.Delete(Path.Combine(repo.Path, "f.txt"));
        await RunAsync(repository, new Switch(main, feature));
        Assert.Equal("feature", await repo.CurrentBranchAsync());
        Assert.Equal("refs/heads/main", (await repo.GitAsync("rev-parse", "--symbolic-full-name", "@{-1}")).StandardOutput.Trim());
    }

    [Fact]
    public async Task A_later_effects_refusal_reverses_what_the_step_applied()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);
        var main = new HeadState("main", await ShaAsync(repo, "main"));
        var (c1, feature) = (await ShaAsync(repo, "HEAD~2"), await ShaAsync(repo, "feature"));

        // The switch carries a.txt (alike on both branches); the move from feature's tip to c1 then refuses it.
        repo.WriteFile("a.txt", "a-changed\n");
        var start = await PictureAsync(repo);
        var refusal = await RefuseAsync(repository, new Switch(main, new HeadState("feature", feature)), new MoveHead("feature", feature, c1, MoveMode.Keep));

        Assert.Equal(UndoRefusalKind.LocalChangesInTheWay, refusal.Kind);
        Assert.Equal(["a.txt"], refusal.Paths);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.StartsWith($"{main.Sha} checkout: moving from feature to main", await HeadReflogAsync(repo), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Renames_configuration_remotes_and_tags_go_forth_and_back()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);
        var start = await PictureAsync(repo);
        var c1 = await ShaAsync(repo, "HEAD~2");
        IReadOnlyList<ConfigEntry> upstream = [new("branch.feature.remote", "origin"), new("branch.feature.merge", "refs/heads/feature")];
        IReadOnlyList<ConfigEntry> other = [new("branch.feature.remote", "origin"), new("branch.feature.merge", "refs/heads/main"), new("branch.feature.rebase", "true")];
        var url = (await repo.GitAsync("config", "remote.origin.url")).StandardOutput.Trim();

        UndoEffect[] effects =
        [
            new UndoEffect.RenameBranch("behind", "behind2"),
            new BranchConfig("feature", upstream, other),
            new RemoteRename("origin", "upstream"),
            new UndoEffect.RemoteUrl("upstream", url, "https://example.com/x.git"),
            new TagRef("t1", c1, Present: true),
        ];
        foreach (var effect in effects)
        {
            await RunAsync(repository, effect);
        }

        Assert.NotNull(await TargetAsync(repo, "refs/heads/behind2"));
        Assert.Equal("refs/heads/main", (await repo.GitAsync("config", "branch.feature.merge")).StandardOutput.Trim());
        Assert.Equal("https://example.com/x.git", (await repo.GitAsync("config", "remote.upstream.url")).StandardOutput.Trim());

        foreach (var effect in effects.Reverse())
        {
            await RunAsync(repository, Inverse(effect), UndoDirection.Redo);
        }

        Assert.Equal(start, await PictureAsync(repo));

        // Refused: a branch of the new name exists, a key changed since, a tag exists again.
        await repo.CreateBranchAsync("behind2");
        await repo.TagAsync("t1", "HEAD");
        var still = await StillAsync(repo);
        Assert.Equal(new UndoRefusal(UndoRefusalKind.BranchExists, Name: "behind2"), await RefuseAsync(repository, effects[0]));
        Assert.Equal(new UndoRefusal(UndoRefusalKind.ConfigChanged, Name: "feature"), await RefuseAsync(repository, new BranchConfig("feature", other, upstream)));
        Assert.Equal(new UndoRefusal(UndoRefusalKind.TagExists, Name: "t1"), await RefuseAsync(repository, effects[4]));
        Assert.Equal(still, await StillAsync(repo));
    }

    [Fact]
    public async Task Files_go_back_byte_for_byte_and_a_changed_one_refuses()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);
        var binary = new byte[] { 0, 1, 2, 0xFF, 0x0D, 0x0A, 0x0A, 0x80 };
        repo.WriteBytes("bin/data.bin", binary).WriteBytes("crlf.txt", Encoding.ASCII.GetBytes("one\r\ntwo\r\n"));
        await repo.ConfigAsync("core.autocrlf", "true");
        var recorded = await repository.RecordUndoFilesAsync(["bin/data.bin", "crlf.txt", "absent.txt"], TestCancelled);
        Assert.False(recorded[2].Exists);

        // Changed and deleted since: put back exactly, whatever core.autocrlf says (0.5).
        repo.WriteFile("crlf.txt", "changed\n").WriteFile("absent.txt", "now here\n");
        File.Delete(Path.Combine(repo.Path, "bin", "data.bin"));
        var now = await repository.RecordUndoFilesAsync(["bin/data.bin", "crlf.txt", "absent.txt"], TestCancelled);
        await RunAsync(repository, new FilesTo(now, recorded));
        Assert.Equal(binary, File.ReadAllBytes(Path.Combine(repo.Path, "bin", "data.bin")));
        Assert.Equal("one\r\ntwo\r\n", ReadText(repo, "crlf.txt"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "absent.txt")));

        // Back again: a file recorded as absent is deleted with the folders it leaves empty.
        await RunAsync(repository, new FilesTo(recorded, now));
        Assert.False(Directory.Exists(Path.Combine(repo.Path, "bin")));

        repo.WriteFile("crlf.txt", "edited once more\n");
        var still = await StillAsync(repo);
        var refusal = await RefuseAsync(repository, new FilesTo(now, recorded));
        Assert.Equal(new UndoRefusal(UndoRefusalKind.FilesChanged, Paths: refusal.Paths), refusal);
        Assert.Equal(["crlf.txt"], refusal.Paths);
        Assert.Equal(still, await StillAsync(repo));
    }

    [Fact]
    public async Task Index_entries_come_back_with_their_conflict_stages_and_flags_git_cannot_keep_refuse()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);
        await repo.CommitFileAsync("src/c.txt", "c-main\n", "c4");
        Assert.Equal(MergeResult.Conflicts, await repository.MergeAsync("refs/heads/feature", MergeMode.NoFastForward, cancellationToken: TestCancelled));
        var conflicted = await repository.ReadRefStatesAsync(new UndoScope { Files = ["src/c.txt"] }, cancellationToken: TestCancelled);
        Assert.Equal(3, conflicted.IndexEntries.Count);
        await repo.GitAsync("add", "src/c.txt");
        var resolved = await repository.ReadRefStatesAsync(new UndoScope { Files = ["src/c.txt"] }, cancellationToken: TestCancelled);

        // The merge is in progress: the effect alone is run as an apply's undo would, after it ends.
        EndMergeLeavingTheIndex(repo);
        await RunAsync(repository, new IndexEntries(["src/c.txt"], resolved.IndexEntries, conflicted.IndexEntries));
        Assert.Contains("UU src/c.txt", (await repo.GitAsync("status", "--porcelain")).StandardOutput, StringComparison.Ordinal);
        Assert.Equal(conflicted.IndexEntries, (await repository.ReadRefStatesAsync(new UndoScope { Files = ["src/c.txt"] }, cancellationToken: TestCancelled)).IndexEntries);

        // Intent-to-add and skip-worktree are lost by --index-info: refused.
        await repo.GitAsync("reset", "--quiet", "--hard");
        repo.WriteFile("y.txt", "y\n");
        await repo.GitAsync("add", "-N", "y.txt");
        await repo.GitAsync("update-index", "--skip-worktree", "a.txt");
        var flagged = await repository.ReadRefStatesAsync(new UndoScope { Files = ["a.txt", "y.txt"] }, cancellationToken: TestCancelled);
        var refusal = await RefuseAsync(repository, new IndexEntries(["a.txt", "y.txt"], flagged.IndexEntries, flagged.IndexEntries));
        Assert.Equal(UndoRefusalKind.FlagNotRestorable, refusal.Kind);
        Assert.Equal(["a.txt", "y.txt"], refusal.Paths);
    }

    [Fact]
    public async Task A_lease_that_fails_refuses_naming_the_remote_and_a_ref_already_there_counts_as_done()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);
        var (c1, c3) = (await ShaAsync(repo, "HEAD~2"), await ShaAsync(repo, "HEAD"));
        var remote = repo.SiblingPath("origin.git");

        var feature = await ShaAsync(repo, "feature");
        var refusal = await RefuseAsync(repository, new RemoteRef("origin", "refs/heads/feature", c1, c3));
        Assert.Equal((UndoRefusalKind.RemoteChanged, "feature", "origin"), (refusal.Kind, refusal.Name, refusal.Remote));
        Assert.Contains("(stale info)", refusal.GitOutput, StringComparison.Ordinal);
        Assert.Contains(feature, (await repo.GitAsync("ls-remote", remote, "refs/heads/feature")).StandardOutput, StringComparison.Ordinal);

        // Must not exist, and already holds the commit: Everything up-to-date (0.5).
        await RunAsync(repository, new RemoteRef("origin", "refs/heads/feature", null, feature));
        await RunAsync(repository, new RemoteRef("origin", "refs/heads/feature", feature, null));
        Assert.Empty((await repo.GitAsync("ls-remote", remote, "refs/heads/feature")).StandardOutput);
        Assert.Null(await TargetAsync(repo, "refs/remotes/origin/feature"));
        await RunAsync(repository, new RemoteRef("origin", "refs/heads/feature", null, feature));
        Assert.Equal(feature, await TargetAsync(repo, "refs/remotes/origin/feature"));
    }

    [Fact]
    public async Task A_step_is_refused_while_an_operation_is_in_progress_and_when_its_commits_are_gone()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);
        var (c1, c3) = (await ShaAsync(repo, "HEAD~2"), await ShaAsync(repo, "HEAD"));

        Assert.Equal(new UndoRefusal(UndoRefusalKind.CommitsGone), await RefuseAsync(repository, new MoveHead("main", c3, new string('1', 40), MoveMode.Soft)));

        await repo.CommitFileAsync("src/c.txt", "c-main\n", "c4");
        Assert.Equal(MergeResult.Conflicts, await repository.MergeAsync("refs/heads/feature", MergeMode.NoFastForward, cancellationToken: TestCancelled));
        Assert.Equal(new UndoRefusal(UndoRefusalKind.InProgress), await RefuseAsync(repository, new SetRef("refs/heads/behind", await ShaAsync(repo, "behind"), c1)));

        // Conflicts left without an operation (a stash's apply): refused too.
        EndMergeLeavingTheIndex(repo);
        var conflicts = await RefuseAsync(repository, new SetRef("refs/heads/behind", await ShaAsync(repo, "behind"), c1));
        Assert.Equal(UndoRefusalKind.Conflicts, conflicts.Kind);
        Assert.Equal(["src/c.txt"], conflicts.Paths);
    }

    // ---- Support -------------------------------------------------------------------------------

    /// <summary>A step made of <paramref name="effects"/> alone, for an entry with nothing recorded.</summary>
    private static UndoStep Step(UndoDirection direction, params UndoEffect[] effects)
    {
        var state = new UndoState { Head = new HeadState(null, null) };
        return new UndoStep(new UndoEntry(UndoKind.Commit, "effect", "op-1", state, state, UndoCaptured.None), direction, effects);
    }

    private static async Task RunAsync(GitRepository repository, UndoEffect effect, UndoDirection direction = UndoDirection.Undo)
    {
        var result = await repository.RunUndoStepAsync(Step(direction, effect), NetworkOptions.None, TestCancelled);
        Assert.True(result.IsDone, $"{effect} was refused: {result.Refusal}");
    }

    private static async Task<UndoRefusal> RefuseAsync(GitRepository repository, params UndoEffect[] effects)
    {
        var result = await repository.RunUndoStepAsync(Step(UndoDirection.Undo, effects), NetworkOptions.None, TestCancelled);
        Assert.False(result.IsDone, $"{string.Join(", ", effects.Select(effect => effect.ToString()))} was not refused.");
        return result.Refusal!;
    }

    private static UndoEffect Inverse(UndoEffect effect) => effect switch
    {
        UndoEffect.RenameBranch rename => new UndoEffect.RenameBranch(rename.To, rename.From),
        BranchConfig keys => keys with { From = keys.To, To = keys.From },
        RemoteRename rename => new RemoteRename(rename.To, rename.From),
        UndoEffect.RemoteUrl url => url with { From = url.To, To = url.From },
        TagRef tag => tag with { Present = !tag.Present },
        _ => throw new ArgumentOutOfRangeException(nameof(effect)),
    };

    /// <summary>Ends a stopped merge as a terminal's <c>git merge --quit</c> would, leaving its index: its files in the git folder go.</summary>
    private static void EndMergeLeavingTheIndex(TempRepo repo)
    {
        foreach (var name in new[] { "MERGE_HEAD", "MERGE_MSG", "MERGE_MODE" })
        {
            File.Delete(Path.Combine(repo.Path, ".git", name));
        }
    }

    private static async Task<string> ShaAsync(TempRepo repo, string revision) => (await repo.GitAsync("rev-parse", revision)).StandardOutput.Trim();

    /// <summary>What a refused step must leave as it was (7.5): the index's entries, every ref (the backups too), HEAD's reflog, every file and the stash list.</summary>
    private static async Task<Still> StillAsync(TempRepo repo)
    {
        var picture = await PictureAsync(repo);
        var refs = (await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname)")).StandardOutput;
        return new Still(picture.Head, refs, picture.Index, picture.Files, picture.Stashes, await HeadReflogAsync(repo));
    }

    private sealed record Still(string Head, string Refs, string Index, string Files, string Stashes, string HeadReflog);

    /// <summary>A runner that runs <paramref name="before"/> ahead of each call: a terminal racing the step.</summary>
    private sealed class InterceptingRunner(IGitRunner real, Func<GitCommand, Task> before) : IGitRunner
    {
        public string ExecutablePath => real.ExecutablePath;

        public async Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default)
        {
            await before(command);
            return await real.RunAsync(command, cancellationToken);
        }
    }
}
