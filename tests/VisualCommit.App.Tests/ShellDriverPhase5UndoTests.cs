using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// The driver's part U helpers (ShellDriver.Phase5.Undo.cs, WP3; the report's UD16) in the headless
/// window: the toolbar's Undo and Redo read as drawn, each enabled or not with its tooltip, and
/// clicked at their centres with a wait for the queue. The buttons are given their state here, so
/// that the helpers are tested whatever binds them; the states the app gives them are
/// <see cref="UndoToolbarTests"/>' subject.
/// </summary>
public class ShellDriverPhase5UndoTests
{
    [AvaloniaFact]
    public async Task Undo_and_Redo_are_read_as_drawn_and_an_enabled_one_is_clicked_at_its_centre()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        await GraphShownAsync(app);

        var undo = (Button)app.FindByAutomationId(ShellDriver.UndoButtonId);
        var redo = (Button)app.FindByAutomationId(ShellDriver.RedoButtonId);
        var clicks = new List<string>();
        Give(undo, enabled: true, "Undo commit \"Describe more\"", () => clicks.Add("undo"));
        Give(redo, enabled: false, "Nothing to redo", () => clicks.Add("redo"));
        Assert.Equal(UndoRedoButtons.CanUndo("commit \"Describe more\""), app.UndoRedoState());

        await app.ClickUndoAsync();
        Assert.Equal(["undo"], clicks);

        // A disabled button is not clicked: the check that clicks it expects it to act.
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => app.ClickRedoAsync());
        Assert.Contains("Nothing to redo", refused.Message, StringComparison.Ordinal);
        Assert.Equal(["undo"], clicks);

        Give(undo, enabled: false, "Nothing to undo", () => clicks.Add("undo"));
        Give(redo, enabled: true, "Redo commit \"Describe more\"", () => clicks.Add("redo"));
        Assert.Equal(UndoRedoButtons.CanRedo("commit \"Describe more\""), app.UndoRedoState());
        await app.ClickRedoAsync();
        Assert.Equal(["undo", "redo"], clicks);

        // Without a tooltip, as an empty tab's buttons are.
        ToolTip.SetTip(undo, null);
        Assert.Null(app.UndoRedoState().UndoToolTip);
        Assert.Equal(new UndoRedoButtons(false, "Nothing to undo", false, "Nothing to redo"), UndoRedoButtons.Nothing);
    }

    /// <summary>Gives a toolbar button a state of the test's: enabled or not through its command, a tooltip, what a click does.</summary>
    private static void Give(Button button, bool enabled, string toolTip, Action click)
    {
        button.IsEnabled = true;
        button.Command = new RelayCommand(click, () => enabled);
        ToolTip.SetTip(button, toolTip);
    }
}
