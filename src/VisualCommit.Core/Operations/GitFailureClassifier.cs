namespace VisualCommit.Core.Operations;

/// <summary>
/// Reads what kind of failure git reported from its English output (phase 3 design 1.6, D30:
/// the runner makes git's messages English). The texts are git's own, unchanged from Git 2.30
/// to the versions CI runs; <c>FailureClassifierTests</c> hold texts recorded from real git.
/// </summary>
public static class GitFailureClassifier
{
    /// <summary>A push's line for a ref it could not update; a fetch's rejected lines look the same.</summary>
    private const string Rejected = " ! [rejected]";

    /// <summary>What <c>git push</c> prints after any ref it could not update; a fetch never does.</summary>
    private const string PushFailed = "failed to push some refs";

    private static readonly string[] AuthenticationTexts =
    [
        "Authentication failed",
        "could not read Username",
        "could not read Password",
        "Permission denied (publickey",
        "Host key verification failed",
    ];

    /// <summary>
    /// The kind of failure that <paramref name="standardError"/> and
    /// <paramref name="standardOutput"/> describe (both are read: a merge writes some messages to
    /// standard output). The raw text is read, before <see cref="GitOutputCleaner"/>, because a
    /// push's rejected line starts with a space. Rules, the first that applies:
    /// <list type="bullet">
    /// <item><see cref="GitFailureKind.CheckoutBlockedByUntrackedFiles"/>: <c>The following untracked working tree files would be overwritten by checkout</c>, alone or with the tracked files' message.</item>
    /// <item><see cref="GitFailureKind.CheckoutBlockedByLocalChanges"/>: <c>Your local changes to the following files would be overwritten by checkout</c>.</item>
    /// <item><see cref="GitFailureKind.PushRejectedStale"/>: a push's <c> ! [rejected]</c> line with <c>(stale info)</c> or <c>(remote ref updated since checkout)</c>.</item>
    /// <item><see cref="GitFailureKind.PushRejected"/>: a push's <c> ! [rejected]</c> line with <c>(fetch first)</c> or <c>(non-fast-forward)</c>.</item>
    /// <item><see cref="GitFailureKind.AuthenticationFailed"/>: <c>Authentication failed</c>, <c>could not read Username</c>, <c>could not read Password</c>, <c>Permission denied (publickey</c> or <c>Host key verification failed</c>.</item>
    /// <item><see cref="GitFailureKind.NotFastForward"/>: <c>Not possible to fast-forward</c>.</item>
    /// <item><see cref="GitFailureKind.IndexLocked"/>: <c>index.lock': File exists</c>.</item>
    /// <item><see cref="GitFailureKind.Other"/>: anything else.</item>
    /// </list>
    /// A rejected line counts as a push's only when git also printed <c>failed to push some refs</c>:
    /// a fetch into a local branch (<c>git fetch . a:b</c>, 2.7) prints the same
    /// <c>! [rejected] … (non-fast-forward)</c> line, and is not a rejected push.
    /// <paramref name="exitCode"/> is not needed by any rule; it is part of the signature for rules
    /// that may need it.
    /// </summary>
    public static GitFailureKind Classify(string standardError, string standardOutput, int? exitCode)
    {
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(standardOutput);
        var text = standardError + "\n" + standardOutput;

        if (Has(text, "The following untracked working tree files would be overwritten by checkout"))
        {
            return GitFailureKind.CheckoutBlockedByUntrackedFiles;
        }

        if (Has(text, "Your local changes to the following files would be overwritten by checkout"))
        {
            return GitFailureKind.CheckoutBlockedByLocalChanges;
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

    private static bool Has(string text, string value) => text.Contains(value, StringComparison.Ordinal);

    /// <summary>The lines that start with git's <c> ! [rejected]</c>, split at carriage returns and line feeds.</summary>
    private static List<string> RejectedLines(string text) =>
        [.. text.Split(['\r', '\n']).Where(line => line.StartsWith(Rejected, StringComparison.Ordinal))];
}
