using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// The order in which a start's restored tabs open when the active one is a repository's tab.
/// Not built yet: every tab opens at once.
/// </summary>
public sealed class RestoreOrder
{
    /// <summary>How many of the waiting tabs open at once after the release, as "Add repositories" opens and a dashboard reads (D207).</summary>
    public const int OpensAtOnce = 4;

    /// <summary>The line logged at Info when the order starts: the first tab's name, the others' count, <see cref="OpensAtOnce"/>.</summary>
    public const string StartedFormat = "Restore order: {0} opens first; the other restored tabs ({1}) wait for its first page, at most 5 s, then open at most {2} at once (a tab shown meanwhile opens at once).";

    /// <summary>The line logged at Info at the release: the first tab's name, the reason, the milliseconds since it began opening, the waiting tabs' count, <see cref="OpensAtOnce"/>.</summary>
    public const string ReleasedFormat = "Restore order: {0}: {1}, {2} ms after it began opening; the other restored tabs ({3}) start opening, at most {4} at once.";

    /// <summary>The release's reason when the first tab's graph has its first page.</summary>
    public const string FirstPageReason = "its graph has its first page";

    /// <summary>The release's reason when the first tab's opening or first load ended without a first page.</summary>
    public const string EndedReason = "its opening ended without a first page";

    /// <summary>The release's reason when the first tab was closed.</summary>
    public const string ClosedReason = "its tab was closed";

    /// <summary>The release's reason when <see cref="LongestWait"/> passed first.</summary>
    public const string WaitedReason = "5 s passed without a first page";

    /// <summary>The line logged at Debug when a waiting tab is shown: its name.</summary>
    public const string ShownFormat = "Restore order: {0} was shown while it waited and opens now.";

    /// <summary>The line logged at Debug when a waiting tab is closed: its name.</summary>
    public const string DroppedFormat = "Restore order: {0} was closed while it waited and does not open.";

    /// <summary>The line logged at Debug when the window closes while tabs wait: their count.</summary>
    public const string StoppedFormat = "Restore order: the window closed with {0} restored tabs still waiting; they do not open.";

    /// <summary>The longest the other restored tabs wait for the first one, on the restore clock.</summary>
    public static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(5);

    private readonly IReadOnlyList<RepoTabViewModel> _restored;
    private readonly Func<RepoTabViewModel, string, Task<bool>> _open;

    public RestoreOrder(
        RepoTabViewModel first,
        IReadOnlyList<RepoTabViewModel> restored,
        Func<RepoTabViewModel, string, Task<bool>> open,
        TimeProvider clock,
        IAppLog log,
        CancellationToken lifetime)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(restored);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(log);
        _restored = [.. restored];
        _open = open;
        _ = lifetime;
    }

    /// <summary>Completes when the task of a tab the order opened has ended.</summary>
    public Task FirstLoadEnded => Task.CompletedTask;

    /// <summary>Opens every restored tab at once and returns a task per tab, in order.</summary>
    public IReadOnlyList<Task> Start() => [.. _restored.Select(tab => (Task)_open(tab, tab.PendingPath!))];

    /// <summary>A tab was shown.</summary>
    public void Shown(RepoTabViewModel tab)
    {
    }

    /// <summary>A tab was closed.</summary>
    public void Closed(RepoTabViewModel tab)
    {
    }

    /// <summary>Nothing more opens.</summary>
    public void Stop()
    {
    }
}
