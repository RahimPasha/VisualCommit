using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// Phase 4's rebases (docs/phase-4-design.md sections 1 to 3, D142, D143): a rebase onto a commit
/// or a branch, the commits an interactive rebase lists, and the interactive rebase itself, which
/// a squash is too. Each is one git call classified by <see cref="ClassifyRebaseAsync"/> (1.4):
/// up to date, rebased or stopped are returned, a refusal throws <see cref="GitException"/> with
/// git's own text. The backup of the branch's old tip, the queue and the questions are the app's.
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>
    /// The standard error of the <see cref="GitException"/> that <see cref="InteractiveRebaseAsync"/>
    /// throws, before anything runs, when HEAD is no longer on the plan's branch at the tip the
    /// list was read from (D124).
    /// </summary>
    public const string RebaseHeadMovedMessage = "HEAD is no longer on the branch at the commit the list was read from.";

    /// <summary>
    /// <c>git rebase &lt;onto&gt;</c> on the current branch (1.3): never a branch argument (git
    /// would detach HEAD for <c>refs/heads/&lt;b&gt;</c>, 0.1) and no option that changes git's
    /// configured behaviour (<c>rebase.autoStash</c>, <c>rebase.backend</c>,
    /// <c>rebase.updateRefs</c> and the hooks are the user's).
    /// </summary>
    public async Task<RebaseResult> RebaseAsync(string onto, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(onto);
        var folderBefore = RebaseFolderExists();
        var command = WriteCommand(options, ["rebase", .. Positional(onto)]);
        var result = RebaseText.WithoutEraseLine(await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false));
        return await ClassifyRebaseAsync(command, result, folderBefore, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The commits an interactive rebase of <paramref name="branch"/> would replay (2.3), read from
    /// the branch's tip as it is now, which the list keeps as <see cref="RebaseList.BranchTip"/>.
    /// "From here": <c>git log --reverse --topo-order &lt;onto&gt;..&lt;tip&gt;</c>; otherwise the commits
    /// git's own todo holds, <c>git log --reverse --topo-order --no-merges --right-only
    /// --cherry-pick &lt;onto&gt;...&lt;tip&gt;</c> (commits whose patch is already upstream left
    /// out; commits that start empty kept, as git keeps them). Each with its subject, author name
    /// and whole message (<c>%B</c>, re-encoded to UTF-8). The merge count is
    /// <c>git rev-list --merges --count &lt;onto&gt;..&lt;tip&gt;</c> in both cases.
    /// </summary>
    /// <exception cref="GitException"><paramref name="onto"/> or <paramref name="branch"/> names no commit.</exception>
    public async Task<RebaseList> ReadRebaseListAsync(string onto, string branch, bool fromCommit, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(onto);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        var ontoSha = await RequireCommitAsync(onto, cancellationToken).ConfigureAwait(false);
        var tip = await RequireCommitAsync(branch, cancellationToken).ConfigureAwait(false);

        string[] range = fromCommit
            ? [$"{ontoSha}..{tip}"]
            : ["--no-merges", "--right-only", "--cherry-pick", $"{ontoSha}...{tip}"];
        var log = await ReadOutputAsync(
            cancellationToken,
            ["log", "--no-show-signature", "--no-color", "--encoding=UTF-8", "-z", "--format=%H%x00%an%x00%s%x00%B", "--reverse", "--topo-order", .. range, "--"])
            .ConfigureAwait(false);
        var merges = ParseCount(await ReadOutputAsync(cancellationToken, "rev-list", "--merges", "--count", $"{ontoSha}..{tip}", "--").ConfigureAwait(false));
        return new RebaseList(ontoSha, tip, RebaseText.ParseListEntries(log), merges);
    }

    public Task<RebaseResult> InteractiveRebaseAsync(RebasePlan plan, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        InteractiveRebaseAsync(plan, Path.GetTempPath(), options, cancellationToken);

    /// <summary>
    /// <see cref="InteractiveRebaseAsync(RebasePlan, NetworkOptions?, CancellationToken)"/> with the
    /// folder its files are written in made under <paramref name="tempRoot"/> (the system's temp
    /// folder; a test's own folder, whose path can hold a space and an apostrophe).
    /// <list type="number">
    /// <item>Refuses (<see cref="RebaseHeadMovedMessage"/>) unless HEAD is on
    /// <see cref="RebasePlan.BranchRef"/> at <see cref="RebasePlan.ExpectedTip"/>.</item>
    /// <item>Builds the todo and its message files (<see cref="RebaseTodo"/>), reading the own
    /// messages it needs exactly (<c>git cat-file commit</c>, after the header), and writes them
    /// as UTF-8 without a byte order mark into <c>&lt;tempRoot&gt;/visualcommit-rebase-&lt;guid&gt;</c>.</item>
    /// <item>Runs <c>git rebase -i &lt;onto&gt;</c> with <c>GIT_SEQUENCE_EDITOR</c> set for this
    /// call to a shell function that copies each message file next to git's todo and the todo
    /// over it (<see cref="RebaseText.SequenceEditor"/>): the messages then live in
    /// <c>rebase-merge/</c> and go with it. The folder is deleted when git returns.</item>
    /// <item>When git refused the app's own todo (exit 1, a rebase folder it made, no
    /// <c>done</c> file, <c>error: invalid line</c> or the <c>missingCommitsCheck</c> warning),
    /// logs it as an error, runs <c>git rebase --abort</c> and throws git's output; otherwise
    /// classifies the call (1.4).</item>
    /// </list>
    /// </summary>
    internal async Task<RebaseResult> InteractiveRebaseAsync(RebasePlan plan, string tempRoot, NetworkOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(tempRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(plan.Onto);
        ArgumentException.ThrowIfNullOrWhiteSpace(plan.ExpectedTip);
        RequirePrefix(plan.BranchRef, HeadsPrefix, nameof(plan));

        // The todo is checked before anything is read or written: a plan that cannot be written is the app's fault.
        var needed = RebaseTodo.OwnMessagesNeeded(plan);

        var head = await ReadHeadAsync(cancellationToken).ConfigureAwait(false);
        if (head.BranchName is null
            || !string.Equals(HeadsPrefix + head.BranchName, plan.BranchRef, StringComparison.Ordinal)
            || !string.Equals(head.Sha, plan.ExpectedTip, StringComparison.OrdinalIgnoreCase))
        {
            throw new GitException($"git rebase -i {plan.Onto}", 0, RebaseHeadMovedMessage);
        }

        var ownMessages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sha in needed)
        {
            ownMessages[sha] = await ReadOwnMessageAsync(sha, cancellationToken).ConfigureAwait(false);
        }

        var files = RebaseTodo.Build(plan, ownMessages);
        var folder = Path.Combine(tempRoot, "visualcommit-rebase-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var todoPath = Path.Combine(folder, RebaseTodo.TodoFileName);
            await File.WriteAllTextAsync(todoPath, files.Todo, RebaseTodo.FileEncoding, cancellationToken).ConfigureAwait(false);
            foreach (var message in files.Messages)
            {
                await File.WriteAllTextAsync(Path.Combine(folder, message.FileName), message.Text, RebaseTodo.FileEncoding, cancellationToken).ConfigureAwait(false);
            }

            var editor = RebaseText.SequenceEditor(todoPath, files.Messages.Count > 0 ? folder : null);
            var environment = new Dictionary<string, string?> { ["GIT_SEQUENCE_EDITOR"] = editor };
            var folderBefore = RebaseFolderExists();
            var command = WriteCommand(options, environment, ["rebase", "-i", .. Positional(plan.Onto)]);
            var result = RebaseText.WithoutEraseLine(await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false));
            if (!folderBefore && result.ExitCode == 1 && RebaseFolderExists() && OwnTodoRefused(result))
            {
                _log.Error($"Git refused the interactive rebase's own todo ({command.DisplayText}): {result.StandardError.Trim()}");
                var abort = WriteCommand(options, "rebase", "--abort");
                var aborted = await _runner.RunAsync(abort, CancellationToken.None).ConfigureAwait(false);
                if (!aborted.Succeeded)
                {
                    _log.Error($"The refused interactive rebase could not be aborted: {aborted.StandardError.Trim()}");
                }

                throw new GitException(command.DisplayText, result.ExitCode, result.StandardError, standardOutput: result.StandardOutput);
            }

            return await ClassifyRebaseAsync(command, result, folderBefore, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DeleteRebaseFolder(folder);
        }
    }

    /// <summary>
    /// Whether git refused the todo the app wrote (2.5): no command of it was done yet
    /// (<c>rebase-merge/done</c> is missing) and git said <c>error: invalid line</c> or gave
    /// <c>rebase.missingCommitsCheck</c>'s warning (<c>some commits may have been dropped
    /// accidentally</c>). Such a rebase is left in progress with HEAD detached at its base.
    /// </summary>
    private bool OwnTodoRefused(GitResult result) =>
        !File.Exists(Path.Combine(GitDirectory, "rebase-merge", "done"))
        && (result.StandardError.Contains("error: invalid line", StringComparison.Ordinal)
            || result.StandardError.Contains("may have been dropped accidentally", StringComparison.Ordinal));

    /// <summary>
    /// A commit's message exactly as git keeps it: the part of <c>git cat-file commit</c> after the
    /// header's first empty line, its <c>#</c> lines and white space included (2.5).
    /// </summary>
    private async Task<string> ReadOwnMessageAsync(string commit, CancellationToken cancellationToken)
    {
        var raw = await ReadOutputAsync(cancellationToken, "cat-file", "commit", commit).ConfigureAwait(false);
        var end = raw.IndexOf("\n\n", StringComparison.Ordinal);
        return end < 0 ? string.Empty : raw[(end + 2)..];
    }

    /// <summary>The full id of the commit <paramref name="revision"/> names, or a <see cref="GitException"/> with git's text.</summary>
    private async Task<string> RequireCommitAsync(string revision, CancellationToken cancellationToken) =>
        (await ReadOutputAsync(cancellationToken, ["rev-parse", "--verify", .. Positional(revision + "^{commit}")]).ConfigureAwait(false)).Trim();

    /// <summary>Deletes the folder the app's todo was written in; a file another program holds is left for the system's clean-up.</summary>
    private void DeleteRebaseFolder(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warning($"Could not delete the interactive rebase's folder {folder}.", ex);
        }
    }

    /// <summary>The pure parts of the rebase calls, kept out of the partial class so that their names cannot meet another package's.</summary>
    internal static class RebaseText
    {
        /// <summary>
        /// The terminal's "erase to the end of the line" (<c>ESC[K</c>) that git writes before
        /// <c>Successfully rebased</c> when <c>TERM</c> names a terminal (0.1); removed from both
        /// streams so that no text shown to the user carries it.
        /// </summary>
        private const string EraseLine = "\u001b[K";

        public static GitResult WithoutEraseLine(GitResult result) =>
            result.StandardError.Contains(EraseLine, StringComparison.Ordinal) || result.StandardOutput.Contains(EraseLine, StringComparison.Ordinal)
                ? result with
                {
                    StandardError = result.StandardError.Replace(EraseLine, string.Empty, StringComparison.Ordinal),
                    StandardOutput = result.StandardOutput.Replace(EraseLine, string.Empty, StringComparison.Ordinal),
                }
                : result;

        /// <summary>
        /// The <c>GIT_SEQUENCE_EDITOR</c> that hands git the app's todo (2.5): git runs it as
        /// <c>sh -c '&lt;editor&gt; "$@"' '&lt;editor&gt;' &lt;todo&gt;</c>, so it is a shell function
        /// whose arguments the appended <c>"$@"</c> becomes (a plain chain of <c>cp</c>s would give
        /// the last one three operands, 0.2): <c>vc() { cp '&lt;folder&gt;/'vc-message-*
        /// "$(dirname "$1")/" &amp;&amp; cp '&lt;todo&gt;' "$1"; }; vc</c>, the message files' copy left
        /// out when <paramref name="messagesFolder"/> is null (the pattern would match nothing and
        /// <c>cp</c> fail). One <c>cp</c> for every message file, not one per file, so that the
        /// command stays short however many groups the todo has: Windows limits a variable and a
        /// command line to 32,767 characters, which some 200 copies of a temp path would pass.
        /// Paths in forward slashes, single-quoted, a <c>'</c> written <c>'\''</c>.
        /// </summary>
        public static string SequenceEditor(string todoPath, string? messagesFolder)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(todoPath);
            var copyTodo = $"cp {Quote(todoPath)} \"$1\"";
            var body = messagesFolder is null
                ? copyTodo
                : $"cp {Quote(messagesFolder.TrimEnd('/', '\\') + "/")}{RebaseTodo.MessageFilePrefix}* \"$(dirname \"$1\")/\" && {copyTodo}";
            return "vc() { " + body + "; }; vc";
        }

        /// <summary><paramref name="path"/> in forward slashes, as one single-quoted word for <c>sh</c>.</summary>
        public static string Quote(string path) => "'" + path.Replace('\\', '/').Replace("'", "'\\''", StringComparison.Ordinal) + "'";

        /// <summary>
        /// The entries of <c>git log -z --format=%H%x00%an%x00%s%x00%B</c>: four fields per commit,
        /// each commit ended by a NUL. The message keeps its line feeds; a carriage return git did
        /// not write is not added.
        /// </summary>
        public static IReadOnlyList<RebaseListEntry> ParseListEntries(string output)
        {
            var fields = output.Split('\0');
            var entries = new List<RebaseListEntry>();
            for (var i = 0; i + 3 < fields.Length; i += 4)
            {
                var sha = fields[i].Trim();
                if (sha.Length == 0)
                {
                    break;
                }

                entries.Add(new RebaseListEntry(sha, fields[i + 2], fields[i + 1], fields[i + 3]));
            }

            return entries;
        }
    }
}
