namespace VisualCommit.Core.Diff;

/// <summary>What a line of a hunk is.</summary>
public enum DiffLineKind
{
    Context,
    Added,
    Removed,
}

/// <summary>
/// One line of a hunk. <see cref="Raw"/> holds its bytes, one character per byte (Latin-1, D66),
/// without git's marker and without the line feed, but with a carriage return the file has; a
/// patch is built from it. <see cref="Text"/> is the same line decoded for display.
/// </summary>
/// <param name="Kind">Context, added or removed.</param>
/// <param name="Raw">The line's bytes as Latin-1 characters.</param>
/// <param name="Text">The line as shown: decoded, without a trailing carriage return.</param>
/// <param name="OldNumber">Its number in the old file, or null for an added line.</param>
/// <param name="NewNumber">Its number in the new file, or null for a removed line.</param>
/// <param name="NoNewlineAtEnd">Git printed "\ No newline at end of file" after it: it is the last line of its side and has no line feed.</param>
public sealed record DiffLine(
    DiffLineKind Kind,
    string Raw,
    string Text,
    int? OldNumber,
    int? NewNumber,
    bool NoNewlineAtEnd = false)
{
    /// <summary>An added or removed line: one that can be staged, unstaged or discarded.</summary>
    public bool IsChange => Kind != DiffLineKind.Context;
}

/// <summary>One hunk of a file's diff: its header and its lines.</summary>
/// <param name="HeaderRaw">Git's header line as Latin-1 characters, such as <c>@@ -12,7 +12,7 @@ public sealed class Calculator</c>.</param>
/// <param name="Header">The header line decoded for display.</param>
/// <param name="OldStart">The first old line the hunk covers (0 when it covers none).</param>
/// <param name="OldCount">How many old lines it covers.</param>
/// <param name="NewStart">The first new line it covers (0 when it covers none).</param>
/// <param name="NewCount">How many new lines it covers.</param>
/// <param name="Lines">Its lines, in git's order.</param>
public sealed record DiffHunk(
    string HeaderRaw,
    string Header,
    int OldStart,
    int OldCount,
    int NewStart,
    int NewCount,
    IReadOnlyList<DiffLine> Lines);

/// <summary>
/// The diff of one file, as <c>git diff</c> prints it, parsed (<see cref="DiffParser"/>).
/// </summary>
/// <param name="HeaderLines">Git's lines before the first hunk (<c>diff --git</c>, mode, rename, <c>index</c>, <c>---</c> and <c>+++</c>), as Latin-1 characters; patches reuse them (D66).</param>
/// <param name="Hunks">The hunks; none for a binary file, a pure rename or a change of mode only.</param>
/// <param name="IsBinary">Git calls the file binary and prints no lines for it.</param>
/// <param name="IsNewFile">The old side does not exist.</param>
/// <param name="IsDeletedFile">The new side does not exist.</param>
/// <param name="OldMode">The old mode when git names one (<c>old mode</c>, <c>deleted file mode</c> or the <c>index</c> line), else null.</param>
/// <param name="NewMode">The new mode when git names one, else null.</param>
/// <param name="TextLength">The number of bytes of the diff's lines, markers included: what D65's 1 MiB is measured on.</param>
public sealed record FileDiff(
    IReadOnlyList<string> HeaderLines,
    IReadOnlyList<DiffHunk> Hunks,
    bool IsBinary,
    bool IsNewFile,
    bool IsDeletedFile,
    string? OldMode,
    string? NewMode,
    long TextLength)
{
    /// <summary>A diff of more lines than this is very large (D65).</summary>
    public const int VeryLargeLineCount = 10_000;

    /// <summary>A diff of more bytes than this is very large (D65): 1 MiB.</summary>
    public const long VeryLargeTextLength = 1024 * 1024;

    /// <summary>A diff with nothing in it: the file has no changes on this side.</summary>
    public static FileDiff Empty { get; } = new([], [], false, false, false, null, null, 0);

    /// <summary>Nothing to show or to act on: no hunks, not binary, the mode unchanged.</summary>
    public bool IsEmpty => Hunks.Count == 0 && !IsBinary && !IsNewFile && !IsDeletedFile && OldMode == NewMode;

    /// <summary>The number of lines in all hunks, headers not counted.</summary>
    public int LineCount => Hunks.Sum(hunk => hunk.Lines.Count);

    /// <summary>The number of added lines.</summary>
    public int AddedCount => Hunks.Sum(hunk => hunk.Lines.Count(line => line.Kind == DiffLineKind.Added));

    /// <summary>The number of removed lines.</summary>
    public int RemovedCount => Hunks.Sum(hunk => hunk.Lines.Count(line => line.Kind == DiffLineKind.Removed));

    /// <summary>Too large to show with highlighting until the user asks (D65).</summary>
    public bool IsVeryLarge => LineCount > VeryLargeLineCount || TextLength > VeryLargeTextLength;
}
