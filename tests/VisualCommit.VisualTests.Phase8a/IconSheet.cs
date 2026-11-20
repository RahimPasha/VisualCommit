using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using VisualCommit.Testing;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// Part B's sheet of the icon's files (docs/phase-8a-design.md 7.5; the report's "Conventions of
/// part B"), a harness control, not the app's: row 1, headed <c>VisualCommit.ico</c>, the
/// <c>.ico</c>'s frames in the file's order with each one's size under it, and row 2, Linux's PNGs
/// of 16 to 128 and the <c>.iconset</c>'s 16, 32 and 128. Every image at its own pixel size
/// (<see cref="Stretch.None"/>, whole-pixel places, no interpolation), left 24, gap 12, bottoms on
/// one line. A size is a bare number at 11 in SemiBold: at the regular weight the digits 2, 0 and 3,
/// which have no straight stem, reach their full ink in no pixel on white, so that <c>TextInk</c>
/// could not read the token from "20" or "32". It reads the repository's files through
/// <see cref="IconFile"/>: a bitmap frame from <see cref="IconFrame.Bgra"/>, a PNG through Avalonia's
/// own decoder. Its texts and layout are its own, not <c>Phase8aExpectations.B</c>'s, so that the
/// checks compare two copies. The colours are the app's tokens, resolved in the window's theme.
/// </summary>
internal sealed class IconSheet : Canvas, IDisposable
{
    private const string HeadingText = "VisualCommit.ico";
    private const int RowLeft = 24;
    private const int RowGap = 12;
    private const int HeadingAt = 24;
    private const int Row1Line = 312;
    private const int SizesAt = 316;
    private const int Row2Line = 488;

    private readonly List<Bitmap> _bitmaps = [];
    private readonly List<Image> _row1 = [];
    private readonly List<TextBlock> _row1Sizes = [];
    private readonly List<Image> _row2 = [];

    /// <summary>Each size's column under row 1: its image's left and width.</summary>
    private readonly Dictionary<Control, (int Left, int Width)> _sizeColumns = [];

    public IconSheet()
    {
        Heading = new TextBlock { Text = HeadingText, FontSize = 13 };
        Heading.Bind(TextBlock.ForegroundProperty, Heading.GetResourceObservable("VcTextPrimaryBrush"));
        Place(Heading, RowLeft, HeadingAt);

        var left = RowLeft;
        foreach (var frame in IconFile.Read(IconFile.RepositoryPath(IconFile.IcoFile)))
        {
            _row1.Add(AddImage(frame.IsPng ? FromPng(frame.Data) : FromBgra(frame.Size, frame.Bgra()), left, Row1Line));
            var size = new TextBlock { Text = frame.Size.ToString(CultureInfo.InvariantCulture), FontSize = 11, FontWeight = FontWeight.SemiBold };
            size.Bind(TextBlock.ForegroundProperty, size.GetResourceObservable("VcTextSecondaryBrush"));
            Place(size, left, SizesAt);
            _sizeColumns[size] = (left, frame.Size);
            _row1Sizes.Add(size);
            left += frame.Size + RowGap;
        }

        left = RowLeft;
        foreach (var file in Row2Files)
        {
            var bitmap = FromPng(File.ReadAllBytes(IconFile.RepositoryPath(file)));
            _row2.Add(AddImage(bitmap, left, Row2Line));
            left += bitmap.PixelSize.Width + RowGap;
        }
    }

    /// <summary>Row 2's files, relative to the repository, left to right.</summary>
    public static IReadOnlyList<string> Row2Files { get; } =
    [
        IconFile.PngFile(16), IconFile.PngFile(24), IconFile.PngFile(32), IconFile.PngFile(48), IconFile.PngFile(64), IconFile.PngFile(128),
        IconFile.IconSetFile("icon_16x16"), IconFile.IconSetFile("icon_32x32"), IconFile.IconSetFile("icon_128x128"),
    ];

    /// <summary>Row 1's heading.</summary>
    public TextBlock Heading { get; }

    /// <summary>Row 1's images, the <c>.ico</c>'s frames in the file's order.</summary>
    public IReadOnlyList<Image> Row1 => _row1;

    /// <summary>The size under each of row 1's images, in the same order.</summary>
    public IReadOnlyList<TextBlock> Row1Sizes => _row1Sizes;

    /// <summary>Row 2's images, in <see cref="Row2Files"/>' order.</summary>
    public IReadOnlyList<Image> Row2 => _row2;

    /// <summary>Every image of both rows.</summary>
    public IEnumerable<Image> Images => _row1.Concat(_row2);

    /// <summary>
    /// Lays every child out at its <c>Canvas.Left</c> and <c>Canvas.Top</c>, but a size under row 1,
    /// which is centred under its image on a whole pixel: text centred inside a wider box starts at a
    /// fraction of a pixel, and its ink then depends on that fraction, where the app's text, which
    /// <c>TextInk</c>'s tolerance was tuned on, starts on whole pixels.
    /// </summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var left = GetLeft(child);
            if (_sizeColumns.TryGetValue(child, out var column))
            {
                left = column.Left + Math.Floor((column.Width - child.DesiredSize.Width) / 2);
            }

            child.Arrange(new Rect(new Point(left, GetTop(child)), child.DesiredSize));
        }

        return finalSize;
    }

    public void Dispose()
    {
        foreach (var bitmap in _bitmaps)
        {
            bitmap.Dispose();
        }

        _bitmaps.Clear();
    }

    /// <summary>An image of <paramref name="bitmap"/> at its own size, its bottom on <paramref name="bottomLine"/>.</summary>
    private Image AddImage(Bitmap bitmap, int left, int bottomLine)
    {
        _bitmaps.Add(bitmap);
        var image = new Image { Source = bitmap, Stretch = Stretch.None };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
        Place(image, left, bottomLine - bitmap.PixelSize.Height);
        return image;
    }

    private void Place(Control control, int left, int top)
    {
        SetLeft(control, left);
        SetTop(control, top);
        Children.Add(control);
    }

    private static Bitmap FromPng(byte[] png)
    {
        using var stream = new MemoryStream(png);
        return new Bitmap(stream);
    }

    /// <summary>A bitmap of an icon frame's pixels: BGRA, top-down, unpremultiplied, at 96 DPI, so that a pixel is a pixel at scale 1.</summary>
    private static WriteableBitmap FromBgra(int size, byte[] bgra)
    {
        var bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var buffer = bitmap.Lock())
        {
            for (var row = 0; row < size; row++)
            {
                Marshal.Copy(bgra, row * size * 4, buffer.Address + (row * buffer.RowBytes), size * 4);
            }
        }

        return bitmap;
    }
}
