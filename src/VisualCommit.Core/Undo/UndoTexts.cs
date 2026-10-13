using System.Globalization;
using VisualCommit.Core.Git;

namespace VisualCommit.Core.Undo;

/// <summary>
/// An entry's text, which names the action as the user knows it (docs/phase-5-design.md 7.6): the
/// toolbar shows <c>Undo {text}</c> and <c>Redo {text}</c>, the step's operation is titled so, and
/// its result reads <c>Undid {text}</c>. Names are short names; a commit is its short SHA; a stash
/// is its message, never its <c>stash@{n}</c>, which renumbering reuses. Counts use the singular
/// for one (D113). The check failures' texts are the app's (<c>OperationTexts</c>).
/// </summary>
public static class UndoTexts
{
    public static string Commit(string subject) => $"commit \"{subject}\"";

    public static string Amend(string subject) => $"amend \"{subject}\"";

    /// <summary>A checkout of every kind: <c>checkout of feature/alpha</c>, <c>checkout of a1b2c3d</c>.</summary>
    public static string Checkout(string name) => $"checkout of {name}";

    public static string CreateBranch(string branch) => $"creation of {branch}";

    public static string RenameBranch(string branch, string newName) => $"rename of {branch} to {newName}";

    public static string DeleteBranch(string branch) => $"deletion of {branch}";

    public static string FastForward(string branch) => $"fast-forward of {branch}";

    /// <summary>A merge and "Check out and merge": <c>merge of feature/alpha into main</c>.</summary>
    public static string Merge(string source, string branch) => $"merge of {source} into {branch}";

    /// <summary><c>reset of main to a1b2c3d (hard)</c>; <paramref name="target"/> is a full SHA or a name.</summary>
    public static string Reset(string branch, string target, ResetMode mode) =>
        $"reset of {branch} to {ShortOf(target)} ({mode.ToString().ToLowerInvariant()})";

    /// <summary><c>cherry-pick of a1b2c3d</c>, <c>cherry-pick of 3 commits</c>.</summary>
    public static string CherryPick(IReadOnlyList<string> commits)
    {
        ArgumentNullException.ThrowIfNull(commits);
        return commits.Count == 1 ? $"cherry-pick of {ShortOf(commits[0])}" : $"cherry-pick of {Commits(commits.Count)}";
    }

    /// <summary><c>revert of a1b2c3d</c>, <c>revert of 3 commits</c>.</summary>
    public static string Revert(IReadOnlyList<string> commits)
    {
        ArgumentNullException.ThrowIfNull(commits);
        return commits.Count == 1 ? $"revert of {ShortOf(commits[0])}" : $"revert of {Commits(commits.Count)}";
    }

    /// <summary>A rebase onto and "Check out and rebase": <c>rebase of feature/topic onto main</c>.</summary>
    public static string Rebase(string branch, string target) => $"rebase of {branch} onto {target}";

    public static string InteractiveRebase(string branch) => $"interactive rebase of {branch}";

    public static string Squash(int count) => $"squash of {Commits(count)}";

    public static string Pull(string branch) => $"pull into {branch}";

    public static string Stash(string message) => $"stash \"{message}\"";

    public static string ApplyStash(string message) => $"apply of stash \"{message}\"";

    public static string PopStash(string message) => $"pop of stash \"{message}\"";

    public static string DropStash(string message) => $"drop of stash \"{message}\"";

    public static string CreateTag(string tag) => $"creation of tag {tag}";

    public static string DeleteTag(string tag) => $"deletion of tag {tag}";

    /// <summary><c>discard of notes.txt</c>, <c>discard of 3 files</c>: a file by its name, without its folder.</summary>
    public static string Discard(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return paths.Count == 1
            ? $"discard of {FileName(paths[0])}"
            : string.Create(CultureInfo.InvariantCulture, $"discard of {paths.Count} files");
    }

    /// <summary>A discard of a hunk or of lines: <c>discard of lines in notes.txt</c>.</summary>
    public static string DiscardLines(string path) => $"discard of lines in {FileName(path)}";

    /// <summary>The restore bar's Restore, and the backups list's "Restore the discarded files…".</summary>
    public const string Restore = "restore of the discarded files";

    // The backups list's other restores (7.10).

    public static string RestoreBranch(string branch) => $"restore of branch {branch}";

    public static string RestoreTag(string tag) => $"restore of tag {tag}";

    public static string RestoreStash(string message) => $"restore of stash \"{message}\"";

    /// <summary>"Restore the changes…" of a hard reset's, an abort's or a skip's snapshot.</summary>
    public const string RestoreChanges = "restore of the changes";

    public const string RestoreStagedChanges = "restore of the staged changes";

    public static string PushBack(string branch, string remote) => $"push of {branch} back to {remote}";

    public static string PushTagBack(string tag, string remote) => $"push of tag {tag} back to {remote}";

    public static string Ignore(string pattern) => $"ignore of {pattern}";

    public static string Upstream(string branch) => $"upstream change of {branch}";

    public static string AddRemote(string remote) => $"addition of remote {remote}";

    public static string RenameRemote(string remote, string newName) => $"rename of remote {remote} to {newName}";

    public static string RemoveRemote(string remote) => $"removal of remote {remote}";

    public static string SetRemoteUrl(string remote) => $"URL change of {remote}";

    public static string Push(string branch, string remote) => $"push of {branch} to {remote}";

    public static string ForcePush(string branch, string remote) => $"force push of {branch} to {remote}";

    public static string DeleteRemoteBranch(string branch, string remote) => $"deletion of {branch} on {remote}";

    public static string PushTag(string tag, string remote) => $"push of tag {tag} to {remote}";

    public static string DeleteRemoteTag(string tag, string remote) => $"deletion of tag {tag} on {remote}";

    /// <summary>A file's name without its folder; a folder's without its slash.</summary>
    public static string FileName(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var trimmed = path.TrimEnd('/');
        var slash = trimmed.LastIndexOf('/');
        return slash < 0 ? trimmed : trimmed[(slash + 1)..];
    }

    private static string Commits(int count) =>
        count == 1 ? "1 commit" : string.Create(CultureInfo.InvariantCulture, $"{count} commits");

    /// <summary>A full commit id's first 7 characters; anything else as it is.</summary>
    private static string ShortOf(string target) =>
        target.Length is 40 or 64 && target.All(char.IsAsciiHexDigitLower) ? target[..7] : target;
}
