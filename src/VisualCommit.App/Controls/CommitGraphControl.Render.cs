using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.Views.Graph;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;

namespace VisualCommit.App.Controls;

/// <summary>Drawing: the theme's colours, the cache of laid-out rows, and one frame of rows.</summary>
public sealed partial class CommitGraphControl
{
    /// <summary>The size of the text in the Message, Author, Date and SHA columns: the window's font size.</summary>
    public const double CellFontSize = 12.5;

    /// <summary>The size of a ref label's text.</summary>
    public const double LabelFontSize = 11;

    public const double LabelHeight = 18;

    /// <summary>Space between a label's edge and its text or icon.</summary>
    public const double LabelPadding = 6;

    /// <summary>Space between the Branch / Tag column's left edge and the first label, and between two labels.</summary>
    public const double LabelStart = 6;

    public const double LabelSpacing = 4;

    public const double LabelIconSize = 10;

    /// <summary>Space between a label's icon and its text.</summary>
    public const double LabelIconGap = 3;

    public const double LineThickness = 2;

    public const double NodeDiameter = 10;

    public const double MergeNodeDiameter = 7;

    /// <summary>The width of a stash node's ring; its outer diameter is <see cref="NodeDiameter"/>.</summary>
    public const double RingThickness = 2;

    private const double LabelCornerRadius = 4;

    /// <summary>The thickness of an icon's stroke in a label, in pixels.</summary>
    private const double LabelIconStroke = 1.3;

    /// <summary>The grid the outlines in Theme/Icons.axaml are drawn on.</summary>
    private const double IconGridSize = 24;

    private const string Ellipsis = "…";

    /// <summary>What the working-changes row says in the Message column (D60) unless <see cref="WorkingRowText"/> says otherwise.</summary>
    public const string DefaultWorkingRowText = "Working changes";

    /// <summary>
    /// The dashes and the gaps of a dashed line, in pixels: the working-changes row's line (D60)
    /// and a stash's line down to its base (docs/phase-5-design.md section 10, D161).
    /// </summary>
    private const double DashLength = 3;

    /// <summary>A dash and a gap: where a dashed line's pattern repeats.</summary>
    private const double DashPeriod = 2 * DashLength;

    /// <summary>
    /// How many rows keep their laid-out text. A view shows at most about 40 rows; the rest of
    /// the cache makes scrolling back a little free. The bound keeps memory flat while scrolling
    /// through 100,000 rows.
    /// </summary>
    private const int MaxCachedRows = 256;

    private readonly Dictionary<int, RowVisual> _rowCache = [];
    private TextLayout? _workingRowLayout;
    private readonly Dictionary<(string Name, bool WithAvatar), TextLayout?> _authorCache = [];
    private Palette? _palette;

    public override void Render(DrawingContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var palette = GetPalette();
        var size = Bounds.Size;
        context.FillRectangle(palette.Background, new Rect(size));

        var data = Data;
        var rowOffset = RowOffset;
        if (data is null || data.Count + rowOffset == 0 || size.Height <= 0 || size.Width <= 0)
        {
            return;
        }

        // Rows are counted as drawn: the working-changes row, when shown, is row 0 and every
        // commit's row is one further down.
        var offset = DrawnOffset;
        var first = Math.Max(0, (int)Math.Floor(offset / RowHeight));
        var last = Math.Min(data.Count + rowOffset - 1, (int)Math.Ceiling((offset + size.Height) / RowHeight) - 1);
        if (last < first)
        {
            return;
        }

        var columns = Columns;
        var selection = Selection;
        var search = ActiveSearch;

        for (var display = first; display <= last; display++)
        {
            var background = RowBackground(data, display, selection, palette);
            if (!ReferenceEquals(background, palette.Background))
            {
                context.FillRectangle(background, new Rect(0, RowTop(display, offset), size.Width, RowHeight));
            }
        }

        if (search is not null)
        {
            DrawSearchMarks(context, data, search, first, last, offset, palette);
        }

        using (context.PushClip(new Rect(columns.Graph.X, 0, columns.Graph.Width, size.Height)))
        {
            // Under everything else in the column: what it passes is drawn over it.
            var dashOrigin = DashOrigin(offset);
            if (rowOffset > 0)
            {
                DrawWorkingRowLine(context, data, offset, size.Height, palette);
            }

            for (var display = first; display <= last; display++)
            {
                var top = RowTop(display, offset);
                var background = RowBackground(data, display, selection, palette);
                if (display < rowOffset)
                {
                    DrawWorkingRowNode(context, data, top, background, palette);
                }
                else
                {
                    var index = display - rowOffset;
                    DrawGraphCell(context, data, index, top, dashOrigin, background, palette, IsDimmed(data, index, search));
                }
            }
        }

        using (context.PushClip(new Rect(columns.Refs.X, 0, columns.Refs.Width, size.Height)))
        {
            for (var display = Math.Max(first, rowOffset); display <= last; display++)
            {
                var index = display - rowOffset;
                if (data.LabelsAt(index).Count > 0)
                {
                    var dimmed = _draggedLabel is { } dragged && dragged.Sha == data.CommitAt(index).Sha ? dragged.RefName : null;
                    using var opacity = IsDimmed(data, index, search) ? context.PushOpacity(SearchDimmedOpacity) : default(DrawingContext.PushedState?);
                    DrawLabels(context, GetRowVisual(data, index), data.RowAt(index).NodeColor, RowTop(display, offset), palette, dimmed);
                }
            }
        }

        for (var display = first; display <= last; display++)
        {
            if (display < rowOffset)
            {
                _workingRowLayout ??= CellLayout(WorkingRowText ?? DefaultWorkingRowText, CellTypeface, palette.TextSecondary, columns.Message);
                DrawCell(context, _workingRowLayout, columns.Message.X, RowTop(display, offset));
            }
            else
            {
                var index = display - rowOffset;
                using var opacity = IsDimmed(data, index, search) ? context.PushOpacity(SearchDimmedOpacity) : default(DrawingContext.PushedState?);
                DrawTextCells(context, GetRowVisual(data, index), RowTop(display, offset), columns);
            }
        }

        TrimRowCache(first - rowOffset, last - rowOffset);

        var elapsed = Stopwatch.GetElapsedTime(started);
        if (data.Count > 0)
        {
            RowsDrawn?.Invoke(this, EventArgs.Empty);
        }

        FrameDrawn?.Invoke(this, elapsed);
    }

    private static double RowTop(int display, double offset) => (display * RowHeight) - offset;

    /// <summary>A drawn row's background: the working-changes row is row 0 when shown; every selected row has the selection's (4h).</summary>
    private IBrush RowBackground(CommitGraphData data, int display, GraphSelection selection, Palette palette)
    {
        var isSelected = display < RowOffset ? IsWorkingRowSelected : IsRowSelected(data, display - RowOffset, selection);
        return isSelected ? palette.Selection
            : display == _hoveredRow ? palette.RowHover
            : palette.Background;
    }

    /// <summary>The lane and colour the working-changes row takes: those of HEAD's commit, or lane 0 and colour 0 (D60). Also HEAD's row, or -1.</summary>
    private static (int Lane, int Color, int HeadIndex) WorkingRowLane(CommitGraphData data)
    {
        var head = data.Refs.Head.Sha is { } sha ? data.IndexOf(sha) : -1;
        if (head < 0)
        {
            return (0, 0, -1);
        }

        var row = data.RowAt(head);
        return (row.NodeLane, Modulo(row.NodeColor), head);
    }

    /// <summary>The working-changes row's ring: 10 across and 2 wide, as a stash's, with the row's background inside.</summary>
    private void DrawWorkingRowNode(DrawingContext context, CommitGraphData data, double top, IBrush rowBackground, Palette palette)
    {
        var (lane, color, _) = WorkingRowLane(data);
        var radius = (NodeDiameter / 2) - (RingThickness / 2);
        context.DrawEllipse(rowBackground, palette.RingPens[color], new Point(LaneCenterX(lane), top + (RowHeight / 2)), radius, radius);
    }

    /// <summary>
    /// The dashed line from the working-changes row's ring down to HEAD's node, in HEAD's lane: a
    /// dash of 3 from the ring's bottom, a gap of 3, and so on. Only the part in view is drawn,
    /// with the pattern shifted so that it stays where it would be if all of it were.
    /// </summary>
    private void DrawWorkingRowLine(DrawingContext context, CommitGraphData data, double offset, double height, Palette palette)
    {
        var (lane, color, head) = WorkingRowLane(data);
        if (head < 0)
        {
            return;
        }

        var x = LaneCenterX(lane);
        var from = DashOrigin(offset);
        var headCommit = data.CommitAt(head);
        var headRadius = (headCommit.Parents.Count > 1 ? MergeNodeDiameter : NodeDiameter) / 2;
        var to = RowTop(head + 1, offset) + (RowHeight / 2) - headRadius;

        var start = Math.Max(from, -RowHeight);
        var end = Math.Min(to, height + RowHeight);
        if (end <= start)
        {
            return;
        }

        context.DrawLine(DashedPen(palette, color, start - from), new Point(x, start), new Point(x, end));
    }

    /// <summary>
    /// The lines and the node of one row: lines first, so the node lies on top of them. A stash's
    /// line down to its base is dashed from <paramref name="dashOrigin"/> (<see cref="DashOrigin"/>);
    /// a curve into the base's node takes the pattern where it starts and carries it along its
    /// length. A row the search dims (3.3) has its node at <see cref="SearchDimmedOpacity"/>; its
    /// lines stay at full strength, so the history's shape stays readable.
    /// </summary>
    private void DrawGraphCell(DrawingContext context, CommitGraphData data, int index, double top, double dashOrigin, IBrush rowBackground, Palette palette, bool dimmed)
    {
        var row = data.RowAt(index);
        var middle = top + (RowHeight / 2);
        var bottom = top + RowHeight;

        // An indexed loop: a foreach over the interface would allocate an enumerator per row and frame.
        var lines = row.Lines;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var pen = line.IsDashed
                ? DashedPen(palette, Modulo(line.Color), (line.Kind == GraphLineKind.OutOfNode ? middle : top) - dashOrigin)
                : palette.LanePens[Modulo(line.Color)];
            var fromX = LaneCenterX(line.FromLane);
            var toX = LaneCenterX(line.ToLane);
            switch (line.Kind)
            {
                case GraphLineKind.PassThrough:
                    context.DrawLine(pen, new Point(fromX, top), new Point(fromX, bottom));
                    break;
                case GraphLineKind.IntoNode:
                    DrawConnection(context, pen, new Point(fromX, top), new Point(toX, middle));
                    break;
                case GraphLineKind.OutOfNode:
                    DrawConnection(context, pen, new Point(fromX, middle), new Point(toX, bottom));
                    break;
            }
        }

        var center = new Point(LaneCenterX(row.NodeLane), middle);
        var color = Modulo(row.NodeColor);
        var commit = data.CommitAt(index);
        var isStash = commit.Kind == CommitKind.Stash;
        var outerRadius = (isStash || commit.Parents.Count < 2 ? NodeDiameter : MergeNodeDiameter) / 2;
        if (dimmed)
        {
            // A dimmed node still covers the lines that meet in it, as a node at full strength
            // does: the row's background first, then the node faded over it.
            context.DrawEllipse(rowBackground, null, center, outerRadius, outerRadius);
        }

        using var opacity = dimmed ? context.PushOpacity(SearchDimmedOpacity) : default(DrawingContext.PushedState?);
        if (isStash)
        {
            // A ring 10 across and 2 wide: the stroke is centred 1 inside the outer edge.
            var radius = outerRadius - (RingThickness / 2);
            context.DrawEllipse(rowBackground, palette.RingPens[color], center, radius, radius);
        }
        else
        {
            context.DrawEllipse(palette.Lanes[color], null, center, outerRadius, outerRadius);
        }
    }

    /// <summary>
    /// Where the pattern of every dashed line starts, in the control's coordinates: the bottom of a
    /// ring in the first drawn row, where the working-changes row's line starts (D60). A stash's
    /// line (docs/phase-5-design.md section 10, D161) takes its dashes from the same place, so its
    /// pattern depends only on the absolute y: drawn row by row and cut by the view's edges, it is
    /// one pattern, and where it shares a lane with the working-changes row's line (a stash made on
    /// HEAD's commit) the two dash as one.
    /// </summary>
    private static double DashOrigin(double offset) => RowTop(0, offset) + (RowHeight / 2) + (NodeDiameter / 2);

    /// <summary>
    /// A dashed pen in lane colour <paramref name="color"/> whose pattern is <paramref name="phase"/>
    /// pixels in where the line starts (any number: it is taken modulo the pattern). Pens for whole
    /// pixels, which every row of the graph gives, are made once per theme.
    /// </summary>
    private static IPen DashedPen(Palette palette, int color, double phase)
    {
        var into = ((phase % DashPeriod) + DashPeriod) % DashPeriod;
        var step = (int)into;
        if (step == into)
        {
            var slot = (color * (int)DashPeriod) + step;
            return palette.DashedLanePens[slot] ??= MakeDashedPen(palette.Lanes[color], into);
        }

        return MakeDashedPen(palette.Lanes[color], into);

        // The dash style counts in units of the line's thickness.
        static IPen MakeDashedPen(IImmutableBrush brush, double phase) =>
            new ImmutablePen(
                brush,
                LineThickness,
                new ImmutableDashStyle([DashLength / LineThickness, DashLength / LineThickness], phase / LineThickness),
                PenLineCap.Flat);
    }

    /// <summary>A line between two points of one row: straight in one lane, otherwise a curve that leaves and arrives vertically.</summary>
    private static void DrawConnection(DrawingContext context, IPen pen, Point from, Point to)
    {
        if (Math.Abs(from.X - to.X) < 0.5)
        {
            context.DrawLine(pen, from, to);
            return;
        }

        var middleY = (from.Y + to.Y) / 2;
        var geometry = new StreamGeometry();
        using (var sink = geometry.Open())
        {
            sink.BeginFigure(from, isFilled: false);
            sink.CubicBezierTo(new Point(from.X, middleY), new Point(to.X, middleY), to);
            sink.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    /// <summary>Draws a row's labels; the one named <paramref name="dimmedRef"/> is being dragged and is drawn at half opacity (4g).</summary>
    private static void DrawLabels(DrawingContext context, RowVisual visual, int nodeColor, double top, Palette palette, string? dimmedRef)
    {
        var color = Modulo(nodeColor);
        foreach (var label in visual.Labels)
        {
            using var opacity = dimmedRef is not null && !label.IsOverflow && label.RefName == dimmedRef
                ? context.PushOpacity(DraggedSourceOpacity)
                : default(DrawingContext.PushedState?);
            var bounds = label.Bounds.Translate(new Vector(0, top));
            var (fill, border, iconPen) = LabelColors(label, color, palette);
            if (border is null)
            {
                context.DrawRectangle(fill, null, new RoundedRect(bounds, LabelCornerRadius));
            }
            else
            {
                // A 1-pixel border inside the label's bounds: the stroke is centred half a pixel in.
                context.DrawRectangle(fill, border, new RoundedRect(bounds.Deflate(0.5), LabelCornerRadius - 0.5));
            }

            var x = bounds.X + LabelPadding;
            var icon = LabelIcon(label, palette);
            if (icon is not null)
            {
                DrawIcon(context, icon, iconPen, new Point(x, bounds.Y + ((LabelHeight - LabelIconSize) / 2)));
                x += LabelIconSize + LabelIconGap;
            }

            label.Layout.Draw(context, new Point(x, bounds.Y + ((LabelHeight - label.Layout.Height) / 2)));
            DrawBadge(context, label, bounds, x + label.Layout.WidthIncludingTrailingWhitespace, iconPen, palette);
        }
    }

    /// <summary>A label's fill, border and the pen of its icon, which has the colour of its text.</summary>
    private static (IBrush? Fill, IPen? Border, IPen IconPen) LabelColors(LabelVisual label, int color, Palette palette)
    {
        if (label.IsOverflow)
        {
            return (palette.ControlBackground, palette.BorderPen, palette.IconPenOnControl);
        }

        return label.Kind switch
        {
            RefLabelKind.DetachedHead or RefLabelKind.CurrentBranch or RefLabelKind.LocalBranch => (palette.Lanes[color], null, palette.IconPenOnFill),
            RefLabelKind.RemoteBranch => (null, palette.LabelBorderPens[color], palette.IconPenOnControl),
            _ => (palette.ControlBackground, palette.BorderPen, palette.IconPenOnControl),
        };
    }

    private static Geometry? LabelIcon(LabelVisual label, Palette palette) =>
        label.IsOverflow ? null
        : label.Kind == RefLabelKind.CurrentBranch ? palette.CheckIcon
        : label.Kind == RefLabelKind.Tag ? palette.TagIcon
        : null;

    /// <summary>Draws an outline from Theme/Icons.axaml, made for a 24-pixel grid, <see cref="LabelIconSize"/> pixels square.</summary>
    private static void DrawIcon(DrawingContext context, Geometry icon, IPen pen, Point topLeft)
    {
        var scale = LabelIconSize / IconGridSize;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(topLeft.X, topLeft.Y)))
        {
            context.DrawGeometry(null, pen, icon);
        }
    }

    private static void DrawTextCells(DrawingContext context, RowVisual visual, double top, GraphColumns columns)
    {
        DrawCell(context, visual.Message, columns.Message.X, top);
        if (visual.Avatar is { } avatar)
        {
            // The image at the text inset, the name 16 + 6 further (11.3).
            DrawAvatar(context, avatar, columns.Author.X, top);
            DrawCell(context, visual.Author, columns.Author.X + AvatarSize + AvatarGap, top);
        }
        else
        {
            DrawCell(context, visual.Author, columns.Author.X, top);
        }

        DrawCell(context, visual.Date, columns.Date.X, top);
        DrawCell(context, visual.Sha, columns.Sha.X, top);
    }

    private static void DrawCell(DrawingContext context, TextLayout? layout, double columnX, double top)
    {
        if (layout is null)
        {
            return;
        }

        layout.Draw(context, new Point(columnX + GraphColumns.TextInset, top + ((RowHeight - layout.Height) / 2)));
    }

    private static int Modulo(int color) => ((color % Palette.LaneCount) + Palette.LaneCount) % Palette.LaneCount;

    private Palette GetPalette() => _palette ??= Palette.Resolve(this);

    private Typeface CellTypeface => new(FontFamily, FontStyle.Normal, FontWeight.Normal);

    private Typeface LabelTypeface => new(FontFamily, FontStyle.Normal, FontWeight.SemiBold);

    /// <summary>The laid-out text and labels of a row, from the cache or made now.</summary>
    private RowVisual GetRowVisual(CommitGraphData data, int index)
    {
        if (_rowCache.TryGetValue(index, out var cached))
        {
            return cached;
        }

        var visual = CreateRowVisual(data, index);
        _rowCache[index] = visual;
        return visual;
    }

    private RowVisual CreateRowVisual(CommitGraphData data, int index)
    {
        var palette = GetPalette();
        var columns = Columns;
        var commit = data.CommitAt(index);
        var dates = Dates ?? FallbackDates.Value;
        var typeface = CellTypeface;

        var messageBrush = commit.Kind == CommitKind.Stash ? palette.TextSecondary : palette.TextPrimary;
        var messageTypeface = SearchStateOf(data, index, ActiveSearch) == GraphSearchRowState.Marked ? MatchTypeface : typeface;
        var message = CellLayout(commit.Subject, messageTypeface, messageBrush, columns.Message);
        var avatar = AvatarOf(commit.AuthorEmail, columns.Author);
        var author = AuthorLayout(commit.AuthorName, avatar is not null, typeface, palette, columns.Author);
        var date = CellLayout(dates.Format(commit.AuthorDate), typeface, palette.TextSecondary, columns.Date);
        var sha = CellLayout(commit.ShortSha, typeface, palette.TextSecondary, columns.Sha);
        var labels = PlaceLabels(data.LabelsAt(index), columns.Refs, palette);
        return new RowVisual(message, author, date, sha, labels, avatar);
    }

    /// <summary>
    /// The Author cell. A history has few authors, so their laid-out names are shared between
    /// rows: a frame that brings many new rows into view then lays out fewer texts. Beside an avatar
    /// the name is laid out 22 narrower (11.3), so it is cached apart.
    /// </summary>
    private TextLayout? AuthorLayout(string author, bool withAvatar, Typeface typeface, Palette palette, GraphColumn column)
    {
        if (!column.IsVisible)
        {
            return null;
        }

        if (_authorCache.TryGetValue((author, withAvatar), out var cached))
        {
            return cached;
        }

        if (_authorCache.Count >= MaxCachedRows)
        {
            _authorCache.Clear();
        }

        var layout = CellLayout(author, typeface, palette.TextSecondary, column, withAvatar ? AvatarSize + AvatarGap : 0);
        _authorCache[(author, withAvatar)] = layout;
        return layout;
    }

    /// <summary>
    /// One cell's text, cut with "…" to fit between the text inset (and <paramref name="before"/>, an
    /// avatar's room) and the column's right edge; null for a hidden column.
    /// </summary>
    private static TextLayout? CellLayout(string text, Typeface typeface, IBrush brush, GraphColumn column, double before = 0)
    {
        if (!column.IsVisible)
        {
            return null;
        }

        var maxWidth = Math.Max(0, column.Width - GraphColumns.TextInset - before);
        return new TextLayout(
            text,
            typeface,
            CellFontSize,
            brush,
            textWrapping: TextWrapping.NoWrap,
            textTrimming: TextTrimming.CharacterEllipsis,
            maxWidth: maxWidth,
            maxLines: 1);
    }

    /// <summary>
    /// Places a row's labels from 6 pixels into the Branch / Tag column, 4 apart. When the next
    /// label does not fit, it and every one after it become a single "+N" label. The first label
    /// is always shown; when it does not fit, by itself or with the "+N" label after it, its name
    /// is cut short with "…". Positions are relative to the row's top.
    /// </summary>
    private LabelVisual[] PlaceLabels(IReadOnlyList<RefLabel> labels, GraphColumn column, Palette palette)
    {
        if (labels.Count == 0)
        {
            return [];
        }

        var typeface = LabelTypeface;
        var left = column.X + LabelStart;
        var right = column.Right;
        var labelTop = (RowHeight - LabelHeight) / 2;

        // A label with a pull request is its badge wider (11.2), so it folds the next ones into "+N" sooner.
        var badges = BadgesOf(labels);
        var widths = new double[labels.Count];
        for (var i = 0; i < labels.Count; i++)
        {
            widths[i] = LabelWidth(MeasureLabelText(labels[i].Text, typeface), HasIcon(labels[i].Kind)) + BadgeWidth(badges[i], typeface);
        }

        // How many labels are shown by name: as many as fit one after another...
        var shown = 1;
        var x = left + widths[0];
        while (shown < labels.Count && x + LabelSpacing + widths[shown] <= right)
        {
            x += LabelSpacing + widths[shown];
            shown++;
        }

        // ...less those that leave no room for the "+N" label that stands for the rest.
        double OverflowWidth(int hidden) => LabelWidth(MeasureLabelText(OverflowText(hidden), typeface), hasIcon: false);
        double EndOf(int count)
        {
            var end = left;
            for (var i = 0; i < count; i++)
            {
                end += (i > 0 ? LabelSpacing : 0) + widths[i];
            }

            return end;
        }

        while (shown > 1 && shown < labels.Count && EndOf(shown) + LabelSpacing + OverflowWidth(labels.Count - shown) > right)
        {
            shown--;
        }

        var hiddenCount = labels.Count - shown;
        var placed = new List<LabelVisual>(shown + 1);
        x = left;
        for (var i = 0; i < shown; i++)
        {
            var label = labels[i];
            var width = widths[i];
            var text = label.Text;
            if (i == 0)
            {
                // The first label always shows; cut it so that it, and the "+N" label if any, fit.
                var room = right - left - (hiddenCount > 0 ? LabelSpacing + OverflowWidth(hiddenCount) : 0);
                if (width > room)
                {
                    // The name is cut, never the badge (D309).
                    var textRoom = room - LabelWidth(0, HasIcon(label.Kind)) - BadgeWidth(badges[0], typeface);
                    text = CutToFit(label.Text, typeface, textRoom);
                    width = LabelWidth(MeasureLabelText(text, typeface), HasIcon(label.Kind)) + BadgeWidth(badges[0], typeface);
                }
            }

            var layout = LabelLayout(text, typeface, label.Kind, palette);
            var badge = badges[i];
            var badgeLayout = badge is null ? null : LabelLayout(BadgeText(badge.Number), typeface, label.Kind, palette);
            placed.Add(new LabelVisual(text, label.Text, label.Kind, new Rect(x, labelTop, width, LabelHeight), IsOverflow: false, layout, label.RefName, badge, badgeLayout));
            x += width + LabelSpacing;
        }

        if (hiddenCount > 0)
        {
            var text = OverflowText(hiddenCount);
            var hidden = labels.Skip(shown).ToList();
            var fullText = string.Join("\n", hidden.Select(label => label.Text));
            var layout = new TextLayout(text, typeface, LabelFontSize, palette.TextPrimary, textWrapping: TextWrapping.NoWrap, maxLines: 1);
            placed.Add(new LabelVisual(text, fullText, hidden[0].Kind, new Rect(x, labelTop, OverflowWidth(hiddenCount), LabelHeight), IsOverflow: true, layout));
        }

        return [.. placed];
    }

    private static string OverflowText(int hidden) => "+" + hidden.ToString(CultureInfo.InvariantCulture);

    private static bool HasIcon(RefLabelKind kind) => kind is RefLabelKind.CurrentBranch or RefLabelKind.Tag;

    private static double LabelWidth(double textWidth, bool hasIcon) =>
        LabelPadding + (hasIcon ? LabelIconSize + LabelIconGap : 0) + textWidth + LabelPadding;

    private static double MeasureLabelText(string text, Typeface typeface)
    {
        var layout = new TextLayout(text, typeface, LabelFontSize, Brushes.Black, textWrapping: TextWrapping.NoWrap, maxLines: 1);
        return layout.WidthIncludingTrailingWhitespace;
    }

    /// <summary>The longest start of <paramref name="text"/> that, followed by "…", is at most <paramref name="maxWidth"/> wide.</summary>
    private static string CutToFit(string text, Typeface typeface, double maxWidth)
    {
        var low = 0;
        var high = text.Length - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (MeasureLabelText(Prefix(text, middle) + Ellipsis, typeface) <= maxWidth)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return Prefix(text, low) + Ellipsis;
    }

    /// <summary>The first <paramref name="length"/> characters, without splitting a surrogate pair.</summary>
    private static string Prefix(string text, int length)
    {
        if (length > 0 && length < text.Length && char.IsHighSurrogate(text[length - 1]))
        {
            length--;
        }

        return text[..length];
    }

    private static TextLayout LabelLayout(string text, Typeface typeface, RefLabelKind kind, Palette palette)
    {
        var brush = kind is RefLabelKind.DetachedHead or RefLabelKind.CurrentBranch or RefLabelKind.LocalBranch
            ? palette.LabelText
            : palette.TextPrimary;
        return new TextLayout(text, typeface, LabelFontSize, brush, textWrapping: TextWrapping.NoWrap, maxLines: 1);
    }

    /// <summary>Forgets every laid-out text: the theme's colours, the font, the column widths or the rows changed.</summary>
    private void ClearRowCache()
    {
        _workingRowLayout = null;
        _rowCache.Clear();
        _authorCache.Clear();
    }

    /// <summary>Keeps the cache bounded: once it is full, forgets every row that is not in view.</summary>
    private void TrimRowCache(int first, int last)
    {
        if (_rowCache.Count <= MaxCachedRows)
        {
            return;
        }

        var stale = new List<int>(_rowCache.Count);
        foreach (var index in _rowCache.Keys)
        {
            if (index < first || index > last)
            {
                stale.Add(index);
            }
        }

        foreach (var index in stale)
        {
            _rowCache.Remove(index);
        }
    }

    /// <summary>A row's laid-out text, made once and drawn in every frame that shows the row, and its author's avatar when one is drawn (11.3).</summary>
    private sealed record RowVisual(TextLayout? Message, TextLayout? Author, TextLayout? Date, TextLayout? Sha, LabelVisual[] Labels, Bitmap? Avatar = null);

    /// <summary>
    /// A placed label with its laid-out text and, with an open pull request, its badge and the
    /// badge's laid-out <c>#n</c> (11.2); <see cref="Bounds"/> is relative to the row's top and includes the badge.
    /// </summary>
    private sealed record LabelVisual(string Text, string FullText, RefLabelKind Kind, Rect Bounds, bool IsOverflow, TextLayout Layout, string? RefName = null, PullRequestBadge? Badge = null, TextLayout? BadgeLayout = null);

    /// <summary>
    /// The theme's colours as immutable brushes and pens, looked up once per theme from the
    /// resources in <c>Theme/Tokens.axaml</c> for the control's theme variant, so a frame does
    /// not search the resource tree.
    /// </summary>
    private sealed class Palette
    {
        public const int LaneCount = 8;

        public required IBrush Background { get; init; }

        public required IBrush Selection { get; init; }

        public required IBrush RowHover { get; init; }

        public required IBrush TextPrimary { get; init; }

        public required IBrush TextSecondary { get; init; }

        public required IBrush LabelText { get; init; }

        public required IBrush ControlBackground { get; init; }

        public required IPen BorderPen { get; init; }

        /// <summary>The mark at the left edge of a row a search matches (docs/phase-5-design.md 3.3).</summary>
        public required IBrush SearchMatch { get; init; }

        public required IImmutableBrush[] Lanes { get; init; }

        /// <summary>Lines in each lane colour, 2 wide.</summary>
        public required IPen[] LanePens { get; init; }

        /// <summary>
        /// Dashed lines in each lane colour, made as they are first drawn
        /// (<see cref="DashedPen"/>): for colour c, the pattern p whole pixels in at the line's
        /// start is at c × 6 + p.
        /// </summary>
        public IPen?[] DashedLanePens { get; } = new IPen?[LaneCount * (int)DashPeriod];

        /// <summary>Stash rings in each lane colour.</summary>
        public required IPen[] RingPens { get; init; }

        /// <summary>A remote branch label's 1-pixel border in each lane colour.</summary>
        public required IPen[] LabelBorderPens { get; init; }

        /// <summary>An icon on a filled label, in the label text colour; its thickness is in the icon grid's units.</summary>
        public required IPen IconPenOnFill { get; init; }

        /// <summary>An icon on a tag's control background, in the main text colour.</summary>
        public required IPen IconPenOnControl { get; init; }

        public Geometry? CheckIcon { get; init; }

        public Geometry? TagIcon { get; init; }

        /// <summary>The pull-request badge's icon (Lucide "git-pull-request", 11.2); null without the resource.</summary>
        public Geometry? PullRequestIcon { get; init; }

        public static Palette Resolve(CommitGraphControl control)
        {
            var theme = control.ActualThemeVariant;

            IImmutableBrush Brush(string key) =>
                control.TryFindResource(key, theme, out var value) && value is IBrush brush
                    ? brush.ToImmutable()
                    : Brushes.Transparent.ToImmutable();

            Geometry? Icon(string key) =>
                control.TryFindResource(key, theme, out var value) ? value as Geometry : null;

            var lanes = new IImmutableBrush[LaneCount];
            var linePens = new IPen[LaneCount];
            var ringPens = new IPen[LaneCount];
            var borderPens = new IPen[LaneCount];
            for (var i = 0; i < LaneCount; i++)
            {
                lanes[i] = Brush($"VcLane{i}Brush");
                linePens[i] = new ImmutablePen(lanes[i], LineThickness, lineCap: PenLineCap.Flat);
                ringPens[i] = new ImmutablePen(lanes[i], RingThickness);
                borderPens[i] = new ImmutablePen(lanes[i], 1);
            }

            // The icon is drawn scaled from its 24-pixel grid, which scales the stroke with it.
            var iconStroke = LabelIconStroke * IconGridSize / LabelIconSize;
            var labelText = Brush("VcLabelTextBrush");
            var textPrimary = Brush("VcTextPrimaryBrush");

            return new Palette
            {
                Background = Brush("VcWindowBackgroundBrush"),
                Selection = Brush("VcSelectionBrush"),
                RowHover = Brush("VcRowHoverBrush"),
                TextPrimary = textPrimary,
                TextSecondary = Brush("VcTextSecondaryBrush"),
                LabelText = labelText,
                ControlBackground = Brush("VcControlBackgroundBrush"),
                BorderPen = new ImmutablePen(Brush("VcBorderBrush"), 1),
                SearchMatch = Brush("VcSearchMatchBrush"),
                IconPenOnFill = new ImmutablePen(labelText, iconStroke, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round),
                IconPenOnControl = new ImmutablePen(textPrimary, iconStroke, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round),
                Lanes = lanes,
                LanePens = linePens,
                RingPens = ringPens,
                LabelBorderPens = borderPens,
                CheckIcon = Icon("IconCheck"),
                TagIcon = Icon("IconTag"),
                PullRequestIcon = Icon("IconPullRequest"),
            };
        }
    }
}
