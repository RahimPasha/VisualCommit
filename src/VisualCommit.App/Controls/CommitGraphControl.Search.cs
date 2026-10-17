using Avalonia;
using Avalonia.Media;
using VisualCommit.App.ViewModels.Graph;

namespace VisualCommit.App.Controls;

/// <summary>
/// How the graph shows a search (docs/phase-5-design.md 3.3, D157): while <see cref="Search"/> is
/// active, a matching row gets a 3-pixel mark at its left edge and its message in semibold, and
/// every other commit row is dimmed: its node, labels and texts at 35 % opacity, the lanes passing
/// through it at full strength. The selection and hover backgrounds are unchanged, so the match
/// Enter went to is the selected row. With no active search the control draws as without one.
/// </summary>
public sealed partial class CommitGraphControl
{
    /// <summary>The opacity of a dimmed row's node, labels and texts (3.3).</summary>
    public const double SearchDimmedOpacity = 0.35;

    /// <summary>The width of a match's mark, over the first pixels of the Branch / Tag column (3.3).</summary>
    public const double SearchMarkWidth = 3;

    /// <summary>The search's marks while one is active with a text (3.3), or null: the control then draws as without a search.</summary>
    private GraphSearchMarks? ActiveSearch => Search is { IsActive: true } search ? search : null;

    /// <summary>A match's message: the cells' font in semibold.</summary>
    private Typeface MatchTypeface => new(FontFamily, FontStyle.Normal, FontWeight.SemiBold);

    /// <summary>
    /// How the search draws commit row <paramref name="index"/> (3.3): marked when its commit
    /// matches, dimmed when it does not, as without a search when no search is active or the row
    /// does not exist. The working-changes row is never marked or dimmed: it is no commit and is
    /// not searched (decision FD2 of the phase 5 report).
    /// </summary>
    public GraphSearchRowState SearchStateOf(int index) =>
        Data is { } data && index >= 0 && index < data.Count ? SearchStateOf(data, index, ActiveSearch) : GraphSearchRowState.None;

    private static GraphSearchRowState SearchStateOf(CommitGraphData data, int index, GraphSearchMarks? search) =>
        search is null ? GraphSearchRowState.None
        : search.IsMatch(data.CommitAt(index).Sha) ? GraphSearchRowState.Marked
        : GraphSearchRowState.Dimmed;

    private static bool IsDimmed(CommitGraphData data, int index, GraphSearchMarks? search) =>
        SearchStateOf(data, index, search) == GraphSearchRowState.Dimmed;

    /// <summary>A new search, new matches or the search's end: the matches' messages change their weight.</summary>
    private void OnSearchChanged(GraphSearchMarks? oldValue, GraphSearchMarks? newValue)
    {
        if (oldValue is not { IsActive: true } && newValue is not { IsActive: true })
        {
            return;
        }

        _rowCache.Clear();

        // A message in semibold may now be cut with "…", or no longer: its tooltip follows.
        UpdateHover();
    }

    /// <summary>The marks of the matching rows in view: <see cref="SearchMarkWidth"/> wide along the row's left edge, over its background.</summary>
    private void DrawSearchMarks(DrawingContext context, CommitGraphData data, GraphSearchMarks search, int first, int last, double offset, Palette palette)
    {
        var rowOffset = RowOffset;
        var x = Columns.Refs.X;
        for (var display = Math.Max(first, rowOffset); display <= last; display++)
        {
            if (search.IsMatch(data.CommitAt(display - rowOffset).Sha))
            {
                context.FillRectangle(palette.SearchMatch, new Rect(x, RowTop(display, offset), SearchMarkWidth, RowHeight));
            }
        }
    }
}

/// <summary>How a row of the graph is drawn for a search (<see cref="CommitGraphControl.SearchStateOf"/>, docs/phase-5-design.md 3.3).</summary>
public enum GraphSearchRowState
{
    /// <summary>As without a search: no search is active, or the row is not a commit's.</summary>
    None,

    /// <summary>The commit matches: a mark at the row's left edge and its message in semibold.</summary>
    Marked,

    /// <summary>The commit does not match: its node, labels and texts are faded, its lanes are not.</summary>
    Dimmed,
}
