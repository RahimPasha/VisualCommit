using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.Controls.Diff;

/// <summary>
/// Shows one file's diff as text (D61, D64), built on AvaloniaEdit: inline in one column, or side
/// by side with the old file on the left and the new on the right, scrolling together. Each side
/// has a gutter with the line numbers and the "+" or "-" sign; rows have the added, removed or
/// context background; each hunk starts with a 26-high header row with git's header line and the
/// hunk's buttons. Syntax colours come from TextMate grammars (<see cref="DiffHighlighter"/>),
/// changed words of paired lines have a stronger background (<see cref="DiffWordHighlights"/>).
/// Rows are selected by their numbers or by dragging over the text (D70);
/// <see cref="SelectedChanges"/> holds the added and removed lines selected.
/// </summary>
/// <remarks>
/// <para>
/// Property changes are applied together before the next layout (or the next query): setting
/// <see cref="FilePath"/> and <see cref="Diff"/> one after the other builds the view once. A new
/// diff for the same path keeps the vertical scroll position (clamped to the new length) and, when
/// its lines are the same as before, the selection too. A new path scrolls to the top. A new mode
/// keeps the line that was at the top of the view at the top. Any other change of the diff or the
/// mode clears the selection.
/// </para>
/// <para>
/// Diffs of more than <see cref="SynchronousHighlightLimit"/> lines are tokenized on a background
/// thread and coloured when that finishes; smaller ones before they are first drawn.
/// </para>
/// <para>
/// "Its own side" (for <see cref="LineNumberPoint"/>, <see cref="TextRangeBounds"/>,
/// <see cref="ForegroundAt"/> and <see cref="FontWeightAt"/>): inline the one column; side by side
/// a removed line is on the left, an added line on the right, and a context line, which is on
/// both, is taken on the right. Inline, a removed line's number is in the old column and any other
/// line's in the new column.
/// </para>
/// </remarks>
public sealed class DiffTextView : UserControl
{
    /// <summary>A diff of at most this many lines is tokenized on the UI thread, before it is drawn.</summary>
    public const int SynchronousHighlightLimit = 1_000;

    public static readonly StyledProperty<FileDiff?> DiffProperty =
        AvaloniaProperty.Register<DiffTextView, FileDiff?>(nameof(Diff));

    public static readonly StyledProperty<DiffMode> ModeProperty =
        AvaloniaProperty.Register<DiffTextView, DiffMode>(nameof(Mode));

    public static readonly StyledProperty<string?> FilePathProperty =
        AvaloniaProperty.Register<DiffTextView, string?>(nameof(FilePath));

    public static readonly StyledProperty<bool> HighlightingProperty =
        AvaloniaProperty.Register<DiffTextView, bool>(nameof(Highlighting), true);

    public static readonly StyledProperty<HunkActions> HunkActionsProperty =
        AvaloniaProperty.Register<DiffTextView, HunkActions>(nameof(HunkActions));

    public static readonly DirectProperty<DiffTextView, IReadOnlySet<DiffLineRef>> SelectedChangesProperty =
        AvaloniaProperty.RegisterDirect<DiffTextView, IReadOnlySet<DiffLineRef>>(nameof(SelectedChanges), view => view.SelectedChanges);

    private static readonly IReadOnlySet<DiffLineRef> NoChanges = ImmutableHashSet<DiffLineRef>.Empty;

    private readonly Grid _grid;
    private readonly DiffPane _left;
    private readonly DiffPane _right;
    private readonly Border _divider;

    private IReadOnlyList<DiffDisplayRow> _rows = [];
    private IReadOnlySet<DiffLineRef> _selectedChanges = NoChanges;
    private DiffPalette? _palette;
    private CancellationTokenSource? _tokenizing;

    // What the view was last built from (EnsureBuilt compares the properties with them).
    private bool _dirty = true;
    private bool _built;
    private FileDiff? _builtDiff;
    private DiffMode _builtMode;
    private string? _builtPath;
    private bool _builtHighlighting;
    private HunkActions _builtActions;

    private PendingScroll? _pendingScroll;
    private bool _pendingFocus;
    private bool _syncingScroll;

    public DiffTextView()
    {
        _left = new DiffPane(this);
        _right = new DiffPane(this);
        _divider = new Border { Width = 1 };
        Grid.SetColumn(_divider, 1);
        Grid.SetColumn(_right.Editor, 2);
        _grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,0,0") };
        _grid.Children.Add(_left.Editor);
        _grid.Children.Add(_divider);
        _grid.Children.Add(_right.Editor);
        Content = _grid;

        AutomationProperties.SetAutomationId(this, "DiffTextView");
        AutomationProperties.SetName(this, "Diff");
        ApplyModeLayout(DiffMode.Inline);

        ActualThemeVariantChanged += (_, _) => OnThemeChanged();
        LayoutUpdated += (_, _) => OnLayoutUpdated();
    }

    /// <summary>A hunk header's button was clicked.</summary>
    public event EventHandler<HunkActionEventArgs>? HunkActionRequested;

    /// <summary>The diff to show; null shows nothing.</summary>
    public FileDiff? Diff
    {
        get => GetValue(DiffProperty);
        set => SetValue(DiffProperty, value);
    }

    /// <summary>Inline (one column) or side by side (D61).</summary>
    public DiffMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>The file's repository-relative path. Its extension picks the grammar; null or an unknown extension shows no syntax colours.</summary>
    public string? FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    /// <summary>Syntax colours and word-level highlights; true by default. Off for a very large diff shown on request (D65).</summary>
    public bool Highlighting
    {
        get => GetValue(HighlightingProperty);
        set => SetValue(HighlightingProperty, value);
    }

    /// <summary>Which buttons each hunk header shows.</summary>
    public HunkActions HunkActions
    {
        get => GetValue(HunkActionsProperty);
        set => SetValue(HunkActionsProperty, value);
    }

    /// <summary>
    /// The added and removed lines among the selected rows (never context lines); side by side,
    /// the removed lines of a selection on the left or the added lines of one on the right. Empty
    /// when nothing is selected. A new set each time it changes.
    /// </summary>
    public IReadOnlySet<DiffLineRef> SelectedChanges
    {
        get => _selectedChanges;
        private set => SetAndRaise(SelectedChangesProperty, ref _selectedChanges, value);
    }

    /// <summary>The rows as displayed in the current mode.</summary>
    public IReadOnlyList<DiffDisplayRow> Rows
    {
        get
        {
            EnsureBuilt();
            return _rows;
        }
    }

    internal DiffPalette Palette => _palette ??= DiffPalette.Resolve(this);

    /// <summary>The syntax tokens of the diff, or null: no grammar, highlighting off, or still tokenizing.</summary>
    internal DiffSyntax? Syntax { get; private set; }

    /// <summary>The word-level highlights of the diff; empty when highlighting is off.</summary>
    internal IReadOnlyDictionary<DiffLineRef, IReadOnlyList<TextRange>> Words { get; private set; } =
        new Dictionary<DiffLineRef, IReadOnlyList<TextRange>>();

    private bool IsSideBySide => _builtMode == DiffMode.SideBySide;

    /// <summary>Selects nothing.</summary>
    public void ClearSelection()
    {
        EnsureBuilt();
        _left.ClearSelectionQuietly();
        _right.ClearSelectionQuietly();
        UpdateSelectedChanges();
    }

    /// <summary>Gives the text the keyboard focus (the left pane side by side). Before the view is shown, it gets the focus once it is.</summary>
    public void FocusBody()
    {
        _pendingFocus = true;
        TryFocus();
    }

    /// <summary>Where row <paramref name="row"/> is, across the view's whole width, in this control's coordinates; null when it is not laid out in view.</summary>
    public Rect? RowBounds(int row)
    {
        EnsureBuilt();
        var pane = _left;
        if (pane.VisualLineOf(row) is not { } line || ToThis(pane.TextView, new Point(0, pane.TopOf(line))) is not { } top)
        {
            return null;
        }

        return new Rect(0, top.Y, Bounds.Width, pane.BottomOf(line) - pane.TopOf(line));
    }

    /// <summary>Where to click to select a line: the centre of its number cell on its own side, in this control's coordinates; null when it is not in view.</summary>
    public Point? LineNumberPoint(DiffLineRef line)
    {
        EnsureBuilt();
        if (Locate(line) is not var (pane, row) || pane.VisualLineOf(row) is not { } visual)
        {
            return null;
        }

        var column = pane.Side == PaneSide.Inline && pane.CellOf(_rows[row])!.Kind != DiffLineKind.Removed ? 1 : 0;
        var y = (pane.TopOf(visual) + pane.BottomOf(visual)) / 2;
        return ToThis(pane.Gutter, new Point(pane.Gutter.ColumnCenter(column), y));
    }

    /// <summary>Where characters [<paramref name="start"/>, <paramref name="start"/> + <paramref name="length"/>) of a line's text are drawn on its own side, in this control's coordinates; null when not in view.</summary>
    public Rect? TextRangeBounds(DiffLineRef line, int start, int length)
    {
        EnsureBuilt();
        if (Locate(line) is not var (pane, row) || pane.VisualLineOf(row) is not { } visual)
        {
            return null;
        }

        var startColumn = visual.GetVisualColumn(start);
        var endColumn = visual.GetVisualColumn(start + length);
        Rect? union = null;
        foreach (var rect in BackgroundGeometryBuilder.GetRectsFromVisualSegment(pane.TextView, visual, startColumn, endColumn))
        {
            union = union is { } before ? before.Union(rect) : rect;
        }

        if (union is not { } bounds || ToThis(pane.TextView, bounds.TopLeft) is not { } topLeft)
        {
            return null;
        }

        return new Rect(topLeft, bounds.Size);
    }

    /// <summary>The brush the character at <paramref name="column"/> of a line's text is drawn with on its own side; null when the view is not laid out.</summary>
    public IBrush? ForegroundAt(DiffLineRef line, int column) => ElementAt(line, column)?.TextRunProperties.ForegroundBrush;

    /// <summary>The weight the character at <paramref name="column"/> of a line's text is drawn with on its own side.</summary>
    public FontWeight FontWeightAt(DiffLineRef line, int column) => ElementAt(line, column)?.TextRunProperties.Typeface.Weight ?? FontWeight.Normal;

    internal void RaiseHunkAction(int hunk, HunkAction action) => HunkActionRequested?.Invoke(this, new HunkActionEventArgs(hunk, action));

    /// <summary>A pane's selection changed: a selection in one pane clears the other's.</summary>
    internal void OnPaneSelectionChanged(DiffPane pane)
    {
        if (pane.Selection is not null)
        {
            var other = pane == _left ? _right : _left;
            if (other.Selection is not null)
            {
                other.ClearSelectionQuietly();
            }
        }

        UpdateSelectedChanges();
    }

    /// <summary>Side by side, a pane scrolled: the other follows, both ways.</summary>
    internal void OnPaneScrolled(DiffPane pane)
    {
        if (!IsSideBySide || _syncingScroll)
        {
            return;
        }

        // A half whose lines are shorter cannot scroll as far sideways: it stops at its end, and the
        // offset it is clamped to is not sent back.
        var other = pane == _left ? _right : _left;
        var offset = pane.Editor.ScrollOffset;
        if (other.Editor.ScrollOffset == offset)
        {
            return;
        }

        _syncingScroll = true;
        try
        {
            other.Editor.ScrollTo(offset.X, offset.Y);
        }
        finally
        {
            _syncingScroll = false;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DiffTextViewAutomationPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DiffProperty
            || change.Property == ModeProperty
            || change.Property == FilePathProperty
            || change.Property == HighlightingProperty
            || change.Property == HunkActionsProperty)
        {
            _dirty = true;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureBuilt();
        return base.MeasureOverride(availableSize);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        OnThemeChanged();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _tokenizing?.Cancel();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// Applies the property changes since the last build: new rows and documents for a new diff or
    /// mode, new tokens and word highlights for a new diff, path or highlighting, new header bars
    /// for new buttons; and decides where the view scrolls to.
    /// </summary>
    private void EnsureBuilt()
    {
        if (!_dirty)
        {
            return;
        }

        _dirty = false;
        var diff = Diff;
        var mode = Mode;
        var path = FilePath;
        var highlighting = Highlighting;
        var actions = HunkActions;

        var pathChanged = _built && !string.Equals(path, _builtPath, StringComparison.Ordinal);
        var diffChanged = !_built || !ReferenceEquals(diff, _builtDiff);
        if (diffChanged && _built && !pathChanged && SameLines(diff, _builtDiff))
        {
            // The same lines again (a refresh): keep everything, the selection included.
            diffChanged = false;
        }

        var modeChanged = _built && mode != _builtMode;
        var highlightingChanged = !_built || highlighting != _builtHighlighting;

        if (pathChanged)
        {
            _pendingScroll = new PendingScroll(0, null);
        }
        else if (diffChanged && _built)
        {
            _pendingScroll = new PendingScroll(_left.Editor.ScrollOffset.Y, null, KeepHorizontal: true);
        }
        else if (modeChanged)
        {
            _pendingScroll = new PendingScroll(0, FirstLineInView());
        }

        _built = true;
        _builtDiff = diff;
        _builtMode = mode;
        _builtPath = path;
        _builtHighlighting = highlighting;

        if (diffChanged || pathChanged || highlightingChanged)
        {
            UpdateHighlights(diff, path, highlighting);
        }

        if (diffChanged || modeChanged)
        {
            _rows = diff is null ? [] : DiffLayout.Build(diff, mode);
            ApplyModeLayout(mode);
            if (mode == DiffMode.SideBySide)
            {
                _left.Show(PaneSide.Left, _rows);
                _right.Show(PaneSide.Right, _rows);
            }
            else
            {
                _left.Show(PaneSide.Inline, _rows);
                _right.Show(PaneSide.Right, []);
            }

            UpdateSelectedChanges();
        }
        else if (pathChanged || highlightingChanged || actions != _builtActions)
        {
            if (pathChanged)
            {
                _left.ClearSelectionQuietly();
                _right.ClearSelectionQuietly();
                UpdateSelectedChanges();
            }

            _left.Redraw();
            _right.Redraw();
        }

        _builtActions = actions;
    }

    /// <summary>Starts tokenizing and comparing words for the diff, or clears them.</summary>
    private void UpdateHighlights(FileDiff? diff, string? path, bool highlighting)
    {
        _tokenizing?.Cancel();
        _tokenizing = null;
        Syntax = null;
        Words = new Dictionary<DiffLineRef, IReadOnlyList<TextRange>>();
        if (diff is null || !highlighting)
        {
            return;
        }

        Words = DiffWordHighlights.Compute(diff);
        if (diff.LineCount <= SynchronousHighlightLimit)
        {
            Syntax = TokenizeOrNothing(diff, path, CancellationToken.None);
            return;
        }

        var cancellation = new CancellationTokenSource();
        _tokenizing = cancellation;
        _ = Task.Run(() => TokenizeOrNothing(diff, path, cancellation.Token), cancellation.Token)
            .ContinueWith(
                task =>
                {
                    if (task.IsCompletedSuccessfully && task.Result is { } syntax && ReferenceEquals(_tokenizing, cancellation))
                    {
                        _tokenizing = null;
                        Syntax = syntax;
                        _left.Redraw();
                        _right.Redraw();
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// The diff's tokens, or null. Highlighting is an extra: when TextMate fails (a grammar it
    /// cannot load, its native regular-expression library missing), the diff shows without colours
    /// rather than not at all.
    /// </summary>
    private static DiffSyntax? TokenizeOrNothing(FileDiff diff, string? path, CancellationToken cancellationToken)
    {
        try
        {
            return DiffHighlighter.Tokenize(diff, path, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>Inline: one pane over the whole width. Side by side: two equal halves with a 1-pixel line between them.</summary>
    private void ApplyModeLayout(DiffMode mode)
    {
        var sideBySide = mode == DiffMode.SideBySide;
        _grid.ColumnDefinitions[1].Width = sideBySide ? new GridLength(1) : new GridLength(0);
        _grid.ColumnDefinitions[2].Width = sideBySide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        _right.Editor.IsVisible = sideBySide;
        _divider.IsVisible = sideBySide;

        // Side by side the right half's scroll bar scrolls both halves; the left one would sit in the middle.
        _left.Editor.VerticalScrollBarVisibility = sideBySide
            ? Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden
            : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;

        AutomationProperties.SetAutomationId(_left.Editor, sideBySide ? "DiffTextLeft" : "DiffText");
        AutomationProperties.SetName(_left.Editor, sideBySide ? "Old file" : "Diff text");
        AutomationProperties.SetAutomationId(_right.Editor, "DiffTextRight");
        AutomationProperties.SetName(_right.Editor, "New file");
    }

    private void OnThemeChanged()
    {
        _palette = null;
        _divider.Background = Palette.Border;
        _left.Redraw();
        _right.Redraw();
    }

    /// <summary>
    /// After a layout pass: the panes that show new rows learn their header rows' height (which
    /// cannot be done inside their own layout), then the view scrolls where a new diff or mode
    /// asked, once the scroll extent counts those heights; and the focus asked for before the view
    /// could take it is given.
    /// </summary>
    private void OnLayoutUpdated()
    {
        var builtHeaders = false;
        foreach (var pane in IsSideBySide ? new[] { _left, _right } : [_left])
        {
            if (pane.HeadersPending && pane.TextView.Bounds.Height > 0 && pane.TextView.VisualLinesValid)
            {
                pane.BuildHeaderLines();
                pane.TextView.InvalidateMeasure();
                builtHeaders = true;
            }
        }

        if (!builtHeaders && _pendingScroll is { } scroll && _left.TextView.Bounds.Height > 0 && _left.TextView.VisualLinesValid)
        {
            _pendingScroll = null;
            var offset = scroll.Offset;
            if (scroll.Line is { } line && RowOf(line) is { } row)
            {
                offset = _left.TextView.GetVisualTopByDocumentLine(row + 1);
            }

            var horizontal = scroll.KeepHorizontal ? (double?)null : 0;
            _left.Editor.ScrollTo(horizontal, offset);
            if (IsSideBySide)
            {
                _right.Editor.ScrollTo(horizontal, offset);
            }
        }

        if (_pendingFocus)
        {
            TryFocus();
        }
    }

    private void TryFocus()
    {
        if (_left.Editor.TextArea.Focus())
        {
            _pendingFocus = false;
        }
    }

    /// <summary>
    /// The diff line of the first row whose top is in view (or that row's hunk header), to keep at
    /// the top when the mode changes. A row cut by the view's top edge is passed over.
    /// </summary>
    private (int Hunk, DiffLineRef? Line)? FirstLineInView()
    {
        var textView = _left.TextView;
        if (!textView.VisualLinesValid || textView.VisualLines.Count == 0)
        {
            return null;
        }

        var line = textView.VisualLines.FirstOrDefault(visual => _left.TopOf(visual) >= 0) ?? textView.VisualLines[0];
        var row = line.FirstDocumentLine.LineNumber - 1;
        if (row < 0 || row >= _rows.Count)
        {
            return null;
        }

        var display = _rows[row];
        return (display.Hunk, display.Kind == DiffRowKind.Line ? (display.Left ?? display.Right)?.Line : null);
    }

    /// <summary>The row that shows a line (or the hunk's header when the line is null).</summary>
    private int? RowOf((int Hunk, DiffLineRef? Line) target)
    {
        for (var r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r];
            if (target.Line is { } line
                ? row.Kind == DiffRowKind.Line && (row.Left?.Line == line || row.Right?.Line == line)
                : row.Kind == DiffRowKind.HunkHeader && row.Hunk == target.Hunk)
            {
                return r;
            }
        }

        return null;
    }

    /// <summary>The pane and row that show a line on its own side (see the remarks).</summary>
    private (DiffPane Pane, int Row)? Locate(DiffLineRef line)
    {
        if (Diff is not { } diff || line.Hunk < 0 || line.Hunk >= diff.Hunks.Count || line.Line < 0 || line.Line >= diff.Hunks[line.Hunk].Lines.Count)
        {
            return null;
        }

        var pane = !IsSideBySide ? _left : diff.Hunks[line.Hunk].Lines[line.Line].Kind == DiffLineKind.Removed ? _left : _right;
        for (var r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r];
            if (row.Kind == DiffRowKind.Line && pane.CellOf(row)?.Line == line)
            {
                return (pane, r);
            }
        }

        return null;
    }

    /// <summary>The visual line element that draws a character of a line's text on its own side.</summary>
    private VisualLineElement? ElementAt(DiffLineRef line, int column)
    {
        EnsureBuilt();
        if (Locate(line) is not var (pane, row) || pane.Editor.Document is not { } document)
        {
            return null;
        }

        VisualLine visual;
        try
        {
            pane.TextView.EnsureVisualLines();
            visual = pane.TextView.GetOrConstructVisualLine(document.GetLineByNumber(row + 1));
        }
        catch (Exception exception) when (exception is InvalidOperationException or VisualLinesInvalidException)
        {
            // The text view has not been measured yet.
            return null;
        }

        foreach (var element in visual.Elements)
        {
            if (column >= element.RelativeTextOffset && column < element.RelativeTextOffset + element.DocumentLength)
            {
                return element;
            }
        }

        return null;
    }

    private void UpdateSelectedChanges()
    {
        var changes = new List<DiffLineRef>();
        foreach (var pane in new[] { _left, _right })
        {
            if (pane.Selection is not var (anchor, active))
            {
                continue;
            }

            for (var r = Math.Min(anchor, active); r <= Math.Max(anchor, active) && r < _rows.Count; r++)
            {
                if (_rows[r].Kind == DiffRowKind.Line && pane.CellOf(_rows[r]) is { Kind: not DiffLineKind.Context } cell)
                {
                    changes.Add(cell.Line);
                }
            }
        }

        if (!changes.ToHashSet().SetEquals(SelectedChanges))
        {
            SelectedChanges = changes.Count == 0 ? NoChanges : ImmutableHashSet.CreateRange(changes);
        }
    }

    private Point? ToThis(Visual from, Point point) => from.TranslatePoint(point, this);

    /// <summary>Two diffs with the same hunks and lines, as a refresh that found nothing new gives.</summary>
    private static bool SameLines(FileDiff? a, FileDiff? b)
    {
        if (a is null || b is null || a.Hunks.Count != b.Hunks.Count)
        {
            return false;
        }

        for (var h = 0; h < a.Hunks.Count; h++)
        {
            if (a.Hunks[h].Header != b.Hunks[h].Header || !a.Hunks[h].Lines.SequenceEqual(b.Hunks[h].Lines))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Where to scroll once the new rows are laid out: a vertical offset, or the row of a line to
    /// put at the top; sideways to the start, or where it is.
    /// </summary>
    private sealed record PendingScroll(double Offset, (int Hunk, DiffLineRef? Line)? Line, bool KeepHorizontal = false);

    /// <summary>Exposes the view to UI Automation as one element that holds the text areas and the hunk buttons.</summary>
    private sealed class DiffTextViewAutomationPeer(DiffTextView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
