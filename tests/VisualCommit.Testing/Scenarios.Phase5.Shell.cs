namespace VisualCommit.Testing;

/// <summary>
/// Phase 5's part A additions to the scenarios (docs/test-reports/phase-5.md, part A, "The
/// waiting merge tool", and its decisions AD7 and AD12; package WP5): a merge tool that waits
/// until it is stopped or released, so that a check can see the merge tool running in the status
/// bar and the activity log, cancel it, and then let it go.
/// </summary>
public static partial class Scenarios
{
    /// <summary>The name of the waiting merge tool (<c>merge.tool</c>).</summary>
    public const string WaitingMergeTool = "slow";

    /// <summary>
    /// The waiting tool's command (<c>mergetool.slow.cmd</c>): it marks that it started, then
    /// waits until it is released, or 600 rounds of <c>sleep 0.1</c> have passed (each <c>sleep</c>
    /// a process of its own on Windows: 111 s measured in Git 2.36.0's <c>sh</c>), and then fails.
    /// Git runs it in the working tree's top folder, so its two files lie beside the working tree.
    /// </summary>
    public const string WaitingMergeToolCommand =
        "touch ../tool-started; i=0; while [ ! -f ../tool-release ] && [ $i -lt 600 ]; do sleep 0.1; i=$((i+1)); done; exit 1";

    /// <summary>
    /// Configures the waiting merge tool in the copy's own configuration (A2's second method):
    /// <c>merge.tool=slow</c>, <c>mergetool.slow.trustExitCode=true</c> and its command,
    /// <see cref="WaitingMergeToolCommand"/>. The tool writes nothing to the file it is given.
    /// </summary>
    public static async Task ConfigureWaitingMergeToolAsync(TempRepo repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        await repo.ConfigAsync("merge.tool", WaitingMergeTool).ConfigureAwait(false);
        await repo.ConfigAsync($"mergetool.{WaitingMergeTool}.trustExitCode", "true").ConfigureAwait(false);
        await repo.ConfigAsync($"mergetool.{WaitingMergeTool}.cmd", WaitingMergeToolCommand).ConfigureAwait(false);
    }

    /// <summary>Whether the waiting merge tool has started: its <c>tool-started</c> file is beside the working tree.</summary>
    public static bool WaitingMergeToolStarted(TempRepo repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        return File.Exists(WaitingMergeToolFile(repo, "tool-started"));
    }

    /// <summary>
    /// Lets the waiting merge tool go (its <c>tool-release</c> file beside the working tree): it
    /// fails at once if it still runs, and one started later fails at once. A check calls it after
    /// its cancel and in a <c>finally</c>, so that where only git is stopped the tool's shell ends.
    /// </summary>
    public static void ReleaseWaitingMergeTool(TempRepo repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        File.WriteAllText(WaitingMergeToolFile(repo, "tool-release"), string.Empty);
    }

    private static string WaitingMergeToolFile(TempRepo repo, string name) =>
        Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(repo.Path))!, name);
}
