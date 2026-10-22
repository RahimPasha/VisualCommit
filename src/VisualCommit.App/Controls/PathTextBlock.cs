using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Media.TextFormatting;

namespace VisualCommit.App.Controls;

/// <summary>
/// A wrapping text block in which a long folder breaks only after a directory separator
/// (docs/phase-6-design.md 13.2, D227, phase 1's report on check 11): Unicode's line breaking
/// (UAX #14) allows a break after <c>C:</c> and after a hyphen inside a name, but none after
/// <c>\</c>, so a Windows path broke at <c>C:</c> or inside <c>plain-a373a2259cd0</c>. Built as
/// <see cref="UrlTextBlock"/>: only the drawn text changes; <see cref="TextBlock.Text"/>, the
/// automation name and what the tests read stay the text as given. Styled as a plain
/// <see cref="TextBlock"/>, so <c>TextBlock.error</c> applies.
/// <para>
/// Inside each rooted path it puts U+FEFF (ZERO WIDTH NO-BREAK SPACE, class WJ: no break before
/// or after it) between every two characters, except after <c>\</c> or <c>/</c>, where it puts
/// U+200B (ZERO WIDTH SPACE, class ZW: a break after it), and never inside a surrogate pair. A
/// line then breaks before a path, after a separator or after the path; only a name longer than
/// the line breaks where it must. Both marks have no advance in Inter (0.8).
/// </para>
/// </summary>
public sealed partial class PathTextBlock : TextBlock
{
    /// <summary>ZERO WIDTH NO-BREAK SPACE: no line break on either side, not drawn (as <see cref="UrlTextBlock.WordJoiner"/>).</summary>
    public const char NoBreak = '\uFEFF';

    /// <summary>ZERO WIDTH SPACE: a line may break after it, not drawn.</summary>
    public const char BreakAfter = '\u200B';

    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <summary>
    /// <paramref name="text"/> with every rooted path in it marked so that a line breaks only
    /// after one of its separators (<see cref="NoBreak"/> and <see cref="BreakAfter"/> as the type
    /// says); the rest, URLs included, unchanged. A rooted path is, in this order: a drive path
    /// (<c>C:\…</c>, <c>C:/…</c>, also the <c>C:/…</c> of a <c>file:///C:/…</c> URL); a UNC path
    /// (<c>\\server\share\…</c>); a Unix path at the start or after a space, a quote or an
    /// opening bracket (so <c>and/or</c> is none). Each ends before white space, a quote, an angle
    /// bracket, <c>|</c>, <c>?</c> or <c>*</c>.
    /// </summary>
    public static string BreakOnlyAfterSeparators(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return RootedPath().Replace(text, match => Mark(match.Value));
    }

    protected override TextLayout CreateTextLayout(string? text) =>
        base.CreateTextLayout(text is null ? null : BreakOnlyAfterSeparators(text));

    private static string Mark(string path)
    {
        var marked = new StringBuilder(path.Length * 2);
        for (var i = 0; i < path.Length; i++)
        {
            var character = path[i];
            marked.Append(character);

            // Never between the two halves of a surrogate pair, and nothing after the last character.
            if (i < path.Length - 1 && !char.IsHighSurrogate(character))
            {
                marked.Append(character is '\\' or '/' ? BreakAfter : NoBreak);
            }
        }

        return marked.ToString();
    }

    [GeneratedRegex(@"(?<![\w])[A-Za-z]:[\\/][^\s""'<>|?*]*|\\\\[^\s""'<>|?*\\]+\\[^\s""'<>|?*]*|(?<=^|[\s""'(\[<])/[^\s""'<>|?*]+", RegexOptions.CultureInvariant)]
    private static partial Regex RootedPath();
}
