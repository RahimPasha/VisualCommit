// Part K of phase 6's scripted walk-through (docs/test-reports/phase-6.md, "Part K: the command
// palette and the keys"): checks K10 to K13 of design 17.4. This class is part K's alone; its
// expected texts and states are in Phase6Expectations.K.cs (Phase6Expectations.PartK), held
// against git in Phase6ExpectationTests.K.cs. One test method per app start, named as part K's
// "Test methods and app starts" names them (KD5): this file holds K10 (the palette, two methods),
// Phase6Checks.K.Keys.cs K11 and K12 (parts A and B), Phase6Checks.K.Dashboard.cs K13 (three
// methods), and Phase6Checks.K.Helpers.cs what they share. Keys are pressed as literal gestures
// with the platform's command modifier, never through PressShortcut (KD9).
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.Views;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

/// <summary>The checks of part K: the command palette and the keys (K10 to K13).</summary>
public sealed partial class Phase6ChecksK : Phase6Checks
{
    /// <summary>
    /// Check K10, method 1 (D191, D212; design 5): on "ops", Ctrl+Shift+P over the graph without
    /// dimming, the commands with their keys and the first highlighted, PageDown and the wrapping
    /// arrows, the wheel; <c>zzz</c> matching nothing and Tab staying in the box; <c>delete main</c>
    /// with a destructive and a disabled item, Enter and a click on the disabled one doing nothing,
    /// Ctrl+T declining; a second Ctrl+Shift+P selecting the query; <c>Check out feature/alpha</c> run
    /// by Enter after the palette has closed; <c>Create branch…</c> by a click, its dialog and Esc
    /// back to the graph; the session's run first in an empty palette; Esc and a press outside the
    /// card cancelling.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K10_palette_search_and_run()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var start = app.LogMark();

        // At the start: OPS0, nothing selected, nothing to undo or redo, one tab.
        AssertGraph(app, PartK.Ops0, PartK.Ops0Lanes);
        AssertDashedLines(app, PartK.Ops0Dashes);
        Assert.Empty(Selected(app));
        AssertUndoRedo(app, null, null);
        Assert.Equal([PartK.Ops], TabTitles(app));

        // Step 1: click "Main work" (row 3) in its Message column.
        var point = app.GraphRowPoint(PartK.MainWorkRow);
        AssertNear(PartK.MainWorkPoint.X, point.X, 0.5, "Main work's Message column's x");
        AssertNear(PartK.MainWorkPoint.Y, point.Y, PartK.Tolerance, "Main work's centre");
        app.Click(point);
        await WaitForDetailsAsync(app, PartK.MainWorkSha, "Main work");
        Assert.Equal([PartK.MainWorkSha], Selected(app));
        Assert.Equal(PartK.MainWorkRow, Graph(app).SelectedIndex);
        AssertFocus(app, "GraphRows", KeyRegionRole.GraphArea);
        app.WaitForCommitDetails();
        app.MoveToNeutral();
        var before = app.Capture();

        // Step 2: Ctrl+Shift+P (k10a).
        var mark = app.LogMark();
        var sequence = app.CallMark();
        var operations = app.Session.Operations.Snapshot().Count;
        PressPaletteKey(app);
        Assert.True(app.IsPaletteOpen, "Ctrl+Shift+P did not open the palette.");
        Assert.True(app.Session.Shell.Dialogs.IsOpen, "The palette is not a dialog of the dialog layer.");
        await AssertOnlyTheWorktreeReadAsync(app, sequence);
        var k10a = Shot(app, "k10a-palette-commands");
        AssertPaletteOpened(app, k10a, before);
        var snapshot = app.PaletteSnapshot();
        AssertCard(snapshot, PartK.RowsInView);
        Assert.Equal(string.Empty, snapshot.Query);
        Assert.False(snapshot.ShowsNothing);
        AssertShown(app, PartK.OpsCommands, PartK.OpsCommandCount);
        Assert.All(app.Palette.Shown, entry => Assert.Equal(PaletteEntryKind.Command, entry.Kind));
        AssertRowsInView(app, PartK.OpsCommands[..PartK.RowsInView], highlighted: 0, k10a);
        AssertScrollBar(app);
        AssertRowAutomation(app, PaletteItem(app, "Fetch all remotes"), "Fetch all remotes", null, null, "Ctrl+Shift+F", selected: true);
        AssertRowAutomation(app, PaletteItem(app, "Stash changes…"), "Stash changes…", null, PartK.NoChangesToStash, "Ctrl+Shift+S", selected: false);
        AssertPaletteLines(app, mark, PartK.PaletteOpened(PartK.Ops));
        AssertNoWrite(app, sequence, operations, "Opening the palette");

        // PageDown three times: rows 11, 22 and 32 (clamped), each whole in view; Down wraps to 0, Up back to 32.
        foreach (var expected in PartK.PageDownRows)
        {
            app.PressKey(Key.PageDown);
            AssertHighlightInView(app, expected);
        }

        app.PressKey(Key.Down);
        AssertHighlightInView(app, 0);
        app.PressKey(Key.Up);
        AssertHighlightInView(app, PartK.OpsCommandCount - 1);
        AssertRowsInView(app, PartK.OpsCommands[PartK.FirstRowInViewAtEnd..], highlighted: PartK.RowsInView - 1);
        Assert.Equal(string.Empty, app.PaletteBox.Text);

        // The wheel over the list: the pointer's move highlights the row under it (the sixth in view), the notch scrolls up.
        var offset = ListOffset(app);
        app.MoveMouse(PartK.WheelPoint);
        Assert.Equal(PartK.WheelRow, app.Palette.HighlightedIndex);
        var moved = app.Capture();
        AssertColour(PaletteOf(app).Selection, moved, new Point(PartK.BoxBounds.Left + 5, PartK.WheelPoint.Y), "row under the pointer's background (VcSelectionBrush)");
        app.Wheel(PartK.WheelPoint, 1);
        var scrolled = ListOffset(app);
        Assert.True(scrolled < offset && scrolled > 0, $"The wheel's notch left the list's offset at {scrolled} (it was {offset}): it should scroll up, not to the top.");
        Assert.Contains(RowsInView(app.PaletteSnapshot()), row => app.Palette.Shown.ToList().FindIndex(entry => entry.Label == row.Label) < PartK.FirstRowInViewAtEnd);
        Assert.Equal(0, RanLines(app, mark));
        Assert.True(app.IsPaletteOpen, "The wheel closed the palette.");
        Assert.Same(app.PaletteBox, app.FocusedElement);
        Assert.Equal(string.Empty, app.PaletteBox.Text);
        app.MoveToNeutral();

        // Step 3: zzz matches nothing; Tab stays in the box; Ctrl+A selects it, delete main replaces it (k10b).
        app.Type(PartK.QueryNothing);
        AssertNothingMatches(app);
        app.PressKey(Key.Tab);
        Assert.Same(app.PaletteBox, app.FocusedElement);
        Assert.Equal(PartK.QueryNothing, app.PaletteBox.Text);
        PressMod(app, Key.A);
        Assert.Equal((0, PartK.QueryNothing.Length), (app.PaletteBox.SelectionStart, app.PaletteBox.SelectionEnd));
        app.Type(PartK.QueryDeleteMain);
        var k10b = Shot(app, "k10b-palette-delete-main");
        snapshot = app.PaletteSnapshot();
        Assert.Equal(PartK.QueryDeleteMain, snapshot.Query);
        AssertCard(snapshot, PartK.DeleteMain.Length);
        AssertShown(app, PartK.DeleteMain, PartK.DeleteMain.Length);
        Assert.All(app.Palette.Shown, entry => Assert.Equal(PaletteEntryKind.Item, entry.Kind));
        AssertRowsInView(app, PartK.DeleteMain, highlighted: 0, k10b);
        AssertColour(PaletteOf(app).Danger, k10b, InkPoint(app, "Delete main on origin…", PaletteOf(app).Danger), "Delete main on origin…'s label (VcDangerBrush)");
        AssertReasonWhole(app, "Delete main…", PartK.CannotDeleteCurrentBranch);
        AssertRowAutomation(app, PaletteItem(app, "Delete main on origin…"), "Delete main on origin…", "origin/main", null, null, selected: true);
        AssertRowAutomation(app, PaletteItem(app, "Delete main…"), "Delete main…", "main", PartK.CannotDeleteCurrentBranch, null, selected: false);
        Assert.Equal(PartK.CannotDeleteCurrentBranch, ToolTip.GetTip(PaletteItem(app, "Delete main…")) as string);

        // Up wraps to the disabled row 1; Enter does nothing (5.7); Down back to row 0.
        sequence = app.CallMark();
        app.PressKey(Key.Up);
        Assert.Equal(1, app.Palette.HighlightedIndex);
        app.PressKey(Key.Enter);
        Assert.True(app.IsPaletteOpen, "Enter on a disabled entry closed the palette.");
        Assert.Equal(0, RanLines(app, mark));
        AssertNoWrite(app, sequence, operations, "Enter on Delete main…");
        app.PressKey(Key.Down);
        Assert.Equal(0, app.Palette.HighlightedIndex);

        // The pointer onto row 1 highlights it; a click on it runs nothing, the box keeps the focus.
        app.MoveMouse(PartK.PaletteRow1Point);
        Assert.Equal(1, app.Palette.HighlightedIndex);
        AssertColour(PaletteOf(app).Selection, app.Capture(), new Point(PartK.BoxBounds.Left + 5, PartK.PaletteRow1Point.Y), "Delete main…'s background under the pointer (VcSelectionBrush)");
        app.Click(PartK.PaletteRow1Point);
        Assert.True(app.IsPaletteOpen, "A click on a disabled entry closed the palette.");
        Assert.Equal(PartK.QueryDeleteMain, app.PaletteBox.Text);
        Assert.Same(app.PaletteBox, app.FocusedElement);
        Assert.Equal(0, RanLines(app, mark));
        AssertNoWrite(app, sequence, operations, "A click on Delete main…");
        app.MoveToNeutral();

        // Ctrl+T declines while a dialog is open (6.3).
        PressMod(app, Key.T);
        Assert.Equal([PartK.Ops], TabTitles(app));
        Assert.Equal(PartK.QueryDeleteMain, app.PaletteBox.Text);
        Assert.True(app.IsPaletteOpen);

        // Step 4: Ctrl+Shift+P selects the whole query (KD20); typing replaces it; Enter runs the checkout after the palette has closed.
        PressPaletteKey(app);
        Assert.True(app.IsPaletteOpen);
        Assert.Equal((0, PartK.QueryDeleteMain.Length), (app.PaletteBox.SelectionStart, app.PaletteBox.SelectionEnd));
        Assert.Single(Messages(app, mark, "Command palette opened"));
        app.Type(PartK.QueryCheckOut);
        Assert.Equal(PartK.QueryCheckOut, app.Palette.Query);
        AssertShown(app, PartK.CheckOutRows, PartK.CheckOutRows.Length);
        AssertRowsInView(app, PartK.CheckOutRows, highlighted: 0);
        long? closedAt = null;
        void OnRemoving(object? sender, DialogViewModel dialog)
        {
            if (dialog is CommandPaletteViewModel)
            {
                closedAt = RecordSequence.Next();
            }
        }

        var logMark = app.LogMark();
        operations = app.Session.Operations.Snapshot().Count;
        app.Session.Services.DialogHost.Removing += OnRemoving;
        try
        {
            app.PressKey(Key.Enter);
        }
        finally
        {
            app.Session.Services.DialogHost.Removing -= OnRemoving;
        }

        Assert.False(app.IsPaletteOpen, "Enter left the palette open.");
        await WaitForOperationAsync(app, repo, PartK.CheckOutFeatureAlpha, logMark);
        AssertFocus(app, "GraphRows", KeyRegionRole.GraphArea);
        AssertPaletteLines(app, logMark, PartK.PaletteRan(PartK.Ops, PartK.CheckOutFeatureAlphaLabel, "feature/alpha"));
        var checkout = AssertOperation(app, operations, PartK.CheckOutFeatureAlpha, PartK.SwitchFeatureAlpha);
        Assert.True(closedAt is { } closed && checkout.Record.Sequence > closed, "The checkout started before the palette had closed (5.9).");
        Assert.Single(checkout.Calls, IsWriteCommand);
        Assert.Contains(PartK.SwitchKey, Lines(checkout.Call.StandardError));
        if (await IsReportGitAsync())
        {
            Assert.Equal([PartK.SwitchOutput], Lines(checkout.Call.StandardOutput));
            Assert.Equal([PartK.SwitchKey], Lines(checkout.Call.StandardError));
        }

        await app.WaitForLogLineAsync(AppLogFile.OperationEnded(PartK.Ops, PartK.CheckOutFeatureAlpha, OperationOutcome.Succeeded, PartK.CheckedOutFeatureAlpha), mark: logMark);
        Assert.Equal("refs/heads/feature/alpha", (await GitLinesAsync(repo, "symbolic-ref", "HEAD")).Single());
        Assert.Equal(PartK.AlphaTwoSha, await RevParseAsync(repo, "HEAD"));
        Assert.Equal([PartK.AlphaBranchLine], await StatusAsync(repo));
        Assert.Equal([$"stash@{{0}}: {PartK.StashMessage}"], await StashListAsync(repo));

        // The picture (k10c): K-OPSa, Main work still selected with its details, feature/alpha the branch.
        await WaitForDetailsAsync(app, PartK.MainWorkSha, "Main work");
        var k10c = Shot(app, "k10c-checked-out-by-palette");
        AssertGraph(app, PartK.OpsA, PartK.Ops0Lanes, picture: k10c);
        Assert.Equal([PartK.MainWorkSha], Selected(app));
        var folder = RefItem(app, "Local branches", "feature");
        Assert.True(folder.IsOpen, "The Local branches' feature folder is closed although it holds HEAD's branch.");
        Assert.True(RefItem(app, "Local branches", "feature", "alpha").IsHead, "feature/alpha is not the left panel's current branch.");
        Assert.False(RefItem(app, "Local branches", "main").IsHead, "main is still the left panel's current branch.");
        Assert.Equal(PaletteOf(app).Accent, await RefNameColourAsync(app, "Local branches", "feature", "alpha"));
        Assert.NotEqual(PaletteOf(app).Accent, await RefNameColourAsync(app, "Local branches", "main"));
        AssertStatus(app, PartK.CheckedOutFeatureAlpha);
        AssertBranch(app, "feature/alpha");
        AssertUndoRedo(app, PartK.CheckoutOfFeatureAlpha, null);

        // Step 5: create branch; a click on row 0 runs Create branch… as Enter would; its dialog (k10d); Esc.
        mark = app.LogMark();
        PressPaletteKey(app);
        app.Type(PartK.QueryCreateBranch);
        snapshot = app.PaletteSnapshot();
        AssertCard(snapshot, PartK.CreateBranch.Length);
        AssertShown(app, PartK.CreateBranch, PartK.CreateBranch.Length);
        AssertRowsInView(app, PartK.CreateBranch, highlighted: 0);
        app.MoveMouse(PartK.PaletteRow0Point);
        Assert.Equal(0, app.Palette.HighlightedIndex);
        app.Click(PartK.PaletteRow0Point);
        var dialog = await app.WaitForDialogAsync<NameDialogViewModel>();
        Assert.False(app.IsPaletteOpen, "The click left the palette open.");
        AssertPaletteLines(app, mark, PartK.PaletteOpened(PartK.Ops), PartK.PaletteRan(PartK.Ops, "Create branch…"));
        var k10d = Shot(app, "k10d-create-branch-from-palette");
        await AssertCreateBranchDialogAsync(app, dialog, PartK.FromAlphaTwo, k10d);
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.Equal(PartK.Ops0LocalBranches, await GitLinesAsync(repo, "for-each-ref", "--format=%(refname)", "refs/heads"));
        AssertFocus(app, "GraphRows", KeyRegionRole.GraphArea);

        // Step 6: the empty palette lists the session's run first (5.6); Esc; then a press outside the card.
        mark = app.LogMark();
        PressPaletteKey(app);
        AssertShown(app, PartK.AfterTheRuns, PartK.OpsCommandCount);
        AssertRowsInView(app, PartK.AfterTheRuns, highlighted: 0);
        app.PressKey(Key.Escape);
        Assert.False(app.IsPaletteOpen, "Esc left the palette open.");
        AssertFocus(app, "GraphRows", KeyRegionRole.GraphArea);
        PressPaletteKey(app);
        Assert.True(app.IsPaletteOpen);
        var menuMark = app.LogMark();
        app.Click(PartK.OutsideCardPoint);
        Assert.False(app.IsPaletteOpen, "The press outside the card left the palette open.");
        await AssertNoMenuAsync(app, menuMark);
        Assert.Equal([PartK.MainWorkSha], Selected(app));
        AssertFocus(app, "GraphRows", KeyRegionRole.GraphArea);
        Assert.Equal(0, RanLines(app, mark));

        // Over the method: four openings and two runs.
        Assert.Equal(4, Messages(app, start, PartK.PaletteOpened(PartK.Ops)).Count);
        Assert.Equal(2, RanLines(app, start));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check K10, method 2 (design 5.5, 5.10, 3.3, 3.8, 2.2): on "Web"'s dashboard, the dashboard's
    /// commands; <c>Open repository web-app</c> opening a tab at the end that adopts the row's view
    /// model; beside the dashboard the repository's commands naming it; <c>Open workspace Web</c>
    /// going back to the dashboard.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K10_palette_opens_repositories_and_workspaces()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");
        await PrepareSameFolderAsync(copy);
        using var app = await OpenWorkspaceAsync(data, copy);
        var oldSite = copy.PathOf("old-site");

        // At the start: the dashboard of "Web" the only tab, its rows as scenario facts 2.3, none selected.
        Assert.Equal([PartK.Workspace], TabTitles(app));
        AssertRows(app, PartK.Web(oldSite));
        Assert.Empty(app.Session.Services.Recent.Items);

        // Step 7: Ctrl+Shift+P: the dashboard's commands, no prefix on the opening line.
        var mark = app.LogMark();
        PressPaletteKey(app);
        Assert.True(app.IsPaletteOpen);
        AssertPaletteLines(app, mark, PartK.PaletteOpened(null));
        AssertShown(app, PartK.DashboardCommands, PartK.DashboardCommandCount);
        AssertRowsInView(app, PartK.DashboardCommands[..PartK.RowsInView], highlighted: 0);
        var dashboardBefore = app.DashboardSnapshot();

        // Typing: Open repository web-app with its folder as saved, cut at its end where it does not fit (k10e).
        app.Type(PartK.QueryOpenWebApp);
        var webAppFolder = copy.PathOf(PartK.WebApp);
        var k10e = Shot(app, "k10e-palette-open-repository");
        var snapshot = app.PaletteSnapshot();
        AssertCard(snapshot, 1);
        AssertShown(app, [PartK.OpenWebApp(webAppFolder)], 1);
        AssertRowsInView(app, [PartK.OpenWebApp(webAppFolder)], highlighted: 0, k10e);
        var item = PaletteItem(app, "Open repository web-app");
        AssertRowAutomation(app, item, "Open repository web-app", webAppFolder, null, null, selected: true);
        AssertFolderCut(app, item, webAppFolder);
        var dashboardBehind = app.DashboardSnapshot();
        Assert.Equal(dashboardBefore.Rows.Select(row => row.ToString()), dashboardBehind.Rows.Select(row => row.ToString()));

        // Enter: a new tab at the end opens web-app, adopting the row's summary-mode view model (one queue).
        var row = app.ActiveDashboard!.Rows.Single(candidate => candidate.Name == PartK.WebApp);
        var sequence = app.CallMark();
        mark = app.LogMark();
        app.PressKey(Key.Enter);
        Assert.False(app.IsPaletteOpen);
        var webApp = await app.WaitForTabOpenedAsync(webAppFolder, OperationChainTimeout);
        AssertPaletteLines(app, mark, PartK.PaletteRan(null, "Open repository web-app", webAppFolder));
        Assert.Equal([PartK.Workspace, PartK.WebApp], TabTitles(app));
        Assert.True(app.Session.Shell.Tabs[0].IsDashboard, "The first tab is no longer the dashboard.");
        Assert.Equal(PartK.WebApp, ActiveTitle(app));
        Assert.Same(row.Repository, webApp);
        Assert.Equal(PartK.WebApp, app.Session.Services.Recent.Items[0].Name);
        Assert.True(SameFolder(copy, webAppFolder, app.Session.Services.Recent.Items[0].Path), $"The recent list's first is {app.Session.Services.Recent.Items[0].Path}.");
        AssertGraph(app, PartK.WebAppGraph, 1);
        Assert.Equal(2, RefItem(app, "Local branches", "main").Ahead);
        AssertBranch(app, "main");
        foreach (var name in PartK.WithFolder.Where(name => name != PartK.WebApp))
        {
            var calls = app.CallsSince(sequence, folder => SameFolder(copy, copy.PathOf(name), folder));
            Assert.True(calls.Count == 0, $"The dashboard in the background read {name}:\n" + string.Join('\n', calls.Select(call => call.CommandText)));
        }

        // Step 8: web-app's palette beside the dashboard: its commands name it (k10f).
        mark = app.LogMark();
        PressPaletteKey(app);
        await app.Session.Shell.PaletteWorktreeRead;
        var k10f = Shot(app, "k10f-palette-names-repository");
        AssertPaletteLines(app, mark, PartK.PaletteOpened(PartK.WebApp));
        AssertShown(app, PartK.WebAppCommands, PartK.WebAppCommandCount);
        AssertRowsInView(app, PartK.WebAppCommands, highlighted: 0, k10f);
        AssertRowAutomation(app, PaletteItem(app, "Stash changes…"), "Stash changes…", PartK.WebApp, PartK.NoChangesToStash, "Ctrl+Shift+S", selected: false);
        var shown = app.Palette.Shown.ToList();
        foreach (var expected in PartK.WebAppTabCommands)
        {
            var entry = Assert.Single(shown, candidate => candidate.Label == expected.Label);
            Assert.Equal(WordsEntry(app, expected).ToString(), Described(entry));
        }

        // Typing: Open workspace Web; Enter activates the open dashboard (2.2's first case), no tab made.
        app.Type(PartK.QueryOpenWorkspace);
        AssertShown(app, [PartK.OpenWorkspaceWeb], 1);
        mark = app.LogMark();
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.IsDashboard, "the dashboard of Web to be active");
        AssertPaletteLines(app, mark, PartK.PaletteRan(PartK.WebApp, PartK.OpenWorkspaceWeb.Label, PartK.OpenWorkspaceWeb.Detail));
        Assert.Equal([PartK.Workspace, PartK.WebApp], TabTitles(app));
        Assert.Equal(0, app.Session.Shell.ActiveTabIndex);
        await app.WaitForAsync(() => SavedActiveTab(data) == 0, "session.json's active tab to be 0");
        await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
    }

    /// <summary>
    /// The palette as Ctrl+Shift+P opens it over "ops" (K10 step 2): its root a Pane named
    /// <c>Command palette</c>, no title drawn; the backdrop transparent, so that around the card the
    /// window shows as <paramref name="before"/> did (no dimming); the card in 5.3's look; the box
    /// named <c>Search commands</c>, empty, its placeholder whole, the keyboard focus in it; the list
    /// named <c>Commands</c> from y 143.
    /// </summary>
    private static void AssertPaletteOpened(ShellDriver app, Screenshot picture, Screenshot before)
    {
        var view = app.PaletteView;
        var palette = PaletteOf(app);
        Assert.Equal("CommandPalette", AutomationProperties.GetAutomationId(view));
        Assert.Equal(PartK.PaletteName, AutomationProperties.GetName(view));
        Assert.Equal(AutomationControlType.Pane, ControlAutomationPeer.CreatePeerForElement(view).GetAutomationControlType());
        Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), block => block.IsEffectivelyVisible && block.Text == PartK.PaletteName);

        // No dimming: a transparent backdrop, the window around the card as before.
        var backdrop = view.GetVisualDescendants().OfType<Panel>().First(panel => panel.Classes.Contains("paletteBackdrop"));
        Assert.Equal(Colors.Transparent, (backdrop.Background as ISolidColorBrush)?.Color);
        var card = (Border)app.FindByAutomationId(view, "CommandPaletteCard");
        var cardBounds = app.BoundsOf(card);
        var shadow = new Rect(cardBounds.Left - 24, cardBounds.Top - 24, cardBounds.Width + 48, cardBounds.Height + 48);
        var differences = DifferencesOutside(before, picture, shadow);
        Assert.True(differences.Count == 0, "Around the palette's card the window does not show as before (it is dimmed or changed):\n" + string.Join('\n', differences));

        // 5.3's look: the panel's background, a 1-px border of VcBorderBrush, corner 8, the menus' shadow.
        Assert.Equal(palette.Panel, (card.Background as ISolidColorBrush)?.Color);
        Assert.Equal(palette.Border, (card.BorderBrush as ISolidColorBrush)?.Color);
        Assert.Equal(new Thickness(1), card.BorderThickness);
        Assert.Equal(new CornerRadius(8), card.CornerRadius);
        Assert.Equal(BoxShadows.Parse(palette.MenuShadow), card.BoxShadow);
        AssertColour(palette.Panel, picture, cardBounds.TopLeft + new Point(10, 4), "card's background (VcPanelBackgroundBrush)");
        AssertColour(palette.Border, picture, new Point(cardBounds.Left, cardBounds.Center.Y), "card's left border (VcBorderBrush)");

        // The box: named, empty, its placeholder whole, the focus in it.
        var box = app.PaletteBox;
        Assert.Equal("CommandPaletteBox", AutomationProperties.GetAutomationId(box));
        Assert.Equal(PartK.PaletteBoxName, AutomationProperties.GetName(box));
        Assert.Equal(AutomationControlType.Edit, ControlAutomationPeer.CreatePeerForElement(box).GetAutomationControlType());
        Assert.True(string.IsNullOrEmpty(box.Text), $"The box holds \"{box.Text}\".");
        Assert.Equal(PartK.Placeholder, box.PlaceholderText);
        var placeholder = box.GetVisualDescendants().OfType<TextBlock>().Single(block => block.IsEffectivelyVisible && block.Text == PartK.Placeholder);
        Assert.Single(placeholder.TextLayout.TextLines);
        Assert.True(placeholder.TextLayout.WidthIncludingTrailingWhitespace <= placeholder.Bounds.Width + 0.5, $"The placeholder is {placeholder.TextLayout.WidthIncludingTrailingWhitespace} wide in {placeholder.Bounds.Width}: it is cut.");
        Assert.True(app.BoundsOf(placeholder).Right <= app.BoundsOf(box).Right, "The placeholder runs past the box.");
        Assert.Same(box, app.FocusedElement);

        // The list: named, from y 143.
        var list = (ListBox)app.FindByAutomationId(view, "CommandPaletteList");
        Assert.Equal(PartK.PaletteListName, AutomationProperties.GetName(list));
        Assert.Equal(AutomationControlType.List, ControlAutomationPeer.CreatePeerForElement(list).GetAutomationControlType());
        AssertNear(PartK.ListTop, app.BoundsOf(list).Top, 0.5, "The list's top");
    }

    /// <summary>The theme's thin vertical scroll bar at the list's right (5.3): shown while the list holds more than 12 rows.</summary>
    private static void AssertScrollBar(ShellDriver app)
    {
        var list = (ListBox)app.FindByAutomationId(app.PaletteView, "CommandPaletteList");
        var bar = list.GetVisualDescendants().OfType<ScrollBar>().SingleOrDefault(candidate => candidate.Orientation == Orientation.Vertical && candidate.IsEffectivelyVisible)
            ?? throw new Xunit.Sdk.XunitException("The palette's list shows no vertical scroll bar.");
        var listBounds = app.BoundsOf(list);
        AssertNear(listBounds.Right, app.BoundsOf(bar).Right, PartK.Tolerance, "The scroll bar's right edge");
        Assert.DoesNotContain(list.GetVisualDescendants().OfType<ScrollBar>(), candidate => candidate.Orientation == Orientation.Horizontal && candidate.IsEffectivelyVisible);
    }

    /// <summary>The highlight on Shown's <paramref name="index"/>, its row whole in the list's view (5.8).</summary>
    private static void AssertHighlightInView(ShellDriver app, int index)
    {
        Assert.Equal(index, app.Palette.HighlightedIndex);
        var row = Assert.Single(app.PaletteSnapshot().Rows, candidate => candidate.IsHighlighted);
        Assert.Equal(app.Palette.Shown[index].Label, row.Label);
        Assert.True(
            row.Bounds.Top >= PartK.ListTop - 0.5 && row.Bounds.Bottom <= PartK.ListTop + (PartK.RowHeight * PartK.RowsInView) + 0.5,
            $"The highlighted row {index} ({row.Label}) at {row.Bounds} is not whole in the list's view.");
    }

    /// <summary>
    /// <c>zzz</c> (K10 step 3): nothing matches, nothing highlighted, no list; <c>No matches</c> in its
    /// place, in the secondary colour, 28 high at y 143 to 171, its text from x 269; the card 84 high.
    /// </summary>
    private static void AssertNothingMatches(ShellDriver app)
    {
        var snapshot = app.PaletteSnapshot();
        Assert.True(snapshot.ShowsNothing, "Something matches zzz.");
        Assert.Null(app.Palette.Highlighted);
        Assert.Empty(app.Palette.Shown);
        Assert.False(app.FindByAutomationId(app.PaletteView, "CommandPaletteList").IsEffectivelyVisible, "The list still shows.");
        var empty = (TextBlock)app.FindByAutomationId(app.PaletteView, "CommandPaletteEmpty");
        Assert.True(empty.IsEffectivelyVisible, "No matches does not show.");
        Assert.Equal(PartK.NoMatches, empty.Text);
        Assert.Equal(PaletteOf(app).TextSecondary, (empty.Foreground as ISolidColorBrush)?.Color);
        var holder = app.BoundsOf((Control)empty.GetVisualParent()!);
        AssertNear(new Rect(PartK.BoxBounds.Left, PartK.ListTop, PartK.BoxBounds.Width, PartK.RowHeight), holder, 0.5, "No matches' row");
        AssertNear(PartK.LabelLeft, app.BoundsOf(empty).Left, PartK.Tolerance, "No matches' text's left");
        AssertCard(snapshot, 1);
    }

    /// <summary>A disabled row's reason drawn whole in the detail's place, in the disabled colour (5.7, KD14).</summary>
    private static void AssertReasonWhole(ShellDriver app, string label, string reason)
    {
        var detail = PaletteItem(app, label).GetVisualDescendants().OfType<TrimmedTextBlock>().Single(block => block.Name == "CommandPaletteItemDetail");
        Assert.Equal(reason, detail.Text);
        Assert.False(detail.IsTrimmed, $"{label}'s reason is cut with \"…\".");
        Assert.Equal(PaletteOf(app).TextDisabled, (detail.Foreground as ISolidColorBrush)?.Color);
    }

    /// <summary>A pixel of <paramref name="label"/>'s row's label drawn in <paramref name="colour"/> (its ink), for a colour assertion on the picture.</summary>
    private static Point InkPoint(ShellDriver app, string label, Color colour)
    {
        var block = PaletteItem(app, label).GetVisualDescendants().OfType<TextBlock>().Single(candidate => candidate.Name == "CommandPaletteItemLabel");
        var bounds = app.BoundsOf(block);
        var picture = app.Capture();
        for (var y = (int)Math.Ceiling(bounds.Top); y < bounds.Bottom; y++)
        {
            for (var x = (int)Math.Ceiling(bounds.Left); x < bounds.Right; x++)
            {
                if (picture.PixelAt(x, y) == colour)
                {
                    return new Point(x, y);
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"No pixel of {label}'s label is drawn in {colour}.");
    }

    /// <summary>
    /// <c>Open repository web-app</c>'s detail (k10e, KD4 and the lead's ruling 4): the whole folder
    /// as its item status; the detail cut at its end with "…" exactly when the folder is wider than
    /// its room (the row's content less the label and the gap, no key column), else whole; its
    /// label 152.1 px wide (±2).
    /// </summary>
    private static void AssertFolderCut(ShellDriver app, ListBoxItem item, string folder)
    {
        var parts = item.GetVisualDescendants().OfType<TrimmedTextBlock>().ToDictionary(block => block.Name!, block => block);
        var label = parts["CommandPaletteItemLabel"];
        var detail = parts["CommandPaletteItemDetail"];
        AssertNear(PartK.OpenWebAppLabelWidth, label.DesiredSize.Width, PartK.Tolerance, "Open repository web-app's label's width");
        Assert.Equal(folder, detail.Text);
        var room = app.BoundsOf(detail).Width;
        var full = new FormattedText(folder, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(detail.FontFamily, detail.FontStyle, detail.FontWeight), detail.FontSize, null).WidthIncludingTrailingWhitespace;
        Assert.True(
            detail.IsTrimmed == (full > room + 0.5),
            System.FormattableString.Invariant($"The folder is {full} px wide in a room of {room}: it should {(full > room + 0.5 ? "be cut with \"…\"" : "show whole")}, and it is {(detail.IsTrimmed ? "cut" : "whole")}."));
        AssertNear(PartK.ContentRight, app.BoundsOf(detail).Right, PartK.Tolerance, "The detail's right (no key column)");
    }

    /// <summary>
    /// The name dialog of Create branch (phase 3's, K10 step 5, K12 step 20): its title, context,
    /// the name box empty with its placeholder and the focus, no error, the option ticked, Cancel and
    /// Create branch (primary, disabled), over the dialog layer's dimming backdrop.
    /// </summary>
    private static async Task AssertCreateBranchDialogAsync(ShellDriver app, NameDialogViewModel dialog, string context, Screenshot? picture)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The name dialog has no view.");
        Assert.Equal(PartK.CreateBranchTitle, dialog.Title);
        Assert.Equal(PartK.CreateBranchTitle, ((TextBlock)app.FindByAutomationId(view, "NameDialogTitle")).Text);
        Assert.Equal(context, ((TextBlock)app.FindByAutomationId(view, "NameDialogContext")).Text);
        var box = (TextBox)app.FindByAutomationId(view, "NameBox");
        Assert.True(string.IsNullOrEmpty(box.Text), $"The name box holds \"{box.Text}\".");
        Assert.Equal(PartK.BranchNamePlaceholder, box.PlaceholderText);
        await app.WaitForAsync(() => box.IsFocused, "the name box to have the keyboard focus");
        Assert.False(app.FindByAutomationId(view, "NameError").IsEffectivelyVisible, "The name dialog shows an error.");
        var option = (CheckBox)app.FindByAutomationId(view, "NameDialogOption");
        Assert.Equal((PartK.CheckOutTheNewBranch, (bool?)true), (option.Content as string, option.IsChecked));
        var cancel = (Button)app.FindByAutomationId(view, "CancelButton");
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Equal((PartK.Cancel, PartK.CreateBranchTitle), (cancel.Content as string, confirm.Content as string));
        Assert.False(confirm.IsEffectivelyEnabled, "Create branch is enabled with no name.");
        Assert.True(app.BoundsOf(cancel).Right < app.BoundsOf(confirm).Left, "Cancel is not left of Create branch.");
        var backdrop = view.GetVisualDescendants().Prepend(view).OfType<Panel>().First(panel => panel.Classes.Contains("dialogBackdrop"));
        Assert.Equal(PaletteOf(app).Backdrop, (backdrop.Background as ISolidColorBrush)?.Color);
        if (picture is not null)
        {
            Assert.NotEqual(PaletteOf(app).Chrome, picture.PixelAt(ShellDriver.NeutralPoint));
        }
    }
}
