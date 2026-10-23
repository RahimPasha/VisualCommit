namespace VisualCommit.App.ViewModels.Commands;

/// <summary>
/// The palette's matching and ranking (docs/phase-6-design.md 5.6, D212), pure. The query is
/// trimmed and split at white space; an empty one lists the commands only, those run this session
/// first (newest first), then the commands' own order. Otherwise an entry matches when every word
/// occurs in its label or its detail, ignoring case, and the matches rank by tier, then enabled
/// before disabled, then the session's runs (the most recent first), then source order (commands
/// before items, then each list's order). No fuzzy subsequence.
/// </summary>
public static class PaletteMatcher
{
    /// <summary>The characters after which a word of a label starts (5.6's tier 1), besides its start.</summary>
    private const string WordStarts = " /-_.(\"";

    /// <summary>
    /// The entries <paramref name="query"/> shows, in rank order. <paramref name="recent"/> is the
    /// session's runs by <see cref="PaletteEntry.Key"/>, newest first.
    /// </summary>
    public static IReadOnlyList<PaletteEntry> Rank(string query, IReadOnlyList<PaletteEntry> commands, IReadOnlyList<PaletteEntry> items, IReadOnlyList<string> recent)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(recent);
        var runs = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < recent.Count; i++)
        {
            runs.TryAdd(recent[i], i);
        }

        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            // Disabled commands keep their places in an empty box (KD1).
            return [.. commands
                .Select((entry, index) => (Entry: entry, Index: index, Run: runs.GetValueOrDefault(entry.Key, int.MaxValue)))
                .OrderBy(entry => entry.Run)
                .ThenBy(entry => entry.Index)
                .Select(entry => entry.Entry)];
        }

        var words = Words(trimmed);
        var matches = new List<(PaletteEntry Entry, int Tier, int Run, int Source)>();
        var source = 0;
        foreach (var entry in commands.Concat(items))
        {
            if (TierOf(trimmed, words, entry) is { } tier)
            {
                matches.Add((entry, tier, runs.GetValueOrDefault(entry.Key, int.MaxValue), source));
            }

            source++;
        }

        return [.. matches
            .OrderBy(match => match.Tier)
            .ThenBy(match => match.Entry.IsEnabled ? 0 : 1)
            .ThenBy(match => match.Run)
            .ThenBy(match => match.Source)
            .Select(match => match.Entry)];
    }

    /// <summary>The words of a query: split at white space, empty parts left out.</summary>
    public static IReadOnlyList<string> Words(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// The tier of <paramref name="entry"/> for a query (5.6), or null when it does not match: 0,
    /// its label starts with the whole <paramref name="trimmedQuery"/>; 1, every word starts a word
    /// of the label (at its start or after a space, <c>/</c>, <c>-</c>, <c>_</c>, <c>.</c>,
    /// <c>(</c> or <c>"</c>); 2, every word is in the label; 3, a word is only in the detail.
    /// </summary>
    public static int? TierOf(string trimmedQuery, IReadOnlyList<string> words, PaletteEntry entry)
    {
        ArgumentNullException.ThrowIfNull(trimmedQuery);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(entry);
        var label = entry.Label;
        var detail = entry.Detail ?? string.Empty;
        var inLabel = true;
        foreach (var word in words)
        {
            if (label.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!detail.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            inLabel = false;
        }

        if (!inLabel)
        {
            return 3;
        }

        if (label.StartsWith(trimmedQuery, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return words.All(word => StartsAWord(label, word)) ? 1 : 2;
    }

    /// <summary>Whether <paramref name="word"/> occurs in <paramref name="label"/> at the start of one of its words.</summary>
    private static bool StartsAWord(string label, string word)
    {
        for (var at = label.IndexOf(word, StringComparison.OrdinalIgnoreCase); at >= 0; at = label.IndexOf(word, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (at == 0 || WordStarts.Contains(label[at - 1], StringComparison.Ordinal))
            {
                return true;
            }

            if (at + 1 >= label.Length)
            {
                break;
            }
        }

        return false;
    }
}
