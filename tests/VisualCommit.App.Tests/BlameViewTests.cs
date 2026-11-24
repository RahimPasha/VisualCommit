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
using VisualCommit.Testing;
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
        var until = DateTime.UtcNow.AddSeconds(60);
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
        until = DateTime.UtcNow.AddSeconds(60);
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

        // The light warning replaced #B7791F (11.10); the dark one is unchanged.
        foreach (var (variant, colors, warning, replaced) in new[]
        {
            (ThemeVariant.Dark, DiffColors.Dark, Color.Parse("#E0A23B"), (Color?)null),
            (ThemeVariant.Light, DiffColors.Light, Color.Parse("#895A17"), Color.Parse("#B7791F")),
            (ThemeVariant.Dark, DiffColors.Dark, Color.Parse("#E0A23B"), null),
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
            // its only text's ink in the warning colour (TextInk's rule).
            var panel = name == "dark" ? Color.Parse("#1A1D24") : Color.Parse("#F5F6F8");
            var area = new Rect(host.ToWindow(gutter, default), gutter.Bounds.Size);
            Assert.Equal(panel, shot.PixelAt(new Point(area.Right - 4, area.Y + 4)));
            Assert.Empty(TextInk.Problems(shot, area.Deflate(1), warning, panel, replaced, $"Not committed yet, {name}"));
        }
    }

    /// <summary>
    /// The keyboard pass (docs/phase-8a-design.md 6.3.2): after an arrow the caret's run is picked,
    /// as a click picks it: its lines on the selection background, its commit in the right panel.
    /// Three Downs from line 1 pick run 0, run 1 and run 2 in turn.
    /// </summary>
    [AvaloniaFact]
    public async Task Down_picks_the_run_the_caret_comes_to_and_shows_its_commit()
    {
        using var page = ThreeRuns(out var pageHost);
        var view = new BlameView { DataContext = page };
        using var host = DiffHost.Show(view, 700, 400, ThemeVariant.Dark);
        await page.LoadAsync();
        DiffHost.Settle();
        var text = Text(view);
        Assert.True(text.TextArea.Focus());
        Assert.Null(page.SelectedRun);

        host.PressKey(Avalonia.Input.Key.Down);
        Assert.Equal(0, page.SelectedRun?.Index);
        host.PressKey(Avalonia.Input.Key.Down);
        Assert.Equal(1, page.SelectedRun?.Index);
        host.PressKey(Avalonia.Input.Key.Down);

        Assert.Equal(4, text.TextArea.Caret.Line);
        Assert.Equal(2, page.SelectedRun?.Index);
        Assert.Equal(2, Gutter(view).SelectedRun);
        Assert.Equal([AddParserSha, RenameSha, AddTracingSha], pageHost.Shown);
    }

    /// <summary>Ctrl+End takes the caret to the last line and picks the last run; Ctrl+Home the first (6.3.2).</summary>
    [AvaloniaFact]
    public async Task Ctrl_End_picks_the_last_run_and_Ctrl_Home_the_first()
    {
        using var page = ThreeRuns(out var pageHost);
        var view = new BlameView { DataContext = page };
        using var host = DiffHost.Show(view, 700, 400, ThemeVariant.Dark);
        await page.LoadAsync();
        DiffHost.Settle();
        Assert.True(Text(view).TextArea.Focus());

        host.PressKey(Avalonia.Input.Key.End, Avalonia.Input.RawInputModifiers.Control);
        Assert.Equal(2, page.SelectedRun?.Index);
        host.PressKey(Avalonia.Input.Key.Home, Avalonia.Input.RawInputModifiers.Control);
        Assert.Equal(0, page.SelectedRun?.Index);
        Assert.Equal([AddTracingSha, AddParserSha], pageHost.Shown);
    }

    /// <summary>
    /// The caret keys pick with any modifier (6.3.2): macOS moves the caret by Cmd with the arrows
    /// (to a line's or the text's ends) and by Option with them (by words), its platform's keys for
    /// Windows' Ctrl with them, so an arrow with Cmd or Option picks the caret's run as one with Ctrl
    /// or Shift does. The headless platform moves no caret on these keys, so the caret is put where
    /// macOS's keys would take it before each.
    /// </summary>
    [AvaloniaFact]
    public async Task An_arrow_with_Cmd_or_Option_picks_the_carets_run()
    {
        using var page = ThreeRuns(out var pageHost);
        var view = new BlameView { DataContext = page };
        using var host = DiffHost.Show(view, 700, 400, ThemeVariant.Dark);
        await page.LoadAsync();
        DiffHost.Settle();
        var text = Text(view);
        Assert.True(text.TextArea.Focus());

        text.TextArea.Caret.Line = 5;
        host.PressKey(Avalonia.Input.Key.Down, Avalonia.Input.RawInputModifiers.Meta);
        Assert.Equal(2, page.SelectedRun?.Index);
        text.TextArea.Caret.Line = 3;
        host.PressKey(Avalonia.Input.Key.Left, Avalonia.Input.RawInputModifiers.Alt);
        Assert.Equal(1, page.SelectedRun?.Index);
        Assert.Equal([AddTracingSha, RenameSha], pageHost.Shown);
    }

    /// <summary>Tab leaves the blame's read-only text (ruling 52, D426): AvaloniaEdit no longer keeps it.</summary>
    [AvaloniaFact]
    public async Task Tab_leaves_the_text()
    {
        using var page = ThreeRuns(out _);
        var view = new BlameView { DataContext = page };
        var after = new Avalonia.Controls.Button { Content = "After" };
        var root = new Avalonia.Controls.StackPanel { Children = { view, after } };
        view.Height = 300;
        using var host = DiffHost.Show(root, 700, 400, ThemeVariant.Dark);
        await page.LoadAsync();
        DiffHost.Settle();
        var text = Text(view);
        Assert.True(text.TextArea.Focus());

        host.PressKey(Avalonia.Input.Key.Tab);

        Assert.True(after.IsFocused, $"Tab did not leave the text: the focus is on {host.Window.FocusManager?.GetFocusedElement()?.GetType().Name ?? "nothing"}.");
        Assert.Equal("namespace History;\n\npublic sealed class Reader\n{\n}", text.Document.Text);
    }

    /// <summary>A click on a line of the text still picks its run (phase 5's 4.2).</summary>
    [AvaloniaFact]
    public async Task A_click_on_the_text_still_picks_its_run()
    {
        using var page = ThreeRuns(out var pageHost);
        var view = new BlameView { DataContext = page };
        using var host = DiffHost.Show(view, 700, 400, ThemeVariant.Dark);
        await page.LoadAsync();
        DiffHost.Settle();
        var text = Text(view);
        host.Capture();

        var textView = text.TextView;
        var top = textView.GetVisualTopByDocumentLine(3) - textView.VerticalOffset;
        host.Click(textView, new Point(40, top + 4));

        Assert.Equal(1, page.SelectedRun?.Index);
        Assert.Equal([RenameSha], pageHost.Shown);
    }

    /// <summary>
    /// A click in the gutter picks a run without touching the text, which keeps the keyboard focus:
    /// it also puts the caret, which is not drawn, on the picked run's first line, so that the arrows
    /// go on from that run (docs/phase-8a-design.md 6.3.2). A click beside line 5, run 2's second
    /// line, puts it on line 4; Up then takes it to line 3 and picks run 1. Before, the caret stayed
    /// on line 1, where the page opened it, and Up picked run 0.
    /// </summary>
    [AvaloniaFact]
    public async Task A_click_in_the_gutter_puts_the_caret_on_its_runs_first_line_so_that_the_arrows_go_on_from_that_run()
    {
        using var page = ThreeRuns(out var pageHost);
        var view = new BlameView { DataContext = page };
        using var host = DiffHost.Show(view, 700, 400, ThemeVariant.Dark);
        await page.LoadAsync();
        DiffHost.Settle();
        var text = Text(view);
        var gutter = Gutter(view);
        Assert.True(text.TextArea.Focus());
        Assert.Equal(1, text.TextArea.Caret.Line);

        host.Click(gutter, GutterPoint(text, gutter, 5));

        Assert.Equal(2, page.SelectedRun?.Index);
        Assert.Equal(4, text.TextArea.Caret.Line);
        Assert.True(text.TextArea.IsKeyboardFocusWithin);

        host.PressKey(Avalonia.Input.Key.Up);

        Assert.Equal(3, text.TextArea.Caret.Line);
        Assert.Equal(1, page.SelectedRun?.Index);
        Assert.Equal([AddTracingSha, RenameSha], pageHost.Shown);
    }

    /// <summary>
    /// The caret put on a picked run moves no text (phase 5's and 8a's blame pictures stay as they
    /// were), though AvaloniaEdit scrolls toward a caret it is given, and the next arrow moves it one
    /// line from a line the user sees (docs/phase-8a-design.md 13.1, ruling 100): with the text
    /// scrolled across and down until line 46, run 1's first, is the last line in view, a click on
    /// run 0's line 40, whose first line is far above the view, puts the caret on the first of run
    /// 0's lines in view, the one drawn at the view's top, not on line 1, and the next Down takes it
    /// one line down with the text where it was and run 0 still picked (on line 1, that Down scrolled
    /// the text up to line 2 and back across); a click on line 46 puts it on line 46, the text still
    /// where it was.
    /// </summary>
    [AvaloniaFact]
    public async Task A_click_in_the_gutter_moves_no_text_however_far_its_runs_first_line_lies()
    {
        using var page = TwoLongRuns();
        var view = new BlameView { DataContext = page };
        using var host = DiffHost.Show(view, 700, 400, ThemeVariant.Dark);
        await page.LoadAsync();
        DiffHost.Settle();
        var text = Text(view);
        var gutter = Gutter(view);
        Assert.True(text.TextArea.Focus());
        var scroller = text.GetVisualDescendants().OfType<Avalonia.Controls.ScrollViewer>().First();
        scroller.Offset = new Vector(60, text.TextView.GetVisualTopByDocumentLine(47) - text.TextView.Bounds.Height);
        DiffHost.Settle();
        var scrolled = (text.HorizontalOffset, text.VerticalOffset);
        Assert.True(scrolled.VerticalOffset > 0 && scrolled.HorizontalOffset > 0, $"The text did not scroll: {scrolled}.");
        Assert.Equal(46, text.LineAt(text.TextView.TranslatePoint(new Point(0, text.TextView.Bounds.Height - 2), text)!.Value.Y));
        Assert.NotNull(text.LineAt(text.TextView.TranslatePoint(new Point(0, text.TextView.GetVisualTopByDocumentLine(40) - text.VerticalOffset + 4), text)!.Value.Y));

        var firstInView = text.LineAt(text.TextView.TranslatePoint(default, text)!.Value.Y) ?? throw new InvalidOperationException("No line is drawn at the view's top.");
        Assert.InRange(firstInView, 2, 40);

        host.Click(gutter, GutterPoint(text, gutter, 40));
        DiffHost.Settle();

        Assert.Equal(0, page.SelectedRun?.Index);
        Assert.Equal(firstInView, text.TextArea.Caret.Line);
        Assert.Equal(scrolled, (text.HorizontalOffset, text.VerticalOffset));

        host.PressKey(Avalonia.Input.Key.Down);
        DiffHost.Settle();

        Assert.Equal(firstInView + 1, text.TextArea.Caret.Line);
        Assert.Equal(0, page.SelectedRun?.Index);
        Assert.Equal(scrolled, (text.HorizontalOffset, text.VerticalOffset));

        host.Click(gutter, GutterPoint(text, gutter, 46));
        DiffHost.Settle();

        Assert.Equal(1, page.SelectedRun?.Index);
        Assert.Equal(46, text.TextArea.Caret.Line);
        Assert.Equal(scrolled, (text.HorizontalOffset, text.VerticalOffset));

        // From a run whose first line is in view, one Up picks the run above.
        host.PressKey(Avalonia.Input.Key.Up);
        DiffHost.Settle();

        Assert.Equal(45, text.TextArea.Caret.Line);
        Assert.Equal(0, page.SelectedRun?.Index);
    }

    /// <summary>
    /// A run whose first line shows only in part at the view's top (docs/phase-8a-design.md 13.1,
    /// ruling 100): that line is in view, so a click on the run's line 50 in the gutter puts the caret
    /// on line 46, the run's first, with the text where it was; the next Down takes it to line 47 and
    /// scrolls nothing; and after a click on its line 52, one Up picks the run above.
    /// </summary>
    [AvaloniaFact]
    public async Task A_click_on_a_run_whose_first_line_shows_in_part_at_the_top_puts_the_caret_there_and_one_Up_picks_the_run_above()
    {
        using var page = TwoLongRuns();
        var view = new BlameView { DataContext = page };
        using var host = DiffHost.Show(view, 700, 400, ThemeVariant.Dark);
        await page.LoadAsync();
        DiffHost.Settle();
        var text = Text(view);
        var gutter = Gutter(view);
        Assert.True(text.TextArea.Focus());
        var scroller = text.GetVisualDescendants().OfType<Avalonia.Controls.ScrollViewer>().First();
        scroller.Offset = new Vector(0, text.TextView.GetVisualTopByDocumentLine(46) + 6);
        DiffHost.Settle();
        var scrolled = (text.HorizontalOffset, text.VerticalOffset);
        Assert.Equal(46, text.LineAt(text.TextView.TranslatePoint(default, text)!.Value.Y));
        Assert.True(text.TextView.GetVisualTopByDocumentLine(46) < text.VerticalOffset - 5, "Line 46 lies wholly in view.");

        host.Click(gutter, GutterPoint(text, gutter, 50));
        DiffHost.Settle();

        Assert.Equal(1, page.SelectedRun?.Index);
        Assert.Equal(46, text.TextArea.Caret.Line);
        Assert.Equal(scrolled, (text.HorizontalOffset, text.VerticalOffset));

        host.PressKey(Avalonia.Input.Key.Down);
        DiffHost.Settle();

        Assert.Equal(47, text.TextArea.Caret.Line);
        Assert.Equal(1, page.SelectedRun?.Index);
        Assert.Equal(scrolled, (text.HorizontalOffset, text.VerticalOffset));

        // Line 52, not 50 again: two presses at one spot within the double-tap time are a double-click.
        host.Click(gutter, GutterPoint(text, gutter, 52));
        DiffHost.Settle();
        Assert.Equal(46, text.TextArea.Caret.Line);
        host.PressKey(Avalonia.Input.Key.Up);
        DiffHost.Settle();

        Assert.Equal(45, text.TextArea.Caret.Line);
        Assert.Equal(0, page.SelectedRun?.Index);
    }

    /// <summary>A blame of 85 lines in two runs: lines 1 to 45, each over 150 characters long, "Add the parser"; lines 46 to 85, short, "Add tracing".</summary>
    private static BlameViewModel TwoLongRuns()
    {
        var first = BlameCommitOf(AddParserSha, "Mira Kovac", 1, "src/Parser.cs");
        var second = BlameCommitOf(AddTracingSha, "Jonas Berg", 4, "src/Parser.cs");
        var repository = new PageFakeRepository
        {
            Blame = (_, _) => new BlameResult(
            [
                .. Enumerable.Range(1, 45).Select(line => new BlameLine($"// line {line} {new string('x', 150)}", first)),
                .. Enumerable.Range(46, 40).Select(line => new BlameLine($"// line {line}", second)),
            ]),
        };
        return new BlameViewModel(new RecordingPageHost(repository), new BlameRequest("src/Reader.cs", null));
    }

    /// <summary>The point 20 right of the gutter's left edge, 4 below the top of line <paramref name="line"/> as the text draws it now.</summary>
    private static Point GutterPoint(BlameTextEditor text, BlameGutter gutter, int line)
    {
        var top = text.TextView.GetVisualTopByDocumentLine(line) - text.TextView.VerticalOffset + 4;
        var inGutter = text.TextView.TranslatePoint(new Point(0, top), gutter) ?? throw new InvalidOperationException("The text and the gutter share no window.");
        return new Point(20, inGutter.Y);
    }

    /// <summary>The page of a blame of five lines in three runs: lines 1 and 2 "Add the parser", line 3 the rename, lines 4 and 5 "Add tracing"; <paramref name="pageHost"/> records what it shows.</summary>
    private static BlameViewModel ThreeRuns(out RecordingPageHost pageHost)
    {
        var tracing = BlameCommitOf(AddTracingSha, "Jonas Berg", 4, "src/Parser.cs");
        var repository = new PageFakeRepository
        {
            Blame = (_, _) => new BlameResult(
            [
                new("namespace History;", Parser),
                new(string.Empty, Parser),
                new("public sealed class Reader", Renamed),
                new("{", tracing),
                new("}", tracing),
            ]),
        };
        pageHost = new RecordingPageHost(repository);
        return new BlameViewModel(pageHost, new BlameRequest("src/Reader.cs", null));
    }

    private static BlameTextEditor Text(BlameView view) =>
        view.GetVisualDescendants().OfType<BlameTextEditor>().Single(control => control.Name == "BlameText");

    private static BlameGutter Gutter(BlameView view) =>
        view.GetVisualDescendants().OfType<BlameGutter>().Single(control => control.Name == "BlameGutter");

    /// <summary><paramref name="count"/> lines of C#.</summary>
    private static string Lines(int count) =>
        string.Join('\n', Enumerable.Range(1, count).Select(line => $"var value{line} = \"line {line}\"; // {line}"));
}
