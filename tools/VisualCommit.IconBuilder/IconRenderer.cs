using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

namespace VisualCommit.IconBuilder;

/// <summary>
/// The rules every master keeps before anything is drawn from it (D428): its viewBox, a width and
/// height that are none or the viewBox's size, paths and basic shapes with solid fills or simple
/// linear gradients only, and nothing it refers to outside itself, so that every renderer draws it
/// the same way and it carries nothing but the mark.
/// </summary>
internal static partial class MasterRules
{
    private const string SvgNamespace = "http://www.w3.org/2000/svg";

    /// <summary>Every element a master may hold: paths and basic shapes, groups, and linear gradients.</summary>
    private static readonly HashSet<string> AllowedElements =
        ["svg", "g", "defs", "title", "desc", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "linearGradient", "stop"];

    /// <summary>What is wrong with the master at <paramref name="path"/>, shown as <paramref name="shown"/>; empty when nothing is.</summary>
    public static IReadOnlyList<string> Check(string path, string shown, Master master)
    {
        if (!File.Exists(path))
        {
            return [$"{shown}: is missing."];
        }

        XDocument svg;
        try
        {
            svg = XDocument.Load(path, LoadOptions.None);
        }
        catch (XmlException exception)
        {
            return [$"{shown}: is not well-formed XML: {exception.Message}"];
        }

        var problems = new List<string>();
        var root = svg.Root!;
        var viewBox = (string?)root.Attribute("viewBox") ?? string.Empty;
        if (viewBox != master.ViewBox)
        {
            problems.Add($"{shown}: has the viewBox \"{viewBox}\"; a master needs \"{master.ViewBox}\".");
        }

        // Svg.Skia draws the viewBox onto the width and height, and the renderer scales that picture
        // by the size over the viewBox's size: another width or height would draw the mark at another
        // size, or stretched.
        var size = master.Size.ToString(CultureInfo.InvariantCulture);
        foreach (var dimension in (string[])["width", "height"])
        {
            if (root.Attribute(dimension) is { } found && found.Value != size)
            {
                problems.Add($"{shown}: has the {dimension} \"{found.Value}\"; a master needs none or \"{size}\".");
            }
        }

        foreach (var element in root.DescendantsAndSelf())
        {
            if (element.Name.NamespaceName != SvgNamespace || !AllowedElements.Contains(element.Name.LocalName))
            {
                problems.Add($"{shown}: holds a <{element.Name.LocalName}> element, which a master may not use.");
            }

            foreach (var attribute in element.Attributes())
            {
                if (attribute.Name.LocalName == "href" && !attribute.Value.StartsWith('#'))
                {
                    problems.Add($"{shown}: refers outside itself: {attribute.Value}");
                }

                foreach (Match reference in UrlReference().Matches(attribute.Value))
                {
                    if (!reference.Groups[1].Value.StartsWith('#'))
                    {
                        problems.Add($"{shown}: refers outside itself: {reference.Value}");
                    }
                }
            }
        }

        return problems;
    }

    /// <summary>
    /// A master's hash for the manifest: the lower-case hex SHA-256 of its UTF-8 text with
    /// <c>\r\n</c> read as <c>\n</c>, so that a Windows checkout's line ends do not count.
    /// </summary>
    public static string Hash(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal))));

    /// <summary>A <c>url(…)</c> in an attribute's value, its target in group 1.</summary>
    [GeneratedRegex(@"url\(\s*['""]?([^'"")]*)['""]?\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex UrlReference();
}

/// <summary>One master's drawing at one size: the PNG bytes and the pixels.</summary>
/// <param name="Size">Its width and height in pixels.</param>
/// <param name="Png">The PNG, 8-bit RGBA, as SkiaSharp's encoder writes it.</param>
/// <param name="Bgra">The pixels, 4 bytes each, top-down, unpremultiplied, as an icon's bitmap takes them.</param>
internal sealed record RenderedImage(int Size, byte[] Png, byte[] Bgra);

/// <summary>
/// Draws one master at any size with Svg.Skia on SkiaSharp: the master's picture scaled by the size
/// over its viewBox onto a transparent square, antialiased as Svg.Skia's paints are by default. Each
/// size is drawn once and kept, so that a file and the <c>.ico</c> frame of the same size and master
/// hold the same pixels.
/// </summary>
internal sealed class IconRenderer : IDisposable
{
    private readonly SKSvg _svg;
    private readonly SKPicture _picture;
    private readonly Master _master;
    private readonly Dictionary<int, RenderedImage> _drawn = [];

    private IconRenderer(SKSvg svg, SKPicture picture, Master master)
    {
        _svg = svg;
        _picture = picture;
        _master = master;
    }

    /// <summary>Loads the master at <paramref name="path"/>.</summary>
    /// <exception cref="InvalidDataException">Svg.Skia made no picture of it.</exception>
    public static IconRenderer Load(string path, string shown, Master master)
    {
        var svg = new SKSvg();
        var picture = svg.Load(path);
        if (picture is null)
        {
            svg.Dispose();
            throw new InvalidDataException($"{shown}: could not be drawn: Svg.Skia made no picture of it.");
        }

        return new IconRenderer(svg, picture, master);
    }

    /// <summary>The master drawn at <paramref name="size"/> × <paramref name="size"/> pixels.</summary>
    public RenderedImage Render(int size)
    {
        if (_drawn.TryGetValue(size, out var drawn))
        {
            return drawn;
        }

        var info = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException($"SkiaSharp made no surface of {size} px.");
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(size / (float)_master.Size);
        canvas.DrawPicture(_picture);
        canvas.Flush();
        using var image = surface.Snapshot();

        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        using var pixels = new SKBitmap(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        if (!image.ReadPixels(pixels.Info, pixels.GetPixels(), pixels.RowBytes, 0, 0))
        {
            throw new InvalidOperationException($"SkiaSharp could not read the pixels of {_master.Name} at {size} px.");
        }

        drawn = new RenderedImage(size, png.ToArray(), pixels.GetPixelSpan().ToArray());
        _drawn[size] = drawn;
        return drawn;
    }

    public void Dispose()
    {
        _picture.Dispose();
        _svg.Dispose();
    }
}

/// <summary>
/// WCAG 2's contrast for the owner's rounds (D407): the share of a drawing's opaque pixels that
/// stand at 3:1 or more against a desk's colour, Microsoft's floor for an icon's shape. Printed only.
/// </summary>
internal static class DeskContrast
{
    /// <summary>The share, 0 to 100, of the fully opaque pixels in <paramref name="bgra"/> at 3:1 or more against <paramref name="desk"/>.</summary>
    public static double ShareAtThreeToOne(byte[] bgra, SKColor desk)
    {
        var deskLuminance = Luminance(desk.Red, desk.Green, desk.Blue);
        var opaque = 0;
        var enough = 0;
        for (var at = 0; at < bgra.Length; at += 4)
        {
            if (bgra[at + 3] != 255)
            {
                continue;
            }

            opaque++;
            var luminance = Luminance(bgra[at + 2], bgra[at + 1], bgra[at]);
            var ratio = (Math.Max(luminance, deskLuminance) + 0.05) / (Math.Min(luminance, deskLuminance) + 0.05);
            enough += ratio >= 3 ? 1 : 0;
        }

        return opaque == 0 ? 0 : 100.0 * enough / opaque;
    }

    private static double Luminance(byte red, byte green, byte blue) =>
        (0.2126 * Linear(red)) + (0.7152 * Linear(green)) + (0.0722 * Linear(blue));

    private static double Linear(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
