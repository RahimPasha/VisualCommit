namespace VisualCommit.App.Controls.Diff;

/// <summary>Which buttons each hunk header of the diff view shows (D70).</summary>
public enum HunkActions
{
    /// <summary>No buttons: a commit's diff.</summary>
    None,

    /// <summary>"Stage hunk" and "Discard hunk": an unstaged diff.</summary>
    StageAndDiscard,

    /// <summary>"Unstage hunk": a staged diff.</summary>
    Unstage,
}

/// <summary>What a hunk header's button asks for.</summary>
public enum HunkAction
{
    Stage,
    Discard,
    Unstage,
}

/// <summary>A hunk header's button was clicked.</summary>
/// <param name="hunk">The hunk, from 0, as in <see cref="Core.Diff.FileDiff.Hunks"/>.</param>
/// <param name="action">What the button asks for.</param>
public sealed class HunkActionEventArgs(int hunk, HunkAction action) : EventArgs
{
    /// <summary>The hunk, from 0, as in <see cref="Core.Diff.FileDiff.Hunks"/>.</summary>
    public int Hunk { get; } = hunk;

    /// <summary>What the button asks for.</summary>
    public HunkAction Action { get; } = action;
}
