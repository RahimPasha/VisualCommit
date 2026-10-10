using VisualCommit.App.Controls.Diff;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Settings;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The diff view's rows (<see cref="DiffLayout"/>), inline and side by side, against the rows that
/// phase 2's test report lists (checks 3, 4 and 11). Rows are written as text:
/// <list type="bullet">
/// <item>a header row as "@ " and git's header line;</item>
/// <item>an inline row as its sign (" ", "-" or "+"), its old and new numbers ("." for none) and its text;</item>
/// <item>a side by side row as its left and right cells, "~" for a filler, each as its sign, its number and its text;</item>
/// <item>a no-newline marker cell as "\" and the sign of the line it follows.</item>
/// </list>
/// </summary>
public class DiffLayoutTests
{
    [Fact]
    public async Task The_calculator_diff_inline_has_the_rows_of_check_3()
    {
        var diffs = await DiffScenario.DiffsAsync();

        var rows = DiffLayout.Build(diffs.Calculator, DiffMode.Inline);

        Assert.Equal(
            [
                "@ @@ -12,7 +12,7 @@ public sealed class Calculator",
                "  12 12 ",
                "  13 13     public int Add(int a, int b)",
                "  14 14     {",
                "- 15 .         return a + b;",
                "+ . 15         return a + b + _offset;",
                "  16 16     }",
                "  17 17 ",
                "  18 18     public int Subtract(int a, int b)",
                "@ @@ -29,4 +29,8 @@ public sealed class Calculator",
                "  29 29     {",
                "  30 30         return a / b;",
                "  31 31     }",
                "+ . 32 ",
                "+ . 33     public int Negate(int a) => -a;",
                "+ . 34 ",
                "+ . 35     public int Square(int a) => a * a;",
                "  32 36 }",
            ],
            rows.Select(Inline));
        Assert.All(rows, row => Assert.Null(row.Right));
        Assert.Equal([0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1], rows.Select(row => row.Hunk));

        // Each cell points at its line of the diff.
        Assert.Equal(new DiffLineRef(0, 4), rows[5].Left!.Line);
        Assert.Equal(new DiffLineRef(1, 6), rows[16].Left!.Line);
        Assert.Equal(35, rows[16].Left!.Number);
        Assert.Equal(15, rows[4].Left!.Number);
    }

    [Fact]
    public async Task The_calculator_diff_side_by_side_faces_line_15_with_line_15_and_pads_the_left_with_fillers()
    {
        var diffs = await DiffScenario.DiffsAsync();

        var rows = DiffLayout.Build(diffs.Calculator, DiffMode.SideBySide);

        Assert.Equal(
            [
                "@ @@ -12,7 +12,7 @@ public sealed class Calculator",
                " 12  |  12 ",
                " 13     public int Add(int a, int b) |  13     public int Add(int a, int b)",
                " 14     { |  14     {",
                "-15         return a + b; | +15         return a + b + _offset;",
                " 16     } |  16     }",
                " 17  |  17 ",
                " 18     public int Subtract(int a, int b) |  18     public int Subtract(int a, int b)",
                "@ @@ -29,4 +29,8 @@ public sealed class Calculator",
                " 29     { |  29     {",
                " 30         return a / b; |  30         return a / b;",
                " 31     } |  31     }",
                "~ | +32 ",
                "~ | +33     public int Negate(int a) => -a;",
                "~ | +34 ",
                "~ | +35     public int Square(int a) => a * a;",
                " 32 } |  36 }",
            ],
            rows.Select(SideBySide));

        // A side shows only its own number.
        Assert.All(rows.Where(row => row.Left is not null), row => Assert.Null(row.Left!.NewNumber));
        Assert.All(rows.Where(row => row.Right is not null), row => Assert.Null(row.Right!.OldNumber));
        Assert.Equal(new DiffLineRef(0, 3), rows[4].Left!.Line);
        Assert.Equal(new DiffLineRef(0, 4), rows[4].Right!.Line);
    }

    [Fact]
    public void The_unstaged_calculator_diff_after_staging_square_has_the_rows_of_check_11()
    {
        var diff = DiffScenario.Parse(
            "diff --git a/src/Calculator.cs b/src/Calculator.cs",
            "index 1111111..2222222 100644",
            "--- a/src/Calculator.cs",
            "+++ b/src/Calculator.cs",
            "@@ -29,5 +29,8 @@ public sealed class Calculator",
            "     {",
            "         return a / b;",
            "     }",
            "+",
            "+    public int Negate(int a) => -a;",
            "+",
            "     public int Square(int a) => a * a;",
            " }");

        Assert.Equal(
            [
                "@ @@ -29,5 +29,8 @@ public sealed class Calculator",
                "  29 29     {",
                "  30 30         return a / b;",
                "  31 31     }",
                "+ . 32 ",
                "+ . 33     public int Negate(int a) => -a;",
                "+ . 34 ",
                "  32 35     public int Square(int a) => a * a;",
                "  33 36 }",
            ],
            DiffLayout.Build(diff, DiffMode.Inline).Select(Inline));

        Assert.Equal(
            [
                "@ @@ -29,5 +29,8 @@ public sealed class Calculator",
                " 29     { |  29     {",
                " 30         return a / b; |  30         return a / b;",
                " 31     } |  31     }",
                "~ | +32 ",
                "~ | +33     public int Negate(int a) => -a;",
                "~ | +34 ",
                " 32     public int Square(int a) => a * a; |  35     public int Square(int a) => a * a;",
                " 33 } |  36 }",
            ],
            DiffLayout.Build(diff, DiffMode.SideBySide).Select(SideBySide));
    }

    [Fact]
    public async Task An_added_file_side_by_side_has_fillers_on_the_left_and_a_deleted_file_on_the_right()
    {
        var diffs = await DiffScenario.DiffsAsync();

        Assert.Equal(
            [
                "@ @@ -0,0 +1,4 @@",
                "~ | +1 # Guide",
                "~ | +2 ",
                "~ | +3 Add numbers with Add.",
                "~ | +4 Subtract them with Subtract.",
            ],
            DiffLayout.Build(diffs.Guide, DiffMode.SideBySide).Select(SideBySide));

        Assert.Equal(
            [
                "@ @@ -1,2 +0,0 @@",
                "-1 Old notes. | ~",
                "-2 They are out of date. | ~",
            ],
            DiffLayout.Build(diffs.OldNotes, DiffMode.SideBySide).Select(SideBySide));

        Assert.Equal(
            [
                "@ @@ -1,2 +0,0 @@",
                "- 1 . Old notes.",
                "- 2 . They are out of date.",
            ],
            DiffLayout.Build(diffs.OldNotes, DiffMode.Inline).Select(Inline));
    }

    [Fact]
    public void A_no_newline_marker_is_a_row_of_its_own_after_its_line_inline_and_after_the_change_side_by_side()
    {
        var diff = DiffScenario.Parse(
            "diff --git a/a.txt b/a.txt",
            "index 1111111..2222222 100644",
            "--- a/a.txt",
            "+++ b/a.txt",
            "@@ -1,2 +1,3 @@",
            " first",
            "-second",
            "\\ No newline at end of file",
            "+second, changed",
            "+third",
            "\\ No newline at end of file");

        var inline = DiffLayout.Build(diff, DiffMode.Inline);
        Assert.Equal(
            [
                "@ @@ -1,2 +1,3 @@",
                "  1 1 first",
                "- 2 . second",
                "\\-",
                "+ . 2 second, changed",
                "+ . 3 third",
                "\\+",
            ],
            inline.Select(Inline));
        Assert.Equal(DiffLayout.NoNewlineText, inline[3].Left!.Text);
        Assert.Null(inline[3].Left!.Number);
        Assert.Equal(new DiffLineRef(0, 1), inline[3].Left!.Line);

        Assert.Equal(
            [
                "@ @@ -1,2 +1,3 @@",
                " 1 first |  1 first",
                "-2 second | +2 second, changed",
                "~ | +3 third",
                "\\- | \\+",
            ],
            DiffLayout.Build(diff, DiffMode.SideBySide).Select(SideBySide));
    }

    [Fact]
    public void A_marker_on_one_side_of_a_change_leaves_a_filler_on_the_other_and_a_context_marker_shows_on_both()
    {
        var oneSide = DiffScenario.Parse(
            "@@ -1 +1,2 @@",
            "-last",
            "\\ No newline at end of file",
            "+last",
            "+new");
        Assert.Equal(
            ["@ @@ -1 +1,2 @@", "-1 last | +1 last", "~ | +2 new", "\\- | ~"],
            DiffLayout.Build(oneSide, DiffMode.SideBySide).Select(SideBySide));

        var context = DiffScenario.Parse(
            "@@ -1,2 +1,2 @@",
            "-a",
            "+b",
            " c",
            "\\ No newline at end of file");
        Assert.Equal(
            ["@ @@ -1,2 +1,2 @@", "-1 a | +1 b", " 2 c |  2 c", "\\  | \\ "],
            DiffLayout.Build(context, DiffMode.SideBySide).Select(SideBySide));
        Assert.Equal(
            ["@ @@ -1,2 +1,2 @@", "- 1 . a", "+ . 1 b", "  2 2 c", "\\ "],
            DiffLayout.Build(context, DiffMode.Inline).Select(Inline));
    }

    [Fact]
    public void Removed_and_added_lines_that_git_interleaves_are_paired_in_order()
    {
        var diff = DiffScenario.Parse(
            "@@ -1,3 +1,3 @@",
            "-a",
            "+A",
            "-b",
            "+B",
            " c");

        Assert.Equal(
            ["@ @@ -1,3 +1,3 @@", "-1 a | +1 A", "-2 b | +2 B", " 3 c |  3 c"],
            DiffLayout.Build(diff, DiffMode.SideBySide).Select(SideBySide));
    }

    [Fact]
    public void An_empty_diff_has_no_rows()
    {
        Assert.Empty(DiffLayout.Build(FileDiff.Empty, DiffMode.Inline));
        Assert.Empty(DiffLayout.Build(FileDiff.Empty, DiffMode.SideBySide));
    }

    private static string Inline(DiffDisplayRow row) => row.Kind switch
    {
        DiffRowKind.HunkHeader => "@ " + row.HeaderText,
        DiffRowKind.NoNewline => "\\" + Sign(row.Left!.Kind),
        _ => $"{Sign(row.Left!.Kind)} {Number(row.Left.OldNumber)} {Number(row.Left.NewNumber)} {row.Left.Text}",
    };

    private static string SideBySide(DiffDisplayRow row) => row.Kind switch
    {
        DiffRowKind.HunkHeader => "@ " + row.HeaderText,
        DiffRowKind.NoNewline => $"{Marker(row.Left)} | {Marker(row.Right)}",
        _ => $"{Cell(row.Left)} | {Cell(row.Right)}",
    };

    private static string Cell(DiffCell? cell) => cell is null ? "~" : $"{Sign(cell.Kind)}{cell.Number} {cell.Text}";

    private static string Marker(DiffCell? cell) => cell is null ? "~" : "\\" + Sign(cell.Kind);

    private static string Sign(DiffLineKind kind) => kind switch
    {
        DiffLineKind.Added => "+",
        DiffLineKind.Removed => "-",
        _ => " ",
    };

    private static string Number(int? number) => number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ".";
}
