using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The main window's hooks for phase 3 (docs/phase-3-design.md section 8): the ordered Esc chain,
/// Shift+F10 and the Closing hook that waits for running operations (1.8).
/// </summary>
public class MainWindowHooksTests
{
    [AvaloniaFact]
    public async Task Esc_goes_to_the_handlers_in_their_order_until_one_takes_it()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var asked = new List<string>();
        var menuOpen = true;
        using var menu = app.Window.AddEscapeHandler(
            new EscapeHandler(() =>
            {
                asked.Add("menu");
                var was = menuOpen;
                menuOpen = false;
                return was;
            }),
            EscapeOrder.Menu);
        using var late = app.Window.AddEscapeHandler(new EscapeHandler(() => { asked.Add("late"); return false; }), EscapeOrder.Diff + 1);
        var confirming = app.Session.Shell.Dialogs.ConfirmAsync(new ConfirmationRequest("Discard changes?", "Discard?", string.Empty, "Discard"));
        app.Settle();

        app.PressKey(Key.Escape);
        Assert.False(confirming.IsCompleted, "An open menu takes Esc before the dialog.");

        app.PressKey(Key.Escape);
        Assert.False(await confirming);

        app.PressKey(Key.Escape);
        Assert.Equal(["menu", "menu", "menu", "late"], asked);
    }

    [AvaloniaFact]
    public async Task Shift_F10_asks_the_focused_element_for_its_context_menu()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var filter = app.Find<TextBox>("FilterBox");
        filter.Focus();
        app.Settle();
        Assert.False(filter.ContextFlyout!.IsOpen);

        app.PressKey(Key.F10, RawInputModifiers.Shift);

        Assert.True(filter.ContextFlyout.IsOpen);
        filter.ContextFlyout.Hide();
    }

    [AvaloniaFact]
    public async Task Closing_the_window_while_an_operation_runs_waits_for_it_and_then_closes()
    {
        using var repo = await Scenarios.LinearAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var gate = new TaskCompletionSource();
        var running = repository.Queue.RunAsync(new GitOperation("Merge feature", "Merging…", OperationKind.Local, RefreshScope.None, async _ =>
        {
            await gate.Task;
            return OperationReport.Done("Merged");
        })
        { HasSeveralSteps = true });
        var waiting = repository.Queue.RunAsync(new GitOperation("Stage", "Staging…", OperationKind.Local, RefreshScope.None, _ => throw new InvalidOperationException("Must not run.")));

        app.Window.Close();
        app.Settle();

        Assert.True(app.Window.IsVisible, "The window waits for the running operation.");
        var finishing = Assert.IsType<FinishingDialogViewModel>(app.Session.Shell.Dialogs.Current);
        Assert.Equal([new FinishingItem($"Merge feature — {repository.Name}", true)], finishing.Items);
        Assert.Equal(OperationOutcome.Cancelled, (await waiting).Outcome);

        // Closing again while it waits opens no second dialog.
        app.Window.Close();
        app.Settle();
        Assert.Single(app.Session.Shell.Dialogs.Open);

        gate.SetResult();
        Assert.Equal(OperationOutcome.Succeeded, (await running).Outcome);
        await app.WaitForAsync(() => !app.Window.IsVisible, "the window to close by itself");
        Assert.True(finishing.IsClosed);
    }

    [AvaloniaFact]
    public async Task Keep_open_leaves_the_window_and_Quit_now_closes_it_at_once()
    {
        using var repo = await Scenarios.LinearAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var gate = new TaskCompletionSource();
        var running = repository.Queue.RunAsync(new GitOperation("Fetch", "Fetching…", OperationKind.Network, RefreshScope.None, async context =>
        {
            await gate.Task.WaitAsync(context.CancellationToken);
            return OperationReport.Done("Fetched");
        }));

        app.Window.Close();
        app.Settle();
        app.PressKey(Key.Escape);
        Assert.True(app.Window.IsVisible, "Keep open (Esc) leaves the window open.");
        Assert.False(app.Session.Shell.Dialogs.IsOpen);

        app.Window.Close();
        app.Settle();
        var finishing = Assert.IsType<FinishingDialogViewModel>(app.Session.Shell.Dialogs.Current);
        finishing.QuitNowCommand.Execute(null);
        await app.WaitForAsync(() => !app.Window.IsVisible, "the window to close");

        // The session ends with the window: closing the tabs stops the network operation.
        app.Session.Shell.CloseAll();
        Assert.Equal(OperationOutcome.Cancelled, (await running).Outcome);
    }
}
