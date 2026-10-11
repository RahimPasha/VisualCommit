using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Shell;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.Tests.GraphSelectionTests;

namespace VisualCommit.App.Tests;

/// <summary>
/// Dragging a branch onto a branch (docs/phase-3-design.md 4g, D87), with simulated mouse input
/// as the scripted walk-through gives it: the press selects, the 4-pixel threshold, the pill and
/// the target's outline in the drag layer, valid and invalid targets, Esc, and the drop that names
/// both branches by their full refs and asks the repository for the menu. The menu itself is the
/// menus' package's (WP7). The ops scenario at 1100×700, as check 7.
/// </summary>
public class BranchDragTests
{
    private const int FastForwardMe = 2;
    private const int MainWork = 3;
    private const int AlphaTwo = 4;
    private const int AlphaOne = 5;
    private const int AddCore = 6;

    [AvaloniaFact]
    public async Task Dragging_a_label_onto_a_label_selects_on_the_press_draws_the_pill_and_the_outline_and_drops_both_full_refs()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path]);
        var repository = await GraphShownAsync(app);
        var layer = app.Find<DragLayer>("DragLayer");
        var service = layer.Service!;
        Assert.Same(service, app.BranchDrag);

        // The drag alone: the drop's menu, which the graph's view gives the repository (WP7), is
        // tested in RepositoryMenusTests.
        Assert.NotNull(repository.BranchDropMenu);
        repository.BranchDropMenu = null;
        var drops = new List<BranchDrop>();
        service.Dropped += (_, drop) => drops.Add(drop);
        var alpha = LabelPoint(app, AlphaTwo, "feature/alpha");
        var main = LabelPoint(app, MainWork, "main");

        // Check 7 step 1: the press selects "Alpha two" before the drag begins.
        app.PressMouse(alpha);
        Assert.Equal(AlphaTwo, repository.SelectedIndex);
        Assert.False(service.IsDragging);
        app.MoveMouseHeld(main, steps: 6);

        Assert.True(service.IsDragging);
        Assert.Equal(("refs/heads/feature/alpha", "feature/alpha", false), (service.Source!.RefName, service.Source.Name, service.Source.IsRemote));
        Assert.Equal(("refs/heads/main", false, true), (service.Target!.RefName, service.Target.IsRemote, service.Target.IsLabel));
        Assert.Equal(3, service.SourceColor);
        Assert.Equal(AlphaTwo, repository.SelectedIndex);
        Assert.False(app.IsMenuOpen);
        await app.WaitForAsync(() => repository.Details.Subject == "Alpha two", "the details of Alpha two");
        Assert.True(service.IsDragging);

        var shot = app.Capture();
        layer.IsVisible = false;
        var withoutLayer = app.Capture();
        layer.IsVisible = true;
        app.Capture();

        // The pill: 8 right of and 8 below the pointer, 18 high, filled with lane colour 3 at 85%
        // over what lies under it.
        var pill = layer.PillBounds!.Value;
        Assert.Equal(main + new Point(8, 8), pill.TopLeft);
        Assert.Equal(CommitGraphControl.LabelHeight, pill.Height);
        var inPill = new Point(pill.X + 2, pill.Center.Y);
        AssertNear(Blend(Brush(app, "VcLane3Brush"), withoutLayer.PixelAt(inPill), DragLayer.PillOpacity), shot.PixelAt(inPill), 3);

        // The target outlined 2 px in VcDragTargetBrush, a pixel away from it, so that it stands
        // out from a label filled with the same colour.
        var mainLabel = LabelBounds(app, MainWork, "main");
        Assert.Equal(mainLabel.Inflate(3), layer.OutlineBounds);
        Assert.Equal(Brush(app, "VcDragTargetBrush"), shot.PixelAt(new Point(mainLabel.X - 2, mainLabel.Center.Y)));
        Assert.Equal(Brush(app, "VcDragTargetBrush"), shot.PixelAt(new Point(mainLabel.X - 3, mainLabel.Center.Y)));
        Assert.Equal(withoutLayer.PixelAt(new Point(mainLabel.X - 1, mainLabel.Center.Y)), shot.PixelAt(new Point(mainLabel.X - 1, mainLabel.Center.Y)));

        // The source label at half opacity over its selected row.
        var alphaLabel = LabelBounds(app, AlphaTwo, "feature/alpha");
        AssertNear(Blend(Brush(app, "VcLane3Brush"), Brush(app, "VcSelectionBrush"), CommitGraphControl.DraggedSourceOpacity), shot.PixelAt(new Point(alphaLabel.X + 2, alphaLabel.Center.Y)), 4);
        Pictures(app, "drag-over-main");

        // Step 2: the release drops; without a menu from the repository nothing opens yet.
        app.ReleaseMouse(main);
        var drop = Assert.Single(drops);
        Assert.Equal(new BranchDrop("refs/heads/feature/alpha", "refs/heads/main", main.X, main.Y), drop);
        Assert.False(service.IsDragging);
        Assert.False(app.IsMenuOpen);
        shot = app.Capture();
        Assert.Null(layer.PillBounds);
        Assert.Null(layer.OutlineBounds);
        Assert.Equal(Brush(app, "VcLane3Brush"), shot.PixelAt(new Point(alphaLabel.X + 2, alphaLabel.Center.Y)));
        Assert.Equal(AlphaTwo, repository.SelectedIndex);

        // With a menu from the repository (WP7 fills it), it opens at the release point.
        BranchDrop? asked = null;
        repository.BranchDropMenu = dropped =>
        {
            asked = dropped;
            return new MenuModel([new MenuHeader("feature/alpha → main"), new ViewModels.Menus.MenuItem("Merge feature/alpha into main", () => { })]);
        };
        app.PressMouse(alpha);
        app.MoveMouseHeld(main, steps: 6);
        app.ReleaseMouse(main);
        Assert.Equal(drop, asked);
        var menu = app.Menu();
        Assert.Equal("feature/alpha → main", menu.Header);
        // The menu layer places it on whole pixels.
        Assert.InRange(menu.Bounds.X - (main.X + 2), -1, 1);
        Assert.InRange(menu.Bounds.Y - (main.Y + 2), -1, 1);
    }

    [AvaloniaFact]
    public async Task A_drag_starts_only_past_4_pixels_and_a_tag_Esc_or_the_source_itself_drops_nothing()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path]);
        var repository = await GraphShownAsync(app);
        var layer = app.Find<DragLayer>("DragLayer");
        var service = layer.Service!;
        var drops = new List<BranchDrop>();
        service.Dropped += (_, drop) => drops.Add(drop);
        var ff = LabelPoint(app, FastForwardMe, "feature/ff");
        var tag = LabelPoint(app, AddCore, "v1.0");

        app.PressMouse(ff);
        Assert.Equal(FastForwardMe, repository.SelectedIndex);
        app.MoveMouseHeld(ff + new Point(4, 0));
        Assert.False(service.IsDragging);
        app.MoveMouseHeld(ff + new Point(4, 3));
        Assert.True(service.IsDragging);

        // Over its own label: no target.
        Assert.Null(service.Target);

        // Check 7 step 6: over the tag no outline shows while the pill follows; the release opens nothing.
        app.MoveMouseHeld(tag, steps: 4);
        Assert.True(service.IsDragging);
        Assert.Null(service.Target);
        app.Capture();
        Assert.Null(layer.OutlineBounds);
        Assert.Equal(tag + new Point(8, 8), layer.PillBounds!.Value.TopLeft);
        Pictures(app, "drag-over-tag");
        app.ReleaseMouse(tag);
        Assert.Empty(drops);
        Assert.False(service.IsDragging);

        // Esc during a drag removes the pill; the release after it drops nothing.
        app.PressMouse(ff);
        app.MoveMouseHeld(LabelPoint(app, MainWork, "main"), steps: 4);
        Assert.NotNull(service.Target);
        app.PressKey(Key.Escape);
        Assert.False(service.IsDragging);
        app.Capture();
        Assert.Null(layer.PillBounds);
        app.MoveMouseHeld(LabelPoint(app, MainWork, "main") + new Point(0, 2));
        Assert.False(service.IsDragging);
        app.ReleaseMouse(LabelPoint(app, MainWork, "main"));
        Assert.Empty(drops);
        Assert.False(app.IsMenuOpen);
        Assert.Equal(FastForwardMe, repository.SelectedIndex);

        // A press on a label with a modifier held selects only.
        app.Click(ff, app.CommandModifier);
        app.PressMouse(LabelPoint(app, MainWork, "main"), KeyModifiers.Shift);
        app.MoveMouseHeld(ff, steps: 4, KeyModifiers.Shift);
        Assert.False(service.IsDragging);
        app.ReleaseMouse(ff, KeyModifiers.Shift);
    }

    [AvaloniaFact]
    public async Task Left_panel_rows_are_sources_and_targets_and_the_row_dragged_gets_no_click()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path]);
        var repository = await GraphShownAsync(app);
        repository.BranchDropMenu = null;
        var layer = app.Find<DragLayer>("DragLayer");
        var service = layer.Service!;
        var drops = new List<BranchDrop>();
        service.Dropped += (_, drop) => drops.Add(drop);
        app.Click(RowPoint(app, AlphaTwo));

        // Check 7 step 5: open both "feature" folders, then drag main onto origin's alpha.
        app.Click(LeftRow(app, row => row.Kind == RefItemKind.Folder && row.Depth == 1));
        app.Click(LeftRow(app, row => row.Kind == RefItemKind.Folder && row.Depth == 2));
        var mainRow = LeftRow(app, row => row.Key == "ref:refs/heads/main");
        var remoteAlpha = LeftRow(app, row => row.Key == "ref:refs/remotes/origin/feature/alpha");
        var from = app.BoundsOf(mainRow).Center;
        var to = app.BoundsOf(remoteAlpha).Center;

        app.PressMouse(from);
        app.MoveMouseHeld(to, steps: 6);
        Assert.True(service.IsDragging);
        Assert.Equal(("refs/heads/main", "main", true), (service.Source!.RefName, service.Source.Name, service.Source.IsCurrent));
        Assert.Equal(("refs/remotes/origin/feature/alpha", true, false), (service.Target!.RefName, service.Target.IsRemote, service.Target.IsLabel));
        Assert.Equal(CommitGraphControl.DraggedSourceOpacity, mainRow.Opacity);
        Assert.Equal(0, service.SourceColor);
        var shot = app.Capture();
        var rowBounds = app.BoundsOf(remoteAlpha);
        Assert.Equal(rowBounds, layer.OutlineBounds);
        Assert.Equal(Brush(app, "VcDragTargetBrush"), shot.PixelAt(new Point(rowBounds.X + 40, rowBounds.Y)));
        Pictures(app, "drag-onto-remote-row");

        app.ReleaseMouse(to);
        Assert.Equal([new BranchDrop("refs/heads/main", "refs/remotes/origin/feature/alpha", to.X, to.Y)], drops);
        Assert.Equal(1, mainRow.Opacity);

        // The row got no click: the graph still shows "Alpha two", and no ref is highlighted.
        Assert.Equal(AlphaTwo, repository.SelectedIndex);
        Assert.DoesNotContain(repository.LeftPanel.Rows.OfType<RefItemRow>(), row => row.IsSelected);

        // A remote branch onto a remote branch, or onto a tag, is no target; onto a local branch it is.
        var remoteMain = LeftRow(app, row => row.Key == "ref:refs/remotes/origin/main");
        app.PressMouse(app.BoundsOf(remoteMain).Center);
        app.MoveMouseHeld(app.BoundsOf(remoteAlpha).Center, steps: 4);
        Assert.True(service.IsDragging);
        Assert.Null(service.Target);
        app.MoveMouseHeld(LabelPoint(app, AlphaTwo, "feature/alpha"), steps: 4);
        Assert.Equal("refs/heads/feature/alpha", service.Target?.RefName);
        app.ReleaseMouse(LabelPoint(app, AlphaTwo, "feature/alpha"));
        Assert.Equal(new BranchDrop("refs/remotes/origin/main", "refs/heads/feature/alpha", LabelPoint(app, AlphaTwo, "feature/alpha").X, LabelPoint(app, AlphaTwo, "feature/alpha").Y), drops[^1]);
    }

    [AvaloniaFact]
    public async Task A_label_of_a_multi_selected_row_is_dragged_without_ending_the_multi_selection()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path]);
        var repository = await GraphShownAsync(app);
        repository.BranchDropMenu = null;
        var service = app.Find<DragLayer>("DragLayer").Service!;
        app.Click(RowPoint(app, AlphaTwo));
        app.Click(RowPoint(app, AlphaOne), app.CommandModifier);

        app.PressMouse(LabelPoint(app, AlphaTwo, "feature/alpha"));
        app.MoveMouseHeld(LabelPoint(app, MainWork, "main"), steps: 6);
        Assert.True(service.IsDragging);
        Assert.Equal(2, repository.Selection.Count);
        app.ReleaseMouse(LabelPoint(app, MainWork, "main"));

        Assert.Equal(2, repository.Selection.Count);
        Assert.Equal("2 commits selected", ((ViewModels.Shell.MultiSelectionViewModel)repository.MultiSelection!).Title);
    }

    /// <summary>The centre of a row's label named <paramref name="text"/>, in the window.</summary>
    private static Point LabelPoint(ShellDriver app, int row, string text) => LabelBounds(app, row, text).Center;

    private static Rect LabelBounds(ShellDriver app, int row, string text) => app.GraphLabelBounds(row, text);

    /// <summary>The left panel's button of the first row that matches.</summary>
    private static Button LeftRow(ShellDriver app, Func<RefItemRow, bool> match) =>
        app.Window.GetVisualDescendants().OfType<Button>().First(button => button.DataContext is RefItemRow row && match(row) && button.IsEffectivelyVisible);

    private static Color Brush(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");

    /// <summary><paramref name="top"/> drawn at <paramref name="opacity"/> over <paramref name="under"/>.</summary>
    private static Color Blend(Color top, Color under, double opacity) => Color.FromRgb(
        (byte)Math.Round((top.R * opacity) + (under.R * (1 - opacity))),
        (byte)Math.Round((top.G * opacity) + (under.G * (1 - opacity))),
        (byte)Math.Round((top.B * opacity) + (under.B * (1 - opacity))));

    private static void AssertNear(Color expected, Color actual, int tolerance) =>
        Assert.True(
            Math.Abs(expected.R - actual.R) <= tolerance && Math.Abs(expected.G - actual.G) <= tolerance && Math.Abs(expected.B - actual.B) <= tolerance,
            $"Expected about {expected}, got {actual}.");
}
