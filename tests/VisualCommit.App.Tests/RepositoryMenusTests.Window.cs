using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The menus in the window, opened by the input of the visual checks 1 to 7 (the scripted run's
/// mouse and keyboard, headless, 1100×700, dark): the views' wiring of every entry point (a
/// right-click on graph rows, labels, the working-changes row, the empty area, left-panel rows,
/// file rows and the diff's text; Shift+F10 at the focused row; the double-clicks; a drop), where
/// each menu opens and what it selects. With <c>VISUALCOMMIT_TEST_PICTURES</c> set, each menu of
/// checks 1 to 4 is saved as a picture to look at.
/// </summary>
public partial class RepositoryMenusTests
{
    private const int FastForwardMeRow = 2;
    private const int MainWorkRow = 3;
    private const int AlphaTwoRow = 4;
    private const int AddCoreRow = 6;

    [AvaloniaFact]
    public async Task In_the_graph_a_right_click_selects_and_opens_the_menu_of_what_it_is_on_and_Shift_F10_opens_it_at_the_message_cell()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path]);
        var repository = await ShownAsync(app);
        var (actions, recorder) = RecordingActions.Create();
        repository.MenuActions = actions;

        // Check 1 step 1.
        var alphaTwo = app.GraphRowPoint(AlphaTwoRow);
        app.RightClick(alphaTwo);
        var menu = app.Menu();
        Assert.Equal(
            ["ce5ea6a Alpha two", "Check out this commit", "Create branch here…", "Create tag here…", "—", "Cherry-pick onto main", "Revert this commit", "Reset main to this commit…", "—", "Copy SHA"],
            menu.Lines());
        Assert.Equal("Check out this commit", menu.Highlighted?.Label);
        AssertNear(alphaTwo + new Point(2, 2), menu.Bounds.TopLeft);
        Assert.InRange(menu.Bounds.Height, 230, 234);
        Assert.Equal(AlphaTwoRow, repository.SelectedIndex);
        Assert.Equal(MenuOpenedBy.Pointer, app.Session.Shell.Menu.OpenedBy);
        TestPictures.Save(app, "01a-commit-menu");

        // Step 2: the item starts its action on the commit.
        app.ClickMenuItem("Copy SHA");
        Assert.False(app.IsMenuOpen);
        Assert.Equal([$"CopyShaAsync({AlphaTwo})"], recorder.Calls.Select(call => call.ToString()));

        // Step 4: a commit already on main.
        app.RightClick(app.GraphRowPoint(AddCoreRow));
        menu = app.Menu();
        Assert.Equal("8beca34 Add core", menu.Header);
        var pick = menu.Item("Cherry-pick onto main");
        Assert.Equal((false, "This commit is already on main"), (pick.IsEnabled, pick.ToolTip));
        TestPictures.Save(app, "01c-cherry-pick-disabled");
        app.PressKey(Key.Escape);

        // Step 5: the "+1" label opens its commit's menu and selects its row.
        app.RightClick(app.GraphLabelBounds(AlphaTwoRow, "+1").Center);
        Assert.Equal("ce5ea6a Alpha two", app.Menu().Header);
        Assert.Equal(AlphaTwoRow, repository.SelectedIndex);
        app.PressKey(Key.Escape);

        // Check 2 steps 2 and 3: branch labels.
        app.RightClick(app.GraphLabelBounds(FastForwardMeRow, "feature/ff").Center);
        menu = app.Menu();
        Assert.Equal("feature/ff", menu.Header);
        Assert.Equal(FastForwardMeRow, repository.SelectedIndex);
        TestPictures.Save(app, "02b-label-menu-no-upstream");
        app.PressKey(Key.Escape);
        app.RightClick(app.GraphLabelBounds(MainWorkRow, "main").Center);
        menu = app.Menu();
        Assert.Equal("main", menu.Header);
        Assert.Equal("Pull from origin/main", menu.Highlighted?.Label);
        Assert.Equal(MainWorkRow, repository.SelectedIndex);
        TestPictures.Save(app, "02c-current-branch-menu");
        app.PressKey(Key.Escape);

        // Check 5 step 1: Shift+F10 opens the selected row's menu at its message cell's bottom-left.
        app.Click(alphaTwo);
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        menu = app.Menu();
        Assert.Equal("ce5ea6a Alpha two", menu.Header);
        Assert.Equal(MenuOpenedBy.Keyboard, app.Session.Shell.Menu.OpenedBy);
        var graph = app.Find<CommitGraphControl>("GraphRows");
        AssertNear(graph.TranslatePoint(new Point(graph.Columns.Message.X, graph.RowBounds(AlphaTwoRow).Bottom), app.Window)!.Value, menu.Bounds.TopLeft);
        AssertNear(new Point(454, 246), menu.Bounds.TopLeft);
        TestPictures.Save(app, "05a-shift-f10-menu");
        app.PressKey(Key.Down);
        Assert.Equal("Create branch here…", app.Menu().Highlighted?.Label);
        app.PressKey(Key.Escape);
        Assert.False(app.IsMenuOpen);

        // Check 3 step 6: the empty area; the selection stays.
        app.RightClick(new Point(520, 424));
        menu = app.Menu();
        Assert.Equal(
            ["ops", "Fetch all remotes", "Pull", "Push", "—", "Create branch…", "Stash changes… (disabled: \"There are no changes to stash\")", "—", "Manage remotes…"],
            menu.Lines());
        AssertNear(new Point(522, 426), menu.Bounds.TopLeft);
        Assert.Equal(AlphaTwoRow, repository.SelectedIndex);
        TestPictures.Save(app, "03f-empty-area-menu");
        app.PressKey(Key.Escape);

        // The log has the line for every menu (D106).
        var log = string.Concat(Directory.GetFiles(Path.Combine(data.Path, "logs")).Select(File.ReadAllText));
        Assert.Contains("Context menu opened: ce5ea6a Alpha two by pointer", log, StringComparison.Ordinal);
        Assert.Contains("Context menu opened: ce5ea6a Alpha two by keyboard", log, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task In_the_left_panel_a_right_click_opens_the_rows_menu_only_and_Shift_F10_opens_it_at_the_rows_bottom_left()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path]);
        var repository = await ShownAsync(app);

        // Check 2 step 1.
        app.Click(LeftRow(app, row => row.Key == "local:feature"));
        app.RightClick(LeftRow(app, row => row.Key == "ref:refs/heads/feature/alpha"));
        var menu = app.Menu();
        Assert.Equal("feature/alpha", menu.Header);
        Assert.Equal(13, menu.Items.Count);
        Assert.InRange(menu.Bounds.Height, 404, 408);
        Assert.True(repository.Selection.IsEmpty);
        Assert.DoesNotContain(repository.LeftPanel.Rows.OfType<RefItemRow>(), row => row.IsSelected);
        Assert.True(repository.Details.ShowsPlaceholder);
        TestPictures.Save(app, "02a-local-branch-menu");
        app.PressKey(Key.Escape);

        // Step 4: a remote branch.
        app.Click(LeftRow(app, row => row.Key == "remote:origin:feature"));
        app.RightClick(LeftRow(app, row => row.Key == "ref:refs/remotes/origin/feature/alpha"));
        Assert.Equal("origin/feature/alpha", app.Menu().Header);
        TestPictures.Save(app, "02d-remote-branch-menu");
        app.PressKey(Key.Escape);

        // Check 3 steps 1 to 4.
        app.RightClick(LeftRow(app, row => row.Key == "ref:refs/tags/v1.0"));
        Assert.Equal("v1.0", app.Menu().Header);
        TestPictures.Save(app, "03a-tag-menu");
        app.PressKey(Key.Escape);
        app.RightClick(LeftRow(app, row => row.Kind == RefItemKind.Stash));
        Assert.Equal("stash@{0} On main: Experiment", app.Menu().Header);
        TestPictures.Save(app, "03b-stash-menu");
        app.PressKey(Key.Escape);
        app.RightClick(LeftRow(app, row => row.Key == "remote:origin"));
        Assert.Equal("origin", app.Menu().Header);
        TestPictures.Save(app, "03c-remote-menu");
        app.PressKey(Key.Escape);
        app.RightClick(SectionRow(app, RefSection.Stashes));
        var stashes = app.Menu();
        Assert.Equal(["Stashes", "Stash changes… (disabled: \"There are no changes to stash\")"], stashes.Lines());
        Assert.Null(stashes.Highlighted);
        TestPictures.Save(app, "03d-stashes-header-disabled");
        app.PressKey(Key.Escape);
        Assert.True(repository.Selection.IsEmpty);

        // Step 5: no menu for "Pull requests" or a folder; a right-click on a header does not toggle it.
        app.RightClick(SectionRow(app, RefSection.PullRequests));
        Assert.False(app.IsMenuOpen);
        app.RightClick(LeftRow(app, row => row.Key == "local:feature"));
        Assert.False(app.IsMenuOpen);
        Assert.True(((RefItemRow)repository.LeftPanel.Rows.Single(row => row.Key == "local:feature")).IsOpen);

        // Check 5 step 5: Shift+F10 on the clicked main row.
        var main = LeftRow(app, row => row.Key == "ref:refs/heads/main");
        app.Click(main);
        Assert.Equal(MainWorkRow, repository.SelectedIndex);
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        var menu5 = app.Menu();
        Assert.Equal("main", menu5.Header);

        // The row starts at the window's left edge: the menu is moved 4 inside the window (4a).
        var corner = app.BoundsOf(main).BottomLeft;
        AssertNear(new Point(Math.Max(4, corner.X), corner.Y), menu5.Bounds.TopLeft);
        TestPictures.Save(app, "05e-left-panel-shift-f10");
        app.PressKey(Key.Escape);
        Assert.Same(main, app.Window.FocusManager?.GetFocusedElement());
    }

    [AvaloniaFact]
    public async Task Files_have_their_menus_in_the_stage_lists_the_diff_and_a_commits_files_and_the_working_changes_row_its_own()
    {
        using var scenario = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path]);
        var repository = await ShownAsync(app);
        var graph = app.Find<CommitGraphControl>("GraphRows");
        var workingRow = graph.TranslatePoint(new Point(graph.Columns.Message.X + 30, graph.WorkingRowBounds.Center.Y), app.Window)!.Value;

        // Check 4 step 8 first: the working-changes row's menu selects the row.
        app.RightClick(workingRow);
        var menu = app.Menu();
        Assert.Equal(
            ["Working changes", "Stage all", "Unstage all", "Discard all changes… (danger)", "—", "Stash changes…", "Create branch here…", "—", "Show in folder"],
            menu.Lines());
        Assert.True(repository.IsWorkingRowSelected);
        TestPictures.Save(app, "04j-working-row-menu");
        app.PressKey(Key.Escape);

        // Step 1: an unstaged tracked file.
        var calculator = FileRow(app, "UnstagedFileList", "src/Calculator.cs");
        app.RightClick(calculator);
        menu = app.Menu();
        Assert.Equal(
            ["Calculator.cs", "Stage", "Discard changes…", "—", "Ignore Calculator.cs (disabled: \"Only untracked files can be ignored\")", "Ignore all *.cs files (disabled: \"Only untracked files can be ignored\")", "—", "Open file", "Show in folder", "Copy path"],
            menu.Lines());
        Assert.False(repository.IsDiffOpen);
        Assert.False(((ChangedFileRow)calculator.DataContext!).IsSelected);
        TestPictures.Save(app, "04a-unstaged-tracked-menu");
        app.PressKey(Key.Escape);

        // Steps 2 and 3: an untracked file and a staged one.
        app.RightClick(FileRow(app, "UnstagedFileList", "docs/guide.md"));
        Assert.Equal("guide.md", app.Menu().Header);
        TestPictures.Save(app, "04c-untracked-menu");
        app.PressKey(Key.Escape);
        app.RightClick(FileRow(app, "StagedFileList", "config/settings.json"));
        Assert.Equal(["settings.json", "Unstage", "—", "Open file", "Show in folder", "Copy path"], app.Menu().Lines());
        TestPictures.Save(app, "04d-staged-menu");
        app.PressKey(Key.Escape);

        // Step 5: the diff's text in its first hunk.
        app.Click(FileRow(app, "UnstagedFileList", "src/Calculator.cs"));
        await app.WaitForAsync(() => repository.Diff?.ShowsText == true, "the diff");
        var text = app.Find<DiffTextView>("DiffText");
        app.Settle();
        var firstLine = Enumerable.Range(0, text.Rows.Count).First(row => text.Rows[row] is { Kind: DiffRowKind.Line, Hunk: 0 } && text.RowBounds(row) is not null);
        var bounds = text.RowBounds(firstLine)!.Value;
        app.RightClick(text.TranslatePoint(new Point(bounds.X + 300, bounds.Center.Y), app.Window)!.Value);
        menu = app.Menu();
        Assert.Equal(
            ["Calculator.cs", "Stage hunk", "Discard hunk…", "—", "Stage file", "Discard file…", "—", "Open file", "Show in folder", "Copy path"],
            menu.Lines());
        TestPictures.Save(app, "04f-diff-menu-no-selection");
        app.PressKey(Key.Escape);
        Assert.True(repository.IsDiffOpen);

        // Step 7: a commit's changed file.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !repository.IsDiffOpen, "the diff closed");
        var addCalculator = Enumerable.Range(0, repository.Graph.Count).Single(row => repository.Graph.CommitAt(row).Subject == "Add calculator");
        app.Click(app.GraphRowPoint(addCalculator));
        await app.WaitForAsync(() => FileButtons(app).Any(), "the commit's files");
        app.RightClick(FileButtons(app).First(button => ((ChangedFileRow)button.DataContext!).File!.Path == "src/Calculator.cs"));
        Assert.Equal(["Calculator.cs", "Open file", "Show in folder", "Copy path"], app.Menu().Lines());
        TestPictures.Save(app, "04i-commit-file-menu");
        app.PressKey(Key.Escape);
        Assert.False(repository.IsDiffOpen);
    }

    [AvaloniaFact]
    public async Task Double_clicks_check_out_branches_and_apply_a_stash_from_the_left_panel_and_only_select_elsewhere()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path]);
        var repository = await ShownAsync(app);
        var (actions, recorder) = RecordingActions.Create();
        repository.MenuActions = actions;
        var stash = repository.Refs!.Stashes[0].Sha;

        // Check 6 steps 1 to 4 and 6.
        app.Click(LeftRow(app, row => row.Key == "local:feature"));
        app.DoubleClick(LeftRow(app, row => row.Key == "ref:refs/heads/feature/ff"));
        Assert.Equal(FastForwardMeRow, repository.SelectedIndex);
        app.DoubleClick(app.GraphLabelBounds(MainWorkRow, "main").Center);
        Assert.Equal(MainWorkRow, repository.SelectedIndex);
        app.DoubleClick(app.GraphLabelBounds(FastForwardMeRow, "feature/ff").Center);
        app.DoubleClick(app.GraphRowPoint(5));
        app.DoubleClick(LeftRow(app, row => row.Key == "ref:refs/tags/v1.0"));
        Assert.Equal(AddCoreRow, repository.SelectedIndex);
        app.DoubleClick(app.GraphRowPoint(0));
        Assert.Equal(0, repository.SelectedIndex);
        app.DoubleClick(LeftRow(app, row => row.Kind == RefItemKind.Stash));

        Assert.Equal(
            ["CheckoutAsync(refs/heads/feature/ff)", "CheckoutAsync(refs/heads/feature/ff)", $"ApplyStashAsync({stash})"],
            recorder.Calls.Select(call => call.ToString()));
    }

    [AvaloniaFact]
    public async Task Dropping_a_branch_on_a_branch_opens_the_drop_menu_at_the_release_point()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path]);
        await ShownAsync(app);
        var main = app.GraphLabelBounds(MainWorkRow, "main").Center;

        // Check 7 steps 1 and 2.
        app.PressMouse(app.GraphLabelBounds(AlphaTwoRow, "feature/alpha").Center);
        app.MoveMouseHeld(main, steps: 6);
        app.ReleaseMouse(main);
        var menu = app.Menu();

        Assert.Equal(
            ["feature/alpha → main", "Merge feature/alpha into main", "Merge feature/alpha into main (create a merge commit)", "Fast-forward main to feature/alpha (disabled: \"main has commits that feature/alpha does not have\")"],
            menu.Lines());
        Assert.Equal("Merge feature/alpha into main", menu.Highlighted?.Label);
        AssertNear(main + new Point(2, 2), menu.Bounds.TopLeft);
        Assert.InRange(menu.Bounds.Height, 108, 112);
        TestPictures.Save(app, "07b-drop-menu");
        app.PressKey(Key.Escape);
    }

    private static async Task<RepositoryViewModel> ShownAsync(ShellDriver app)
    {
        app.Window.RequestedThemeVariant = ThemeVariant.Dark;
        var repository = await GraphSelectionTests.GraphShownAsync(app);
        app.Settle();
        return repository;
    }

    private static void AssertNear(Point expected, Point actual)
    {
        Assert.InRange(actual.X, expected.X - 2, expected.X + 2);
        Assert.InRange(actual.Y, expected.Y - 2, expected.Y + 2);
    }

    /// <summary>The left panel's button of the first visible row that matches.</summary>
    private static Button LeftRow(ShellDriver app, Func<RefItemRow, bool> match) =>
        app.Window.GetVisualDescendants().OfType<Button>().First(button => button.DataContext is RefItemRow row && match(row) && button.IsEffectivelyVisible);

    private static Button SectionRow(ShellDriver app, RefSection section) =>
        app.Window.GetVisualDescendants().OfType<Button>().First(button => button.DataContext is RefSectionRow row && row.Section == section && button.IsEffectivelyVisible);

    /// <summary>A file's row button in one of the stage panel's lists, the list scrolled to it.</summary>
    private static Button FileRow(ShellDriver app, string list, string path)
    {
        var items = app.Find<ItemsControl>(list);
        var index = items.Items.Cast<object?>().ToList().FindIndex(item => item is ChangedFileRow { File: { } file } && file.Path == path);
        Assert.True(index >= 0, $"{list} has no {path}.");
        items.ScrollIntoView(index);
        app.Settle();
        return items.GetVisualDescendants().OfType<Button>()
            .First(button => button.IsVisible && button.DataContext is ChangedFileRow { File: { } file } && file.Path == path);
    }

    /// <summary>The file buttons of the details panel's changed files.</summary>
    private static IEnumerable<Button> FileButtons(ShellDriver app)
    {
        app.Settle();
        return app.Window.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("fileRow") && button.IsEffectivelyVisible && button.DataContext is ChangedFileRow { File: not null });
    }
}
