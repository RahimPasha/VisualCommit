using System.Text;
using VisualCommit.Core.Diff;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary><see cref="DiffParser"/> and <see cref="WordDiff"/>: reading git's unified diff, and the words that differ in a pair of lines.</summary>
public class DiffParserTests
{
    [Fact]
    public void A_modified_file_gives_its_header_hunks_and_numbered_lines()
    {
        var diff = DiffParser.Parse(
            "diff --git a/src/Calculator.cs b/src/Calculator.cs\n" +
            "index 504fe1f..a57e0c2 100644\n" +
            "--- a/src/Calculator.cs\n" +
            "+++ b/src/Calculator.cs\n" +
            "@@ -12,7 +12,7 @@ public sealed class Calculator\n" +
            " \n" +
            "     public int Add(int a, int b)\n" +
            "     {\n" +
            "-        return a + b;\n" +
            "+        return a + b + _offset;\n" +
            "     }\n" +
            " \n" +
            "     public int Subtract(int a, int b)\n" +
            "@@ -29,4 +29,6 @@ public sealed class Calculator\n" +
            "     {\n" +
            "         return a / b;\n" +
            "     }\n" +
            "+\n" +
            "+    public int Negate(int a) => -a;\n" +
            " }\n");

        Assert.Equal(4, diff.HeaderLines.Count);
        Assert.False(diff.IsBinary);
        Assert.False(diff.IsNewFile);
        Assert.False(diff.IsDeletedFile);
        Assert.Equal("100644", diff.OldMode);
        Assert.Equal("100644", diff.NewMode);
        Assert.Equal(2, diff.Hunks.Count);

        var first = diff.Hunks[0];
        Assert.Equal("@@ -12,7 +12,7 @@ public sealed class Calculator", first.Header);
        Assert.Equal((12, 7, 12, 7), (first.OldStart, first.OldCount, first.NewStart, first.NewCount));
        Assert.Equal(
            ["C 12 12 ", "C 13 13     public int Add(int a, int b)", "C 14 14     {", "R 15 -         return a + b;", "A - 15         return a + b + _offset;", "C 16 16     }", "C 17 17 ", "C 18 18     public int Subtract(int a, int b)"],
            first.Lines.Select(Describe));

        var second = diff.Hunks[1];
        Assert.Equal(["C 29 29     {", "C 30 30         return a / b;", "C 31 31     }", "A - 32 ", "A - 33     public int Negate(int a) => -a;", "C 32 34 }"], second.Lines.Select(Describe));
        Assert.Equal(14, diff.LineCount);
        Assert.Equal(3, diff.AddedCount);
        Assert.Equal(1, diff.RemovedCount);
        Assert.False(diff.IsVeryLarge);
    }

    [Fact]
    public void A_new_file_from_no_index_and_a_deleted_file_are_recognised()
    {
        var added = DiffParser.Parse(
            "diff --git a/docs/guide.md b/docs/guide.md\nnew file mode 100644\nindex 0000000..9a1b2c3\n--- /dev/null\n+++ b/docs/guide.md\n@@ -0,0 +1,2 @@\n+# Guide\n+\n");
        Assert.True(added.IsNewFile);
        Assert.Null(added.OldMode);
        Assert.Equal("100644", added.NewMode);
        Assert.Equal(["A - 1 # Guide", "A - 2 "], added.Hunks.Single().Lines.Select(Describe));

        var deleted = DiffParser.Parse(
            "diff --git a/docs/old-notes.txt b/docs/old-notes.txt\ndeleted file mode 100644\nindex 637d51e..0000000\n--- a/docs/old-notes.txt\n+++ /dev/null\n@@ -1,2 +0,0 @@\n-Old notes.\n-They are out of date.\n");
        Assert.True(deleted.IsDeletedFile);
        Assert.Equal("100644", deleted.OldMode);
        Assert.Null(deleted.NewMode);
        Assert.Equal(["R 1 - Old notes.", "R 2 - They are out of date."], deleted.Hunks.Single().Lines.Select(Describe));
    }

    [Fact]
    public void A_binary_file_has_no_hunks()
    {
        var diff = DiffParser.Parse("diff --git a/data/blob.bin b/data/blob.bin\nindex c866266..187cd58 100644\nBinary files a/data/blob.bin and b/data/blob.bin differ\n");

        Assert.True(diff.IsBinary);
        Assert.Empty(diff.Hunks);
        Assert.False(diff.IsEmpty);
    }

    [Fact]
    public void Empty_output_is_an_empty_diff()
    {
        Assert.True(DiffParser.Parse(string.Empty).IsEmpty);
        Assert.Same(FileDiff.Empty, DiffParser.Parse(string.Empty));
    }

    [Fact]
    public void No_newline_at_end_of_file_marks_the_line_before_it()
    {
        var diff = DiffParser.Parse("diff --git a/x b/x\n--- a/x\n+++ b/x\n@@ -1 +1 @@\n-old\n\\ No newline at end of file\n+new\n\\ No newline at end of file\n");

        var lines = diff.Hunks.Single().Lines;
        Assert.Equal(2, lines.Count);
        Assert.True(lines[0].NoNewlineAtEnd);
        Assert.True(lines[1].NoNewlineAtEnd);
        Assert.Equal((1, 1, 1, 1), (diff.Hunks[0].OldStart, diff.Hunks[0].OldCount, diff.Hunks[0].NewStart, diff.Hunks[0].NewCount));
    }

    [Fact]
    public void A_carriage_return_stays_in_the_raw_line_and_leaves_the_text()
    {
        var diff = DiffParser.Parse("diff --git a/x b/x\n--- a/x\n+++ b/x\n@@ -1 +1 @@\n-old\r\n+new\r\n");

        var removed = diff.Hunks.Single().Lines[0];
        Assert.Equal("old\r", removed.Raw);
        Assert.Equal("old", removed.Text);
    }

    [Fact]
    public void Utf8_is_decoded_for_display_and_kept_as_bytes_for_patches()
    {
        var latin1 = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes("diff --git a/x b/x\n--- a/x\n+++ b/x\n@@ -1 +1 @@\n-café\n+naïve\n"));

        var lines = DiffParser.Parse(latin1).Hunks.Single().Lines;

        Assert.Equal("café", lines[0].Text);
        Assert.Equal("naïve", lines[1].Text);
        Assert.Equal(Encoding.UTF8.GetBytes("naïve"), Encoding.Latin1.GetBytes(lines[1].Raw));
    }

    [Fact]
    public void A_file_that_is_not_utf8_is_shown_as_latin1_and_its_bytes_are_kept()
    {
        // "é" in Latin-1 is the single byte 0xE9, which is not valid UTF-8.
        var latin1 = "diff --git a/x b/x\n--- a/x\n+++ b/x\n@@ -1 +1 @@\n-café\n+cafés\n";

        var lines = DiffParser.Parse(latin1).Hunks.Single().Lines;

        Assert.Equal("café", lines[0].Text);
        Assert.Equal(new byte[] { (byte)'c', (byte)'a', (byte)'f', 0xE9 }, Encoding.Latin1.GetBytes(lines[0].Raw));
    }

    [Fact]
    public void A_count_left_out_of_a_hunk_header_is_one()
    {
        Assert.Equal((3, 1, 3, 1), DiffParser.ParseRange("@@ -3 +3 @@"));
        Assert.Equal((0, 0, 1, 4), DiffParser.ParseRange("@@ -0,0 +1,4 @@"));
    }

    [Fact]
    public void A_diff_of_more_than_10000_lines_is_very_large()
    {
        var text = new StringBuilder("diff --git a/x b/x\n--- a/x\n+++ b/x\n@@ -1,5001 +1,5001 @@\n");
        for (var i = 0; i < 5001; i++)
        {
            text.Append("-Line\n");
        }

        for (var i = 0; i < 5001; i++)
        {
            text.Append("+Row\n");
        }

        var diff = DiffParser.Parse(text.ToString());

        Assert.Equal(10_002, diff.LineCount);
        Assert.True(diff.IsVeryLarge);
    }

    [Fact]
    public void Words_are_runs_of_letters_digits_and_underscores_runs_of_spaces_and_single_other_characters()
    {
        var line = "  return a+b_2;";
        Assert.Equal(["  ", "return", " ", "a", "+", "b_2", ";"], WordDiff.Words(line).Select(range => line.Substring(range.Start, range.Length)));
    }

    [Fact]
    public void Word_highlights_of_the_readme_line()
    {
        const string removed = "A small calculator for tests.";
        const string added = "A small calculator for the visual checks.";

        var (inRemoved, inAdded) = WordDiff.Compare(removed, added);

        Assert.Equal(["tests"], inRemoved.Select(range => removed.Substring(range.Start, range.Length)));
        Assert.Equal(["the visual checks"], inAdded.Select(range => added.Substring(range.Start, range.Length)));
    }

    [Fact]
    public void Word_highlights_of_the_calculator_line()
    {
        const string removed = "        return a + b;";
        const string added = "        return a + b + _offset;";

        var (inRemoved, inAdded) = WordDiff.Compare(removed, added);

        Assert.Empty(inRemoved);
        Assert.Equal([" + _offset"], inAdded.Select(range => added.Substring(range.Start, range.Length)));
    }

    [Fact]
    public void Word_highlights_of_the_renamed_python_line()
    {
        const string removed = "\"\"\"Small helpers for the calculator.\"\"\"";
        const string added = "\"\"\"Helpers shared by the calculator.\"\"\"";

        var (inRemoved, inAdded) = WordDiff.Compare(removed, added);

        Assert.Equal(["Small", "helpers", "for"], inRemoved.Select(range => removed.Substring(range.Start, range.Length)));
        Assert.Equal(["Helpers", "shared", "by"], inAdded.Select(range => added.Substring(range.Start, range.Length)));
    }

    [Fact]
    public void Lines_that_share_no_word_have_no_highlights()
    {
        Assert.Equal((0, 0), Count(WordDiff.Compare("{", "}")));
        Assert.Equal((0, 0), Count(WordDiff.Compare("alpha beta", "gamma delta")));

        static (int, int) Count((IReadOnlyList<TextRange> Removed, IReadOnlyList<TextRange> Added) ranges) => (ranges.Removed.Count, ranges.Added.Count);
    }

    private static string Describe(DiffLine line) =>
        $"{line.Kind.ToString()[0]} {line.OldNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"} {line.NewNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"} {line.Text}";
}
