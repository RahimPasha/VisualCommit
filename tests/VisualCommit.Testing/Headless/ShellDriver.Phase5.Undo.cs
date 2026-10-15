using Avalonia.Controls;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Part U's additions to the driver (docs/phase-5-design.md 1.1, 7.4, 15.2; the report's part U
/// and its decision UD16, package WP3): the toolbar's Undo and Redo read as both harnesses read
/// them (each button's enabled state and its tooltip, which is also its automation help text) and
/// clicked at their centres. The keys are WP5's (<c>PressGitUndo</c> and <c>PressGitRedo</c>, in
/// ShellDriver.Phase5.Shell.cs).
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The toolbar's Undo (<c>UndoButton</c>, 13.4).</summary>
    public const string UndoButtonId = "UndoButton";

    /// <summary>The toolbar's Redo (<c>RedoButton</c>, 13.4).</summary>
    public const string RedoButtonId = "RedoButton";

    /// <summary>
    /// The toolbar's Undo and Redo as they are drawn now (UD16): whether each is enabled
    /// (<c>IsEffectivelyEnabled</c>: its command may disable it) and its tooltip's text, null for
    /// none. Settles first. The report writes the states T0, T+ and T− (<see cref="UndoRedoButtons"/>).
    /// </summary>
    public UndoRedoButtons UndoRedoState()
    {
        Settle();
        var undo = (Control)FindByAutomationId(UndoButtonId);
        var redo = (Control)FindByAutomationId(RedoButtonId);
        return new UndoRedoButtons(undo.IsEffectivelyEnabled, TipOf(undo), redo.IsEffectivelyEnabled, TipOf(redo));
    }

    /// <summary>
    /// Clicks the toolbar's Undo at its centre, then waits until the active tab's queue is idle
    /// (UD16): the step has ended with its refresh, or, for a step that changes a remote, the
    /// question is open (no operation runs until it is answered).
    /// </summary>
    /// <exception cref="InvalidOperationException">Undo is disabled: a click would do nothing, and a check that clicks it expects it to act.</exception>
    public Task ClickUndoAsync(TimeSpan? timeout = null) => ClickStepButtonAsync(UndoButtonId, timeout);

    /// <summary>Clicks the toolbar's Redo at its centre, then waits as <see cref="ClickUndoAsync"/> does.</summary>
    /// <exception cref="InvalidOperationException">Redo is disabled.</exception>
    public Task ClickRedoAsync(TimeSpan? timeout = null) => ClickStepButtonAsync(RedoButtonId, timeout);

    private async Task ClickStepButtonAsync(string automationId, TimeSpan? timeout)
    {
        Settle();
        var button = (Control)FindByAutomationId(automationId);
        if (!button.IsEffectivelyVisible || !button.IsEffectivelyEnabled)
        {
            throw new InvalidOperationException($"{automationId} is disabled ({TipOf(button) ?? "no tooltip"}): a click would do nothing.");
        }

        Click(button);
        await WaitForIdleAsync(timeout: timeout);
    }

    /// <summary>A control's tooltip as text, or null when it has none.</summary>
    private static string? TipOf(Control control) => ToolTip.GetTip(control) is { } tip ? TipText(tip) : null;
}

/// <summary>
/// The toolbar's Undo and Redo as drawn (the report's part U, UD16): each enabled or not, with its
/// tooltip (its automation help text). <see cref="Nothing"/> is the report's T0,
/// <see cref="CanUndo(string)"/> its T+ and <see cref="CanRedo(string)"/> its T−.
/// </summary>
/// <param name="UndoEnabled">Undo is enabled.</param>
/// <param name="UndoToolTip">Undo's tooltip, or null for none.</param>
/// <param name="RedoEnabled">Redo is enabled.</param>
/// <param name="RedoToolTip">Redo's tooltip, or null for none.</param>
public sealed record UndoRedoButtons(bool UndoEnabled, string? UndoToolTip, bool RedoEnabled, string? RedoToolTip)
{
    /// <summary>T0: Undo disabled with <c>Nothing to undo</c>, Redo disabled with <c>Nothing to redo</c>.</summary>
    public static UndoRedoButtons Nothing { get; } = new(false, "Nothing to undo", false, "Nothing to redo");

    /// <summary>T+ x: Undo enabled with <c>Undo x</c>; Redo disabled with <c>Nothing to redo</c>.</summary>
    public static UndoRedoButtons CanUndo(string text) => new(true, $"Undo {text}", false, "Nothing to redo");

    /// <summary>T− x: Undo disabled with <c>Nothing to undo</c>; Redo enabled with <c>Redo x</c>.</summary>
    public static UndoRedoButtons CanRedo(string text) => new(false, "Nothing to undo", true, $"Redo {text}");
}
