using Avalonia.Controls.Primitives;
using Avalonia.Media;
using VisualCommit.Core.Diff;

namespace VisualCommit.App.Controls.Diff;

/// <summary>
/// What a <see cref="DiffPane"/> asks of the control it belongs to (docs/phase-4-design.md 11.3):
/// the colours and font, the syntax runs and word highlights of its rows' lines, each row's
/// background, what each header row's bar holds, and whether rows can be selected and the sign
/// column shows. Phase 2's <see cref="DiffTextView"/> and phase 4's conflict panes are hosts.
/// </summary>
internal interface IDiffPaneHost
{
    /// <summary>The control the panes belong to: the bars' buttons take their theme and font from it.</summary>
    TemplatedControl View { get; }

    /// <summary>The palette of the host's theme.</summary>
    DiffPalette Palette { get; }

    /// <summary>The syntax tokens of the rows' lines, or null: they show in the main text colour.</summary>
    DiffSyntax? Syntax { get; }

    /// <summary>The ranges of each line's text drawn with the stronger background; empty for none.</summary>
    IReadOnlyDictionary<DiffLineRef, IReadOnlyList<TextRange>> Words { get; }

    /// <summary>
    /// Whether rows are selected by their numbers and by a drag over the text (the diff, D70). When
    /// false, a click on a number selects nothing and a drag over the text is an ordinary text
    /// selection, drawn in the selection colour.
    /// </summary>
    bool RowsSelectable { get; }

    /// <summary>Whether the gutter has the 16-wide sign column after its number columns.</summary>
    bool ShowsSign { get; }

    /// <summary>A row's background, gutter included; <paramref name="cell"/> is the pane's cell of the row (null for a header row or a filler).</summary>
    IImmutableBrush RowBackground(DiffPane pane, DiffDisplayRow row, DiffCell? cell);

    /// <summary>What the bar over header row <paramref name="header"/> of <paramref name="pane"/> holds.</summary>
    HeaderBarContent HeaderOf(DiffPane pane, DiffDisplayRow header);

    /// <summary>A pane scrolled: the host may make the other follow.</summary>
    void OnPaneScrolled(DiffPane pane);

    /// <summary>A pane's row selection changed.</summary>
    void OnPaneSelectionChanged(DiffPane pane);
}

/// <summary>
/// What a header row's bar holds (<see cref="HunkHeaderBar"/>): a text in the secondary colour, cut
/// with "…" and whole in its tooltip, and buttons. By default the text starts the bar and the
/// buttons sit at its right end (the diff's hunk header); with <paramref name="ButtonsFirst"/> the
/// buttons start the bar and the text follows them (a conflict block's bar).
/// </summary>
/// <param name="Text">The text, or null for no text.</param>
/// <param name="Buttons">The buttons in order, or null for no button area at all.</param>
/// <param name="ButtonsFirst">The buttons start the bar, 6 from its left edge.</param>
/// <param name="TextInCodeFont">The text is in the code font (git's header line); otherwise in the host's font.</param>
/// <param name="TextAutomationId">The text's automation id, or null.</param>
internal sealed record HeaderBarContent(
    string? Text,
    IReadOnlyList<HeaderBarButton>? Buttons,
    bool ButtonsFirst = false,
    bool TextInCodeFont = true,
    string? TextAutomationId = null);

/// <summary>A button of a header row's bar: its label (also its accessible name), automation id and what a click does.</summary>
internal sealed record HeaderBarButton(string Label, string AutomationId, Action Click);
