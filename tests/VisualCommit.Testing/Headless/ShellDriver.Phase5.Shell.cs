using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Operations;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 5's shell in the driver (docs/phase-5-design.md 1.3, 2, 15.2; docs/test-reports/phase-5.md,
/// part A and its decision AD12, package WP5): the window's keys for the toolbar's Search, Undo
/// and Redo, the activity log read as a snapshot, its rows, its toggle and its splitter, and the
/// wait for an operation's record. Phase 4's <see cref="Undo"/> and <see cref="Redo"/> send the
/// same keys as <see cref="PressGitUndo"/> and <see cref="PressGitRedo"/> and stay the text undo of
/// a text box and the resolver: a check uses these where the key is meant for the window, so that
/// its code says which handler should take it.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>
    /// Presses the command modifier with F (Ctrl+F; Cmd+F where the platform's command modifier
    /// is Cmd), the toolbar's Search (1.3).
    /// </summary>
    public void PressSearch() => PressKey(Key.F, Raw(CommandModifier));

    /// <summary>Presses the platform's undo gesture (<see cref="UndoGesture"/>, Ctrl+Z), the toolbar's Undo when no text box or the resolver takes it (1.3).</summary>
    public void PressGitUndo() => PressGesture(UndoGesture);

    /// <summary>
    /// Presses one of the platform's redo gestures, the toolbar's Redo when no text box or the
    /// resolver takes it (1.3): the <paramref name="gesture"/>-th of <c>HotkeyConfiguration.Redo</c>
    /// (Ctrl+Y, then Ctrl+Shift+Z on Windows and in the headless platform).
    /// </summary>
    public void PressGitRedo(int gesture = 0)
    {
        var gestures = RedoGestures;
        if (gesture < 0 || gesture >= gestures.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(gesture), gesture, $"The platform names {gestures.Count} redo gestures.");
        }

        PressGesture(gestures[gesture]);
    }

    /// <summary>Every redo gesture of the platform, in its order (<c>HotkeyConfiguration.Redo</c>).</summary>
    public IReadOnlyList<KeyGesture> RedoGestures => [.. Hotkeys.Redo];

    /// <summary>The activity log's panel (<c>ActivityLog</c>), shown or not.</summary>
    public ActivityLogView ActivityLogView => Find<ActivityLogView>("ActivityLog");

    /// <summary>The activity log's view model: the window's (<c>MainWindowViewModel.ActivityLog</c>).</summary>
    public ActivityLogViewModel ActivityLogModel => Session.Shell.ActivityLog;

    /// <summary>
    /// The activity log as it is drawn now (AD12, the report's part A, "Reading the log"), in the
    /// window's coordinates: the panel, its header's texts, "Show reads", and every row of its list
    /// top to bottom, each with its kind and automation name, and while it is realised its bounds,
    /// indent and the parts its controls draw: chevron, outcome glyph, texts, colours and the output
    /// box's parts. Settles first.
    /// </summary>
    public ActivityLogSnapshot ActivityLog()
    {
        Settle();
        return ActivityLogView.Snapshot(Window);
    }

    /// <summary>
    /// The row of the activity log with the automation name <paramref name="name"/> (an operation's
    /// title, a call's command), the <paramref name="occurrence"/>-th of them among the list's rows,
    /// top to bottom, from 0 (AD12): a check clicks it at its centre. Throws when there is no such
    /// row, or when it is not wholly in the list's view (a step that clicks it would miss).
    /// </summary>
    public ActivityLogRowSnapshot ActivityLogRow(string name, int occurrence = 0)
    {
        ArgumentNullException.ThrowIfNull(name);
        var rows = ActivityLog().Rows
            .Where(row => row.Kind != ActivityLogRowKind.Output && string.Equals(row.AutomationName, name, StringComparison.Ordinal))
            .ToList();
        if (occurrence < 0 || occurrence >= rows.Count)
        {
            throw new InvalidOperationException($"The activity log has {rows.Count} rows named \"{name}\", not {occurrence + 1}.");
        }

        var found = rows[occurrence];
        if (found.Bounds is null || !found.IsInView)
        {
            throw new InvalidOperationException($"The activity log's row \"{name}\" ({occurrence}) is not in the list's view.");
        }

        return found;
    }

    /// <summary>Clicks the activity log's row (<see cref="ActivityLogRow"/>) at its centre: an operation's or a call's opens or closes.</summary>
    public void ClickActivityLogRow(string name, int occurrence = 0) => Click(ActivityLogRow(name, occurrence).Bounds!.Value.Center);

    /// <summary>Clicks the status bar's Activity log toggle (<c>ActivityLogToggle</c>, 1.2).</summary>
    public void ClickActivityLogToggle() => Click(FindByAutomationId("ActivityLogToggle"));

    /// <summary>
    /// Drags the activity log's splitter (<c>ActivityLogSplitter</c>, 2.1) with the left button from
    /// its centre by <paramref name="dy"/> (negative up: a taller panel), in <paramref name="steps"/>
    /// equal moves, and releases it, as A3 does.
    /// </summary>
    public void DragActivityLogSplitter(double dy, int steps = 5)
    {
        var splitter = (Control)FindByAutomationId("ActivityLogSplitter");
        if (!splitter.IsEffectivelyVisible)
        {
            throw new InvalidOperationException("The activity log's splitter is not shown: open the panel first.");
        }

        var from = BoundsOf(splitter).Center;
        Drag(from, from + new Vector(0, dy), steps);
    }

    /// <summary>
    /// Waits until the session's operation log has a record titled <paramref name="title"/> and
    /// returns the newest (15.2): the background clean-up's at opening (<c>Clean up backups</c>),
    /// which says nothing in the status bar. Settles the UI thread while it waits.
    /// </summary>
    public async Task<OperationRecord> WaitForOperationRecordAsync(string title, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        OperationRecord? found = null;
        await WaitForAsync(
            () => (found = Session.Operations.Snapshot().LastOrDefault(record => string.Equals(record.Title, title, StringComparison.Ordinal))) is not null,
            $"the operation record \"{title}\"",
            timeout);
        return found!;
    }
}
