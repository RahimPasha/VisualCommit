namespace VisualCommit.App.Controls.Conflicts;

/// <summary>
/// The background a row of the conflict panes or a line of the result is drawn on
/// (docs/phase-4-design.md 7.3, 11.3). Tests and drivers read it instead of sampling pixels.
/// </summary>
public enum ConflictTint
{
    /// <summary>No tint: the window background (a line both sides share, or a resolved line).</summary>
    None,

    /// <summary>The ours side of a block (<c>VcConflictOursBrush</c>).</summary>
    Ours,

    /// <summary>The theirs side of a block (<c>VcConflictTheirsBrush</c>).</summary>
    Theirs,

    /// <summary>A <c>diff3</c> base section in the result (<c>VcConflictBaseBrush</c>).</summary>
    Base,

    /// <summary>A marker line of an unresolved block in the result (<c>VcConflictMarkerBrush</c>, its text in <c>VcWarningBrush</c>).</summary>
    Marker,

    /// <summary>A block's side once it is used in the result (<c>VcConflictUsedBrush</c>).</summary>
    Used,

    /// <summary>A filler row: the shorter side of a block (<c>VcDiffFillerBrush</c>).</summary>
    Filler,

    /// <summary>A block's bar, 26 high, with its buttons (<c>VcDiffHunkBrush</c>, as a hunk header).</summary>
    Bar,
}
