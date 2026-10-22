namespace VisualCommit.App.Views.Workspaces;

/// <summary>The dashboard's columns, from the left (docs/phase-6-design.md 2.6).</summary>
public enum DashboardColumn
{
    Repository,
    Folder,
    Branch,
    AheadBehind,
    Changes,
    State,
    Operation,
}

/// <summary>One column of the dashboard: where it starts and how wide it is.</summary>
/// <param name="X">The column's left edge.</param>
/// <param name="Width">Its width; 0 for a hidden column.</param>
/// <param name="IsVisible">False for the Folder column below 1280 wide.</param>
public readonly record struct DashboardColumnSpan(double X, double Width, bool IsVisible)
{
    /// <summary>The column's right edge.</summary>
    public double Right => X + Width;
}

/// <summary>
/// The dashboard's column layout (docs/phase-6-design.md 2.6, D206), shared by the column headings
/// and every row (<see cref="DashboardRowPanel"/>), as <c>GraphColumns</c> is (D49): Repository 132,
/// Folder, Branch 150, Ahead / behind 130, Changes 220, State 180, Operation. With
/// <c>rest = width − 812</c>: from <c>rest ≥ 468</c> (1280 wide) Operation is
/// <c>clamp(300 + (rest − 468) / 2, 300, 460)</c> and Folder the rest; below, Folder is hidden and
/// Operation <c>max(rest, 180)</c>. Pure, so it is tested without a UI.
/// </summary>
public sealed record DashboardColumns
{
    public const double RepositoryWidth = 132;

    public const double BranchWidth = 150;

    public const double AheadBehindWidth = 130;

    public const double ChangesWidth = 220;

    public const double StateWidth = 180;

    /// <summary>The fixed columns together: what the Folder and Operation columns share the rest of.</summary>
    public const double FixedWidth = RepositoryWidth + BranchWidth + AheadBehindWidth + ChangesWidth + StateWidth;

    /// <summary>The rest from which Folder shows (1280 wide).</summary>
    public const double FolderFrom = 468;

    public const double MinOperationWithFolder = 300;

    public const double MaxOperation = 460;

    public const double MinOperation = 180;

    /// <summary>How far into its column a heading's or a cell's text starts, and how far before the next it ends.</summary>
    public const double TextInset = 8;

    private DashboardColumns(double width, DashboardColumnSpan[] columns)
    {
        Width = width;
        _columns = columns;
    }

    private readonly DashboardColumnSpan[] _columns;

    /// <summary>The layout of a list with no width, before it has been arranged.</summary>
    public static DashboardColumns Empty { get; } = Compute(0);

    /// <summary>The width the layout was computed for.</summary>
    public double Width { get; }

    /// <summary>The Folder column shows.</summary>
    public bool ShowsFolder => this[DashboardColumn.Folder].IsVisible;

    /// <summary>Where <paramref name="column"/> lies.</summary>
    public DashboardColumnSpan this[DashboardColumn column] => _columns[(int)column];

    /// <summary>Lays the columns out for a list <paramref name="width"/> wide.</summary>
    public static DashboardColumns Compute(double width)
    {
        if (double.IsNaN(width) || width < 0)
        {
            width = 0;
        }

        var rest = width - FixedWidth;
        double folder;
        double operation;
        if (rest >= FolderFrom)
        {
            operation = Math.Clamp(MinOperationWithFolder + ((rest - FolderFrom) / 2), MinOperationWithFolder, MaxOperation);
            folder = rest - operation;
        }
        else
        {
            folder = 0;
            operation = Math.Max(rest, MinOperation);
        }

        var columns = new DashboardColumnSpan[7];
        var x = 0.0;
        Add(DashboardColumn.Repository, RepositoryWidth, true);
        Add(DashboardColumn.Folder, folder, folder > 0);
        Add(DashboardColumn.Branch, BranchWidth, true);
        Add(DashboardColumn.AheadBehind, AheadBehindWidth, true);
        Add(DashboardColumn.Changes, ChangesWidth, true);
        Add(DashboardColumn.State, StateWidth, true);
        Add(DashboardColumn.Operation, operation, true);
        return new DashboardColumns(width, columns);

        void Add(DashboardColumn column, double columnWidth, bool visible)
        {
            columns[(int)column] = new DashboardColumnSpan(x, visible ? columnWidth : 0, visible);
            x += visible ? columnWidth : 0;
        }
    }

    public bool Equals(DashboardColumns? other) =>
        other is not null && Width.Equals(other.Width) && _columns.AsSpan().SequenceEqual(other._columns);

    public override int GetHashCode() => Width.GetHashCode();
}
