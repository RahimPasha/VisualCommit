using System.Text;
using Avalonia.Threading;
using VisualCommit.App.ViewModels.Conflicts;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Testing;

namespace VisualCommit.App.Tests;

/// <summary>
/// A recording fake of <see cref="IConflictActions"/> (docs/phase-4-design.md 11): every call is
/// recorded in words, the files' contents and sides come from dictionaries the test fills, the
/// questions are answered from a queue, and a write can be held until the test lets it end. A
/// write that resolves a file first runs <see cref="BeforeWriteEnds"/>, as the queue refreshes the
/// status before an operation's task ends.
/// </summary>
internal sealed class RecordingConflictActions : IConflictActions
{
    private readonly Queue<bool> _answers = new();

    /// <summary>What was called, in order: <c>Read notes.txt</c>, <c>Confirm Discard your changes to notes.txt?</c>, <c>MarkResolved notes.txt</c>, …</summary>
    public List<string> Calls { get; } = [];

    /// <summary>The questions asked, in order.</summary>
    public List<ConfirmationRequest> Questions { get; } = [];

    /// <summary>The bytes written by Mark resolved, by path.</summary>
    public Dictionary<string, byte[]> Written { get; } = new(StringComparer.Ordinal);

    /// <summary>What <see cref="ReadAsync"/> gives for each path; change it to stand for a file changed on disk.</summary>
    public Dictionary<string, ConflictContent> Contents { get; } = new(StringComparer.Ordinal);

    /// <summary>What <see cref="ReadSideAsync"/> gives for each path and side; missing: null.</summary>
    public Dictionary<(string Path, ConflictSide Side), byte[]> Sides { get; } = [];

    /// <summary>Whether each write resolves the file (true unless set).</summary>
    public bool WritesResolve { get; set; } = true;

    /// <summary>Run as a write ends, before its task completes: the test's stand-in for the queue's refresh.</summary>
    public Action<string>? BeforeWriteEnds { get; set; }

    /// <summary>While set, every write waits for <see cref="Release"/>.</summary>
    public TaskCompletionSource? Hold { get; set; }

    /// <summary>Queues the answers of the next questions; an unanswered question goes ahead.</summary>
    public void Answer(params bool[] answers)
    {
        foreach (var answer in answers)
        {
            _answers.Enqueue(answer);
        }
    }

    /// <summary>Lets held writes end.</summary>
    public void Release() => Hold?.TrySetResult();

    /// <summary>A text file's content as git leaves it: UTF-8, marker size 7, no binary side.</summary>
    public void SetText(string path, string text, int markerSize = 7) =>
        Contents[path] = new ConflictContent(Encoding.UTF8.GetBytes(text), markerSize, false, false, false, true);

    public Task<ConflictContent> ReadAsync(ConflictEntry entry)
    {
        Calls.Add($"Read {entry.Path}");
        return Task.FromResult(Contents.TryGetValue(entry.Path, out var content)
            ? content
            : new ConflictContent([], 7, false, false, false, false));
    }

    public Task<byte[]?> ReadSideAsync(ConflictEntry entry, ConflictSide side)
    {
        Calls.Add($"ReadSide {entry.Path} {side}");
        return Task.FromResult(Sides.TryGetValue((entry.Path, side), out var bytes) ? bytes : null);
    }

    /// <summary>Run while a question is open, before it is answered: what happens meanwhile (a refresh).</summary>
    public Action<ConfirmationRequest>? WhileAsking { get; set; }

    public Task<bool> ConfirmAsync(ConfirmationRequest request)
    {
        Calls.Add($"Confirm {request.Title}");
        Questions.Add(request);
        WhileAsking?.Invoke(request);
        return Task.FromResult(_answers.Count == 0 || _answers.Dequeue());
    }

    /// <summary>
    /// Mark resolved's operation, recorded as <c>MarkResolved {path}</c> when it is called: waits
    /// while <see cref="Hold"/> is set, then, as the queued operation does just before it writes,
    /// compares the file (<see cref="Contents"/>) with the bytes the resolver expected. The same:
    /// the bytes go to <see cref="Written"/> and to <see cref="Contents"/> (when the write
    /// resolves). Different: nothing is written and the call is recorded as
    /// <c>Refused {path}: changed on disk</c> instead.
    /// </summary>
    public async Task<MarkResolvedResult> MarkResolvedAsync(ConflictEntry entry, byte[] content, byte[]? expected)
    {
        var path = entry.Path;
        var call = Calls.Count;
        Calls.Add($"MarkResolved {path}");
        if (Hold is { } hold)
        {
            await hold.Task;
        }

        var onDisk = Contents.TryGetValue(path, out var now) && now.WorkingTreeExists ? now.WorkingTree : null;
        if (expected is null ? onDisk is not null : onDisk is null || !onDisk.AsSpan().SequenceEqual(expected))
        {
            Calls[call] = $"Refused {path}: changed on disk";
            return new MarkResolvedResult(MarkResolvedOutcome.ChangedOnDisk, onDisk);
        }

        Written[path] = content;
        if (WritesResolve && Contents.TryGetValue(path, out var read))
        {
            Contents[path] = read with { WorkingTree = content, WorkingTreeExists = true };
        }

        return new MarkResolvedResult(await WriteAsync(path) ? MarkResolvedOutcome.Resolved : MarkResolvedOutcome.Failed);
    }

    public Task<bool> TakeSideAsync(string path, ConflictSide side)
    {
        Calls.Add($"TakeSide {path} {side}");
        return WriteAsync(path);
    }

    public Task<bool> RemoveAsync(string path)
    {
        Calls.Add($"Remove {path}");
        return WriteAsync(path);
    }

    public Task<bool> RunMergeToolAsync(string path)
    {
        Calls.Add($"RunMergeTool {path}");
        return WriteAsync(path);
    }

    /// <summary>What <see cref="MergedRemoteBranch"/> answers for a branch and MERGE_HEAD; null: none.</summary>
    public Func<string, string?, string?>? RemoteBranches { get; set; }

    public string? MergedRemoteBranch(string branch, string? mergeHead) => RemoteBranches?.Invoke(branch, mergeHead);

    /// <summary>The merge tool that runs now, as the test sets it (the file menu's, for instance).</summary>
    public MergeToolRun? RunningMergeTool { get; set; }

    public event EventHandler<MergeToolRun>? MergeToolStarted;

    /// <summary>A merge tool starts on <paramref name="path"/> as the file menu starts it: running until <paramref name="staged"/> completes.</summary>
    public MergeToolRun StartMergeTool(string path, string tool, Task<bool> staged)
    {
        var run = new MergeToolRun(path, tool, staged);
        RunningMergeTool = run;
        MergeToolStarted?.Invoke(this, run);
        return run;
    }

    /// <summary>The writes recorded, without the reads and questions.</summary>
    public IReadOnlyList<string> Writes => Calls.Where(call => !call.StartsWith("Read", StringComparison.Ordinal) && !call.StartsWith("Confirm", StringComparison.Ordinal) && !call.StartsWith("Refused", StringComparison.Ordinal)).ToList();

    private async Task<bool> WriteAsync(string path)
    {
        if (Hold is { } hold)
        {
            await hold.Task;
        }

        if (WritesResolve)
        {
            BeforeWriteEnds?.Invoke(path);
        }

        return WritesResolve;
    }
}

/// <summary>Delays a test lets end one by one: the resolver's parse of typed text (11.5).</summary>
/// <summary>A clock that moves only when the test moves it, for the double-click guard (11.1).</summary>
internal sealed class ManualTime : TimeProvider
{
    private long _ticks = TimeSpan.FromHours(1).Ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public void Advance(TimeSpan by) => _ticks += by.Ticks;
}

internal sealed class ManualDelays
{
    private readonly List<(TaskCompletionSource Done, CancellationToken Token)> _waiting = [];

    /// <summary>How many delays wait and were not cancelled.</summary>
    public int Waiting => _waiting.Count(entry => !entry.Token.IsCancellationRequested && !entry.Done.Task.IsCompleted);

    public Task Delay(TimeSpan delay, CancellationToken token)
    {
        var done = new TaskCompletionSource();
        token.Register(() => done.TrySetCanceled(token));
        _waiting.Add((done, token));
        return done.Task;
    }

    /// <summary>Ends every delay that waits, as if its time had passed.</summary>
    public void Elapse()
    {
        foreach (var (done, _) in _waiting.ToList())
        {
            done.TrySetResult();
        }

        _waiting.Clear();
        ResolverFixtures.Settle();
    }
}

/// <summary>What the resolver's tests share: conflicts, statuses and the scenario's files.</summary>
internal static class ResolverFixtures
{
    public const string Zero = "0000000000000000000000000000000000000000";

    /// <summary>The merge's stop of the conflicts scenario (design 13.1; the report's FM).</summary>
    public static string Notes => Scenarios.ConflictsFiles.NotesMergeConflicted;

    /// <summary>R1: FM after Use ours on block 1 (the report's C10 results).</summary>
    public static string R1 => string.Concat(
        "line 1\nline 2 ours\nline 3\nline 4\nline 5\nline 6 theirs\nline 7\nline 8\nline 9\n",
        "<<<<<<< HEAD\nline 10 ours\n=======\nline 10 theirs\n>>>>>>> feature/theirs\nline 11\nline 12\n");

    /// <summary>R2: R1 after Use theirs on block 2.</summary>
    public static string R2 => "line 1\nline 2 ours\nline 3\nline 4\nline 5\nline 6 theirs\nline 7\nline 8\nline 9\nline 10 theirs\nline 11\nline 12\n";

    /// <summary>R3: R1 after Use both (ours first) on block 2.</summary>
    public static string R3 => "line 1\nline 2 ours\nline 3\nline 4\nline 5\nline 6 theirs\nline 7\nline 8\nline 9\nline 10 ours\nline 10 theirs\nline 11\nline 12\n";

    /// <summary>A conflicted path with all three stages (or the ones <paramref name="kind"/> has).</summary>
    public static ConflictEntry Entry(string path, ConflictKind kind = ConflictKind.BothModified, bool submodule = false, string mode = "100644")
    {
        var hasBase = kind is ConflictKind.BothModified or ConflictKind.DeletedByThem or ConflictKind.DeletedByUs or ConflictKind.BothDeleted;
        var hasOurs = kind is ConflictKind.BothModified or ConflictKind.BothAdded or ConflictKind.DeletedByThem or ConflictKind.AddedByUs;
        var hasTheirs = kind is ConflictKind.BothModified or ConflictKind.BothAdded or ConflictKind.DeletedByUs or ConflictKind.AddedByThem;
        return new ConflictEntry(
            path,
            kind,
            submodule,
            hasBase ? mode : "000000",
            hasOurs ? mode : "000000",
            hasTheirs ? mode : "000000",
            hasBase ? new string('1', 40) : Zero,
            hasOurs ? new string('2', 40) : Zero,
            hasTheirs ? new string('3', 40) : Zero);
    }

    /// <summary>A merge of <c>feature/theirs</c> stopped (MERGE_MSG's first line as its subject).</summary>
    public static InProgressState Merge(string branch = "feature/theirs") =>
        new(InProgressKind.Merge, "5c77ffb0ec78abf986d121f42013eee14422e5a5", $"Merge branch '{branch}'", null, null, 0, false);

    /// <summary>A status whose unstaged list is the conflicted files, with <paramref name="inProgress"/>.</summary>
    public static WorkingTreeStatus Status(InProgressState inProgress, params ConflictEntry[] conflicts) =>
        new([], [.. conflicts.Select(conflict => new ChangedFile(conflict.Path, FileChangeKind.Conflicted))], new HashSet<string>(StringComparer.Ordinal))
        {
            Conflicts = conflicts,
            InProgress = inProgress,
        };

    /// <summary>Runs the UI thread's jobs until nothing is left to do.</summary>
    public static void Settle()
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>A resolver over <paramref name="actions"/> with delays the test ends, fed with <paramref name="status"/>.</summary>
    public static ConflictResolverViewModel Resolver(RecordingConflictActions actions, ManualDelays delays, WorkingTreeStatus status, string currentName = "main", string? ontoName = null, TimeProvider? time = null)
    {
        var resolver = new ConflictResolverViewModel(actions, delay: delays.Delay, time: time);
        resolver.Update(status, currentName, ontoName);
        return resolver;
    }

    /// <summary>The document's text with LF line ends shown as they are.</summary>
    public static string Text(ConflictResolverViewModel resolver) => resolver.Document?.Text ?? throw new InvalidOperationException("The resolver shows no result.");

    /// <summary>The offset of the start of line <paramref name="line"/> (from 1) of the result.</summary>
    public static int LineStart(ConflictResolverViewModel resolver, int line) => resolver.Document!.GetLineByNumber(line).Offset;
}
