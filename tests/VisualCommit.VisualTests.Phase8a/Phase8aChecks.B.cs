using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// Part B, the icon (docs/test-reports/phase-8a.md, checks B1 and B2; design 7.5): the sheet of the
/// icon's files (<see cref="IconSheet"/>) in a headless window of 1100×700 on the window's
/// background, dark and light. Every image at its own pixel size, in its row's place, its centre its
/// file's centre pixel composited over the background, its top-left pixel and the gaps the
/// background. No session, scenario, git or data folder; no pointer moves and no key is pressed. The
/// real window's steps for part B (B1 (a) to (c), B2) are in launch 3, shared with part A, and have
/// no scripted twin.
/// </summary>
public sealed partial class Phase8aChecksB : Phase8aChecks
{
    private static readonly TimeSpan WaitLimit = ShellDriver.DefaultWaitTimeout;
    private static readonly Lock TimingGate = new();
    private static readonly HashSet<string> TimingsStarted = [];

    /// <summary>B1: the sheet, dark.</summary>
    [AvaloniaFact]
    public Task Check_B1_icon_sizes_dark() =>
        CheckSheetAsync(new Look(ThemeVariant.Dark, B.BackgroundDark, B.SizeDark, B.HeadingDark), B.B1IconSizesDark);

    /// <summary>B2: the sheet, light.</summary>
    [AvaloniaFact]
    public Task Check_B2_icon_sizes_light() =>
        CheckSheetAsync(new Look(ThemeVariant.Light, B.BackgroundLight, B.SizeLight, B.HeadingLight), B.B2IconSizesLight);

    /// <summary>B1's and B2's steps: show the sheet and picture it (step 1), then close it (step 2).</summary>
    private static async Task CheckSheetAsync(Look look, string picture)
    {
        var clock = Stopwatch.StartNew();
        using var sheet = new IconSheet();
        var window = new Window
        {
            Width = DefaultSize.Width,
            Height = DefaultSize.Height,
            CanResize = false,
            RequestedThemeVariant = look.Theme,
            Content = sheet,
        };
        window.Bind(TemplatedControl.BackgroundProperty, window.GetResourceObservable("VcWindowBackgroundBrush"));
        var closed = false;
        window.Closed += (_, _) => closed = true;
        try
        {
            // Step 1: shown, each of the 23 images with its bitmap and a laid-out Bounds.
            window.Show();
            await WaitForAsync(
                () => window.IsVisible && sheet.Images.Count() == 23 && sheet.Images.All(image => image.Source is not null && image.Bounds.Width > 0 && image.Bounds.Height > 0),
                "the sheet's 23 images to be laid out");
            var frame = ShotSheet(window, picture, clock);
            AssertSheet(window, sheet, frame, look);

            // Step 2: closed.
            window.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.True(closed, "The sheet's window did not close.");
        }
        finally
        {
            if (!closed)
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }
    }

    /// <summary>What B1 and B2 expect after step 1, in <paramref name="look"/>'s theme and colours.</summary>
    private static void AssertSheet(Window window, IconSheet sheet, Screenshot frame, Look look)
    {
        Assert.Equal(DefaultSize, (frame.Width, frame.Height));
        Assert.Equal(look.Theme, window.ActualThemeVariant);

        // Row 1's heading, wholly above row 1's images.
        Assert.Equal(B.Heading, sheet.Heading.Text);
        Assert.Equal(B.HeadingFontSize, sheet.Heading.FontSize);
        Assert.Equal(look.Heading, (sheet.Heading.Foreground as ISolidColorBrush)?.Color);
        var heading = BoundsOf(sheet.Heading, window);
        var row1Top = sheet.Row1.Min(image => BoundsOf(image, window).Top);
        Assert.True(heading.Bottom <= row1Top, $"The heading at {heading} is not wholly above row 1's images, whose top is {row1Top}.");

        // Row 1: the .ico's 14 frames in IcoSizes' order, each with its size under it.
        var frames = IconFile.Read(IconFile.RepositoryPath(B.IcoFile));
        Assert.Equal(B.IcoSizes, frames.Select(file => file.Size));
        var row1 = AssertRow(window, sheet.Row1, B.IcoSizes, B.Row1Lefts, B.Row1Bottom, "row 1");
        Assert.Equal(B.Row1End, row1[^1].Right);
        Assert.Equal(B.IcoSizes.Count, sheet.Row1Sizes.Count);
        for (var index = 0; index < row1.Count; index++)
        {
            var size = sheet.Row1Sizes[index];
            var image = row1[index];
            var bounds = BoundsOf(size, window);
            Assert.Equal(B.IcoSizes[index].ToString(CultureInfo.InvariantCulture), size.Text);
            Assert.Equal(B.SizeFontSize, size.FontSize);
            Assert.Equal(B.SizeFontWeight, size.FontWeight);
            Assert.Equal(look.Size, (size.Foreground as ISolidColorBrush)?.Color);
            Assert.True(
                bounds.Top >= image.Bottom && bounds.Left >= image.Left && bounds.Right <= image.Right,
                $"The size {size.Text} at {bounds} is not under its image at {image}.");
            Assert.Empty(TextInk.Problems(frame, bounds, look.Size, look.Background, what: $"the size {size.Text}"));
        }

        // Row 2, wholly below row 1's sizes.
        Assert.Equal(B.Row2.Select(file => file.File), IconSheet.Row2Files);
        var row2 = AssertRow(window, sheet.Row2, [.. B.Row2.Select(file => file.Size)], B.Row2Lefts, B.Row2Bottom, "row 2");
        Assert.Equal(B.Row2End, row2[^1].Right);
        var sizesBottom = sheet.Row1Sizes.Max(size => BoundsOf(size, window).Bottom);
        Assert.True(row2.Min(image => image.Top) >= sizesBottom, $"Row 2 starts at {row2.Min(image => image.Top)}, above row 1's sizes' bottom, {sizesBottom}.");

        // Every image inside the frame; its centre its file's centre pixel over the background; its top-left pixel the background.
        var files = frames.Select(file => (file.Size, Pixels: file.Bgra()))
            .Concat(B.Row2.Select(file => (file.Size, Pixels: IconFile.DecodePng(File.ReadAllBytes(IconFile.RepositoryPath(file.File))).Bgra)))
            .ToList();
        var places = row1.Concat(row2).ToList();
        for (var index = 0; index < places.Count; index++)
        {
            var place = places[index];
            var (size, pixels) = files[index];
            Assert.True(place.Left >= 0 && place.Top >= 0 && place.Right <= frame.Width && place.Bottom <= frame.Height, $"The {size} px image at {place} is not wholly inside the frame.");

            var centre = Opaque(frame.PixelAt((int)place.Left + (size / 2), (int)place.Top + (size / 2)));
            var expected = Composite(pixels, size, size / 2, size / 2, look.Background);
            Assert.True(
                Distance(centre, expected) <= B.Tolerance,
                $"The {size} px image at {place} has the centre {Hex(centre)}, not its file's centre pixel over the background, {Hex(expected)} (within {B.Tolerance}).");

            var topLeft = Opaque(frame.PixelAt((int)place.Left, (int)place.Top));
            Assert.True(topLeft == look.Background, $"The {size} px image at {place} has the top-left pixel {Hex(topLeft)}, not the background {Hex(look.Background)}.");
        }

        // The gaps: the background from the taller image's top to the row's bottom line.
        AssertGaps(frame, row1, look.Background, "row 1");
        AssertGaps(frame, row2, look.Background, "row 2");

        var clipped = LayoutAudit.FindClippedText(window, allowShortenedWithToolTip: true, allowScrolledOutOfView: true);
        Assert.True(clipped.Count == 0, "Clipped text:\n" + string.Join("\n", clipped));
    }

    /// <summary>
    /// A row's images: each <see cref="Stretch.None"/>, its bitmap and its bounds exactly its size,
    /// on whole pixels, at its left, every bottom on <paramref name="bottomLine"/>. Returns their
    /// places in the window.
    /// </summary>
    private static List<Rect> AssertRow(Window window, IReadOnlyList<Image> images, IReadOnlyList<int> sizes, IReadOnlyList<int> lefts, int bottomLine, string row)
    {
        Assert.Equal(sizes.Count, images.Count);
        var places = new List<Rect>(images.Count);
        for (var index = 0; index < images.Count; index++)
        {
            var image = images[index];
            var size = sizes[index];
            var place = BoundsOf(image, window);
            Assert.Equal(Stretch.None, image.Stretch);
            Assert.Equal(new PixelSize(size, size), (image.Source as Bitmap)?.PixelSize);
            Assert.True(place.Width == size && place.Height == size, $"The {size} px image of {row} is {place.Width}×{place.Height}.");
            Assert.True(IsWhole(place.Left) && IsWhole(place.Top), $"The {size} px image of {row} is not on whole pixels: {place}.");
            Assert.True(place.Left == lefts[index], $"The {size} px image of {row} starts at x {place.Left}, not {lefts[index]}.");
            places.Add(place);
        }

        Assert.True(places.All(place => place.Bottom == bottomLine), $"The bottoms of {row} are not on one line at {bottomLine}: {string.Join(", ", places.Select(place => place.Bottom))}.");
        return places;
    }

    /// <summary>Every pixel between two neighbouring images of a row, across the gap and from the taller one's top to the row's bottom line, is the background.</summary>
    private static void AssertGaps(Screenshot frame, List<Rect> row, Color background, string what)
    {
        var wrong = new List<string>();
        for (var index = 0; index + 1 < row.Count; index++)
        {
            var (left, right) = (row[index], row[index + 1]);
            var top = (int)Math.Min(left.Top, right.Top);
            for (var x = (int)left.Right; x < (int)right.Left; x++)
            {
                for (var y = top; y < (int)left.Bottom; y++)
                {
                    var pixel = Opaque(frame.PixelAt(x, y));
                    if (pixel != background && wrong.Count < 10)
                    {
                        wrong.Add(string.Create(CultureInfo.InvariantCulture, $"({x}, {y}) {Hex(pixel)}"));
                    }
                }
            }
        }

        Assert.True(wrong.Count == 0, $"The gaps of {what} are not all {Hex(background)}: " + string.Join(", ", wrong));
    }

    /// <summary>A file's pixel (top-down, unpremultiplied BGRA) blended over <paramref name="background"/> by its alpha.</summary>
    private static Color Composite(byte[] bgra, int size, int x, int y, Color background)
    {
        var at = ((y * size) + x) * 4;
        var alpha = bgra[at + 3];
        byte Mix(byte channel, byte under) => (byte)Math.Round(((channel * alpha) + (under * (255 - alpha))) / 255.0);
        return Color.FromRgb(Mix(bgra[at + 2], background.R), Mix(bgra[at + 1], background.G), Mix(bgra[at], background.B));
    }

    private static Rect BoundsOf(Visual visual, Window window)
    {
        var topLeft = visual.TranslatePoint(default, window) ?? throw new InvalidOperationException("The control is not in the sheet's window.");
        return new Rect(topLeft, visual.Bounds.Size);
    }

    private static bool IsWhole(double value) => value == Math.Round(value);

    private static Color Opaque(Color colour) => Color.FromRgb(colour.R, colour.G, colour.B);

    private static int Distance(Color a, Color b) => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

    private static string Hex(Color colour) => string.Create(CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");

    /// <summary>Waits on the UI thread until <paramref name="condition"/> holds, letting it run what was posted.</summary>
    private static async Task WaitForAsync(Func<bool> condition, string what)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition())
            {
                return;
            }

            if (clock.Elapsed >= WaitLimit)
            {
                throw new TimeoutException(string.Create(CultureInfo.InvariantCulture, $"Timed out after {WaitLimit.TotalSeconds:0} s waiting for {what}."));
            }

            // No ConfigureAwait(false): the next look must run on the UI thread.
            await Task.Delay(15);
        }
    }

    /// <summary>
    /// Saves the sheet's frame (its own, 1100×700 at scale 1) as
    /// artifacts/visual/phase-8a/scripted/<paramref name="name"/>.png, as part I saves its notice:
    /// the name claimed once in the run, the step's time noted under artifacts/visual/phase-8a/timings/.
    /// </summary>
    private static Screenshot ShotSheet(Window window, string name, Stopwatch clock)
    {
        ClaimPictureName(name);
        Dispatcher.UIThread.RunJobs();
        var frame = new Screenshot(window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The sheet has not rendered a frame."));
        frame.Save(Phase, name);
        NoteTime(new StepTime(name, clock.Elapsed, clock.Elapsed));
        return frame;
    }

    private static void NoteTime(StepTime step)
    {
        var test = TestContext.Current.Test?.TestDisplayName ?? "unknown";
        var name = string.Concat(test.Select(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-' ? character : '_'));
        var file = Path.Combine(Path.GetDirectoryName(VisualRun.FolderFor(Phase))!, "timings", name + ".txt");
        lock (TimingGate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            if (TimingsStarted.Add(test))
            {
                File.WriteAllText(file, test + "\n");
            }

            File.AppendAllText(file, step + "\n");
        }
    }

    /// <summary>A sheet's theme and the colours it is read in.</summary>
    private sealed record Look(ThemeVariant Theme, Color Background, Color Size, Color Heading);
}
