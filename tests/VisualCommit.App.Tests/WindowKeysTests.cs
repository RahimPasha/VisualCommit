using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Conflicts;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The window's keys for the toolbar (docs/phase-5-design.md 1.3, D163): the platform's undo and
/// every gesture of its redo are the toolbar's Undo and Redo, unless a dialog or a menu is open or
/// the focus is in a text box or the conflict resolver, which keep their own undo; the command
/// modifier with F is Search, unless a dialog or a menu is open. What Undo, Redo and Search do is
/// faked (<see cref="ToolbarViewModel.Actions"/>, <see cref="ToolbarViewModel.UndoStates"/>,
/// <see cref="ToolbarViewModel.SearchAction"/>); the gestures are the headless platform's own,
/// read through the driver as the window reads them.
/// </summary>
public class WindowKeysTests
{
    [AvaloniaFact]
    public async Task In_the_graph_the_undo_key_and_every_redo_gesture_run_the_toolbars_Undo_and_Redo()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var toolbar = await OpenAsync(app, canUndo: true, canRedo: true);
        app.Click(app.GraphRowPoint(1));
        Assert.IsType<CommitGraphControl>(app.Window.FocusManager?.GetFocusedElement());

        // Every gesture of the platform's redo list redoes (AD10): Ctrl+Y and Ctrl+Shift+Z on
        // Windows and in the headless platform, as Avalonia 12.1.3 gives them.
        var redoGestures = app.RedoGestures.Count;
        Assert.True(redoGestures >= 1, "The platform names no redo gesture.");
        if (app.CommandModifier == KeyModifiers.Control)
        {
            Assert.Equal("Ctrl+Z", app.UndoGesture.ToString());
            Assert.Equal(["Ctrl+Y", "Ctrl+Shift+Z"], app.RedoGestures.Select(gesture => gesture.ToString()));
        }

        app.PressGitUndo();
        for (var gesture = 0; gesture < redoGestures; gesture++)
        {
            app.PressGitRedo(gesture);
        }

        await app.WaitForAsync(() => Actions(toolbar).Calls.Count == 1 + redoGestures, "the calls");
        Assert.Equal(["undo", .. Enumerable.Repeat("redo", redoGestures)], Actions(toolbar).Calls);
        Assert.Equal(1, app.Session.Shell.ActiveTab.Repository!.SelectedIndex);
    }

    [AvaloniaFact]
    public async Task A_disabled_Undo_or_Redo_takes_its_key_and_does_nothing()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var toolbar = await OpenAsync(app, canUndo: false, canRedo: false);
        app.Click(app.GraphRowPoint(1));
        var keyDowns = new List<bool>();
        app.Window.AddHandler(InputElement.KeyDownEvent, (_, e) => keyDowns.Add(e.Handled), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);

        app.PressGitUndo();
        app.PressGitRedo(app.RedoGestures.Count - 1);

        Assert.Empty(Actions(toolbar).Calls);
        Assert.Equal([true, true], keyDowns);
    }

    [AvaloniaFact]
    public async Task In_a_text_box_the_keys_are_its_own_text_undo_also_with_nothing_left_to_undo()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var toolbar = await OpenAsync(app, canUndo: true, canRedo: true);
        var filter = app.Find<TextBox>("FilterBox");
        app.Click(filter);
        app.Type("main");
        Assert.Equal("main", filter.Text);

        app.Undo();
        Assert.True(string.IsNullOrEmpty(filter.Text));
        app.Undo();
        app.Redo();
        app.PressGitRedo(app.RedoGestures.Count - 1);

        Assert.Same(filter, app.Window.FocusManager?.GetFocusedElement());
        Assert.Empty(Actions(toolbar).Calls);
    }

    [AvaloniaFact]
    public async Task While_a_menu_or_a_dialog_is_open_the_keys_go_on_as_before()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var toolbar = await OpenAsync(app, canUndo: true, canRedo: true);
        var searches = 0;
        toolbar.SearchAction = () => searches++;

        app.RightClick(app.GraphRowPoint(1));
        Assert.True(app.IsMenuOpen);
        app.PressGitUndo();
        app.PressGitRedo(0);
        app.PressSearch();
        Assert.True(app.IsMenuOpen);
        app.PressKey(Key.Escape);
        Assert.False(app.IsMenuOpen);

        var answer = app.Session.Shell.Dialogs.ConfirmAsync(new ConfirmationRequest("Delete feature/alpha?", "Delete it?", string.Empty, "Delete"));
        app.Settle();
        Assert.True(app.Session.Shell.Dialogs.IsOpen);
        app.PressGitUndo();
        app.PressGitRedo(app.RedoGestures.Count - 1);
        app.PressSearch();
        app.PressKey(Key.Escape);
        Assert.False(await answer);

        Assert.Empty(Actions(toolbar).Calls);
        Assert.Equal(0, searches);
    }

    [AvaloniaFact]
    public async Task The_search_key_runs_Search_wherever_the_focus_is_and_takes_the_key()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var toolbar = await OpenAsync(app, canUndo: false, canRedo: false);
        var searches = 0;
        toolbar.SearchAction = () => searches++;
        var reachedTheBox = 0;
        var filter = app.Find<TextBox>("FilterBox");
        filter.AddHandler(InputElement.KeyDownEvent, (_, e) => reachedTheBox += e.Key == Key.F ? 1 : 0, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: false);

        app.Click(app.GraphRowPoint(1));
        app.PressSearch();
        app.Click(filter);
        app.PressSearch();

        Assert.Equal(2, searches);
        Assert.Equal(0, reachedTheBox);
        Assert.True(string.IsNullOrEmpty(filter.Text), "Ctrl+F typed nothing into the box.");
    }

    [AvaloniaFact]
    public async Task In_the_conflict_resolver_Search_is_disabled_and_the_undo_keys_are_the_resolvers()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var toolbar = await OpenAsync(app, canUndo: true, canRedo: true);
        var searches = 0;
        toolbar.SearchAction = () => searches++;
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Status.Conflicts.Count == 1, "the conflict read", TimeSpan.FromSeconds(30));
        Assert.Equal((true, $"Search commits ({SearchGesture(app)})"), (toolbar.CanSearch, toolbar.SearchToolTip));

        repository.IsWorkingRowSelected = true;
        repository.OpenDiff(Assert.Single(repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted), staged: false);
        await app.WaitForResolverAsync("notes.txt");

        Assert.Equal((false, "Close the conflict resolver first"), (toolbar.CanSearch, toolbar.SearchToolTip));
        var button = (ToolbarButton)app.FindByAutomationId("SearchButton");
        Assert.False(button.IsEffectivelyEnabled);
        Assert.Equal("Close the conflict resolver first", ToolTip.GetTip(button));

        // The focus in the result, as a choice leaves it (phase 4's 11.5), and then on a button of
        // the resolver: the window leaves the keys to the resolver either way.
        app.ClickResultAt(1, 1);
        Assert.NotNull(FocusInResolver(app));
        app.PressSearch();
        app.PressGitUndo();
        app.PressGitRedo(0);
        app.PressGitRedo(app.RedoGestures.Count - 1);
        var close = app.ResolverView.GetVisualDescendants().OfType<Button>().First(candidate => candidate.IsEffectivelyVisible && candidate.Focusable);
        close.Focus();
        Assert.NotNull(FocusInResolver(app));
        app.PressGitUndo();

        Assert.Null(repository.Search);
        Assert.Equal(0, searches);
        Assert.Empty(Actions(toolbar).Calls);
        Assert.True(repository.IsResolverOpen);
    }

    /// <summary>Waits for the repository and gives its toolbar recording actions and the undo states asked for.</summary>
    private static async Task<ToolbarViewModel> OpenAsync(ShellDriver app, bool canUndo, bool canRedo)
    {
        var repository = await app.WaitForRepositoryAsync();
        var toolbar = repository.Toolbar;
        toolbar.Actions = new RecordingToolbarActions();
        toolbar.UndoStates = () => new UndoAvailability(
            canUndo, canUndo ? "Undo commit \"Describe the lines\"" : "Nothing to undo",
            canRedo, canRedo ? "Redo commit \"Describe the lines\"" : "Nothing to redo");
        toolbar.UpdateUndoStates();
        app.Settle();
        return toolbar;
    }

    private static RecordingToolbarActions Actions(ToolbarViewModel toolbar) => (RecordingToolbarActions)toolbar.Actions!;

    /// <summary>Search's key in words on this platform (1.1): Ctrl+F, or Cmd+F where the command modifier is Cmd.</summary>
    private static string SearchGesture(ShellDriver app) => app.CommandModifier == KeyModifiers.Meta ? "Cmd+F" : "Ctrl+F";

    private static Avalonia.Visual? FocusInResolver(ShellDriver app) =>
        app.Window.FocusManager?.GetFocusedElement() is Avalonia.Visual focused && focused.GetSelfAndVisualAncestors().OfType<ConflictResolverView>().Any()
            ? focused
            : null;

    /// <summary>The toolbar's actions, recorded.</summary>
    private sealed class RecordingToolbarActions : IToolbarActions
    {
        public List<string> Calls { get; } = [];

        public Task FetchAllAsync() => Record("fetch");

        public Task PullAsync() => Record("pull");

        public Task PushAsync() => Record("push");

        public Task CreateBranchAtHeadAsync() => Record("branch");

        public Task StashAsync() => Record("stash");

        public Task PopAsync() => Record("pop");

        public Task UndoAsync() => Record("undo");

        public Task RedoAsync() => Record("redo");

        private Task Record(string call)
        {
            Calls.Add(call);
            return Task.CompletedTask;
        }
    }
}
