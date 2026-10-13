using System.Globalization;
using System.Text.RegularExpressions;
using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// The operation in progress (phase 3 design section 5, D89, D110; docs/phase-4-design.md section
/// 8), read from the files git leaves in the repository's own git folder, and its abort (2.15).
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>The pick heads in the order they are read, with the operation each tells.</summary>
    private static readonly (string Name, InProgressKind Kind)[] PickHeads =
        [(CherryPickHead, InProgressKind.CherryPick), (RevertHead, InProgressKind.Revert)];

    public Task AbortAsync(InProgressKind kind, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        string[] arguments = kind switch
        {
            InProgressKind.Merge => ["merge", "--abort"],
            InProgressKind.CherryPick => ["cherry-pick", "--abort"],
            InProgressKind.Revert => ["revert", "--abort"],
            InProgressKind.Rebase => ["rebase", "--abort"],
            InProgressKind.ApplyMailbox => ["am", "--abort"],
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "There is no operation of this kind to abort."),
        };
        return RunWriteAsync(options, cancellationToken, arguments);
    }

    /// <summary>
    /// What is in progress, by these files of <see cref="GitDirectory"/> (per worktree), in this
    /// order: <c>rebase-merge/</c> (<see cref="ReadRebaseMergeAsync"/>); <c>rebase-apply/</c> (with
    /// <c>applying</c>: <c>git am</c>); <c>MERGE_HEAD</c>; the pick heads <c>CHERRY_PICK_HEAD</c> and
    /// <c>REVERT_HEAD</c>, read as files or, in a reftable repository, as refs
    /// (<see cref="ReadPseudoRefAsync"/>, 8.1); <c>sequencer/todo</c> alone (a list paused between
    /// commits, or stopped on a commit git could not apply). Git calls are made only while
    /// something is in progress (D89): the subject of the commit being applied, HEAD when a
    /// sequencer is there (D110), the abbreviated commit of a paused list's first line, and the
    /// commits a list has made (<see cref="ReadListProgressAsync"/>).
    /// <paramref name="status"/> tells whether a stop is empty.
    /// <para>
    /// A list (<see cref="InProgressState.Sequenced"/>) without a pick head is paused: committed in a
    /// terminal (<see cref="InProgressState.AbortKeepsHead"/>), an empty revert (git leaves
    /// <c>MERGE_MSG</c> and no <c>REVERT_HEAD</c>: <see cref="InProgressState.Empty"/>), or stopped on
    /// a commit git could not apply without a conflict (neither); its <see cref="InProgressState.Commit"/>
    /// is <c>sequencer/todo</c>'s first line, the stopped command (section 4, 5.1).
    /// </para>
    /// </summary>
    internal async Task<InProgressState> ReadInProgressAsync(WorkingTreeStatus status, CancellationToken cancellationToken)
    {
        var rebaseMerge = Path.Combine(GitDirectory, "rebase-merge");
        if (Directory.Exists(rebaseMerge))
        {
            return await ReadRebaseMergeAsync(rebaseMerge, status, cancellationToken).ConfigureAwait(false);
        }

        var rebaseApply = Path.Combine(GitDirectory, "rebase-apply");
        if (Directory.Exists(rebaseApply))
        {
            // git am and the old rebase backend share the folder; "next" is the patch being
            // applied and "last" the number of patches.
            var next = ReadNumber("rebase-apply", "next");
            var last = ReadNumber("rebase-apply", "last");
            var remaining = Math.Max(0, last - next);
            if (File.Exists(Path.Combine(rebaseApply, "applying")))
            {
                return new InProgressState(InProgressKind.ApplyMailbox, null, null, null, null, remaining, false);
            }

            // The apply backend (rebase.backend=apply, from a terminal): the commit being applied
            // is "original-commit". Its Continue cannot leave out a commit that changes nothing
            // ("No changes - did you forget to use 'git add'?"), so it never stops as Empty.
            var original = FirstLine(ReadFile(Path.Combine(rebaseApply, "original-commit")));
            return RebaseState(rebaseApply, original, original is null ? null : await ReadSubjectAsync(original, cancellationToken).ConfigureAwait(false), remaining) with
            {
                StopKind = RebaseStopKind.Pick,
                Done = Math.Max(0, next),
                Total = Math.Max(0, last),
            };
        }

        if (FirstLine(ReadGitFile("MERGE_HEAD")) is { } mergeHead)
        {
            return new InProgressState(InProgressKind.Merge, mergeHead, FirstLine(ReadGitFile("MERGE_MSG")), null, null, 0, false);
        }

        var sequencerTodo = ReadGitFile("sequencer", "todo");
        var sequenced = Directory.Exists(Path.Combine(GitDirectory, "sequencer"));
        foreach (var (name, kind) in PickHeads)
        {
            if (await ReadPseudoRefAsync(name, cancellationToken).ConfigureAwait(false) is { } commit)
            {
                // Git stopped on a pick that changes nothing: the index is HEAD's tree (nothing
                // staged) and nothing conflicts (2.12).
                var (done, total) = sequenced ? await ReadListProgressAsync(sequencerTodo, firstMade: false, cancellationToken).ConfigureAwait(false) : (0, 0);
                return new InProgressState(
                    kind,
                    commit,
                    await ReadSubjectAsync(commit, cancellationToken).ConfigureAwait(false),
                    null,
                    null,
                    RemainingAfterStop(sequencerTodo),
                    NothingStaged(status))
                {
                    AbortKeepsHead = sequenced && await HeadMovedSinceSequencerAsync(cancellationToken).ConfigureAwait(false),
                    Sequenced = sequenced,
                    HasPickHead = true,
                    Done = done,
                    Total = total,
                };
            }
        }

        if (sequencerTodo is not null && TodoKind(sequencerTodo) is { } stoppedKind)
        {
            // The stopped command stays the todo's first line, abbreviated (0.3).
            var first = TodoLines(sequencerTodo).First().Split(' ', 3);
            var commit = first.Length > 1 ? await ResolveCommitAsync(first[1], cancellationToken).ConfigureAwait(false) : null;
            var empty = File.Exists(Path.Combine(GitDirectory, "MERGE_MSG"));
            var abortKeepsHead = await HeadMovedSinceSequencerAsync(cancellationToken).ConfigureAwait(false);

            // Paused after the stopped pick was committed in a terminal: its line is made already.
            var (done, total) = await ReadListProgressAsync(sequencerTodo, firstMade: abortKeepsHead && !empty, cancellationToken).ConfigureAwait(false);
            return new InProgressState(
                stoppedKind,
                commit,
                commit is null ? null : await ReadSubjectAsync(commit, cancellationToken).ConfigureAwait(false),
                null,
                null,
                RemainingAfterStop(sequencerTodo),
                empty)
            {
                AbortKeepsHead = abortKeepsHead,
                Sequenced = true,
                Done = done,
                Total = total,
            };
        }

        return InProgressState.None;

        int ReadNumber(string folder, string name) =>
            int.TryParse(FirstLine(ReadGitFile(folder, name)), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    /// <summary>
    /// A rebase of the merge backend (<c>git rebase</c>, <c>git rebase -i</c>, the app's rebases),
    /// stopped with <c>rebase-merge/</c> present, also after an exit 0 (8). Its stop is read in this
    /// order:
    /// <list type="number">
    /// <item><see cref="RebaseStopKind.Rescheduled"/>: the first line of <c>git-rebase-todo</c> is a
    /// pick-like command that names <c>stopped-sha</c> (an untracked file was in the way; git put
    /// the command back, 0.1). A newer git than 2.36 writes no <c>stopped-sha</c> for it (read in
    /// git's source, not run here: since 2023 a rescheduled command makes no patch, only
    /// <c>REBASE_HEAD</c>), and <c>done</c> then ends with the same command: that counts too.</item>
    /// <item><see cref="RebaseStopKind.ExecFailed"/>: <c>done</c> ends with an <c>exec</c> line
    /// (git removes <c>stopped-sha</c> before each command, so there is none then; the one case
    /// where <c>done</c> ends with an <c>exec</c> and one exists, Git 2.36 appending the command
    /// before a rescheduled pick again, is read as rescheduled first);
    /// <see cref="RebaseStopKind.OwnAmendFailed"/> when that line is the app's guarded amend (it
    /// names <c>rebase-merge/vc-message-&lt;n&gt;</c>, 2.5).</item>
    /// <item><see cref="RebaseStopKind.Edit"/>: <c>amend</c> exists and <c>done</c> ends with
    /// <c>edit</c>.</item>
    /// <item><see cref="RebaseStopKind.Empty"/>: <c>CHERRY_PICK_HEAD</c> beside the folder (an
    /// interactive rebase's commit that became empty), or <c>done</c> ends with a pick-like
    /// command for <c>stopped-sha</c> with nothing staged and nothing conflicted.</item>
    /// <item><see cref="RebaseStopKind.Pick"/> otherwise.</item>
    /// </list>
    /// "Pick-like" is <c>pick</c>, <c>reword</c>, <c>edit</c>, <c>squash</c> and <c>fixup</c> (and
    /// their letters); a <c>merge</c> of <c>--rebase-merges</c> makes its commit when continued with
    /// nothing staged, so it never counts as empty.
    /// <para>
    /// <see cref="InProgressState.Commit"/> is <c>stopped-sha</c>; at an exec stop, which has none,
    /// the commit the command ran after: for the app's amend, the group's target (the first commit
    /// command after <c>label vc-&lt;n&gt;</c> in <c>done</c>), for another exec the last commit
    /// command in <c>done</c>, else HEAD. <see cref="InProgressState.Group"/> is the
    /// <c>&lt;n&gt;</c> of the app's group whose amend still waits in <c>git-rebase-todo</c> before
    /// the next <c>label</c> line, or, for <see cref="RebaseStopKind.OwnAmendFailed"/>, the group
    /// whose amend failed. <see cref="InProgressState.NextIsSquash"/>: the command after the stopped
    /// one (after the rescheduled one for <see cref="RebaseStopKind.Rescheduled"/>) is
    /// <c>fixup</c> or <c>squash</c>, and the stopped command is not one itself: a group's member
    /// that stopped is no target, and leaving it out joins nothing to another commit.
    /// </para>
    /// <para>
    /// <see cref="InProgressState.Done"/> counts the distinct commit commands of <c>done</c> (plus
    /// the rescheduled one): Git 2.36 appends the previous command to <c>done</c> a second time when
    /// it reschedules one, and every git appends the rescheduled command again at each new try.
    /// <see cref="InProgressState.Remaining"/> counts the commit commands left in
    /// <c>git-rebase-todo</c> after the current one; <see cref="InProgressState.Total"/> is both.
    /// </para>
    /// </summary>
    private async Task<InProgressState> ReadRebaseMergeAsync(string folder, WorkingTreeStatus status, CancellationToken cancellationToken)
    {
        var done = ParseTodo(ReadFile(Path.Combine(folder, "done")));
        var todo = ParseTodo(ReadFile(Path.Combine(folder, "git-rebase-todo")));
        var stoppedSha = FirstLine(ReadFile(Path.Combine(folder, "stopped-sha")));
        var pickHead = await ReadPseudoRefAsync(CherryPickHead, cancellationToken).ConfigureAwait(false);
        var lastDone = done.Count > 0 ? done[^1] : (TodoLine?)null;
        var firstTodo = todo.Count > 0 ? todo[0] : (TodoLine?)null;

        RebaseStopKind kind;
        int? group = null;
        if (firstTodo is { IsPickLike: true } rescheduled
            && (rescheduled.Names(stoppedSha) || (stoppedSha is null && lastDone is { } previous && previous.Key == rescheduled.Key)))
        {
            kind = RebaseStopKind.Rescheduled;
        }
        else if (stoppedSha is null && pickHead is null && lastDone is { IsCommitCommand: true } && firstTodo is { } waiting && MessageGroup(waiting) is { } rescheduledGroup)
        {
            // The app's amend failed and git rescheduled it (rebase.rescheduleFailedExec): it is
            // the first line of git-rebase-todo again, and Git 2.36 ends done with the commit
            // command before it a second time. Nothing else stops before the amend without a
            // stopped-sha or a pick head (the review's finding 13).
            group = rescheduledGroup;
            kind = RebaseStopKind.OwnAmendFailed;
        }
        else if (lastDone is { Command: "exec" } exec)
        {
            group = MessageGroup(exec);
            kind = group is null ? RebaseStopKind.ExecFailed : RebaseStopKind.OwnAmendFailed;
        }
        else if (lastDone is { Command: "edit" } && File.Exists(Path.Combine(folder, "amend")))
        {
            kind = RebaseStopKind.Edit;
        }
        else if (pickHead is not null || (lastDone is { IsPickLike: true } stopped && stopped.Names(stoppedSha) && NothingStaged(status)))
        {
            kind = RebaseStopKind.Empty;
        }
        else
        {
            kind = RebaseStopKind.Pick;
        }

        group ??= WaitingGroup(todo);

        // The stopped command and the one after it.
        var current = kind == RebaseStopKind.Rescheduled ? firstTodo : lastDone;
        var next = kind == RebaseStopKind.Rescheduled ? (todo.Count > 1 ? todo[1] : (TodoLine?)null) : firstTodo;
        var nextIsSquash = next is { IsSquashLike: true } && current is not { IsSquashLike: true };

        var keys = done.Where(line => line.IsCommitCommand).Select(line => line.Key).ToHashSet(StringComparer.Ordinal);
        if (kind == RebaseStopKind.Rescheduled && firstTodo is { } again)
        {
            keys.Add(again.Key);
        }

        var remaining = todo.Count(line => line.IsCommitCommand) - (kind == RebaseStopKind.Rescheduled ? 1 : 0);

        var commit = kind switch
        {
            RebaseStopKind.Rescheduled => firstTodo?.Commit,
            RebaseStopKind.OwnAmendFailed => GroupTarget(done, group!.Value) ?? LastCommit(done),
            RebaseStopKind.ExecFailed => LastCommit(done),
            _ => stoppedSha ?? pickHead ?? lastDone?.Commit,
        };
        commit = commit is null
            ? await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false)
            : await ResolveCommitAsync(commit, cancellationToken).ConfigureAwait(false);
        var subject = commit is null ? null : await ReadSubjectAsync(commit, cancellationToken).ConfigureAwait(false);

        return RebaseState(folder, commit, subject, Math.Max(0, remaining)) with
        {
            HasPickHead = pickHead is not null,
            StopKind = kind,
            NextIsSquash = nextIsSquash,
            Group = group,
            Done = keys.Count,
            Total = keys.Count + Math.Max(0, remaining),
        };

        static string? LastCommit(List<TodoLine> lines) => lines.LastOrDefault(line => line.IsCommitCommand).Commit;
    }

    /// <summary>A rebase's state with its branch (<c>head-name</c> without <c>refs/heads/</c>) and new base (<c>onto</c>) from <paramref name="folder"/>.</summary>
    private static InProgressState RebaseState(string folder, string? commit, string? subject, int remaining)
    {
        var headName = FirstLine(ReadFile(Path.Combine(folder, "head-name")));
        var branch = headName is not null && headName.StartsWith(HeadsPrefix, StringComparison.Ordinal) ? headName[HeadsPrefix.Length..] : null;
        return new InProgressState(InProgressKind.Rebase, commit, subject, branch, FirstLine(ReadFile(Path.Combine(folder, "onto"))), remaining, false);
    }

    /// <summary>Nothing is staged (the index holds HEAD's tree) and nothing conflicts, by the status just read.</summary>
    private static bool NothingStaged(WorkingTreeStatus status) =>
        status.Staged.Count == 0 && !status.Unstaged.Any(entry => entry.Kind == FileChangeKind.Conflicted);

    /// <summary>
    /// The group the app wrote whose guarded amend still waits (2.5, 5.3): the <c>&lt;n&gt;</c> of
    /// the first <c>exec … vc-message-&lt;n&gt;</c> line of <paramref name="todo"/> before its first
    /// <c>label</c> line, or null.
    /// </summary>
    private static int? WaitingGroup(List<TodoLine> todo)
    {
        foreach (var line in todo)
        {
            if (line.Command == "label")
            {
                return null;
            }

            if (line.Command == "exec" && MessageGroup(line) is { } group)
            {
                return group;
            }
        }

        return null;
    }

    /// <summary>The group number of the app's guarded amend (2.5): the <c>&lt;n&gt;</c> of the <c>rebase-merge/vc-message-&lt;n&gt;</c> it names, or null for another line.</summary>
    private static int? MessageGroup(TodoLine line) =>
        line.Command == "exec" && MessageFilePattern().Match(line.Text) is { Success: true } match
            && int.TryParse(match.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var group)
            ? group
            : null;

    /// <summary>The target of the app's group <paramref name="group"/> in <paramref name="done"/>: the first commit command after its last <c>label vc-&lt;n&gt;</c> line.</summary>
    private static string? GroupTarget(List<TodoLine> done, int group)
    {
        var label = string.Create(CultureInfo.InvariantCulture, $"vc-{group}");
        var at = done.FindLastIndex(line => line.Command == "label" && line.Argument == label);
        return at < 0 ? null : done.Skip(at + 1).FirstOrDefault(line => line.IsCommitCommand).Commit;
    }

    [GeneratedRegex(@"rebase-merge/vc-message-([0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex MessageFilePattern();

    /// <summary>
    /// The full id of a commit a todo names: as it is when it is a full id already (git writes
    /// them so into <c>done</c> and <c>git-rebase-todo</c>), else through <c>git rev-parse
    /// --verify --quiet &lt;id&gt;^{commit}</c> (a sequencer's todo abbreviates them, 0.3); null
    /// when git knows none.
    /// </summary>
    private async Task<string?> ResolveCommitAsync(string id, CancellationToken cancellationToken)
    {
        if (id.Length is 40 or 64 && id.All(char.IsAsciiHexDigitLower))
        {
            return id;
        }

        return id.StartsWith('-') ? null : await ResolveAsync(id + "^{commit}", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The lines of a rebase todo (<c>done</c>, <c>git-rebase-todo</c>) that name a step, parsed; empty for a missing file.</summary>
    private static List<TodoLine> ParseTodo(string? text) =>
        text is null ? [] : [.. TodoLines(text).Select(TodoLine.Parse)];

    /// <summary>
    /// One line of a rebase todo: its command in git's long form (<c>p</c> is <c>pick</c>), the
    /// commit it names (null for <c>exec</c>, <c>label</c> and the like), the rest after the
    /// command, and the whole line.
    /// </summary>
    private readonly record struct TodoLine(string Command, string? Commit, string Argument, string Text)
    {
        /// <summary>Applies a commit: <c>pick</c>, <c>reword</c>, <c>edit</c>, <c>squash</c>, <c>fixup</c>, <c>merge</c> (8: what <c>{k} of {n}</c> counts).</summary>
        public bool IsCommitCommand => Command is "pick" or "reword" or "edit" or "squash" or "fixup" or "merge";

        /// <summary>Picks one commit: <c>pick</c>, <c>reword</c>, <c>edit</c>, <c>squash</c>, <c>fixup</c>.</summary>
        public bool IsPickLike => Command is "pick" or "reword" or "edit" or "squash" or "fixup";

        /// <summary>Joins its commit to the one before it: <c>fixup</c>, <c>squash</c>.</summary>
        public bool IsSquashLike => Command is "squash" or "fixup";

        /// <summary>The line as one key for counting: the command in long form and the rest, single-spaced.</summary>
        public string Key => Command + " " + Argument;

        /// <summary>Whether the line's commit is <paramref name="sha"/> (either may be abbreviated).</summary>
        public bool Names(string? sha) =>
            sha is not null && Commit is { Length: >= 4 } commit
            && (sha.StartsWith(commit, StringComparison.OrdinalIgnoreCase) || commit.StartsWith(sha, StringComparison.OrdinalIgnoreCase));

        public static TodoLine Parse(string line)
        {
            var tokens = line.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            var command = tokens[0] switch
            {
                "p" => "pick",
                "r" => "reword",
                "e" => "edit",
                "s" => "squash",
                "f" => "fixup",
                "x" => "exec",
                "b" => "break",
                "d" => "drop",
                "l" => "label",
                "t" => "reset",
                "m" => "merge",
                "u" => "update-ref",
                var word => word,
            };

            // fixup -C <commit> and merge -C <commit> <label> (2.32+) name their commit after an option.
            var at = tokens.Length > 2 && tokens[1] is "-C" or "-c" ? 2 : 1;
            string? commit = null;
            if (command is "pick" or "reword" or "edit" or "squash" or "fixup" or "drop" || (command == "merge" && at == 2))
            {
                commit = tokens.Length > at ? tokens[at] : null;
            }

            return new TodoLine(command, commit, string.Join(' ', tokens.Skip(1)), line);
        }
    }

    /// <summary>
    /// The commits a sequence has left after the stopped one: the todo's lines less one. Git keeps
    /// the stopped pick as the first line, also after it was committed, and <c>--continue</c>
    /// always skips that line (D110). 0 without a sequencer.
    /// </summary>
    private static int RemainingAfterStop(string? sequencerTodo) =>
        sequencerTodo is null ? 0 : Math.Max(0, CountTodoLines(sequencerTodo) - 1);

    /// <summary>
    /// How far a list of picks or reverts has come (section 4, 10.2's "Cherry-picked {K} of {N}
    /// commits"): Done, the commits it has made so far (<c>git rev-list --count
    /// &lt;sequencer/head&gt;..HEAD</c>, from the HEAD the list started on); Total, those plus the
    /// commands left in <c>sequencer/todo</c>, less its first line when that commit was made already
    /// (<paramref name="firstMade"/>: a list paused after the stopped pick was committed in a
    /// terminal). Git keeps no record of a skipped commit, so one that was skipped counts in
    /// neither. (0, 0) when <c>sequencer/head</c> is missing or names nothing git can count from.
    /// </summary>
    private async Task<(int Done, int Total)> ReadListProgressAsync(string? sequencerTodo, bool firstMade, CancellationToken cancellationToken)
    {
        if (FirstLine(ReadGitFile("sequencer", "head")) is not { Length: > 0 } start)
        {
            return (0, 0);
        }

        int made;
        try
        {
            made = await CountCommitsAsync(start, "HEAD", cancellationToken).ConfigureAwait(false);
        }
        catch (GitException)
        {
            return (0, 0);
        }

        var left = CountTodoLines(sequencerTodo) - (firstMade ? 1 : 0);
        return (made, made + Math.Max(0, left));
    }

    /// <summary>A sequence's kind by its todo's first line: <c>pick</c> or <c>revert</c>.</summary>
    private static InProgressKind? TodoKind(string todo)
    {
        var first = TodoLines(todo).FirstOrDefault();
        var verb = first?.Split(' ', 2)[0];
        return verb switch
        {
            "pick" or "p" => InProgressKind.CherryPick,
            "revert" => InProgressKind.Revert,
            _ => null,
        };
    }

    private static int CountTodoLines(string? todo) => todo is null ? 0 : TodoLines(todo).Count();

    /// <summary>The lines of a todo list that name a step: not empty, not a comment.</summary>
    private static IEnumerable<string> TodoLines(string todo) =>
        todo.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#'));

    /// <summary>
    /// Whether HEAD is no longer where the sequencer left it (<c>sequencer/abort-safety</c>), as after
    /// the stopped pick was committed: git's <c>--abort</c> then ends the sequence without moving
    /// HEAD (D110). A missing file counts as moved, as git counts it.
    /// </summary>
    private async Task<bool> HeadMovedSinceSequencerAsync(CancellationToken cancellationToken)
    {
        var safety = FirstLine(ReadGitFile("sequencer", "abort-safety"));
        var head = await ResolveAsync("HEAD", cancellationToken).ConfigureAwait(false);
        return !string.Equals(safety, head, StringComparison.Ordinal);
    }

    /// <summary>A commit's subject, or null when git cannot read it.</summary>
    private async Task<string?> ReadSubjectAsync(string commit, CancellationToken cancellationToken)
    {
        var command = Read("log", "-1", "--no-show-signature", "--no-color", "--encoding=UTF-8", "--format=%s", commit, "--");
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? FirstLine(result.StandardOutput) : null;
    }

    /// <summary>A file of the git folder, or null when it is not there (or vanished while git finished).</summary>
    private string? ReadGitFile(params string[] parts) => ReadFile(Path.Combine([GitDirectory, .. parts]));

    private static string? ReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? FirstLine(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var end = text.IndexOf('\n', StringComparison.Ordinal);
        var line = (end < 0 ? text : text[..end]).TrimEnd('\r').Trim();
        return line.Length > 0 ? line : null;
    }
}
