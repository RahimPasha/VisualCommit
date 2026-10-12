using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>Stash save, apply, pop and drop (docs/phase-3-design.md 2.24 to 2.26, 2.31; D91, D108).</summary>
public sealed partial class GitRepository
{
    /// <summary>
    /// The standard error of the <see cref="GitException"/> that <see cref="ApplyStashAsync"/> and
    /// <see cref="DropStashAsync"/> throw for a stash that is no longer listed (2.25).
    /// </summary>
    public const string StashGoneMessage = "This stash no longer exists.";

    /// <summary>What git prints, with exit code 0, when <c>git stash push</c> found nothing to stash.</summary>
    private const string NothingToStash = "No local changes to save";

    /// <summary>What <c>git stash pop --index</c> says, changing nothing, when the stash's index part does not apply (section 0).</summary>
    private const string StashIndexConflict = "conflicts in index. Try without --index.";

    public async Task<string?> StashAsync(string? message, bool includeUntracked, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        var arguments = new List<string> { "stash", "push" };
        if (includeUntracked)
        {
            arguments.Add("--include-untracked");
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            arguments.Add("--message");
            arguments.Add(message);
        }

        // Without GIT_LITERAL_PATHSPECS (D108): git removes the stashed untracked files with
        // "git clean ... :/", and the variable would make ":/" a file name, leaving them behind.
        // The app passes no path here, so the variable protects nothing.
        var command = StashCommand(options, literalPathspecs: false, [.. arguments]);
        var result = (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
        if (result.StandardOutput.Contains(NothingToStash, StringComparison.Ordinal)
            || result.StandardError.Contains(NothingToStash, StringComparison.Ordinal))
        {
            return null;
        }

        var stash = await _runner.RunAsync(Read("rev-parse", "--verify", "--quiet", "refs/stash"), cancellationToken).ConfigureAwait(false);
        return stash.Succeeded ? stash.StandardOutput.Trim() : null;
    }

    public async Task<ApplyResult> ApplyStashAsync(string stashCommit, StashApplyMode mode, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stashCommit);
        var selector = await StashSelectorAsync(stashCommit, cancellationToken).ConfigureAwait(false);

        // A conflict that was there before cannot be told apart from one this call made, so with
        // one any failure is thrown as it is (git refuses to apply onto an unmerged index anyway).
        var conflictedBefore = await StashHasUnmergedFilesAsync(cancellationToken).ConfigureAwait(false);

        string[] arguments = mode switch
        {
            StashApplyMode.Apply => ["stash", "apply", selector],
            StashApplyMode.Pop => ["stash", "pop", selector],
            StashApplyMode.PopKeepingIndex => ["stash", "pop", "--index", selector],
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };

        var statusBefore = mode == StashApplyMode.PopKeepingIndex
            ? await StashStatusSnapshotAsync(cancellationToken).ConfigureAwait(false)
            : null;

        var command = StashCommand(options, literalPathspecs: true, arguments);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
        {
            return ApplyResult.Applied;
        }

        // D91: the index part of the stash does not apply onto the new branch; git refused and
        // changed nothing, so a plain pop follows, which applies the changes unstaged.
        if (mode == StashApplyMode.PopKeepingIndex
            && result.StandardError.Contains(StashIndexConflict, StringComparison.Ordinal)
            && !conflictedBefore
            && string.Equals(statusBefore, await StashStatusSnapshotAsync(cancellationToken).ConfigureAwait(false), StringComparison.Ordinal))
        {
            _log.Info($"git stash pop --index could not restore the index of {selector}; popping it without --index.");
            command = StashCommand(options, literalPathspecs: true, "stash", "pop", selector);
            result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                return ApplyResult.Applied;
            }
        }

        if (!conflictedBefore && await StashHasUnmergedFilesAsync(cancellationToken).ConfigureAwait(false))
        {
            // A pop that conflicts keeps the entry ("The stash entry is kept in case you need it again.").
            return ApplyResult.Conflicts;
        }

        // Failed without conflicts: local changes in the way, untracked files that already exist.
        throw new GitException(command.DisplayText, result.ExitCode, result.StandardError, standardOutput: result.StandardOutput);
    }

    public async Task DropStashAsync(string stashCommit, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stashCommit);
        var selector = await StashSelectorAsync(stashCommit, cancellationToken).ConfigureAwait(false);

        // "git stash drop <sha>" is refused ("is not a stash reference"): only stash@{n} works (section 0).
        var command = StashCommand(null, literalPathspecs: true, "stash", "drop", selector);
        (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
    }

    /// <summary>
    /// <c>stash@{n}</c> for the stash commit <paramref name="stashCommit"/>, found by its id in
    /// <c>git stash list</c>: the list may have changed since the app read it (2.25).
    /// </summary>
    /// <exception cref="GitException">The stash is no longer listed: <see cref="StashGoneMessage"/>.</exception>
    private async Task<string> StashSelectorAsync(string stashCommit, CancellationToken cancellationToken)
    {
        string[] arguments = ["stash", "list", "--format=%H"];
        var output = await ReadOutputAsync(cancellationToken, arguments).ConfigureAwait(false);
        var commits = SplitLines(output);
        var index = commits.FindIndex(commit => string.Equals(commit, stashCommit.Trim(), StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            throw new GitException(new GitCommand(arguments).DisplayText, 0, StashGoneMessage);
        }

        return $"stash@{{{index}}}";
    }

    /// <summary>Whether the index has a conflicted (unmerged) file: <c>git ls-files --unmerged</c>.</summary>
    private async Task<bool> StashHasUnmergedFilesAsync(CancellationToken cancellationToken) =>
        (await ReadOutputAsync(cancellationToken, "ls-files", "--unmerged", "-z").ConfigureAwait(false)).Length > 0;

    /// <summary>
    /// The working tree's status with the index's object ids (<c>git status --porcelain=v2</c>):
    /// equal before and after a call when the call changed nothing that a pop can change.
    /// </summary>
    private Task<string> StashStatusSnapshotAsync(CancellationToken cancellationToken) =>
        ReadOutputAsync(cancellationToken, "status", "--porcelain=v2", "-z", "--untracked-files=all");

    /// <summary>
    /// A stash call that writes: the operation's prompt variables (a filter can prompt, 1.5), and
    /// <c>GIT_LITERAL_PATHSPECS</c> removed where <paramref name="literalPathspecs"/> is false (D108).
    /// </summary>
    private GitCommand StashCommand(NetworkOptions? options, bool literalPathspecs, params string[] arguments)
    {
        var environment = new Dictionary<string, string?>();
        foreach (var (name, value) in (options ?? NetworkOptions.None).Environment)
        {
            environment[name] = value;
        }

        if (!literalPathspecs)
        {
            environment["GIT_LITERAL_PATHSPECS"] = null;
        }

        return new GitCommand(arguments) { WorkingDirectory = WorkingDirectory, Environment = environment };
    }
}
