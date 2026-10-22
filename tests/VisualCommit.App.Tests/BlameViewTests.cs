using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Blame;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.App.Views.Pages;
using VisualCommit.Core.Git;
using Xunit;
using static VisualCommit.App.Tests.PageFakes;

namespace VisualCommit.App.Tests;

/// <summary>
/// The blame page's text and view in a headless window (docs/phase-5-design.md 4.2; D64, D65): a
/// read fills the text and the gutter once, and the syntax colours of a file over the diff's
/// 1,000 lines are made on a background thread, as the diff's are, so that the window never stops
/// for them.
/// </summary>
public class BlameViewTests
{
    private static readonly BlameCommit Parser = BlameCommitOf(AddParserSha, "Mira Kovac", 1, "src/Parser.cs");
    private static readonly BlameCommit Renamed = BlameCommitOf(RenameSha, "Mira Kovac", 6, "src/Reader.cs", AddTracingSha, "src/Parser.cs");

    [AvaloniaFact]
    public async Task A_read_fills_the_text_and_the_gutter_once()
    {
        var repository = new PageFakeRepository
        {
            Blame = (_, _) => new BlameResult([new("namespace History;", Parser), new(string.Empty, Parser), new("public sealed class Reader", Renamed)]),
        };
        using var page = new BlameViewModel(new RecordingPageHost(repository), new BlameRequest("src/Reader.cs", null));
        var view = new BlameView { DataContext = page };
        using var host = DiffHost.Show(view, 700, 400, ThemeVariant.Dark);
        var text = view.GetVisualDescendants().OfType<BlameTextEditor>().Single(control => control.Name == "BlameText");
        var gutter = view.GetVisualDescendants().OfType<BlameGutter>().Single(control => control.Name == "BlameGutter");
        var documents = 0;
        text.DocumentChanged += (_, _) => documents++;

        await page.LoadAsync();
        DiffHost.Settle();

        // One document for the read, whatever order its lines, runs and highlighting are set in.
        Assert.Equal(1, documents);
        Assert.Equal("namespace History;\n\npublic sealed class Reader", text.Document.Text);
        Assert.Equal([(1, 2), (3, 3)], gutter.Runs.Select(run => (run.FirstLine, run.LastLine)));
        Assert.True(text.IsHighlighted);
        Assert.False(text.IsTokenizing);
    }

    /// <summary>
    /// Step 25's review: the gutter found a line's run by scanning every run from the first, for
    /// every visible line of every frame. It is a binary search over the runs, which follow one
    /// another in line order: the first, a middle and the last line of each run, and none outside.
    /// </summary>
    [AvaloniaFact]
    public void The_gutter_finds_the_run_of_every_line_and_none_outside_the_file()
    {
        const int Count = 20_001;
        var gutter = new BlameGutter
        {
            Runs = [.. Enumerable.Range(0, Count).Select(index => new BlameGutterRun(1 + (index * 3), 3 + (index * 3), index % 2 == 0, "abc1234", "A", "2026-01-01", false, string.Empty))],
        };

        foreach (var index in new[] { 0, 1, 2, Count / 2, Count - 2, Count - 1 })
        {
            Assert.Equal(index, gutter.RunOfLine(1 + (index * 3)));
            Assert.Equal(index, gutter.RunOfLine(2 + (index * 3)));
            Assert.Equal(index, gutter.RunOfLine(3 + (index * 3)));
        }

        Assert.Null(gutter.RunOfLine(0));
        Assert.Null(gutter.RunOfLine(4 + ((Count - 1) * 3)));
        Assert.Null(new BlameGutter().RunOfLine(1));
    }

    [AvaloniaFact]
    public async Task A_large_files_colours_are_made_on_a_background_thread_and_a_small_files_before_it_is_drawn()
    {
        var editor = new BlameTextEditor();
        using var host = DiffHost.Show(editor, 600, 400, ThemeVariant.Dark);

        editor.Show(Lines(DiffTextView.SynchronousHighlightLimit), "src/Small.cs", highlighting: true);
        Assert.False(editor.IsTokenizing);
        Assert.True(editor.IsHighlighted);

        // Over the limit: the text shows at once, its colours when the background thread ends.
        var large = Lines(DiffTextView.SynchronousHighlightLimit + 500);
        editor.Show(large, "src/Large.cs", highlighting: true);
        Assert.True(editor.IsTokenizing);
        Assert.False(editor.IsHighlighted);
        Assert.Equal(DiffTextView.SynchronousHighlightLimit + 500, editor.Document.LineCount);
        var until = DateTime.UtcNow.AddSeconds(30);
        while (editor.IsTokenizing)
        {
            Assert.True(DateTime.UtcNow < until, "The background tokenizing did not end.");
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(editor.IsHighlighted);

        // A newer text replaces a tokenizing still running: the older one, cancelled, posts no
        // colours, so its result neither ends the newer one's wait nor clears its colours. Only
        // the newer one's tokens end the wait, and they colour the text.
        editor.Show(large, "src/Large.cs", highlighting: true);
        editor.Show(large, "src/Large.cs", highlighting: true);
        Assert.True(editor.IsTokenizing);
        until = DateTime.UtcNow.AddSeconds(30);
        while (editor.IsTokenizing)
        {
            Assert.True(DateTime.UtcNow < until, "The background tokenizing did not end.");
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(editor.IsHighlighted, "The replaced tokenizing ended the newer one's wait without its colours.");

        // Without colours (D65) nothing is tokenized.
        editor.Show(large, "src/Large.cs", highlighting: false);
        Assert.False(editor.IsTokenizing);
        Assert.False(editor.IsHighlighted);
    }

    /// <summary>
    /// The theme switched while the blame shows (docs/phase-6-design.md 11.15, the live switches
    /// r06 found untested): the text and the gutter take the other theme's colours at once, the
    /// gutter's <c>Not committed yet</c> in that theme's warning colour (light <c>#895A17</c>, 11.10).
    /// A file not committed yet: every line is one run, so the gutter's only text is that one.
    /// </summary>
    [AvaloniaFact]
    public async Task Switching_the_theme_recolours_the_text_and_the_gutters_Not_committed_yet()
    {
        var uncommitted = new BlameCommit(Zero, "Not Committed Yet", "not.committed.yet", Minute(9), "Version of src/Notes.cs from src/Notes.cs", "src/Notes.cs", null, null, IsBoundary: false);
        var repository = new PageFakeRepository
        {
            Blame = (_, _) => new BlameResult([new("namespace History;", uncommitted), new(string.Empty, uncommitted), new("public sealed class Notes", uncommitted)]),
        };
        using var page = new BlameViewModel(new RecordingPageHost(repository), new BlameRequest("src/Notes.cs", null));
        var view = new BlameView { DataContext = page };
        using var host = DiffHost.Show(view, 700, 400, ThemeVariant.Dark);
        var text = view.GetVisualDescendants().OfType<BlameTextEditor>().Single(control => control.Name == "BlameText");
        var gutter = view.GetVisualDescendants().OfType<BlameGutter>().Single(control => control.Name == "BlameGutter");
        await page.LoadAsync();
        DiffHost.Settle();
        Assert.Equal([BlameGutter.NotCommittedYetText], gutter.TextsOf(0).Select(part => part.Text));

        foreach (var (variant, colors, warning) in new[]
        {
            (ThemeVariant.Dark, DiffColors.Dark, Color.Parse("#E0A23B")),
            (ThemeVariant.Light, DiffColors.Light, Color.Parse("#895A17")),
            (ThemeVariant.Dark, DiffColors.Dark, Color.Parse("#E0A23B")),
        })
        {
            host.Window.RequestedThemeVariant = variant;
            var shot = host.Capture();
            TestPictures.Save(host, $"blame-switched-{(variant == ThemeVariant.Dark ? "dark" : "light")}");
            var name = variant == ThemeVariant.Dark ? "dark" : "light";

            DiffColors.AssertBrush(colors.TextPrimary, text.Foreground, $"the text's colour, {name}");
            DiffColors.AssertBrush(colors.Background, text.Background, $"the text's background, {name}");
            var textArea = new Rect(host.ToWindow(text, default), text.Bounds.Size);
            Assert.Equal(colors.Background, shot.PixelAt(new Point(textArea.Right - 30, textArea.Bottom - 30)));

            // The gutter: run 0 on the panel's background (right of its text, in the first line),
            // its only text's ink in the warning colour.
            var panel = name == "dark" ? Color.Parse("#1A1D24") : Color.Parse("#F5F6F8");
            var area = new Rect(host.ToWindow(gutter, default), gutter.Bounds.Size);
            Assert.Equal(panel, shot.PixelAt(new Point(area.Right - 4, area.Y + 4)));
            Assert.Equal(warning, InkOf(shot, area.Deflate(1), dark: name == "dark"));
        }
    }

    /// <summary>
    /// The ink of the text in <paramref name="area"/>, channel by channel: the most of each on a
    /// dark background, the least on a light one. Antialiased text, subpixel or not, reaches its
    /// colour in each channel somewhere, where nothing else in the area does.
    /// </summary>
    private static Color InkOf(Testing.Headless.Screenshot shot, Rect area, bool dark)
    {
        byte r = dark ? (byte)0 : (byte)255, g = r, b = r;
        for (var y = (int)Math.Ceiling(area.Y); y < (int)Math.Floor(area.Bottom); y++)
        {
            for (var x = (int)Math.Ceiling(area.X); x < (int)Math.Floor(area.Right); x++)
            {
                var pixel = shot.PixelAt(x, y);
                (r, g, b) = dark
                    ? (Math.Max(r, pixel.R), Math.Max(g, pixel.G), Math.Max(b, pixel.B))
                    : (Math.Min(r, pixel.R), Math.Min(g, pixel.G), Math.Min(b, pixel.B));
            }
        }

        return Color.FromRgb(r, g, b);
    }

    /// <summary><paramref name="count"/> lines of C#.</summary>
    private static string Lines(int count) =>
        string.Join('\n', Enumerable.Range(1, count).Select(line => $"var value{line} = \"line {line}\"; // {line}"));
}
