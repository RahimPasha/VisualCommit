using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
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
/// U+200B (ZERO WIDTH SPACE, class ZW: a break after it) but between a UNC path's two leading
/// backslashes, and never inside a surrogate pair. A line then breaks before a path, after a
/// separator or after the path; only a name longer than the line breaks where it must. Both marks
/// have no advance in Inter (0.8).
/// </para>
/// <para>
/// A path ends at white space there, as a path inside running text must. Where the text is one
/// path and nothing else, <see cref="IsOnePath"/> marks all of it, spaces included: phase 8a's
/// gate saw the first-run screen's path break at the space of <c>Visual Git</c> and after the
/// hyphen of <c>net10.0-windows</c>.
/// </para>
/// </summary>
public sealed partial class PathTextBlock : TextBlock
{
    /// <summary>ZERO WIDTH NO-BREAK SPACE: no line break on either side, not drawn (as <see cref="UrlTextBlock.WordJoiner"/>).</summary>
    public const char NoBreak = '\uFEFF';

    /// <summary>ZERO WIDTH SPACE: a line may break after it, not drawn.</summary>
    public const char BreakAfter = '\u200B';

    /// <summary>Defines the <see cref="IsOnePath"/> property.</summary>
    public static readonly StyledProperty<bool> IsOnePathProperty =
        AvaloniaProperty.Register<PathTextBlock, bool>(nameof(IsOnePath));

    /// <summary>
    /// The whole text is one path and nothing else, such as the first-run screen's
    /// <c>GitSetupPath</c>: it is marked from its first character to its last
    /// (<see cref="MarkAsOnePath"/>), so that a line breaks only after a separator, also where the
    /// path holds a space or a hyphen. False, the default, for running text: each rooted path in it
    /// is found and marked, and ends before white space (<see cref="BreakOnlyAfterSeparators"/>), so
    /// that the words around it keep their breaks; a path with a space is then marked only up to the
    /// space, and its rest breaks as plain text does.
    /// </summary>
    public bool IsOnePath
    {
        get => GetValue(IsOnePathProperty);
        set => SetValue(IsOnePathProperty, value);
    }

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

    /// <summary>
    /// <paramref name="text"/>, all of it taken as one path, marked as
    /// <see cref="BreakOnlyAfterSeparators"/> marks each rooted path it finds, the spaces and
    /// hyphens of a name included: a line breaks only after <c>\</c> or <c>/</c> (never between a
    /// UNC path's two leading backslashes), and only a name longer than the line breaks where it
    /// must. The text need not be rooted (a name, a relative path). Its own line breaks (CR, LF) get
    /// no mark beside them, so that CR LF stays one break; each line between them is marked on its own.
    /// </summary>
    public static string MarkAsOnePath(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return LineOfText().Replace(text, match => Mark(match.Value));
    }

    protected override TextLayout CreateTextLayout(string? text) =>
        base.CreateTextLayout(text is null ? null : IsOnePath ? MarkAsOnePath(text) : BreakOnlyAfterSeparators(text));

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // The marks live in the layout alone: the other mode needs a layout of its own.
        if (change.Property == IsOnePathProperty)
        {
            InvalidateTextLayout();
        }
    }

    private static string Mark(string path)
    {
        var marked = new StringBuilder(path.Length * 2);
        for (var i = 0; i < path.Length; i++)
        {
            var character = path[i];
            marked.Append(character);

            // Never between the two halves of a surrogate pair, and nothing after the last character.
            // A UNC path's two leading backslashes stay together: a line ending in a lone "\" and
            // the next starting "\server\…" would read as a rooted local path.
            if (i < path.Length - 1 && !char.IsHighSurrogate(character))
            {
                var uncPrefix = i == 0 && path.StartsWith(@"\\", StringComparison.Ordinal);
                marked.Append(character is '\\' or '/' && !uncPrefix ? BreakAfter : NoBreak);
            }
        }

        return marked.ToString();
    }

    [GeneratedRegex(@"(?<![\w])[A-Za-z]:[\\/][^\s""'<>|?*]*|\\\\[^\s""'<>|?*\\]+\\[^\s""'<>|?*]*|(?<=^|[\s""'(\[<])/[^\s""'<>|?*]+", RegexOptions.CultureInvariant)]
    private static partial Regex RootedPath();

    /// <summary>A run of characters between the text's own line breaks.</summary>
    [GeneratedRegex(@"[^\r\n]+", RegexOptions.CultureInvariant)]
    private static partial Regex LineOfText();
}
