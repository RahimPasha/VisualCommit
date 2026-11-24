using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The app never takes the keyboard focus away without putting it somewhere (D462;
/// docs/phase-8a-design.md 6.3.4, 6.3.5, 6.3.7): a control pressed by the keys (Enter or Space with
/// the keyboard's frame, as Tab or the arrows give it) that its press hides, removes or disables
/// leaves the focus, once the press's run has ended and the layout has followed, on the control
/// itself when it shows again, enabled; else on the control that stands in its place or that the
/// action was about; else on the target of the region it lay in, as F6 gives it; with the frame.
/// This file holds the tab strip, the toolbar, the stage panel, the diff and the right panel; the
/// other parts of the sweep are in the files beside it. A fact that held before the sweep is a
/// guard and says so.
/// </summary>
public sealed partial class KeyboardPassTests
{
    /// <summary>How long a fact waits for the focus to come where it should, once the press's effects are in.</summary>
    private static readonly TimeSpan FocusWait = TimeSpan.FromSeconds(3);

    // ---- The tab strip ---------------------------------------------------------------------------

    /// <summary>
    /// Two tabs, the second active: its close button, pressed by the keys, closes it; the tab now
    /// at its place, the first, shows no close button any more (one tab is left), and its own
    /// button has the focus, with the frame.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public async Task Close_tab_pressed_by_the_keys_gives_the_focus_to_the_tab_now_at_its_place(Key key)
    {
        using var first = await Scenarios.ChangesAsync();
        using var second = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenTabsAsync(data, [first.Path, second.Path], activeTab: 1);
        var tabs = app.Session.Shell.Tabs;
        var remaining = tabs[0];

        await PressByTheKeysAsync(app, CloseButtonOf(app, tabs[1]), key);
        await app.WaitForAsync(() => tabs.Count == 1, "the second tab to close");

        await AssertFocusAsync(app, TabFact(remaining), $"{key} on the second tab's close button");
    }

    /// <summary>
    /// Three tabs, the third active: the first tab's close button, pressed by Enter, closes that
    /// tab, which was not active; the tab now at its place, the second, has the focus on its own
    /// button, with the frame, and the third stays active.
    /// </summary>
    [AvaloniaFact]
    public async Task Close_tab_pressed_by_the_keys_on_a_tab_not_active_gives_the_focus_to_the_tab_now_at_its_place()
    {
        using var first = await Scenarios.ChangesAsync();
        using var second = await Scenarios.ChangesAsync();
        using var third = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenTabsAsync(data, [first.Path, second.Path, third.Path], activeTab: 2);
        var tabs = app.Session.Shell.Tabs;
        var (now, active) = (tabs[1], tabs[2]);

        await PressByTheKeysAsync(app, CloseButtonOf(app, tabs[0]), Key.Enter);
        await app.WaitForAsync(() => tabs.Count == 2, "the first tab to close");

        await AssertFocusAsync(app, TabFact(now), "Enter on the first tab's close button");
        Assert.Same(active, app.Session.Shell.ActiveTab);
    }

    // ---- The toolbar -----------------------------------------------------------------------------

    /// <summary>
    /// README.md's unstaged change discarded, the one entry to undo: Undo, pressed by the keys,
    /// undoes it and is then disabled ("Nothing to undo"); the graph area's target, the graph, has
    /// the focus, with the frame (the toolbar is no region of its own).
    /// </summary>
    [AvaloniaFact]
    public async Task Undo_pressed_by_the_keys_that_undoes_the_last_entry_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var changes = await DiscardReadmeAsync(app);
        var undo = app.FindByAutomationId(ShellDriver.UndoButtonId);

        await PressByTheKeysAsync(app, undo, Key.Enter);
        await app.WaitForAsync(() => changes.UnstagedTitle == Phase8aExpectations.K.Unstaged7 && !undo.IsEffectivelyEnabled, "the discard undone and Undo disabled");

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Undo");
    }

    /// <summary>The discard undone by its command: Redo, pressed by the keys, redoes it and is then disabled; the graph has the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task Redo_pressed_by_the_keys_that_redoes_the_last_entry_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var changes = await DiscardReadmeAsync(app);
        Repository(app).Toolbar.UndoCommand.Execute(null);
        await app.WaitForAsync(() => changes.UnstagedTitle == Phase8aExpectations.K.Unstaged7, "the discard undone");
        await app.WaitForIdleAsync();
        var redo = app.FindByAutomationId(ShellDriver.RedoButtonId);
        await app.WaitForAsync(() => redo.IsEffectivelyEnabled, "Redo enabled");

        await PressByTheKeysAsync(app, redo, Key.Enter);
        await app.WaitForAsync(() => changes.UnstagedTitle == Phase8aExpectations.K.Unstaged6 && !redo.IsEffectivelyEnabled, "the discard redone and Redo disabled");

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Redo");
    }

    /// <summary>
    /// "changes": Stash, pressed by the keys, asks for the stash's message; Enter in its box stashes
    /// every change, untracked files too, and Stash is then disabled ("There are no changes to
    /// stash"); the graph has the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Stash_pressed_by_the_keys_that_stashes_every_change_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var stash = app.FindByAutomationId("StashButton");

        await PressByTheKeysAsync(app, stash, Key.Enter);
        var dialog = await app.WaitForDialogAsync<StashDialogViewModel>();
        await app.WaitForAsync(() => app.FocusedElement is TextBox, $"the focus in the stash's box (it is on {app.DescribeFocus()})");
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => dialog.IsClosed, "the stash dialog to close");
        await app.WaitForIdleAsync();
        await app.WaitForAsync(() => !stash.IsEffectivelyEnabled, "Stash disabled");

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Stash");
    }

    /// <summary>"graph", whose one stash is stash@{0}: Pop, pressed by the keys, pops it and is then disabled; the graph has the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task Pop_pressed_by_the_keys_that_pops_the_last_stash_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var pop = app.FindByAutomationId("PopButton");
        await app.WaitForAsync(() => pop.IsEffectivelyEnabled, "Pop enabled");

        await PressByTheKeysAsync(app, pop, Key.Enter);
        await app.WaitForIdleAsync();
        await app.WaitForAsync(() => !pop.IsEffectivelyEnabled, "Pop disabled");

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Pop");
    }

    /// <summary>
    /// "sync", main changed both here and on origin in README.md: Pull, pressed by the keys, stops on
    /// the conflict, and Pull is then disabled ("Finish or abort the merge first"); the graph has the
    /// focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Pull_pressed_by_the_keys_that_stops_on_a_conflict_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "README.md", "# Theirs\n", "Their change");
        await repo.CommitFileAsync("README.md", "# Ours\n", "Our change");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var pull = app.FindByAutomationId("PullButton");

        await PressByTheKeysAsync(app, pull, Key.Enter);
        await AnswerAnyDialogsByClickAsync(app, () => Repository(app).Banner.IsVisible && !pull.IsEffectivelyEnabled);

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Pull, stopped on a conflict");
    }

    // ---- The status bar's offer ------------------------------------------------------------------

    /// <summary>
    /// A success whose report offers "Open in browser": the status bar's offer, pressed by the keys,
    /// is disabled while its run waits (held here, as a browser that answers late); once it has
    /// answered, the offer has the focus back, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task The_status_bars_offer_pressed_by_the_keys_gets_the_focus_back_with_the_frame()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var repository = Repository(app);
        var held = new TaskCompletionSource();
        var report = VisualCommit.Core.Operations.OperationReport.Done("Created pull request #9") with
        {
            Offer = new VisualCommit.Core.Operations.OperationOffer("Open in browser", () => held.Task),
        };
        await repository.Queue.RunAsync(new VisualCommit.Core.Operations.GitOperation(
            "Create pull request for topic", "Creating…", VisualCommit.Core.Operations.OperationKind.Network, VisualCommit.Core.Operations.RefreshScope.None, _ => Task.FromResult(report)));
        await app.WaitForIdleAsync(repository);
        var offer = app.FindByAutomationId("OperationOfferButton");

        await PressByTheKeysAsync(app, offer, Key.Enter);
        app.Settle();
        Assert.False(offer.IsEffectivelyEnabled, "The offer is enabled while its run waits.");
        held.SetResult();
        await app.WaitForAsync(() => !repository.OperationStatus.OfferCommand.IsRunning, "the offer's run to end");

        await AssertFocusAsync(app, Framed(offer, VisualCommit.App.Views.KeyRegionRole.None), "Enter on the offer");
    }

    // ---- The stage panel -------------------------------------------------------------------------

    /// <summary>
    /// README.md's unstaged change discarded, the restore bar shown: its Dismiss, pressed by the
    /// keys, hides the bar with itself; the unstaged list's first row, the list the bar stood over,
    /// has the focus, with the frame (the stage panel's target, as F6 gives it).
    /// </summary>
    [AvaloniaTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public async Task The_restore_bars_Dismiss_pressed_by_the_keys_gives_the_focus_to_the_unstaged_lists_first_row(Key key)
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var changes = await DiscardReadmeAsync(app);

        await PressByTheKeysAsync(app, app.FindByAutomationId("DismissRestoreButton"), key);
        await app.WaitForAsync(() => !changes.HasRestore, "the restore bar to go");

        await AssertFocusAsync(app, RowFact(changes.Unstaged.Rows[0].Key, "UnstagedFileList"), $"{key} on Dismiss");
    }

    /// <summary>
    /// README.md's unstaged change discarded: the bar's Restore, pressed by the keys, restores it
    /// and the bar goes; the unstaged list's first row has the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task The_restore_bars_Restore_pressed_by_the_keys_that_restores_gives_the_focus_to_the_unstaged_lists_first_row()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var changes = await DiscardReadmeAsync(app);

        await PressByTheKeysAsync(app, app.FindByAutomationId("RestoreDiscardButton"), Key.Enter);
        await app.WaitForAsync(() => !changes.HasRestore && changes.UnstagedTitle == Phase8aExpectations.K.Unstaged7, "README.md restored and the bar gone");

        await AssertFocusAsync(app, RowFact(changes.Unstaged.Rows[0].Key, "UnstagedFileList"), "Enter on Restore");
    }

    /// <summary>Stage all, pressed by the keys, stages every file and hides itself with the unstaged list's header; the staged list's first row has the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task Stage_all_pressed_by_the_keys_that_stages_everything_gives_the_focus_to_the_staged_lists_first_row()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var changes = Repository(app).Changes;

        await PressByTheKeysAsync(app, app.FindByAutomationId("StageAllButton"), Key.Enter);
        await app.WaitForAsync(() => !changes.HasUnstaged, "every file staged");

        await AssertFocusAsync(app, RowFact(changes.Staged.Rows[0].Key, "StagedFileList"), "Enter on Stage all");
    }

    /// <summary>Discard all, pressed by the keys and its question confirmed, discards every unstaged change and hides itself; the staged list's first row has the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task Discard_all_pressed_by_the_keys_and_confirmed_gives_the_focus_to_the_staged_lists_first_row()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var changes = Repository(app).Changes;

        await PressByTheKeysAsync(app, app.FindByAutomationId("DiscardAllButton"), Key.Enter);
        await ConfirmByClickAsync(app);
        await app.WaitForAsync(() => !changes.HasUnstaged, "every unstaged change discarded");

        await AssertFocusAsync(app, RowFact(changes.Staged.Rows[0].Key, "StagedFileList"), "Enter on Discard all, confirmed");
    }

    /// <summary>Unstage all, pressed by the keys, unstages every file and hides itself; the unstaged list's first row has the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task Unstage_all_pressed_by_the_keys_gives_the_focus_to_the_unstaged_lists_first_row()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var changes = Repository(app).Changes;

        await PressByTheKeysAsync(app, app.FindByAutomationId("UnstageAllButton"), Key.Enter);
        await app.WaitForAsync(() => !changes.HasStaged, "every file unstaged");

        await AssertFocusAsync(app, RowFact(changes.Unstaged.Rows[0].Key, "UnstagedFileList"), "Enter on Unstage all");
    }

    /// <summary>
    /// A summary typed and Commit pressed by the keys: the commit empties the boxes, so Commit is
    /// disabled; the unstaged list's first row has the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Commit_pressed_by_the_keys_gives_the_focus_to_the_unstaged_lists_first_row()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var changes = Repository(app).Changes;
        app.PressKey(Key.M, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CommitSummary", "the focus in the summary");
        app.Type(Phase8aExpectations.K.First);
        var commit = app.FindByAutomationId("CommitButton");

        await PressByTheKeysAsync(app, commit, Key.Enter);
        await app.WaitForAsync(() => !changes.IsCommitting && !changes.HasStaged && !commit.IsEffectivelyEnabled, "the commit to end");

        Assert.Equal(Phase8aExpectations.K.First, (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
        await AssertFocusAsync(app, RowFact(changes.Unstaged.Rows[0].Key, "UnstagedFileList"), "Enter on Commit");
    }

    // ---- The diff --------------------------------------------------------------------------------

    /// <summary>src/Calculator.cs's unstaged diff over the graph: its Close, pressed by the keys, closes it; the graph has the focus, with the frame.</summary>
    [AvaloniaTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public async Task The_diffs_Close_pressed_by_the_keys_gives_the_focus_to_the_graph_with_the_frame(Key key)
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenCalculatorDiffAsync(app);

        await PressByTheKeysAsync(app, app.FindByAutomationId("CloseDiffButton"), key);
        await app.WaitForAsync(() => Repository(app).Diff is null, "the diff to close");

        await AssertFocusAsync(app, GraphFramedFact, $"{key} on the diff's Close");
    }

    /// <summary>Esc in the diff's text, which has the focus without the frame, closes the diff; the graph has the focus, without the frame.</summary>
    [AvaloniaFact]
    public async Task Esc_in_the_diffs_text_gives_the_focus_to_the_graph()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenCalculatorDiffAsync(app);
        Assert.False(app.FocusVisible, "The diff's text shows the keyboard's frame.");

        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => Repository(app).Diff is null, "the diff to close");

        await AssertFocusAsync(app, GraphFact, "Esc in the diff's text");
    }

    /// <summary>The diff's Stage file, pressed by the keys, stages src/Calculator.cs, which leaves the unstaged list, and the diff closes; the graph has the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task The_diffs_Stage_file_pressed_by_the_keys_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenCalculatorDiffAsync(app);

        await PressByTheKeysAsync(app, app.FindByAutomationId("StageFileButton"), Key.Enter);
        await app.WaitForAsync(() => Repository(app).Diff is null, "the diff to close");

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Stage file");
    }

    /// <summary>config/settings.json's staged diff: its Unstage file, pressed by the keys, unstages it, and the diff closes; the graph has the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task The_diffs_Unstage_file_pressed_by_the_keys_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenStagedDiffAsync(app, "config/settings.json");

        await PressByTheKeysAsync(app, app.FindByAutomationId("UnstageFileButton"), Key.Enter);
        await app.WaitForAsync(() => Repository(app).Diff is null, "the diff to close");

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Unstage file");
    }

    /// <summary>
    /// The diff's Discard file, pressed by the keys and its question confirmed by a click, discards
    /// src/Calculator.cs's changes, and the diff closes; the graph has the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task The_diffs_Discard_file_pressed_by_the_keys_and_confirmed_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenCalculatorDiffAsync(app);

        await PressByTheKeysAsync(app, app.FindByAutomationId("DiscardFileButton"), Key.Enter);
        await ConfirmByClickAsync(app);
        await app.WaitForAsync(() => Repository(app).Diff is null, "the diff to close");

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Discard file, confirmed");
    }

    /// <summary>
    /// Two lines of src/Calculator.cs's second hunk chosen by the arrows: Stage lines, pressed by the
    /// keys, stages them and hides itself with the selection; the text, where the lines were chosen,
    /// has the focus, without the frame, as after Discard lines.
    /// </summary>
    [AvaloniaFact]
    public async Task Stage_lines_pressed_by_the_keys_gives_the_focus_to_the_text()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenCalculatorDiffAsync(app);
        var diff = await ChooseTwoCalculatorLinesAsync(app);

        await PressByTheKeysAsync(app, app.FindByAutomationId("StageLinesButton"), Key.Enter);
        await app.WaitForAsync(() => diff.SelectedChanges.Count == 0 && !app.FindByAutomationId("StageLinesButton").IsEffectivelyVisible, "the lines staged");

        await AssertFocusAsync(app, DiffTextFact, "Enter on Stage lines");
    }

    /// <summary>README.md's staged diff, two of its four added lines chosen: Unstage lines, pressed by the keys, unstages them and hides itself; the text has the focus, without the frame.</summary>
    [AvaloniaFact]
    public async Task Unstage_lines_pressed_by_the_keys_gives_the_focus_to_the_text()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenStagedDiffAsync(app, Phase8aExpectations.K.Readme);
        var diff = Repository(app).Diff!;
        app.PressKey(Key.Home, RawInputModifiers.Control);
        await ChooseChangedLinesAsync(app, diff, 2);

        await PressByTheKeysAsync(app, app.FindByAutomationId("UnstageLinesButton"), Key.Enter);
        await app.WaitForAsync(() => diff.SelectedChanges.Count == 0 && !app.FindByAutomationId("UnstageLinesButton").IsEffectivelyVisible, "the lines unstaged");

        await AssertFocusAsync(app, DiffTextFact, "Enter on Unstage lines");
    }

    /// <summary>
    /// README.md's unstaged diff, its one change (a line removed and one added) chosen: Discard
    /// lines, pressed by the keys and confirmed by a click, discards README.md's last unstaged
    /// change, and the diff closes; the graph has the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Discard_lines_pressed_by_the_keys_on_the_files_last_change_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenUnstagedDiffAsync(app, Phase8aExpectations.K.Readme);
        var diff = Repository(app).Diff!;
        app.PressKey(Key.Home, RawInputModifiers.Control);
        await ChooseChangedLinesAsync(app, diff, 2);

        await PressByTheKeysAsync(app, app.FindByAutomationId("DiscardLinesButton"), Key.Enter);
        await ConfirmByClickAsync(app);
        await app.WaitForAsync(() => Repository(app).Diff is null, "the diff to close");

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Discard lines, confirmed");
    }

    /// <summary>README.md's unstaged diff, one hunk: its Stage hunk, reached by Tab from the text and pressed by Enter, stages it, and the diff closes; the graph has the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task Stage_hunk_pressed_by_the_keys_on_the_files_last_hunk_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenUnstagedDiffAsync(app, Phase8aExpectations.K.Readme);
        Assert.Single(Repository(app).Diff!.Diff!.Hunks);

        await app.TabToAsync(focused => focused is Control control && AutomationProperties.GetAutomationId(control) == "StageHunkButton", "Stage hunk");
        Assert.True(app.FocusVisible, "Stage hunk shows no keyboard frame.");
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => Repository(app).Diff is null, "the diff to close");

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Stage hunk");
    }

    // ---- The right panel -------------------------------------------------------------------------

    /// <summary>
    /// "Add settings page" in the details: its parent's link, pressed by the keys, shows the parent,
    /// whose own parent's link stands in its place; that link has the focus, with the frame.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public async Task A_parents_link_pressed_by_the_keys_gives_the_focus_to_the_link_now_at_its_place(Key key)
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.Click(app.GraphRowPoint(2));
        app.WaitForCommitDetails();
        await app.WaitForIdleAsync();
        var details = Repository(app).Details;
        var link = app.FindByAutomationId("ParentLink");
        var before = details.Parents[0];

        await PressByTheKeysAsync(app, link, key);
        await app.WaitForAsync(() => details.Parents.Count > 0 && !ReferenceEquals(details.Parents[0], before) && details.Sha == before.Sha, "the parent in the details");
        await app.WaitForIdleAsync();

        await AssertFocusAsync(app, $"Button ParentLink \"{AutomationProperties.GetName(app.FindByAutomationId("ParentLink"))}\" [Right], framed", $"{key} on the parent's link");
    }

    /// <summary>The comparison of feature/conflict with main: Swap, pressed by the keys, is disabled while the comparison is read the other way round, and has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task Swap_pressed_by_the_keys_gets_the_focus_back_with_the_frame()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.Click(await app.RefRowAsync(Phase8aExpectations.U.LocalBranches, Phase8aExpectations.U.Feature));
        app.RightClick(await app.RefRowAsync(Phase8aExpectations.U.LocalBranches, Phase8aExpectations.U.Feature, Phase8aExpectations.U.Conflict));
        app.ClickMenuItem(Phase8aExpectations.U.CompareConflictWithMain);
        await app.WaitForCompareAsync();
        var comparison = Repository(app).Comparison!;
        var swap = app.FindByAutomationId("CompareSwap");
        var from = comparison.FromText;

        await PressByTheKeysAsync(app, swap, Key.Enter);
        await app.WaitForAsync(() => comparison.FromText != from && !comparison.SwapCommand.IsRunning, "the comparison swapped");

        await AssertFocusAsync(app, $"Button CompareSwap \"{AutomationProperties.GetName(swap)}\" [Right], framed", "Enter on Swap");
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    /// <summary>The diff's text, without the frame: the focused element lies in <c>DiffText</c>, in the graph area.</summary>
    private const string DiffTextFact = "in DiffText [GraphArea]";

    /// <summary>
    /// Gives <paramref name="control"/> the focus as Tab does, with the keyboard's frame, and presses
    /// <paramref name="key"/> on it.
    /// </summary>
    private static async Task PressByTheKeysAsync(ShellDriver app, Control control, Key key)
    {
        await FocusByTheKeysAsync(app, control);
        app.PressKey(key);
    }

    /// <summary>Gives <paramref name="control"/> the focus as Tab does, with the keyboard's frame.</summary>
    private static async Task FocusByTheKeysAsync(ShellDriver app, Control control)
    {
        control.BringIntoView();
        app.Settle();
        var name = $"{AutomationProperties.GetAutomationId(control)} \"{AutomationProperties.GetName(control)}\"";
        await app.WaitForAsync(() => control is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true }, $"{name} shown and enabled");
        if (control.IsFocused && !app.FocusVisible)
        {
            // Focused without the frame (a click, or the view's own focus): Tab away and back, as a keyboard user would.
            app.PressKey(Key.Tab);
            app.PressKey(Key.Tab, RawInputModifiers.Shift);
        }

        Assert.True(control.Focus(NavigationMethod.Tab), $"{name} takes no focus.");
        app.Settle();
        Assert.True(ReferenceEquals(app.FocusedElement, control) && app.FocusVisible, $"{name} is not focused with the keyboard's frame: the focus is on {FocusFact(app)}.");
    }

    /// <summary>
    /// Waits, a short while, for the focus <see cref="FocusOf"/> names to be <paramref name="expected"/>;
    /// then, the active tab's operations ended and the dispatcher's queue run, that it is still
    /// there. <paramref name="after"/> says what was done, for the message.
    /// </summary>
    private static async Task AssertFocusAsync(ShellDriver app, string expected, string after)
    {
        try
        {
            await app.WaitForAsync(() => FocusOf(app) == expected, $"the focus on {expected}", FocusWait);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"After {after} the focus is on {FocusOf(app)}, not {expected}.");
        }

        if (app.Session.Shell.ActiveTab.Repository is { } repository)
        {
            await app.WaitForIdleAsync(repository);
        }

        app.Settle();
        Assert.True(FocusOf(app) == expected, $"After {after}, once the app was idle, the focus is on {FocusOf(app)}, not {expected}.");
    }

    /// <summary>
    /// The focus as <see cref="FocusFact"/> names it; a focus inside the diff's text (its text area,
    /// which has no id of its own) as "in DiffText" with its region and its frame.
    /// </summary>
    private static string FocusOf(ShellDriver app) =>
        app.FocusedElement is Control control && AutomationProperties.GetAutomationId(control) is null or ""
            && control.GetVisualAncestors().OfType<Control>().FirstOrDefault(ancestor => AutomationProperties.GetAutomationId(ancestor) == "DiffText") is not null
            ? $"in DiffText [{app.FocusedRegion()}]" + (app.FocusVisible ? ", framed" : string.Empty)
            : FocusFact(app);

    /// <summary>A stage list's row's own button, with the keyboard's frame, as <see cref="FocusFact"/> names it.</summary>
    private static string RowFact(string path, string list) => $"Button StageFileRow \"{path}\" of {path} in {list} [Right], framed";

    /// <summary>A tab's own button, with the keyboard's frame, as <see cref="FocusFact"/> names it.</summary>
    private static string TabFact(RepoTabViewModel tab) => $"Button {tab.TabAutomationId} \"{tab.Title}\" [None], framed";

    /// <summary>The close button of <paramref name="tab"/> in the tab strip.</summary>
    private static Button CloseButtonOf(ShellDriver app, RepoTabViewModel tab)
    {
        var list = app.Find<ItemsControl>("TabList");
        var container = list.ContainerFromItem(tab) ?? throw new InvalidOperationException($"The tab {tab.Title} has no control in the strip.");
        return container.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "CloseTabButton");
    }

    /// <summary>Tabs on <paramref name="folders"/>, <paramref name="activeTab"/> active, its repository read and the watchers quiet, the pointer at rest.</summary>
    private static async Task<ShellDriver> OpenTabsAsync(TempDirectory data, IReadOnlyList<string?> folders, int activeTab)
    {
        var app = ShellDriver.StartWithTabs(data.Path, folders, activeTab: activeTab);
        try
        {
            await app.WaitForRepositoryAsync();
            await app.WaitForWatchersQuietAsync();
            app.MoveToNeutral();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The stage panel shown and README.md's unstaged change discarded by its command, the question
    /// confirmed by a click: the restore bar shows, the app is idle and the pointer at rest.
    /// </summary>
    private static async Task<WorkingChangesViewModel> DiscardReadmeAsync(ShellDriver app)
    {
        await ShowStagePanelAsync(app);
        var repository = Repository(app);
        var changes = repository.Changes;
        changes.DiscardFileCommand.Execute(changes.Unstaged.Rows.Single(row => row.Key == Phase8aExpectations.K.Readme));
        await ConfirmByClickAsync(app);
        await app.WaitForAsync(() => changes.HasRestore && changes.UnstagedTitle == Phase8aExpectations.K.Unstaged6, "the discard and its restore bar");
        await app.WaitForIdleAsync(repository);
        app.MoveToNeutral();
        return changes;
    }

    /// <summary>The open question confirmed by a click on its confirm button, and closed.</summary>
    private static async Task ConfirmByClickAsync(ShellDriver app)
    {
        var question = await app.WaitForDialogAsync<ConfirmationViewModel>();
        app.Click(app.Find<Button>("ConfirmButton"));
        await app.WaitForAsync(() => question.IsClosed, "the question to close");
    }

    /// <summary>The unstaged diff of <paramref name="path"/> opened by a click on its row, its text shown with the focus.</summary>
    private static async Task OpenUnstagedDiffAsync(ShellDriver app, string path)
    {
        await ShowStagePanelAsync(app);
        app.Click(await app.FileRowAsync("UnstagedFileList", path));
        await app.WaitForAsync(() => Repository(app).Diff is { ShowsText: true }, $"{path}'s diff");
        await app.WaitForAsync(() => app.FocusedAutomationId() == "DiffText", $"the focus in the diff's text (it is on {app.DescribeFocus()})");
        app.MoveToNeutral();
    }

    /// <summary>The staged diff of <paramref name="path"/> opened by a click on its row, its text shown with the focus.</summary>
    private static async Task OpenStagedDiffAsync(ShellDriver app, string path)
    {
        await ShowStagePanelAsync(app);
        app.Click(await app.FileRowAsync("StagedFileList", path));
        await app.WaitForAsync(() => Repository(app).Diff is { ShowsText: true }, $"{path}'s staged diff");
        await app.WaitForAsync(() => app.FocusedAutomationId() == "DiffText", $"the focus in the diff's text (it is on {app.DescribeFocus()})");
        app.MoveToNeutral();
    }

    /// <summary>Two changed lines of src/Calculator.cs's second hunk chosen by the arrows, as G1 chooses them.</summary>
    private static async Task<VisualCommit.App.ViewModels.Diff.DiffViewModel> ChooseTwoCalculatorLinesAsync(ShellDriver app)
    {
        var diff = Repository(app).Diff!;
        app.PressKey(Key.Home, RawInputModifiers.Control);
        var downs = 1 + diff.Diff!.Hunks[0].Lines.Count + Phase8aExpectations.K.DownsToLines;
        for (var down = 0; down < downs; down++)
        {
            app.PressKey(Key.Down);
        }

        for (var down = 0; down < Phase8aExpectations.K.ShiftDownsForLines; down++)
        {
            app.PressKey(Key.Down, RawInputModifiers.Shift);
        }

        await app.WaitForAsync(() => diff.SelectedChanges.Count == 2, "two changed lines selected");
        return diff;
    }

    /// <summary>From the text's first row (Ctrl+Home), Down to the first changed line, then Shift+Down until <paramref name="count"/> changed lines are chosen.</summary>
    private static async Task ChooseChangedLinesAsync(ShellDriver app, VisualCommit.App.ViewModels.Diff.DiffViewModel diff, int count)
    {
        var lines = diff.Diff!.Hunks[0].Lines;
        var first = lines.ToList().FindIndex(line => line.Kind != VisualCommit.Core.Diff.DiffLineKind.Context);
        Assert.True(first >= 0, "The first hunk has no changed line.");

        // Row 0 is the hunk's header.
        for (var down = 0; down < first + 1; down++)
        {
            app.PressKey(Key.Down);
        }

        for (var shift = 0; diff.SelectedChanges.Count < count; shift++)
        {
            Assert.True(shift < 8, $"Eight Shift+Down chose {diff.SelectedChanges.Count} changed lines, not {count}.");
            app.PressKey(Key.Down, RawInputModifiers.Shift);
        }

        await app.WaitForAsync(() => diff.SelectedChanges.Count == count, $"{count} changed lines selected");
    }
}
