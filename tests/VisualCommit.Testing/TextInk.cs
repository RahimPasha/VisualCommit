using System.Globalization;
using Avalonia;
using Avalonia.Media;
using VisualCommit.Testing.Headless;

namespace VisualCommit.Testing;

/// <summary>
/// The colour a small text is drawn in, read from a picture (docs/phase-6-design.md 11.10 to
/// 11.12, 11.15). Text of 11 to 12 px is drawn with subpixel smoothing, which colours each
/// channel of a pixel apart, and the embedded Inter is rasterized differently on Windows, macOS
/// (CoreText) and Linux (FreeType's LCD filter): a pixel of exactly the text's colour is rare, and
/// even the fullest pixels are tinted a few steps. The rule: within the text's rectangle, each
/// channel's extreme toward the foreground (the most of a channel where the foreground is above
/// the background, the least where it is below) is the text's ink; it lies within
/// <see cref="Tolerance"/> of the foreground in every channel and, where the token replaced a
/// colour, more than <see cref="Tolerance"/> from that colour in some channel. Nothing else in
/// the rectangle may lie further toward the foreground than the text (a border or a background
/// lies between the two, as a tooltip's and a gutter's do).
/// </summary>
public static class TextInk
{
    /// <summary>How far the ink may lie from the text's colour in each channel: phase 5's text tolerance (its blame gutter's date at 11 px reached #9BA3AB for #9BA3B0).</summary>
    public const int Tolerance = 8;

    /// <summary>
    /// The ink of the text drawn in <paramref name="foreground"/> on <paramref name="background"/>
    /// inside <paramref name="area"/> (window pixels, whole pixels inside it): per channel, the
    /// extreme toward the foreground.
    /// </summary>
    public static Color Of(Screenshot picture, Rect area, Color foreground, Color background)
    {
        ArgumentNullException.ThrowIfNull(picture);
        var (r, g, b) = (background.R, background.G, background.B);
        for (var y = (int)Math.Ceiling(area.Top); y < (int)Math.Floor(area.Bottom); y++)
        {
            for (var x = (int)Math.Ceiling(area.Left); x < (int)Math.Floor(area.Right); x++)
            {
                var pixel = picture.PixelAt(x, y);
                r = Toward(r, pixel.R, foreground.R, background.R);
                g = Toward(g, pixel.G, foreground.G, background.G);
                b = Toward(b, pixel.B, foreground.B, background.B);
            }
        }

        return Color.FromRgb(r, g, b);
    }

    /// <summary>
    /// What is wrong with the text in <paramref name="area"/> (<see cref="Of"/>'s rule): its ink
    /// further than <see cref="Tolerance"/> from <paramref name="foreground"/> in a channel, or,
    /// when <paramref name="replaced"/> is given (the colour the token had before, or Fluent's own),
    /// within <see cref="Tolerance"/> of it in every channel. Empty when the text is drawn in the
    /// foreground: <c>Assert.Empty(TextInk.Problems(…))</c>.
    /// </summary>
    public static IReadOnlyList<string> Problems(Screenshot picture, Rect area, Color foreground, Color background, Color? replaced = null, string what = "the text")
    {
        var ink = Of(picture, area, foreground, background);
        var problems = new List<string>();
        if (Distance(ink, foreground) > Tolerance)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"The ink of {what} at {area} is {Hex(ink)}, not within {Tolerance} of {Hex(foreground)} in each channel."));
        }

        if (replaced is { } old && Distance(ink, old) <= Tolerance)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"The ink of {what} at {area} is {Hex(ink)}, within {Tolerance} of the colour it replaced, {Hex(old)}."));
        }

        return problems;
    }

    /// <summary>The largest difference of two colours in one channel.</summary>
    private static int Distance(Color a, Color b) => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

    /// <summary>The channel value furthest toward the foreground's so far.</summary>
    private static byte Toward(byte best, byte pixel, byte foreground, byte background) =>
        foreground >= background ? Math.Max(best, pixel) : Math.Min(best, pixel);

    private static string Hex(Color colour) => string.Create(CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");
}
