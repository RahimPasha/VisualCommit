using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using VisualCommit.Core.Settings;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.GuideShots;

/// <summary>
/// Takes one guide picture in one theme (docs/phase-8a-design.md 11.3.5; D439, D440): a fresh copy
/// of the demo (<see cref="GuideScenario.FreshAsync"/>), a fresh start of the app on it through the
/// scripted walk-through's harness (<see cref="ShellDriver"/>), the theme chosen through the
/// session's own <c>UseTheme</c> and waited for before the shot's state is set up, so that no theme
/// switch closes a menu, and the window's render scaling set to <see cref="Scale"/> before the
/// pointer first moves; then the shot's state, a capture, a crop to the shot's frame times the
/// scale plus <see cref="Margin"/> pixels on each side, the <see cref="LeakGuard"/>'s look, and the
/// picture saved as
/// <see cref="GuideShotCatalogue.FileName"/> in <see cref="GuideSettings.PicturesFolder"/>.
/// </summary>
public static class GuideCamera
{
    /// <summary>The render scaling of every picture; 1 is ruling 61's fallback, under which the pages' <c>srcset</c>s go (D440).</summary>
    public const double Scale = 2;

    /// <summary>The pixels kept around a shot's frame on each side, within the window.</summary>
    public const int Margin = 8;

    /// <summary>Every shot's two cases.</summary>
    public static TheoryData<GuideTheme> Themes => new() { GuideTheme.Dark, GuideTheme.Light };

    /// <summary>
    /// Takes the picture <paramref name="name"/> in <paramref name="theme"/>: the app started as
    /// <paramref name="start"/> says, then <paramref name="setUp"/> brings it to the shot's state
    /// and says what to frame. Nothing is saved when a step or the leak guard fails.
    /// </summary>
    public static async Task ShootAsync(string name, GuideTheme theme, GuideStart start, Func<GuideRun, Task<GuideFrame>> setUp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(setUp);
        if (!GuideShotCatalogue.Names.Contains(name))
        {
            throw new ArgumentException($"{name} is no picture of the guide's catalogue.", nameof(name));
        }

        var scenario = await GuideScenario.FreshAsync();
        await using var run = await GuideRun.StartAsync(scenario, start);
        var app = run.App;
        app.Session.Shell.UseTheme(theme == GuideTheme.Dark ? AppTheme.Dark : AppTheme.Light);
        var variant = theme == GuideTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        await app.WaitForAsync(() => app.Window.ActualThemeVariant == variant, $"the window to show the {theme} theme");

        // The scaling before the pointer first moves: set later, it re-reads the pointer's last
        // place in the new scaling's pixels, half as far from the corner at 2, and hovers what lies
        // there (a reflog row did, under the neutral point's half).
        app.Window.SetRenderScaling(Scale);
        app.Settle();
        await run.WaitUntilShownAsync();

        var frame = await setUp(run);
        app.WaitForCommitDetails();
        app.Settle();

        // A frame first, so that what draws itself late (the graph, the diff's text) has done so.
        app.Capture();
        app.Settle();
        var picture = Capture(app, frame.BoundsIn(app));
        LeakGuard.Check(app.Window, name);
        Save(picture, Path.Combine(GuideSettings.PicturesFolder(), GuideShotCatalogue.FileName(name, theme)));
    }

    /// <summary>
    /// The window's frame at <see cref="Scale"/>, cut to <paramref name="bounds"/> (in the window's
    /// logical pixels) times the scale plus <see cref="Margin"/> pixels on each side, within the frame.
    /// </summary>
    private static WriteableBitmap Capture(ShellDriver app, Rect bounds)
    {
        using var frame = app.Window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("The window has not rendered a frame.");
        var expected = new PixelSize((int)Math.Round(app.Window.ClientSize.Width * Scale), (int)Math.Round(app.Window.ClientSize.Height * Scale));
        if (Math.Abs(frame.PixelSize.Width - expected.Width) > 1 || Math.Abs(frame.PixelSize.Height - expected.Height) > 1)
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"The frame is {frame.PixelSize.Width}x{frame.PixelSize.Height} pixels; at a scaling of {Scale} the {app.Window.ClientSize.Width}x{app.Window.ClientSize.Height} window gives {expected.Width}x{expected.Height}."));
        }

        var left = Math.Max(0, (int)Math.Floor(bounds.Left * Scale) - Margin);
        var top = Math.Max(0, (int)Math.Floor(bounds.Top * Scale) - Margin);
        var right = Math.Min(frame.PixelSize.Width, (int)Math.Ceiling(bounds.Right * Scale) + Margin);
        var bottom = Math.Min(frame.PixelSize.Height, (int)Math.Ceiling(bounds.Bottom * Scale) + Margin);
        if (right <= left || bottom <= top)
        {
            throw new InvalidOperationException($"The shot's frame {bounds} lies outside the window.");
        }

        using var source = frame.Lock();
        var cropped = new WriteableBitmap(new PixelSize(right - left, bottom - top), new Vector(96 * Scale, 96 * Scale), source.Format, frame.AlphaFormat);
        using (var target = cropped.Lock())
        {
            var line = new byte[(right - left) * 4];
            for (var row = 0; row < bottom - top; row++)
            {
                Marshal.Copy(source.Address + ((top + row) * source.RowBytes) + (left * 4), line, 0, line.Length);
                Marshal.Copy(line, 0, target.Address + (row * target.RowBytes), line.Length);
            }
        }

        return cropped;
    }

    private static void Save(WriteableBitmap picture, string path)
    {
        using (picture)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            picture.Save(path, PngBitmapEncoderOptions.Default);
        }
    }
}

/// <summary>What a shot's picture shows: a region of the window, in its logical pixels, read after the scaling is set.</summary>
public sealed class GuideFrame
{
    private readonly Func<ShellDriver, Rect> _bounds;

    private GuideFrame(Func<ShellDriver, Rect> bounds)
    {
        _bounds = bounds;
    }

    /// <summary>The whole window.</summary>
    public static GuideFrame Window { get; } = new(app => new Rect(app.Window.ClientSize));

    /// <summary>The smallest region that holds every one of <paramref name="visuals"/>.</summary>
    public static GuideFrame Of(params Visual[] visuals)
    {
        ArgumentNullException.ThrowIfNull(visuals);
        if (visuals.Length == 0)
        {
            throw new ArgumentException("A frame needs at least one control.", nameof(visuals));
        }

        return new(app => visuals.Select(app.BoundsOf).Aggregate((one, other) => one.Union(other)));
    }

    /// <summary>A region given in the window's logical pixels.</summary>
    public static GuideFrame Area(Rect area) => new(_ => area);

    /// <summary>This frame grown by <paramref name="pixels"/> logical pixels on each side.</summary>
    public GuideFrame Inflate(double pixels) => new(app => _bounds(app).Inflate(pixels));

    /// <summary>This frame and <paramref name="other"/> together.</summary>
    public GuideFrame With(GuideFrame other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(app => _bounds(app).Union(other._bounds(app)));
    }

    /// <summary>The region in <paramref name="app"/>'s window now.</summary>
    public Rect BoundsIn(ShellDriver app) => _bounds(app);
}
