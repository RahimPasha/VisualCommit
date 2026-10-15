using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// The driver's part U helpers (ShellDriver.Phase5.Undo.cs, WP3; the report's UD16) in the headless
/// window: the toolbar's Undo and Redo read as drawn, each enabled or not with its tooltip, and
/// clicked at their centres with a wait for the step's operation, or for its question when one
/// opens first, also after a git read. The buttons are given their state and their step here, so
/// that the helpers are tested whatever binds them; the states the app gives them are
/// <see cref="UndoToolbarTests"/>' subject.
/// </summary>
public class ShellDriverPhase5UndoTests
{
    [AvaloniaFact]
    public async Task Undo_and_Redo_are_read_as_drawn_and_a_click_waits_for_the_steps_end_or_its_question()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);

        var undo = (Button)app.FindByAutomationId(ShellDriver.UndoButtonId);
        var redo = (Button)app.FindByAutomationId(ShellDriver.RedoButtonId);
        var steps = new List<string>();

        // A step without a question: the click returns once its operation has ended.
        Give(undo, enabled: true, "Undo commit \"Describe more\"", () => repository.Queue.RunAsync(new GitOperation("Undo commit \"Describe more\"", "Undoing…", OperationKind.Local, RefreshScope.Status, async _ =>
        {
            await Task.Delay(200);
            steps.Add("undo");
            return OperationReport.Done("Undid commit \"Describe more\"");
        })));
        Give(redo, enabled: false, "Nothing to redo", () => Task.CompletedTask);
        Assert.Equal(UndoRedoButtons.CanUndo("commit \"Describe more\""), app.UndoRedoState());

        await app.ClickUndoAsync();
        Assert.Equal(["undo"], steps);
        Assert.Null(repository.Queue.Current);

        // A disabled button is not clicked: the check that clicks it expects it to act.
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => app.ClickRedoAsync());
        Assert.Contains("Nothing to redo", refused.Message, StringComparison.Ordinal);
        Assert.Equal(["undo"], steps);

        // A step that changes a remote asks first, after a git read (a tag's commit, 7.5): the
        // queue is idle all along, and the click returns with the question open.
        Give(undo, enabled: false, "Nothing to undo", () => Task.CompletedTask);
        Give(redo, enabled: true, "Redo push of tag v1.0 to origin", async () =>
        {
            await repository.Repository.ResolveAsync("v1.0^{commit}");
            await Task.Delay(200);
            if (await repository.Operations.ConfirmAsync(new ConfirmationRequest("Push tag v1.0 to origin again?", "origin gets tag v1.0 back at 88d6b91.", string.Empty, "Redo", IsDestructive: false)))
            {
                steps.Add("redo");
            }
        });
        Assert.Equal(UndoRedoButtons.CanRedo("push of tag v1.0 to origin"), app.UndoRedoState());
        await app.ClickRedoAsync();
        Assert.Equal("Push tag v1.0 to origin again?", app.Dialog<ConfirmationViewModel>().Title);
        app.Click(app.FindByAutomationId("CancelButton"));
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is null, "the question to close");
        Assert.Equal(["undo"], steps);

        // Without a tooltip, as an empty tab's buttons are.
        ToolTip.SetTip(undo, null);
        Assert.Null(app.UndoRedoState().UndoToolTip);
        Assert.Equal(new UndoRedoButtons(false, "Nothing to undo", false, "Nothing to redo"), UndoRedoButtons.Nothing);
    }

    /// <summary>Gives a toolbar button a state of the test's: enabled or not through its command, a tooltip, the step a click starts.</summary>
    private static void Give(Button button, bool enabled, string toolTip, Func<Task> click)
    {
        button.IsEnabled = true;
        button.Command = new AsyncRelayCommand(click, () => enabled);
        ToolTip.SetTip(button, toolTip);
    }
}
