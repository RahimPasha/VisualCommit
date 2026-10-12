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
    /// The standard error of the <see cref="GitException"/> that
    /// <see cref="InteractiveRebaseAsync(RebasePlan, NetworkOptions?, CancellationToken)"/> throws, before anything runs, when HEAD is no longer on the plan's branch at the tip the
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
    /// Whether git stashes a dirty working tree itself before a rebase (1.3 step 1, D142): <c>git
    /// config --type=bool --get rebase.autoStash</c>, read as git reads a boolean; exit 1 (unset) is
    /// false. A read (<c>GIT_OPTIONAL_LOCKS=0</c>); a value git cannot read as a boolean throws
    /// <see cref="GitException"/> with git's text, as <c>git rebase</c> would refuse it.
    /// </summary>
    public async Task<bool> ReadRebaseAutoStashAsync(NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        var command = new GitCommand("config", "--type=bool", "--get", "rebase.autoStash") { WorkingDirectory = WorkingDirectory, Environment = ReadEnvironmentWith(options) };
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1)
        {
            return false;
        }

        return string.Equals(result.EnsureSuccess(command).StandardOutput.Trim(), "true", StringComparison.Ordinal);
    }

    /// <summary>
    /// The commits an interactive rebase of <paramref name="branch"/> would replay (2.3), read from
    /// the branch's tip as it is now, which the list keeps as <see cref="RebaseList.BranchTip"/>.
    /// "From here": <c>git log --reverse --topo-order &lt;onto&gt;..&lt;tip&gt;</c>; otherwise the commits
    /// git's own todo holds, as git's sequencer chooses them: <c>git log --reverse --topo-order
    /// --no-merges --right-only --cherry-mark &lt;onto&gt;...&lt;tip&gt;</c>, less the commits
    /// marked <c>=</c> (their patch is already upstream) that do not start empty. A commit that
    /// starts empty (its tree is its first parent's) is kept even when marked, as git keeps it:
    /// every empty commit has the same patch id, so <c>--cherry-pick</c> would drop a branch's
    /// empty commit whenever the target side has one too (the review's findings 2 and 3). Each
    /// with its subject, author name and whole message (<c>%B</c>, re-encoded to UTF-8). The merge
    /// count is <c>git rev-list --merges --count &lt;onto&gt;..&lt;tip&gt;</c> in both cases.
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
            : ["--no-merges", "--right-only", "--cherry-mark", $"{ontoSha}...{tip}"];
        var log = await ReadOutputAsync(
            cancellationToken,
            ["log", "--no-show-signature", "--no-color", "--encoding=UTF-8", "-z", $"--format={RebaseText.ListFormat}", "--reverse", "--topo-order", .. range, "--"])
            .ConfigureAwait(false);
        var commits = RebaseText.ParseListCommits(log);
        if (!fromCommit)
        {
            commits = await WithoutPatchesUpstreamAsync(commits, cancellationToken).ConfigureAwait(false);
        }

        var merges = ParseCount(await ReadOutputAsync(cancellationToken, "rev-list", "--merges", "--count", $"{ontoSha}..{tip}", "--").ConfigureAwait(false));
        return new RebaseList(ontoSha, tip, [.. commits.Select(commit => commit.Entry)], merges);
    }

    /// <summary>
    /// <paramref name="commits"/> without those whose patch is upstream (<c>--cherry-mark</c>'s
    /// <c>=</c>) and that do not start empty, as git's sequencer leaves them out of its todo
    /// (<c>if (!is_empty &amp;&amp; (flags &amp; PATCHSAME)) continue;</c>): a commit starts empty when its
    /// tree is its first parent's, or the empty tree for a root commit. The parents' trees of the
    /// marked commits are read with one <c>git rev-parse</c>.
    /// </summary>
    private async Task<IReadOnlyList<RebaseText.ListCommit>> WithoutPatchesUpstreamAsync(IReadOnlyList<RebaseText.ListCommit> commits, CancellationToken cancellationToken)
    {
        var marked = commits.Where(commit => commit.PatchUpstream && commit.FirstParent is not null).ToList();
        var parentTrees = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (marked.Count > 0)
        {
            var trees = (await ReadOutputAsync(cancellationToken, ["rev-parse", .. marked.Select(commit => commit.FirstParent + "^{tree}")]).ConfigureAwait(false))
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 0; i < marked.Count && i < trees.Length; i++)
            {
                parentTrees[marked[i].Entry.Sha] = trees[i];
            }
        }

        return [.. commits.Where(commit => !commit.PatchUpstream || StartsEmpty(commit))];

        bool StartsEmpty(RebaseText.ListCommit commit) => commit.FirstParent is null
            ? RebaseText.EmptyTrees.Contains(commit.Tree, StringComparer.OrdinalIgnoreCase)
            : parentTrees.TryGetValue(commit.Entry.Sha, out var parentTree) && string.Equals(parentTree, commit.Tree, StringComparison.OrdinalIgnoreCase);
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
    /// messages it needs exactly (<see cref="ReadOwnMessageAsync"/>, in UTF-8), and writes them
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
    /// A commit's message exactly as git keeps it, its <c>#</c> lines and white space included
    /// (2.5), in UTF-8: <c>git log -1 --encoding=UTF-8 -z --format=%B &lt;sha&gt;</c>, which gives the
    /// raw message re-encoded from the commit's <c>encoding</c> header and ends it with a NUL
    /// (taken off). The raw <c>git cat-file commit</c> would hand a commit made with
    /// <c>i18n.commitEncoding=ISO-8859-1</c> to the UTF-8 decoder, and every non-ASCII letter
    /// would become U+FFFD in the rewritten commit (the review's finding 15).
    /// </summary>
    private async Task<string> ReadOwnMessageAsync(string commit, CancellationToken cancellationToken)
    {
        var raw = await ReadOutputAsync(cancellationToken, "log", "-1", "--no-show-signature", "--no-color", "--encoding=UTF-8", "-z", "--format=%B", commit, "--").ConfigureAwait(false);
        return raw.EndsWith('\0') ? raw[..^1] : raw;
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
        /// The list's <c>git log</c> format (2.3): the mark (<c>=</c> for a commit whose patch is
        /// upstream under <c>--cherry-mark</c>), the SHA, its tree, its parents, the author's name,
        /// the subject and the whole message, NUL-separated (with <c>-z</c>, each commit ended by a NUL).
        /// </summary>
        public const string ListFormat = "%m%x00%H%x00%T%x00%P%x00%an%x00%s%x00%B";

        /// <summary>The empty tree's id, SHA-1 and SHA-256: a root commit with it starts empty.</summary>
        public static readonly string[] EmptyTrees =
        [
            "4b825dc642cb6eb9a060e54bf8d69288fbee4904",
            "6ef19b41225c5369f1c104d45d8d85efa9b057b53b14b4b9b939dd74decc5321",
        ];

        /// <summary>One commit of <see cref="ListFormat"/>'s output.</summary>
        /// <param name="Entry">What the list shows of it.</param>
        /// <param name="PatchUpstream">Marked <c>=</c>: its patch is already upstream.</param>
        /// <param name="Tree">Its tree.</param>
        /// <param name="FirstParent">Its first parent, or null for a root commit.</param>
        public sealed record ListCommit(RebaseListEntry Entry, bool PatchUpstream, string Tree, string? FirstParent);

        /// <summary>
        /// The commits of <c>git log -z --format=</c><see cref="ListFormat"/>: seven fields per
        /// commit, each commit ended by a NUL. The message keeps its line feeds; a carriage return
        /// git did not write is not added.
        /// </summary>
        public static IReadOnlyList<ListCommit> ParseListCommits(string output)
        {
            var fields = output.Split('\0');
            var commits = new List<ListCommit>();
            for (var i = 0; i + 6 < fields.Length; i += 7)
            {
                var sha = fields[i + 1].Trim();
                if (sha.Length == 0)
                {
                    break;
                }

                var parents = fields[i + 3].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                commits.Add(new ListCommit(
                    new RebaseListEntry(sha, fields[i + 5], fields[i + 4], fields[i + 6]),
                    fields[i].Trim() == "=",
                    fields[i + 2].Trim(),
                    parents.Length > 0 ? parents[0] : null));
            }

            return commits;
        }
    }
}
