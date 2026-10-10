using System.Globalization;
using System.Text;

namespace VisualCommit.Core.Diff;

/// <summary>A line of a <see cref="FileDiff"/>: hunk number and line number within the hunk, both from 0.</summary>
public readonly record struct DiffLineRef(int Hunk, int Line);

/// <summary>Which way <c>git apply</c> will apply a patch.</summary>
public enum PatchDirection
{
    /// <summary>As it is: to stage changes of an unstaged diff (<c>git apply --cached</c>).</summary>
    Forward,

    /// <summary>
    /// With <c>--reverse</c>: to unstage changes of a staged diff (<c>--cached --reverse</c>) or to
    /// discard changes of an unstaged diff from the working tree (<c>--reverse</c>).
    /// </summary>
    Reverse,
}

/// <summary>
/// Builds the patch that stages, unstages or discards some of a file's changed lines (D66): a whole
/// hunk is all of its changed lines. The patch keeps git's own header lines, so paths and modes
/// are exactly as git wrote them, and is made of the lines' own bytes (Latin-1 characters).
/// <para>
/// The side the patch is applied to keeps the changes that are not chosen. Applied forward (to
/// the index, from an unstaged diff), an unchosen removed line stays as context and an unchosen
/// added line is left out. Applied in reverse (the side applied to is the diff's new side), an
/// unchosen added line stays as context and an unchosen removed line is left out. Hunks without
/// a chosen line are left out, and the line numbers of the hunks that follow are moved by what
/// the left-out changes would have added or removed.
/// </para>
/// </summary>
public static class PatchBuilder
{
    private const string NoNewlineMarker = "\\ No newline at end of file";

    /// <summary>
    /// The patch for the changed lines in <paramref name="chosen"/> (context lines in it are
    /// ignored), or null when it holds no changed line of <paramref name="diff"/>.
    /// </summary>
    public static string? Build(FileDiff diff, IReadOnlySet<DiffLineRef> chosen, PatchDirection direction)
    {
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(chosen);

        var body = new StringBuilder();
        var delta = 0;
        var allChosen = true;
        var anyChosen = false;
        for (var h = 0; h < diff.Hunks.Count; h++)
        {
            var hunk = diff.Hunks[h];
            var lines = new List<(char Marker, DiffLine Line)>();
            var oldCount = 0;
            var newCount = 0;
            var hunkHasChoice = false;
            for (var l = 0; l < hunk.Lines.Count; l++)
            {
                var line = hunk.Lines[l];
                var isChosen = line.IsChange && chosen.Contains(new DiffLineRef(h, l));
                if (line.IsChange)
                {
                    hunkHasChoice |= isChosen;
                    allChosen &= isChosen;
                }

                var marker = (line.Kind, isChosen, direction) switch
                {
                    (DiffLineKind.Context, _, _) => ' ',
                    (DiffLineKind.Added, true, _) => '+',
                    (DiffLineKind.Removed, true, _) => '-',
                    (DiffLineKind.Added, false, PatchDirection.Forward) => '\0',
                    (DiffLineKind.Removed, false, PatchDirection.Forward) => ' ',
                    (DiffLineKind.Added, false, PatchDirection.Reverse) => ' ',
                    (DiffLineKind.Removed, false, PatchDirection.Reverse) => '\0',
                    _ => '\0',
                };

                if (marker == '\0')
                {
                    continue;
                }

                lines.Add((marker, line));
                oldCount += marker is ' ' or '-' ? 1 : 0;
                newCount += marker is ' ' or '+' ? 1 : 0;
            }

            if (!hunkHasChoice)
            {
                // A hunk left out moves the lines after it on the side being built.
                continue;
            }

            anyChosen = true;
            int oldStart, newStart;
            if (direction == PatchDirection.Forward)
            {
                // The old side is the side applied to and keeps git's numbers.
                oldStart = hunk.OldStart;
                newStart = StartFor(hunk.OldStart + delta, hunk.OldCount, newCount);
            }
            else
            {
                // The new side is the side applied to and keeps git's numbers.
                newStart = hunk.NewStart;
                oldStart = StartFor(hunk.NewStart - delta, hunk.NewCount, oldCount);
            }

            delta += newCount - oldCount;
            body.Append(CultureInfo.InvariantCulture, $"@@ -{Range(oldStart, oldCount)} +{Range(newStart, newCount)} @@\n");
            foreach (var (marker, line) in lines)
            {
                body.Append(marker).Append(line.Raw).Append('\n');
                if (line.NoNewlineAtEnd)
                {
                    body.Append(NoNewlineMarker).Append('\n');
                }
            }
        }

        if (!anyChosen)
        {
            return null;
        }

        return string.Concat(Header(diff, direction, allChosen).Select(line => line + "\n")) + body;
    }

    /// <summary>
    /// The start of a range of <paramref name="count"/> lines that begins where a range of git's
    /// began: git numbers an empty range by the line before it.
    /// </summary>
    private static int StartFor(int start, int gitCount, int count)
    {
        // Git's start for an empty range is one less than for a range with lines.
        var first = gitCount == 0 ? start + 1 : start;
        return count == 0 ? Math.Max(0, first - 1) : first;
    }

    private static string Range(int start, int count) =>
        count == 1 ? start.ToString(CultureInfo.InvariantCulture) : string.Create(CultureInfo.InvariantCulture, $"{start},{count}");

    /// <summary>
    /// Git's header lines, kept as they are when the patch still creates or deletes the whole file
    /// the way the diff does, and turned into the header of a change to the file at its new path
    /// otherwise: a rename's hunk changes only the content of the renamed file, and a creation or
    /// deletion of which only some lines are applied leaves the file in place.
    /// </summary>
    private static IEnumerable<string> Header(FileDiff diff, PatchDirection direction, bool allChosen)
    {
        var renamed = diff.HeaderLines.Any(line => line.StartsWith("rename from ", StringComparison.Ordinal) || line.StartsWith("copy from ", StringComparison.Ordinal));
        var keepsFileWhole =
            !(direction == PatchDirection.Forward && diff.IsDeletedFile && !allChosen) &&
            !(direction == PatchDirection.Reverse && diff.IsNewFile && !allChosen);
        if (!renamed && keepsFileWhole)
        {
            return diff.HeaderLines;
        }

        // The file's path as git wrote it on the side that is kept: the new side, or the old one
        // when the new side is /dev/null.
        var plus = diff.HeaderLines.FirstOrDefault(line => line.StartsWith("+++ ", StringComparison.Ordinal) && line != "+++ /dev/null");
        var minus = diff.HeaderLines.FirstOrDefault(line => line.StartsWith("--- ", StringComparison.Ordinal) && line != "--- /dev/null");
        var path = plus is not null ? WithoutPrefix(plus[4..], 'b') : WithoutPrefix(minus![4..], 'a');

        // No "index" line: the file's mode stays as it is where the patch is applied.
        return
        [
            $"diff --git {WithPrefix(path, 'a')} {WithPrefix(path, 'b')}",
            $"--- {WithPrefix(path, 'a')}",
            $"+++ {WithPrefix(path, 'b')}",
        ];
    }

    /// <summary>A path from a <c>---</c> or <c>+++</c> line without its <c>a/</c> or <c>b/</c>, keeping git's quotes when it has them.</summary>
    private static string WithoutPrefix(string side, char prefix) =>
        side.StartsWith('"') && side.Length > 3 && side[1] == prefix && side[2] == '/'
            ? "\"" + side[3..]
            : side.Length > 2 && side[0] == prefix && side[1] == '/' ? side[2..] : side;

    private static string WithPrefix(string path, char prefix) =>
        path.StartsWith('"') ? $"\"{prefix}/{path[1..]}" : $"{prefix}/{path}";
}
