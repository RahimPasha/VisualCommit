using VisualCommit.Core.Git;

namespace VisualCommit.Core.Undo;

/// <summary>
/// Collects, while a recorded action runs, what the reads before and after it cannot see
/// (docs/phase-5-design.md 7.7 step 3): a reset's mode and snapshot, the stash a stash made, a
/// push's remote values, the snapshot a discard saved or a restore put back, the files a hard
/// reset changed, and every backup the action saved (the <c>.gitignore</c> the recorder reads
/// itself, before and after the append). The
/// recorder begins one around the action (<see cref="Begin"/>); the operations report through the
/// static methods, which do nothing when no capture runs in their flow. It flows with the
/// action's awaits (an <see cref="AsyncLocal{T}"/>) into the git layer's pool threads, so it is
/// safe to use from any thread.
/// </summary>
public sealed class UndoCapture
{
    private static readonly AsyncLocal<UndoCapture?> CurrentCapture = new();

    private readonly Lock _gate = new();
    private UndoCaptured _captured = UndoCaptured.None;

    private UndoCapture()
    {
    }

    /// <summary>The capture of the action that runs in this flow, or null.</summary>
    public static UndoCapture? Current => CurrentCapture.Value;

    /// <summary>What was captured so far.</summary>
    public UndoCaptured Captured
    {
        get
        {
            lock (_gate)
            {
                return _captured;
            }
        }
    }

    /// <summary>
    /// Starts a capture for the action that runs in this flow until the result is disposed, which
    /// gives the flow back the capture it had before.
    /// </summary>
    public static IDisposable Begin(out UndoCapture capture)
    {
        var previous = CurrentCapture.Value;
        capture = new UndoCapture();
        CurrentCapture.Value = capture;
        return new Restore(previous);
    }

    /// <summary>A reset ran in <paramref name="mode"/>, with its <c>-changes</c> snapshot or none.</summary>
    public static void Reset(ResetMode mode, string? snapshot) =>
        Change(captured => captured with { ResetMode = mode, ResetSnapshot = snapshot });

    /// <summary>A stash was made (<paramref name="stash"/>, null when git had nothing to stash), with or without untracked files, with the user's message or none.</summary>
    public static void Stash(string? stash, bool includedUntracked, string? message) =>
        Change(captured => captured with { Stash = stash, StashIncludedUntracked = includedUntracked, StashMessage = message });

    /// <summary>A push to <paramref name="remote"/> changed the remote refs <paramref name="outcome"/> lists.</summary>
    public static void Push(string remote, PushOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        Change(captured => captured with { Remote = remote, Push = outcome });
    }

    /// <summary>A discard saved <paramref name="snapshot"/>, or a restore put it back.</summary>
    public static void Snapshot(DiscardSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Change(captured => captured with { Snapshot = snapshot });
    }

    /// <summary>The action changed these files beyond its scope's (a hard reset's): the read after it records their bytes.</summary>
    public static void FilesChanged(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Change(captured => captured with { ChangedFiles = [.. captured.ChangedFiles.Union(paths, StringComparer.Ordinal)] });
    }

    /// <summary>The action saved these backup refs (full names).</summary>
    public static void Backups(IReadOnlyList<string> refs)
    {
        ArgumentNullException.ThrowIfNull(refs);
        Change(captured => captured with { Backups = [.. captured.Backups.Union(refs, StringComparer.Ordinal)] });
    }

    private static void Change(Func<UndoCaptured, UndoCaptured> change)
    {
        if (CurrentCapture.Value is not { } capture)
        {
            return;
        }

        lock (capture._gate)
        {
            capture._captured = change(capture._captured);
        }
    }

    private sealed class Restore(UndoCapture? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                CurrentCapture.Value = previous;
            }
        }
    }
}
