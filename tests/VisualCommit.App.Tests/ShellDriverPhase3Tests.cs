using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Phase 3's additions to the headless driver (docs/phase-3-design.md 7.4) and the fakes it
/// injects: what the checks of phase 3 lean on and nothing else proves, so that a check that
/// fails points at the app and not at the harness.
/// </summary>
public class ShellDriverPhase3Tests
{
    [AvaloniaFact]
    public async Task Start_gives_the_app_the_fake_launcher_and_clipboard_and_they_record_what_it_asked()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();

        Assert.Same(app.Launcher, app.Session.Services.Launcher);
        Assert.Same(app.Clipboard, app.Session.Services.Clipboard);
        Assert.Null(app.Clipboard.Text);

        var file = Path.Combine(data.Path, "src", "a file.txt");
        await app.Session.Services.Launcher!.OpenAsync(file);
        await app.Session.Services.Launcher.RevealAsync(data.Path);
        await app.Session.Services.Clipboard!.SetTextAsync("88d6b91");
        await app.Session.Services.Clipboard.SetTextAsync("main");

        Assert.Equal([new FileLaunch(FileLaunchKind.Open, file), new FileLaunch(FileLaunchKind.Reveal, data.Path)], app.Launcher.Requests);
        Assert.Equal([file], app.Launcher.Opened);
        Assert.Equal([data.Path], app.Launcher.Revealed);
        Assert.Equal(["88d6b91", "main"], app.Clipboard.Texts);
        Assert.Equal("main", app.Clipboard.Text);
    }

    [AvaloniaFact]
    public async Task Start_hands_a_prompt_handler_to_the_app_when_given_one()
    {
        using var data = new TempDirectory("data");
        var prompts = new ScriptedPromptHandler();
        using var app = ShellDriver.Start(data.Path, promptHandler: prompts);
        await app.WaitUntilReadyAsync();

        // The session's broker asks the handler, not the dialogs: the real helper, run as git
        // runs it in an open scope, gets the handler's answer, and no dialog opens.
        prompts.Answer("alice");
        string? environmentAnswer;
        using (var scope = app.Session.Services.Prompts.Begin("Fetch", "auth", PromptMode.Ask))
        {
            var environment = scope.Environment;
            var run = Task.Run(() => AskPassRunner.RunAsync(environment, "Username for 'http://127.0.0.1': ", cancellationToken: TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
            await app.WaitForAsync(() => run.IsCompleted, "the helper to end", TimeSpan.FromSeconds(60));
            environmentAnswer = (await run).Answer;
        }

        Assert.Equal("alice", environmentAnswer);
        var asked = Assert.Single(prompts.Prompts);
        Assert.Equal((GitPromptKind.Username, "Fetch", "auth"), (asked.Kind, asked.Title, asked.RepositoryName));
        Assert.False(app.Session.Services.DialogHost.IsOpen);
    }

    [AvaloniaFact]
    public async Task FindByAutomationId_looks_in_the_dialog_on_top_first_then_in_the_window()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var dialogs = app.Session.Services.Dialogs;
        Assert.Null(app.TopDialogView);

        // Two confirmations stacked (D104): the one below comes first in the visual tree.
        var below = dialogs.ConfirmAsync(new ConfirmationRequest("Delete branch?", "Delete feature/alpha?", string.Empty, "Delete"));
        var top = dialogs.ConfirmAsync(new ConfirmationRequest("ssh asks", "Allow use of key?", string.Empty, "Yes", IsDestructive: false));
        app.Settle();

        var view = Assert.IsAssignableFrom<Control>(app.TopDialogView);
        Assert.Equal("ConfirmationDialog", AutomationProperties.GetAutomationId(view));
        Assert.Same(app.Session.Services.DialogHost.Current, view.DataContext);
        Assert.Equal("Yes", ((Button)app.FindByAutomationId("ConfirmButton")).Content);
        Assert.Equal("ssh asks", ((TextBlock)app.FindByAutomationId("ConfirmTitle")).Text);
        Assert.Same(app.FindByAutomationId("ConfirmButton"), app.FindByAutomationId(view, "ConfirmButton"));

        // What is not in the dialog is still found in the window behind it.
        Assert.Equal("Ready", ((TextBlock)app.FindByAutomationId("OperationStatus")).Text);
        Assert.Throws<InvalidOperationException>(() => app.FindByAutomationId(view, "OperationStatus"));

        app.Click(app.FindByAutomationId("CancelButton"));
        Assert.False(await top);
        Assert.Equal("Delete", ((Button)app.FindByAutomationId("ConfirmButton")).Content);
        app.Click(app.FindByAutomationId("ConfirmButton"));
        Assert.True(await below);
        Assert.Null(app.TopDialogView);
    }

    [AvaloniaFact]
    public async Task OpenMenuView_is_the_open_menu_and_FindByAutomationId_looks_in_it()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        Assert.Null(app.OpenMenuView);

        app.Session.Shell.Menu.Open(new MenuModel([new MenuHeader("Tags"), new VisualCommit.App.ViewModels.Menus.MenuItem("Push v1.0 to origin", () => { })]), new MenuPosition(300, 450));
        app.Menu();

        var menu = Assert.IsAssignableFrom<Control>(app.OpenMenuView);
        var header = app.FindByAutomationId(MenuLayer.HeaderAutomationId);
        Assert.Same(header, app.FindByAutomationId(menu, MenuLayer.HeaderAutomationId));
        Assert.Equal("Tags", AutomationProperties.GetName(header));

        Assert.True(app.Session.Shell.Menu.Close());
        Assert.Null(app.OpenMenuView);
    }

    [AvaloniaFact]
    public async Task A_click_with_modifiers_carries_them_on_the_press_and_the_release()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var seen = RecordPointer(app.Window);
        var point = new Point(500, 650);

        app.Click(point, KeyModifiers.Control | KeyModifiers.Shift);
        app.Click(point, app.CommandModifier);
        app.Click(point);

        Assert.Equal(
            [
                ("pressed", KeyModifiers.Control | KeyModifiers.Shift),
                ("released", KeyModifiers.Control | KeyModifiers.Shift),
                ("pressed", app.CommandModifier),
                ("released", app.CommandModifier),
                ("pressed", KeyModifiers.None),
                ("released", KeyModifiers.None),
            ],
            seen.Where(e => e.Kind != "moved").Select(e => (e.Kind, e.Modifiers)));
    }

    [AvaloniaFact]
    public async Task The_command_modifier_is_the_platforms_own()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();

        var platform = app.Window.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.None;

        Assert.NotEqual(KeyModifiers.None, app.CommandModifier);
        Assert.Equal(platform == KeyModifiers.None ? KeyModifiers.Control : platform, app.CommandModifier);
    }

    [AvaloniaFact]
    public async Task A_drag_in_steps_holds_the_button_and_keeps_the_capture_and_dropping_off_a_button_does_not_click_it()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var open = app.FindByAutomationId("OpenRepoButton");
        var start = app.BoundsOf(open).Center;
        var away = new Point(start.X + 300, start.Y + 200);

        app.PressMouse(start);
        var seen = RecordPointer(app.Window);
        app.MoveMouseHeld(away, steps: 4);

        // In the middle of the drag: the button is held, and the press's target (the button or
        // a part of it) keeps the pointer although the mouse has left it.
        var moves = seen.Where(e => e.Kind == "moved").ToList();
        Assert.Equal(4, moves.Count);
        Assert.All(moves, move => Assert.True(move.LeftPressed, "A move of the drag came without the button held."));
        Assert.All(moves, move => Assert.True(
            move.Captured is Visual captured && (ReferenceEquals(captured, open) || open.IsVisualAncestorOf(captured)),
            $"The pointer is captured by {move.Captured?.GetType().Name ?? "nothing"}, not by the pressed button."));
        Assert.Equal(away, moves[^1].Position);

        app.ReleaseMouse(away);

        Assert.Equal("released", seen[^1].Kind);
        Assert.Empty(app.Folders.Titles);

        // A plain click on the same button does click it: the folder dialog is asked for.
        app.Click(open);
        await app.WaitForAsync(() => app.Folders.Titles.Count == 1, "the folder dialog to be asked for");
    }

    [AvaloniaFact]
    public async Task Dialog_returns_the_dialog_on_top_and_says_what_is_there_otherwise()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();

        var none = Assert.Throws<InvalidOperationException>(() => app.Dialog<ConfirmationViewModel>());
        Assert.Contains("No dialog is open", none.Message, StringComparison.Ordinal);

        var answer = app.Session.Services.Dialogs.ConfirmAsync(new ConfirmationRequest("Delete branch?", "Delete feature/alpha?", string.Empty, "Delete"));
        var dialog = await app.WaitForDialogAsync<ConfirmationViewModel>();

        Assert.Same(dialog, app.Dialog<ConfirmationViewModel>());
        Assert.Equal("Delete branch?", dialog.Title);
        var wrong = Assert.Throws<InvalidOperationException>(() => app.Dialog<FinishingDialogViewModel>());
        Assert.Contains("ConfirmationViewModel (\"Delete branch?\")", wrong.Message, StringComparison.Ordinal);

        dialog.ConfirmCommand.Execute(null);
        Assert.True(await answer);
    }

    [AvaloniaFact]
    public async Task TypeInto_focuses_the_box_and_types_after_what_it_holds()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        app.Click(app.FindByAutomationId("CloneRepoButton"));

        app.TypeInto("CloneUrlBox", "https://example.com/");
        app.TypeInto("CloneParentBox", "parent");
        app.TypeInto("CloneUrlBox", "repo.git");

        var url = (TextBox)app.FindByAutomationId("CloneUrlBox");
        Assert.Equal("https://example.com/repo.git", url.Text);
        Assert.Equal("parent", ((TextBox)app.FindByAutomationId("CloneParentBox")).Text);
        Assert.True(url.IsFocused);
        Assert.Throws<InvalidOperationException>(() => app.TypeInto("CloneRepoButton", "x"));
    }

    [AvaloniaFact]
    public async Task WaitForIdleAsync_waits_for_the_queue_and_the_refresh_after_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        var graphBefore = repository.Graph;
        await app.WaitForIdleAsync();

        var gate = new TaskCompletionSource();
        var running = repository.Queue.RunAsync(new GitOperation("Create branch idle", "Creating branch idle…", OperationKind.Local, RefreshScope.Refs, async _ =>
        {
            await gate.Task;
            await repo.GitAsync("branch", "idle", "HEAD");
            return OperationReport.Done("Created branch idle");
        }));

        await Assert.ThrowsAsync<TimeoutException>(() => app.WaitForIdleAsync(timeout: TimeSpan.FromMilliseconds(300)));
        Assert.False(running.IsCompleted);

        gate.SetResult();
        await app.WaitForIdleAsync(repository);

        Assert.True(running.IsCompleted);
        Assert.Equal(OperationOutcome.Succeeded, repository.Queue.LastResult?.Outcome);

        // The refresh the operation named has run: the new branch changed the refs, so a new graph replaced the old.
        Assert.NotSame(graphBefore, repository.Graph);
        Assert.NotNull(await repo.RefTargetAsync("refs/heads/idle"));
    }

    [AvaloniaFact]
    public async Task WaitForIdleAsync_needs_a_repository()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => app.WaitForIdleAsync());
    }

    /// <summary>Records the pointer events that reach the window, handled ones included.</summary>
    private static List<PointerRecord> RecordPointer(Window window)
    {
        var seen = new List<PointerRecord>();
        window.AddHandler(InputElement.PointerPressedEvent, (_, e) => seen.Add(Record("pressed", e, window)), RoutingStrategies.Tunnel, handledEventsToo: true);
        window.AddHandler(InputElement.PointerMovedEvent, (_, e) => seen.Add(Record("moved", e, window)), RoutingStrategies.Tunnel, handledEventsToo: true);
        window.AddHandler(InputElement.PointerReleasedEvent, (_, e) => seen.Add(Record("released", e, window)), RoutingStrategies.Tunnel, handledEventsToo: true);
        return seen;
    }

    private static PointerRecord Record(string kind, PointerEventArgs e, Window window) =>
        new(kind, e.KeyModifiers, e.GetCurrentPoint(window).Properties.IsLeftButtonPressed, e.Pointer.Captured, e.GetPosition(window));

    private sealed record PointerRecord(string Kind, KeyModifiers Modifiers, bool LeftPressed, IInputElement? Captured, Point Position);
}
