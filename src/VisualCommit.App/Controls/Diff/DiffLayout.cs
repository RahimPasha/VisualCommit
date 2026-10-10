using VisualCommit.Core.Diff;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.Controls.Diff;

/// <summary>What a row of the diff view shows.</summary>
public enum DiffRowKind
{
    /// <summary>A hunk's header row: git's <c>@@</c> line and the hunk's buttons.</summary>
    HunkHeader,

    /// <summary>A line of a hunk, or side by side a pair of lines facing each other.</summary>
    Line,

    /// <summary>Git's "\ No newline at end of file" after a line.</summary>
    NoNewline,
}

/// <summary>
/// One side of a row: a line of the diff as one side shows it. A side with nothing on it (side by
/// side, the shorter side of a change: a filler) has no cell.
/// </summary>
/// <param name="Line">The diff line the cell shows. For a no-newline row, the line the marker follows.</param>
/// <param name="Kind">That line's kind: context, added or removed.</param>
/// <param name="OldNumber">
/// The number shown in the old number column: inline, the line's number in the old file (context
/// and removed lines); side by side, the left cell's number. Null where none is shown, and always
/// for a no-newline row.
/// </param>
/// <param name="NewNumber">
/// The number shown in the new number column: inline, the line's number in the new file (context
/// and added lines); side by side, the right cell's number. Null where none is shown.
/// </param>
/// <param name="Text">What the cell's text column shows: the line's text, or the no-newline marker.</param>
public sealed record DiffCell(DiffLineRef Line, DiffLineKind Kind, int? OldNumber, int? NewNumber, string Text)
{
    /// <summary>
    /// The cell's number on its own side: side by side the only number the cell has; inline the new
    /// number when the line has one (context and added lines), else the old (removed lines).
    /// </summary>
    public int? Number => NewNumber ?? OldNumber;
}

/// <summary>
/// One row of the diff view as it is displayed in a mode (<see cref="DiffLayout"/>).
/// </summary>
/// <param name="Kind">What the row shows.</param>
/// <param name="Hunk">The hunk the row belongs to, from 0.</param>
/// <param name="Left">
/// Inline: the row's only cell (null for a header row). Side by side: the left (old) side's cell,
/// null for a header row or a filler.
/// </param>
/// <param name="Right">Inline: always null. Side by side: the right (new) side's cell, null for a header row or a filler.</param>
/// <param name="HeaderText">Git's header line for a header row (<c>@@ -12,7 +12,7 @@ public sealed class Calculator</c>); null otherwise.</param>
public sealed record DiffDisplayRow(DiffRowKind Kind, int Hunk, DiffCell? Left, DiffCell? Right, string? HeaderText);

/// <summary>
/// Lays out a file's diff as the rows the diff view shows (D61), with no UI: inline, one column in
/// git's order; side by side, the old file on the left and the new on the right.
/// </summary>
/// <remarks>
/// <para>
/// Inline: each hunk's header row, then a row per line in git's order (within a change the
/// removed lines before the added ones), each with its one cell in <see cref="DiffDisplayRow.Left"/>
/// carrying both numbers git gives it. A line git marks "\ No newline at end of file" is followed
/// by a no-newline row of its own.
/// </para>
/// <para>
/// Side by side: a context line is one row with a cell on each side. A change, the run of removed
/// and added lines between two context lines, of n removed and m added lines becomes max(n, m)
/// rows: the removed lines top-down on the left, the added top-down on the right, the shorter
/// side padded with fillers (null cells) below its lines. When a line of the change has git's
/// no-newline marker, one no-newline row follows the change, with the marker on the side of each
/// line that has one and a filler on a side without. A context line's marker is a row with the
/// marker on both sides.
/// </para>
/// </remarks>
public static class DiffLayout
{
    /// <summary>Git's marker for a last line without a line feed, as the no-newline rows show it.</summary>
    public const string NoNewlineText = "\\ No newline at end of file";

    /// <summary>The rows of <paramref name="diff"/> in <paramref name="mode"/>.</summary>
    public static IReadOnlyList<DiffDisplayRow> Build(FileDiff diff, DiffMode mode)
    {
        ArgumentNullException.ThrowIfNull(diff);
        var rows = new List<DiffDisplayRow>(diff.Hunks.Count + diff.LineCount);
        for (var h = 0; h < diff.Hunks.Count; h++)
        {
            var hunk = diff.Hunks[h];
            rows.Add(new DiffDisplayRow(DiffRowKind.HunkHeader, h, null, null, hunk.Header));
            if (mode == DiffMode.SideBySide)
            {
                AddSideBySide(rows, h, hunk);
            }
            else
            {
                AddInline(rows, h, hunk);
            }
        }

        return rows;
    }

    private static void AddInline(List<DiffDisplayRow> rows, int h, DiffHunk hunk)
    {
        for (var l = 0; l < hunk.Lines.Count; l++)
        {
            var line = hunk.Lines[l];
            var at = new DiffLineRef(h, l);
            rows.Add(new DiffDisplayRow(DiffRowKind.Line, h, new DiffCell(at, line.Kind, line.OldNumber, line.NewNumber, line.Text), null, null));
            if (line.NoNewlineAtEnd)
            {
                rows.Add(new DiffDisplayRow(DiffRowKind.NoNewline, h, Marker(at, line.Kind), null, null));
            }
        }
    }

    private static void AddSideBySide(List<DiffDisplayRow> rows, int h, DiffHunk hunk)
    {
        var lines = hunk.Lines;
        var l = 0;
        while (l < lines.Count)
        {
            var line = lines[l];
            if (line.Kind == DiffLineKind.Context)
            {
                var at = new DiffLineRef(h, l);
                rows.Add(new DiffDisplayRow(
                    DiffRowKind.Line,
                    h,
                    new DiffCell(at, line.Kind, line.OldNumber, null, line.Text),
                    new DiffCell(at, line.Kind, null, line.NewNumber, line.Text),
                    null));
                if (line.NoNewlineAtEnd)
                {
                    rows.Add(new DiffDisplayRow(DiffRowKind.NoNewline, h, Marker(at, line.Kind), Marker(at, line.Kind), null));
                }

                l++;
                continue;
            }

            // A change: every line up to the next context line.
            var removed = new List<int>();
            var added = new List<int>();
            for (; l < lines.Count && lines[l].Kind != DiffLineKind.Context; l++)
            {
                (lines[l].Kind == DiffLineKind.Removed ? removed : added).Add(l);
            }

            for (var i = 0; i < Math.Max(removed.Count, added.Count); i++)
            {
                DiffCell? left = null;
                DiffCell? right = null;
                if (i < removed.Count)
                {
                    var old = lines[removed[i]];
                    left = new DiffCell(new DiffLineRef(h, removed[i]), old.Kind, old.OldNumber, null, old.Text);
                }

                if (i < added.Count)
                {
                    var @new = lines[added[i]];
                    right = new DiffCell(new DiffLineRef(h, added[i]), @new.Kind, null, @new.NewNumber, @new.Text);
                }

                rows.Add(new DiffDisplayRow(DiffRowKind.Line, h, left, right, null));
            }

            var leftMarker = removed.LastOrDefault(index => lines[index].NoNewlineAtEnd, -1);
            var rightMarker = added.LastOrDefault(index => lines[index].NoNewlineAtEnd, -1);
            if (leftMarker >= 0 || rightMarker >= 0)
            {
                rows.Add(new DiffDisplayRow(
                    DiffRowKind.NoNewline,
                    h,
                    leftMarker >= 0 ? Marker(new DiffLineRef(h, leftMarker), DiffLineKind.Removed) : null,
                    rightMarker >= 0 ? Marker(new DiffLineRef(h, rightMarker), DiffLineKind.Added) : null,
                    null));
            }
        }
    }

    private static DiffCell Marker(DiffLineRef line, DiffLineKind kind) => new(line, kind, null, null, NoNewlineText);
}
