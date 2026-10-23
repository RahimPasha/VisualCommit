// Checks W2 and W3 of part W (docs/test-reports/phase-6.md): the ten repositories read only, their
// row menus, Locate… to a folder's new place, Remove from workspace and Alt+Up (W2); the same rows
// at 1920×1080 in the light theme with the Folder column (W3).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksW
{
    /// <summary>
    /// Check W2: the ten repositories, read only (design 1.10, 2.4 to 2.7, 2.10): every row's cells,
    /// tones and tooltips; an Ahead / behind tooltip; web-app's and old-site's row menus; old-site's
    /// <c>Locate…</c> to the folder's new place (WD7); its <c>Remove from workspace</c>; Alt+Up; each
    /// repository's exact state unchanged and only reads called.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_W2_ten_repositories()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        await PrepareSameFolderAsync(copy);
        using var data = new TempDirectory("data");

        // Step 1: before the start the exact states, and notes copied file by file to archive/old-site (WD7).
        var exact = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in PartW.WithFolder)
        {
            using var repo = copy.Repo(name);
            exact[name] = await repo.ExactStateAsync();
        }

        var archive = PartW.LocatedFolder(copy.Root);
        Phase6ExpectationTestsW.CopyFiles(copy.PathOf("notes"), archive);
        await PrepareSameFolderAsync(copy, archive);
        string archiveState;
        using (var located = copy.RepoAt(archive))
        {
            archiveState = await located.ExactStateAsync();
        }

        using var app = await OpenWorkspaceAsync(data, copy);
        var shot = Shot(app, PartW.W2aTenRows);
        var w0 = PartW.W0(copy.PathOf("old-site"));
        Assert.Equal(PartW.Workspace, Control<TextBlock>(app, "DashboardTitle").Text);
        Assert.Equal(PartW.Count(10), Control<TextBlock>(app, "DashboardCount").Text);
        AssertHeadings(app, PartW.Columns1100, shot);
        AssertRows(app, copy, w0);
        AssertRowPlaces(app, PartW.Names);
        AssertArrowsAndBackgrounds(app, shot, PartW.Names);
        AssertTd(app, PartW.Workspace, PartW.Count(10));
        AssertActivityToggle(app, enabled: false, PartW.SelectRepository);
        Assert.Single(app.Session.Shell.Tabs);
        AssertOnePass(app, 0, 10);
        foreach (var name in PartW.WithFolder)
        {
            Assert.True(LinesWith(app, 0, PartW.SummaryRead(name)).Count == 1, $"{name}'s summary read was not logged once.");
        }

        Assert.Empty(LinesWith(app, 0, PartW.SummaryRead("old-site")));
        Assert.Empty(CallsIn(app, copy, 0, "old-site"));
        AssertOnlyReads(app.GitCalls(), "W2's start");

        // Step 2: the pointer rests on mobile's Ahead / behind cell: its tooltip.
        var mobile = app.DashboardRowControl("mobile");
        var cell = mobile.GetVisualDescendants().OfType<Control>().Single(control => Avalonia.Automation.AutomationProperties.GetAutomationId(control) == "DashboardRowAheadBehind");
        var centre = app.BoundsOf(cell).Center;
        Assert.True(Math.Abs(centre.Y - PartW.RowCentre(3)) <= PartW.Tolerance && centre.X > 282 && centre.X < 412, $"mobile's Ahead / behind cell's centre is at {centre}, not about (330, 243).");
        var tip = await app.RestPointerOnAsync(cell);
        shot = Shot(app, PartW.W2bAheadBehindToolTip, keepPointer: true);
        Assert.Equal(PartW.Arrows(1, 2).ToolTip, tip);
        Assert.Equal(PartW.Arrows(1, 2).Name, Avalonia.Automation.AutomationProperties.GetName(cell));
        AssertToolTipInWindow(app, shot);
        Assert.Null(app.DashboardSnapshot().SelectedRow);

        // Step 3: web-app's row menu (2.7): a right-click selects the row.
        app.MoveToNeutral();
        var logMark = app.LogMark();
        var point = RowPoint(app, "web-app");
        var menu = await app.RowMenuAsync("web-app");
        Shot(app, PartW.W2cRowMenu);
        AssertMenu(menu, PartW.ReadyRowMenu("web-app"));
        AssertHighlighted(menu, PartW.Open);
        Assert.Equal(copy.PathOf("web-app"), menu.HeaderToolTip);
        AssertRowMenuPlace(app, menu, point);
        Assert.Equal(PartW.ReadyRowMenu("web-app").Length - 1, menu.Entries.Count);
        Assert.True(Math.Abs(menu.Bounds.Height - MenuHeight(8, 3)) <= PartW.Tolerance, $"web-app's menu is {menu.Bounds.Height} high, not 267.");
        Assert.Equal("web-app", app.DashboardSnapshot().SelectedRow);
        Assert.True(LinesWith(app, logMark, PartW.MenuOpened("web-app")).Count == 1, "The menu layer did not log \"Web: Context menu opened: web-app by pointer\".");
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 4: old-site's menu, its folder gone (1.10).
        point = RowPoint(app, "old-site");
        menu = await app.RowMenuAsync("old-site");
        Shot(app, PartW.W2dGoneFolderMenu);
        AssertMenu(menu, PartW.GoneFolderMenu(copy.PathOf("old-site")));
        AssertHighlighted(menu, PartW.CopyPath);
        Assert.Equal(copy.PathOf("old-site"), menu.HeaderToolTip);
        AssertRowMenuPlace(app, menu, point);
        Assert.True(Math.Abs(menu.Bounds.Height - MenuHeight(9, 3)) <= PartW.Tolerance && Math.Abs(menu.Bounds.Bottom - 694) <= PartW.Tolerance, $"old-site's menu is at {menu.Bounds}, not 293 high ending at y 694.");
        Assert.Equal("old-site", app.DashboardSnapshot().SelectedRow);

        // Step 5: Locate… answered with archive/old-site: the row read again at once (trigger 6).
        app.Folders.Answer(archive);
        logMark = app.LogMark();
        var calls = app.CallMark();
        app.ClickMenuItem(PartW.Locate);
        await app.WaitForRowAsync("old-site", row => row.Branch.Text == "main", OperationChainTimeout);
        await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
        await app.WaitForAsync(() => PassLines(app, logMark).Count > 0, "the located row's pass line");
        app.MoveToNeutral();
        Shot(app, PartW.W2eLocated);
        Assert.Equal([PartW.LocateFolderTitle("old-site")], app.Folders.Titles);
        await AssertNoDialogAsync(app);
        var (_, files) = ReadWorkspacesFile(data);
        var saved = Assert.Single(files).Paths;
        Assert.Equal(10, saved.Count);
        Assert.True(SameFolder(copy, archive, saved[9]), $"workspaces.json's tenth path is {saved[9]}, not {archive}'s.");
        Assert.Equal(PartW.WrOrder.Select(copy.PathOf), saved.Take(9));
        Assert.Equal(PartW.Count(10), Control<TextBlock>(app, "DashboardCount").Text);
        AssertRows(app, copy, [.. w0.Take(9), PartW.Located], selected: "old-site", folders: new Dictionary<string, string> { ["old-site"] = archive });
        Assert.Equal(saved[9], app.DashboardSnapshot().Row("old-site").Repository.ToolTip);
        AssertOpeningAndSummaryRead(app.CallsSince(calls, folder => SameFolder(copy, archive, folder)), "Locate… to archive/old-site");
        AssertOnlyReads(app.CallsSince(calls), "Locate…");
        AssertOnePass(app, logMark, 1);

        // Step 6: old-site's menu, a Ready row's now; Remove from workspace, no question.
        point = RowPoint(app, "old-site");
        menu = await app.RowMenuAsync("old-site");
        AssertMenu(menu, PartW.ReadyRowMenu("old-site", fetchReason: PartW.NoRemotesText, pullReason: PartW.NotesPullReason, moveDownReason: PartW.LastRepository));
        AssertHighlighted(menu, PartW.Open);
        Assert.Equal(saved[9], menu.HeaderToolTip);
        AssertRowMenuPlace(app, menu, point);
        Assert.True(Math.Abs(menu.Bounds.Height - MenuHeight(8, 3)) <= PartW.Tolerance, $"old-site's Ready menu is {menu.Bounds.Height} high, not 267.");
        app.ClickMenuItem(PartW.RemoveFromWorkspace);
        app.Settle();
        await AssertNoDialogAsync(app);
        Shot(app, PartW.W2fRemoved);
        var wr = w0.Take(9).ToList();
        AssertRows(app, copy, wr);
        Assert.Equal(PartW.Count(9), Control<TextBlock>(app, "DashboardCount").Text);
        await app.WaitForAsync(() => app.FocusIsWithin(app.Find<ListBox>("DashboardList")), "the keyboard focus on the dashboard's list (2.7)");
        Assert.DoesNotContain(app.Session.Services.OpenRepositories.Summaries, summary => SameFolder(copy, archive, summary.Path));
        AssertWorkspacesFile(data, copy, PartW.WorkspaceId, PartW.Workspace, PartW.WrOrder);
        using (var located = copy.RepoAt(archive))
        {
            Assert.True(archiveState == await located.ExactStateAsync(), "archive/old-site changed.");
        }

        // Step 7: docs clicked, then Alt+Up: WM, docs still selected, the focus in the list (WD8).
        app.Click(RowPoint(app, "docs"));
        app.PressKey(Key.Up, RawInputModifiers.Alt);
        app.Settle();
        Shot(app, PartW.W2gMovedUp);
        var byName = w0.ToDictionary(row => row.Name, StringComparer.Ordinal);
        AssertRows(app, copy, [.. PartW.WmOrder.Select(name => byName[name])], selected: "docs");
        var docs = app.BoundsOf(app.DashboardRowControl("docs"));
        Assert.True(Math.Abs(docs.Top - 178) <= PartW.Tolerance && Math.Abs(docs.Bottom - 204) <= PartW.Tolerance, $"docs is at {docs}, not y 178 to 204.");
        Assert.True(app.FocusIsWithin(app.Find<ListBox>("DashboardList")), "The keyboard focus left the list (WD8).");
        AssertWorkspacesFile(data, copy, PartW.WorkspaceId, PartW.Workspace, PartW.WmOrder);

        // Step 8: api's menu: Move up disabled; nothing ran in git.
        menu = await app.RowMenuAsync("api");
        AssertMenu(menu, PartW.ReadyRowMenu("api", moveUpReason: PartW.FirstRepository));
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        foreach (var name in PartW.WithFolder)
        {
            using var repo = copy.Repo(name);
            Assert.True(exact[name] == await repo.ExactStateAsync(), $"{name} changed.");
        }

        AssertOnlyReads(app.GitCalls(), "W2");
    }

    /// <summary>
    /// Check W3: W2's rows at 1920×1080 in the light theme, with the Folder column (design 2.4,
    /// 2.6, 11.10; D206): every cell, the saved folders in Folder (WD29), the light tones.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_W3_ten_repositories_wide_and_light()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        await PrepareSameFolderAsync(copy);
        using var data = new TempDirectory("data");

        // Step 1.
        using var app = await OpenWorkspaceAsync(data, copy, 1920, 1080, AppTheme.Light);
        var shot = Shot(app, PartW.W3aTenRowsWideLight);
        Assert.Same(Light, PaletteOf(app));
        var dashboard = app.BoundsOf((Control)app.FindByAutomationId("Dashboard"));
        Assert.True(Math.Abs(dashboard.Left) <= 0.5 && Math.Abs(dashboard.Right - 1920) <= 0.5, $"The dashboard is at {dashboard}, not x 0 to 1920.");
        Assert.Equal(Light.Chrome, ((ISolidColorBrush)Control<Border>(app, "DashboardHeader").Background!).Color);
        AssertColour(Light.Chrome, shot, new Point(500, 106), "header's background (VcChromeBackgroundBrush, light)");
        AssertHeaderLayout(app);
        AssertHeadings(app, PartW.Columns1920, shot);
        var rows = AssertRows(app, copy, PartW.W0(copy.PathOf("old-site")));
        AssertRowPlaces(app, PartW.Names);
        foreach (var name in PartW.Names)
        {
            var folder = rows.Row(name).Folder;
            Assert.True(folder.IsShown, $"{name}'s Folder cell does not show at 1920.");
            Assert.Equal(copy.PathOf(name), folder.Text);
            var block = app.DashboardRowControl(name).GetVisualDescendants().OfType<TrimmedTextBlock>().Single(text => Avalonia.Automation.AutomationProperties.GetAutomationId(text) == "DashboardRowFolder");
            var left = app.BoundsOf(block).Left;
            Assert.True(Math.Abs(left - (132 + PartW.CellInset)) <= PartW.Tolerance, $"{name}'s Folder cell starts at x {left}, not 140.");

            // Whole where the folder fits, as on the development machine, whose temporary folder is
            // short enough, and cut with "…" where it does not (WD29): CI's Windows runner's longer
            // folder lies near the cell's width, and the scenario's random name tips it either way.
            var room = app.BoundsOf(block).Width;
            var full = new FormattedText(folder.Text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(block.FontFamily, block.FontStyle, block.FontWeight), block.FontSize, null).WidthIncludingTrailingWhitespace;
            Assert.True(
                folder.IsTrimmed == (full > room + 0.5),
                System.FormattableString.Invariant($"{name}'s folder is {full} px wide in a room of {room}: it should {(full > room + 0.5 ? "be cut with \"…\"" : "show whole")}, and it is {(folder.IsTrimmed ? "cut" : "whole")}: \"{folder.Text}\"."));
        }

        AssertArrowsAndBackgrounds(app, shot, PartW.Names);
        var bar = app.BoundsOf((Control)app.FindByAutomationId("StatusBar"));
        Assert.True(Math.Abs(bar.Top - 1054) <= PartW.Tolerance && Math.Abs(bar.Bottom - 1080) <= PartW.Tolerance, $"The status bar is at {bar}, not y 1054 to 1080.");
        AssertTd(app, PartW.Workspace, PartW.Count(10), light: true);
        Assert.Null(app.DashboardSnapshot().SelectedRow);
        Assert.Null(app.ToolTipOwnerAt(ShellDriver.NeutralPoint));
        Assert.True(ShellDriver.NeutralPoint.Y > PartW.RowCentre(9) + 13, "The neutral point is not below the tenth row.");
        AssertOnlyReads(app.GitCalls(), "W3");
    }

    /// <summary>
    /// The rows of W0 as drawn (W2 step 1, W3): each Ahead / behind cell's arrows (<c>IconArrowUp</c>,
    /// <c>IconArrowDown</c>, 10×10) in the secondary colour, and every row on no background (none
    /// hovered, none selected): the window's background at the right end of its Operation cell.
    /// </summary>
    private static void AssertArrowsAndBackgrounds(ShellDriver app, Screenshot picture, IReadOnlyList<string> names)
    {
        var palette = PaletteOf(app);
        var width = app.Window.ClientSize.Width;
        foreach (var name in names)
        {
            var row = app.DashboardRowControl(name);
            var icons = row.GetVisualDescendants().OfType<Icon>().Where(icon => icon.IsEffectivelyVisible).ToList();
            foreach (var icon in icons)
            {
                Assert.Equal((10d, 10d), (icon.Bounds.Width, icon.Bounds.Height));
                Assert.Equal(palette.TextSecondary, (icon.Foreground as ISolidColorBrush)?.Color);
            }

            var bounds = app.BoundsOf(row);
            AssertColour(palette.Window, picture, new Point(width - 10, bounds.Center.Y), $"background of the row {name} (none: VcWindowBackgroundBrush)");
        }
    }

    /// <summary>The open tooltip (W2 step 2) inside the window, in the app's tooltip colours (11.12): its background <c>VcPanelBackgroundBrush</c>, its text <c>VcTextPrimaryBrush</c>.</summary>
    private static void AssertToolTipInWindow(ShellDriver app, Screenshot picture)
    {
        var palette = PaletteOf(app);
        var tip = Assert.Single(app.Window.GetVisualDescendants().OfType<ToolTip>(), candidate => candidate.IsEffectivelyVisible);
        var bounds = app.BoundsOf(tip);
        var window = app.Window.ClientSize;
        Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= window.Width && bounds.Bottom <= window.Height, $"The tooltip at {bounds} is not inside the window.");
        Assert.Equal(palette.Panel, (tip.Background as ISolidColorBrush)?.Color);
        Assert.Equal(palette.TextPrimary, (tip.Foreground as ISolidColorBrush)?.Color);
        AssertColour(palette.Panel, picture, new Point(Math.Ceiling(bounds.Left) + 3, bounds.Center.Y), "tooltip's background (VcPanelBackgroundBrush)");
    }
}
