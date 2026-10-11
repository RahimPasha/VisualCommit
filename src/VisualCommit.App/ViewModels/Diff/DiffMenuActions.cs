using VisualCommit.App.ViewModels.Menus;

namespace VisualCommit.App.ViewModels.Diff;

/// <summary>
/// The open diff's own line and hunk actions as the diff view's menu offers them
/// (docs/phase-3-design.md 4b, D70, D76): the menu's "Stage selected lines", "Discard selected
/// lines…", "Unstage selected lines" and the hunk items run exactly what the diff view's buttons
/// run, on the lines selected and the hunk under the pointer when the menu opened. A click while
/// one of them runs is ignored, as it is for the buttons.
/// </summary>
public sealed class DiffMenuActions : IDiffMenuActions
{
    private readonly DiffViewModel _diff;

    /// <param name="diff">The open diff.</param>
    public DiffMenuActions(DiffViewModel diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        _diff = diff;
    }

    public Task StageLinesAsync() => _diff.StageLinesCommand.ExecuteAsync(null);

    public Task UnstageLinesAsync() => _diff.UnstageLinesCommand.ExecuteAsync(null);

    public Task DiscardLinesAsync() => _diff.DiscardLinesCommand.ExecuteAsync(null);

    public Task RunHunkActionAsync(int hunk, LineAction action) => _diff.RunHunkActionAsync(hunk, action);
}
