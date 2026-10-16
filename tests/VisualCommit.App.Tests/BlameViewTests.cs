using Avalonia.Headless.XUnit;
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

    /// <summary><paramref name="count"/> lines of C#.</summary>
    private static string Lines(int count) =>
        string.Join('\n', Enumerable.Range(1, count).Select(line => $"var value{line} = \"line {line}\"; // {line}"));
}
