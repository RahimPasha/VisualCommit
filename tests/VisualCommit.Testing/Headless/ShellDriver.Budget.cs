using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// S8's mark and read of the git calls with the watchers quiet (docs/phase-7a-design.md 5.3,
/// <see cref="GitCallBudget"/>): a check counts git calls only through these, so that a refresh
/// the step before started (a write the system reports late, as macOS does) never straddles a mark.
/// Tabs only: a dashboard's rows have no watcher (D207).
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>
    /// How many refreshes of the refs the open tabs' watchers have asked for so far
    /// (<see cref="RepositoryViewModel.WatcherRefreshes"/>, summed).
    /// </summary>
    public int WatcherRefreshes => Session.Shell.Tabs.Select(tab => tab.Repository).OfType<RepositoryViewModel>().Sum(repository => repository.WatcherRefreshes);

    /// <summary>
    /// The mark of a step's git calls (<see cref="CallMark"/>), taken once every watcher is quiet
    /// (<see cref="WaitForWatchersQuietAsync"/>, at the default wait).
    /// </summary>
    /// <exception cref="TimeoutException">A watcher was still busy when the time was up.</exception>
    public async Task<long> QuietCallMarkAsync()
    {
        await WaitForWatchersQuietAsync();
        return CallMark();
    }

    /// <summary>
    /// The git calls since <paramref name="sequence"/> (<see cref="CallsSince"/>, in the folders
    /// <paramref name="inFolder"/> accepts when given), read once every watcher is quiet again.
    /// </summary>
    /// <exception cref="TimeoutException">A watcher was still busy when the time was up.</exception>
    public async Task<IReadOnlyList<GitCallRecord>> QuietCallsSinceAsync(long sequence, Func<string, bool>? inFolder = null)
    {
        await WaitForWatchersQuietAsync();
        return CallsSince(sequence, inFolder);
    }

    /// <summary>
    /// The mark of a step that starts no git call (<see cref="AssertNoStepCallsAsync"/>), taken once
    /// every watcher is quiet, as <see cref="QuietCallMarkAsync"/> takes its own: the calls, the
    /// watchers' refreshes and the operations so far.
    /// </summary>
    /// <exception cref="TimeoutException">A watcher was still busy when the time was up.</exception>
    public async Task<QuietStepMark> QuietStepMarkAsync()
    {
        await WaitForWatchersQuietAsync();
        return new(CallMark(), WatcherRefreshes, Session.Operations.Snapshot().Count);
    }

    /// <summary>
    /// Asserts that the step since <paramref name="mark"/> started no git call, read once every
    /// watcher is quiet again (the caller has waited for the queues to be idle,
    /// <see cref="WaitForIdleAsync"/>): no operation ended in the session's log, and every call
    /// since the mark is a whole refresh the watchers asked for (<see cref="GitCallBudget.AssertOnlyWatcherRefreshes"/>),
    /// the refresh of a write the system reported late (macOS; R8's lag, D262). A refresh the step
    /// asked for itself, or an operation's, which runs outside the operation, fails.
    /// </summary>
    /// <exception cref="TimeoutException">A watcher was still busy when the time was up.</exception>
    /// <exception cref="GitCallBudgetException">An operation ended, or a call is no refresh the watchers asked for.</exception>
    public async Task AssertNoStepCallsAsync(QuietStepMark mark, string what)
    {
        var calls = await QuietCallsSinceAsync(mark.Calls);

        // Read with the calls, in the same turn of the UI thread, where the watchers' changes ask
        // for their refreshes: every refresh among the calls was asked for before this count.
        var watcherRefreshes = WatcherRefreshes - mark.WatcherRefreshes;
        var operations = Session.Operations.Snapshot();
        if (operations.Count != mark.Operations)
        {
            throw new GitCallBudgetException(
                $"{what} ran an operation: " + string.Join(", ", operations.Skip(mark.Operations).Select(record => $"{record.Title} ({record.Outcome})")) + ". The calls:\n  "
                + string.Join("\n  ", calls.Select(call => $"{call.CommandText}{(call.OperationId is null ? string.Empty : $" (in {call.OperationId})")} → {call.ExitCode}")));
        }

        GitCallBudget.AssertOnlyWatcherRefreshes(calls, watcherRefreshes, what);
    }
}

/// <summary>
/// The mark of a step that starts no git call (<see cref="ShellDriver.QuietStepMarkAsync"/>): its
/// git calls' (<see cref="ShellDriver.CallMark"/>), the refreshes the open tabs' watchers had asked
/// for (<see cref="ShellDriver.WatcherRefreshes"/>) and the operation log's count, taken together.
/// </summary>
public readonly record struct QuietStepMark(long Calls, int WatcherRefreshes, int Operations);
