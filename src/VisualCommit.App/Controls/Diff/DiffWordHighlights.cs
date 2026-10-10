using VisualCommit.Core.Diff;

namespace VisualCommit.App.Controls.Diff;

/// <summary>
/// The word-level highlights of a whole diff (C5): in each change, the first removed line is
/// paired with the first added line, the second with the second, and so on as far as both sides
/// have lines, and each pair is compared with <see cref="WordDiff.Compare"/>.
/// </summary>
public static class DiffWordHighlights
{
    /// <summary>The ranges of each line's text to highlight; lines without highlights are left out.</summary>
    public static IReadOnlyDictionary<DiffLineRef, IReadOnlyList<TextRange>> Compute(FileDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        var result = new Dictionary<DiffLineRef, IReadOnlyList<TextRange>>();
        for (var h = 0; h < diff.Hunks.Count; h++)
        {
            var lines = diff.Hunks[h].Lines;
            var l = 0;
            while (l < lines.Count)
            {
                if (lines[l].Kind == DiffLineKind.Context)
                {
                    l++;
                    continue;
                }

                var removed = new List<int>();
                var added = new List<int>();
                for (; l < lines.Count && lines[l].Kind != DiffLineKind.Context; l++)
                {
                    (lines[l].Kind == DiffLineKind.Removed ? removed : added).Add(l);
                }

                for (var i = 0; i < Math.Min(removed.Count, added.Count); i++)
                {
                    var (old, @new) = WordDiff.Compare(lines[removed[i]].Text, lines[added[i]].Text);
                    if (old.Count > 0)
                    {
                        result[new DiffLineRef(h, removed[i])] = old;
                    }

                    if (@new.Count > 0)
                    {
                        result[new DiffLineRef(h, added[i])] = @new;
                    }
                }
            }
        }

        return result;
    }
}
