using System.Globalization;
using System.Text;

namespace VisualCommit.Core.Git;

/// <summary>
/// The todo list and message files the app hands git for an interactive rebase or a squash
/// (docs/phase-4-design.md 2.5, section 3, D143). Pure: built from a <see cref="RebasePlan"/>,
/// written by the git layer into a folder of its own and copied into <c>rebase-merge/</c> by the
/// app's sequence editor.
/// <para>
/// The todo has full SHAs without subjects, no comments, LF line ends and an explicit
/// <c>drop</c> line for every dropped commit (so that <c>rebase.missingCommitsCheck</c> passes).
/// A plain pick is <c>pick &lt;sha&gt;</c>. Every row whose message changes and every group (a
/// target with the squash and fixup rows that join it) has one shape: <c>label vc-&lt;n&gt;</c>,
/// <c>pick &lt;target&gt;</c>, <c>fixup &lt;sha&gt;</c> for each member in history order (squash
/// members too: the message comes from the file), then <see cref="GuardedAmend"/>, which gives
/// the commit the message file <c>vc-message-&lt;n&gt;</c> only when the group made exactly one
/// commit on top of its label. The files are meant to be written as UTF-8 without a byte order
/// mark (<see cref="FileEncoding"/>).
/// </para>
/// </summary>
public static class RebaseTodo
{
    /// <summary>The name of the todo file in the folder the git layer writes (the sequence editor copies it over git's own).</summary>
    public const string TodoFileName = "todo";

    /// <summary>
    /// UTF-8 without a byte order mark: a BOM breaks git's todo (<c>error: invalid line 1</c>,
    /// docs/phase-4-design.md 0.2) and would enter a commit message.
    /// </summary>
    public static Encoding FileEncoding { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>What every message file's name starts with: the sequence editor copies them all with one pattern.</summary>
    public const string MessageFilePrefix = "vc-message-";

    /// <summary>The message file of group <paramref name="group"/> (1, 2, …), as it lies in <c>rebase-merge/</c>: <c>vc-message-&lt;n&gt;</c>.</summary>
    public static string MessageFileName(int group) => MessageFilePrefix + GroupNumber(group);

    /// <summary>The label written before group <paramref name="group"/>'s pick: <c>vc-&lt;n&gt;</c> (git keeps it as <c>refs/rewritten/vc-&lt;n&gt;</c> while the rebase runs).</summary>
    public static string Label(int group) => "vc-" + GroupNumber(group);

    /// <summary>
    /// The <c>exec</c> line that ends group <paramref name="group"/> (2.5): it amends HEAD with the
    /// group's message file only when <c>refs/rewritten/vc-&lt;n&gt;</c> exists and is HEAD's first
    /// parent, so a pick that was skipped or became empty never rewrites another commit, and a
    /// missing label never matches. The amend keeps the message exactly (<c>#</c> lines too), the
    /// author and the author date, and runs the <c>pre-commit</c> and <c>commit-msg</c> hooks, as
    /// git's own reword does. The file is found through <c>git rev-parse --git-path</c>, so the
    /// line works from any worktree.
    /// </summary>
    public static string GuardedAmend(int group)
    {
        var label = Label(group);
        return $"exec if l=$(git rev-parse -q --verify refs/rewritten/{label}) && [ -n \"$l\" ] && [ \"$(git rev-parse -q --verify HEAD^)\" = \"$l\" ]; "
            + $"then git commit --amend --only --allow-empty --cleanup=whitespace --quiet --file=\"$(git rev-parse --git-path rebase-merge/{MessageFileName(group)})\"; fi";
    }

    /// <summary>
    /// The commits whose own message <see cref="Build"/> needs, because the plan gives none for a
    /// group's target: the target of a reword or a group without <see cref="RebaseStep.Message"/>,
    /// and, for such a group with a squash, its squash members. The git layer reads each exactly
    /// (<c>git cat-file commit &lt;sha&gt;</c>, the part after the header). In plan order, each once.
    /// </summary>
    /// <exception cref="ArgumentException">The plan cannot be written (see <see cref="Build"/>).</exception>
    public static IReadOnlyList<string> OwnMessagesNeeded(RebasePlan plan)
    {
        var needed = new List<string>();
        foreach (var group in Groups(plan))
        {
            if (!group.ChangesMessage || group.Target.Message is not null)
            {
                continue;
            }

            needed.Add(group.Target.Sha);
            needed.AddRange(group.Members.Where(member => member.Action == RebaseAction.Squash).Select(member => member.Sha));
        }

        return [.. needed.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The todo and message files for <paramref name="plan"/>. A group's message is its target's
    /// <see cref="RebaseStep.Message"/>; without one, the target's own message from
    /// <paramref name="ownMessages"/> for a reword or a group of fixups only, and for a group
    /// with a squash git's own squash message (<see cref="SquashMessage"/> of the target's and
    /// the squash members' own messages). A message the plan gives gets LF line ends; an own
    /// message is written as it was read. Dropped rows inside a group are written right after
    /// the group, in their order. A pick that no squash or fixup joins ignores a message the
    /// plan gives it.
    /// </summary>
    /// <param name="plan">The steps, oldest first.</param>
    /// <param name="ownMessages">The own messages of <see cref="OwnMessagesNeeded"/>, by full SHA.</param>
    /// <exception cref="ArgumentException">
    /// The plan has no steps, a SHA that is not a full hexadecimal object id or appears twice, a
    /// squash or fixup with no pick or reword before it to join, or <paramref name="ownMessages"/>
    /// lacks a message it needs.
    /// </exception>
    public static RebaseTodoFiles Build(RebasePlan plan, IReadOnlyDictionary<string, string>? ownMessages = null)
    {
        var todo = new StringBuilder();
        var messages = new List<RebaseMessageFile>();
        foreach (var group in Groups(plan))
        {
            // A group without a target holds only drops that come before every target.
            if (group.HasTarget && !group.ChangesMessage)
            {
                Line(todo, "pick", group.Target.Sha);
            }
            else if (group.ChangesMessage)
            {
                var number = messages.Count + 1;
                todo.Append("label ").Append(Label(number)).Append('\n');
                Line(todo, "pick", group.Target.Sha);
                foreach (var member in group.Members)
                {
                    Line(todo, "fixup", member.Sha);
                }

                todo.Append(GuardedAmend(number)).Append('\n');
                messages.Add(new RebaseMessageFile(MessageFileName(number), GroupMessage(group, ownMessages)));
            }

            foreach (var dropped in group.Dropped)
            {
                Line(todo, "drop", dropped.Sha);
            }
        }

        return new RebaseTodoFiles(todo.ToString(), messages);

        static void Line(StringBuilder todo, string command, string sha) => todo.Append(command).Append(' ').Append(sha).Append('\n');
    }

    /// <summary>
    /// Git's squash message for <paramref name="messagesOldestFirst"/>, as the list and the squash
    /// dialog start it (2.2, section 3): the messages in history order, each without the white
    /// space at its end, joined by a blank line, with one line feed at the end. Unlike git's own
    /// (with <c>GIT_EDITOR=:</c>), the messages' own <c>#</c> lines stay.
    /// </summary>
    public static string SquashMessage(IEnumerable<string> messagesOldestFirst)
    {
        ArgumentNullException.ThrowIfNull(messagesOldestFirst);
        var parts = messagesOldestFirst.Select(message => message.ReplaceLineEndings("\n").TrimEnd()).ToList();
        return parts.Count == 0 ? string.Empty : string.Join("\n\n", parts) + "\n";
    }

    /// <summary>
    /// The plan of "Squash {N} commits" (docs/phase-4-design.md section 3): <paramref name="list"/>
    /// is the "from here" list read from the oldest squashed commit's parent
    /// (<c>ReadRebaseListAsync(parent, branchRef, fromCommit: true)</c>); the oldest
    /// <paramref name="squashed"/>.Count commits of it must be exactly <paramref name="squashed"/>.
    /// They become one group with <paramref name="message"/> (the oldest a pick, the others
    /// squashes), and every later commit a pick. The rebase goes onto the list's base and expects
    /// the list's tip.
    /// </summary>
    /// <exception cref="ArgumentException">Fewer than two commits, or they are not the oldest of the list.</exception>
    public static RebasePlan SquashPlan(RebaseList list, string branchRef, IReadOnlyCollection<string> squashed, string message)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        ArgumentNullException.ThrowIfNull(squashed);
        ArgumentNullException.ThrowIfNull(message);
        if (squashed.Count < 2)
        {
            throw new ArgumentException("A squash joins at least two commits.", nameof(squashed));
        }

        var commits = list.CommitsOldestFirst;
        var chosen = new HashSet<string>(squashed, StringComparer.OrdinalIgnoreCase);
        if (chosen.Count != squashed.Count || commits.Count < chosen.Count || !commits.Take(chosen.Count).All(entry => chosen.Contains(entry.Sha)))
        {
            throw new ArgumentException("The squashed commits must be the oldest commits of the list, each once.", nameof(squashed));
        }

        var steps = commits.Select((entry, index) => index switch
        {
            0 => new RebaseStep(entry.Sha, RebaseAction.Pick, message),
            _ when index < chosen.Count => new RebaseStep(entry.Sha, RebaseAction.Squash, null),
            _ => new RebaseStep(entry.Sha, RebaseAction.Pick, null),
        });
        return new RebasePlan(list.Onto, list.BranchTip, branchRef, [.. steps]);
    }

    private static string GroupMessage(Group group, IReadOnlyDictionary<string, string>? ownMessages)
    {
        if (group.Target.Message is { } given)
        {
            return given.ReplaceLineEndings("\n");
        }

        var squashes = group.Members.Where(member => member.Action == RebaseAction.Squash).ToList();
        if (squashes.Count == 0)
        {
            return Own(group.Target.Sha);
        }

        return SquashMessage([Own(group.Target.Sha), .. squashes.Select(member => Own(member.Sha))]);

        string Own(string sha) =>
            ownMessages is not null && ownMessages.TryGetValue(sha, out var message)
                ? message
                : throw new ArgumentException($"The own message of {sha} is needed and was not given.", nameof(ownMessages));
    }

    /// <summary>
    /// The plan's steps cut into groups, in order: each Pick or Reword starts one, with the
    /// Squash and Fixup rows after it (newer) as its members and the Drop rows among them kept
    /// aside; Drop rows before the first target form groups of their own without a target.
    /// </summary>
    private static List<Group> Groups(RebasePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(plan.StepsOldestFirst);
        if (plan.StepsOldestFirst.Count == 0)
        {
            throw new ArgumentException("An interactive rebase needs at least one commit.", nameof(plan));
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groups = new List<Group>();
        Group? current = null;
        foreach (var step in plan.StepsOldestFirst)
        {
            ArgumentNullException.ThrowIfNull(step, nameof(plan));
            if (!IsObjectId(step.Sha))
            {
                throw new ArgumentException($"Not a full commit id: '{step.Sha}'.", nameof(plan));
            }

            if (!seen.Add(step.Sha))
            {
                throw new ArgumentException($"The commit {step.Sha} appears twice in the plan.", nameof(plan));
            }

            switch (step.Action)
            {
                case RebaseAction.Pick or RebaseAction.Reword:
                    current = new Group(step);
                    groups.Add(current);
                    break;
                case RebaseAction.Squash or RebaseAction.Fixup:
                    if (current is null || !current.HasTarget)
                    {
                        throw new ArgumentException($"The {step.Action.ToString().ToLowerInvariant()} of {step.Sha} has no pick or reword before it to join.", nameof(plan));
                    }

                    current.Members.Add(step);
                    break;
                case RebaseAction.Drop:
                    if (current is null)
                    {
                        // Drops before any target: a group with nothing but drops.
                        current = new Group(null);
                        groups.Add(current);
                    }

                    current.Dropped.Add(step);
                    break;
                default:
                    throw new ArgumentException($"Unknown action {step.Action}.", nameof(plan));
            }
        }

        return groups;
    }

    /// <summary>A full object id: 40 (SHA-1) or 64 (SHA-256) hexadecimal digits, nothing else, so that no text can enter the todo.</summary>
    private static bool IsObjectId(string? sha) =>
        sha is { Length: 40 or 64 } && sha.All(char.IsAsciiHexDigit);

    private static string GroupNumber(int group) =>
        group >= 1 ? group.ToString(CultureInfo.InvariantCulture) : throw new ArgumentOutOfRangeException(nameof(group), group, "Groups are numbered from 1.");

    /// <summary>One target with what joins it, or (target null) drops that come before every target.</summary>
    private sealed class Group(RebaseStep? target)
    {
        private readonly RebaseStep? _target = target;

        public RebaseStep Target => _target ?? throw new InvalidOperationException("A group of drops has no target.");

        public bool HasTarget => _target is not null;

        public List<RebaseStep> Members { get; } = [];

        public List<RebaseStep> Dropped { get; } = [];

        /// <summary>Whether the group has the labelled shape: a reword, or a target that something joins.</summary>
        public bool ChangesMessage => _target is not null && (_target.Action == RebaseAction.Reword || Members.Count > 0);
    }
}

/// <summary>What <see cref="RebaseTodo.Build"/> makes: the todo and the message files.</summary>
/// <param name="Todo">The todo list, LF line ends, ending with a line feed.</param>
/// <param name="Messages">The message files in group order (<c>vc-message-1</c>, …).</param>
public sealed record RebaseTodoFiles(string Todo, IReadOnlyList<RebaseMessageFile> Messages);

/// <summary>One message file of an interactive rebase's todo.</summary>
/// <param name="FileName">Its name in <c>rebase-merge/</c> (<see cref="RebaseTodo.MessageFileName"/>).</param>
/// <param name="Text">The message, written as <see cref="RebaseTodo.FileEncoding"/>.</param>
public sealed record RebaseMessageFile(string FileName, string Text);
