using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Diff;
using VisualCommit.Testing.Headless;

namespace VisualCommit.GuideShots;

/// <summary>
/// The steps the shots share, through the scripted walk-through's driver: the working changes, a
/// file's diff, a commit's menu, the top dialog's card and the open menu.
/// </summary>
internal static class GuideSteps
{
    /// <summary>The graph's rows (from 0, the working changes' row not counted) the shots use.</summary>
    public const int StashRow = 0;

    public const int MainRow = 1;

    public const int CheckWiringRow = 3;

    public const int RoundToDigitsRow = 4;

    public const int CacheReadingsRow = 6;

    public const int RoundHalfUpRow = 7;

    public const int UpdateReadmeRow = 9;

    public const int ReleaseOneOneRow = 13;

    /// <summary>Selects the working changes' row and waits until the stage panel lists the three changed files.</summary>
    public static async Task<Control> OpenWorkingChangesAsync(ShellDriver app)
    {
        app.Click(app.WorkingRowPoint());
        await app.FileRowAsync("UnstagedFileList", GuideScenario.UnitsFile);
        await app.FileRowAsync("UnstagedFileList", GuideScenario.ReadmeFile);
        await app.FileRowAsync("UnstagedFileList", GuideScenario.IconFile);
        app.Settle();
        return app.FindByAutomationId("StagePanel");
    }

    /// <summary>Opens the diff of the unstaged <paramref name="path"/> from the stage panel and waits until it shows.</summary>
    public static async Task<Control> OpenDiffAsync(ShellDriver app, string path)
    {
        await OpenWorkingChangesAsync(app);
        app.Click(await app.FileRowAsync("UnstagedFileList", path));
        var name = WorkingChangesViewModel.FileName(path);
        await app.WaitForAsync(
            () => app.Window.GetVisualDescendants().OfType<DiffView>().Any(view => view.IsEffectivelyVisible)
                && app.Window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "DiffFileName" && text.IsEffectivelyVisible && text.Text == name),
            $"the diff of {path}");
        await WaitForDiffTextAsync(app, path);
        return app.Window.GetVisualDescendants().OfType<DiffView>().First(view => view.IsEffectivelyVisible);
    }

    /// <summary>
    /// Waits until the open diff of <paramref name="path"/> shows its text with its rows laid out,
    /// as phase 3's checks wait for it: the diff is read by git after the click, and its rows are
    /// made as it is drawn.
    /// </summary>
    public static async Task WaitForDiffTextAsync(ShellDriver app, string path)
    {
        await app.WaitForAsync(
            () =>
            {
                app.Capture();
                return app.Session.Shell.ActiveTab.Repository?.Diff is { ShowsText: true } diff && diff.Target.Path == path
                    && app.Window.GetVisualDescendants().OfType<DiffTextView>().Any(view => view.Name == "DiffText" && view.IsEffectivelyVisible && view.Rows.Count > 0 && view.RowBounds(0) is not null);
            },
            $"the text diff of {path}");
        await app.WaitForIdleAsync();
        app.Settle();
    }

    /// <summary>
    /// The open diff from its header down to its last row and a little more, so that a short diff
    /// leaves no empty pane below it in the picture.
    /// </summary>
    public static GuideFrame DiffFrame(ShellDriver app, Control diff)
    {
        var text = app.Window.GetVisualDescendants().OfType<DiffTextView>().First(view => view.Name == "DiffText" && view.IsEffectivelyVisible);
        var last = text.RowBounds(text.Rows.Count - 1)
            ?? throw new InvalidOperationException("The diff's last row is not laid out in view.");
        var bottom = text.TranslatePoint(last.BottomLeft, app.Window)
            ?? throw new InvalidOperationException("The diff's text is not in the window.");
        var bounds = app.BoundsOf(diff);
        return GuideFrame.Area(new Rect(bounds.Left, bounds.Top, bounds.Width, Math.Min(bounds.Bottom, bottom.Y + 24) - bounds.Top));
    }

    /// <summary>
    /// Right-clicks the graph's row <paramref name="row"/>, in its Message column or at the window's
    /// <paramref name="x"/>, and returns the menu's view.
    /// </summary>
    public static Control OpenCommitMenu(ShellDriver app, int row, double? x = null)
    {
        var point = app.GraphRowPoint(row);
        app.RightClick(x is { } at ? point.WithX(at) : point);
        app.Menu();
        return OpenMenu(app);
    }

    /// <summary>
    /// Throws unless the open <paramref name="menu"/>, with its card's margin, lies within the
    /// graph's area from left to right: a menu that reached past it would take a strip of the
    /// panel beside it into a picture framed on the graph.
    /// </summary>
    public static void ThrowUnlessMenuInGraph(ShellDriver app, Control menu)
    {
        var graph = app.BoundsOf(GraphArea(app));
        var shown = GuideFrame.Card(menu).BoundsIn(app);
        if (shown.Left < graph.Left || shown.Right > graph.Right)
        {
            throw new InvalidOperationException($"The menu with its margin, {shown}, reaches past the graph's area, {graph}.");
        }
    }

    /// <summary>The graph's rows: the control that draws the commits.</summary>
    public static CommitGraphControl GraphRows(ShellDriver app) =>
        app.Window.GetVisualDescendants().OfType<CommitGraphControl>().First(graph => graph.Name == "GraphRows" && graph.IsEffectivelyVisible);

    /// <summary>
    /// The window's y of each commit row's bottom edge (from row 0, the working changes' row not
    /// counted) that lies in the graph's view, top to bottom.
    /// </summary>
    public static IReadOnlyList<double> GraphRowBottoms(ShellDriver app)
    {
        var rows = GraphRows(app);
        var view = app.BoundsOf(rows);
        var bottoms = new List<double>();
        for (var row = 0; row < (rows.Data?.Count ?? 0); row++)
        {
            var bottom = (rows.TranslatePoint(rows.RowBounds(row).BottomLeft, app.Window)
                ?? throw new InvalidOperationException("The graph is not in the window.")).Y;
            if (bottom > view.Bottom)
            {
                break;
            }

            bottoms.Add(bottom);
        }

        return bottoms;
    }

    /// <summary>
    /// The graph's area from its top down to the bottom edge of the last commit row it shows
    /// whole, so that no row is cut through its text at the picture's bottom.
    /// </summary>
    public static Rect GraphAreaToLastWholeRow(ShellDriver app)
    {
        var area = app.BoundsOf(GraphArea(app));
        var bottom = GraphRowBottoms(app).LastOrDefault(row => row <= area.Bottom);
        if (bottom <= area.Top)
        {
            throw new InvalidOperationException("The graph's area shows no whole commit row.");
        }

        return new Rect(area.Left, area.Top, area.Width, bottom - area.Top);
    }

    /// <summary>The bottom edge of the first commit row of the graph whose bottom lies at or below <paramref name="y"/>.</summary>
    public static double GraphRowBottomBelow(ShellDriver app, double y)
    {
        foreach (var bottom in GraphRowBottoms(app))
        {
            if (bottom >= y)
            {
                return bottom;
            }
        }

        throw new InvalidOperationException($"No commit row of the graph in view ends at or below {y}.");
    }

    /// <summary>The right panel's row of the changed file named <paramref name="name"/>, in a commit's or a stash's details; null while it is not shown.</summary>
    public static Control? DetailsFileRow(ShellDriver app, string name) =>
        app.FindByAutomationId("RightPanel").GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(row => row.IsEffectivelyVisible && AutomationProperties.GetAutomationId(row) == "ChangedFile" && AutomationProperties.GetName(row) == name);

    /// <summary>The open context menu's view.</summary>
    public static Control OpenMenu(ShellDriver app) =>
        app.OpenMenuView ?? throw new InvalidOperationException("No context menu is open.");

    /// <summary>The card of the dialog on top: its border of class <c>dialogCard</c>.</summary>
    public static Control DialogCard(ShellDriver app)
    {
        var view = app.TopDialogView ?? throw new InvalidOperationException("No dialog is open.");
        return view.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Classes.Contains("dialogCard"))
            ?? throw new InvalidOperationException($"The dialog {view.GetType().Name} has no card.");
    }

    /// <summary>The graph's area: the column between the side panels, or the whole width while it spans them.</summary>
    public static Control GraphArea(ShellDriver app) => app.FindByAutomationId("CommitGraph");

    /// <summary>The window's region from its left edge to its right, between <paramref name="top"/> and <paramref name="bottom"/>.</summary>
    public static Rect Band(ShellDriver app, double top, double bottom) =>
        new(0, top, app.Window.ClientSize.Width, bottom - top);
}
