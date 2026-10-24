namespace VisualCommit.Core.Operations;

/// <summary>
/// The prompts of one bulk run (Fetch all or Pull all, docs/phase-6-design.md 4.4, D210): one per
/// run, from its start to its end, so that ten repositories on one host do not open ten stacked
/// sign-in dialogs (D104). The prompt dialogs show one prompt of a group at a time and answer a
/// prompt text the group was answered before from that answer, until the group ends.
/// <see cref="GitOperation.PromptGroup"/> carries it to the queue, which passes it to
/// <see cref="IGitPromptBroker.Begin"/>; each <see cref="GitPrompt.Group"/> of the scope names it.
/// </summary>
public sealed class PromptGroup : IDisposable
{
    private static long s_next;
    private readonly CancellationTokenSource _ended = new();
    private int _disposed;

    public PromptGroup()
    {
        Id = $"bulk-{Interlocked.Increment(ref s_next)}";
        Ended = _ended.Token;
    }

    /// <summary>The group's id, <c>bulk-&lt;n&gt;</c>, unique in the process: the key of its kept answers.</summary>
    public string Id { get; }

    /// <summary>Cancelled when the group ends (<see cref="Dispose"/>): a prompt waiting for its group's turn then stops waiting, and the group's answers are forgotten.</summary>
    public CancellationToken Ended { get; }

    /// <summary>Whether the group has ended.</summary>
    public bool IsEnded => Volatile.Read(ref _disposed) != 0;

    /// <summary>Ends the group: the run is over.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Cancelled, not disposed: Ended stays readable by a prompt that looks at it late.
        _ended.Cancel();
    }
}
