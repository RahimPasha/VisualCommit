namespace VisualCommit.App.ViewModels.Graph;

/// <summary>
/// The commits selected in the graph (docs/phase-3-design.md 4h, D88): commit SHAs, never row
/// numbers, because rows move whenever the graph reloads. Immutable: every change makes a new
/// selection, so that the graph control's two-way <c>Selection</c> property sees a change.
/// </summary>
public sealed class GraphSelection : IEquatable<GraphSelection>
{
    private readonly HashSet<string> _set;

    /// <param name="shas">The selected commits, in the order the user selected them; repeats are dropped.</param>
    /// <param name="anchor">Where Shift extends from; null takes the last of <paramref name="shas"/>.</param>
    /// <param name="focus">The last row clicked that is still selected; null takes the last of <paramref name="shas"/>.</param>
    public GraphSelection(IEnumerable<string> shas, string? anchor = null, string? focus = null)
    {
        ArgumentNullException.ThrowIfNull(shas);
        var ordered = new List<string>();
        _set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sha in shas)
        {
            if (_set.Add(sha))
            {
                ordered.Add(sha);
            }
        }

        Shas = ordered;
        Anchor = anchor ?? (ordered.Count > 0 ? ordered[^1] : null);
        Focus = focus is not null && _set.Contains(focus) ? focus : (ordered.Count > 0 ? ordered[^1] : null);
    }

    /// <summary>Nothing selected.</summary>
    public static GraphSelection Empty { get; } = new([]);

    /// <summary>The selected commits, in the order they were selected.</summary>
    public IReadOnlyList<string> Shas { get; }

    /// <summary>Where a Shift gesture extends from, or null.</summary>
    public string? Anchor { get; }

    /// <summary>The focused commit: the last row clicked that is still selected, or null.</summary>
    public string? Focus { get; }

    public int Count => Shas.Count;

    public bool IsEmpty => Shas.Count == 0;

    /// <summary>Two or more commits: the details panel lists them and their menu acts on all (4h).</summary>
    public bool IsMultiple => Shas.Count > 1;

    /// <summary>The one selected commit, or null when none or several are selected.</summary>
    public string? Single => Shas.Count == 1 ? Shas[0] : null;

    /// <summary>One commit selected, which is also the anchor and the focus.</summary>
    public static GraphSelection Of(string sha)
    {
        ArgumentNullException.ThrowIfNull(sha);
        return new GraphSelection([sha]);
    }

    public bool Contains(string sha) => _set.Contains(sha);

    public bool Equals(GraphSelection? other) =>
        other is not null && Anchor == other.Anchor && Focus == other.Focus && Shas.SequenceEqual(other.Shas, StringComparer.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as GraphSelection);

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var sha in Shas)
        {
            hash.Add(sha, StringComparer.Ordinal);
        }

        hash.Add(Anchor);
        hash.Add(Focus);
        return hash.ToHashCode();
    }

    public override string ToString() => Shas.Count switch
    {
        0 => "nothing selected",
        1 => Shas[0],
        _ => $"{Shas.Count} commits",
    };
}
