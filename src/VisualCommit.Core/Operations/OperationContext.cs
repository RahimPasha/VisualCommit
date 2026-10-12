namespace VisualCommit.Core.Operations;

/// <summary>What an operation's <see cref="GitOperation.Run"/> is given (phase 3 design 1.2).</summary>
public sealed class OperationContext
{
    private static readonly IReadOnlyDictionary<string, string?> NoVariables = new Dictionary<string, string?>();

    private readonly CancellationToken _cancellationToken;
    private readonly Func<bool>? _endCancelWindow;
    private readonly Func<bool>? _promptCancelled;
    private volatile bool _cancelWindowOpen = true;

    /// <param name="operationId">The operation's id, <c>op-&lt;n&gt;</c>.</param>
    /// <param name="cancellationToken">Cancelled when the user cancels a network operation or a local one that can be cancelled (<see cref="GitOperation.CanBeCancelled"/>); <see cref="CancellationToken.None"/> for any other local one.</param>
    /// <param name="environment">The prompt scope's variables for every git call of the operation; empty for <see cref="PromptMode.None"/>.</param>
    /// <param name="progress">Where git's progress goes; null discards it.</param>
    /// <param name="endCancelWindow">
    /// Called by <see cref="EndCancelWindow"/>: tells the queue that Cancel is no longer offered,
    /// and returns true when the user had already cancelled.
    /// </param>
    /// <param name="promptCancelled">Whether the user has cancelled one of the operation's prompts (<see cref="PromptScope.UserCancelled"/>).</param>
    public OperationContext(
        string operationId,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string?>? environment = null,
        IProgress<OperationProgress>? progress = null,
        Func<bool>? endCancelWindow = null,
        Func<bool>? promptCancelled = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        OperationId = operationId;
        _cancellationToken = cancellationToken;
        Environment = environment ?? NoVariables;
        Progress = progress ?? NoProgress.Instance;
        _endCancelWindow = endCancelWindow;
        _promptCancelled = promptCancelled;
    }

    /// <summary>
    /// The user pressed Cancel or Esc at one of the operation's prompts: the operation ends as
    /// cancelled (2.16), so a call that would ask again (the next remote of a fetch of all
    /// remotes) is not made.
    /// </summary>
    public bool PromptCancelled => _promptCancelled?.Invoke() ?? false;

    /// <summary><c>op-&lt;n&gt;</c>, unique in the app session. Each git call of the operation records it (1.7).</summary>
    public string OperationId { get; }

    /// <summary>
    /// The token to pass to git: cancelled when the user cancels a network operation, or a local
    /// one that can be cancelled (<see cref="GitOperation.CanBeCancelled"/>, the merge tool).
    /// <see cref="CancellationToken.None"/> for any other local operation, and after
    /// <see cref="EndCancelWindow"/>.
    /// </summary>
    public CancellationToken CancellationToken => _cancelWindowOpen ? _cancellationToken : CancellationToken.None;

    /// <summary>The prompt scope's variables (section 3), to put into every git call's environment. Empty for <see cref="PromptMode.None"/>.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; }

    /// <summary>Where git's progress goes; the queue shows at most one update per 100 ms.</summary>
    public IProgress<OperationProgress> Progress { get; }

    /// <summary>
    /// From now on Cancel is no longer offered and <see cref="CancellationToken"/> no longer
    /// reaches git: pull calls it between its fetch and its merge or rebase (2.18, D84), so that
    /// git is never stopped while it writes the working tree.
    /// </summary>
    /// <exception cref="OperationCanceledException">The user cancelled before the window ended: the operation must stop here.</exception>
    public void EndCancelWindow()
    {
        if (!_cancelWindowOpen)
        {
            return;
        }

        var cancelled = _endCancelWindow?.Invoke() ?? false;
        cancelled |= _cancellationToken.IsCancellationRequested;
        _cancelWindowOpen = false;
        if (cancelled)
        {
            throw new OperationCanceledException("The operation was cancelled before its cancel window ended.", _cancellationToken);
        }
    }

    private sealed class NoProgress : IProgress<OperationProgress>
    {
        public static NoProgress Instance { get; } = new();

        public void Report(OperationProgress value)
        {
        }
    }
}
