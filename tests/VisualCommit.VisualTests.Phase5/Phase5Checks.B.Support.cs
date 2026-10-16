// What checks B5 to B9 share (docs/test-reports/phase-5.md, "Conventions of part B"): the right
// panel, the stage panel, the menus as the report writes them, the pages' headers, the history's
// rows and diff, the blame's runs, the reflog's and the backups list's rows, the clean-up at opening,
// the end of an operation, the left panel's sections and git's state. The checks are in
// Phase5Checks.B.cs (B5, B6), Phase5Checks.B.AllFiles.cs (B7), Phase5Checks.B.Reflog.cs (B8) and
// Phase5Checks.B.Backups.cs (B9); their expected values in Phase5Expectations.B.cs (PartB).
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Controls.Blame;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Pages;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations.PartB;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksB
{
    /// <summary>The active tab's repository.</summary>
    private static RepositoryViewModel Repo(ShellDriver app) =>
        app.Session.Shell.ActiveTab.Repository ?? throw new Xunit.Sdk.XunitException("The active tab has no repository.");

    // ---- The clean-up at opening (BD23) ------------------------------------------------------------

    /// <summary>
    /// Waits, before the check's first input, for the background <c>Clean up backups</c> record of
    /// "reflog" (15.2, BD23) and asserts it: succeeded with <c>Removed 2 backups</c>, its two notes,
    /// the log's Info lines <c>reflog: Removed backup …</c>, nothing in the status bar (D139), and
    /// 15 backups left.
    /// </summary>
    private static async Task WaitForCleanUpAsync(ShellDriver app, TempRepo repo)
    {
        var record = await app.WaitForOperationRecordAsync(CleanUpTitle);
        await app.WaitForIdleAsync();
        Assert.Equal((OperationOutcome.Succeeded, CleanUpMessage), (record.Outcome, record.Message));
        Assert.True(record.IsBackground, "The clean-up is not a background operation.");
        Assert.Equal(CleanUpNotes, record.Notes);
        var name = NameOf(repo);
        foreach (var note in CleanUpNotes)
        {
            Assert.Equal(1, app.CountLogLines(AppLogFile.Containing($"[INF] {name}: {note}")));
        }

        Assert.NotEqual(CleanUpMessage, Repo(app).OperationStatus.Text);
        Assert.Equal(BackupsLeft, (await BackupsAsync(repo)).Select(entry => entry.Ref.Stem).Distinct().Count());
    }

    // ---- Git's state --------------------------------------------------------------------------------

    /// <summary>Every ref with what it points at, as <c>git for-each-ref</c> lists them.</summary>
    private static async Task<List<string>> RefsAsync(TempRepo repo) =>
        Split((await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname)")).StandardOutput);

    /// <summary><c>git status --porcelain</c>'s lines.</summary>
    private static async Task<List<string>> StatusAsync(TempRepo repo) =>
        Split((await repo.GitAsync("status", "--porcelain")).StandardOutput);

    /// <summary><c>git status --porcelain --untracked-files=all</c>'s lines: every untracked file by its path, as the report's derivation read them.</summary>
    private static async Task<List<string>> StatusAllAsync(TempRepo repo) =>
        Split((await repo.GitAsync("status", "--porcelain", "--untracked-files=all")).StandardOutput);

    /// <summary>A command's output as its lines, without the empty last one.</summary>
    private static List<string> Split(string output) =>
        [.. output.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n').Where(line => line.Length > 0)];

    /// <summary>"Git unchanged": the refs as they were (<paramref name="refs"/>), the status <paramref name="status"/>, nothing under <c>refs/visualcommit/</c> beyond what was there.</summary>
    private static async Task AssertGitUnchangedAsync(TempRepo repo, IReadOnlyList<string> refs, IReadOnlyList<string> status)
    {
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Equal(status, await StatusAsync(repo));
    }

    /// <summary>"No operation ran": no log line of an operation's end of this repository other than the clean-up at opening.</summary>
    private static void AssertNoOperationRan(ShellDriver app, TempRepo repo)
    {
        var pattern = new Regex($@"\] {Regex.Escape(NameOf(repo))}: (?<title>.+): \w+ after \d+ ms", RegexOptions.CultureInvariant);
        var ended = app.LogLines()
            .Where(line => pattern.Match(line) is { Success: true } match && match.Groups["title"].Value != CleanUpTitle)
            .ToList();
        Assert.True(ended.Count == 0, "An operation ran:\n" + string.Join('\n', ended));
    }

    // ---- Operations (12.1, 12.2, 7.6) ----------------------------------------------------------------

    /// <summary>
    /// Waits for the operation <paramref name="operation"/> that an input after <paramref name="mark"/>
    /// started, and asserts what part B asserts of every operation it starts: the queue's log line
    /// <c>&lt;repo&gt;: &lt;Title&gt;: Succeeded after &lt;N&gt; ms: &lt;status&gt;</c>, the status
    /// bar's text, and the toolbar's Undo enabled with <c>Undo {text}</c> as its tooltip and its
    /// automation help text.
    /// </summary>
    private static async Task AssertOperationDoneAsync(ShellDriver app, TempRepo repo, Operation operation, int mark, TimeSpan? timeout = null)
    {
        await WaitForOperationAsync(app, repo, operation.Title, mark, timeout);
        Assert.Equal(1, app.CountLogLines(AppLogFile.OperationEnded(NameOf(repo), operation.Title, OperationOutcome.Succeeded, operation.Status), mark));
        AssertStatus(app, operation.Status);
        var undo = app.UndoRedoState();
        Assert.Equal((true, "Undo " + operation.UndoText), (undo.UndoEnabled, undo.UndoToolTip));
        Assert.Equal("Undo " + operation.UndoText, AutomationProperties.GetHelpText((Control)app.FindByAutomationId(ShellDriver.UndoButtonId)));
    }

    // ---- The right panel ----------------------------------------------------------------------------

    /// <summary>
    /// Waits until the details panel shows <paramref name="expected"/> (its SHA and subject, read)
    /// and asserts it as part B's "The right panel" says: subject, body or none, author, Date, a
    /// "Commit date" only where expected, the parents, the list's title and its rows; and the
    /// graph's selection behind: commit row <paramref name="graphRow"/> alone, or nothing (-1)
    /// for a commit the graph has no row for (11.3).
    /// </summary>
    private static async Task AssertDetailsAsync(ShellDriver app, ShownDetails expected, int graphRow)
    {
        await app.WaitForAsync(
            () => Shown(app, "DetailsSha") is SelectableTextBlock sha && sha.Text == expected.Sha
                && Shown(app, "DetailsSubject") is SelectableTextBlock subject && subject.Text == expected.Subject
                && !Repo(app).Details.IsLoading
                && ((TextBlock)app.FindByAutomationId("ChangedFilesTitle")).Text == expected.FilesTitle,
            $"the details of {expected.Sha[..7]} \"{expected.Subject}\" with \"{expected.FilesTitle}\"");
        app.Settle();
        Assert.False(app.FindByAutomationId("StagePanel").IsEffectivelyVisible, "The stage panel shows, not the details.");
        var body = (SelectableTextBlock)app.FindByAutomationId("DetailsBody");
        Assert.True((expected.Body is not null) == body.IsEffectivelyVisible, $"The details {(body.IsEffectivelyVisible ? "show the body \"" + body.Text + "\"" : "show no body")}.");
        if (expected.Body is not null)
        {
            Assert.Equal(expected.Body, (body.Text ?? string.Empty).ReplaceLineEndings("\n"));
        }

        Assert.Equal(expected.Author, ((SelectableTextBlock)app.FindByAutomationId("DetailsAuthor")).Text);
        Assert.Equal(expected.Date, ((TextBlock)app.FindByAutomationId("DetailsDate")).Text);
        var commitDate = (TextBlock)app.FindByAutomationId("DetailsCommitDate");
        Assert.True((expected.CommitDate is not null) == commitDate.IsEffectivelyVisible, $"The details {(commitDate.IsEffectivelyVisible ? "show" : "do not show")} a Commit date row.");
        if (expected.CommitDate is not null)
        {
            Assert.Equal(expected.CommitDate, commitDate.Text);
        }

        var parents = app.Window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == "ParentLink")
            .OrderBy(link => app.BoundsOf(link).X)
            .Select(link => AutomationProperties.GetName(link) ?? string.Empty)
            .ToList();
        Assert.Equal(expected.Parents, parents);
        await app.WaitForAsync(() => DetailsRows(app).SequenceEqual(expected.Files), $"the details' files {string.Join(", ", expected.Files)}");
        AssertGraphSelection(app, graphRow);
    }

    /// <summary>Where commit row <paramref name="row"/> is drawn, in the window.</summary>
    private static Rect RowInWindow(ShellDriver app, int row)
    {
        var graph = Graph(app);
        var bounds = graph.RowBounds(row);
        return new Rect(app.BoundsOf(graph).TopLeft + bounds.TopLeft, bounds.Size);
    }

    /// <summary>Asserts that commit row <paramref name="row"/> is wholly inside the graph's view, its centre at window y <paramref name="centreY"/>.</summary>
    private static void AssertRowWhollyInView(ShellDriver app, int row, double centreY)
    {
        var view = app.BoundsOf(Graph(app));
        var bounds = RowInWindow(app, row);
        Assert.True(bounds.Top >= view.Top - 0.5 && bounds.Bottom <= view.Bottom + 0.5, $"Row {row} at {bounds} is not wholly in the graph's view {view}.");
        Assert.Equal(centreY, bounds.Center.Y, 0.5);
    }

    /// <summary>A comparer of points within <paramref name="tolerance"/> on each axis (the report's ±2).</summary>
    private static IEqualityComparer<Point> PointWithin(double tolerance) =>
        EqualityComparer<Point>.Create((a, b) => Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance, point => 0);

    /// <summary>The graph's selection behind a page: commit row <paramref name="row"/> alone, not the working-changes row; -1 for nothing selected.</summary>
    private static void AssertGraphSelection(ShellDriver app, int row)
    {
        var repository = Repo(app);
        Assert.Equal(row, repository.SelectedIndex);
        Assert.False(repository.IsWorkingRowSelected, "The working-changes row is selected.");
        Assert.Equal(row < 0 ? 0 : 1, repository.Selection.Count);
    }

    /// <summary>The details panel's file rows as "letter name folder origin" (each part only when it shows), top to bottom.</summary>
    private static List<string> DetailsRows(ShellDriver app)
    {
        var list = (ItemsControl)app.FindByAutomationId("FileList");
        return [.. ((IEnumerable<ChangedFileRow>)(list.ItemsSource ?? Array.Empty<ChangedFileRow>()))
            .Where(row => row.IsFile)
            .Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder, row.OriginText }.Where(part => !string.IsNullOrEmpty(part))))];
    }

    /// <summary>Asserts that the right panel shows the stage panel with these titles and rows (phase 2's, with phase 4's titles), not the details.</summary>
    private static async Task AssertStagePanelAsync(ShellDriver app, string unstagedTitle, IReadOnlyList<string> unstaged, string stagedTitle, IReadOnlyList<string> staged)
    {
        await app.WaitForAsync(
            () => app.FindByAutomationId("StagePanel").IsEffectivelyVisible
                && app.Find<TextBlock>("UnstagedTitle").Text == unstagedTitle
                && app.Find<TextBlock>("StagedTitle").Text == stagedTitle
                && app.StagePanelSettled,
            $"the stage panel with \"{unstagedTitle}\" and \"{stagedTitle}\"");
        Assert.Equal(unstaged, StageRows(app, "UnstagedFileList"));
        Assert.Equal(staged, StageRows(app, "StagedFileList"));
        var noStaged = app.Find<TextBlock>("NoStaged");
        Assert.Equal(staged.Count == 0, noStaged.IsEffectivelyVisible);
        if (staged.Count == 0)
        {
            Assert.Equal(NoStagedChanges, noStaged.Text);
        }
    }

    /// <summary>A stage list's file rows as "letter name folder", top to bottom.</summary>
    private static List<string> StageRows(ShellDriver app, string list)
    {
        var items = (ItemsControl)app.FindByAutomationId(list);
        return [.. ((IEnumerable<ChangedFileRow>)(items.ItemsSource ?? Array.Empty<ChangedFileRow>()))
            .Where(row => row.IsFile)
            .Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => !string.IsNullOrEmpty(part))))];
    }

    /// <summary>The visible control with this automation id, or null while none is in the window.</summary>
    private static Control? Shown(ShellDriver app, string automationId) =>
        app.Window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == automationId);

    // ---- Menus ----------------------------------------------------------------------------------------

    /// <summary>
    /// Asserts an open menu as the report writes it: its lines, the highlighted item (D105), the
    /// height the report gives (±2) and 4a's shape, each item's text in the primary colour (a
    /// disabled one in the disabled colour), no label cut; with a picture taken while it was open,
    /// the highlighted item's background in the hover colour.
    /// </summary>
    private static void AssertMenuAsReported(ShellDriver app, MenuSnapshot menu, IReadOnlyList<string> lines, string? highlighted, double height, Screenshot? picture = null)
    {
        AssertMenu(menu, [.. lines]);
        AssertHighlighted(menu, highlighted);
        Assert.True(
            Math.Abs(menu.Bounds.Height - height) <= WindowLayout.MenuHeightTolerance,
            string.Create(CultureInfo.InvariantCulture, $"The menu \"{menu.Header}\" is {menu.Bounds.Height} high; the report says {height}."));
        AssertMenuShape(app, menu);
        var palette = PaletteOf(app);
        foreach (var item in menu.Items)
        {
            var colour = !item.IsEnabled ? palette.TextDisabled : item.IsDestructive ? palette.Danger : palette.TextPrimary;
            Assert.True(item.TextColor == colour, $"The menu item \"{item.Label}\" is drawn in {item.TextColor}, expected {colour}.");
        }

        var presenter = app.OpenMenuView ?? throw new Xunit.Sdk.XunitException("No menu is open.");
        var labels = presenter.GetVisualDescendants().OfType<TrimmedTextBlock>().ToList();
        Assert.All(labels, label => Assert.False(label.IsTrimmed, $"The menu's label \"{label.Text}\" is cut."));
        if (picture is not null && menu.Highlighted is { } item2)
        {
            var tips = app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible && tip.Bounds.Width > 0).Select(app.BoundsOf).ToList();
            var point = new[] { new Point(item2.Bounds.Right - 4, item2.Bounds.Center.Y), new Point(item2.Bounds.Left + 4, item2.Bounds.Center.Y) }
                .FirstOrDefault(candidate => !tips.Any(tip => tip.Contains(candidate)), new Point(item2.Bounds.Right - 4, item2.Bounds.Center.Y));
            AssertColour(palette.Hover, picture, point, $"background of the highlighted item \"{item2.Label}\"");
        }
    }

    /// <summary>Asserts that the tooltip <paramref name="text"/> is open and lies inside the window.</summary>
    private static void AssertToolTipShown(ShellDriver app, string text)
    {
        var tips = app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible && tip.Bounds.Width > 0).ToList();
        var tip = Assert.Single(tips, candidate => candidate.Content as string == text || (candidate.Content is Visual visual && string.Join('\n', TextsIn(visual)) == text));
        var bounds = app.BoundsOf(tip);
        var size = app.Window.ClientSize;
        Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= size.Width && bounds.Bottom <= size.Height, $"The tooltip at {bounds} is not inside the {size.Width}x{size.Height} window.");
    }

    /// <summary>Presses Esc and asserts that the menu closed and nothing opened.</summary>
    private static async Task CloseMenuAsync(ShellDriver app)
    {
        var mark = app.LogMark();
        app.PressKey(Avalonia.Input.Key.Escape);
        await AssertNoMenuAsync(app, mark);
    }

    // ---- The pages ------------------------------------------------------------------------------------

    /// <summary>Asserts that a page is shown in place of the graph (its column headers included) and not the graph.</summary>
    private static void AssertPageInPlaceOfGraph(ShellDriver app, Rect bounds, Rect page)
    {
        Assert.Equal(bounds, page);
        Assert.False(Repo(app).ShowsGraph, "The graph shows beside the page.");
        Assert.False(Graph(app).IsEffectivelyVisible, "The graph's rows are drawn beside the page.");
    }

    /// <summary>Asserts a page's header: 36 high at the page's top, with the icon <paramref name="iconKey"/> (13.3) in the secondary colour.</summary>
    private static void AssertPageHeader(ShellDriver app, string headerId, Rect page, string iconKey)
    {
        var header = (Border)app.FindByAutomationId(headerId);
        Assert.Equal(new Rect(page.X, page.Y, page.Width, HeaderHeight), app.BoundsOf(header));
        var icon = header.GetVisualDescendants().OfType<Icon>().FirstOrDefault(candidate => candidate.IsEffectivelyVisible && ReferenceEquals(candidate.Data, Resource(app, iconKey)))
            ?? throw new Xunit.Sdk.XunitException($"The header has no {iconKey}.");
        Assert.Equal(PaletteOf(app).TextSecondary, (icon.Foreground as ISolidColorBrush)?.Color);
    }

    /// <summary>An application resource by its key (an icon's geometry, the code font).</summary>
    private static object? Resource(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) ? value : null;

    /// <summary>Asserts the history page's header texts: the title, the folder, the <c>from</c> text or none, <c>Blame</c> enabled and the close button.</summary>
    private static void AssertHistoryHeader(ShellDriver app, FileHistorySnapshot page, string title, string? from)
    {
        Assert.Equal((title, (string?)SrcFolder, from, true), (page.Title, page.Folder, page.From, page.BlameEnabled));
        Assert.Equal(title, ((TrimmedTextBlock)app.FindByAutomationId("FileHistoryTitle")).Text);
        Assert.Equal(SrcFolder, ((TextBlock)app.FindByAutomationId("FileHistoryFolder")).Text);
        var fromBlock = (TextBlock)app.FindByAutomationId("FileHistoryFrom");
        Assert.Equal(from is not null, fromBlock.IsEffectivelyVisible);
        if (from is not null)
        {
            Assert.Equal(from, fromBlock.Text);
        }

        Assert.Equal("Blame", ((Button)app.FindByAutomationId("FileHistoryBlame")).Content);
        Assert.True(app.FindByAutomationId("FileHistoryClose").IsEffectivelyVisible, "The history has no close button.");
        Assert.Equal(title, AutomationProperties.GetName(app.FindByAutomationId("FileHistory")));
    }

    /// <summary>
    /// Asserts the history's rows (4.1, BD2, BD13): letter and its colour, short SHA, subject,
    /// secondary text, author and date (the surface holds both; the columns show as
    /// <paramref name="authorShown"/> and <paramref name="dateShown"/> say), the rows'
    /// <c>FileHistoryRow</c> automation names <c>{short} {subject}</c>, each 26 high from the
    /// list's top, and the selected row with its background (the selection colour) and the
    /// keyboard focus.
    /// </summary>
    private static void AssertHistoryRows(ShellDriver app, FileHistorySnapshot page, IReadOnlyList<HistoryRow> expected, int selected, bool authorShown = false, bool dateShown = false)
    {
        var palette = PaletteOf(app);
        Assert.Equal(expected.Select(row => row.Name), page.Rows.Select(row => row.Name));
        var list = (ListBox)app.FindByAutomationId("FileHistoryList");
        for (var index = 0; index < expected.Count; index++)
        {
            var want = expected[index];
            var row = page.Rows[index];
            var bounds = row.Bounds ?? throw new Xunit.Sdk.XunitException($"History row {index} is not drawn.");
            AssertRowAutomation(list, index, "FileHistoryRow", want.Name);
            Assert.Equal(new Rect(page.ListBounds.X, page.ListBounds.Y + (PageRowHeight * index), page.ListBounds.Width, PageRowHeight), bounds);
            Assert.Equal((want.Letter, want.ShortSha, want.Subject, want.Secondary, want.Author, want.Date), (row.Letter, row.ShortSha, row.Subject, row.Secondary, row.Author, row.Date));
            Assert.Equal(LetterColour(palette, want.Letter), row.LetterColor);
            Assert.Equal((authorShown, dateShown), (row.AuthorShown, row.DateShown));
            Assert.Equal(index == selected, row.IsSelected);
            Assert.Equal(index == selected, row.HasFocus);
            if (index == selected)
            {
                Assert.Equal(palette.Selection, row.Background);
            }
        }
    }

    /// <summary>A status letter's colour (the texts' "Colours"): A success, M warning, D danger, R and C accent.</summary>
    private static Color LetterColour(Palette palette, string letter) => letter switch
    {
        "A" => palette.Success,
        "M" => palette.Warning,
        "D" => palette.Danger,
        _ => palette.Accent,
    };

    /// <summary>
    /// Asserts the history's diff part (4.1): phase 2's header (letter, name, folder, origin), the
    /// side label, no close button, Inline selected and Side by side, no action buttons, and its
    /// first hunk's header (and rows, where the report gives them all).
    /// </summary>
    private static async Task AssertHistoryDiffAsync(ShellDriver app, string letter, string name, string folder, string? origin, string sideLabel, ExpectedHunk hunk)
    {
        // The hosted diff takes the new row's side label before its text has loaded: wait for both.
        try
        {
            await app.WaitForAsync(
                () => app.ShownPage is FileHistoryView && app.FileHistorySnapshot() is { Diff: { ShowsText: true } diff, DiffSideLabel: var label }
                    && label == sideLabel && diff.SideLabel == sideLabel && diff.TextDiff?.Hunks.FirstOrDefault()?.Header == hunk.Header,
                $"the history's diff of {sideLabel}");
        }
        catch (TimeoutException) when (app.ShownPage is FileHistoryView && app.FileHistorySnapshot().Diff is { } shown)
        {
            Assert.Fail($"The history's diff shows {shown.SideLabel} ({shown.Body}) with the hunks {string.Join(" | ", shown.TextDiff?.Hunks.Select(h => h.Header) ?? [])}; expected {sideLabel} with {hunk.Header}.");
        }

        var page = app.FileHistorySnapshot();
        var diff = page.Diff!;
        Assert.Equal((letter, name, folder, origin ?? string.Empty), (diff.StatusLetter, diff.FileName, diff.Folder, diff.OriginText));
        Assert.False(page.DiffShowsClose, "The history's diff has a close button.");
        var view = (Control)app.FindByAutomationId("FileHistoryDiff");
        Assert.True(diff.IsInline, "The history's diff is not inline.");
        Assert.True(Named<Button>(view, "InlineDiffButton").Classes.Contains("selected"), "Inline is not selected.");
        Assert.True(Named<Button>(view, "SideBySideDiffButton").IsEffectivelyVisible, "Side by side does not show.");
        foreach (var action in new[] { "StageLinesButton", "DiscardLinesButton", "UnstageLinesButton", "StageFileButton", "DiscardFileButton", "UnstageFileButton" })
        {
            Assert.False(Named<Button>(view, action).IsEffectivelyVisible, $"The history's diff shows {action}.");
        }

        AssertHunk(diff.TextDiff!, hunk);
    }

    /// <summary>Asserts a diff's one hunk: its header, and its rows where the report gives them all.</summary>
    private static void AssertHunk(FileDiff diff, ExpectedHunk hunk)
    {
        var only = Assert.Single(diff.Hunks);
        Assert.Equal(hunk.Header, only.Header);
        if (hunk.Rows is not null)
        {
            Assert.Equal(hunk.Rows, only.Lines.Select(RowText));
        }
    }

    /// <summary>A diff's changed rows, as "+text" and "-text".</summary>
    private static List<string> Changes(FileDiff diff) =>
        [.. diff.Hunks.SelectMany(hunk => hunk.Lines).Where(line => line.IsChange).Select(RowText)];

    /// <summary>A diff row as the report writes it: "+", "-" or " " and its text.</summary>
    private static string RowText(DiffLine line) => line.Kind switch
    {
        DiffLineKind.Added => "+" + line.Text,
        DiffLineKind.Removed => "-" + line.Text,
        _ => " " + line.Text,
    };

    /// <summary>A control by its name inside <paramref name="scope"/>.</summary>
    private static T Named<T>(Visual scope, string name)
        where T : Control =>
        scope.GetVisualDescendants().OfType<T>().FirstOrDefault(control => control.Name == name)
        ?? throw new Xunit.Sdk.XunitException($"No {typeof(T).Name} named {name} in the {scope.GetType().Name}.");

    // ---- The blame ------------------------------------------------------------------------------------

    /// <summary>Waits until the blame shows <paramref name="lines"/> lines of the file titled <paramref name="title"/>, laid out, and returns it.</summary>
    private static async Task<BlameSnapshot> WaitForBlameAsync(ShellDriver app, string title, string caption, int lines)
    {
        await app.WaitForPageAsync<BlameView>();
        await app.WaitForAsync(
            () => app.ShownPage is BlameView && app.BlameSnapshot() is { IsLoading: false } blame && blame.Title == title && blame.Caption == caption
                && blame.Text.Split('\n').Length == lines && blame.Runs.All(run => run.Background is not null),
            $"the blame \"{title}\" {caption} with {lines} lines");
        app.Settle();
        return app.BlameSnapshot();
    }

    /// <summary>
    /// Asserts the blame's runs (BD12): their lines and commits; each committed run's first line
    /// shows its short SHA (code font, at the gutter's left + 4, 50.4 wide) and the date 6 after
    /// it, whole; a run not committed yet shows <c>Not committed yet</c> alone (97.2 wide); the
    /// backgrounds alternate from <c>VcPanelBackgroundBrush</c>, the run <paramref name="selected"/>
    /// (or none) in <c>VcSelectionBrush</c>.
    /// </summary>
    private static void AssertRuns(BlameSnapshot blame, IReadOnlyList<BlameRun> expected, int? selected = null)
    {
        Assert.Equal(expected.Select(run => (run.First, run.Last)), blame.Runs.Select(run => (run.FirstLine, run.LastLine)));
        var left = blame.GutterBounds.X;
        for (var index = 0; index < expected.Count; index++)
        {
            var want = expected[index];
            var run = blame.Runs[index];
            if (want.ShortSha is null)
            {
                Assert.Equal(new string('0', 40), run.Sha);
                var text = Assert.Single(run.Texts);
                Assert.Equal(NotCommittedYet, text.Text);
                Assert.Equal(left + GutterPadding, text.X, 0.5);
                Assert.Equal(NotCommittedYetWidth, text.Width, 0.5);
            }
            else
            {
                Assert.StartsWith(want.ShortSha, run.Sha, StringComparison.Ordinal);
                Assert.Equal([want.ShortSha, GutterDate], run.Texts.Select(part => part.Text));
                Assert.Equal(left + GutterPadding, run.Texts[0].X, 0.5);
                Assert.Equal(GutterShaWidth, run.Texts[0].Width, 0.5);
                Assert.Equal(left + GutterPadding + GutterShaWidth + GutterGap, run.Texts[1].X, 0.5);
                Assert.Equal(GutterDateWidth, run.Texts[1].Width, 0.5);
                Assert.True(run.Texts[1].X + run.Texts[1].Width <= blame.GutterBounds.Right - GutterPadding + 0.5, $"Run {index}'s date is not whole in the gutter.");
            }

            var background = index == selected ? "VcSelectionBrush" : index % 2 == 0 ? "VcPanelBackgroundBrush" : "VcWindowBackgroundBrush";
            Assert.True(background == run.Background, $"Run {index} (lines {run.FirstLine} to {run.LastLine}) is drawn on {run.Background}, expected {background}.");
            Assert.Equal(index == selected, run.IsSelected);
        }
    }

    /// <summary>Asserts the blame's gutter and text at 1100×700 (4.2, 13.4): the gutter 130 wide beside the text, both from the content's top; the number column; syntax colours where the check states them (<paramref name="highlighted"/>); no caret.</summary>
    private static void AssertBlameLayout(ShellDriver app, BlameSnapshot blame, bool? highlighted = null)
    {
        Assert.Equal(PageBounds, blame.Bounds);
        Assert.Equal(GutterBounds, blame.GutterBounds);
        Assert.Equal(BlameTextBounds, blame.TextBounds);
        Assert.Equal(NumberColumnWidth, blame.NumberColumnWidth, 2.0);
        if (highlighted is { } colours)
        {
            Assert.True(colours == blame.IsHighlighted, $"The blame's text has {(blame.IsHighlighted ? "" : "no ")}syntax colours.");
        }
        var text = (BlameTextEditor)app.FindByAutomationId("BlameText");
        Assert.Same(Brushes.Transparent, text.TextArea.CaretBrush);
        Assert.Equal(blame.Title, ((TrimmedTextBlock)app.FindByAutomationId("BlameTitle")).Text);
        Assert.Equal(blame.Caption, ((TrimmedTextBlock)app.FindByAutomationId("BlameCaption")).Text);
        Assert.Equal("File history", ((Button)app.FindByAutomationId("BlameHistory")).Content);
        Assert.True(app.FindByAutomationId("BlameClose").IsEffectivelyVisible, "The blame has no close button.");
        Assert.Equal(blame.Title, AutomationProperties.GetName(app.FindByAutomationId("Blame")));
    }

    /// <summary>
    /// Some pixel inside <paramref name="area"/> of the picture has the colour, each channel within
    /// <see cref="TextColourTolerance"/>: a text drawn in it. Small text is drawn with subpixel
    /// smoothing, which tints even its fullest pixels a few steps (the gutter's date at 11 px
    /// reaches #9BA3AB for #9BA3B0), while the theme's text colours lie far apart. A thin line
    /// icon (the lost mark's 12-px <c>IconAlert</c>) has no whole pixel at all: its check passes
    /// a wider <paramref name="tolerance"/>.
    /// </summary>
    private static void AssertDrawnIn(Screenshot picture, Rect area, Color colour, string what, int tolerance = TextColourTolerance)
    {
        for (var y = (int)Math.Ceiling(area.Top); y < (int)Math.Floor(area.Bottom); y++)
        {
            for (var x = (int)Math.Ceiling(area.Left); x < (int)Math.Floor(area.Right); x++)
            {
                var pixel = picture.PixelAt(new Point(x + 0.5, y + 0.5));
                if (Math.Abs(pixel.R - colour.R) <= tolerance && Math.Abs(pixel.G - colour.G) <= tolerance && Math.Abs(pixel.B - colour.B) <= tolerance)
                {
                    return;
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"No pixel of {what} at {area} is {colour}.");
    }

    /// <summary>How far a pixel of a text may lie from the text's colour in each channel (<see cref="AssertDrawnIn"/>).</summary>
    private const int TextColourTolerance = 8;

    /// <summary>How far a pixel of a 12-px line icon may lie from its colour in each channel: its fullest pixel of #E0A23B is #C79036.</summary>
    private const int IconColourTolerance = 32;

    // ---- The reflog page and the backups list ------------------------------------------------------

    /// <summary>
    /// Asserts a reflog's rows (5.2, BD3): selectors in order, and on Git 2.36 and 2.55 every
    /// entry's action, commit and subject word for word; the lost marks; the time held for the
    /// surface; the drawn rows' columns at 440 (selector, action, commit, lost mark; the time
    /// hidden), the selector in the code font and the secondary colour, an action cut with "…"
    /// carrying its whole text as its tooltip, the lost mark's <c>IconAlert</c> in the warning colour.
    /// </summary>
    private static async Task AssertReflogRowsAsync(ShellDriver app, ReflogSnapshot page, IReadOnlyList<ReflogRow> expected)
    {
        Assert.Equal(expected.Select(row => row.Selector), page.Rows.Select(row => row.Selector));
        Assert.Equal(expected.Select(row => (row.Selector, row.ShortSha, row.Lost)), page.Rows.Select(row => (row.Selector, row.ShortSha, row.IsLost)));
        if (await IsReflogTextGitAsync())
        {
            Assert.Equal(expected.Select(row => (row.Time, row.Action, row.Subject)), page.Rows.Select(row => (row.Time, row.Action, row.Subject)));
        }

        var list = (ListBox)app.FindByAutomationId("ReflogList");
        var palette = PaletteOf(app);
        var code = Resource(app, "VcCodeFontFamily");
        for (var index = 0; index < page.Rows.Count; index++)
        {
            var row = page.Rows[index];
            if (row.Bounds is not { } bounds || bounds.Bottom > page.ListBounds.Bottom + 0.5)
            {
                continue;
            }

            Assert.Equal(new Rect(page.ListBounds.X, page.ListBounds.Y + (PageRowHeight * index), page.ListBounds.Width, PageRowHeight), bounds);
            AssertRowAutomation(list, index, "ReflogRow", row.Selector);
            Assert.Null(row.TimeBounds);
            Assert.Equal(ReflogSelectorX, (row.SelectorBounds!.Value.Left, row.SelectorBounds.Value.Right));
            Assert.Equal(ReflogActionX, (row.ActionBounds!.Value.Left, row.ActionBounds.Value.Right));
            Assert.Equal(ReflogCommitX.Left, row.CommitBounds!.Value.Left);
            var item = (ListBoxItem)list.ContainerFromIndex(index)!;
            Assert.Equal(ReflogCommitX.Right, app.BoundsOf(Named<TrimmedTextBlock>(item, "ReflogCommitSubject")).Right, 0.5);
            var selector = Named<TextBlock>(item, "ReflogSelector");
            Assert.Equal(palette.TextSecondary, (selector.Foreground as ISolidColorBrush)?.Color);
            Assert.Same(code, selector.FontFamily);
            var action = Named<TrimmedTextBlock>(item, "ReflogAction");
            Assert.Equal(action.IsTrimmed ? action.Text : null, ToolTip.GetTip(action) as string);
            Assert.Equal(row.IsLost, row.LostMarkBounds is not null);
            if (row.LostMarkBounds is { } mark)
            {
                Assert.Equal(new Rect(ReflogLostMarkX.Left, bounds.Y, ReflogLostMarkX.Right - ReflogLostMarkX.Left, PageRowHeight), mark);
                var icon = Named<Border>(item, "ReflogLostMark").GetVisualDescendants().OfType<Icon>().Single();
                Assert.Same(Resource(app, "IconAlert"), icon.Data);
                Assert.Equal(palette.Warning, (icon.Foreground as ISolidColorBrush)?.Color);
                Assert.Equal(LostToolTip, ToolTip.GetTip(Named<Border>(item, "ReflogLostMark")) as string);
            }
        }
    }

    /// <summary>
    /// Waits until the reflog page shows <paramref name="rows"/> rows of the source
    /// <paramref name="source"/> from a read after <paramref name="readsBefore"/> (the page's
    /// <c>ReadsShown</c> before the input; 0 for a page the input opens), made for the refs as they
    /// are now, its lost marks included (they come once the list shows, 5.2), and returns it.
    /// </summary>
    private static async Task<ReflogSnapshot> WaitForReflogAsync(ShellDriver app, string title, string source, int rows, int readsBefore = 0)
    {
        await app.WaitForPageAsync<ReflogView>();
        await app.WaitForAsync(
            () => app.ShownPage is ReflogView && app.ReflogSnapshot() is { IsLoading: false } page && page.Title == title && page.SourceLabel == source,
            $"the page \"{title}\"");
        await app.WaitForReflogReadAsync(readsBefore);
        var shown = app.ReflogSnapshot();
        Assert.True(shown.Rows.Count == rows || shown.Backups.Count == rows, $"The page \"{title}\" shows {Math.Max(shown.Rows.Count, shown.Backups.Count)} rows, not {rows}.");
        app.Settle();
        return app.ReflogSnapshot();
    }

    /// <summary>Asserts the reflog page's header (BD6): <c>ReflogSource</c> labelled <paramref name="source"/> with its chevron 8 from the left, <c>ReflogClose</c> 8 from the right, no other text.</summary>
    private static void AssertReflogHeader(ShellDriver app, ReflogSnapshot page, string title, string source)
    {
        Assert.Equal((title, source), (page.Title, page.SourceLabel));
        Assert.Equal(title, AutomationProperties.GetName(app.FindByAutomationId("Reflog")));
        var header = (Border)app.FindByAutomationId("ReflogHeader");
        Assert.Equal(new Rect(page.Bounds.X, page.Bounds.Y, page.Bounds.Width, HeaderHeight), app.BoundsOf(header));
        Assert.Equal(page.Bounds.X + 8, page.SourceBounds.X, 0.5);
        var close = app.BoundsOf(app.FindByAutomationId("ReflogClose"));
        Assert.Equal(page.Bounds.Right - 8, close.Right, 0.5);
        var sourceButton = app.FindByAutomationId("ReflogSource");
        Assert.Contains(sourceButton.GetVisualDescendants().OfType<Icon>(), icon => icon.IsEffectivelyVisible && ReferenceEquals(icon.Data, Resource(app, "IconChevronDown")));
        Assert.Equal([source], TextsIn(header));
    }

    /// <summary>
    /// Asserts the backups list's rows (5.3, BD4): names, times, words, commits in order; the
    /// drawn rows' columns at 440; the words and the subject cut with "…" only where they do not
    /// fit, each with its whole text as tooltip.
    /// </summary>
    private static void AssertBackupRows(ShellDriver app, ReflogSnapshot page, IReadOnlyList<BackupRow> expected)
    {
        Assert.Equal(expected.Select(row => row.Name), page.Backups.Select(row => row.Name));
        Assert.Equal(expected.Select(row => (row.Time, row.Words, row.ShortSha, row.Subject)), page.Backups.Select(row => (row.Time, row.Words, row.ShortSha, row.Subject)));
        var list = (ListBox)app.FindByAutomationId("ReflogList");
        for (var index = 0; index < page.Backups.Count; index++)
        {
            var row = page.Backups[index];
            var bounds = row.Bounds ?? throw new Xunit.Sdk.XunitException($"Backup row {index} is not drawn.");
            Assert.Equal(new Rect(page.ListBounds.X, page.ListBounds.Y + (PageRowHeight * index), page.ListBounds.Width, PageRowHeight), bounds);
            AssertRowAutomation(list, index, "BackupRow", row.Name);
            Assert.Equal(BackupTimeX, (row.TimeBounds!.Value.Left, row.TimeBounds.Value.Right));
            Assert.Equal(BackupWordsX, (row.WordsBounds!.Value.Left, row.WordsBounds.Value.Right));
            Assert.Equal(BackupCommitX, (row.CommitBounds!.Value.Left, row.CommitBounds.Value.Left + 160));
            var item = (ListBoxItem)list.ContainerFromIndex(index)!;
            var time = Named<TextBlock>(item, "BackupTime");
            Assert.True(time.TextLayout.TextLines.All(line => !line.HasCollapsed), $"The time of backup row {index} is cut.");
            foreach (var name in new[] { "BackupWords", "BackupCommitSubject" })
            {
                var text = Named<TrimmedTextBlock>(item, name);
                Assert.Equal(text.IsTrimmed ? text.Text : null, ToolTip.GetTip(text) as string);
            }
        }
    }

    /// <summary>A page list's row <paramref name="index"/> as UI Automation finds it (13.4): its automation id and name.</summary>
    private static void AssertRowAutomation(ListBox list, int index, string automationId, string name)
    {
        var item = list.ContainerFromIndex(index) ?? throw new Xunit.Sdk.XunitException($"Row {index} of {list.Name} is not realised.");
        Assert.Equal((automationId, name), (AutomationProperties.GetAutomationId(item), AutomationProperties.GetName(item)));
    }

    // ---- The left panel ---------------------------------------------------------------------------------

    /// <summary>A section of the left panel: its count and its first-level rows, a closed folder as "▸ name".</summary>
    private static (int Count, List<string> Rows) Section(ShellDriver app, string title)
    {
        app.Settle();
        var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
        var start = rows.FindIndex(row => row is RefSectionRow && row.Text == title);
        Assert.True(start >= 0, $"The left panel has no section {title}.");
        var section = (RefSectionRow)rows[start];
        var children = rows.Skip(start + 1).TakeWhile(row => row is RefItemRow).Cast<RefItemRow>()
            .Where(row => row.Depth == 1)
            .Select(row => row.ShowsClosedChevron ? "▸ " + row.Text : row.Text)
            .ToList();
        return (section.Count, children);
    }
}
