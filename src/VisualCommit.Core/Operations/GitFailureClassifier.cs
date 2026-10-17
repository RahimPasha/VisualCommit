namespace VisualCommit.Core.Operations;

/// <summary>
/// Reads what kind of failure git reported from its English output (phase 3 design 1.6, D30:
/// the runner makes git's messages English). The texts are git's own, unchanged from Git 2.30
/// to the versions CI runs; <c>FailureClassifierTests</c> hold texts recorded from real git.
/// Phase 5 adds the paths a refusal for local changes names, which the error dialog counts, and
/// the refusals of a merge, cherry-pick or revert for the same reason (docs/phase-5-design.md
/// 9.3, 0.7, D179).
/// </summary>
public static class GitFailureClassifier
{
    /// <summary>A push's line for a ref it could not update; a fetch's rejected lines look the same.</summary>
    private const string Rejected = " ! [rejected]";

    /// <summary>What <c>git push</c> prints after any ref it could not update; a fetch never does.</summary>
    private const string PushFailed = "failed to push some refs";

    /// <summary>The header of git's block of untracked files in a checkout's way.</summary>
    private const string UntrackedByCheckout = "untracked working tree files would be overwritten by checkout";

    /// <summary>The same for an untracked file where the target has a folder.</summary>
    private const string UntrackedRemovedByCheckout = "untracked working tree files would be removed by checkout";

    private const string LocalChangesByCheckout = "Your local changes to the following files would be overwritten by checkout";

    /// <summary>The header of git's block of folders whose untracked files a checkout or a merge would delete, followed by an empty line.</summary>
    private const string FolderInTheWay = "Updating the following directories would lose untracked files in them";

    /// <summary>The end of every header of a merge's refusal for changes in the way (the merge machinery of merge, cherry-pick, revert and pull).</summary>
    private const string ByMerge = " by merge:";

    private static readonly string[] AuthenticationTexts =
    [
        "Authentication failed",
        "could not read Username",
        "could not read Password",
        "Permission denied (publickey",
        "Host key verification failed",
    ];

    /// <summary>What a cherry-pick and a revert say when anything is staged: they name no file (docs/phase-5-design.md 0.7).</summary>
    private static readonly string[] StagedTexts =
    [
        "your local changes would be overwritten by cherry-pick.",
        "your local changes would be overwritten by revert.",
    ];

    /// <summary>
    /// The kind of failure that <paramref name="standardError"/> and
    /// <paramref name="standardOutput"/> describe (both are read: a merge writes some messages to
    /// standard output). The raw text is read, before <see cref="GitOutputCleaner"/>, because a
    /// push's rejected line starts with a space. Rules, the first that applies:
    /// <list type="bullet">
    /// <item><see cref="GitFailureKind.CheckoutBlockedByUntrackedFiles"/>: <c>The following untracked working tree files would be overwritten by checkout</c> (or <c>would be removed by checkout</c>), alone or with the other blocks.</item>
    /// <item><see cref="GitFailureKind.CheckoutBlockedByUntrackedFolder"/>: <c>Updating the following directories would lose untracked files in them</c>, alone or with the tracked files' block, and no block of a merge.</item>
    /// <item><see cref="GitFailureKind.CheckoutBlockedByLocalChanges"/>: <c>Your local changes to the following files would be overwritten by checkout</c>.</item>
    /// <item><see cref="GitFailureKind.MergeBlockedByLocalChanges"/>: a block whose header ends <c>by merge:</c> with paths on tab-indented lines under it (<see cref="BlockedPaths"/>).</item>
    /// <item><see cref="GitFailureKind.MergeBlockedByStagedChanges"/>: such a header with no tab-indented line under it (ort names the staged files after two spaces, Git 2.36 without <c>error:</c>, 2.55 with it: the report's PD10), or <c>your local changes would be overwritten by cherry-pick.</c> (<c>by revert.</c>).</item>
    /// <item><see cref="GitFailureKind.PushRejectedStale"/>: a push's <c> ! [rejected]</c> line with <c>(stale info)</c> or <c>(remote ref updated since checkout)</c>.</item>
    /// <item><see cref="GitFailureKind.PushRejected"/>: a push's <c> ! [rejected]</c> line with <c>(fetch first)</c> or <c>(non-fast-forward)</c>.</item>
    /// <item><see cref="GitFailureKind.AuthenticationFailed"/>: <c>Authentication failed</c>, <c>could not read Username</c>, <c>could not read Password</c>, <c>Permission denied (publickey</c> or <c>Host key verification failed</c>.</item>
    /// <item><see cref="GitFailureKind.NotFastForward"/>: <c>Not possible to fast-forward</c>.</item>
    /// <item><see cref="GitFailureKind.IndexLocked"/>: <c>index.lock': File exists</c>.</item>
    /// <item><see cref="GitFailureKind.Other"/>: anything else.</item>
    /// </list>
    /// A rejected line counts as a push's only when git also printed <c>failed to push some refs</c>:
    /// a fetch into a local branch (<c>git fetch . a:b</c>, 2.7) prints the same
    /// <c>! [rejected] … (non-fast-forward)</c> line, and is not a rejected push. A folder block
    /// alone reads the same from a checkout and from a merge on Git 2.36, so a merge's dialog
    /// counts <see cref="GitFailure.BlockedPaths"/> whatever the kind. <paramref name="exitCode"/>
    /// is not needed by any rule (a merge refused for local changes exits 128 on Git 2.36 and 2 on
    /// 2.55); it is part of the signature for rules that may need it.
    /// </summary>
    public static GitFailureKind Classify(string standardError, string standardOutput, int? exitCode)
    {
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(standardOutput);
        var text = standardError + "\n" + standardOutput;

        if (Has(text, UntrackedByCheckout) || Has(text, UntrackedRemovedByCheckout))
        {
            return GitFailureKind.CheckoutBlockedByUntrackedFiles;
        }

        var byMerge = Lines(text).Any(IsMergeHeader);
        if (Has(text, FolderInTheWay) && !byMerge)
        {
            return GitFailureKind.CheckoutBlockedByUntrackedFolder;
        }

        if (Has(text, LocalChangesByCheckout))
        {
            return GitFailureKind.CheckoutBlockedByLocalChanges;
        }

        if (byMerge)
        {
            return BlockedPaths(text).Count > 0 ? GitFailureKind.MergeBlockedByLocalChanges : GitFailureKind.MergeBlockedByStagedChanges;
        }

        if (StagedTexts.Any(staged => Has(text, staged)))
        {
            return GitFailureKind.MergeBlockedByStagedChanges;
        }

        if (Has(text, PushFailed))
        {
            var rejected = RejectedLines(text);
            if (rejected.Any(line => Has(line, "(stale info)") || Has(line, "(remote ref updated since checkout)")))
            {
                return GitFailureKind.PushRejectedStale;
            }

            if (rejected.Any(line => Has(line, "(fetch first)") || Has(line, "(non-fast-forward)")))
            {
                return GitFailureKind.PushRejected;
            }
        }

        if (AuthenticationTexts.Any(authentication => Has(text, authentication)))
        {
            return GitFailureKind.AuthenticationFailed;
        }

        if (Has(text, "Not possible to fast-forward"))
        {
            return GitFailureKind.NotFastForward;
        }

        if (Has(text, "index.lock': File exists"))
        {
            return GitFailureKind.IndexLocked;
        }

        return GitFailureKind.Other;
    }

    /// <summary>
    /// The paths git names in <paramref name="text"/> (standard error, standard output, both
    /// joined, or the error dialog's cleaned output) under the headers of a refusal for changes in
    /// the way (docs/phase-5-design.md 0.7, 9.3): every tab-indented line under a line that ends
    /// with a colon and says <c>would be overwritten by</c>, <c>would be removed by</c> or
    /// <c>Updating the following directories would lose untracked files in them</c> (a checkout's,
    /// a merge's, a cherry-pick's or a revert's, with or without <c>error:</c>), up to the first
    /// line that is not one; without the tab, each path once, in git's order. Git lists every
    /// path, raw. Their count is D153's "{N} conflicts"; ort's staged form, whose names follow two
    /// spaces, names none (the report's PD10). Empty when there is no such block.
    /// </summary>
    public static IReadOnlyList<string> BlockedPaths(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<string>? paths = null;
        var inBlock = false;
        foreach (var line in Lines(text))
        {
            if (IsBlockHeader(line))
            {
                inBlock = true;
            }
            else if (inBlock && line.Length > 1 && line[0] == '\t')
            {
                paths ??= [];
                var path = line[1..];
                if (!paths.Contains(path, StringComparer.Ordinal))
                {
                    paths.Add(path);
                }
            }
            else
            {
                inBlock = false;
            }
        }

        // No block: the one empty list, so that failures without one compare equal as records.
        if (paths is null)
        {
            return [];
        }

        return paths;
    }

    private static bool IsBlockHeader(string line) =>
        line.EndsWith(':')
        && (Has(line, "would be overwritten by ") || Has(line, "would be removed by ") || Has(line, FolderInTheWay));

    private static bool IsMergeHeader(string line) => IsBlockHeader(line) && line.EndsWith(ByMerge, StringComparison.Ordinal);

    private static bool Has(string text, string value) => text.Contains(value, StringComparison.Ordinal);

    /// <summary>The lines of <paramref name="text"/>, split at line feeds, each without the carriage return at its end.</summary>
    private static IEnumerable<string> Lines(string text) => text.Split('\n').Select(line => line.TrimEnd('\r'));

    /// <summary>The lines that start with git's <c> ! [rejected]</c>, split at carriage returns and line feeds.</summary>
    private static List<string> RejectedLines(string text) =>
        [.. text.Split(['\r', '\n']).Where(line => line.StartsWith(Rejected, StringComparison.Ordinal))];
}
