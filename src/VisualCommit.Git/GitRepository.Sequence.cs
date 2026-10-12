using System.Globalization;
using System.Text;
using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Continue and Skip for a merge, a cherry-pick or revert (single or a list), and a rebase
/// (docs/phase-4-design.md 5.1, 5.4), and the message the stage panel fills in for the stopped
/// commit (5.3). Each runs inside one operation of the queue; the question and the backups of Skip
/// are the app's. A call git refuses (unresolved files, nothing to skip, a refusing hook) throws
/// <see cref="GitException"/> with git's own text and changes nothing; a call that goes on and
/// stops again returns <see cref="ContinueOutcome.Stopped"/>, and the next status read says why.
/// </summary>
public sealed partial class GitRepository
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// The first line of the standard error of the <see cref="GitException"/> that Continue of a
    /// rebase throws, before anything runs, while a tracked file that is not conflicted has
    /// unstaged changes (git's own <c>--continue</c> refuses then, after the app would already have
    /// committed); the files follow, one per line.
    /// </summary>
    public const string RebaseUnstagedChangesMessage = "Tracked files have unstaged changes, and git continues a rebase only when nothing else is unstaged:";

    public async Task<ContinueResult> ContinueAsync(InProgressState state, string? message, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        message = string.IsNullOrWhiteSpace(message) ? null : message;
        switch (state.Kind)
        {
            case InProgressKind.Merge:
                await CommitStoppedAsync(state, message, options, cancellationToken).ConfigureAwait(false);
                return await FinishedAsync(cancellationToken).ConfigureAwait(false);

            case InProgressKind.CherryPick or InProgressKind.Revert when state.HasPickHead:
                // The stopped commit with the panel's message (D110: git's own --continue would
                // strip the commit's "#" lines); git then leaves the committed line out of the list.
                await CommitStoppedAsync(state, message, options, cancellationToken).ConfigureAwait(false);
                return state.Sequenced
                    ? await RunSequenceAsync(state.Kind, "--continue", options, cancellationToken).ConfigureAwait(false)
                    : await FinishedAsync(cancellationToken).ConfigureAwait(false);

            case InProgressKind.CherryPick or InProgressKind.Revert when state.Sequenced:
                if (!state.AbortKeepsHead && !state.Empty)
                {
                    // Stopped on a commit git could not apply (section 4, "Refused"): --continue
                    // would leave it out without a word (0.3). Skip says so; Continue does not.
                    throw new InvalidOperationException(
                        $"Git could not apply {Short(state.Commit)}, and continuing would leave it out without a word: skip it or abort.");
                }

                return await RunSequenceAsync(state.Kind, "--continue", options, cancellationToken).ConfigureAwait(false);

            case InProgressKind.CherryPick or InProgressKind.Revert:
                throw new ArgumentException("No cherry-pick or revert is stopped: there is nothing to continue.", nameof(state));

            case InProgressKind.Rebase:
                return await ContinueRebaseAsync(state, message, options, cancellationToken).ConfigureAwait(false);

            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "This operation is continued in a terminal, or nothing is in progress.");
        }
    }

    public async Task<ContinueResult> SkipAsync(InProgressState state, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        switch (state.Kind)
        {
            case InProgressKind.CherryPick or InProgressKind.Revert:
                return await RunSequenceAsync(state.Kind, "--skip", options, cancellationToken).ConfigureAwait(false);

            case InProgressKind.Rebase when state.StopKind == RebaseStopKind.OwnAmendFailed:
                // The app's amend failed: going on without the new message is git's --continue,
                // which does not run a failed exec again (no-reschedule-failed-exec, 0.1); it
                // refuses as Continue does while other files are unstaged.
                await RefuseUnstagedChangesAsync(options, cancellationToken).ConfigureAwait(false);
                return await RebaseStepAsync("--continue", options, cancellationToken).ConfigureAwait(false);

            case InProgressKind.Rebase:
                return await RebaseStepAsync("--skip", options, cancellationToken).ConfigureAwait(false);

            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Kind, "There is no skip for this operation.");
        }
    }

    public async Task<string?> ReadOperationMessageAsync(InProgressState state, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        switch (state.Kind)
        {
            case InProgressKind.Merge:
            case InProgressKind.CherryPick or InProgressKind.Revert when !state.Sequenced || state.HasPickHead:
                return ReadGitFile("MERGE_MSG") is { } mergeMessage
                    ? Tidy(WithoutConflictsBlock(mergeMessage, await CommentCharAsync(cancellationToken).ConfigureAwait(false)))
                    : null;

            case InProgressKind.Rebase when Directory.Exists(Path.Combine(GitDirectory, "rebase-merge")):
                if (state.Group is { } group && ReadFile(MessageFilePath(group)) is { } groupMessage)
                {
                    return Tidy(groupMessage);
                }

                if (ReadGitFile("rebase-merge", "message") is not { } message)
                {
                    return null;
                }

                // A fixup or squash of a rebase started elsewhere: git's own message, which it
                // commits without its comment lines. Otherwise the commit's message, less the
                // "Conflicts:" block that newer git (2.55 on CI) appends to it at a conflict stop
                // and 2.36 does not.
                var comment = await CommentCharAsync(cancellationToken).ConfigureAwait(false);
                return LastDoneCommand() is { IsSquashLike: true }
                    ? Tidy(WithoutCommentLines(message, comment))
                    : Tidy(WithoutConflictsBlock(message, comment));

            case InProgressKind.Rebase:
                return ReadGitFile("rebase-apply", "final-commit") is { } patchMessage ? Tidy(patchMessage) : null;

            default:
                // A list paused between commits fills no message (8); git am is continued in a terminal.
                return null;
        }
    }

    // ---- Merge, cherry-pick, revert (5.4) ---------------------------------------------------------

    /// <summary>
    /// Commits the stopped merge, pick or revert: <c>git commit --file=- --cleanup=whitespace</c>
    /// with <paramref name="message"/>, or the operation's own (5.3) when it is null. A pick keeps
    /// its author (git takes it from <c>CHERRY_PICK_HEAD</c>); hooks run, as for any commit.
    /// </summary>
    private async Task CommitStoppedAsync(InProgressState state, string? message, NetworkOptions? options, CancellationToken cancellationToken)
    {
        message ??= await ReadOperationMessageAsync(state, options, cancellationToken).ConfigureAwait(false);
        var basis = WriteCommand(options, "commit", "--file=-", "--cleanup=whitespace");
        var command = new GitCommand([.. basis.Arguments]) { WorkingDirectory = basis.WorkingDirectory, Environment = basis.Environment, StandardInput = message ?? string.Empty };
        (await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
    }

    /// <summary>
    /// <c>git cherry-pick</c> or <c>git revert</c> with <paramref name="flag"/> (<c>--continue</c>,
    /// <c>--skip</c>) and how it ended: nothing in progress after it, <see cref="ContinueOutcome.Done"/>;
    /// still in progress after an exit 0, or after a failure that changed something (HEAD, the
    /// list, the pick head: it went on and stopped at the next commit, or at one it could not
    /// apply), <see cref="ContinueOutcome.Stopped"/>; a failure that changed nothing (unresolved
    /// files, "there is nothing to skip", a revert's list for <c>cherry-pick</c>) throws.
    /// </summary>
    private async Task<ContinueResult> RunSequenceAsync(InProgressKind kind, string flag, NetworkOptions? options, CancellationToken cancellationToken)
    {
        var before = await SequenceSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var command = WriteCommand(options, SequenceVerb(kind), flag);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        var after = await SequenceSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (!after.InProgress)
        {
            result.EnsureSuccess(command);
            return new ContinueResult(ContinueOutcome.Done, after.Head);
        }

        if (!result.Succeeded && after == before)
        {
            result.EnsureSuccess(command);
        }

        return new ContinueResult(ContinueOutcome.Stopped, after.Head);
    }

    /// <summary>What tells a sequence's step from a refusal: HEAD, the list, the pick heads and <c>MERGE_MSG</c>.</summary>
    private sealed record SequenceSnapshot(string? Head, bool Sequenced, string? Todo, string? PickHead, string? RevertHeadCommit, bool MergeMessage)
    {
        public bool InProgress => Sequenced || PickHead is not null || RevertHeadCommit is not null;
    }

    private async Task<SequenceSnapshot> SequenceSnapshotAsync(CancellationToken cancellationToken) => new(
        await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false),
        Directory.Exists(Path.Combine(GitDirectory, "sequencer")),
        ReadGitFile("sequencer", "todo"),
        await ReadPseudoRefAsync(CherryPickHead, cancellationToken).ConfigureAwait(false),
        await ReadPseudoRefAsync(RevertHead, cancellationToken).ConfigureAwait(false),
        File.Exists(Path.Combine(GitDirectory, "MERGE_MSG")));

    private async Task<ContinueResult> FinishedAsync(CancellationToken cancellationToken) =>
        new(ContinueOutcome.Done, await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false));

    // ---- Rebase (5.4) -----------------------------------------------------------------------------

    /// <summary>
    /// Continue for a rebase (5.4). Inside a group the app wrote (<see cref="InProgressState.Group"/>),
    /// a given message is first written to <c>rebase-merge/vc-message-&lt;n&gt;</c>, so that the
    /// group's amend applies it. Then:
    /// <list type="bullet">
    /// <item><see cref="RebaseStopKind.OwnAmendFailed"/>: when <c>HEAD^</c> is
    /// <c>refs/rewritten/vc-&lt;n&gt;</c> (the group made exactly one commit), the amend the exec
    /// failed to make: <c>git commit --amend --only --allow-empty --cleanup=whitespace
    /// --file=&lt;vc-message-n&gt;</c>, hooks included (a refusal throws with the hook's output and
    /// the rebase stays stopped); then <c>git rebase --continue</c>.</item>
    /// <item><see cref="RebaseStopKind.Pick"/> on a <c>pick</c> or <c>reword</c> line of
    /// <c>rebase-merge/</c>, nothing conflicted and something staged: <c>git commit --no-verify
    /// --cleanup=whitespace --file=-</c> with the message (or the operation's own, 5.3) and the
    /// stopped commit's author from <c>rebase-merge/author-script</c>, so that the message is kept
    /// exactly (git's own Continue strips its <c>#</c> lines, 0.1) with the author and author date;
    /// then <c>git rebase --continue</c>, which takes that commit for the stopped one.</item>
    /// <item>Otherwise (<see cref="RebaseStopKind.Rescheduled"/>, <see cref="RebaseStopKind.ExecFailed"/>,
    /// <see cref="RebaseStopKind.Edit"/>, <see cref="RebaseStopKind.Empty"/>, nothing staged,
    /// unresolved files, a <c>fixup</c> or <c>squash</c> stop, the apply backend): <c>git rebase
    /// --continue</c>.</item>
    /// </list>
    /// How it ended is <see cref="ClassifyRebaseAsync"/>'s: still a rebase folder,
    /// <see cref="ContinueOutcome.Stopped"/>; git's refusal (unresolved files, no rebase) throws.
    /// Before any of it, git's own check of <c>--continue</c> (<see cref="RefuseUnstagedChangesAsync"/>):
    /// a tracked file that is not conflicted with unstaged changes refuses the whole Continue, so
    /// that the app's own commit is never made for a <c>--continue</c> git then refuses.
    /// </summary>
    private async Task<ContinueResult> ContinueRebaseAsync(InProgressState state, string? message, NetworkOptions? options, CancellationToken cancellationToken)
    {
        await RefuseUnstagedChangesAsync(options, cancellationToken).ConfigureAwait(false);
        var merging = Directory.Exists(Path.Combine(GitDirectory, "rebase-merge"));
        if (merging && state.Group is { } group && message is not null)
        {
            File.WriteAllText(MessageFilePath(group), message.ReplaceLineEndings("\n"), Utf8WithoutBom);
        }

        if (merging && state.StopKind == RebaseStopKind.OwnAmendFailed && state.Group is { } amended)
        {
            var label = await ResolveAsync(string.Create(CultureInfo.InvariantCulture, $"refs/rewritten/vc-{amended}"), cancellationToken).ConfigureAwait(false);
            var parent = await ResolveAsync("HEAD^", cancellationToken).ConfigureAwait(false);
            if (label is not null && string.Equals(label, parent, StringComparison.Ordinal))
            {
                var amend = WriteCommand(options, "commit", "--amend", "--only", "--allow-empty", "--cleanup=whitespace", "--file=" + MessageFilePath(amended));
                (await _runner.RunAsync(amend, cancellationToken).ConfigureAwait(false)).EnsureSuccess(amend);
            }
        }
        else if (merging
            && state.StopKind == RebaseStopKind.Pick
            && LastDoneCommand() is { Command: "pick" or "reword" }
            && ReadAuthorScript() is { } author
            && !await HasConflictsAsync(cancellationToken).ConfigureAwait(false)
            && !await IndexEqualsHeadAsync(cancellationToken).ConfigureAwait(false))
        {
            var text = message ?? await ReadOperationMessageAsync(state, options, cancellationToken).ConfigureAwait(false) ?? string.Empty;
            var basis = WriteCommand(options, author, "commit", "--no-verify", "--cleanup=whitespace", "--file=-");
            var commit = new GitCommand([.. basis.Arguments]) { WorkingDirectory = basis.WorkingDirectory, Environment = basis.Environment, StandardInput = text };
            (await _runner.RunAsync(commit, cancellationToken).ConfigureAwait(false)).EnsureSuccess(commit);
        }

        return await RebaseStepAsync("--continue", options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The check <c>git rebase --continue</c> makes before anything (builtin/rebase.c: it refreshes
    /// the index, then refuses with "You must edit all merge conflicts…" while
    /// <c>git diff-files --ignore-submodules</c> reports a change), made before the app's own
    /// commit: <c>git update-index -q --refresh</c> (its exit code ignored, as git ignores it: it is
    /// 1 while paths are unmerged), then <c>git diff-files --name-only -z --ignore-submodules</c>.
    /// A changed path that is not conflicted throws <see cref="GitException"/> with
    /// <see cref="RebaseUnstagedChangesMessage"/> and the paths, and nothing is written; conflicted
    /// paths alone are left to git's own refusal, which also changes nothing (0.1).
    /// </summary>
    private async Task RefuseUnstagedChangesAsync(NetworkOptions? options, CancellationToken cancellationToken)
    {
        await _runner.RunAsync(WriteCommand(options, "update-index", "-q", "--refresh"), cancellationToken).ConfigureAwait(false);
        var changed = (await ReadOutputAsync(cancellationToken, "diff-files", "--name-only", "-z", "--ignore-submodules").ConfigureAwait(false))
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (changed.Length == 0)
        {
            return;
        }

        var unmerged = (await ReadOutputAsync(cancellationToken, "ls-files", "--unmerged", "-z").ConfigureAwait(false))
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.IndexOf('\t', StringComparison.Ordinal) is var tab and >= 0 ? entry[(tab + 1)..] : entry)
            .ToHashSet(StringComparer.Ordinal);
        var others = changed.Where(path => !unmerged.Contains(path)).Distinct(StringComparer.Ordinal).ToList();
        if (others.Count > 0)
        {
            throw new GitException("git rebase --continue", 1, RebaseUnstagedChangesMessage + "\n" + string.Join("\n", others) + "\n");
        }
    }

    /// <summary><c>git rebase &lt;flag&gt;</c> (<c>--continue</c>, <c>--skip</c>), classified by <see cref="ClassifyRebaseAsync"/>.</summary>
    private async Task<ContinueResult> RebaseStepAsync(string flag, NetworkOptions? options, CancellationToken cancellationToken)
    {
        var folderBefore = RebaseFolderExists();
        var command = WriteCommand(options, "rebase", flag);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        var outcome = await ClassifyRebaseAsync(command, result, folderBefore, cancellationToken).ConfigureAwait(false);
        return outcome.Outcome == RebaseOutcome.Stopped
            ? new ContinueResult(ContinueOutcome.Stopped, await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false))
            : new ContinueResult(ContinueOutcome.Done, outcome.NewTip ?? await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false))
            {
                AutostashConflicted = outcome.AutostashConflicted,
            };
    }

    /// <summary>The last line of <c>rebase-merge/done</c>: the command the rebase stopped at (or after), or null.</summary>
    private TodoLine? LastDoneCommand() =>
        ParseTodo(ReadGitFile("rebase-merge", "done")) is { Count: > 0 } done ? done[^1] : null;

    /// <summary>A group's message file the app's todo wrote (2.5): <c>rebase-merge/vc-message-&lt;n&gt;</c> of this worktree.</summary>
    private string MessageFilePath(int group) =>
        Path.Combine(GitDirectory, "rebase-merge", string.Create(CultureInfo.InvariantCulture, $"vc-message-{group}"));

    /// <summary>
    /// The stopped commit's author from <c>rebase-merge/author-script</c> as environment variables
    /// (<c>GIT_AUTHOR_NAME</c>, <c>GIT_AUTHOR_EMAIL</c>, <c>GIT_AUTHOR_DATE</c>, the date as git
    /// wrote it, <c>@&lt;seconds&gt; &lt;zone&gt;</c>), or null when the file or one of them is missing.
    /// </summary>
    private Dictionary<string, string?>? ReadAuthorScript()
    {
        if (ReadGitFile("rebase-merge", "author-script") is not { } script)
        {
            return null;
        }

        var author = ParseAuthorScript(script);
        return author.ContainsKey("GIT_AUTHOR_NAME") && author.ContainsKey("GIT_AUTHOR_EMAIL") && author.ContainsKey("GIT_AUTHOR_DATE")
            ? author
            : null;
    }

    /// <summary>
    /// Reads git's <c>author-script</c>: one <c>NAME='value'</c> line per variable, quoted for
    /// <c>sh</c> as git's <c>sq_quote</c> quotes (a <c>'</c> written <c>'\''</c>, a <c>!</c>
    /// <c>'\!'</c>). Only the <c>GIT_AUTHOR_*</c> variables are kept.
    /// </summary>
    internal static Dictionary<string, string?> ParseAuthorScript(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var variables = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var raw in script.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                continue;
            }

            var name = line[..equals].Trim();
            if (name.StartsWith("GIT_AUTHOR_", StringComparison.Ordinal))
            {
                variables[name] = Unquote(line[(equals + 1)..]);
            }
        }

        return variables;

        static string Unquote(string value)
        {
            var text = new StringBuilder(value.Length);
            var quoted = false;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c == '\'')
                {
                    quoted = !quoted;
                }
                else if (!quoted && c == '\\' && i + 1 < value.Length)
                {
                    text.Append(value[++i]);
                }
                else
                {
                    text.Append(c);
                }
            }

            return text.ToString();
        }
    }

    // ---- The stage panel's message (5.3) ----------------------------------------------------------

    /// <summary>
    /// Git's comment string, <c>core.commentChar</c> (one <c>git config</c> call), <c>#</c> when it
    /// is unset or <c>auto</c> (git then still writes its blocks with <c>#</c>).
    /// </summary>
    private async Task<string> CommentCharAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(Read("config", "--get", "core.commentChar"), cancellationToken).ConfigureAwait(false);
        var value = result.Succeeded ? result.StandardOutput.TrimEnd('\r', '\n') : string.Empty;
        return value.Length == 0 || string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase) ? "#" : value;
    }

    /// <summary>
    /// <paramref name="message"/> without git's trailing comment block (5.3): from a line
    /// <c>{c} Conflicts:</c> to the end, when every later line is empty or starts with
    /// <paramref name="comment"/>, with the scissors line git puts above it under
    /// <c>commit.cleanup=scissors</c>. The message's own comment lines stay (D68).
    /// </summary>
    internal static string WithoutConflictsBlock(string message, string comment)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrEmpty(comment);
        var lines = message.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (line.Length > 0 && !line.StartsWith(comment, StringComparison.Ordinal))
            {
                break; // A line of the message itself: no block of git's below it.
            }

            if (line.StartsWith(comment + " Conflicts:", StringComparison.Ordinal))
            {
                var start = i;
                var above = i - 1;
                while (above >= 0 && lines[above] == comment)
                {
                    above--;
                }

                if (above >= 0 && lines[above].StartsWith(comment + " ", StringComparison.Ordinal) && lines[above].Contains(" >8 ", StringComparison.Ordinal))
                {
                    start = above;
                }

                lines.RemoveRange(start, lines.Count - start);
                break;
            }
        }

        return string.Join('\n', lines);
    }

    /// <summary>A message as git's <c>strip</c> clean-up leaves it: without the lines that start with <paramref name="comment"/>, runs of blank lines made one.</summary>
    internal static string WithoutCommentLines(string message, string comment)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrEmpty(comment);
        var kept = new List<string>();
        foreach (var line in message.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => !line.StartsWith(comment, StringComparison.Ordinal)))
        {
            if (line.Trim().Length == 0 && (kept.Count == 0 || kept[^1].Length == 0))
            {
                continue;
            }

            kept.Add(line.Trim().Length == 0 ? string.Empty : line);
        }

        return string.Join('\n', kept);
    }

    /// <summary>A message without the blank lines and spaces at its end, null when nothing is left.</summary>
    private static string? Tidy(string message)
    {
        var text = message.TrimEnd();
        return text.Length > 0 ? text : null;
    }

    private static string Short(string? sha) => sha is null ? "the stopped commit" : sha.Length > 7 ? sha[..7] : sha;
}
