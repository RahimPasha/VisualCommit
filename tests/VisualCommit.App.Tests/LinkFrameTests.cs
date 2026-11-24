using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// <see cref="LinkFrame"/>'s rule on pictures made in memory, laid out as GitLab's legacy token link
/// is drawn ("Or create a legacy token, filled in", Inter at 12.5 px, dark): a text box 200 by 16 with
/// the letters' bodies above the underline and two letters (its g and y) crossing the underline's two
/// rows, the underline thinner than a pixel over those rows (Windows' shares, 0.257 and 0.595 of a
/// pixel), Fluent's frame 4 outside the box on every side. Avalonia 12.1.3's underline skips
/// the ink of a letter that crosses it (<c>TextDecoration.Draw</c>: a gap of the line's thickness,
/// 0.852 here, each side of the glyph's outline just under the line), so the line is drawn in pieces
/// whose ends lie inside a column; measured for the legacy link on Windows: 371.000 to 452.008,
/// 456.431 to 472.332 and 475.216 to 564.928. Here the pieces end at the same fractions of a pixel,
/// round a g's tail at 79 to 83 and a y's at 150 to 153.
/// </summary>
public class LinkFrameTests
{
    private const int Width = 240;
    private const int Height = 40;
    private const int UnderlineRow = 23;
    private const int BodyTop = 13;
    private const int BodyBottom = 20;
    private const int TailBottom = 24;

    private static readonly Color Background = Color.Parse("#1A1D24");
    private static readonly Color Accent = Color.Parse("#7C8CFF");
    private static readonly Color Frame = Colors.White;
    private static readonly Color FrameInner = Colors.Black;
    private static readonly Rect TextBox = new(20, 10, 200, 16);

    /// <summary>The share of a pixel of the line in each of its two rows (the legacy link's line, 341.169 ± 0.426, in the rows 340 and 341).</summary>
    private static readonly double[] RowShares = [0.257, 0.595];

    /// <summary>The pieces of the line, their ends inside the letters' tails as on Windows (the g's at 79 to 83, the y's at 150 to 153).</summary>
    private static readonly (double From, double To)[] Pieces = [(20.0, 79.008), (83.431, 150.332), (153.216, 219.928)];

    [AvaloniaFact]
    public void An_underline_drawn_as_on_Windows_passes()
    {
        Assert.Empty(Problems(Draw()));
    }

    /// <summary>
    /// CI's macOS run 45019059991: CoreText draws the y's tail one column narrower in the line's
    /// rows (473 to 475 where Windows and Linux draw 472 to 476), so the column where the piece
    /// before it ends, 472.332, lies clear of the letters and holds only a third of the line
    /// (#232739 and #2B324C over #1A1D24): a piece's end beside a letter, which is no fault.
    /// </summary>
    [AvaloniaFact]
    public void A_piece_s_end_beside_a_letter_that_crosses_the_line_passes_faint()
    {
        var (unfocused, focused) = Draw(yTail: (151, 154));
        Assert.Equal(Mix(0.257 * 0.332), focused.PixelAt(150, UnderlineRow));
        Assert.Empty(Problems((unfocused, focused)));
    }

    [AvaloniaFact]
    public void An_underline_missing_over_several_columns_fails_naming_them()
    {
        var problem = Assert.Single(Problems(Draw(pieces: [(20.0, 79.008), (83.431, 100.0), (106.0, 150.332), (153.216, 219.928)])));
        Assert.Contains("in 6 of the", problem, StringComparison.Ordinal);
        Assert.Contains("100: 0.00 + 0.00 = 0.00 (#1A1D24, #1A1D24), beside 99: 0.26 + 0.59 = 0.85 and 101: 0.00 + 0.00 = 0.00", problem, StringComparison.Ordinal);
        Assert.Contains("105: 0.00 + 0.00 = 0.00 (#1A1D24, #1A1D24), beside 104: 0.00 + 0.00 = 0.00 and 106: 0.26 + 0.59 = 0.85", problem, StringComparison.Ordinal);
    }

    /// <summary>A piece's end lies in one column: a faint column beside a letter and another beside it is a line cut short, not a piece's end.</summary>
    [AvaloniaFact]
    public void Two_faint_columns_side_by_side_beside_a_letter_fail()
    {
        var problem = Assert.Single(Problems(Draw(pieces: [(20.0, 79.008), (83.431, 148.332), (153.216, 219.928)])));
        Assert.Contains("in 2 of the", problem, StringComparison.Ordinal);
        Assert.Contains("148: 0.08 + 0.20 = 0.28 (#222637, #2D334F), beside 147: 0.26 + 0.59 = 0.85 and 149: 0.00 + 0.00 = 0.00", problem, StringComparison.Ordinal);
        Assert.Contains("149: 0.00 + 0.00 = 0.00 (#1A1D24, #1A1D24), beside 148: 0.08 + 0.20 = 0.28 and 150: a letter", problem, StringComparison.Ordinal);
    }

    /// <summary>A faint column away from the letters is no piece's end: the line is cut there.</summary>
    [AvaloniaFact]
    public void A_faint_column_away_from_the_letters_fails()
    {
        var problem = Assert.Single(Problems(Draw(pieces: [(20.0, 79.008), (83.431, 120.332), (121.0, 150.332), (153.216, 219.928)])));
        Assert.Contains("in 1 of the", problem, StringComparison.Ordinal);
        Assert.Contains("120: 0.08 + 0.20 = 0.28 (#222637, #2D334F), beside 119: 0.26 + 0.59 = 0.85 and 121: 0.26 + 0.59 = 0.85", problem, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void A_text_without_its_underline_fails()
    {
        var problem = Assert.Single(Problems(Draw(pieces: [])));
        Assert.Contains("shows no underline", problem, StringComparison.Ordinal);
    }

    /// <summary>
    /// At a display scaling of 2 the line is about two pixels thick and lies across three rows (About's
    /// links' underline there: about 0.5, 1 and 0.25 of a pixel): no fault at 2, while at 1, where the
    /// line is thinner than a pixel, three changed rows are more than an underline.
    /// </summary>
    [AvaloniaFact]
    public void An_underline_across_three_rows_passes_at_a_scaling_of_2_and_not_at_1()
    {
        var pictures = Draw(shares: [0.5, 1.0, 0.25]);
        Assert.Empty(Problems(pictures, scale: 2));
        var problem = Assert.Single(Problems(pictures, scale: 1));
        Assert.Contains("in the rows 23, 24, 25", problem, StringComparison.Ordinal);
        Assert.Contains("an underline of at most 2 rows", problem, StringComparison.Ordinal);
    }

    /// <summary>Four changed rows are more than an underline at 2 too.</summary>
    [AvaloniaFact]
    public void Four_changed_rows_fail_at_a_scaling_of_2()
    {
        var problem = Assert.Single(Problems(Draw(shares: [0.3, 1.0, 1.0, 0.3], firstRow: UnderlineRow - 1), scale: 2));
        Assert.Contains("in the rows 22, 23, 24, 25", problem, StringComparison.Ordinal);
        Assert.Contains("an underline of at most 3 rows", problem, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void The_underlines_rows_grow_with_the_scaling()
    {
        Assert.Equal([2, 3, 3, 3, 3], new[] { 1, 1.25, 1.5, 1.75, 2 }.Select(LinkFrame.UnderlineRowsAt));
        Assert.Throws<ArgumentOutOfRangeException>(() => LinkFrame.UnderlineRowsAt(0.75));
    }

    /// <summary>A frame's line over the underline's rows (as Fluent's frame lay on the link's bounds): no column holds the link's colour there.</summary>
    [AvaloniaFact]
    public void An_underline_covered_by_a_frame_s_line_fails()
    {
        var problems = Problems(Draw(cover: true));
        Assert.Contains(problems, problem => problem.Contains("change to a colour that is not the link's", StringComparison.Ordinal));
        var underline = Assert.Single(problems, problem => problem.Contains("underline in the rows 23, 24", StringComparison.Ordinal));
        Assert.Contains("20: #FFFFFF + #FFFFFF (#FFFFFF, #FFFFFF), beside 19: past the text's ends and 21: #FFFFFF + #FFFFFF", underline, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same window with and without the focus; the letters' bodies above the line, the g's and y's
    /// tails crossing it; the line's share of a pixel in each row from <paramref name="firstRow"/>,
    /// <paramref name="shares"/> (<see cref="RowShares"/> from <see cref="UnderlineRow"/> when not given).
    /// </summary>
    private static (Screenshot Unfocused, Screenshot Focused) Draw((int From, int To)? yTail = null, (double From, double To)[]? pieces = null, bool cover = false, double[]? shares = null, int firstRow = UnderlineRow)
    {
        var rowShares = shares ?? RowShares;
        var letters = Enumerable.Range(0, 33).Select(i => (From: 20 + (6 * i), To: 24 + (6 * i))).Append((From: 216, To: 220)).ToList();
        (int From, int To)[] tails = [(79, 84), yTail ?? (150, 154)];
        var plain = new Color[Width, Height];
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                var body = y >= BodyTop && y <= BodyBottom && letters.Any(l => x >= l.From && x < l.To);
                var tail = y > BodyBottom && y <= TailBottom && tails.Any(t => x >= t.From && x < t.To);
                plain[x, y] = body || tail ? Accent : Background;
            }
        }

        var focused = (Color[,])plain.Clone();
        for (var x = 0; x < Width; x++)
        {
            var covered = (pieces ?? Pieces).Sum(piece => Math.Max(0, Math.Min(piece.To, x + 1) - Math.Max(piece.From, x)));
            for (var row = 0; row < rowShares.Length; row++)
            {
                if (covered > 0 && focused[x, firstRow + row] == Background)
                {
                    focused[x, firstRow + row] = Mix(rowShares[row] * covered);
                }
            }
        }

        // Fluent's frame: 2 of its colour outside, 1 inside, round the box grown by 4.
        for (var x = 16; x < 224; x++)
        {
            for (var y = 6; y < 30; y++)
            {
                var ring = Math.Min(Math.Min(x - 16, 223 - x), Math.Min(y - 6, 29 - y));
                if (ring < 2)
                {
                    focused[x, y] = Frame;
                }
                else if (ring == 2)
                {
                    focused[x, y] = FrameInner;
                }
            }
        }

        if (cover)
        {
            for (var x = 20; x < 220; x++)
            {
                focused[x, UnderlineRow] = Frame;
                focused[x, UnderlineRow + 1] = Frame;
            }
        }

        return (Picture(plain), Picture(focused));
    }

    private static IReadOnlyList<string> Problems((Screenshot Unfocused, Screenshot Focused) pictures, double scale = 1) =>
        LinkFrame.Problems(pictures.Unfocused, pictures.Focused, TextBox, Accent, Background, Frame, "the link", scale);

    /// <summary>The link's colour over the background with <paramref name="share"/> of a pixel, each channel rounded.</summary>
    private static Color Mix(double share)
    {
        byte Channel(byte from, byte to) => (byte)Math.Round(from + ((to - from) * share), MidpointRounding.AwayFromZero);
        return Color.FromRgb(Channel(Background.R, Accent.R), Channel(Background.G, Accent.G), Channel(Background.B, Accent.B));
    }

    /// <summary>A screenshot of <paramref name="pixels"/> (x, y), as the headless window's capture holds one.</summary>
    private static Screenshot Picture(Color[,] pixels)
    {
        var bitmap = new WriteableBitmap(new PixelSize(Width, Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var buffer = bitmap.Lock())
        {
            var row = new byte[buffer.RowBytes];
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var colour = pixels[x, y];
                    (row[x * 4], row[(x * 4) + 1], row[(x * 4) + 2], row[(x * 4) + 3]) = (colour.B, colour.G, colour.R, 255);
                }

                Marshal.Copy(row, 0, buffer.Address + (y * buffer.RowBytes), row.Length);
            }
        }

        return new Screenshot(bitmap);
    }
}
