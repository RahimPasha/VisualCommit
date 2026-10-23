// Checks L26 and L27 of part L (docs/test-reports/phase-6.md): the sweep's welcome page and its
// clone form, and the graph of "ops" with its drag, selection, menus, tooltips and dialogs, each
// screen in both themes by one pair (ShotInBothThemesAsync), which also proves that the window
// follows the system's change both ways on that screen (11.14, D194). The part's map is in
// Phase6Checks.L.cs.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Shell;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6Look;

public sealed partial class Phase6ChecksL
{
    /// <summary>
    /// Check L26: the welcome page of an empty tab with a recent list and one workspace, its
    /// Workspaces section between <c>Init</c> and <c>Recent repositories</c>, the recent folders cut
    /// with "…"; then the clone form, which hides the section; both in both themes (11.14, 13.1;
    /// LD1 to LD3). Empty tab, System with the fake dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L26_welcome_page_in_both_themes()
    {
        using var graph = await Scenarios.GraphAsync();
        using var linear = await Scenarios.LinearAsync();
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");

        // Step 1: no tab; the recent list graph (newest), then linear; "Web" in workspaces.json; System.
        var opened = new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);
        ShellDriver.WriteSession(data.Path, new SessionState
        {
            Recent = [new RecentRepository(graph.Path, "graph", opened.AddHours(1)), new RecentRepository(linear.Path, "linear", opened)],
        });
        ShellDriver.WriteWorkspaces(data.Path, new WorkspaceList { Workspaces = [copy.ToWorkspace()] });
        WriteSystemTheme(data);
        using var app = ShellDriver.Start(data.Path, PartL.Size.Width, PartL.Size.Height, systemDark: true);
        await app.WaitUntilReadyAsync();
        app.MoveToNeutral();

        // At the start: the log's line, one tab "New tab" without a close button, the toolbar, the status bar.
        AssertSystemDarkStart(app);
        var strip = app.TabStrip();
        var tab = Assert.Single(strip.Tabs);
        Assert.Equal((PartL.NewTab, false), (tab.Title, tab.ShowsCloseButton));
        foreach (var id in PartL.DisabledOnEmptyTab)
        {
            AssertButton(app, id, false, null);
        }

        AssertButton(app, "SettingsButton", true, PartL.SettingsToolTip);
        AssertButton(app, "ThemeSwitch", true, PartL.SwitchToLightSystem);
        Assert.Equal(PartL.NoRepository, app.Find<TextBlock>("CurrentBranch").Text);
        List<(string, string)> recent = [("graph", graph.Path), ("linear", linear.Path)];

        await ShotInBothThemesAsync(app, "l26a-welcome", assertEach: (picture, palette) => AssertWelcomePage(app, picture, palette, recent));

        // Step 2: Clone; the URL box takes the focus; the form in place of the buttons, the section and the list.
        app.Click(app.Find<Button>("CloneRepoButton"));
        await app.WaitForAsync(() => app.Find<TextBox>("CloneUrlBox").IsFocused, "CloneUrlBox to have the keyboard focus");
        await ShotInBothThemesAsync(app, "l26b-clone-form", assertEach: (picture, palette) => AssertCloneForm(app, picture, palette));

        // After Cancel: the page as after step 1.
        app.Click(app.Find<Button>("CloneCloseButton"));
        await app.WaitForAsync(() => app.Find<StackPanel>("WelcomeButtons").IsEffectivelyVisible, "the welcome page's buttons to show again");
        app.MoveToNeutral();
        AssertWelcomePage(app, app.Capture(), PaletteOf(app), recent);
    }

    /// <summary>
    /// Check L27: the graph of "ops" with its menus, tooltips and dialogs in both themes (11.12,
    /// 11.13, 11.14; LD4 to LD6): a branch drag's pill and outline flipped mid-drag (the drag layer
    /// redrawn), Esc ending it; three rows selected; a danger item under the pointer; a disabled
    /// toolbar button's and a disabled menu item's tooltips; Create branch…; the push dialog without
    /// an upstream; Manage remotes…. Nothing changes git. "ops", System with the fake dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L27_graph_menus_tooltips_and_dialogs_in_both_themes()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, PartL.Size.Width, PartL.Size.Height, AppTheme.System, systemDark: true);
        var exact = await repo.ExactStateAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;

        // At the start: OPS0, nothing selected, the details placeholder; the toolbar; Undo "Nothing to undo".
        AssertSystemDarkStart(app);
        AssertGraph(app, PartL.Ops0, PartL.Ops0Lanes);
        Assert.Equal(-1, repository.SelectedIndex);
        Assert.True(app.FindByAutomationId("DetailsPlaceholder").IsEffectivelyVisible, "The details placeholder does not show.");
        AssertToolbar(app, PartL.OpsToolbar);
        Assert.Equal(UndoRedoButtons.Nothing, app.UndoRedoState());

        // Step 1: press feature/alpha of row 4 and move onto main of row 3 in 5 steps, the button held.
        var alpha = app.GraphLabelBounds(4, "feature/alpha");
        var main = app.GraphLabelBounds(3, "main");
        Assert.True(Math.Abs(main.Center.X - 291.4) <= 1 && Math.Abs(main.Center.Y - 207) <= 1, $"main's label is centred at {main.Center}, not (291.4, 207) (phase 3's MainLabelCentre).");
        var logMark = app.LogMark();
        app.PressMouse(alpha.Center);
        Assert.Equal(4, repository.SelectedIndex);
        app.MoveMouseHeld(main.Center, steps: 5);
        await app.WaitForAsync(() => app.BranchDrag.IsDragging && app.BranchDrag.Target?.RefName == "refs/heads/main", "the drag of feature/alpha over main");
        var layer = app.Find<DragLayer>("DragLayer");
        var origin = (Vector)app.BoundsOf(layer).TopLeft;
        await ShotInBothThemesAsync(app, "l27a-drag", keepPointer: true, assertEach: (picture, palette) =>
        {
            var dark = IsDark(palette);
            Assert.True(app.BranchDrag.IsDragging, "The drag stopped.");
            Assert.Equal(4, repository.SelectedIndex);
            Assert.Equal(1, repository.Selection.Count);
            Assert.StartsWith(PartL.AlphaTwo, app.Find<SelectableTextBlock>("DetailsSha").Text, StringComparison.Ordinal);
            var pill = layer.PillBounds!.Value.Translate(origin);
            Assert.True(
                Math.Abs(pill.X - (main.Center.X + 8)) <= 0.5 && Math.Abs(pill.Y - (main.Center.Y + 8)) <= 0.5 && Math.Abs(pill.Height - 18) <= 0.5,
                $"The pill is at {pill}, not 8 right of and 8 below the pointer {main.Center}, 18 high.");
            AssertNear(PartL.PillPoint(dark), picture, new Point(Math.Floor(pill.X + 4), Math.Floor(pill.Top + 6)), PartL.BlendTolerance, "pill's left padding over the selected row");
            AssertNear(PartL.Blend(palette.Lane(3), 0.5, palette.Selection), picture, new Point(alpha.X + 2, alpha.Center.Y), PartL.BlendTolerance, "dimmed source label feature/alpha (50 %)");
            var outline = layer.OutlineBounds!.Value.Translate(origin);
            AssertColour(palette.DragTarget, picture, new Point(outline.Center.X, outline.Top + 1), "outline's top stroke (VcDragTargetBrush)");
            AssertColour(palette.DragTarget, picture, new Point(outline.X + 1, outline.Center.Y), "outline's left stroke (VcDragTargetBrush)");
            if (!dark)
            {
                AssertNoPixel(picture, new Rect(0, 0, PartL.Size.Width, PartL.Size.Height), Dark.DragTarget, "The light picture");
            }

            AssertRowBackground(picture, palette.Selection, 4, "selected row 4 (VcSelectionBrush)");
            AssertOpsColours(app, picture, palette, nodes: [1, 2, 4, 5]);
            Assert.False(app.Session.Shell.Menu.IsOpen, "A menu is open during the drag.");
        });
        var flippedBack = app.Capture();
        var outlineBack = layer.OutlineBounds!.Value.Translate(origin);
        AssertColour(Dark.DragTarget, flippedBack, new Point(outlineBack.Center.X, outlineBack.Top + 1), "outline's top stroke after the flip back");
        AssertColour(Dark.DragTarget, flippedBack, new Point(outlineBack.X + 1, outlineBack.Center.Y), "outline's left stroke after the flip back");
        var pillBack = layer.PillBounds!.Value.Translate(origin);
        AssertNear(PartL.PillPoint(dark: true), flippedBack, new Point(Math.Floor(pillBack.X + 4), Math.Floor(pillBack.Top + 6)), PartL.BlendTolerance, "pill's point after the flip back");

        // Esc with the button held ends the drag; the release opens no menu.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.BranchDrag.IsDragging, "the drag to end on Esc");
        var ended = app.Capture();
        Assert.Null(layer.PillBounds);
        Assert.Null(layer.OutlineBounds);
        AssertColour(Dark.Lane(3), ended, new Point(alpha.X + 2, alpha.Center.Y), "source label at full opacity after Esc");
        app.ReleaseMouse(main.Center);
        await AssertNoMenuAsync(app, logMark);
        Assert.Equal(exact, await repo.ExactStateAsync());

        // Step 2: rows 5, 6 and 7 selected by a click and two command-clicks.
        app.Click(Near(app.GraphRowPoint(5), PartL.OpsRowPoint(5), "row 5"));
        app.Click(Near(app.GraphRowPoint(6), PartL.OpsRowPoint(6), "row 6"), app.CommandModifier);
        app.Click(Near(app.GraphRowPoint(7), PartL.OpsRowPoint(7), "row 7"), app.CommandModifier);
        await app.WaitForAsync(() => Shown(app, "MultiSelectionView") is not null && repository.Selection.Count == 3, "the three commits' panel");
        await ShotInBothThemesAsync(app, "l27b-three-selected", assertEach: (picture, palette) =>
        {
            Assert.Equal(
                new[] { 5, 6, 7 }.Select(row => repository.Graph.CommitAt(row).Sha).Order(StringComparer.Ordinal),
                repository.Selection.Shas.Order(StringComparer.Ordinal));
            for (var row = 0; row < PartL.Ops0.Count; row++)
            {
                if (row >= 5)
                {
                    AssertRowBackground(picture, palette.Selection, row, $"selected row {row}");
                }
                else
                {
                    Assert.NotEqual(palette.Selection, picture.PixelAt(RowBackgroundPoint(row)));
                }
            }

            Assert.Equal(PartL.ThreeSelected, app.Find<TextBlock>("MultiSelectionTitle").Text);
            var items = app.FindByAutomationId("MultiSelectionList").GetVisualDescendants().OfType<Control>()
                .Where(control => Avalonia.Automation.AutomationProperties.GetAutomationId(control) == "MultiSelectionItem" && control.IsEffectivelyVisible)
                .OrderBy(control => app.BoundsOf(control).Y)
                .Select(control => string.Join(' ', TextsIn(control)))
                .ToList();
            Assert.Equal(PartL.ThreeSelectedList.Select(entry => entry.Sha + " " + entry.Subject), items);
            var shaBlock = app.FindByAutomationId("MultiSelectionList").GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == PartL.AlphaOne);
            Assert.Equal(palette.TextSecondary, (shaBlock.Foreground as ISolidColorBrush)?.Color);
            Assert.Equal(PartL.MultiSelectionHint, app.Find<TextBlock>("MultiSelectionHint").Text);
            Assert.Null(Shown(app, "FileList"));
            AssertOpsColours(app, picture, palette, nodes: [1, 2, 4, 5]);
        });

        // Step 3: feature/alpha's menu, the pointer moved onto "Delete feature/alpha…" (no tooltip: see the report).
        app.RightClick(app.GraphLabelBounds(4, "feature/alpha").Center);
        var menu = app.Menu();
        Assert.Equal(4, repository.SelectedIndex);
        Assert.Equal(1, repository.Selection.Count);
        AssertMenu(menu, [.. PartL.AlphaMenu]);
        AssertMenuShape(app, menu);
        Assert.Equal(PartL.AlphaMenuHeight, menu.Bounds.Height, PartL.Tolerance);
        Assert.Equal(4, menu.Bounds.Top, PartL.Tolerance);
        Assert.All(menu.Items, item => Assert.Null(item.KeyText));
        app.MoveMouse(app.MenuItemBounds(PartL.DeleteAlpha).Center);
        app.Settle();
        await ShotInBothThemesAsync(app, "l27c-danger-item", keepPointer: true, assertEach: (picture, palette) =>
        {
            var shown = app.Menu();
            AssertHighlighted(shown, PartL.DeleteAlpha);
            Assert.Equal("feature/alpha", shown.Header);
            var delete = shown.Item(PartL.DeleteAlpha);
            AssertColour(palette.Hover, picture, new Point(delete.Bounds.Right - 6, delete.Bounds.Top + 3), "Delete feature/alpha…'s background under the pointer (VcHoverBackgroundBrush)");
            AssertInk(picture, delete.Bounds, palette.Danger, palette.Hover, "Delete feature/alpha…'s label (VcDangerBrush, 11.10)");
            var force = shown.Item(PartL.ForcePushAlpha);
            AssertColour(palette.Panel, picture, new Point(force.Bounds.Right - 6, force.Bounds.Top + 3), "menu's background (VcPanelBackgroundBrush)");
            AssertInk(picture, force.Bounds, palette.Danger, palette.Panel, "Force push feature/alpha…'s label (VcDangerBrush)");
            var disabled = shown.Item(PartL.FastForwardAlpha);
            AssertInk(picture, disabled.Bounds, palette.TextDisabled, palette.Panel, "the disabled item's label (VcTextDisabledBrush)");
            Assert.DoesNotContain(app.Window.GetVisualDescendants().OfType<Control>(), control => ToolTip.GetIsOpen(control));
        });
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 4: the pointer rests on the disabled Stash; its tooltip, the reason alone.
        var stash = ToolbarButton(app, "StashButton");
        Assert.Equal(PartL.NoChangesToStash, await app.RestPointerOnAsync(stash));
        Assert.Equal(PartL.NoChangesToStash, Avalonia.Automation.AutomationProperties.GetHelpText(stash));
        await ShotInBothThemesAsync(app, "l27d-disabled-tooltip", keepPointer: true, assertEach: (picture, palette) =>
        {
            Assert.False(stash.IsEffectivelyEnabled, "Stash is enabled.");
            AssertToolTipInside(app, PartL.NoChangesToStash);
            var label = stash.GetVisualDescendants().OfType<TextBlock>().First(text => text.IsEffectivelyVisible);
            AssertInk(picture, app.BoundsOf(label), palette.TextDisabled, palette.Chrome, "Stash's label (VcTextDisabledBrush)");
            AssertIcon(app, picture, stash, palette.TextDisabled, "Stash's icon");
            AssertOpsColours(app, picture, palette, nodes: [1, 2, 4, 5]);
        });

        // Step 5: Main work selected; main's menu; the pointer rests on the disabled "Delete main…".
        app.MoveToNeutral();
        app.Click(Near(app.GraphRowPoint(3), PartL.OpsRowPoint(3), "row 3"));
        await app.WaitForAsync(() => repository.SelectedIndex == 3, "Main work to be selected");
        var pointer = app.GraphLabelBounds(3, "main").Center;
        app.RightClick(pointer);
        menu = app.Menu();
        Assert.Equal(3, repository.SelectedIndex);
        Assert.Equal(PartL.MainMenu, menu.LinesWithKeys());
        AssertMenuShape(app, menu);
        AssertMenuAtPointer(menu, pointer);
        Assert.Equal(PartL.MainMenuHeight, menu.Bounds.Height, PartL.Tolerance);
        AssertHighlighted(menu, PartL.PullFromOrigin);
        Assert.Equal(PartL.CannotDeleteCurrent, await app.RestPointerOnMenuItemAsync(PartL.DeleteMain));
        await ShotInBothThemesAsync(app, "l27e-menu-item-tooltip", keepPointer: true, assertEach: (picture, palette) =>
        {
            var shown = app.Menu();
            AssertHighlighted(shown, PartL.PullFromOrigin);
            var pull = shown.Item(PartL.PullFromOrigin);
            AssertColour(palette.Hover, picture, new Point(pull.Bounds.Left + 4, pull.Bounds.Top + 3), "the highlighted Pull from origin/main's background (VcHoverBackgroundBrush)");
            var delete = shown.Item(PartL.DeleteMain);
            Assert.False(delete.IsEnabled);
            AssertInk(picture, LabelPart(delete.Bounds), palette.TextDisabled, palette.Panel, "Delete main…'s label (VcTextDisabledBrush, not the danger colour, D114)");
            foreach (var keyed in shown.Items.Where(item => item.KeyText is not null))
            {
                var block = ShellDriver.MenuKeyBlockOf(app.MenuItemControl(keyed.Label)) ?? throw new Xunit.Sdk.XunitException($"{keyed.Label} draws no key.");
                AssertInk(picture, app.BoundsOf(block), palette.TextSecondary, keyed.IsHighlighted ? palette.Hover : palette.Panel, $"{keyed.Label}'s key (VcTextSecondaryBrush)");
            }

            AssertToolTipInside(app, PartL.CannotDeleteCurrent);
            AssertOpsColours(app, picture, palette, nodes: [1, 2], outside: shown.Bounds);
        });
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 6: the toolbar's Branch: the name dialog.
        app.Click(ToolbarButton(app, "BranchButton"));
        await app.WaitForDialogAsync<NameDialogViewModel>();
        await WaitForDialogFocusAsync(app, "NameBox");
        await ShotInBothThemesAsync(app, "l27f-name-dialog", assertEach: (picture, palette) =>
        {
            AssertCard(app, picture, palette, PartL.CardWidth, PartL.CardLeft);
            Assert.Equal(PartL.CreateBranch, DialogText(app, "NameDialogTitle"));
            Assert.Equal(PartL.CreateBranchContext, DialogText(app, "NameDialogContext"));
            var box = InDialog<TextBox>(app, "NameBox");
            Assert.True(string.IsNullOrEmpty(box.Text), $"NameBox holds \"{box.Text}\".");
            Assert.Equal(PartL.BranchNamePlaceholder, box.PlaceholderText);
            Assert.True(box.IsFocused, "NameBox does not have the keyboard focus.");
            Assert.True(Shown(app.TopDialogView!, "NameError") is not TextBlock { Text.Length: > 0 }, "NameError shows a text.");
            var option = InDialog<CheckBox>(app, "NameDialogOption");
            Assert.Equal((PartL.CheckOutTheNewBranch, true), (option.Content as string, option.IsChecked == true));
            Assert.Equal(["Cancel", PartL.CreateBranch], ButtonsOf(app, app.TopDialogView!));
            Assert.False(InDialog<Button>(app, "ConfirmButton").IsEffectivelyEnabled, "Create branch is enabled with no name.");
            AssertBackdrop(picture, palette);
        });
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);

        // Step 7: feature/ff's menu, Push feature/ff…: the push dialog without an upstream.
        app.RightClick(app.GraphLabelBounds(2, "feature/ff").Center);
        app.Menu();
        Assert.Equal(2, repository.SelectedIndex);
        app.ClickMenuItem(PartL.PushFf);
        await app.WaitForDialogAsync<PushDialogViewModel>();
        await WaitForDialogFocusAsync(app, "PushBranchBox");
        await ShotInBothThemesAsync(app, "l27g-push-dialog", assertEach: (picture, palette) =>
        {
            AssertCard(app, picture, palette, PartL.CardWidth, PartL.CardLeft);
            Assert.Equal(PartL.PushFfTitle, DialogText(app, "PushTitle"));
            Assert.Equal("origin", DialogText(app, "PushRemote"));
            Assert.Contains("To", TextsIn(app.TopDialogView!));
            var box = InDialog<TextBox>(app, "PushBranchBox");
            Assert.Equal("feature/ff", box.Text);
            Assert.True(box.IsFocused, "PushBranchBox does not have the keyboard focus.");
            var upstream = InDialog<CheckBox>(app, "PushSetUpstream");
            Assert.Equal((PartL.SetAsUpstream, true), (upstream.Content as string, upstream.IsChecked == true));
            Assert.Equal(["Cancel", "Push"], ButtonsOf(app, app.TopDialogView!));
            Assert.True(InDialog<Button>(app, "ConfirmButton").IsEffectivelyEnabled, "Push is disabled.");
            AssertBackdrop(picture, palette);
        });
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.Equal(PartL.OpsRemoteHeads, await RemoteHeadsAsync(repo));

        // Step 8: the Remotes section's menu, Manage remotes…: the remotes dialog.
        app.RightClick(await app.RefRowAsync("Remotes"));
        app.Menu();
        app.ClickMenuItem(PartL.ManageRemotes);
        await app.WaitForDialogAsync<RemotesDialogViewModel>();
        var url = await OriginUrlAsync(repo);
        await ShotInBothThemesAsync(app, "l27h-remotes-dialog", assertEach: (picture, palette) =>
        {
            AssertCard(app, picture, palette, PartL.WideCardWidth, PartL.WideCardLeft);
            Assert.Equal(PartL.RemotesTitle, DialogText(app, "RemotesTitle"));
            var rows = app.TopDialogView!.GetVisualDescendants().OfType<Control>()
                .Where(control => control.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetAutomationId(control) == "RemoteRow").ToList();
            var row = Assert.Single(rows);
            Assert.Equal("origin", row.GetVisualDescendants().OfType<TextBlock>().First(text => text.IsEffectivelyVisible).Text);
            var shownUrl = (TextBlock)Shown(row, "RemoteUrl")!;
            Assert.Equal(url, shownUrl.Text);
            Assert.Equal(url, ToolTip.GetTip(shownUrl) as string);
            Assert.True(Shown(row, "RemoteEditButton") is Button { IsEffectivelyEnabled: true }, "Edit does not show.");
            var remove = (Button)Shown(row, "RemoteRemoveButton")!;
            AssertInk(picture, app.BoundsOf(remove), palette.Danger, BackgroundUnder(picture, app.BoundsOf(remove)), "Remove's label (VcDangerBrush)");
            Assert.True(Shown(app.TopDialogView!, "AddRemoteButton") is Button { IsEffectivelyVisible: true }, "Add remote does not show.");
            Assert.Equal("Close", InDialog<Button>(app, "CancelButton").Content as string);
            AssertBackdrop(picture, palette);
        });
        app.Click(InDialog<Button>(app, "CancelButton"));
        await AssertNoDialogAsync(app);

        // At the end: nothing changed in git; no backup; Undo "Nothing to undo".
        Assert.Equal(exact, await repo.ExactStateAsync());
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal(UndoRedoButtons.Nothing, app.UndoRedoState());
    }

    /// <summary>
    /// OPS0's colours outside any open menu (L27's "In l27a, l27b and l27d, and in l27e for rows 1
    /// and 2 only"): the nodes of <paramref name="nodes"/> in their lanes' colours (D198's light lanes
    /// 1 and 2), the labels feature/conflict and feature/ff filled with lanes 1 and 2 with their text
    /// in <c>VcLabelTextBrush</c>; every point also outside <paramref name="outside"/>.
    /// </summary>
    private static void AssertOpsColours(ShellDriver app, Screenshot picture, Palette palette, int[] nodes, Rect? outside = null)
    {
        foreach (var row in nodes)
        {
            var node = PartL.OpsNode(row);
            Assert.False(outside is { } menu && menu.Contains(node), $"Row {row}'s node {node} lies under the open menu.");
            AssertColour(palette.Lane(PartL.Ops0[row].Colour), picture, node, $"row {row}'s node");
        }

        foreach (var (row, label) in new[] { (1, "feature/conflict"), (2, "feature/ff") })
        {
            var placed = Graph(app).LabelsInRow(row)[0];
            Assert.StartsWith(label[..9], placed.Text, StringComparison.Ordinal);
            var bounds = app.GraphLabelBounds(row, placed.Text);
            var fill = new Point(bounds.X + 2, bounds.Center.Y);
            Assert.False(outside is { } menu && menu.Intersects(bounds), $"The label {label} lies under the open menu.");
            AssertColour(palette.Lane(PartL.Ops0[row].Colour), picture, fill, $"label {label}'s fill");
            AssertInk(picture, bounds.Deflate(new Thickness(3, 2)), palette.LabelText, palette.Lane(PartL.Ops0[row].Colour), $"label {label}'s text (VcLabelTextBrush)");
        }
    }

    /// <summary>Where a commit row's background is sampled clear of text: x 575, 3 below the row's top (row r from y 116 + 26 r).</summary>
    private static Point RowBackgroundPoint(int row) => new(575, 116 + (26 * row) + 3);

    /// <summary>A commit row's background at <see cref="RowBackgroundPoint"/>.</summary>
    private static void AssertRowBackground(Screenshot picture, Color colour, int row, string what) =>
        AssertColour(colour, picture, RowBackgroundPoint(row), what);

    /// <summary>A point the app's bounds give, held against the report's (±2), returned for the click.</summary>
    private static Point Near(Point actual, Point reported, string what)
    {
        Assert.True(Math.Abs(actual.X - reported.X) <= PartL.Tolerance && Math.Abs(actual.Y - reported.Y) <= PartL.Tolerance, $"{what} is at {actual}; the report says {reported} (±2).");
        return actual;
    }

    /// <summary>The left part of a menu item, where its label is drawn (left of a key column).</summary>
    private static Rect LabelPart(Rect item) => new(item.X, item.Y, item.Width * 0.6, item.Height);

    /// <summary>The colour behind a control: the most common pixel of the row 2 inside its top.</summary>
    private static Color BackgroundUnder(Screenshot picture, Rect bounds)
    {
        var counts = new Dictionary<Color, int>();
        for (var x = (int)Math.Ceiling(bounds.Left); x < (int)Math.Floor(bounds.Right); x++)
        {
            var pixel = picture.PixelAt(x, (int)Math.Ceiling(bounds.Top) + 2);
            counts[pixel] = counts.GetValueOrDefault(pixel) + 1;
        }

        return counts.OrderByDescending(pair => pair.Value).First().Key;
    }

    /// <summary>The one open tooltip shows <paramref name="text"/> and lies inside the window.</summary>
    private static void AssertToolTipInside(ShellDriver app, string text)
    {
        var tip = app.Window.GetVisualDescendants().OfType<ToolTip>().Single(candidate => candidate.IsEffectivelyVisible);
        Assert.Equal(text, string.Join('\n', TextsIn(tip)));
        var bounds = app.BoundsOf(tip);
        var size = app.Window.ClientSize;
        Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= size.Width && bounds.Bottom <= size.Height, $"The tooltip at {bounds} is not inside the window.");
    }

    /// <summary>The first visible icon inside <paramref name="owner"/> (or the owner itself), in <paramref name="colour"/>: see <see cref="AssertIconColour"/>.</summary>
    private static void AssertIcon(ShellDriver app, Screenshot picture, Visual owner, Color colour, string what)
    {
        var icon = owner as VisualCommit.App.Controls.Icon
            ?? owner.GetVisualDescendants().OfType<VisualCommit.App.Controls.Icon>().First(candidate => candidate.IsEffectivelyVisible);
        AssertIconColour(app, picture, icon, colour, what);
    }

    /// <summary>
    /// An icon drawn in <paramref name="colour"/> (part L's "Colours" for a mark that is not text):
    /// its brush is the token's, and it is drawn toward that colour, some pixel of its bounds lying at
    /// most half as far from the colour (the largest channel difference) as the background behind it
    /// (the bounds' commonest pixel). A 10-to-18-px line icon's strokes are thinner than a pixel, so
    /// no pixel of it need be exactly, or even nearly, its colour (phase 5's 12-px IconAlert reached
    /// #C79036 for #E0A23B; IconSolo #6875D4 for #7C8CFF; the activity log's 10-px IconCheck
    /// #2F7B5A for #3FB97F): "contains" is not asserted for an icon, which the handback names.
    /// </summary>
    private static void AssertIconColour(ShellDriver app, Screenshot picture, VisualCommit.App.Controls.Icon icon, Color colour, string what)
    {
        Assert.True((icon.Foreground as ISolidColorBrush)?.Color == colour, $"{what} is drawn with {(icon.Foreground as ISolidColorBrush)?.Color}, not {Hex(colour)}.");
        var bounds = app.BoundsOf(icon);
        var pixels = new List<Color>();
        for (var y = (int)Math.Floor(bounds.Top); y < (int)Math.Ceiling(bounds.Bottom); y++)
        {
            for (var x = (int)Math.Floor(bounds.Left); x < (int)Math.Ceiling(bounds.Right); x++)
            {
                pixels.Add(picture.PixelAt(x, y));
            }
        }

        static int Distance(Color a, Color b) => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));
        var background = pixels.GroupBy(pixel => pixel).OrderByDescending(group => group.Count()).First().Key;
        var nearest = pixels.MinBy(pixel => Distance(pixel, colour));
        Assert.True(
            Distance(nearest, colour) * 2 <= Distance(background, colour),
            $"{what} at {bounds} is not drawn toward {Hex(colour)}: its nearest pixel {Hex(nearest)} is not at most half as far from it as the background {Hex(background)}.");
    }

    /// <summary>The heads <c>ls-remote origin</c> lists, by name.</summary>
    private static async Task<List<string>> RemoteHeadsAsync(TempRepo repo) =>
        [.. (await repo.GitAsync("ls-remote", "--heads", "origin")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Split('\t')[1].TrimEnd('\r'))];

    /// <summary>The URL of the remote origin as configured.</summary>
    private static async Task<string> OriginUrlAsync(TempRepo repo) =>
        (await repo.GitAsync("config", "--get", "remote.origin.url")).StandardOutput.Trim();

    /// <summary>
    /// L26 step 1's welcome page in the variant shown (13.1, LD3): in the graph area, its stack 392
    /// wide and centred vertically without scrolling; its texts in reading order; no error; the
    /// Workspaces section between the buttons' row and <c>Recent repositories</c>, 78 high, with the
    /// item "Web"; the recent items with their folders cut with "…", whole in their tooltips; the
    /// left and right panels as phase 0 gives them; the colours; no tooltip open.
    /// </summary>
    private static void AssertWelcomePage(ShellDriver app, Screenshot picture, Palette palette, IReadOnlyList<(string Name, string Folder)> recent)
    {
        var view = app.ActiveWelcomeView;
        var page = app.BoundsOf(view);
        Assert.True(
            Math.Abs(page.Left - PartL.GraphArea.Left) <= PartL.Tolerance && Math.Abs(page.Right - PartL.GraphArea.Right) <= PartL.Tolerance
                && Math.Abs(page.Top - PartL.GraphArea.Top) <= PartL.Tolerance && Math.Abs(page.Bottom - PartL.GraphArea.Bottom) <= PartL.Tolerance,
            $"The welcome page is at {page}, not in the graph area (x 260 to 700, y 88 to 674).");
        var buttons = view.GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Name == "WelcomeButtons");
        var stack = (StackPanel)buttons.GetVisualParent()!;
        var stackBounds = app.BoundsOf(stack);
        Assert.True(
            Math.Abs(stackBounds.Left - PartL.StackLeft) <= PartL.Tolerance && Math.Abs(stackBounds.Right - PartL.StackRight) <= PartL.Tolerance,
            $"The welcome page's stack is at {stackBounds}, not x 284 to 676 (392 wide).");
        Assert.True(Math.Abs(stackBounds.Center.Y - PartL.StackCentreY) <= 1, $"The stack's centre is at y {stackBounds.Center.Y}, not 381 ± 1 (LD3).");
        var scroller = view.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroller.Extent.Height <= scroller.Viewport.Height + 0.5, $"The welcome page scrolls: {scroller.Extent.Height} of content in {scroller.Viewport.Height}.");
        Assert.Equal(PartL.WelcomeTexts(recent), TextsIn(view));
        Assert.False(app.Find<TextBlock>("WelcomeError").IsEffectivelyVisible, "WelcomeError shows.");

        // The Workspaces section (13.1 as built): between the buttons' row and "Recent repositories".
        var section = app.WelcomeWorkspaces();
        var buttonsRow = app.BoundsOf(app.Find<Button>("OpenRepoButton").GetVisualParent<StackPanel>()!);
        var headingRow = app.BoundsOf(view.GetVisualDescendants().OfType<DockPanel>().Single(panel => panel.Name == "WorkspacesHeadingRow"));
        Assert.True(Math.Abs(headingRow.Top - buttonsRow.Bottom - PartL.HeadingRowGap) <= PartL.Tolerance, $"The heading row's top is {headingRow.Top - buttonsRow.Bottom} below the buttons' row, not 24.");
        Assert.True(Math.Abs(headingRow.Height - PartL.HeadingRowHeight) <= PartL.Tolerance, $"The heading row is {headingRow.Height} high, not 20.");
        Assert.Equal(PartL.WorkspacesHeading, section.Heading);
        var heading = view.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "WorkspacesHeading");
        Assert.Equal((11d, FontWeight.SemiBold), (heading.FontSize, heading.FontWeight));
        Assert.Equal(palette.TextSecondary, (section.HeadingForeground as ISolidColorBrush)?.Color);
        Assert.True(Math.Abs(section.HeadingBounds.Left - stackBounds.Left) <= PartL.Tolerance, "WorkspacesHeading is not at the stack's left.");
        Assert.True(Math.Abs(section.NewWorkspaceBounds.Right - stackBounds.Right) <= PartL.Tolerance, "NewWorkspaceButton is not at the stack's right.");
        Assert.True(section.NewWorkspaceEnabled, "NewWorkspaceButton is disabled.");
        Assert.Equal((PartL.NewWorkspaceToolTip, PartL.NewWorkspaceToolTip), (section.NewWorkspaceToolTip, section.NewWorkspaceHelpText));
        var newWorkspace = app.Find<Button>("NewWorkspaceButton");
        var plus = newWorkspace.GetVisualDescendants().OfType<VisualCommit.App.Controls.Icon>().Single();
        Assert.True(ReferenceEquals(Application.Current!.FindResource("IconPlus"), plus.Data), "NewWorkspaceButton's icon is not IconPlus.");
        Assert.Equal((12d, 12d), (plus.Bounds.Width, plus.Bounds.Height));
        var label = newWorkspace.GetVisualDescendants().OfType<TextBlock>().Single(text => text.IsEffectivelyVisible);
        Assert.Equal(PartL.NewWorkspace, label.Text);
        Assert.True(Math.Abs(app.BoundsOf(label).Left - app.BoundsOf(plus).Right - 4) <= PartL.Tolerance, "New workspace…'s label is not 4 after its icon.");
        Assert.Equal(palette.TextSecondary, (label.Foreground as ISolidColorBrush)?.Color);
        Assert.Null(section.Notice);
        Assert.False(section.ShowsNoWorkspaces, "NoWorkspacesText shows beside a workspace.");
        var item = Assert.Single(section.Items);
        Assert.Equal((PartL.Workspace, PartL.Count(10), PartL.WebItemToolTip, PartL.Workspace), (item.Name, item.CountText, item.ToolTip, item.AutomationName));
        Assert.True(
            Math.Abs(item.Bounds.Height - PartL.ItemHeight) <= PartL.Tolerance && Math.Abs(item.Bounds.Left - stackBounds.Left) <= PartL.Tolerance && Math.Abs(item.Bounds.Right - stackBounds.Right) <= PartL.Tolerance,
            $"The workspace item is at {item.Bounds}, not 26 high across the stack's 392.");
        Assert.True(item.NameBounds.Left < item.CountBounds.Left, "The item's name is not left of its count.");
        Assert.Equal(palette.TextPrimary, (item.NameForeground as ISolidColorBrush)?.Color);
        Assert.Equal(palette.TextSecondary, (item.CountForeground as ISolidColorBrush)?.Color);
        Assert.True(Math.Abs(item.Bounds.Bottom - buttonsRow.Bottom - PartL.SectionHeight(1)) <= PartL.Tolerance, $"The Workspaces section is {item.Bounds.Bottom - buttonsRow.Bottom} high, not 78 (52 + 26 × 1).");
        var recentHeading = view.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == PartL.RecentRepositories);
        Assert.True(app.BoundsOf(recentHeading).Top >= item.Bounds.Bottom - 0.5, "Recent repositories is not below the Workspaces section.");

        // The recent list: each item's name (primary) and, 12 after it, its folder (secondary), cut with "…" at the item's right edge, whole in its tooltip.
        var items = view.GetVisualDescendants().OfType<Button>().Where(button => Avalonia.Automation.AutomationProperties.GetAutomationId(button) == "RecentRepository").ToList();
        Assert.Equal(recent.Select(entry => entry.Name), items.Select(button => Avalonia.Automation.AutomationProperties.GetName(button)));
        foreach (var (button, (name, folder)) in items.Zip(recent))
        {
            Assert.Equal(folder, ToolTip.GetTip(button) as string);
            var texts = button.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).OrderBy(text => app.BoundsOf(text).X).ToList();
            Assert.Equal([name, folder], texts.Select(text => text.Text));
            Assert.Equal(palette.TextPrimary, (texts[0].Foreground as ISolidColorBrush)?.Color);
            Assert.Equal(palette.TextSecondary, (texts[1].Foreground as ISolidColorBrush)?.Color);
            Assert.True(Math.Abs(app.BoundsOf(texts[1]).Left - app.BoundsOf(texts[0]).Right - 12) <= PartL.Tolerance, $"{name}'s folder does not start 12 after its name.");
            var drawn = DrawnLines.Of(texts[1]).Single();
            Assert.True(drawn.EndsWith('…') && drawn.Length < folder.Length, $"{name}'s folder is drawn \"{drawn}\", not cut with \"…\".");
            Assert.True(Math.Abs(app.BoundsOf(texts[1]).Right - (app.BoundsOf(button).Right - 8)) <= PartL.Tolerance, $"{name}'s folder does not end at the item's right edge.");
            AssertInk(picture, app.BoundsOf(texts[1]), palette.TextSecondary, palette.Window, $"{name}'s folder");
        }

        // The left and right panels as phase 0 gives them; the toolbar and nothing open.
        Assert.Equal(
            PartL.LeftPanelSections.SelectMany(name => new[] { name, "0" }),
            TextsIn(app.FindByAutomationId("LeftPanel")).Where(text => text != "Filter"));
        Assert.Equal(PartL.DetailsPlaceholder, TextsIn(app.FindByAutomationId("RightPanel")));

        // The colours of the variant.
        AssertColour(palette.Window, picture, new Point(270, 100), "graph area's background (VcWindowBackgroundBrush)");
        AssertInk(picture, item.NameBounds, palette.TextPrimary, palette.Window, "the item Web's name");
        AssertInk(picture, item.CountBounds, palette.TextSecondary, palette.Window, "10 repositories");
        AssertInk(picture, section.HeadingBounds, palette.TextSecondary, palette.Window, "Workspaces");
        Assert.DoesNotContain(app.Window.GetVisualDescendants().OfType<Control>(), control => ToolTip.GetIsOpen(control));
    }

    /// <summary>
    /// L26 step 2's clone form in the variant shown (phase 1's 13a, 13.1): the buttons, the
    /// Workspaces section and the recent list hidden; the heading and its line above the form; the
    /// form's fields, the URL box empty with the keyboard focus; no error, no progress; the boxes'
    /// colours, the focused one's border the accent's.
    /// </summary>
    private static void AssertCloneForm(ShellDriver app, Screenshot picture, Palette palette)
    {
        var view = app.ActiveWelcomeView;
        Assert.False(app.Find<StackPanel>("WelcomeButtons").IsEffectivelyVisible, "WelcomeButtons shows beside the clone form.");
        Assert.False(app.Find<TextBlock>("WorkspacesHeading").IsEffectivelyVisible, "The Workspaces section shows beside the clone form.");
        Assert.DoesNotContain(view.GetVisualDescendants().OfType<Button>(), button => button.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetAutomationId(button) == "RecentRepository");
        Assert.Equal(PartL.CloneFormTexts, TextsIn(view));
        var url = app.Find<TextBox>("CloneUrlBox");
        Assert.True(url.IsFocused, "CloneUrlBox does not have the keyboard focus.");
        Assert.True(string.IsNullOrEmpty(url.Text), $"CloneUrlBox holds \"{url.Text}\".");
        Assert.True(app.Find<Button>("CloneBrowseButton").IsEffectivelyVisible && app.Find<Button>("CloneStartButton").IsEffectivelyVisible && app.Find<Button>("CloneCloseButton").IsEffectivelyVisible, "The form's buttons do not show.");
        Assert.True(app.BoundsOf(app.Find<Button>("CloneBrowseButton")).Left >= app.BoundsOf(app.Find<TextBox>("CloneParentBox")).Right - 0.5, "Browse is not at the parent box's right.");
        Assert.False(app.Find<TextBlock>("CloneError").IsEffectivelyVisible, "CloneError shows.");
        Assert.False(app.Find<ProgressBar>("CloneProgress").IsEffectivelyVisible, "CloneProgress shows.");
        var dark = IsDark(palette);
        foreach (var id in new[] { "CloneUrlBox", "CloneParentBox", "CloneNameBox" })
        {
            var box = app.Find<TextBox>(id);
            var bounds = app.BoundsOf(box);
            AssertColour(PartL.BoxBackground(dark), picture, new Point(bounds.Right - 6, bounds.Center.Y), $"{id}'s background (TextControlBackground)");
            var border = id == "CloneUrlBox" ? PartL.BoxFocusedBorder(dark) : PartL.BoxBorder(dark);
            AssertColour(border, picture, new Point(bounds.Left + 0.5, bounds.Center.Y), $"{id}'s left border ({(id == "CloneUrlBox" ? "focused, TextControlBorderBrushFocused" : "TextControlBorderBrush")})");
        }
    }
}
