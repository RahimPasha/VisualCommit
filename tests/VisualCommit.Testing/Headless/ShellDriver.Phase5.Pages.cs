using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Controls.Blame;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Diff;
using VisualCommit.App.Views.Pages;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// The graph area's pages in the driver (docs/phase-5-design.md 4, 5, 11, 15.2; package WP6;
/// docs/test-reports/phase-5.md part B, "Harness this part needs"): waiting for a page, the file
/// history, the blame and the reflog page (or the backups list) as snapshots in the window's
/// coordinates, the points the report clicks (a page row's at window x 420 over its centre, BD1;
/// a blame line's in the gutter and in the text, 13.4; a line of the main diff, as phase 3's
/// checks find it), and the details panel's rows for "All files". The history's own diff is
/// looked for inside <c>FileHistory</c> only (11.1: phase 2's lookups find the main diff first).
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>How far right of a page's left edge its rows are clicked: window x 420 at every size part B uses (BD1), in the subject, action or words column.</summary>
    public const double PageRowPointX = 160;

    /// <summary>How far right of the gutter's left edge a blame line is clicked in it (part B's conventions: window x 300 at 1100×700).</summary>
    public const double BlameGutterPointX = 40;

    /// <summary>The page of the graph area that is shown, or null while none is (a diff covering a page hides it).</summary>
    public UserControl? ShownPage
    {
        get
        {
            Settle();
            return Window.GetVisualDescendants().OfType<UserControl>()
                .FirstOrDefault(view => view is FileHistoryView or BlameView or ReflogView && view.IsEffectivelyVisible);
        }
    }

    /// <summary>
    /// Waits until a page of type <typeparamref name="TView"/> is shown and has loaded: its rows,
    /// its lines or its message are there, laid out (a history streams its rows: the first is enough).
    /// </summary>
    public async Task<TView> WaitForPageAsync<TView>(TimeSpan? timeout = null)
        where TView : UserControl
    {
        TView? found = null;
        await WaitForAsync(
            () =>
            {
                found = ShownPage as TView;
                return found is not null && found.Bounds.Height > 0 && found.DataContext switch
                {
                    FileHistoryViewModel history => !history.ShowsLoading,
                    BlameViewModel blame => !blame.ShowsLoading && (!blame.ShowsText || Find<BlameTextEditor>(found, "BlameText") is { TextView.VisualLinesValid: true, IsTokenizing: false }),
                    ReflogViewModel reflog => !reflog.ShowsLoading && reflog.ReadsShown > 0,
                    _ => false,
                };
            },
            $"the {typeof(TView).Name} to show and load",
            timeout);
        Settle();
        return found!;
    }

    /// <summary>Waits until the shown history has listed at least <paramref name="rows"/> rows and has ended, or failed.</summary>
    public Task WaitForFileHistoryRowsAsync(int rows, TimeSpan? timeout = null) =>
        WaitForAsync(
            () => ShownPage is FileHistoryView { DataContext: FileHistoryViewModel history } && history.Rows.Count >= rows && !history.IsLoading,
            $"the file history to list {rows} rows",
            timeout);

    /// <summary>
    /// Waits until the reflog page has shown a read after <paramref name="readsShown"/> (it reads
    /// again on every refresh, 5.2) that was made for the repository's refs as they are now, lost
    /// marks included: after <see cref="WaitForIdleAsync"/>, whose operation's refresh has handed
    /// the page its refs, the operation's effect is in it then. A read asked for before the
    /// operation, which may still end after the count was taken, does not count.
    /// </summary>
    public Task WaitForReflogReadAsync(int readsShown, TimeSpan? timeout = null) =>
        WaitForAsync(
            () => ShownPage is ReflogView { DataContext: ReflogViewModel reflog }
                && reflog.ReadsShown > readsShown
                && Session.Shell.ActiveTab.Repository is { Refs: { } refs }
                && ReferenceEquals(reflog.ShownFor, refs),
            "the reflog page to show a read of the refs as they are now",
            timeout);

    /// <summary>The file history page as drawn now. Throws when it is not shown.</summary>
    public FileHistorySnapshot FileHistorySnapshot()
    {
        var view = ShownPage as FileHistoryView ?? throw new InvalidOperationException("No file history page is shown.");
        var model = (FileHistoryViewModel)view.DataContext!;
        var list = Find<ListBox>(view, "FileHistoryList");
        var diffPart = Find<Border>(view, "FileHistoryDiff");
        var diffView = view.GetVisualDescendants().OfType<DiffView>().First();
        var rows = new List<FileHistoryRowSnapshot>();
        for (var index = 0; index < model.Rows.Count; index++)
        {
            var row = model.Rows[index];
            var item = list.ContainerFromIndex(index) as ListBoxItem;
            var drawn = item is { IsEffectivelyVisible: true } && item.Bounds.Height > 0 ? item : null;
            rows.Add(new FileHistoryRowSnapshot(
                row.Name,
                row.StatusLetter,
                drawn is null ? null : ColorOf(Find<TextBlock>(drawn, "FileHistoryStatus").Foreground),
                row.ShortSha,
                row.Subject,
                ShownText(drawn, "FileHistorySecondary"),
                row.Author,
                row.DateText,
                ShownBounds(drawn, "FileHistoryAuthor"),
                ShownBounds(drawn, "FileHistoryDate"),
                drawn is null ? null : BoundsOf(drawn),
                ReferenceEquals(model.SelectedRow, row),
                drawn?.IsFocused == true,
                drawn is null ? null : RowBackground(drawn)));
        }

        var diff = model.Diff;
        return new FileHistorySnapshot(
            BoundsOf(view),
            BoundsOf(Find<Border>(view, "FileHistoryHeader")),
            model.Title,
            model.HasFolder ? model.Folder : null,
            model.FromText,
            Find<Button>(view, "FileHistoryBlame").IsEffectivelyEnabled,
            BoundsOf(list),
            BoundsOf(diffPart),
            model.IsSideBySide,
            rows,
            model.ShowsLoading,
            model.Message,
            list.IsKeyboardFocusWithin,
            diff,
            diff is null ? null : Find<TextBlock>(diffView, "DiffSideLabel").Text,
            diff is not null && Find<Button>(diffView, "CloseDiffButton").IsEffectivelyVisible);
    }

    /// <summary>The point of the history's row <paramref name="row"/> (from 0): its vertical centre, window x 420 at the default panel widths (BD1). Throws when it is not drawn.</summary>
    public Point FileHistoryRowPoint(int row)
    {
        var snapshot = FileHistorySnapshot();
        var bounds = snapshot.Rows[row].Bounds ?? throw new InvalidOperationException($"History row {row} is not drawn: scroll it into view first.");
        return new Point(snapshot.Bounds.X + PageRowPointX, bounds.Center.Y);
    }

    /// <summary>The blame page as drawn now. Throws when it is not shown.</summary>
    public BlameSnapshot BlameSnapshot()
    {
        var view = ShownPage as BlameView ?? throw new InvalidOperationException("No blame page is shown.");
        var model = (BlameViewModel)view.DataContext!;
        var gutter = Find<BlameGutter>(view, "BlameGutter");
        var text = Find<BlameTextEditor>(view, "BlameText");
        var gutterOrigin = BoundsOf(gutter).TopLeft;

        // What the gutter draws: its own runs' texts, brushes and picked run, as Render paints them.
        var runs = model.Runs.Select(run =>
        {
            var drawn = gutter.Runs.Count > run.Index;
            return new BlameRunSnapshot(
                run.Index,
                run.FirstLine,
                run.LastLine,
                run.Commit.Sha,
                drawn ? [.. gutter.TextsOf(run.Index).Select(part => new BlameGutterText(part.Text, gutterOrigin.X + part.X, part.Width))] : [],
                drawn ? gutter.BackgroundKeyOf(run.Index) : null,
                gutter.SelectedRun == run.Index,
                drawn ? gutter.Runs[run.Index].ToolTip : model.ToolTipOf(run));
        }).ToList();
        return new BlameSnapshot(
            BoundsOf(view),
            model.Title,
            model.Caption,
            model.ShowsLoading,
            model.ShowsMessage ? model.Message : null,
            model.ShowsText ? text.Document?.Text ?? string.Empty : string.Empty,
            BoundsOf(gutter),
            BoundsOf(text),
            text.NumberColumnWidth,
            text.IsHighlighted,
            runs,
            text.TextArea.IsKeyboardFocusWithin);
    }

    /// <summary>
    /// The point of blame line <paramref name="line"/> (from 1) in the gutter: <see cref="BlameGutterPointX"/>
    /// right of its left edge, halfway down the line (window x 300 at 1100×700). Throws when the line is not in view.
    /// </summary>
    public Point BlameGutterPoint(int line)
    {
        var view = ShownPage as BlameView ?? throw new InvalidOperationException("No blame page is shown.");
        var gutter = Find<BlameGutter>(view, "BlameGutter");
        var text = Find<BlameTextEditor>(view, "BlameText");
        var point = text.PointAt(line, 1) ?? throw new InvalidOperationException($"Line {line} of the blame is not in view.");
        var inWindow = text.TranslatePoint(point, Window) ?? throw new InvalidOperationException("The blame is not in the window.");
        return new Point(BoundsOf(gutter).X + BlameGutterPointX, inWindow.Y);
    }

    /// <summary>Where column <paramref name="column"/> of blame line <paramref name="line"/> (both from 1) starts, halfway down the line, in the window (13.4, as phase 4's <c>ResultPoint</c>).</summary>
    public Point BlameTextPoint(int line, int column)
    {
        var view = ShownPage as BlameView ?? throw new InvalidOperationException("No blame page is shown.");
        var text = Find<BlameTextEditor>(view, "BlameText");
        var point = text.PointAt(line, column) ?? throw new InvalidOperationException($"Line {line} of the blame is not in view.");
        return text.TranslatePoint(point, Window) ?? throw new InvalidOperationException("The blame is not in the window.");
    }

    /// <summary>The reflog page (a reflog or the backups list) as drawn now. Throws when it is not shown.</summary>
    public ReflogSnapshot ReflogSnapshot()
    {
        var view = ShownPage as ReflogView ?? throw new InvalidOperationException("No reflog page is shown.");
        var model = (ReflogViewModel)view.DataContext!;
        var list = Find<ListBox>(view, "ReflogList");
        var entries = new List<ReflogRowSnapshot>();
        var backups = new List<BackupRowSnapshot>();
        for (var index = 0; index < model.Rows.Count; index++)
        {
            var item = list.ContainerFromIndex(index) as ListBoxItem;
            var drawn = item is { IsEffectivelyVisible: true } && item.Bounds.Height > 0 ? item : null;
            var selected = ReferenceEquals(model.SelectedRow, model.Rows[index]);
            switch (model.Rows[index])
            {
                case ReflogRowViewModel entry:
                    entries.Add(new ReflogRowSnapshot(
                        entry.Selector,
                        entry.TimeText,
                        entry.Action,
                        entry.ShortSha,
                        entry.Subject,
                        entry.IsLost,
                        drawn is null ? null : BoundsOf(drawn),
                        ShownBounds(drawn, "ReflogSelector"),
                        ShownBounds(drawn, "ReflogTime"),
                        ShownBounds(drawn, "ReflogAction"),
                        ShownBounds(drawn, "ReflogCommitSha"),
                        ShownBounds(drawn, "ReflogLostMark"),
                        selected,
                        drawn?.IsFocused == true));
                    break;
                case BackupRowViewModel backup:
                    backups.Add(new BackupRowSnapshot(
                        backup.Name,
                        backup.TimeText,
                        backup.Words,
                        backup.ShortSha,
                        backup.Subject,
                        drawn is null ? null : BoundsOf(drawn),
                        ShownBounds(drawn, "BackupTime"),
                        ShownBounds(drawn, "BackupWords"),
                        ShownBounds(drawn, "BackupCommitSha"),
                        selected));
                    break;
            }
        }

        var footer = Find<Border>(view, "ReflogFooter");
        return new ReflogSnapshot(
            BoundsOf(view),
            model.Title,
            model.SourceLabel,
            BoundsOf(Find<Button>(view, "ReflogSource")),
            BoundsOf(list),
            model.Footer,
            footer.IsEffectivelyVisible ? BoundsOf(footer) : null,
            model.ShowsLoading,
            model.Message,
            model.Detail,
            entries,
            backups,
            list.IsKeyboardFocusWithin,
            model.ReadsShown);
    }

    /// <summary>The point of the reflog row named <paramref name="selector"/> (<c>HEAD@{9}</c>): its centre's height, window x 420 (BD1). Throws when it is not drawn.</summary>
    public Point ReflogRowPoint(string selector)
    {
        var snapshot = ReflogSnapshot();
        var row = snapshot.Rows.SingleOrDefault(entry => entry.Selector == selector) ?? throw new InvalidOperationException($"The reflog has no row {selector}.");
        var bounds = row.Bounds ?? throw new InvalidOperationException($"The row {selector} is not drawn: scroll it into view first.");
        return new Point(snapshot.Bounds.X + PageRowPointX, bounds.Center.Y);
    }

    /// <summary>Where the lost mark of the reflog row <paramref name="selector"/> is (20 wide, BD3). Throws when the row has none drawn.</summary>
    public Rect LostMarkBounds(string selector) =>
        ReflogSnapshot().Rows.SingleOrDefault(entry => entry.Selector == selector)?.LostMarkBounds
        ?? throw new InvalidOperationException($"The row {selector} has no lost mark in view.");

    /// <summary>The point of the backup row named <paramref name="name"/>: its centre's height, window x 420 (BD1). Throws when it is not drawn.</summary>
    public Point BackupRowPoint(string name)
    {
        var snapshot = ReflogSnapshot();
        var row = snapshot.Backups.SingleOrDefault(backup => backup.Name == name) ?? throw new InvalidOperationException($"The backups list has no row {name}.");
        var bounds = row.Bounds ?? throw new InvalidOperationException($"The backup {name} is not drawn: scroll it into view first.");
        return new Point(snapshot.Bounds.X + PageRowPointX, bounds.Center.Y);
    }

    /// <summary>Where the words of the backup row <paramref name="name"/> are (BD4). Throws when the row is not drawn.</summary>
    public Rect BackupWordsBounds(string name) =>
        ReflogSnapshot().Backups.SingleOrDefault(backup => backup.Name == name)?.WordsBounds
        ?? throw new InvalidOperationException($"The backup {name} is not drawn.");

    /// <summary>
    /// The details panel's file rows as drawn (phase 1's list, with "All files", 4.5): each file's
    /// status letter (empty for a file the commit did not change), name and folder (empty in the
    /// tree view and at the root), and whether it has the selection background.
    /// </summary>
    public IReadOnlyList<DetailsFileRow> DetailsFileRows()
    {
        Settle();
        var list = Find<ItemsControl>("FileList");
        return [.. ((IEnumerable<ChangedFileRow>)(list.ItemsSource ?? Array.Empty<ChangedFileRow>()))
            .Where(row => row.IsFile)
            .Select(row => new DetailsFileRow(row.StatusLetter, row.Name, row.Folder, row.IsUnchanged, row.IsSelected))];
    }

    /// <summary>
    /// The index of the main diff's row whose new number is <paramref name="newNumber"/> (an added
    /// or a context line), or -1 (part B's B5 step 7). A removed line has no new number, so the
    /// removed old line of the same number, which comes first in a change, is never the one found
    /// (phase 3's own helper, which falls back to the old number, would find it). The main diff
    /// comes before the history page's in the window (11.1), so this is never the page's.
    /// </summary>
    public int DiffRowOfLine(int newNumber)
    {
        var text = Find<DiffTextView>("DiffText");
        return text.Rows.ToList().FindIndex(row => row.Kind == DiffRowKind.Line && row.Left?.NewNumber == newNumber);
    }

    /// <summary>A point on the text of the main diff's row <paramref name="row"/>: 240 right of the row's left edge, on its text and not on its numbers (phase 3's <c>DiffTextPoint</c>).</summary>
    public Point DiffTextPoint(int row)
    {
        var text = Find<DiffTextView>("DiffText");
        var bounds = text.RowBounds(row) ?? throw new InvalidOperationException($"Diff row {row} is not in view.");
        return text.TranslatePoint(new Point(bounds.X + 240, bounds.Center.Y), Window) ?? throw new InvalidOperationException("The diff is not in the window.");
    }

    private static T Find<T>(Visual scope, string name)
        where T : Control =>
        scope.GetVisualDescendants().OfType<T>().FirstOrDefault(control => control.Name == name)
        ?? throw new InvalidOperationException($"No {typeof(T).Name} named '{name}' is in the {scope.GetType().Name}.");

    /// <summary>The text of a cell when it is drawn, or null when its column is hidden or the row is not drawn.</summary>
    private static string? ShownText(Visual? row, string name) =>
        row?.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(text => text.Name == name) is { IsEffectivelyVisible: true } text ? text.Text : null;

    /// <summary>Where a part of a row is, when it is drawn.</summary>
    private Rect? ShownBounds(Visual? row, string name) =>
        row?.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => control.Name == name) is { IsEffectivelyVisible: true } part ? BoundsOf(part) : null;

    /// <summary>The background a list row is drawn on: its content presenter's, as the selection and hover styles set it.</summary>
    private static Color? RowBackground(ListBoxItem item) =>
        ColorOf(item.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().FirstOrDefault()?.Background);

    private static Color? ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : null;
}

/// <summary>The file history page as drawn (4.1, BD2), in the window's coordinates.</summary>
/// <param name="Bounds">The page (the graph area's place).</param>
/// <param name="HeaderBounds">The header (<c>FileHistoryHeader</c>), 36 high.</param>
/// <param name="Title">The title (<c>FileHistoryTitle</c>): <c>History of Reader.cs</c>, also the pane's name.</param>
/// <param name="Folder">The folder after the title, or null at the root.</param>
/// <param name="From">The <c>from {short}</c> text, or null (BD24).</param>
/// <param name="BlameEnabled">Whether the header's <c>Blame</c> (<c>FileHistoryBlame</c>) is enabled.</param>
/// <param name="ListBounds">The list (<c>FileHistoryList</c>).</param>
/// <param name="DiffBounds">The diff part (<c>FileHistoryDiff</c>).</param>
/// <param name="IsSideBySide">Whether the list and the diff lie side by side.</param>
/// <param name="Rows">Every row, top to bottom.</param>
/// <param name="IsLoading">The list's place says <c>Loading…</c>.</param>
/// <param name="Message">The list's message (none touched the file, git's failure), or null.</param>
/// <param name="ListHasFocus">The keyboard focus is in the list.</param>
/// <param name="Diff">The hosted diff's view model: its header texts, side label, hunks and mode; null before a row is selected.</param>
/// <param name="DiffSideLabel">The hosted diff's side label as drawn, or null.</param>
/// <param name="DiffShowsClose">Whether the hosted diff shows a close button (it never should).</param>
public sealed record FileHistorySnapshot(
    Rect Bounds,
    Rect HeaderBounds,
    string Title,
    string? Folder,
    string? From,
    bool BlameEnabled,
    Rect ListBounds,
    Rect DiffBounds,
    bool IsSideBySide,
    IReadOnlyList<FileHistoryRowSnapshot> Rows,
    bool IsLoading,
    string? Message,
    bool ListHasFocus,
    DiffViewModel? Diff,
    string? DiffSideLabel,
    bool DiffShowsClose)
{
    /// <summary>The selected row, or null.</summary>
    public FileHistoryRowSnapshot? Selected => Rows.FirstOrDefault(row => row.IsSelected);
}

/// <summary>A row of the file history as drawn (4.1, BD2, BD13).</summary>
/// <param name="Name">Its automation name: <c>{short} {subject}</c>.</param>
/// <param name="Letter">The status letter.</param>
/// <param name="LetterColor">The letter's colour as drawn, or null when the row is not drawn.</param>
/// <param name="ShortSha">The short SHA.</param>
/// <param name="Subject">The subject.</param>
/// <param name="Secondary">The secondary text as drawn, or null when its column is hidden, it has none, or the row is not drawn.</param>
/// <param name="Author">The row's author, which its author column shows while it is visible (BD2: the surface holds it also when hidden).</param>
/// <param name="Date">The row's date, <c>yyyy-MM-dd HH:mm</c>, which its date column shows while it is visible.</param>
/// <param name="AuthorBounds">The author column in the window, or null when it is hidden or the row is not drawn.</param>
/// <param name="DateBounds">The date column in the window, or null when it is hidden or the row is not drawn.</param>
/// <param name="Bounds">The row in the window, or null when it is not drawn.</param>
/// <param name="IsSelected">The row is selected.</param>
/// <param name="HasFocus">The row has the keyboard focus.</param>
/// <param name="Background">The row's background colour as drawn (the selection's, the hover's), or null.</param>
public sealed record FileHistoryRowSnapshot(
    string Name,
    string Letter,
    Color? LetterColor,
    string ShortSha,
    string Subject,
    string? Secondary,
    string Author,
    string Date,
    Rect? AuthorBounds,
    Rect? DateBounds,
    Rect? Bounds,
    bool IsSelected,
    bool HasFocus,
    Color? Background)
{
    /// <summary>The author column is drawn.</summary>
    public bool AuthorShown => AuthorBounds is not null;

    /// <summary>The date column is drawn.</summary>
    public bool DateShown => DateBounds is not null;
}

/// <summary>The blame page as drawn (4.2, 13.4), in the window's coordinates.</summary>
/// <param name="Bounds">The page.</param>
/// <param name="Title">The title (<c>BlameTitle</c>): <c>Blame of Reader.cs</c>.</param>
/// <param name="Caption">The secondary text: <c>in the working tree</c>, <c>at {short} {subject}</c>, ….</param>
/// <param name="IsLoading">The text's place says <c>Loading…</c>.</param>
/// <param name="Message">The text's place's message (binary, empty, git's failure), or null.</param>
/// <param name="Text">The text (<c>BlameText</c>'s document), empty without one.</param>
/// <param name="GutterBounds">The gutter (<c>BlameGutter</c>): 130 or 200 wide.</param>
/// <param name="TextBounds">The text with its number column.</param>
/// <param name="NumberColumnWidth">The number column's width: 30.4 up to 99 lines.</param>
/// <param name="IsHighlighted">The text has syntax colours.</param>
/// <param name="Runs">The gutter's runs, top to bottom.</param>
/// <param name="TextHasFocus">The keyboard focus is in the text.</param>
public sealed record BlameSnapshot(
    Rect Bounds,
    string Title,
    string Caption,
    bool IsLoading,
    string? Message,
    string Text,
    Rect GutterBounds,
    Rect TextBounds,
    double NumberColumnWidth,
    bool IsHighlighted,
    IReadOnlyList<BlameRunSnapshot> Runs,
    bool TextHasFocus);

/// <summary>A run of the blame's gutter (BD12).</summary>
/// <param name="Index">Its place among the runs, from 0.</param>
/// <param name="FirstLine">Its first line, from 1.</param>
/// <param name="LastLine">Its last line.</param>
/// <param name="Sha">Its commit (the zero id for lines not committed yet).</param>
/// <param name="Texts">What its first line draws, left to right, with each text's left edge in the window and its width.</param>
/// <param name="Background">The key of the brush the gutter draws its lines on: <c>VcPanelBackgroundBrush</c>, <c>VcWindowBackgroundBrush</c> or <c>VcSelectionBrush</c>; null while the gutter does not have the run.</param>
/// <param name="IsSelected">The gutter draws it as the picked run.</param>
/// <param name="ToolTip">Its tooltip (BD11), as the gutter shows it.</param>
public sealed record BlameRunSnapshot(int Index, int FirstLine, int LastLine, string Sha, IReadOnlyList<BlameGutterText> Texts, string? Background, bool IsSelected, string ToolTip);

/// <summary>A text a gutter run's first line draws, with its left edge in the window and its width.</summary>
public sealed record BlameGutterText(string Text, double X, double Width);

/// <summary>The reflog page or the backups list as drawn (5.2, 5.3, BD3 to BD6), in the window's coordinates.</summary>
/// <param name="Bounds">The page.</param>
/// <param name="Title">The page's title, its pane's name: <c>Reflog of HEAD</c>, <c>Backups</c>.</param>
/// <param name="SourceLabel">The source selector's label: <c>HEAD</c>, a branch, <c>Backups</c>.</param>
/// <param name="SourceBounds">The source selector (<c>ReflogSource</c>).</param>
/// <param name="ListBounds">The list (<c>ReflogList</c>).</param>
/// <param name="Footer">The footer's text, or null without one.</param>
/// <param name="FooterBounds">The footer (<c>ReflogFooter</c>), 26 high along the page's bottom, or null.</param>
/// <param name="IsLoading">The list's place says <c>Loading…</c>.</param>
/// <param name="Message">The list's message (no reflog, no backups, git's failure), or null.</param>
/// <param name="Detail">The second line under the message (git's rules), or null.</param>
/// <param name="Rows">A reflog's rows, top to bottom; empty for the backups list.</param>
/// <param name="Backups">The backups list's rows; empty for a reflog.</param>
/// <param name="ListHasFocus">The keyboard focus is in the list.</param>
/// <param name="ReadsShown">How many reads the page has shown.</param>
public sealed record ReflogSnapshot(
    Rect Bounds,
    string Title,
    string SourceLabel,
    Rect SourceBounds,
    Rect ListBounds,
    string? Footer,
    Rect? FooterBounds,
    bool IsLoading,
    string? Message,
    string? Detail,
    IReadOnlyList<ReflogRowSnapshot> Rows,
    IReadOnlyList<BackupRowSnapshot> Backups,
    bool ListHasFocus,
    int ReadsShown)
{
    /// <summary>The selected reflog row, or null.</summary>
    public ReflogRowSnapshot? Selected => Rows.FirstOrDefault(row => row.IsSelected);
}

/// <summary>A reflog row (5.2, BD3); a part's bounds are null when its column is hidden or the row is not drawn.</summary>
public sealed record ReflogRowSnapshot(
    string Selector,
    string Time,
    string Action,
    string ShortSha,
    string Subject,
    bool IsLost,
    Rect? Bounds,
    Rect? SelectorBounds,
    Rect? TimeBounds,
    Rect? ActionBounds,
    Rect? CommitBounds,
    Rect? LostMarkBounds,
    bool IsSelected,
    bool HasFocus);

/// <summary>A backup row (5.3, BD4); a part's bounds are null when the row is not drawn.</summary>
public sealed record BackupRowSnapshot(
    string Name,
    string Time,
    string Words,
    string ShortSha,
    string Subject,
    Rect? Bounds,
    Rect? TimeBounds,
    Rect? WordsBounds,
    Rect? CommitBounds,
    bool IsSelected);

/// <summary>A file row of the details panel (4.5): its letter (empty for an unchanged file of "All files"), name, folder, and selection background.</summary>
public sealed record DetailsFileRow(string Letter, string Name, string Folder, bool IsUnchanged, bool IsSelected);
