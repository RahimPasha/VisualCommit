// Part F's own helpers (docs/test-reports/phase-5.md, "Conventions of part F"): the selection and
// the details panel, the search bar and its marks, the comparison, the left panel in the report's
// notation with its marks of hidden and soloed branches, menus as "Menus" writes them, the diff's
// rows, sampled pixels, git's calls and state read back, and the session's filters. Only part F's
// writer edits this file; what every part shares is in Phase5Checks.Support.cs.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksF
{
    /// <summary>The active tab's repository.</summary>
    private static RepositoryViewModel Repo(ShellDriver app) => app.Session.Shell.ActiveTab.Repository!;

    // ---- The graph's selection and the details ----

    /// <summary>Nothing is selected: no commit, not the working-changes row, and the details panel shows its placeholder.</summary>
    private static void AssertNothingSelected(ShellDriver app)
    {
        app.Settle();
        var repository = Repo(app);
        Assert.True(repository.Selection.IsEmpty, $"Commits are selected: {repository.Selection}.");
        Assert.Equal(-1, repository.SelectedIndex);
        Assert.False(repository.IsWorkingRowSelected, "The working-changes row is selected.");
        var placeholder = (TextBlock)app.FindByAutomationId("DetailsPlaceholder");
        Assert.True(placeholder.IsEffectivelyVisible, "The details panel does not show its placeholder.");
        Assert.Equal(PartF.NothingSelected, placeholder.Text);
    }

    /// <summary>Commit row <paramref name="row"/> alone is selected (the graph's selection and the view model's), not the working-changes row.</summary>
    private static void AssertRowSelected(ShellDriver app, int row)
    {
        app.Settle();
        var repository = Repo(app);
        Assert.Equal(row, repository.SelectedIndex);
        Assert.Equal(repository.Graph.CommitAt(row).Sha, repository.Selection.Single);
        Assert.False(repository.IsWorkingRowSelected, "The working-changes row is selected.");
        Assert.Equal(row, Graph(app).SelectedIndex);
    }

    /// <summary>Exactly the commits of <paramref name="rows"/> are selected (several commits), not the working-changes row.</summary>
    private static void AssertRowsSelected(ShellDriver app, params int[] rows)
    {
        app.Settle();
        var repository = Repo(app);
        var expected = rows.Select(row => repository.Graph.CommitAt(row).Sha).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(expected, repository.Selection.Shas.Order(StringComparer.Ordinal).ToList());
        Assert.False(repository.IsWorkingRowSelected, "The working-changes row is selected.");
    }

    /// <summary>
    /// The selected row's selection background in a picture: sampled in the Graph column 2 px left
    /// of its right edge (right of every lane's node and line in part F's scenarios), at the row's centre.
    /// </summary>
    private static void AssertSelectionBackground(ShellDriver app, Screenshot shot, int row)
    {
        var graph = Graph(app);
        var origin = app.BoundsOf(graph).TopLeft;
        var at = new Point(origin.X + graph.Columns.Graph.Right - 2, origin.Y + graph.RowBounds(row).Center.Y);
        AssertColour(PaletteOf(app).Selection, shot, at, $"selection background of row {row}");
    }

    /// <summary>Commit row <paramref name="row"/> lies wholly inside the graph control, as scrolled: it is in view.</summary>
    private static void AssertRowInView(ShellDriver app, int row)
    {
        var graph = Graph(app);
        var bounds = graph.RowBounds(row);
        Assert.True(bounds.Top >= -0.5 && bounds.Bottom <= graph.Bounds.Height + 0.5, $"Row {row} at {bounds} is not in view in the graph {graph.Bounds.Size}.");
    }

    /// <summary>The visible control with this automation id, or null.</summary>
    private static Control? Shown(ShellDriver app, string automationId) =>
        app.Window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == automationId);

    /// <summary>Waits until the details panel shows the commit <paramref name="subject"/>, the selected one.</summary>
    private static async Task WaitForDetailsAsync(ShellDriver app, string subject)
    {
        await app.WaitForAsync(
            () => Shown(app, "DetailsSubject") is SelectableTextBlock text && text.Text == subject
                && Shown(app, "DetailsSha") is SelectableTextBlock sha
                && Graph(app).Selection.Shas is [var selected] && sha.Text == selected,
            $"the details of \"{subject}\", the selected commit");
        app.WaitForCommitDetails();
    }

    /// <summary>
    /// Asserts the details of the selected commit as the report gives them: its subject and body
    /// (none shown when null), the author, the date, no Committer and no Commit date row (the
    /// history scenario's commits have the same author and committer and dates), the SHA, the
    /// parents' short ids in order, and the changed files.
    /// </summary>
    private static async Task AssertDetailsAsync(ShellDriver app, PartF.Details details)
    {
        await WaitForDetailsAsync(app, details.Subject);
        var body = Shown(app, "DetailsBody") as SelectableTextBlock;
        Assert.Equal(details.Body, string.IsNullOrEmpty(body?.Text) ? null : body.Text);
        Assert.Equal(details.Author, ((SelectableTextBlock)app.FindByAutomationId("DetailsAuthor")).Text);
        Assert.Equal(details.Date, ((TextBlock)app.FindByAutomationId("DetailsDate")).Text);
        Assert.Null(Shown(app, "DetailsCommitter"));
        Assert.Null(Shown(app, "DetailsCommitDate"));
        Assert.Equal(details.Sha, ((SelectableTextBlock)app.FindByAutomationId("DetailsSha")).Text);
        var parents = app.Window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == "ParentLink")
            .OrderBy(link => app.BoundsOf(link).X)
            .Select(link => AutomationProperties.GetName(link) ?? string.Empty)
            .ToList();
        Assert.Equal(details.Parents, parents);
        Assert.Equal(PartF.ChangedFiles(details.Files.Count), ((TextBlock)app.FindByAutomationId("ChangedFilesTitle")).Text);
        await app.WaitForAsync(() => FileRows(app, "FileList").SequenceEqual(details.Files), $"the details' files {string.Join(", ", details.Files)}");
    }

    /// <summary>A file list's rows as the report writes them: "letter name [folder] [renamed from …]" (<c>UnstagedFileList</c>, <c>FileList</c>, <c>CompareFileList</c>).</summary>
    private static List<string> FileRows(ShellDriver app, string list)
    {
        app.Settle();
        var items = (ItemsControl)app.FindByAutomationId(list);
        return [.. ((IEnumerable<ChangedFileRow>)(items.ItemsSource ?? Array.Empty<ChangedFileRow>())).Where(row => row.IsFile).Select(FileRow)];
    }

    /// <summary>One file row as the report writes it.</summary>
    private static string FileRow(ChangedFileRow row) =>
        string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder, row.OriginText }.Where(part => !string.IsNullOrEmpty(part)));

    // ---- The search ----

    /// <summary>The search bar as it shows; fails while it is closed.</summary>
    private static SearchBarSnapshot Bar(ShellDriver app) => app.SearchBar() ?? throw new Xunit.Sdk.XunitException("The search bar is not open.");

    /// <summary>
    /// Asserts the search bar's state: its mode, text, placeholder, whether the box has the keyboard
    /// focus, the count, previous and next enabled or not, the tooltips of previous, next and close
    /// (3.1's texts), and the progress bar hidden. Returns what it shows.
    /// </summary>
    private static SearchBarSnapshot AssertBar(ShellDriver app, string mode, string text, string placeholder, bool focus, string count)
    {
        var bar = Bar(app);
        Assert.Equal((mode, text, placeholder), (bar.Mode, bar.Text, bar.Placeholder));
        Assert.True(bar.BoxHasFocus == focus, $"The search box {(bar.BoxHasFocus ? "has" : "does not have")} the keyboard focus.");
        Assert.Equal(count, bar.Count);
        var matches = count.Length > 0 && count != PartF.NoMatches && !count.StartsWith("Searching", StringComparison.Ordinal);
        Assert.True(bar.PreviousEnabled == matches && bar.NextEnabled == matches, $"Previous and next are {(bar.PreviousEnabled ? "enabled" : "disabled")} and {(bar.NextEnabled ? "enabled" : "disabled")} with the count \"{count}\".");
        Assert.Equal((PartF.PreviousToolTip, PartF.NextToolTip, PartF.CloseToolTip), (bar.PreviousToolTip, bar.NextToolTip, bar.CloseToolTip));
        Assert.False(bar.ProgressShown, "The search's progress bar shows.");
        return bar;
    }

    /// <summary>
    /// Asserts where the bar and its parts are (design 3.1, FD18): the bar from x 260 to the graph
    /// area's right edge and y 88 to 124; left to right, 8 from its edges and 6 apart, the mode
    /// button, the box (at least 120 wide), the count (when it shows), previous, next and close (22
    /// wide each); the graph's rows 36 lower. Each stated position ±2.
    /// </summary>
    private static void AssertBarLayout(ShellDriver app, SearchBarSnapshot bar, double right)
    {
        var tolerance = PartF.PositionTolerance;
        AssertRect(new Rect(WindowLayout.LeftPanelWidth, PartF.BarTop, right - WindowLayout.LeftPanelWidth, PartF.BarHeight), bar.Bounds, "the search bar");
        Assert.Equal(bar.Bounds.Left + PartF.BarInset, bar.ModeButtonBounds.Left, tolerance);
        Assert.Equal(bar.ModeButtonBounds.Right + PartF.BarGap, bar.BoxBounds.Left, tolerance);
        Assert.True(bar.BoxBounds.Width >= PartF.BarBoxMinWidth - 0.5, $"The search box is {bar.BoxBounds.Width} wide, less than {PartF.BarBoxMinWidth}.");
        Assert.True(bar.CountShown, "The count is not in the bar.");
        Assert.Equal(bar.BoxBounds.Right + PartF.BarGap, bar.CountBounds.Left, tolerance);
        Assert.Equal(bar.CountBounds.Right + PartF.BarGap, bar.PreviousBounds.Left, tolerance);
        Assert.Equal(bar.PreviousBounds.Right + PartF.BarGap, bar.NextBounds.Left, tolerance);
        Assert.Equal(bar.NextBounds.Right + PartF.BarGap, bar.CloseBounds.Left, tolerance);
        Assert.Equal(right - PartF.BarInset, bar.CloseBounds.Right, tolerance);
        foreach (var (bounds, what) in new[] { (bar.PreviousBounds, "previous"), (bar.NextBounds, "next"), (bar.CloseBounds, "close") })
        {
            Assert.True(Math.Abs(bounds.Width - PartF.BarIconButtonWidth) <= tolerance, $"The {what} button is {bounds.Width} wide, not {PartF.BarIconButtonWidth}.");
            Assert.True(bounds.Top >= bar.Bounds.Top && bounds.Bottom <= bar.Bounds.Bottom, $"The {what} button at {bounds} is not inside the bar {bar.Bounds}.");
        }
    }

    /// <summary>Asserts a rectangle the report states, each edge ±2.</summary>
    private static void AssertRect(Rect expected, Rect actual, string what)
    {
        var tolerance = PartF.PositionTolerance;
        Assert.True(
            Math.Abs(expected.Left - actual.Left) <= tolerance && Math.Abs(expected.Top - actual.Top) <= tolerance
                && Math.Abs(expected.Right - actual.Right) <= tolerance && Math.Abs(expected.Bottom - actual.Bottom) <= tolerance,
            $"{what} is at {actual}; the report says {expected} (±{tolerance}).");
    }

    /// <summary>
    /// Asserts where "history"'s rows are (part F's conventions): the graph control's top (116 with
    /// the bar closed, 152 open), the column headers' 28 above it, the working-changes row's centre
    /// and every commit row's, ±2.
    /// </summary>
    private static void AssertHistoryRowsAt(ShellDriver app, bool barOpen)
    {
        var graph = Graph(app);
        var top = app.BoundsOf(graph).Top;
        Assert.Equal(barOpen ? PartF.RowsTopOpen : PartF.RowsTopClosed, top, PartF.PositionTolerance);
        var headers = app.Find<Control>("ColumnHeaders");
        AssertRect(new Rect(app.BoundsOf(headers).Left, top - WindowLayout.ColumnHeaderHeight, app.BoundsOf(headers).Width, WindowLayout.ColumnHeaderHeight), app.BoundsOf(headers), "the column headers");
        Assert.Equal(PartF.WorkingCentre(barOpen), app.WorkingRowPoint().Y, PartF.PositionTolerance);
        for (var row = 0; row < PartF.H0.Count; row++)
        {
            Assert.Equal(PartF.HistoryRowCentre(row, barOpen), app.GraphRowPoint(row).Y, PartF.PositionTolerance);
        }
    }

    /// <summary>
    /// Asserts the search's marks read from the graph control (never from pixels, 15.2):
    /// <paramref name="marked"/> rows marked and every other commit row dimmed while a search is
    /// active; nothing marked or dimmed otherwise; the working-changes row neither (FD2).
    /// </summary>
    private static void AssertMarks(ShellDriver app, IReadOnlyList<int>? marked)
    {
        var marks = app.SearchMarks();
        var graph = Graph(app);
        var count = graph.Data!.Count;
        if (marked is null)
        {
            Assert.False(marks.IsActive, "A search marks the graph.");
            Assert.False(graph.Search.IsActive, "GraphRows.Search is active.");
            Assert.Empty(marks.Marked);
            Assert.Empty(marks.Dimmed);
            Assert.All(Enumerable.Range(0, count), row => Assert.Equal(GraphSearchRowState.None, graph.SearchStateOf(row)));
            return;
        }

        Assert.True(marks.IsActive, "No search marks the graph.");
        Assert.Equal(marked, marks.Marked);
        Assert.Equal(PartF.Others(count, marked), marks.Dimmed);
        for (var row = 0; row < count; row++)
        {
            Assert.Equal(marked.Contains(row) ? GraphSearchRowState.Marked : GraphSearchRowState.Dimmed, graph.SearchStateOf(row));
        }

        Assert.Equal(GraphSearchRowState.None, graph.SearchStateOf(-1));
    }

    /// <summary>The search calls (<c>git log --no-walk=unsorted …</c>) the app made since <paramref name="mark"/>.</summary>
    private static List<GitCallRecord> SearchCallsSince(ShellDriver app, int mark) =>
        [.. app.GitCalls().Skip(mark).Where(call => call.CommandText.StartsWith(PartF.SearchCallStart, StringComparison.Ordinal))];

    /// <summary>Types <paramref name="text"/> into the search box, which has the focus, and waits until its search has ended.</summary>
    private static async Task SearchForAsync(ShellDriver app, string text)
    {
        app.Type(text);
        await app.WaitForSearchAsync();
        app.Settle();
    }

    /// <summary>Replaces the search box's text with <paramref name="text"/> (the command modifier with A, then typing) and waits until its search has ended.</summary>
    private static async Task ReplaceSearchAsync(ShellDriver app, string text)
    {
        app.ReplaceText("SearchBox", text);
        await app.WaitForSearchAsync();
        app.Settle();
    }

    /// <summary>Clicks the toolbar's Search and waits until the bar shows with the focus in its box.</summary>
    private static async Task OpenSearchAsync(ShellDriver app)
    {
        app.Click(app.FindByAutomationId("SearchButton"));
        await app.WaitForAsync(() => app.SearchBar() is { BoxHasFocus: true }, "the search bar with the focus in its box");
    }

    /// <summary>Waits until <paramref name="row"/> is the selected row and the count reads <paramref name="count"/>.</summary>
    private static Task WaitForMatchAsync(ShellDriver app, int row, string count) =>
        app.WaitForAsync(() => Repo(app).SelectedIndex == row && app.SearchBar()?.Count == count, $"row {row} selected and the count \"{count}\"");

    /// <summary>
    /// Asserts the icons a control draws, left to right: each visible <c>Icon</c> inside
    /// <paramref name="scope"/> draws the app's geometry resource of that name (design 13.2's
    /// icon set, <c>Icons.axaml</c>).
    /// </summary>
    private static void AssertIcons(ShellDriver app, Visual scope, params string[] keys)
    {
        var icons = scope.GetVisualDescendants().OfType<Icon>().Where(icon => icon.IsEffectivelyVisible).OrderBy(icon => app.BoundsOf(icon).X).ToList();
        Assert.Equal(keys.Length, icons.Count);
        for (var i = 0; i < keys.Length; i++)
        {
            Assert.True(app.Window.TryFindResource(keys[i], out var geometry), $"The app has no icon {keys[i]}.");
            Assert.True(ReferenceEquals(geometry, icons[i].Data), $"Icon {i + 1} of {scope.GetType().Name} \"{(scope as Control)?.Name}\" is not {keys[i]}.");
        }
    }

    /// <summary>The search bar's icons (3.1): the mode button's chevron, previous, next and close.</summary>
    private static void AssertBarIcons(ShellDriver app)
    {
        AssertIcons(app, app.FindByAutomationId("SearchModeButton"), "IconChevronDown");
        AssertIcons(app, app.FindByAutomationId("SearchPrevious"), "IconChevronUp");
        AssertIcons(app, app.FindByAutomationId("SearchNext"), "IconChevronDown");
        AssertIcons(app, app.FindByAutomationId("SearchClose"), "IconClose");
    }

    // ---- Pixels ----

    /// <summary>A pixel of a picture at a point the report states, each channel within ±3 of <paramref name="expected"/> (part F's conventions).</summary>
    private static void AssertNear(Color expected, Screenshot shot, PartF.Sample at, string what)
    {
        var actual = shot.PixelAt(at.X, at.Y);
        var tolerance = PartF.ChannelTolerance;
        Assert.True(
            Math.Abs(actual.R - expected.R) <= tolerance && Math.Abs(actual.G - expected.G) <= tolerance && Math.Abs(actual.B - expected.B) <= tolerance,
            $"The {what} at ({at.X}, {at.Y}) is {actual}, expected {expected} ±{tolerance}.");
    }

    // ---- Menus ----

    /// <summary>Right-clicks a left-panel row, found by its path, and returns the menu it opens and the point clicked.</summary>
    private static async Task<(MenuSnapshot Menu, Point Pointer)> RightClickRefAsync(ShellDriver app, params string[] path)
    {
        var row = await app.RefRowAsync(path);
        var pointer = app.BoundsOf(row).Center;
        app.RightClick(pointer);
        return (app.Menu(), pointer);
    }

    /// <summary>Clicks a closed folder of the left panel open, found by its path.</summary>
    private static async Task OpenFolderAsync(ShellDriver app, params string[] path)
    {
        app.Click(await app.RefRowAsync(path));
        await app.WaitForAsync(() => app.RefRowModel(path).IsOpen, $"the folder {string.Join(" › ", path)} to open");
    }

    /// <summary>
    /// Asserts an open menu as the report writes it: its lines (<see cref="AssertMenu"/>), the
    /// highlighted item (D105), the height the report gives (±2) and 4a's shape
    /// (<see cref="AssertMenuShape"/>), each item's text in the primary colour, an enabled
    /// destructive one in the danger colour, a disabled one in the disabled colour, and no label cut.
    /// </summary>
    private static void AssertMenuAsReported(ShellDriver app, MenuSnapshot menu, string[] lines, string? highlighted, double height)
    {
        AssertMenu(menu, lines);
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
        Assert.All(presenter.GetVisualDescendants().OfType<TrimmedTextBlock>(), label => Assert.False(label.IsTrimmed, $"The menu's label \"{label.Text}\" is cut."));
    }

    /// <summary>Presses Esc and asserts that the menu closed and nothing opened.</summary>
    private static async Task CloseMenuAsync(ShellDriver app)
    {
        var mark = app.LogMark();
        app.PressKey(Avalonia.Input.Key.Escape);
        await AssertNoMenuAsync(app, mark);
    }

    /// <summary>The open tooltip with <paramref name="text"/> lies inside the window.</summary>
    private static void AssertToolTipInsideWindow(ShellDriver app, string text)
    {
        var tips = app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible && tip.Bounds.Width > 0).ToList();
        var tip = Assert.Single(tips, candidate => candidate.Content as string == text || (candidate.Content is Visual visual && TextsIn(visual).Contains(text)));
        var bounds = app.BoundsOf(tip);
        var size = app.Window.ClientSize;
        Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= size.Width && bounds.Bottom <= size.Height, $"The tooltip at {bounds} is not inside the {size.Width}x{size.Height} window.");
    }

    // ---- The left panel ----

    /// <summary>
    /// The left panel in part F's notation: a section <c>▾ Title N</c> (▸ when closed), a row
    /// indented two spaces per level, a container with ▾ or ▸, then <c> (head)</c>, <c> ↑N</c>,
    /// <c> ↓N</c>, <c> (dimmed)</c> for a name in the disabled colour, <c> [eye]</c> or
    /// <c> [solo]</c> for the toggle it shows, and <c> (selected)</c>.
    /// </summary>
    private static List<string> LeftPanelLines(ShellDriver app)
    {
        app.Settle();
        return [.. app.Session.Shell.ActiveTab.LeftPanel.Rows.Select(row => row switch
        {
            RefSectionRow section => $"{(section.IsOpen ? "▾" : "▸")} {section.Text} {section.CountText}",
            RefItemRow item => new string(' ', 2 * item.Depth)
                + (item.IsContainer ? (item.IsOpen ? "▾ " : "▸ ") : string.Empty)
                + item.Text
                + (item.IsHead ? " (head)" : string.Empty)
                + (item.Ahead > 0 ? " ↑" + item.AheadText : string.Empty)
                + (item.Behind > 0 ? " ↓" + item.BehindText : string.Empty)
                + (item.IsDimmed ? " (dimmed)" : string.Empty)
                + (item.ShowsHiddenToggle ? " [eye]" : string.Empty)
                + (item.ShowsSoloToggle ? " [solo]" : string.Empty)
                + (item.IsSelected ? " (selected)" : string.Empty),
            _ => row.Text,
        })];
    }

    /// <summary>Asserts the left panel's lines in part F's notation (<see cref="LeftPanelLines"/>).</summary>
    private static void AssertLeftPanel(ShellDriver app, IReadOnlyList<string> expected)
    {
        var lines = LeftPanelLines(app);
        Assert.True(
            lines.SequenceEqual(expected),
            "The left panel is not the expected one.\nExpected:\n  " + string.Join("\n  ", expected) + "\nActual:\n  " + string.Join("\n  ", lines));
    }

    /// <summary>Asserts that no left-panel row has the selection background (D114, FD19).</summary>
    private static void AssertNoRefHighlighted(ShellDriver app)
    {
        app.Settle();
        var selected = app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefItemRow>().Where(row => row.IsSelected).Select(row => row.Key).ToList();
        Assert.True(selected.Count == 0, "Left-panel rows are highlighted: " + string.Join(", ", selected));
    }

    /// <summary>
    /// Asserts how a left-panel row draws the graph's filter (8.3): its name's colour (the
    /// disabled colour for a hidden or dimmed row, the accent for HEAD's branch, else the primary),
    /// which toggle shows, 18 by 18 at the row's right, with its tooltip and icon colour.
    /// </summary>
    private static async Task AssertRefMarksAsync(ShellDriver app, Color name, string? toggle, params string[] path)
    {
        var button = await app.RefRowAsync(path);
        var marks = app.RefRowMarks(path);
        Assert.True(marks.NameColor == name, $"{string.Join(" › ", path)} is drawn in {marks.NameColor}, expected {name}.");
        Assert.Equal(toggle == "eye", marks.ShowsHiddenToggle);
        Assert.Equal(toggle == "solo", marks.ShowsSoloToggle);
        if (toggle is null)
        {
            Assert.Null(marks.ToggleBounds);
            return;
        }

        var bounds = marks.ToggleBounds!.Value;
        Assert.Equal(new Size(PartF.ToggleSize, PartF.ToggleSize), bounds.Size);
        var control = button.GetVisualDescendants().OfType<Button>().Single(candidate => candidate.Name == (toggle == "eye" ? "RefHiddenToggle" : "RefSoloToggle"));
        AssertIcons(app, control, toggle == "eye" ? "IconEyeOff" : "IconSolo");
        Assert.True(bounds.Center.X > app.BoundsOf(button).Center.X && bounds.Right <= app.BoundsOf(button).Right, $"The toggle at {bounds} is not at the right of the row {app.BoundsOf(button)}.");
        Assert.Equal(toggle == "eye" ? PartF.HiddenToggleToolTip : PartF.SoloToggleToolTip, marks.ToggleToolTip);
        if (toggle == "solo")
        {
            Assert.Equal(PaletteOf(app).Accent, marks.ToggleColor);
        }
    }

    // ---- The comparison ----

    /// <summary>The comparison in the right panel; fails while none is shown.</summary>
    private static CompareSnapshot Comparison(ShellDriver app) => app.Compare() ?? throw new Xunit.Sdk.XunitException("The right panel shows no comparison.");

    /// <summary>
    /// Asserts the comparison as the report gives it: the title, From and To with their links
    /// enabled or not (and the disabled one's tooltip), the counts (null for none), whether "All
    /// differences" shows and is checked, Swap always and Close for branches (with their tooltips),
    /// <c>Loading…</c> gone, no notice, "Changed files (N)" and the files in git's order.
    /// </summary>
    private static CompareSnapshot AssertComparison(
        ShellDriver app,
        string from,
        string to,
        string? counts,
        bool branches,
        bool allDifferences,
        IReadOnlyList<string> files,
        bool toEnabled = true)
    {
        var compare = Comparison(app);
        Assert.Equal(PartF.CompareTitle, compare.Title);
        Assert.Equal((from, to), (compare.From, compare.To));
        Assert.True(compare.FromEnabled, "The From link is disabled.");
        Assert.Null(compare.FromToolTip);
        Assert.True(compare.ToEnabled == toEnabled, $"The To link is {(compare.ToEnabled ? "enabled" : "disabled")}.");
        Assert.Equal(toEnabled ? null : PartF.NotInGraph, compare.ToToolTip);
        Assert.Equal(counts, compare.Counts);
        Assert.Equal(branches, compare.AllDifferencesShown);
        Assert.Equal(allDifferences, compare.AllDifferencesChecked);
        Assert.True(compare.SwapShown, "The comparison shows no swap button.");
        Assert.Equal(branches, compare.CloseShown);
        Assert.False(compare.IsLoading, "The comparison still shows Loading….");
        Assert.Null(compare.Notice);
        Assert.Equal(PartF.ChangedFiles(files.Count), compare.FilesTitle);
        Assert.Equal(files, compare.Files.Select(FileRow).ToList());
        var view = app.FindByAutomationId(ShellDriver.CompareId);
        AssertTip(app.FindByAutomationId(view, "CompareSwap"), PartF.SwapToolTip);
        AssertTip(app.FindByAutomationId(view, "CompareClose"), PartF.CompareCloseToolTip);
        AssertIcons(app, app.FindByAutomationId(view, "CompareSwap"), "IconArrowDown", "IconArrowUp");
        if (branches)
        {
            AssertIcons(app, app.FindByAutomationId(view, "CompareClose"), "IconClose");
        }

        Assert.Equal(PartF.AllDifferences, ((CheckBox)app.FindByAutomationId(view, "CompareAllDifferences")).Content);
        Assert.True(((Button)app.FindByAutomationId(view, "CompareFlatButton")).Classes.Contains("selected"), "The comparison's list is not flat.");
        Assert.False(((Button)app.FindByAutomationId(view, "CompareTreeButton")).Classes.Contains("selected"), "The comparison's list is a tree.");
        Assert.DoesNotContain(view.GetVisualDescendants().OfType<Control>(), control => control.IsEffectivelyVisible && AutomationProperties.GetName(control) == "All files");
        return compare;
    }

    /// <summary>The comparison's file row whose diff is open, by its path, or null when none has the selection.</summary>
    private static string? SelectedCompareFile(ShellDriver app) =>
        Repo(app).Comparison?.Files.FirstOrDefault(row => row.IsFile && row.IsSelected)?.Key;

    // ---- The diff ----

    /// <summary>Waits until the diff of <paramref name="path"/> shows its text rows laid out.</summary>
    private static Task WaitForTextDiffAsync(ShellDriver app, string path) =>
        app.WaitForAsync(
            () => Repo(app).Diff is { ShowsText: true } diff && diff.Target.Path == path
                && app.Window.GetVisualDescendants().OfType<DiffTextView>().Any(view => view.Name == "DiffText" && view.IsEffectivelyVisible && view.Rows.Count > 0 && view.RowBounds(0) is not null),
            $"the text diff of {path}",
            OutsideChangeTimeout);

    /// <summary>
    /// The open diff's rows as part F writes them: hunk headers as git wrote them, every other row
    /// "K old/new text" (K: C context, R removed, A added; "-" for no number), the text as git has it.
    /// </summary>
    private static List<string> DiffRows(ShellDriver app) =>
        [.. app.Find<DiffTextView>("DiffText").Rows.Select(row => row.Kind switch
        {
            DiffRowKind.HunkHeader => row.HeaderText ?? string.Empty,
            _ when row.Left is { } cell => $"{Letter(cell.Kind)} {Number(cell.OldNumber)}/{Number(cell.NewNumber)} {cell.Text}",
            _ => "?",
        })];

    private static string Letter(DiffLineKind kind) => kind switch
    {
        DiffLineKind.Added => "A",
        DiffLineKind.Removed => "R",
        _ => "C",
    };

    private static string Number(int? number) => number?.ToString(CultureInfo.InvariantCulture) ?? "-";

    /// <summary>
    /// Asserts phase 2's diff view as a comparison opens it (6.4): the status letter, the file's
    /// name and folder, the side label, the close button, Inline selected and Side by side, no
    /// action on the file or its lines, no hunk button, and the rows.
    /// </summary>
    private static void AssertCompareDiff(ShellDriver app, string letter, string name, string folder, string sideLabel, IReadOnlyList<string> rows)
    {
        var diff = Repo(app).Diff ?? throw new Xunit.Sdk.XunitException("No diff is open.");
        Assert.Equal((letter, name, folder), (diff.StatusLetter, diff.FileName, diff.Folder));
        Assert.Equal(sideLabel, ((TextBlock)app.FindByAutomationId("DiffSideLabel")).Text);
        Assert.True(app.FindByAutomationId("CloseDiffButton").IsEffectivelyVisible, "The diff has no close button.");
        Assert.True(((Button)app.FindByAutomationId("InlineDiffButton")).Classes.Contains("selected"), "Inline is not selected.");
        Assert.True(app.FindByAutomationId("SideBySideDiffButton").IsEffectivelyVisible, "The diff has no Side by side.");
        Assert.False(diff.ShowsFileStageDiscard || diff.ShowsFileUnstage || diff.ShowsLineStageDiscard || diff.ShowsLineUnstage, "The diff's toolbar shows an action.");
        Assert.Equal(HunkActions.None, diff.HunkActions);
        Assert.Equal(rows, DiffRows(app));
    }

    // ---- Git ----

    /// <summary>Asserts the call <paramref name="command"/> made since <paramref name="mark"/>: its exit code, and returns it.</summary>
    private static GitCallRecord AssertCall(ShellDriver app, int mark, string command, int exitCode = 0)
    {
        var call = AssertCalled(app, mark, command);
        Assert.True(call.ExitCode == exitCode, $"\"{command}\" exited {call.ExitCode}; the report says {exitCode}.\n{call.StandardError}{call.StandardOutput}");
        return call;
    }

    /// <summary>The lines a call printed on its standard output, without empty ones.</summary>
    private static List<string> OutputLines(GitCallRecord call) =>
        [.. call.StandardOutput.Split(['\r', '\n']).Where(line => line.Length > 0)];

    /// <summary>Plain git's output as lines (none for an empty output).</summary>
    private static async Task<string[]> GitLinesAsync(TempRepo repo, params string[] arguments)
    {
        var text = (await repo.GitAsync(arguments)).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
        return text.Length == 0 ? [] : text.Split('\n');
    }

    /// <summary>Every ref and what it points at, as <c>git for-each-ref</c> lists them.</summary>
    private static async Task<List<string>> RefsAsync(TempRepo repo) =>
        [.. await GitLinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)")];

    /// <summary>The repository's own configuration file, as git wrote it.</summary>
    private static string ConfigFile(TempRepo repo) => File.ReadAllText(Path.Combine(repo.Path, ".git", "config"));

    /// <summary>Asserts that git is as built: the status, every ref and the configuration file unchanged.</summary>
    private static async Task AssertGitUnchangedAsync(TempRepo repo, IReadOnlyList<string> status, IReadOnlyList<string> refs, string config)
    {
        Assert.Equal(status, await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Equal(config, ConfigFile(repo));
    }

    // ---- The session ----

    /// <summary>The session's graph filters as the data folder holds them (<c>JsonSessionStore</c>).</summary>
    private static IReadOnlyList<RepositoryGraphFilter> SavedFilters(TempDirectory data) =>
        new JsonSessionStore(new AppPaths(data.Path).SessionFile).Current.GraphFilters;

    /// <summary>
    /// Asserts the session's filter entry for <paramref name="repo"/>: the only one, with these
    /// hidden and soloed branches, no hidden remote, the upstreams that came with them and the
    /// remote origin (LastUsed not asserted); or no entry at all when both are empty (FD14).
    /// </summary>
    private static async Task AssertSavedFilterAsync(TempDirectory data, TempRepo repo, IReadOnlyList<string> hidden, IReadOnlyList<string> soloed, IReadOnlyList<BranchUpstream> upstreams)
    {
        var filters = SavedFilters(data);
        if (hidden.Count == 0 && soloed.Count == 0)
        {
            Assert.Empty(filters);
            return;
        }

        var filter = Assert.Single(filters);
        Assert.Equal(await WorkingTreeAsync(repo), filter.RepositoryPath);
        Assert.Equal(hidden, filter.Hidden);
        Assert.Equal(soloed, filter.Soloed);
        Assert.Empty(filter.HiddenRemotes);
        Assert.Equal(upstreams, filter.Upstreams);
        Assert.Equal(["origin"], filter.Remotes);
    }

    // ---- The graph's lines ----

    /// <summary>Asserts which lines of the layout are dashed: exactly <paramref name="dashed"/>, each in its row with its kind and lanes (design 10).</summary>
    private static void AssertDashedLines(ShellDriver app, IReadOnlyList<PartF.LayoutLine> dashed)
    {
        var data = Graph(app).Data!;
        var actual = Enumerable.Range(0, data.Count)
            .SelectMany(row => data.RowAt(row).Lines.Where(line => line.IsDashed).Select(line => new PartF.LayoutLine(row, line.Kind.ToString(), line.FromLane, line.ToLane)))
            .OrderBy(line => line.Row).ThenBy(line => line.Kind, StringComparer.Ordinal).ThenBy(line => line.From)
            .ToList();
        var expected = dashed.OrderBy(line => line.Row).ThenBy(line => line.Kind, StringComparer.Ordinal).ThenBy(line => line.From).ToList();
        Assert.True(actual.SequenceEqual(expected), "The dashed lines are not the expected ones.\nExpected:\n  " + string.Join("\n  ", expected) + "\nActual:\n  " + string.Join("\n  ", actual));
    }

    /// <summary>Samples the stretches the report gives: dashed ones by FD15's rule, solid ones within 3 per channel of the lane colour.</summary>
    private static void AssertStretches(ShellDriver app, Screenshot shot, IReadOnlyList<PartF.Stretch> stretches)
    {
        var palette = PaletteOf(app);
        foreach (var stretch in stretches)
        {
            var colour = palette.Lane(stretch.Colour);
            if (stretch.MinimumDashes > 0)
            {
                ShellDriver.AssertDashedPixels(shot, stretch.X, stretch.Top, stretch.Bottom, colour, palette.Window, stretch.MinimumDashes, stretch.What);
            }
            else
            {
                ShellDriver.AssertSolidPixels(shot, stretch.X, stretch.Top, stretch.Bottom, colour, stretch.What);
            }
        }
    }

    /// <summary>The graph's node or ring of row <paramref name="row"/> is at the point the report states (±2): lane centre x 406 + 16 L, row centre y.</summary>
    private static void AssertNodeAt(ShellDriver app, int row, int lane, PartF.Sample at)
    {
        var graph = Graph(app);
        var origin = app.BoundsOf(graph).TopLeft;
        Assert.Equal(at.X, origin.X + graph.LaneCenterX(lane), PartF.PositionTolerance);
        Assert.Equal(at.Y, origin.Y + graph.RowBounds(row).Center.Y, PartF.PositionTolerance);
    }

    /// <summary>The graph's load with the filter's revisions since <paramref name="mark"/>: the last such call.</summary>
    private static GitCallRecord LastGraphLoad(ShellDriver app, int mark) =>
        app.GitCalls().Skip(mark).LastOrDefault(call => call.CommandText.StartsWith(PartF.GraphLogStart, StringComparison.Ordinal))
        ?? throw new Xunit.Sdk.XunitException("The graph was not loaded. The calls:\n" + string.Join('\n', CallsSince(app, mark)));
}
