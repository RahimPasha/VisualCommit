using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// What phase 3's visual checks share beyond design 7.4's list (docs/test-reports/phase-3.md,
/// each part's conventions): waiting for a repository to open, the working-changes row and the
/// banner (D122), left-panel rows by their path, replacing a box's text, the error dialog as it
/// shows, the status bar's progress, and closing the window.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>Whether the main window has closed (its <c>Closed</c> event came): check 31's window closing by itself.</summary>
    public bool IsWindowClosed { get; private set; }

    /// <summary>
    /// Waits until the active tab's repository has opened as the checks' default start says: the
    /// whole history is loaded and drawn by the graph control, the working tree's status has been
    /// read and shown (the <c>&lt;repo&gt;: working tree:</c> log line the real-window pass waits
    /// for too), and the operation queue is idle. Returns the repository.
    /// </summary>
    public async Task<RepositoryViewModel> WaitForRepositoryAsync(TimeSpan? timeout = null)
    {
        await WaitUntilReadyAsync();
        var tab = Session.Shell.ActiveTab;
        RepositoryViewModel? repository = null;
        bool Opened()
        {
            repository = tab.Repository;
            if (repository is null || ReferenceEquals(repository.Graph, CommitGraphData.Empty) || !repository.Graph.IsComplete)
            {
                return false;
            }

            // The graph control shows this history (an unborn repository's view may have none).
            var control = Window.GetVisualDescendants().OfType<CommitGraphControl>().FirstOrDefault(graph => graph.Name == "GraphRows");
            return (control is null || ReferenceEquals(control.Data, repository.Graph))
                && CountLogLines(AppLogFile.WorkingTreeRead(repository.Name)) > 0;
        }

        await WaitForAsync(Opened, "the repository to open: its history loaded and shown, its status read", timeout);
        await WaitForIdleAsync(repository, timeout);
        return repository!;
    }

    /// <summary>
    /// A point on the working-changes row (display row 0, which <see cref="GraphRowPoint"/> skips),
    /// <see cref="WindowLayout.MessageClickInset"/> into the Message column, taken from the graph
    /// control's bounds: right also while a banner is shown, whose height moves the rows (D122).
    /// </summary>
    /// <exception cref="InvalidOperationException">The working-changes row is not shown.</exception>
    public Point WorkingRowPoint()
    {
        var graph = Find<CommitGraphControl>("GraphRows");
        if (!graph.ShowsWorkingRow)
        {
            throw new InvalidOperationException("The graph shows no working-changes row.");
        }

        var x = graph.Columns.Message.IsVisible ? graph.Columns.Message.X + WindowLayout.MessageClickInset : graph.Columns.Graph.Right + 10;
        return graph.TranslatePoint(new Point(x, graph.WorkingRowBounds.Center.Y), Window)
            ?? throw new InvalidOperationException("The graph is not in the window.");
    }

    /// <summary>
    /// Where the in-progress banner (<c>ConflictBanner</c>, design section 5) is in the window, or
    /// null while none is shown. Its height is at least <see cref="WindowLayout.BannerMinHeight"/>
    /// and grows with its lines (D122).
    /// </summary>
    public Rect? BannerBounds
    {
        get
        {
            Settle();
            return Window.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(control => AutomationProperties.GetAutomationId(control) == "ConflictBanner" && control.IsEffectivelyVisible && control.Bounds.Height > 0)
                is { } banner ? BoundsOf(banner) : null;
        }
    }

    /// <summary>
    /// The top of the graph's rows in the window (where display row 0 starts, scrolled to the top):
    /// <see cref="WindowLayout.RowsTop"/> without a banner, the banner's bottom plus the column
    /// headers with one (<see cref="WindowLayout.RowsTopBelow"/>, D122).
    /// </summary>
    public double GraphRowsTop
    {
        get
        {
            var graph = Find<CommitGraphControl>("GraphRows");
            return BoundsOf(graph).Top;
        }
    }

    /// <summary>
    /// The left panel's row at <paramref name="path"/>, as the report writes it: the section's
    /// title, then each row's text down to the one meant, such as
    /// <c>RefRow("Remotes", "origin", "feature", "alpha")</c> for "Remotes › origin › feature ›
    /// alpha", or <c>RefRow("Local branches", "main")</c>. The section's title alone is its
    /// header row. The list is scrolled until the row is laid out, and the row's own button (what
    /// a click, a right-click or a drag starts on) is returned. Every folder on the way must be
    /// open: the check opens it first, as a user would.
    /// </summary>
    /// <exception cref="InvalidOperationException">The panel has no such row; the message lists what it shows.</exception>
    public async Task<Control> RefRowAsync(params string[] path)
    {
        var list = Find<ItemsControl>("RefList");
        var row = RefRowModel(path);
        list.ScrollIntoView(((IEnumerable<RefListRow>)list.ItemsSource!).ToList().IndexOf(row));
        Control? found = null;

        // A refresh can replace the panel's rows while this waits (the watcher reads the refs
        // again), so the row is looked up by its path on every try, not kept from the first.
        await WaitForAsync(
            () =>
            {
                var current = RefRowModel(path);
                found = list.GetVisualDescendants().OfType<Button>()
                    .FirstOrDefault(button => ReferenceEquals(button.DataContext, current) && button.IsEffectivelyVisible && button.Bounds.Height > 0);
                if (found is null)
                {
                    list.ScrollIntoView(((IEnumerable<RefListRow>)list.ItemsSource!).ToList().IndexOf(current));
                    return false;
                }

                return BoundsOf(found).Bottom <= BoundsOf(list).Bottom + 0.5;
            },
            $"the left panel's row {string.Join(" › ", path)} to be laid out");
        return found!;
    }

    /// <summary>
    /// The left panel's row at <paramref name="path"/> (see <see cref="RefRowAsync"/>) as its view
    /// model: its selection, ahead and behind counts, whether it is open.
    /// </summary>
    public RefListRow RefRowModel(params string[] path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(path.Length, 1);
        Settle();
        var rows = Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
        var start = rows.FindIndex(row => row is RefSectionRow && row.Text == path[0]);
        if (start < 0)
        {
            throw new InvalidOperationException($"The left panel has no section \"{path[0]}\"; it shows:\n{DescribeRows(rows)}");
        }

        // A section's own rows have depth 1, theirs 2, and so on: path[depth] is at that depth.
        var current = start;
        for (var depth = 1; depth < path.Length; depth++)
        {
            var next = -1;
            for (var i = current + 1; i < rows.Count && rows[i] is RefItemRow item && item.Depth >= depth; i++)
            {
                if (item.Depth == depth && item.Text == path[depth])
                {
                    next = i;
                    break;
                }
            }

            if (next < 0)
            {
                throw new InvalidOperationException(
                    $"The left panel has no row {string.Join(" › ", path)} (is every folder on the way open?); it shows:\n{DescribeRows(rows)}");
            }

            current = next;
        }

        return rows[current];
    }

    /// <summary>
    /// A file's row in one of the file lists, by the file's path relative to the working tree
    /// (<c>src/Calculator.cs</c>): <c>UnstagedFileList</c> or <c>StagedFileList</c> in the stage
    /// panel, <c>FileList</c> in the commit details. The list is scrolled until the row is laid
    /// out inside it ("scrolling the list to it"), and the row's own button is returned: what a
    /// click (its diff), a right-click (its menu) or a hover (its tooltip) is aimed at. In a tree,
    /// the folders on the way must be open.
    /// </summary>
    /// <exception cref="InvalidOperationException">The list has no file with that path.</exception>
    public async Task<Control> FileRowAsync(string list, string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(list);
        ArgumentException.ThrowIfNullOrEmpty(path);
        var items = Find<ItemsControl>(list);
        var rows = ((IEnumerable<ChangedFileRow>)(items.ItemsSource ?? Array.Empty<ChangedFileRow>())).ToList();
        var index = rows.FindIndex(row => row.IsFile && row.Key == path);
        if (index < 0)
        {
            throw new InvalidOperationException($"{list} has no file {path}; it has: {string.Join(", ", rows.Where(row => row.IsFile).Select(row => row.Key))}.");
        }

        items.ScrollIntoView(index);
        Control? found = null;

        // A refresh can replace the list's rows while this waits (the watcher reads the status
        // again), so the row is looked up by its path on every try, not kept from the first.
        await WaitForAsync(
            () =>
            {
                var current = ((IEnumerable<ChangedFileRow>)(items.ItemsSource ?? Array.Empty<ChangedFileRow>())).ToList();
                var at = current.FindIndex(row => row.IsFile && row.Key == path);
                if (at < 0)
                {
                    return false;
                }

                found = items.GetVisualDescendants().OfType<Button>()
                    .FirstOrDefault(button => ReferenceEquals(button.DataContext, current[at]) && button.IsEffectivelyVisible && button.Bounds.Height > 0);
                if (found is null)
                {
                    items.ScrollIntoView(at);
                    return false;
                }

                return BoundsOf(found).Top >= BoundsOf(items).Top - 0.5
                    && BoundsOf(found).Bottom <= BoundsOf(items).Bottom + 0.5;
            },
            $"the row of {path} in {list} to be laid out");
        return found!;
    }

    /// <summary>
    /// Replaces what the text box with the automation id <paramref name="automationId"/> holds, as
    /// the report's "select all and type X" says: the box takes the focus, the platform's command
    /// modifier + A (<see cref="CommandModifier"/>) selects its text, and the text is typed (an
    /// empty text deletes the selection with Backspace). <see cref="TypeInto"/> keeps what the box
    /// held instead.
    /// </summary>
    public void ReplaceText(string automationId, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var box = TextBoxOf(automationId);
        box.Focus(NavigationMethod.Pointer);
        Settle();
        PressKey(Key.A, Raw(CommandModifier));
        if (text.Length == 0)
        {
            PressKey(Key.Back);
        }
        else
        {
            Type(text);
        }
    }

    /// <summary>
    /// The error dialog on top (<c>ErrorDialog</c>, design 1.6 and 4f) as it shows: its title,
    /// summary, repository line, git's output (<c>ErrorOutput</c>, null when hidden), the command
    /// (<c>ErrorCommand</c>, null when hidden) and the labels of its visible buttons from left to
    /// right.
    /// </summary>
    /// <exception cref="InvalidOperationException">The dialog on top is not the error dialog.</exception>
    public ErrorDialogSnapshot ErrorDialog()
    {
        var model = Dialog<ErrorDialogViewModel>();
        var view = TopDialogView ?? throw new InvalidOperationException("The error dialog has no view in the window.");
        string Text(string id) => ((TextBlock)FindByAutomationId(view, id)).Text ?? string.Empty;
        var output = (TextBox)FindByAutomationId(view, "ErrorOutput");
        var command = (TextBlock)FindByAutomationId(view, "ErrorCommand");
        var buttons = view.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && button.Content is string)
            .OrderBy(button => BoundsOf(button).X)
            .Select(button => (string)button.Content!)
            .ToList();
        return new ErrorDialogSnapshot(
            Text("ErrorTitle"),
            Text("ErrorSummary"),
            Text("ErrorRepository"),
            output.IsEffectivelyVisible ? (output.Text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal) : null,
            command.IsEffectivelyVisible ? command.Text : null,
            buttons,
            model);
    }

    /// <summary>
    /// Waits until the status bar shows a percentage for the running operation (its text ends with
    /// "N%" and the bar is determinate) and returns N: "wait until the status bar shows a
    /// percentage" (checks 17, 18 and 31, with the server holding the fetch).
    /// </summary>
    public async Task<int> WaitForProgressPercentAsync(TimeSpan? timeout = null)
    {
        var status = Session.Shell.ActiveTab.OperationStatus;
        await WaitForAsync(
            () => status.IsRunning && status.Percent is not null && PercentAtEnd().IsMatch(status.Text),
            "the status bar to show a percentage",
            timeout);
        return status.Percent!.Value;
    }

    /// <summary>
    /// Waits until the active tab's details panel is not reading a commit's details
    /// (<see cref="CommitDetailsViewModel.IsLoading"/> false), without leaving the UI thread, for a
    /// picture taken right after an input that selected a commit (a click, a right-click, a key):
    /// the panel reads the details on the thread pool and, until they arrive, still shows the
    /// commit selected before, or the placeholder, beside the newly selected row (phase 3's gate:
    /// 01c, 13f, 16a). A selection that reads nothing ends the wait at once: nothing selected, the
    /// working-changes row, several commits, a tab without a repository. A read that fails ends it
    /// too (the panel then shows why). Between looks the UI thread runs what was posted to it, as
    /// in <see cref="WaitForAsync"/>; this one is synchronous, for a synchronous capture.
    /// </summary>
    /// <exception cref="TimeoutException">The details were still being read when the time was up.</exception>
    public void WaitForCommitDetails(TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultWaitTimeout;
        var clock = Stopwatch.StartNew();
        while (true)
        {
            Settle();
            if (Session.Shell.ActiveTab.Repository?.Details is not { IsLoading: true })
            {
                return;
            }

            if (clock.Elapsed >= limit)
            {
                throw new TimeoutException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Timed out after {limit.TotalSeconds:0.###} s waiting for the details panel to show the selected commit."));
            }

            Thread.Sleep(PollInterval);
        }
    }

    /// <summary>
    /// Starts recording every text the active tab's status bar shows (<c>OperationStatusViewModel.Text</c>,
    /// through its <c>PropertyChanged</c>), for a running text that is not pictured (check 18's
    /// push). Dispose the recorder to stop.
    /// </summary>
    public StatusTextRecorder RecordStatusTexts() => new(Session.Shell.ActiveTab.OperationStatus);

    /// <summary>
    /// Asks the main window to close, as its close button would (<c>Window.Close()</c>), and
    /// returns whether it closed: false while the app keeps it open, for example for the
    /// finishing dialog while a fetch runs (check 31).
    /// </summary>
    public bool RequestClose()
    {
        Window.Close();
        Settle();
        return IsWindowClosed;
    }

    private TextBox TextBoxOf(string automationId)
    {
        var control = FindByAutomationId(automationId);
        var box = control as TextBox
            ?? control.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()
            ?? throw new InvalidOperationException($"The control with the automation id '{automationId}' is a {control.GetType().Name}, not a text box.");
        if (!box.IsEffectivelyEnabled || !box.IsEffectivelyVisible)
        {
            throw new InvalidOperationException($"The text box '{automationId}' cannot be typed into: it is {(box.IsEffectivelyVisible ? "disabled" : "not visible")}.");
        }

        return box;
    }

    private static string DescribeRows(IEnumerable<RefListRow> rows) =>
        string.Join('\n', rows.Select(row => row is RefItemRow item ? new string(' ', 2 * item.Depth) + item.Text : row.Text));

    [GeneratedRegex(@"\d{1,3}%$", RegexOptions.CultureInvariant)]
    private static partial Regex PercentAtEnd();
}

/// <summary>The error dialog as <see cref="ShellDriver.ErrorDialog"/> reads it from the window.</summary>
/// <param name="Title">The title (<c>ErrorTitle</c>).</param>
/// <param name="Summary">The summary (<c>ErrorSummary</c>).</param>
/// <param name="Repository">The repository line, "In &lt;repo&gt;" (<c>ErrorRepository</c>).</param>
/// <param name="Output">Git's cleaned output as the box shows it (<c>ErrorOutput</c>), or null when the dialog has none.</param>
/// <param name="Command">The failed call (<c>ErrorCommand</c>), or null when the dialog has none.</param>
/// <param name="Buttons">The visible buttons' labels, left to right: "Copy output", the extra action, "Close".</param>
/// <param name="Model">The dialog's view model, for its commands.</param>
public sealed record ErrorDialogSnapshot(
    string Title,
    string Summary,
    string Repository,
    string? Output,
    string? Command,
    IReadOnlyList<string> Buttons,
    ErrorDialogViewModel Model)
{
    /// <summary>The output's lines (empty when there is no output).</summary>
    public IReadOnlyList<string> OutputLines => Output is null ? [] : Output.Split('\n');
}

/// <summary>Records every text a status bar shows while it is not disposed (<see cref="ShellDriver.RecordStatusTexts"/>).</summary>
public sealed class StatusTextRecorder : IDisposable
{
    private readonly IStatusBarOperation _status;
    private readonly Lock _gate = new();
    private readonly List<string> _texts = [];

    internal StatusTextRecorder(IStatusBarOperation status)
    {
        _status = status;
        _status.PropertyChanged += OnChanged;
    }

    /// <summary>The texts shown since recording started, in order (repeats left out).</summary>
    public IReadOnlyList<string> Texts
    {
        get
        {
            lock (_gate)
            {
                return [.. _texts];
            }
        }
    }

    public void Dispose() => _status.PropertyChanged -= OnChanged;

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IStatusBarOperation.Text))
        {
            return;
        }

        lock (_gate)
        {
            if (_texts.Count == 0 || _texts[^1] != _status.Text)
            {
                _texts.Add(_status.Text);
            }
        }
    }
}
