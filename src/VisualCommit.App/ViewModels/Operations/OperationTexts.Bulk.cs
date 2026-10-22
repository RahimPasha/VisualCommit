using System.Globalization;
using System.Text;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Workspaces;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The texts of the dashboard's bulk runs, Fetch all and Pull all (docs/phase-6-design.md 4.2,
/// 4.5, 4.9, 4.10, 14.2, 14.3, 14.5, 14.14): the running texts, the results, the rows' states, the
/// dashboard's toolbar and the log lines. Counts take the singular for one (D113).
/// </summary>
public static partial class OperationTexts
{
    /// <summary>A row in line or still being read for Fetch all (4.2).</summary>
    public const string WaitingToFetch = "Waiting to fetch…";

    /// <summary>A row in line or still being read for Pull all (4.2).</summary>
    public const string WaitingToPull = "Waiting to pull…";

    /// <summary>The run's name as the log and the reasons say it: "Fetch all", "Pull all".</summary>
    public static string BulkName(BulkKind kind) => kind == BulkKind.Fetch ? DashboardTexts.FetchAll : DashboardTexts.PullAll;

    /// <summary>A row in line or still being read (4.2): <see cref="WaitingToFetch"/> or <see cref="WaitingToPull"/>.</summary>
    public static string WaitingFor(BulkKind kind) => kind == BulkKind.Fetch ? WaitingToFetch : WaitingToPull;

    /// <summary>
    /// The reason of the dashboard's Fetch and Pull, and of a row's Fetch, Pull and Remove, while a
    /// run runs (4.7, 4.10): "Wait for Fetch all to finish" / "Wait for Pull all to finish".
    /// </summary>
    public static string WaitForBulkRun(BulkKind kind) => $"Wait for {BulkName(kind)} to finish";

    /// <summary>A row a run stopped before its turn (4.5): "Fetch cancelled" / "Pull cancelled".</summary>
    public static string BulkRowCancelled(BulkKind kind) => Cancelled(kind == BulkKind.Fetch ? FetchAll.Title : "Pull");

    /// <summary>Cancel all's tooltip (2.5, 4.5): "Cancel the rest of Fetch all" / "Cancel the rest of Pull all".</summary>
    public static string CancelTheRestOf(BulkKind kind) => $"Cancel the rest of {BulkName(kind)}";

    /// <summary>
    /// The run's text while it runs (4.9): "Fetching 10 repositories…", "Pulling 1 repository…";
    /// once rows ended "Fetching 10 repositories… 4 done".
    /// </summary>
    public static string BulkRunning(BulkKind kind, int repositories, int done)
    {
        var running = $"{(kind == BulkKind.Fetch ? "Fetching" : "Pulling")} {Repositories(repositories)}…";
        return done > 0 ? string.Create(CultureInfo.InvariantCulture, $"{running} {done} done") : running;
    }

    /// <summary>The run's text after Cancel all, until it ends (4.5, 4.9): "Cancelling: waiting for 2 repositories…".</summary>
    public static string BulkCancelling(int running) => $"Cancelling: waiting for {Repositories(running)}…";

    /// <summary>
    /// The run's result (4.9): "Fetched 9 repositories; 1 failed", "Pulled 6 repositories; 1 failed;
    /// 2 skipped", "Fetched no repositories; 8 cancelled; 2 skipped": the rows that succeeded, then
    /// each part only when not 0, failures first.
    /// </summary>
    public static string BulkResult(BulkKind kind, int succeeded, int failed, int stopped, int cancelled, int skipped)
    {
        var text = new StringBuilder(kind == BulkKind.Fetch ? "Fetched " : "Pulled ");
        text.Append(succeeded == 0 ? "no repositories" : Repositories(succeeded));
        Part(failed, "failed");
        Part(stopped, "stopped");
        Part(cancelled, "cancelled");
        Part(skipped, "skipped");
        return text.ToString();

        void Part(int count, string what)
        {
            if (count > 0)
            {
                text.Append(CultureInfo.InvariantCulture, $"; {count} {what}");
            }
        }
    }

    /// <summary>The log line at a run's start (4.3, 14.14): "Web: Fetch all: started for 8 repositories, 2 skipped".</summary>
    public static string BulkStartedLine(string workspace, BulkKind kind, int repositories, int skipped) =>
        string.Create(CultureInfo.InvariantCulture, $"{workspace}: {BulkName(kind)}: started for {Repositories(repositories)}, {skipped} skipped");

    /// <summary>The log line at a run's end (4.3, 14.14): "Web: Fetch all: Fetched 8 repositories; 2 skipped".</summary>
    public static string BulkEndedLine(string workspace, BulkKind kind, string result) => $"{workspace}: {BulkName(kind)}: {result}";

    /// <summary>The dashboard's enabled Fetch (4.10): "Fetch every repository of Web (Ctrl+Shift+F)", with its key (6.8).</summary>
    public static string FetchEveryRepository(string workspace) =>
        KeyTexts.Current.WithKey($"Fetch every repository of {workspace}", AppCommand.Fetch);

    /// <summary>The dashboard's enabled Pull (4.10): "Pull every repository of Web (Ctrl+Shift+L)", with its key (6.8).</summary>
    public static string PullEveryRepository(string workspace) =>
        KeyTexts.Current.WithKey($"Pull every repository of {workspace}", AppCommand.Pull);

    /// <summary>The dashboard's Fetch when no row would be fetched and none is being read (4.10).</summary>
    public static string NoRepositoryHasRemote(string workspace) => $"No repository of {workspace} has a remote";

    /// <summary>The dashboard's Pull when no row would be pulled and none is being read (4.10).</summary>
    public static string NoRepositoryCanBePulled(string workspace) => $"No repository of {workspace} can be pulled";

    /// <summary>"1 repository", "10 repositories" (D113).</summary>
    private static string Repositories(int count) =>
        count == 1 ? "1 repository" : string.Create(CultureInfo.InvariantCulture, $"{count} repositories");
}
