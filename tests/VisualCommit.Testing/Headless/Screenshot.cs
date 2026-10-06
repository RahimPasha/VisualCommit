using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace VisualCommit.Testing.Headless;

/// <summary>A captured frame of a window: it can be saved as a PNG and its pixels can be read.</summary>
public sealed class Screenshot
{
    private readonly WriteableBitmap _bitmap;

    public Screenshot(WriteableBitmap bitmap)
    {
        _bitmap = bitmap;
    }

    public int Width => _bitmap.PixelSize.Width;

    public int Height => _bitmap.PixelSize.Height;

    /// <summary>Saves the screenshot as a PNG, creating the folder if needed. Returns the full path.</summary>
    public string Save(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _bitmap.Save(fullPath, PngBitmapEncoderOptions.Default);
        return fullPath;
    }

    /// <summary>The colour of the pixel at a position given in pixels from the top-left corner.</summary>
    public Color PixelAt(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);

        using var buffer = _bitmap.Lock();
        var pixel = new byte[4];
        Marshal.Copy(buffer.Address + (y * buffer.RowBytes) + (x * 4), pixel, 0, 4);

        if (buffer.Format == PixelFormat.Bgra8888)
        {
            return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
        }

        if (buffer.Format == PixelFormat.Rgba8888)
        {
            return Color.FromArgb(pixel[3], pixel[0], pixel[1], pixel[2]);
        }

        throw new NotSupportedException($"Screenshots in the pixel format {buffer.Format} cannot be read.");
    }

    /// <summary>The colour at a window position in logical pixels. Screenshots are taken at a scaling of 1.</summary>
    public Color PixelAt(Point position) => PixelAt((int)position.X, (int)position.Y);
}
