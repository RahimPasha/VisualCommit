// Part U of phase 8a's walk-through, screen readers (docs/test-reports/phase-8a.md, "Part U";
// docs/phase-8a-design.md 9.5; D411, D435, D450): the graph's selected row as a client meets it
// (U1), the sweep on the ops tab's busiest state (U2), and the fixed names of 1.12's table read
// with every tooltip, help text and key as they were (U3). Three methods, three starts, as the
// report's methods table has them; method 2 is the scripted counterpart of the real window's
// launch 13 (no twin: that launch takes no capture). Its helpers are at the end of this file.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Shell;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>Part U: screen readers (docs/test-reports/phase-8a.md; design 9.5).</summary>
public sealed class Phase8aChecksU : Phase8aChecks
{
    /// <summary>
    /// Method 1, U1 on a fresh "ops" with README.md changed: "Main work" by a click on its branch and
    /// F6, a picture before and after the peers are made (the same bytes), then Down, Up, two
    /// Shift+Downs, six Ups to the working-changes row and a click on a branch in the left panel,
    /// each raising one name change of the one exposed row.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U1_graph_row_by_keys()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile(U.Readme, U.ReadmeChanged);
        using var data = new TempDirectory("data");
        using var pictures = new TempDirectory("phase8a u1 frames");

        // Step 1.
        using var app = await OpenAsync(data, repo.Path);
        var repository = Repository(app);
        await app.WaitForAsync(() => repository.Graph.IsComplete && app.GraphControl().ShowsWorkingRow, "the graph with the working-changes row");
        var exact = await repo.ExactStateAsync();

        // Step 2 (u1a): Main work by its branch, then F6 to the graph.
        app.Click(await app.RefRowAsync(U.LocalBranches, U.Main));
        await app.WaitForAsync(() => repository.SelectedCommits is [{ Subject: "Main work" }] && TextOf(app, "DetailsSubject") == "Main work", "Main work selected with its details");
        app.PressF6();
        await app.WaitForAsync(() => app.FocusedAutomationId() == U.GraphRowsId, $"the focus on the graph (it is on {app.DescribeFocus()})");
        var first = Picture(app, U.U1aGraphRowHead);
        Assert.True(app.FocusVisible, "The graph shows no keyboard frame after F6.");
        Assert.True(((RefItemRow)app.RefRowModel(U.LocalBranches, U.Main)).IsSelected, "Local branches › main is not selected.");
        Assert.Null(ControlAutomationPeer.FromElement(app.GraphControl()));

        // Step 3: the peers made, the same frame again.
        var graph = app.GraphPeer();
        var row = app.GraphRowPeer();
        using var names = ShellDriver.RecordNameChanges(row);
        app.MoveToNeutral();
        var again = Path.Combine(pictures.Path, U.U1aGraphRowHead + ".png");
        app.Capture().Save(again);
        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(again));
        AssertGraphPeer(graph);
        AssertExposedRow(app, graph, row, U.MainWork, U.MainWork.Hint, selected: 1, focus: U.GraphRowsId);
        await app.WaitForIdleAsync();
        Assert.Empty(names.Changes);

        // Steps 4 to 7: each key one name change, the old value the name raised before it.
        string? lastName = U.FirstOldName;
        for (var step = 0; step < U.OpsMoves.Count; step++)
        {
            var move = U.OpsMoves[step];
            var count = names.Count;
            PressMove(app, move.Key);
            lastName = await AssertOneNameChangeAsync(app, names, count, move.Row.Name, lastName, $"{move.Key} (move {step + 1})");
            AssertExposedRow(app, graph, row, move.Row, move.Hint, move.Selected, U.GraphRowsId);
            if (step == 3)
            {
                // Step 6 (u1b): three rows selected, the multi-selection panel.
                Picture(app, U.U1bGraphRowsThreeSelected);
                Assert.Equal(["Main work", "Alpha two", "Alpha one"], repository.SelectedCommits.Select(commit => commit.Subject));
                Assert.Contains(U.ThreeCommitsSelected, TextsIn(app.FindByAutomationId("RightPanel")));
                Assert.False(((RefItemRow)app.RefRowModel(U.LocalBranches, U.Main)).IsSelected, "Local branches › main keeps its selection with several selected.");
                Assert.True(app.FocusVisible, "The graph lost its keyboard frame.");
            }
        }

        // Step 7 (u1c): the working-changes row and the stage panel.
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.ShowsStagePanel && TextOf(app, "UnstagedTitle") == U.Unstaged1 && TextOf(app, "StagedTitle") == U.Staged0, "the stage panel");
        Picture(app, U.U1cGraphWorkingRow);
        Assert.True(repository.IsWorkingRowSelected, "The working-changes row is not selected.");
        Assert.Equal([U.Readme], repository.Changes.Unstaged.Rows.Select(changed => changed.Key));
        Assert.True(app.FocusVisible, "The graph lost its keyboard frame.");

        // Step 8: the folder's click raises nothing; alpha's click moves the row, the focus in the left panel.
        var before = names.Count;
        app.Click(await app.RefRowAsync(U.LocalBranches, U.Feature));
        var alpha = await app.RefRowAsync(U.LocalBranches, U.Feature, U.Alpha);
        await app.WaitForIdleAsync();
        Assert.Equal(before, names.Count);
        app.Click(alpha);
        lastName = await AssertOneNameChangeAsync(app, names, before, U.AlphaTwo.Name, lastName, "the click on alpha");
        AssertExposedRow(app, graph, row, U.AlphaTwo, U.AlphaTwo.Hint, selected: 1, focus: U.RefItemId);
        Assert.Equal(U.Alpha, ControlAutomationPeer.CreatePeerForElement((Control)app.FocusedElement!).GetName());
        Assert.False(row.HasKeyboardFocus());

        // Step 9.
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// Method 2, U1 and U3 on a fresh stopped merge with u-untracked.txt (launch 13's session): "Ours
    /// edits" clicked, its row's peer made, Up, Down, Down; then the resolver of notes.txt and its
    /// five names.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U1_U3_stopped_merge_rows_and_resolver()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        repo.WriteFile(U.Untracked, U.UntrackedContent);
        using var data = new TempDirectory("data");

        // Step 1.
        using var app = await OpenAsync(data, repo.Path);
        var repository = Repository(app);
        await app.WaitForAsync(() => IsShown(app, ShellDriver.BannerContinueId), "the merge's banner");
        await app.WaitForAsync(() => repository.Graph.IsComplete && app.GraphControl().ShowsWorkingRow, "the graph with the working-changes row");
        var exact = await repo.ExactStateAsync();

        // Step 2: Ours edits clicked; the peers made.
        Assert.True(IsShown(app, ShellDriver.BannerContinueId), "The merge's banner does not show.");
        app.Click(app.GraphRowPoint(0));
        await app.WaitForAsync(() => repository.SelectedCommits is [{ Subject: "Ours edits" }] && app.FocusedAutomationId() == U.GraphRowsId, "Ours edits selected with the focus on the graph");
        await app.WaitForIdleAsync();
        var graph = app.GraphPeer();
        var row = app.GraphRowPeer();
        using var names = ShellDriver.RecordNameChanges(row);
        AssertGraphPeer(graph);
        AssertExposedRow(app, graph, row, U.OursEdits, U.OursEdits.Hint, selected: 1, focus: U.GraphRowsId);

        // Step 3: Up, Down, Down.
        string? lastName = U.FirstOldName;
        foreach (var move in U.MergeMoves)
        {
            var count = names.Count;
            PressMove(app, move.Key);
            lastName = await AssertOneNameChangeAsync(app, names, count, move.Row.Name, lastName, move.Key);
            AssertExposedRow(app, graph, row, move.Row, move.Hint, move.Selected, U.GraphRowsId);
        }

        // Step 4 (u3a): the stage panel, then the resolver by a double-click on notes.txt.
        app.Click(app.WorkingRowPoint());
        var notes = await app.FileRowAsync(U.UnstagedListId, U.Notes);
        _ = await app.FileRowAsync(U.UnstagedListId, U.Untracked);
        AssertTip(notes, U.ConflictToolTip);
        app.MoveToNeutral();
        app.DoubleClick(notes);
        await app.WaitForResolverAsync(U.Notes);
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        Picture(app, U.U3aResolverNames);

        // Step 5: the five names; the sweep.
        foreach (var id in U.ResolverIds)
        {
            var control = app.FindByAutomationId(id);
            Assert.Equal(AutomationControlType.Button, ControlAutomationPeer.CreatePeerForElement(control).GetAutomationControlType());
            AssertNamed(app, U.FixedNames.Single(named => named.Id == id));
        }

        AutomationSweep.AssertNamed(app.Window, U.SweepResolver);

        // Step 6: Esc, then the resolver closed. The double-click leaves the keyboard focus in the
        // result, where the resolver takes Esc and does nothing (phase 4's 11.10), so a resolver
        // still shown is closed by its close button, as phase 4's checks close it; its result is
        // unchanged, so nothing asks.
        app.PressKey(Key.Escape);
        await app.WaitForIdleAsync();
        if (app.ShownResolver is not null)
        {
            app.ClickResolverButton("ResolverCloseButton");
        }

        await app.WaitForResolverClosedAsync();
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// Method 3, U2 and U3 on a fresh "ops" with three changes: the search's names, the comparison's,
    /// feature/alpha hidden, the activity log, README.md's diff and u-untracked.txt hovered (u2a, the
    /// sweep), the rows' and eyes' names, feature/ff soloed and src/core.txt hovered (u3b), then a
    /// discard of README.md and the restore bar's name.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U2_U3_sweep_and_names_on_the_ops_tab()
    {
        using var repo = await AccessibleNameSweepTests.OpsWithThreeChangesAsync();
        using var data = new TempDirectory("data");

        // Step 1.
        using var app = await OpenAsync(data, repo.Path);
        var repository = Repository(app);
        var exact = await repo.ExactStateAsync();
        app.MoveToNeutral();

        // Step 2: the search bar's three names.
        app.PressSearch();
        await app.WaitForAsync(() => repository.Search is not null && app.FindByAutomationId("SearchBox").IsFocused, "the search bar with its box focused");
        Assert.Equal(U.CommitsMode, repository.Search!.ModeLabel);
        foreach (var id in U.SearchIds)
        {
            AssertNamed(app, U.FixedNames.Single(named => named.Id == id));
        }

        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => repository.Search is null, "the search bar to close");

        // Step 3: the comparison's two names.
        app.Click(await app.RefRowAsync(U.LocalBranches, U.Feature));
        app.RightClick(await app.RefRowAsync(U.LocalBranches, U.Feature, U.Conflict));
        app.ClickMenuItem(U.CompareConflictWithMain);
        await app.WaitForAsync(() => app.FindByAutomationId("CompareSwap").IsEffectivelyVisible && app.FindByAutomationId("CompareClose").IsEffectivelyVisible, "the comparison's two buttons");
        var comparison = app.Compare()!;
        Assert.Equal((U.CompareFrom, U.CompareTo), (comparison.From, comparison.To));
        Assert.True(repository.Comparison is { IsBranches: true }, "The comparison is not one of branches.");
        foreach (var id in U.CompareIds)
        {
            AssertNamed(app, U.FixedNames.Single(named => named.Id == id));
        }

        app.Click(app.FindByAutomationId("CompareClose"));
        await app.WaitForAsync(() => app.Compare() is null, "the comparison to close");

        // Step 4: feature/alpha hidden, its upstream with it.
        app.RightClick(await app.RefRowAsync(U.LocalBranches, U.Feature, U.Alpha));
        app.ClickMenuItem(U.HideFeatureAlpha);
        await app.WaitForAsync(
            () => app.RefRowMarks(U.LocalBranches, U.Feature, U.Alpha).ShowsHiddenToggle && !GraphSubjects(repository).Contains("Alpha one") && !GraphSubjects(repository).Contains("Alpha two"),
            "feature/alpha hidden and out of the graph");
        app.Click(await app.RefRowAsync(U.Remotes, U.Origin, U.Feature));
        await app.WaitForAsync(() => app.RefRowMarks(U.Remotes, U.Origin, U.Feature, U.Alpha).ShowsHiddenToggle, "origin/feature/alpha's eye");

        // Step 5: the activity log.
        app.PressKey(Key.J, RawInputModifiers.Control);
        await app.WaitForAsync(() => app.FindByAutomationId("ActivityLogClose").IsEffectivelyVisible, "the activity log");

        // Step 6: the working-changes row; README.md's diff.
        app.Click(app.WorkingRowPoint());
        await app.WaitForAsync(() => TextOf(app, "UnstagedTitle") == U.Unstaged2 && TextOf(app, "StagedTitle") == U.Staged1, "the stage panel's two lists");
        app.Click(await app.FileRowAsync(U.UnstagedListId, U.Readme));
        await app.WaitForAsync(() => repository.Diff is { ShowsText: true } diff && diff.Target.Path == U.Readme && app.FindByAutomationId("CloseDiffButton").IsEffectivelyVisible, "README.md's diff");
        await app.WaitForIdleAsync();
        Assert.Equal([U.Readme, U.Untracked], repository.Changes.Unstaged.Rows.Select(changed => changed.Key));
        Assert.Equal([U.Core], repository.Changes.Staged.Rows.Select(changed => changed.Key));

        // Step 7 (u2a): u-untracked.txt hovered until its tooltip shows; the sweep.
        var tip = await app.RestPointerOnAsync(RowOwnButton(app, U.UnstagedListId, U.Untracked));
        Assert.Equal(U.Untracked, tip);
        Picture(app, U.U2aSweepOpsTab, keepPointer: true);
        AssertTwoRowsButtonsInUiAutomation(app);
        AutomationSweep.AssertNamed(app.Window, U.SweepOpsTab);

        // Step 8: the names of the rows, the diff's close, the log's two, the splitters and the eyes.
        foreach (var named in U.RowNames.Where(named => named.List == U.UnstagedListId))
        {
            AssertRowNamed(app, named);
        }

        foreach (var id in new[] { "CloseDiffButton" }.Concat(U.LogAndSplitterIds))
        {
            AssertNamed(app, U.FixedNames.Single(named => named.Id == id));
        }

        AssertRowNamed(app, U.RowNames.Single(named => named.Row == "feature/alpha"));
        AssertRowNamed(app, U.RowNames.Single(named => named.Row == "origin/feature/alpha"));

        // The two alpha rows through their own peers: the help text the ref's full name, from the
        // row's ToolTip, and no tooltip on the row's button (D456 (3)).
        foreach (var help in U.RefRowHelpTexts)
        {
            var refRow = RefItemButton(app, help.Path);
            var what = string.Join(" › ", help.Path);
            var helpText = ControlAutomationPeer.CreatePeerForElement(refRow).GetHelpText();
            Assert.True(help.HelpText == helpText, $"{what}'s peer reads the help text \"{helpText}\", not \"{help.HelpText}\".");
            Assert.True(ToolTip.GetTip(refRow) is null, $"{what}'s button has the tooltip \"{ToolTip.GetTip(refRow)}\".");
        }

        // Step 9: feature/ff soloed.
        app.RightClick(await app.RefRowAsync(U.LocalBranches, U.Feature, U.Ff));
        app.ClickMenuItem(U.SoloFeatureFf);
        await app.WaitForAsync(() => app.RefRowMarks(U.LocalBranches, U.Feature, U.Ff).ShowsSoloToggle, "ff's solo button");
        Assert.True(app.RefRowMarks(U.LocalBranches, U.Feature, U.Alpha).ShowsHiddenToggle, "alpha's eye went in solo mode.");
        await app.WaitForIdleAsync();

        // Step 10 (u3b): src/core.txt hovered until its tooltip shows; the names; the sweep.
        tip = await app.RestPointerOnAsync(RowOwnButton(app, U.StagedListId, U.Core));
        Assert.Equal(U.Core, tip);
        Picture(app, U.U3bStageRowNames, keepPointer: true);
        Assert.True(app.StageRow(U.UnstagedListId, U.Readme)!.Classes.Contains("selected"), "README.md's row is no longer selected.");
        AssertRowNamed(app, U.RowNames.Single(named => named.Id == "RowUnstageButton"));
        AssertRowNamed(app, U.RowNames.Single(named => named.Id == "RefSoloToggle"));
        AssertRowNamed(app, U.RowNames.Single(named => named.Row == "feature/alpha"));
        AssertRowNamed(app, U.RowNames.Single(named => named.Row == "origin/feature/alpha"));
        AutomationSweep.AssertNamed(app.Window, U.SweepSoloed);

        // Step 11: README.md discarded; the restore bar.
        var readme = app.StageRow(U.UnstagedListId, U.Readme)!;
        app.MoveMouse(readme);
        app.Settle();
        app.Click(ShellDriver.RowButton(readme, "RowDiscardButton"));
        var question = await app.WaitForDialogAsync<ConfirmationViewModel>();
        Assert.Equal(U.DiscardTitle, question.Title);
        var confirm = (Button)app.FindByAutomationId(app.TopDialogView!, "ConfirmButton");
        Assert.Equal(U.DiscardButton, confirm.Content as string);
        Assert.Contains("danger", confirm.Classes);
        app.Click(confirm);
        await app.WaitForAsync(() => TextOf(app, "RestoreBarText") == U.RestoreText, "the restore bar");
        await app.WaitForIdleAsync();
        Assert.Equal(U.Restore, ControlAutomationPeer.CreatePeerForElement(app.FindByAutomationId("RestoreDiscardButton")).GetName());
        Assert.DoesNotContain(U.Readme, repository.Changes.Unstaged.Rows.Select(changed => changed.Key));
        AssertNamed(app, U.FixedNames.Single(named => named.Id == "DismissRestoreButton"));
        AutomationSweep.AssertNamed(app.Window, U.SweepRestoreBar);

        // Step 12: README.md as committed (its file and its status line), one backup ref more, whose
        // commit holds README.md as it was (D67); nothing else.
        var after = (await repo.ExactStateAsync()).Split('\n').ToList();
        var backup = Assert.Single(after, line => line.StartsWith(U.BackupRefPrefix, StringComparison.Ordinal));
        var backupCommit = backup[(backup.LastIndexOf(' ') + 1)..];
        Assert.Equal(U.ReadmeChanged, (await repo.GitAsync("show", backupCommit + ":" + U.Readme)).StandardOutput);
        after.Remove(backup);
        var expected = exact.Replace(U.Readme + ": " + U.ReadmeChanged, U.Readme + ": " + U.ReadmeCommitted, StringComparison.Ordinal)
            .Split('\n')
            .Where(line => !(line.StartsWith("1 ", StringComparison.Ordinal) && line.EndsWith(" " + U.Readme, StringComparison.Ordinal)));
        Assert.Equal(expected, after);
        Assert.Equal("M  " + U.Core, (await repo.GitAsync("status", "--porcelain", "--", U.Core)).StandardOutput.TrimEnd('\r', '\n'));
        Assert.Equal("?? " + U.Untracked, (await repo.GitAsync("status", "--porcelain", "--", U.Untracked)).StandardOutput.TrimEnd('\r', '\n'));
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private static RepositoryViewModel Repository(ShellDriver app) => app.Session.Shell.ActiveTab.Repository!;

    /// <summary><see cref="Phase8aChecks.Shot"/>, then no clipped text in the window (the report's conventions); returns the saved file.</summary>
    private static string Picture(ShellDriver app, string name, bool keepPointer = false)
    {
        Shot(app, name, keepPointer);
        AssertNoClippedText(app);
        return Path.Combine(VisualRun.FolderFor(Phase), name + ".png");
    }

    /// <summary>The text of the shown control whose automation id is <paramref name="automationId"/>, or null while none shows.</summary>
    private static string? TextOf(ShellDriver app, string automationId) =>
        app.Window.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == automationId) switch
        {
            TextBlock block => block.Text,
            TextBox box => box.Text,
            _ => null,
        };

    /// <summary>Whether a control whose automation id is <paramref name="automationId"/> shows; false while none is in the window.</summary>
    private static bool IsShown(ShellDriver app, string automationId) =>
        app.Window.GetVisualDescendants().OfType<Control>().Any(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == automationId);

    private static List<string> GraphSubjects(RepositoryViewModel repository) =>
        [.. Enumerable.Range(0, repository.Graph.Count).Select(index => repository.Graph.CommitAt(index).Subject)];

    /// <summary>Presses a move of the report's tables: <c>Down</c>, <c>Up</c> or <c>Shift+Down</c>.</summary>
    private static void PressMove(ShellDriver app, string key)
    {
        switch (key)
        {
            case "Down":
                app.PressKey(Key.Down);
                break;
            case "Up":
                app.PressKey(Key.Up);
                break;
            case "Shift+Down":
                app.PressKey(Key.Down, RawInputModifiers.Shift);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(key), key, "No such move in part U.");
        }
    }

    /// <summary>
    /// Waits for one name change more than the <paramref name="before"/> that <paramref name="names"/>
    /// had before the input, lets the dispatcher settle, and asserts it alone: its new value
    /// <paramref name="expected"/>, its old value the name the change before raised
    /// (<paramref name="lastName"/>; null for the first). Returns the new name.
    /// </summary>
    private static async Task<string?> AssertOneNameChangeAsync(ShellDriver app, NameChangeRecorder names, int before, string expected, string? lastName, string what)
    {
        await app.WaitForAsync(() => names.Count > before, $"a name change of the graph's row after {what}");
        await app.WaitForIdleAsync();
        Assert.True(names.Count == before + 1, $"After {what} the row raised {names.Count - before} name changes: {string.Join(" | ", names.Changes.Skip(before))}.");
        Assert.Equal((lastName, expected), names.Last);
        return names.Last.New;
    }

    /// <summary>The graph's peer: <c>GraphRows</c>, a list named <c>Commit graph</c>, that may select several rows and needs none.</summary>
    private static void AssertGraphPeer(AutomationPeer graph)
    {
        Assert.Equal((U.GraphRowsId, AutomationControlType.List, U.GraphName), (graph.GetAutomationId(), graph.GetAutomationControlType(), graph.GetName()));
        var selection = graph.GetProvider<ISelectionProvider>()!;
        Assert.True(selection.CanSelectMultiple);
        Assert.False(selection.IsSelectionRequired);
    }

    /// <summary>
    /// The exposed row after a step: the graph's one child and selection is <paramref name="row"/>,
    /// the same peer for the graph's life; its id, class, type, live setting, selection, name, help
    /// text and bounds (the display row's, 26 high, inside the graph); the number of commits
    /// selected; the keyboard focus on <paramref name="focus"/> and on the row while that is the
    /// graph; no control carrying the row's id.
    /// </summary>
    private static void AssertExposedRow(ShellDriver app, AutomationPeer graph, AutomationPeer row, U.Row expected, string hint, int selected, string focus)
    {
        Assert.Same(row, Assert.Single(graph.GetChildren()));
        Assert.Same(row, Assert.Single(graph.GetProvider<ISelectionProvider>()!.GetSelection()));
        Assert.Equal(
            (U.SelectedRowId, U.SelectedRowClass, AutomationControlType.ListItem, AutomationLiveSetting.Polite),
            (row.GetAutomationId(), row.GetClassName(), row.GetAutomationControlType(), row.GetLiveSetting()));
        var item = row.GetProvider<ISelectionItemProvider>()!;
        Assert.True(item.IsSelected);
        Assert.Same(graph, item.SelectionContainer);
        Assert.Equal((expected.Name, hint), (row.GetName(), row.GetHelpText()));

        var control = app.GraphControl();
        var bounds = row.GetBoundingRectangle();
        var drawn = expected.DisplayRow == 0 ? control.WorkingRowBounds : control.RowBounds(expected.DisplayRow - (control.ShowsWorkingRow ? 1 : 0));
        var centre = control.TranslatePoint(drawn.Center, app.Window)!.Value;
        var graphBounds = app.BoundsOf(control);
        Assert.Equal(U.RowHeight, bounds.Height, 0.5);
        Assert.Equal(centre.Y, bounds.Center.Y, 0.5);
        Assert.True(graphBounds.Inflate(0.5).Contains(bounds), $"The row's bounds {bounds} lie outside the graph's {graphBounds}.");
        Assert.False(row.IsOffscreen());

        var repository = Repository(app);
        Assert.Equal(selected, repository.IsWorkingRowSelected ? 0 : repository.SelectedCommits.Count);
        Assert.Equal(focus, app.FocusedAutomationId());
        Assert.Equal(focus == U.GraphRowsId, row.HasKeyboardFocus());
        Assert.DoesNotContain(app.Window.GetVisualDescendants().OfType<Control>(), each => AutomationProperties.GetAutomationId(each) == U.SelectedRowId);
    }

    /// <summary>
    /// A control of 1.12's table as a client reads it through its own peer: its name, its tooltip
    /// (the app's constant in this platform's words), its help text as the table says it is given,
    /// and its key.
    /// </summary>
    private static void AssertNamed(ShellDriver app, U.Named expected)
    {
        var control = app.FindByAutomationId(expected.Id);
        Assert.True(control.IsEffectivelyVisible, $"{expected.Id} does not show.");
        var (tip, key) = CurrentTipAndKey(expected);
        AssertRead(control, expected.Id, expected.Name, tip, expected.HelpText, key);
    }

    /// <summary>A row's button or a branch row's toggle (U3's table), found by its id within its row, read as <see cref="AssertNamed(ShellDriver, U.Named)"/> reads.</summary>
    private static void AssertRowNamed(ShellDriver app, U.RowNamed expected)
    {
        var button = RowControl(app, expected);
        AssertRead(button, $"{expected.Row}'s {expected.Id}", expected.Name, expected.ToolTip, U.HelpTextSource.PeerFallback, null);
    }

    private static void AssertRead(Control control, string what, string name, string? tip, U.HelpTextSource helpText, string? key)
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(control);
        Assert.True(name == peer.GetName(), $"{what} is named \"{peer.GetName()}\", not \"{name}\".");
        Assert.True(tip == ToolTip.GetTip(control) as string, $"{what}'s tooltip is \"{ToolTip.GetTip(control)}\", not \"{tip}\".");
        Assert.True(key == peer.GetAcceleratorKey(), $"{what}'s key is \"{peer.GetAcceleratorKey()}\", not \"{key}\".");
        var own = AutomationProperties.GetHelpText(control);
        switch (helpText)
        {
            case U.HelpTextSource.ToolTip:
                Assert.True((tip, tip) == (own, peer.GetHelpText()), $"{what}'s help text is \"{own}\" set and \"{peer.GetHelpText()}\" read, not its tooltip \"{tip}\".");
                break;
            case U.HelpTextSource.PeerFallback:
                Assert.True(own is null, $"{what} sets the help text \"{own}\" of its own.");
                Assert.True(tip == peer.GetHelpText(), $"{what}'s peer reads the help text \"{peer.GetHelpText()}\", not its tooltip \"{tip}\".");
                break;
            default:
                Assert.True(own is null && string.IsNullOrEmpty(peer.GetHelpText()), $"{what} has the help text \"{own}\" set and \"{peer.GetHelpText()}\" read; none is expected.");
                break;
        }
    }

    /// <summary>
    /// The tooltip and key of a control of 1.12's table as the app gives them on this platform (the
    /// report's conventions: the platform's words, the same constants the expectations quote in
    /// Windows' words).
    /// </summary>
    private static (string? ToolTip, string? Key) CurrentTipAndKey(U.Named named) => named.Id switch
    {
        "ResolverPreviousConflict" => (ShellToolTips.PreviousConflict, ShellToolTips.PreviousConflictKey),
        "ResolverNextConflict" => (ShellToolTips.NextConflict, ShellToolTips.NextConflictKey),
        "ResolverPreviousFile" => (ShellToolTips.PreviousFile, ShellToolTips.PreviousFileKey),
        "ResolverNextFile" => (ShellToolTips.NextFile, ShellToolTips.NextFileKey),
        "ResolverCloseButton" => (ShellToolTips.CloseResolver, ShellToolTips.CloseResolverKey),
        "SearchPrevious" => (SearchViewModel.PreviousToolTip, SearchViewModel.PreviousKey),
        "SearchNext" => (SearchViewModel.NextToolTip, SearchViewModel.NextKey),
        "SearchClose" => (SearchViewModel.CloseToolTip, null),
        "ActivityLogToggle" => (ShellToolTips.HideActivityLog, KeyTexts.Current.Of(AppCommand.ToggleActivityLog)),
        _ => (named.ToolTip, named.Key),
    };

    /// <summary>A file row's own button (<c>StageFileRow</c>), the row realised in its list.</summary>
    private static Button RowOwnButton(ShellDriver app, string list, string path) =>
        ShellDriver.RowButton(app.StageRow(list, path) ?? throw new InvalidOperationException($"{path} has no row in {list}."), "StageFileRow");

    /// <summary>The button of U3's table in its row: a file row's action in its list, or a branch row's toggle by the ref's full name.</summary>
    private static Button RowControl(ShellDriver app, U.RowNamed expected)
    {
        if (expected.List != U.RefListId)
        {
            return ShellDriver.RowButton(app.StageRow(expected.List, expected.Row) ?? throw new InvalidOperationException($"{expected.Row} has no row in {expected.List}."), expected.Id);
        }

        var list = app.Find<ItemsControl>(U.RefListId);
        return list.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(button => button.IsEffectivelyVisible && AutomationProperties.GetAutomationId(button) == expected.Id
                && button.DataContext is RefItemRow { ToolTip: var full } && full == expected.Row)
            ?? throw new InvalidOperationException($"No {expected.Id} shows on the row of {expected.Row}.");
    }

    /// <summary>The left panel's branch row (<c>RefItem</c>) at <paramref name="path"/>, laid out; found without scrolling the list.</summary>
    private static Button RefItemButton(ShellDriver app, IReadOnlyList<string> path)
    {
        var model = app.RefRowModel([.. path]);
        return app.Find<ItemsControl>(U.RefListId).GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(button => ReferenceEquals(button.DataContext, model) && AutomationProperties.GetAutomationId(button) == U.RefItemId && button.IsEffectivelyVisible)
            ?? throw new InvalidOperationException($"The left panel's row {string.Join(" › ", path)} is not laid out.");
    }

    /// <summary>
    /// U2's step 7: README.md's (selected) and u-untracked.txt's (hovered) Stage and Discard are each
    /// in UI Automation under <c>UnstagedFileList</c>, each pair named apart, and no other row's.
    /// </summary>
    private static void AssertTwoRowsButtonsInUiAutomation(ShellDriver app)
    {
        var list = ControlAutomationPeer.CreatePeerForElement(app.FindByAutomationId(U.UnstagedListId));
        var buttons = Descendants(list)
            .Where(peer => peer.GetAutomationId() is "RowStageButton" or "RowDiscardButton" && peer is ControlAutomationPeer { Owner.IsEffectivelyVisible: true })
            .Select(peer => (peer.GetAutomationId(), peer.GetName()))
            .OrderBy(pair => pair.Item2, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(
            [("RowDiscardButton", U.DiscardReadme), ("RowDiscardButton", U.DiscardUntracked), ("RowStageButton", U.StageReadme), ("RowStageButton", U.StageUntracked)],
            buttons);
    }

    private static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
    {
        foreach (var child in peer.GetChildren())
        {
            yield return child;
            foreach (var below in Descendants(child))
            {
                yield return below;
            }
        }
    }
}
