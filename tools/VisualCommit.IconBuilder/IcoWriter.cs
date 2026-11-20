using System.Buffers.Binary;

namespace VisualCommit.IconBuilder;

/// <summary>One image of an <c>.ico</c>: its size and its bytes, a bitmap made by <see cref="IcoWriter.Bitmap"/> or a whole PNG.</summary>
/// <param name="Size">Its width and height in pixels, at most 256.</param>
/// <param name="Data">The entry's bytes as the file holds them.</param>
internal sealed record IcoImage(int Size, byte[] Data);

/// <summary>
/// Writes a Windows <c>.ico</c> (D429): the 6-byte header (reserved 0, type 1, the count), a 16-byte
/// directory entry per image (a width and height of 256 written as 0, one plane, 32 bits), then the
/// images in the directory's order.
/// </summary>
internal static class IcoWriter
{
    private const int HeaderSize = 6;
    private const int EntrySize = 16;
    private const int BitmapInfoHeaderSize = 40;

    /// <summary>The <c>.ico</c> that holds <paramref name="images"/> in their order.</summary>
    public static byte[] Write(IReadOnlyList<IcoImage> images)
    {
        var length = HeaderSize + (EntrySize * images.Count) + images.Sum(image => image.Data.Length);
        var file = new byte[length];
        var span = file.AsSpan();
        BinaryPrimitives.WriteUInt16LittleEndian(span[2..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], (ushort)images.Count);

        var offset = HeaderSize + (EntrySize * images.Count);
        for (var index = 0; index < images.Count; index++)
        {
            var image = images[index];
            if (image.Size is < 1 or > 256)
            {
                throw new ArgumentException($"An icon's image is 1 to 256 pixels, not {image.Size}.", nameof(images));
            }

            var entry = span.Slice(HeaderSize + (EntrySize * index), EntrySize);
            entry[0] = (byte)(image.Size == 256 ? 0 : image.Size);
            entry[1] = entry[0];
            BinaryPrimitives.WriteUInt16LittleEndian(entry[4..], 1);
            BinaryPrimitives.WriteUInt16LittleEndian(entry[6..], 32);
            BinaryPrimitives.WriteInt32LittleEndian(entry[8..], image.Data.Length);
            BinaryPrimitives.WriteInt32LittleEndian(entry[12..], offset);
            image.Data.CopyTo(span[offset..]);
            offset += image.Data.Length;
        }

        return file;
    }

    /// <summary>
    /// An icon's 32-bit bitmap of <paramref name="bgra"/> (top-down, unpremultiplied): a 40-byte
    /// <c>BITMAPINFOHEADER</c> with the height doubled for the mask, the rows bottom-up, then an AND
    /// mask of zeros, which the alpha channel makes unused.
    /// </summary>
    public static byte[] Bitmap(int size, byte[] bgra)
    {
        var stride = size * 4;
        if (bgra.Length != stride * size)
        {
            throw new ArgumentException($"{size} px needs {stride * size} bytes of BGRA, not {bgra.Length}.", nameof(bgra));
        }

        var maskStride = (size + 31) / 32 * 4;
        var data = new byte[BitmapInfoHeaderSize + (stride * size) + (maskStride * size)];
        var span = data.AsSpan();
        BinaryPrimitives.WriteInt32LittleEndian(span, BitmapInfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], size);
        BinaryPrimitives.WriteInt32LittleEndian(span[8..], size * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(span[12..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[14..], 32);

        // Compression 0 (BI_RGB); the image's size, the resolutions and the colour counts 0.
        BinaryPrimitives.WriteInt32LittleEndian(span[20..], (stride * size) + (maskStride * size));
        for (var row = 0; row < size; row++)
        {
            bgra.AsSpan(row * stride, stride).CopyTo(span[(BitmapInfoHeaderSize + ((size - 1 - row) * stride))..]);
        }

        return data;
    }
}
