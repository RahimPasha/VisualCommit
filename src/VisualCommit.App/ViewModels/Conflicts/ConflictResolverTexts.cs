using System.Globalization;
using System.Text.RegularExpressions;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Conflicts;

/// <summary>
/// The conflict resolver's texts (docs/phase-4-design.md 7.2, 10.4, 11.2, 11.4, 11.8, 14.3), word
/// for word, with the singular for one (D113).
/// </summary>
public static partial class ConflictResolverTexts
{
    /// <summary>The result's title strip.</summary>
    public const string ResultTitle = "Result";

    /// <summary>The result's title while the file was marked resolved outside the resolver and the result is modified (11.11).</summary>
    public const string ResolvedElsewhere = "Marked resolved elsewhere";

    /// <summary>The toolbar's position, and the result's count, with no block left (11.2, 14.3).</summary>
    public const string NoConflictsLeft = "No conflicts left";

    /// <summary>Take ours or Take theirs for a side the conflict lacks (10.4, 11.6).</summary>
    public const string SideDeletedReason = "This side deleted the file";

    /// <summary>The Merge tool button without <c>merge.tool</c> (10.4, 11.9).</summary>
    public const string NoMergeToolReason = "Set merge.tool in git's configuration to use a merge tool";

    /// <summary>The button that discards a modified result (7.2).</summary>
    public const string Discard = "Discard";

    /// <summary>The button that keeps a modified result (7.2).</summary>
    public const string KeepEditing = "Keep editing";

    /// <summary>The ours pane's title (11.4): <c>Ours: {ours name}</c>.</summary>
    public static string OursTitle(string oursName) => "Ours: " + oursName;

    /// <summary>The theirs pane's title (11.4): <c>Theirs: {theirs name}</c>.</summary>
    public static string TheirsTitle(string theirsName) => "Theirs: " + theirsName;

    /// <summary>The kind in git's words (11.2).</summary>
    public static string KindText(ConflictKind kind) => kind switch
    {
        ConflictKind.BothModified => "Both modified",
        ConflictKind.BothAdded => "Both added",
        ConflictKind.DeletedByThem => "Deleted by them",
        ConflictKind.DeletedByUs => "Deleted by us",
        ConflictKind.AddedByUs => "Added by us",
        ConflictKind.AddedByThem => "Added by them",
        ConflictKind.BothDeleted => "Both deleted",
        _ => kind.ToString(),
    };

    /// <summary>The header's position among the conflicted files: <c>File {i} of {n}</c>.</summary>
    public static string FilePosition(int position, int count) =>
        string.Create(CultureInfo.InvariantCulture, $"File {position} of {count}");

    /// <summary>The toolbar's position: <c>Conflict {k} of {m}</c>.</summary>
    public static string ConflictPosition(int position, int count) =>
        string.Create(CultureInfo.InvariantCulture, $"Conflict {position} of {count}");

    /// <summary>The result's count (11.2): <c>2 conflicts left</c>, <c>1 conflict left</c>, <c>No conflicts left</c>.</summary>
    public static string ConflictsLeft(int count) => count switch
    {
        0 => NoConflictsLeft,
        1 => "1 conflict left",
        _ => string.Create(CultureInfo.InvariantCulture, $"{count} conflicts left"),
    };

    /// <summary>The result's title while the merge tool runs (11.9).</summary>
    public static string WaitingForTool(string tool) => $"Waiting for {tool} to close…";

    /// <summary>
    /// The sides' names (11.4) for what is in progress: <paramref name="currentName"/> is
    /// <c>{cur}</c>, <paramref name="ontoName"/> a branch at a rebase's new base (null: its short
    /// SHA), <paramref name="theirsLabel"/> the label of the file's first theirs marker, or null.
    /// </summary>
    public static (string Ours, string Theirs) SideNames(InProgressState inProgress, string currentName, string? ontoName, string? theirsLabel)
    {
        ArgumentNullException.ThrowIfNull(inProgress);
        ArgumentNullException.ThrowIfNull(currentName);
        var commit = Short(inProgress.Commit);
        var subject = inProgress.Subject ?? string.Empty;
        var picked = string.IsNullOrEmpty(subject) ? commit : $"{commit} {subject}";
        return inProgress.Kind switch
        {
            InProgressKind.Merge => (currentName, MergedName(inProgress, theirsLabel)),
            InProgressKind.CherryPick => (currentName, picked),
            InProgressKind.Revert => (currentName, $"the revert of {picked}"),
            InProgressKind.Rebase => ($"{ontoName ?? Short(inProgress.Onto)} (the new base)", $"{picked} (your commit)"),
            InProgressKind.ApplyMailbox => (currentName, "the patch"),
            _ => (currentName, theirsLabel == "Stashed changes" ? "the stash" : "the other side"),
        };
    }

    /// <summary>
    /// The card's text for a conflict without text (11.8), with <paramref name="file"/> the file's
    /// name; null for a text conflict.
    /// </summary>
    public static string CardText(ConflictCardKind kind, string file, string oursName, string theirsName, string? otherName = null, string? folder = null) => kind switch
    {
        ConflictCardKind.DeletedByThem => $"{oursName} changed {file}; {theirsName} deleted it.",
        ConflictCardKind.DeletedByUs => $"{oursName} deleted {file}; {theirsName} changed it.",
        ConflictCardKind.AddedByUs => $"{file} exists only in {oursName}.",
        ConflictCardKind.AddedByThem => $"{file} exists only in {theirsName}.",
        ConflictCardKind.MovedAside => $"Git moved {file} aside because {otherName} has a folder named {folder}: rename or remove it, then mark it resolved.",
        ConflictCardKind.BothDeleted => $"Both sides deleted {file}.",
        ConflictCardKind.Binary => $"{file} is a binary file: its two versions cannot be merged line by line.",
        ConflictCardKind.SymbolicLink => $"{file} is a symbolic link changed on both sides.",
        ConflictCardKind.Submodule => $"{file} is a submodule: resolve it in a terminal.",
        _ => string.Empty,
    };

    /// <summary>Leaving a modified result (7.2, 11.1): Discard (danger), Keep editing.</summary>
    public static ConfirmationRequest LeaveQuestion(string file) =>
        new($"Discard your changes to {file}?", "The result has not been marked resolved.", string.Empty, Discard, IsDestructive: true) { CancelText = KeepEditing };

    /// <summary>The merge tool over a modified result (7.2, 11.9): Discard (danger), Keep editing.</summary>
    public static ConfirmationRequest MergeToolQuestion(string file) =>
        new($"Discard your changes to {file}?", "The merge tool starts from the file as it is on disk.", string.Empty, Discard, IsDestructive: true) { CancelText = KeepEditing };

    /// <summary>Mark resolved with blocks left (7.2, 11.7): Mark resolved (primary), Cancel.</summary>
    public static ConfirmationRequest MarkersQuestion(string file) =>
        new($"{file} still has conflict markers.", "Mark it resolved anyway? The markers are committed with it.", string.Empty, "Mark resolved", IsDestructive: false);

    /// <summary>Mark resolved over a file changed on disk (7.2, 11.7): Overwrite (danger), Cancel.</summary>
    public static ConfirmationRequest ChangedOnDiskQuestion(string file) =>
        new($"{file} changed on disk.", "Overwrite it with the result?", string.Empty, "Overwrite", IsDestructive: true);

    /// <summary>A commit's short SHA (its first 7 characters), or empty.</summary>
    public static string Short(string? sha) => sha is null ? string.Empty : sha[..Math.Min(7, sha.Length)];

    /// <summary>
    /// A merge's theirs name (11.4): the branch of <c>MERGE_MSG</c>'s <c>Merge branch '&lt;x&gt;'</c>
    /// (or <c>remote-tracking branch '&lt;x&gt;'</c>), else the theirs marker's label, else
    /// <c>MERGE_HEAD</c>'s short SHA.
    /// </summary>
    private static string MergedName(InProgressState inProgress, string? theirsLabel)
    {
        if (inProgress.Subject is { } subject && MergedBranch().Match(subject) is { Success: true } match)
        {
            return match.Groups["name"].Value;
        }

        return !string.IsNullOrEmpty(theirsLabel) ? theirsLabel : Short(inProgress.Commit);
    }

    [GeneratedRegex(@"^Merge (?:remote-tracking )?branch '(?<name>[^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex MergedBranch();
}

/// <summary>What a conflict without text is, which decides its card (11.8).</summary>
public enum ConflictCardKind
{
    /// <summary><c>UD</c>: ours changed the file, theirs deleted it.</summary>
    DeletedByThem,

    /// <summary><c>DU</c>: ours deleted the file, theirs changed it.</summary>
    DeletedByUs,

    /// <summary><c>AU</c>: only ours has the file.</summary>
    AddedByUs,

    /// <summary><c>UA</c>: only theirs has the file.</summary>
    AddedByThem,

    /// <summary><c>AU</c> or <c>UA</c> on <c>&lt;p&gt;~&lt;label&gt;</c>, <c>&lt;p&gt;</c> being a folder: a file/folder clash.</summary>
    MovedAside,

    /// <summary><c>DD</c>: both sides deleted it.</summary>
    BothDeleted,

    /// <summary>Both sides have it and a side is binary.</summary>
    Binary,

    /// <summary>Both sides have it and a side is a symbolic link.</summary>
    SymbolicLink,

    /// <summary>A submodule.</summary>
    Submodule,
}
