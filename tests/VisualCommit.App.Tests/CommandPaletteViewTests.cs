using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The command palette in the window (docs/phase-6-design.md 5.2, 5.3, 5.7 to 5.11, 5.12, D212):
/// its key, its card and box where 5.3 puts them, its keys and pointer, its rows' colours and UI
/// Automation, running an entry after it has closed, cancelling, its log lines, the repositories
/// it opens, and its items built again after the worktrees' read. Headless, on phase 3's "ops".
/// </summary>
public class CommandPaletteViewTests
{
    private const string MainWork = "88d6b91e821a43cd39c94aa7892c8095b628caf8";

    /// <summary>The point in "Main work"'s Message column (OPS0's row 3).</summary>
    private static readonly Point MainWorkRow = new(484, 207);

    [AvaloniaTheory]
    [InlineData(1100, 700, 250)]
    [InlineData(1920, 1080, 660)]
    public async Task Its_key_opens_it_with_the_focus_in_its_box_and_its_card_where_5_3_puts_it(int width, int height, double left)
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: width, height: height);
        await app.WaitForRepositoryAsync();
        app.Click(MainWorkRow);
        var mark = app.LogMark();

        app.PressPalette();
        Assert.True(app.IsPaletteOpen);
        var snapshot = app.PaletteSnapshot();

        // 5.3: 600 wide at the top centre, 8 below the toolbar, 12 rows of 28 (392 high); the box 9
        // inside it, 32 high; the list from 6 below the box.
        Assert.Equal(new Rect(left, 96, 600, 392), snapshot.CardBounds);
        Assert.Equal(new Rect(left + 9, 105, 582, 32), snapshot.BoxBounds);
        Assert.Equal(new Rect(left + 9, 143, 582, 28), snapshot.Rows[0].Bounds);
        Assert.Equal(12, snapshot.Rows.Count(row => row.Bounds.Bottom <= 488 + 0.5));
        Assert.Equal(string.Empty, snapshot.Query);
        Assert.Same(app.PaletteBox, app.FocusedElement);
        Assert.Equal("Search commands, branches, tags, stashes, remotes, repositories and workspaces", app.PaletteBox.PlaceholderText);
        Assert.Equal("Search commands", AutomationProperties.GetName(app.PaletteBox));
        Assert.Equal("Command palette", AutomationProperties.GetName(app.PaletteView));
        Assert.Equal("Commands", AutomationProperties.GetName(app.FindByAutomationId(app.PaletteView, "CommandPaletteList")));

        // No dimming: the backdrop is transparent.
        var backdrop = app.PaletteView.GetVisualDescendants().OfType<Panel>().First(panel => panel.Classes.Contains("paletteBackdrop"));
        Assert.Equal(Colors.Transparent, ((ISolidColorBrush)backdrop.Background!).Color);

        // One log line for the opening (5.9).
        Assert.Equal(["ops: Command palette opened"], app.LogLinesSince(mark).Where(line => line.Contains("Command palette", StringComparison.Ordinal)).Select(Message));
        TestPictures.Save(app, $"wpk-palette-commands-{width}x{height}");

        // A second Mod+Shift+P selects the box's text (KD20); no second opening line.
        app.TypeInPalette("check out");
        app.PressPalette();
        Assert.Equal((0, 9), (app.PaletteBox.SelectionStart, app.PaletteBox.SelectionEnd));
        Assert.True(app.IsPaletteOpen);
        Assert.Equal(1, app.LogLinesSince(mark).Count(line => line.Contains("Command palette opened", StringComparison.Ordinal)));

        // Esc cancels and gives the focus back to the graph.
        app.PressKey(Key.Escape);
        Assert.False(app.IsPaletteOpen);
        Assert.Equal("GraphRows", app.FocusedAutomationId());
        Assert.DoesNotContain(app.LogLinesSince(mark), line => line.Contains("Command palette ran", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task The_keys_move_the_highlight_into_view_and_typing_filters()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        app.Click(MainWorkRow);
        app.PressPalette();
        var palette = app.Palette;
        Assert.Equal(33, palette.Shown.Count);

        // PageDown three times: rows 11, 22 and 32 (clamped), each scrolled whole into view.
        foreach (var expected in new[] { 11, 22, 32 })
        {
            app.PressKey(Key.PageDown);
            var snapshot = app.PaletteSnapshot();
            var row = Assert.Single(snapshot.Rows, row => row.IsHighlighted);
            Assert.Equal(palette.Shown[expected].Label, row.Label);
            Assert.True(row.Bounds.Top >= 143 - 0.5 && row.Bounds.Bottom <= 479 + 0.5, $"Row {expected} at {row.Bounds} lies in view.");
        }

        app.PressKey(Key.Down);
        Assert.Equal(0, palette.HighlightedIndex);
        app.PressKey(Key.Up);
        Assert.Equal(32, palette.HighlightedIndex);
        app.PressKey(Key.Up);
        Assert.Equal(31, palette.HighlightedIndex);

        // Tab stays in the box; typing filters, with the first match highlighted.
        app.PressKey(Key.Tab);
        Assert.Same(app.PaletteBox, app.FocusedElement);
        app.TypeInPalette("zzz");
        var nothing = app.PaletteSnapshot();
        Assert.True(nothing.ShowsNothing);
        Assert.Empty(nothing.Rows);
        Assert.Equal(new Rect(250, 96, 600, 84), nothing.CardBounds);
        var empty = (TextBlock)app.FindByAutomationId(app.PaletteView, "CommandPaletteEmpty");
        Assert.Equal("No matches", empty.Text);
        Assert.True(empty.IsEffectivelyVisible);
        Assert.Equal(269, app.BoundsOf(empty).Left, 1);

        app.TypeInPalette("delete main");
        var delete = app.PaletteSnapshot();
        Assert.Equal(
            [("Delete main on origin…", "origin/main", true, true), ("Delete main…", "main", false, false)],
            delete.Rows.Select(row => (row.Label, row.Detail, row.IsEnabled, row.IsHighlighted)));
        Assert.Equal(new Rect(250, 96, 600, 112), delete.CardBounds);

        // Enter on a disabled entry does nothing: the palette stays open (5.7).
        app.PressKey(Key.Up);
        app.PressKey(Key.Enter);
        Assert.True(app.IsPaletteOpen);
        Assert.Equal(1, palette.HighlightedIndex);
        app.PressKey(Key.Escape);
    }

    [AvaloniaFact]
    public async Task Its_rows_draw_an_enabled_a_destructive_and_a_disabled_entry_in_both_themes_and_tell_UI_Automation()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        app.Click(MainWorkRow);
        app.PressPalette();

        foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            app.Window.RequestedThemeVariant = theme;
            app.TypeInPalette("delete main");
            var rows = app.PaletteSnapshot().Rows;
            Assert.Equal(Brush(app, "VcDangerBrush"), rows.Single(row => row.Label == "Delete main on origin…").LabelColour);
            Assert.Equal(Brush(app, "VcTextDisabledBrush"), rows.Single(row => row.Label == "Delete main…").LabelColour);
            Assert.Equal(Brush(app, "VcSelectionBrush"), HighlightColour(app));
            var parts = Row(app, "Delete main…").GetVisualDescendants().OfType<TextBlock>().ToDictionary(block => block.Name!, block => block);
            Assert.Equal("You cannot delete the branch you are on", parts["CommandPaletteItemDetail"].Text);
            Assert.Equal(Brush(app, "VcTextDisabledBrush"), ((ISolidColorBrush)parts["CommandPaletteItemDetail"].Foreground!).Color);
            TestPictures.Save(app, $"wpk-palette-delete-main-{(theme == ThemeVariant.Dark ? "dark" : "light")}");

            app.TypeInPalette("pull from origin/main");
            Assert.Equal(Brush(app, "VcTextPrimaryBrush"), app.PaletteSnapshot().Rows.First(row => row.Label == "Pull from origin/main").LabelColour);
            var enabledParts = Row(app, "Pull from origin/main").GetVisualDescendants().OfType<TextBlock>().ToDictionary(block => block.Name!, block => block);
            Assert.Equal(Brush(app, "VcTextSecondaryBrush"), ((ISolidColorBrush)enabledParts["CommandPaletteItemDetail"].Foreground!).Color);
            Assert.Equal(Brush(app, "VcTextSecondaryBrush"), ((ISolidColorBrush)enabledParts["CommandPaletteItemKey"].Foreground!).Color);
            TestPictures.Save(app, $"wpk-palette-pull-{(theme == ThemeVariant.Dark ? "dark" : "light")}");
        }

        app.Window.RequestedThemeVariant = ThemeVariant.Dark;
        app.Settle();

        // UI Automation (5.11): Name the label, ItemStatus the detail, HelpText a disabled entry's
        // reason, AcceleratorKey the key, selected while highlighted.
        var pull = Row(app, "Pull from origin/main");
        Assert.Equal(("CommandPaletteItem", "Pull from origin/main", "main", null, "Ctrl+Shift+L"), (AutomationProperties.GetAutomationId(pull), AutomationProperties.GetName(pull), AutomationProperties.GetItemStatus(pull), AutomationProperties.GetHelpText(pull), AutomationProperties.GetAcceleratorKey(pull)));
        var pullKey = pull.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "CommandPaletteItemKey");
        Assert.Equal(831, app.BoundsOf(pullKey).Right, 1);

        app.TypeInPalette("delete main");
        var deleteMain = Row(app, "Delete main…");
        Assert.Equal(("Delete main…", "main", "You cannot delete the branch you are on"), (AutomationProperties.GetName(deleteMain), AutomationProperties.GetItemStatus(deleteMain), AutomationProperties.GetHelpText(deleteMain)));
        Assert.Equal("You cannot delete the branch you are on", ToolTip.GetTip(deleteMain));
        Assert.True(Row(app, app.Palette.Highlighted!.Label).IsSelected);

        // A row without a key has no key column: the detail runs to the row's padding (5.3).
        var keyless = Row(app, "Delete main on origin…");
        var key = keyless.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "CommandPaletteItemKey");
        Assert.False(key.IsVisible);
        var detail = keyless.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "CommandPaletteItemDetail");
        Assert.Equal(831, app.BoundsOf(detail).Right, 1);
        var label = keyless.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "CommandPaletteItemLabel");
        Assert.Equal(269, app.BoundsOf(label).Left, 1);
        Assert.Equal(app.BoundsOf(label).Right + 8, app.BoundsOf(detail).Left, 1);
        app.PressKey(Key.Escape);
    }

    [AvaloniaFact]
    public async Task An_entry_runs_after_the_palette_has_closed_and_a_dialog_it_opens_gives_the_focus_back_to_the_graph()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        app.Click(MainWorkRow);
        var mark = app.LogMark();

        app.PressPalette();
        app.TypeInPalette("create branch");
        Assert.Equal("Create branch…", app.Palette.Highlighted?.Label);
        TestPictures.Save(app, "wpk-palette-create-branch");
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is NameDialogViewModel, "the name dialog");
        Assert.False(app.IsPaletteOpen);
        Assert.Equal("Create branch", app.Session.Services.DialogHost.Current!.Title);
        Assert.Equal(
            ["ops: Command palette opened", "ops: Command palette ran: Create branch…"],
            app.LogLinesSince(mark).Where(line => line.Contains("Command palette", StringComparison.Ordinal)).Select(Message));

        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.Session.Services.DialogHost.IsOpen, "the name dialog to close");
        Assert.Equal("GraphRows", app.FocusedAutomationId());

        // The session's run comes first in an empty palette (5.6).
        app.PressPalette();
        Assert.Equal("Create branch…", app.Palette.Shown[0].Label);

        // A press outside the card cancels, handled: the graph's selection stays.
        app.Click(new Point(480, 600));
        Assert.False(app.IsPaletteOpen);
        Assert.Equal(MainWork, app.Session.Shell.ActiveTab.Repository!.Selection.Single);
        Assert.Equal("GraphRows", app.FocusedAutomationId());
        Assert.Equal(1, app.LogLinesSince(mark).Count(line => line.Contains("Command palette ran", StringComparison.Ordinal)));
    }

    [AvaloniaFact]
    public async Task The_pointer_highlights_a_row_and_a_click_runs_an_enabled_one_and_not_a_disabled_one()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        app.Click(MainWorkRow);
        app.PressPalette();
        app.TypeInPalette("delete main");

        // Row 1 (the disabled one): the move highlights it, the click runs nothing.
        app.MoveMouse(new Point(550, 185));
        Assert.Equal(1, app.Palette.HighlightedIndex);
        app.Click(new Point(550, 185));
        Assert.True(app.IsPaletteOpen);
        Assert.Same(app.PaletteBox, app.FocusedElement);

        // A click on an enabled one runs it: "Show reflog".
        app.TypeInPalette("show reflog");
        Assert.Equal("Show reflog", app.Palette.Shown[0].Label);
        app.Click(new Point(550, 157));
        await app.WaitForAsync(() => !app.IsPaletteOpen && app.Session.Shell.ActiveTab.Repository!.Page is not null, "the reflog page");
    }

    [AvaloniaFact]
    public async Task It_declines_while_a_menu_or_another_dialog_is_open()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();

        app.RightClick(new Point(484, 360));
        app.Menu();
        app.PressPalette();
        Assert.False(app.IsPaletteOpen);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.Session.Shell.Menu.IsOpen, "the menu to close");

        app.Session.Shell.ActiveTab.Toolbar.BranchCommand.Execute(null);
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is NameDialogViewModel, "the name dialog");
        app.PressPalette();
        Assert.False(app.IsPaletteOpen);
        Assert.IsAssignableFrom<NameDialogViewModel>(app.Session.Services.DialogHost.Current);
        app.PressKey(Key.Escape);
    }

    [AvaloniaFact]
    public async Task It_opens_a_repository_in_its_tab_or_the_empty_tab_or_a_new_one_and_a_cancelled_dialog_makes_none()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var other = await Scenarios.LinearAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path, null], activeTab: 1);
        await app.WaitUntilReadyAsync();
        var shell = app.Session.Shell;
        await app.WaitForAsync(() => shell.Tabs[0].Repository is not null, "ops to open");

        // 5.10's first case: the tab that shows the worktree is activated; nothing opens again.
        await shell.OpenRepositoryAtAsync(repo.Path);
        Assert.Same(shell.Tabs[0], shell.ActiveTab);
        Assert.Equal(2, shell.Tabs.Count);

        // A cancelled folder dialog makes no tab (Open and Init).
        app.Folders.Answer(null);
        await shell.OpenRepositoryAsync();
        app.Folders.Answer(null);
        await shell.InitRepositoryAsync();
        Assert.Equal(2, shell.Tabs.Count);
        Assert.Equal(["Open a repository", "Choose a folder for the new repository"], app.Folders.Titles.TakeLast(2));

        // The second case: the active empty tab opens it.
        shell.ActivateTabCommand.Execute(shell.Tabs[1]);
        app.Folders.Answer(other.Path);
        await shell.OpenRepositoryAsync();
        await app.WaitForRepositoryAsync();
        Assert.Equal((2, "linear"), (shell.Tabs.Count, shell.ActiveTab.Title));

        // The third: a new tab at the end (Clone, which skips the first step, likewise).
        shell.CloneRepository();
        app.Settle();
        Assert.Equal(3, shell.Tabs.Count);
        Assert.Same(shell.Tabs[2], shell.ActiveTab);
        Assert.True(shell.ActiveTab.Welcome.IsCloneFormOpen);
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CloneUrlBox", "the focus in the clone form's URL box");
    }

    [AvaloniaFact]
    public async Task The_items_are_built_again_only_when_the_worktrees_read_finds_others()
    {
        using var repo = await Scenarios.OperationsAsync();

        // feature/alpha one behind its upstream, so that its fast-forward is enabled unless another
        // worktree has it checked out (phase 3 design 2.7).
        await repo.GitAsync("branch", "-f", "feature/alpha", "feature/alpha~1");
        using var worktree = new TempDirectory("wt");
        await repo.GitAsync("worktree", "add", Path.Combine(worktree.Path, "alpha"), "feature/alpha");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        var shell = app.Session.Shell;

        // The first opening builds the items with no worktree read yet: the fast-forward enabled;
        // the read finds the other worktree, so the items are built again with it disabled.
        Assert.Contains(shell.Commands.PaletteItems(), entry => entry is { Label: "Fast-forward feature/alpha to origin/feature/alpha", IsEnabled: true });
        _ = shell.OpenPaletteAsync("fast-forward feature/alpha");
        app.Settle();
        var palette = app.Palette;
        await shell.PaletteWorktreeRead;
        app.Settle();
        Assert.True(palette.Shown is [{ IsEnabled: false }], "Built again after the read, it is disabled.");
        Assert.Equal("fast-forward feature/alpha", palette.Query);
        Assert.Equal(0, palette.HighlightedIndex);
        Assert.StartsWith("feature/alpha is checked out in another worktree", palette.Shown[0].DisabledReason, StringComparison.Ordinal);
        app.PressKey(Key.Escape);

        // The second opening builds them with that worktree, and its read finds the same: nothing replaced.
        var calls = app.GitCalls().Count;
        _ = shell.OpenPaletteAsync("fast-forward feature/alpha");
        app.Settle();
        var second = app.Palette;
        var resets = 0;
        second.Shown.CollectionChanged += (_, _) => resets++;
        await shell.PaletteWorktreeRead;
        app.Settle();
        Assert.Contains(app.GitCalls().Skip(calls), call => call.CommandText.Contains("worktree list --porcelain", StringComparison.Ordinal));
        Assert.Equal(0, resets);
        Assert.False(second.Shown[0].IsEnabled);
        app.PressKey(Key.Escape);
    }

    /// <summary>A log line's message, without its time and level (<c>yyyy-MM-dd HH:mm:ss.fff zzz [LVL] message</c>).</summary>
    private static string Message(string line) => Regex.Replace(line, @"^.*?\[\w+\] ", string.Empty);

    private static ListBoxItem Row(ShellDriver app, string label) =>
        ((ListBox)app.FindByAutomationId(app.PaletteView, "CommandPaletteList")).GetRealizedContainers().OfType<ListBoxItem>().First(item => (item.DataContext as PaletteEntry)?.Label == label);

    private static Color HighlightColour(ShellDriver app)
    {
        var row = ((ListBox)app.FindByAutomationId(app.PaletteView, "CommandPaletteList")).GetRealizedContainers().OfType<ListBoxItem>().First(item => item.IsSelected);
        var presenter = row.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First(part => part.Name == "PART_ContentPresenter");
        return ((ISolidColorBrush)presenter.Background!).Color;
    }

    private static Color Brush(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");
}
