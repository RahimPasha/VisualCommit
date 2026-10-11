using VisualCommit.Core.Diff;

namespace VisualCommit.App.Controls.Diff;

/// <summary>
/// The word-level highlights of a whole diff (C5): in each change, the first removed line is
/// paired with the first added line, the second with the second, and so on as far as both sides
/// have lines, and each pair is compared with <see cref="WordDiff.Compare"/>.
/// <para>
/// A comparison costs about the product of the two lines' word counts. A diff has a budget of
/// such pairs of words; once a pair would go over it, that pair and the ones after it are left
/// without highlights, so that a diff of many long lines costs no more than the budget.
/// </para>
/// </summary>
public static class DiffWordHighlights
{
    /// <summary>The budget of a diff that is compared on the UI thread before it is drawn: some tens of milliseconds at most.</summary>
    public const long SynchronousBudget = 2_000_000;

    /// <summary>The budget of a diff compared in the background.</summary>
    public const long BackgroundBudget = 40_000_000;

    /// <summary>The ranges of each line's text to highlight; lines without highlights are left out.</summary>
    /// <param name="diff">The diff.</param>
    /// <param name="budget">The most pairs of words compared in all.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    public static IReadOnlyDictionary<DiffLineRef, IReadOnlyList<TextRange>> Compute(FileDiff diff, long budget = SynchronousBudget, CancellationToken cancellationToken = default)
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
                    cancellationToken.ThrowIfCancellationRequested();
                    var oldText = lines[removed[i]].Text;
                    var newText = lines[added[i]].Text;
                    var cost = Math.Max(1L, (long)WordDiff.Words(oldText).Count * WordDiff.Words(newText).Count);
                    if (cost > WordDiff.MaxWordPairs)
                    {
                        // WordDiff leaves such a pair alone, at no cost.
                        continue;
                    }

                    if (cost > budget)
                    {
                        return result;
                    }

                    budget -= cost;
                    var (old, @new) = WordDiff.Compare(oldText, newText);
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
