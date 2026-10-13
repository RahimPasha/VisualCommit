using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Diff;
using VisualCommit.Core.Conflicts;
using VisualCommit.Core.Diff;

namespace VisualCommit.App.Controls.Conflicts;

/// <summary>Which side of a conflict a pane shows.</summary>
public enum ConflictPaneSide
{
    /// <summary>The left pane: the file with every block's ours side.</summary>
    Ours,

    /// <summary>The right pane: the file with every block's theirs side.</summary>
    Theirs,
}

/// <summary>What a block bar's button asks for (docs/phase-4-design.md 11.5).</summary>
public enum ConflictChoice
{
    /// <summary>"Use ours" (<c>ConflictUseOurs</c>): the block becomes its ours lines.</summary>
    UseOurs,

    /// <summary>"Use both" in the ours pane (<c>ConflictUseBothOursFirst</c>): ours, then theirs.</summary>
    UseBothOursFirst,

    /// <summary>"Use theirs" (<c>ConflictUseTheirs</c>): the block becomes its theirs lines.</summary>
    UseTheirs,

    /// <summary>"Use both" in the theirs pane (<c>ConflictUseBothTheirsFirst</c>): theirs, then ours.</summary>
    UseBothTheirsFirst,
}

/// <summary>A block bar's button was clicked.</summary>
/// <param name="block">The block, from 0, in the file's order.</param>
/// <param name="choice">What the button asks for.</param>
public sealed class ConflictChoiceEventArgs(int block, ConflictChoice choice) : EventArgs
{
    /// <summary>The block, from 0, in the file's order.</summary>
    public int Block { get; } = block;

    /// <summary>What the button asks for.</summary>
    public ConflictChoice Choice { get; } = choice;
}

/// <summary>
/// One piece of a conflicted file as the panes show it: lines both sides have, or a block with
/// each side's lines. The resolver's model (<c>ConflictText</c>) gives the panes these.
/// </summary>
/// <param name="Ours">The ours side's lines (a common piece: its lines).</param>
/// <param name="Theirs">The theirs side's lines (a common piece: the same lines).</param>
/// <param name="IsBlock">A block; otherwise lines both sides have.</param>
public sealed record ConflictPanesPart(IReadOnlyList<string> Ours, IReadOnlyList<string> Theirs, bool IsBlock)
{
    /// <summary>Lines both sides have.</summary>
    public static ConflictPanesPart Common(params IReadOnlyList<string> lines) => new(lines, lines, false);

    /// <summary>A block with each side's lines.</summary>
    public static ConflictPanesPart Block(IReadOnlyList<string> ours, IReadOnlyList<string> theirs) => new(ours, theirs, true);
}

/// <summary>A row of a conflict pane as tests and drivers read it.</summary>
/// <param name="Text">The row's text: a line, or empty for a bar or a filler.</param>
/// <param name="Tint">The row's background.</param>
/// <param name="IsBar">The row is a block's bar.</param>
/// <param name="Number">The line's number in the pane's own file, or null for a bar or a filler.</param>
public sealed record ConflictPaneRow(string Text, ConflictTint Tint, bool IsBar, int? Number)
{
    /// <summary>The block the row belongs to (its bar, its lines, its fillers), from 0; null for a common line.</summary>
    public int? Block { get; init; }

    /// <summary>A bar's state on this pane's side: its buttons, or the text that says whether the side is used; null for any other row.</summary>
    public ConflictSideState? BarState { get; init; }

    /// <summary>What a bar shows instead of its buttons (<c>Used in the result</c>, <c>Not used</c>); null while it shows its buttons, and for any other row.</summary>
    public string? BarText { get; init; }
}

/// <summary>
/// What one side of a block shows (docs/phase-4-design.md 11.5, 11.6): while the block's markers
/// are in the result, its bar's buttons and the side's tint; once a choice took the block out of
/// the result, whether this side is in what replaced it.
/// </summary>
public enum ConflictSideState
{
    /// <summary>The block is unresolved: the bar shows its buttons and the side's rows its ours or theirs tint.</summary>
    Choose,

    /// <summary>The side is in the result: the bar says <c>Used in the result</c>, its rows take <c>VcConflictUsedBrush</c>.</summary>
    Used,

    /// <summary>The side is not in the result: the bar says <c>Not used</c>, its rows lose their tint.</summary>
    NotUsed,
}

/// <summary>The state of both sides of one block (<see cref="ConflictPanes.Bars"/>).</summary>
/// <param name="Ours">The ours pane's side.</param>
/// <param name="Theirs">The theirs pane's side.</param>
public readonly record struct ConflictBlockBars(ConflictSideState Ours, ConflictSideState Theirs)
{
    /// <summary>An unresolved block: both bars show their buttons.</summary>
    public static ConflictBlockBars Open { get; } = new(ConflictSideState.Choose, ConflictSideState.Choose);
}

/// <summary>
/// The conflict resolver's top half (docs/phase-4-design.md 11.2, 11.3, 11.5): the ours pane on
/// the left and the theirs pane on the right, each half the width with a 1-pixel line between,
/// built on the diff's panes (<see cref="DiffPane"/>). The ours pane shows the file with every
/// block's ours lines, the theirs pane with every block's theirs lines; their rows line up, the
/// shorter side of a block filled with filler rows, and before each block both panes have a bar
/// 26 high with its buttons ("Use ours" and "Use both" in the ours pane, "Use theirs" and "Use
/// both" in the theirs pane). Each pane has one number column counting its own lines and no sign
/// column; rows are not selected, a drag selects text. The panes scroll together; the theirs
/// pane's scroll bar scrolls both. Syntax colours as the diff's (D64): the ours file is
/// tokenized as a diff's old side, the theirs file as its new side.
/// </summary>
/// <remarks>
/// Rows are built from <see cref="Parts"/> before the next layout (or the next query), as a
/// synthetic one-hunk diff: a common line is a context line, a block's ours lines removed lines
/// and its theirs lines added lines, so that <see cref="DiffHighlighter"/> and
/// <see cref="DiffPane"/> work on it unchanged. A block's bar is a header row whose hunk number
/// is the block's. <see cref="Bars"/> says, per block, what each side's bar shows and how its
/// rows are tinted; a change of it redraws the bars and the backgrounds without moving the view.
/// The panes' texts are no tab stops: Tab goes from the bars' buttons of the ours pane to those
/// of the theirs pane (11.10).
/// </remarks>
public sealed class ConflictPanes : UserControl, IDiffPaneHost
{
    /// <summary>A bar's text once its side is in the result (11.5).</summary>
    public const string UsedText = "Used in the result";

    /// <summary>A bar's text once its side is not in the result (11.5).</summary>
    public const string NotUsedText = "Not used";

    /// <summary>The automation id of a bar's text (7.4).</summary>
    public const string BarStateAutomationId = "ConflictBarState";

    public static readonly StyledProperty<IReadOnlyList<ConflictPanesPart>?> PartsProperty =
        AvaloniaProperty.Register<ConflictPanes, IReadOnlyList<ConflictPanesPart>?>(nameof(Parts));

    public static readonly StyledProperty<IReadOnlyList<ConflictBlockBars>?> BarsProperty =
        AvaloniaProperty.Register<ConflictPanes, IReadOnlyList<ConflictBlockBars>?>(nameof(Bars));

    public static readonly StyledProperty<string?> FilePathProperty =
        AvaloniaProperty.Register<ConflictPanes, string?>(nameof(FilePath));

    public static readonly StyledProperty<string?> OursAutomationIdProperty =
        AvaloniaProperty.Register<ConflictPanes, string?>(nameof(OursAutomationId), "ConflictOurs");

    public static readonly StyledProperty<string?> TheirsAutomationIdProperty =
        AvaloniaProperty.Register<ConflictPanes, string?>(nameof(TheirsAutomationId), "ConflictTheirs");

    private static readonly IReadOnlyDictionary<DiffLineRef, IReadOnlyList<TextRange>> NoWords =
        new Dictionary<DiffLineRef, IReadOnlyList<TextRange>>();

    private readonly DiffPane _ours;
    private readonly DiffPane _theirs;
    private readonly Border _divider;

    private IReadOnlyList<DiffDisplayRow> _rows = [];
    private int[] _barRows = [];
    private DiffPalette? _palette;
    private bool _dirty = true;
    private bool _pendingTop;
    private int? _pendingReveal;
    private Vector? _pendingOffset;
    private bool _syncingScroll;

    public ConflictPanes()
    {
        _ours = new DiffPane(this);
        _theirs = new DiffPane(this);
        _divider = new Border { Width = 1 };
        Grid.SetColumn(_divider, 1);
        Grid.SetColumn(_theirs.Editor, 2);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,1,*") };
        grid.Children.Add(_ours.Editor);
        grid.Children.Add(_divider);
        grid.Children.Add(_theirs.Editor);
        Content = grid;

        // The theirs pane's scroll bar scrolls both; the ours one would sit in the middle.
        _ours.Editor.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        AutomationProperties.SetName(_ours.Editor, "Ours");
        AutomationProperties.SetName(_theirs.Editor, "Theirs");
        ApplyAutomationIds();

        // The texts are read and selected with the mouse; the keyboard goes from bar to bar.
        // AvaloniaEdit's text area keeps Tab to itself (it types a tab): here it lets the
        // keyboard's navigation through to the bars' buttons.
        foreach (var pane in new[] { _ours, _theirs })
        {
            pane.Editor.IsTabStop = false;
            pane.Editor.TextArea.IsTabStop = false;
            KeyboardNavigation.SetTabNavigation(pane.Editor, KeyboardNavigationMode.Continue);
            KeyboardNavigation.SetTabNavigation(pane.Editor.TextArea, KeyboardNavigationMode.Continue);

            // The text area takes the focus itself whenever an element inside it gets it: a bar's
            // button keeps it (the event stops at the bars' layer, below the text area).
            pane.Headers.AddHandler(GotFocusEvent, (_, e) => e.Handled = true);
        }

        ActualThemeVariantChanged += (_, _) => OnThemeChanged();
        LayoutUpdated += (_, _) => OnLayoutUpdated();
    }

    /// <summary>A block bar's button was clicked.</summary>
    public event EventHandler<ConflictChoiceEventArgs>? ChoiceRequested;

    /// <summary>The file's pieces, in order; null shows nothing.</summary>
    public IReadOnlyList<ConflictPanesPart>? Parts
    {
        get => GetValue(PartsProperty);
        set => SetValue(PartsProperty, value);
    }

    /// <summary>
    /// What each block's bars show and how its sides are tinted, by block (11.5); a block past
    /// the list's end, or a null list, is unresolved (<see cref="ConflictBlockBars.Open"/>).
    /// </summary>
    public IReadOnlyList<ConflictBlockBars>? Bars
    {
        get => GetValue(BarsProperty);
        set => SetValue(BarsProperty, value);
    }

    /// <summary>The panes' scroll offset (both scroll together), for keeping it while another tab's resolver is shown.</summary>
    public Vector ScrollOffset => _theirs.Editor.ScrollOffset;

    /// <summary>The file's repository-relative path: its extension picks the grammar.</summary>
    public string? FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    /// <summary>The ours pane's automation id (<c>ConflictOurs</c> unless its owner sets another).</summary>
    public string? OursAutomationId
    {
        get => GetValue(OursAutomationIdProperty);
        set => SetValue(OursAutomationIdProperty, value);
    }

    /// <summary>The theirs pane's automation id (<c>ConflictTheirs</c> unless its owner sets another).</summary>
    public string? TheirsAutomationId
    {
        get => GetValue(TheirsAutomationIdProperty);
        set => SetValue(TheirsAutomationIdProperty, value);
    }

    TemplatedControl IDiffPaneHost.View => this;

    DiffPalette IDiffPaneHost.Palette => Palette;

    /// <summary>The syntax tokens of both sides, or null: no grammar for the file.</summary>
    DiffSyntax? IDiffPaneHost.Syntax => Syntax;

    IReadOnlyDictionary<DiffLineRef, IReadOnlyList<TextRange>> IDiffPaneHost.Words => NoWords;

    bool IDiffPaneHost.RowsSelectable => false;

    bool IDiffPaneHost.ShowsSign => false;

    private DiffPalette Palette => _palette ??= DiffPalette.Resolve(this);

    private DiffSyntax? Syntax { get; set; }

    /// <summary>The automation id of the button that asks for <paramref name="choice"/> in <paramref name="side"/>'s bar, or null when that pane has no such button.</summary>
    public static string? AutomationIdOf(ConflictPaneSide side, ConflictChoice choice) => (side, choice) switch
    {
        (ConflictPaneSide.Ours, ConflictChoice.UseOurs) => "ConflictUseOurs",
        (ConflictPaneSide.Ours, ConflictChoice.UseBothOursFirst) => "ConflictUseBothOursFirst",
        (ConflictPaneSide.Theirs, ConflictChoice.UseTheirs) => "ConflictUseTheirs",
        (ConflictPaneSide.Theirs, ConflictChoice.UseBothTheirsFirst) => "ConflictUseBothTheirsFirst",
        _ => null,
    };

    /// <summary>The panes' pieces for a parsed conflicted file (11.3): its common segments and blocks, each line without its line end.</summary>
    public static IReadOnlyList<ConflictPanesPart> PartsOf(ConflictText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = new List<ConflictPanesPart>(text.Segments.Count);
        foreach (var segment in text.Segments)
        {
            parts.Add(segment switch
            {
                ConflictBlock block => ConflictPanesPart.Block([.. block.Ours.Select(line => line.Text)], [.. block.Theirs.Select(line => line.Text)]),
                CommonSegment common => ConflictPanesPart.Common([.. common.Lines.Select(line => line.Text)]),
                _ => throw new InvalidOperationException($"Unknown segment {segment.GetType().Name}."),
            });
        }

        return parts;
    }

    /// <summary>The rows a pane shows, top to bottom; the two panes have as many.</summary>
    public IReadOnlyList<ConflictPaneRow> RowsOf(ConflictPaneSide side)
    {
        EnsureBuilt();
        var rows = new List<ConflictPaneRow>(_rows.Count);
        foreach (var row in _rows)
        {
            var cell = side == ConflictPaneSide.Ours ? row.Left : row.Right;
            var isBar = row.Kind == DiffRowKind.HunkHeader;
            var state = isBar ? StateOf(row.Hunk, side) : (ConflictSideState?)null;
            rows.Add(new ConflictPaneRow(cell?.Text ?? string.Empty, TintOf(row, cell, side), isBar, cell?.OldNumber ?? cell?.NewNumber)
            {
                Block = row.Hunk >= 0 ? row.Hunk : null,
                BarState = state,
                BarText = state is { } shown ? BarTextOf(shown) : null,
            });
        }

        return rows;
    }

    /// <summary>What block <paramref name="block"/>'s bar shows in <paramref name="side"/>'s pane instead of its buttons, or null while it shows them (or there is no such block).</summary>
    public string? BarText(int block, ConflictPaneSide side)
    {
        EnsureBuilt();
        return block >= 0 && block < _barRows.Length ? BarTextOf(StateOf(block, side)) : null;
    }

    /// <summary>
    /// Scrolls both panes so that block <paramref name="block"/>'s bar is the third row in view,
    /// with fewer rows above it near the start and the panes stopping at their end (11.5); done
    /// once the panes are laid out with their bars' heights known.
    /// </summary>
    public void RevealBlock(int block)
    {
        _pendingReveal = block;
        _pendingOffset = null;
        TryScrollPending();
    }

    /// <summary>Scrolls both panes to <paramref name="offset"/> once they are laid out (a resolver shown again keeps its place).</summary>
    public void ScrollToOffset(Vector offset)
    {
        _pendingOffset = offset;
        _pendingReveal = null;
        TryScrollPending();
    }

    /// <summary>The pane <paramref name="source"/> (a control, or something inside it) is in, or null outside both panes.</summary>
    public ConflictPaneSide? SideOf(object? source)
    {
        for (var visual = source as Visual; visual is not null && visual != this; visual = visual.GetVisualParent())
        {
            if (visual == _ours.Editor)
            {
                return ConflictPaneSide.Ours;
            }

            if (visual == _theirs.Editor)
            {
                return ConflictPaneSide.Theirs;
            }
        }

        return null;
    }

    /// <summary>Whether text is selected in <paramref name="side"/>'s pane.</summary>
    public bool HasSelection(ConflictPaneSide side) => !PaneOf(side).Editor.TextArea.Selection.IsEmpty;

    /// <summary>Copies the text selected in <paramref name="side"/>'s pane to the clipboard, as the editor's own Copy does.</summary>
    public void Copy(ConflictPaneSide side) => PaneOf(side).Editor.Copy();

    /// <summary>The editor of <paramref name="side"/>'s pane: its text area takes a click and a selection.</summary>
    public Control PaneEditor(ConflictPaneSide side) => PaneOf(side).Editor;

    /// <summary>The button of block <paramref name="block"/>'s bar in <paramref name="side"/>'s pane that asks for <paramref name="choice"/>; null when that bar is not in view or the pane has no such button.</summary>
    public Button? BarButton(int block, ConflictPaneSide side, ConflictChoice choice)
    {
        EnsureBuilt();
        if (block < 0 || block >= _barRows.Length || AutomationIdOf(side, choice) is not { } id)
        {
            return null;
        }

        var pane = PaneOf(side);
        return pane.Headers.BarAt(_barRows[block])?.Buttons.FirstOrDefault(button => AutomationProperties.GetAutomationId(button) == id);
    }

    /// <summary>Where row <paramref name="row"/> of a pane is, across that pane's width, in this control's coordinates; null when it is not laid out in view.</summary>
    public Rect? RowBounds(ConflictPaneSide side, int row)
    {
        EnsureBuilt();
        var pane = PaneOf(side);
        if (pane.VisualLineOf(row) is not { } line || pane.Editor.TranslatePoint(default, this) is not { } paneOrigin
            || pane.TextView.TranslatePoint(new Point(0, pane.TopOf(line)), this) is not { } top)
        {
            return null;
        }

        return new Rect(paneOrigin.X, top.Y, pane.Editor.Bounds.Width, pane.BottomOf(line) - pane.TopOf(line));
    }

    /// <summary>Where a pane's text column starts, right of its gutter, in this control's coordinates; null before it is laid out.</summary>
    public double? TextLeft(ConflictPaneSide side)
    {
        EnsureBuilt();
        return PaneOf(side).TextView.TranslatePoint(default, this)?.X;
    }

    /// <summary>The row of block <paramref name="block"/>'s bar, or -1.</summary>
    public int BarRowOf(int block)
    {
        EnsureBuilt();
        return block >= 0 && block < _barRows.Length ? _barRows[block] : -1;
    }

    /// <summary>
    /// A block bar's background is a hunk header's; an unresolved block's sides are tinted ours
    /// and theirs, a side used in the result the used tint and a side not used none; a filler is
    /// the diff's filler; common lines have none.
    /// </summary>
    IImmutableBrush IDiffPaneHost.RowBackground(DiffPane pane, DiffDisplayRow row, DiffCell? cell) => TintOf(row, cell, pane == _ours ? ConflictPaneSide.Ours : ConflictPaneSide.Theirs) switch
    {
        ConflictTint.Bar => Palette.Hunk,
        ConflictTint.Filler => Palette.Filler,
        ConflictTint.Ours => Palette.ConflictOurs,
        ConflictTint.Theirs => Palette.ConflictTheirs,
        ConflictTint.Used => Palette.ConflictUsed,
        _ => Palette.Background,
    };

    /// <summary>
    /// A block's bar: while the block is unresolved, "Use ours" and "Use both" in the ours pane,
    /// "Use theirs" and "Use both" in the theirs pane, at the bar's start; once it is resolved,
    /// <see cref="UsedText"/> or <see cref="NotUsedText"/> in the secondary colour (11.5).
    /// </summary>
    HeaderBarContent IDiffPaneHost.HeaderOf(DiffPane pane, DiffDisplayRow header)
    {
        var block = header.Hunk;
        var side = pane == _ours ? ConflictPaneSide.Ours : ConflictPaneSide.Theirs;
        if (BarTextOf(StateOf(block, side)) is { } text)
        {
            return new HeaderBarContent(text, null, ButtonsFirst: true, TextInCodeFont: false, TextAutomationId: BarStateAutomationId);
        }

        IReadOnlyList<HeaderBarButton> buttons = pane == _ours
            ?
            [
                Button("Use ours", ConflictPaneSide.Ours, ConflictChoice.UseOurs),
                Button("Use both", ConflictPaneSide.Ours, ConflictChoice.UseBothOursFirst),
            ]
            :
            [
                Button("Use theirs", ConflictPaneSide.Theirs, ConflictChoice.UseTheirs),
                Button("Use both", ConflictPaneSide.Theirs, ConflictChoice.UseBothTheirsFirst),
            ];

        return new HeaderBarContent(null, buttons, ButtonsFirst: true);

        HeaderBarButton Button(string label, ConflictPaneSide side, ConflictChoice choice) =>
            new(label, AutomationIdOf(side, choice)!, () => ChoiceRequested?.Invoke(this, new ConflictChoiceEventArgs(block, choice)));
    }

    /// <summary>A pane scrolled: the other follows, both ways (a pane whose lines are shorter stops sideways at its end).</summary>
    void IDiffPaneHost.OnPaneScrolled(DiffPane pane)
    {
        if (_syncingScroll)
        {
            return;
        }

        var other = pane == _ours ? _theirs : _ours;
        var offset = pane.Editor.ScrollOffset;
        var y = Math.Min(offset.Y, MaxVerticalOffset());
        if (y == offset.Y && other.Editor.ScrollOffset == offset)
        {
            return;
        }

        _syncingScroll = true;
        try
        {
            if (y < offset.Y)
            {
                pane.Editor.ScrollTo(null, y);
            }

            other.Editor.ScrollTo(offset.X, y);
        }
        finally
        {
            _syncingScroll = false;
        }
    }

    /// <summary>
    /// How far down both panes can scroll: each text view stops where its last row meets its
    /// bottom, and the two can stop a fraction apart (the ours pane hides its scroll bar, and its
    /// text view can be half a pixel taller than its scroll viewer's viewport). Both stop at the
    /// nearer end, so that the rows stay lined up (11.3).
    /// </summary>
    private double MaxVerticalOffset()
    {
        static double Max(DiffPane pane) =>
            Math.Max(0, pane.Editor.ExtentHeight - Math.Max(pane.Editor.ViewportHeight, pane.TextView.Bounds.Height));

        return Math.Min(Max(_ours), Max(_theirs));
    }

    void IDiffPaneHost.OnPaneSelectionChanged(DiffPane pane)
    {
        // Rows are not selected here.
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ConflictPanesAutomationPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PartsProperty || change.Property == FilePathProperty)
        {
            _dirty = true;
            InvalidateMeasure();
        }
        else if (change.Property == BarsProperty)
        {
            // The rows stay where they are: only their backgrounds and the bars change.
            foreach (var pane in new[] { _ours, _theirs })
            {
                pane.RedrawBackgrounds();
                pane.Headers.Rebuild();
            }
        }
        else if (change.Property == OursAutomationIdProperty || change.Property == TheirsAutomationIdProperty)
        {
            ApplyAutomationIds();
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

    /// <summary>What a bar shows in place of its buttons, or null while it shows them.</summary>
    private static string? BarTextOf(ConflictSideState state) => state switch
    {
        ConflictSideState.Used => UsedText,
        ConflictSideState.NotUsed => NotUsedText,
        _ => null,
    };

    /// <summary>The state of block <paramref name="block"/>'s <paramref name="side"/>: <see cref="ConflictSideState.Choose"/> unless <see cref="Bars"/> says otherwise.</summary>
    private ConflictSideState StateOf(int block, ConflictPaneSide side)
    {
        var bars = Bars is { } list && block >= 0 && block < list.Count ? list[block] : ConflictBlockBars.Open;
        return side == ConflictPaneSide.Ours ? bars.Ours : bars.Theirs;
    }

    /// <summary>The background of a row on a pane's side.</summary>
    private ConflictTint TintOf(DiffDisplayRow row, DiffCell? cell, ConflictPaneSide side)
    {
        if (row.Kind == DiffRowKind.HunkHeader)
        {
            return ConflictTint.Bar;
        }

        var sided = cell?.Kind switch
        {
            null => ConflictTint.Filler,
            DiffLineKind.Removed => ConflictTint.Ours,
            DiffLineKind.Added => ConflictTint.Theirs,
            _ => ConflictTint.None,
        };

        if (sided is not (ConflictTint.Ours or ConflictTint.Theirs))
        {
            return sided;
        }

        return StateOf(row.Hunk, side) switch
        {
            ConflictSideState.Used => ConflictTint.Used,
            ConflictSideState.NotUsed => ConflictTint.None,
            _ => sided,
        };
    }

    /// <summary>The synthetic diff of the parts (see the remarks) and the panes' rows.</summary>
    private static (FileDiff Diff, List<DiffDisplayRow> Rows, int[] BarRows) Build(IReadOnlyList<ConflictPanesPart> parts)
    {
        var lines = new List<DiffLine>();
        var rows = new List<DiffDisplayRow>();
        var barRows = new List<int>();
        var oursNumber = 0;
        var theirsNumber = 0;
        foreach (var part in parts)
        {
            if (!part.IsBlock)
            {
                foreach (var text in part.Ours)
                {
                    var at = new DiffLineRef(0, lines.Count);
                    oursNumber++;
                    theirsNumber++;
                    lines.Add(new DiffLine(DiffLineKind.Context, text, text, oursNumber, theirsNumber));
                    rows.Add(new DiffDisplayRow(
                        DiffRowKind.Line,
                        -1,
                        new DiffCell(at, DiffLineKind.Context, oursNumber, null, text),
                        new DiffCell(at, DiffLineKind.Context, null, theirsNumber, text),
                        null));
                }

                continue;
            }

            var block = barRows.Count;
            barRows.Add(rows.Count);
            rows.Add(new DiffDisplayRow(DiffRowKind.HunkHeader, block, null, null, null));
            var ours = new List<DiffCell>();
            foreach (var text in part.Ours)
            {
                oursNumber++;
                ours.Add(new DiffCell(new DiffLineRef(0, lines.Count), DiffLineKind.Removed, oursNumber, null, text));
                lines.Add(new DiffLine(DiffLineKind.Removed, text, text, oursNumber, null));
            }

            var theirs = new List<DiffCell>();
            foreach (var text in part.Theirs)
            {
                theirsNumber++;
                theirs.Add(new DiffCell(new DiffLineRef(0, lines.Count), DiffLineKind.Added, null, theirsNumber, text));
                lines.Add(new DiffLine(DiffLineKind.Added, text, text, null, theirsNumber));
            }

            for (var i = 0; i < Math.Max(ours.Count, theirs.Count); i++)
            {
                rows.Add(new DiffDisplayRow(DiffRowKind.Line, block, i < ours.Count ? ours[i] : null, i < theirs.Count ? theirs[i] : null, null));
            }
        }

        var hunk = new DiffHunk(string.Empty, string.Empty, 1, oursNumber, 1, theirsNumber, lines);
        return (new FileDiff([], [hunk], false, false, false, null, null, 0), rows, [.. barRows]);
    }

    /// <summary>
    /// The tokens of both files, or null. Highlighting is an extra: when TextMate fails, the panes
    /// show without colours. Files of up to <see cref="DiffTextView.SynchronousHighlightLimit"/>
    /// lines are tokenized before they are drawn; larger ones show without colours (a background
    /// tokenization, as the diff's, is the resolver's to add).
    /// </summary>
    private static DiffSyntax? TokenizeOrNothing(FileDiff diff, string? path)
    {
        if (diff.LineCount > DiffTextView.SynchronousHighlightLimit)
        {
            return null;
        }

        try
        {
            return DiffHighlighter.Tokenize(diff, path);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
    }

    private DiffPane PaneOf(ConflictPaneSide side) => side == ConflictPaneSide.Ours ? _ours : _theirs;

    /// <summary>Builds the rows and the tokens again after the parts or the path changed, and starts at the top.</summary>
    private void EnsureBuilt()
    {
        if (!_dirty)
        {
            return;
        }

        _dirty = false;
        var parts = Parts ?? [];
        var (diff, rows, barRows) = Build(parts);
        _rows = rows;
        _barRows = barRows;
        Syntax = TokenizeOrNothing(diff, FilePath);
        _ours.Show(PaneSide.Left, _rows);
        _theirs.Show(PaneSide.Right, _rows);
        _pendingTop = true;
    }

    private void ApplyAutomationIds()
    {
        AutomationProperties.SetAutomationId(_ours.Editor, OursAutomationId);
        AutomationProperties.SetAutomationId(_theirs.Editor, TheirsAutomationId);
    }

    private void OnThemeChanged()
    {
        _palette = null;
        _divider.Background = Palette.Border;
        _ours.Redraw();
        _theirs.Redraw();
    }

    /// <summary>
    /// After a layout pass: panes with new rows learn their bars' height (which cannot be done
    /// inside their own layout), then new rows start at the top once the scroll extent counts it.
    /// </summary>
    private void OnLayoutUpdated()
    {
        var builtHeaders = false;
        foreach (var pane in new[] { _ours, _theirs })
        {
            if (pane.HeadersPending && pane.TextView.Bounds.Height > 0 && pane.TextView.VisualLinesValid)
            {
                pane.BuildHeaderLines();
                pane.TextView.InvalidateMeasure();
                builtHeaders = true;
            }
        }

        if (!builtHeaders)
        {
            TryScrollPending();
        }
    }

    /// <summary>
    /// Scrolls as asked once the panes are laid out and their bars' heights are known: to a block
    /// (<see cref="RevealBlock"/>), to an offset (<see cref="ScrollToOffset"/>), or, after new
    /// rows, to the top.
    /// </summary>
    private void TryScrollPending()
    {
        if (_dirty || _ours.HeadersPending || _theirs.HeadersPending
            || !(_ours.TextView.Bounds.Height > 0) || !_ours.TextView.VisualLinesValid
            || !(_theirs.TextView.Bounds.Height > 0) || !_theirs.TextView.VisualLinesValid)
        {
            return;
        }

        Vector? target = null;
        if (_pendingReveal is { } block)
        {
            var row = BarRowOf(block);
            var top = row < 0 ? 0 : _ours.TextView.GetVisualTopByDocumentLine(Math.Max(0, row - 2) + 1);
            target = new Vector(0, top);
        }
        else if (_pendingOffset is { } offset)
        {
            target = offset;
        }
        else if (_pendingTop)
        {
            target = default(Vector);
        }

        _pendingTop = false;
        _pendingReveal = null;
        _pendingOffset = null;
        if (target is { } to)
        {
            var y = Math.Min(to.Y, MaxVerticalOffset());
            _ours.Editor.ScrollTo(to.X, y);
            _theirs.Editor.ScrollTo(to.X, y);
        }
    }

    /// <summary>Exposes the panes to UI Automation as one element that holds the two texts and the bars' buttons.</summary>
    private sealed class ConflictPanesAutomationPeer(ConflictPanes owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
