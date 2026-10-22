using Avalonia.Media;

namespace VisualCommit.Testing;

/// <summary>
/// WCAG 2's contrast ratio of two opaque colours (docs/phase-6-design.md 11.10, 11.15): the
/// lighter colour's relative luminance plus 0.05 over the darker one's plus 0.05, from 1 (the
/// same) to 21 (black on white). Text needs 4.5:1, marks that are not text 3:1. Alpha is ignored:
/// the tokens it measures are opaque.
/// </summary>
public static class Contrast
{
    /// <summary>The contrast ratio of <paramref name="first"/> and <paramref name="second"/>, in either order.</summary>
    public static double Ratio(Color first, Color second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>WCAG 2's relative luminance of an sRGB colour: 0 for black, 1 for white.</summary>
    public static double RelativeLuminance(Color colour) =>
        (0.2126 * Linear(colour.R)) + (0.7152 * Linear(colour.G)) + (0.0722 * Linear(colour.B));

    /// <summary>An sRGB channel (0 to 255) as linear light (0 to 1).</summary>
    private static double Linear(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
