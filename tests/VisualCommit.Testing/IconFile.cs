using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using VisualCommit.Testing.Headless;

namespace VisualCommit.Testing;

/// <summary>
/// The icon's files as the tests read them (docs/phase-8a-design.md 1.15, 7.3; D429, D430, D447):
/// the lists of what the icon's builder writes, the files' places in the repository, and readers of
/// an <c>.ico</c>, an <c>.icns</c> and a PNG. Plain .NET, no Avalonia and no renderer, so that the
/// real-window pass reads the files the same way. The lists are the tests' own copy of the builder's
/// (<c>tools/VisualCommit.IconBuilder/IconOutputs.cs</c>), written from the design, not read from it.
/// </summary>
public static class IconFile
{
    /// <summary>The masters' folder.</summary>
    public const string MastersFolder = "assets/brand";

    /// <summary>The app's three icon files, which the build embeds.</summary>
    public const string AppIconFolder = "src/VisualCommit.App/Assets/Icon";

    /// <summary>The <c>.ico</c>: the window's and the executable's icon.</summary>
    public const string IcoFile = AppIconFolder + "/VisualCommit.ico";

    /// <summary>The generated files that are no resource of the app.</summary>
    public const string GeneratedFolder = MastersFolder + "/generated";

    /// <summary>Linux's PNGs.</summary>
    public const string PngFolder = GeneratedFolder + "/png";

    /// <summary>macOS's <c>.iconset</c>, the <c>.icns</c>'s images.</summary>
    public const string IconSetFolder = GeneratedFolder + "/VisualCommit.iconset";

    /// <summary>macOS's <c>.icns</c> (D447).</summary>
    public const string IcnsFile = GeneratedFolder + "/VisualCommit.icns";

    /// <summary>The masters' hashes and every file's sizes.</summary>
    public const string ManifestFile = GeneratedFolder + "/manifest.json";

    /// <summary>The three masters, in <see cref="MastersFolder"/>.</summary>
    public static IReadOnlyList<string> Masters { get; } = ["icon.svg", "icon-small.svg", "icon-macos.svg"];

    /// <summary>The <c>.ico</c>'s sizes, ascending: <c>icon-small.svg</c>'s to 32, <c>icon.svg</c>'s above; the 256 a PNG, the others bitmaps.</summary>
    public static IReadOnlyList<int> IcoSizes { get; } = [16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256];

    /// <summary>The largest size the <c>.ico</c> draws from <c>icon-small.svg</c>.</summary>
    public const int SmallMasterUpTo = 32;

    /// <summary>The <c>.ico</c>'s one PNG entry; the others are bitmaps.</summary>
    public const int IcoPngSize = 256;

    /// <summary>Linux's PNGs, <c>png/icon-&lt;N&gt;.png</c>, drawn as the <c>.ico</c>'s are.</summary>
    public static IReadOnlyList<int> PngSizes { get; } = [16, 24, 32, 48, 64, 128, 256, 512];

    /// <summary>The app's two PNGs, <c>VisualCommit-&lt;N&gt;.png</c>, from <c>icon.svg</c>.</summary>
    public static IReadOnlyList<int> AppImageSizes { get; } = [48, 96];

    /// <summary>The <c>.iconset</c>'s ten files (Apple's names) and their sizes, from <c>icon-macos.svg</c>.</summary>
    public static IReadOnlyList<(string Name, int Size)> IconSetNames { get; } =
    [
        ("icon_16x16", 16),
        ("icon_16x16@2x", 32),
        ("icon_32x32", 32),
        ("icon_32x32@2x", 64),
        ("icon_128x128", 128),
        ("icon_128x128@2x", 256),
        ("icon_256x256", 256),
        ("icon_256x256@2x", 512),
        ("icon_512x512", 512),
        ("icon_512x512@2x", 1024),
    ];

    /// <summary>The <c>.icns</c>'s entries in the file's order: Apple's type and the image's size (D447).</summary>
    public static IReadOnlyList<(string Type, int Size)> IcnsTypes { get; } =
    [
        ("icp4", 16),
        ("icp5", 32),
        ("ic11", 32),
        ("ic12", 64),
        ("ic07", 128),
        ("ic13", 256),
        ("ic08", 256),
        ("ic14", 512),
        ("ic09", 512),
        ("ic10", 1024),
    ];

    /// <summary>The <c>.iconset</c> file whose bytes each entry of <see cref="IcnsTypes"/> holds, in the same order.</summary>
    public static IReadOnlyList<string> IcnsFiles { get; } =
    [
        "icon_16x16",
        "icon_32x32",
        "icon_16x16@2x",
        "icon_32x32@2x",
        "icon_128x128",
        "icon_128x128@2x",
        "icon_256x256",
        "icon_256x256@2x",
        "icon_512x512",
        "icon_512x512@2x",
    ];

    private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Linux's PNG of <paramref name="size"/> pixels, relative to the repository.</summary>
    public static string PngFile(int size) => $"{PngFolder}/icon-{size}.png";

    /// <summary>The app's PNG of <paramref name="size"/> pixels, relative to the repository.</summary>
    public static string AppImageFile(int size) => $"{AppIconFolder}/VisualCommit-{size}.png";

    /// <summary>The <c>.iconset</c>'s file named <paramref name="name"/>, relative to the repository.</summary>
    public static string IconSetFile(string name) => $"{IconSetFolder}/{name}.png";

    /// <summary>A path relative to the repository, with <c>/</c> between its parts, as a full path on this machine.</summary>
    public static string RepositoryPath(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);
        return Path.GetFullPath(Path.Combine(VisualRun.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
    }

    /// <summary>Whether <paramref name="data"/> starts with PNG's signature.</summary>
    public static bool IsPng(ReadOnlySpan<byte> data) => data.StartsWith(PngSignature);

    /// <summary>
    /// The frames of the <c>.ico</c> at <paramref name="path"/>, in the file's order. A frame's size
    /// is its directory entry's width, a width byte of 0 being 256.
    /// </summary>
    /// <exception cref="InvalidDataException">The file is no icon: its reserved field is not 0, its type not 1, or an entry lies outside it.</exception>
    public static IReadOnlyList<IconFrame> Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 6 || BinaryPrimitives.ReadUInt16LittleEndian(bytes) != 0 || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)) != 1)
        {
            throw new InvalidDataException($"{path} is not an icon: its header is not reserved 0, type 1.");
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4));
        var frames = new List<IconFrame>(count);
        for (var index = 0; index < count; index++)
        {
            var entry = 6 + (index * 16);
            if (entry + 16 > bytes.Length)
            {
                throw new InvalidDataException($"{path}: its directory entry {index} lies outside the file.");
            }

            var width = bytes[entry];
            var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(entry + 8));
            var offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(entry + 12));
            if (offset < 0 || length < 0 || (long)offset + length > bytes.Length)
            {
                throw new InvalidDataException($"{path}: the image of entry {index} lies outside the file.");
            }

            var data = bytes.AsSpan(offset, length).ToArray();
            frames.Add(new IconFrame(width == 0 ? 256 : width, IsPng(data), data));
        }

        return frames;
    }

    /// <summary>
    /// The entries of the <c>.icns</c> at <paramref name="path"/>, in the file's order: each one's
    /// type (four ASCII characters) and its data, without the eight bytes of its header.
    /// </summary>
    /// <exception cref="InvalidDataException">The file does not start with <c>icns</c>, its length field is not its length, or an entry runs past its end.</exception>
    public static IReadOnlyList<(string Type, byte[] Data)> ReadIcns(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 8 || Encoding.ASCII.GetString(bytes, 0, 4) != "icns")
        {
            throw new InvalidDataException($"{path} does not start with icns.");
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4));
        if (length != bytes.Length)
        {
            throw new InvalidDataException($"{path} says it is {length} bytes long and is {bytes.Length}.");
        }

        var entries = new List<(string, byte[])>();
        var at = 8;
        while (at < bytes.Length)
        {
            if (at + 8 > bytes.Length)
            {
                throw new InvalidDataException($"{path}: an entry's header at {at} runs past the file's end.");
            }

            var type = Encoding.ASCII.GetString(bytes, at, 4);
            var entryLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at + 4));
            if (entryLength < 8 || at + (long)entryLength > bytes.Length)
            {
                throw new InvalidDataException($"{path}: the entry {type} at {at} says it is {entryLength} bytes long, which runs past the file's end.");
            }

            entries.Add((type, bytes.AsSpan(at + 8, (int)entryLength - 8).ToArray()));
            at += (int)entryLength;
        }

        return entries;
    }

    /// <summary>
    /// A PNG's pixels as BGRA, 4 bytes a pixel, top-down, unpremultiplied, as PNG stores them. Reads
    /// what the icon's builder writes: 8 bits a channel, RGBA or RGB, not interlaced.
    /// </summary>
    /// <exception cref="InvalidDataException">The data is no PNG, or one of a form this reader does not take.</exception>
    public static (int Width, int Height, byte[] Bgra) DecodePng(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        var header = PngHeader.Read(png);
        if (header.BitDepth != 8 || header.ColourType is not (PngHeader.Rgba or PngHeader.Rgb) || png[28] != 0)
        {
            throw new InvalidDataException($"A PNG of bit depth {header.BitDepth}, colour type {header.ColourType} and interlace {png[28]}: this reader takes 8-bit RGBA or RGB, not interlaced.");
        }

        using var compressed = new MemoryStream();
        var at = 8;
        while (at + 12 <= png.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(at));
            var type = Encoding.ASCII.GetString(png, at + 4, 4);
            if (type == "IDAT")
            {
                compressed.Write(png, at + 8, length);
            }
            else if (type == "IEND")
            {
                break;
            }

            at += 12 + length;
        }

        var channels = header.ColourType == PngHeader.Rgba ? 4 : 3;
        var stride = header.Width * channels;
        var raw = new byte[(stride + 1) * header.Height];
        compressed.Position = 0;
        using (var inflater = new ZLibStream(compressed, CompressionMode.Decompress))
        {
            inflater.ReadExactly(raw);
        }

        var bgra = new byte[header.Width * header.Height * 4];
        var previous = new byte[stride];
        var current = new byte[stride];
        for (var y = 0; y < header.Height; y++)
        {
            var filter = raw[y * (stride + 1)];
            Array.Copy(raw, (y * (stride + 1)) + 1, current, 0, stride);
            Unfilter(filter, current, previous, channels);
            for (var x = 0; x < header.Width; x++)
            {
                var source = x * channels;
                var target = ((y * header.Width) + x) * 4;
                bgra[target] = current[source + 2];
                bgra[target + 1] = current[source + 1];
                bgra[target + 2] = current[source];
                bgra[target + 3] = channels == 4 ? current[source + 3] : (byte)255;
            }

            (previous, current) = (current, previous);
        }

        return (header.Width, header.Height, bgra);
    }

    /// <summary>Undoes one scanline's PNG filter in place.</summary>
    private static void Unfilter(byte filter, byte[] line, byte[] above, int bytesPerPixel)
    {
        for (var i = 0; i < line.Length; i++)
        {
            int left = i >= bytesPerPixel ? line[i - bytesPerPixel] : 0;
            int up = above[i];
            int upLeft = i >= bytesPerPixel ? above[i - bytesPerPixel] : 0;
            line[i] = filter switch
            {
                0 => line[i],
                1 => (byte)(line[i] + left),
                2 => (byte)(line[i] + up),
                3 => (byte)(line[i] + ((left + up) / 2)),
                4 => (byte)(line[i] + Paeth(left, up, upLeft)),
                _ => throw new InvalidDataException($"A PNG scanline has the filter {filter}, which PNG does not define."),
            };
        }
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var toLeft = Math.Abs(estimate - left);
        var toUp = Math.Abs(estimate - up);
        var toUpLeft = Math.Abs(estimate - upLeft);
        return toLeft <= toUp && toLeft <= toUpLeft ? left : toUp <= toUpLeft ? up : upLeft;
    }
}

/// <summary>
/// One image of an <c>.ico</c>: its size, whether it is a PNG, and its bytes as the file holds them
/// (a 40-byte <c>BITMAPINFOHEADER</c> with its pixels and mask, or a whole PNG).
/// </summary>
/// <param name="Size">The image's width and height in pixels.</param>
/// <param name="IsPng">The entry holds a PNG; otherwise a 32-bit bitmap.</param>
/// <param name="Data">The entry's bytes.</param>
public sealed record IconFrame(int Size, bool IsPng, byte[] Data)
{
    /// <summary>
    /// The frame's pixels as BGRA, 4 bytes a pixel, top-down and unpremultiplied (as an icon's
    /// bitmap stores them, bottom-up there): a 32-bit bitmap's rows turned the right way up, or a
    /// PNG entry decoded.
    /// </summary>
    /// <exception cref="InvalidDataException">A bitmap entry that is not 32 bits a pixel and uncompressed.</exception>
    public byte[] Bgra()
    {
        if (IsPng)
        {
            return IconFile.DecodePng(Data).Bgra;
        }

        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(Data);
        var bitCount = BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(14));
        var compression = BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(16));
        if (bitCount != 32 || compression != 0)
        {
            throw new InvalidDataException($"The {Size} px frame has {bitCount} bits a pixel and compression {compression}: only 32-bit BI_RGB bitmaps are read.");
        }

        var stride = Size * 4;
        var pixels = new byte[stride * Size];
        for (var row = 0; row < Size; row++)
        {
            Array.Copy(Data, headerSize + (row * stride), pixels, (Size - 1 - row) * stride, stride);
        }

        return pixels;
    }
}

/// <summary>A PNG's header: its size, its bits a channel and its colour type (6 RGBA, 2 RGB).</summary>
public static class PngHeader
{
    /// <summary>PNG's colour type of red, green, blue and alpha.</summary>
    public const int Rgba = 6;

    /// <summary>PNG's colour type of red, green and blue.</summary>
    public const int Rgb = 2;

    /// <summary>The header of the PNG at <paramref name="path"/>.</summary>
    /// <exception cref="InvalidDataException">The file does not start as a PNG does.</exception>
    public static (int Width, int Height, int BitDepth, int ColourType) Read(string path)
    {
        var start = new byte[29];
        using (var file = File.OpenRead(path))
        {
            file.ReadAtLeast(start, start.Length, throwOnEndOfStream: false);
        }

        return Read(start);
    }

    /// <summary>The header of the PNG whose bytes start with <paramref name="png"/>.</summary>
    /// <exception cref="InvalidDataException">The bytes do not start as a PNG does: the signature, then the IHDR chunk.</exception>
    public static (int Width, int Height, int BitDepth, int ColourType) Read(ReadOnlySpan<byte> png)
    {
        if (png.Length < 26 || !IconFile.IsPng(png) || Encoding.ASCII.GetString(png.Slice(12, 4)) != "IHDR")
        {
            throw new InvalidDataException("The data is no PNG: it does not start with PNG's signature and an IHDR chunk.");
        }

        return (
            (int)BinaryPrimitives.ReadUInt32BigEndian(png[16..]),
            (int)BinaryPrimitives.ReadUInt32BigEndian(png[20..]),
            png[24],
            png[25]);
    }
}
