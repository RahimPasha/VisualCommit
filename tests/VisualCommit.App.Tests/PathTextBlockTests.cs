using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using VisualCommit.App.Controls;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// A long folder in the welcome page's errors and the scan's folder line breaks only after a
/// directory separator or at a space (docs/phase-6-design.md 13.2, 13.5, D227): phase 1's 11a broke
/// <c>…\VisualCommit.Tests\plain-a373a2259cd0</c> at its hyphen. A text that is one path and
/// nothing else (<see cref="PathTextBlock.IsOnePath"/>, the first-run screen's path) breaks only
/// after a separator, also across a space: phase 8a's gate saw the stand-in's path break at the
/// space of <c>Visual Git</c> and after the hyphen of <c>net10.0-windows</c>. The text the block
/// holds, its automation name and what tests read stay as given.
/// </summary>
public class PathTextBlockTests
{
    private const string NoBreak = "\uFEFF";
    private const string BreakAfter = "\u200B";

    /// <summary>13.5's folder: phase 1's 11a with another user's name.</summary>
    private const string NotARepository = @"C:\Users\someone\AppData\Local\Temp\VisualCommit.Tests\plain-a373a2259cd0 is not a git repository.";

    /// <summary>
    /// The stand-in's path in phase 8a's real-window pass (launches 8 and 9), which the first-run
    /// screen's <c>GitSetupPath</c> showed alone: as running text it broke at the space of
    /// <c>Visual Git</c> and after the hyphen of <c>net10.0-windows</c>.
    /// </summary>
    private const string StandInPath = @"D:\Projects\Personal\Visual Git\tests\VisualCommit.RealWindowTests\bin\Release\net10.0-windows\visualcommit-git-stand-in.exe";

    /// <summary>
    /// The first-run screen's column in the window at 1100 × 700, as in the gate's captures: the
    /// graph area's 440 less the screen's margins of 24 (under its <c>MaxWidth</c> of 520).
    /// </summary>
    private const double FirstRunWidth = 392;

    [Fact]
    public void A_drive_path_is_marked_to_break_only_after_its_separators()
    {
        Assert.Equal(
            "C" + NoBreak + ":" + NoBreak + @"\" + BreakAfter + "a" + NoBreak + "b" + NoBreak + @"\" + BreakAfter + "c",
            PathTextBlock.BreakOnlyAfterSeparators(@"C:\ab\c"));

        // The forward slashes of a drive path, and the drive path of a file URL; the text around a
        // path keeps its own breaks, and the path ends at a space.
        Assert.Equal("d" + NoBreak + ":" + NoBreak + "/" + BreakAfter + "x", PathTextBlock.BreakOnlyAfterSeparators("d:/x"));
        Assert.Equal(
            "file:///C" + NoBreak + ":" + NoBreak + "/" + BreakAfter + "r" + NoBreak + "/" + BreakAfter + "x",
            PathTextBlock.BreakOnlyAfterSeparators("file:///C:/r/x"));
        Assert.Equal(
            "In C" + NoBreak + ":" + NoBreak + @"\" + BreakAfter + "x is not a git repository.",
            PathTextBlock.BreakOnlyAfterSeparators(@"In C:\x is not a git repository."));

        // A letter before the drive letter makes no drive path (a word such as "abc:\x").
        Assert.Equal(@"abc:\x", PathTextBlock.BreakOnlyAfterSeparators(@"abc:\x"));
    }

    [Fact]
    public void A_unc_path_and_a_rooted_unix_path_are_marked_and_and_or_is_not()
    {
        // A break may come after every separator but the first of a UNC path's two leading backslashes.
        Assert.Equal(
            @"\" + NoBreak + @"\" + BreakAfter + "s" + NoBreak + @"\" + BreakAfter + "d",
            PathTextBlock.BreakOnlyAfterSeparators(@"\\s\d"));
        Assert.Equal("/" + BreakAfter + "t" + NoBreak + "/" + BreakAfter + "x", PathTextBlock.BreakOnlyAfterSeparators("/t/x"));

        // A Unix path after a space, a quote or an opening bracket; "and/or" is no path.
        Assert.Equal("at /" + BreakAfter + "a", PathTextBlock.BreakOnlyAfterSeparators("at /a"));
        Assert.Equal("'/" + BreakAfter + "a'", PathTextBlock.BreakOnlyAfterSeparators("'/a'"));
        Assert.Equal("(/" + BreakAfter + "a" + NoBreak + ")", PathTextBlock.BreakOnlyAfterSeparators("(/a)"));
        Assert.Equal("this and/or that", PathTextBlock.BreakOnlyAfterSeparators("this and/or that"));
    }

    [Fact]
    public void A_url_keeps_its_breaks_and_a_surrogate_pair_stays_whole()
    {
        Assert.Equal("fetch https://example.com/team/x.git now", PathTextBlock.BreakOnlyAfterSeparators("fetch https://example.com/team/x.git now"));

        const string Emoji = "😀";
        var marked = PathTextBlock.BreakOnlyAfterSeparators(@"C:\x\" + Emoji + "y");
        Assert.Contains(Emoji, marked, StringComparison.Ordinal);
        Assert.Equal(@"C:\x\" + Emoji + "y", DrawnLines.WithoutMarks(marked));
    }

    [Fact]
    public void The_text_comes_back_without_the_marks()
    {
        var texts = new[]
        {
            NotARepository,
            @"fatal: 'C:/Users/someone/AppData/Local/Temp/clones-0123456789ab/does-not-exist' does not appear to be a git repository",
            @"Found 9 repositories in \\server\share\workspace-0123456789ab.",
            "Found 9 repositories in /tmp/VisualCommit.Tests/workspace-0123456789ab.",
        };
        Assert.All(texts, text => Assert.Equal(text, DrawnLines.WithoutMarks(PathTextBlock.BreakOnlyAfterSeparators(text))));
    }

    [Fact]
    public void One_path_is_marked_from_its_first_character_to_its_last_where_running_text_ends_it_at_a_space()
    {
        // A space and a hyphen inside a name get the marks of any other character.
        Assert.Equal(
            "C" + NoBreak + ":" + NoBreak + @"\" + BreakAfter + "a" + NoBreak + " " + NoBreak + "b" + NoBreak + @"\" + BreakAfter + "c" + NoBreak + "-" + NoBreak + "d",
            PathTextBlock.MarkAsOnePath(@"C:\a b\c-d"));
        Assert.Equal("C" + NoBreak + ":" + NoBreak + @"\" + BreakAfter + @"a b\c-d", PathTextBlock.BreakOnlyAfterSeparators(@"C:\a b\c-d"));

        // A Unix path with a space, and a UNC path with one: its two leading backslashes stay together.
        Assert.Equal("/" + BreakAfter + "a" + NoBreak + " " + NoBreak + "b" + NoBreak + "/" + BreakAfter + "c", PathTextBlock.MarkAsOnePath("/a b/c"));
        Assert.Equal("/" + BreakAfter + "a b/c", PathTextBlock.BreakOnlyAfterSeparators("/a b/c"));
        Assert.Equal(
            @"\" + NoBreak + @"\" + BreakAfter + "s" + NoBreak + @"\" + BreakAfter + "a" + NoBreak + " " + NoBreak + "b",
            PathTextBlock.MarkAsOnePath(@"\\s\a b"));

        // A text that is no rooted path is still one path: a name, or a relative path.
        Assert.Equal("g" + NoBreak + "i" + NoBreak + "t", PathTextBlock.MarkAsOnePath("git"));
        Assert.Equal("a" + NoBreak + "/" + BreakAfter + "b", PathTextBlock.MarkAsOnePath("a/b"));
    }

    [Fact]
    public void One_path_keeps_a_surrogate_pair_whole_and_the_texts_own_line_breaks_unmarked()
    {
        const string Emoji = "😀";
        var marked = PathTextBlock.MarkAsOnePath(@"C:\a " + Emoji + @" b\" + Emoji);
        Assert.Equal(2, marked.Split(Emoji).Length - 1);
        Assert.Equal(@"C:\a " + Emoji + @" b\" + Emoji, DrawnLines.WithoutMarks(marked));

        // No mark next to a line break, so that CR LF stays one break.
        Assert.Equal("a" + NoBreak + "b\r\nc" + NoBreak + "d", PathTextBlock.MarkAsOnePath("ab\r\ncd"));
        Assert.Equal(string.Empty, PathTextBlock.MarkAsOnePath(string.Empty));
        Assert.Throws<ArgumentNullException>(() => PathTextBlock.MarkAsOnePath(null!));

        var texts = new[] { StandInPath, "/home/jane doe/tools/git", @"\\server\team share\tools\git.exe", "x" };
        Assert.All(texts, text => Assert.Equal(text, DrawnLines.WithoutMarks(PathTextBlock.MarkAsOnePath(text))));
    }

    [AvaloniaFact]
    public void At_392_pixels_phase_1s_error_breaks_after_the_temporary_folder()
    {
        var (host, block) = Show(NotARepository, 392);
        using (host)
        {
            Assert.Equal(
                [@"C:\Users\someone\AppData\Local\Temp\VisualCommit.Tests\", "plain-a373a2259cd0 is not a git repository."],
                DrawnLines.Of(block));
            Assert.Equal(NotARepository, block.Text);
            Assert.Equal(NotARepository, ControlAutomationPeer.CreatePeerForElement(block).GetName());
            TestPictures.Save(host, "path-392");
        }
    }

    [AvaloniaFact]
    public void At_292_pixels_every_line_but_the_last_ends_with_a_separator_or_a_space_and_none_is_the_drive()
    {
        var (host, block) = Show(NotARepository, 292);
        using (host)
        {
            var lines = DrawnLines.Of(block);
            Assert.True(lines.Count > 2, $"Expected the text to need three lines or more: {string.Join(" | ", lines)}");
            Assert.All(lines.Take(lines.Count - 1), line => Assert.True(line.EndsWith('\\') || line.EndsWith(' '), $"The line \"{line}\" ends inside a name."));
            Assert.All(lines, line => Assert.NotEqual("C:", line.Trim()));
            Assert.Contains(lines, line => line.StartsWith(@"C:\Users\", StringComparison.Ordinal));
            Assert.Equal(NotARepository, string.Concat(lines));
            Assert.Contains(lines, line => line.Contains("plain-a373a2259cd0", StringComparison.Ordinal));
            TestPictures.Save(host, "path-292");
        }
    }

    [AvaloniaFact]
    public void A_name_longer_than_the_line_breaks_where_it_must_and_stays_within_the_block()
    {
        var longName = string.Concat(Enumerable.Repeat("very-long-folder-name-", 6));
        var text = $@"C:\src\{longName}end\repo is not a git repository.";
        var (host, block) = Show(text, 292);
        using (host)
        {
            var lines = DrawnLines.Of(block);
            Assert.True(lines.Count >= 3);
            Assert.Equal(text, string.Concat(lines));
            Assert.All(block.TextLayout.TextLines, line => Assert.True(line.Width <= block.Bounds.Width + 0.5, $"A line is {line.Width} wide in a block of {block.Bounds.Width}."));
        }
    }

    [AvaloniaFact]
    public void As_running_text_the_stand_ins_path_breaks_at_its_space_and_after_a_hyphen_as_the_gate_saw()
    {
        var (host, block) = Show(StandInPath, FirstRunWidth);
        using (host)
        {
            Assert.False(block.IsOnePath);
            Assert.Equal(
                [@"D:\Projects\Personal\Visual ", @"Git\tests\VisualCommit.RealWindowTests\bin\Release\net10.0-", @"windows\visualcommit-git-stand-in.exe"],
                DrawnLines.Of(block));
        }
    }

    [AvaloniaFact]
    public void As_one_path_the_stand_ins_path_breaks_only_after_a_separator_at_the_first_run_screens_width()
    {
        var (host, block) = Show(StandInPath, FirstRunWidth, isOnePath: true);
        using (host)
        {
            var lines = DrawnLines.Of(block);
            Assert.True(lines.Count > 1, $"Expected the path to need two lines or more: {string.Join(" | ", lines)}");
            Assert.All(lines.Take(lines.Count - 1), line => Assert.True(line.EndsWith('\\'), $"The line \"{line}\" ends inside a name."));
            Assert.Equal(StandInPath, string.Concat(lines));
            Assert.Contains(lines, line => line.Contains("Visual Git", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.Contains("net10.0-windows", StringComparison.Ordinal));

            // Each line as full as the line allows: a break at an earlier separator would also end
            // every line with one.
            Assert.Equal([@"D:\Projects\Personal\Visual Git\tests\", @"VisualCommit.RealWindowTests\bin\Release\net10.0-windows\", "visualcommit-git-stand-in.exe"], lines);
            Assert.Equal(StandInPath, block.Text);
            Assert.Equal(StandInPath, ControlAutomationPeer.CreatePeerForElement(block).GetName());
            TestPictures.Save(host, "path-one-392");
        }
    }

    [AvaloniaFact]
    public void Turning_one_path_on_and_off_lays_the_text_out_again()
    {
        var (host, block) = Show(StandInPath, FirstRunWidth);
        using (host)
        {
            var running = DrawnLines.Of(block);

            block.IsOnePath = true;
            DiffHost.Settle();
            var whole = DrawnLines.Of(block);
            Assert.NotEqual(running, whole);
            Assert.All(whole.Take(whole.Count - 1), line => Assert.True(line.EndsWith('\\'), $"The line \"{line}\" ends inside a name."));

            block.IsOnePath = false;
            DiffHost.Settle();
            Assert.Equal(running, DrawnLines.Of(block));
        }
    }

    [AvaloniaFact]
    public void As_one_path_a_unix_path_with_a_space_breaks_only_after_its_slashes_where_running_text_breaks_at_the_space()
    {
        const string Text = "/home/jane doe/tools/git";
        var (host, block) = Show(Text, 80, isOnePath: true);
        using (host)
        {
            Assert.Equal(["/home/", "jane doe/", "tools/git"], DrawnLines.Of(block));
        }

        (host, block) = Show(Text, 80);
        using (host)
        {
            Assert.Equal(["/home/jane ", "doe/tools/git"], DrawnLines.Of(block));
        }
    }

    [AvaloniaFact]
    public void As_one_path_a_name_longer_than_the_line_breaks_where_it_must_and_stays_within_the_block()
    {
        var longName = string.Concat(Enumerable.Repeat("very long-folder name ", 6));
        foreach (var text in new[] { $@"C:\src\{longName}end\git.exe", longName + "end" })
        {
            var (host, block) = Show(text, 292, isOnePath: true);
            using (host)
            {
                var lines = DrawnLines.Of(block);
                Assert.True(lines.Count >= 3);
                Assert.Equal(text, string.Concat(lines));
                Assert.All(block.TextLayout.TextLines, line => Assert.True(line.Width <= block.Bounds.Width + 0.5, $"A line is {line.Width} wide in a block of {block.Bounds.Width}."));
            }
        }
    }

    [AvaloniaFact]
    public void As_one_path_an_empty_or_missing_text_is_drawn_as_by_a_text_block()
    {
        foreach (var text in new[] { string.Empty, null })
        {
            var path = new PathTextBlock { Text = text, IsOnePath = true, FontSize = 12.5, TextWrapping = TextWrapping.Wrap };
            var plain = new TextBlock { Text = text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap };
            using var host = DiffHost.Show(new StackPanel { Children = { path, plain } }, 600, 200, ThemeVariant.Dark);

            Assert.Equal(plain.Bounds.Size, path.Bounds.Size);
            Assert.Equal(DrawnLines.Of(plain), DrawnLines.Of(path));
            Assert.Equal(text, path.Text);
        }
    }

    [AvaloniaFact]
    public void A_short_path_is_as_wide_as_in_a_text_block()
    {
        const string Text = @"C:\src\api is not a git repository.";
        var path = new PathTextBlock { Text = Text, FontSize = 12.5, HorizontalAlignment = HorizontalAlignment.Left };
        var plain = new TextBlock { Text = Text, FontSize = 12.5, HorizontalAlignment = HorizontalAlignment.Left };
        using var host = DiffHost.Show(new StackPanel { Children = { path, plain } }, 600, 200, ThemeVariant.Dark);

        Assert.InRange(path.Bounds.Width, plain.Bounds.Width - 0.5, plain.Bounds.Width + 0.5);
        Assert.Equal([Text], DrawnLines.Of(path));
    }

    [AvaloniaFact]
    public void The_error_style_applies_to_it()
    {
        var block = new PathTextBlock { Text = @"C:\x is not a git repository.", Classes = { "error" } };
        using var host = DiffHost.Show(block, 400, 100, ThemeVariant.Dark);

        // The dark danger of the contrast round (D410).
        DiffColors.AssertBrush(Color.Parse("#EA7D86"), block.Foreground, "the error's colour");
    }

    /// <summary>
    /// The welcome page's error at <paramref name="width"/>: a wrapping, centred <see cref="PathTextBlock"/>
    /// at the window's 12.5; with <paramref name="isOnePath"/>, the first-run screen's path in its place.
    /// </summary>
    private static (DiffHost Host, PathTextBlock Block) Show(string text, double width, bool isOnePath = false)
    {
        var block = new PathTextBlock
        {
            Text = text,
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Width = width,
            IsOnePath = isOnePath,
        };
        if (!isOnePath)
        {
            block.Classes.Add("error");
        }

        var host = DiffHost.Show(new StackPanel { Margin = new Avalonia.Thickness(24), Children = { block } }, 1100, 700, ThemeVariant.Dark);
        return (host, block);
    }
}
