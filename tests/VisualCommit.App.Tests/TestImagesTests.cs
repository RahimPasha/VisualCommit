using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>The images that scenario repos commit (<see cref="TestImages"/>) are PNGs that Avalonia, and so the app, can decode.</summary>
public class TestImagesTests
{
    [AvaloniaFact]
    public void A_square_on_a_background_decodes_to_its_size_and_colours()
    {
        var png = TestImages.SquareOn(64, 48, TestImages.Rgb.Parse("#1E8E5A"), new TestImages.Rgb(255, 255, 255), 16);

        using var bitmap = new Bitmap(new MemoryStream(png));

        Assert.Equal(new PixelSize(64, 48), bitmap.PixelSize);
        Assert.Equal((0x1E, 0x8E, 0x5A), PixelAt(bitmap, 2, 2));
        Assert.Equal((0x1E, 0x8E, 0x5A), PixelAt(bitmap, 23, 15));
        Assert.Equal((255, 255, 255), PixelAt(bitmap, 24, 16));
        Assert.Equal((255, 255, 255), PixelAt(bitmap, 39, 31));
        Assert.Equal((0x1E, 0x8E, 0x5A), PixelAt(bitmap, 40, 32));
    }

    [Fact]
    public void The_same_pixels_give_the_same_bytes()
    {
        var first = TestImages.SquareOn(48, 48, TestImages.Rgb.Parse("#4353D8"), new TestImages.Rgb(255, 255, 255), 16);
        var second = TestImages.SquareOn(48, 48, TestImages.Rgb.Parse("#4353D8"), new TestImages.Rgb(255, 255, 255), 16);

        Assert.Equal(first, second);
    }

    [AvaloniaFact]
    public void An_image_larger_than_one_stored_block_decodes_whole()
    {
        // 200 × 120 pixels of RGB plus a filter byte per row is 72,120 bytes: two stored blocks.
        var png = TestImages.Png(200, 120, (x, y) => new TestImages.Rgb((byte)x, (byte)y, 7));

        using var bitmap = new Bitmap(new MemoryStream(png));

        Assert.Equal(new PixelSize(200, 120), bitmap.PixelSize);
        Assert.Equal((0, 0, 7), PixelAt(bitmap, 0, 0));
        Assert.Equal((199, 119, 7), PixelAt(bitmap, 199, 119));
    }

    private static (int R, int G, int B) PixelAt(Bitmap bitmap, int x, int y)
    {
        var buffer = new byte[4];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(x, y, 1, 1), handle.AddrOfPinnedObject(), buffer.Length, 4);
        }
        finally
        {
            handle.Free();
        }

        // Skia decodes to the platform's preferred order: BGRA on Windows and Linux, RGBA on macOS.
        return bitmap.Format == Avalonia.Platform.PixelFormats.Rgba8888
            ? (buffer[0], buffer[1], buffer[2])
            : (buffer[2], buffer[1], buffer[0]);
    }
}
