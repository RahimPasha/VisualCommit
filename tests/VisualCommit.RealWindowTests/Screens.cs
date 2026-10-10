using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// The screenshot folders of the visual test gate, and the comparison of a real-window
/// screenshot with the scripted screenshot of the same step.
/// </summary>
public static class Screens
{
    private static readonly string RepoRoot =
        typeof(Screens).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(attribute => attribute.Key == "RepoRoot").Value!;

    /// <summary>Where the real-window pass of a phase keeps its screenshots: <c>artifacts/visual/phase-N/real-window/</c>.</summary>
    public static string RealWindowFolder(int phase) =>
        Path.Combine(RepoRoot, "artifacts", "visual", $"phase-{phase}", "real-window");

    /// <summary>The scripted walk-through's screenshot with this name, written by VisualCommit.VisualTests.</summary>
    public static string ScriptedFile(int phase, string name) =>
        Path.Combine(RepoRoot, "artifacts", "visual", $"phase-{phase}", "scripted", name + ".png");

    public static string Save(this Bitmap bitmap, int phase, string name)
    {
        var path = Path.Combine(RealWindowFolder(phase), name + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    /// <summary>
    /// The colour at a client-area position given in logical pixels, in a capture taken at
    /// <paramref name="scaling"/>.
    /// </summary>
    public static Color ColourAt(this Bitmap capture, double x, double y, double scaling) =>
        capture.GetPixel((int)(x * scaling), (int)(y * scaling));

    /// <summary>
    /// Compares a real-window capture with a scripted screenshot. The capture is first scaled to
    /// the scripted screenshot's size, which undoes the display scaling. A pixel of the scripted
    /// screenshot counts as different when no pixel of the scaled capture at the same place or
    /// next to it (1 pixel in any direction) has its colour, within <paramref name="tolerance"/>
    /// in every channel (D58): the edges of letters, drawn at another scaling, land a pixel off,
    /// while a wrong colour, a missing region or a moved layout does not match anywhere near.
    /// Returns the share of different pixels (0 to 1) by that rule, and by the plain rule of D33
    /// that compares the same place only, and saves a picture of where they are: the scripted
    /// screenshot faded, the different pixels in magenta and those that only the plain rule counts
    /// in pale pink.
    /// </summary>
    public static (double Different, double DifferentInPlace) DifferenceFrom(this Bitmap capture, string scriptedFile, string differencePicture, int tolerance = 48)
    {
        using var scripted = new Bitmap(scriptedFile);
        using var scaled = new Bitmap(scripted.Width, scripted.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(scaled))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(capture, new Rectangle(0, 0, scaled.Width, scaled.Height));
        }

        var expected = Pixels(scripted);
        var actual = Pixels(scaled);
        var picture = new byte[expected.Length];
        var width = scripted.Width;
        var height = scripted.Height;
        var differing = 0;
        var differingInPlace = 0;

        bool Matches(int i, int j) =>
            Math.Abs(expected[i] - actual[j]) <= tolerance
            && Math.Abs(expected[i + 1] - actual[j + 1]) <= tolerance
            && Math.Abs(expected[i + 2] - actual[j + 2]) <= tolerance;

        for (var i = 0; i < expected.Length; i += 4)
        {
            var inPlace = Matches(i, i);
            var nearby = inPlace;
            var x = (i / 4) % width;
            var y = (i / 4) / width;
            for (var dy = -1; dy <= 1 && !nearby; dy++)
            {
                for (var dx = -1; dx <= 1 && !nearby; dx++)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    nearby = nx >= 0 && ny >= 0 && nx < width && ny < height && Matches(i, ((ny * width) + nx) * 4);
                }
            }

            if (!inPlace)
            {
                differingInPlace++;
            }

            if (!nearby)
            {
                differing++;
                (picture[i], picture[i + 1], picture[i + 2], picture[i + 3]) = (255, 0, 255, 255);
            }
            else if (!inPlace)
            {
                (picture[i], picture[i + 1], picture[i + 2], picture[i + 3]) = (230, 190, 255, 255);
            }
            else
            {
                // Faded towards mid-grey, so the magenta stands out in both themes.
                picture[i] = (byte)((expected[i] + 128) / 2);
                picture[i + 1] = (byte)((expected[i + 1] + 128) / 2);
                picture[i + 2] = (byte)((expected[i + 2] + 128) / 2);
                picture[i + 3] = 255;
            }
        }

        using var output = new Bitmap(scripted.Width, scripted.Height, PixelFormat.Format32bppArgb);
        var area = output.LockBits(new Rectangle(0, 0, output.Width, output.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(picture, 0, area.Scan0, picture.Length);
        output.UnlockBits(area);
        Directory.CreateDirectory(Path.GetDirectoryName(differencePicture)!);
        output.Save(differencePicture, ImageFormat.Png);

        var total = expected.Length / 4.0;
        return (differing / total, differingInPlace / total);
    }

    /// <summary>The pixels of a bitmap as blue, green, red, alpha bytes, row by row without padding.</summary>
    private static byte[] Pixels(Bitmap bitmap)
    {
        using var copy = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), PixelFormat.Format32bppArgb);
        var area = copy.LockBits(new Rectangle(0, 0, copy.Width, copy.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[copy.Width * copy.Height * 4];
            Marshal.Copy(area.Scan0, pixels, 0, pixels.Length);
            return pixels;
        }
        finally
        {
            copy.UnlockBits(area);
        }
    }
}
