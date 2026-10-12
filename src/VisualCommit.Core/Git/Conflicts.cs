namespace VisualCommit.Core.Git;

/// <summary>
/// The kind of a conflicted path, in git's words (docs/phase-4-design.md 0.4, 11.2): the XY of
/// its <c>u</c> line in <c>git status --porcelain=v2</c>.
/// </summary>
public enum ConflictKind
{
    /// <summary><c>UU</c>: both sides changed it.</summary>
    BothModified,

    /// <summary><c>AA</c>: both sides added it (no base stage).</summary>
    BothAdded,

    /// <summary><c>UD</c>: ours changed it, theirs deleted it (ours is in the working tree).</summary>
    DeletedByThem,

    /// <summary><c>DU</c>: ours deleted it, theirs changed it (theirs is in the working tree).</summary>
    DeletedByUs,

    /// <summary><c>AU</c>: only ours has it (a rename/rename, or a file moved aside by a file/folder clash).</summary>
    AddedByUs,

    /// <summary><c>UA</c>: only theirs has it.</summary>
    AddedByThem,

    /// <summary><c>DD</c>: both sides deleted it (a rename/rename's old path); there is no file.</summary>
    BothDeleted,
}

/// <summary>
/// One conflicted path as <c>git status --porcelain=v2 -z</c> prints it (0.4):
/// <c>u &lt;XY&gt; &lt;sub&gt; &lt;m1&gt; &lt;m2&gt; &lt;m3&gt; &lt;mW&gt; &lt;h1&gt; &lt;h2&gt; &lt;h3&gt; &lt;path&gt;</c>.
/// Stage 1 is the base, 2 ours, 3 theirs; a missing stage has git's zeros as its mode
/// (<c>000000</c>) and id.
/// </summary>
/// <param name="Path">The path, repository-relative with <c>/</c>.</param>
/// <param name="Kind">The kind (XY).</param>
/// <param name="IsSubmodule">The <c>sub</c> field starts with <c>S</c>: a submodule, resolved in a terminal (11.8).</param>
/// <param name="BaseMode">Stage 1's mode (<c>m1</c>), as git prints it in octal.</param>
/// <param name="OursMode">Stage 2's mode (<c>m2</c>); <c>120000</c> for a symbolic link.</param>
/// <param name="TheirsMode">Stage 3's mode (<c>m3</c>).</param>
/// <param name="BaseId">Stage 1's object id (<c>h1</c>).</param>
/// <param name="OursId">Stage 2's object id (<c>h2</c>).</param>
/// <param name="TheirsId">Stage 3's object id (<c>h3</c>).</param>
public sealed record ConflictEntry(
    string Path,
    ConflictKind Kind,
    bool IsSubmodule,
    string BaseMode,
    string OursMode,
    string TheirsMode,
    string BaseId,
    string OursId,
    string TheirsId)
{
    /// <summary>Whether a stage's mode or id is git's "missing" (all zeros).</summary>
    public static bool IsMissing(string modeOrId)
    {
        ArgumentNullException.ThrowIfNull(modeOrId);
        return modeOrId.Length > 0 && modeOrId.All(c => c == '0');
    }

    /// <summary>The base stage exists (not for an add/add).</summary>
    public bool HasBase => !IsMissing(BaseId);

    /// <summary>Ours has the file.</summary>
    public bool HasOurs => !IsMissing(OursId);

    /// <summary>Theirs has the file.</summary>
    public bool HasTheirs => !IsMissing(TheirsId);
}

/// <summary>A side of a conflict: stage 2 (ours) or stage 3 (theirs) (0.4, 11.4).</summary>
public enum ConflictSide
{
    Ours,
    Theirs,
}

/// <summary>What the resolver reads of one conflicted file when it opens it (11.3, 11.8).</summary>
/// <param name="WorkingTree">The working-tree file's bytes, as git (or rerere, or the user) left it; empty when it does not exist.</param>
/// <param name="MarkerSize">The conflict marker size for the path (<c>git check-attr conflict-marker-size</c>, default 7).</param>
/// <param name="OursBinary">Stage 2 is binary as git decides (a NUL in its first 8000 bytes).</param>
/// <param name="TheirsBinary">Stage 3 is binary.</param>
/// <param name="IsSymbolicLink">A side is a symbolic link (mode <c>120000</c>).</param>
/// <param name="WorkingTreeExists">The working-tree file exists.</param>
public sealed record ConflictContent(byte[] WorkingTree, int MarkerSize, bool OursBinary, bool TheirsBinary, bool IsSymbolicLink, bool WorkingTreeExists);

/// <summary>How the external merge tool ended (11.9).</summary>
/// <param name="Staged">Git staged the file afterwards (the tool resolved it); false when the tool closed without resolving it.</param>
public sealed record MergeToolResult(bool Staged);
