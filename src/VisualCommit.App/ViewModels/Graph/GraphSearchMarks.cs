namespace VisualCommit.App.ViewModels.Graph;

/// <summary>
/// What the graph marks of a search (docs/phase-5-design.md 3.3, D157): the matching commits, by
/// SHA, and whether a search is active with a text. While it is, the graph marks each matching row
/// and dims every other commit row; otherwise it draws as without a search. Immutable: the search
/// makes a new one as its matches arrive (3.4), and the graph control
/// (<c>CommitGraphControl.Search</c>) draws again for each.
/// </summary>
public sealed class GraphSearchMarks
{
    private readonly HashSet<string> _matches;

    /// <param name="matches">The matching commits' SHAs.</param>
    /// <param name="isActive">A search is active and its text is not empty.</param>
    public GraphSearchMarks(IEnumerable<string> matches, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(matches);
        _matches = new HashSet<string>(matches, StringComparer.Ordinal);
        IsActive = isActive;
    }

    /// <summary>No search: nothing is marked or dimmed.</summary>
    public static GraphSearchMarks None { get; } = new([], isActive: false);

    /// <summary>The matching commits' SHAs.</summary>
    public IReadOnlySet<string> Matches => _matches;

    /// <summary>A search is active and its text is not empty: the graph marks and dims.</summary>
    public bool IsActive { get; }

    /// <summary>Whether the commit <paramref name="sha"/> matches.</summary>
    public bool IsMatch(string sha) => _matches.Contains(sha);
}
