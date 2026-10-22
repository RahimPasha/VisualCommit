using Avalonia.Controls;

namespace VisualCommit.Testing.Headless;

/// <summary>One line a text block draws: its text and whether it ends one of the text's own lines.</summary>
/// <param name="Text">The line's characters as drawn, without the marks that keep a URL or a path together and without its line break.</param>
/// <param name="EndsWithLineBreak">The line ends where the text itself has a line break, not where it wrapped.</param>
public sealed record DrawnLine(string Text, bool EndsWithLineBreak);

/// <summary>
/// The lines a <see cref="TextBlock"/> draws, read from its layout (docs/phase-6-design.md 13.2,
/// 13.5): where it wrapped and where it cut a text with "…". Shared by the App tests
/// (<c>PathTextBlockTests</c>) and the walk-throughs (phase 1's check 11, part L's L32): the drawn
/// lines are the layout's (<c>TextLayout</c>), which UI Automation does not expose. The marks that
/// <c>UrlTextBlock</c> and <c>PathTextBlock</c> draw between characters (U+FEFF, U+200B) are left
/// out, so a line reads as the text it shows.
/// </summary>
public static class DrawnLines
{
    /// <summary>U+FEFF, which <c>UrlTextBlock</c> and <c>PathTextBlock</c> put between characters that stay on one line.</summary>
    public const char NoBreak = '﻿';

    /// <summary>U+200B, which <c>PathTextBlock</c> puts after a directory separator.</summary>
    public const char BreakAfter = '​';

    /// <summary>The texts of the lines <paramref name="block"/> draws, top to bottom, each without its marks and its line break.</summary>
    public static IReadOnlyList<string> Of(TextBlock block) => [.. LinesOf(block).Select(line => line.Text)];

    /// <summary>The lines <paramref name="block"/> draws, with whether each ends one of the text's own lines.</summary>
    public static IReadOnlyList<DrawnLine> LinesOf(TextBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return [.. block.TextLayout.TextLines.Select(line =>
        {
            var text = WithoutMarks(string.Concat(line.TextRuns.Select(run => run.Text.ToString())));
            var trimmed = text.TrimEnd('\r', '\n');
            return new DrawnLine(trimmed, line.NewLineLength > 0 || trimmed.Length != text.Length);
        })];
    }

    /// <summary><paramref name="text"/> without the marks <see cref="NoBreak"/> and <see cref="BreakAfter"/>.</summary>
    public static string WithoutMarks(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Replace(NoBreak.ToString(), string.Empty, StringComparison.Ordinal)
            .Replace(BreakAfter.ToString(), string.Empty, StringComparison.Ordinal);
    }
}
