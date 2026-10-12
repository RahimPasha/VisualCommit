using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Phase 3's history operations (design 2.11 to 2.14): reset, and cherry-pick and revert of one
/// commit at a time. The app never starts git's sequencer (2.13, D88): several commits are picked
/// or reverted by calling these once per commit. The backups of a reset are saved before it by the
/// caller (<see cref="SaveBackupRefsAsync"/>, 2.0).
/// </summary>
public sealed partial class GitRepository
{
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
        return ApplyCommitAsync(options, "CHERRY_PICK_HEAD", cancellationToken, ["cherry-pick", .. Positional(commit)]);
    }

    public Task<ApplyResult> RevertAsync(string commit, bool isMerge, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);

        // -m 1 undoes what the merge brought in from its second parent (D94).
        return isMerge
            ? ApplyCommitAsync(options, "REVERT_HEAD", cancellationToken, ["revert", "--no-edit", "-m", "1", .. Positional(commit)])
            : ApplyCommitAsync(options, "REVERT_HEAD", cancellationToken, ["revert", "--no-edit", .. Positional(commit)]);
    }

    /// <summary>
    /// Runs one cherry-pick or revert and tells how it ended. A failure with a conflicted file in
    /// the index stopped with conflicts. A failure that left <paramref name="stateFile"/> in the git
    /// folder with no conflict and an index equal to HEAD's tree stopped on a commit that changes
    /// nothing (2.12): git keeps the operation in progress, and Abort ends it. One that left it with
    /// staged changes and no conflict stopped as with conflicts that are resolved: rerere (with
    /// <c>rerere.autoUpdate</c>) staged its recorded resolutions. Any other failure
    /// (local changes in the way, a hook that refused) throws <see cref="GitException"/> with git's
    /// own text. Git 2.36 writes no <c>REVERT_HEAD</c> for a revert that changes nothing: it fails
    /// with "nothing to commit" and leaves nothing in progress, which throws.
    /// Known issue: under the reftable ref format (Git 2.45+) <c>CHERRY_PICK_HEAD</c> and <c>REVERT_HEAD</c> are no files, so a stop there is not seen.
    /// </summary>
    private async Task<ApplyResult> ApplyCommitAsync(NetworkOptions? options, string stateFile, CancellationToken cancellationToken, params string[] arguments)
    {
        // Conflicts that were there before make git refuse the call: a failure, not a stop.
        // So do a pick or revert already in progress.
        var conflictedBefore = await HasConflictsAsync(cancellationToken).ConfigureAwait(false);
        var statePath = Path.Combine(GitDirectory, stateFile);
        var stoppedBefore = File.Exists(statePath);
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

        if (File.Exists(statePath))
        {
            // In progress with nothing conflicted: empty, or changes staged that wait for their
            // commit, as when rerere (with rerere.autoUpdate) staged its recorded resolutions.
            return await IndexEqualsHeadAsync(cancellationToken).ConfigureAwait(false) ? ApplyResult.Empty : ApplyResult.Conflicts;
        }

        result.EnsureSuccess(command);
        return ApplyResult.Applied; // Not reached: the call failed.
    }

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
