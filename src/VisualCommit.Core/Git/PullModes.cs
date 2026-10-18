namespace VisualCommit.Core.Git;

/// <summary>The pull modes the settings screen offers (docs/phase-6-design.md 8.7), each writing <c>pull.rebase</c> and <c>pull.ff</c> at the chosen scope.</summary>
public enum PullMode
{
    /// <summary>Both keys unset: the scope below decides; with nothing anywhere the app merges (D78).</summary>
    NotSet,

    /// <summary><c>pull.rebase=false</c>, <c>pull.ff=true</c>: merge, fast-forward when possible.</summary>
    Merge,

    /// <summary><c>pull.rebase=false</c>, <c>pull.ff=false</c>: always a merge commit.</summary>
    MergeCommit,

    /// <summary><c>pull.rebase=true</c>, <c>pull.ff=true</c>.</summary>
    Rebase,

    /// <summary><c>pull.rebase=merges</c>, <c>pull.ff=true</c>: rebase, keeping merge commits.</summary>
    RebaseMerges,

    /// <summary><c>pull.rebase</c> unset, <c>pull.ff=only</c>.</summary>
    FastForwardOnly,
}

/// <summary>How a scope's pair of pull keys reads (8.7).</summary>
/// <param name="Mode">The mode the pair gives; null for the radio "Other", a value the six modes do not cover.</param>
/// <param name="Other">For "Other": <c>Other: {key} = {value}</c> (both keys when both are unusual); null otherwise.</param>
/// <param name="OtherNote">For "Other": its second line (8.7's table); null otherwise.</param>
public sealed record PullModeReading(PullMode? Mode, string? Other, string? OtherNote);

/// <summary>
/// The pull modes' pure logic (8.7), shared by the settings screen and its tests: how a scope's
/// keys read, and what each mode writes. Work package WP2 gives it its bodies.
/// </summary>
public static class PullModes
{
    /// <summary>
    /// How <paramref name="rebase"/> (<c>pull.rebase</c>'s value; null when unset;
    /// <paramref name="rebaseIsBare"/> for a key without a value) and <paramref name="fastForward"/>
    /// (<c>pull.ff</c>'s) read, by 8.7's rules 1 to 8.
    /// </summary>
    public static PullModeReading Read(string? rebase, bool rebaseIsBare, string? fastForward) =>
        throw new NotImplementedException("Work package WP2 builds PullModes.Read (docs/phase-6-design.md 8.7).");

    /// <summary>The two writes of <paramref name="mode"/>: <c>pull.rebase</c>, then <c>pull.ff</c>, each a value or an unset (8.7's table).</summary>
    public static IReadOnlyList<GitConfigWrite> Writes(PullMode mode) =>
        throw new NotImplementedException("Work package WP2 builds PullModes.Writes (docs/phase-6-design.md 8.7).");
}
