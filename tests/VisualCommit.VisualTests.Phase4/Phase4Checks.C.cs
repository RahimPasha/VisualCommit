// Part C of phase 4's scripted walk-through (docs/test-reports/phase-4.md, "Part C: conflicts,
// Continue, Skip and Abort, the resolver"). The class Phase4ChecksC is part C's; this file declares
// it and holds what checks C10 to C13 share. Their expected results are in
// Phase4Expectations.C.cs (Phase4Expectations.PartC); every check uses Phase4Checks.Support.cs (read
// its header and Phase4Checks.cs for the conventions: one test method per app start, pictures
// through Shot with the report's names, no fixed waits).
//
// Checks C10 to C13: Phase4Checks.C.Merge.cs (C10), Phase4Checks.C.Rebase.cs (C11),
// Phase4Checks.C.Picks.cs (C12), Phase4Checks.C.SkipAbort.cs (C13). Checks C14 to C17 have files of
// their own in this class.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC;

namespace VisualCommit.VisualTests.Phase4;

/// <summary>
/// The checks of part C: conflicts, Continue, Skip and Abort, and the conflict resolver
/// (docs/test-reports/phase-4.md, checks C10 to C17).
/// </summary>
public sealed partial class Phase4ChecksC : Phase4Checks
{
    /// <summary>The graph area's left edge and width while the graph shows, and the resolver's width (11.2), at 1100×700 with the session's default widths.</summary>
    private const double GraphAreaLeft = WindowLayout.LeftPanelWidth;

    private const double GraphAreaWidth = 1100 - WindowLayout.LeftPanelWidth - WindowLayout.RightPanelWidth;

    private const double ResolverWidth = 1100 - WindowLayout.LeftPanelWidth;

    /// <summary>VcDiffHunkBrush in the dark theme: every block bar's background (the report's "The tints").</summary>
    private static readonly Color DarkBarColour = Color.Parse("#1C2130");

    // ---- The banner ----

    /// <summary>
    /// The in-progress banner as C10 to C13 state it (this part's conventions): its text, its
    /// buttons (Continue, Skip: null for none; Abort), where they stand (on a row of their own
    /// under the text, the banner taking more than one line, while the graph shows; beside the
    /// text while the resolver is open, one line or not unstated), its width (the graph area's
    /// 440, or the resolver's 840) and place (above the column headers while the graph shows), its
    /// background and bottom line by pixel, the warning icon, and no text cut anywhere.
    /// </summary>
    private static Rect AssertStopBanner(
        ShellDriver app,
        Screenshot shot,
        string text,
        string abort,
        BannerButton continueButton,
        BannerButton? skipButton,
        bool overResolver = false)
    {
        var banner = AssertBanner(
            app,
            text,
            abort,
            continueButton,
            skipButton,
            oneLine: overResolver ? null : false,
            placement: overResolver ? BannerButtonPlacement.BesideText : BannerButtonPlacement.OwnRow);
        Assert.Equal(GraphAreaLeft, banner.X, 1.0);
        Assert.Equal(overResolver ? ResolverWidth : GraphAreaWidth, banner.Width, 1.0);
        Assert.Equal(WindowLayout.GraphAreaTop, banner.Top, 1.0);
        if (overResolver)
        {
            Assert.Equal(banner.Bottom, app.BoundsOf(app.ResolverView).Top, 1.0);
        }
        else
        {
            Assert.Equal(banner.Bottom + WindowLayout.ColumnHeaderHeight, app.GraphRowsTop, 1.0);
        }

        var palette = PaletteOf(app);
        var right = banner.Right - 3;
        AssertColour(palette.BannerBackground, shot, new Point(right, banner.Top + 2), "the banner's background");
        AssertColour(palette.Warning, shot, new Point(right, banner.Bottom - 0.5), "the banner's bottom line");
        var icon = app.BoundsOf(app.Find<Control>("BannerIcon"));
        var found = false;
        for (var x = (int)icon.Left; x < (int)icon.Right && !found; x++)
        {
            for (var y = (int)icon.Top; y < (int)icon.Bottom && !found; y++)
            {
                found = shot.PixelAt(x, y) == palette.Warning;
            }
        }

        Assert.True(found, "The banner's icon has no pixel in the warning colour.");
        AssertNoClippedText(app);
        return banner;
    }

    /// <summary>The tooltip that the banner's icon and text share (10.3), without picturing it.</summary>
    private static string? BannerTip(ShellDriver app) => ToolTip.GetTip(app.Find<Border>("BannerInfo")) as string;

    /// <summary>
    /// The open tooltip with <paramref name="text"/> lies inside the window ("drawn inside the
    /// window") and below the pointer at <paramref name="pointer"/> (this part's conventions: a
    /// tooltip opens below the pointer).
    /// </summary>
    private static void AssertTipInsideWindow(ShellDriver app, string text, Point pointer)
    {
        var tips = app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible && tip.Bounds.Width > 0).ToList();
        var tip = Assert.Single(tips, candidate => TipTextOf(candidate) == text);
        var bounds = app.BoundsOf(tip);
        var size = app.Window.ClientSize;
        Assert.True(
            bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= size.Width && bounds.Bottom <= size.Height,
            $"The tooltip at {bounds} is not inside the {size.Width}x{size.Height} window.");
        Assert.True(bounds.Top >= pointer.Y, $"The tooltip at {bounds} is not below the pointer at {pointer}.");
    }

    private static string TipTextOf(ToolTip tip) => tip.Content switch
    {
        string text => text,
        Visual visual => string.Join('\n', visual.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text)),
        _ => string.Empty,
    };

    // ---- The graph's selection ----

    /// <summary>Nothing is selected in the graph: no commit, not the working-changes row.</summary>
    private static void AssertNothingSelected(ShellDriver app)
    {
        var graph = Graph(app);
        Assert.Equal(-1, graph.SelectedIndex);
        Assert.False(graph.IsWorkingRowSelected, "The working-changes row is selected.");
        Assert.True(graph.Selection.IsEmpty, "Commits are selected: " + string.Join(", ", graph.Selection.Shas));
    }

    /// <summary>
    /// Exactly the commits <paramref name="shortShas"/> are selected (by SHA, 4h), not the
    /// working-changes row, each drawn on the selection background in <paramref name="shot"/>.
    /// </summary>
    private static void AssertSelectedCommits(ShellDriver app, Screenshot shot, params string[] shortShas)
    {
        var graph = Graph(app);
        var data = graph.Data!;
        Assert.False(graph.IsWorkingRowSelected, "The working-changes row is selected.");
        var selected = graph.Selection.Shas.Select(sha => sha[..7]).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(shortShas.Order(StringComparer.Ordinal).ToList(), selected);
        var origin = app.BoundsOf(graph).TopLeft;

        // An open menu lies over the Message column (C13 step 9's drop menu): then the row's
        // left end, where no label starts, shows the selection.
        var x = app.IsMenuOpen ? 2 : graph.Columns.Message.Right - 4;
        foreach (var sha in graph.Selection.Shas)
        {
            var row = data.IndexOf(sha);
            Assert.True(row >= 0, $"The selected commit {sha} is not in the graph.");
            var point = new Point(origin.X + x, origin.Y + graph.RowBounds(row).Top + 4);
            AssertColour(PaletteOf(app).Selection, shot, point, $"the background of the selected row {row}");
        }
    }

    /// <summary>The working-changes row is selected (and no commit), on the selection background in <paramref name="shot"/>.</summary>
    private static void AssertWorkingRowSelected(ShellDriver app, Screenshot? shot)
    {
        var graph = Graph(app);
        Assert.True(graph.IsWorkingRowSelected, "The working-changes row is not selected.");
        Assert.Equal(-1, graph.SelectedIndex);
        if (shot is not null)
        {
            var origin = app.BoundsOf(graph).TopLeft;
            var point = new Point(origin.X + graph.Columns.Message.Right - 4, origin.Y + graph.WorkingRowBounds.Top + 4);
            AssertColour(PaletteOf(app).Selection, shot, point, "the selected working-changes row's background");
        }
    }

    /// <summary>No row of the left panel is selected (D114: a right-click there selects nothing).</summary>
    private static void AssertNoLeftPanelSelected(ShellDriver app)
    {
        app.Settle();
        var selected = app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefItemRow>().Where(row => row.IsSelected).Select(row => row.Text).ToList();
        Assert.True(selected.Count == 0, "Selected in the left panel: " + string.Join(", ", selected));
    }

    /// <summary>
    /// The left panel's Local branches as the report's graphs of "conflicts" state them: 5, the
    /// closed folder <c>feature</c>, then <c>main</c>, which is in the accent colour when HEAD is on
    /// it (<paramref name="mainIsHead"/>: GC0); with HEAD detached (GC-R1) no local branch is.
    /// </summary>
    private static async Task AssertLocalBranchesAsync(ShellDriver app, bool mainIsHead)
    {
        app.Settle();
        var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
        var start = rows.FindIndex(row => row is RefSectionRow && row.Text == "Local branches");
        Assert.True(start >= 0, "The left panel has no section Local branches.");
        Assert.Equal(5, ((RefSectionRow)rows[start]).Count);
        var items = rows.Skip(start + 1).TakeWhile(row => row is RefItemRow).Cast<RefItemRow>().ToList();
        Assert.Equal(["feature", "main"], items.Select(row => row.Text).ToList());
        Assert.True(items[0].IsContainer && !items[0].IsOpen, "The folder feature is open.");
        Assert.DoesNotContain(items, row => row.IsHead && row.Text != "main");
        Assert.Equal(mainIsHead, items[1].IsHead);
        var button = await app.RefRowAsync("Local branches", "main");
        var name = button.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "main" && text.IsEffectivelyVisible);
        var palette = PaletteOf(app);
        Assert.Equal(mainIsHead ? palette.Accent : palette.TextPrimary, (name.Foreground as ISolidColorBrush)?.Color);
    }

    /// <summary>Clicks commit row <paramref name="row"/> in its Message column and waits for its details.</summary>
    private static async Task ClickCommitRowAsync(ShellDriver app, int row, string subject)
    {
        app.Click(app.GraphRowPoint(row));
        await WaitForDetailsOfAsync(app, subject);
    }

    // ---- The details panel ----

    /// <summary>Waits until the details panel shows the commit <paramref name="subject"/>.</summary>
    private static Task WaitForDetailsOfAsync(ShellDriver app, string subject) =>
        app.WaitForAsync(
            () => app.FindByAutomationId("RightPanel").GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.IsEffectivelyVisible && text.Text == subject && AutomationProperties.GetAutomationId(text) == "DetailsSubject"),
            $"the details of \"{subject}\"");

    /// <summary>
    /// The details of the selected commit (phase 1's panel with phase 3's Commit date row): the
    /// subject, the body (null: none shown), Author Test Author, the Date, the Commit date row
    /// (null: none), the parents' short SHAs in order and the changed files as "letter name".
    /// </summary>
    private static async Task AssertCommitDetailsAsync(ShellDriver app, string subject, string? body, string date, string? commitDate, string[] parents, params string[] files)
    {
        await WaitForDetailsOfAsync(app, subject);
        var bodyBlock = (SelectableTextBlock)app.FindByAutomationId("DetailsBody");
        Assert.Equal(body is not null, bodyBlock.IsEffectivelyVisible);
        if (body is not null)
        {
            Assert.Equal(body, bodyBlock.Text);
        }

        Assert.Equal("Test Author <author@example.com>", ((SelectableTextBlock)app.FindByAutomationId("DetailsAuthor")).Text);
        Assert.Equal(date, ((TextBlock)app.FindByAutomationId("DetailsDate")).Text);
        var commitDateBlock = (TextBlock)app.FindByAutomationId("DetailsCommitDate");
        Assert.Equal(commitDate is not null, commitDateBlock.IsEffectivelyVisible);
        if (commitDate is not null)
        {
            Assert.Equal(commitDate, commitDateBlock.Text);
        }

        var links = app.Window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == "ParentLink")
            .OrderBy(link => app.BoundsOf(link).X)
            .Select(link => AutomationProperties.GetName(link))
            .ToList();
        Assert.Equal(parents, links);
        Assert.Equal($"Changed files ({files.Length.ToString(CultureInfo.InvariantCulture)})", ((TextBlock)app.FindByAutomationId("ChangedFilesTitle")).Text);
        await app.WaitForAsync(
            () => FileRowsOf(app, "FileList").SequenceEqual(files),
            $"the details' files {string.Join(", ", files)}");
    }

    // ---- The stage panel ----

    /// <summary>Waits until the stage panel shows with these titles of its two lists.</summary>
    private static Task WaitForStageTitlesAsync(ShellDriver app, string unstagedTitle, string stagedTitle) =>
        app.WaitForAsync(
            () => app.FindByAutomationId("StagePanel").IsEffectivelyVisible
                && app.Find<TextBlock>("UnstagedTitle").Text == unstagedTitle
                && app.Find<TextBlock>("StagedTitle").Text == stagedTitle,
            $"the stage panel with \"{unstagedTitle}\" and \"{stagedTitle}\"");

    /// <summary>A file list's rows as "letter name folder" (the folder left out when the file has none): the stage panel's two lists, the details' list.</summary>
    private static List<string> FileRowsOf(ShellDriver app, string list)
    {
        var rows = (IEnumerable<ChangedFileRow>)(app.Find<ItemsControl>(list).ItemsSource ?? Array.Empty<ChangedFileRow>());
        return [.. rows.Where(row => row.IsFile).Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => !string.IsNullOrEmpty(part))))];
    }

    /// <summary>
    /// The stage panel as C10 to C13 state it: its two titles, the rows of each list, the
    /// "No unstaged changes" and "No staged changes" texts shown when a list is empty, the summary
    /// and the description, the commit button's text and whether it is enabled, with its tooltip
    /// when the check states one (a disabled button's reason always).
    /// </summary>
    private static async Task AssertStagePanelAsync(
        ShellDriver app,
        string unstagedTitle,
        string[] unstaged,
        string stagedTitle,
        string[] staged,
        string summary,
        string commitText,
        bool commitEnabled,
        string? commitTip = null)
    {
        await WaitForStageTitlesAsync(app, unstagedTitle, stagedTitle);
        Assert.Equal(unstaged, FileRowsOf(app, "UnstagedFileList"));
        Assert.Equal(staged, FileRowsOf(app, "StagedFileList"));
        var noUnstaged = app.Find<TextBlock>("NoUnstaged");
        Assert.Equal(unstaged.Length == 0, noUnstaged.IsEffectivelyVisible);
        Assert.Equal(NoUnstagedChanges, noUnstaged.Text);
        var noStaged = app.Find<TextBlock>("NoStaged");
        Assert.Equal(staged.Length == 0, noStaged.IsEffectivelyVisible);
        Assert.Equal(NoStagedChanges, noStaged.Text);
        Assert.Equal(summary, BoxText(app, "CommitSummary"));
        Assert.Equal(string.Empty, BoxText(app, "CommitDescription"));
        var commit = app.Find<Button>("CommitButton");
        Assert.Equal(commitText, commit.Content);
        Assert.True(commitEnabled == commit.IsEffectivelyEnabled, $"The commit button \"{commitText}\" is {(commit.IsEffectivelyEnabled ? "enabled" : "disabled")}.");
        if (!commitEnabled || commitTip is not null)
        {
            AssertTip(commit, commitTip);
        }
    }

    /// <summary>The unstaged conflicted notes.txt's row: U in the danger colour, no folder, the conflict tooltip; returns the row's button.</summary>
    private static async Task<Control> AssertConflictedNotesRowAsync(ShellDriver app)
    {
        var row = await app.FileRowAsync("UnstagedFileList", ResolverFile);
        AssertTip(row, ConflictRowTip);
        var letter = row.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "U" && text.IsEffectivelyVisible);
        Assert.Equal(PaletteOf(app).Danger, (letter.Foreground as ISolidColorBrush)?.Color);
        return row;
    }

    /// <summary>What a text box holds (empty for none).</summary>
    private static string BoxText(ShellDriver app, string automationId) => ((TextBox)app.FindByAutomationId(automationId)).Text ?? string.Empty;

    /// <summary>Amend's state (5.3): disabled during a rebase or a list, with its reason.</summary>
    private static void AssertAmendDisabled(ShellDriver app, string reason)
    {
        var amend = (CheckBox)app.FindByAutomationId("AmendCheckBox");
        Assert.False(amend.IsEffectivelyEnabled, "Amend is enabled.");
        AssertTip(amend, reason);
    }

    /// <summary>Clicks the working-changes row (display row 0) and waits for the stage panel.</summary>
    private static async Task ClickWorkingRowAsync(ShellDriver app)
    {
        app.Click(app.WorkingRowPoint());
        await app.WaitForAsync(() => app.FindByAutomationId("StagePanel").IsEffectivelyVisible, "the stage panel");
    }

    // ---- The resolver ----

    /// <summary>
    /// Where the resolver stands and what stays around it (11.2): over the graph and the right
    /// panel's columns, x 260 to 1100; the left panel and LeftSplitter stay; the right panel's
    /// views and RightSplitter are hidden; the header and the toolbar 36 high; the panes half the
    /// width each with a 1-px line between, under their 26-high title strips; the halves split the
    /// height equally with a 1-px line between; the result's title strip 26 high.
    /// </summary>
    private static void AssertResolverLayout(ShellDriver app)
    {
        var view = app.ResolverView;
        var bounds = app.BoundsOf(view);
        Assert.Equal(GraphAreaLeft, bounds.X, 1.0);
        Assert.Equal(ResolverWidth, bounds.Width, 1.0);
        Assert.Equal(app.Window.ClientSize.Width, bounds.Right, 1.0);
        var tab = app.Session.Shell.ActiveTab;
        Assert.False(tab.ShowsRightPanel, "The right panel shows beside the resolver.");
        Assert.False(app.FindByAutomationId("RightPanel").IsEffectivelyVisible, "The details panel shows beside the resolver.");
        Assert.False(app.FindByAutomationId("StagePanel").IsEffectivelyVisible, "The stage panel shows beside the resolver.");
        Assert.False(app.Find<GridSplitter>("RightSplitter").IsEffectivelyVisible, "RightSplitter shows beside the resolver.");
        Assert.True(app.Find<GridSplitter>("LeftSplitter").IsEffectivelyVisible, "LeftSplitter is hidden.");
        var left = app.BoundsOf(app.FindByAutomationId("LeftPanel"));
        Assert.True(app.FindByAutomationId("LeftPanel").IsEffectivelyVisible, "The left panel is hidden.");
        Assert.Equal(0, left.X, 1.0);
        Assert.Equal(WindowLayout.LeftPanelWidth, left.Width, 1.0);
        Assert.False(app.Session.Shell.ActiveTab.Repository!.ShowsGraph, "The graph shows beside the resolver.");

        var header = app.BoundsOf(app.Find<Border>("ResolverHeader"));
        Assert.Equal(36, header.Height, 1.0);
        Assert.Equal(36, app.BoundsOf(app.Find<Border>("ResolverToolbar")).Height, 1.0);

        // The header's file arrows and close button (at its right), the toolbar's conflict arrows.
        foreach (var id in new[] { "ResolverPreviousFile", "ResolverNextFile", "ResolverCloseButton", "ResolverPreviousConflict", "ResolverNextConflict" })
        {
            Assert.True(app.Find<Button>(id).IsEffectivelyVisible, $"{id} is hidden.");
        }

        var close = app.BoundsOf(app.Find<Button>("ResolverCloseButton"));
        Assert.True(header.Right - close.Right <= 8 && close.Left > app.BoundsOf(app.Find<Button>("ResolverNextFile")).Right, $"The close button at {close} is not at the header's right ({header}).");

        // The text is the code font at 12 in the panes and the result (11.2).
        var oursEditor = (AvaloniaEdit.TextEditor)app.ResolverPanes.PaneEditor(ConflictPaneSide.Ours);
        var theirsEditor = (AvaloniaEdit.TextEditor)app.ResolverPanes.PaneEditor(ConflictPaneSide.Theirs);
        Assert.Equal((12.0, 12.0, 12.0), (oursEditor.FontSize, theirsEditor.FontSize, app.ResultEditor.FontSize));
        Assert.Equal(oursEditor.FontFamily, app.ResultEditor.FontFamily);
        Assert.Equal(oursEditor.FontFamily, theirsEditor.FontFamily);

        // The result's caret does not blink in a scripted check (D151).
        Assert.False(ConflictResultEditor.CaretBlinks, "The result's caret blinks.");
        var ours = app.BoundsOf(app.FindByAutomationId("ConflictOurs"));
        var theirs = app.BoundsOf(app.FindByAutomationId("ConflictTheirs"));
        Assert.Equal(ours.Width, theirs.Width, 1.0);
        Assert.Equal(1, theirs.Left - ours.Right, 1.0);
        Assert.Equal(bounds.Left, ours.Left, 1.0);
        Assert.Equal(bounds.Right, theirs.Right, 1.0);
        var oursTitle = app.BoundsOf(app.Find<Border>("OursTitleStrip"));
        var theirsTitle = app.BoundsOf(app.Find<Border>("TheirsTitleStrip"));
        var resultTitle = app.BoundsOf(app.Find<Border>("ResultTitleStrip"));
        Assert.Equal(26, oursTitle.Height, 1.0);
        Assert.Equal(26, theirsTitle.Height, 1.0);
        Assert.Equal(26, resultTitle.Height, 1.0);
        Assert.Equal(oursTitle.Bottom, ours.Top, 1.0);
        var result = app.BoundsOf(app.ResultEditor);
        Assert.Equal(resultTitle.Bottom, result.Top, 1.0);
        var top = app.BoundsOf(app.Find<Grid>("ResolverText"));
        var topHalf = ours.Bottom - oursTitle.Top;
        var bottomHalf = result.Bottom - resultTitle.Top;
        Assert.True(Math.Abs(topHalf - bottomHalf) <= 1, string.Create(CultureInfo.InvariantCulture, $"The halves are {topHalf} and {bottomHalf} high."));
        Assert.Equal(1, resultTitle.Top - ours.Bottom, 1.0);
        Assert.Equal(top.Bottom, result.Bottom, 1.0);
    }

    /// <summary>
    /// The resolver's header, toolbar, titles, result and panes at one step of C10 to C13: the
    /// header <c>U</c> (in VcDangerBrush), notes.txt, no folder, Both modified, File 1 of 1; the
    /// toolbar's position, Take ours and Take theirs enabled, Merge tool… disabled with its reason,
    /// Mark resolved enabled in the primary style; the panes' titles, <c>Result</c> and the count;
    /// the result's text, caret, modified state and the tint of every line; every row of both
    /// panes with its tint and its bar's state; the keyboard focus in the result (where the step
    /// states it: <paramref name="resultFocus"/> null states nothing); and, on
    /// <paramref name="shot"/>, the tints of the rows and lines in view, by pixel.
    /// </summary>
    private static void AssertResolverState(ShellDriver app, Screenshot shot, ResolverTitles titles, ResolverState expected, bool? resultFocus = true)
    {
        var r = app.Resolver();
        Assert.Equal("U", r.Status);
        Assert.Equal(PaletteOf(app).Danger, (app.Find<TextBlock>("ResolverFileStatus").Foreground as ISolidColorBrush)?.Color);
        Assert.Equal(ResolverFile, r.FileName);
        Assert.True(string.IsNullOrEmpty(r.Folder), $"ResolverFolder reads \"{r.Folder}\" for a file at the working tree's top.");
        Assert.Equal(BothModified, r.Kind);
        Assert.Equal(FileOneOfOne, r.FilePosition);
        Assert.Equal(expected.ConflictPosition, r.ConflictPosition);
        Assert.Equal(new ResolverButton(TakeOurs, true, null), r.TakeOurs! with { ToolTip = null });
        Assert.Equal(new ResolverButton(TakeTheirs, true, null), r.TakeTheirs! with { ToolTip = null });
        Assert.Equal(new ResolverButton(MergeToolButton, false, SetMergeToolReason), r.MergeTool);
        Assert.True(ToolTip.GetShowOnDisabled(app.Find<Button>("ResolverMergeTool")), "Merge tool… does not show its reason while disabled.");
        Assert.Equal(new ResolverButton(MarkResolved, true, null), r.MarkResolved! with { ToolTip = null });
        Assert.Same(app.Window.FindResource("VcPrimaryButton"), app.Find<Button>("ResolverMarkResolved").Theme);
        Assert.Equal(titles.Ours, r.OursTitle);
        Assert.Equal(titles.Theirs, r.TheirsTitle);
        Assert.Equal(ResultTitle, r.ResultTitle);
        Assert.Equal(expected.ResultCount, r.ResultCount);
        Assert.Equal(expected.Result, r.Result);
        if (expected.Caret is { } caret)
        {
            Assert.True(
                r.Caret is { } at && at.Line == caret.Line && at.Column == caret.Column,
                $"The caret is at {r.Caret}, expected line {caret.Line}, column {caret.Column}.");
        }

        Assert.True(expected.IsModified == r.IsModified, $"The result is {(r.IsModified ? string.Empty : "not ")}modified.");
        if (resultFocus is { } focus)
        {
            Assert.True(focus == r.ResultHasFocus, $"The result does {(r.ResultHasFocus ? string.Empty : "not ")}have the keyboard focus.");
        }

        Assert.False(r.IsParsePending, "The resolver has not parsed the result yet.");

        // The document holds one more line than the file: the empty line after its last line feed
        // (design 14.4, WP6), which has no tint.
        var tints = r.ResultTints.Select(TintOf).ToList();
        Assert.True(
            tints.Count == expected.Tints.Count + 1 && tints.Take(expected.Tints.Count).SequenceEqual(expected.Tints) && tints[^1] == LineTint.None,
            "The result's tints are not the expected ones.\nExpected:\n  " + string.Join(", ", expected.Tints) + ", None\nActual:\n  " + string.Join(", ", tints));
        AssertPaneRows("ours", expected.OursRows, r.OursRows);
        AssertPaneRows("theirs", expected.TheirsRows, r.TheirsRows);
        AssertTintPixels(app, shot);
    }

    private static LineTint TintOf(ConflictTint tint) => tint switch
    {
        ConflictTint.None => LineTint.None,
        ConflictTint.Ours => LineTint.Ours,
        ConflictTint.Theirs => LineTint.Theirs,
        ConflictTint.Marker => LineTint.Marker,
        ConflictTint.Used => LineTint.Used,
        ConflictTint.Bar => LineTint.Bar,
        _ => throw new Xunit.Sdk.XunitException($"No part of C10 to C13 expects the tint {tint}."),
    };

    private static PaneRow PaneRowOf(ConflictPaneRow row) =>
        new(row.Text, TintOf(row.Tint), row.IsBar, row.Number, row.Block, row.BarText);

    private static void AssertPaneRows(string side, IReadOnlyList<PaneRow> expected, IReadOnlyList<ConflictPaneRow> rows)
    {
        var actual = rows.Select(PaneRowOf).ToList();
        Assert.True(
            actual.SequenceEqual(expected),
            $"The {side} pane's rows are not the expected ones.\nExpected:\n  " + string.Join("\n  ", expected) + "\nActual:\n  " + string.Join("\n  ", actual));
    }

    /// <summary>
    /// The tints by pixel (7.3, dark), for every pane row and result line in view: a tinted row
    /// or line has its tint's colour three quarters of the way across, a bar the hunk colour, and
    /// a row or line without a tint none of the tints' colours there.
    /// </summary>
    private static void AssertTintPixels(ShellDriver app, Screenshot shot)
    {
        var palette = PaletteOf(app);
        var tintColours = new[] { palette.ConflictOurs, palette.ConflictTheirs, palette.ConflictMarker, palette.ConflictUsed };
        var panes = app.ResolverPanes;

        // The panes' rows line up, row for row (11.3).
        var count = panes.RowsOf(ConflictPaneSide.Ours).Count;
        Assert.Equal(count, panes.RowsOf(ConflictPaneSide.Theirs).Count);
        for (var index = 0; index < count; index++)
        {
            var ours = panes.RowBounds(ConflictPaneSide.Ours, index);
            var theirs = panes.RowBounds(ConflictPaneSide.Theirs, index);
            Assert.True((ours is null) == (theirs is null), $"Row {index} is in view in one pane only.");
            if (ours is { } left && theirs is { } right)
            {
                Assert.True(Math.Abs(left.Top - right.Top) <= 0.5 && Math.Abs(left.Height - right.Height) <= 0.5, $"Row {index} is at {left} in the ours pane and at {right} in the theirs pane.");
            }
        }

        foreach (var side in new[] { ConflictPaneSide.Ours, ConflictPaneSide.Theirs })
        {
            var editor = app.BoundsOf(panes.PaneEditor(side));
            var rows = panes.RowsOf(side);
            for (var index = 0; index < rows.Count; index++)
            {
                if (panes.RowBounds(side, index) is not { } local || panes.TranslatePoint(local.TopLeft, app.Window) is not { } origin)
                {
                    continue;
                }

                var point = new Point(origin.X + (local.Width * 0.75), origin.Y + (local.Height / 2));
                if (point.Y < editor.Top + 1 || point.Y > editor.Bottom - 1)
                {
                    continue;
                }

                AssertTintPixel(shot, point, TintOf(rows[index].Tint), palette, tintColours, $"{side} pane's row {index} (\"{rows[index].Text}\")");
            }
        }

        var result = app.ResultEditor;
        var area = app.BoundsOf(result);
        var lines = result.Document?.LineCount ?? 0;
        for (var line = 1; line <= lines; line++)
        {
            if (result.PointAt(line, 1) is not { } local || result.TranslatePoint(local, app.Window) is not { } at)
            {
                continue;
            }

            AssertTintPixel(shot, new Point(area.Left + (area.Width * 0.75), at.Y), TintOf(result.TintAt(line)), palette, tintColours, $"result's line {line}");
        }
    }

    private static void AssertTintPixel(Screenshot shot, Point point, LineTint tint, Palette palette, Color[] tintColours, string what)
    {
        var expected = tint switch
        {
            LineTint.Ours => palette.ConflictOurs,
            LineTint.Theirs => palette.ConflictTheirs,
            LineTint.Marker => palette.ConflictMarker,
            LineTint.Used => palette.ConflictUsed,
            LineTint.Bar => DarkBarColour,
            _ => (Color?)null,
        };
        var actual = shot.PixelAt(point);
        if (expected is { } colour)
        {
            Assert.True(actual == colour, $"The {what} at {point} is {actual}, expected {tint} {colour}.");
        }
        else
        {
            Assert.False(tintColours.Contains(actual), $"The {what} at {point} has a tint's colour {actual}; it has no tint.");
        }
    }

    /// <summary>Block <paramref name="block"/>'s bars are in view in both panes (11.5).</summary>
    private static void AssertBarsInView(ShellDriver app, int block)
    {
        app.Capture();
        var panes = app.ResolverPanes;
        foreach (var side in new[] { ConflictPaneSide.Ours, ConflictPaneSide.Theirs })
        {
            var row = panes.BarRowOf(block);
            var editor = app.BoundsOf(panes.PaneEditor(side));
            var bounds = panes.RowBounds(side, row) is { } local && panes.TranslatePoint(local.TopLeft, app.Window) is { } origin
                ? new Rect(origin, local.Size)
                : (Rect?)null;
            Assert.True(
                bounds is { } shown && shown.Top >= editor.Top - 0.5 && shown.Bottom <= editor.Bottom + 0.5,
                $"Block {block + 1}'s bar (row {row}) is not in view in the {side} pane: {bounds?.ToString() ?? "not laid out"}, the pane at {editor}.");
        }
    }

    /// <summary>The result's lines <paramref name="from"/> to <paramref name="to"/> are in view (11.5).</summary>
    private static void AssertResultLinesInView(ShellDriver app, int from, int to)
    {
        app.Capture();
        for (var line = from; line <= to; line++)
        {
            Assert.True(app.ResultEditor.PointAt(line, 1) is not null, $"The result's line {line} is not in view.");
        }
    }

    /// <summary>Nothing scrolled (C10 step 5): each pane's first row is line 1, and the result's first line is line 1.</summary>
    private static void AssertNothingScrolled(ShellDriver app)
    {
        Assert.Equal(0, app.ResolverPanes.ScrollOffset.Y, 0.5);
        Assert.Equal(0, app.ResultEditor.VerticalOffset, 0.5);
        Assert.NotNull(app.ResultEditor.PointAt(1, 1));
    }

    /// <summary>Clicks a bar's button and lets the resolver apply the choice (bars, tints and counts update at once, 11.5).</summary>
    private static async Task ChooseAsync(ShellDriver app, int block, ConflictPaneSide side, ConflictChoice choice)
    {
        app.ClickBarButton(block, side, choice);
        await app.WaitForResultParsedAsync();
    }

    /// <summary>Clicks Mark resolved and waits for its operation <c>Resolve notes.txt</c> and the resolver to close.</summary>
    private static async Task MarkResolvedAsync(ShellDriver app, TempRepo repo)
    {
        var mark = app.LogMark();
        app.ClickResolverButton("ResolverMarkResolved");
        await WaitForOperationAsync(app, repo, ResolveOperation, mark);
        await app.WaitForResolverClosedAsync();
        await app.WaitForIdleAsync();
    }

    /// <summary>After Mark resolved closed the resolver (11.1, 11.7): the graph and the right panel are back, and the keyboard focus is on the banner's Continue.</summary>
    private static async Task AssertResolverClosedAsync(ShellDriver app)
    {
        var tab = app.Session.Shell.ActiveTab;
        Assert.Null(app.ShownResolver);
        Assert.True(tab.Repository!.ShowsGraph, "The graph does not show.");
        Assert.True(tab.ShowsRightPanel, "The right panel does not show.");
        Assert.True(app.Find<GridSplitter>("RightSplitter").IsEffectivelyVisible, "RightSplitter is hidden.");
        await app.WaitForAsync(() => app.BannerContinueButton.IsFocused, "the keyboard focus on the banner's Continue");
    }

    // ---- Git, read back with plain git ----

    /// <summary>The lines git prints for these arguments, without empty ones.</summary>
    private static async Task<List<string>> LinesOfGitAsync(TempRepo repo, params string[] arguments) =>
        [.. (await repo.GitAsync(arguments)).StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];

    /// <summary><c>git status --porcelain</c>'s lines.</summary>
    private static Task<List<string>> PorcelainAsync(TempRepo repo) => LinesOfGitAsync(repo, "status", "--porcelain");

    /// <summary>The commit (or object) a revision names, in full.</summary>
    private static async Task<string> FullShaAsync(TempRepo repo, string revision) =>
        (await repo.GitAsync("rev-parse", "--verify", revision)).StandardOutput.Trim();

    /// <summary>A file of the git folder as git wrote it, or null when it does not exist.</summary>
    private static string? GitFile(TempRepo repo, string name)
    {
        var path = Path.Combine(repo.Path, ".git", name);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>Whether a file or folder exists in the git folder (<c>MERGE_HEAD</c>, <c>rebase-merge</c>, …).</summary>
    private static bool GitHas(TempRepo repo, string name)
    {
        var path = Path.Combine(repo.Path, ".git", name);
        return File.Exists(path) || Directory.Exists(path);
    }

    /// <summary>A working-tree file as git wrote it (its bytes as UTF-8, line ends kept).</summary>
    private static string WorkingFile(TempRepo repo, string path) => File.ReadAllText(Path.Combine(repo.Path, path));

    /// <summary>A file's content in a commit, as git stores it.</summary>
    private static async Task<string> FileAtAsync(TempRepo repo, string revision, string path) =>
        (await repo.GitAsync("cat-file", "blob", revision + ":" + path)).StandardOutput;

    /// <summary>The blobs of notes.txt's conflict stages 1 to 3 (<c>git ls-files -u</c>), abbreviated to 7 characters.</summary>
    private static async Task<List<string>> StagesOfNotesAsync(TempRepo repo) =>
        [.. (await LinesOfGitAsync(repo, "ls-files", "-u", "--", ResolverFile)).Select(line => line.Split(' ', '\t')[1][..7])];

    /// <summary>Every local branch's tip, abbreviated, as <c>git for-each-ref refs/heads</c> lists them.</summary>
    private static async Task<List<(string Ref, string ShortSha)>> BranchTipsAsync(TempRepo repo) =>
        [.. (await LinesOfGitAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)", "refs/heads"))
            .Select(line => line.Split(' '))
            .Select(parts => (parts[0], parts[1][..7]))];

    /// <summary>The local branches as GC0 has them, except those named in <paramref name="changed"/> (full ref, short SHA).</summary>
    private static async Task AssertBranchesAsync(TempRepo repo, params (string Ref, string ShortSha)[] changed)
    {
        var expected = ConflictsBranches.Select(branch => changed.FirstOrDefault(change => change.Ref == branch.Ref) is { Ref: not null } change ? change : branch).ToList();
        Assert.Equal(expected, await BranchTipsAsync(repo));
    }

    /// <summary>
    /// A <c>-changes</c> snapshot (D67): a commit whose only parent is <paramref name="parent"/>,
    /// whose notes.txt is <paramref name="notes"/>, markers included, and whose other files are
    /// the parent's.
    /// </summary>
    private static async Task AssertChangesSnapshotAsync(TempRepo repo, string snapshot, string parent, string notes)
    {
        Assert.Equal([parent], (await LinesOfGitAsync(repo, "rev-list", "--parents", "-n", "1", snapshot))[0].Split(' ').Skip(1).ToList());
        Assert.Equal(notes, await FileAtAsync(repo, snapshot, ResolverFile));
        Assert.Equal([ResolverFile], await LinesOfGitAsync(repo, "diff", "--name-only", parent, snapshot));
    }

    /// <summary>A commit's message as <c>git cat-file commit</c> stores it: everything after the header's empty line.</summary>
    private static async Task<string> CommitMessageAsync(TempRepo repo, string revision)
    {
        var raw = (await repo.GitAsync("cat-file", "commit", revision)).StandardOutput;
        var end = raw.IndexOf("\n\n", StringComparison.Ordinal);
        return end < 0 ? string.Empty : raw[(end + 2)..];
    }

    /// <summary>A commit as <c>git log -1</c> shows it: author and committer, their dates, and its parents.</summary>
    private static async Task AssertCommitAsync(TempRepo repo, string sha, DateTimeOffset authorDate, DateTimeOffset commitDate, params string[] parents)
    {
        var lines = (await repo.GitAsync("log", "-1", "--format=%an <%ae>%n%cn <%ce>%n%aI%n%cI%n%P", sha)).StandardOutput.Split('\n');
        Assert.Equal("Test Author <author@example.com>", lines[0]);
        Assert.Equal("Test Author <author@example.com>", lines[1]);
        AssertGitDate(authorDate, lines[2]);
        AssertGitDate(commitDate, lines[3]);
        Assert.Equal(parents, lines[4].Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// The <c>rebase-merge/</c> files a step names, each as git wrote it. Git 2.36 writes them as
    /// the report gives them; a newer git (CI's 2.55) writes a todo line's subject as a comment
    /// (<c>pick &lt;sha&gt; # First change</c>) and ends the message with its <c>"# Conflicts:"</c>
    /// block (design 0.1), which are taken out before comparing: the rest is the same.
    /// </summary>
    private static void AssertRebaseFiles(TempRepo repo, IReadOnlyDictionary<string, string> files)
    {
        foreach (var (name, content) in files)
        {
            var written = GitFile(repo, "rebase-merge/" + name);
            var actual = written is null ? null : name switch
            {
                "done" or "git-rebase-todo" => System.Text.RegularExpressions.Regex.Replace(written, @"(?m)^(\w+ [0-9a-f]{7,40}) # ", "$1 "),
                "message" => System.Text.RegularExpressions.Regex.Replace(written, @"(?m)^# Conflicts:\n(#.*(\n|$))*", string.Empty),
                _ => written,
            };
            Assert.True(actual == content, $"rebase-merge/{name} is \"{written}\", expected \"{content}\".");
        }
    }

    /// <summary>
    /// The rebase's first stop as git leaves it (C11 after step 2; C13 after steps 1 and 10):
    /// notes.txt both modified (FR1, with its stages), HEAD detached at Ours edits, the
    /// <c>rebase-merge/</c> files, <c>author-script</c>'s date, <c>REBASE_HEAD</c> and
    /// <c>MERGE_MSG</c>; feature/rebase still at Second change and every other branch as in GC0.
    /// </summary>
    private static async Task AssertRebaseFirstStopAsync(TempRepo repo)
    {
        Assert.Equal(["UU notes.txt"], await PorcelainAsync(repo));
        Assert.Equal(NotesRebaseFirst, WorkingFile(repo, ResolverFile));
        Assert.Equal(RebaseFirstStages, await StagesOfNotesAsync(repo));
        Assert.Equal(StopSha.OursEdits, await repo.HeadAsync());
        Assert.Equal(string.Empty, (await repo.GitAllowingFailureAsync("symbolic-ref", "-q", "HEAD")).StandardOutput.Trim());
        AssertRebaseFiles(repo, RebaseFirstStopFiles);
        Assert.Contains(FirstAuthorDateLine, (GitFile(repo, "rebase-merge/author-script") ?? string.Empty).Split('\n'));
        Assert.Equal(StopSha.FirstChange, GitFile(repo, "REBASE_HEAD")?.Trim());
        Assert.Equal(RebaseFirstMergeMsg, GitFile(repo, "MERGE_MSG"));
        await AssertBranchesAsync(repo);
    }

    /// <summary>The list's stop as git leaves it (C12 after step 2, C13 after step 6), with main at <paramref name="pickedClean"/>.</summary>
    private static async Task AssertPicksStopAsync(TempRepo repo, string pickedClean)
    {
        Assert.Equal(["UU notes.txt"], await PorcelainAsync(repo));
        Assert.Equal(NotesPicks, WorkingFile(repo, ResolverFile));
        Assert.Equal(PicksStages, await StagesOfNotesAsync(repo));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(pickedClean, await repo.HeadAsync());
        Assert.Equal(StopSha.PickConflicting, GitFile(repo, "CHERRY_PICK_HEAD")?.Trim());
        Assert.Equal(PicksMergeMsg, GitFile(repo, "MERGE_MSG"));
        Assert.Equal(StopSha.OursEdits, GitFile(repo, "sequencer/head")?.Trim());
        Assert.Equal(pickedClean, GitFile(repo, "sequencer/abort-safety")?.Trim());
        Assert.Equal(PicksTodo, GitFile(repo, "sequencer/todo"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "last.txt")), "last.txt exists: Pick last was picked.");
        await AssertBranchesAsync(repo, ("refs/heads/main", StopSha.Short(pickedClean)));
    }

    /// <summary>Asserts lines a git call printed (on either stream, where the report names none) on Git 2.36 only (this part's conventions).</summary>
    private static Task AssertGitPrintedAsync(VisualCommit.Core.Git.GitCallRecord call, params string[] lines) =>
        AssertGitLinesAsync(call, call.StandardOutput + "\n" + call.StandardError, lines);

    /// <summary>Asserts lines a git call printed on its standard output, on Git 2.36 only.</summary>
    private static Task AssertGitStandardOutputAsync(VisualCommit.Core.Git.GitCallRecord call, params string[] lines) =>
        AssertGitLinesAsync(call, call.StandardOutput, lines);

    private static async Task AssertGitLinesAsync(VisualCommit.Core.Git.GitCallRecord call, string output, string[] lines)
    {
        if (!await IsReportGitAsync())
        {
            return;
        }

        var printed = output.Replace("\r", "\n", StringComparison.Ordinal).Split('\n').Select(line => line.TrimEnd()).ToList();
        foreach (var line in lines)
        {
            Assert.True(printed.Contains(line), $"\"{call.CommandText}\" did not print \"{line}\". It printed:\n{call.StandardOutput}\n{call.StandardError}");
        }
    }
}
