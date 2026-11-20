using System.Buffers.Binary;
using System.Text;

namespace VisualCommit.IconBuilder;

/// <summary>
/// Writes macOS's <c>.icns</c> (D447) from the <c>.iconset</c>'s PNGs, with no second render:
/// big-endian, the magic <c>icns</c> and the file's length, then per entry its type (four ASCII
/// characters), its length with its 8-byte header, and the very bytes of its PNG.
/// </summary>
internal static class IcnsWriter
{
    private const int HeaderSize = 8;

    /// <summary>The <c>.icns</c> that holds <paramref name="entries"/> in their order.</summary>
    public static byte[] Write(IReadOnlyList<(string Type, byte[] Png)> entries)
    {
        var length = HeaderSize + entries.Sum(entry => HeaderSize + entry.Png.Length);
        var file = new byte[length];
        var span = file.AsSpan();
        Encoding.ASCII.GetBytes("icns", span);
        BinaryPrimitives.WriteUInt32BigEndian(span[4..], (uint)length);

        var at = HeaderSize;
        foreach (var (type, png) in entries)
        {
            if (type.Length != 4 || !Ascii.IsValid(type))
            {
                throw new ArgumentException($"An .icns entry's type is four ASCII characters, not \"{type}\".", nameof(entries));
            }

            Encoding.ASCII.GetBytes(type, span[at..]);
            BinaryPrimitives.WriteUInt32BigEndian(span[(at + 4)..], (uint)(HeaderSize + png.Length));
            png.CopyTo(span[(at + HeaderSize)..]);
            at += HeaderSize + png.Length;
        }

        return file;
    }
}
