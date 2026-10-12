using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaEdit.Document;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels.Conflicts;
using VisualCommit.App.Views.Conflicts;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// The conflict resolver as a check reads and drives it (docs/phase-4-design.md 13.2, package
/// WP6): the resolver as a snapshot (<see cref="Resolver"/>), a bar's button
/// (<see cref="ClickBarButton"/>), the result's text, caret, tints and modified state, typing into
/// the result, a click at a line and column (<see cref="ClickResultAt"/>), the panes' rows, the
/// card's buttons, and waiting for it to open, to close and to parse what was typed. Built on the
/// controls' test surface (<see cref="ConflictPanes.RowsOf"/>, <see cref="ConflictPanes.BarButton"/>,
/// <see cref="ConflictResultEditor.PointAt"/>, <see cref="ConflictResultEditor.TintAt"/>,
/// <see cref="ConflictResultEditor.CaretPosition"/>) and the automation ids of 7.4.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The resolver's root (7.4).</summary>
    public const string ResolverId = "ConflictResolver";

    /// <summary>The resolver's view while it is shown, or null.</summary>
    public ConflictResolverView? ShownResolver =>
        Window.GetVisualDescendants().OfType<ConflictResolverView>().FirstOrDefault(view => view.IsEffectivelyVisible && view.DataContext is ConflictResolverViewModel { IsOpen: true });

    /// <summary>The resolver's view; throws when it is not shown.</summary>
    public ConflictResolverView ResolverView => ShownResolver ?? throw new InvalidOperationException("The conflict resolver is not shown.");

    /// <summary>The shown resolver's view model (<c>IsParsePending</c>, <c>IsModified</c>, the conflicts it read).</summary>
    public ConflictResolverViewModel ResolverModel => (ConflictResolverViewModel)ResolverView.DataContext!;

    /// <summary>The shown resolver's result (<c>ConflictResult</c>).</summary>
    public ConflictResultEditor ResultEditor => ResolverView.ResultEditor;

    /// <summary>The shown resolver's ours and theirs panes.</summary>
    public ConflictPanes ResolverPanes => ResolverView.ConflictPanes;

    /// <summary>The result's text, line ends included (<c>ConflictResultEditor.Text</c>).</summary>
    public string ResultText => ResultEditor.Text;

    /// <summary>The result's caret: line and column, both from 1.</summary>
    public TextLocation ResultCaret => ResultEditor.CaretPosition;

    /// <summary>Whether the result differs from the file as the resolver last read or wrote it.</summary>
    public bool ResultIsModified => ResultEditor.IsModified;

    /// <summary>The tint of the result's line <paramref name="line"/> (from 1).</summary>
    public ConflictTint ResultTintAt(int line) => ResultEditor.TintAt(line);

    /// <summary>The rows of a pane, top to bottom, with their tints and the bars' states.</summary>
    public IReadOnlyList<ConflictPaneRow> PaneRows(ConflictPaneSide side) => ResolverPanes.RowsOf(side);

    /// <summary>Everything the checks of part C read of the shown resolver, at this moment.</summary>
    public ResolverSnapshot Resolver()
    {
        Settle();
        var view = ResolverView;
        var model = ResolverModel;
        var editor = view.ResultEditor;
        var lines = editor.Document is { } document ? document.LineCount : 0;
        return new ResolverSnapshot(
            TextOf("ResolverFileStatus"),
            TextOf("ResolverFileName"),
            TextOf("ResolverFolder"),
            TextOf("ResolverKind"),
            TextOf("ResolverFilePosition"),
            model.ShowsConflictTools ? TextOf("ResolverConflictPosition") : null,
            ButtonOf("ResolverTakeOurs"),
            ButtonOf("ResolverTakeTheirs"),
            ButtonOf("ResolverMergeTool"),
            ButtonOf("ResolverMarkResolved"),
            model.ShowsText ? TextOf("ConflictOursTitle") : null,
            model.ShowsText ? TextOf("ConflictTheirsTitle") : null,
            model.ShowsText ? TextOf("ConflictResultTitle") : null,
            model.ShowsText ? TextOf("ConflictResultCount") : null,
            model.ShowsText ? editor.Text : null,
            model.ShowsText ? editor.CaretPosition : null,
            model.ShowsText && editor.IsModified,
            model.ShowsText ? [.. Enumerable.Range(1, lines).Select(editor.TintAt)] : [],
            model.ShowsText ? view.ConflictPanes.RowsOf(ConflictPaneSide.Ours) : [],
            model.ShowsText ? view.ConflictPanes.RowsOf(ConflictPaneSide.Theirs) : [],
            model.ShowsCard ? TextOf("ConflictCardText") : null,
            model.ShowsCard ? [.. CardButtons().Select(button => (string)button.Content!)] : [],
            editor.TextArea.IsKeyboardFocusWithin,
            model.IsParsePending);

        string? TextOf(string id) => FindIn(view, id) is TextBlock { IsEffectivelyVisible: true } text ? text.Text : null;

        ResolverButton? ButtonOf(string id) => FindIn(view, id) is Button { IsEffectivelyVisible: true } button
            ? new ResolverButton((string?)button.Content ?? string.Empty, button.IsEffectivelyEnabled, ToolTip.GetTip(button) as string)
            : null;
    }

    /// <summary>Waits until the resolver shows <paramref name="fileName"/> (any file when null) and has laid out what it read.</summary>
    public async Task WaitForResolverAsync(string? fileName = null, TimeSpan? timeout = null)
    {
        await WaitForAsync(
            () => ShownResolver is { } view && view.DataContext is ConflictResolverViewModel { Body: not ConflictResolverBody.None } model
                && (fileName is null || model.FileName == fileName)
                && (!model.ShowsText || (view.ResultEditor.Document is not null && view.ResultEditor.Bounds.Height > 0)),
            fileName is null ? "the conflict resolver" : $"the conflict resolver on {fileName}",
            timeout);
        Capture();
    }

    /// <summary>Waits until no resolver is shown.</summary>
    public Task WaitForResolverClosedAsync(TimeSpan? timeout = null) =>
        WaitForAsync(() => ShownResolver is null, "the conflict resolver to close", timeout);

    /// <summary>Waits until the resolver has parsed what was typed into the result (<c>IsParsePending</c> false, design 14.3).</summary>
    public Task WaitForResultParsedAsync(TimeSpan? timeout = null) =>
        WaitForAsync(() => ShownResolver?.DataContext is ConflictResolverViewModel { IsParsePending: false }, "the resolver to parse the result", timeout);

    /// <summary>
    /// The button of block <paramref name="block"/>'s bar (from 0, in the file's order) in
    /// <paramref name="side"/>'s pane that asks for <paramref name="choice"/>; throws when that bar
    /// is not in view or shows its state instead of its buttons.
    /// </summary>
    public Button BarButton(int block, ConflictPaneSide side, ConflictChoice choice)
    {
        Capture();
        return ResolverPanes.BarButton(block, side, choice)
            ?? throw new InvalidOperationException($"Block {block + 1}'s {side} bar has no {ConflictPanes.AutomationIdOf(side, choice) ?? choice.ToString()} in view ({ResolverPanes.BarText(block, side) ?? "its buttons are not in view"}).");
    }

    /// <summary>Clicks a bar's button (<see cref="BarButton"/>) as a user would.</summary>
    public void ClickBarButton(int block, ConflictPaneSide side, ConflictChoice choice) => Click(BarButton(block, side, choice));

    /// <summary>What block <paramref name="block"/>'s bar says in <paramref name="side"/>'s pane (<c>Used in the result</c>, <c>Not used</c>), or null while it shows its buttons.</summary>
    public string? BarText(int block, ConflictPaneSide side)
    {
        Capture();
        return ResolverPanes.BarText(block, side);
    }

    /// <summary>
    /// Where the caret goes for line <paramref name="line"/> and column <paramref name="column"/>
    /// of the result (both from 1), in the window; throws when the line is not in view.
    /// </summary>
    public Point ResultPoint(int line, int column)
    {
        Capture();
        var editor = ResultEditor;
        var point = editor.PointAt(line, column) ?? throw new InvalidOperationException($"Line {line} of the result is not in view.");
        return editor.TranslatePoint(point, Window) ?? throw new InvalidOperationException("The result is not in the window.");
    }

    /// <summary>Clicks in the result at line <paramref name="line"/>, column <paramref name="column"/>: the caret goes there and the result takes the focus.</summary>
    public void ClickResultAt(int line, int column) => Click(ResultPoint(line, column));

    /// <summary>Right-clicks in the result at a line and column: the result's menu opens there (11.12).</summary>
    public void RightClickResultAt(int line, int column) => RightClick(ResultPoint(line, column));

    /// <summary>The centre of row <paramref name="row"/> of a pane (from 0, bars included), 100 right of the pane's left edge, in the window.</summary>
    public Point PaneRowPoint(ConflictPaneSide side, int row)
    {
        Capture();
        var panes = ResolverPanes;
        var bounds = panes.RowBounds(side, row) ?? throw new InvalidOperationException($"Row {row} of the {side} pane is not in view.");
        return panes.TranslatePoint(new Point(bounds.X + 100, bounds.Center.Y), Window) ?? throw new InvalidOperationException("The panes are not in the window.");
    }

    /// <summary>Types <paramref name="text"/> into the result, which must have the keyboard focus; a line feed presses Enter.</summary>
    public void TypeInResult(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!ResultEditor.TextArea.IsKeyboardFocusWithin)
        {
            throw new InvalidOperationException("The result does not have the keyboard focus: click in it first.");
        }

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                PressKey(Key.Enter);
            }

            if (lines[i].Length > 0)
            {
                Type(lines[i]);
            }
        }
    }

    /// <summary>Clicks a button of the resolver's header, toolbar or card by its automation id (7.4); throws when it is hidden or disabled.</summary>
    public void ClickResolverButton(string automationId)
    {
        Settle();
        if (FindIn(ResolverView, automationId) is not Button { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } button)
        {
            throw new InvalidOperationException($"The resolver's {automationId} cannot be clicked: it is not shown or not enabled.");
        }

        Click(button);
    }

    /// <summary>The card's buttons, left to right.</summary>
    public IReadOnlyList<Button> CardButtons() =>
        [.. FindIn(ResolverView, "ConflictCard") is { } card ? card.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible) : []];

    /// <summary>Clicks the card's button with this automation id (<c>ConflictKeepFile</c>, <c>ConflictUseTheirsFile</c>, …).</summary>
    public void ClickCardButton(string automationId)
    {
        var button = CardButtons().FirstOrDefault(candidate => AutomationProperties.GetAutomationId(candidate) == automationId)
            ?? throw new InvalidOperationException($"The card has no {automationId}.");
        Click(button);
    }
}

/// <summary>A button of the resolver's header or toolbar as a check reads it.</summary>
/// <param name="Label">What it says.</param>
/// <param name="IsEnabled">Whether it can be clicked.</param>
/// <param name="ToolTip">Its tooltip: a disabled button's reason.</param>
public sealed record ResolverButton(string Label, bool IsEnabled, string? ToolTip);

/// <summary>The conflict resolver as the checks of part C read it (docs/phase-4-design.md 11.2, 7.4).</summary>
/// <param name="Status">The header's <c>U</c> (<c>ResolverFileStatus</c>).</param>
/// <param name="FileName"><c>ResolverFileName</c>.</param>
/// <param name="Folder"><c>ResolverFolder</c>, empty at the working tree's top.</param>
/// <param name="Kind"><c>ResolverKind</c>, in git's words.</param>
/// <param name="FilePosition"><c>File {i} of {n}</c>.</param>
/// <param name="ConflictPosition"><c>Conflict {k} of {m}</c> or <c>No conflicts left</c>; null while the toolbar does not show it (a card).</param>
/// <param name="TakeOurs">The toolbar's Take ours, or null while hidden.</param>
/// <param name="TakeTheirs">Take theirs, or null while hidden.</param>
/// <param name="MergeTool"><c>Merge tool…</c>, or null while hidden.</param>
/// <param name="MarkResolved">Mark resolved, or null while hidden.</param>
/// <param name="OursTitle"><c>ConflictOursTitle</c>, or null for a card.</param>
/// <param name="TheirsTitle"><c>ConflictTheirsTitle</c>, or null for a card.</param>
/// <param name="ResultTitle"><c>ConflictResultTitle</c>, or null for a card.</param>
/// <param name="ResultCount"><c>ConflictResultCount</c>, or null for a card.</param>
/// <param name="Result">The result's text, or null for a card.</param>
/// <param name="Caret">The result's caret (line, column from 1), or null for a card.</param>
/// <param name="IsModified">The result differs from the file as read or written.</param>
/// <param name="ResultTints">The tint of each of the result's lines, line 1 first.</param>
/// <param name="OursRows">The ours pane's rows.</param>
/// <param name="TheirsRows">The theirs pane's rows.</param>
/// <param name="CardText"><c>ConflictCardText</c>, or null without a card.</param>
/// <param name="CardButtons">The card's buttons' labels, left to right.</param>
/// <param name="ResultHasFocus">The result has the keyboard focus.</param>
/// <param name="IsParsePending">A typed change waits to be parsed.</param>
public sealed record ResolverSnapshot(
    string? Status,
    string? FileName,
    string? Folder,
    string? Kind,
    string? FilePosition,
    string? ConflictPosition,
    ResolverButton? TakeOurs,
    ResolverButton? TakeTheirs,
    ResolverButton? MergeTool,
    ResolverButton? MarkResolved,
    string? OursTitle,
    string? TheirsTitle,
    string? ResultTitle,
    string? ResultCount,
    string? Result,
    TextLocation? Caret,
    bool IsModified,
    IReadOnlyList<ConflictTint> ResultTints,
    IReadOnlyList<ConflictPaneRow> OursRows,
    IReadOnlyList<ConflictPaneRow> TheirsRows,
    string? CardText,
    IReadOnlyList<string> CardButtons,
    bool ResultHasFocus,
    bool IsParsePending);
