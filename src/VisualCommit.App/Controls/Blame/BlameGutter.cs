using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using VisualCommit.App.Controls.Diff;

namespace VisualCommit.App.Controls.Blame;

/// <summary>One run of the gutter as it draws it (docs/phase-5-design.md 4.2, BD11, BD12).</summary>
/// <param name="FirstLine">Its first line, from 1.</param>
/// <param name="LastLine">Its last line.</param>
/// <param name="IsEven">Run 0, 2, …: the panel's background; the others the window's.</param>
/// <param name="ShortSha">The commit's first 7 characters; empty for lines not committed yet.</param>
/// <param name="Author">The author, shown while the gutter is wide.</param>
/// <param name="Date">The date, <c>yyyy-MM-dd</c>; empty for lines not committed yet.</param>
/// <param name="IsUncommitted">Lines not committed yet: the first shows <c>Not committed yet</c> in the warning colour.</param>
/// <param name="ToolTip">The run's tooltip (BD11).</param>
public sealed record BlameGutterRun(int FirstLine, int LastLine, bool IsEven, string ShortSha, string Author, string Date, bool IsUncommitted, string ToolTip);

/// <summary>What a press on the gutter asked for: the run at the pointer and how many clicks.</summary>
/// <param name="Run">The run's index in <see cref="BlameGutter.Runs"/>.</param>
/// <param name="ClickCount">1 for a click, 2 for a double-click.</param>
public sealed class BlameRunPressedEventArgs(int run, int clickCount) : EventArgs
{
    public int Run { get; } = run;

    public int ClickCount { get; } = clickCount;
}

/// <summary>
/// The blame's gutter (docs/phase-5-design.md 4.2, 13.4: <c>BlameGutter</c>, a control element of
/// its own), left of the text's line numbers, sharing the text's top and line height and following
/// its scroll. Lines are grouped in runs of one commit; runs alternate between
/// <c>VcPanelBackgroundBrush</c> (run 0) and the window's background, and the picked run's lines
/// have <c>VcSelectionBrush</c>. A run's first line shows, from 4: the short SHA in the code font
/// at 12 in the primary colour, 6, while the gutter is wide the author in the UI font at 11 in the
/// secondary colour cut with "…", 6, then the date at 11 in the secondary colour (BD12); a run not
/// committed yet shows <c>Not committed yet</c> in <c>VcWarningBrush</c>. The pointer over a run
/// shows its tooltip.
/// </summary>
public sealed class BlameGutter : Control
{
    /// <summary>The padding at either end of a run's first line (BD12).</summary>
    public const double Padding = 4;

    /// <summary>The space between the SHA, the author and the date (BD12).</summary>
    public const double Gap = 6;

    /// <summary>The UI font's size of the author, the date and <c>Not committed yet</c>.</summary>
    public const double SmallFontSize = 11;

    /// <summary>What the first line of a run not committed yet says (4.2).</summary>
    public const string NotCommittedYetText = "Not committed yet";

    private IReadOnlyList<BlameGutterRun> _runs = [];
    private BlameTextEditor? _editor;
    private int? _selectedRun;
    private bool _showsAuthor;

    public BlameGutter()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>Raised when a run is pressed with the left button: a click picks it, a double-click shows it in the graph.</summary>
    public event EventHandler<BlameRunPressedEventArgs>? RunPressed;

    /// <summary>The text whose lines the gutter follows.</summary>
    public BlameTextEditor? Editor
    {
        get => _editor;
        set
        {
            if (_editor is { } old)
            {
                old.TextView.ScrollOffsetChanged -= OnTextViewChanged;
                old.TextView.VisualLinesChanged -= OnTextViewChanged;
            }

            _editor = value;
            if (value is not null)
            {
                value.TextView.ScrollOffsetChanged += OnTextViewChanged;
                value.TextView.VisualLinesChanged += OnTextViewChanged;
            }

            InvalidateVisual();
        }
    }

    /// <summary>The runs, top to bottom.</summary>
    public IReadOnlyList<BlameGutterRun> Runs
    {
        get => _runs;
        set
        {
            _runs = value ?? [];
            InvalidateVisual();
        }
    }

    /// <summary>The index of the run whose lines have the selection background, or null.</summary>
    public int? SelectedRun
    {
        get => _selectedRun;
        set
        {
            _selectedRun = value;
            InvalidateVisual();
        }
    }

    /// <summary>The author shows between the SHA and the date: while the gutter is wide (4.2).</summary>
    public bool ShowsAuthor
    {
        get => _showsAuthor;
        set
        {
            _showsAuthor = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// The index of the run of line <paramref name="line"/> (from 1), or null. The runs follow one
    /// another in line order (<see cref="ViewModels.Pages.BlameViewModel.RunsOf"/>), so a binary search finds it:
    /// every visible line of every frame asks, and a large, much-edited file has tens of thousands of runs.
    /// </summary>
    public int? RunOfLine(int line)
    {
        int low = 0, high = _runs.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var run = _runs[middle];
            if (line < run.FirstLine)
            {
                high = middle - 1;
            }
            else if (line > run.LastLine)
            {
                low = middle + 1;
            }
            else
            {
                return middle;
            }
        }

        return null;
    }

    /// <summary>The index of the run drawn at <paramref name="y"/> in the gutter's coordinates, or null.</summary>
    public int? RunAt(double y) =>
        _editor is { } editor && editor.LineAt(this.TranslatePoint(new Point(0, y), editor)?.Y ?? y) is { } line ? RunOfLine(line) : null;

    /// <summary>
    /// The resource key of the brush a run's lines are drawn on, as <see cref="Render"/> paints
    /// them: <c>VcSelectionBrush</c> for the picked run, otherwise <c>VcPanelBackgroundBrush</c>
    /// for run 0, 2, … and <c>VcWindowBackgroundBrush</c> for the others.
    /// </summary>
    public string BackgroundKeyOf(int run)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(run);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(run, _runs.Count);
        return run == _selectedRun ? "VcSelectionBrush" : _runs[run].IsEven ? "VcPanelBackgroundBrush" : "VcWindowBackgroundBrush";
    }

    /// <summary>
    /// The texts a run's first line draws, as the test surface reads them: the SHA, the author
    /// (empty while it is not shown), the date, or <c>Not committed yet</c> alone, each with its
    /// left edge and width in the gutter's coordinates.
    /// </summary>
    public IReadOnlyList<(string Text, double X, double Width)> TextsOf(int run)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(run);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(run, _runs.Count);
        return [.. Layout(_runs[run]).Select(part => (part.Text, part.X, part.Width))];
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new BlameGutterAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        var textView = _editor?.TextView;
        if (textView is null || !textView.VisualLinesValid || _runs.Count == 0)
        {
            return;
        }

        var brushes = new Dictionary<string, IBrush>(StringComparer.Ordinal);
        var offset = TopOffset(textView);
        foreach (var line in textView.VisualLines)
        {
            var number = line.FirstDocumentLine.LineNumber;
            if (RunOfLine(number) is not { } index)
            {
                continue;
            }

            var run = _runs[index];
            var top = line.VisualTop - textView.VerticalOffset + offset;
            var rect = new Rect(0, Math.Round(top), Bounds.Width, Math.Round(top + line.Height) - Math.Round(top));
            var key = BackgroundKeyOf(index);
            if (!brushes.TryGetValue(key, out var background))
            {
                brushes[key] = background = Brush(key);
            }

            context.FillRectangle(background, rect);
            if (number != run.FirstLine)
            {
                continue;
            }

            foreach (var part in Layout(run))
            {
                context.DrawText(part.Formatted, new Point(part.X, top + ((line.Height - part.Formatted.Height) / 2)));
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var tip = RunAt(e.GetPosition(this).Y) is { } index ? _runs[index].ToolTip : null;
        if (!Equals(ToolTip.GetTip(this), tip))
        {
            ToolTip.SetTip(this, tip);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed && RunAt(point.Position.Y) is { } index)
        {
            RunPressed?.Invoke(this, new BlameRunPressedEventArgs(index, e.ClickCount));
            e.Handled = true;
        }
    }

    /// <summary>Where the editor's text view starts below the gutter's top (the two share their top: 0 when laid out side by side).</summary>
    private double TopOffset(Avalonia.Visual textView) => textView.TranslatePoint(default, this)?.Y ?? 0;

    /// <summary>The parts of a run's first line, from the left (BD12).</summary>
    private List<(string Text, FormattedText Formatted, double X, double Width)> Layout(BlameGutterRun run)
    {
        var parts = new List<(string, FormattedText, double, double)>();
        var secondary = Brush("VcTextSecondaryBrush");
        var ui = new Typeface(GetValue(TextElement.FontFamilyProperty));
        if (run.IsUncommitted)
        {
            var text = Text(NotCommittedYetText, ui, SmallFontSize, Brush("VcWarningBrush"), Bounds.Width - (2 * Padding));
            parts.Add((NotCommittedYetText, text, Padding, text.WidthIncludingTrailingWhitespace));
            return parts;
        }

        var code = new Typeface(_editor?.FontFamily ?? GetValue(TextElement.FontFamilyProperty));
        var sha = Text(run.ShortSha, code, DiffPalette.CodeFontSize, Brush("VcTextPrimaryBrush"), double.PositiveInfinity);
        var shaWidth = sha.WidthIncludingTrailingWhitespace;
        parts.Add((run.ShortSha, sha, Padding, shaWidth));
        var dateNatural = Text(run.Date, ui, SmallFontSize, secondary, double.PositiveInfinity).WidthIncludingTrailingWhitespace;
        var afterSha = Padding + shaWidth + Gap;
        if (_showsAuthor)
        {
            var authorRoom = Math.Max(0, Bounds.Width - afterSha - Gap - dateNatural - Padding);
            var author = Text(run.Author, ui, SmallFontSize, secondary, authorRoom);
            parts.Add((run.Author, author, afterSha, authorRoom));
            afterSha += authorRoom + Gap;
        }

        // The date is cut with "…" only if it ever does not fit; the SHA never is (BD12).
        var dateRoom = Math.Max(0, Bounds.Width - afterSha - Padding);
        var date = Text(run.Date, ui, SmallFontSize, secondary, dateRoom);
        parts.Add((run.Date, date, afterSha, Math.Min(dateNatural, dateRoom)));
        return parts;
    }

    private static FormattedText Text(string text, Typeface typeface, double size, IBrush brush, double maxWidth)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, brush);
        if (!double.IsInfinity(maxWidth))
        {
            formatted.MaxTextWidth = Math.Max(1, maxWidth);
            formatted.MaxLineCount = 1;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }

        return formatted;
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Transparent;

    private void OnTextViewChanged(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>The gutter as UI Automation sees it: a control element of its own (13.4), so that the real-window pass can find its bounds.</summary>
    private sealed class BlameGutterAutomationPeer(BlameGutter owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
