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
/// keys read, and what each mode writes.
/// </summary>
public static class PullModes
{
    /// <summary>The key of the rebase setting a mode writes.</summary>
    public const string RebaseKey = "pull.rebase";

    /// <summary>The key of the fast-forward setting a mode writes.</summary>
    public const string FastForwardKey = "pull.ff";

    /// <summary>The second line of "Other" for <c>interactive</c> and <c>i</c> (8.7).</summary>
    public const string InteractiveNote = "Git opens an editor for the list; the app rebases without one.";

    /// <summary>The second line of "Other" for <c>pull.rebase = m</c> (8.7).</summary>
    public const string MNote = "Git 2.36 merges with this value and newer gits rebase keeping merge commits; the app does what its git does.";

    /// <summary>The second line of "Other" for a unit letter alone (8.7).</summary>
    public const string UnitLetterNote = "Git 2.36 reads this value as false and newer gits refuse it; the app does what its git does.";

    /// <summary>The second line of "Other" for a value git and the app's Pull refuse (8.7).</summary>
    public const string RefusedNote = "Pulls fail while this value is set.";

    /// <summary>
    /// How <paramref name="rebase"/> (<c>pull.rebase</c>'s value; null when unset;
    /// <paramref name="rebaseIsBare"/> for a key without a value) and <paramref name="fastForward"/>
    /// (<c>pull.ff</c>'s) read, by 8.7's rules 1 to 8, with <c>pull.rebase</c> a unit letter alone
    /// ("Other") checked before rule 2: newer gits refuse it under <c>pull.ff = only</c> too.
    /// </summary>
    public static PullModeReading Read(string? rebase, bool rebaseIsBare, string? fastForward) =>
        Read(rebase, rebaseIsBare, fastForward, fastForwardIsBare: false);

    /// <summary>
    /// <see cref="Read(string?, bool, string?)"/> with <paramref name="fastForwardIsBare"/> for a
    /// <c>pull.ff</c> written without a value, which git reads as true.
    /// </summary>
    public static PullModeReading Read(string? rebase, bool rebaseIsBare, string? fastForward, bool fastForwardIsBare)
    {
        var rebaseValue = ClassifyRebase(rebase, rebaseIsBare);
        var ffValue = ClassifyFastForward(fastForward, fastForwardIsBare);

        // 1. A value the app's Pull does not understand: refused.
        if (rebaseValue.Kind == ValueKind.Refused || ffValue.Kind == ValueKind.Refused)
        {
            return Other(rebase, rebaseValue, fastForward, ffValue);
        }

        // A unit letter alone in pull.rebase comes before rule 2: git reads pull.rebase even when
        // pull.ff = only decides, so with pull.ff = only Git 2.36 fast-forwards only (the letter is
        // false there) while newer gits refuse every pull ("fatal: invalid value for 'pull.rebase':
        // 'k'", 2.55, 2026-10-18). m is merges on newer gits and false on 2.36: rule 2 holds on both.
        if (rebaseValue.Kind == ValueKind.UnitLetter)
        {
            return Other(rebase, rebaseValue, fastForward, ffValue);
        }

        // 2. pull.ff = only wins over every rebase setting.
        if (ffValue.Kind == ValueKind.Only)
        {
            return new(PullMode.FastForwardOnly, null, null);
        }

        // 3. Values the app reads as its git does, which the two gits read differently, or which git
        // reads with an editor the app does not open.
        if (rebaseValue.Kind is ValueKind.Interactive or ValueKind.M or ValueKind.UnitLetter || ffValue.Kind == ValueKind.UnitLetter)
        {
            return Other(rebase, rebaseValue, fastForward, ffValue);
        }

        // 4. A rebase: pull.ff = false is ignored when rebasing.
        if (rebaseValue is { Kind: ValueKind.Boolean, Flag: true })
        {
            return new(PullMode.Rebase, null, null);
        }

        // 5.
        if (rebaseValue.Kind == ValueKind.Merges)
        {
            return new(PullMode.RebaseMerges, null, null);
        }

        // 6.
        if (ffValue is { Kind: ValueKind.Boolean, Flag: false })
        {
            return new(PullMode.MergeCommit, null, null);
        }

        // 7.
        if (rebaseValue is { Kind: ValueKind.Boolean, Flag: false } || ffValue is { Kind: ValueKind.Boolean, Flag: true })
        {
            return new(PullMode.Merge, null, null);
        }

        // 8. Both unset.
        return new(PullMode.NotSet, null, null);
    }

    /// <summary>The two writes of <paramref name="mode"/>: <c>pull.rebase</c>, then <c>pull.ff</c>, each a value or an unset (8.7's table).</summary>
    public static IReadOnlyList<GitConfigWrite> Writes(PullMode mode) => mode switch
    {
        PullMode.NotSet => [new(RebaseKey, null), new(FastForwardKey, null)],
        PullMode.Merge => [new(RebaseKey, "false"), new(FastForwardKey, "true")],
        PullMode.MergeCommit => [new(RebaseKey, "false"), new(FastForwardKey, "false")],
        PullMode.Rebase => [new(RebaseKey, "true"), new(FastForwardKey, "true")],
        PullMode.RebaseMerges => [new(RebaseKey, "merges"), new(FastForwardKey, "true")],
        PullMode.FastForwardOnly => [new(RebaseKey, null), new(FastForwardKey, "only")],
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown pull mode."),
    };

    /// <summary>
    /// "Other" for the unusual keys of the pair: <c>Other: {key} = {value}</c>, both keys when both
    /// are unusual, and the second line of the first unusual key's value, a refused value's first
    /// (the pull then fails, whatever the other key says).
    /// </summary>
    private static PullModeReading Other(string? rebase, Classified rebaseValue, string? fastForward, Classified ffValue)
    {
        var parts = new List<string>(2);
        if (rebaseValue.IsUnusual)
        {
            parts.Add($"{RebaseKey} = {rebase}");
        }

        if (ffValue.IsUnusual)
        {
            parts.Add($"{FastForwardKey} = {fastForward}");
        }

        var note = rebaseValue.Kind == ValueKind.Refused || ffValue.Kind == ValueKind.Refused
            ? RefusedNote
            : NoteOf(rebaseValue.IsUnusual ? rebaseValue.Kind : ffValue.Kind);
        return new(null, "Other: " + string.Join(", ", parts), note);
    }

    private static string NoteOf(ValueKind kind) => kind switch
    {
        ValueKind.Interactive => InteractiveNote,
        ValueKind.M => MNote,
        ValueKind.UnitLetter => UnitLetterNote,
        _ => RefusedNote,
    };

    /// <summary><c>pull.rebase</c> as 8.7 reads it: unset, a boolean, <c>merges</c>, <c>m</c>, <c>interactive</c> or <c>i</c> (exactly), a unit letter alone, or refused.</summary>
    private static Classified ClassifyRebase(string? value, bool bare)
    {
        if (value is null && !bare)
        {
            return new(ValueKind.Unset, false);
        }

        // "m" is a unit letter too, but has its own reading: merges on newer gits.
        switch (value)
        {
            case "merges":
                return new(ValueKind.Merges, false);
            case "m":
                return new(ValueKind.M, false);
            case "interactive" or "i":
                return new(ValueKind.Interactive, false);
        }

        if (GitBoolean.IsUnitLetterAlone(value))
        {
            return new(ValueKind.UnitLetter, false);
        }

        return GitBoolean.TryParse(value, bare, out var flag) ? new(ValueKind.Boolean, flag) : new(ValueKind.Refused, false);
    }

    /// <summary><c>pull.ff</c> as 8.7 reads it: unset, a boolean, <c>only</c> (exactly), a unit letter alone (<c>m</c> included), or refused.</summary>
    private static Classified ClassifyFastForward(string? value, bool bare)
    {
        if (value is null && !bare)
        {
            return new(ValueKind.Unset, false);
        }

        if (value == "only")
        {
            return new(ValueKind.Only, false);
        }

        if (GitBoolean.IsUnitLetterAlone(value))
        {
            return new(ValueKind.UnitLetter, false);
        }

        return GitBoolean.TryParse(value, bare, out var flag) ? new(ValueKind.Boolean, flag) : new(ValueKind.Refused, false);
    }

    private enum ValueKind
    {
        Unset,
        Boolean,
        Merges,
        M,
        Interactive,
        Only,
        UnitLetter,
        Refused,
    }

    /// <summary>One key's value as 8.7 reads it.</summary>
    /// <param name="Kind">What it is.</param>
    /// <param name="Flag">A boolean's value.</param>
    private readonly record struct Classified(ValueKind Kind, bool Flag)
    {
        /// <summary>Unusual (8.7): a value "Other" names: refused, <c>interactive</c>, <c>i</c>, <c>m</c>, a unit letter alone.</summary>
        public bool IsUnusual => Kind is ValueKind.Refused or ValueKind.Interactive or ValueKind.M or ValueKind.UnitLetter;
    }
}
