namespace VisualCommit.Core.Operations;

/// <summary>
/// The operation the current asynchronous flow belongs to (phase 3 design 1.7). The queue enters
/// it around an operation's <see cref="GitOperation.Run"/>; the git runner reads it and records
/// it in each <see cref="Git.GitCallRecord"/>, so that the activity log can group calls by
/// operation. It flows into awaited calls and tasks started from them, as any
/// <see cref="AsyncLocal{T}"/> does.
/// </summary>
public static class GitOperationScope
{
    private static readonly AsyncLocal<string?> CurrentId = new();

    /// <summary>The id of the operation running in this flow, or null.</summary>
    public static string? Current => CurrentId.Value;

    /// <summary>Makes <paramref name="operationId"/> the current operation until the result is disposed, which puts back the one before.</summary>
    public static IDisposable Enter(string? operationId)
    {
        var previous = CurrentId.Value;
        CurrentId.Value = operationId;
        return new Restore(previous);
    }

    private sealed class Restore(string? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                CurrentId.Value = previous;
            }
        }
    }
}
