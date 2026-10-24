using System.Globalization;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The labels and disabled reasons of phase 5's file history, blame, reflog and backups items and
/// of their pages' menus (docs/phase-5-design.md 4.4, 5.2, 5.3, 7.10, 12.3, 12.4, 13.1), which
/// <c>RepositoryMenus.Pages.cs</c> builds its items from, and the backups in words that the
/// backups list and its menu's header show (12.3).
/// </summary>
public static partial class OperationTexts
{
    public static partial class MenuTexts
    {
        // The file menus (4.4).

        public const string FileHistory = "File history";

        public const string Blame = "Blame";

        /// <summary>12.4: an untracked file, a staged new file.</summary>
        public const string InNoCommitYet = "This file is in no commit yet";

        /// <summary>A deleted file's blame of HEAD's version (4.4): the page's caption.</summary>
        public const string AtHead = "at HEAD";

        /// <summary>A file a commit deleted: its blame at the first parent (4.4), the page's caption <c>before a1b2c3d</c>.</summary>
        public static string Before(string sha) => $"before {Short(sha)}";

        // The reflog and the backups (5.1, 5.2).

        public const string ShowReflog = "Show reflog";

        public const string ShowBackups = "Show backups";

        public static string ShowReflogOf(string branch) => $"Show reflog of {branch}";

        /// <summary>The reflog page's source menu (5.2, BD7): its header.</summary>
        public const string ReflogSourceHeader = "Reflog and backups";

        /// <summary>The source menu's HEAD and the page's source label.</summary>
        public const string HeadSource = "HEAD";

        /// <summary>The source menu's backups and the page's source label.</summary>
        public const string BackupsSource = "Backups";

        // The pages' rows (4.1, 4.2, 5.2).

        public const string ShowInGraph = "Show in graph";

        /// <summary>12.4: a commit no shown ref reaches (4.3).</summary>
        public const string NotInTheGraph = "This commit is not in the graph";

        public const string BlameAtThisCommit = "Blame at this commit";

        public const string BlameBeforeThisCommit = "Blame before this commit";

        /// <summary>12.4: a blame run without <c>previous</c>.</summary>
        public const string FilesFirstCommit = "This is the file's first commit";

        /// <summary>A blame run of lines not committed yet: its menu's header and the gutter's text (4.2).</summary>
        public const string NotCommittedYet = "Not committed yet";

        // The backups list's restores (7.10, 12.4, BD21).

        public static string RestoreBranch(string branch) => $"Restore branch {branch}";

        public static string BranchExists(string branch) => $"A branch named {branch} exists";

        public static string RestoreTag(string tag) => $"Restore tag {tag}";

        public static string TagExists(string tag) => $"A tag named {tag} exists";

        public const string RestoreStash = "Restore stash";

        public const string RestoreTheDiscardedFiles = "Restore the discarded files…";

        public const string RestoreTheChanges = "Restore the changes…";

        /// <summary>BD21 (D183): no "…", since no question follows it.</summary>
        public const string RestoreTheStagedChanges = "Restore the staged changes";

        /// <summary>12.4: "Restore the staged changes" while the index differs from HEAD.</summary>
        public const string UnstageYourChangesFirst = "Unstage your changes first";

        /// <summary>"Push {b} back to {r}…", "Push {t} back to {r}…" (7.10): it asks first (7.5).</summary>
        public static string PushBackTo(string name, string remote) => $"Push {name} back to {remote}…";

        /// <summary>12.4: a force push's backup whose remote cannot be mapped back.</summary>
        public const string RemoteOfBackupNotFound = "The remote this backup came from cannot be found";

        /// <summary>A deleted remote branch's or tag's backup that more than one configured remote name starts (<see cref="OperationTexts.RemoteOfBackupAmbiguous"/>).</summary>
        public static string RemoteOfBackupAmbiguous(string name, IReadOnlyList<string> remotes) => OperationTexts.RemoteOfBackupAmbiguous(name, remotes);

        /// <summary>12.4: a force push's backup whose lease cannot be read from the remote-tracking reflog.</summary>
        public const string ForcePushCommitNotFound = "The commit your force push left can no longer be found";

        // Backups in words (12.3).

        /// <summary>
        /// A backup in words (12.3): the backups list's "What" and its menu's header. A remote
        /// and its branch or tag are split by the longest of <paramref name="remotes"/> that the
        /// ref's name starts with, as the deletion named them; a remote no longer configured is
        /// the name's first part.
        /// </summary>
        public static string BackupWords(BackupInfo backup, IReadOnlyList<string> remotes)
        {
            ArgumentNullException.ThrowIfNull(backup);
            ArgumentNullException.ThrowIfNull(remotes);
            var main = backup.Main;
            var name = main?.Name ?? string.Empty;
            switch (backup.Kind)
            {
                case "discard":
                    var paths = PathsOf(main?.Body);
                    return paths.Count == 1 ? "Before discarding 1 file" : string.Create(CultureInfo.InvariantCulture, $"Before discarding {paths.Count} files");
                case "reset":
                    return backup.Refs.FirstOrDefault(entry => entry.IsChanges) is not { } changes ? "Before a reset"
                        : IsMixedResetSnapshot(changes) ? "Before a mixed reset"
                        : changes.Subject.LastIndexOf(HardResetPrefix, StringComparison.Ordinal) is >= 0 and var at
                            ? $"Before a hard reset to {changes.Subject[(at + HardResetPrefix.Length)..].Trim()}"
                            : "Before a reset";
                case "detached":
                    return "Commits a detached HEAD left behind";
                case "branch":
                    return $"Deleted branch {name}";
                case "remote-branch":
                    var (remote, branch, _) = SplitRemote(name, remotes);
                    return $"Deleted {branch} on {remote}";
                case "remote":
                    return $"Removed remote {SplitRemote(name, remotes).Remote}";
                case "force-push":
                    return $"Before a force push of {name}";
                case "tag":
                    return $"Deleted tag {name}";
                case "tag-undone":
                    return $"Tag {name}, its creation undone";
                case "remote-tag":
                    var (tagRemote, tag, _) = SplitRemote(name, remotes);
                    return $"Deleted tag {tag} on {tagRemote}";
                case "stash":
                    return $"Dropped or popped stash \"{main?.Subject}\"";
                case "abort":
                    return "Before aborting";
                case "rebase":
                    return $"Before rebasing {name}";
                case "skip":
                    return "Before skipping a commit";
                default:
                    return backup.Kind;
            }
        }

        /// <summary>
        /// Splits <c>&lt;r&gt;/&lt;name&gt;</c> by the longest of <paramref name="remotes"/> it starts
        /// with (remote names may hold <c>/</c>, 0.5); without one, at the first <c>/</c>, and
        /// <c>IsConfigured</c> is false.
        /// </summary>
        public static (string Remote, string Name, bool IsConfigured) SplitRemote(string name, IReadOnlyList<string> remotes)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(remotes);
            var configured = RemoteBranchNames.LongestRemoteOf(name, remotes) is not null;
            var remote = RemoteBranchNames.RemoteOf(name, remotes);
            return remote.Length < name.Length ? (remote, name[(remote.Length + 1)..], configured) : (name, string.Empty, false);
        }

        /// <summary>A <c>-changes</c> snapshot of a mixed reset: the index alone, its paths not in its message (7.10).</summary>
        public static bool IsMixedResetSnapshot(BackupRefInfo changes)
        {
            ArgumentNullException.ThrowIfNull(changes);
            return changes.Subject.StartsWith(MixedResetSubject, StringComparison.Ordinal);
        }

        /// <summary>The paths a snapshot's message lists, one per line after its subject.</summary>
        public static IReadOnlyList<string> PathsOf(string? body) =>
            string.IsNullOrEmpty(body) ? [] : [.. body.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];

        /// <summary>A mixed reset's snapshot's subject (phase 4's backups).</summary>
        private const string MixedResetSubject = "VisualCommit: index before a mixed reset";

        /// <summary>What a hard reset's snapshot's subject says before the commit it reset to.</summary>
        private const string HardResetPrefix = "hard reset to ";
    }
}
