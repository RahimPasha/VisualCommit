// Part A's own helpers (docs/test-reports/phase-3.md, part A): reading the graph's rows, the left
// panel and the file lists in the report's notation, the menu's look as "The menu" gives it, the
// name dialog, the toolbar, and git's state read back with plain git. Only part A's writer edits
// this file; what every part shares is in Phase3Checks.Support.cs.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Graph;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksA
{
    /// <summary>The active tab's repository.</summary>
    private static RepositoryViewModel Repo(ShellDriver app) => app.Session.Shell.ActiveTab.Repository!;

    /// <summary>
    /// The graph's commit rows in the notation of part A's expectations:
    /// <c>&lt;sha7&gt; &lt;subject&gt; [&lt;labels&gt;] &lt;lane&gt;/&lt;colour&gt;</c>, then
    /// <c> ends &lt;lanes&gt;</c> for the lanes that end in its node (lines into the node from
    /// another lane) and <c> out &lt;lane&gt;/&lt;colour&gt;</c> for a line from its node to another
    /// lane (a merge's second parent). Labels are <c>Kind:Text</c> in phase 1's order, all of them,
    /// also those the column has no room for.
    /// </summary>
    private static List<string> GraphLines(ShellDriver app)
    {
        var data = Repo(app).Graph;
        var lines = new List<string>();
        for (var row = 0; row < data.Count; row++)
        {
            var commit = data.CommitAt(row);
            var graph = data.RowAt(row);
            var labels = string.Join(", ", data.LabelsAt(row).Select(label => $"{label.Kind}:{label.Text}"));
            var text = $"{commit.Sha[..7]} {commit.Subject} [{labels}] {graph.NodeLane}/{graph.NodeColor}";
            var ends = graph.Lines.Where(line => line.Kind == GraphLineKind.IntoNode && line.FromLane != graph.NodeLane).Select(line => line.FromLane).Order().ToList();
            if (ends.Count > 0)
            {
                text += " ends " + string.Join(",", ends);
            }

            foreach (var line in graph.Lines.Where(line => line.Kind == GraphLineKind.OutOfNode && line.ToLane != graph.NodeLane))
            {
                text += string.Create(CultureInfo.InvariantCulture, $" out {line.ToLane}/{line.Color}");
            }

            lines.Add(text);
        }

        return lines;
    }

    /// <summary>
    /// Asserts the graph's rows (<see cref="GraphLines"/>), with a message that shows both lists. An
    /// expected row that starts with "???????" stands for a commit the app made, whose id the report
    /// does not give: any id matches there.
    /// </summary>
    private static void AssertGraph(ShellDriver app, IReadOnlyList<string> expected)
    {
        var actual = GraphLines(app);
        static bool Matches(string want, string have) =>
            want.StartsWith("???????", StringComparison.Ordinal) ? have.Length >= 7 && want[7..] == have[7..] : want == have;
        Assert.True(
            actual.Count == expected.Count && actual.Zip(expected).All(pair => Matches(pair.Second, pair.First)),
            "The graph is not the expected one.\nExpected:\n  " + string.Join("\n  ", expected) + "\nActual:\n  " + string.Join("\n  ", actual));
    }

    /// <summary>
    /// The left panel as part A writes it: a section <c>▾ Title N</c> (▸ when closed), a row indented
    /// two spaces per level, a container with ▾ or ▸, the head branch with <c> (head)</c>, then
    /// <c> ↑N</c> and <c> ↓N</c>, and <c> (selected)</c> for the row with the selection background.
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
                + (item.IsSelected ? " (selected)" : string.Empty),
            _ => row.Text,
        })];
    }

    /// <summary>Asserts the left panel (<see cref="LeftPanelLines"/>).</summary>
    private static void AssertLeftPanel(ShellDriver app, IReadOnlyList<string> expected)
    {
        var actual = LeftPanelLines(app);
        Assert.True(
            actual.SequenceEqual(expected),
            "The left panel is not the expected one.\nExpected:\n  " + string.Join("\n  ", expected) + "\nActual:\n  " + string.Join("\n  ", actual));
    }

    /// <summary>
    /// Asserts which left-panel ref has the selection background: the model's <c>IsSelected</c> and
    /// the row button's background in the selection colour, for exactly the row at
    /// <paramref name="path"/>, or for none when it is null.
    /// </summary>
    private static async Task AssertRefHighlightedAsync(ShellDriver app, params string[]? path)
    {
        var selected = app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefItemRow>().Where(row => row.IsSelected).ToList();
        if (path is null || path.Length == 0)
        {
            Assert.True(selected.Count == 0, "Left-panel rows have the selection background: " + string.Join(", ", selected.Select(row => row.Key)));
            Assert.DoesNotContain(
                app.Find<ItemsControl>("RefList").GetVisualDescendants().OfType<Button>(),
                button => button.IsEffectivelyVisible && button.Classes.Contains("selected"));
            return;
        }

        var expected = app.RefRowModel(path);
        Assert.True(selected.Count == 1 && ReferenceEquals(selected[0], expected), $"Expected only {string.Join(" › ", path)} highlighted; highlighted: {string.Join(", ", selected.Select(row => row.Key))}.");
        var button = (Button)await app.RefRowAsync(path);
        Assert.Equal(PaletteOf(app).Selection, (button.Background as ISolidColorBrush)?.Color);
    }

    /// <summary>The colour a left-panel ref's name is drawn in (the accent colour for the head branch).</summary>
    private static async Task<Color?> RefNameColourAsync(ShellDriver app, params string[] path)
    {
        var button = await app.RefRowAsync(path);
        var name = button.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "RefName");
        return (name.Foreground as ISolidColorBrush)?.Color;
    }

    /// <summary>
    /// Asserts that commit row <paramref name="row"/> alone is selected (the graph's selection and
    /// the view model's), and, given a picture taken with the pointer away from the rows, that its
    /// background is the selection colour, sampled 2 inside the row's left end, where no label reaches.
    /// </summary>
    private static void AssertRowSelected(ShellDriver app, int row, Screenshot? screenshot = null)
    {
        var repository = Repo(app);
        Assert.Equal(row, repository.SelectedIndex);
        Assert.Equal(repository.Graph.CommitAt(row).Sha, repository.Selection.Single);
        Assert.False(repository.IsWorkingRowSelected);
        if (screenshot is not null)
        {
            var graph = Graph(app);
            var point = graph.TranslatePoint(new Point(2, graph.RowBounds(row).Y + 2), app.Window)!.Value;
            AssertColour(PaletteOf(app).Selection, screenshot, point, $"selection background of row {row}");
        }
    }

    /// <summary>Waits until the details panel shows the commit <paramref name="subject"/>.</summary>
    private static Task WaitForDetailsAsync(ShellDriver app, string subject) =>
        app.WaitForAsync(
            () => app.FindByAutomationId("RightPanel").GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.IsEffectivelyVisible && text.Text == subject && AutomationProperties.GetAutomationId(text) == "DetailsSubject"),
            $"the details of \"{subject}\"");

    /// <summary>
    /// Asserts the open menu as "The menu" gives its look, beyond its lines (<see cref="AssertMenu"/>):
    /// the header in the secondary colour; each item's text in the primary colour, an enabled
    /// destructive one in the danger colour, a disabled one in the disabled colour; the menu 200 to
    /// 520 wide with no label cut; the height by 4a's formula and 4 inside the window
    /// (<see cref="AssertMenuShape"/>); and in <paramref name="screenshot"/> the highlighted item's
    /// background and the menu's own background (sampled where no text reaches).
    /// </summary>
    private static void AssertMenuLook(ShellDriver app, MenuSnapshot menu, Screenshot? screenshot)
    {
        var palette = PaletteOf(app);
        var presenter = app.OpenMenuView ?? throw new Xunit.Sdk.XunitException("No menu is open.");
        var header = presenter.GetVisualDescendants().OfType<Avalonia.Controls.MenuItem>().Single(item => item.Classes.Contains(MenuLayer.HeaderClass));
        Assert.Equal(palette.TextSecondary, ((header.Header as TextBlock)?.Foreground as ISolidColorBrush)?.Color);
        foreach (var item in menu.Items)
        {
            var colour = !item.IsEnabled ? palette.TextDisabled : item.IsDestructive ? palette.Danger : palette.TextPrimary;
            Assert.True(item.TextColor == colour, $"\"{item.Label}\" is drawn in {item.TextColor}, expected {colour}.");
        }

        Assert.InRange(menu.Bounds.Width, MenuLayer.MinMenuWidth - 0.5, MenuLayer.MaxMenuWidth + 0.5);
        var labels = presenter.GetVisualDescendants().OfType<TrimmedTextBlock>().ToList();
        Assert.Equal(menu.Items.Count, labels.Count);
        Assert.All(labels, label => Assert.False(label.IsTrimmed, $"\"{label.Text}\" is cut."));
        AssertMenuShape(app, menu);
        if (screenshot is null)
        {
            return;
        }

        // Samples where no text reaches, and not under an open tooltip (a rested pointer's tooltip
        // opens below it, over the next item).
        var tips = app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible).Select(app.BoundsOf).ToList();
        Point Free(params Point[] candidates) =>
            candidates.FirstOrDefault(point => !tips.Any(tip => tip.Contains(point)), candidates[0]);
        AssertColour(palette.Panel, screenshot, Free(new Point(menu.Bounds.Right - 3, menu.Bounds.Top + 16), new Point(menu.Bounds.Left + 3, menu.Bounds.Top + 16)), "menu's background");
        if (menu.Highlighted is { } highlighted)
        {
            var point = Free(new Point(highlighted.Bounds.Right - 4, highlighted.Bounds.Center.Y), new Point(highlighted.Bounds.Left + 4, highlighted.Bounds.Center.Y), new Point(highlighted.Bounds.Left + 4, highlighted.Bounds.Top + 2));
            Assert.False(tips.Any(tip => tip.Contains(point)), $"A tooltip covers the highlighted \"{highlighted.Label}\".");
            AssertColour(palette.Hover, screenshot, point, $"highlight of \"{highlighted.Label}\"");
        }
    }

    /// <summary>
    /// Asserts where a menu opened by the pointer at <paramref name="pointer"/> is, by 4a's placement
    /// rule ("The menu", Placement), for a step that gives no position of its own: its top-left 2
    /// right of and 2 below the pointer; flipped to the left of the pointer (its right edge 2 left of
    /// it) when it would cross the window's right edge, above it when it would cross the bottom;
    /// then kept 4 inside the window (±2).
    /// </summary>
    private static void AssertMenuPlaced(ShellDriver app, MenuSnapshot menu, Point pointer)
    {
        var window = app.Window.ClientSize;
        var size = menu.Bounds.Size;
        var gap = WindowLayout.MenuPointerGap;
        var inset = WindowLayout.MenuEdgeInset;
        var x = pointer.X + gap + size.Width > window.Width ? pointer.X - gap - size.Width : pointer.X + gap;
        var y = pointer.Y + gap + size.Height > window.Height ? pointer.Y - gap - size.Height : pointer.Y + gap;
        x = Math.Max(Math.Min(x, window.Width - inset - size.Width), inset);
        y = Math.Max(Math.Min(y, window.Height - inset - size.Height), inset);
        Assert.True(
            Math.Abs(menu.Bounds.X - x) <= 2 && Math.Abs(menu.Bounds.Y - y) <= 2,
            $"The menu's top-left is {menu.Bounds.TopLeft}; 4a places a {size} menu opened at {pointer} at ({x}, {y}).");
    }

    /// <summary>The name dialog as the report gives it (title, context, the boxes, the option, the buttons).</summary>
    private sealed record NameDialogExpected(
        string Title,
        string? Context,
        string? NamePlaceholder,
        string? MessagePlaceholder,
        bool MessageMultiline,
        string? Option,
        bool OptionChecked,
        string ConfirmText,
        bool ConfirmEnabled);

    /// <summary>
    /// Waits for the name dialog (<c>NameDialog</c>, card 400 wide over the backdrop) and asserts it
    /// as the report gives it: its title, context, the empty first box with the keyboard focus and
    /// its placeholder, no message (<c>NameError</c> empty, D114), the message box when it has one
    /// (3 lines high or one), the option, "Cancel" and the confirm button, enabled or not.
    /// </summary>
    private static async Task AssertNameDialogAsync(ShellDriver app, NameDialogExpected expected)
    {
        await app.WaitForDialogAsync<App.ViewModels.Dialogs.NameDialogViewModel>();
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The name dialog has no view.");
        Assert.Equal("NameDialog", AutomationProperties.GetAutomationId(view));
        Assert.Equal(400, view.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "NameDialogCard").Bounds.Width, 0.5);
        var backdrop = view.GetVisualDescendants().OfType<Panel>().First(panel => panel.Classes.Contains("dialogBackdrop"));
        Assert.Equal(PaletteOf(app).Backdrop, (backdrop.Background as ISolidColorBrush)?.Color);
        Assert.Equal(expected.Title, ((TextBlock)app.FindByAutomationId(view, "NameDialogTitle")).Text);
        var context = (TextBlock)app.FindByAutomationId(view, "NameDialogContext");
        Assert.Equal(expected.Context is not null, context.IsEffectivelyVisible);
        if (expected.Context is not null)
        {
            Assert.Equal(expected.Context, context.Text);
        }

        var name = (TextBox)app.FindByAutomationId(view, "NameBox");
        var message = (TextBox)app.FindByAutomationId(view, "MessageBox");
        Assert.Equal(expected.NamePlaceholder is not null, name.IsEffectivelyVisible);
        Assert.Equal(expected.MessagePlaceholder is not null, message.IsEffectivelyVisible);
        var first = expected.NamePlaceholder is not null ? name : message;
        Assert.True(string.IsNullOrEmpty(first.Text), $"The first box holds \"{first.Text}\".");
        Assert.Equal(expected.NamePlaceholder ?? expected.MessagePlaceholder, first.PlaceholderText);
        await app.WaitForAsync(() => first.IsFocused, "the dialog's first box to have the keyboard focus");
        if (expected.MessagePlaceholder is not null)
        {
            Assert.Equal(expected.MessagePlaceholder, message.PlaceholderText);
            Assert.True(string.IsNullOrEmpty(message.Text));
            Assert.Equal(expected.MessageMultiline ? 66 : message.Bounds.Height, message.Bounds.Height, 0.5);
            Assert.Equal(expected.MessageMultiline, message.AcceptsReturn);
        }

        var error = (TextBlock)app.FindByAutomationId(view, "NameError");
        Assert.False(error.IsEffectivelyVisible && !string.IsNullOrEmpty(error.Text), $"NameError shows \"{error.Text}\".");
        var option = (CheckBox)app.FindByAutomationId(view, "NameDialogOption");
        Assert.Equal(expected.Option is not null, option.IsEffectivelyVisible);
        if (expected.Option is not null)
        {
            Assert.Equal(expected.Option, option.Content);
            Assert.Equal(expected.OptionChecked, option.IsChecked);
        }

        var cancel = (Button)app.FindByAutomationId(view, "CancelButton");
        Assert.Equal("Cancel", cancel.Content);
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Equal(expected.ConfirmText, confirm.Content);
        Assert.Equal(expected.ConfirmEnabled, confirm.IsEffectivelyEnabled);
    }

    /// <summary>The toolbar's buttons that depend on the repository, in order, with their automation ids.</summary>
    private static readonly string[] RepositoryButtons = ["FetchButton", "PullButton", "PushButton", "BranchButton", "StashButton", "PopButton"];

    /// <summary>
    /// Asserts the toolbar as 4d gives it: the buttons in <paramref name="disabled"/> disabled, each
    /// with its tooltip (<c>ToolTip.GetTip</c>, also shown while disabled) and its icon and label in
    /// the disabled colour; the other repository buttons enabled in the primary colour with no
    /// tooltip; Undo, Redo and Search disabled without a tooltip; Theme enabled.
    /// </summary>
    private static void AssertToolbar(ShellDriver app, IReadOnlyDictionary<string, string> disabled)
    {
        var palette = PaletteOf(app);
        foreach (var id in RepositoryButtons)
        {
            var button = ToolbarButton(app, id);
            var label = button.GetVisualDescendants().OfType<TextBlock>().Single();
            var icon = button.GetVisualDescendants().OfType<Icon>().Single();
            if (disabled.TryGetValue(id, out var reason))
            {
                Assert.False(button.IsEffectivelyEnabled, $"{id} is enabled.");
                AssertTip(button, reason);
                Assert.True(ToolTip.GetShowOnDisabled(button), $"{id} does not show its tooltip while disabled.");
                Assert.Equal(palette.TextDisabled, (label.Foreground as ISolidColorBrush)?.Color);
                Assert.Equal(palette.TextDisabled, (icon.Foreground as ISolidColorBrush)?.Color);
            }
            else
            {
                Assert.True(button.IsEffectivelyEnabled, $"{id} is disabled: \"{ToolTip.GetTip(button)}\".");
                AssertTip(button, null);
                Assert.Equal(palette.TextPrimary, (label.Foreground as ISolidColorBrush)?.Color);
                Assert.Equal(palette.TextPrimary, (icon.Foreground as ISolidColorBrush)?.Color);
            }
        }

        foreach (var id in new[] { "UndoButton", "RedoButton", "SearchButton" })
        {
            var button = (Control)app.FindByAutomationId(id);
            Assert.False(button.IsEffectivelyEnabled, $"{id} is enabled.");
            AssertTip(button, null);
        }

        Assert.True(app.FindByAutomationId("ThemeSwitch").IsEffectivelyEnabled);
    }

    /// <summary>
    /// A stage or details file list's rows as text: status letter, name, folder (flat) and where a
    /// rename came from, as phase 2's checks write them ("R helpers.py src renamed from src/util.py").
    /// </summary>
    private static List<string> FileRows(ShellDriver app, string list)
    {
        app.Settle();
        var rows = (IEnumerable<ChangedFileRow>)(app.Find<ItemsControl>(list).ItemsSource ?? Array.Empty<ChangedFileRow>());
        return [.. rows.Where(row => row.IsFile).Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder, row.OriginText }.Where(part => !string.IsNullOrWhiteSpace(part))))];
    }

    /// <summary>Waits until the stage panel shows, with these numbers of files in its two lists.</summary>
    private static Task WaitForStagePanelAsync(ShellDriver app, int unstaged, int staged) =>
        app.WaitForAsync(
            () => app.FindByAutomationId("StagePanel").IsEffectivelyVisible
                && app.Find<TextBlock>("UnstagedTitle").Text == $"Unstaged files ({unstaged})"
                && app.Find<TextBlock>("StagedTitle").Text == $"Staged files ({staged})",
            $"the stage panel with {unstaged} unstaged and {staged} staged files");

    /// <summary>The diff's text view.</summary>
    private static DiffTextView DiffText(ShellDriver app) => app.Find<DiffTextView>("DiffText");

    /// <summary>Waits until the open diff of <paramref name="path"/> shows its text rows laid out.</summary>
    private static Task WaitForTextDiffAsync(ShellDriver app, string path) =>
        app.WaitForAsync(
            () => Repo(app).Diff is { ShowsText: true } diff && diff.Target.Path == path
                && app.Window.GetVisualDescendants().OfType<DiffTextView>().Any(view => view.Name == "DiffText" && view.IsEffectivelyVisible && view.Rows.Count > 0 && view.RowBounds(0) is not null),
            $"the text diff of {path}");

    /// <summary>
    /// The diff's rows as phase 2's checks write them: "K old/new text" inline (K the kind's letter,
    /// "-" for no number), hunk headers as git wrote them, texts without their indentation.
    /// </summary>
    private static List<string> DiffRows(ShellDriver app) =>
        [.. DiffText(app).Rows.Select(row => row.Kind switch
        {
            DiffRowKind.HunkHeader => row.HeaderText ?? string.Empty,
            _ when row.Left is { } only => Join($"{Letter(only)} {Number(only.OldNumber)}/{Number(only.NewNumber)}", only.Text.Trim()),
            _ => "?",
        })];

    /// <summary>A point on the text of diff row <paramref name="row"/> (a line, not its numbers), in the window.</summary>
    private static Point DiffTextPoint(ShellDriver app, int row)
    {
        var view = DiffText(app);
        var bounds = view.RowBounds(row) ?? throw new InvalidOperationException($"Diff row {row} is not in view.");
        return view.TranslatePoint(new Point(bounds.X + 240, bounds.Center.Y), app.Window)!.Value;
    }

    /// <summary>The index of the diff's row for new line <paramref name="newNumber"/> (or old line, when the line was removed).</summary>
    private static int DiffRowOfLine(ShellDriver app, int newNumber) =>
        DiffText(app).Rows.ToList().FindIndex(row => row.Kind != DiffRowKind.HunkHeader && row.Left is { } cell && (cell.NewNumber ?? cell.OldNumber) == newNumber);

    /// <summary>Where to click a line's number, in window coordinates.</summary>
    private static Point LineNumberPoint(ShellDriver app, DiffLineRef line) =>
        app.BoundsOf(DiffText(app)).TopLeft + (DiffText(app).LineNumberPoint(line) ?? throw new InvalidOperationException($"Line {line} is not in view."));

    private static string Letter(DiffCell cell) => cell.Kind switch
    {
        DiffLineKind.Added => "A",
        DiffLineKind.Removed => "R",
        _ => "C",
    };

    private static string Number(int? number) => number?.ToString(CultureInfo.InvariantCulture) ?? "-";

    private static string Join(string head, string text) => text.Length == 0 ? head : $"{head} {text}";

    /// <summary>Plain git's output, trimmed, line ends as line feeds.</summary>
    private static async Task<string> GitTextAsync(TempRepo repo, params string[] arguments) =>
        (await repo.GitAsync(arguments)).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    /// <summary>Plain git's output as lines (none for an empty output).</summary>
    private static async Task<string[]> GitLinesAsync(TempRepo repo, params string[] arguments)
    {
        var text = await GitTextAsync(repo, arguments);
        return text.Length == 0 ? [] : text.Split('\n');
    }

    /// <summary>
    /// <c>git check-ignore -v &lt;path&gt;</c>'s output: plain git, without the runner's
    /// <c>GIT_LITERAL_PATHSPECS</c>, which check-ignore refuses.
    /// </summary>
    private static async Task<string> CheckIgnoreAsync(TempRepo repo, string path)
    {
        var command = new VisualCommit.Core.Git.GitCommand("check-ignore", "-v", path)
        {
            WorkingDirectory = repo.Path,
            Environment = new Dictionary<string, string?>(repo.Environment) { ["GIT_LITERAL_PATHSPECS"] = null },
        };
        var result = await repo.Runner.RunAsync(command);
        return result.EnsureSuccess(command).StandardOutput.Trim();
    }

    /// <summary>
    /// What "git's state is unchanged" compares: every ref and what it points at, HEAD, the index,
    /// the working tree's status with untracked files, the stash list and the config.
    /// </summary>
    private static async Task<string> GitStateAsync(TempRepo repo) =>
        string.Join(
            "\n---\n",
            await GitTextAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)"),
            await GitTextAsync(repo, "rev-parse", "HEAD"),
            (await repo.GitAllowingFailureAsync("symbolic-ref", "HEAD")).StandardOutput.Trim(),
            await GitTextAsync(repo, "ls-files", "--stage"),
            await GitTextAsync(repo, "status", "--porcelain", "--untracked-files=all"),
            await GitTextAsync(repo, "stash", "list"),
            await GitTextAsync(repo, "config", "--local", "--list"));

    /// <summary>A full path normalised as part A's conventions say for paths: full, without a trailing separator.</summary>
    private static string FullPath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
