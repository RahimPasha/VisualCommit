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
        return ApplyCommitAsync(options, "CHERRY_PICK_HEAD", cancellationToken, "cherry-pick", commit);
    }

    public Task<ApplyResult> RevertAsync(string commit, bool isMerge, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);

        // -m 1 undoes what the merge brought in from its second parent (D94).
        return isMerge
            ? ApplyCommitAsync(options, "REVERT_HEAD", cancellationToken, "revert", "--no-edit", "-m", "1", commit)
            : ApplyCommitAsync(options, "REVERT_HEAD", cancellationToken, "revert", "--no-edit", commit);
    }

    /// <summary>
    /// Runs one cherry-pick or revert and tells how it ended. A failure with a conflicted file in
    /// the index stopped with conflicts. A failure that left <paramref name="stateFile"/> in the git
    /// folder with no conflict and an index equal to HEAD's tree stopped on a commit that changes
    /// nothing (2.12): git keeps the operation in progress, and Abort ends it. Any other failure
    /// (local changes in the way, a hook that refused) throws <see cref="GitException"/> with git's
    /// own text. Git 2.36 writes no <c>REVERT_HEAD</c> for a revert that changes nothing: it fails
    /// with "nothing to commit" and leaves nothing in progress, which throws.
    /// </summary>
    private async Task<ApplyResult> ApplyCommitAsync(NetworkOptions? options, string stateFile, CancellationToken cancellationToken, params string[] arguments)
    {
        // Conflicts that were there before make git refuse the call: a failure, not a stop.
        var conflictedBefore = await HasConflictsAsync(cancellationToken).ConfigureAwait(false);
        var command = WriteCommand(options, arguments);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
        {
            return ApplyResult.Applied;
        }

        if (conflictedBefore)
        {
            result.EnsureSuccess(command);
        }

        if (await HasConflictsAsync(cancellationToken).ConfigureAwait(false))
        {
            return ApplyResult.Conflicts;
        }

        if (File.Exists(Path.Combine(GitDirectory, stateFile)) && await IndexEqualsHeadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ApplyResult.Empty;
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
