namespace VisualCommit.Core.Operations;

/// <summary>
/// Reads what kind of failure git reported from its English output (phase 3 design 1.6).
/// </summary>
/// <remarks>
/// A placeholder made by work package WP0 so that the queue can call it: it answers
/// <see cref="GitFailureKind.Other"/> for everything. WP2 owns this file and writes the rules of
/// the design's table, with <c>FailureClassifierTests</c>.
/// </remarks>
public static class GitFailureClassifier
{
    /// <summary>The kind of failure that <paramref name="standardError"/> and <paramref name="standardOutput"/> describe.</summary>
    public static GitFailureKind Classify(string standardError, string standardOutput, int? exitCode)
    {
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(standardOutput);
        return GitFailureKind.Other;
    }
}
