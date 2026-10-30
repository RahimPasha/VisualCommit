using System.Diagnostics;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.GraphTestHost;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// The authors' avatars in the graph's Author column (docs/phase-7a-design.md 11.3, 11.4; D310):
/// drawn once decoded, 16×16 and round at the column's x + 8, the name from + 30; an author without
/// an image, a file that does not decode and a hidden column as without avatars; a dimmed row's
/// image dimmed; <see cref="CommitGraphControl.AvatarsApplied"/> once per change, its count the
/// authors whose image decoded, with the column shown or hidden (the report's open point 42).
/// </summary>
public sealed class GraphAvatarTests : IDisposable
{
    private const string OctoEmail = "octo@example.com";
    private static readonly Color Blue = Color.Parse("#2F81F7");

    private readonly TempDirectory _folder = new("phase7a graph avatars");

    public void Dispose() => _folder.Dispose();

    /// <summary>Two rows by Octo Cat (row 0 with octo's address, which has an image; row 1 with another) and one by Test Author.</summary>
    private static CommitGraphData Authors() =>
        Graph([("Octo Cat", "Octo@Example.com"), ("Octo Cat", "other@example.com"), ("Test Author", "author@example.com")], (2, Local("main", head: true)));

    private string Image(string name, byte[] bytes)
    {
        var path = Path.Combine(_folder.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string OctoImage() => Image("1001.png", TestImages.SquareOn(64, 64, TestImages.Rgb.Parse("#2F81F7"), TestImages.Rgb.Parse("#FFFFFF"), 24));

    /// <summary>Sets <paramref name="avatars"/> and pumps the UI thread until the control has applied them; returns each count it raised.</summary>
    private static List<int> Apply(GraphTestHost host, AuthorAvatars avatars)
    {
        var counts = new List<int>();
        void Applied(object? sender, int count) => counts.Add(count);
        host.Control.AvatarsApplied += Applied;
        try
        {
            host.Control.Avatars = avatars;
            var clock = Stopwatch.StartNew();
            while (counts.Count == 0 && clock.Elapsed < TimeSpan.FromSeconds(30))
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }

            // Whatever else was posted runs, so a second event would show.
            Dispatcher.UIThread.RunJobs();
            Assert.NotEmpty(counts);
            return counts;
        }
        finally
        {
            host.Control.AvatarsApplied -= Applied;
        }
    }

    private static AuthorAvatars Of(params (string Email, string File)[] entries) =>
        new(entries.ToDictionary(entry => entry.Email, entry => entry.File, StringComparer.Ordinal));

    /// <summary>The leftmost x in the row's Author column whose pixel is not the row's background, from <paramref name="from"/>.</summary>
    private static int FirstInk(Screenshot frame, CommitGraphControl control, int row, int from)
    {
        var column = control.Columns.Author;
        var bounds = control.RowBounds(row);
        var background = frame.PixelAt((int)column.Right - 2, (int)bounds.Y + 2);
        for (var x = from; x < column.Right; x++)
        {
            for (var y = (int)bounds.Y + 1; y < bounds.Bottom - 1; y++)
            {
                if (frame.PixelAt(x, y) != background)
                {
                    return x;
                }
            }
        }

        return -1;
    }

    private static void AssertSamePixels(Screenshot expected, Screenshot actual, Rect area)
    {
        for (var y = (int)area.Y; y < (int)area.Bottom; y++)
        {
            for (var x = (int)area.X; x < (int)area.Right; x++)
            {
                Assert.True(expected.PixelAt(x, y) == actual.PixelAt(x, y), $"The pixel at ({x}, {y}) differs: {expected.PixelAt(x, y)} and {actual.PixelAt(x, y)}.");
            }
        }
    }

    private static void AssertNear(Color expected, Color actual, int tolerance, string what) =>
        Assert.True(
            Math.Abs(expected.R - actual.R) <= tolerance && Math.Abs(expected.G - actual.G) <= tolerance && Math.Abs(expected.B - actual.B) <= tolerance,
            $"{what} is {actual}, not {expected} ±{tolerance}.");

    [AvaloniaFact]
    public void A_decoded_image_is_drawn_round_before_the_name_which_moves_22()
    {
        var data = Authors();
        using var plain = Show(new CommitGraphControl { Data = data }, WideWidth);
        using var host = Show(new CommitGraphControl { Data = data }, WideWidth);
        Assert.True(host.Control.Columns.Author.IsVisible);
        var before = host.Capture();
        Assert.False(host.Control.ShowsAvatarFor(OctoEmail));

        Assert.Equal([1], Apply(host, Of((OctoEmail, OctoImage()))));
        Assert.True(host.Control.ShowsAvatarFor(OctoEmail));
        Assert.True(host.Control.ShowsAvatarFor("OCTO@example.com"));
        Assert.False(host.Control.ShowsAvatarFor("other@example.com"));
        Assert.False(host.Control.ShowsAvatarFor("author@example.com"));

        var frame = host.Capture();
        var x = (int)host.Control.Columns.Author.X;
        var top = (int)host.Control.RowBounds(0).Y;

        // The image from x + 8 to x + 24 and y + 5 to y + 21: its white centre, its colour 5 left,
        // and the corner of its square cut by the circle; nothing left of it.
        AssertNear(Colors.White, frame.PixelAt(x + 16, top + 13), 24, "The image's centre");
        AssertNear(Blue, frame.PixelAt(x + 11, top + 13), 24, "The image's colour 5 left of its centre");
        AssertNear(Blue, frame.PixelAt(x + 9, top + 13), 60, "The image's left edge");
        var background = frame.PixelAt(x + 2, top + 2);
        Assert.Equal(background, frame.PixelAt(x + 8, top + 5));
        Assert.Equal(background, frame.PixelAt(x + 23, top + 20));
        Assert.Equal(background, frame.PixelAt(x + 7, top + 13));
        Assert.Equal(background, frame.PixelAt(x + 25, top + 13));

        // The same name 22 further than on the row without an image, whose name is where it was.
        var bare = FirstInk(frame, host.Control, 1, x);
        Assert.Equal(FirstInk(before, host.Control, 1, x), bare);
        Assert.Equal(bare + 22, FirstInk(frame, host.Control, 0, x + 25));

        // The rows without an image are as without avatars.
        var unchanged = plain.Capture();
        AssertSamePixels(unchanged, frame, new Rect(0, host.Control.RowBounds(1).Y, GraphTestHost.WideWidth, 2 * CommitGraphControl.RowHeight));
    }

    [AvaloniaFact]
    public void AuthorInRow_gives_where_the_image_and_the_name_are_drawn()
    {
        var data = Authors();
        using var host = Show(new CommitGraphControl { Data = data }, WideWidth);
        var column = host.Control.Columns.Author;
        var before = host.Control.AuthorInRow(0) ?? throw new Xunit.Sdk.XunitException("Row 0 draws no author.");
        Assert.Null(before.Avatar);
        Assert.Equal(column.X + 8, before.Name.X);

        // With octo's image: the image at x + 8 and the row's top + 5, 16 square; the name from + 30,
        // centred in the row, inside the column; the row without an image as before.
        Apply(host, Of((OctoEmail, OctoImage())));
        var top = host.Control.RowBounds(0).Y;
        var placed = host.Control.AuthorInRow(0) ?? throw new Xunit.Sdk.XunitException("Row 0 draws no author.");
        Assert.Equal(new Rect(column.X + 8, top + 5, 16, 16), placed.Avatar);
        Assert.Equal(column.X + 30, placed.Name.X);
        Assert.Equal(top + ((CommitGraphControl.RowHeight - placed.Name.Height) / 2), placed.Name.Y, 0.01);
        Assert.Equal(before.Name.Size, placed.Name.Size);
        Assert.True(placed.Name.Width > 0 && placed.Name.Right <= column.Right, $"Row 0's name is laid out at {placed.Name}, the column at {column.X} to {column.Right}.");
        var other = host.Control.AuthorInRow(1) ?? throw new Xunit.Sdk.XunitException("Row 1 draws no author.");
        Assert.Equal((null, column.X + 8), (other.Avatar, other.Name.X));

        // None for a row that is not there, nor while the column is hidden.
        using var narrow = Show(new CommitGraphControl { Data = data });
        Assert.All(new[] { host.Control.AuthorInRow(3), host.Control.AuthorInRow(-1), narrow.Control.AuthorInRow(0) }, Assert.Null);
    }

    [AvaloniaFact]
    public void A_file_that_does_not_decode_draws_as_today_and_is_counted_out()
    {
        var data = Authors();
        using var plain = Show(new CommitGraphControl { Data = data }, WideWidth);
        using var host = Show(new CommitGraphControl { Data = data }, WideWidth);
        Assert.Equal([0], Apply(host, Of((OctoEmail, Image("broken.png", [0x89, 0x50, 0x4E, 0x47, 1, 2, 3])), ("other@example.com", Path.Combine(_folder.Path, "missing.png")))));
        Assert.False(host.Control.ShowsAvatarFor(OctoEmail));
        AssertSamePixels(plain.Capture(), host.Capture(), new Rect(0, 0, GraphTestHost.WideWidth, 3 * CommitGraphControl.RowHeight));
    }

    [AvaloniaFact]
    public void A_hidden_author_column_draws_none_and_still_counts_the_decoded_authors()
    {
        var data = Authors();
        using var plain = Show(new CommitGraphControl { Data = data });
        using var host = Show(new CommitGraphControl { Data = data });
        Assert.False(host.Control.Columns.Author.IsVisible);

        // Open point 42: the event and its count come with the column hidden too. The image is
        // applied (ShowsAvatarFor says so whatever the column), but no pixel shows it.
        Assert.Equal([1], Apply(host, Of((OctoEmail, OctoImage()))));
        Assert.True(host.Control.ShowsAvatarFor(OctoEmail));
        AssertSamePixels(plain.Capture(), host.Capture(), new Rect(0, 0, GraphTestHost.AreaWidth, 3 * CommitGraphControl.RowHeight));

        // Back to none: a change too, counting 0.
        Assert.Equal([0], Apply(host, AuthorAvatars.None));
        Assert.False(host.Control.ShowsAvatarFor(OctoEmail));
    }

    [AvaloniaFact]
    public void A_row_the_search_dims_dims_its_image_with_its_text()
    {
        var data = Authors();
        using var host = Show(new CommitGraphControl { Data = data }, WideWidth);
        Apply(host, Of((OctoEmail, OctoImage())));
        var x = (int)host.Control.Columns.Author.X;
        var top = (int)host.Control.RowBounds(0).Y;
        var full = host.Capture().PixelAt(x + 11, top + 13);
        var background = host.Capture().PixelAt(x + 4, top + 13);

        // A search that matches row 2 only dims rows 0 and 1.
        host.Control.Search = new GraphSearchMarks([ShaOf(2)], isActive: true);
        var dimmed = host.Capture().PixelAt(x + 11, top + 13);
        var expected = Color.FromRgb(
            (byte)Math.Round((full.R * CommitGraphControl.SearchDimmedOpacity) + (background.R * (1 - CommitGraphControl.SearchDimmedOpacity))),
            (byte)Math.Round((full.G * CommitGraphControl.SearchDimmedOpacity) + (background.G * (1 - CommitGraphControl.SearchDimmedOpacity))),
            (byte)Math.Round((full.B * CommitGraphControl.SearchDimmedOpacity) + (background.B * (1 - CommitGraphControl.SearchDimmedOpacity))));
        AssertNear(expected, dimmed, 6, "The dimmed image's colour");
    }

    [AvaloniaFact]
    public void An_overtaken_change_applies_nothing_and_the_newest_is_applied_once()
    {
        var data = Authors();
        using var host = Show(new CommitGraphControl { Data = data }, WideWidth);
        var counts = new List<int>();
        host.Control.AvatarsApplied += (_, count) => counts.Add(count);
        host.Control.Avatars = Of((OctoEmail, OctoImage()));
        Assert.Equal([0], Apply(host, Of(("author@example.com", Path.Combine(_folder.Path, "missing.png")))));
        Assert.Equal([0], counts);
        Assert.False(host.Control.ShowsAvatarFor(OctoEmail));
    }
}
