using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.Views;
using VisualCommit.Core.Git;

namespace VisualCommit.App.Controls;

/// <summary>
/// Selecting rows (docs/phase-3-design.md 4h, D88): a click selects one row; the platform's
/// command modifier adds or removes a row, Shift selects from the anchor to a row, and Shift with
/// Up, Down, Page Up, Page Down, Home or End extends from the anchor. The selection is kept as
/// SHAs in <see cref="Selection"/>; <see cref="SelectedIndex"/> stays the single selection that
/// phases 1 and 2 bind to and is -1 while several rows are selected. The working-changes row and
/// stash rows never join a multi-selection: a modifier on them acts as a plain click.
/// </summary>
public sealed partial class CommitGraphControl
{
    /// <summary>
    /// A press on a row of a multi-selection without a modifier keeps the multi-selection, so
    /// that a branch label on it can be dragged; the row is selected alone on a release without
    /// a drag (4g). The SHA of that row, until the release or a drag.
    /// </summary>
    private string? _selectOnRelease;

    /// <summary>
    /// The modifier that adds a row to the selection with a click (4h): the platform's command
    /// modifier as Avalonia's hotkey configuration reports it (Ctrl on Windows and Linux, Cmd on
    /// macOS), never a test of the operating system. Control when the control is in no window or
    /// the platform names none.
    /// </summary>
    public KeyModifiers CommandModifiers => ShortcutKeys.CommandModifierOf(this.GetPlatformSettings()?.HotkeyConfiguration);

    /// <summary>
    /// The selection as the user sees it: <see cref="Selection"/> while several rows are selected,
    /// otherwise the row of <see cref="SelectedIndex"/> (with <see cref="Selection"/>'s anchor when
    /// it names that row), or nothing while the working-changes row is selected.
    /// </summary>
    private GraphSelection CurrentSelection
    {
        get
        {
            var data = Data;
            var selection = Selection;
            if (data is null)
            {
                return GraphSelection.Empty;
            }

            if (selection.IsMultiple)
            {
                return selection;
            }

            if ((ShowsWorkingRow && IsWorkingRowSelected) || SelectedIndex < 0 || SelectedIndex >= data.Count)
            {
                return GraphSelection.Empty;
            }

            var sha = data.CommitAt(SelectedIndex).Sha;
            return selection.Single == sha ? selection : GraphSelection.Of(sha);
        }
    }

    /// <summary>Whether commit row <paramref name="index"/> is drawn selected: one of several, or the single selected row.</summary>
    private bool IsRowSelected(CommitGraphData data, int index, GraphSelection selection) =>
        selection.IsMultiple ? selection.Contains(data.CommitAt(index).Sha) : index == SelectedIndex;

    /// <summary>The row of the focused commit (the last row clicked that is still selected), or -1.</summary>
    private int FocusRow
    {
        get
        {
            var data = Data;
            if (data is null)
            {
                return -1;
            }

            var selection = Selection;
            if (selection.IsMultiple && selection.Focus is { } focus)
            {
                return data.IndexOf(focus);
            }

            return SelectedIndex >= 0 && SelectedIndex < data.Count ? SelectedIndex : -1;
        }
    }

    /// <summary>A left press on a drawn row, with the keys held: selects as 4h says.</summary>
    private void PressRow(int display, KeyModifiers modifiers)
    {
        _selectOnRelease = null;
        var data = Data;
        if (data is null)
        {
            return;
        }

        if (display < RowOffset)
        {
            SelectDisplayRow(display);
            return;
        }

        var row = display - RowOffset;
        var commit = data.CommitAt(row);
        var command = (modifiers & CommandModifiers) == CommandModifiers;
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        var current = CurrentSelection;
        if (commit.Kind == CommitKind.Stash || (!command && !shift))
        {
            if (commit.Kind != CommitKind.Stash && !command && !shift && current.IsMultiple && current.Contains(commit.Sha))
            {
                // Kept until the release: a drag may start from here (4g).
                _selectOnRelease = commit.Sha;
                return;
            }

            ApplySelection(GraphSelection.Of(commit.Sha));
            return;
        }

        if (shift)
        {
            ExtendTo(row, current, union: command);
        }
        else
        {
            Toggle(row, current);
        }
    }

    /// <summary>The release that ends a press on a row of a multi-selection without a drag: that row alone is selected.</summary>
    private void ReleaseRow()
    {
        if (_selectOnRelease is { } sha)
        {
            _selectOnRelease = null;
            if (Data is { } data && data.IndexOf(sha) >= 0)
            {
                ApplySelection(GraphSelection.Of(sha));
            }
        }
    }

    /// <summary>The command modifier's click: adds the row to the selection, or removes it; the anchor moves to it.</summary>
    private void Toggle(int row, GraphSelection current)
    {
        var data = Data!;
        var sha = data.CommitAt(row).Sha;
        var shas = CommitsOnly(data, current.Shas);
        if (shas.Remove(sha))
        {
            if (shas.Count == 0)
            {
                ApplySelection(GraphSelection.Empty);
                return;
            }

            // The focus stays where it was, unless it was this row: then it moves to the nearest
            // selected row above it (4h), or else the nearest below.
            var focus = current.Focus is { } kept && kept != sha && shas.Contains(kept) ? kept : NearestTo(data, row, shas);
            ApplySelection(new GraphSelection(shas, anchor: sha, focus: focus));
        }
        else
        {
            shas.Add(sha);
            ApplySelection(new GraphSelection(shas, anchor: sha, focus: sha));
        }
    }

    /// <summary>Shift: the rows from the anchor to <paramref name="row"/>, added to what is selected with the command modifier.</summary>
    private void ExtendTo(int row, GraphSelection current, bool union)
    {
        var data = Data!;
        var sha = data.CommitAt(row).Sha;
        var anchorRow = current.Anchor is { } anchor ? data.IndexOf(anchor) : -1;
        if (anchorRow < 0 || data.CommitAt(anchorRow).Kind == CommitKind.Stash)
        {
            ApplySelection(GraphSelection.Of(sha));
            return;
        }

        var range = CommitsBetween(data, anchorRow, row);
        var shas = union ? CommitsOnly(data, current.Shas).Concat(range) : range;
        ApplySelection(new GraphSelection(shas, anchor: current.Anchor, focus: sha));
    }

    /// <summary>
    /// Shift with a navigation key: the focus moves as the key would move a single selection, and
    /// the selection becomes the rows from the anchor to it. Returns false when there is no
    /// anchor to extend from (nothing, the working-changes row or a stash is selected).
    /// </summary>
    private bool ExtendWithKey(Key key)
    {
        var data = Data;
        var current = CurrentSelection;
        var anchorRow = data is not null && current.Anchor is { } anchor ? data.IndexOf(anchor) : -1;
        if (data is null || anchorRow < 0 || data.CommitAt(anchorRow).Kind == CommitKind.Stash)
        {
            return false;
        }

        var focusRow = current.Focus is { } focus && data.IndexOf(focus) is >= 0 and var index ? index : anchorRow;
        var target = key switch
        {
            Key.Down => focusRow + 1,
            Key.Up => focusRow - 1,
            Key.PageDown => focusRow + RowsPerPage,
            Key.PageUp => focusRow - RowsPerPage,
            Key.Home => 0,
            _ => data.Count - 1,
        };
        target = Math.Clamp(target, 0, data.Count - 1);

        // A stash never joins: the focus stops at the nearest commit towards the anchor.
        var step = target < anchorRow ? 1 : -1;
        while (target != anchorRow && data.CommitAt(target).Kind == CommitKind.Stash)
        {
            target += step;
        }

        ApplySelection(new GraphSelection(CommitsBetween(data, anchorRow, target), anchor: current.Anchor, focus: data.CommitAt(target).Sha));
        ScrollIntoView(target);
        return true;
    }

    /// <summary>
    /// What a right-click selects (4b, D114): the row under the pointer, unless it is part of a
    /// multi-selection, which then stays; the working-changes row; nothing in the empty area.
    /// </summary>
    private void SelectForContext(GraphHit hit)
    {
        switch (hit.Kind)
        {
            case GraphHitKind.WorkingRow when !IsWorkingRowSelected:
                SelectDisplayRow(0);
                break;
            case GraphHitKind.Commit or GraphHitKind.Label when hit.Sha is { } sha:
                if (!(Selection.IsMultiple && Selection.Contains(sha)))
                {
                    ApplySelection(GraphSelection.Of(sha));
                }

                break;
        }
    }

    /// <summary>
    /// Makes <paramref name="selection"/> the selection: <see cref="Selection"/> first, then
    /// <see cref="SelectedIndex"/> (its one row, or -1 for none or several), which a bound view
    /// model has set already from the selection.
    /// </summary>
    private void ApplySelection(GraphSelection selection)
    {
        _selectOnRelease = null;
        if (IsWorkingRowSelected)
        {
            SetCurrentValue(IsWorkingRowSelectedProperty, false);
        }

        SetCurrentValue(SelectionProperty, selection);
        var index = selection.Single is { } sha && Data is { } data ? data.IndexOf(sha) : -1;
        SetCurrentValue(SelectedIndexProperty, index);
    }

    /// <summary>The commits among <paramref name="shas"/>: stashes and commits that are not loaded are left out.</summary>
    private static List<string> CommitsOnly(CommitGraphData data, IEnumerable<string> shas) =>
        [.. shas.Where(sha => data.IndexOf(sha) is >= 0 and var index && data.CommitAt(index).Kind != CommitKind.Stash)];

    /// <summary>The commits of the rows from <paramref name="from"/> to <paramref name="to"/>, both included, in that order, without stashes.</summary>
    private static List<string> CommitsBetween(CommitGraphData data, int from, int to)
    {
        var step = to >= from ? 1 : -1;
        var shas = new List<string>(Math.Abs(to - from) + 1);
        for (var row = from; ; row += step)
        {
            var commit = data.CommitAt(row);
            if (commit.Kind != CommitKind.Stash)
            {
                shas.Add(commit.Sha);
            }

            if (row == to)
            {
                return shas;
            }
        }
    }

    /// <summary>The selected commit nearest above <paramref name="row"/>, or else the nearest below it.</summary>
    private static string NearestTo(CommitGraphData data, int row, List<string> shas)
    {
        var rows = shas.Select(sha => (Sha: sha, Row: data.IndexOf(sha))).ToList();
        var above = rows.Where(entry => entry.Row < row).OrderByDescending(entry => entry.Row).FirstOrDefault();
        return above.Sha ?? rows.OrderBy(entry => Math.Abs(entry.Row - row)).First().Sha;
    }
}
