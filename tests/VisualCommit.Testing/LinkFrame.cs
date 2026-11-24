using System.Globalization;
using Avalonia;
using Avalonia.Media;
using VisualCommit.Testing.Headless;

namespace VisualCommit.Testing;

/// <summary>
/// A link's keyboard frame read from two pictures of one window, the link not focused and then
/// focused by the keyboard (<c>VcLinkButton</c>; phase 8a's A1 step 9, where Fluent's frame lay on
/// the link's bounds, which are its text's line box, and covered its first and last letters and its
/// underline). The rule, inside the text's box (its <c>TextBlock</c>'s bounds, whole pixels):
/// <list type="bullet">
/// <item>the focus changes no row of the box but those of the underline: at most
/// <see cref="UnderlineRowsAt"/> rows at the pictures' display scaling (<see cref="UnderlineRows"/> at
/// 1), next to each other, in the box's lower half;</item>
/// <item>in those rows every pixel either stays as it was or moves toward the link's colour in each
/// channel (an underline drawn in the link's colour over the text), so no ink of a letter is
/// covered by anything else;</item>
/// <item>the underline shows: across the text's ink, from its first column to its last, every column
/// whose underline rows lay on the background holds there the link's colour over the background, at
/// least <see cref="MinUnderline"/> of a pixel in all, but where a piece of the line ends: the first
/// and last columns, and one column alone beside a letter that crosses the line, which the line skips
/// (<c>UnderlineColumns.PieceEnd</c>); and such columns are most of the text's;</item>
/// </list>
/// and outside the box, the frame: a line of exactly the frame's colour on each of the four sides,
/// within <see cref="Reach"/> of the box, the four lines closing a ring (no side cut by a parent's
/// clip), none of it in the picture without the focus; and between the frame and the text's ink,
/// its underline included, a line of the background on each side, so that the frame touches no
/// letter.
/// </summary>
public static class LinkFrame
{
    /// <summary>The most rows the underline may take at a display scaling of 1 (a line of about one pixel, antialiased over two).</summary>
    public const int UnderlineRows = 2;

    /// <summary>
    /// How far outside the text's box the frame's outer line is looked for, in the pictures' device
    /// pixels. <c>VcLinkButton</c>'s outer line lies 2 to 4 px outside the link's bounds, which are its
    /// text's box: its nearest whole row lies 3 device pixels out at a display scaling of 1 and 5 at 2,
    /// and within this reach up to a scaling of 3.5.
    /// </summary>
    public const int Reach = 8;

    /// <summary>
    /// The least of a pixel of the link's colour a column's underline rows hold: the app's underline
    /// (Inter's, under 12 to 13 px text) is thinner than a pixel at a display scaling of 1 and lies
    /// across two rows, about 0.85 of a pixel in all, in either theme; at a greater scaling it holds
    /// more.
    /// </summary>
    public const double MinUnderline = 0.5;

    /// <summary>How far the channels' shares of one pixel may differ (rounding of channels that span 32 steps or more).</summary>
    private const double ShareTolerance = 0.12;

    /// <summary>
    /// The most rows the underline may take in pictures drawn at the display scaling
    /// <paramref name="scale"/>: the line, about one pixel thick at 1, is about
    /// <paramref name="scale"/> device pixels thick there and lies across a row more than the whole
    /// pixels it reaches into; <see cref="UnderlineRows"/> at 1, 3 from 1.25 to 2 (at 2 About's links'
    /// underline lies across three rows, about 0.5, 1 and 0.25 of a pixel).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scale"/> is less than 1.</exception>
    public static int UnderlineRowsAt(double scale)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(scale, 1);
        return UnderlineRows - 1 + (int)Math.Ceiling(scale);
    }

    /// <summary>
    /// What is wrong with the focused link's look; empty when the frame lies outside the text, the
    /// text's ink is all there and the underline shows under it.
    /// </summary>
    /// <param name="unfocused">The window with the link neither focused nor hovered.</param>
    /// <param name="focused">The same window with the link focused by the keyboard (<c>:focus-visible</c>), not hovered.</param>
    /// <param name="text">The link's text's bounds (its <c>TextBlock</c>'s) in the pictures' device pixels: its bounds in the window times <paramref name="scale"/>, the window's pixels at a scaling of 1.</param>
    /// <param name="link">The link's text colour.</param>
    /// <param name="background">The colour behind the link.</param>
    /// <param name="frame">The frame's outer line's colour (Fluent's <c>SystemControlFocusVisualPrimaryBrush</c>).</param>
    /// <param name="what">The link's name, for the messages.</param>
    /// <param name="scale">The display scaling the two pictures were drawn at, whose device pixels <paramref name="text"/> is given in; 1 or more.</param>
    public static IReadOnlyList<string> Problems(Screenshot unfocused, Screenshot focused, Rect text, Color link, Color background, Color frame, string what, double scale = 1)
    {
        ArgumentNullException.ThrowIfNull(unfocused);
        ArgumentNullException.ThrowIfNull(focused);
        var underlineRows = UnderlineRowsAt(scale);
        var problems = new List<string>();
        if (unfocused.Width != focused.Width || unfocused.Height != focused.Height)
        {
            problems.Add($"The two pictures of {what} differ in size.");
            return problems;
        }

        var left = (int)Math.Ceiling(text.Left);
        var top = (int)Math.Ceiling(text.Top);
        var right = (int)Math.Floor(text.Right);
        var bottom = (int)Math.Floor(text.Bottom);
        if (left - Reach < 0 || top - Reach < 0 || right + Reach > focused.Width || bottom + Reach > focused.Height || right <= left || bottom <= top)
        {
            problems.Add(Text($"The text's box of {what}, {text}, does not lie {Reach} or more inside the picture."));
            return problems;
        }

        // The text's ink in the picture without the focus: its first and last columns.
        var inkColumns = Enumerable.Range(left, right - left).Where(x => Enumerable.Range(top, bottom - top).Any(y => unfocused.PixelAt(x, y) != background)).ToList();
        if (inkColumns.Count == 0)
        {
            problems.Add(Text($"{what} shows no ink in its box {Box(left, top, right, bottom)} without the focus."));
            return problems;
        }

        var (inkLeft, inkRight) = (inkColumns[0], inkColumns[^1]);

        // The rows the focus changes inside the box: the underline's only.
        var changed = new List<int>();
        var changes = new List<(int X, int Y)>();
        for (var y = top; y < bottom; y++)
        {
            var any = false;
            for (var x = left; x < right; x++)
            {
                if (unfocused.PixelAt(x, y) != focused.PixelAt(x, y))
                {
                    changes.Add((x, y));
                    any = true;
                }
            }

            if (any)
            {
                changed.Add(y);
            }
        }

        var middle = top + ((bottom - top) / 2);
        if (changed.Count > underlineRows || (changed.Count > 1 && changed[^1] - changed[0] != changed.Count - 1) || changed.Any(y => y < middle))
        {
            var within = Box(changes.Min(c => c.X), changes.Min(c => c.Y), changes.Max(c => c.X) + 1, changes.Max(c => c.Y) + 1);
            problems.Add(Text($"The focus changes {changes.Count} pixels of {what}'s text box {Box(left, top, right, bottom)} in the rows {string.Join(", ", changed)} (within {within}), where only an underline of at most {underlineRows} rows in the box's lower half may change it: something is drawn over the text."));
        }

        // In the underline's rows, every pixel stays or moves toward the link's colour.
        var covered = changes.Where(c => changed.Count <= underlineRows && !TowardLink(unfocused.PixelAt(c.X, c.Y), focused.PixelAt(c.X, c.Y), link)).ToList();
        if (covered.Count > 0)
        {
            var (x, y) = covered[0];
            problems.Add(Text($"{covered.Count} pixels of {what}'s text change to a colour that is not the link's drawn over them, the first at ({x}, {y}): {Hex(unfocused.PixelAt(x, y))} became {Hex(focused.PixelAt(x, y))}, not toward {Hex(link)}."));
        }

        // The underline across the text's ink: in every column whose underline rows lay on the
        // background without the focus, those rows hold the link's colour over it, together at
        // least MinUnderline of a pixel (at a scaling of 1 the line is thinner than a pixel and
        // antialiased over two), but where a piece of the line ends (PieceEnd).
        if (changed.Count == 0)
        {
            problems.Add(Text($"{what} shows no underline in its box {Box(left, top, right, bottom)}: the focus changes none of its pixels."));
        }
        else if (changed.Count <= underlineRows)
        {
            var width = inkRight - inkLeft + 1;
            var clear = Enumerable.Range(inkLeft, width).Where(x => changed.All(y => unfocused.PixelAt(x, y) == background)).ToHashSet();
            var line = new UnderlineColumns(focused, changed, clear, inkLeft, inkRight, link, background);
            var faults = clear.Order().Where(x => line.Share(x) is not { } share || (share < MinUnderline && !line.PieceEnd(x))).ToList();
            if (clear.Count * 2 < width)
            {
                problems.Add(Text($"{what}'s underline rows {string.Join(", ", changed)} cross the letters in {width - clear.Count} of the {width} columns {inkLeft} to {inkRight}: they are no underline under the text."));
            }

            if (faults.Count > 0)
            {
                const int Named = 8;
                var named = faults.Take(Named).Select(x => Text($"{x}: {line.Describe(x)} ({string.Join(", ", changed.Select(y => Hex(focused.PixelAt(x, y))))}), beside {x - 1}: {line.Describe(x - 1)} and {x + 1}: {line.Describe(x + 1)}"));
                var more = faults.Count > Named ? Text($"; and {faults.Count - Named} more") : string.Empty;
                problems.Add(Text($"{what}'s underline in the rows {string.Join(", ", changed)} is not the link's {Hex(link)} over {Hex(background)} at {MinUnderline} of a pixel or more (less only at the text's first and last columns and, alone, beside a letter that crosses the line, where a piece of the line ends) in {faults.Count} of the {clear.Count} columns clear of the letters; each with its share of a pixel in each row, or the colour of a row that is not the link's over the background: {string.Join("; ", named)}{more}."));
            }
        }

        // The frame's outer line: one line of its colour on each side within Reach, closing a ring.
        var leftLine = FirstLine(Enumerable.Range(1, Reach).Select(d => left - d), x => Enumerable.Range(top, bottom - top).All(y => focused.PixelAt(x, y) == frame));
        var rightLine = FirstLine(Enumerable.Range(0, Reach).Select(d => right + d), x => Enumerable.Range(top, bottom - top).All(y => focused.PixelAt(x, y) == frame));
        var topLine = FirstLine(Enumerable.Range(1, Reach).Select(d => top - d), y => Enumerable.Range(left, right - left).All(x => focused.PixelAt(x, y) == frame));
        var bottomLine = FirstLine(Enumerable.Range(0, Reach).Select(d => bottom + d), y => Enumerable.Range(left, right - left).All(x => focused.PixelAt(x, y) == frame));
        foreach (var (line, side) in new[] { (leftLine, "left of"), (rightLine, "right of"), (topLine, "above"), (bottomLine, "below") })
        {
            if (line is null)
            {
                problems.Add(Text($"No whole line of the frame's colour {Hex(frame)} lies {side} {what}'s text box {Box(left, top, right, bottom)} within {Reach} pixels."));
            }
        }

        if (leftLine is { } l && rightLine is { } r && topLine is { } t && bottomLine is { } b)
        {
            var ring = Enumerable.Range(l, r - l + 1).SelectMany(x => new[] { (x, t), (x, b) })
                .Concat(Enumerable.Range(t, b - t + 1).SelectMany(y => new[] { (l, y), (r, y) }))
                .ToList();
            var cut = ring.Where(p => focused.PixelAt(p.Item1, p.Item2) != frame).ToList();
            if (cut.Count > 0)
            {
                problems.Add(Text($"{what}'s frame {Box(l, t, r + 1, b + 1)} is cut: {cut.Count} pixels of its outer line are not {Hex(frame)}, the first at ({cut[0].Item1}, {cut[0].Item2}), {Hex(focused.PixelAt(cut[0].Item1, cut[0].Item2))}."));
            }

            var before = ring.Where(p => unfocused.PixelAt(p.Item1, p.Item2) == frame).ToList();
            if (before.Count > 0)
            {
                problems.Add(Text($"{before.Count} pixels of {what}'s frame {Box(l, t, r + 1, b + 1)} are {Hex(frame)} already without the focus."));
            }

            // Air: a line of the background between the frame and the text's ink, its underline
            // included, on each side.
            var inkRows = Enumerable.Range(top, bottom - top).Where(y => Enumerable.Range(left, right - left).Any(x => unfocused.PixelAt(x, y) != background)).ToList();
            var (inkTop, inkBottom) = (inkRows[0], Math.Max(inkRows[^1], changed.Count > 0 ? changed[^1] : inkRows[^1]));
            var (allLeft, allRight) = changes.Count > 0 ? (Math.Min(inkLeft, changes.Min(c => c.X)), Math.Max(inkRight, changes.Max(c => c.X))) : (inkLeft, inkRight);
            var around = Enumerable.Range(allLeft - 1, allRight - allLeft + 3).SelectMany(x => new[] { (x, inkTop - 1), (x, inkBottom + 1) })
                .Concat(Enumerable.Range(inkTop - 1, inkBottom - inkTop + 3).SelectMany(y => new[] { (allLeft - 1, y), (allRight + 1, y) }))
                .Where(p => focused.PixelAt(p.Item1, p.Item2) != background)
                .ToList();
            if (around.Count > 0)
            {
                problems.Add(Text($"The frame of {what} touches its text: {around.Count} pixels of the line round the ink {Box(allLeft, inkTop, allRight + 1, inkBottom + 1)} are not the background {Hex(background)}, the first at ({around[0].Item1}, {around[0].Item2}), {Hex(focused.PixelAt(around[0].Item1, around[0].Item2))}."));
            }
        }

        return problems;
    }

    /// <summary>The frame's outer line found on one side, or null: the first of <paramref name="candidates"/> (nearest the box first) that is wholly the frame's colour.</summary>
    private static int? FirstLine(IEnumerable<int> candidates, Func<int, bool> whole)
    {
        foreach (var candidate in candidates)
        {
            if (whole(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Each channel of <paramref name="after"/> lies between <paramref name="before"/>'s and <paramref name="link"/>'s, within <see cref="TextInk.Tolerance"/>.</summary>
    private static bool TowardLink(Color before, Color after, Color link) =>
        Between(after.R, before.R, link.R) && Between(after.G, before.G, link.G) && Between(after.B, before.B, link.B);

    private static bool Between(byte value, byte from, byte to) =>
        value >= Math.Min(from, to) - TextInk.Tolerance && value <= Math.Max(from, to) + TextInk.Tolerance;

    /// <summary>The underline's columns across the text's ink, in the picture with the focus.</summary>
    /// <param name="focused">The picture with the focus.</param>
    /// <param name="rows">The underline's rows.</param>
    /// <param name="clear">The columns whose underline rows lay on the background without the focus.</param>
    /// <param name="inkLeft">The text's first column.</param>
    /// <param name="inkRight">The text's last column.</param>
    /// <param name="link">The link's colour.</param>
    /// <param name="background">The colour behind the link.</param>
    private sealed class UnderlineColumns(Screenshot focused, IReadOnlyList<int> rows, IReadOnlySet<int> clear, int inkLeft, int inkRight, Color link, Color background)
    {
        /// <summary>
        /// How much of a pixel of the link's colour column <paramref name="x"/>'s rows hold over the
        /// background, the sum of each row's <see cref="Coverage"/>; null when a row holds another colour.
        /// </summary>
        public double? Share(int x)
        {
            var total = 0.0;
            foreach (var y in rows)
            {
                if (Coverage(focused.PixelAt(x, y), background, link) is not { } share)
                {
                    return null;
                }

                total += share;
            }

            return total;
        }

        /// <summary>
        /// Whether a clear column may hold less than <see cref="MinUnderline"/> because a piece of the line
        /// ends in it: the text's first and last columns (the line's own ends), and a column beside a letter
        /// that crosses the underline's rows, alone, no clear column beside it holding less. Avalonia's
        /// underline skips the ink of a letter that crosses it (12.1.3's <c>TextDecoration.Draw</c>: it
        /// leaves out the glyph's outline just under the line, as <c>GlyphRun.GetIntersections</c> gives
        /// it, and the line's thickness on each side, and draws each piece with flat ends at fractional
        /// x), so the column where a piece ends holds the part of a pixel the piece covers, 0 to all of
        /// it. That column lies next to the letter's ink in the line's rows wherever the ink reaches
        /// within a pixel of the piece's end: for GitLab's legacy link (g and y, the pieces 371.000 to
        /// 452.008, 456.431 to 472.332 and 475.216 to 564.928 on Windows), Windows and Linux draw the y's
        /// tail in the line's rows over 472 to 476 and macOS over 473 to 475, so on macOS 472 is clear and
        /// holds a third of the line (CI run 45019059991: 0.09 and 0.17). A piece cut short by more than a
        /// column, or a faint column away from the letters, is no piece's end.
        /// </summary>
        public bool PieceEnd(int x) =>
            x == inkLeft || x == inkRight || ((Letter(x - 1) || Letter(x + 1)) && !Faint(x - 1) && !Faint(x + 1));

        /// <summary>Column <paramref name="x"/>'s underline for a message: each row's share of a pixel (or its colour when it is not the link's over the background) and their sum; "a letter" where a letter's ink crosses the rows.</summary>
        public string Describe(int x)
        {
            if (x < inkLeft || x > inkRight)
            {
                return "past the text's ends";
            }

            if (Letter(x))
            {
                return "a letter";
            }

            var each = string.Join(" + ", rows.Select(y => Coverage(focused.PixelAt(x, y), background, link) is { } share ? Text($"{share:0.00}") : Hex(focused.PixelAt(x, y))));
            return Share(x) is { } total ? Text($"{each} = {total:0.00}") : each;
        }

        /// <summary>A column inside the text's ink whose underline rows hold a letter's ink without the focus.</summary>
        private bool Letter(int x) => x >= inkLeft && x <= inkRight && !clear.Contains(x);

        /// <summary>A clear column holding less than <see cref="MinUnderline"/> of the line, or another colour.</summary>
        private bool Faint(int x) => clear.Contains(x) && (Share(x) is not { } share || share < MinUnderline);
    }

    /// <summary>
    /// The share of the way from <paramref name="background"/> to <paramref name="link"/> that
    /// <paramref name="pixel"/> lies, the same in each channel within <see cref="ShareTolerance"/>
    /// (the link's colour drawn over the background with that coverage); null for another colour,
    /// such as the frame's.
    /// </summary>
    private static double? Coverage(Color pixel, Color background, Color link)
    {
        var shares = new List<double>();
        foreach (var (p, b, l) in new[] { (pixel.R, background.R, link.R), (pixel.G, background.G, link.G), (pixel.B, background.B, link.B) })
        {
            if (Math.Abs(l - b) >= 24)
            {
                shares.Add((double)(p - b) / (l - b));
            }
            else if (Math.Abs(p - b) > TextInk.Tolerance)
            {
                return null;
            }
        }

        if (shares.Count == 0)
        {
            return null;
        }

        var mean = shares.Average();
        return mean >= -ShareTolerance && mean <= 1 + ShareTolerance && shares.All(share => Math.Abs(share - mean) <= ShareTolerance) ? mean : null;
    }

    private static string Box(int left, int top, int right, int bottom) => Text($"({left}, {top})-({right}, {bottom})");

    private static string Hex(Color colour) => Text($"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
