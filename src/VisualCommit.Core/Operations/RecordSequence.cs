namespace VisualCommit.Core.Operations;

/// <summary>
/// The numbers that order the activity log's rows by when they started (docs/phase-5-design.md
/// 2.3, D173): an operation (<see cref="OperationRecord.Sequence"/>) and a git call
/// (<see cref="Git.GitCallRecord.Sequence"/>) each take one when they start, from this one
/// counter, so the log orders them by start whatever their times say (the harnesses freeze the
/// times) and whenever it was opened. The counter is the process's, as operation ids are: every
/// session of a process (tests start several) gets increasing numbers, not consecutive ones.
/// Safe to use from any thread.
/// </summary>
public static class RecordSequence
{
    private static long _last;

    /// <summary>The next number: greater than every number taken before it, on any thread.</summary>
    public static long Next() => Interlocked.Increment(ref _last);
}
