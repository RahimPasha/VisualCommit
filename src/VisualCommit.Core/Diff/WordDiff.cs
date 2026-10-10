namespace VisualCommit.Core.Diff;

/// <summary>A run of characters in a line: where it starts and how long it is.</summary>
public readonly record struct TextRange(int Start, int Length)
{
    public int End => Start + Length;
}

/// <summary>
/// Word-level highlights (C5): which words of a removed line and of the added line paired with it
/// differ. Words are runs of letters, digits and <c>_</c>; runs of white space; and every other
/// character alone. The words that are not in the longest common sequence of the two lines are
/// highlighted. When the lines share no word other than white space and punctuation, nothing is:
/// the whole line changed, and its plain background says so.
/// </summary>
public static class WordDiff
{
    /// <summary>
    /// Above this many pairs of words the comparison is skipped and nothing is highlighted, so
    /// that a pair of very long lines costs no noticeable time.
    /// </summary>
    private const long MaxWordPairs = 250_000;

    /// <summary>The ranges of <paramref name="removed"/> and of <paramref name="added"/> to highlight, each in order and not overlapping.</summary>
    public static (IReadOnlyList<TextRange> Removed, IReadOnlyList<TextRange> Added) Compare(string removed, string added)
    {
        ArgumentNullException.ThrowIfNull(removed);
        ArgumentNullException.ThrowIfNull(added);

        var a = Words(removed);
        var b = Words(added);
        if ((long)a.Count * b.Count > MaxWordPairs)
        {
            return ([], []);
        }

        // Longest common sequence of words, by the usual table: lcs[i, j] is the length for the
        // first i words of a and the first j of b.
        var lcs = new int[a.Count + 1, b.Count + 1];
        for (var i = 1; i <= a.Count; i++)
        {
            for (var j = 1; j <= b.Count; j++)
            {
                lcs[i, j] = Same(removed, a[i - 1], added, b[j - 1])
                    ? lcs[i - 1, j - 1] + 1
                    : Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
            }
        }

        // Walked back from the end. On a tie a word of the added line is passed over first, so
        // that the words they share match as early as they can in the added line: "for tests"
        // against "for the visual checks" keeps "for " together and highlights "the visual checks".
        var inA = new bool[a.Count];
        var inB = new bool[b.Count];
        var sharesAWord = false;
        for (int i = a.Count, j = b.Count; i > 0 && j > 0;)
        {
            if (Same(removed, a[i - 1], added, b[j - 1]))
            {
                inA[i - 1] = true;
                inB[j - 1] = true;
                sharesAWord |= IsWord(removed, a[i - 1]);
                i--;
                j--;
            }
            else if (lcs[i, j - 1] >= lcs[i - 1, j])
            {
                j--;
            }
            else
            {
                i--;
            }
        }

        if (!sharesAWord)
        {
            return ([], []);
        }

        return (Unmatched(a, inA), Unmatched(b, inB));
    }

    /// <summary>The words of a line, as ranges.</summary>
    public static List<TextRange> Words(string line)
    {
        var words = new List<TextRange>();
        var i = 0;
        while (i < line.Length)
        {
            var start = i;
            if (IsWordChar(line[i]))
            {
                while (i < line.Length && IsWordChar(line[i]))
                {
                    i++;
                }
            }
            else if (char.IsWhiteSpace(line[i]))
            {
                while (i < line.Length && char.IsWhiteSpace(line[i]))
                {
                    i++;
                }
            }
            else
            {
                // A surrogate pair is one character to the eye.
                i += char.IsHighSurrogate(line[i]) && i + 1 < line.Length && char.IsLowSurrogate(line[i + 1]) ? 2 : 1;
            }

            words.Add(new TextRange(start, i - start));
        }

        return words;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static bool IsWord(string line, TextRange word) => IsWordChar(line[word.Start]);

    private static bool Same(string a, TextRange x, string b, TextRange y) =>
        x.Length == y.Length && a.AsSpan(x.Start, x.Length).SequenceEqual(b.AsSpan(y.Start, y.Length));

    /// <summary>The words not matched, joined where they touch.</summary>
    private static List<TextRange> Unmatched(List<TextRange> words, bool[] matched)
    {
        var ranges = new List<TextRange>();
        for (var k = 0; k < words.Count; k++)
        {
            if (matched[k])
            {
                continue;
            }

            if (ranges.Count > 0 && ranges[^1].End == words[k].Start)
            {
                ranges[^1] = new TextRange(ranges[^1].Start, ranges[^1].Length + words[k].Length);
            }
            else
            {
                ranges.Add(words[k]);
            }
        }

        return ranges;
    }
}
