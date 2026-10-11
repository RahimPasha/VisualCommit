using System.Globalization;
using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Phase 3's branch operations and the reads they rely on (design 2.1 to 2.10, D93, D94): checkouts,
/// create, rename, delete, the fast-forward of a branch that is not checked out, and merge. Each
/// write is one git call (merge: two) that throws <see cref="GitException"/> with git's own text
/// when git refuses; a merge's conflicts are returned, not thrown. The queue and the confirmations
/// around them are the caller's (WP7).
/// </summary>
public sealed partial class GitRepository
{
    // ---- Reads (GIT_OPTIONAL_LOCKS=0) ------------------------------------------------------------

    public async Task<int> CountDetachedCommitsAsync(CancellationToken cancellationToken = default)
    {
        // An unborn HEAD has no commits to leave behind, and rev-list would refuse the name.
        if (await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false) is null)
        {
            return 0;
        }

        var output = await ReadOutputAsync(cancellationToken, "rev-list", "--count", "HEAD", "--not", "--branches", "--remotes", "--tags", "--")
            .ConfigureAwait(false);
        return ParseCount(output);
    }

    public async Task<bool> IsAncestorAsync(string ancestor, string descendant, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ancestor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descendant);
        var command = Read("merge-base", "--is-ancestor", ancestor, descendant);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        return result.ExitCode switch
        {
            0 => true,
            1 => false,
            _ => throw new GitException(command.DisplayText, result.ExitCode, result.StandardError, standardOutput: result.StandardOutput),
        };
    }

    public async Task<int> CountCommitsAsync(string from, string to, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        var output = await ReadOutputAsync(cancellationToken, "rev-list", "--count", $"{from}..{to}", "--").ConfigureAwait(false);
        return ParseCount(output);
    }

    public async Task<string?> ResolveAsync(string revision, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        var command = Read("rev-parse", "--verify", "--quiet", revision);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1)
        {
            return null; // --quiet: the name names nothing.
        }

        var sha = result.EnsureSuccess(command).StandardOutput.Trim();
        return sha.Length > 0 ? sha : null;
    }

    public async Task<IReadOnlyList<WorktreeInfo>> ReadWorktreesAsync(CancellationToken cancellationToken = default)
    {
        var output = await ReadOutputAsync(cancellationToken, "worktree", "list", "--porcelain").ConfigureAwait(false);
        return ParseWorktrees(output);
    }

    /// <summary>
    /// Reads <c>git worktree list --porcelain</c>: blocks separated by an empty line, each starting
    /// with <c>worktree &lt;path&gt;</c> and holding <c>branch &lt;full ref&gt;</c>, <c>detached</c>
    /// or <c>bare</c> among other lines. The main worktree comes first. Paths are printed with
    /// forward slashes on every platform; they get the platform's separators here.
    /// </summary>
    internal static IReadOnlyList<WorktreeInfo> ParseWorktrees(string output)
    {
        var worktrees = new List<WorktreeInfo>();
        string? path = null;
        string? branch = null;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                Flush();
                path = line["worktree ".Length..];
            }
            else if (line.StartsWith("branch ", StringComparison.Ordinal))
            {
                branch = line["branch ".Length..];
            }
            else if (line.Length == 0)
            {
                Flush();
            }
        }

        Flush();
        return worktrees;

        void Flush()
        {
            if (path is not null)
            {
                var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
                worktrees.Add(new WorktreeInfo(full, branch, IsMain: worktrees.Count == 0));
            }

            path = null;
            branch = null;
        }
    }

    // ---- Checkouts (2.1 to 2.3) -----------------------------------------------------------------

    public Task CheckoutBranchAsync(string branch, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);

        // switch takes only branches, so a tag of the same name cannot be checked out instead,
        // and --no-guess keeps it from creating a branch out of a remote one of that name.
        return RunWriteAsync(options, cancellationToken, "switch", "--no-guess", branch);
    }

    public Task CheckoutTrackingBranchAsync(string branch, string remoteBranchRef, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        RequirePrefix(remoteBranchRef, RemotesPrefix, nameof(remoteBranchRef));
        return RunWriteAsync(options, cancellationToken, "switch", "--create", branch, "--track", remoteBranchRef);
    }

    public Task CheckoutDetachedAsync(string commit, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);
        return RunWriteAsync(options, cancellationToken, "switch", "--detach", commit);
    }

    // ---- Create, rename, delete (2.4 to 2.6) -----------------------------------------------------

    public Task CreateBranchAsync(string name, string startPoint, bool checkout, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(startPoint);
        return checkout
            ? RunWriteAsync(options, cancellationToken, "switch", "--create", name, startPoint)
            : RunWriteAsync(options, cancellationToken, "branch", name, startPoint);
    }

    public Task RenameBranchAsync(string branch, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        return RunWriteAsync(null, cancellationToken, "branch", "--move", branch, newName);
    }

    public Task DeleteBranchAsync(string branch, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        return RunWriteAsync(null, cancellationToken, "branch", "--delete", "--force", branch);
    }

    // ---- Fast-forward a branch that is not checked out (2.7) -------------------------------------

    public Task FastForwardBranchAsync(string branch, string target, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        // "." is this repository: no network and no prompt. Git refuses a non-fast-forward and a
        // branch checked out in any worktree, so a branch that moved meanwhile is never damaged.
        return RunWriteAsync(
            null,
            cancellationToken,
            "fetch", "--no-auto-maintenance", "--no-write-fetch-head", ".", $"{target}:{HeadsPrefix}{branch}");
    }

    // ---- Merge (2.8 to 2.10, D94) ----------------------------------------------------------------

    public async Task<MergeResult> MergeAsync(string reference, MergeMode mode, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        // A name that is no commit gets no message; git's merge then refuses it in its own words.
        var target = await ResolveAsync(reference + "^{commit}", cancellationToken).ConfigureAwait(false);
        var before = await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false);
        var conflictedBefore = await HasConflictsAsync(cancellationToken).ConfigureAwait(false);

        var arguments = new List<string> { "merge" };
        if (mode == MergeMode.FastForwardOnly)
        {
            arguments.Add("--ff-only");
        }
        else
        {
            arguments.AddRange(["--no-edit", "--no-log"]);
            var message = target is null ? string.Empty : await MergeMessageAsync(reference, target, cancellationToken).ConfigureAwait(false);
            if (message.Length > 0)
            {
                arguments.AddRange(["-m", message]);
            }

            if (mode == MergeMode.NoFastForward)
            {
                arguments.Add("--no-ff");
            }
        }

        arguments.Add(reference);
        var command = WriteCommand(options, [.. arguments]);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            // Conflicts the merge made, not ones that were there before and made git refuse it.
            if (!conflictedBefore && await HasConflictsAsync(cancellationToken).ConfigureAwait(false))
            {
                return MergeResult.Conflicts;
            }

            result.EnsureSuccess(command);
        }

        var after = await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false);
        if (string.Equals(after, before, StringComparison.Ordinal))
        {
            return MergeResult.UpToDate;
        }

        return string.Equals(after, target, StringComparison.Ordinal) ? MergeResult.FastForward : MergeResult.Merged;
    }

    /// <summary>
    /// The message <c>git merge &lt;short name&gt;</c> would write, from <c>git fmt-merge-msg</c> (D94):
    /// "Merge branch 'feature/alpha'", "Merge remote-tracking branch 'origin/main'" or "Merge tag
    /// 'v1.0'", with " into &lt;current&gt;" and the shortlog where git's configuration adds them.
    /// Empty for anything else (git then writes its own) and when git prints nothing, as it does
    /// for a commit that is already merged; the merge then has nothing to commit.
    /// </summary>
    private async Task<string> MergeMessageAsync(string reference, string target, CancellationToken cancellationToken)
    {
        string? description = null;
        if (reference.StartsWith(HeadsPrefix, StringComparison.Ordinal))
        {
            description = $"branch '{reference[HeadsPrefix.Length..]}'";
        }
        else if (reference.StartsWith(RemotesPrefix, StringComparison.Ordinal))
        {
            description = $"remote-tracking branch '{reference[RemotesPrefix.Length..]}'";
        }
        else if (reference.StartsWith(TagsPrefix, StringComparison.Ordinal))
        {
            description = $"tag '{reference[TagsPrefix.Length..]}'";
        }

        if (description is null)
        {
            return string.Empty;
        }

        var command = new GitCommand("fmt-merge-msg")
        {
            WorkingDirectory = WorkingDirectory,
            Environment = ReadEnvironment,
            StandardInput = $"{target}\t\t{description} of .\n",
        };
        var result = (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
        return result.StandardOutput.TrimEnd('\r', '\n');
    }

    // ---- Shared by the files of WP1 --------------------------------------------------------------

    /// <summary>
    /// A call that writes, in the working tree, with the operation's prompt variables
    /// (<see cref="NetworkOptions.Environment"/>, design 1.5).
    /// </summary>
    private GitCommand WriteCommand(NetworkOptions? options, params string[] arguments) =>
        new(arguments) { WorkingDirectory = WorkingDirectory, Environment = (options ?? NetworkOptions.None).Environment };

    /// <summary>Runs a call that writes; a failure throws <see cref="GitException"/> with git's own text.</summary>
    private async Task RunWriteAsync(NetworkOptions? options, CancellationToken cancellationToken, params string[] arguments)
    {
        var command = WriteCommand(options, arguments);
        (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
    }

    /// <summary>Whether the index has a conflicted entry: how a merging operation that failed is told from one that stopped (2.8).</summary>
    private async Task<bool> HasConflictsAsync(CancellationToken cancellationToken)
    {
        var output = await ReadOutputAsync(cancellationToken, "ls-files", "--unmerged", "-z").ConfigureAwait(false);
        return output.Length > 0;
    }

    private static int ParseCount(string output) =>
        int.Parse(output.Trim(), NumberStyles.None, CultureInfo.InvariantCulture);

    private static void RequirePrefix(string fullName, string prefix, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName, parameterName);
        if (!fullName.StartsWith(prefix, StringComparison.Ordinal) || fullName.Length == prefix.Length)
        {
            throw new ArgumentException($"A full ref name under {prefix} is needed, not '{fullName}'.", parameterName);
        }
    }
}
