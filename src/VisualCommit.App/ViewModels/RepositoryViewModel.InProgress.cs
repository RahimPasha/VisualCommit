using System.Globalization;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// Work package WP9's part of the repository's view model (docs/phase-3-design.md section 5, 6.3):
/// the conflict banner and the working-changes row's text, which follow every status read
/// (<see cref="OnStatusApplied"/>); the Abort question; the left panel's selection, which follows
/// the graph's (<see cref="OnGraphSelectionChanged"/>, D98); <c>MERGE_MSG</c> for the stage
/// panel; and what auto-fetch needs to know of the repository.
/// </summary>
public sealed partial class RepositoryViewModel
{
    private ConflictBannerViewModel? _banner;
    private IInProgressActions? _inProgressActions;

    /// <summary>The banner above the graph or the diff (section 5).</summary>
    public ConflictBannerViewModel Banner => _banner ??= new ConflictBannerViewModel(AbortInProgressAsync);

    /// <summary>
    /// Where the banner's Abort goes after the question: <see cref="Operations"/> once it
    /// implements <see cref="IInProgressActions"/> (WP7), or what a test set.
    /// </summary>
    public IInProgressActions? InProgressActions
    {
        get => _inProgressActions ?? (object?)Operations as IInProgressActions;
        set => _inProgressActions = value;
    }

    /// <summary>How long ago opening the repository began: auto-fetch's first fetch comes one interval after it (6.1).</summary>
    internal TimeSpan TimeSinceOpening => _sinceOpening.Elapsed;

    /// <summary>
    /// <c>MERGE_MSG</c> as git left it in the repository's git folder (section 5), or null when
    /// there is none. The stage panel fills an empty message with it during a merge, cherry-pick
    /// or revert.
    /// </summary>
    public async Task<string?> ReadMergeMessageAsync()
    {
        var path = System.IO.Path.Combine(Repository.GitDirectory, "MERGE_MSG");
        try
        {
            return File.Exists(path) ? await File.ReadAllTextAsync(path, _lifetime.Token) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>What the working-changes row says (section 5): "Working changes · 2 conflicts" while files conflict.</summary>
    public static string WorkingRowTextFor(int conflicts) => conflicts switch
    {
        0 => "Working changes",
        1 => "Working changes · 1 conflict",
        _ => string.Create(CultureInfo.InvariantCulture, $"Working changes · {conflicts} conflicts"),
    };

    partial void OnStatusApplied(WorkingTreeStatus status)
    {
        var conflicts = status.Unstaged.Count(file => file.Kind == FileChangeKind.Conflicted);
        WorkingRowText = WorkingRowTextFor(conflicts);
        Banner.Show(status.InProgress, conflicts);
    }

    partial void OnGraphSelectionChanged(GraphSelection selection) => LeftPanel.SyncSelection(selection.Single);

    /// <summary>The banner's Abort: asks section 5's question, then hands the abort to <see cref="InProgressActions"/>.</summary>
    private async Task AbortInProgressAsync(InProgressState state)
    {
        try
        {
            if (!await AskAsync(ConflictBannerViewModel.AbortConfirmation(state, HasSequencer())))
            {
                return;
            }

            if (InProgressActions is not { } actions)
            {
                _log.Warning($"{Name}: the banner's Abort has nothing to run.");
                return;
            }

            await actions.AbortAsync(state);
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: aborting the {ConflictBannerViewModel.KindName(state.Kind)} failed.", ex);
        }
    }

    /// <summary>A cherry-pick or revert sequence started in a terminal is in progress (<c>sequencer/</c> exists, section 5).</summary>
    private bool HasSequencer()
    {
        try
        {
            return Directory.Exists(System.IO.Path.Combine(Repository.GitDirectory, "sequencer"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
