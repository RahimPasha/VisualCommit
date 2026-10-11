// What part D's checks share beyond Phase3Checks.Support.cs: the graph and the left panel as the
// report's "Scenario graphs" write them, the status bar's progress, git's state read back with
// plain git, and finding a dialog's own rows and buttons the way a user would see them.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksD
{
    /// <summary>
    /// Asserts the graph as the report lists it, row by row from the top: each row's subject, node
    /// lane and colour, whether it is a merge, its labels in drawing order (" ✓" marks the branch
    /// HEAD is on), what the Branch / Tag column draws when the report names a "+N", and the date
    /// when the report gives one. Then the number of rows, the lane count and the Graph column's
    /// width (16 per lane plus 16, at least 48), and whether the Date column shows.
    /// </summary>
    private static void AssertGraph(ShellDriver app, IReadOnlyList<PartD.Row> rows, int lanes, bool? showsDate = null)
    {
        var graph = Graph(app);
        var data = graph.Data ?? throw new Xunit.Sdk.XunitException("The graph has no data.");
        var actual = Enumerable.Range(0, data.Count).Select(row => Describe(graph, row)).ToList();
        Assert.True(
            actual.Count == rows.Count && Enumerable.Range(0, rows.Count).All(row => Matches(graph, rows[row], row)),
            "The graph is not the expected one.\nExpected:\n  " + string.Join("\n  ", rows) + "\nActual:\n  " + string.Join("\n  ", actual));

        Assert.Equal(lanes, data.MaxLaneCount);
        Assert.Equal(WindowLayout.GraphColumnWidth(lanes), graph.Columns.Graph.Width);
        if (showsDate is { } date)
        {
            Assert.Equal(date, graph.Columns.ShowsDate);
        }
    }

    private static bool Matches(CommitGraphControl graph, PartD.Row want, int row)
    {
        var data = graph.Data!;
        var commit = data.CommitAt(row);
        var layout = data.RowAt(row);
        if (commit.Subject != want.Message || layout.NodeLane != want.Lane || layout.NodeColor != want.Colour)
        {
            return false;
        }

        if (want.Merge is { } merge && merge != commit.Parents.Count > 1)
        {
            return false;
        }

        if (!Labels(data, row).SequenceEqual(want.Labels))
        {
            return false;
        }

        if (want.Drawn is { } drawn && !graph.LabelsInRow(row).Select(LabelText).SequenceEqual(drawn))
        {
            return false;
        }

        return want.Date is null || graph.Dates?.Format(commit.AuthorDate) == want.Date;
    }

    private static string Describe(CommitGraphControl graph, int row)
    {
        var data = graph.Data!;
        var commit = data.CommitAt(row);
        var layout = data.RowAt(row);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"\"{commit.Subject}\" lane {layout.NodeLane} colour {layout.NodeColor}{(commit.Parents.Count > 1 ? " merge" : string.Empty)} [{string.Join(", ", Labels(data, row))}] drawn [{string.Join(", ", graph.LabelsInRow(row).Select(LabelText))}] {graph.Dates?.Format(commit.AuthorDate)}");
    }

    private static IEnumerable<string> Labels(CommitGraphData data, int row) =>
        data.LabelsAt(row).Select(label => label.Kind == RefLabelKind.CurrentBranch ? label.Text + " ✓" : label.Text);

    private static string LabelText(PlacedLabel label) =>
        label.Kind == RefLabelKind.CurrentBranch && !label.IsOverflow ? label.Text + " ✓" : label.Text;

    /// <summary>The row of the commit whose subject is <paramref name="subject"/>, or -1.</summary>
    private static int RowOf(ShellDriver app, string subject)
    {
        var data = Graph(app).Data!;
        return Enumerable.Range(0, data.Count).FirstOrDefault(row => data.CommitAt(row).Subject == subject, -1);
    }

    /// <summary>
    /// Asserts the left panel's sections with their counts, in order (the report's "Local branches
    /// 3", "Remotes 2", "Pull requests 0", …).
    /// </summary>
    private static void AssertSections(ShellDriver app, params (string Title, int Count)[] sections)
    {
        app.Settle();
        var actual = app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefSectionRow>().Select(row => (row.Text, row.Count)).ToList();
        Assert.Equal(sections.Select(section => (section.Title, section.Count)), actual);
    }

    /// <summary>
    /// Asserts a local branch's row in the left panel: its ahead and behind arrows (0 for none) and,
    /// when <paramref name="head"/> says so, that it is the branch HEAD is on, drawn in the accent
    /// colour, or that it is drawn in the primary colour.
    /// </summary>
    private static void AssertLocalBranch(ShellDriver app, string name, int ahead, int behind, bool? head = null)
    {
        var row = (RefItemRow)app.RefRowModel("Local branches", name);
        Assert.True(
            row.Ahead == ahead && row.Behind == behind,
            $"{name} shows ↑{row.Ahead} ↓{row.Behind}; the report says ↑{ahead} ↓{behind}.");
        if (head is { } isHead)
        {
            Assert.Equal(isHead, row.IsHead);
            var colour = isHead ? PaletteOf(app).Accent : PaletteOf(app).TextPrimary;
            Assert.Equal(colour, TextColourOfRow(app, row));
        }
    }

    /// <summary>The rows directly under the remote <paramref name="remote"/> in the left panel (its branches and folders), in order; the remote must be open.</summary>
    private static List<string> RemoteChildren(ShellDriver app, string remote)
    {
        var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
        var start = rows.IndexOf(app.RefRowModel("Remotes", remote));
        var children = new List<string>();
        for (var i = start + 1; i < rows.Count && rows[i] is RefItemRow item && item.Depth >= 2; i++)
        {
            if (item.Depth == 2)
            {
                children.Add(item.Text);
            }
        }

        return children;
    }

    /// <summary>The remotes under "Remotes", in order, each with whether it is open.</summary>
    private static List<(string Name, bool IsOpen)> Remotes(ShellDriver app) =>
        [.. app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefItemRow>()
            .Where(row => row.Kind == RefItemKind.Remote)
            .Select(row => (row.Text, row.IsOpen))];

    /// <summary>Asserts that no row of the left panel is selected and no commit of the graph either: a right-click on a left-panel row selects nothing (D114).</summary>
    private static void AssertNothingSelected(ShellDriver app, int graphSelection = -1)
    {
        app.Settle();
        Assert.Empty(app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefItemRow>().Where(row => row.IsSelected).Select(row => row.Key));
        Assert.Equal(graphSelection, Graph(app).SelectedIndex);
    }

    /// <summary>The colour the name of a left-panel row is drawn in.</summary>
    private static Color? TextColourOfRow(ShellDriver app, RefListRow row)
    {
        var button = app.Find<ItemsControl>("RefList").GetVisualDescendants().OfType<Button>()
            .First(candidate => ReferenceEquals(candidate.DataContext, row));
        var text = button.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == row.Text);
        return (text.Foreground as ISolidColorBrush)?.Color;
    }

    /// <summary>
    /// Asserts the status bar while an operation is held at a percentage (the report's
    /// "Percentages"): the text <paramref name="running"/> followed by " &lt;stage&gt; N%" with N
    /// from 1 to 100, the bar determinate at N, Cancel shown and no "waiting" text unless
    /// <paramref name="waiting"/> names one. Returns N.
    /// </summary>
    private static int AssertHeldProgress(ShellDriver app, string running, string stage = "Counting objects", string? waiting = null)
    {
        var status = app.Session.Shell.ActiveTab.OperationStatus;
        var percent = status.Percent ?? throw new Xunit.Sdk.XunitException($"The status bar shows no percentage: \"{status.Text}\".");
        Assert.InRange(percent, 1, 100);
        AssertStatus(app, string.Create(CultureInfo.InvariantCulture, $"{running} {stage} {percent}%"));
        var bar = (ProgressBar)app.FindByAutomationId("OperationProgress");
        Assert.True(bar.IsEffectivelyVisible, "The progress bar is hidden.");
        Assert.False(bar.IsIndeterminate, "The progress bar is indeterminate.");
        Assert.Equal(percent, bar.Value);
        Assert.True(app.FindByAutomationId("OperationCancelButton").IsEffectivelyVisible, "Cancel is not shown.");
        var waitingText = (TextBlock)app.FindByAutomationId("OperationWaiting");
        Assert.Equal(waiting is not null, waitingText.IsEffectivelyVisible);
        if (waiting is not null)
        {
            Assert.Equal(waiting, waitingText.Text);
        }

        return percent;
    }

    /// <summary>Asserts the status bar of a running operation without a percentage: the text, an indeterminate bar and Cancel.</summary>
    private static void AssertRunningWithoutPercent(ShellDriver app, string running)
    {
        AssertStatus(app, running);
        var bar = (ProgressBar)app.FindByAutomationId("OperationProgress");
        Assert.True(bar.IsEffectivelyVisible, "The progress bar is hidden.");
        Assert.True(bar.IsIndeterminate, "The progress bar is not indeterminate.");
        Assert.True(app.FindByAutomationId("OperationCancelButton").IsEffectivelyVisible, "Cancel is not shown.");
    }

    /// <summary>Asserts a status bar with no operation running: no progress bar, no Cancel, no "waiting".</summary>
    private static void AssertIdleStatus(ShellDriver app, string text, OperationStatusTone tone = OperationStatusTone.Secondary)
    {
        AssertStatus(app, text, tone);
        Assert.False(app.FindByAutomationId("OperationProgress").IsEffectivelyVisible, "The progress bar is shown.");
        Assert.False(app.FindByAutomationId("OperationCancelButton").IsEffectivelyVisible, "Cancel is shown.");
        Assert.False(app.FindByAutomationId("OperationWaiting").IsEffectivelyVisible, "A \"waiting\" text is shown.");
    }

    /// <summary>
    /// "Wait until idle" after an input that starts an operation (directly or through a dialog's
    /// button, whose action starts a moment later): waits until the status bar's text is
    /// <paramref name="result"/> and the queue is idle, at most <see cref="Phase3Checks.NetworkTimeout"/>.
    /// When it does not come, the assertions that follow say what the status bar shows instead.
    /// </summary>
    private static async Task WaitForResultAsync(ShellDriver app, string result)
    {
        var status = app.Session.Shell.ActiveTab.OperationStatus;
        var queue = app.Session.Shell.ActiveTab.Repository!.Queue;
        try
        {
            await app.WaitForAsync(
                () => !status.IsRunning && status.Text == result && queue.Current is null && queue.Waiting == 0,
                $"the status bar to say \"{result}\"",
                NetworkTimeout);
        }
        catch (TimeoutException)
        {
            // The status bar shows something else: the check's AssertStatus says what.
        }

        await app.WaitForIdleAsync(timeout: NetworkTimeout);
    }

    /// <summary>Clicks a visible control found by its automation id (the dialog on top first), as a user would.</summary>
    private static void ClickId(ShellDriver app, string automationId)
    {
        var control = app.FindByAutomationId(automationId);
        Assert.True(control.IsEffectivelyVisible && control.IsEffectivelyEnabled, $"{automationId} cannot be clicked: visible {control.IsEffectivelyVisible}, enabled {control.IsEffectivelyEnabled}.");
        app.Click(control);
    }

    /// <summary>Right-clicks a left-panel row, found by its path, and returns the menu it opens and the point clicked.</summary>
    private static async Task<(MenuSnapshot Menu, Point Pointer)> RightClickRowAsync(ShellDriver app, params string[] path)
    {
        var row = await app.RefRowAsync(path);
        var pointer = app.BoundsOf(row).Center;
        app.RightClick(pointer);
        return (app.Menu(), pointer);
    }

    /// <summary>The visible text of a text block in the dialog on top, by its automation id.</summary>
    private static string DialogText(ShellDriver app, string automationId)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        var control = app.FindByAutomationId(view, automationId);
        Assert.True(control.IsEffectivelyVisible, $"{automationId} is hidden.");
        return control switch
        {
            TextBlock block => block.Text ?? string.Empty,
            TextBox box => box.Text ?? string.Empty,
            _ => throw new Xunit.Sdk.XunitException($"{automationId} is a {control.GetType().Name}."),
        };
    }

    /// <summary>The visible buttons of the dialog on top, left to right, by their labels.</summary>
    private static List<string> DialogButtons(ShellDriver app)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        return [.. view.GetVisualDescendants().OfType<Button>()
            .Where(button => button is not Avalonia.Controls.Primitives.ToggleButton && button.IsEffectivelyVisible && button.Content is string && !button.Classes.Contains("remoteAction"))
            .OrderBy(button => app.BoundsOf(button).X)
            .Select(button => (string)button.Content!)];
    }

    /// <summary>A button of the dialog on top by its automation id.</summary>
    private static Button DialogButton(ShellDriver app, string automationId)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        return (Button)app.FindByAutomationId(view, automationId);
    }

    /// <summary>Asserts the card of the dialog on top: its width (400, or 460 for the wide card) and that it lies over the backdrop.</summary>
    private static void AssertCard(ShellDriver app, string cardName, double width)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        var card = view.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Name == cardName)
            ?? throw new Xunit.Sdk.XunitException($"The dialog has no card {cardName}.");
        Assert.Equal(width, card.Bounds.Width, 0.5);
        var backdrop = view.GetVisualDescendants().OfType<Panel>().FirstOrDefault(panel => panel.Classes.Contains("dialogBackdrop"))
            ?? throw new Xunit.Sdk.XunitException("The dialog has no backdrop.");
        Assert.Equal(PaletteOf(app).Backdrop, (backdrop.Background as ISolidColorBrush)?.Color);
    }

    /// <summary>A radio button of the dialog on top (<c>ChoiceOption</c>) by its text.</summary>
    private static RadioButton ChoiceOption(ShellDriver app, string text)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        return view.GetVisualDescendants().OfType<RadioButton>()
            .Single(button => AutomationProperties.GetAutomationId(button) == "ChoiceOption" && AutomationProperties.GetName(button) == text && button.IsEffectivelyVisible);
    }

    /// <summary>The radio buttons of the dialog on top, in order, each with whether it is checked.</summary>
    private static List<(string Text, bool Checked)> ChoiceOptions(ShellDriver app)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        return [.. view.GetVisualDescendants().OfType<RadioButton>()
            .Where(button => AutomationProperties.GetAutomationId(button) == "ChoiceOption" && button.IsEffectivelyVisible)
            .Select(button => (AutomationProperties.GetName(button) ?? string.Empty, button.IsChecked == true))];
    }

    /// <summary>Asserts that the text box <paramref name="automationId"/> of the dialog on top has the keyboard focus (waiting for it, as the dialog gives it when it opens).</summary>
    private static async Task AssertFocusedAsync(ShellDriver app, string automationId)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        var control = app.FindByAutomationId(view, automationId);
        await app.WaitForAsync(() => control.IsFocused || control.IsKeyboardFocusWithin, $"{automationId} to have the keyboard focus");
    }

    /// <summary>The refs of a repository as <c>git for-each-ref</c> lists them: name and object.</summary>
    private static async Task<string> RefsAsync(TempRepo repo) =>
        (await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname)")).StandardOutput;

    /// <summary>What <c>git status -sb</c> prints first: the branch line.</summary>
    private static async Task<string> StatusLineAsync(TempRepo repo) =>
        (await repo.GitAsync("status", "-sb")).StandardOutput.Split('\n')[0].TrimEnd('\r');

    /// <summary>The object a revision names (<c>git rev-parse --verify</c>), or null when it names none.</summary>
    private static async Task<string?> RevParseAsync(TempRepo repo, string revision)
    {
        var result = await repo.GitAllowingFailureAsync("rev-parse", "--verify", "--quiet", revision);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    /// <summary>The subject of the commit a revision names.</summary>
    private static async Task<string> SubjectAsync(TempRepo repo, string revision) =>
        (await repo.GitAsync("log", "-1", "--format=%s", revision)).StandardOutput.TrimEnd('\n', '\r');

    /// <summary>A setting of the repository, or null when it is not set.</summary>
    private static async Task<string?> ConfigValueAsync(TempRepo repo, string key)
    {
        var result = await repo.GitAllowingFailureAsync("config", "--get", key);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    /// <summary>The lock files under the repository's .git folder, relative to the working tree with '/'.</summary>
    private static List<string> LockFiles(TempRepo repo)
    {
        var dotGit = Path.Combine(repo.Path, ".git");
        return [.. Directory.EnumerateFiles(dotGit, "*.lock", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(repo.Path, file).Replace('\\', '/'))];
    }

    /// <summary>Runs git in a bare repository (the server's) and returns its output.</summary>
    private static async Task<string> BareGitAsync(TempRepo repo, string bare, params string[] arguments) =>
        (await repo.GitAsync(["--git-dir", bare, .. arguments])).StandardOutput;

    /// <summary>The full id of a commit of the repository by its subject.</summary>
    private static async Task<string> CommitBySubjectAsync(TempRepo repo, string subject)
    {
        var output = (await repo.GitAsync("log", "--all", "--format=%H%x09%s")).StandardOutput;
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t', 2))
            .First(fields => fields[1].TrimEnd('\r') == subject)[0];
    }
}
