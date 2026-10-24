using System.Globalization;
using VisualCommit.Core.Git;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// The texts the dialogs share (docs/phase-3-design.md 2, 4f): how a commit is named, and the
/// messages a name box shows under it (2.4, 2.23, D96, D114).
/// </summary>
public static class DialogText
{
    /// <summary>How many characters of a subject a dialog shows before it cuts with "…".</summary>
    public const int SubjectLength = 50;

    /// <summary>"Enter a name.": a name box that was edited and is empty (D114).</summary>
    public const string EnterName = "Enter a name.";

    /// <summary>"Enter a URL.": the Remotes form's URL box, edited and empty (2.23).</summary>
    public const string EnterUrl = "Enter a URL.";

    /// <summary>The detail of every confirmation that saves a backup first (design 2).</summary>
    public const string BackupDetail = "A backup is saved first, so this can be undone.";

    /// <summary>A subject cut at <see cref="SubjectLength"/> characters with "…".</summary>
    public static string Subject(string subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return subject.Length <= SubjectLength ? subject : string.Concat(subject.AsSpan(0, SubjectLength), "…");
    }

    /// <summary>A commit as dialogs name it: <c>a1b2c3d "Add core"</c>, the subject cut as <see cref="Subject"/> does.</summary>
    public static string Commit(string sha, string subject)
    {
        ArgumentNullException.ThrowIfNull(sha);
        return $"{ShortSha(sha)} \"{Subject(subject)}\"";
    }

    /// <summary>The first 7 characters of a commit id.</summary>
    public static string ShortSha(string sha)
    {
        ArgumentNullException.ThrowIfNull(sha);
        return sha[..Math.Min(7, sha.Length)];
    }

    /// <summary>
    /// What the name box of a branch says about <paramref name="name"/> (2.4), or null when it is
    /// a valid new name: "Enter a name.", "This is not a valid branch name: &lt;reason&gt;.",
    /// "A branch named &lt;name&gt; exists.", "A branch named &lt;existing&gt; exists in another
    /// case." or "This conflicts with the branch &lt;existing&gt;.".
    /// </summary>
    /// <param name="name">The name as typed.</param>
    /// <param name="existing">The local branches' short names.</param>
    /// <param name="ignoreCase">Compare ignoring case too (<see cref="RefNameRules.NamesIgnoreCase"/>: Windows and macOS).</param>
    public static string? BranchNameError(string name, IEnumerable<string> existing, bool ignoreCase) =>
        NameError("branch", name, RefNameRules.BranchNameProblem, existing, ignoreCase);

    /// <summary>What the name box of a tag says (2.27): <see cref="BranchNameError"/>'s texts with "tag".</summary>
    public static string? TagNameError(string name, IEnumerable<string> existing, bool ignoreCase) =>
        NameError("tag", name, RefNameRules.TagNameProblem, existing, ignoreCase);

    /// <summary>
    /// What the Remotes form says about a remote's name (2.23), or null: "Enter a name.", "This is
    /// not a valid remote name: &lt;reason&gt;.", "A remote named &lt;name&gt; exists." or "A remote
    /// named &lt;existing&gt; exists in another case." (only with <paramref name="ignoreCase"/>).
    /// </summary>
    public static string? RemoteNameError(string name, IEnumerable<string> existing, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(existing);
        if (name.Length == 0)
        {
            return EnterName;
        }

        if (RefNameRules.RemoteNameProblem(name) is { } problem)
        {
            return $"This is not a valid remote name: {problem}.";
        }

        var names = existing.ToList();
        if (names.Contains(name, StringComparer.Ordinal))
        {
            return $"A remote named {name} exists.";
        }

        return ignoreCase && names.FirstOrDefault(other => string.Equals(other, name, StringComparison.OrdinalIgnoreCase)) is { } otherCase
            ? $"A remote named {otherCase} exists in another case."
            : null;
    }

    /// <summary>A workspace name of more than <see cref="Workspace.MaxNameLength"/> characters (docs/phase-6-design.md 1.6).</summary>
    public const string WorkspaceNameTooLong = "This is not a valid workspace name: it has more than 100 characters.";

    /// <summary>A workspace name with a line break, a tab or another control character (1.6).</summary>
    public const string WorkspaceNameHasControlCharacter = "This is not a valid workspace name: it has a line break or another control character.";

    /// <summary>
    /// What the name box of a workspace says about <paramref name="name"/> (docs/phase-6-design.md
    /// 1.6), or null when it is a valid name. The trimmed name is checked in this order: empty
    /// "Enter a name."; longer than <see cref="Workspace.MaxNameLength"/> characters (as a reader
    /// counts them: text elements, so an emoji is one); a control character (a line break, a tab)
    /// or a Unicode line or paragraph separator (U+2028, U+2029); another workspace has it, ignoring case on every platform (a name is no path):
    /// "A workspace named &lt;existing&gt; exists.", in the other's spelling.
    /// </summary>
    /// <param name="name">The name as typed.</param>
    /// <param name="existing">The other workspaces' names (on a rename, the workspace itself left out).</param>
    public static string? WorkspaceNameError(string name, IEnumerable<string> existing)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(existing);
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return EnterName;
        }

        if (new StringInfo(trimmed).LengthInTextElements > Workspace.MaxNameLength)
        {
            return WorkspaceNameTooLong;
        }

        if (trimmed.Any(IsLineBreakOrControl))
        {
            return WorkspaceNameHasControlCharacter;
        }

        return existing.FirstOrDefault(other => string.Equals(other.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)) is { } clash
            ? $"A workspace named {clash} exists."
            : null;
    }

    /// <summary>A control character (Cc: line feed, tab, …) or a Unicode line or paragraph separator (U+2028, U+2029), which also break a line.</summary>
    private static bool IsLineBreakOrControl(char character) =>
        char.IsControl(character)
        || char.GetUnicodeCategory(character) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;

    private static string? NameError(string kind, string name, Func<string, string?> problemOf, IEnumerable<string> existing, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(existing);
        if (name.Length == 0)
        {
            return EnterName;
        }

        if (problemOf(name) is { } problem)
        {
            return $"This is not a valid {kind} name: {problem}.";
        }

        return RefNameRules.FindClash(name, existing, ignoreCase) switch
        {
            null => null,
            { Kind: RefNameClashKind.SameName } => $"A {kind} named {name} exists.",
            { Kind: RefNameClashKind.OtherCase, Existing: var other } when string.Equals(other, name, StringComparison.OrdinalIgnoreCase) =>
                $"A {kind} named {other} exists in another case.",
            { Existing: var other } => $"This conflicts with the {kind} {other}.",
        };
    }
}
