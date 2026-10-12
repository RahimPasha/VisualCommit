using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// The history operations: reset, cherry-pick and revert of one commit (phase 3 design 2.11 to
/// 2.14), and phase 4's lists of picks and reverts with git's sequencer (docs/phase-4-design.md
/// section 4, D136, D145). The backups of a reset are saved before it by the caller
/// (<see cref="SaveBackupRefsAsync"/>, 2.0). Every read of a pick head goes through
/// <see cref="ReadPseudoRefAsync"/>, so that a reftable repository is read as a files one (8.1).
/// </summary>
public sealed partial class GitRepository
{
    private const string CherryPickHead = "CHERRY_PICK_HEAD";
    private const string RevertHead = "REVERT_HEAD";

    public Task ResetAsync(string commit, ResetMode mode, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);
        if (commit.StartsWith('-'))
        {
            // git reset refuses --end-of-options before its revision, and would take this for an option.
            throw new ArgumentException($"'{commit}' is no commit to reset to.", nameof(commit));
        }

        // "--" keeps a file of the commit's name from being taken for a path. --mixed lists the
        // files left unstaged on standard output unless it is quiet.
        string[] arguments = mode switch
        {
            ResetMode.Soft => ["reset", "--soft", commit, "--"],
            ResetMode.Mixed => ["reset", "--mixed", "--quiet", commit, "--"],
            ResetMode.Hard => ["reset", "--hard", commit, "--"],
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
        return RunWriteAsync(options, cancellationToken, arguments);
    }

    public Task<ApplyResult> CherryPickAsync(string commit, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);
        return ApplyCommitAsync(options, CherryPickHead, cancellationToken, ["cherry-pick", .. Positional(commit)]);
    }

    public Task<ApplyResult> RevertAsync(string commit, bool isMerge, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);

        // -m 1 undoes what the merge brought in from its second parent (D94).
        return isMerge
            ? ApplyCommitAsync(options, RevertHead, cancellationToken, ["revert", "--no-edit", "-m", "1", .. Positional(commit)])
            : ApplyCommitAsync(options, RevertHead, cancellationToken, ["revert", "--no-edit", .. Positional(commit)]);
    }

    /// <summary>
    /// Runs one cherry-pick or revert and tells how it ended. A failure with a conflicted file in
    /// the index stopped with conflicts. A failure that left the pick head <paramref name="pickHead"/>
    /// with no conflict and an index equal to HEAD's tree stopped on a commit that changes
    /// nothing (2.12): git keeps the operation in progress, and Abort ends it. One that left it with
    /// staged changes and no conflict stopped as with conflicts that are resolved: rerere (with
    /// <c>rerere.autoUpdate</c>) staged its recorded resolutions. Any other failure
    /// (local changes in the way, a hook that refused) throws <see cref="GitException"/> with git's
    /// own text. Git 2.36 writes no <c>REVERT_HEAD</c> for a revert that changes nothing: it fails
    /// with "nothing to commit" and leaves nothing in progress, which throws (D117).
    /// </summary>
    private async Task<ApplyResult> ApplyCommitAsync(NetworkOptions? options, string pickHead, CancellationToken cancellationToken, params string[] arguments)
    {
        // Conflicts that were there before make git refuse the call: a failure, not a stop.
        // So do a pick or revert already in progress.
        var conflictedBefore = await HasConflictsAsync(cancellationToken).ConfigureAwait(false);
        var stoppedBefore = await ReadPseudoRefAsync(pickHead, cancellationToken).ConfigureAwait(false) is not null;
        var command = WriteCommand(options, arguments);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
        {
            return ApplyResult.Applied;
        }

        if (conflictedBefore || stoppedBefore)
        {
            result.EnsureSuccess(command);
        }

        if (await HasConflictsAsync(cancellationToken).ConfigureAwait(false))
        {
            return ApplyResult.Conflicts;
        }

        if (await ReadPseudoRefAsync(pickHead, cancellationToken).ConfigureAwait(false) is not null)
        {
            // In progress with nothing conflicted: empty, or changes staged that wait for their
            // commit, as when rerere (with rerere.autoUpdate) staged its recorded resolutions.
            return await IndexEqualsHeadAsync(cancellationToken).ConfigureAwait(false) ? ApplyResult.Empty : ApplyResult.Conflicts;
        }

        result.EnsureSuccess(command);
        return ApplyResult.Applied; // Not reached: the call failed.
    }

    // ---- Lists with git's sequencer (section 4, D136, D145) ---------------------------------------

    public Task<SequenceResult> CherryPickAsync(IReadOnlyList<string> commitsOldestFirst, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        ApplyListAsync(InProgressKind.CherryPick, commitsOldestFirst, ["cherry-pick"], options, cancellationToken);

    public Task<SequenceResult> RevertAsync(IReadOnlyList<string> commitsNewestFirst, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        ApplyListAsync(InProgressKind.Revert, commitsNewestFirst, ["revert", "--no-edit"], options, cancellationToken);

    public Task QuitSequenceAsync(InProgressKind kind, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        RunWriteAsync(options, cancellationToken, SequenceVerb(kind), "--quit");

    /// <summary>
    /// Runs one <c>git cherry-pick</c> or <c>git revert --no-edit</c> with the whole list and reads
    /// how it ended from git's files (section 4's table; "pick head" is <c>CHERRY_PICK_HEAD</c> or
    /// <c>REVERT_HEAD</c>, read through <see cref="ReadPseudoRefAsync"/>):
    /// <list type="bullet">
    /// <item>exit 0: <see cref="SequenceStop.None"/>;</item>
    /// <item>a pick head and conflicted files: <see cref="SequenceStop.Conflicts"/>; a pick head,
    /// nothing conflicted and the index equal to HEAD: <see cref="SequenceStop.Empty"/>; with
    /// the index not equal to HEAD (rerere's <c>autoUpdate</c>, a refusing hook):
    /// <see cref="SequenceStop.Conflicts"/> with 0 files (D128);</item>
    /// <item>no pick head, <c>sequencer/</c> and <c>MERGE_MSG</c>: an empty revert (also the
    /// first of the list), <see cref="SequenceStop.Empty"/>;</item>
    /// <item>no pick head, <c>sequencer/</c>, no <c>MERGE_MSG</c>, HEAD at <c>sequencer/head</c>:
    /// refused at the start; git wrote <c>sequencer/</c> anyway, so <c>--quit</c> removes it and
    /// keeps the user's changes (<c>--abort</c> would drop staged ones), then git's failure throws
    /// (D145);</item>
    /// <item>no pick head, <c>sequencer/</c>, no <c>MERGE_MSG</c>, HEAD moved: a later commit git
    /// could not apply without a conflict (an untracked file in the way),
    /// <see cref="SequenceStop.Refused"/>;</item>
    /// <item>anything else that failed: <see cref="GitException"/> with git's text, as does a call
    /// that git refused because conflicts or a pick, revert or list were there before (then
    /// nothing of this call is quit).</item>
    /// </list>
    /// <see cref="SequenceResult.Applied"/> is the list's length less the commands left in
    /// <c>sequencer/todo</c> (the stopped one is still its first line); a single commit that
    /// stopped writes no <c>sequencer/</c>, and none of it is applied.
    /// </summary>
    private async Task<SequenceResult> ApplyListAsync(InProgressKind kind, IReadOnlyList<string> commits, string[] verb, NetworkOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commits);
        if (commits.Count == 0)
        {
            throw new ArgumentException("The list of commits is empty.", nameof(commits));
        }

        foreach (var commit in commits)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(commit, nameof(commits));
        }

        var pickHead = kind == InProgressKind.CherryPick ? CherryPickHead : RevertHead;
        var sequencer = Path.Combine(GitDirectory, "sequencer");
        var inProgressBefore = await HasConflictsAsync(cancellationToken).ConfigureAwait(false)
            || Directory.Exists(sequencer)
            || await ReadPseudoRefAsync(CherryPickHead, cancellationToken).ConfigureAwait(false) is not null
            || await ReadPseudoRefAsync(RevertHead, cancellationToken).ConfigureAwait(false) is not null;
        var command = WriteCommand(options, [.. verb, .. Positional([.. commits])]);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        var total = commits.Count;
        if (result.Succeeded)
        {
            return new SequenceResult(SequenceStop.None, total, total);
        }

        if (inProgressBefore)
        {
            result.EnsureSuccess(command);
        }

        var sequenced = Directory.Exists(sequencer);
        var applied = sequenced
            ? Math.Max(0, total - CountTodoLines(ReadGitFile("sequencer", "todo")))
            : total - 1;
        if (await ReadPseudoRefAsync(pickHead, cancellationToken).ConfigureAwait(false) is not null)
        {
            var stop = await HasConflictsAsync(cancellationToken).ConfigureAwait(false) || !await IndexEqualsHeadAsync(cancellationToken).ConfigureAwait(false)
                ? SequenceStop.Conflicts
                : SequenceStop.Empty;
            return new SequenceResult(stop, applied, total);
        }

        if (sequenced)
        {
            if (File.Exists(Path.Combine(GitDirectory, "MERGE_MSG")))
            {
                return new SequenceResult(SequenceStop.Empty, applied, total);
            }

            var head = await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false);
            if (!string.Equals(head, FirstLine(ReadGitFile("sequencer", "head")), StringComparison.Ordinal))
            {
                return new SequenceResult(SequenceStop.Refused, applied, total);
            }

            // Refused at the start: nothing was applied, but git wrote its list.
            var quit = WriteCommand(options, SequenceVerb(kind), "--quit");
            var quitResult = await _runner.RunAsync(quit, cancellationToken).ConfigureAwait(false);
            if (!quitResult.Succeeded)
            {
                _log.Warning($"{Name}: {quit.DisplayText} failed after a refused {command.DisplayText}: {quitResult.StandardError.Trim()}");
            }
        }

        result.EnsureSuccess(command);
        return new SequenceResult(SequenceStop.None, total, total); // Not reached: the call failed.
    }

    /// <summary>The git command of a list's kind: <c>cherry-pick</c> or <c>revert</c>.</summary>
    private static string SequenceVerb(InProgressKind kind) => kind switch
    {
        InProgressKind.CherryPick => "cherry-pick",
        InProgressKind.Revert => "revert",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only a cherry-pick or a revert has a list."),
    };

    /// <summary>Whether the index holds HEAD's tree: <c>git diff-index --cached --quiet HEAD</c> exits 0 when it does, 1 when not.</summary>
    private async Task<bool> IndexEqualsHeadAsync(CancellationToken cancellationToken)
    {
        var command = Read("diff-index", "--cached", "--quiet", "HEAD", "--");
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        return result.ExitCode switch
        {
            0 => true,
            1 => false,
            _ => throw new GitException(command.DisplayText, result.ExitCode, result.StandardError, standardOutput: result.StandardOutput),
        };
    }
}
