namespace VisualCommit.Core.Operations;

/// <summary>Whether an operation talks to a remote (phase 3 design 1.5).</summary>
public enum OperationKind
{
    /// <summary>
    /// Touches only the repository on disk. Once started, it runs all its steps to the end: its
    /// git calls get no cancellation token, unless it waits only for another program
    /// (<see cref="GitOperation.CanBeCancelled"/>, phase 4's merge tool).
    /// </summary>
    Local,

    /// <summary>Talks to a remote: fetch, pull, push. The user can cancel it until <see cref="OperationContext.EndCancelWindow"/>.</summary>
    Network,
}

/// <summary>What the queue reads again after an operation (D71), whether it succeeded or not.</summary>
[Flags]
public enum RefreshScope
{
    None = 0,

    /// <summary>The working tree's status and the open diff.</summary>
    Status = 1,

    /// <summary>The refs, the history when they changed what the graph shows, then the status too.</summary>
    Refs = 2,
}

/// <summary>Where a failure is shown.</summary>
public enum ErrorDisplay
{
    /// <summary>The error dialog (phase 3's operations).</summary>
    Dialog,

    /// <summary>The stage panel's error text (phase 2's writes), and nothing in the status bar.</summary>
    StagePanel,
}

/// <summary>How git's and ssh's prompts are answered during an operation (section 3, D82).</summary>
public enum PromptMode
{
    /// <summary>Nothing is added to git's environment: a prompt fails at once (D30's <c>GIT_TERMINAL_PROMPT=0</c>).</summary>
    None,

    /// <summary>A prompt opens a dialog in the window.</summary>
    Ask,

    /// <summary>Every prompt is refused at once, without a dialog (auto-fetch).</summary>
    Deny,
}

/// <summary>
/// One operation for the <see cref="IOperationQueue"/> (phase 3 design 1.2, D81): what the user
/// sees of it and the delegate that does its work.
/// </summary>
/// <param name="Title">"Fetch", "Push main": the log line, the status bar's "&lt;Title&gt; failed" and the error dialog's title.</param>
/// <param name="RunningText">"Fetching from all remotes…": what the status bar shows while it runs.</param>
/// <param name="Kind">Local or network (1.5).</param>
/// <param name="Refresh">What is read again afterwards, also after a failure or a cancel.</param>
/// <param name="Run">
/// The work. It is called on the synchronization context of the <see cref="IOperationQueue.RunAsync"/>
/// call (the UI thread), with the watcher paused. Its git calls leave that thread inside the
/// runner (D59). It must not wait for another operation of the same queue.
/// </param>
public sealed record GitOperation(
    string Title,
    string RunningText,
    OperationKind Kind,
    RefreshScope Refresh,
    Func<OperationContext, Task<OperationReport>> Run)
{
    /// <summary>
    /// Null (the default): never merged with another operation. Otherwise a second
    /// <see cref="IOperationQueue.RunAsync"/> with the same key, while this one waits, returns
    /// this one's task instead of queueing a second run; once this one runs, a second one is
    /// queued behind it, because a running one has already read what it acts on. The key names the operation and
    /// every argument that changes what it does: <c>fetch:*</c>, <c>fetch:&lt;remote&gt;</c>,
    /// <c>pull:&lt;full branch ref&gt;</c>, <c>push:&lt;full branch ref&gt;-&gt;&lt;remote&gt;/&lt;remote ref&gt;</c>.
    /// </summary>
    public string? DedupeKey { get; init; }

    /// <summary>Where a failure is shown: the error dialog, or the stage panel (phase 2's writes).</summary>
    public ErrorDisplay Errors { get; init; } = ErrorDisplay.Dialog;

    /// <summary>
    /// A background operation: started with <see cref="IOperationQueue.TryStartInBackground"/>,
    /// never pre-empted, shows no running text or progress (1.8, D85). Two are: auto-fetch, which
    /// the user can cancel only while an operation of theirs waits for it (the status bar then
    /// offers Cancel), and the backups' clean-up (docs/phase-4-design.md section 9, D139), a local
    /// operation that cannot be cancelled and whose end the status bar does not show.
    /// </summary>
    public bool IsBackground { get; init; }

    /// <summary>
    /// How prompts are answered. Null: <see cref="PromptMode.Ask"/>, or <see cref="PromptMode.Deny"/>
    /// for a background operation. Phase 2's writes pass <see cref="PromptMode.None"/>.
    /// </summary>
    public PromptMode? Prompts { get; init; }

    /// <summary>
    /// The bulk run the operation belongs to (docs/phase-6-design.md 4.4): the queue passes it to
    /// <see cref="IGitPromptBroker.Begin"/>, so that the run's prompts are answered as one group.
    /// Null (the default) outside a run.
    /// </summary>
    public PromptGroup? PromptGroup { get; init; }

    /// <summary>
    /// A local operation of more than one git call (stash, check out and re-apply; check out and
    /// merge; several cherry-picks): the "Finishing operations" dialog says that its remaining
    /// steps will not run when the user quits now (1.8).
    /// </summary>
    public bool HasSeveralSteps { get; init; }

    /// <summary>
    /// A local operation that only waits for another program, the external merge tool
    /// (docs/phase-4-design.md 11.9, D149, amending D81): the queue gives it a cancellation
    /// source as it gives a network operation, so the status bar offers Cancel, which stops git
    /// and what it started; the stale lock files are looked for after its cancel, as after a
    /// network one. Otherwise it stays local: closing the tab leaves it to finish.
    /// </summary>
    public bool CanBeCancelled { get; init; }
}
