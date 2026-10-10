namespace VisualCommit.App.Views.Graph;

/// <summary>One column of the commit graph: where it starts and how wide it is, in the graph control's coordinates.</summary>
/// <param name="X">The column's left edge.</param>
/// <param name="Width">Its width; 0 for a hidden column.</param>
/// <param name="IsVisible">False when the graph area is too narrow for it (D49).</param>
public readonly record struct GraphColumn(double X, double Width, bool IsVisible)
{
    /// <summary>The column's right edge.</summary>
    public double Right => X + Width;
}

/// <summary>
/// The column layout of the commit graph, shared by the column headers and the custom-drawn rows
/// so that the two always line up (D49). From the left: Branch / Tag, Graph, Message, Author,
/// Date, SHA. The sizes are the ones the phase 1 test report gives, and the report's checks hold
/// the app to them, so they are constants here rather than styles.
/// </summary>
/// <remarks>
/// It is a pure function of the graph's width and lane count, so it is tested without a UI and
/// two layouts compare equal when nothing changed, which lets the control keep its text cache.
/// </remarks>
public sealed record GraphColumns
{
    /// <summary>The Branch / Tag column.</summary>
    public const double RefsWidth = 130;

    /// <summary>The distance between two lanes, and from the Graph column's left edge to the first lane's centre.</summary>
    public const double LaneSpacing = 16;

    public const double MinGraphWidth = 48;

    public const double MaxGraphWidth = 240;

    public const double AuthorWidth = 140;

    public const double DateWidth = 120;

    public const double ShaWidth = 72;

    /// <summary>The message keeps at least this much; the SHA, Author and Date columns give way first, in that order.</summary>
    public const double MinMessageWidth = 120;

    /// <summary>How far into its column a header's or a cell's text starts.</summary>
    public const double TextInset = 8;

    private GraphColumns(double width, int laneCount, GraphColumn refs, GraphColumn graph, GraphColumn message, GraphColumn author, GraphColumn date, GraphColumn sha)
    {
        Width = width;
        LaneCount = laneCount;
        Refs = refs;
        Graph = graph;
        Message = message;
        Author = author;
        Date = date;
        Sha = sha;
    }

    /// <summary>The layout of a graph with no width and no lanes, before the control has been arranged.</summary>
    public static GraphColumns Empty { get; } = Compute(0, 0);

    /// <summary>The width the layout was computed for.</summary>
    public double Width { get; }

    /// <summary>The lane count the Graph column was sized for.</summary>
    public int LaneCount { get; }

    public GraphColumn Refs { get; }

    public GraphColumn Graph { get; }

    public GraphColumn Message { get; }

    public GraphColumn Author { get; }

    public GraphColumn Date { get; }

    public GraphColumn Sha { get; }

    public bool ShowsAuthor => Author.IsVisible;

    public bool ShowsDate => Date.IsVisible;

    public bool ShowsSha => Sha.IsVisible;

    /// <summary>
    /// Lays the columns out for a graph <paramref name="width"/> wide whose rows use
    /// <paramref name="laneCount"/> lanes at most. The Graph column is 16 per lane plus 16, at
    /// least 48 and at most 240. The message takes the rest; when that would be less than 120,
    /// the SHA column is hidden first, then Author, then Date. If even then the message gets less
    /// than 120, it keeps 120 and the rows are cut at the control's right edge.
    /// </summary>
    public static GraphColumns Compute(double width, int laneCount)
    {
        if (double.IsNaN(width) || width < 0)
        {
            width = 0;
        }

        laneCount = Math.Max(0, laneCount);
        var graphWidth = Math.Clamp((LaneSpacing * laneCount) + LaneSpacing, MinGraphWidth, MaxGraphWidth);
        var rest = width - RefsWidth - graphWidth;

        var showsSha = true;
        var showsAuthor = true;
        var showsDate = true;
        double Fixed() => (showsAuthor ? AuthorWidth : 0) + (showsDate ? DateWidth : 0) + (showsSha ? ShaWidth : 0);

        if (rest - Fixed() < MinMessageWidth)
        {
            showsSha = false;
        }

        if (rest - Fixed() < MinMessageWidth)
        {
            showsAuthor = false;
        }

        if (rest - Fixed() < MinMessageWidth)
        {
            showsDate = false;
        }

        var messageWidth = Math.Max(MinMessageWidth, rest - Fixed());

        var refs = new GraphColumn(0, RefsWidth, true);
        var graph = new GraphColumn(refs.Right, graphWidth, true);
        var message = new GraphColumn(graph.Right, messageWidth, true);
        var author = new GraphColumn(message.Right, showsAuthor ? AuthorWidth : 0, showsAuthor);
        var date = new GraphColumn(author.Right, showsDate ? DateWidth : 0, showsDate);
        var sha = new GraphColumn(date.Right, showsSha ? ShaWidth : 0, showsSha);
        return new GraphColumns(width, laneCount, refs, graph, message, author, date, sha);
    }

    /// <summary>The centre of lane <paramref name="lane"/> (0 at the left), in the graph control's coordinates.</summary>
    public double LaneCenterX(int lane) => Graph.X + LaneSpacing + (LaneSpacing * lane);
}
