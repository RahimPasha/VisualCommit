using System.Text;
using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="BlameParser"/> (docs/phase-5-design.md 4.2, 0.1): <c>git blame --porcelain</c> as
/// Git 2.36 prints it, read as Latin-1 (D66).
/// </summary>
public class BlameParserTests
{
    private static readonly string A = new('a', 40);
    private static readonly string B = new('b', 40);
    private static readonly string Zero = new('0', 40);

    /// <summary>The metadata git prints the first time a commit appears.</summary>
    private static string Metadata(string author, string mail, long time, string zone, string summary) =>
        $"author {author}\nauthor-mail <{mail}>\nauthor-time {time}\nauthor-tz {zone}\n"
        + $"committer {author}\ncommitter-mail <{mail}>\ncommitter-time {time}\ncommitter-tz {zone}\nsummary {summary}\n";

    /// <summary>Text as the runner reads it with Latin-1: one character per UTF-8 byte.</summary>
    private static string Latin1(string text) => Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void Each_line_gets_its_commit_from_the_group_it_comes_in()
    {
        var porcelain =
            $"{A} 1 1 2\n" + Metadata("Alice", "alice@example.com", 1767268800, "+0100", "First") + "boundary\nfilename f.txt\n\tone\n"
            + $"{A} 2 2\n\ttwo\n"
            + $"{B} 3 3 1\n" + Metadata("Bob", "bob@example.com", 1767272400, "-0700", "Second") + $"previous {A} f.txt\nfilename f.txt\n\tthree\n"
            + $"{A} 3 4 1\n\tfour\n";

        var result = BlameParser.Parse(porcelain);

        Assert.Equal(["one", "two", "three", "four"], result.Lines.Select(line => line.Text));
        var first = result.Lines[0].Commit;
        Assert.Equal(new BlameCommit(A, "Alice", "alice@example.com", new DateTimeOffset(2026, 1, 1, 13, 0, 0, TimeSpan.FromHours(1)), "First", "f.txt", null, null, IsBoundary: true), first);
        Assert.Equal(TimeSpan.FromHours(1), first.AuthorTime.Offset);
        Assert.Same(first, result.Lines[1].Commit);
        Assert.Same(first, result.Lines[3].Commit);
        Assert.Equal(new BlameCommit(B, "Bob", "bob@example.com", new DateTimeOffset(2026, 1, 1, 6, 0, 0, TimeSpan.FromHours(-7)), "Second", "f.txt", A, "f.txt", IsBoundary: false), result.Lines[2].Commit);
        Assert.False(result.IsEmpty);
        Assert.False(result.IsBinary);
    }

    [Fact]
    public void A_commit_met_under_two_paths_gives_each_line_the_path_of_its_group()
    {
        // Git repeats previous and filename at each group of such a commit; a group without
        // previous has none, even after one that had it.
        var porcelain =
            $"{A} 1 1 1\n" + Metadata("Alice", "a@x", 0, "+0000", "Both") + $"previous {B} old.txt\nfilename new.txt\n\tone\n"
            + $"{A} 1 2 1\nfilename copy.txt\n\ttwo\n"
            + $"{A} 2 3 1\nprevious {B} old.txt\nfilename new.txt\n\tthree\n";

        var lines = BlameParser.Parse(porcelain).Lines;

        Assert.Equal(("new.txt", B, "old.txt"), (lines[0].Commit.Path, lines[0].Commit.PreviousSha, lines[0].Commit.PreviousPath));
        Assert.Equal(("copy.txt", (string?)null, (string?)null), (lines[1].Commit.Path, lines[1].Commit.PreviousSha, lines[1].Commit.PreviousPath));
        Assert.Same(lines[0].Commit, lines[2].Commit);
    }

    [Fact]
    public void Quoted_paths_are_unquoted_and_names_are_read_as_utf8()
    {
        var porcelain =
            $"{A} 1 1 1\n" + Latin1(Metadata("José Núñez", "jose@example.com", 0, "+0000", "Café menu: Straße"))
            + $"previous {B} \"d/caf\\303\\251 \\\"q\\\"\\tx.txt\"\n"
            + "filename \"d/caf\\303\\251 \\\"q\\\"\\tx.txt\"\n\tline\n";

        var commit = BlameParser.Parse(porcelain).Lines[0].Commit;

        Assert.Equal("José Núñez", commit.AuthorName);
        Assert.Equal("Café menu: Straße", commit.Summary);
        Assert.Equal("d/café \"q\"\tx.txt", commit.Path);
        Assert.Equal("d/café \"q\"\tx.txt", commit.PreviousPath);
    }

    [Theory]
    [InlineData("plain.txt", "plain.txt")]
    [InlineData("\"a\\\\b\"", "a\\b")]
    [InlineData("\"\\a\\b\\t\\n\\v\\f\\r\"", "\a\b\t\n\v\f\r")]
    [InlineData("\"\\346\\227\\245.txt\"", "日.txt")]
    [InlineData("\"unfinished\\", "\"unfinished\\")]
    public void Unquote_reads_gits_c_quoting(string quoted, string expected)
    {
        Assert.Equal(expected, BlameParser.Unquote(Latin1(quoted)));
    }

    [Fact]
    public void A_raw_utf8_path_is_decoded()
    {
        // core.quotePath=false leaves non-ASCII bytes alone.
        Assert.Equal("日本語/ßΩ.txt", BlameParser.Unquote(Latin1("日本語/ßΩ.txt")));
    }

    [Fact]
    public void An_empty_message_has_an_empty_summary()
    {
        var porcelain = $"{A} 1 1 1\n" + Metadata("A", "a@x", 0, "+0000", $"({A})") + "filename f.txt\n\tline\n";

        Assert.Equal(string.Empty, BlameParser.Parse(porcelain).Lines[0].Commit.Summary);
    }

    [Fact]
    public void Lines_not_committed_yet_carry_the_zero_id()
    {
        var porcelain =
            $"{Zero} 1 1 1\n" + Metadata("Not Committed Yet", "not.committed.yet", 1791864196, "-0700", "Version of f.txt from f.txt")
            + $"previous {A} f.txt\nfilename f.txt\n\tnew line\n";

        var commit = BlameParser.Parse(porcelain).Lines[0].Commit;

        Assert.True(commit.IsUncommitted);
        Assert.Equal("Not Committed Yet", commit.AuthorName);
        Assert.Equal("not.committed.yet", commit.AuthorEmail);
    }

    [Fact]
    public void Line_bytes_are_kept_a_carriage_return_and_nul_included()
    {
        var porcelain =
            $"{A} 1 1 2\n" + Metadata("A", "a@x", 0, "+0000", "S") + "filename f.txt\n\tcrlf\r\n"
            + $"{A} 2 2\n\tnul\0byte \u00e9\n";

        var result = BlameParser.Parse(porcelain);

        Assert.Equal("crlf\r", result.Lines[0].Text);
        Assert.Equal("nul\0byte \u00e9", result.Lines[1].Text);
        Assert.True(result.IsBinary);
    }

    [Fact]
    public void Empty_output_is_an_empty_file()
    {
        var result = BlameParser.Parse(string.Empty);

        Assert.True(result.IsEmpty);
        Assert.False(result.IsBinary);
    }

    [Fact]
    public void A_sha256_repository_is_read()
    {
        var sha = new string('c', 64);
        var porcelain = $"{sha} 1 1 1\n" + Metadata("A", "a@x", 0, "+0000", "S") + "filename f.txt\n\tline\n";

        Assert.Equal(sha, BlameParser.Parse(porcelain).Lines[0].Commit.Sha);
    }

    [Fact]
    public void A_zone_beyond_fourteen_hours_keeps_its_clock_time_as_utc()
    {
        var porcelain = $"{A} 1 1 1\n" + Metadata("A", "a@x", 1767268800, "+1500", "S") + "filename f.txt\n\tline\n";

        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 0, 0, TimeSpan.Zero), BlameParser.Parse(porcelain).Lines[0].Commit.AuthorTime);
    }

    [Theory]
    [InlineData("not a header\n\tline\n")]
    [InlineData("{A} 1 2 1\nfilename f.txt\n\tline\n")]
    [InlineData("{A} 1 1 1\n\tline\n")]
    [InlineData("{A} 1 1 1\nfilename f.txt\n")]
    [InlineData("{A} 1 1 1\nprevious {A}\nfilename f.txt\n\tline\n")]
    public void Output_that_is_no_blame_is_refused(string template)
    {
        Assert.Throws<FormatException>(() => BlameParser.Parse(template.Replace("{A}", A, StringComparison.Ordinal)));
    }

    [Fact]
    public void The_message_of_a_missing_ignore_revs_file_names_the_file()
    {
        Assert.Equal("dir with space/nope", GitRepository.MissingIgnoreRevsFile("fatal: could not open object name list: dir with space/nope\n"));
        Assert.Null(GitRepository.MissingIgnoreRevsFile("fatal: no such path 'u.txt' in HEAD\n"));
    }
}
