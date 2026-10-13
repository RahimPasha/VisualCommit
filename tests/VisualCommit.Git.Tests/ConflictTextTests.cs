using System.Text;
using VisualCommit.Core.Conflicts;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="ConflictText"/> (docs/phase-4-design.md 11.3, 11.5): the markers as git writes them
/// in each conflict style, the line ends each line keeps, what does not form a whole block, and
/// the texts and choices built from the blocks. Pure: no git. The texts git itself writes are
/// parsed against real git in <see cref="ConflictOperationTests"/>.
/// </summary>
public class ConflictTextTests
{
    private const string MergeStyle =
        "line 1\n" +
        "<<<<<<< HEAD\n" +
        "line 2 ours\n" +
        "=======\n" +
        "line 2 theirs\n" +
        "line 2b theirs\n" +
        ">>>>>>> feature/theirs\n" +
        "line 3\n";

    [Fact]
    public void A_merge_style_block_is_split_into_its_sides_and_labels()
    {
        var text = ConflictText.Parse(MergeStyle);

        Assert.True(text.HasMarkers);
        Assert.Equal(1, text.BlockCount);
        Assert.Equal(7, text.MarkerSize);
        Assert.Collection(
            text.Segments,
            segment => AssertCommon(segment, 0, ["line 1"]),
            segment => Assert.IsType<ConflictBlock>(segment),
            segment => AssertCommon(segment, 7, ["line 3"]));

        var block = text.Blocks[0];
        Assert.Equal(0, block.Index);
        Assert.Equal("HEAD", block.OursLabel);
        Assert.Equal("feature/theirs", block.TheirsLabel);
        Assert.Equal(["line 2 ours"], Texts(block.Ours));
        Assert.Equal(["line 2 theirs", "line 2b theirs"], Texts(block.Theirs));
        Assert.Null(block.Base);
        Assert.Null(block.BaseLabel);
        Assert.Null(block.BaseLine);
        Assert.Equal((1, 3, 6), (block.StartLine, block.SeparatorLine, block.EndLine));
        Assert.Equal((1, 6), (block.FirstLine, block.LineCount));

        // From the start of the "<" line to the end of the ">" line, its line end included.
        Assert.Equal("<<<<<<< HEAD\nline 2 ours\n=======\nline 2 theirs\nline 2b theirs\n>>>>>>> feature/theirs\n", MergeStyle.Substring(block.Offset, block.Length));

        Assert.Equal("line 1\nline 2 ours\nline 3\n", text.OursText);
        Assert.Equal("line 1\nline 2 theirs\nline 2b theirs\nline 3\n", text.TheirsText);
        Assert.Equal(["line 1", "line 2 ours", "line 3"], Texts(text.OursLines));
        Assert.Equal(["line 1", "line 2 theirs", "line 2b theirs", "line 3"], Texts(text.TheirsLines));
    }

    [Theory]
    [InlineData(BlockChoice.Ours, "line 1\nline 2 ours\nline 3\n")]
    [InlineData(BlockChoice.Theirs, "line 1\nline 2 theirs\nline 2b theirs\nline 3\n")]
    [InlineData(BlockChoice.OursThenTheirs, "line 1\nline 2 ours\nline 2 theirs\nline 2b theirs\nline 3\n")]
    [InlineData(BlockChoice.TheirsThenOurs, "line 1\nline 2 theirs\nline 2b theirs\nline 2 ours\nline 3\n")]
    public void A_choice_replaces_the_block_from_its_first_marker_to_its_last(BlockChoice choice, string expected)
    {
        var text = ConflictText.Parse(MergeStyle);

        var chosen = text.Choose(0, choice);

        Assert.Equal(expected, chosen);
        Assert.False(ConflictText.Parse(chosen).HasMarkers);
    }

    [Fact]
    public void A_choice_leaves_the_other_blocks_as_they_are()
    {
        const string Two =
            "a\n<<<<<<< HEAD\nb1\n=======\nb2\n>>>>>>> x\nc\n<<<<<<< HEAD\nd1\n=======\nd2\n>>>>>>> x\ne\n";
        var text = ConflictText.Parse(Two);
        Assert.Equal(2, text.BlockCount);
        Assert.Equal(1, text.Blocks[1].Index);

        var second = text.Choose(1, BlockChoice.Theirs);
        var first = text.Choose(0, BlockChoice.Ours);

        Assert.Equal("a\n<<<<<<< HEAD\nb1\n=======\nb2\n>>>>>>> x\nc\nd2\ne\n", second);
        Assert.Equal("a\nb1\nc\n<<<<<<< HEAD\nd1\n=======\nd2\n>>>>>>> x\ne\n", first);
        var left = ConflictText.Parse(first);
        Assert.Equal(1, left.BlockCount);
        Assert.Equal(0, left.Blocks[0].Index);
        Assert.Equal(["d1"], Texts(left.Blocks[0].Ours));
    }

    [Fact]
    public void A_diff3_block_has_its_base_section_and_label()
    {
        const string Diff3 =
            "l1\n<<<<<<< HEAD\nours\n||||||| a699539\nl2\n=======\ntheirs\n>>>>>>> theirs\nl3\n";

        var text = ConflictText.Parse(Diff3);

        var block = Assert.Single(text.Blocks);
        Assert.Equal("a699539", block.BaseLabel);
        Assert.Equal(["l2"], Texts(block.Base!));
        Assert.Equal(["ours"], Texts(block.Ours));
        Assert.Equal(["theirs"], Texts(block.Theirs));
        Assert.Equal((1, 3, 5, 7), (block.StartLine, block.BaseLine, block.SeparatorLine, block.EndLine));
        Assert.Equal("l1\nours\nl3\n", text.OursText);
        Assert.Equal("l1\ntheirs\nl3\n", text.TheirsText);

        // The base is never part of a choice.
        Assert.Equal("l1\nours\ntheirs\nl3\n", text.Choose(0, BlockChoice.OursThenTheirs));
    }

    [Fact]
    public void A_zdiff3_block_with_an_empty_base_and_an_unlabelled_base_marker_is_whole()
    {
        // zdiff3 moves the lines both sides share out of the block; its markers are diff3's. A
        // base marker can come without a label, and a base section can be empty (both added).
        const string Zdiff3 =
            "shared\n<<<<<<< HEAD:old.txt\nadded ours\n|||||||\n=======\nadded theirs\n>>>>>>> 1a2b3c4 (Add a line)\nshared too\n";

        var text = ConflictText.Parse(Zdiff3);

        var block = Assert.Single(text.Blocks);
        Assert.Equal("HEAD:old.txt", block.OursLabel);
        Assert.Equal(string.Empty, block.BaseLabel);
        Assert.Empty(block.Base!);
        Assert.Equal("1a2b3c4 (Add a line)", block.TheirsLabel);
        Assert.Equal("shared\nadded ours\nshared too\n", text.OursText);
    }

    [Fact]
    public void Crlf_lines_keep_their_line_ends_and_their_markers_are_recognised()
    {
        var crlf = MergeStyle.Replace("\n", "\r\n", StringComparison.Ordinal);

        var text = ConflictText.Parse(crlf);

        var block = Assert.Single(text.Blocks);
        Assert.Equal("feature/theirs", block.TheirsLabel);
        Assert.All(text.Lines, line => Assert.Equal("\r\n", line.Ending));
        Assert.Equal(["line 2 ours"], Texts(block.Ours));
        Assert.Equal("line 1\r\nline 2 ours\r\nline 3\r\n", text.OursText);
        Assert.Equal("line 1\r\nline 2 theirs\r\nline 2b theirs\r\nline 3\r\n", text.Choose(0, BlockChoice.Theirs));
        Assert.Equal(crlf.Length, text.Segments.Sum(segment => segment.Length));
    }

    [Fact]
    public void Mixed_line_ends_are_kept_line_by_line()
    {
        const string Mixed = "a\r\n<<<<<<< HEAD\nours\r\n=======\r\ntheirs\n>>>>>>> t\r\nz";

        var text = ConflictText.Parse(Mixed);

        Assert.Equal(["\r\n", "\n", "\r\n", "\r\n", "\n", "\r\n", ""], text.Lines.Select(line => line.Ending));
        Assert.Equal("a\r\nours\r\nz", text.OursText);
        Assert.Equal("a\r\ntheirs\nz", text.TheirsText);
    }

    [Fact]
    public void A_last_line_without_a_line_end_is_kept_so_and_can_be_a_marker()
    {
        const string NoFinalNewline = "first\n<<<<<<< HEAD\nours\n=======\ntheirs\n>>>>>>> t";

        var text = ConflictText.Parse(NoFinalNewline);

        var block = Assert.Single(text.Blocks);
        Assert.Equal("t", block.TheirsLabel);
        Assert.Equal(string.Empty, text.Lines[^1].Ending);
        Assert.Equal(NoFinalNewline.Length, block.Offset + block.Length);
        Assert.Equal("first\nours\n", text.Choose(0, BlockChoice.Ours));

        var common = ConflictText.Parse("only line");
        Assert.False(common.HasMarkers);
        Assert.Equal([new ConflictLine("only line", string.Empty)], common.Lines);
        Assert.Equal("only line", common.OursText);
    }

    [Fact]
    public void A_marker_needs_exactly_its_size_then_a_space_or_the_lines_end()
    {
        Assert.True(ConflictText.IsMarker("<<<<<<<", '<'));
        Assert.True(ConflictText.IsMarker("<<<<<<< HEAD", '<'));
        Assert.True(ConflictText.IsMarker("======= ", '='));
        Assert.False(ConflictText.IsMarker("<<<<<<<< HEAD", '<'));
        Assert.False(ConflictText.IsMarker("<<<<<<<HEAD", '<'));
        Assert.False(ConflictText.IsMarker("<<<<<<\t", '<'));
        Assert.False(ConflictText.IsMarker("<<<<<<<\tHEAD", '<'));
        Assert.False(ConflictText.IsMarker("<<<<<<", '<'));
        Assert.False(ConflictText.IsMarker(" <<<<<<< HEAD", '<'));
        Assert.False(ConflictText.IsMarker("<<<<<<< HEAD", '>'));
        Assert.True(ConflictText.IsMarker("<<<<<<<<< HEAD", '<', 9));
        Assert.False(ConflictText.IsMarker("<<<<<<< HEAD", '<', 9));
    }

    [Theory]
    [InlineData("a\n<<<<<<< HEAD\nb\n", 7, true)]
    [InlineData("a\n=======\nb\n", 7, true)]
    [InlineData("a\n>>>>>>> feature\r\nb\n", 7, true)]
    [InlineData("a\n||||||| base", 7, true)]
    [InlineData("a\n<<<<<<<<< HEAD\nb\n", 9, true)]
    [InlineData("a\n<<<<<<< HEAD\nb\n", 9, false)]
    [InlineData("a\n<<<<<<<< HEAD\n<<<<<<HEAD\n b\n", 7, false)]
    [InlineData("", 7, false)]
    public void Any_single_marker_line_counts_as_a_marker_left_whole_block_or_not(string text, int markerSize, bool expected)
    {
        Assert.Equal(expected, ConflictText.HasMarkerLines(text, markerSize));

        // A lone marker line forms no block: the count says none is left.
        Assert.False(ConflictText.Parse(text, markerSize).HasMarkers);
    }

    [Fact]
    public void With_marker_size_9_seven_character_markers_are_text()
    {
        // conflict-marker-size=9 for a file whose own text has seven-character "markers".
        const string Size9 =
            "intro\n<<<<<<<<< HEAD\nours\n<<<<<<< not a marker\n=======\n=========\ntheirs\n>>>>>>> nor this\n>>>>>>>>> theirs\nend\n";

        var nine = ConflictText.Parse(Size9, 9);
        var seven = ConflictText.Parse(Size9);

        var block = Assert.Single(nine.Blocks);
        Assert.Equal(9, nine.MarkerSize);
        Assert.Equal(["ours", "<<<<<<< not a marker", "======="], Texts(block.Ours));
        Assert.Equal(["theirs", ">>>>>>> nor this"], Texts(block.Theirs));
        Assert.Equal("HEAD", block.OursLabel);

        // At size 7 the same text has one whole block, from its own "<" line to its own ">" line.
        var inner = Assert.Single(seven.Blocks);
        Assert.Equal("not a marker", inner.OursLabel);
        Assert.Empty(inner.Ours);
        Assert.Equal(["=========", "theirs"], Texts(inner.Theirs));
        Assert.Equal("nor this", inner.TheirsLabel);
    }

    [Fact]
    public void A_block_that_another_starts_inside_is_not_whole_and_the_inner_one_is()
    {
        const string Nested =
            "<<<<<<< outer\nouter ours\n<<<<<<< inner\ninner ours\n=======\ninner theirs\n>>>>>>> inner\nafter\n";

        var text = ConflictText.Parse(Nested);

        var block = Assert.Single(text.Blocks);
        Assert.Equal("inner", block.OursLabel);
        Assert.Equal(2, block.StartLine);
        AssertCommon(text.Segments[0], 0, ["<<<<<<< outer", "outer ours"]);
        AssertCommon(text.Segments[2], 7, ["after"]);
        Assert.Equal("<<<<<<< outer\nouter ours\ninner ours\nafter\n", text.OursText);
    }

    [Theory]
    [InlineData("<<<<<<< a\nx\n>>>>>>> b\n", "no = line")]
    [InlineData("<<<<<<< a\nx\n=======\ny\n=======\nz\n>>>>>>> b\n", "two = lines")]
    [InlineData("<<<<<<< a\nx\n=======\ny\n||||||| base\nz\n>>>>>>> b\n", "a | line after the = line")]
    [InlineData("<<<<<<< a\nx\n||||||| one\n||||||| two\n=======\ny\n>>>>>>> b\n", "two | lines")]
    [InlineData("<<<<<<< a\nx\n>>>>>>> b\n=======\ny\n>>>>>>> b\n", "a > line before the = line")]
    [InlineData("<<<<<<< a\nx\n=======\ny\n", "no > line before the end")]
    [InlineData("=======\n>>>>>>> b\n||||||| c\n", "markers without a < line")]
    public void Markers_out_of_order_form_no_block_and_stay_common_text(string input, string why)
    {
        var text = ConflictText.Parse(input);

        Assert.False(text.HasMarkers, why);
        Assert.Equal(0, text.BlockCount);
        var common = Assert.IsType<CommonSegment>(Assert.Single(text.Segments));
        Assert.Equal(input, ConflictText.Join(common.Lines));
        Assert.Equal(input, text.OursText);
        Assert.Equal(input, text.TheirsText);
    }

    [Fact]
    public void A_broken_block_before_a_whole_one_leaves_the_whole_one_parsed()
    {
        // The user typed over the first block's "=" line: only the second is a block.
        const string Text =
            "<<<<<<< HEAD\na1\na2\n>>>>>>> t\nmiddle\n<<<<<<< HEAD\nb1\n=======\nb2\n>>>>>>> t\n";

        var text = ConflictText.Parse(Text);

        var block = Assert.Single(text.Blocks);
        Assert.Equal(5, block.StartLine);
        Assert.Equal(["b1"], Texts(block.Ours));
    }

    [Fact]
    public void Empty_sides_and_adjacent_blocks_are_whole()
    {
        const string Text =
            "<<<<<<< HEAD\n=======\nonly theirs\n>>>>>>> t\n<<<<<<< HEAD\nonly ours\n=======\n>>>>>>> t\n";

        var text = ConflictText.Parse(Text);

        Assert.Equal(2, text.BlockCount);
        Assert.All(text.Segments, segment => Assert.IsType<ConflictBlock>(segment));
        Assert.Empty(text.Blocks[0].Ours);
        Assert.Empty(text.Blocks[1].Theirs);
        Assert.Equal("only ours\n", text.OursText);
        Assert.Equal("only theirs\n", text.TheirsText);
        Assert.Equal("<<<<<<< HEAD\nonly ours\n=======\n>>>>>>> t\n", text.Choose(0, BlockChoice.Ours));
        Assert.Equal("<<<<<<< HEAD\n=======\nonly theirs\n>>>>>>> t\nonly ours\n", text.Choose(1, BlockChoice.TheirsThenOurs));
    }

    [Fact]
    public void A_text_without_markers_is_one_common_segment_and_an_empty_text_has_none()
    {
        var plain = ConflictText.Parse("a\nb\n");
        var empty = ConflictText.Parse(string.Empty);

        Assert.False(plain.HasMarkers);
        AssertCommon(Assert.Single(plain.Segments), 0, ["a", "b"]);
        Assert.Empty(empty.Lines);
        Assert.Empty(empty.Segments);
        Assert.Equal(string.Empty, empty.OursText);
        Assert.Throws<ArgumentOutOfRangeException>(() => plain.Choose(0, BlockChoice.Ours));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConflictText.Parse("a", 0));
    }

    [Theory]
    [InlineData(MergeStyle)]
    [InlineData("a\r\n<<<<<<< HEAD\r\nb\r\n|||||||\r\nc\r\n=======\r\nd\r\n>>>>>>> t\r\ne")]
    [InlineData("<<<<<<< a\n<<<<<<< b\nx\n=======\ny\n>>>>>>> c\n>>>>>>> d\n=======\n")]
    [InlineData("lone\rcarriage\rreturns\n<<<<<<< a\r\n=======\n>>>>>>> b")]
    public void The_segments_cover_every_line_once_and_join_back_into_the_text(string input)
    {
        var text = ConflictText.Parse(input);

        var joined = new StringBuilder();
        var line = 0;
        foreach (var segment in text.Segments)
        {
            Assert.Equal(line, segment.FirstLine);
            Assert.Equal(joined.Length, segment.Offset);
            var part = input.Substring(segment.Offset, segment.Length);
            joined.Append(part);
            Assert.Equal(part, ConflictText.Join(text.Lines.Skip(segment.FirstLine).Take(segment.LineCount)));
            line += segment.LineCount;
        }

        Assert.Equal(input, joined.ToString());
        Assert.Equal(text.Lines.Count, line);
        Assert.Equal(input, ConflictText.Join(text.Lines));
    }

    [Fact]
    public void A_lone_carriage_return_is_part_of_its_line()
    {
        Assert.Equal(
            [new ConflictLine("a\rb", "\n"), new ConflictLine("c\r", string.Empty)],
            ConflictText.SplitLines("a\rb\nc\r"));
    }

    [Fact]
    public void Labels_keep_what_follows_the_markers_space_including_a_path()
    {
        const string Text = "<<<<<<< HEAD:rm2.txt\nx\n=======\ny\n>>>>>>> parent of 1a2b3c4 (Rename: rm.txt)\n";

        var block = Assert.Single(ConflictText.Parse(Text).Blocks);

        Assert.Equal("HEAD:rm2.txt", block.OursLabel);
        Assert.Equal("parent of 1a2b3c4 (Rename: rm.txt)", block.TheirsLabel);
        Assert.Equal(string.Empty, Assert.Single(ConflictText.Parse("<<<<<<<\nx\n=======\ny\n>>>>>>>\n").Blocks).OursLabel);
    }

    [Fact]
    public void Blocks_with_the_same_lines_match_whatever_their_line_ends_and_labels()
    {
        var lf = ConflictText.Parse(MergeStyle).Blocks[0];
        var crlf = ConflictText.Parse(MergeStyle.Replace("\n", "\r\n", StringComparison.Ordinal).Replace("HEAD", "Updated upstream", StringComparison.Ordinal)).Blocks[0];
        var other = ConflictText.Parse(MergeStyle.Replace("line 2 ours", "changed", StringComparison.Ordinal)).Blocks[0];

        Assert.True(lf.HasSameSides(crlf));
        Assert.False(lf.HasSameSides(other));
    }

    [Fact]
    public void Bytes_are_decoded_as_utf8_or_else_latin1_and_encoded_back_byte_for_byte()
    {
        byte[] utf8 = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("café ✓\r\n")];
        byte[] latin1 = [0x63, 0x61, 0x66, 0xE9, 0x0D, 0x0A, 0xFF];

        var fromUtf8 = ConflictText.Decode(utf8);
        var fromLatin1 = ConflictText.Decode(latin1);

        Assert.True(fromUtf8.IsUtf8);
        Assert.Equal("﻿café ✓\r\n", fromUtf8.Text);
        Assert.Equal(utf8, ConflictText.Encode(fromUtf8.Text, fromUtf8.IsUtf8));
        Assert.False(fromLatin1.IsUtf8);
        Assert.Equal("café\r\nÿ", fromLatin1.Text);
        Assert.Equal(latin1, ConflictText.Encode(fromLatin1.Text, fromLatin1.IsUtf8));
        Assert.Equal("a?"u8.ToArray(), ConflictText.Encode("a✓", isUtf8: false));
    }

    private static void AssertCommon(ConflictSegment segment, int firstLine, string[] lines)
    {
        var common = Assert.IsType<CommonSegment>(segment);
        Assert.Equal(firstLine, common.FirstLine);
        Assert.Equal(lines, Texts(common.Lines));
    }

    private static string[] Texts(IEnumerable<ConflictLine> lines) => [.. lines.Select(line => line.Text)];
}
