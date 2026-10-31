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
}
