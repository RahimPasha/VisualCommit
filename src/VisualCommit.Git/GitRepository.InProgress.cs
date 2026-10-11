using System.Globalization;
using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// The operation in progress (design section 5, D89, D110), read from the files git leaves in the
/// repository's own git folder, and its abort (2.15).
/// </summary>
public sealed partial class GitRepository
{
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
    /// order: <c>rebase-merge/</c>; <c>rebase-apply/</c> (with <c>applying</c>: <c>git am</c>);
    /// <c>MERGE_HEAD</c>; <c>CHERRY_PICK_HEAD</c>; <c>REVERT_HEAD</c>; <c>sequencer/todo</c> alone (a
    /// sequence started in a terminal, stopped between commits). Git calls are made only while
    /// something is in progress: the subject of the commit being picked or reverted, and HEAD when
    /// a sequencer is there (D110). <paramref name="status"/> tells whether a pick is empty.
    /// </summary>
    internal async Task<InProgressState> ReadInProgressAsync(WorkingTreeStatus status, CancellationToken cancellationToken)
    {
        var rebaseMerge = Path.Combine(GitDirectory, "rebase-merge");
        if (Directory.Exists(rebaseMerge))
        {
            return Rebase(rebaseMerge, CountTodoLines(ReadGitFile("rebase-merge", "git-rebase-todo")));
        }

        var rebaseApply = Path.Combine(GitDirectory, "rebase-apply");
        if (Directory.Exists(rebaseApply))
        {
            // git am and the old rebase backend share the folder; "next" is the patch being
            // applied and "last" the number of patches.
            var remaining = Math.Max(0, ReadNumber("rebase-apply", "last") - ReadNumber("rebase-apply", "next"));
            return File.Exists(Path.Combine(rebaseApply, "applying"))
                ? new InProgressState(InProgressKind.ApplyMailbox, null, null, null, null, remaining, false)
                : Rebase(rebaseApply, remaining);
        }

        if (FirstLine(ReadGitFile("MERGE_HEAD")) is { } mergeHead)
        {
            return new InProgressState(InProgressKind.Merge, mergeHead, FirstLine(ReadGitFile("MERGE_MSG")), null, null, 0, false);
        }

        var sequencerTodo = ReadGitFile("sequencer", "todo");
        var sequenced = Directory.Exists(Path.Combine(GitDirectory, "sequencer"));
        foreach (var (file, kind) in (ReadOnlySpan<(string, InProgressKind)>)[("CHERRY_PICK_HEAD", InProgressKind.CherryPick), ("REVERT_HEAD", InProgressKind.Revert)])
        {
            if (FirstLine(ReadGitFile(file)) is { } commit)
            {
                // Git stopped on a pick that changes nothing: the index is HEAD's tree (nothing
                // staged) and nothing conflicts (2.12).
                var empty = status.Staged.Count == 0 && !status.Unstaged.Any(entry => entry.Kind == FileChangeKind.Conflicted);
                return new InProgressState(
                    kind,
                    commit,
                    await ReadSubjectAsync(commit, cancellationToken).ConfigureAwait(false),
                    null,
                    null,
                    RemainingAfterStop(sequencerTodo),
                    empty)
                {
                    AbortKeepsHead = sequenced && await HeadMovedSinceSequencerAsync(cancellationToken).ConfigureAwait(false),
                };
            }
        }

        if (sequencerTodo is not null && TodoKind(sequencerTodo) is { } stoppedKind)
        {
            return new InProgressState(stoppedKind, null, null, null, null, RemainingAfterStop(sequencerTodo), false)
            {
                AbortKeepsHead = await HeadMovedSinceSequencerAsync(cancellationToken).ConfigureAwait(false),
            };
        }

        return InProgressState.None;

        InProgressState Rebase(string folder, int remaining)
        {
            var headName = FirstLine(ReadFile(Path.Combine(folder, "head-name")));
            var branch = headName is not null && headName.StartsWith(HeadsPrefix, StringComparison.Ordinal) ? headName[HeadsPrefix.Length..] : null;
            return new InProgressState(InProgressKind.Rebase, null, null, branch, FirstLine(ReadFile(Path.Combine(folder, "onto"))), remaining, false);
        }

        int ReadNumber(string folder, string name) =>
            int.TryParse(FirstLine(ReadGitFile(folder, name)), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    /// <summary>
    /// The commits a sequence has left after the stopped one: the todo's lines less one. Git keeps
    /// the stopped pick as the first line, also after it was committed, and <c>--continue</c>
    /// always skips that line (D110). 0 without a sequencer.
    /// </summary>
    private static int RemainingAfterStop(string? sequencerTodo) =>
        sequencerTodo is null ? 0 : Math.Max(0, CountTodoLines(sequencerTodo) - 1);

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
