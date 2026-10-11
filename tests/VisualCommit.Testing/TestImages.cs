using System.Buffers.Binary;
using System.Text;

namespace VisualCommit.Testing;

/// <summary>
/// Makes small PNG images for scenario repos. The bytes depend only on the pixels: the image data
/// is stored in uncompressed deflate blocks, so no zlib version can change them, and an image
/// committed to a scenario repo has the same blob id on every machine.
/// </summary>
public static class TestImages
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>A colour as red, green and blue, each 0 to 255.</summary>
    public readonly record struct Rgb(byte R, byte G, byte B)
    {
        /// <summary>Reads <c>#RRGGBB</c>.</summary>
        public static Rgb Parse(string hex)
        {
            ArgumentNullException.ThrowIfNull(hex);
            var value = Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Rgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        }
    }

    /// <summary>A width × height RGB image filled with <paramref name="background"/>, with a square of <paramref name="square"/> <paramref name="squareSize"/> wide in its centre.</summary>
    public static byte[] SquareOn(int width, int height, Rgb background, Rgb square, int squareSize)
    {
        var left = (width - squareSize) / 2;
        var top = (height - squareSize) / 2;
        return Png(width, height, (x, y) =>
            x >= left && x < left + squareSize && y >= top && y < top + squareSize ? square : background);
    }

    /// <summary>A width × height RGB image (8 bits per channel, no alpha) whose pixel at (x, y) is <paramref name="pixel"/>(x, y).</summary>
    public static byte[] Png(int width, int height, Func<int, int, Rgb> pixel)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentNullException.ThrowIfNull(pixel);

        // Each scanline: filter type 0 (none), then the pixels.
        var stride = 1 + (width * 3);
        var raw = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var colour = pixel(x, y);
                var at = (y * stride) + 1 + (x * 3);
                raw[at] = colour.R;
                raw[at + 1] = colour.G;
                raw[at + 2] = colour.B;
            }
        }

        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)height);
        header[8] = 8; // bits per channel
        header[9] = 2; // colour type: RGB
        // Compression, filter and interlace methods are all 0.

        using var png = new MemoryStream();
        png.Write(Signature);
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", StoredZlib(raw));
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    /// <summary>A zlib stream that holds <paramref name="data"/> in stored (uncompressed) deflate blocks.</summary>
    private static byte[] StoredZlib(byte[] data)
    {
        const int MaxBlock = 65535;
        using var zlib = new MemoryStream();
        zlib.WriteByte(0x78); // deflate, 32 KB window
        zlib.WriteByte(0x01); // no preset dictionary, fastest; the check bits make 0x7801 a multiple of 31

        var offset = 0;
        do
        {
            var length = Math.Min(MaxBlock, data.Length - offset);
            var last = offset + length == data.Length;
            zlib.WriteByte(last ? (byte)1 : (byte)0); // BFINAL, BTYPE 00 (stored)
            zlib.WriteByte((byte)length);
            zlib.WriteByte((byte)(length >> 8));
            zlib.WriteByte((byte)~length);
            zlib.WriteByte((byte)(~length >> 8));
            zlib.Write(data, offset, length);
            offset += length;
        }
        while (offset < data.Length);

        Span<byte> adler = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(adler, Adler32(data));
        zlib.Write(adler);
        return zlib.ToArray();
    }

    private static void WriteChunk(Stream png, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(number, (uint)data.Length);
        png.Write(number);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        png.Write(typeBytes);
        png.Write(data);

        var crc = Crc32(Crc32(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(number, crc);
        png.Write(number);
    }

    private static uint Adler32(byte[] data)
    {
        const uint Modulus = 65521;
        uint a = 1, b = 0;
        foreach (var value in data)
        {
            a = (a + value) % Modulus;
            b = (b + a) % Modulus;
        }

        return (b << 16) | a;
    }

    private static uint Crc32(uint crc, byte[] data)
    {
        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
