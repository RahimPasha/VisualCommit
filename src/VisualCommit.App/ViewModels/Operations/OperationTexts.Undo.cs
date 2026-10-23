using System.Globalization;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Undo;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The texts of undo and redo and of the backups list's restores (docs/phase-5-design.md 7.4 to
/// 7.6, 7.10, 12.1, 12.2, 13.2; the report's part U and decisions UD9, UD10, UD13, BD15, BD17,
/// BD20): the operations' four texts, their results, the reasons the toolbar gives, the wording of
/// a refused step's checks and the questions before a step that changes a remote. An entry's own
/// text is <see cref="UndoTexts"/>' (Core). Counts use the singular for one (D113).
/// </summary>
public static partial class OperationTexts
{
    /// <summary>Undo with no entry before the undo history's cursor (7.4's reason 4).</summary>
    public const string NothingToUndo = "Nothing to undo";

    /// <summary>Redo with no entry after the cursor (7.4's reason 4).</summary>
    public const string NothingToRedo = "Nothing to redo";

    /// <summary>Undo and Redo while the interactive rebase's list is open (7.4's reason 3): it holds work, and moving its branch would disable its Start.</summary>
    public const string CloseInteractiveRebaseFirst = "Close the interactive rebase first";

    /// <summary>Undo and Redo with conflicts and no operation in progress (7.4's reason 2, D119's text).</summary>
    public const string ResolveConflictsFirst = "Resolve the conflicts first";

    /// <summary>The ending of 7.6's text for local changes in the way of a move or a switch.</summary>
    private const string CommitStashOrDiscard = "Commit, stash, or discard your changes first.";

    /// <summary>How many paths a text names before it stops (7.6's "{first names}").</summary>
    private const int NamesShown = 3;

    /// <summary>12.1: the toolbar's Undo of an entry, <c>Undo commit "Fix the parser"</c>.</summary>
    public static OperationText Undo(string text) =>
        new($"Undo {text}", $"Undoing {text}…", "Undo failed", $"Git could not undo {text}.");

    /// <summary>12.1: the toolbar's Redo of an entry.</summary>
    public static OperationText Redo(string text) =>
        new($"Redo {text}", $"Redoing {text}…", "Redo failed", $"Git could not redo {text}.");

    /// <summary>The toolbar's Undo or Redo of an entry with <paramref name="text"/>.</summary>
    public static OperationText Step(UndoDirection direction, string text) =>
        direction == UndoDirection.Undo ? Undo(text) : Redo(text);

    /// <summary>12.2: <c>Undid commit "Fix the parser"</c>, <c>Redid …</c>.</summary>
    public static string Stepped(UndoDirection direction, string text) =>
        direction == UndoDirection.Undo ? $"Undid {text}" : $"Redid {text}";

    /// <summary>The toolbar's Undo or Redo while enabled (1.1): <c>Undo {text}</c>, <c>Redo {text}</c>.</summary>
    public static string StepToolTip(UndoDirection direction, string text) => Step(direction, text).Title;

    /// <summary>7.4's reason 1: a user operation of the repository runs or waits.</summary>
    public static string WaitFor(string title) => $"Wait for {title} to finish";

    /// <summary>A refused step's summary (7.6, 12.1): the check's reason after <c>Can't undo {text}: </c>.</summary>
    public static string CantStep(UndoDirection direction, string text, string reason) =>
        $"Can't {(direction == UndoDirection.Undo ? "undo" : "redo")} {text}: {reason}";

    /// <summary>
    /// A refused restore of the backups list (7.10): <c>Can't restore branch doomed: a branch named
    /// doomed exists again</c>, the operation's title after <c>Can't</c>.
    /// </summary>
    public static string CantRestore(OperationText text, string reason)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"Can't {char.ToLowerInvariant(text.Title[0])}{text.Title[1..]}: {reason}";
    }

    /// <summary>
    /// A lease that failed (7.5, UD9): <c>published on origin has changed since: nothing was
    /// changed.</c>, the summary of the step's or the push back's error dialog without the prefix.
    /// </summary>
    public static string RemoteChangedSince(string name, string remote) => $"{name} on {remote} has changed since: nothing was changed.";

    /// <summary>
    /// What a refused check found, in 7.6's words and 7.12's (the toolbar's tooltip, and after
    /// <see cref="CantStep"/> the error dialog's summary). Paths are named as git names them,
    /// relative to the working tree (UD13); <paramref name="here"/> is this worktree's folder, for a
    /// branch checked out in it.
    /// </summary>
    public static string RefusalReason(UndoRefusal refusal, string here)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        ArgumentNullException.ThrowIfNull(here);
        var name = refusal.Name ?? string.Empty;
        var paths = refusal.Paths ?? [];
        return refusal.Kind switch
        {
            UndoRefusalKind.InProgress => "an operation is in progress",
            UndoRefusalKind.Conflicts => ResolveConflictsFirst,
            UndoRefusalKind.HeadNotOnBranch => $"HEAD is no longer on {name}",
            UndoRefusalKind.HeadNotAtCommit => $"HEAD is no longer at {Short(refusal.Sha ?? string.Empty)}",
            UndoRefusalKind.BranchMoved => refusal.Sha is { } sha ? $"{name} has moved since (it is at {Short(sha)})" : $"{name} no longer exists",
            UndoRefusalKind.RefChanged => $"{RepositoryOperations.ShortName(name)} has changed since",
            UndoRefusalKind.BranchExists => $"a branch named {name} exists again",
            UndoRefusalKind.TagExists => $"a tag named {name} exists again",
            UndoRefusalKind.ConfigChanged => $"the settings of {name} have changed since",
            UndoRefusalKind.RemoteExists => $"a remote named {name} exists again",
            UndoRefusalKind.CheckedOutElsewhere => $"{name} is checked out in {(paths.Count > 0 ? paths[0] : here)}",
            UndoRefusalKind.LocalChangesInTheWay => paths.Count == 1
                ? $"1 conflict prevents it. {CommitStashOrDiscard}"
                : string.Create(CultureInfo.InvariantCulture, $"{paths.Count} conflicts prevent it. {CommitStashOrDiscard}"),
            UndoRefusalKind.IgnoredFilesInTheWay => paths.Count == 1
                ? $"1 ignored file is in the way: {paths[0]}. Move it away first."
                : string.Create(CultureInfo.InvariantCulture, $"{paths.Count} ignored files are in the way: {FirstNames(paths)}. Move them away first."),
            UndoRefusalKind.IndexChanged => "you have staged changes since",
            UndoRefusalKind.FilesChanged => paths.Count == 1
                ? $"1 file has changed since: {paths[0]}"
                : string.Create(CultureInfo.InvariantCulture, $"{paths.Count} files have changed since: {FirstNames(paths)}"),
            UndoRefusalKind.StashGone => "the stash is no longer in the list",
            UndoRefusalKind.StashListed => "the stash is in the list again",
            UndoRefusalKind.StatusChanged => paths.Count switch
            {
                0 => "your changes have changed since the stash",
                1 => $"1 file is in the way: {paths[0]}",
                _ => string.Create(CultureInfo.InvariantCulture, $"{paths.Count} files are in the way: {FirstNames(paths)}"),
            },
            UndoRefusalKind.CommitsGone => "the commits no longer exist",
            UndoRefusalKind.RemoteChanged => $"{name} on {refusal.Remote} has changed since",
            UndoRefusalKind.FlagNotRestorable => $"{(paths.Count > 0 ? paths[0] : name)} has a flag git cannot restore",
            UndoRefusalKind.GitRefused => FirstLine(refusal.GitOutput) is { Length: > 0 } line ? line : "git refused it",
            _ => refusal.Kind.ToString(),
        };
    }

    /// <summary>
    /// The question before a step that changes a remote (7.5's table, D155, D170; UD10): which of
    /// its four forms follows from the direction and from whether the remote ref is created,
    /// deleted or moved. <paramref name="name"/> is the ref's name on the remote (a tag's with
    /// <paramref name="isTag"/>, which puts the word <c>tag</c> before it); <paramref name="from"/>
    /// and <paramref name="to"/> are the short SHAs of the commits it holds before and after, null
    /// where it does not exist. The confirm button is <c>Undo</c> or <c>Redo</c>, not destructive in
    /// style (it restores), with no detail line.
    /// </summary>
    public static ConfirmationRequest RemoteStepQuestion(UndoDirection direction, string remote, string name, bool isTag, string? from, string? to)
    {
        ArgumentNullException.ThrowIfNull(remote);
        ArgumentNullException.ThrowIfNull(name);
        var undo = direction == UndoDirection.Undo;
        var shown = isTag ? $"tag {name}" : name;
        var (title, question) = (from, to) switch
        {
            (not null, null) when undo => ($"Delete {shown} on {remote}?", $"Undoing the push deletes {shown} on {remote}. Anyone who fetched it keeps it."),
            (not null, null) => ($"Delete {shown} on {remote} again?", $"{shown} is deleted on {remote} again."),
            (null, { } back) when undo => ($"Undo the deletion of {shown} on {remote}?", $"{remote} gets {shown} back at {back}."),
            (null, { } again) => ($"Push {shown} to {remote} again?", $"{remote} gets {shown} back at {again}."),
            ({ } now, { } before) when undo => ($"Undo the push to {remote}?", $"{shown} on {remote} goes back to {before}, which it was before the push. Anyone who fetched {now} keeps it."),
            (_, { } moved) => ($"Redo the push to {remote}?", $"{shown} on {remote} moves to {moved} again."),
            _ => throw new ArgumentException("A remote ref is created, deleted or moved.", nameof(to)),
        };
        return new ConfirmationRequest(title, question, string.Empty, undo ? "Undo" : "Redo", IsDestructive: false);
    }

    // The backups list's restores (7.10, 12.1, 12.2).

    /// <summary>"Restore branch {b}" (12.1).</summary>
    public static OperationText RestoreBranch(string branch) =>
        new($"Restore branch {branch}", $"Restoring {branch}…", "Restoring the branch failed", $"Git could not restore {branch}.");

    /// <summary>"Restore tag {t}" (12.1).</summary>
    public static OperationText RestoreTag(string tag) =>
        new($"Restore tag {tag}", $"Restoring {tag}…", "Restoring the tag failed", $"Git could not restore {tag}.");

    /// <summary>"Restore stash" (12.1).</summary>
    public static OperationText RestoreStash { get; } =
        new("Restore stash", "Restoring the stash…", "Restoring the stash failed", "Git could not restore the stash.");

    /// <summary>"Restore the discarded files…" (12.1), without the "…" in its title.</summary>
    public static OperationText RestoreDiscardedFiles { get; } =
        new("Restore the discarded files", "Restoring the discarded files…", "Restoring the files failed", "Git could not restore the files.");

    /// <summary>"Restore the changes…" (12.1) of a hard reset's, an abort's or a skip's snapshot.</summary>
    public static OperationText RestoreTheChanges { get; } =
        new("Restore the changes", "Restoring the changes…", "Restoring the changes failed", "Git could not restore the changes.");

    /// <summary>"Restore the staged changes" (12.1, BD21) of a mixed reset's snapshot.</summary>
    public static OperationText RestoreStagedChanges { get; } =
        new("Restore the staged changes", "Restoring the staged changes…", "Restoring the staged changes failed", "Git could not restore the staged changes.");

    /// <summary>"Push {b} back to {r}…" and a tag's (12.1), without the "…" in its title.</summary>
    public static OperationText PushBack(string name, string remote) =>
        new($"Push {name} back to {remote}", $"Pushing {name} back to {remote}…", "Push failed", $"Git could not push {name} back to {remote}.");

    /// <summary>12.2.</summary>
    public static string RestoredBranch(string branch) => $"Restored branch {branch}";

    /// <summary>12.2.</summary>
    public static string RestoredTag(string tag) => $"Restored tag {tag}";

    /// <summary>12.2: a stash stored again comes back first in the list.</summary>
    public const string RestoredStash = "Restored the stash as stash@{0}";

    /// <summary>12.2, BD20: every path of the snapshot counts, also one whose content was already equal.</summary>
    public static string RestoredFiles(int count) =>
        count == 1 ? "Restored 1 file" : string.Create(CultureInfo.InvariantCulture, $"Restored {count} files");

    /// <summary>12.2.</summary>
    public const string RestoredStagedChanges = "Restored the staged changes";

    /// <summary>12.2.</summary>
    public static string PushedBack(string name, string remote) => $"Pushed {name} back to {remote}";

    /// <summary>"Restore the staged changes" while the index differs from HEAD (7.10, 12.4).</summary>
    public const string UnstageYourChangesFirst = "Unstage your changes first";

    /// <summary>A remote the backup names that is not configured now (7.10, 12.4).</summary>
    public static string RemoteNoLongerExists(string remote) => $"{remote} no longer exists";

    /// <summary>A force push's backup whose remote cannot be mapped back (7.10, 12.4).</summary>
    public const string RemoteOfBackupNotFound = "The remote this backup came from cannot be found";

    /// <summary>
    /// A deleted remote branch's or tag's backup, <c>&lt;r&gt;/&lt;name&gt;</c>, that more than one
    /// configured remote name starts (<c>team/fork/main</c> with <c>team</c> and <c>team/fork</c>,
    /// whose names hold slashes, 0.5): the backup does not say which remote it came from, and the
    /// longest is not always the one (deleting <c>fork/main</c> on <c>team</c> saved the same name).
    /// </summary>
    public static string RemoteOfBackupAmbiguous(string name, IReadOnlyList<string> remotes) =>
        $"The remote this backup came from cannot be told: {name} fits {List(remotes)}";

    /// <summary>A force push's backup whose lease is no longer in the remote-tracking reflog (7.10, 12.4).</summary>
    public const string ForcePushCommitNotFound = "The commit your force push left can no longer be found";

    /// <summary>
    /// The question before the backups list's push back (7.5's last row, 13.2): <c>Push {b} back
    /// to {r}?</c>, <c>{r} gets {b} back at {short}.</c>, confirmed by <c>Push</c>, not destructive;
    /// a tag's names it <c>tag {t}</c>, as the undo's questions do (UD10).
    /// </summary>
    public static ConfirmationRequest PushBackQuestion(string name, string remote, bool isTag, string shortSha)
    {
        var shown = isTag ? $"tag {name}" : name;
        return new ConfirmationRequest($"Push {shown} back to {remote}?", $"{remote} gets {shown} back at {shortSha}.", string.Empty, "Push", IsDestructive: false);
    }

    /// <summary>
    /// D75's question, as the restore bar asks it (BD15): the backups list's file restores ask it
    /// when a file would be overwritten. <paramref name="changed"/> holds the paths that count.
    /// </summary>
    public static ConfirmationRequest RestoreChangedFilesQuestion(IReadOnlyList<string> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var question = changed.Count == 1
            ? $"{WorkingChangesViewModel.FileName(changed[0])} has changed since the discard. Put back the version from before the discard?"
            : string.Create(CultureInfo.InvariantCulture, $"{changed.Count} files have changed since the discard. Put back the versions from before the discard?");
        return new ConfirmationRequest("Restore discarded changes?", question, "A snapshot of the newer changes is saved first.", "Restore");
    }

    /// <summary>The first <see cref="NamesShown"/> paths, then "…" when there are more (7.6's "{first names}").</summary>
    private static string FirstNames(IReadOnlyList<string> paths) =>
        string.Join(", ", paths.Take(NamesShown)) + (paths.Count > NamesShown ? ", …" : string.Empty);

    private static string? FirstLine(string? text) =>
        text?.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).FirstOrDefault();
}
