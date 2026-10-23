// Part K's checks K11 (keys in menus and tooltips) and K12 (every main action from the keyboard
// alone, parts A and B; KD5) (docs/test-reports/phase-6.md, part K). The class's map is in
// Phase6Checks.K.cs, what the checks share in Phase6Checks.K.Helpers.cs.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksK
{
    /// <summary>
    /// Check K11 (D192, D199, D215; design 6.7, 6.8): the empty graph area's menu and the
    /// working-changes row's with their keys at the right; the newest stash's <c>Pop stash</c> keyed
    /// and another branch's push not; every toolbar button's tooltip and help text with its key while
    /// enabled, a disabled one's reason alone, every keyed control's <c>AcceleratorKey</c>; the commit
    /// button's <c>Commit 1 file (Ctrl+Enter)</c> with a file staged and a summary typed and no
    /// tooltip while it cannot commit; the activity log toggle's, "+"'s and the welcome page's; the
    /// close button's key only on the active tab, following a switch.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K11_keys_in_menus_and_tooltips()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: the empty graph area's menu (k11a).
        var mark = app.LogMark();
        app.RightClick(PartK.EmptyAreaPoint);
        var menu = app.Menu();
        Shot(app, "k11a-empty-area-menu-keys", keepPointer: true);
        AssertMenuAtPointer(menu, PartK.EmptyAreaPoint);
        AssertKeyedMenu(app, menu, PartK.EmptyAreaMenu, PartK.EmptyAreaMenuWidth);
        AssertNear(PartK.EightItemsTwoSeparators, menu.Bounds.Height, 2, "The empty graph area's menu's height");
        AssertHighlighted(menu, PartK.FetchAllRemotes);
        Assert.Equal([PartK.MenuOpened(PartK.Ops, PartK.Ops, PartK.ByPointer)], Messages(app, mark, "Context menu opened"));
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 2: the toolbar's, the tab strip's and the status bar's tooltips, help texts and keys.
        foreach (var expected in PartK.OpsToolbar)
        {
            AssertKeyed(app, expected);
        }

        AssertKeyed(app, PartK.AddRepo);
        Assert.Equal(PartK.NewTabButtonName, AutomationProperties.GetName(app.FindByAutomationId("AddRepoButton")));
        AssertKeyed(app, CloseButtonOf(app, PartK.Ops), PartK.CloseActiveTab);
        AssertKeyed(app, PartK.ShowLog);
        var toolbar = app.Session.Shell.ActiveTab.Toolbar;
        Assert.Equal(
            (null, null, null, null, PartK.NoChangesToStash, null),
            (toolbar.FetchDisabledReason, toolbar.PullDisabledReason, toolbar.PushDisabledReason, toolbar.BranchDisabledReason, toolbar.StashDisabledReason, toolbar.PopDisabledReason));

        // Push's tooltip with its key under the pointer (k11b, KD23); Stash's reason alone.
        var push = ToolbarButton(app, "PushButton");
        Assert.Equal(Words(app, "Push main to origin/main (Ctrl+Shift+K)"), await app.RestPointerOnAsync(push));
        var k11b = Shot(app, "k11b-push-tooltip-key", keepPointer: true);
        AssertToolTipLook(app, push, Words(app, "Push main to origin/main (Ctrl+Shift+K)"), k11b);
        var tip = app.BoundsOf(app.Window.GetVisualDescendants().OfType<ToolTip>().Single(candidate => candidate.IsEffectivelyVisible));
        Assert.True(tip.Top > app.BoundsOf(push).Center.Y, $"Push's tooltip at {tip} is not below the pointer resting on the button.");
        var stash = ToolbarButton(app, "StashButton");
        Assert.Equal(PartK.NoChangesToStash, await app.RestPointerOnAsync(stash));
        Assert.DoesNotContain("(", PartK.NoChangesToStash, StringComparison.Ordinal);

        // The newest stash's menu: only Pop stash keyed; another branch's menu: no key, its push included.
        app.RightClick(PartK.TopRowPoint);
        menu = app.Menu();
        AssertKeyedMenu(app, menu, PartK.StashMenu, width: null);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        app.RightClick(app.GraphLabelBounds(PartK.AlphaTwoRow, "feature/alpha").Center);
        menu = app.Menu();
        Assert.Equal("feature/alpha", menu.Header);
        Assert.Contains(menu.Items, item => item.Label == PartK.PushFeatureAlpha);
        Assert.All(menu.Items, item => Assert.True(item.KeyText is null, $"feature/alpha's item \"{item.Label}\" shows the key {item.KeyText} (6.7: another branch's items are not keyed)."));
        Assert.All(menu.Items, item => Assert.Null(NullIfEmpty(AutomationProperties.GetAcceleratorKey(app.MenuItemControl(item.Label)))));
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        await WaitForDetailsAsync(app, PartK.AlphaTwoSha, "Alpha two");

        // Step 3: src/core.txt changed outside the app: K-OPSw; Alpha two still selected; Stash enabled with its key.
        mark = app.LogMark();
        repo.WriteFile(PartK.CoreFile, PartK.CoreMore);
        await WaitForStatusReadAsync(app, 0, 1, mark);
        AssertGraph(app, PartK.Ops0, PartK.Ops0Lanes, PartK.OpsWorking);
        Assert.Equal([PartK.AlphaTwoSha], Selected(app));
        Assert.False(Repository(app).IsWorkingRowSelected, "The working-changes row is selected.");
        Assert.Null(Shown(app, "StagePanel"));
        AssertKeyed(app, PartK.StashEnabled);
        Assert.Null(app.Session.Shell.ActiveTab.Toolbar.StashDisabledReason);

        // Step 4: the working-changes row's menu (k11c); Stage all.
        var working = app.WorkingRowPoint();
        AssertNear(PartK.TopRowPoint.X, working.X, 0.5, "The working-changes row's Message column's x");
        AssertNear(PartK.TopRowPoint.Y, working.Y, PartK.Tolerance, "The working-changes row's centre");
        mark = app.LogMark();
        app.RightClick(working);
        menu = app.Menu();
        Shot(app, "k11c-working-row-menu-keys", keepPointer: true);
        Assert.True(Repository(app).IsWorkingRowSelected, "The right-click did not select the working-changes row.");
        Assert.True(app.FindByAutomationId("StagePanel").IsEffectivelyVisible, "The stage panel does not show behind the menu.");
        AssertMenuAtPointer(menu, working);
        AssertKeyedMenu(app, menu, PartK.WorkingChangesMenu, PartK.WorkingMenuWidth);
        AssertNear(PartK.EightItemsTwoSeparators, menu.Bounds.Height, 2, "The working-changes row's menu's height");
        AssertHighlighted(menu, PartK.StageAll);
        var sequence = app.CallMark();
        var operations = app.Session.Operations.Snapshot().Count;
        mark = app.LogMark();
        app.ClickMenuItem(PartK.StageAll);
        await WaitForStatusReadAsync(app, 1, 0, mark);
        await app.WaitForIdleAsync();
        var writes = app.CallsSince(sequence).Where(IsWriteCommand).ToList();
        Assert.True(writes.Count == 1 && writes[0].CommandText == PartK.AddCore && writes[0].Succeeded, "Stage all wrote:\n" + string.Join('\n', writes.Select(call => call.CommandText)));
        Assert.Equal([PartK.MainBranchLine, PartK.CoreStaged], await StatusAsync(repo));
        await AssertStagePanelAsync(app, unstaged: [], staged: [("M", PartK.CoreName, PartK.CoreFolder)], PartK.CommitOneFile, enabled: false);
        Assert.Single(OperationsSince(app, operations));

        // Step 5: the disabled commit button has no tooltip; with a summary it says what it does and its key (k11d).
        var commit = (Button)app.FindByAutomationId("CommitButton");
        app.MoveMouse(commit);
        Assert.Null(ToolTip.GetTip(commit));
        Assert.Null(Repository(app).Changes.CommitDisabledReason);
        Assert.Null(NullIfEmpty(ShellDriver.HelpTextOf(commit)));
        Assert.Equal(Words(app, PartK.CommitKey), ShellDriver.AcceleratorKeyOf(commit));
        var summary = (TextBox)app.FindByAutomationId("CommitSummary");
        app.Click(summary);
        app.Type(PartK.NoteMoreCoreWork);
        Assert.Equal(PartK.NoteMoreCoreWork, summary.Text);
        Assert.Equal(PartK.Counter(PartK.NoteMoreCoreWork), TextOf(app, "SummaryCounter"));
        Assert.True(commit.IsEffectivelyEnabled, "The commit button is disabled with a file staged and a summary.");
        Assert.Equal(PartK.CommitOneFile, ButtonText(commit));
        AssertKeyed(app, commit, new PartK.Keyed("CommitButton", true, PartK.CommitOneFileToolTip, PartK.CommitKey));
        Assert.Equal(Words(app, PartK.CommitOneFileToolTip), await app.RestPointerOnAsync(commit));
        var k11d = Shot(app, "k11d-commit-tooltip-key", keepPointer: true);
        AssertToolTipLook(app, commit, Words(app, PartK.CommitOneFileToolTip), k11d);
        Assert.Same(summary, app.FocusedElement);
        Assert.Equal(PartK.MainWorkSha, await RevParseAsync(repo, "refs/heads/main"));

        // Step 6: the activity log toggle; "+"; the close buttons follow the active tab.
        var toggle = app.FindByAutomationId("ActivityLogToggle");
        Assert.Equal(Words(app, PartK.ShowActivityLog), await app.RestPointerOnAsync(toggle));
        app.Click(app.FindByAutomationId("AddRepoButton"));
        await app.WaitForAsync(() => app.Session.Shell.Tabs.Count == 2, "a second tab");
        Assert.Equal([PartK.Ops, PartK.NewTabTitle], TabTitles(app));
        Assert.Equal(PartK.NewTabTitle, ActiveTitle(app));
        Assert.True(app.ActiveWelcomeView.IsEffectivelyVisible, "The new tab shows no welcome page.");
        foreach (var expected in PartK.WelcomeButtons)
        {
            AssertKeyed(app, expected);
        }

        foreach (var expected in PartK.EmptyTabToolbar)
        {
            AssertKeyed(app, expected);
        }

        AssertKeyed(app, PartK.ShowLog with { Enabled = false });
        Assert.Equal(PartK.CloseOtherTab.ToolTip, await app.RestPointerOnAsync(CloseButtonOf(app, PartK.Ops)));
        AssertKeyed(app, CloseButtonOf(app, PartK.Ops), PartK.CloseOtherTab);
        Assert.Equal(Words(app, PartK.CloseActiveTab.ToolTip!), await app.RestPointerOnAsync(CloseButtonOf(app, PartK.NewTabTitle)));
        AssertKeyed(app, CloseButtonOf(app, PartK.NewTabTitle), PartK.CloseActiveTab);
        app.Click(TabTitleOf(app, PartK.Ops));
        await app.WaitForAsync(() => ActiveTitle(app) == PartK.Ops, "the ops tab to be active");
        Assert.Equal(Words(app, PartK.CloseActiveTab.ToolTip!), await app.RestPointerOnAsync(CloseButtonOf(app, PartK.Ops)));
        AssertKeyed(app, CloseButtonOf(app, PartK.Ops), PartK.CloseActiveTab);
        Assert.Equal(PartK.CloseOtherTab.ToolTip, await app.RestPointerOnAsync(CloseButtonOf(app, PartK.NewTabTitle)));
        AssertKeyed(app, CloseButtonOf(app, PartK.NewTabTitle), PartK.CloseOtherTab);
        Assert.Equal(PartK.MainWorkSha, await RevParseAsync(repo, "refs/heads/main"));
    }

    /// <summary>
    /// Check K12, part A (R7, D192, D213, D214; design 6.1 to 6.6, 6.10): no pointer event after the
    /// start. Ctrl+T, Ctrl+O with the folder dialog, the tab keys round the strip; a disabled
    /// command's key consumed; Ctrl+Shift+E, F6 and Shift+F6 round the regions with 6.6's targets
    /// (k12a to k12d); Stage all, Go to the commit message, a summary and Ctrl+Enter committing;
    /// Ctrl+Z in a text box undoing typing only; Enter on a file row, Ctrl+Shift+H and Ctrl+Shift+B;
    /// Ctrl+Z and Ctrl+Y undoing and redoing the commit (D163).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K12_tabs_regions_and_the_commit_by_keys()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile(PartK.CoreFile, PartK.CoreKeys);
        using var graph = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // At the start: K-OPSw, nothing selected, the details' placeholder, one tab.
        AssertGraph(app, PartK.Ops0, PartK.Ops0Lanes, PartK.OpsWorking);
        Assert.Empty(Selected(app));
        Assert.False(Repository(app).IsWorkingRowSelected);
        Assert.True(Shown(app, "DetailsPlaceholder") is not null, "The right panel's placeholder does not show.");
        Assert.Equal([PartK.Ops], TabTitles(app));
        Assert.Equal([PartK.MainBranchLine, PartK.CoreUnstaged], await StatusAsync(repo));

        // Step 1: Ctrl+T: a new tab with its welcome page.
        PressMod(app, Key.T);
        Assert.Equal([PartK.Ops, PartK.NewTabTitle], TabTitles(app));
        Assert.Equal(PartK.NewTabTitle, ActiveTitle(app));
        Assert.Contains(PartK.NoRepositoryOpen, TextsIn(app.ActiveWelcomeView));
        Assert.All(app.TabStrip().Tabs, tab => Assert.True(tab.ShowsCloseButton, $"The tab {tab.Title} shows no close button."));

        // Step 2: Ctrl+O with "graph" queued: the active empty tab opens it.
        app.Folders.Answer(graph.Path);
        PressMod(app, Key.O);
        await app.WaitForTabOpenedAsync(graph.Path, OperationChainTimeout);
        Assert.Equal(PartK.OpenRepositoryDialog, app.Folders.Titles[^1]);
        Assert.Equal([PartK.Ops, PartK.Graph], TabTitles(app));
        Assert.Equal(PartK.Graph, ActiveTitle(app));
        Assert.Equal(PartK.Gr0Subjects, Enumerable.Range(0, Graph(app).Data!.Count).Select(row => Graph(app).Data!.CommitAt(row).Subject));
        AssertBranch(app, "main");
        Assert.Equal(PartK.Graph, app.Session.Services.Recent.Items[0].Name);

        // Step 3: the tab keys round the strip, session.json following.
        foreach (var (press, active, saved) in new (Action Press, string Active, int Saved)[]
        {
            (() => PressControl(app, Key.Tab), PartK.Ops, 0),
            (() => PressControl(app, Key.Tab, shift: true), PartK.Graph, 1),
            (() => PressMod(app, Key.D1), PartK.Ops, 0),
            (() => PressMod(app, Key.D9), PartK.Graph, 1),
            (() => PressControl(app, Key.PageUp), PartK.Ops, 0),
        })
        {
            press();
            Assert.Equal(active, ActiveTitle(app));
            await app.WaitForAsync(() => SavedActiveTab(data) == saved, $"session.json's active tab to be {saved}");
            Assert.Equal([PartK.Ops, PartK.Graph], TabTitles(app));
        }

        await app.WaitForIdleAsync();

        // Step 4: Ctrl+Shift+X: Unstage all is listed and disabled: consumed, nothing runs.
        var state = app.Session.Shell.Commands.Describe(AppCommand.UnstageAll);
        Assert.Equal((true, false, PartK.NothingStaged), (state.IsListed, state.IsEnabled, state.DisabledReason));
        var sequence = app.CallMark();
        var operations = app.Session.Operations.Snapshot().Count;
        Assert.True(app.PressKeyHandled(Key.X, Mod(app) | RawInputModifiers.Shift), "Ctrl+Shift+X was not consumed.");
        await app.WaitForIdleAsync();
        AssertNoWrite(app, sequence, operations, "Ctrl+Shift+X");
        Assert.Equal([PartK.MainBranchLine, PartK.CoreUnstaged], await StatusAsync(repo));
        Assert.Empty(Selected(app));
        Assert.False(Repository(app).IsWorkingRowSelected);

        // Step 5: Ctrl+Shift+E: the filter.
        PressMod(app, Key.E, shift: true);
        AssertFocus(app, "FilterBox", KeyRegionRole.Left);
        var filter = (TextBox)app.FindByAutomationId("FilterBox");
        Assert.True(string.IsNullOrEmpty(filter.Text));

        // Step 6: F6: the graph's rows with the keyboard frame (k12a); Home selects the working-changes row.
        app.PressKey(Key.F6);
        Shot(app, "k12a-f6-graph", keepPointer: true);
        AssertFocus(app, "GraphRows", KeyRegionRole.GraphArea);
        Assert.True(FocusVisible(app), "The graph's rows show no keyboard focus frame.");
        AssertGraph(app, PartK.Ops0, PartK.Ops0Lanes, PartK.OpsWorking);
        Assert.Empty(Selected(app));
        Assert.False(filter.IsFocused);
        Assert.NotEqual(PaletteOf(app).Accent, (filter.BorderBrush as ISolidColorBrush)?.Color);
        Assert.True(Shown(app, "DetailsPlaceholder") is not null, "The right panel's placeholder does not show.");
        app.PressKey(Key.Home);
        await app.WaitForAsync(() => Repository(app).IsWorkingRowSelected, "the working-changes row to be selected");
        await AssertStagePanelAsync(app, unstaged: [("M", PartK.CoreName, PartK.CoreFolder)], staged: [], PartK.StageFilesToCommit, enabled: false);
        var noStaged = app.Find<TextBlock>("NoStaged");
        Assert.True(noStaged.IsEffectivelyVisible && noStaged.Text == PartK.NoStagedChanges, $"Staged files (0) does not say No staged changes: \"{noStaged.Text}\", {(noStaged.IsEffectivelyVisible ? "shown" : "hidden")}.");
        Assert.Equal(PartK.Counter(string.Empty), TextOf(app, "SummaryCounter"));
        Assert.True(string.IsNullOrEmpty(((TextBox)app.FindByAutomationId("CommitSummary")).Text));

        // Step 7: F6: the stage panel's first unstaged row, not StageFlatButton (k12b).
        app.PressKey(Key.F6);
        Shot(app, "k12b-f6-stage-row", keepPointer: true);
        Assert.Equal(KeyRegionRole.Right, app.FocusedRegion());
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("UnstagedFileList")), $"The focus is on {app.FocusedAutomationId()}, not in UnstagedFileList.");
        Assert.Contains(PartK.CoreName, TextsIn((Visual)app.FocusedElement!));
        Assert.True(FocusVisible(app), "The unstaged row shows no keyboard focus frame.");
        Assert.True(Repository(app).IsWorkingRowSelected);

        // Step 8: Ctrl+Shift+A: Stage all.
        sequence = app.CallMark();
        var mark = app.LogMark();
        PressMod(app, Key.A, shift: true);
        await WaitForStatusReadAsync(app, 1, 0, mark);
        await app.WaitForIdleAsync();
        var writes = app.CallsSince(sequence).Where(IsWriteCommand).ToList();
        Assert.True(writes.Count == 1 && writes[0].CommandText == PartK.AddCore && writes[0].Succeeded, "Stage all wrote:\n" + string.Join('\n', writes.Select(call => call.CommandText)));
        Assert.Equal([PartK.MainBranchLine, PartK.CoreStaged], await StatusAsync(repo));
        await AssertStagePanelAsync(app, unstaged: [], staged: [("M", PartK.CoreName, PartK.CoreFolder)], PartK.CommitOneFile, enabled: false);

        // Step 9: Ctrl+Shift+M: the commit message; typing the summary.
        PressMod(app, Key.M, shift: true);
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CommitSummary", "the focus in CommitSummary");
        AssertFocus(app, "CommitSummary", KeyRegionRole.Right);
        Assert.True(Repository(app).IsWorkingRowSelected);
        app.Type(PartK.KeysOnly);
        Assert.Equal(PartK.Counter(PartK.KeysOnly), TextOf(app, "SummaryCounter"));
        Assert.True(((Button)app.FindByAutomationId("CommitButton")).IsEffectivelyEnabled, "The commit button is disabled with a summary.");

        // Step 10: Ctrl+Enter commits.
        sequence = app.CallMark();
        operations = app.Session.Operations.Snapshot().Count;
        mark = app.LogMark();
        PressMod(app, Key.Enter);
        await WaitForOperationAsync(app, repo, PartK.CommitTitle, mark);
        await WaitForStatusReadAsync(app, 0, 0, mark);
        await AssertCommittedAsync(app, repo, sequence);
        Assert.Equal(["Keys only"], await GitLinesAsync(repo, "log", "-1", "--format=%B"));
        Assert.Single(OperationsSince(app, operations), record => record.Title == PartK.CommitTitle);

        // Step 11: Ctrl+Z in the filter undoes the typing only (D163).
        PressMod(app, Key.E, shift: true);
        AssertFocus(app, "FilterBox", KeyRegionRole.Left);
        app.Type(PartK.FilterText);
        Assert.Equal(PartK.FilterText, filter.Text);
        await app.WaitForAsync(() => !LocalBranchNames(app).Contains("main"), "the left panel to be filtered");
        sequence = app.CallMark();
        operations = app.Session.Operations.Snapshot().Count;
        PressMod(app, Key.Z);
        Assert.True(string.IsNullOrEmpty(filter.Text), $"Ctrl+Z left \"{filter.Text}\" in the filter.");
        await app.WaitForAsync(() => LocalBranchNames(app).Contains("main"), "the left panel to be whole again");
        await app.WaitForIdleAsync();
        Assert.DoesNotContain(OperationsSince(app, operations), record => record.Title == PartK.UndoCommitKeysOnly);
        Assert.DoesNotContain(app.CallsSince(sequence), call => call.CommandText.StartsWith("git update-ref", StringComparison.Ordinal));
        Assert.Equal(PartK.KeysOnlySha, await RevParseAsync(repo, "refs/heads/main"));
        AssertUndoRedo(app, PartK.CommitKeysOnly, null);

        // Step 12: Ctrl+J shows the log; Shift+F6 goes back round to it (k12c).
        PressMod(app, Key.J);
        app.PressKey(Key.F6, RawInputModifiers.Shift);
        var k12c = Shot(app, "k12c-shift-f6-log", keepPointer: true);
        AssertLogShown(app, k12c);
        AssertKeyed(app, PartK.ShowLog with { ToolTip = PartK.HideActivityLog });
        AssertGraph(app, PartK.OpsK, PartK.KeysOnlyLanes);
        Assert.Equal([PartK.KeysOnlySha], Selected(app));

        // Step 13: Esc closes the log; F6 to the commit's file; Enter, Ctrl+Shift+H, Esc, F6, Enter, Ctrl+Shift+B, Esc (k12d).
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.Session.Shell.ActivityLog.IsShown, "the activity log to close");
        AssertFocus(app, "GraphRows", KeyRegionRole.GraphArea);
        AssertKeyed(app, PartK.ShowLog);
        app.PressKey(Key.F6);
        Shot(app, "k12d-f6-details-row", keepPointer: true);
        Assert.Equal(KeyRegionRole.Right, app.FocusedRegion());
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("FileList")), $"The focus is on {app.FocusedAutomationId()}, not in FileList.");
        Assert.Contains(PartK.CoreName, TextsIn((Visual)app.FocusedElement!));
        Assert.True(FocusVisible(app), "The commit's file row shows no keyboard focus frame.");
        sequence = app.CallMark();
        operations = app.Session.Operations.Snapshot().Count;
        app.PressKey(Key.Enter);
        await AssertCoreDiffOpenAsync(app);
        PressMod(app, Key.H, shift: true);
        await AssertCoreHistoryAsync(app);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => Repository(app).Page is null, "the file history page to close");
        AssertFocus(app, "GraphRows", KeyRegionRole.GraphArea);
        Assert.Equal([PartK.KeysOnlySha], Selected(app));
        app.PressKey(Key.F6);
        Assert.Equal(KeyRegionRole.Right, app.FocusedRegion());
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("FileList")), $"The focus is on {app.FocusedAutomationId()}, not back on FileList's row.");
        app.PressKey(Key.Enter);
        await AssertCoreDiffOpenAsync(app);
        PressMod(app, Key.B, shift: true);
        await AssertCoreBlameAsync(app);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => Repository(app).Page is null, "the blame page to close");
        AssertFocus(app, "GraphRows", KeyRegionRole.GraphArea);
        await app.WaitForIdleAsync();
        AssertNoWrite(app, sequence, operations, "The diff and its pages");
        Assert.Equal(PartK.KeysOnlySha, await RevParseAsync(repo, "refs/heads/main"));

        // Step 14: Ctrl+Z undoes the commit.
        operations = app.Session.Operations.Snapshot().Count;
        mark = app.LogMark();
        PressMod(app, Key.Z);
        await WaitForOperationAsync(app, repo, PartK.UndoCommitKeysOnly, mark);
        var undo = AssertOperation(app, operations, PartK.UndoCommitKeysOnly, PartK.UndoCommitUpdateRef);
        Assert.Equal(PartK.UndoInput, undo.Call.StandardInput?.TrimEnd('\n'));
        AssertStatus(app, PartK.UndidCommitKeysOnly);
        Assert.Equal(PartK.MainWorkSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal([PartK.MainBranchLine, PartK.CoreStaged], await StatusAsync(repo));
        Assert.Equal(0, (await repo.GitAllowingFailureAsync("cat-file", "-e", PartK.KeysOnlySha)).ExitCode);
        AssertGraph(app, PartK.Ops0, PartK.Ops0Lanes, PartK.OpsWorking);
        await app.WaitForAsync(() => Repository(app).Changes.Summary == PartK.KeysOnly, "the summary Keys only back in its box");
        AssertUndoRedo(app, null, PartK.CommitKeysOnly);

        // Step 15: Ctrl+Y redoes it.
        operations = app.Session.Operations.Snapshot().Count;
        mark = app.LogMark();
        PressMod(app, Key.Y);
        await WaitForOperationAsync(app, repo, PartK.RedoCommitKeysOnly, mark);
        var redo = AssertOperation(app, operations, PartK.RedoCommitKeysOnly, PartK.RedoCommitUpdateRef);
        Assert.Equal(PartK.RedoInput, redo.Call.StandardInput?.TrimEnd('\n'));
        AssertStatus(app, PartK.RedidCommitKeysOnly);
        Assert.Equal(PartK.KeysOnlySha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal([PartK.AheadOneBranchLine], await StatusAsync(repo));
        Assert.Equal(PartK.RedoReflog, (await GitLinesAsync(repo, "log", "-g", "--format=%gs", "HEAD")).Take(3));
        AssertGraph(app, PartK.OpsK, PartK.KeysOnlyLanes);
        AssertDashedLines(app, PartK.OpsKDashes);
        Assert.True(string.IsNullOrEmpty(Repository(app).Changes.Summary), $"The summary still holds \"{Repository(app).Changes.Summary}\".");
        AssertUndoRedo(app, PartK.CommitKeysOnly, null);
    }

    /// <summary>
    /// Check K12, part B (R7, D106, D163, D192, D214; design 6.1, 6.5, 6.9, 7.2, 5.10): Ctrl+Shift+M
    /// consumed with nothing to commit; Tab round the left panel to <c>main</c>; Shift+F10, Down and
    /// Enter in its menu (k12e); Fetch, Pull and Push by their keys; Create branch, Pop and Stash; the
    /// search with F3 and Shift+F3; the reflog; Ctrl+Shift+D without a dashboard; Ctrl+, with the
    /// window's keys declining; Init cancelled; Clone and Ctrl+W; F5.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K12_menus_remotes_stash_and_dialogs_by_keys()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var refs = await GitLinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)");

        // Step 16: Ctrl+Shift+M with nothing to commit: listed, disabled, consumed.
        AssertGraph(app, PartK.Ops0, PartK.Ops0Lanes);
        var state = app.Session.Shell.Commands.Describe(AppCommand.GoToCommitMessage);
        Assert.Equal((true, false, PartK.NoChangesToCommit), (state.IsListed, state.IsEnabled, state.DisabledReason));
        var sequence = app.CallMark();
        var operations = app.Session.Operations.Snapshot().Count;
        Assert.True(app.PressKeyHandled(Key.M, Mod(app) | RawInputModifiers.Shift), "Ctrl+Shift+M was not consumed.");
        await app.WaitForIdleAsync();
        Assert.Empty(Selected(app));
        Assert.False(Repository(app).IsWorkingRowSelected);
        Assert.Null(Shown(app, "StagePanel"));
        AssertNoWrite(app, sequence, operations, "Ctrl+Shift+M");

        // Step 17: Ctrl+Shift+E, then Tab through the left panel's rows to main (KD8).
        PressMod(app, Key.E, shift: true);
        AssertFocus(app, "FilterBox", KeyRegionRole.Left);
        var stops = new List<(string? Id, string? Name, KeyRegionRole Region)>();
        for (var press = 0; press < 6 && !IsRefItem(app, "main"); press++)
        {
            app.PressKey(Key.Tab);
            stops.Add((app.FocusedAutomationId(), FocusedName(app), app.FocusedRegion()));
        }

        Assert.True(IsRefItem(app, "main"), "Six Tabs did not reach the RefItem main. The stops: " + string.Join(", ", stops));
        Assert.All(stops, stop => Assert.Equal(KeyRegionRole.Left, stop.Region));
        var local = stops.FindIndex(stop => stop.Id == "Section" && stop.Name == "Local branches");
        var feature = stops.FindIndex(stop => stop.Id == "RefItem" && stop.Name == "feature");
        Assert.True(local >= 0 && feature > local && feature < stops.Count - 1, "The stops before main are not Local branches, then feature: " + string.Join(", ", stops));

        // Step 18: Shift+F10 opens main's menu by keyboard; Esc; again, Down twice (k12e); Enter pushes.
        var mainRow = (Control)app.FocusedElement!;
        var mark = app.LogMark();
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        var menu = app.Menu();
        AssertKeyedMenu(app, menu, PartK.MainMenu, PartK.MainMenuWidth);
        AssertNear(PartK.FourteenItemsFiveSeparators, menu.Bounds.Height, 2, "main's menu's height");
        AssertNear(WindowLayout.MenuEdgeInset, menu.Bounds.X, PartK.Tolerance, "main's menu's left");
        AssertNear(app.BoundsOf(mainRow).Bottom, menu.Bounds.Y, PartK.Tolerance, "main's menu's top");
        AssertHighlighted(menu, PartK.PullFromOriginMain);
        Assert.Equal([PartK.MenuOpened(PartK.Ops, "main", PartK.ByKeyboard)], Messages(app, mark, "Context menu opened"));
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        Assert.Same(mainRow, app.FocusedElement);
        mark = app.LogMark();
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        app.Menu();
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        menu = app.Menu();
        Shot(app, "k12e-branch-menu-by-keys", keepPointer: true);
        AssertHighlighted(menu, PartK.PushMainToOriginMain);
        Assert.Single(menu.Entries, entry => entry.IsHighlighted);
        Assert.Equal([PartK.MenuOpened(PartK.Ops, "main", PartK.ByKeyboard)], Messages(app, mark, "Context menu opened"));
        operations = app.Session.Operations.Snapshot().Count;
        mark = app.LogMark();
        app.PressKey(Key.Enter);
        Assert.False(app.IsMenuOpen, "Enter left main's menu open.");
        await WaitForOperationAsync(app, repo, PartK.PushMain, mark, NetworkTimeout);
        AssertPush(app, operations);
        Assert.Equal(PartK.MainWorkSha, (await GitLinesAsync(repo, "--git-dir", repo.SiblingPath("origin.git"), "rev-parse", "refs/heads/main")).Single());

        // Step 19: Ctrl+Shift+F, Ctrl+Shift+L, Ctrl+Shift+K.
        operations = app.Session.Operations.Snapshot().Count;
        mark = app.LogMark();
        PressMod(app, Key.F, shift: true);
        await WaitForOperationAsync(app, repo, PartK.Fetch, mark, NetworkTimeout);
        var fetch = AssertOperation(app, operations, PartK.Fetch, PartK.FetchOrigin);
        if (await IsReportGitAsync())
        {
            Assert.Equal((string.Empty, string.Empty), (fetch.Call.StandardOutput, fetch.Call.StandardError));
        }

        AssertStatus(app, PartK.FetchedFromAllRemotes);
        operations = app.Session.Operations.Snapshot().Count;
        mark = app.LogMark();
        using (var texts = app.RecordStatusTexts())
        {
            PressMod(app, Key.L, shift: true);
            await WaitForOperationAsync(app, repo, PartK.Pull, mark, NetworkTimeout);
            Assert.Contains(PartK.PullingIntoMain, texts.Texts);
        }

        var pull = AssertOperation(app, operations, PartK.Pull, PartK.MergeFetchHead);
        var pullCalls = pull.Calls.Select(call => call.CommandText).ToList();
        Assert.True(pullCalls.IndexOf(PartK.FetchOrigin) is >= 0 and var at && pullCalls.IndexOf(PartK.MergeFetchHead) > at, "The pull did not fetch, then merge:\n" + string.Join('\n', pullCalls));
        Assert.Contains(PartK.AlreadyUpToDate, Lines(pull.Call.StandardOutput));
        AssertStatus(app, PartK.MainIsUpToDate);
        operations = app.Session.Operations.Snapshot().Count;
        mark = app.LogMark();
        PressMod(app, Key.K, shift: true);
        await WaitForOperationAsync(app, repo, PartK.PushMain, mark, NetworkTimeout);
        AssertPush(app, operations);
        Assert.Equal(refs, await GitLinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)"));
        Assert.Equal([PartK.MainBranchLine], await StatusAsync(repo));

        // Step 20: Ctrl+B: the name dialog from HEAD's commit; Esc.
        PressMod(app, Key.B);
        var dialog = await app.WaitForDialogAsync<NameDialogViewModel>();
        await AssertCreateBranchDialogAsync(app, dialog, PartK.FromMainWork, picture: null);
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.Equal(PartK.Ops0LocalBranches, await GitLinesAsync(repo, "for-each-ref", "--format=%(refname)", "refs/heads"));

        // Step 21: Ctrl+Shift+O pops the newest stash.
        operations = app.Session.Operations.Snapshot().Count;
        mark = app.LogMark();
        using (var texts = app.RecordStatusTexts())
        {
            PressMod(app, Key.O, shift: true);
            await WaitForOperationAsync(app, repo, PartK.PopStash, mark);
            Assert.Contains(PartK.PoppingStash, texts.Texts);
        }

        var popRecord = Assert.Single(OperationsSince(app, operations), record => record.Title == PartK.PopStash);
        Assert.Equal(OperationOutcome.Succeeded, popRecord.Outcome);
        var pop = app.GitCalls().Last(call => call.OperationId == popRecord.Id && call.CommandText.StartsWith(PartK.StashPopPrefix, StringComparison.Ordinal));
        Assert.True(pop.Succeeded, $"\"{pop.CommandText}\" exited {pop.ExitCode}.");
        Assert.Equal(PartK.DroppedStash, Lines(pop.StandardOutput)[^1]);
        AssertStatus(app, PartK.PoppedStash);
        Assert.Equal([PartK.MainBranchLine, PartK.ReadmeUnstaged], await StatusAsync(repo));
        Assert.Equal(PartK.ReadmeExperiment, File.ReadAllText(Path.Combine(repo.Path, "README.md")).Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Empty(await StashListAsync(repo));
        AssertGraph(app, PartK.OpsP, PartK.Ops0Lanes, PartK.OpsWorking with { Lane = 1, Colour = 1 });
        AssertUndoRedo(app, PartK.PopOfStash, null);

        // Step 22: Ctrl+Shift+S: the stash dialog; a message; Enter stashes.
        PressMod(app, Key.S, shift: true);
        var stashDialog = await app.WaitForDialogAsync<StashDialogViewModel>();
        await AssertStashDialogAsync(app, stashDialog);
        app.Type(PartK.StashAgainTyped);
        operations = app.Session.Operations.Snapshot().Count;
        mark = app.LogMark();
        using (var texts = app.RecordStatusTexts())
        {
            app.PressKey(Key.Enter);
            await WaitForOperationAsync(app, repo, PartK.Stash, mark);
            Assert.Contains(PartK.StashingTheChanges, texts.Texts);
        }

        var stash = AssertOperation(app, operations, PartK.Stash, PartK.StashPushCommand);
        Assert.Contains(PartK.SavedStash, Lines(stash.Call.StandardOutput));
        AssertStatus(app, PartK.StashedTheChanges);
        Assert.Equal(PartK.StashAgainSha, await RevParseAsync(repo, "refs/stash"));
        Assert.Equal([$"stash@{{0}}: {PartK.StashAgainMessage}"], await StashListAsync(repo));
        Assert.Equal([PartK.MainBranchLine], await StatusAsync(repo));
        AssertGraph(app, PartK.OpsS, PartK.Ops0Lanes);
        AssertUndoRedo(app, PartK.StashAgainEntry, null);

        // Step 23: Ctrl+F; alpha; F3, F3, Shift+F3 between the matches; Esc.
        PressMod(app, Key.F);
        await app.WaitForAsync(() => app.SearchBar() is { BoxHasFocus: true }, "the search bar with the focus in its box");
        Assert.Equal("Commits", app.SearchBar()!.Mode);
        sequence = app.CallMark();
        operations = app.Session.Operations.Snapshot().Count;
        app.Type(PartK.SearchQuery);
        await app.WaitForAsync(() => app.SearchBar()?.Count == "2 matches", "SearchCount to read 2 matches");
        Assert.Equal(PartK.AlphaMatches, app.SearchMarks().Marked);
        Assert.Equal(Enumerable.Range(0, PartK.OpsS.Length).Except(PartK.AlphaMatches), app.SearchMarks().Dimmed);
        Assert.Empty(Selected(app));
        foreach (var (key, shift, sha, subject, count) in new[]
        {
            (Key.F3, false, PartK.AlphaTwoSha, "Alpha two", "1 of 2"),
            (Key.F3, false, "dff8fbf", "Alpha one", "2 of 2"),
            (Key.F3, true, PartK.AlphaTwoSha, "Alpha two", "1 of 2"),
        })
        {
            app.PressKey(key, shift ? RawInputModifiers.Shift : RawInputModifiers.None);
            await app.WaitForAsync(() => app.SearchBar()?.Count == count, $"SearchCount to read {count}");
            Assert.StartsWith(sha, Assert.Single(Selected(app)), StringComparison.Ordinal);
            Assert.True(app.SearchBar()!.BoxHasFocus, "The search box lost the focus.");
        }

        await WaitForDetailsAsync(app, PartK.AlphaTwoSha, "Alpha two");
        await app.WaitForIdleAsync();
        AssertNoWrite(app, sequence, operations, "The search");
        app.PressKey(Key.Escape);
        Assert.Null(app.SearchBar());
        Assert.False(app.SearchMarks().IsActive, "The search's marks stay.");
        Assert.Equal([PartK.AlphaTwoSha], Selected(app));
        AssertFocus(app, "GraphRows", KeyRegionRole.GraphArea);

        // Step 24: Ctrl+Shift+R: HEAD's reflog page; Esc.
        sequence = app.CallMark();
        PressMod(app, Key.R, shift: true);
        await app.WaitForPageAsync<VisualCommit.App.Views.Pages.ReflogView>();
        await app.WaitForAsync(() => app.ReflogSnapshot() is { IsLoading: false, Rows.Count: > 0 }, "the reflog page to show its rows");
        Assert.True(app.ReflogSnapshot().ListHasFocus, "The reflog's list does not have the focus.");
        await app.WaitForIdleAsync();
        AssertNoWrite(app, sequence, operations, "The reflog page");
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => Repository(app).Page is null, "the reflog page to close");
        Assert.True(Repository(app).ShowsGraph, "The graph is not back.");

        // Step 25: Ctrl+Shift+D without a dashboard: the palette at "workspace" (KD16); Esc.
        mark = app.LogMark();
        PressMod(app, Key.D, shift: true);
        Assert.True(app.IsPaletteOpen, "Ctrl+Shift+D did not open the palette.");
        Assert.Equal(PartK.WorkspaceQuery, app.PaletteBox.Text);
        Assert.Equal(PartK.WorkspaceQuery.Length, app.PaletteBox.CaretIndex);
        Assert.Equal(app.PaletteBox.SelectionStart, app.PaletteBox.SelectionEnd);
        AssertShown(app, [PartK.NewWorkspace], 1);
        Assert.Equal(0, app.Palette.HighlightedIndex);
        AssertPaletteLines(app, mark, PartK.PaletteOpened(PartK.Ops));
        app.PressKey(Key.Escape);
        Assert.False(app.IsPaletteOpen);
        Assert.Equal(0, RanLines(app, mark));

        // Step 26: Ctrl+, opens the settings screen on General; Ctrl+T declines; Esc.
        AssertFocus(app, "GraphRows", KeyRegionRole.GraphArea);
        PressMod(app, Key.OemComma);
        await app.WaitForDialogAsync<SettingsDialogViewModel>();
        Assert.Equal(VisualCommit.App.ViewModels.SettingsSection.General, app.SettingsSnapshot().Section);
        await app.WaitForAsync(() => app.FocusedAutomationId() == "SettingsNavGeneral", "the focus on SettingsNavGeneral");
        Assert.True(app.FindByAutomationId("SettingsNavGeneral").Classes.Contains("selected"), "General is not the selected section.");
        PressMod(app, Key.T);
        Assert.Equal([PartK.Ops], TabTitles(app));
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        AssertFocus(app, "GraphRows", KeyRegionRole.GraphArea);

        // Step 27: Ctrl+Shift+I with the folder dialog cancelled.
        mark = app.LogMark();
        var folders = app.Folders.Titles.Count;
        PressMod(app, Key.I, shift: true);
        await app.WaitForAsync(() => app.Folders.Titles.Count == folders + 1, "the folder dialog of Init");
        app.Settle();
        Assert.Equal(PartK.InitRepositoryDialog, app.Folders.Titles[^1]);
        Assert.Equal([PartK.Ops], TabTitles(app));
        Assert.Empty(WarningsAndErrors(app, mark));

        // Step 28: Ctrl+Shift+C: the clone form in a new tab at the end; Ctrl+W in its URL box closes it.
        PressMod(app, Key.C, shift: true);
        await app.WaitForAsync(() => app.Session.Shell.Tabs.Count == 2, "a new tab for the clone form");
        Assert.Equal([PartK.Ops, PartK.NewTabTitle], TabTitles(app));
        Assert.Equal(PartK.NewTabTitle, ActiveTitle(app));
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CloneUrlBox", "the focus in CloneUrlBox");
        PressMod(app, Key.W);
        await app.WaitForAsync(() => app.Session.Shell.Tabs.Count == 1, "the clone's tab to close");
        Assert.Equal([PartK.Ops], TabTitles(app));
        Assert.Equal(PartK.Ops, ActiveTitle(app));

        // Step 29: F5 reads.
        sequence = app.CallMark();
        operations = app.Session.Operations.Snapshot().Count;
        app.PressKey(Key.F5);
        await app.WaitForAsync(() => app.CallsSince(sequence).Any(call => call.CommandText.StartsWith("git status", StringComparison.Ordinal)), "F5's status read");
        await app.WaitForIdleAsync();
        var reads = app.CallsSince(sequence);
        Assert.Contains(reads, call => call.CommandText.StartsWith("git for-each-ref --format=", StringComparison.Ordinal));
        AssertNoWrite(app, sequence, operations, "F5");
        AssertGraph(app, PartK.OpsS, PartK.Ops0Lanes);
        AssertStatus(app, PartK.StashedTheChanges);
        AssertBranch(app, "main");
    }

    // ---- K11's and K12's own helpers. ----

    /// <summary>The close button of the tab <paramref name="title"/> (<c>CloseTabButton</c> in its <c>RepoTab</c>).</summary>
    private static Button CloseButtonOf(ShellDriver app, string title) => TabPart<Button>(app, title, "CloseTabButton");

    /// <summary>The title of the tab <paramref name="title"/> (<c>TabTitle</c>), what a click on the tab aims at.</summary>
    private static TextBlock TabTitleOf(ShellDriver app, string title) => TabPart<TextBlock>(app, title, "TabTitle");

    private static T TabPart<T>(ShellDriver app, string title, string name)
        where T : Control
    {
        app.Settle();
        var border = app.Window.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(candidate => candidate.Name == "RepoTab" && candidate.DataContext is RepoTabViewModel tab && tab.Title == title)
            ?? throw new Xunit.Sdk.XunitException($"No tab {title} is in the strip.");
        return border.GetVisualDescendants().OfType<T>().First(control => control.Name == name);
    }

    /// <summary>The focused element's (or its nearest named ancestor's) automation name.</summary>
    private static string? FocusedName(ShellDriver app) =>
        (app.FocusedElement as Visual)?.GetSelfAndVisualAncestors().OfType<Control>().Select(AutomationProperties.GetName).FirstOrDefault(name => !string.IsNullOrEmpty(name));

    /// <summary>Whether the focus is on the left panel's <c>RefItem</c> named <paramref name="name"/>.</summary>
    private static bool IsRefItem(ShellDriver app, string name) => app.FocusedAutomationId() == "RefItem" && FocusedName(app) == name;

    /// <summary>The left panel's Local branches' rows' texts shown now (with the filter applied).</summary>
    private static List<string> LocalBranchNames(ShellDriver app)
    {
        app.Settle();
        var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
        var start = rows.FindIndex(row => row is RefSectionRow section && section.Text == "Local branches");
        return start < 0 ? [] : [.. rows.Skip(start + 1).TakeWhile(row => row is RefItemRow).Select(row => row.Text)];
    }

    /// <summary>
    /// The stage panel (phases 2 and 4): <c>Unstaged files (n)</c> and <c>Staged files (n)</c> with
    /// their rows' letter, name and folder in order, and the commit button's text and state.
    /// </summary>
    private static async Task AssertStagePanelAsync(ShellDriver app, (string Letter, string Name, string Folder)[] unstaged, (string Letter, string Name, string Folder)[] staged, string button, bool enabled)
    {
        await app.WaitForAsync(() => Shown(app, "StagePanel") is not null, "the stage panel to show");
        var (unstagedTitle, stagedTitle, commit) = StagePanel(app);
        Assert.Equal((PartK.UnstagedTitle(unstaged.Length), PartK.StagedTitle(staged.Length)), (unstagedTitle, stagedTitle));
        Assert.Equal(unstaged, FileRows(app, "UnstagedFileList"));
        Assert.Equal(staged, FileRows(app, "StagedFileList"));
        Assert.Equal(button, ButtonText(commit));
        Assert.Equal(enabled, commit.IsEffectivelyEnabled);
    }

    /// <summary>A stage panel list's rows (<c>UnstagedFileList</c> or <c>StagedFileList</c>): each file's status letter, name and folder, as its view model gives them to the row.</summary>
    private static (string Letter, string Name, string Folder)[] FileRows(ShellDriver app, string list)
    {
        var changes = Repository(app).Changes;
        var rows = list == "UnstagedFileList" ? changes.Unstaged.Rows : changes.Staged.Rows;
        return [.. rows.Where(row => row.IsFile).Select(row => (row.StatusLetter, row.Name, row.Folder))];
    }

    /// <summary>
    /// The commit of K12 step 10 (K-OPSk): one <c>git commit --file=- --cleanup=whitespace</c> since
    /// <paramref name="sequence"/>, exit 0, its key line; <c>main</c> at <c>9522dae</c>, ahead by one,
    /// clean; HEAD's reflog's newest <c>commit: Keys only</c>; the graph K-OPSk with "Keys only"
    /// selected and its details; Undo <c>Undo commit "Keys only" (Ctrl+Z)</c>.
    /// </summary>
    private static async Task AssertCommittedAsync(ShellDriver app, TempRepo repo, long sequence)
    {
        var commits = app.CallsSince(sequence).Where(call => call.CommandText == PartK.CommitCommand).ToList();
        var commit = Assert.Single(commits);
        Assert.True(commit.Succeeded, $"The commit exited {commit.ExitCode}:\n{commit.StandardError}");
        Assert.Contains(PartK.CommitKeyLine, Lines(commit.StandardOutput));
        if (await IsReportGitAsync())
        {
            Assert.Equal([PartK.CommitKeyLine, PartK.CommitSummaryLine.Trim()], Lines(commit.StandardOutput).Select(line => line.Trim()));
        }

        Assert.Equal(PartK.KeysOnlySha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal([PartK.AheadOneBranchLine], await StatusAsync(repo));
        Assert.Equal("commit: Keys only", (await GitLinesAsync(repo, "log", "-g", "-1", "--format=%gs", "HEAD")).Single());
        await app.WaitForAsync(() => Selected(app).SequenceEqual([PartK.KeysOnlySha]), "Keys only to be selected");
        AssertGraph(app, PartK.OpsK, PartK.KeysOnlyLanes);
        AssertDashedLines(app, PartK.OpsKDashes);
        await WaitForDetailsAsync(app, PartK.KeysOnlySha, PartK.KeysOnly);
        AssertUndoRedo(app, PartK.CommitKeysOnly, null);
    }

    /// <summary>
    /// The activity log as Ctrl+J shows it and Shift+F6 enters it (K12 step 12): at its default
    /// height under the graph, its header, exactly the two operations Stage and Commit closed at the
    /// frozen clock's time, the focus on the first row drawn on <c>VcSelectionBrush</c>.
    /// </summary>
    private static void AssertLogShown(ShellDriver app, Screenshot picture)
    {
        var palette = PaletteOf(app);
        var log = app.ActivityLog();
        AssertNear(PartK.LogTop, log.Bounds.Top, PartK.Tolerance, "The activity log's top");
        AssertNear(PartK.LogBottom, log.Bounds.Bottom, PartK.Tolerance, "The activity log's bottom");
        AssertNear(PartK.LogHeaderBottom, log.HeaderBounds.Bottom, PartK.Tolerance, "The activity log's header's bottom");
        AssertNear(PartK.GraphContentBottom, app.BoundsOf(Graph(app)).Bottom, PartK.Tolerance, "The graph area's content's bottom");
        Assert.Equal(("Activity log", PartK.Ops, false), (log.Title, log.RepositoryName, log.ShowReads));
        Assert.Equal(palette.TextSecondary, log.RepositoryNameColor);
        Assert.True(log.CloseBounds.Width > 0, "The activity log shows no close button.");
        Assert.True(log.Rows.Count == 2, "The activity log holds other rows than Stage and Commit:\n" + string.Join('\n', log.Rows.Select(row => row.AutomationName)));
        string[] titles = [PartK.Stage, PartK.CommitTitle];
        for (var i = 0; i < 2; i++)
        {
            var row = log.Rows[i];
            Assert.Equal(titles[i], row.Title);
            Assert.Equal(("IconChevronRight", false, "IconCheck", "09:00:00", "0 ms"), (row.Chevron, row.IsOpen, row.Glyph, row.Time, row.Duration));
            Assert.Equal(palette.Success, row.GlyphColor);
            Assert.True(string.IsNullOrEmpty(row.Message), $"The row {titles[i]} shows the message \"{row.Message}\".");
            var bounds = row.Bounds ?? throw new Xunit.Sdk.XunitException($"The row {titles[i]} is not drawn.");
            AssertNear(PartK.LogHeaderBottom + (PartK.LogRowHeight * i), bounds.Top, PartK.Tolerance, $"The row {titles[i]}'s top");
            AssertNear(PartK.LogRowHeight, bounds.Height, 0.5, $"The row {titles[i]}'s height");
        }

        Assert.Equal(KeyRegionRole.ActivityLog, app.FocusedRegion());
        var focused = app.BoundsOf((Visual)app.FocusedElement!);
        var stage = log.Rows[0].Bounds!.Value;
        Assert.True(focused.Intersects(stage) && !focused.Intersects(log.Rows[1].Bounds!.Value.Deflate(1)), $"The focus at {focused} is not on the row Stage at {stage}.");
        AssertColour(palette.Selection, picture, new Point(stage.Right - 4, stage.Center.Y), "focused row Stage's background (VcSelectionBrush)");
        Assert.NotEqual(palette.Selection, picture.PixelAt(new Point(log.Rows[1].Bounds!.Value.Right - 4, log.Rows[1].Bounds!.Value.Center.Y)));
    }

    /// <summary>The diff of <c>src/core.txt</c> in "Keys only" open in place of the graph (D63), its one hunk adding <c>keys</c>, its row in the commit's files selected.</summary>
    private static async Task AssertCoreDiffOpenAsync(ShellDriver app)
    {
        await app.WaitForAsync(() => Repository(app).Diff is { ShowsText: true }, "the diff of src/core.txt to show");
        var diff = Repository(app).Diff!;
        Assert.Equal((PartK.CoreFile, PartK.KeysOnlySha), (diff.Target.Path, diff.Target.Commit));
        var hunk = Assert.Single(diff.TextDiff!.Hunks);
        Assert.StartsWith(PartK.CoreDiff[0], hunk.HeaderRaw, StringComparison.Ordinal);
        Assert.Equal(PartK.CoreDiff[1..], hunk.Lines.Select(line => (line.Kind switch { VisualCommit.Core.Diff.DiffLineKind.Added => "+", VisualCommit.Core.Diff.DiffLineKind.Removed => "-", _ => " " }) + line.Text));
        Assert.True(Repository(app).IsDiffOpen);
        Assert.Contains(app.DetailsFileRows(), row => row.Name == PartK.CoreName && row.IsSelected);
    }

    /// <summary>Ctrl+Shift+H from the diff (K12 step 13): the diff closed, the file history page of <c>src/core.txt</c> from HEAD's commit, its three rows, the first selected with its change.</summary>
    private static async Task AssertCoreHistoryAsync(ShellDriver app)
    {
        await app.WaitForPageAsync<VisualCommit.App.Views.Pages.FileHistoryView>();
        await app.WaitForFileHistoryRowsAsync(PartK.CoreHistory.Length);
        await app.WaitForAsync(() => app.FileHistorySnapshot().Diff is { ShowsText: true }, "the file history's change to show");
        Assert.False(Repository(app).IsDiffOpen, "The diff is still open over the file history.");
        var history = app.FileHistorySnapshot();
        Assert.Equal((PartK.HistoryOfCore, PartK.CoreFolder, (string?)null), (history.Title, history.Folder, history.From));
        Assert.Equal(PartK.CoreHistory, history.Rows.Select(row => (row.Subject, row.ShortSha)));
        Assert.True(history.Rows[0].IsSelected, "The file history's first row is not selected.");
        Assert.Equal(PartK.KeysOnlySha, history.Diff!.Target.Commit);
        Assert.Equal([PartK.KeysOnlySha], Selected(app));
        await WaitForDetailsAsync(app, PartK.KeysOnlySha, PartK.KeysOnly);
    }

    /// <summary>Ctrl+Shift+B from the diff (K12 step 13): the diff closed, the blame page of <c>src/core.txt</c> at "Keys only", each line a run of its own commit.</summary>
    private static async Task AssertCoreBlameAsync(ShellDriver app)
    {
        await app.WaitForPageAsync<VisualCommit.App.Views.Pages.BlameView>();
        await app.WaitForAsync(() => app.BlameSnapshot() is { IsLoading: false, Runs.Count: > 0 }, "the blame page to show its lines");
        Assert.False(Repository(app).IsDiffOpen, "The diff is still open over the blame.");
        var blame = app.BlameSnapshot();
        Assert.Equal((PartK.BlameOfCore, PartK.BlameAtKeysOnly), (blame.Title, blame.Caption));
        Assert.Equal(PartK.CoreBlame.Select(line => line.Line), blame.Text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n'));
        Assert.Equal(PartK.CoreBlame.Length, blame.Runs.Count);
        for (var i = 0; i < blame.Runs.Count; i++)
        {
            Assert.Equal((i + 1, i + 1), (blame.Runs[i].FirstLine, blame.Runs[i].LastLine));
            Assert.StartsWith(PartK.CoreBlame[i].ShortSha, blame.Runs[i].Sha, StringComparison.Ordinal);
        }
    }

    /// <summary>The push of <c>main</c> (K12 steps 18 and 19): one operation <c>Push main</c>, its push exit 0 with <c>Everything up-to-date</c> (key), the status <c>Pushed main to origin/main</c>.</summary>
    private static void AssertPush(ShellDriver app, int operations)
    {
        var push = AssertOperation(app, operations, PartK.PushMain, PartK.PushMainCommand);
        Assert.Contains(PartK.EverythingUpToDate, Lines(push.Call.StandardError));
        AssertStatus(app, PartK.PushedMain);
    }

    /// <summary>The stash dialog (phase 3, K12 step 22): its title, its message box with its placeholder and the focus, <c>Include untracked files</c> ticked, Cancel and Stash.</summary>
    private static async Task AssertStashDialogAsync(ShellDriver app, StashDialogViewModel dialog)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The stash dialog has no view.");
        Assert.Equal(PartK.StashChangesTitle, dialog.Title);
        Assert.Equal(PartK.StashChangesTitle, ((TextBlock)app.FindByAutomationId(view, "NameDialogTitle")).Text);
        var box = (TextBox)app.FindByAutomationId(view, "MessageBox");
        Assert.Equal(PartK.StashMessagePlaceholder, box.PlaceholderText);
        await app.WaitForAsync(() => box.IsFocused, "the stash's message box to have the keyboard focus");
        var option = (CheckBox)app.FindByAutomationId(view, "NameDialogOption");
        Assert.Equal((PartK.IncludeUntracked, (bool?)true), (option.Content as string, option.IsChecked));
        Assert.Equal((PartK.Cancel, PartK.StashConfirm), (((Button)app.FindByAutomationId(view, "CancelButton")).Content as string, ((Button)app.FindByAutomationId(view, "ConfirmButton")).Content as string));
    }
}
