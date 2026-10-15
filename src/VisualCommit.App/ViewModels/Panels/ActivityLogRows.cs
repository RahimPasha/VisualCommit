using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels.Panels;

/// <summary>What a row of the activity log is (docs/phase-5-design.md 2.2).</summary>
public enum ActivityLogRowKind
{
    /// <summary>An operation: its outcome, time, title, message and duration; it opens to what follows.</summary>
    Operation,

    /// <summary>Under an opened operation: its failure's summary, as the error dialog showed it.</summary>
    FailureSummary,

    /// <summary>Under an opened operation: one of its notes (the clean-up's removals).</summary>
    Note,

    /// <summary>Under an opened operation whose calls the memory budget dropped: "Its git commands are no longer kept."</summary>
    NoLongerKept,

    /// <summary>A git call: under its operation, or a read at the top level; it opens to its output box.</summary>
    Call,

    /// <summary>Under an opened call: what it was given and what it printed.</summary>
    Output,
}

/// <summary>The colour of a part of a row: the tokens of 2.2.</summary>
public enum ActivityLogTone
{
    /// <summary><c>VcTextPrimaryBrush</c>: a title, a command.</summary>
    Primary,

    /// <summary><c>VcTextSecondaryBrush</c>: a message that is neither a warning nor a failure, a note, <c>exit 0</c>, a duration.</summary>
    Secondary,

    /// <summary><c>VcSuccessBrush</c>: the outcome glyph of an operation that succeeded.</summary>
    Success,

    /// <summary><c>VcWarningBrush</c>: a warning or a stop with conflicts.</summary>
    Warning,

    /// <summary><c>VcDangerBrush</c>: a failure, its summary, an exit code other than 0.</summary>
    Danger,
}

/// <summary>An operation's outcome as its row's glyph shows it (2.2).</summary>
public enum ActivityLogOutcome
{
    /// <summary>Still running: no glyph.</summary>
    Running,

    /// <summary><c>IconCheck</c> in the success colour.</summary>
    Succeeded,

    /// <summary><c>IconAlert</c> in the warning colour: a warning, or a stop with conflicts.</summary>
    Warning,

    /// <summary><c>IconClose</c> in the danger colour.</summary>
    Failed,

    /// <summary><c>IconMinus</c> in the secondary colour.</summary>
    Cancelled,
}

/// <summary>
/// One row of the activity log's list (docs/phase-5-design.md 2.2): the list is flat, an opened
/// operation's lines and calls and an opened call's output box following it as rows of their own,
/// indented 24 per level. A row keeps its identity while the log grows, so the list changes only
/// where it must.
/// </summary>
public abstract class ActivityLogRow : ObservableObject
{
    /// <summary>The indent of one level: an operation's lines and calls, a call's output box (2.2).</summary>
    public const double IndentStep = 24;

    protected ActivityLogRow(int depth) => Depth = depth;

    public abstract ActivityLogRowKind Kind { get; }

    /// <summary>0 for an operation and a read, 1 under an operation, 2 for the output box of an operation's call.</summary>
    public int Depth { get; }

    /// <summary>How far the row's content is indented, in pixels.</summary>
    public double Indent => Depth * IndentStep;

    /// <summary>The name UI Automation gives the row: an operation's title, a call's command, a line's text.</summary>
    public abstract string AutomationName { get; }

    /// <summary>
    /// A duration as the log shows it (2.2): <c>{N} ms</c> below a second, <c>{N.N} s</c> from a
    /// second on.
    /// </summary>
    public static string FormatDuration(TimeSpan duration) =>
        duration < TimeSpan.FromSeconds(1)
            ? string.Create(CultureInfo.InvariantCulture, $"{(long)Math.Max(0, duration.TotalMilliseconds)} ms")
            : string.Create(CultureInfo.InvariantCulture, $"{duration.TotalSeconds:0.0} s");
}

/// <summary>An operation's row (2.2), running until its record comes; it opens and closes.</summary>
public sealed partial class ActivityLogOperationRow : ActivityLogRow
{
    public ActivityLogOperationRow(string id)
        : base(0)
    {
        Id = id;
    }

    public override ActivityLogRowKind Kind => ActivityLogRowKind.Operation;

    /// <summary>The operation's id (<see cref="OperationRecord.Id"/>), which its calls carry.</summary>
    public string Id { get; }

    public override string AutomationName => Title;

    /// <summary>Its lines and calls show under it.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGlyph))]
    public partial ActivityLogOutcome Outcome { get; private set; }

    /// <summary>A running operation has no glyph (2.2).</summary>
    public bool HasGlyph => Outcome != ActivityLogOutcome.Running;

    /// <summary>When it started, <c>HH:mm:ss</c> in the zone dates are shown in (D43).</summary>
    [ObservableProperty]
    public partial string Time { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutomationName))]
    public partial string Title { get; private set; } = string.Empty;

    /// <summary>The status bar's text for it, <c>Running…</c> while it runs, empty for a stage panel's write.</summary>
    [ObservableProperty]
    public partial string Message { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial ActivityLogTone MessageTone { get; private set; } = ActivityLogTone.Secondary;

    /// <summary>How long it ran; empty while it runs (decision AD4).</summary>
    [ObservableProperty]
    public partial string Duration { get; private set; } = string.Empty;

    /// <summary>Shows a started operation that has no record yet.</summary>
    internal void Show(OperationStart start, string time)
    {
        Title = start.Title;
        Time = time;
        Outcome = ActivityLogOutcome.Running;
        Message = ActivityLogViewModel.RunningText;
        MessageTone = ActivityLogTone.Secondary;
        Duration = string.Empty;
    }

    /// <summary>Shows an ended operation by its record.</summary>
    internal void Show(OperationRecord record, string time)
    {
        Title = record.Title;
        Time = time;
        Outcome = record.Outcome switch
        {
            OperationOutcome.Succeeded when record.IsWarning => ActivityLogOutcome.Warning,
            OperationOutcome.Succeeded => ActivityLogOutcome.Succeeded,
            OperationOutcome.Conflicts => ActivityLogOutcome.Warning,
            OperationOutcome.Failed => ActivityLogOutcome.Failed,
            _ => ActivityLogOutcome.Cancelled,
        };

        // The stage panel shows its writes' messages itself; the status bar says nothing of them (2.2).
        Message = record.Errors == ErrorDisplay.StagePanel ? string.Empty : record.Message;
        MessageTone = Outcome switch
        {
            ActivityLogOutcome.Warning => ActivityLogTone.Warning,
            ActivityLogOutcome.Failed => ActivityLogTone.Danger,
            _ => ActivityLogTone.Secondary,
        };
        Duration = FormatDuration(record.DisplayedDuration);
    }
}

/// <summary>A line under an opened operation (2.2): its failure's summary, a note, or that its calls are no longer kept.</summary>
public sealed class ActivityLogLineRow : ActivityLogRow
{
    public ActivityLogLineRow(ActivityLogRowKind kind, string text)
        : base(1)
    {
        Kind = kind;
        Text = text;
    }

    public override ActivityLogRowKind Kind { get; }

    public string Text { get; }

    /// <summary>The summary in the danger colour, the rest secondary.</summary>
    public ActivityLogTone Tone => Kind == ActivityLogRowKind.FailureSummary ? ActivityLogTone.Danger : ActivityLogTone.Secondary;

    public override string AutomationName => Text;
}

/// <summary>A git call's row (2.2): under its operation, or a read at the top level; it opens to its output box.</summary>
public sealed partial class ActivityLogCallRow : ActivityLogRow
{
    private ActivityLogOutputRow? _output;

    public ActivityLogCallRow(GitCallRecord call, int depth)
        : base(depth)
    {
        Call = call;
        (ExitText, ExitTone) = call.Outcome switch
        {
            GitCallOutcome.Completed when call.ExitCode == 0 => ("exit 0", ActivityLogTone.Secondary),
            GitCallOutcome.Completed => (string.Create(CultureInfo.InvariantCulture, $"exit {call.ExitCode}"), ActivityLogTone.Danger),
            GitCallOutcome.Cancelled => ("cancelled", ActivityLogTone.Secondary),
            _ => ("failed to start", ActivityLogTone.Danger),
        };
        Duration = FormatDuration(call.DisplayedDuration);
    }

    public override ActivityLogRowKind Kind => ActivityLogRowKind.Call;

    /// <summary>The record the row shows.</summary>
    public GitCallRecord Call { get; }

    /// <summary>A read, outside every operation: a row at the top level, shown with "Show reads".</summary>
    public bool IsRead => Call.OperationId is null;

    /// <summary><c>exit 0</c>, <c>exit 1</c>, <c>cancelled</c>, <c>failed to start</c>.</summary>
    public string ExitText { get; }

    public ActivityLogTone ExitTone { get; }

    /// <summary>The command as a user would type it, credentials hidden (<see cref="GitCallRecord.CommandText"/>).</summary>
    public string Command => Call.CommandText;

    /// <summary>Made on a temporary index (a snapshot's): <c>(on a temporary index)</c> follows the command.</summary>
    public bool OnTemporaryIndex => Call.OnTemporaryIndex;

    public string Duration { get; }

    public override string AutomationName => Command;

    /// <summary>Its output box shows under it.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    /// <summary>The row of its output box, made the first time it opens.</summary>
    internal ActivityLogOutputRow Output => _output ??= new ActivityLogOutputRow(Call, Depth + 1);
}

/// <summary>One titled part of an output box (2.2): <c>Input</c>, <c>Output</c> or <c>Error</c>.</summary>
/// <param name="Title">The part's title.</param>
/// <param name="Text">What it holds, as recorded (16 KiB at most, credentials hidden), without its last line break.</param>
public sealed record ActivityLogOutputPart(string Title, string Text);

/// <summary>
/// The output box under an opened call (2.2): what it was given on standard input when the app
/// records that (<c>update-ref --stdin</c>'s lines), then its standard output and standard error,
/// each part titled and shown when not empty, or <c>No output</c> when all are empty.
/// </summary>
public sealed class ActivityLogOutputRow : ActivityLogRow
{
    public ActivityLogOutputRow(GitCallRecord call, int depth)
        : base(depth)
    {
        ArgumentNullException.ThrowIfNull(call);
        Call = call;
        Parts =
        [
            .. new[] { ("Input", call.StandardInput), ("Output", call.StandardOutput), ("Error", call.StandardError) }
                .Where(part => !string.IsNullOrEmpty(part.Item2))
                .Select(part => new ActivityLogOutputPart(part.Item1, part.Item2!.TrimEnd('\r', '\n'))),
        ];
        Text = Parts.Count == 0
            ? ActivityLogViewModel.NoOutputText
            : string.Join("\n\n", Parts.Select(part => part.Title + "\n" + part.Text));
    }

    public override ActivityLogRowKind Kind => ActivityLogRowKind.Output;

    /// <summary>The call whose output it shows.</summary>
    public GitCallRecord Call { get; }

    /// <summary>The parts that are not empty, in 2.2's order.</summary>
    public IReadOnlyList<ActivityLogOutputPart> Parts { get; }

    /// <summary>All of it is empty: the box says <c>No output</c>.</summary>
    public bool IsEmpty => Parts.Count == 0;

    /// <summary>The box's text as one string, each part under its title: what UI Automation reads (its Value).</summary>
    public string Text { get; }

    public override string AutomationName => "Output";
}
