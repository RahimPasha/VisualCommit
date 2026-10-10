using Avalonia.Media;
using VisualCommit.App.Controls.Diff;
using VisualCommit.Core.Diff;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Syntax highlighting of the scenario's diffs (D64), through <see cref="DiffHighlighter"/>, against
/// the colours of phase 2's test report ("Syntax highlighting") in both themes.
/// </summary>
public class DiffHighlighterTests
{
    public static TheoryData<bool> Themes => [true, false];

    [Theory]
    [MemberData(nameof(Themes))]
    public async Task The_calculator_has_the_keyword_function_parameter_and_operator_colours_of_the_report(bool dark)
    {
        var diffs = await DiffScenario.DiffsAsync();
        var syntax = DiffHighlighter.Tokenize(diffs.Calculator, "src/Calculator.cs", TestContext.Current.CancellationToken);
        Assert.NotNull(syntax);

        var keyword = Color.Parse(dark ? "#569CD6" : "#0000FF");
        var control = Color.Parse(dark ? "#C586C0" : "#AF00DB");
        var function = Color.Parse(dark ? "#DCDCAA" : "#795E26");
        var variable = Color.Parse(dark ? "#9CDCFE" : "#001080");
        var @operator = Color.Parse(dark ? "#D4D4D4" : "#000000");

        // Context 13/13 "    public int Add(int a, int b)", on both sides.
        var add = new DiffLineRef(0, 1);
        foreach (var version in new[] { DiffVersion.Old, DiffVersion.New })
        {
            AssertStyle(keyword, syntax, add, version, 4, dark, "public");
            AssertStyle(keyword, syntax, add, version, 11, dark, "int");
            AssertStyle(function, syntax, add, version, 15, dark, "Add");
            AssertStyle(variable, syntax, add, version, 23, dark, "a");
            AssertStyle(variable, syntax, add, version, 30, dark, "b");
            AssertStyle(null, syntax, add, version, 18, dark, "(");
            AssertStyle(null, syntax, add, version, 31, dark, ")");
        }

        // Context 14/14 "    {".
        AssertStyle(null, syntax, new DiffLineRef(0, 2), DiffVersion.New, 4, dark, "{");

        // Removed 15 "        return a + b;" (old side) and added 15 "        return a + b + _offset;" (new side).
        var removed = new DiffLineRef(0, 3);
        AssertStyle(control, syntax, removed, DiffVersion.Old, 8, dark, "return");
        AssertStyle(variable, syntax, removed, DiffVersion.Old, 15, dark, "a");
        AssertStyle(@operator, syntax, removed, DiffVersion.Old, 17, dark, "+");
        AssertStyle(null, syntax, removed, DiffVersion.Old, 20, dark, ";");
        var added = new DiffLineRef(0, 4);
        AssertStyle(control, syntax, added, DiffVersion.New, 8, dark, "return");
        AssertStyle(@operator, syntax, added, DiffVersion.New, 21, dark, "+");
        AssertStyle(variable, syntax, added, DiffVersion.New, 23, dark, "_offset");
        AssertStyle(null, syntax, added, DiffVersion.New, 30, dark, ";");

        // Context 30/30 "        return a / b;".
        AssertStyle(@operator, syntax, new DiffLineRef(1, 1), DiffVersion.Old, 17, dark, "/");

        // Added 33 "    public int Negate(int a) => -a;" and 35 "    public int Square(int a) => a * a;".
        var negate = new DiffLineRef(1, 4);
        AssertStyle(function, syntax, negate, DiffVersion.New, 15, dark, "Negate");
        AssertStyle(@operator, syntax, negate, DiffVersion.New, 29, dark, "=>");
        AssertStyle(@operator, syntax, negate, DiffVersion.New, 32, dark, "-");
        var square = new DiffLineRef(1, 6);
        AssertStyle(function, syntax, square, DiffVersion.New, 15, dark, "Square");
        AssertStyle(@operator, syntax, square, DiffVersion.New, 34, dark, "*");

        // Context 32/36 "}", the class's closing brace.
        AssertStyle(null, syntax, new DiffLineRef(1, 7), DiffVersion.Old, 0, dark, "} (old)");
        AssertStyle(null, syntax, new DiffLineRef(1, 7), DiffVersion.New, 0, dark, "} (new)");

        // A side has no tokens for the other side's lines.
        Assert.Empty(syntax.RunsOf(added, DiffVersion.Old, dark));
        Assert.Empty(syntax.RunsOf(removed, DiffVersion.New, dark));
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public async Task Markdown_headings_are_bold_in_the_heading_colour_and_prose_is_plain(bool dark)
    {
        var diffs = await DiffScenario.DiffsAsync();
        var syntax = DiffHighlighter.Tokenize(diffs.ReadmeUnstaged, "README.md", TestContext.Current.CancellationToken);
        Assert.NotNull(syntax);
        var heading = Color.Parse(dark ? "#569CD6" : "#800000");

        // "# Calculator" (context 1) and "## Usage" (context 5).
        foreach (var (line, length) in new[] { (new DiffLineRef(0, 0), "# Calculator".Length), (new DiffLineRef(0, 5), "## Usage".Length) })
        {
            for (var column = 0; column < length; column++)
            {
                var style = syntax.StyleAt(line, DiffVersion.New, column, dark);
                Assert.Equal(heading, style.Foreground);
                Assert.True(style.Bold, $"Column {column} of the heading on line {line} is not bold.");
            }
        }

        // "A small calculator for the visual checks." (added 3).
        var prose = new DiffLineRef(0, 3);
        for (var column = 0; column < "A small calculator for the visual checks.".Length; column++)
        {
            Assert.Equal(SyntaxStyle.Plain, syntax.StyleAt(prose, DiffVersion.New, column, dark));
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public async Task A_python_docstring_has_the_string_colour_and_def_and_the_function_name_theirs(bool dark)
    {
        var diffs = await DiffScenario.DiffsAsync();
        var syntax = DiffHighlighter.Tokenize(diffs.Helpers, "src/helpers.py", TestContext.Current.CancellationToken);
        Assert.NotNull(syntax);
        var @string = Color.Parse(dark ? "#CE9178" : "#A31515");

        // Removed 1 and added 1: the docstrings, quotes included.
        foreach (var (line, version, text) in new[]
        {
            (new DiffLineRef(0, 0), DiffVersion.Old, "\"\"\"Small helpers for the calculator.\"\"\""),
            (new DiffLineRef(0, 1), DiffVersion.New, "\"\"\"Helpers shared by the calculator.\"\"\""),
        })
        {
            for (var column = 0; column < text.Length; column++)
            {
                Assert.Equal(@string, syntax.StyleAt(line, version, column, dark).Foreground);
            }
        }

        // Context 4/4 "def clamp(value, low, high):".
        var clamp = new DiffLineRef(0, 4);
        AssertStyle(Color.Parse(dark ? "#569CD6" : "#0000FF"), syntax, clamp, DiffVersion.New, 0, dark, "def");
        AssertStyle(Color.Parse(dark ? "#DCDCAA" : "#795E26"), syntax, clamp, DiffVersion.New, 4, dark, "clamp");
    }

    [Fact]
    public async Task A_file_without_a_grammar_or_without_a_path_is_not_tokenized()
    {
        var diffs = await DiffScenario.DiffsAsync();

        Assert.Null(DiffHighlighter.Tokenize(diffs.OldNotes, "docs/old-notes.txt", TestContext.Current.CancellationToken));
        Assert.Null(DiffHighlighter.Tokenize(diffs.Calculator, null, TestContext.Current.CancellationToken));
        Assert.Null(DiffHighlighter.Tokenize(diffs.Calculator, "Makefile", TestContext.Current.CancellationToken));
        Assert.False(DiffHighlighter.HasGrammar("docs/old-notes.txt"));
        Assert.True(DiffHighlighter.HasGrammar("src/Calculator.cs"));
    }

    [Fact]
    public async Task Tokenizing_can_be_cancelled()
    {
        var diffs = await DiffScenario.DiffsAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.Throws<OperationCanceledException>(() => DiffHighlighter.Tokenize(diffs.Calculator, "src/Calculator.cs", cancellation.Token));
    }

    private static void AssertStyle(Color? expected, DiffSyntax syntax, DiffLineRef line, DiffVersion version, int column, bool dark, string what)
    {
        var style = syntax.StyleAt(line, version, column, dark);
        Assert.True(
            style.Foreground == expected,
            $"'{what}' at column {column} of line {line} ({version}, {(dark ? "dark" : "light")}): expected {expected?.ToString() ?? "the main text colour"}, found {style.Foreground?.ToString() ?? "the main text colour"}.");
        Assert.False(style.Bold, $"'{what}' is bold.");
    }
}
