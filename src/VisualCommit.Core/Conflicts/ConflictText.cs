using System.Text;

namespace VisualCommit.Core.Conflicts;

/// <summary>One line of a text and its line end, kept as the file has it.</summary>
/// <param name="Text">The line without its line end.</param>
/// <param name="Ending">Its line end: <c>"\n"</c>, <c>"\r\n"</c>, or empty for a last line that has none.</param>
public readonly record struct ConflictLine(string Text, string Ending)
{
    /// <summary>The line with its line end, as it stands in the text.</summary>
    public string Whole => Text + Ending;
}

/// <summary>What a block's text becomes when the user chooses (docs/phase-4-design.md 11.5).</summary>
public enum BlockChoice
{
    /// <summary>"Use ours": the block becomes its ours lines.</summary>
    Ours,

    /// <summary>"Use theirs": the block becomes its theirs lines.</summary>
    Theirs,

    /// <summary>"Use both" in the ours pane: the ours lines, then the theirs lines.</summary>
    OursThenTheirs,

    /// <summary>"Use both" in the theirs pane: the theirs lines, then the ours lines.</summary>
    TheirsThenOurs,
}

/// <summary>
/// A piece of a conflicted file, in the file's order: lines both sides have
/// (<see cref="CommonSegment"/>) or a conflict block (<see cref="ConflictBlock"/>).
/// </summary>
/// <param name="FirstLine">Its first line's index in <see cref="ConflictText.Lines"/>, from 0.</param>
/// <param name="LineCount">How many lines of the text it covers (a block's marker lines included).</param>
/// <param name="Offset">Where it starts in <see cref="ConflictText.Text"/>, in characters.</param>
/// <param name="Length">Its length in characters, its last line's line end included.</param>
public abstract record ConflictSegment(int FirstLine, int LineCount, int Offset, int Length);

/// <summary>Lines outside every block: both sides have them as they are.</summary>
/// <param name="Lines">The lines, with their line ends.</param>
public sealed record CommonSegment(IReadOnlyList<ConflictLine> Lines, int FirstLine, int Offset, int Length)
    : ConflictSegment(FirstLine, Lines.Count, Offset, Length);

/// <summary>
/// One whole conflict block, from its <c>&lt;</c> marker line to its <c>&gt;</c> marker line
/// (docs/phase-4-design.md 11.3): the ours section, an optional base section (<c>diff3</c> and
/// <c>zdiff3</c>), the <c>=</c> line and the theirs section. The line indexes count the lines of
/// the whole text from 0.
/// </summary>
/// <param name="Index">The block's number in the file's order, from 0.</param>
/// <param name="OursLabel">The text after the <c>&lt;</c> marker and its space (<c>HEAD</c>, <c>HEAD:path</c>), or empty.</param>
/// <param name="Ours">The ours section's lines.</param>
/// <param name="BaseLabel">The text after the <c>|</c> marker and its space, or null without a base section.</param>
/// <param name="Base">The base section's lines, or null without a base section (empty for an empty one).</param>
/// <param name="TheirsLabel">The text after the <c>&gt;</c> marker and its space (a branch, <c>&lt;short&gt; (&lt;subject&gt;)</c>), or empty.</param>
/// <param name="Theirs">The theirs section's lines.</param>
/// <param name="StartLine">The <c>&lt;</c> marker's line.</param>
/// <param name="BaseLine">The <c>|</c> marker's line, or null.</param>
/// <param name="SeparatorLine">The <c>=</c> marker's line.</param>
/// <param name="EndLine">The <c>&gt;</c> marker's line.</param>
/// <param name="Offset">Where the <c>&lt;</c> line starts in the text, in characters.</param>
/// <param name="Length">The characters from there to the end of the <c>&gt;</c> line, its line end included: what a choice replaces.</param>
public sealed record ConflictBlock(
    int Index,
    string OursLabel,
    IReadOnlyList<ConflictLine> Ours,
    string? BaseLabel,
    IReadOnlyList<ConflictLine>? Base,
    string TheirsLabel,
    IReadOnlyList<ConflictLine> Theirs,
    int StartLine,
    int? BaseLine,
    int SeparatorLine,
    int EndLine,
    int Offset,
    int Length)
    : ConflictSegment(StartLine, EndLine - StartLine + 1, Offset, Length)
{
    /// <summary>
    /// The text that replaces the block for <paramref name="choice"/>: the chosen sides' lines,
    /// each with its own line end (a section's lines always end with one, since a marker line
    /// follows them).
    /// </summary>
    public string TextFor(BlockChoice choice)
    {
        IReadOnlyList<ConflictLine> lines = choice switch
        {
            BlockChoice.Ours => Ours,
            BlockChoice.Theirs => Theirs,
            BlockChoice.OursThenTheirs => [.. Ours, .. Theirs],
            BlockChoice.TheirsThenOurs => [.. Theirs, .. Ours],
            _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, null),
        };
        return ConflictText.Join(lines);
    }

    /// <summary>
    /// Whether <paramref name="other"/> has the same ours and theirs lines, compared without their
    /// line ends: how a block of the edited result is matched to a block of the panes (11.5).
    /// </summary>
    public bool HasSameSides(ConflictBlock other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return SameTexts(Ours, other.Ours) && SameTexts(Theirs, other.Theirs);

        static bool SameTexts(IReadOnlyList<ConflictLine> one, IReadOnlyList<ConflictLine> two) =>
            one.Count == two.Count && one.Select(line => line.Text).SequenceEqual(two.Select(line => line.Text), StringComparer.Ordinal);
    }
}

/// <summary>
/// A conflicted file's text split into lines both sides have and conflict blocks, by git's
/// marker lines (docs/phase-4-design.md 11.3, D148). Pure: no git, no files.
/// <para>
/// A marker is a line that starts with exactly <see cref="MarkerSize"/> of one character
/// (<c>&lt;</c>, <c>|</c>, <c>=</c> or <c>&gt;</c>) followed by a space or the line's end; the
/// text after the space is its label. A block runs from a <c>&lt;</c> marker to the next
/// <c>&gt;</c> marker, with an optional <c>|</c> marker (the base section of <c>diff3</c> and
/// <c>zdiff3</c>) and exactly one <c>=</c> marker between them, in that order, and no other
/// marker inside, as git's own reading of a conflicted file (rerere) has it. A <c>&lt;</c> marker
/// that starts no whole block is a common line, and so is every line outside the blocks: a
/// marker of another size (an inner conflict of a recursive merge has longer ones) is text.
/// </para>
/// <para>
/// Lines end with <c>"\n"</c> or <c>"\r\n"</c>, as git splits them; each line keeps its own
/// line end, and the last line may have none. Nothing is lost: the segments' lines, joined,
/// are the text.
/// </para>
/// </summary>
public sealed class ConflictText
{
    /// <summary>Git's default marker size (the <c>conflict-marker-size</c> attribute unset).</summary>
    public const int DefaultMarkerSize = 7;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private ConflictText(string text, int markerSize, IReadOnlyList<ConflictLine> lines, IReadOnlyList<ConflictSegment> segments)
    {
        Text = text;
        MarkerSize = markerSize;
        Lines = lines;
        Segments = segments;
        Blocks = [.. segments.OfType<ConflictBlock>()];
        OursLines = [.. segments.SelectMany(segment => segment is ConflictBlock block ? block.Ours : ((CommonSegment)segment).Lines)];
        TheirsLines = [.. segments.SelectMany(segment => segment is ConflictBlock block ? block.Theirs : ((CommonSegment)segment).Lines)];
    }

    /// <summary>The text that was parsed.</summary>
    public string Text { get; }

    /// <summary>The marker size it was parsed with.</summary>
    public int MarkerSize { get; }

    /// <summary>Every line of the text, with its line end.</summary>
    public IReadOnlyList<ConflictLine> Lines { get; }

    /// <summary>The common segments and the blocks, in the text's order; together they cover every line once.</summary>
    public IReadOnlyList<ConflictSegment> Segments { get; }

    /// <summary>The whole blocks, in the text's order (<see cref="ConflictBlock.Index"/> is the position here).</summary>
    public IReadOnlyList<ConflictBlock> Blocks { get; }

    /// <summary>How many whole blocks the text has: the result's "{m} conflicts left".</summary>
    public int BlockCount => Blocks.Count;

    /// <summary>Whether any whole block is left.</summary>
    public bool HasMarkers => Blocks.Count > 0;

    /// <summary>The file as it would be with every block's ours side: the ours pane's lines (11.3).</summary>
    public IReadOnlyList<ConflictLine> OursLines { get; }

    /// <summary>The file as it would be with every block's theirs side: the theirs pane's lines.</summary>
    public IReadOnlyList<ConflictLine> TheirsLines { get; }

    /// <summary>The text of <see cref="OursLines"/>.</summary>
    public string OursText => Join(OursLines);

    /// <summary>The text of <see cref="TheirsLines"/>.</summary>
    public string TheirsText => Join(TheirsLines);

    /// <summary>Splits <paramref name="text"/> into common segments and whole blocks of <paramref name="markerSize"/> markers.</summary>
    public static ConflictText Parse(string text, int markerSize = DefaultMarkerSize)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(markerSize, 1);

        var lines = SplitLines(text);
        var offsets = new int[lines.Count + 1];
        for (var i = 0; i < lines.Count; i++)
        {
            offsets[i + 1] = offsets[i] + lines[i].Text.Length + lines[i].Ending.Length;
        }

        var segments = new List<ConflictSegment>();
        var blocks = 0;
        var commonStart = 0;
        var line = 0;
        while (line < lines.Count)
        {
            if (TryReadBlock(lines, line, markerSize, blocks, offsets) is { } block)
            {
                blocks++;
                AddCommon(commonStart, line);
                segments.Add(block);
                line = block.EndLine + 1;
                commonStart = line;
            }
            else
            {
                line++;
            }
        }

        AddCommon(commonStart, lines.Count);
        return new ConflictText(text, markerSize, lines, segments);

        void AddCommon(int from, int to)
        {
            if (to > from)
            {
                segments.Add(new CommonSegment([.. lines.Skip(from).Take(to - from)], from, offsets[from], offsets[to] - offsets[from]));
            }
        }
    }

    /// <summary>
    /// The whole text with block <paramref name="blockIndex"/> replaced by the lines
    /// <paramref name="choice"/> takes (from its <c>&lt;</c> line to its <c>&gt;</c> line, 11.5);
    /// the other blocks stay as they are.
    /// </summary>
    public string Choose(int blockIndex, BlockChoice choice)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(blockIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(blockIndex, Blocks.Count);
        var block = Blocks[blockIndex];
        return string.Concat(Text.AsSpan(0, block.Offset), block.TextFor(choice), Text.AsSpan(block.Offset + block.Length));
    }

    /// <summary>
    /// Whether <paramref name="text"/> has any line that is a <c>&lt;</c>, <c>|</c>, <c>=</c> or
    /// <c>&gt;</c> marker of <paramref name="markerSize"/>, whether or not it is part of a whole
    /// block: what staging a conflicted file and the resolver's Mark resolved ask about (7.1, 7.2,
    /// D148), as git's own check of staged markers finds them.
    /// </summary>
    public static bool HasMarkerLines(string text, int markerSize = DefaultMarkerSize)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(markerSize, 1);
        return SplitLines(text).Any(line =>
            IsMarker(line.Text, '<', markerSize)
            || IsMarker(line.Text, '=', markerSize)
            || IsMarker(line.Text, '>', markerSize)
            || IsMarker(line.Text, '|', markerSize));
    }

    /// <summary>
    /// Whether <paramref name="lineText"/> (a line without its line end) is a marker of
    /// <paramref name="marker"/>: exactly <paramref name="markerSize"/> of it, then a space or
    /// the line's end.
    /// </summary>
    public static bool IsMarker(string lineText, char marker, int markerSize = DefaultMarkerSize)
    {
        ArgumentNullException.ThrowIfNull(lineText);
        if (lineText.Length < markerSize || (lineText.Length > markerSize && lineText[markerSize] != ' '))
        {
            return false;
        }

        for (var i = 0; i < markerSize; i++)
        {
            if (lineText[i] != marker)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The lines of <paramref name="text"/>, each with its line end: a line ends at <c>"\n"</c>
    /// (<c>"\r\n"</c> when a carriage return comes before it); text after the last line feed is a
    /// last line without one. A lone carriage return is part of its line, as for git.
    /// </summary>
    public static IReadOnlyList<ConflictLine> SplitLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = new List<ConflictLine>();
        var start = 0;
        while (start < text.Length)
        {
            var feed = text.IndexOf('\n', start);
            if (feed < 0)
            {
                lines.Add(new ConflictLine(text[start..], string.Empty));
                break;
            }

            var crlf = feed > start && text[feed - 1] == '\r';
            lines.Add(new ConflictLine(text[start..(crlf ? feed - 1 : feed)], crlf ? "\r\n" : "\n"));
            start = feed + 1;
        }

        return lines;
    }

    /// <summary>The lines joined back into text, each with its line end.</summary>
    public static string Join(IEnumerable<ConflictLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var text = new StringBuilder();
        foreach (var line in lines)
        {
            text.Append(line.Text).Append(line.Ending);
        }

        return text.ToString();
    }

    /// <summary>
    /// Reads a whole block that starts with a <c>&lt;</c> marker at <paramref name="start"/>, or
    /// null: the sections must come in their order (ours, an optional base, theirs), each marker
    /// once, and no other marker of the size inside, as git's rerere reads them.
    /// </summary>
    private static ConflictBlock? TryReadBlock(IReadOnlyList<ConflictLine> lines, int start, int markerSize, int index, int[] offsets)
    {
        if (!IsMarker(lines[start].Text, '<', markerSize))
        {
            return null;
        }

        int? baseLine = null;
        int? separator = null;
        for (var j = start + 1; j < lines.Count; j++)
        {
            var text = lines[j].Text;
            if (IsMarker(text, '<', markerSize))
            {
                // Another block starts before this one ends: this one is not whole.
                return null;
            }

            if (IsMarker(text, '|', markerSize))
            {
                if (baseLine is not null || separator is not null)
                {
                    return null;
                }

                baseLine = j;
            }
            else if (IsMarker(text, '=', markerSize))
            {
                if (separator is not null)
                {
                    return null;
                }

                separator = j;
            }
            else if (IsMarker(text, '>', markerSize))
            {
                if (separator is not { } equals)
                {
                    return null;
                }

                var oursEnd = baseLine ?? equals;
                return new ConflictBlock(
                    index,
                    Label(lines[start].Text),
                    Range(start + 1, oursEnd),
                    baseLine is { } b ? Label(lines[b].Text) : null,
                    baseLine is { } from ? Range(from + 1, equals) : null,
                    Label(text),
                    Range(equals + 1, j),
                    start,
                    baseLine,
                    equals,
                    j,
                    offsets[start],
                    offsets[j + 1] - offsets[start]);
            }
        }

        return null;

        string Label(string marker) => marker.Length > markerSize ? marker[(markerSize + 1)..] : string.Empty;

        IReadOnlyList<ConflictLine> Range(int from, int to) => [.. lines.Skip(from).Take(to - from)];
    }

    /// <summary>
    /// A conflicted file's bytes as text, decoded as the diff view decodes a file (D66): UTF-8,
    /// or Latin-1 (one character per byte) when the bytes are not valid UTF-8. A byte order mark
    /// stays in the text as its character, so that <see cref="Encode"/> gives it back.
    /// </summary>
    public static DecodedText Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            return new DecodedText(StrictUtf8.GetString(bytes), IsUtf8: true);
        }
        catch (DecoderFallbackException)
        {
            return new DecodedText(Encoding.Latin1.GetString(bytes), IsUtf8: false);
        }
    }

    /// <summary>
    /// <paramref name="text"/> as bytes in the encoding the file was read in (11.7): UTF-8 without
    /// an added byte order mark, or Latin-1. A character Latin-1 cannot hold (typed into a Latin-1
    /// file) is written as <c>?</c>.
    /// </summary>
    public static byte[] Encode(string text, bool isUtf8)
    {
        ArgumentNullException.ThrowIfNull(text);
        return isUtf8 ? Utf8NoBom.GetBytes(text) : Encoding.Latin1.GetBytes(text);
    }
}

/// <summary>A file's bytes decoded (<see cref="ConflictText.Decode"/>).</summary>
/// <param name="Text">The text.</param>
/// <param name="IsUtf8">The bytes were valid UTF-8; otherwise they were read as Latin-1.</param>
public sealed record DecodedText(string Text, bool IsUtf8);
