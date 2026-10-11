using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Media.TextFormatting;

namespace VisualCommit.App.Controls;

/// <summary>
/// A wrapping text block that never breaks a line inside a URL (<c>http://127.0.0.1:47391</c>,
/// <c>https://host/x.git</c>) or an ssh address (<c>git@host:owner/x.git</c>): Unicode's line
/// breaking allows a break after "/", which put the host on a line of its own (check 21). The
/// line breaks before the URL instead, and only a URL longer than a whole line is broken where
/// it must be. Only the drawn text changes: <see cref="TextBlock.Text"/>, the automation name and
/// what the tests read stay the text as given. Styled as a plain <see cref="TextBlock"/>.
/// </summary>
public sealed partial class UrlTextBlock : TextBlock
{
    /// <summary>
    /// ZERO WIDTH NO-BREAK SPACE, of the line breaking class WJ: no line break before or after it,
    /// and it is not drawn. Not U+2060 WORD JOINER, which the app's font (Inter) lacks, so that
    /// every platform would look for it in a font of its own.
    /// </summary>
    public const char WordJoiner = '﻿';

    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <summary>
    /// <paramref name="text"/> with <see cref="WordJoiner"/> between every two characters of each
    /// URL and ssh address in it, so that no line breaks inside them; the rest unchanged.
    /// </summary>
    public static string KeepUrlsTogether(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return UrlPattern().Replace(text, match => Join(match.Value));
    }

    protected override TextLayout CreateTextLayout(string? text) =>
        base.CreateTextLayout(text is null ? null : KeepUrlsTogether(text));

    private static string Join(string url)
    {
        var joined = new StringBuilder(url.Length * 2);
        for (var i = 0; i < url.Length; i++)
        {
            joined.Append(url[i]);

            // Never between the two halves of a surrogate pair.
            if (i < url.Length - 1 && !char.IsHighSurrogate(url[i]))
            {
                joined.Append(WordJoiner);
            }
        }

        return joined.ToString();
    }

    /// <summary>A URL with a scheme, or an ssh address <c>user@host:path</c>; it ends before white space, a quote or an angle bracket.</summary>
    [GeneratedRegex(@"[A-Za-z][A-Za-z0-9+.\-]*://[^\s'""<>]+|[\w.\-]+@[\w.\-]+:[^\s'""<>]+", RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();
}
