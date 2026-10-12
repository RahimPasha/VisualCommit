namespace VisualCommit.App.Controls.Conflicts;

/// <summary>
/// A whole conflict block of a text, by its marker lines (0-based line indexes): the <c>&lt;</c>
/// line, the optional <c>|</c> line of a <c>diff3</c> base section, the <c>=</c> line and the
/// <c>&gt;</c> line.
/// </summary>
public sealed record ConflictMarkerBlock(int Start, int? Base, int Separator, int End);

/// <summary>
/// Finds the conflict blocks of a text by git's marker lines (docs/phase-4-design.md 11.3): a
/// block runs from a line of <c>markerSize</c> <c>&lt;</c> followed by a space or the line's end to
/// the next such <c>&gt;</c> line, with an optional <c>|</c> line and one <c>=</c> line between.
/// Lines that do not form a whole block are common text. The result editor's own parser, enough
/// for its tints; the resolver's model (<c>ConflictText</c>, Core) parses the same way.
/// </summary>
public static class ConflictMarkers
{
    /// <summary>Git's default marker size (<c>conflict-marker-size</c>).</summary>
    public const int DefaultSize = 7;

    /// <summary>The whole blocks of <paramref name="lines"/>, in order.</summary>
    public static IReadOnlyList<ConflictMarkerBlock> Parse(IReadOnlyList<string> lines, int markerSize = DefaultSize)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var blocks = new List<ConflictMarkerBlock>();
        for (var start = 0; start < lines.Count; start++)
        {
            if (!IsMarker(lines[start], '<', markerSize))
            {
                continue;
            }

            int? @base = null;
            int? separator = null;
            int? end = null;
            for (var j = start + 1; j < lines.Count; j++)
            {
                var line = lines[j];
                if (IsMarker(line, '<', markerSize))
                {
                    // Another block starts before this one ends: this one is not whole.
                    break;
                }

                if (separator is null)
                {
                    if (@base is null && IsMarker(line, '|', markerSize))
                    {
                        @base = j;
                    }
                    else if (IsMarker(line, '=', markerSize))
                    {
                        separator = j;
                    }
                }
                else if (IsMarker(line, '>', markerSize))
                {
                    end = j;
                    break;
                }
            }

            if (end is { } last)
            {
                blocks.Add(new ConflictMarkerBlock(start, @base, separator!.Value, last));
                start = last;
            }
        }

        return blocks;
    }

    /// <summary>The tint of each of <paramref name="lineCount"/> lines: the blocks' marker lines, ours, base and theirs sections; every other line none.</summary>
    public static ConflictTint[] Tints(int lineCount, IReadOnlyList<ConflictMarkerBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        var tints = new ConflictTint[lineCount];
        foreach (var block in blocks)
        {
            var oursEnd = block.Base ?? block.Separator;
            Fill(block.Start + 1, oursEnd, ConflictTint.Ours);
            if (block.Base is { } @base)
            {
                Fill(@base + 1, block.Separator, ConflictTint.Base);
            }

            Fill(block.Separator + 1, block.End, ConflictTint.Theirs);
            foreach (var marker in new[] { block.Start, block.Base, block.Separator, block.End })
            {
                if (marker is { } line && line < lineCount)
                {
                    tints[line] = ConflictTint.Marker;
                }
            }
        }

        return tints;

        void Fill(int from, int to, ConflictTint tint)
        {
            for (var line = from; line < to && line < lineCount; line++)
            {
                tints[line] = tint;
            }
        }
    }

    /// <summary>Whether a line is a marker of <paramref name="marker"/> characters: exactly <paramref name="size"/> of them, then a space or the line's end.</summary>
    public static bool IsMarker(string line, char marker, int size = DefaultSize)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Length < size || (line.Length > size && line[size] != ' '))
        {
            return false;
        }

        for (var i = 0; i < size; i++)
        {
            if (line[i] != marker)
            {
                return false;
            }
        }

        return true;
    }
}
