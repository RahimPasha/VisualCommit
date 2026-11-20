using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The icon's files as the icon's builder writes them and the repository holds them
/// (docs/phase-8a-design.md 7.3, 7.4; D428 to D430, D447): the three masters keep the masters'
/// rules, the manifest's hashes are the masters' (a master changed without its files fails here),
/// and every generated file has its format and its sizes. Never by pixels, which may differ between
/// machines (D430), but for whether each icon frame is drawn at all. No session; the repository's
/// files through <see cref="IconFile.RepositoryPath"/>. The test of macOS's <c>iconutil</c> runs on
/// macOS alone (CI's macOS job).
/// </summary>
public sealed partial class IconFilesTests
{
    private const string SvgNamespace = "http://www.w3.org/2000/svg";

    /// <summary>Paths and basic shapes, with at most simple linear gradients (7.3): every element a master may hold.</summary>
    private static readonly HashSet<string> AllowedElements =
        ["svg", "g", "defs", "title", "desc", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "linearGradient", "stop"];

    /// <summary>The elements 7.3 names as never in a master.</summary>
    private static readonly string[] ForbiddenElements = ["text", "tspan", "image", "filter", "script", "style", "foreignObject", "pattern"];

    // ---- 1. The masters ---------------------------------------------------------------------

    [Fact]
    public void The_three_masters_exist_and_keep_the_masters_rules()
    {
        foreach (var name in IconFile.Masters)
        {
            var path = MasterPath(name);
            Assert.True(File.Exists(path), $"assets/brand/{name} is missing.");
            var svg = XDocument.Load(path, LoadOptions.None);
            var root = svg.Root!;
            Assert.Equal(XName.Get("svg", SvgNamespace), root.Name);
            Assert.Equal(name == "icon-small.svg" ? "0 0 16 16" : "0 0 1024 1024", (string?)root.Attribute("viewBox"));

            var comment = svg.DescendantNodes().OfType<XComment>().FirstOrDefault();
            Assert.True(comment is not null, $"assets/brand/{name} has no comment that says what it is.");
            Assert.Matches(ChosenComment(), comment.Value);

            foreach (var element in root.DescendantsAndSelf())
            {
                Assert.True(element.Name.NamespaceName == SvgNamespace, $"assets/brand/{name} holds the element {element.Name}, outside SVG's namespace.");
                Assert.DoesNotContain(element.Name.LocalName, ForbiddenElements);
                Assert.True(AllowedElements.Contains(element.Name.LocalName), $"assets/brand/{name} holds a <{element.Name.LocalName}> element, which a master may not use.");
                foreach (var attribute in element.Attributes())
                {
                    if (attribute.Name.LocalName == "href")
                    {
                        Assert.True(attribute.Value.StartsWith('#'), $"assets/brand/{name} refers outside itself: {attribute.Value}");
                    }

                    foreach (Match reference in UrlReference().Matches(attribute.Value))
                    {
                        Assert.True(reference.Groups[1].Value.StartsWith('#'), $"assets/brand/{name} refers outside itself: {reference.Value}");
                    }
                }
            }
        }
    }

    [Fact]
    public void The_small_master_has_its_straight_edges_on_whole_units_of_its_16_grid()
    {
        var root = XDocument.Load(MasterPath("icon-small.svg")).Root!;
        var problems = new List<string>();
        foreach (var rect in root.Descendants(XName.Get("rect", SvgNamespace)))
        {
            var x = Number(rect, "x");
            var y = Number(rect, "y");
            var edges = new[] { ("left", x), ("top", y), ("right", x + Number(rect, "width")), ("bottom", y + Number(rect, "height")) };
            problems.AddRange(edges.Where(edge => !IsWhole(edge.Item2)).Select(edge => $"a rect's {edge.Item1} edge at {edge.Item2}"));
        }

        foreach (var path in root.Descendants(XName.Get("path", SvgNamespace)))
        {
            problems.AddRange(StraightEdgesOffTheGrid((string)path.Attribute("d")!));
        }

        Assert.True(problems.Count == 0, "icon-small.svg's straight edges must fall on whole units:\n" + string.Join('\n', problems));
    }

    // ---- 2. The manifest --------------------------------------------------------------------

    [Fact]
    public void The_manifest_holds_the_masters_hashes_and_every_files_sizes()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(IconFile.RepositoryPath(IconFile.ManifestFile)));
        var root = manifest.RootElement;
        Assert.Equal(1, root.GetProperty("v").GetInt32());

        var masters = root.GetProperty("masters");
        Assert.Equal(IconFile.Masters.Order(StringComparer.Ordinal), masters.EnumerateObject().Select(master => master.Name).Order(StringComparer.Ordinal));
        foreach (var name in IconFile.Masters)
        {
            Assert.True(
                masters.GetProperty(name).GetString() == HashOf(MasterPath(name)),
                $"assets/brand/{name} changed after the icon's files were made: run dotnet run --project tools/VisualCommit.IconBuilder and commit what it writes.");
        }

        var files = root.GetProperty("files").EnumerateObject().ToDictionary(file => file.Name, file => file.Value.EnumerateArray().Select(size => size.GetInt32()).ToList(), StringComparer.Ordinal);
        var expected = ExpectedFiles();
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), files.Keys.Order(StringComparer.Ordinal));
        foreach (var (file, sizes) in expected)
        {
            Assert.True(sizes.SequenceEqual(files[file]), $"The manifest gives {file} the sizes {string.Join(", ", files[file])}, not {string.Join(", ", sizes)}.");
        }
    }

    // ---- 3. The .ico ------------------------------------------------------------------------

    [Fact]
    public void The_ico_holds_its_14_sizes_ascending_the_256_a_png_and_the_others_32_bit_bitmaps()
    {
        var path = IconFile.RepositoryPath(IconFile.IcoFile);
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(bytes));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)));
        Assert.Equal(14, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4)));

        var frames = IconFile.Read(path);
        Assert.Equal(IconFile.IcoSizes, frames.Select(frame => frame.Size));
        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames[index];
            var size = frame.Size;
            var entry = bytes.AsSpan(6 + (index * 16), 16);
            Assert.Equal(entry[0], entry[1]);
            Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(entry[4..]));
            Assert.Equal(32, BinaryPrimitives.ReadUInt16LittleEndian(entry[6..]));
            if (size == IconFile.IcoPngSize)
            {
                Assert.True(frame.IsPng, "The .ico's 256 is not a PNG.");
                var (width, height, _, _) = PngHeader.Read(frame.Data);
                Assert.Equal((256, 256), (width, height));
            }
            else
            {
                Assert.False(frame.IsPng, $"The .ico's {size} is a PNG, not a bitmap.");
                var data = frame.Data;
                Assert.Equal(40, BinaryPrimitives.ReadInt32LittleEndian(data));
                Assert.Equal(size, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4)));
                Assert.Equal(2 * size, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8)));
                Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(12)));
                Assert.Equal(32, BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(14)));
                Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(16)));
                var maskRow = (size + 31) / 32 * 4;
                Assert.Equal(40 + (size * size * 4) + (maskRow * size), data.Length);
                Assert.True(data.AsSpan(40 + (size * size * 4)).IndexOfAnyExcept((byte)0) < 0, $"The .ico's {size} has an AND mask that is not all zeros.");
            }

            AssertDrawnAndNotASquare(frame.Bgra(), size, $"The .ico's {size}");
        }
    }

    // ---- 4 to 6. The PNGs -------------------------------------------------------------------

    [Fact]
    public void The_iconset_holds_exactly_its_ten_8_bit_rgba_pngs()
    {
        var folder = IconFile.RepositoryPath(IconFile.IconSetFolder);
        Assert.Equal(
            IconFile.IconSetNames.Select(file => file.Name + ".png").Order(StringComparer.Ordinal),
            Directory.EnumerateFileSystemEntries(folder).Select(entry => Path.GetFileName(entry)).Order(StringComparer.Ordinal));
        foreach (var (name, size) in IconFile.IconSetNames)
        {
            Assert.Equal((size, size, 8, PngHeader.Rgba), PngHeader.Read(IconFile.RepositoryPath(IconFile.IconSetFile(name))));
        }
    }

    [Fact]
    public void Linux_s_png_folder_holds_exactly_its_eight_sizes()
    {
        var folder = IconFile.RepositoryPath(IconFile.PngFolder);
        Assert.Equal(
            IconFile.PngSizes.Select(size => Path.GetFileName(IconFile.PngFile(size))).Order(StringComparer.Ordinal),
            Directory.EnumerateFileSystemEntries(folder).Select(entry => Path.GetFileName(entry)).Order(StringComparer.Ordinal));
        foreach (var size in IconFile.PngSizes)
        {
            var (width, height, _, _) = PngHeader.Read(IconFile.RepositoryPath(IconFile.PngFile(size)));
            Assert.Equal((size, size), (width, height));
        }
    }

    [Fact]
    public void The_app_s_icon_folder_holds_exactly_the_ico_and_its_two_pngs()
    {
        var folder = IconFile.RepositoryPath(IconFile.AppIconFolder);
        Assert.Equal(
            new[] { "VisualCommit-48.png", "VisualCommit-96.png", "VisualCommit.ico" },
            Directory.EnumerateFileSystemEntries(folder).Select(entry => Path.GetFileName(entry)).Order(StringComparer.Ordinal));
        foreach (var size in IconFile.AppImageSizes)
        {
            Assert.Equal((size, size, 8, PngHeader.Rgba), PngHeader.Read(IconFile.RepositoryPath(IconFile.AppImageFile(size))));
        }
    }

    // ---- 7 and 8. The .icns -----------------------------------------------------------------

    [Fact]
    public void The_icns_holds_the_iconset_s_pngs_byte_for_byte_under_apple_s_types_in_order()
    {
        var path = IconFile.RepositoryPath(IconFile.IcnsFile);
        var bytes = File.ReadAllBytes(path);
        Assert.Equal("icns", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal((uint)bytes.Length, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4)));

        var entries = IconFile.ReadIcns(path);
        Assert.Equal(IconFile.IcnsTypes.Select(entry => entry.Type), entries.Select(entry => entry.Type));
        for (var index = 0; index < entries.Count; index++)
        {
            var source = IconFile.IconSetFile(IconFile.IcnsFiles[index]);
            Assert.True(
                entries[index].Data.AsSpan().SequenceEqual(File.ReadAllBytes(IconFile.RepositoryPath(source))),
                $"The .icns's {entries[index].Type} is not {source}, byte for byte.");
            var (width, _, _, _) = PngHeader.Read(entries[index].Data);
            Assert.Equal(IconFile.IcnsTypes[index].Size, width);
        }
    }

    [Fact]
    public async Task Apple_s_iconutil_turns_the_iconset_into_an_icns_and_reads_the_committed_icns_back()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "iconutil exists only on macOS; CI's macOS job runs this (D447).");
        var ct = TestContext.Current.CancellationToken;

        using var made = new TempDirectory("iconutil-icns");
        var icns = made.Combine("VisualCommit.icns");
        await IconUtilAsync(ct, "-c", "icns", IconFile.RepositoryPath(IconFile.IconSetFolder), "-o", icns);
        var start = new byte[4];
        await using (var file = File.OpenRead(icns))
        {
            await file.ReadExactlyAsync(start, ct);
        }

        Assert.Equal("icns", Encoding.ASCII.GetString(start));

        using var read = new TempDirectory("iconutil-iconset");
        var iconset = read.Combine("VisualCommit.iconset");
        await IconUtilAsync(ct, "-c", "iconset", IconFile.RepositoryPath(IconFile.IcnsFile), "-o", iconset);
        Assert.Equal(
            IconFile.IconSetNames.Select(file => file.Name + ".png").Order(StringComparer.Ordinal),
            Directory.EnumerateFileSystemEntries(iconset).Select(entry => Path.GetFileName(entry)).Order(StringComparer.Ordinal));
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private static string MasterPath(string name) => IconFile.RepositoryPath(IconFile.MastersFolder + "/" + name);

    /// <summary>A master's hash (7.3): the lower-case hex SHA-256 of its UTF-8 text with <c>\r\n</c> read as <c>\n</c>.</summary>
    private static string HashOf(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal))));

    /// <summary>Every generated file, relative to the repository, with its sizes in order, as the manifest lists them (7.3).</summary>
    private static Dictionary<string, List<int>> ExpectedFiles()
    {
        var files = new Dictionary<string, List<int>>(StringComparer.Ordinal) { [IconFile.IcoFile] = [.. IconFile.IcoSizes] };
        foreach (var size in IconFile.AppImageSizes)
        {
            files[IconFile.AppImageFile(size)] = [size];
        }

        foreach (var size in IconFile.PngSizes)
        {
            files[IconFile.PngFile(size)] = [size];
        }

        foreach (var (name, size) in IconFile.IconSetNames)
        {
            files[IconFile.IconSetFile(name)] = [size];
        }

        files[IconFile.IcnsFile] = [.. IconFile.IcnsTypes.Select(entry => entry.Size)];
        return files;
    }

    /// <summary>A frame that is drawn and is no full square (7.4 item 3): at least a quarter of its pixels opaque, at least one transparent.</summary>
    private static void AssertDrawnAndNotASquare(byte[] bgra, int size, string what)
    {
        Assert.Equal(size * size * 4, bgra.Length);
        var opaque = 0;
        var clear = 0;
        for (var at = 3; at < bgra.Length; at += 4)
        {
            opaque += bgra[at] == 255 ? 1 : 0;
            clear += bgra[at] == 0 ? 1 : 0;
        }

        Assert.True(opaque * 4 >= size * size, $"{what} has {opaque} opaque pixels of {size * size}: fewer than a quarter.");
        Assert.True(clear >= 1, $"{what} has no transparent pixel: a full square.");
    }

    private static double Number(XElement element, string attribute) =>
        element.Attribute(attribute) is { } value ? double.Parse(value.Value, CultureInfo.InvariantCulture) : 0;

    private static bool IsWhole(double value) => Math.Abs(value - Math.Round(value)) < 1e-9;

    /// <summary>
    /// The horizontal and vertical segments of a path whose line is not on a whole unit: a
    /// horizontal one's y, a vertical one's x. Curves and slanted lines are no straight edge of the
    /// grid; a segment of no length is none either.
    /// </summary>
    private static IEnumerable<string> StraightEdgesOffTheGrid(string data)
    {
        var tokens = PathToken().Matches(data).Select(match => match.Value).ToList();
        var (x, y, startX, startY) = (0.0, 0.0, 0.0, 0.0);
        var command = 'M';
        var at = 0;
        var problems = new List<string>();

        void Edge(double toX, double toY)
        {
            if (toX == x && toY != y && !IsWhole(x))
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"a vertical edge at x {x}"));
            }
            else if (toY == y && toX != x && !IsWhole(y))
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"a horizontal edge at y {y}"));
            }

            (x, y) = (toX, toY);
        }

        double Next() => double.Parse(tokens[at++], CultureInfo.InvariantCulture);

        while (at < tokens.Count)
        {
            if (char.IsLetter(tokens[at][0]))
            {
                command = tokens[at++][0];
                if (command is 'Z' or 'z')
                {
                    Edge(startX, startY);
                    continue;
                }
            }

            var relative = char.IsLower(command);
            var (baseX, baseY) = relative ? (x, y) : (0.0, 0.0);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    (x, y) = (baseX + Next(), baseY + Next());
                    (startX, startY) = (x, y);
                    command = relative ? 'l' : 'L';
                    break;
                case 'L':
                case 'T':
                    var (lineX, lineY) = (baseX + Next(), baseY + Next());
                    if (char.ToUpperInvariant(command) == 'L')
                    {
                        Edge(lineX, lineY);
                    }
                    else
                    {
                        (x, y) = (lineX, lineY);
                    }

                    break;
                case 'H':
                    Edge((relative ? x : 0) + Next(), y);
                    break;
                case 'V':
                    Edge(x, (relative ? y : 0) + Next());
                    break;
                case 'C':
                    at += 4;
                    (x, y) = (baseX + Next(), baseY + Next());
                    break;
                case 'S':
                case 'Q':
                    at += 2;
                    (x, y) = (baseX + Next(), baseY + Next());
                    break;
                case 'A':
                    at += 5;
                    (x, y) = (baseX + Next(), baseY + Next());
                    break;
                default:
                    throw new InvalidDataException($"The path command {command} is not SVG's.");
            }
        }

        return problems;
    }

    /// <summary>Runs <c>/usr/bin/iconutil</c> and asserts that it exits with 0, its standard error in the message otherwise.</summary>
    private static async Task IconUtilAsync(CancellationToken ct, params string[] arguments)
    {
        var start = new ProcessStartInfo("/usr/bin/iconutil") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync(ct);
        var output = process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        Assert.True(process.ExitCode == 0, $"iconutil {string.Join(' ', arguments)} exited with {process.ExitCode}:\n{await error}{await output}");
    }

    /// <summary>The first comment of a master after the owner's choice (7.3).</summary>
    [GeneratedRegex(@"^ VisualCommit's mark, chosen by the owner on \d{4}-\d{2}-\d{2} \(D407\)\. $", RegexOptions.CultureInvariant)]
    private static partial Regex ChosenComment();

    /// <summary>A <c>url(…)</c> in an attribute's value, its target in group 1.</summary>
    [GeneratedRegex(@"url\(\s*['""]?([^'"")]*)['""]?\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex UrlReference();

    /// <summary>A path's command letter or number.</summary>
    [GeneratedRegex(@"[MmLlHhVvCcSsQqTtAaZz]|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex PathToken();
}
