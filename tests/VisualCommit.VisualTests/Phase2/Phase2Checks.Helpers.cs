using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase0;
using Xunit;

namespace VisualCommit.VisualTests.Phase2;

/// <summary>What phase 2's checks share: starting the app on a scenario, finding rows and buttons, sampling colours.</summary>
public partial class Phase2Checks
{
    /// <summary>
    /// Starts the app with one tab on <paramref name="repo"/> and waits until its history of
    /// <paramref name="commits"/> commits is loaded and, with <paramref name="changes"/>, the
    /// working-changes row shows. The mouse then rests on the status bar, out of every hover.
    /// </summary>
    private static async Task<ShellDriver> OpenAsync(
        TempDirectory data,
        TempRepo repo,
        int commits,
        int width = 1100,
        int height = 700,
        AppTheme theme = AppTheme.Dark,
        bool changes = true)
    {
        if (theme == AppTheme.Light)
        {
            new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = AppTheme.Light });
        }

        var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: width, height: height);
        try
        {
            await app.WaitUntilReadyAsync();
            var tab = app.Session.Shell.ActiveTab;
            await app.WaitForAsync(
                () => tab.Repository is { Graph: { IsComplete: true } graph } repository && graph.Count == commits && repository.HasWorkingChanges == changes,
                $"a history of {commits} commits{(changes ? " and the working-changes row" : string.Empty)}");
            app.MoveMouse(new Point(130, height - 13));
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    private static CommitGraphControl Graph(ShellDriver app) => app.Find<CommitGraphControl>("GraphRows");

    private static List<string> Subjects(CommitGraphControl graph) =>
        [.. Enumerable.Range(0, graph.Data!.Count).Select(index => graph.Data.CommitAt(index).Subject)];

    /// <summary>A point on the working-changes row, in the Message column's free part.</summary>
    private static Point WorkingRowPoint(ShellDriver app)
    {
        var graph = Graph(app);
        var origin = app.BoundsOf(graph).TopLeft;
        return new Point(origin.X + graph.Columns.Message.X + 60, origin.Y + graph.WorkingRowBounds.Center.Y);
    }

    /// <summary>The centre of the working-changes row's ring: on lane 0 (HEAD's lane in these scenarios).</summary>
    private static Point WorkingRingCentre(ShellDriver app, int lane = 0)
    {
        var graph = Graph(app);
        var origin = app.BoundsOf(graph).TopLeft;
        return new Point(origin.X + graph.LaneCenterX(lane), origin.Y + graph.WorkingRowBounds.Center.Y);
    }

    private static Point RowPoint(ShellDriver app, int row, double x = 250)
    {
        var graph = Graph(app);
        var origin = app.BoundsOf(graph).TopLeft;
        return new Point(origin.X + x, origin.Y + graph.RowBounds(row).Center.Y);
    }

    private static void ClickRow(ShellDriver app, int row) => app.Click(RowPoint(app, row));

    /// <summary>A ring 10 across and 2 wide: between 3 and 5 pixels right of its centre some pixel has the colour, unblended.</summary>
    private static void AssertRing(Screenshot screenshot, Point centre, Color colour, string what)
    {
        var samples = Enumerable.Range(3, 3).Select(offset => screenshot.PixelAt(centre + new Point(offset, 0))).ToList();
        Assert.True(samples.Contains(colour), $"The {what} is not {colour} anywhere from 3 to 5 pixels right of its centre: {string.Join(", ", samples)}.");
        Assert.NotEqual(colour, screenshot.PixelAt(centre));
    }

    /// <summary>
    /// The dashed line from the working-changes ring down to HEAD's node in row 0: dashes of 3 from
    /// the ring's bottom (5 below the row's centre), gaps of 3. Sampled on the line's left pixel
    /// column, in the middle of the first dash, the first gap and the second dash.
    /// </summary>
    private static void AssertDashedLine(ShellDriver app, Screenshot screenshot, Color colour)
    {
        var centre = WorkingRingCentre(app);
        var x = centre.X - 1;
        var ringBottom = centre.Y + 5;
        Assert.Equal(colour, screenshot.PixelAt(new Point(x, ringBottom + 1)));
        Assert.NotEqual(colour, screenshot.PixelAt(new Point(x, ringBottom + 4)));
        Assert.Equal(colour, screenshot.PixelAt(new Point(x, ringBottom + 7)));
    }

    /// <summary>The working-changes row's background, sampled at the far right of its Message column, where no text reaches.</summary>
    private static void AssertWorkingRowBackground(ShellDriver app, Screenshot screenshot, Color colour)
    {
        var graph = Graph(app);
        var origin = app.BoundsOf(graph).TopLeft;
        var point = new Point(origin.X + graph.Columns.Message.Right - 4, origin.Y + graph.WorkingRowBounds.Top + 4);
        ShellExpectations.AssertColour(colour, screenshot, point, "the working-changes row's background");
    }

    /// <summary>Waits until the stage panel shows, with these numbers of files in its two lists.</summary>
    private static Task WaitForStagePanelAsync(ShellDriver app, int unstaged, int staged) =>
        app.WaitForAsync(
            () => app.FindByAutomationId("StagePanel").IsEffectivelyVisible
                && app.Find<TextBlock>("UnstagedTitle").Text == $"Unstaged files ({unstaged})"
                && app.Find<TextBlock>("StagedTitle").Text == $"Staged files ({staged})",
            $"the stage panel with {unstaged} unstaged and {staged} staged files");

    /// <summary>A list's rows as text: status letter, name, folder (flat) and origin, from the list's own rows, scrolled into view or not.</summary>
    private static List<string> FileRows(ShellDriver app, string list)
    {
        var rows = (IEnumerable<ChangedFileRow>)app.Find<ItemsControl>(list).ItemsSource!;
        return [.. rows.Select(row => row.IsFolder
            ? $"{new string(' ', row.Depth * 2)}{row.Name}/"
            : string.Join(' ', new[] { new string(' ', row.Depth * 2) + row.StatusLetter, row.Name, row.Folder, row.OriginText }.Where(part => part.Trim().Length > 0)))];
    }

    /// <summary>Scrolls a stage list until a file's row is laid out, and returns the row's own button.</summary>
    private static async Task<Button> ScrollToFileRowAsync(ShellDriver app, string list, string path)
    {
        var items = app.Find<ItemsControl>(list);
        var rows = ((IEnumerable<ChangedFileRow>)items.ItemsSource!).ToList();
        var index = rows.FindIndex(row => row.Key == path && row.IsFile);
        Assert.True(index >= 0, $"{path} is not in {list}.");
        items.ScrollIntoView(index);
        Button? found = null;
        await app.WaitForAsync(
            () => (found = FileRowButton(app, list, path)) is not null && app.BoundsOf(found).Bottom <= app.BoundsOf(items).Bottom + 0.5,
            $"the row of {path} in {list}");
        return found!;
    }

    /// <summary>A file row's own button in a list, by the file's path; null when it is not laid out.</summary>
    private static Button? FileRowButton(ShellDriver app, string list, string path) =>
        app.Find<ItemsControl>(list).GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(button => button.IsEffectivelyVisible
                && Avalonia.Automation.AutomationProperties.GetAutomationId(button) == "StageFileRow"
                && Avalonia.Automation.AutomationProperties.GetName(button) == path);

    /// <summary>The panel of a file row: what draws its hover and selection backgrounds and holds its buttons.</summary>
    private static Panel RowPanel(Button rowButton) => (Panel)rowButton.GetVisualParent()!;

    private static List<string> VisibleRowButtons(Panel row) =>
        [.. row.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetAutomationId(button) is "RowStageButton" or "RowDiscardButton" or "RowUnstageButton")
            .Select(button => Avalonia.Automation.AutomationProperties.GetAutomationId(button)!)];

    private static async Task WaitForDetailsAsync(ShellDriver app, string subject)
    {
        await app.WaitForAsync(
            () => app.FindByAutomationId("RightPanel").GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.IsEffectivelyVisible && text.Text == subject && Avalonia.Automation.AutomationProperties.GetAutomationId(text) == "DetailsSubject"),
            $"the details of \"{subject}\"");
        app.Settle();
    }

    private static void AssertNoClippedText(ShellDriver app)
    {
        var problems = LayoutAudit.FindClippedText(app.Window, allowShortenedWithToolTip: true, allowScrolledOutOfView: true);
        Assert.True(problems.Count == 0, "Clipped text:\n" + string.Join("\n", problems));
    }
}
